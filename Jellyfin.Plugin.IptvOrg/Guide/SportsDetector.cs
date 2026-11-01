using System;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Recognises sports broadcasts (matches, races, fights) on any channel from the programme
/// text. Deliberately strict, so that films, documentaries and talk shows about sport stay out:
/// <list type="number">
/// <item>the title or sub-title names a sport or a competition;</item>
/// <item>the title or sub-title shows that this is the event itself: two opponents separated by
/// a dash ("Зенит" – "Спартак"), or a word such as "матч", "финал", "тур", "гонка", "бой";</item>
/// <item>no exclusion word: review, news, highlights, history, studio, preview, lottery…</item>
/// </list>
/// The description is used only to see whether the broadcast is live.
/// </summary>
internal static partial class SportsDetector
{
    /// <summary>
    /// Checks whether a programme is a sports broadcast.
    /// </summary>
    /// <param name="title">Title (without the genre prefix).</param>
    /// <param name="subTitle">Sub-title, if any.</param>
    /// <param name="description">Description, if any.</param>
    /// <param name="liveOnly">Whether only live broadcasts count.</param>
    /// <returns><c>true</c> for a sports broadcast.</returns>
    public static bool IsSportsBroadcast(string? title, string? subTitle, string? description, bool liveOnly)
    {
        var head = ((title ?? string.Empty) + " " + (subTitle ?? string.Empty)).Trim();
        if (head.Length == 0)
        {
            return false;
        }

        if (!SportRegex().IsMatch(head) && !CompetitionRegex().IsMatch(head))
        {
            return false;
        }

        if (!OpponentsRegex().IsMatch(head) && !EventWordRegex().IsMatch(head))
        {
            return false;
        }

        if (ExclusionRegex().IsMatch(head))
        {
            return false;
        }

        return !liveOnly || IsLive(head + " " + (description ?? string.Empty));
    }

    /// <summary>
    /// Checks whether the text says the broadcast is live.
    /// </summary>
    /// <param name="text">Programme text.</param>
    /// <returns><c>true</c> for a live broadcast.</returns>
    public static bool IsLive(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return LiveRegex().IsMatch(text) && !RecordedRegex().IsMatch(text);
    }

    [GeneratedRegex(
        @"(?<!\p{L})(футбол|мини-футбол|хокке|баскетбол|волейбол|гандбол|теннис|настольн\w* теннис|биатлон|лыжн\w* гонк|лыжн\w* спорт|фигурн\w* катани|бокс|ufc|mma|смешанн\w* единоборств|единоборств|кикбоксинг|борьб|дзюдо|самбо|формул\w*[- ]?1|f1|motogp|мотогп|наскар|nascar|ралли|керлинг|регби|снукер|бильярд|дартс|лёгк\w* атлетик|легк\w* атлетик|плавани|гимнастик|велоспорт|велогонк|конькобежн|шорт-трек|бобслей|скелетон|санн\w* спорт|прыжки с трамплин|сноуборд|фристайл|водное поло|пляжн\w* волейбол|киберспорт)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SportRegex();

    [GeneratedRegex(
        @"(?<!\p{L})(рпл|кхл|нхл|нба|вхл|мхл|фнл|втб|единая лига|евролига|лига чемпионов|лига европы|лига конференций|лига наций|премьер-лига|ла лига|серия а|бундеслига|лига 1|чемпионат (мира|европы|россии|англии|испании|италии|германии|франции|кхл)|кубок (мира|россии|европы|гагарина|англии|испании|италии|германии|франции|стэнли|дэвиса)|олимпи|паралимпи|гран-при|grand prix|уимблдон|ролан гаррос|us open|australian open)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompetitionRegex();

    // Two capitalised (or quoted) names separated by a spaced dash: «Зенит» – «Спартак», ЦСКА — СКА.
    [GeneratedRegex(
        @"[«""]?[\p{Lu}\d][^–—\-«»""]{0,40}[»""]?\s[–—-]\s[«""]?[\p{Lu}\d]",
        RegexOptions.CultureInvariant)]
    private static partial Regex OpponentsRegex();

    [GeneratedRegex(
        @"(?<!\p{L})(матч|финал|полуфинал|четвертьфинал|1/\d{1,2} финала|плей-офф|\d+-?й тур|тур\s?\d+|игра \d|гонк[аи]|спринт|эстафет|масс-старт|бой|поединок|этап|квалификаци|заезд|трансляци|прямой эфир)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EventWordRegex();

    [GeneratedRegex(
        @"(?<!\p{L})(обзор|новости|итоги|лучшее|легенд|истори|документальн|д/ф|д/с|х/ф|т/с|м/ф|м/с|фильм|сериал|ток-шоу|студия|все на матч|после футбола|прогноз|разбор|превью|анонс|предматчев|специальн\w* репортаж|репортаж|интервью|спортлото|лото|викторин|магазин|программа передач|неделя спорта|культ тура|тренировк)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExclusionRegex();

    [GeneratedRegex(@"(?<!\p{L})(прям\w*\s+(трансляци|эфир)|live(?!\p{L}))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LiveRegex();

    [GeneratedRegex(@"(?<!\p{L})(в записи|запись(?!\p{L})|повтор(?!\p{L})|трансляция из)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecordedRegex();
}
