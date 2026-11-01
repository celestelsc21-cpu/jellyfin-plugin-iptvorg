using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Feed (regional or quality variant of a channel) from <c>feeds.json</c>.
/// </summary>
internal sealed class ApiFeed
{
    /// <summary>Gets or sets the channel id.</summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>Gets or sets the feed id, unique within the channel.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the feed name, for example <c>HD</c> or <c>+2</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets a value indicating whether this is the channel's main feed.</summary>
    [JsonPropertyName("is_main")]
    public bool IsMain { get; set; }

    /// <summary>Gets or sets the ISO 639-3 broadcast languages.</summary>
    [JsonPropertyName("languages")]
    public IReadOnlyList<string>? Languages { get; set; }
}
