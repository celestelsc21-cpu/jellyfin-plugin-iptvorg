using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Guide mapping from <c>guides.json</c>: which hosted XMLTV files carry a channel.
/// </summary>
internal sealed class ApiGuide
{
    /// <summary>Gets or sets the channel id, if known.</summary>
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    /// <summary>Gets or sets the feed id, if known.</summary>
    [JsonPropertyName("feed")]
    public string? Feed { get; set; }

    /// <summary>Gets or sets the hosted guide files that contain this channel.</summary>
    [JsonPropertyName("sources")]
    public IReadOnlyList<ApiGuideSource>? Sources { get; set; }
}
