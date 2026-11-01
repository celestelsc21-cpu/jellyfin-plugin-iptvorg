using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// A hosted guide file listed in <c>guides.json</c>.
/// </summary>
internal sealed class ApiGuideSource
{
    /// <summary>Gets or sets the host that serves the guide.</summary>
    [JsonPropertyName("host")]
    public string? Host { get; set; }

    /// <summary>Gets or sets the file format: <c>XML</c>, <c>GZIP</c> or <c>JSON</c>.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>Gets or sets the guide URL.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
