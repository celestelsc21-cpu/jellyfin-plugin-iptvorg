using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// Catalog sync, playlist and guide generation, and Live TV registration.
/// All operations are serialized: only one runs at a time.
/// </summary>
public interface IIptvOrgService
{
    /// <summary>
    /// Gets the generated playlist path.
    /// </summary>
    string PlaylistPath { get; }

    /// <summary>
    /// Gets the generated guide path.
    /// </summary>
    string GuidePath { get; }

    /// <summary>
    /// Gets a value indicating whether the catalog has been downloaded.
    /// </summary>
    bool HasCatalog { get; }

    /// <summary>
    /// Downloads the iptv-org catalog and rebuilds the playlist.
    /// </summary>
    /// <param name="progress">Progress (0–100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    Task SyncAsync(IProgress<double> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Rebuilds the playlist from the cached catalog (after a settings change).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    Task RebuildPlaylistAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Builds the merged guide from the hosted guide files.
    /// </summary>
    /// <param name="progress">Progress (0–100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    Task UpdateGuideAsync(IProgress<double> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the M3U tuner and the XMLTV guide source to Live TV, if missing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registration result.</returns>
    Task<LiveTvRegistration> RegisterInLiveTvAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current Live TV registration state.
    /// </summary>
    /// <returns>The registration state.</returns>
    LiveTvRegistration GetRegistration();

    /// <summary>
    /// Gets the persisted status.
    /// </summary>
    /// <returns>The status.</returns>
    PluginStatus GetStatus();

    /// <summary>
    /// Gets the categories known from the catalog, with Russian names and channel counts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The categories in catalog order.</returns>
    Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken cancellationToken);
}
