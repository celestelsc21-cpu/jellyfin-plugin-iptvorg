using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Service;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.IptvOrg.Tasks;

/// <summary>
/// Builds the merged XMLTV guide from the hosted guide files (every 12 hours).
/// </summary>
public class UpdateGuideTask : IScheduledTask
{
    /// <summary>
    /// Task key.
    /// </summary>
    public const string TaskKey = "IptvOrgUpdateGuide";

    private readonly IIptvOrgService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateGuideTask"/> class.
    /// </summary>
    /// <param name="service">IPTV-org service.</param>
    public UpdateGuideTask(IIptvOrgService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Обновить телегид IPTV-org";

    /// <inheritdoc />
    public string Key => TaskKey;

    /// <inheritdoc />
    public string Description => "Собирает программу передач для каналов плейлиста из источников гида iptv-org.";

    /// <inheritdoc />
    public string Category => "IPTV-org";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _service.UpdateGuideAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Every 12 hours. Also runs at 05:13, after the daily catalog sync.
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(12).Ticks
            },
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = new TimeSpan(5, 13, 0).Ticks
            }
        ];
    }
}
