using System.Linq;
using System.Net;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Guide;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Guide;

public class GuideSourcePlannerTests
{
    private static PlaylistEntry Entry(string channel, string? feed)
        => new(feed is null ? channel : channel + "@" + feed, channel, feed, channel, "https://s/" + channel, null, "news", "Новости", 1);

    private static ApiGuide Guide(string channel, string? feed, params string[] urls)
        => new()
        {
            Channel = channel,
            Feed = feed,
            Sources = urls.Select(u => new ApiGuideSource { Url = u, Format = u.EndsWith(".gz", System.StringComparison.Ordinal) ? "GZIP" : "XML" }).ToArray()
        };

    [Fact]
    public void Plan_OrdersByCoverage()
    {
        var entries = new[] { Entry("A.ru", null), Entry("B.ru", null), Entry("C.ru", null) };
        var guides = new[]
        {
            Guide("A.ru", null, "https://small.example/guide.xml"),
            Guide("A.ru", null, "https://big.example/guide.xml.gz"),
            Guide("B.ru", null, "https://big.example/guide.xml.gz"),
            Guide("C.ru", null, "https://big.example/guide.xml.gz")
        };

        var plan = GuideSourcePlanner.Plan(entries, guides, 10);

        Assert.Equal(2, plan.Count);
        Assert.Equal("big.example", plan[0].Url.Host);
        Assert.Equal(3, plan[0].Coverage);
        Assert.True(plan[0].IsGzip);
    }

    [Fact]
    public void Plan_FeedSpecificGuide_MapsBothIdForms()
    {
        var entries = new[] { Entry("A.ru", "HD") };
        var guides = new[] { Guide("A.ru", "HD", "https://g.example/a.xml") };

        var plan = GuideSourcePlanner.Plan(entries, guides, 10);

        var map = Assert.Single(plan).IdMap;
        Assert.Equal(new[] { "A.ru@HD" }, map["A.ru@HD"]);
        Assert.Equal(new[] { "A.ru@HD" }, map["A.ru"]);
    }

    [Fact]
    public void Plan_FeedSpecificGuide_DoesNotServeOtherFeeds()
    {
        var entries = new[] { Entry("A.ru", "SD") };
        var guides = new[] { Guide("A.ru", "Plus4", "https://g.example/a.xml") };

        Assert.Empty(GuideSourcePlanner.Plan(entries, guides, 10));
    }

    [Fact]
    public void Plan_ChannelWideGuide_ServesEveryFeed()
    {
        var entries = new[] { Entry("A.ru", "SD"), Entry("A.ru", "Plus4") };
        var guides = new[] { Guide("A.ru", null, "https://g.example/a.xml") };

        var plan = GuideSourcePlanner.Plan(entries, guides, 10);

        Assert.Equal(new[] { "A.ru@Plus4", "A.ru@SD" }, Assert.Single(plan).IdMap["A.ru"]);
    }

    [Fact]
    public void Plan_KnownSiteWithoutSources_UsesSiteFileWithSiteId()
    {
        var entries = new[] { Entry("ChannelOne.ru", "HD") };
        var guides = new[]
        {
            new ApiGuide { Channel = "ChannelOne.ru", Feed = null, Site = "epg.iptvx.one", SiteId = "perviy", Sources = [] }
        };

        var plan = GuideSourcePlanner.Plan(entries, guides, 10);

        var source = Assert.Single(plan);
        Assert.Equal("iptvx.one", source.Url.Host);
        Assert.True(source.IsGzip);
        Assert.Equal(new[] { "ChannelOne.ru@HD" }, source.IdMap["perviy"]);
    }

    [Fact]
    public void Plan_UnknownSiteWithoutSources_PlansNothing()
    {
        var entries = new[] { Entry("ChannelOne.ru", null) };
        var guides = new[] { new ApiGuide { Channel = "ChannelOne.ru", Site = "tv.mail.ru", SiteId = "123" } };

        Assert.Empty(GuideSourcePlanner.Plan(entries, guides, 10));
    }

