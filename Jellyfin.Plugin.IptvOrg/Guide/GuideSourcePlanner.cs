using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Playlist;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Decides which hosted guide files to download for the playlist channels.
/// Pure logic without I/O.
/// </summary>
internal static class GuideSourcePlanner
{
    /// <summary>
    /// Plans the guide sources.
    /// </summary>
    /// <param name="entries">Playlist entries.</param>
    /// <param name="guides">Guide mappings from <c>guides.json</c>.</param>
    /// <param name="maxSources">Maximum number of files to download.</param>
    /// <returns>Sources ordered by how many playlist channels they cover (most first).</returns>
    public static IReadOnlyList<GuideSource> Plan(
        IReadOnlyList<PlaylistEntry> entries,
        IReadOnlyList<ApiGuide> guides,
        int maxSources)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(guides);

        // Playlist channels by iptv-org channel id.
        var byChannel = new Dictionary<string, List<PlaylistEntry>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.ChannelId is null || entry.TvgId is null)
            {
                continue;
            }

            if (!byChannel.TryGetValue(entry.ChannelId, out var list))
            {
                list = [];
                byChannel[entry.ChannelId] = list;
            }

            list.Add(entry);
        }

        // url → (guide channel id → playlist tvg-ids)
        var plans = new Dictionary<string, (Uri Url, bool IsGzip, Dictionary<string, HashSet<string>> Map)>(StringComparer.Ordinal);

        var covered = new HashSet<string>(StringComparer.Ordinal);

        // Pass 1: guides for the same feed, or channel-wide guides.
        foreach (var guide in guides)
        {
            if (string.IsNullOrEmpty(guide.Channel)
                || !byChannel.TryGetValue(guide.Channel, out var channelEntries))
            {
                continue;
            }

            var targets = channelEntries
                .Where(e => guide.Feed is null || e.FeedId is null || string.Equals(e.FeedId, guide.Feed, StringComparison.Ordinal))
                .Select(e => e.TvgId!)
                .ToArray();
            if (targets.Length > 0 && AddGuide(plans, guide, targets))
            {
                covered.UnionWith(targets);
            }
        }

        // Pass 2: a channel variant without its own guide (for example "HD" when only "SD" is
        // listed) borrows the guide of another variant of the same channel: the programme is the
        // same. Time-shifted variants (+2, HD +4) never lend or borrow, their times differ.
        foreach (var guide in guides)
        {
            if (string.IsNullOrEmpty(guide.Channel)
                || guide.Feed is null
                || PlaylistBuilder.IsTimeshift(guide.Feed, null)
                || !byChannel.TryGetValue(guide.Channel, out var channelEntries))
            {
                continue;
            }

            var targets = channelEntries
                .Where(e => e.FeedId is not null
                    && !string.Equals(e.FeedId, guide.Feed, StringComparison.Ordinal)
                    && !PlaylistBuilder.IsTimeshift(e.FeedId, null)
                    && !covered.Contains(e.TvgId!))
                .Select(e => e.TvgId!)
                .ToArray();
            if (targets.Length > 0 && AddGuide(plans, guide, targets))
            {
                covered.UnionWith(targets);
            }
        }

        return plans.Values
            .Select(p => new GuideSource(
                p.Url,
                p.IsGzip,
                p.Map.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<string>)kv.Value.OrderBy(v => v, StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal)))
            .OrderByDescending(s => s.Coverage)
            .ThenBy(s => s.Url.AbsoluteUri, StringComparer.Ordinal)
            .Take(Math.Max(0, maxSources))
            .ToArray();
    }

    /// <summary>
    /// Adds every usable file of one guide entry to the plan.
    /// </summary>
    /// <param name="plans">Plans by file URL.</param>
    /// <param name="guide">Guide entry.</param>
    /// <param name="targets">Playlist <c>tvg-id</c> values the entry fills.</param>
    /// <returns><c>true</c> when at least one file was usable.</returns>
    private static bool AddGuide(
        Dictionary<string, (Uri Url, bool IsGzip, Dictionary<string, HashSet<string>> Map)> plans,
        ApiGuide guide,
        IReadOnlyList<string> targets)
    {
        var added = false;

        // Guide files may label the channel with or without the feed suffix.
        string[] sourceIds = guide.Feed is null
            ? [guide.Channel!]
            : [guide.Channel + "@" + guide.Feed, guide.Channel!];

        // 1) Hosted files produced by iptv-org grabber servers: channels are labelled with iptv-org ids.
        foreach (var source in guide.Sources ?? [])
        {
            if (TryGetUrl(source, out var url, out var isGzip))
            {
                AddTargets(plans, url, isGzip, sourceIds, targets);
                added = true;
            }
        }

        // 2) Sites whose own XMLTV file the plugin reads directly: channels are labelled with site_id.
        if (!string.IsNullOrEmpty(guide.SiteId) && SiteGuideFiles.TryGet(guide.Site, out var siteUrl) && siteUrl is not null)
        {
            AddTargets(plans, siteUrl, siteUrl.AbsolutePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase), [guide.SiteId], targets);
            added = true;
        }

        return added;
    }

    private static void AddTargets(
        Dictionary<string, (Uri Url, bool IsGzip, Dictionary<string, HashSet<string>> Map)> plans,
        Uri url,
        bool isGzip,
        IReadOnlyList<string> sourceIds,
        IReadOnlyList<string> targets)
    {
        var key = url.AbsoluteUri;
        if (!plans.TryGetValue(key, out var plan))
        {
            plan = (url, isGzip, new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));
            plans[key] = plan;
        }

        foreach (var id in sourceIds)
        {
            if (!plan.Map.TryGetValue(id, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                plan.Map[id] = set;
            }

            set.UnionWith(targets);
        }
    }

    /// <summary>
    /// Validates a guide URL: only http(s), XML or gzip, and never local or private-network hosts.
    /// </summary>
    /// <param name="source">Source.</param>
    /// <param name="url">The parsed URL.</param>
    /// <param name="isGzip">Whether the file is gzip-compressed.</param>
    /// <returns><c>true</c> when the source can be used.</returns>
    internal static bool TryGetUrl(ApiGuideSource source, out Uri url, out bool isGzip)
    {
        url = null!;
        isGzip = false;

        if (string.IsNullOrWhiteSpace(source.Url)
            || !Uri.TryCreate(source.Url.Trim(), UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || IsLocalHost(parsed))
        {
            return false;
        }

        var format = source.Format?.Trim() ?? string.Empty;
        var path = parsed.AbsolutePath;
        isGzip = string.Equals(format, "GZIP", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        var isXml = string.Equals(format, "XML", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

        // JSON guides are not XMLTV.
        if (!isGzip && !isXml)
        {
            return false;
        }

        url = parsed;
        return true;
    }

    /// <summary>
    /// Rejects loopback, link-local and private-network targets (SSRF protection:
    /// the guide list comes from a third party).
    /// </summary>
    /// <param name="url">URL.</param>
    /// <returns><c>true</c> when the host is local or private.</returns>
    internal static bool IsLocalHost(Uri url)
    {
        if (url.IsLoopback
            || string.Equals(url.IdnHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || url.IdnHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(url.DnsSafeHost, out var address))
        {
            return false;
        }

        return IsPrivateAddress(address);
    }

    /// <summary>
    /// Checks whether an address is loopback, link-local, private or otherwise non-public.
    /// </summary>
    /// <param name="address">Address.</param>
    /// <returns><c>true</c> for non-public addresses.</returns>
    internal static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;
        }

        var b = address.GetAddressBytes();
        return b[0] == 10
            || b[0] == 127
            || b[0] == 0
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
    }
}
