namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// Whether the plugin's tuner and guide source are present in Live TV.
/// </summary>
/// <param name="TunerId">Id of the M3U tuner that reads the playlist, if registered.</param>
/// <param name="ListingsProviderId">Id of the XMLTV source that reads the guide, if registered.</param>
public sealed record LiveTvRegistration(string? TunerId, string? ListingsProviderId);
