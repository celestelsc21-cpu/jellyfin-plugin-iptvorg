namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// A category shown in the settings page.
/// </summary>
/// <param name="Id">Category id.</param>
/// <param name="Name">Display name (Russian when known).</param>
/// <param name="ChannelCount">Number of channels with streams in this category.</param>
/// <param name="IsAdult">Whether the category is adult-only.</param>
public sealed record CategoryInfo(string Id, string Name, int ChannelCount, bool IsAdult);
