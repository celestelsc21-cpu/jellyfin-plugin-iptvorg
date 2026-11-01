using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Configuration;
using Jellyfin.Plugin.IptvOrg.Tasks;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// Rebuilds the playlist when the settings change, and queues the first catalog
/// download after installation. Never blocks server startup.
/// </summary>
internal sealed class StartupService : IHostedService, IDisposable
{
    private static readonly TimeSpan FirstSyncDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RebuildDebounce = TimeSpan.FromSeconds(3);

    private readonly IIptvOrgService _service;
    private readonly IPluginConfigurationAccessor _config;
    private readonly ITaskManager _taskManager;
    private readonly ILogger<StartupService> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _rebuildLock = new();
    private CancellationTokenSource? _pendingRebuild;
    private Plugin? _plugin;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupService"/> class.
    /// </summary>
    /// <param name="service">IPTV-org service.</param>
    /// <param name="config">Configuration accessor.</param>
    /// <param name="taskManager">Task manager.</param>
    /// <param name="logger">Logger.</param>
    public StartupService(
        IIptvOrgService service,
        IPluginConfigurationAccessor config,
        ITaskManager taskManager,
        ILogger<StartupService> logger)
    {
        _service = service;
        _config = config;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _plugin = _config.Instance;
        if (_plugin is not null)
        {
            _plugin.ConfigurationChanged += OnConfigurationChanged;
        }

        if (!_service.HasCatalog)
        {
            _ = QueueFirstSyncAsync(_stopping.Token);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_plugin is not null)
        {
            _plugin.ConfigurationChanged -= OnConfigurationChanged;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_rebuildLock)
        {
            _pendingRebuild?.Dispose();
            _pendingRebuild = null;
        }

        _stopping.Dispose();
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        CancellationToken token;
        lock (_rebuildLock)
        {
            // Debounce: several saves in a row cause one rebuild.
            // The superseded source is cancelled but not disposed: its rebuild may still be
            // observing the token; it is small and collected with the task.
            _pendingRebuild?.Cancel();
            _pendingRebuild = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            token = _pendingRebuild.Token;
        }

        _ = RebuildAfterDelayAsync(token);
    }

    private async Task RebuildAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RebuildDebounce, cancellationToken).ConfigureAwait(false);
            await _service.RebuildPlaylistAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer change or the server is stopping.
        }
#pragma warning disable CA1031 // A background rebuild must never crash the server.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Rebuilding the IPTV-org playlist after a settings change failed");
        }
    }

    private async Task QueueFirstSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Let the server finish starting before the first (large) download.
            await Task.Delay(FirstSyncDelay, cancellationToken).ConfigureAwait(false);
            if (!_service.HasCatalog)
            {
                _logger.LogInformation("IPTV-org catalog is missing; queueing the first download");
                _taskManager.QueueScheduledTask<SyncCatalogTask>();
            }
        }
        catch (OperationCanceledException)
        {
            // Server is stopping.
        }
#pragma warning disable CA1031 // Never crash the server from a background helper.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Could not queue the first IPTV-org catalog download");
        }
    }
}
