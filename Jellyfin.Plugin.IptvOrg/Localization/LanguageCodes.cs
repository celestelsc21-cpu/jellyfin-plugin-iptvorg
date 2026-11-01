using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.IptvOrg.Localization;

/// <summary>
/// Normalizes language preferences to the ISO 639-1 codes used in XMLTV files.
/// </summary>
internal static class LanguageCodes
{
    private static readonly FrozenDictionary<string, string> ThreeToTwo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["rus"] = "ru",
        ["eng"] = "en",
        ["ukr"] = "uk",
        ["bel"] = "be",
        ["kaz"] = "kk",
        ["deu"] = "de",
        ["ger"] = "de",
        ["fra"] = "fr",
        ["fre"] = "fr",
        ["spa"] = "es",
        ["ita"] = "it",
        ["tur"] = "tr",
        ["pol"] = "pl",
        ["por"] = "pt",
        ["ara"] = "ar",
        ["zho"] = "zh",
        ["chi"] = "zh",
        ["jpn"] = "ja",
        ["heb"] = "he",
        ["hye"] = "hy",
        ["arm"] = "hy",
        ["kat"] = "ka",
        ["geo"] = "ka",
        ["aze"] = "az",
        ["uzb"] = "uz",
        ["ron"] = "ro",
        ["rum"] = "ro",
        ["srp"] = "sr",
        ["bul"] = "bg",
        ["lav"] = "lv",
        ["lit"] = "lt",
        ["est"] = "et",
        ["fin"] = "fi"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes the preferences: lower case, three-letter codes mapped to two letters, duplicates removed.
    /// </summary>
    /// <param name="codes">Codes from the configuration.</param>
    /// <returns>The normalized codes, most preferred first.</returns>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? codes)
    {
        return (codes ?? [])
            .Select(c => c.Trim().ToLowerInvariant())
            .Where(c => c.Length > 0)
            .Select(c => ThreeToTwo.TryGetValue(c, out var two) ? two : c)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Decides whether generated texts (channel descriptions) are written in Russian.
    /// </summary>
    /// <param name="preferred">Normalized preferences.</param>
    /// <returns><c>true</c> unless English is preferred over Russian.</returns>
    public static bool PrefersRussian(IReadOnlyList<string> preferred)
    {
        ArgumentNullException.ThrowIfNull(preferred);

        foreach (var code in preferred)
        {
            if (string.Equals(code, "ru", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(code, "en", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
