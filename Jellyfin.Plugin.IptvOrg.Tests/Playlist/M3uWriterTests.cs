using Jellyfin.Plugin.IptvOrg.Playlist;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Playlist;

public class M3uWriterTests
{
    [Fact]
    public void Write_ProducesJellyfinCompatibleLines()
    {
        var entries = new[]
        {
            new PlaylistEntry("Ch.ru@SD", "Ch.ru", "SD", "Канал", "https://a/1.m3u8", "https://l/1.png", "news", "Новости", 7)
        };

        var text = M3uWriter.Write(entries);

        Assert.Equal(
            "#EXTM3U\n"
            + "#EXTINF:-1 tvg-id=\"Ch.ru@SD\" tvg-chno=\"7\" tvg-logo=\"https://l/1.png\" group-title=\"Новости\",Канал\n"
            + "https://a/1.m3u8\n",
            text);
    }

    [Fact]
    public void Write_OmitsEmptyAttributes()
    {
        var entries = new[] { new PlaylistEntry(null, null, null, "X", "https://a/x", null, "undefined", "Без категории", 1) };

        var text = M3uWriter.Write(entries);

        Assert.DoesNotContain("tvg-id", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("tvg-logo", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Write_StripsQuotesAndNewLinesFromAttributes()
    {
        var entries = new[] { new PlaylistEntry("a\"b", null, null, "X", "https://a/x\n#EXTINF:-1,evil", null, "news", "Ново\"сти", 1) };

        var text = M3uWriter.Write(entries);

        Assert.Contains("tvg-id=\"ab\"", text, System.StringComparison.Ordinal);
        Assert.Contains("group-title=\"Новости\"", text, System.StringComparison.Ordinal);
        Assert.Equal(4, text.Split('\n').Length);
    }

    [Theory]
    [InlineData("Канал, Москва", "Канал Москва")]
    [InlineData("  Канал   HD ", "Канал HD")]
    [InlineData("24 Канал", "24 Канал")]
    [InlineData("5.1 Radio", "5.1 Radio")]
    [InlineData("1+1", "1+1")]
    [InlineData("", "?")]
    public void SanitizeName_KeepsNameIntactForParser(string input, string expected)
    {
        Assert.Equal(expected, M3uWriter.SanitizeName(input));
    }
}
