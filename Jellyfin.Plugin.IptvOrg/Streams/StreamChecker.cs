using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvOrg.Streams;

/// <summary>
/// Probes a stream with one or two small HTTP requests: the playlist and, for an HLS
/// master playlist, its first variant. Media segments are never downloaded.
/// </summary>
internal sealed class StreamChecker : IStreamChecker
{
    private const int MaxBytes = 16 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StreamChecker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamChecker"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public StreamChecker(IHttpClientFactory httpClientFactory, ILogger<StreamChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsAliveAsync(string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            // RTSP/RTP/UDP cannot be probed over HTTP; keep them.
            return true;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var first = await FetchAsync(uri, timeoutSource.Token).ConfigureAwait(false);
            if (!first.Success)
            {
                return false;
            }

            if (first.Text is null || !HlsPlaylist.IsPlaylist(first.Text))
            {
                // A non-HLS answer with data (MPEG-TS, DASH manifest...) counts as alive.
                return true;
            }

            if (!HlsPlaylist.TryGetFirstVariant(first.Text, first.FinalUri, out var variant) || variant is null)
            {
                // A media playlist: the stream itself answered.
                return true;
            }

            var second = await FetchAsync(variant, timeoutSource.Token).ConfigureAwait(false);
            return second.Success && (second.Text is null || HlsPlaylist.IsPlaylist(second.Text));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout.
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug("Stream check failed for {Host}: {Reason}", uri.Host, ex.Message);
            return false;
        }
    }

    private async Task<FetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(IptvOrgConstants.ProbeHttpClientName);
        using var response = await client
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var finalUri = response.RequestMessage?.RequestUri ?? uri;
        if (!response.IsSuccessStatusCode)
        {
            return new FetchResult(false, null, finalUri);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[MaxBytes];
            var total = 0;
            int read;
            while (total < buffer.Length
                && (read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;

                // Enough to recognize an HLS playlist and its first variant.
                if (total >= 4096 && !LooksLikeText(buffer, total))
                {
                    break;
                }
            }

            if (total == 0)
            {
                return new FetchResult(false, null, finalUri);
            }

            var text = LooksLikeText(buffer, total) ? Encoding.UTF8.GetString(buffer, 0, total) : null;
            return new FetchResult(true, text, finalUri);
        }
    }

    private static bool LooksLikeText(byte[] buffer, int length)
    {
        // HLS and DASH manifests are text; MPEG-TS starts with 0x47 and contains control bytes.
        var limit = Math.Min(length, 512);
        for (var i = 0; i < limit; i++)
        {
            var b = buffer[i];
            if (b < 0x09 || (b > 0x0D && b < 0x20 && b != 0x1B))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record FetchResult(bool Success, string? Text, Uri FinalUri);
}
