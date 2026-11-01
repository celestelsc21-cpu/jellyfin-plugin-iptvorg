using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.IptvOrg.Playlist;

/// <summary>
/// Replaces channel names with the Russian names found in the program guide.
/// </summary>
internal static class ChannelNames
{
    /// <summary>
    /// Applies the names.
    /// </summary>
    /// <param name="entries">Playlist entries.</param>
    /// <param name="russianNames">Russian names by <c>tvg-id</c>.</param>
    /// <param name="prefixCategory">Whether names carry the <c>[Category]</c> prefix.</param>
    /// <returns>The entries with Russian names where known.</returns>
    public static IReadOnlyList<PlaylistEntry> Apply(
        IReadOnlyList<PlaylistEntry> entries,
        IReadOnlyDictionary<string, string> russianNames,
        bool prefixCategory)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(russianNames);

        if (russianNames.Count == 0)
        {
            return entries;
        }

        return entries
            .Select(e =>
            {
                if (e.TvgId is null || !russianNames.TryGetValue(e.TvgId, out var russian) || string.IsNullOrWhiteSpace(russian))
                {
                    return e;
                }

                var name = prefixCategory ? $"[{e.CategoryName}] {russian.Trim()}" : russian.Trim();
                return e with { Name = name };
            })
            .ToArray();
    }
}
