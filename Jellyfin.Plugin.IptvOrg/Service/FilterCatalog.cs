using System.Collections.Generic;

namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// The values offered by the country, language and region filters, with channel counts.
/// Computed when the playlist is built, so the settings page never parses the large catalog.
/// </summary>
public sealed class FilterCatalog
{
    /// <summary>Gets or sets the countries that have channels, most channels first.</summary>
    public IReadOnlyList<FilterOption> Countries { get; set; } = [];

    /// <summary>Gets or sets the languages that have channels, most channels first.</summary>
    public IReadOnlyList<FilterOption> Languages { get; set; } = [];

    /// <summary>Gets or sets the regions that have channels, most channels first.</summary>
    public IReadOnlyList<FilterOption> Regions { get; set; } = [];
}
