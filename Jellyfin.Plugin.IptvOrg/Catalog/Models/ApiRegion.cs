using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Region from <c>regions.json</c>.
/// </summary>
internal sealed class ApiRegion
{
    /// <summary>Gets or sets the region code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the countries in the region.</summary>
    [JsonPropertyName("countries")]
    public IReadOnlyList<string>? Countries { get; set; }
}
