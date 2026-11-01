using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.IptvOrg.Catalog;

/// <summary>
/// Downloads the iptv-org API datasets into the local cache.
/// </summary>
internal interface IIptvOrgClient
{
    /// <summary>
    /// Downloads every dataset into <paramref name="cacheDirectory"/>. Files are replaced
    /// atomically; unchanged files (same ETag) are not downloaded again.
    /// </summary>
    /// <param name="cacheDirectory">Target directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when at least one file changed.</returns>
    Task<bool> DownloadAllAsync(string cacheDirectory, CancellationToken cancellationToken);
}
