using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvOrg.Foss;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.IptvOrg.Api;

/// <summary>
/// Separate OTT-play FOSS export and its HTTP endpoints.
/// </summary>
[ApiController]
[Route("IptvOrg/Foss")]
public class IptvOrgFossController : ControllerBase
{
    private readonly IFossExporter _exporter;

    /// <summary>
    /// Initializes the FOSS controller.
    /// </summary>
    /// <param name="exporter">FOSS exporter.</param>
    public IptvOrgFossController(IFossExporter exporter)
    {
        _exporter = exporter;
    }

    /// <summary>
    /// Generates the separate FOSS playlist and JSON EPG files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Build result or conflict when the source files are unavailable.</returns>
    [Authorize(Policy = Policies.RequiresElevation)]
    [HttpPost("Build")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Build(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _exporter.BuildAsync(cancellationToken).ConfigureAwait(false);
            return count.HasValue ? Ok(new { GuideChannels = count.Value }) : Conflict("Сборка FOSS уже выполняется.");
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Serves the FOSS playlist with a direct JSON EPG source on the current HTTP host.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>FOSS M3U or 404.</returns>
    [AllowAnonymous]
    [HttpGet("Playlist")]
    [Produces("application/x-mpegURL")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Playlist(CancellationToken cancellationToken)
    {
        if (!System.IO.File.Exists(_exporter.PlaylistPath))
        {
            return NotFound();
        }

        var playlist = await System.IO.File.ReadAllTextAsync(_exporter.PlaylistPath, cancellationToken).ConfigureAwait(false);
        var source = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/IptvOrg/Foss/Epg/";
        var header = "#EXTM3U foss-tvg=\"=iptvorg::" + source + "\"";
        if (!playlist.StartsWith("#EXTM3U\n", StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Content(header + playlist["#EXTM3U".Length..], "application/x-mpegURL", Encoding.UTF8);
    }

    /// <summary>
    /// Serves the FOSS JSON EPG file identified by the unsigned xxHash32 of the channel ID.
    /// </summary>
    /// <param name="hash">Unsigned decimal channel ID hash.</param>
    /// <returns>Per-channel FOSS JSON or 404.</returns>
    [AllowAnonymous]
    [HttpGet("Epg/{hash}.json")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Epg(string hash)
    {
        if (!uint.TryParse(hash, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return NotFound();
        }

        var path = Path.Combine(_exporter.EpgDirectoryPath, id.ToString(CultureInfo.InvariantCulture) + ".json");
        return System.IO.File.Exists(path)
            ? PhysicalFile(path, "application/json", enableRangeProcessing: true)
            : NotFound();
    }
}
