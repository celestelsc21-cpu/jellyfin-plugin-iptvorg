using Jellyfin.Plugin.IptvOrg.Catalog.Models;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// A stream that passed the filters.
/// </summary>
/// <param name="Stream">The stream.</param>
/// <param name="FeedId">The feed id (the channel's main feed when the stream names none).</param>
/// <param name="IsMainFeed">Whether the feed is the channel's main feed.</param>
internal sealed record StreamCandidate(ApiStream Stream, string? FeedId, bool IsMainFeed)
{
    /// <summary>
    /// Gets the trimmed stream URL.
    /// </summary>
    public string Url => Stream.Url.Trim();
}
