using System.Linq;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Xunit;
using static Jellyfin.Plugin.IptvOrg.Tests.TestCatalog;

namespace Jellyfin.Plugin.IptvOrg.Tests.Playlist;

public class FilterCatalogBuilderTests
{
    [Fact]
    public void Build_CountsCountriesLanguagesAndRegions()
    {
        var catalog = Create(
            [
                Channel("A.ru", "A", "RU", "news"),
                Channel("B.ru", "B", "RU", "news"),
                Channel("C.by", "C", "BY", "news"),
                Channel("NoStream.ru", "N", "RU", "news")
            ],
            [
                Stream("A.ru", "https://a/a1.m3u8"),
                Stream("A.ru", "https://a/a2.m3u8"),
                Stream("B.ru", "https://a/b.m3u8"),
                Stream("C.by", "https://a/c.m3u8")
            ],
            feeds: [Feed("A.ru", "SD", true, "rus"), Feed("B.ru", "SD", true, "rus"), Feed("C.by", "SD", true, "bel", "rus")],
            regions: [new ApiRegion { Code = "CIS", Name = "CIS", Countries = ["RU", "BY", "KZ"] }],
            countries: [new ApiCountry { Code = "RU", Name = "Russia", Flag = "🇷🇺" }]);

        var filters = FilterCatalogBuilder.Build(catalog, includeNsfw: false);

        var ru = filters.Countries.First();
        Assert.Equal("RU", ru.Code);
        Assert.Equal("Russia", ru.Name);
        Assert.Equal(2, ru.ChannelCount);
        Assert.Equal(3, filters.Languages.Single(l => l.Code == "rus").ChannelCount);
        Assert.Equal(1, filters.Languages.Single(l => l.Code == "bel").ChannelCount);
        Assert.Equal(3, Assert.Single(filters.Regions).ChannelCount);
    }

    [Fact]
    public void Build_SkipsAdultUnlessIncluded()
    {
        var catalog = Create([Channel("X.us", "X", "US", "xxx")], [Stream("X.us", "https://a/x.m3u8")]);

        Assert.Empty(FilterCatalogBuilder.Build(catalog, includeNsfw: false).Countries);
        Assert.Single(FilterCatalogBuilder.Build(catalog, includeNsfw: true).Countries);
    }
}
