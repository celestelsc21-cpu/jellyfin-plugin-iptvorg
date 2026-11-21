using System;
using System.Collections.Generic;
using Jellyfin.Plugin.IptvOrg.Foss;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Foss;

public class FossExporterTests
{
    [Theory]
    [InlineData("", 0x02CC5D05U)]
    [InlineData("abc", 0x32D153FFU)]
    public void HashChannelId_MatchesXxHash32(string id, uint expected)
    {
        Assert.Equal(expected, FossExporter.HashChannelId(id));
    }

    [Fact]
    public void WritePlaylist_PreservesChannelDetailsAndAddsDirectEpgBinding()
    {
        var entries = new List<PlaylistEntry>
        {
            new("AutoPlus.ru@SD", "AutoPlus.ru", "SD", "Авто Плюс", "https://example.org/live.m3u8", "https://example.org/logo.png", "auto", "Авто", 5),
            new(null, null, null, "Другое", "https://example.org/other.m3u8", null, "other", "Разное", 6)
        };

        var result = FossExporter.WritePlaylist(entries, new HashSet<string>(StringComparer.Ordinal) { "AutoPlus.ru@SD" });

        Assert.StartsWith("#EXTM3U\n", result, StringComparison.Ordinal);
        Assert.Contains("tvg-id=\"AutoPlus.ru@SD\"", result, StringComparison.Ordinal);
        Assert.Contains("tvg-name=\"Авто Плюс\"", result, StringComparison.Ordinal);
        Assert.Contains("group-title=\"Авто\"", result, StringComparison.Ordinal);
        Assert.Contains("tvg-source=\"=iptvorg\"", result, StringComparison.Ordinal);
        Assert.DoesNotContain("[Авто]", result, StringComparison.Ordinal);
        Assert.Equal(1, result.Split("tvg-source=\"=iptvorg\"", StringSplitOptions.None).Length - 1);
    }
}
