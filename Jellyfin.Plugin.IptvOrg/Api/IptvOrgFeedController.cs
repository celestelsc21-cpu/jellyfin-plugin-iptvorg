using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Jellyfin.Plugin.IptvOrg.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.IptvOrg.Api;

/// <summary>
/// Public read-only feeds for IPTV clients. Access must be restricted at the network level.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("IptvOrg")]
public class IptvOrgFeedController : ControllerBase
{
    private readonly IIptvOrgService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="IptvOrgFeedController"/> class.
    /// </summary>
    /// <param name="service">IPTV-org service.</param>
    public IptvOrgFeedController(IIptvOrgService service)
    {
        _service = service;
    }

    /// <summary>
    /// Serves the latest generated M3U playlist with a linked XMLTV guide for IPTV clients.
    /// </summary>
    /// <returns>The playlist, or 404 if it has not been generated.</returns>
    [HttpGet("Playlist")]
    [Produces("application/x-mpegURL")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlaylist(CancellationToken cancellationToken)
    {
        var path = _service.PlaylistPath;
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var playlist = await System.IO.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var epgUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/IptvOrg/Epg";
        return Content(M3uWriter.WithGuideUrl(playlist, epgUrl), "application/x-mpegURL", Encoding.UTF8);
    }

    /// <summary>
    /// Serves the latest generated XMLTV guide without creating a copy.
    /// </summary>
    /// <returns>The guide, or 404 if it has not been generated.</returns>
    [HttpGet("Epg")]
    [Produces("application/xml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetEpg()
    {
        var path = _service.GuidePath;
        return System.IO.File.Exists(path)
            ? PhysicalFile(path, "application/xml", enableRangeProcessing: true)
            : NotFound();
    }
}
