using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Streams;
using Xunit;

namespace Jellyfin.Plugin.IptvOrg.Tests.Streams;

public class StreamCheckCacheTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryGetFresh_AliveExpiresBeforeDead()
    {
        var cache = StreamCheckCache.Empty();
        cache.Set("alive", true, Now);
        cache.Set("dead", false, Now);

        var later = Now.AddHours(21);
        Assert.False(cache.TryGetFresh("alive", later, out _));
        Assert.True(cache.TryGetFresh("dead", later, out var alive));
        Assert.False(alive);
        Assert.False(cache.TryGetFresh("dead", Now.AddDays(4), out _));
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips_AndDropsOldEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var cache = StreamCheckCache.Empty();
            cache.Set("fresh", true, Now);
            cache.Set("ancient", false, Now.AddDays(-30));
            await cache.SaveAsync(path, Now, CancellationToken.None);

            var loaded = await StreamCheckCache.LoadAsync(path, CancellationToken.None);

            Assert.Equal(1, loaded.Count);
            Assert.True(loaded.TryGetFresh("fresh", Now, out var alive));
            Assert.True(alive);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_BrokenFile_GivesEmptyCache()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            await File.WriteAllTextAsync(path, "{not json");

            var loaded = await StreamCheckCache.LoadAsync(path, CancellationToken.None);

            Assert.Equal(0, loaded.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
