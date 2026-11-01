using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Service;
using Jellyfin.Plugin.IptvOrg.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.IptvOrg.Api;

/// <summary>
/// Settings-page endpoints. Administrators only.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("IptvOrg")]
[Produces(MediaTypeNames.Application.Json)]
public class IptvOrgController : ControllerBase
{
    private readonly IIptvOrgService _service;
    private readonly ITaskManager _taskManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="IptvOrgController"/> class.
    /// </summary>
    /// <param name="service">IPTV-org service.</param>
    /// <param name="taskManager">Task manager.</param>
    public IptvOrgController(IIptvOrgService service, ITaskManager taskManager)
    {
        _service = service;
        _taskManager = taskManager;
    }

    /// <summary>
    /// Gets the plugin state: last runs, channel counts, file locations and Live TV registration.
    /// </summary>
    /// <response code="200">State.</response>
    /// <returns>The state.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatusResponse> GetStatus()
    {
        var registration = _service.GetRegistration();
        return new StatusResponse(
            _service.GetStatus(),
            _service.HasCatalog,
            _service.PlaylistPath,
            _service.GuidePath,
            registration.TunerId is not null,
            registration.ListingsProviderId is not null);
    }

    /// <summary>
    /// Gets the categories with Russian names and channel counts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Categories.</response>
    /// <returns>The categories.</returns>
    [HttpGet("Categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryInfo>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await _service.GetCategoriesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(categories);
    }

    /// <summary>
    /// Queues the catalog download.
    /// </summary>
    /// <response code="204">Queued.</response>
    /// <returns>No content.</returns>
    [HttpPost("Sync")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Sync()
    {
        _taskManager.QueueScheduledTask<SyncCatalogTask>();
        return NoContent();
    }

    /// <summary>
    /// Queues the guide update.
    /// </summary>
    /// <response code="204">Queued.</response>
    /// <returns>No content.</returns>
    [HttpPost("Guide")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult UpdateGuide()
    {
        _taskManager.QueueScheduledTask<UpdateGuideTask>();
        return NoContent();
    }

    /// <summary>
    /// Adds the plugin's M3U tuner and XMLTV guide to Live TV, if missing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Registered.</response>
    /// <response code="409">The playlist does not exist yet.</response>
    /// <returns>The registration.</returns>
    [HttpPost("LiveTv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveTvRegistration>> RegisterInLiveTv(CancellationToken cancellationToken)
    {
        try
        {
            return await _service.RegisterInLiveTvAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
