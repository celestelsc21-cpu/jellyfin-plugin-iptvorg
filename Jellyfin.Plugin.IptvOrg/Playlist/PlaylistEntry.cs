namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// One channel of the generated playlist.
/// </summary>
/// <param name="TvgId">XMLTV id (<c>Channel.cc</c> or <c>Channel.cc@Feed</c>); <c>null</c> for unknown channels.</param>
/// <param name="ChannelId">iptv-org channel id, if known.</param>
/// <param name="FeedId">iptv-org feed id, if known.</param>
/// <param name="Name">Display name.</param>
/// <param name="Url">Stream URL.</param>
/// <param name="LogoUrl">Logo URL, if any.</param>
/// <param name="CategoryId">Primary category id.</param>
/// <param name="CategoryName">Primary category display name.</param>
/// <param name="Number">Channel number.</param>
/// <param name="Description">Channel description (country, regions, languages, categories).</param>
internal sealed record PlaylistEntry(
    string? TvgId,
    string? ChannelId,
    string? FeedId,
    string Name,
    string Url,
    string? LogoUrl,
    string CategoryId,
    string CategoryName,
    int Number,
    string? Description = null);
