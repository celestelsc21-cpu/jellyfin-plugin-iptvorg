using System.Linq;
using System.Xml.Linq;
using Jellyfin.Plugin.IptvOrg.Guide;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Guide;

public class ProgrammeClassifierTests
{
    private static XElement Programme(string title, string? desc = null, params string[] categories)
    {
        var element = new XElement("programme", new XElement("title", title));
        if (desc is not null)
        {
            element.Add(new XElement("desc", desc));
        }

        foreach (var category in categories)
        {
            element.Add(new XElement("category", category));
        }

        return element;
    }

    private static string[] Categories(XElement programme) => programme.Elements("category").Select(c => c.Value).ToArray();

    [Fact]
    public void Film_PrefixRemovedAndMovieAdded()
    {
        var p = Programme("х/ф Ирония судьбы");

        ProgrammeClassifier.Classify(p, "general");

        Assert.Equal("Ирония судьбы", p.Element("title")!.Value);
        Assert.Contains("movie", Categories(p));
    }

    [Fact]
    public void Cartoons_GoToKids()
    {
        var film = Programme("м/ф Ну, погоди!");
        var series = Programme("м/с Смешарики");

        ProgrammeClassifier.Classify(film, null);
        ProgrammeClassifier.Classify(series, null);

        Assert.Equal(new[] { "kids" }, Categories(film));
        Assert.Equal(new[] { "kids" }, Categories(series));
        Assert.Null(series.Element("episode-num"));
    }

    [Fact]
    public void Series_WithEpisodeNumber_GetsEpisodeNum()
    {
        var p = Programme("т/с Улицы разбитых фонарей. 12-я серия", "2-й сезон.");

        ProgrammeClassifier.Classify(p, null);

        Assert.Equal("Улицы разбитых фонарей. 12-я серия", p.Element("title")!.Value);
        var episode = p.Elements("episode-num").Single(e => (string?)e.Attribute("system") == "xmltv_ns");
        Assert.Equal("1.11.", episode.Value);
        Assert.Single(p.Elements("episode-num"), e => (string?)e.Attribute("system") == "dd_progid");
    }

    [Fact]
    public void Series_WithoutNumber_StillSeries()
    {
        var p = Programme("т/с Тайны следствия");
        var doc = Programme("д/с Необыкновенные люди");
        p.SetAttributeValue("start", "20261101180000 +0300");

        ProgrammeClassifier.Classify(p, null);
        ProgrammeClassifier.Classify(doc, null);

        Assert.Equal("0.", p.Elements("episode-num").Single(e => (string?)e.Attribute("system") == "xmltv_ns").Value.TrimStart('.'));
        Assert.NotNull(doc.Element("episode-num"));
        Assert.Equal("Необыкновенные люди", doc.Element("title")!.Value);
    }

    [Fact]
    public void Series_DifferentShows_GetDifferentProgrammeIds()
    {
        var a = Programme("т/с Первый сериал. 3-я серия");
        var b = Programme("т/с Второй сериал. 3-я серия");

        ProgrammeClassifier.Classify(a, null);
        ProgrammeClassifier.Classify(b, null);

        string Id(XElement x) => x.Elements("episode-num").Single(e => (string?)e.Attribute("system") == "dd_progid").Value;
        Assert.NotEqual(Id(a), Id(b));
    }

    [Fact]
    public void ChannelCategory_SportsAndKids()
    {
        var sports = Programme("Футбол. Чемпионат России");
        var kids = Programme("Спокойной ночи, малыши!");

        ProgrammeClassifier.Classify(sports, "sports");
        ProgrammeClassifier.Classify(kids, "kids");

        Assert.Contains("sports", Categories(sports));
        Assert.Contains("kids", Categories(kids));
    }

    [Fact]
    public void RussianCategories_Mapped()
    {
        var p = Programme("Время", null, "Новости");

        ProgrammeClassifier.Classify(p, null);

        Assert.Contains("news", Categories(p));
    }

    [Fact]
    public void MovieOnSeriesChannel_IsNotSeries()
    {
        var p = Programme("х/ф Брат. 1 серия");

        ProgrammeClassifier.Classify(p, "series");

        Assert.Null(p.Element("episode-num"));
        Assert.Contains("movie", Categories(p));
    }

    [Fact]
    public void ExistingCategory_NotDuplicated()
    {
        var p = Programme("Матч", null, "sports");

        ProgrammeClassifier.Classify(p, "sports");

        Assert.Single(Categories(p));
    }
}
