using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Guide;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Guide;

public class GuideNameMatcherTests
{
    private const string Iptvx = "epg.iptvx.one";

    private static ApiGuide Unlinked(string siteId, string siteName, string site = Iptvx)
        => new() { Site = site, SiteId = siteId, SiteName = siteName };

    private static ApiChannel Channel(string id, string name, string country, params string[] altNames)
        => new() { Id = id, Name = name, Country = country, AltNames = altNames };

    private static ApiFeed Feed(string channel, string id, bool isMain, params string[] languages)
        => new() { Channel = channel, Id = id, Name = id, IsMain = isMain, Languages = languages };

    private static IReadOnlyList<ApiGuide> Match(
        IReadOnlyList<ApiChannel> channels,
        IReadOnlyList<ApiFeed> feeds,
        IReadOnlyList<ApiGuide> guides,
        params (string Channel, string? Feed)[] wanted)
        => GuideNameMatcher.Match(TestCatalog.Create(channels, [], feeds, guides: guides), wanted);

    [Theory]
    [InlineData("Belarus-1", "Беларусь 1 HD")]
    [InlineData("Belarus-4 Gomel", "Беларусь 4 Гомель")]
    [InlineData("Belarus-4 Mogilev", "Беларусь 4 Могилев")]
    [InlineData("Detskoe kino", "Детское кино")]
    [InlineData("Priklyucheniya", "Приключения HD")]
    [InlineData("Arktika 24", "Арктика 24")]
    [InlineData("Ugra-TV", "Югра")]
    [InlineData("Sibir 24", "Сибирь 24")]
    [InlineData("Novyi Channel", "Новый канал")]
    public void Normalize_SameKeyForLatinAndCyrillic(string latin, string cyrillic)
    {
        Assert.Equal(ChannelNameKey.Normalize(latin), ChannelNameKey.Normalize(cyrillic));
    }

    [Theory]
    [InlineData("Sport TV", "ТВ Спорт")]
    [InlineData("Belarus-1", "Беларусь 2")]
    [InlineData("Belarus-4 Gomel", "Беларусь 4 Витебск")]
    public void Normalize_DifferentNamesStayDifferent(string a, string b)
    {
        Assert.NotEqual(ChannelNameKey.Normalize(a), ChannelNameKey.Normalize(b));
    }

    [Theory]
    [InlineData("Россия 1 (+4)", true)]
    [InlineData("Мир +2", true)]
    [InlineData("ТВ-21+", false)]
    [InlineData("Беларусь 1 HD", false)]
    public void IsTimeshiftName(string name, bool expected)
    {
        Assert.Equal(expected, ChannelNameKey.IsTimeshiftName(name));
    }

    [Fact]
    public void Match_MainAndHdFeedsGetTheChannelGuide()
    {
        var result = Match(
            [Channel("Belarus1.by", "Belarus-1", "BY", "Беларусь 1")],
            [Feed("Belarus1.by", "SD", true, "rus", "bel"), Feed("Belarus1.by", "HD", false, "rus", "bel")],
            [Unlinked("belarus1", "Беларусь 1 HD"), Unlinked("belarus2", "Беларусь 2 HD")],
            ("Belarus1.by", "SD"),
            ("Belarus1.by", "HD"));

        Assert.Equal(2, result.Count);
        Assert.All(result, g => Assert.Equal("belarus1", g.SiteId));
        Assert.All(result, g => Assert.Equal(Iptvx, g.Site));
        Assert.Equal(new[] { "HD", "SD" }, result.Select(g => g.Feed!).Order().ToArray());
    }

    [Fact]
    public void Match_RegionalFeedNeedsTheRegionInTheName()
    {
        var result = Match(
            [Channel("Belarus4.by", "Belarus-4", "BY")],
            [Feed("Belarus4.by", "Minsk", true, "rus"), Feed("Belarus4.by", "Gomel", false, "rus"), Feed("Belarus4.by", "Brest", false, "rus")],
            [Unlinked("belarus4-gomel", "Беларусь 4 Гомель"), Unlinked("belarus4-vitebsk", "Беларусь 4 Витебск")],
            ("Belarus4.by", "Gomel"),
            ("Belarus4.by", "Brest"),
            ("Belarus4.by", "Minsk"));

        var match = Assert.Single(result);
        Assert.Equal("Gomel", match.Feed);
        Assert.Equal("belarus4-gomel", match.SiteId);
    }

