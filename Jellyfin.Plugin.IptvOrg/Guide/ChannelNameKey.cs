using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Turns a channel name into a comparison key, so that "Belarus-1", "Беларусь 1" and
/// "Беларусь 1 HD" give the same key: Cyrillic is transliterated, quality marks (HD, SD, 4K)
/// and punctuation are dropped, common transliteration variants are folded (kh/h, iy/yy/i,
/// ts/c, ya/a…) and a trailing "TV"/"channel" is removed.
/// </summary>
internal static partial class ChannelNameKey
{
    private static readonly FrozenDictionary<char, string> Transliteration = new Dictionary<char, string>
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e",
        ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
        ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = string.Empty,
        ['ы'] = "y", ['ь'] = string.Empty, ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",

        // Ukrainian and Belarusian letters.
        ['і'] = "i", ['ї'] = "i", ['є'] = "e", ['ґ'] = "g", ['ў'] = "u"
    }.ToFrozenDictionary();

    // Applied in this order after transliteration.
    private static readonly (string From, string To)[] Folds =
    [
        ("kh", "h"), ("iy", "i"), ("yy", "i"), ("ij", "i"), ("yi", "i"), ("j", "y"), ("ts", "c"),
        ("w", "v"), ("x", "ks"), ("ph", "f"), ("ie", "e"), ("ye", "e"), ("yu", "u"), ("ya", "a")
    ];

    /// <summary>
    /// Builds the comparison key of a name.
    /// </summary>
    /// <param name="name">Channel name.</param>
    /// <returns>The key; empty when nothing comparable is left.</returns>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var lower = name.ToLowerInvariant();
        var builder = new StringBuilder(lower.Length * 2);
        foreach (var ch in lower)
        {
            if (ch == '&')
            {
                builder.Append("and");
            }
            else if (Transliteration.TryGetValue(ch, out var latin))
            {
                builder.Append(latin);
            }
            else
            {
                builder.Append(ch);
            }
        }

        var key = QualityRegex().Replace(builder.ToString(), " ");
        key = NonAlphanumericRegex().Replace(key, string.Empty);
        foreach (var (from, to) in Folds)
        {
            key = key.Replace(from, to, StringComparison.Ordinal);
        }

        return SuffixRegex().Replace(key, string.Empty);
    }

    /// <summary>
    /// Checks whether a guide channel name denotes a time-shifted copy, such as
    /// "Россия 1 (+4)" or "Мир +2".
    /// </summary>
    /// <param name="name">Name.</param>
    /// <returns><c>true</c> for a time-shifted copy.</returns>
    public static bool IsTimeshiftName(string? name)
        => !string.IsNullOrEmpty(name) && TimeshiftRegex().IsMatch(name);

    [GeneratedRegex(@"(?<![a-z0-9])(hd|sd|uhd|fhd|4k)(?![a-z0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex QualityRegex();

    [GeneratedRegex("[^a-z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("(tv|tele|kanal|channel)$", RegexOptions.CultureInvariant)]
    private static partial Regex SuffixRegex();

    [GeneratedRegex(@"\(\s*[+-]\d+\s*\)|\s[+-]\d+(?!\w)", RegexOptions.CultureInvariant)]
    private static partial Regex TimeshiftRegex();
}
