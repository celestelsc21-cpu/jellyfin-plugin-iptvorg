using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.IptvOrg.Configuration;
using Jellyfin.Plugin.IptvOrg.Playlist;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// Writes channel descriptions into Jellyfin's Live TV channel items. Neither the M3U tuner
/// nor XMLTV carries a channel description, so after every Jellyfin "Refresh Guide" run the
/// plugin sets the <c>Overview</c> of its own channels (Jellyfin's guide refresh leaves it alone).
/// </summary>
internal sealed class ChannelMetadataService : IHostedService, IDisposable
{
    private readonly ITaskManager _taskManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IPluginConfigurationAccessor _config;
    private readonly PluginPaths _paths;
    private readonly ILogger<ChannelMetadataService> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelMetadataService"/> class.
    /// </summary>
    /// <param name="taskManager">Task manager.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="config">Configuration accessor.</param>
    /// <param name="paths">Plugin paths.</param>
    /// <param name="logger">Logger.</param>
    public ChannelMetadataService(
        ITaskManager taskManager,
        ILibraryManager libraryManager,
        IPluginConfigurationAccessor config,
        PluginPaths paths,
        ILogger<ChannelMetadataService> logger)
    {
        _taskManager = taskManager;
        _libraryManager = libraryManager;
        _config = config;
        _paths = paths;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _taskManager.TaskCompleted += OnTaskCompleted;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _taskManager.TaskCompleted -= OnTaskCompleted;
        await _stopping.CancelAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// Builds the channel item external id Jellyfin's M3U tuner uses:
    /// <c>m3u_</c> + MD5 of the playlist path + MD5 of the stream URL (see M3UTunerHost / M3uParser).
    /// </summary>
    /// <param name="playlistPath">Playlist path the tuner reads.</param>
    /// <param name="streamUrl">Stream URL.</param>
    /// <returns>The external id.</returns>
    internal static string GetExternalId(string playlistPath, string streamUrl)
    {
        return IptvOrgConstants.M3uTunerType + "_"
            + playlistPath.GetMD5().ToString("N", CultureInfo.InvariantCulture)
            + streamUrl.GetMD5().ToString("N", CultureInfo.InvariantCulture);
    }

    private void OnTaskCompleted(object? sender, TaskCompletionEventArgs e)
    {
        if (!string.Equals(e.Task.ScheduledTask.Key, IptvOrgConstants.RefreshGuideTaskKey, StringComparison.Ordinal))
        {
            return;
        }

        _ = ApplyDescriptionsAsync(_stopping.Token);
    }

    private async Task ApplyDescriptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_config.Current.ChannelDescriptions || !File.Exists(_paths.EntriesPath))
            {
                return;
            }

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                IReadOnlyList<PlaylistEntry> entries;
                var stream = new FileStream(_paths.EntriesPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
                await using (stream.ConfigureAwait(false))
                {
                    entries = await JsonSerializer.DeserializeAsync<List<PlaylistEntry>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
                }

                var byExternalId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Description))
                    {
                        byExternalId.TryAdd(GetExternalId(_paths.PlaylistPath, entry.Url), entry.Description);
                    }
                }

                if (byExternalId.Count == 0)
                {
                    return;
                }

                var channels = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = [BaseItemKind.LiveTvChannel]
                });

                var updated = 0;
                foreach (var channel in channels)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(channel.ExternalId)
                        || !byExternalId.TryGetValue(channel.ExternalId, out var description)
                        || string.Equals(channel.Overview, description, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    channel.Overview = description;
                    await _libraryManager.UpdateItemAsync(channel, channel.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                    updated++;
                }

                if (updated > 0)
                {
                    _logger.LogInformation("IPTV-org: descriptions written to {Count} Live TV channels", updated);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Server is stopping.
        }
#pragma warning disable CA1031 // A background helper must never crash the server.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Writing IPTV-org channel descriptions failed");
        }
    }
}
