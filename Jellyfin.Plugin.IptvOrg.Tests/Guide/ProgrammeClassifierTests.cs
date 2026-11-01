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
    public void Cartoon_IsMovieAndKids()
    {
        var p = Programme("м/ф Ну, погоди!");

        ProgrammeClassifier.Classify(p, null);

        Assert.Equal(new[] { "movie", "kids" }, Categories(p));
    }

    [Fact]
    public void Series_WithEpisodeNumber_GetsEpisodeNum()
    {
        var p = Programme("т/с Улицы разбитых фонарей. 12-я серия", "2-й сезон.");

        ProgrammeClassifier.Classify(p, null);

        Assert.Equal("Улицы разбитых фонарей. 12-я серия", p.Element("title")!.Value);
        var episode = p.Element("episode-num")!;
        Assert.Equal("xmltv_ns", (string?)episode.Attribute("system"));
        Assert.Equal("1.11.", episode.Value);
    }

    [Fact]
    public void Series_WithoutNumber_NoFakeEpisode()
    {
        var p = Programme("т/с Тайны следствия");

        ProgrammeClassifier.Classify(p, null);

        Assert.Null(p.Element("episode-num"));
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
