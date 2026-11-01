using System;
using Jellyfin.Plugin.IptvOrg.Streams;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Streams;

public class HlsPlaylistTests
{
    private static readonly Uri Base = new("https://cdn.example/live/master.m3u8");

    [Fact]
    public void TryGetFirstVariant_ResolvesRelativeUri()
    {
        const string Text = "#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-STREAM-INF:BANDWIDTH=800000\nlow/index.m3u8\n#EXT-X-STREAM-INF:BANDWIDTH=2000000\nhigh/index.m3u8\n";

        Assert.True(HlsPlaylist.TryGetFirstVariant(Text, Base, out var variant));
        Assert.Equal("https://cdn.example/live/low/index.m3u8", variant!.AbsoluteUri);
    }

    [Fact]
    public void TryGetFirstVariant_MediaPlaylist_ReturnsFalse()
    {
        const string Text = "#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXTINF:6.0,\nseg1.ts\n";

        Assert.False(HlsPlaylist.TryGetFirstVariant(Text, Base, out _));
    }

    [Fact]
    public void TryGetFirstVariant_AbsoluteVariant_Kept()
    {
        const string Text = "#EXTM3U\r\n#EXT-X-STREAM-INF:BANDWIDTH=1\r\nhttps://other.example/v.m3u8\r\n";

        Assert.True(HlsPlaylist.TryGetFirstVariant(Text, Base, out var variant));
        Assert.Equal("other.example", variant!.Host);
    }

    [Theory]
    [InlineData("#EXTM3U\n", true)]
    [InlineData("﻿  #EXTM3U", true)]
    [InlineData("<html>", false)]
    public void IsPlaylist_DetectsHeader(string text, bool expected)
    {
        Assert.Equal(expected, HlsPlaylist.IsPlaylist(text));
    }
}
