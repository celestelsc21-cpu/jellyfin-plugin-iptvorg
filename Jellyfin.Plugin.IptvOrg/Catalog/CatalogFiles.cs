using System.Collections.Generic;

namespace Jellyfin.Plugin.IptvOrg.Catalog;

/// <summary>
/// Names of the iptv-org API files the plugin downloads.
/// </summary>
internal static class CatalogFiles
{
    /// <summary>Channels dataset.</summary>
    public const string Channels = "channels.json";

    /// <summary>Feeds dataset.</summary>
    public const string Feeds = "feeds.json";

    /// <summary>Streams dataset.</summary>
    public const string Streams = "streams.json";

    /// <summary>Categories dataset.</summary>
    public const string Categories = "categories.json";

    /// <summary>Logos dataset.</summary>
    public const string Logos = "logos.json";

    /// <summary>Regions dataset.</summary>
    public const string Regions = "regions.json";

    /// <summary>Guide sources dataset.</summary>
    public const string Guides = "guides.json";

    /// <summary>
    /// Gets all files, in download order (small files first).
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
        [Categories, Regions, Feeds, Logos, Guides, Streams, Channels];
}
