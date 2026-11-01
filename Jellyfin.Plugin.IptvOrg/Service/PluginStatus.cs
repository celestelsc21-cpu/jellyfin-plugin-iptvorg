using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// Persisted results of the last sync, playlist build and guide update.
/// </summary>
public sealed class PluginStatus
{
    /// <summary>Gets or sets the time of the last successful catalog download.</summary>
    public DateTime? LastSyncUtc { get; set; }

    /// <summary>Gets or sets the time of the last playlist build.</summary>
    public DateTime? LastPlaylistUtc { get; set; }

    /// <summary>Gets or sets the number of channels in the playlist.</summary>
    public int ChannelCount { get; set; }

    /// <summary>Gets or sets the number of channels per category in the whole catalog.</summary>
    public IReadOnlyDictionary<string, int> CatalogCategoryCounts { get; set; } = new Dictionary<string, int>();

    /// <summary>Gets or sets a value indicating whether streams were checked in the last build.</summary>
    public bool StreamCheckEnabled { get; set; }

    /// <summary>Gets or sets the number of streams probed in the last build (cached results excluded).</summary>
    public int StreamsChecked { get; set; }

    /// <summary>Gets or sets the number of dead streams found in the last build.</summary>
    public int DeadStreams { get; set; }

    /// <summary>Gets or sets the number of channels left out because no stream worked.</summary>
    public int DroppedChannels { get; set; }

    /// <summary>Gets or sets the time of the last guide update.</summary>
    public DateTime? LastGuideUtc { get; set; }

    /// <summary>Gets or sets the number of playlist channels that have guide data.</summary>
    public int GuideChannelCount { get; set; }

    /// <summary>Gets or sets the number of programmes in the guide.</summary>
    public int GuideProgrammeCount { get; set; }

    /// <summary>Gets or sets the number of guide files that were read.</summary>
    public int GuideSourcesUsed { get; set; }

    /// <summary>Gets or sets the number of guide files that failed.</summary>
    public int GuideSourcesFailed { get; set; }

    /// <summary>Gets or sets the last error, if any (no secrets or user paths).</summary>
    public string? LastError { get; set; }
}
