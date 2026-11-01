using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.IptvOrg.Streams;

/// <summary>
/// Remembers stream check results so that unchanged channels are not probed on every
/// rebuild. Working streams are re-checked after <see cref="AliveTtl"/>, dead ones after
/// <see cref="DeadTtl"/> (they may come back).
/// </summary>
internal sealed class StreamCheckCache
{
    /// <summary>
    /// How long a successful check is trusted (shorter than a day, so the daily sync re-checks).
    /// </summary>
    public static readonly TimeSpan AliveTtl = TimeSpan.FromHours(20);

    /// <summary>
    /// How long a failed check is trusted.
    /// </summary>
    public static readonly TimeSpan DeadTtl = TimeSpan.FromDays(3);

    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly ConcurrentDictionary<string, Entry> _entries;

    private StreamCheckCache(IEnumerable<KeyValuePair<string, Entry>> entries)
    {
        _entries = new ConcurrentDictionary<string, Entry>(entries, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the number of remembered results.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Creates an empty cache.
    /// </summary>
    /// <returns>The cache.</returns>
    public static StreamCheckCache Empty() => new(new Dictionary<string, Entry>(StringComparer.Ordinal));

    /// <summary>
    /// Loads the cache; a missing or broken file gives an empty cache.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cache.</returns>
    public static async Task<StreamCheckCache> LoadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Empty();
            }

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                var entries = await JsonSerializer.DeserializeAsync<Dictionary<string, Entry>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
                return entries is null ? Empty() : new StreamCheckCache(entries);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Empty();
        }
    }

    /// <summary>
    /// Gets a result that is still fresh.
    /// </summary>
    /// <param name="url">Stream URL.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <param name="alive">The remembered result.</param>
    /// <returns><c>true</c> when a fresh result exists.</returns>
    public bool TryGetFresh(string url, DateTime nowUtc, out bool alive)
    {
        alive = false;
        if (!_entries.TryGetValue(url, out var entry))
        {
            return false;
        }

        var ttl = entry.Alive ? AliveTtl : DeadTtl;
        if (nowUtc - entry.CheckedUtc > ttl)
        {
            return false;
        }

        alive = entry.Alive;
        return true;
    }

    /// <summary>
    /// Stores a result.
    /// </summary>
    /// <param name="url">Stream URL.</param>
    /// <param name="alive">Whether the stream works.</param>
    /// <param name="nowUtc">Check time.</param>
    public void Set(string url, bool alive, DateTime nowUtc)
    {
        _entries[url] = new Entry(alive, nowUtc);
    }

    /// <summary>
    /// Saves the cache, dropping results older than a week.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(string path, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var snapshot = _entries
            .Where(kv => nowUtc - kv.Value.CheckedUtc <= MaxAge)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var tempPath = path + ".tmp";
        var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// A remembered check result.
    /// </summary>
    /// <param name="Alive">Whether the stream worked.</param>
    /// <param name="CheckedUtc">When it was checked.</param>
    internal sealed record Entry(bool Alive, DateTime CheckedUtc);
}
