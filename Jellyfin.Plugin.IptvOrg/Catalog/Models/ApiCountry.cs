using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Country from <c>countries.json</c>.
/// </summary>
internal sealed class ApiCountry
{
    /// <summary>Gets or sets the ISO 3166-1 alpha-2 code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the flag emoji.</summary>
    [JsonPropertyName("flag")]
    public string? Flag { get; set; }
}
