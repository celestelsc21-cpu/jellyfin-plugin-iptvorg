using System;
using System.Collections.Generic;
using System.IO;
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

public class RussianNamesTests
{
    [Theory]
    [InlineData("<channel id=\"x\"><display-name lang=\"en\">Channel One</display-name><display-name lang=\"ru\">Первый канал</display-name></channel>", "Первый канал")]
    [InlineData("<channel id=\"x\"><display-name>Россия 1</display-name></channel>", "Россия 1")]
    [InlineData("<channel id=\"x\"><display-name lang=\"uk\">Перший</display-name></channel>", null)]
    [InlineData("<channel id=\"x\"><display-name>Euronews</display-name></channel>", null)]
    public void FindRussianName_PrefersRussian(string xml, string? expected)
    {
        Assert.Equal(expected, XmltvMerger.FindRussianName(XElement.Parse(xml)));
    }

    [Fact]
    public void Apply_ReplacesNameAndKeepsPrefix()
    {
        var entries = new[]
        {
            new PlaylistEntry("One.ru@HD", "One.ru", "HD", "[Общие] Channel One HD", "https://s/1", null, "general", "Общие", 1),
            new PlaylistEntry("Two.ru", "Two.ru", null, "[Общие] Two", "https://s/2", null, "general", "Общие", 2)
        };
        var names = new Dictionary<string, string> { ["One.ru@HD"] = "Первый канал HD" };

        var result = ChannelNames.Apply(entries, names, prefixCategory: true);

        Assert.Equal("[Общие] Первый канал HD", result[0].Name);
        Assert.Equal("[Общие] Two", result[1].Name);
    }

    [Fact]
    public async Task Merger_CollectsNamesForMappedChannels()
    {
        var xml = "<?xml version=\"1.0\"?><tv>"
            + "<channel id=\"perviy\"><display-name lang=\"ru\">Первый канал</display-name></channel>"
            + "<channel id=\"other\"><display-name lang=\"ru\">Чужой</display-name></channel>"
            + "</tv>";
        var entries = new[] { new PlaylistEntry("ChannelOne.ru", "ChannelOne.ru", null, "Channel One", "https://s/1", null, "general", "Общие", 1) };
        var source = new GuideSource(
            new Uri("https://g.example/epg.xml"),
            false,
            new Dictionary<string, IReadOnlyList<string>> { ["perviy"] = new[] { "ChannelOne.ru" } });

        using var output = new MemoryStream();
        var writer = XmlWriter.Create(output, new XmlWriterSettings { Async = true });
        await using (writer)
        {
            var merger = new XmltvMerger(writer, entries, DateTimeOffset.UtcNow);
            await merger.WriteHeaderAsync();
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            await merger.AddSourceAsync(source, content, CancellationToken.None);
            await merger.WriteFooterAsync();

            Assert.Equal("Первый канал", merger.RussianNames["ChannelOne.ru"]);
            Assert.Single(merger.RussianNames);
        }
    }
}
