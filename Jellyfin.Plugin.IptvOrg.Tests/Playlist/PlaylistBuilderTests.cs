using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Xunit;
using static Jellyfin.Plugin.IptvOrg.Tests.TestCatalog;

namespace Jellyfin.Plugin.IptvOrg.Tests.Playlist;

public class PlaylistBuilderTests
{
    [Fact]
    public void Build_NoFilter_IncludesAllButAdult()
    {
        var catalog = Create(
            [
                Channel("News1.ru", "Новости 1", "RU", "news"),
                Channel("Adult.us", "Adult", "US", "xxx"),
                Channel("Hidden.us", "Hidden", "US", "movies")
            ],
            [
                Stream("News1.ru", "https://a/news.m3u8"),
                Stream("Adult.us", "https://a/adult.m3u8"),
                Stream("Hidden.us", "https://a/hidden.m3u8")
            ]);
        catalog.Channels[2].IsNsfw = true;

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions());

        Assert.Single(entries);
        Assert.Equal("News1.ru", entries[0].TvgId);
    }

    [Fact]
    public void Build_IncludeNsfw_KeepsAdult()
    {
        var catalog = Create(
            [Channel("Adult.us", "Adult", "US", "xxx")],
            [Stream("Adult.us", "https://a/adult.m3u8")]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions { IncludeNsfw = true });

        Assert.Single(entries);
        Assert.Equal("18+", entries[0].CategoryName);
    }

    [Fact]
    public void Build_CategoryFilter_UsesFirstEnabledCategory()
    {
        var catalog = Create(
            [Channel("Mix.ru", "Микс", "RU", "news", "sports")],
            [Stream("Mix.ru", "https://a/mix.m3u8")]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions { Categories = ["sports"] });

        Assert.Single(entries);
        Assert.Equal("sports", entries[0].CategoryId);
        Assert.Equal("Спорт", entries[0].CategoryName);
    }

    [Fact]
    public void Build_CategoryFilter_DropsOtherCategories()
    {
        var catalog = Create(
            [Channel("News1.ru", "Новости", "RU", "news"), Channel("Kids.ru", "Дети", "RU", "kids")],
            [Stream("News1.ru", "https://a/1.m3u8"), Stream("Kids.ru", "https://a/2.m3u8")]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions { Categories = ["kids"] });

        Assert.Equal(new[] { "Kids.ru" }, entries.Select(e => e.TvgId));
    }

    [Fact]
    public void Build_UncategorizedChannel_GoesToUndefinedOnlyWhenSelected()
    {
        var catalog = Create(
            [Channel("Plain.ru", "Без", "RU")],
            [Stream("Plain.ru", "https://a/p.m3u8")]);

        Assert.Single(PlaylistBuilder.Build(catalog, new ChannelFilterOptions()));
        Assert.Empty(PlaylistBuilder.Build(catalog, new ChannelFilterOptions { Categories = ["news"] }));
        Assert.Single(PlaylistBuilder.Build(catalog, new ChannelFilterOptions { Categories = ["undefined"] }));
    }

    [Fact]
    public void Build_SameChannelAndFeed_KeepsBestStream()
    {
        var catalog = Create(
            [Channel("Ch.ru", "Канал", "RU", "news")],
            [
                Stream("Ch.ru", "https://a/low.m3u8", quality: "480p"),
                Stream("Ch.ru", "https://a/high.m3u8", quality: "1080p"),
                new ApiStream { Channel = "Ch.ru", Url = "https://a/geo.m3u8", Quality = "2160p", Labels = ["Geo-blocked"] }
            ]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions());

        Assert.Single(entries);
        Assert.Equal("https://a/high.m3u8", entries[0].Url);
    }

    [Fact]
    public void Build_DifferentFeeds_AreSeparateChannelsWithFeedTvgId()
    {
        var catalog = Create(
            [Channel("Ch.ru", "Канал", "RU", "news")],
            [Stream("Ch.ru", "https://a/main.m3u8"), Stream("Ch.ru", "https://a/plus4.m3u8", feed: "Plus4")],
            feeds: [Feed("Ch.ru", "SD", true, "rus"), Feed("Ch.ru", "Plus4", false, "rus")]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions());

        Assert.Equal(new[] { "Ch.ru@Plus4", "Ch.ru@SD" }, entries.Select(e => e.TvgId).OrderBy(x => x, System.StringComparer.Ordinal));
    }

    [Fact]
    public void Build_CustomHeaders_SkippedByDefault()
    {
        var catalog = Create(
            [Channel("Ch.ru", "Канал", "RU", "news")],
            [new ApiStream { Channel = "Ch.ru", Url = "https://a/1.m3u8", Referrer = "https://site/" }]);

        Assert.Empty(PlaylistBuilder.Build(catalog, new ChannelFilterOptions()));
        Assert.Single(PlaylistBuilder.Build(catalog, new ChannelFilterOptions { SkipStreamsWithCustomHeaders = false }));
    }

    [Fact]
    public void Build_UnsupportedScheme_Skipped()
    {
        var catalog = Create(
            [Channel("Ch.ru", "Канал", "RU", "news")],
            [Stream("Ch.ru", "file:///etc/passwd")]);

        Assert.Empty(PlaylistBuilder.Build(catalog, new ChannelFilterOptions()));
    }

    [Fact]
    public void Build_ClosedChannel_Skipped()
    {
        var catalog = Create(
            [new ApiChannel { Id = "Old.ru", Name = "Old", Country = "RU", Categories = ["news"], Closed = "2020-01-01" }],
            [Stream("Old.ru", "https://a/old.m3u8")]);

        Assert.Empty(PlaylistBuilder.Build(catalog, new ChannelFilterOptions()));
    }

    [Fact]
    public void Build_LanguageFilter_UsesFeedLanguages()
    {
        var catalog = Create(
            [Channel("Ru.ru", "Ру", "RU", "news"), Channel("En.uk", "En", "UK", "news")],
            [Stream("Ru.ru", "https://a/ru.m3u8"), Stream("En.uk", "https://a/en.m3u8")],
            feeds: [Feed("Ru.ru", "SD", true, "rus"), Feed("En.uk", "SD", true, "eng")]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions { Languages = new[] { "rus" }.ToFrozenSet() });

        Assert.Equal(new[] { "Ru.ru@SD" }, entries.Select(e => e.TvgId));
    }

    [Fact]
    public void Build_RegionFilter_ExpandsToCountries()
    {
        var catalog = Create(
            [Channel("Ru.ru", "Ру", "RU", "news"), Channel("Us.us", "Us", "US", "news")],
            [Stream("Ru.ru", "https://a/ru.m3u8"), Stream("Us.us", "https://a/us.m3u8")],
            regions: [new ApiRegion { Code = "CIS", Countries = ["RU", "BY"] }]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions { Regions = new[] { "CIS" }.ToFrozenSet() });

        Assert.Equal(new[] { "Ru.ru" }, entries.Select(e => e.TvgId));
    }

    [Fact]
    public void Build_OrdersByCategoryThenName_AndNumbersFromOne()
    {
        var catalog = Create(
            [
                Channel("B.ru", "Бета", "RU", "news"),
                Channel("A.ru", "Альфа", "RU", "news"),
                Channel("K.ru", "Кидс", "RU", "kids"),
                Channel("U.ru", "Юникс", "RU")
            ],
            [
                Stream("B.ru", "https://a/b.m3u8"),
                Stream("A.ru", "https://a/a.m3u8"),
                Stream("K.ru", "https://a/k.m3u8"),
                Stream("U.ru", "https://a/u.m3u8")
            ]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions());

        Assert.Equal(new[] { "K.ru", "A.ru", "B.ru", "U.ru" }, entries.Select(e => e.TvgId));
        Assert.Equal(new[] { 1, 2, 3, 4 }, entries.Select(e => e.Number));
    }

    [Fact]
    public void Build_PrefixCategory_AddsRussianCategoryName()
    {
        var catalog = Create(
            [Channel("Ch.ru", "Канал", "RU", "news")],
            [Stream("Ch.ru", "https://a/1.m3u8", title: "Канал HD")]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions { PrefixCategoryInName = true });

        Assert.Equal("[Новости] Канал HD", entries[0].Name);
    }

    [Fact]
    public void Build_Logo_PrefersFeedThenRaster()
    {
        var catalog = Create(
            [Channel("Ch.ru", "Канал", "RU", "news")],
            [Stream("Ch.ru", "https://a/1.m3u8", feed: "HD")],
            logos:
            [
                new ApiLogo { Channel = "Ch.ru", Feed = null, InUse = true, Format = "SVG", Url = "https://l/any.svg" },
                new ApiLogo { Channel = "Ch.ru", Feed = null, InUse = true, Format = "PNG", Url = "https://l/any.png" },
                new ApiLogo { Channel = "Ch.ru", Feed = "HD", InUse = true, Format = "PNG", Url = "https://l/hd.png" }
            ]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions());

        Assert.Equal("https://l/hd.png", entries[0].LogoUrl);
    }

    [Fact]
    public void Build_StreamWithoutChannel_KeptAsUndefinedWithoutTvgId()
    {
        var catalog = Create([], [Stream(null, "https://a/x.m3u8", title: "Неизвестный")]);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions());

        Assert.Single(entries);
        Assert.Null(entries[0].TvgId);
        Assert.Equal("undefined", entries[0].CategoryId);
    }

    [Fact]
    public void CountByCategory_CountsOnlyChannelsWithStreams()
    {
        var catalog = Create(
            [Channel("A.ru", "A", "RU", "news", "sports"), Channel("B.ru", "B", "RU", "news")],
            [Stream("A.ru", "https://a/a.m3u8")]);

        var counts = PlaylistBuilder.CountByCategory(catalog, includeNsfw: false);

        Assert.Equal(1, counts["news"]);
        Assert.Equal(1, counts["sports"]);
    }

    [Theory]
    [InlineData("1080p", 1080)]
    [InlineData("720p", 720)]
    [InlineData(null, 0)]
    [InlineData("HD", 0)]
    public void ParseQuality_ParsesLeadingNumber(string? value, int expected)
    {
        Assert.Equal(expected, PlaylistBuilder.ParseQuality(value));
    }

    [Fact]
    public void PickCategory_NoEnabledMatch_ReturnsNull()
    {
        var enabled = new HashSet<string>(["news"], System.StringComparer.OrdinalIgnoreCase);

        Assert.Null(PlaylistBuilder.PickCategory(["sports"], enabled));
        Assert.Equal("news", PlaylistBuilder.PickCategory(["sports", "News"], enabled));
    }
}
