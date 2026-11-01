using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.IptvOrg.Catalog;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Localization;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// Builds a short channel description from iptv-org metadata: country, regions,
/// broadcast languages and categories.
/// </summary>
internal static class ChannelDescriptionBuilder
{
    // Regions that say nothing about a channel (every country is in them).
    private static readonly HashSet<string> SkippedRegions = new(StringComparer.OrdinalIgnoreCase) { "WW", "UN", "EMEA" };

    /// <summary>
    /// Builds descriptions for the channels of the playlist groups.
    /// </summary>
    /// <param name="catalog">Full catalog snapshot (feeds, regions, names).</param>
    /// <param name="groups">Playlist groups.</param>
    /// <param name="russian">Whether the description is written in Russian (otherwise English).</param>
    /// <returns>Descriptions by iptv-org channel id.</returns>
    public static IReadOnlyDictionary<string, string> BuildAll(
        CatalogSnapshot catalog,
        IReadOnlyList<ChannelCandidates> groups,
        bool russian)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(groups);

        var feedLanguages = new Dictionary<(string Channel, string Feed), IReadOnlyList<string>>();
        foreach (var feed in catalog.Feeds)
        {
            if (!string.IsNullOrEmpty(feed.Channel) && !string.IsNullOrEmpty(feed.Id))
            {
                feedLanguages.TryAdd((feed.Channel, feed.Id), feed.Languages ?? []);
            }
        }

        var countryNames = catalog.Countries
            .Where(c => !string.IsNullOrEmpty(c.Code))
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
        var languageNames = catalog.Languages
            .Where(l => !string.IsNullOrEmpty(l.Code))
            .GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
        var categoryNames = catalog.Categories
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var channel = group.Channel;
            if (channel is null || result.ContainsKey(channel.Id))
            {
                continue;
            }

            var channelId = channel.Id;

            var languages = group.Streams
                .Where(s => s.FeedId is not null)
                .SelectMany(s => feedLanguages.TryGetValue((channelId, s.FeedId!), out var l) ? l : [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var country = channel.Country ?? string.Empty;
            var regions = country.Length == 0
                ? []
                : catalog.Regions
                    .Where(r => !string.IsNullOrEmpty(r.Code)
                        && !SkippedRegions.Contains(r.Code)
                        && (r.Countries?.Contains(country, StringComparer.OrdinalIgnoreCase) ?? false))
                    .ToArray();

            var description = Describe(channel, languages, regions, russian, countryNames, languageNames, categoryNames);
            if (description.Length > 0)
            {
                result[channel.Id] = description;
            }
        }

        return result;
    }

    /// <summary>
    /// Formats one description.
    /// </summary>
    /// <param name="channel">Channel.</param>
    /// <param name="languages">Broadcast language codes.</param>
    /// <param name="regions">Regions that contain the channel's country.</param>
    /// <param name="russian">Whether to write Russian.</param>
    /// <param name="countryNames">English country names by code.</param>
    /// <param name="languageNames">English language names by code.</param>
    /// <param name="categoryNames">English category names by id.</param>
    /// <returns>The description (lines separated by new lines), or an empty string.</returns>
    internal static string Describe(
        ApiChannel channel,
        IReadOnlyList<string> languages,
        IReadOnlyList<ApiRegion> regions,
        bool russian,
        IReadOnlyDictionary<string, string> countryNames,
        IReadOnlyDictionary<string, string> languageNames,
        IReadOnlyDictionary<string, string> categoryNames)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(channel.Country))
        {
            var english = countryNames.GetValueOrDefault(channel.Country) ?? channel.Country;
            var name = russian ? RussianNames.Country(channel.Country) ?? english : english;
            AppendLine(sb, russian ? "Страна" : "Country", name);
        }

        if (regions.Count > 0)
        {
            var names = regions.Select(r => russian ? RussianNames.Region(r.Code) ?? r.Name : r.Name);
            AppendLine(sb, russian ? "Регионы" : "Regions", string.Join(", ", names));
        }

        if (languages.Count > 0)
        {
            var names = languages.Select(l =>
            {
                var english = languageNames.GetValueOrDefault(l) ?? l;
                return russian ? RussianNames.Language(l) ?? english : english;
            });
            var label = russian
                ? (languages.Count == 1 ? "Язык" : "Языки")
                : (languages.Count == 1 ? "Language" : "Languages");
            AppendLine(sb, label, string.Join(", ", names));
        }

        var categories = channel.Categories?.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray() ?? [];
        if (categories.Length > 0)
        {
            var names = categories.Select(c =>
            {
                var english = categoryNames.GetValueOrDefault(c) ?? c;
                return russian ? CategoryNames.Get(c, english) : english;
            });
            var label = russian
                ? (categories.Length == 1 ? "Категория" : "Категории")
                : (categories.Length == 1 ? "Category" : "Categories");
            AppendLine(sb, label, string.Join(", ", names));
        }

        return sb.ToString().TrimEnd('\n');
    }

    private static void AppendLine(StringBuilder sb, string label, string value)
    {
        sb.Append(label).Append(": ").Append(value).Append('\n');
    }
}
