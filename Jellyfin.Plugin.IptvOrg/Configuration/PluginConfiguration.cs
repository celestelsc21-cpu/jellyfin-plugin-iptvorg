using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.IptvOrg.Configuration;

/// <summary>
/// Plugin configuration. Filter semantics: values inside one filter are combined with OR,
/// different filters with AND; an empty filter means "no restriction".
/// </summary>
#pragma warning disable CA1819 // Properties should not return arrays (XML-serialized configuration).
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Current configuration schema version.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Upper bound for <see cref="MaxGuideSources"/>.
    /// </summary>
    public const int MaxGuideSourcesLimit = 50;

    private const int MaxFilterValues = 500;
    private const int MaxCodeLength = 64;

    /// <summary>
    /// Gets or sets the configuration schema version (used for future migrations).
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets or sets the enabled iptv-org category ids (for example <c>news</c>).
    /// <c>undefined</c> selects channels without a category. Empty means all categories.
    /// </summary>
    public string[] Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the ISO 639-3 language codes (for example <c>rus</c>). Empty means all.
    /// Not exposed in the settings page yet.
    /// </summary>
    public string[] Languages { get; set; } = [];

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country codes (for example <c>RU</c>). Empty means all.
    /// Countries and regions form one geographic filter. Not exposed in the settings page yet.
    /// </summary>
    public string[] Countries { get; set; } = [];

    /// <summary>
    /// Gets or sets the iptv-org region codes (for example <c>CIS</c>). Empty means all.
    /// Not exposed in the settings page yet.
    /// </summary>
    public string[] Regions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether adult (NSFW) channels are included.
    /// </summary>
    public bool IncludeNsfw { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether streams that need a custom Referer or
    /// User-Agent are skipped. Jellyfin's M3U tuner cannot send per-channel headers.
    /// </summary>
    public bool SkipStreamsWithCustomHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether streams labelled "Geo-blocked" are skipped.
    /// </summary>
    public bool SkipGeoBlocked { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether streams labelled "Not 24/7" are skipped.
    /// </summary>
    public bool SkipNot247 { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the category is prepended to channel names,
    /// for example <c>[Новости] Euronews</c>.
    /// </summary>
    public bool PrefixCategoryInName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin builds the program guide
    /// from the hosted guide files listed by iptv-org.
    /// </summary>
    public bool EnableGuide { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of guide files downloaded per guide update
    /// (protects slow servers and networks).
    /// </summary>
    public int MaxGuideSources { get; set; } = 10;

    /// <summary>
    /// Cleans up values coming from the settings page: trims, normalizes case,
    /// removes duplicates and caps sizes.
    /// </summary>
    public void Normalize()
    {
        Categories = Clean(Categories, toUpper: false);
        Languages = Clean(Languages, toUpper: false);
        Countries = Clean(Countries, toUpper: true);
        Regions = Clean(Regions, toUpper: true);

        MaxGuideSources = Math.Clamp(MaxGuideSources, 1, MaxGuideSourcesLimit);

        if (SchemaVersion < CurrentSchemaVersion)
        {
            SchemaVersion = CurrentSchemaVersion;
        }
    }

    private static string[] Clean(IEnumerable<string>? values, bool toUpper)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Where(v => v.Length <= MaxCodeLength)
            .Select(v => toUpper ? v.ToUpperInvariant() : v.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxFilterValues)
            .ToArray();
    }
}
#pragma warning restore CA1819
