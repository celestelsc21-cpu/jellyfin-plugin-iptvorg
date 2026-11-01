using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.IptvOrg.Catalog;
using Jellyfin.Plugin.IptvOrg.Service;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// Counts channels per country, language and region for the settings page.
/// </summary>
internal static class FilterCatalogBuilder
{
    /// <summary>
    /// Builds the filter values.
    /// </summary>
    /// <param name="catalog">Catalog snapshot.</param>
    /// <param name="includeNsfw">Whether adult channels count.</param>
    /// <returns>The filter values with channel counts.</returns>
    public static FilterCatalog Build(CatalogSnapshot catalog, bool includeNsfw)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var channels = catalog.Channels
            .Where(c => !string.IsNullOrEmpty(c.Id) && string.IsNullOrEmpty(c.Closed))
            .Where(c => includeNsfw || !(c.IsNsfw || (c.Categories?.Contains(IptvOrgConstants.AdultCategory, StringComparer.OrdinalIgnoreCase) ?? false)))
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var mainFeeds = new Dictionary<string, string>(StringComparer.Ordinal);
        var feedLanguages = new Dictionary<(string Channel, string Feed), IReadOnlyList<string>>();
        foreach (var feed in catalog.Feeds)
        {
            if (string.IsNullOrEmpty(feed.Channel) || string.IsNullOrEmpty(feed.Id))
            {
                continue;
            }

            feedLanguages.TryAdd((feed.Channel, feed.Id), feed.Languages ?? []);
            if (feed.IsMain)
            {
                mainFeeds.TryAdd(feed.Channel, feed.Id);
            }
        }

        var countryChannels = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var languageChannels = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var stream in catalog.Streams)
        {
            if (string.IsNullOrEmpty(stream.Channel) || !channels.TryGetValue(stream.Channel, out var channel))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(channel.Country))
            {
                Add(countryChannels, channel.Country, channel.Id);
            }

            var feedId = string.IsNullOrEmpty(stream.Feed) ? mainFeeds.GetValueOrDefault(channel.Id) : stream.Feed;
            if (feedId is not null && feedLanguages.TryGetValue((channel.Id, feedId), out var languages))
            {
                foreach (var language in languages)
                {
                    Add(languageChannels, language, channel.Id);
                }
            }
        }

        var countryNames = catalog.Countries
            .Where(c => !string.IsNullOrEmpty(c.Code))
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var languageNames = catalog.Languages
            .Where(l => !string.IsNullOrEmpty(l.Code))
            .GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        var countries = countryChannels
            .Select(kv =>
            {
                countryNames.TryGetValue(kv.Key, out var country);
                return new FilterOption(kv.Key.ToUpperInvariant(), country?.Name ?? kv.Key, country?.Flag, kv.Value.Count);
            })
            .OrderByDescending(o => o.ChannelCount)
            .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var languagesResult = languageChannels
            .Select(kv => new FilterOption(kv.Key.ToLowerInvariant(), languageNames.GetValueOrDefault(kv.Key) ?? kv.Key, null, kv.Value.Count))
            .OrderByDescending(o => o.ChannelCount)
            .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var regions = catalog.Regions
            .Where(r => !string.IsNullOrEmpty(r.Code))
            .Select(r => new FilterOption(
                r.Code.ToUpperInvariant(),
                string.IsNullOrEmpty(r.Name) ? r.Code : r.Name,
                null,
                (r.Countries ?? []).Sum(c => countryChannels.TryGetValue(c, out var set) ? set.Count : 0)))
            .Where(o => o.ChannelCount > 0)
            .OrderByDescending(o => o.ChannelCount)
            .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FilterCatalog
        {
            Countries = countries,
            Languages = languagesResult,
            Regions = regions
        };
    }

    private static void Add(Dictionary<string, HashSet<string>> map, string key, string channelId)
    {
        if (!map.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            map[key] = set;
        }

        set.Add(channelId);
    }
}
