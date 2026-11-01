using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Xunit;
using static Jellyfin.Plugin.IptvOrg.Tests.TestCatalog;

namespace Jellyfin.Plugin.IptvOrg.Tests.Playlist;

public class FeedHandlingTests
{
    private static readonly ApiFeed[] Feeds =
    [
        Feed("One.ru", "SD", true, "rus"),
        Feed("One.ru", "HD", false, "rus"),
        new ApiFeed { Channel = "One.ru", Id = "Plus4", Name = "+4", Languages = ["rus"] },
        new ApiFeed { Channel = "One.ru", Id = "HDPlus4", Name = "HD +4", Languages = ["rus"] }
    ];

    private static readonly ApiStream[] Streams =
    [
        Stream("One.ru", "https://a/sd.m3u8", feed: "SD", quality: "576p"),
        Stream("One.ru", "https://a/hd.m3u8", feed: "HD", quality: "1080p"),
        Stream("One.ru", "https://a/plus4.m3u8", feed: "Plus4", quality: "576p"),
        Stream("One.ru", "https://a/hdplus4.m3u8", feed: "HDPlus4", quality: "1080p")
    ];

    [Theory]
    [InlineData("Plus2", "+2", true)]
    [InlineData("HDPlus4", "HD +4", true)]
    [InlineData("Minus1", "-1", true)]
    [InlineData("SD", "SD", false)]
    [InlineData("HD", "HD", false)]
    [InlineData("Plus", "Plus", false)]
    [InlineData("Europe", "+1 Europe", true)]
    [InlineData("SD", "1+1", false)]
    public void IsTimeshift_DetectsShiftedFeeds(string id, string name, bool expected)
    {
        Assert.Equal(expected, PlaylistBuilder.IsTimeshift(id, name));
    }

    [Fact]
    public void Collect_HideTimeshift_DropsShiftedFeeds()
    {
        var catalog = Create([Channel("One.ru", "Первый", "RU", "general")], Streams, feeds: Feeds);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions { HideTimeshiftFeeds = true });

        Assert.Equal(new[] { "One.ru@HD", "One.ru@SD" }, entries.Select(e => e.TvgId).OrderBy(x => x, System.StringComparer.Ordinal));
    }

    [Fact]
    public void Collect_OneStreamPerChannel_PicksBestQuality()
    {
        var catalog = Create([Channel("One.ru", "Первый", "RU", "general")], Streams, feeds: Feeds);

        var entries = PlaylistBuilder.Build(catalog, new ChannelFilterOptions { HideTimeshiftFeeds = true, OneStreamPerChannel = true });

        var entry = Assert.Single(entries);
        Assert.Equal("https://a/hd.m3u8", entry.Url);
        Assert.Equal("One.ru@HD", entry.TvgId);
    }

    [Fact]
    public void Materialize_DeadBestStream_FallsBackToNext()
    {
        var catalog = Create([Channel("One.ru", "Первый", "RU", "general")], Streams, feeds: Feeds);
        var options = new ChannelFilterOptions { HideTimeshiftFeeds = true, OneStreamPerChannel = true };
        var groups = PlaylistBuilder.Collect(catalog, options);

        var entries = PlaylistBuilder.Materialize(groups, catalog, options, new HashSet<string> { "https://a/hd.m3u8" });

        var entry = Assert.Single(entries);
        Assert.Equal("https://a/sd.m3u8", entry.Url);
        Assert.Equal("One.ru@SD", entry.TvgId);
    }

    [Fact]
    public void Materialize_AllStreamsDead_DropsChannel()
    {
        var catalog = Create([Channel("One.ru", "Первый", "RU", "general")], Streams, feeds: Feeds);
        var options = new ChannelFilterOptions { HideTimeshiftFeeds = true, OneStreamPerChannel = true };
        var groups = PlaylistBuilder.Collect(catalog, options);

        var entries = PlaylistBuilder.Materialize(groups, catalog, options, new HashSet<string> { "https://a/hd.m3u8", "https://a/sd.m3u8" });

        Assert.Empty(entries);
    }

    [Fact]
    public void Collect_OrdersStreamsWithoutLabelsFirst()
    {
        var streams = new[]
        {
            new ApiStream { Channel = "One.ru", Url = "https://a/geo.m3u8", Quality = "1080p", Labels = ["Geo-blocked"] },
            Stream("One.ru", "https://a/plain.m3u8", quality: "480p")
        };
        var catalog = Create([Channel("One.ru", "Первый", "RU", "general")], streams);

        var group = Assert.Single(PlaylistBuilder.Collect(catalog, new ChannelFilterOptions { OneStreamPerChannel = true }));

        Assert.Equal(new[] { "https://a/plain.m3u8", "https://a/geo.m3u8" }, group.Streams.Select(s => s.Url));
    }
}
