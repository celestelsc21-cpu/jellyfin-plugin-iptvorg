namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// One selectable value of a country, language or region filter.
/// </summary>
/// <param name="Code">Code stored in the configuration.</param>
/// <param name="Name">English name from iptv-org (the settings page localizes it when it can).</param>
/// <param name="Flag">Flag emoji, for countries.</param>
/// <param name="ChannelCount">Number of channels with streams that match this value.</param>
public sealed record FilterOption(string Code, string Name, string? Flag, int ChannelCount);
