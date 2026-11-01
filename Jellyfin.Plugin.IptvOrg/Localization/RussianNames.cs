using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Jellyfin.Plugin.IptvOrg.Localization;

/// <summary>
/// Russian names of countries, languages and regions (embedded resource generated from
/// Unicode CLDR data; regions translated by hand).
/// </summary>
internal static class RussianNames
{
    private static readonly Lazy<NameTables> Tables = new(Load);

    /// <summary>
    /// Gets the Russian country name, or <c>null</c>.
    /// </summary>
    /// <param name="code">ISO 3166-1 alpha-2 code (iptv-org uses <c>UK</c> for the United Kingdom).</param>
    /// <returns>The name.</returns>
    public static string? Country(string code) => Lookup(Tables.Value.Countries, code);

    /// <summary>
    /// Gets the Russian language name, or <c>null</c>.
    /// </summary>
    /// <param name="code">ISO 639-3 code.</param>
    /// <returns>The name.</returns>
    public static string? Language(string code) => Lookup(Tables.Value.Languages, code);

    /// <summary>
    /// Gets the Russian region name, or <c>null</c>.
    /// </summary>
    /// <param name="code">iptv-org region code.</param>
    /// <returns>The name.</returns>
    public static string? Region(string code) => Lookup(Tables.Value.Regions, code);

    private static string? Lookup(Dictionary<string, string> table, string code)
        => table.TryGetValue(code, out var name) ? name : null;

    private static NameTables Load()
    {
        var assembly = typeof(RussianNames).Assembly;
        using var stream = assembly.GetManifestResourceStream(typeof(RussianNames).Namespace + ".names-ru.json");
        if (stream is null)
        {
            return new NameTables();
        }

        var tables = JsonSerializer.Deserialize<NameTables>(stream) ?? new NameTables();
        return new NameTables
        {
            Countries = new Dictionary<string, string>(tables.Countries, StringComparer.OrdinalIgnoreCase),
            Languages = new Dictionary<string, string>(tables.Languages, StringComparer.OrdinalIgnoreCase),
            Regions = new Dictionary<string, string>(tables.Regions, StringComparer.OrdinalIgnoreCase)
        };
    }

#pragma warning disable CA2227 // Collection properties should be read only (JSON deserialization target).
    private sealed class NameTables
    {
        [System.Text.Json.Serialization.JsonPropertyName("countries")]
        public Dictionary<string, string> Countries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [System.Text.Json.Serialization.JsonPropertyName("languages")]
        public Dictionary<string, string> Languages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [System.Text.Json.Serialization.JsonPropertyName("regions")]
        public Dictionary<string, string> Regions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
#pragma warning restore CA2227
}
