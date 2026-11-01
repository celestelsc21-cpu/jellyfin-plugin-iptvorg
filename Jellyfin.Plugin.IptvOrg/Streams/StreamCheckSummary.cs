namespace Jellyfin.Plugin.IptvOrg.Streams;

/// <summary>
/// Result counters of one stream check run.
/// </summary>
/// <param name="Checked">Streams probed over the network (cached results excluded).</param>
/// <param name="Dead">Streams found not working.</param>
/// <param name="DroppedChannels">Channels left out because none of their streams worked.</param>
internal sealed record StreamCheckSummary(int Checked, int Dead, int DroppedChannels);
