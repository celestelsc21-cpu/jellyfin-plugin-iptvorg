using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;

namespace Jellyfin.Plugin.IptvOrg.Catalog;

/// <summary>
/// Reads the cached iptv-org datasets from disk.
/// </summary>
internal static class CatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Checks whether every dataset is present in the cache.
    /// </summary>
    /// <param name="cacheDirectory">Cache directory.</param>
    /// <returns><c>true</c> when the cache is complete.</returns>
    public static bool IsComplete(string cacheDirectory)
        => CatalogFiles.All.All(f => File.Exists(Path.Combine(cacheDirectory, f)));

    /// <summary>
    /// Loads all datasets.
    /// </summary>
    /// <param name="cacheDirectory">Cache directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="FileNotFoundException">A dataset is missing; run the sync task first.</exception>
    public static async Task<CatalogSnapshot> LoadAsync(string cacheDirectory, CancellationToken cancellationToken)
    {
        var channels = await ReadAsync<ApiChannel>(cacheDirectory, CatalogFiles.Channels, cancellationToken).ConfigureAwait(false);
        var feeds = await ReadAsync<ApiFeed>(cacheDirectory, CatalogFiles.Feeds, cancellationToken).ConfigureAwait(false);
        var streams = await ReadAsync<ApiStream>(cacheDirectory, CatalogFiles.Streams, cancellationToken).ConfigureAwait(false);
        var categories = await ReadAsync<ApiCategory>(cacheDirectory, CatalogFiles.Categories, cancellationToken).ConfigureAwait(false);
        var logos = await ReadAsync<ApiLogo>(cacheDirectory, CatalogFiles.Logos, cancellationToken).ConfigureAwait(false);
        var regions = await ReadAsync<ApiRegion>(cacheDirectory, CatalogFiles.Regions, cancellationToken).ConfigureAwait(false);
        var guides = await ReadAsync<ApiGuide>(cacheDirectory, CatalogFiles.Guides, cancellationToken).ConfigureAwait(false);

        return new CatalogSnapshot(channels, feeds, streams, categories, logos, regions, guides);
    }

    /// <summary>
    /// Loads only the categories (small file), for the settings page.
    /// </summary>
    /// <param name="cacheDirectory">Cache directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The categories, or an empty list when not downloaded yet.</returns>
    public static async Task<IReadOnlyList<ApiCategory>> LoadCategoriesAsync(string cacheDirectory, CancellationToken cancellationToken)
    {
        if (!File.Exists(Path.Combine(cacheDirectory, CatalogFiles.Categories)))
        {
            return [];
        }

        return await ReadAsync<ApiCategory>(cacheDirectory, CatalogFiles.Categories, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<T>> ReadAsync<T>(string directory, string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, fileName);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using (stream.ConfigureAwait(false))
        {
            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return items ?? [];
        }
    }
}
