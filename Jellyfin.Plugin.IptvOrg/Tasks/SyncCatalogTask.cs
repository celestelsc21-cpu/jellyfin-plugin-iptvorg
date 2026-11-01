using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Service;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.IptvOrg.Tasks;

/// <summary>
/// Downloads the iptv-org catalog and rebuilds the playlist (daily).
/// </summary>
public class SyncCatalogTask : IScheduledTask
{
    /// <summary>
    /// Task key.
    /// </summary>
    public const string TaskKey = "IptvOrgSyncCatalog";

    private readonly IIptvOrgService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncCatalogTask"/> class.
    /// </summary>
    /// <param name="service">IPTV-org service.</param>
    public SyncCatalogTask(IIptvOrgService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Обновить каналы IPTV-org";

    /// <inheritdoc />
    public string Key => TaskKey;

    /// <inheritdoc />
    public string Description => "Скачивает каталог iptv-org и пересобирает плейлист по фильтрам плагина.";

    /// <inheritdoc />
    public string Category => "IPTV-org";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _service.SyncAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Once a day, early morning; minute chosen off the hour to avoid the busy :00 mark.
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = new TimeSpan(4, 37, 0).Ticks
            }
        ];
    }
}
