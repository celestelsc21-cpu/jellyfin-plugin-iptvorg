using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Adds the genre markers Jellyfin needs for its "Movies", "Series", "Sports", "Kids" and
/// "News" sections. Jellyfin only recognises the English XMLTV categories <c>movie</c>,
/// <c>sports</c>, <c>kids</c>, <c>news</c> and treats a programme as a series only when it
/// has an episode number; Russian guides instead mark programmes with title prefixes such as
/// <c>х/ф</c> (film) or <c>т/с</c> (TV series) and rarely carry categories.
/// </summary>
internal static partial class ProgrammeClassifier
{
    [Flags]
    private enum Kinds
    {
        None = 0,
        Movie = 1,
        Series = 2,
        Kids = 4,
        Sports = 8,
        News = 16
    }

    /// <summary>
    /// Classifies one programme in place: strips the Russian genre prefix from the title, adds
    /// the English categories Jellyfin matches, and an episode number when the text names one.
    /// </summary>
    /// <param name="programme">Programme element.</param>
    /// <param name="channelCategory">iptv-org category of the channel (for example <c>kids</c>), or <c>null</c>.</param>
    public static void Classify(XElement programme, string? channelCategory)
    {
        ArgumentNullException.ThrowIfNull(programme);

        var kind = Kinds.None;

        var title = programme.Element("title");
        if (title is not null)
        {
            var match = PrefixRegex().Match(title.Value);
            if (match.Success)
            {
                kind |= PrefixKind(match.Groups["p"].Value);
                var rest = title.Value[match.Length..].Trim();
                if (rest.Length > 0)
                {
                    title.Value = rest;
                }
            }
        }

        foreach (var category in programme.Elements("category"))
        {
            kind |= CategoryKind(category.Value);
        }

        kind |= ChannelKind(channelCategory);

        // A film is not a series even on a series channel.
        if ((kind & Kinds.Movie) != 0)
        {
            kind &= ~Kinds.Series;
        }

        var existing = new HashSet<string>(
            programme.Elements("category").Select(c => c.Value.Trim()),
            StringComparer.OrdinalIgnoreCase);
        AddCategory(programme, existing, kind, Kinds.Movie, "movie");
        AddCategory(programme, existing, kind, Kinds.Kids, "kids");
        AddCategory(programme, existing, kind, Kinds.Sports, "sports");
        AddCategory(programme, existing, kind, Kinds.News, "news");

        if ((kind & Kinds.Series) != 0 && programme.Element("episode-num") is null)
        {
            var text = (title?.Value ?? string.Empty) + " " + (programme.Element("sub-title")?.Value ?? string.Empty) + " " + (programme.Element("desc")?.Value ?? string.Empty);
            var episode = FindNumber(EpisodeRegex(), text);
            if (episode is > 0)
            {
                var season = FindNumber(SeasonRegex(), text);
                var seasonIndex = season is > 0 ? (season.Value - 1).ToString(CultureInfo.InvariantCulture) : string.Empty;
                var value = string.Create(CultureInfo.InvariantCulture, $"{seasonIndex}.{episode.Value - 1}.");
                programme.Add(new XElement("episode-num", new XAttribute("system", "xmltv_ns"), value));
            }
        }
    }

    private static void AddCategory(XElement programme, HashSet<string> existing, Kinds kind, Kinds flag, string category)
    {
        if ((kind & flag) != 0 && existing.Add(category))
        {
            programme.Add(new XElement("category", new XAttribute("lang", "en"), category));
        }
    }

    private static Kinds PrefixKind(string prefix)
    {
        var p = prefix.ToLowerInvariant().Replace('.', '/').Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('/');
        return p switch
        {
            "х/ф" or "к/ф" or "фильм" => Kinds.Movie,
            "м/ф" or "мультфильм" => Kinds.Movie | Kinds.Kids,
            "т/с" or "сериал" or "телесериал" or "д/с" => Kinds.Series,
            "м/с" or "мультсериал" => Kinds.Series | Kinds.Kids,
            _ => Kinds.None
        };
    }

    private static Kinds CategoryKind(string category)
    {
        var c = category.Trim().ToLowerInvariant();
        if (c.Length == 0)
        {
            return Kinds.None;
        }

        var kind = Kinds.None;
        if (c.Contains("мульт", StringComparison.Ordinal) || c.Contains("дет", StringComparison.Ordinal) || c is "kids" or "children")
        {
            kind |= Kinds.Kids;
        }

        if (c.Contains("сериал", StringComparison.Ordinal) || c is "series")
        {
            kind |= Kinds.Series;
        }
        else if (c.Contains("фильм", StringComparison.Ordinal) || c.Contains("кино", StringComparison.Ordinal) || c is "movie" or "film")
        {
            kind |= Kinds.Movie;
        }

        if (c.Contains("спорт", StringComparison.Ordinal) || c is "sport" or "sports")
        {
            kind |= Kinds.Sports;
        }

        if (c.Contains("новост", StringComparison.Ordinal) || c is "news")
        {
            kind |= Kinds.News;
        }

        return kind;
    }

    private static Kinds ChannelKind(string? channelCategory) => channelCategory?.ToLowerInvariant() switch
    {
        "kids" or "animation" => Kinds.Kids,
        "sports" => Kinds.Sports,
        "news" => Kinds.News,
        "movies" => Kinds.Movie,
        "series" => Kinds.Series,
        _ => Kinds.None
    };

    private static int? FindNumber(Regex regex, string text)
    {
        var match = regex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var group = match.Groups["n1"].Success ? match.Groups["n1"] : match.Groups["n2"];
        return int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is > 0 and < 10000 ? n : null;
    }

    [GeneratedRegex(@"^\s*(?<p>[хкмтд]\s*[/.]\s*[фс]\.?|мультфильм|мультсериал|телесериал|сериал|фильм)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrefixRegex();

    [GeneratedRegex(@"(?<n1>\d{1,4})\s*-?\s*(?:я|й|ая)?\s+серия|серия\s*№?\s*(?<n2>\d{1,4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeRegex();

    [GeneratedRegex(@"(?<n1>\d{1,3})\s*-?\s*(?:й|ый)?\s+сезон|сезон\s*№?\s*(?<n2>\d{1,3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonRegex();
}
