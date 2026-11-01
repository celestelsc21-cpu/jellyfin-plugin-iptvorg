using System.Collections.Generic;

namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// Channel names collected from the guide, with the language preferences they were collected for.
/// </summary>
internal sealed class GuideNamesFile
{
    /// <summary>Gets or sets the language preferences (comma-separated) the names belong to.</summary>
    public string Languages { get; set; } = string.Empty;

#pragma warning disable CA2227 // Collection properties should be read only (JSON serialization target).
    /// <summary>Gets or sets the names by playlist <c>tvg-id</c>.</summary>
    public Dictionary<string, string> Names { get; set; } = new();
#pragma warning restore CA2227
}
