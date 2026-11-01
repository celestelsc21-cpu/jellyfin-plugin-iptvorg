using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Language from <c>languages.json</c>.
/// </summary>
internal sealed class ApiLanguage
{
    /// <summary>Gets or sets the ISO 639-3 code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
