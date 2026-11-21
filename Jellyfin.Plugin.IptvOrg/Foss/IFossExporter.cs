using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.IptvOrg.Foss;

/// <summary>
/// Provides the separately generated OTT-play FOSS playlist and EPG.
/// </summary>
public interface IFossExporter
{
    /// <summary>Gets the generated FOSS M3U path.</summary>
    string PlaylistPath { get; }

    /// <summary>Gets the folder with per-channel FOSS JSON guide files.</summary>
    string EpgDirectoryPath { get; }

    /// <summary>
    /// Creates the separate FOSS files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of covered channels or null if already running.</returns>
    Task<int?> BuildAsync(CancellationToken cancellationToken);
}
