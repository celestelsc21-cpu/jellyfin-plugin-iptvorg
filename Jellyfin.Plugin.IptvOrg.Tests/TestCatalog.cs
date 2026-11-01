using System.Collections.Generic;
using Jellyfin.Plugin.IptvOrg.Catalog;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;

namespace Jellyfin.Plugin.IptvOrg.Tests;

/// <summary>
/// Builders for small in-memory catalogs.
/// </summary>
internal static class TestCatalog
{
    public static ApiChannel Channel(string id, string name, string country, params string[] categories)
        => new() { Id = id, Name = name, Country = country, Categories = categories };

    public static ApiStream Stream(string? channel, string url, string? feed = null, string? title = null, string? quality = null)
        => new() { Channel = channel, Url = url, Feed = feed, Title = title, Quality = quality };

    public static ApiFeed Feed(string channel, string id, bool isMain, params string[] languages)
        => new() { Channel = channel, Id = id, IsMain = isMain, Languages = languages };

    public static CatalogSnapshot Create(
        IReadOnlyList<ApiChannel> channels,
        IReadOnlyList<ApiStream> streams,
        IReadOnlyList<ApiFeed>? feeds = null,
        IReadOnlyList<ApiLogo>? logos = null,
        IReadOnlyList<ApiRegion>? regions = null,
        IReadOnlyList<ApiGuide>? guides = null)
    {
        var categories = new List<ApiCategory>
        {
            new() { Id = "kids", Name = "Kids" },
            new() { Id = "movies", Name = "Movies" },
            new() { Id = "news", Name = "News" },
            new() { Id = "sports", Name = "Sports" },
            new() { Id = "xxx", Name = "XXX" }
        };

        return new CatalogSnapshot(
            channels,
            feeds ?? [],
            streams,
            categories,
            logos ?? [],
            regions ?? [],
            guides ?? []);
    }
}
