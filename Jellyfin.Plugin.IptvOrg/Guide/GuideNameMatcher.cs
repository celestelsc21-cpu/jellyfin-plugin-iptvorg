using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.IptvOrg.Catalog;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Playlist;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Links playlist channels to guide channels that iptv-org lists for a site but has not linked
/// to any channel (entries of <c>guides.json</c> without <c>channel</c>), by comparing names.
/// Used only for the sites whose whole XMLTV file the plugin reads anyway (see
/// <see cref="SiteGuideFiles"/>), and deliberately strict: a link is made only when
/// <list type="bullet">
/// <item>the channel is in the site's area (broadcast language or country);</item>
/// <item>the name key points to exactly one unlinked guide channel of the site, which iptv-org
/// has not linked to another channel either;</item>
/// <item>the same key belongs to exactly one channel of the whole catalog in that area;</item>
/// <item>neither side is a time-shifted copy.</item>
/// </list>
/// </summary>
internal static class GuideNameMatcher
{
    // Alternative names give short ambiguous keys ("NTV", "ART"); only the main name may be that short.
    private const int MinAltNameKeyLength = 4;
    private const int MinKeyLength = 2;

    /// <summary>
    /// Finds guide channels for the given channel feeds by name.
    /// </summary>
    /// <param name="catalog">Catalog (channels, feeds and guides are used).</param>
    /// <param name="wanted">Channel and feed ids of the playlist candidates (feed may be <c>null</c>).</param>
    /// <returns>Guide entries (channel, feed, site, site id) for the matched feeds.</returns>
    public static IReadOnlyList<ApiGuide> Match(
        CatalogSnapshot catalog,
        IReadOnlyCollection<(string Channel, string? Feed)> wanted)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(wanted);

        // Unlinked guide channels per site: key → site ids.
        var linked = new HashSet<(string Site, string SiteId)>();
        foreach (var guide in catalog.Guides)
        {
            if (!string.IsNullOrEmpty(guide.Channel) && guide.Site is not null && guide.SiteId is not null)
            {
                linked.Add((guide.Site.ToLowerInvariant(), guide.SiteId));
            }
        }

        var siteKeys = new Dictionary<string, (NameMatchRule Rule, Dictionary<string, HashSet<string>> Keys)>(StringComparer.OrdinalIgnoreCase);
        foreach (var guide in catalog.Guides)
        {
            if (!string.IsNullOrEmpty(guide.Channel)
                || string.IsNullOrEmpty(guide.SiteId)
                || !SiteGuideFiles.TryGetNameMatchRule(guide.Site, out var rule)
                || linked.Contains((guide.Site!.ToLowerInvariant(), guide.SiteId))
                || ChannelNameKey.IsTimeshiftName(guide.SiteName))
            {
                continue;
            }

            var key = ChannelNameKey.Normalize(guide.SiteName);
            if (key.Length < MinKeyLength)
            {
                continue;
            }

            var siteName = guide.Site!;
            if (!siteKeys.TryGetValue(siteName, out var site))
            {
                site = (rule, new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));
                siteKeys[siteName] = site;
            }

            if (!site.Keys.TryGetValue(key, out var ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                site.Keys[key] = ids;
            }

            ids.Add(guide.SiteId);
        }

        if (siteKeys.Count == 0 || wanted.Count == 0)
        {
            return [];
        }

        var channels = new Dictionary<string, ApiChannel>(StringComparer.Ordinal);
        foreach (var channel in catalog.Channels)
        {
            channels.TryAdd(channel.Id, channel);
        }

        var feedsByChannel = catalog.Feeds
            .Where(f => !string.IsNullOrEmpty(f.Channel))
            .GroupBy(f => f.Channel, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);

        var result = new List<ApiGuide>();
        foreach (var (siteName, site) in siteKeys)
        {
            // Which catalog channels own each key within the site's area (uniqueness check).
            var keyOwners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var channel in channels.Values)
            {
                feedsByChannel.TryGetValue(channel.Id, out var channelFeeds);
                if (channelFeeds is null || channelFeeds.Length == 0)
                {
                    AddOwner(keyOwners, channel, null, site.Rule);
                    continue;
                }

                foreach (var feed in channelFeeds)
                {
                    AddOwner(keyOwners, channel, feed, site.Rule);
                }
            }

            foreach (var (channelId, feedId) in wanted)
            {
                if (!channels.TryGetValue(channelId, out var channel))
                {
                    continue;
                }

                ApiFeed? feed = null;
                if (feedId is not null && feedsByChannel.TryGetValue(channelId, out var channelFeeds))
                {
                    feed = Array.Find(channelFeeds, f => string.Equals(f.Id, feedId, StringComparison.Ordinal));
                }

                if (!InArea(channel, feed, site.Rule) || (feed is not null && PlaylistBuilder.IsTimeshift(feed.Id, feed.Name)))
                {
                    continue;
                }

                foreach (var key in Keys(channel, feed))
                {
                    if (site.Keys.TryGetValue(key, out var ids) && ids.Count == 1
                        && keyOwners.TryGetValue(key, out var owners) && owners.Count == 1)
                    {
                        result.Add(new ApiGuide
                        {
                            Channel = channelId,
                            Feed = feedId,
                            Site = siteName,
                            SiteId = ids.First()
                        });
                        break;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the name keys of a channel feed, main name first.
    /// </summary>
    /// <param name="channel">Channel.</param>
    /// <param name="feed">Feed, if known.</param>
    /// <returns>Distinct keys.</returns>
    internal static IReadOnlyList<string> Keys(ApiChannel channel, ApiFeed? feed)
    {
        // A regional feed ("Gomel") is a different programme: its name is part of the key.
        // Quality feeds (HD/SD) and the main feed carry the channel's programme.
        var generic = feed is null
            || feed.IsMain
            || IsQualityFeed(feed.Id)
            || IsQualityFeed(feed.Name);
        var suffix = generic ? string.Empty : " " + (string.IsNullOrEmpty(feed!.Name) ? feed.Id : feed.Name);

        var keys = new List<string>();
        var names = new List<string> { channel.Name };
        names.AddRange(channel.AltNames ?? []);
        for (var i = 0; i < names.Count; i++)
        {
            var key = ChannelNameKey.Normalize(names[i] + suffix);
            if (key.Length < (i == 0 ? MinKeyLength : MinAltNameKeyLength) || keys.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }

            keys.Add(key);
        }

        return keys;
    }

    private static void AddOwner(Dictionary<string, HashSet<string>> owners, ApiChannel channel, ApiFeed? feed, NameMatchRule rule)
    {
        if (!InArea(channel, feed, rule) || (feed is not null && PlaylistBuilder.IsTimeshift(feed.Id, feed.Name)))
        {
            return;
        }

        foreach (var key in Keys(channel, feed))
        {
            if (!owners.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                owners[key] = set;
            }

            set.Add(channel.Id);
        }
    }

    private static bool InArea(ApiChannel channel, ApiFeed? feed, NameMatchRule rule)
        => (feed?.Languages ?? []).Contains(rule.Language, StringComparer.Ordinal)
            || (channel.Country is not null && rule.Countries.Contains(channel.Country));

    private static bool IsQualityFeed(string? id)
        => string.Equals(id, "HD", StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, "SD", StringComparison.OrdinalIgnoreCase);
}
