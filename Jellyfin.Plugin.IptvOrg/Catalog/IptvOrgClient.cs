using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvOrg.Catalog;

/// <summary>
/// Downloads iptv-org API files with ETag revalidation, retries and atomic replacement.
/// </summary>
internal sealed class IptvOrgClient : IIptvOrgClient
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IptvOrgClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IptvOrgClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public IptvOrgClient(IHttpClientFactory httpClientFactory, ILogger<IptvOrgClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> DownloadAllAsync(string cacheDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(cacheDirectory);

        var changed = false;
        foreach (var file in CatalogFiles.All)
        {
            if (await DownloadFileAsync(cacheDirectory, file, cancellationToken).ConfigureAwait(false))
            {
                changed = true;
            }
        }

        return changed;
    }

    private async Task<bool> DownloadFileAsync(string cacheDirectory, string fileName, CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(cacheDirectory, fileName);
        var etagPath = targetPath + ".etag";
        var tempPath = targetPath + ".tmp";

        string? etag = null;
        if (File.Exists(targetPath) && File.Exists(etagPath))
        {
            etag = (await File.ReadAllTextAsync(etagPath, cancellationToken).ConfigureAwait(false)).Trim();
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(IptvOrgConstants.ApiBaseUri, fileName));
                if (!string.IsNullOrEmpty(etag) && EntityTagHeaderValue.TryParse(etag, out var tag))
                {
                    request.Headers.IfNoneMatch.Add(tag);
                }

                var client = _httpClientFactory.CreateClient(IptvOrgConstants.HttpClientName);
                using var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    _logger.LogDebug("iptv-org {File} is unchanged", fileName);
                    return false;
                }

                if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                {
                    var delay = GetRetryDelay(response, attempt);
                    _logger.LogWarning(
                        "iptv-org {File} returned {StatusCode}, retrying in {Delay}",
                        fileName,
                        (int)response.StatusCode,
                        delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                {
                    var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                    await using (output.ConfigureAwait(false))
                    {
                        await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (!await LooksLikeJsonArrayAsync(tempPath, cancellationToken).ConfigureAwait(false))
                {
                    File.Delete(tempPath);
                    throw new InvalidDataException($"iptv-org {fileName} is not a JSON array.");
                }

                File.Move(tempPath, targetPath, overwrite: true);

                var newTag = response.Headers.ETag?.ToString();
                if (string.IsNullOrEmpty(newTag))
                {
                    File.Delete(etagPath);
                }
                else
                {
                    await File.WriteAllTextAsync(etagPath, newTag, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation("Downloaded iptv-org {File}", fileName);
                return true;
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex, "Download of iptv-org {File} failed, retrying in {Delay}", fileName, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < MaxAttempts)
            {
                // HttpClient timeout.
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex, "Download of iptv-org {File} timed out, retrying in {Delay}", fileName, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        var delay = retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
        return delay > MaxRetryDelay ? MaxRetryDelay : delay;
    }

    private static async Task<bool> LooksLikeJsonArrayAsync(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[64];
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < read; i++)
            {
                var b = buffer[i];

                // Skip a UTF-8 BOM and whitespace.
                if (b is 0xEF or 0xBB or 0xBF or (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                {
                    continue;
                }

                return b == (byte)'[';
            }

            return false;
        }
    }
}
