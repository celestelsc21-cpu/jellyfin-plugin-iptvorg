using System.Linq;
using System.Xml.Linq;
using Jellyfin.Plugin.IptvOrg.Guide;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Guide;

public class SportsDetectorTests
{
    [Theory]
    [InlineData("Футбол. РПЛ. «Зенит» – «Спартак». Прямая трансляция")]
    [InlineData("Хоккей. КХЛ. ЦСКА - СКА")]
    [InlineData("Биатлон. Кубок мира. Спринт. Мужчины")]
    [InlineData("Формула-1. Гран-при Бразилии. Гонка")]
    [InlineData("Бокс. Бой за титул чемпиона мира")]
    [InlineData("Теннис. Australian Open. Финал")]
    [InlineData("Баскетбол. Единая лига ВТБ. 1/4 финала")]
    public void Matches_AreRecognised(string title)
    {
        Assert.True(SportsDetector.IsSportsBroadcast(title, null, null, liveOnly: false));
    }

    [Theory]
    [InlineData("Футбол. Обзор тура")]
    [InlineData("Все на Матч!")]
    [InlineData("Новости спорта")]
    [InlineData("Футбол. Лучшее")]
    [InlineData("Москва - Петушки")]
    [InlineData("Хоккей")]
    [InlineData("Футбольный клуб")]
    [InlineData("Спортлото 6 из 45")]
    [InlineData("Легенды хоккея")]
    [InlineData("Профессиональный бокс")]
    [InlineData("Футбол. Прогноз на матч")]
    public void NonMatches_AreIgnored(string title)
    {
        Assert.False(SportsDetector.IsSportsBroadcast(title, null, null, liveOnly: false));
    }

    [Fact]
    public void LiveOnly_UsesDescription()
    {
        Assert.True(SportsDetector.IsSportsBroadcast("Хоккей. КХЛ. ЦСКА - СКА", null, "Прямая трансляция", liveOnly: true));
        Assert.False(SportsDetector.IsSportsBroadcast("Хоккей. КХЛ. ЦСКА - СКА", null, "Трансляция из Москвы. Запись", liveOnly: true));
        Assert.False(SportsDetector.IsSportsBroadcast("Хоккей. КХЛ. ЦСКА - СКА", null, null, liveOnly: true));
    }

    [Fact]
    public void Classifier_MarksMatchOnGeneralChannel()
    {
        var p = new XElement("programme", new XElement("title", "Футбол. Лига чемпионов. «Реал» – «Бавария»"));

        ProgrammeClassifier.Classify(p, "general", detectSports: true);

        Assert.Contains("sports", p.Elements("category").Select(c => c.Value));
    }

    [Fact]
    public void Classifier_FilmAboutSport_IsNotSports()
    {
        var p = new XElement("programme", new XElement("title", "х/ф Легенда №17. Хоккей. СССР – Канада"));

        ProgrammeClassifier.Classify(p, "general", detectSports: true);

        Assert.DoesNotContain("sports", p.Elements("category").Select(c => c.Value));
        Assert.Contains("movie", p.Elements("category").Select(c => c.Value));
    }

    [Fact]
    public void Classifier_LiveOnly_DropsStudioOnSportsChannel()
    {
        var studio = new XElement("programme", new XElement("title", "Все на Матч!"));
        var live = new XElement("programme", new XElement("title", "Футбол. РПЛ. «Зенит» – «Спартак»"), new XElement("desc", "Прямая трансляция"));

        ProgrammeClassifier.Classify(studio, "sports", detectSports: true, liveSportsOnly: true);
        ProgrammeClassifier.Classify(live, "sports", detectSports: true, liveSportsOnly: true);

        Assert.DoesNotContain("sports", studio.Elements("category").Select(c => c.Value));
        Assert.Contains("sports", live.Elements("category").Select(c => c.Value));
    }
}