    [Fact]
    public void Match_SkipsGuideChannelsAlreadyLinkedToAnotherChannel()
    {
        var linked = new ApiGuide { Channel = "Other.ru", Feed = "SD", Site = Iptvx, SiteId = "x", SiteName = "Икс" };
        var result = Match(
            [Channel("X.ru", "Iks", "RU")],
            [Feed("X.ru", "SD", true, "rus")],
            [linked, Unlinked("x", "Икс")],
            ("X.ru", "SD"));

        Assert.Empty(result);
    }

    [Fact]
    public void Match_AmbiguousGuideName_NoMatch()
    {
        var result = Match(
            [Channel("Kino1.ru", "Kino 1", "RU")],
            [Feed("Kino1.ru", "SD", true, "rus")],
            [Unlinked("kino1-a", "Кино 1"), Unlinked("kino1-b", "Кино 1 HD")],
            ("Kino1.ru", "SD"));

        Assert.Empty(result);
    }

    [Fact]
    public void Match_TwoCatalogChannelsWithTheSameName_NoMatch()
    {
        var result = Match(
            [Channel("Kino1.ru", "Kino 1", "RU"), Channel("Kino1.ua", "Kino 1", "UA")],
            [Feed("Kino1.ru", "SD", true, "rus"), Feed("Kino1.ua", "SD", true, "ukr")],
            [Unlinked("kino1", "Кино 1")],
            ("Kino1.ru", "SD"));

        Assert.Empty(result);
    }

    [Fact]
    public void Match_ChannelOutsideTheSiteArea_NoMatch()
    {
        var result = Match(
            [Channel("AMC.us", "AMC", "US")],
            [Feed("AMC.us", "SD", true, "eng")],
            [Unlinked("amc", "AMC")],
            ("AMC.us", "SD"));

        Assert.Empty(result);
    }

    [Fact]
    public void Match_RussianLanguageChannelAbroad_Matches()
    {
        var result = Match(
            [Channel("TVRain.nl", "TV Rain", "NL", "Дождь")],
            [Feed("TVRain.nl", "SD", true, "rus")],
            [Unlinked("dozhd", "Дождь")],
            ("TVRain.nl", "SD"));

        Assert.Equal("dozhd", Assert.Single(result).SiteId);
    }

    [Fact]
    public void Match_ShortAlternativeName_Ignored()
    {
        var result = Match(
            [Channel("NaxcivanTV.az", "Naxcivan TV", "AZ", "NTV")],
            [Feed("NaxcivanTV.az", "SD", true, "aze", "rus")],
            [Unlinked("n-tv", "n-tv HD")],
            ("NaxcivanTV.az", "SD"));

        Assert.Empty(result);
    }

    [Fact]
    public void Match_TimeshiftOnEitherSide_NoMatch()
    {
        var result = Match(
            [Channel("Mir.ru", "Mir", "RU")],
            [Feed("Mir.ru", "SD", true, "rus"), Feed("Mir.ru", "Plus2", false, "rus")],
            [Unlinked("mir-2", "Мир (+2)")],
            ("Mir.ru", "SD"),
            ("Mir.ru", "Plus2"));

        Assert.Empty(result);
    }

    [Fact]
    public void Match_SiteWithoutNameRule_NoMatch()
    {
        var result = Match(
            [Channel("Belarus1.by", "Belarus-1", "BY")],
            [Feed("Belarus1.by", "SD", true, "rus")],
            [Unlinked("1385", "Беларусь 1", "tv.mail.ru")],
            ("Belarus1.by", "SD"));

        Assert.Empty(result);
    }

    [Fact]
    public void Plan_NameMatchFillsOnlyChannelsWithoutGuide()
    {
        PlaylistEntry Entry(string channel, string feed)
            => new(channel + "@" + feed, channel, feed, channel, "https://s/" + channel + feed, null, "general", "Общие", 1);

        var entries = new[] { Entry("Belarus1.by", "SD"), Entry("Own.ru", "SD") };
        var guides = new[]
        {
            new ApiGuide { Channel = "Own.ru", Feed = "SD", Site = Iptvx, SiteId = "own" }
        };
        var matches = new[]
        {
            new ApiGuide { Channel = "Belarus1.by", Feed = "SD", Site = Iptvx, SiteId = "belarus1" },
            new ApiGuide { Channel = "Own.ru", Feed = "SD", Site = Iptvx, SiteId = "wrong" }
        };

        var plan = GuideSourcePlanner.Plan(entries, guides, 10, matches);

        var map = Assert.Single(plan).IdMap;
        Assert.Equal(new[] { "Belarus1.by@SD" }, map["belarus1"]);
        Assert.Equal(new[] { "Own.ru@SD" }, map["own"]);
        Assert.False(map.ContainsKey("wrong"));
    }
}
