using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Jellyfin.Plugin.IptvOrg.Configuration;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// Filter settings used by <see cref="PlaylistBuilder"/>.
/// </summary>
internal sealed class ChannelFilterOptions
{
    /// <summary>
    /// Gets or sets the enabled category ids in the administrator's order; empty means all.
    /// </summary>
    public IReadOnlyList<string> Categories { get; set; } = [];

    /// <summary>Gets or sets the language codes; empty means all.</summary>
    public FrozenSet<string> Languages { get; set; } = FrozenSet<string>.Empty;

    /// <summary>Gets or sets the country codes; empty means all.</summary>
    public FrozenSet<string> Countries { get; set; } = FrozenSet<string>.Empty;

    /// <summary>Gets or sets the region codes; empty means all.</summary>
    public FrozenSet<string> Regions { get; set; } = FrozenSet<string>.Empty;

    /// <summary>Gets or sets a value indicating whether adult channels are included.</summary>
    public bool IncludeNsfw { get; set; }

    /// <summary>Gets or sets a value indicating whether streams needing custom headers are skipped.</summary>
    public bool SkipStreamsWithCustomHeaders { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether geo-blocked streams are skipped.</summary>
    public bool SkipGeoBlocked { get; set; }

    /// <summary>Gets or sets a value indicating whether part-time streams are skipped.</summary>
    public bool SkipNot247 { get; set; }

    /// <summary>Gets or sets a value indicating whether the category is prepended to names.</summary>
    public bool PrefixCategoryInName { get; set; }

    /// <summary>
    /// Creates options from the plugin configuration.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <returns>The options.</returns>
    public static ChannelFilterOptions FromConfiguration(PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new ChannelFilterOptions
        {
            Categories = configuration.Categories ?? [],
            Languages = (configuration.Languages ?? []).ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            Countries = (configuration.Countries ?? []).ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            Regions = (configuration.Regions ?? []).ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            IncludeNsfw = configuration.IncludeNsfw,
            SkipStreamsWithCustomHeaders = configuration.SkipStreamsWithCustomHeaders,
            SkipGeoBlocked = configuration.SkipGeoBlocked,
            SkipNot247 = configuration.SkipNot247,
            PrefixCategoryInName = configuration.PrefixCategoryInName
        };
    }
}