    [Fact]
    public void Plan_SeveralChannelsOnOneSite_ShareOneFile()
    {
        var entries = new[] { Entry("A.ru", null), Entry("B.ru", null) };
        var guides = new[]
        {
            new ApiGuide { Channel = "A.ru", Site = "epg.iptvx.one", SiteId = "a" },
            new ApiGuide { Channel = "B.ru", Site = "epg.iptvx.one", SiteId = "b" }
        };

        var source = Assert.Single(GuideSourcePlanner.Plan(entries, guides, 10));

        Assert.Equal(2, source.Coverage);
    }

    [Fact]
    public void Plan_HdWithoutOwnGuide_BorrowsSdGuide()
    {
        var entries = new[] { Entry("Evrokino.ru", "HD") };
        var guides = new[]
        {
            new ApiGuide { Channel = "Evrokino.ru", Feed = "SD", Site = "epg.iptvx.one", SiteId = "evrokino" },
            new ApiGuide { Channel = "Evrokino.ru", Feed = "HD", Site = "tv.mail.ru", SiteId = "2751" }
        };

        var source = Assert.Single(GuideSourcePlanner.Plan(entries, guides, 10));

        Assert.Equal(new[] { "Evrokino.ru@HD" }, source.IdMap["evrokino"]);
    }

    [Fact]
    public void Plan_OwnGuideWins_NoBorrowing()
    {
        var entries = new[] { Entry("A.ru", "HD") };
        var guides = new[]
        {
            new ApiGuide { Channel = "A.ru", Feed = "HD", Site = "epg.iptvx.one", SiteId = "a-hd" },
            new ApiGuide { Channel = "A.ru", Feed = "SD", Site = "epg.iptvx.one", SiteId = "a-sd" }
        };

        var source = Assert.Single(GuideSourcePlanner.Plan(entries, guides, 10));

        Assert.True(source.IdMap.ContainsKey("a-hd"));
        Assert.False(source.IdMap.ContainsKey("a-sd"));
    }

    [Fact]
    public void Plan_RespectsMaxSources()
    {
        var entries = new[] { Entry("A.ru", null) };
        var guides = new[] { Guide("A.ru", null, "https://1.example/a.xml", "https://2.example/a.xml", "https://3.example/a.xml") };

        Assert.Equal(2, GuideSourcePlanner.Plan(entries, guides, 2).Count);
    }

    [Theory]
    [InlineData("https://g.example/guide.json", "JSON")]
    [InlineData("ftp://g.example/guide.xml", "XML")]
    [InlineData("http://127.0.0.1/guide.xml", "XML")]
    [InlineData("http://192.168.1.5:8080/guide.xml", "XML")]
    [InlineData("http://localhost/guide.xml", "XML")]
    [InlineData("http://[::1]/guide.xml", "XML")]
    public void TryGetUrl_RejectsUnsafeOrUnsupported(string url, string format)
    {
        Assert.False(GuideSourcePlanner.TryGetUrl(new ApiGuideSource { Url = url, Format = format }, out _, out _));
    }

    [Fact]
    public void TryGetUrl_AcceptsPublicXml()
    {
        Assert.True(GuideSourcePlanner.TryGetUrl(new ApiGuideSource { Url = "http://194.163.157.137:8080/guide.xml", Format = "XML" }, out var url, out var gzip));
        Assert.Equal(8080, url.Port);
        Assert.False(gzip);
    }

    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.5.5", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("100.64.0.1", true)]
    [InlineData("169.254.1.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("fd00::1", true)]
    [InlineData("2001:4860:4860::8888", false)]
    public void IsPrivateAddress_ClassifiesRanges(string address, bool expected)
    {
        Assert.Equal(expected, GuideSourcePlanner.IsPrivateAddress(IPAddress.Parse(address)));
    }
}
