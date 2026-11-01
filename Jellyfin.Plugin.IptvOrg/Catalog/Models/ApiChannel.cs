using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Channel from <c>channels.json</c>. Only the fields the plugin uses are mapped.
/// </summary>
internal sealed class ApiChannel
{
    /// <summary>Gets or sets the unique channel id, for example <c>Euronews.fr</c>.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the channel name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISO 3166-1 alpha-2 country code.</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>Gets or sets the category ids.</summary>
    [JsonPropertyName("categories")]
    public IReadOnlyList<string>? Categories { get; set; }

    /// <summary>Gets or sets a value indicating whether the channel is adult-oriented.</summary>
    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; set; }

    /// <summary>Gets or sets the closing date, if the channel no longer broadcasts.</summary>
    [JsonPropertyName("closed")]
    public string? Closed { get; set; }
}
