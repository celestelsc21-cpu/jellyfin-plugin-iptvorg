using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.IptvOrg.Catalog.Models;

/// <summary>
/// Category from <c>categories.json</c>.
/// </summary>
internal sealed class ApiCategory
{
    /// <summary>Gets or sets the category id.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the English category name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
