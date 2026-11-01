using System;

namespace Jellyfin.Plugin.IptvOrg.Streams;

/// <summary>
/// Minimal HLS playlist inspection used by the stream check.
/// </summary>
internal static class HlsPlaylist
{
    /// <summary>
    /// Checks whether a text is an HLS playlist.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <returns><c>true</c> when it starts with <c>#EXTM3U</c>.</returns>
    public static bool IsPlaylist(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.TrimStart('﻿', ' ', '\t', '\r', '\n').StartsWith("#EXTM3U", StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds the first variant of a master playlist.
    /// </summary>
    /// <param name="text">Playlist text.</param>
    /// <param name="baseUri">URL the playlist was loaded from (after redirects).</param>
    /// <param name="variant">The absolute variant URL.</param>
    /// <returns><c>true</c> for a master playlist with at least one variant.</returns>
    public static bool TryGetFirstVariant(string text, Uri baseUri, out Uri? variant)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(baseUri);

        variant = null;
        var expectUri = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal))
            {
                expectUri = true;
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            if (expectUri)
            {
                return Uri.TryCreate(baseUri, line, out variant);
            }
        }

        return false;
    }
}
