using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Logo from <c>logos.json</c>.
/// </summary>
internal sealed class ApiLogo
{
    /// <summary>Gets or sets the channel id.</summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>Gets or sets the feed id, or <c>null</c> for a channel-wide logo.</summary>
    [JsonPropertyName("feed")]
    public string? Feed { get; set; }

    /// <summary>Gets or sets a value indicating whether the broadcaster currently uses this logo.</summary>
    [JsonPropertyName("in_use")]
    public bool InUse { get; set; }

    /// <summary>Gets or sets the image format, for example <c>PNG</c> or <c>SVG</c>.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>Gets or sets the logo URL.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
