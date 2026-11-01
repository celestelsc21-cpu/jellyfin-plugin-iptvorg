using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Jellyfin.Plugin.IptvOrg.Guide;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Guide;

public class XmltvMergerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly PlaylistEntry[] Entries =
    [
        new("A.ru", "A.ru", null, "Канал A", "https://s/a", "https://l/a.png", "news", "Новости", 1),
        new("B.ru@HD", "B.ru", "HD", "Канал B", "https://s/b", null, "news", "Новости", 2)
    ];

    private static string Programme(string channel, string title, string stop = "20261101150000 +0000")
        => $"<programme start=\"20261101140000 +0000\" stop=\"{stop}\" channel=\"{channel}\"><title>{title}</title></programme>";

    private static MemoryStream Xml(params string[] programmes)
        => new(Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><tv><channel id=\"x\"><display-name>x</display-name></channel>" + string.Concat(programmes) + "</tv>"));

    private static GuideSource Source(params (string SourceId, string Target)[] map)
        => new(
            new Uri("https://g.example/guide.xml"),
            false,
            map.GroupBy(m => m.SourceId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(m => m.Target).ToArray()));

    private static async Task<XDocument> RunAsync(Func<XmltvMerger, Task> body)
    {
        using var output = new MemoryStream();
        var writer = XmlWriter.Create(output, new XmlWriterSettings { Async = true });
        await using (writer)
        {
            var merger = new XmltvMerger(writer, Entries, Now);
            await merger.WriteHeaderAsync();
            await body(merger);
            await merger.WriteFooterAsync();
        }

        output.Position = 0;
        return await XDocument.LoadAsync(output, LoadOptions.None, CancellationToken.None);
    }

    [Fact]
    public async Task Merge_WritesChannelsAndOnlyMappedProgrammes()
    {
        var doc = await RunAsync(async merger =>
        {
            using var content = Xml(Programme("A.ru", "Новости"), Programme("Other.us", "Чужое"));
            await merger.AddSourceAsync(Source(("A.ru", "A.ru")), content, CancellationToken.None);
        });

        Assert.Equal(new[] { "A.ru", "B.ru@HD" }, doc.Root!.Elements("channel").Select(c => (string?)c.Attribute("id")));
        var programme = Assert.Single(doc.Root.Elements("programme"));
        Assert.Equal("A.ru", (string?)programme.Attribute("channel"));
        Assert.Equal("Новости", (string?)programme.Element("title"));
    }

    [Fact]
    public async Task Merge_RewritesSourceIdToPlaylistId()
    {
        var doc = await RunAsync(async merger =>
        {
            using var content = Xml(Programme("B.ru", "Фильм"));
            await merger.AddSourceAsync(Source(("B.ru", "B.ru@HD")), content, CancellationToken.None);
        });

        Assert.Equal("B.ru@HD", (string?)Assert.Single(doc.Root!.Elements("programme")).Attribute("channel"));
    }

    [Fact]
    public async Task Merge_LaterSourceOnlyFillsEmptyChannels()
    {
        var copiedSecond = 0;
        var doc = await RunAsync(async merger =>
        {
            using (var first = Xml(Programme("A.ru", "Первый")))
            {
                await merger.AddSourceAsync(Source(("A.ru", "A.ru")), first, CancellationToken.None);
            }

            using var second = Xml(Programme("A.ru", "Второй"), Programme("B.ru", "Резерв"));
            copiedSecond = await merger.AddSourceAsync(Source(("A.ru", "A.ru"), ("B.ru", "B.ru@HD")), second, CancellationToken.None);
        });

        Assert.Equal(1, copiedSecond);
        Assert.Equal(
            new[] { "Первый", "Резерв" },
            doc.Root!.Elements("programme").Select(p => (string?)p.Element("title")));
    }

    [Fact]
    public async Task Merge_DropsProgrammesThatEndedLongAgo()
    {
        var doc = await RunAsync(async merger =>
        {
            using var content = Xml(Programme("A.ru", "Старое", "20261031000000 +0000"), Programme("A.ru", "Текущее"));
            await merger.AddSourceAsync(Source(("A.ru", "A.ru")), content, CancellationToken.None);
        });

        Assert.Equal("Текущее", (string?)Assert.Single(doc.Root!.Elements("programme")).Element("title"));
    }

    [Fact]
    public async Task Merge_ReadsGzipByContent()
    {
        var doc = await RunAsync(async merger =>
        {
            using var raw = Xml(Programme("A.ru", "Сжатое"));
            using var compressed = new MemoryStream();
            await using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                await raw.CopyToAsync(gzip);
            }

            compressed.Position = 0;
            await merger.AddSourceAsync(Source(("A.ru", "A.ru")), compressed, CancellationToken.None);
        });

        Assert.Single(doc.Root!.Elements("programme"));
    }

    [Fact]
    public async Task Merge_IgnoresDtd()
    {
        var xml = "<?xml version=\"1.0\"?><!DOCTYPE tv [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><tv>"
            + Programme("A.ru", "Ок") + "</tv>";

        var doc = await RunAsync(async merger =>
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            await merger.AddSourceAsync(Source(("A.ru", "A.ru")), content, CancellationToken.None);
        });

        Assert.Single(doc.Root!.Elements("programme"));
    }

    [Theory]
    [InlineData("20261101180000 +0300", 15)]
    [InlineData("20261101180000 -0100", 19)]
    [InlineData("20261101180000", 18)]
    public void TryParseXmltvTime_ParsesOffsets(string value, int expectedUtcHour)
    {
        Assert.True(XmltvMerger.TryParseXmltvTime(value, out var result));
        Assert.Equal(expectedUtcHour, result.UtcDateTime.Hour);
    }
}
