using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Stream from <c>streams.json</c>.
/// </summary>
internal sealed class ApiStream
{
    /// <summary>Gets or sets the channel id, if known.</summary>
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    /// <summary>Gets or sets the feed id, if known.</summary>
    [JsonPropertyName("feed")]
    public string? Feed { get; set; }

    /// <summary>Gets or sets the stream title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the stream URL.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the required Referer header, if any.</summary>
    [JsonPropertyName("referrer")]
    public string? Referrer { get; set; }

    /// <summary>Gets or sets the required User-Agent header, if any.</summary>
    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>Gets or sets the maximum quality, for example <c>720p</c>.</summary>
    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    /// <summary>Gets or sets the labels (<c>Geo-blocked</c>, <c>Not 24/7</c>).</summary>
    [JsonPropertyName("labels")]
    public IReadOnlyList<string>? Labels { get; set; }
}
