using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Localization;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Jellyfin.Plugin.IptvOrg.Service;
using Xunit;
using static Jellyfin.Plugin.IptvOrg.Tests.TestCatalog;

namespace Jellyfin.Plugin.IptvOrg.Tests.Playlist;

public class ChannelDescriptionTests
{
    [Fact]
    public void BuildAll_Russian_ListsCountryRegionsLanguagesCategories()
    {
        var catalog = Create(
            [Channel("One.ru", "Channel One", "RU", "general", "news")],
            [Stream("One.ru", "https://a/1.m3u8")],
            feeds: [Feed("One.ru", "SD", true, "rus")],
            regions:
            [
                new ApiRegion { Code = "CIS", Name = "Commonwealth of Independent States", Countries = ["RU", "BY"] },
                new ApiRegion { Code = "WW", Name = "Worldwide", Countries = ["RU", "US"] },
                new ApiRegion { Code = "AFR", Name = "Africa", Countries = ["EG"] }
            ],
            countries: [new ApiCountry { Code = "RU", Name = "Russia" }],
            languages: [new ApiLanguage { Code = "rus", Name = "Russian" }]);
        var groups = PlaylistBuilder.Collect(catalog, new ChannelFilterOptions());

        var description = ChannelDescriptionBuilder.BuildAll(catalog, groups, russian: true)["One.ru"];

        Assert.Equal("Страна: Россия\nРегионы: СНГ\nЯзык: русский\nКатегории: Общие, Новости", description);
    }

    [Fact]
    public void BuildAll_English_UsesApiNames()
    {
        var catalog = Create(
            [Channel("One.ru", "Channel One", "RU", "news")],
            [Stream("One.ru", "https://a/1.m3u8")],
            feeds: [Feed("One.ru", "SD", true, "rus", "eng")],
            countries: [new ApiCountry { Code = "RU", Name = "Russia" }],
            languages: [new ApiLanguage { Code = "rus", Name = "Russian" }, new ApiLanguage { Code = "eng", Name = "English" }]);
        var groups = PlaylistBuilder.Collect(catalog, new ChannelFilterOptions());

        var description = ChannelDescriptionBuilder.BuildAll(catalog, groups, russian: false)["One.ru"];

        Assert.Equal("Country: Russia\nLanguages: Russian, English\nCategory: News", description);
    }

    [Theory]
    [InlineData(new[] { "RUS", "eng" }, "ru,en")]
    [InlineData(new[] { " en ", "en", "ukr" }, "en,uk")]
    public void LanguageCodes_Normalize(string[] input, string expected)
    {
        Assert.Equal(expected, string.Join(',', LanguageCodes.Normalize(input)));
    }

    [Theory]
    [InlineData(new[] { "ru", "en" }, true)]
    [InlineData(new[] { "en", "ru" }, false)]
    [InlineData(new[] { "de" }, true)]
    public void LanguageCodes_PrefersRussian(string[] input, bool expected)
    {
        Assert.Equal(expected, LanguageCodes.PrefersRussian(input));
    }

    [Fact]
    public void ExternalId_MatchesJellyfinFormat()
    {
        var id = ChannelMetadataService.GetExternalId("/config/data/iptvorg/playlist.m3u", "https://a/1.m3u8");

        Assert.StartsWith("m3u_", id, System.StringComparison.Ordinal);
        Assert.Equal(4 + 32 + 32, id.Length);
        Assert.True(id.Skip(4).All(char.IsAsciiHexDigit));
    }

    [Fact]
    public void RussianNames_ResourceLoads()
    {
        Assert.Equal("Россия", RussianNames.Country("RU"));
        Assert.Equal("русский", RussianNames.Language("rus"));
        Assert.Equal("СНГ", RussianNames.Region("CIS"));
    }

    [Fact]
    public void Apply_KeepsDescriptionOnRename()
    {
        var entries = new[] { new PlaylistEntry("One.ru", "One.ru", null, "Channel One", "https://s/1", null, "general", "Общие", 1, "Страна: Россия") };

        var result = ChannelNames.Apply(entries, new Dictionary<string, string> { ["One.ru"] = "Первый канал" }, prefixCategory: false);

        Assert.Equal("Первый канал", result[0].Name);
        Assert.Equal("Страна: Россия", result[0].Description);
    }
}
