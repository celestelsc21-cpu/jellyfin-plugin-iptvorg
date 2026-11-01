using System.Collections.Generic;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// One future playlist channel and its usable streams, best first.
/// </summary>
/// <param name="Channel">The iptv-org channel, or <c>null</c> for a stream without one.</param>
/// <param name="Category">Primary category id.</param>
/// <param name="Streams">Candidate streams in order of preference.</param>
internal sealed record ChannelCandidates(
    ApiChannel? Channel,
    string Category,
    IReadOnlyList<StreamCandidate> Streams);
