using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.IptvOrg.Streams;

/// <summary>
/// Checks whether a stream URL currently answers like a stream.
/// </summary>
internal interface IStreamChecker
{
    /// <summary>
    /// Probes a stream.
    /// </summary>
    /// <param name="url">Stream URL.</param>
    /// <param name="timeout">Time limit for the whole check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the stream works or cannot be checked (non-HTTP).</returns>
    Task<bool> IsAliveAsync(string url, TimeSpan timeout, CancellationToken cancellationToken);
}
