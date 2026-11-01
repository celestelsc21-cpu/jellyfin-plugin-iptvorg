using Jellyfin.Plugin.IptvOrg.Service;

namespace Jellyfin.Plugin.IptvOrg.Api;

/// <summary>
/// Plugin state for the settings page.
/// </summary>
/// <param name="Status">Persisted run results.</param>
/// <param name="HasCatalog">Whether the catalog has been downloaded.</param>
/// <param name="PlaylistPath">Generated playlist path (as seen by the server).</param>
/// <param name="GuidePath">Generated guide path (as seen by the server).</param>
/// <param name="TunerRegistered">Whether the M3U tuner is present in Live TV.</param>
/// <param name="GuideRegistered">Whether the XMLTV guide is present in Live TV.</param>
public sealed record StatusResponse(
    PluginStatus Status,
    bool HasCatalog,
    string PlaylistPath,
    string GuidePath,
    bool TunerRegistered,
    bool GuideRegistered);
