using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// One hosted XMLTV file and how its channel ids map to playlist channels.
/// </summary>
/// <param name="Url">Guide URL.</param>
/// <param name="IsGzip">Whether the file is gzip-compressed.</param>
/// <param name="IdMap">Channel id used inside the guide file → playlist <c>tvg-id</c> values.</param>
internal sealed record GuideSource(
    Uri Url,
    bool IsGzip,
    IReadOnlyDictionary<string, IReadOnlyList<string>> IdMap)
{
    /// <summary>
    /// Gets the number of distinct playlist channels this source can fill.
    /// </summary>
    public int Coverage { get; } = CountDistinct(IdMap);

    private static int CountDistinct(IReadOnlyDictionary<string, IReadOnlyList<string>> map)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var targets in map.Values)
        {
            set.UnionWith(targets);
        }

        return set.Count;
    }
}
