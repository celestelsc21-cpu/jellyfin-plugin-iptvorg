using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.IptvOrg.Catalog;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// Turns the iptv-org catalog into a filtered, de-duplicated, categorized channel list.
/// Pure logic without I/O, so it is fully unit-testable.
/// </summary>
internal static partial class PlaylistBuilder
{
    private const string GeoBlockedLabel = "Geo-blocked";
    private const string Not247Label = "Not 24/7";

    /// <summary>
    /// Builds the playlist entries without stream checks.
    /// </summary>
    /// <param name="catalog">Catalog snapshot.</param>
    /// <param name="options">Filter options.</param>
    /// <returns>Entries ordered by category and name, numbered from 1.</returns>
    public static IReadOnlyList<PlaylistEntry> Build(CatalogSnapshot catalog, ChannelFilterOptions options)
        => Materialize(Collect(catalog, options), catalog, options, deadUrls: null);

    /// <summary>
    /// Applies the filters and groups the remaining streams into future channels.
    /// </summary>
    /// <param name="catalog">Catalog snapshot.</param>
    /// <param name="options">Filter options.</param>
    /// <returns>One group per playlist channel, with its streams best first.</returns>
    public static IReadOnlyList<ChannelCandidates> Collect(CatalogSnapshot catalog, ChannelFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        var channels = new Dictionary<string, ApiChannel>(StringComparer.Ordinal);
        foreach (var channel in catalog.Channels)
        {
            if (!string.IsNullOrEmpty(channel.Id))
            {
                channels.TryAdd(channel.Id, channel);
            }
        }

        var feeds = new Dictionary<(string Channel, string Feed), ApiFeed>();
        var mainFeeds = new Dictionary<string, ApiFeed>(StringComparer.Ordinal);
        foreach (var feed in catalog.Feeds)
        {
            if (string.IsNullOrEmpty(feed.Channel) || string.IsNullOrEmpty(feed.Id))
            {
                continue;
            }

            feeds.TryAdd((feed.Channel, feed.Id), feed);
            if (feed.IsMain)
            {
                mainFeeds.TryAdd(feed.Channel, feed);
            }
        }

        var allowedCountries = ExpandCountries(catalog, options);
        var enabledCategories = options.Categories.Count == 0
            ? null
            : new HashSet<string>(options.Categories, StringComparer.OrdinalIgnoreCase);

        // Group key: channel (one entry per channel), channel@feed, or the URL for streams without a channel.
        var groups = new Dictionary<string, (ApiChannel? Channel, string Category, List<StreamCandidate> Streams)>(StringComparer.Ordinal);

        foreach (var stream in catalog.Streams)
        {
            if (!IsSupportedUrl(stream.Url))
            {
                continue;
            }

            ApiChannel? channel = null;
            if (!string.IsNullOrEmpty(stream.Channel))
            {
                channels.TryGetValue(stream.Channel, out channel);
            }

            if (channel is not null && !string.IsNullOrEmpty(channel.Closed))
            {
                continue;
            }

            var categories = channel?.Categories?.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray() ?? [];
            var isAdult = (channel?.IsNsfw ?? false)
                || categories.Contains(IptvOrgConstants.AdultCategory, StringComparer.OrdinalIgnoreCase);
            if (isAdult && !options.IncludeNsfw)
            {
                continue;
            }

            if (options.SkipStreamsWithCustomHeaders
                && (!string.IsNullOrWhiteSpace(stream.Referrer) || !string.IsNullOrWhiteSpace(stream.UserAgent)))
            {
                continue;
            }

            if (options.SkipGeoBlocked && HasLabel(stream, GeoBlockedLabel))
            {
                continue;
            }

            if (options.SkipNot247 && HasLabel(stream, Not247Label))
            {
                continue;
            }

            ApiFeed? mainFeed = null;
            if (channel is not null)
            {
                mainFeeds.TryGetValue(channel.Id, out mainFeed);
            }

            var feedId = string.IsNullOrEmpty(stream.Feed) ? mainFeed?.Id : stream.Feed;
            ApiFeed? feed = null;
            if (channel is not null && feedId is not null)
            {
                feeds.TryGetValue((channel.Id, feedId), out feed);
            }

            if (options.HideTimeshiftFeeds && IsTimeshift(feedId, feed?.Name))
            {
                continue;
            }

            if (options.Languages.Count > 0
                && !(feed?.Languages ?? []).Any(l => options.Languages.Contains(l)))
            {
                continue;
            }

            var country = channel?.Country;
            if (allowedCountries is not null
                && (string.IsNullOrEmpty(country) || !allowedCountries.Contains(country)))
            {
                continue;
            }

            var category = PickCategory(categories, enabledCategories);
            if (category is null)
            {
                continue;
            }

            string key;
            if (channel is null)
            {
                key = "url:" + stream.Url.Trim();
            }
            else if (options.OneStreamPerChannel)
            {
                key = channel.Id;
            }
            else
            {
                key = channel.Id + "@" + (feedId ?? string.Empty);
            }

            if (!groups.TryGetValue(key, out var group))
            {
                group = (channel, category, new List<StreamCandidate>());
                groups[key] = group;
            }

            var isMain = feedId is not null && string.Equals(feedId, mainFeed?.Id, StringComparison.Ordinal);
            group.Streams.Add(new StreamCandidate(stream, feedId, isMain));
        }

        return groups.Values
            .Select(g => new ChannelCandidates(
                g.Channel,
                g.Category,
                g.Streams
                    .GroupBy(c => c.Url, StringComparer.Ordinal)
                    .Select(d => d.First())
                    .OrderBy(c => c.Stream.Labels?.Count ?? 0)
                    .ThenByDescending(c => ParseQuality(c.Stream.Quality))
                    .ThenByDescending(c => c.IsMainFeed)
                    .ThenBy(c => c.Url, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }

    /// <summary>
    /// Picks the first working stream of every group and produces the numbered playlist.
    /// </summary>
    /// <param name="groups">Groups from <see cref="Collect"/>.</param>
    /// <param name="catalog">Catalog snapshot (categories and logos).</param>
    /// <param name="options">Filter options.</param>
    /// <param name="deadUrls">URLs known not to work, or <c>null</c> when streams were not checked.</param>
    /// <returns>Entries ordered by category and name, numbered from 1.</returns>
    public static IReadOnlyList<PlaylistEntry> Materialize(
        IReadOnlyList<ChannelCandidates> groups,
        CatalogSnapshot catalog,
        ChannelFilterOptions options,
        IReadOnlySet<string>? deadUrls)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        var categoryRank = BuildCategoryRank(catalog);
        var categoryApiNames = catalog.Categories
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
        var logos = BuildLogoIndex(catalog.Logos);

        var chosen = new List<(ChannelCandidates Group, StreamCandidate Stream, string Name, int Rank)>(groups.Count);
        foreach (var group in groups)
        {
            var stream = group.Streams.FirstOrDefault(s => deadUrls is null || !deadUrls.Contains(s.Url));
            if (stream is null)
            {
                continue;
            }

            var rank = string.Equals(group.Category, IptvOrgConstants.UndefinedCategory, StringComparison.OrdinalIgnoreCase)
                ? int.MaxValue
                : categoryRank.GetValueOrDefault(group.Category, int.MaxValue - 1);
            chosen.Add((group, stream, GetName(group.Channel, stream.Stream), rank));
        }

        var nameComparer = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
        var ordered = chosen
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Name, nameComparer)
            .ThenBy(x => x.Stream.Url, StringComparer.Ordinal)
            .ToList();

        var result = new List<PlaylistEntry>(ordered.Count);
        var number = 1;
        foreach (var item in ordered)
        {
            var channel = item.Group.Channel;
            var feedId = item.Stream.FeedId;
            var categoryName = CategoryNames.Get(item.Group.Category, categoryApiNames.GetValueOrDefault(item.Group.Category));
            var name = options.PrefixCategoryInName ? $"[{categoryName}] {item.Name}" : item.Name;
            var tvgId = channel is null
                ? null
                : (string.IsNullOrEmpty(feedId) ? channel.Id : channel.Id + "@" + feedId);

            result.Add(new PlaylistEntry(
                tvgId,
                channel?.Id,
                feedId,
                name,
                item.Stream.Url,
                channel is null ? null : FindLogo(logos, channel.Id, feedId),
                item.Group.Category,
                categoryName,
                number++));
        }

        return result;
    }

    /// <summary>
    /// Detects time-shifted feeds such as <c>Plus2</c> / <c>+2</c> or <c>HDPlus4</c> / <c>HD +4</c>.
    /// </summary>
    /// <param name="feedId">Feed id.</param>
    /// <param name="feedName">Feed name.</param>
    /// <returns><c>true</c> for a time-shifted feed.</returns>
    internal static bool IsTimeshift(string? feedId, string? feedName)
    {
        return (feedId is not null && TimeshiftIdRegex().IsMatch(feedId))
            || (feedName is not null && TimeshiftNameRegex().IsMatch(feedName));
    }

    /// <summary>
    /// Counts channels per category (before category filtering), for the settings page.
    /// </summary>
    /// <param name="catalog">Catalog snapshot.</param>
    /// <param name="includeNsfw">Whether adult channels count.</param>
    /// <returns>Channel count per category id.</returns>
    public static IReadOnlyDictionary<string, int> CountByCategory(CatalogSnapshot catalog, bool includeNsfw)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var withStreams = new HashSet<string>(
            catalog.Streams.Where(s => !string.IsNullOrEmpty(s.Channel)).Select(s => s.Channel!),
            StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var channel in catalog.Channels)
        {
            if (!withStreams.Contains(channel.Id) || !string.IsNullOrEmpty(channel.Closed))
            {
                continue;
            }

            var categories = channel.Categories?.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray() ?? [];
            if (!includeNsfw && (channel.IsNsfw || categories.Contains(IptvOrgConstants.AdultCategory, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (categories.Length == 0)
            {
                categories = [IptvOrgConstants.UndefinedCategory];
            }

            foreach (var category in categories)
            {
                counts[category] = counts.GetValueOrDefault(category) + 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Picks the primary category: the channel's first category that is enabled.
    /// </summary>
    /// <param name="categories">Channel categories (may be empty).</param>
    /// <param name="enabled">Enabled categories, or <c>null</c> for all.</param>
    /// <returns>The category id, or <c>null</c> when the channel is filtered out.</returns>
    internal static string? PickCategory(IReadOnlyList<string> categories, HashSet<string>? enabled)
    {
        if (categories.Count == 0)
        {
            return enabled is null || enabled.Contains(IptvOrgConstants.UndefinedCategory)
                ? IptvOrgConstants.UndefinedCategory
                : null;
        }

        if (enabled is null)
        {
            return categories[0].ToLowerInvariant();
        }

        foreach (var category in categories)
        {
            if (enabled.Contains(category))
            {
                return category.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a quality string such as <c>1080p</c> into its line count.
    /// </summary>
    /// <param name="quality">Quality string.</param>
    /// <returns>The number of lines, or 0 when unknown.</returns>
    internal static int ParseQuality(string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality))
        {
            return 0;
        }

        var digits = new string(quality.TakeWhile(char.IsAsciiDigit).ToArray());
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static HashSet<string>? ExpandCountries(CatalogSnapshot catalog, ChannelFilterOptions options)
    {
        if (options.Countries.Count == 0 && options.Regions.Count == 0)
        {
            return null;
        }

        var result = new HashSet<string>(options.Countries, StringComparer.OrdinalIgnoreCase);
        foreach (var region in catalog.Regions)
        {
            if (options.Regions.Contains(region.Code))
            {
                result.UnionWith(region.Countries ?? []);
            }
        }

        return result;
    }

    private static Dictionary<string, int> BuildCategoryRank(CatalogSnapshot catalog)
    {
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        foreach (var category in catalog.Categories)
        {
            if (!string.IsNullOrEmpty(category.Id))
            {
                rank.TryAdd(category.Id, i++);
            }
        }

        return rank;
    }

    private static bool IsSupportedUrl(string? url)
    {
        // Same schemes Jellyfin's M3U parser accepts; anything else would be dropped there.
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps
                || string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, "rtp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, "udp", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasLabel(ApiStream stream, string label)
        => stream.Labels?.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase)) ?? false;

    private static string GetName(ApiChannel? channel, ApiStream stream)
    {
        if (!string.IsNullOrWhiteSpace(stream.Title))
        {
            return stream.Title.Trim();
        }

        var channelName = channel?.Name;
        if (!string.IsNullOrWhiteSpace(channelName))
        {
            return channelName.Trim();
        }

        return channel?.Id ?? stream.Url.Trim();
    }

    private static Dictionary<string, List<ApiLogo>> BuildLogoIndex(IReadOnlyList<ApiLogo> logos)
    {
        var index = new Dictionary<string, List<ApiLogo>>(StringComparer.Ordinal);
        foreach (var logo in logos)
        {
            if (string.IsNullOrEmpty(logo.Channel) || string.IsNullOrWhiteSpace(logo.Url))
            {
                continue;
            }

            if (!index.TryGetValue(logo.Channel, out var list))
            {
                list = [];
                index[logo.Channel] = list;
            }

            list.Add(logo);
        }

        return index;
    }

    private static string? FindLogo(Dictionary<string, List<ApiLogo>> index, string channelId, string? feedId)
    {
        if (!index.TryGetValue(channelId, out var logos))
        {
            return null;
        }

        // Prefer: the feed's own logo, then a channel-wide logo, then any; within that,
        // logos in use and raster formats (SVG is not rendered by every Jellyfin client).
        return logos
            .OrderByDescending(l => feedId is not null && string.Equals(l.Feed, feedId, StringComparison.Ordinal))
            .ThenByDescending(l => string.IsNullOrEmpty(l.Feed))
            .ThenByDescending(l => l.InUse)
            .ThenByDescending(l => !string.Equals(l.Format, "SVG", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Url.Trim())
            .FirstOrDefault();
    }

    [GeneratedRegex(@"(Plus|Minus)\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex TimeshiftIdRegex();

    [GeneratedRegex(@"(^|\s)[+\-\u2212]\d{1,2}(\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex TimeshiftNameRegex();
}
