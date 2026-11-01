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

    /// <summary>
    /// Upper bound for <see cref="StreamCheckConcurrency"/>.
    /// </summary>
    public const int MaxStreamCheckConcurrency = 16;

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
    /// </summary>
    public string[] Languages { get; set; } = [];

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country codes (for example <c>RU</c>). Empty means all.
    /// Countries and regions form one geographic filter (a channel matches either).
    /// </summary>
    public string[] Countries { get; set; } = [];

    /// <summary>
    /// Gets or sets the iptv-org region codes (for example <c>CIS</c>). Empty means all.
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
    /// Gets or sets a value indicating whether time-shifted versions of a channel
    /// (feeds such as <c>+2</c> or <c>HD +4</c>) are left out.
    /// </summary>
    public bool HideTimeshiftFeeds { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a channel appears once even when it has
    /// several feeds (HD/SD, regional): the best working stream is used.
    /// When off, every feed is a separate channel.
    /// </summary>
    public bool OneStreamPerChannel { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether streams are probed before they enter the
    /// playlist; dead streams are replaced by another stream of the same channel or dropped.
    /// </summary>
    public bool CheckStreams { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of simultaneous stream checks.
    /// </summary>
    public int StreamCheckConcurrency { get; set; } = 4;

    /// <summary>
    /// Gets or sets the stream check timeout in seconds.
    /// </summary>
    public int StreamCheckTimeoutSeconds { get; set; } = 8;

    /// <summary>
    /// Gets or sets a value indicating whether the plugin builds the program guide
    /// from the hosted guide files listed by iptv-org.
    /// </summary>
    public bool EnableGuide { get; set; } = true;

    /// <summary>
    /// Gets or sets the preferred languages, most preferred first, as ISO 639-1 codes
    /// (for example <c>ru</c>, <c>en</c>). Used for channel names and programme titles and
    /// descriptions taken from the guide, and for the language of channel descriptions.
    /// </summary>
    public string[] PreferredLanguages { get; set; } = ["ru"];

    /// <summary>
    /// Gets or sets a value indicating whether channel names are replaced with the name in the
    /// preferred language found in the program guide (applied after each guide update).
    /// </summary>
    public bool UseGuideChannelNames { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether channels get a description with their country,
    /// regions, languages and categories (shown on the channel page in Jellyfin).
    /// </summary>
    public bool ChannelDescriptions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether programmes get the genre markers Jellyfin uses for
    /// its Live TV sections (films, series, sports, kids, news), derived from Russian title
    /// prefixes (х/ф, т/с, м/ф…), guide categories and the channel's category.
    /// </summary>
    public bool ClassifyProgrammes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether sports broadcasts (matches, races, fights) on any
    /// channel are put into "Sports", recognised strictly from the programme title.
    /// </summary>
    public bool DetectSportsBroadcasts { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether only live sports broadcasts go to "Sports".
    /// </summary>
    public bool LiveSportsOnly { get; set; }

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
        PreferredLanguages = Clean(PreferredLanguages, toUpper: false);

        MaxGuideSources = Math.Clamp(MaxGuideSources, 1, MaxGuideSourcesLimit);
        StreamCheckConcurrency = Math.Clamp(StreamCheckConcurrency, 1, MaxStreamCheckConcurrency);
        StreamCheckTimeoutSeconds = Math.Clamp(StreamCheckTimeoutSeconds, 3, 30);

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
