using System.IO;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.IptvOrg;

/// <summary>
/// File locations used by the plugin. All paths are derived from the server's own
/// directories; nothing comes from user input.
/// </summary>
internal sealed class PluginPaths
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginPaths"/> class.
    /// </summary>
    /// <param name="applicationPaths">Server paths.</param>
    public PluginPaths(IApplicationPaths applicationPaths)
    {
        CacheDirectory = Path.Combine(applicationPaths.CachePath, IptvOrgConstants.FolderName);
        DataDirectory = Path.Combine(applicationPaths.DataPath, IptvOrgConstants.FolderName);
        PlaylistPath = Path.Combine(DataDirectory, IptvOrgConstants.PlaylistFileName);
        GuidePath = Path.Combine(DataDirectory, IptvOrgConstants.GuideFileName);
        StatusPath = Path.Combine(DataDirectory, IptvOrgConstants.StatusFileName);
        GuideDownloadDirectory = Path.Combine(CacheDirectory, "guides");
    }

    /// <summary>Gets the directory with downloaded API files.</summary>
    public string CacheDirectory { get; }

    /// <summary>Gets the directory with generated files.</summary>
    public string DataDirectory { get; }

    /// <summary>Gets the generated playlist path (used by the M3U tuner).</summary>
    public string PlaylistPath { get; }

    /// <summary>Gets the generated guide path (used by the XMLTV provider).</summary>
    public string GuidePath { get; }

    /// <summary>Gets the persisted status path.</summary>
    public string StatusPath { get; }

    /// <summary>Gets the temporary directory for guide downloads.</summary>
    public string GuideDownloadDirectory { get; }
}
