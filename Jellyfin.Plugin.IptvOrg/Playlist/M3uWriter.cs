using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// Writes playlist entries in the M3U dialect read by Jellyfin's M3U tuner.
/// </summary>
internal static class M3uWriter
{
    private const string NoBreakSpace = "\u00A0";

    /// <summary>
    /// Renders the playlist.
    /// </summary>
    /// <param name="entries">Entries.</param>
    /// <returns>The M3U text.</returns>
    public static string Write(IReadOnlyList<PlaylistEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sb = new StringBuilder(entries.Count * 200);
        sb.Append("#EXTM3U\n");

        foreach (var entry in entries)
        {
            sb.Append("#EXTINF:-1");
            AppendAttribute(sb, "tvg-id", entry.TvgId);
            AppendAttribute(sb, "tvg-chno", entry.Number.ToString(CultureInfo.InvariantCulture));
            AppendAttribute(sb, "tvg-logo", entry.LogoUrl);
            AppendAttribute(sb, "group-title", entry.CategoryName);
            sb.Append(',').Append(SanitizeName(entry.Name)).Append('\n');
            sb.Append(SingleLine(entry.Url)).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Adds an EPG URL to the M3U header for external IPTV players.
    /// The on-disk playlist used by Jellyfin remains unchanged.
    /// </summary>
    /// <param name="playlist">Generated M3U playlist.</param>
    /// <param name="guideUrl">Absolute URL of the XMLTV guide.</param>
    /// <returns>The playlist with both common EPG header attributes.</returns>
    internal static string WithGuideUrl(string playlist, string guideUrl)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(guideUrl);

        if (!playlist.StartsWith("#EXTM3U\n", StringComparison.Ordinal))
        {
            throw new ArgumentException("The playlist must start with #EXTM3U.", nameof(playlist));
        }

        if (!Uri.TryCreate(guideUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The guide URL must use HTTP or HTTPS.", nameof(guideUrl));
        }

        var url = uri.AbsoluteUri;
        return "#EXTM3U url-tvg=\"" + url + "\" x-tvg-url=\"" + url + "\""
            + playlist["#EXTM3U".Length..];
    }

    /// <summary>
    /// Makes a channel name safe for Jellyfin's parser: it takes the text after the last
    /// comma, and drops a leading number followed by a space ("24 Канал" would become "Канал").
    /// </summary>
    /// <param name="name">Channel name.</param>
    /// <returns>The sanitized name.</returns>
    internal static string SanitizeName(string name)
    {
        var value = SingleLine(name).Replace(',', ' ').Trim();
        while (value.Contains("  ", StringComparison.Ordinal))
        {
            value = value.Replace("  ", " ", StringComparison.Ordinal);
        }

        var space = value.IndexOf(' ', StringComparison.Ordinal);
        if (space > 0)
        {
            var head = value.AsSpan(0, space).Trim(" .");
            if (double.TryParse(head, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                // A no-break space keeps the number part of the name.
                value = string.Concat(value.AsSpan(0, space), NoBreakSpace, value.AsSpan(space + 1));
            }
        }

        return value.Length == 0 ? "?" : value;
    }

    private static void AppendAttribute(StringBuilder sb, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        // Jellyfin reads attributes with name="[^"]+", so quotes cannot be escaped: drop them.
        var clean = SingleLine(value).Replace("\"", string.Empty, StringComparison.Ordinal).Trim();
        if (clean.Length == 0)
        {
            return;
        }

        sb.Append(' ').Append(name).Append("=\"").Append(clean).Append('"');
    }

    private static string SingleLine(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
