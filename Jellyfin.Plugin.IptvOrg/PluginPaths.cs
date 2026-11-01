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

        // Where Jellyfin's XMLTV provider keeps its copy of a guide (<cache>/xmltv/<provider id>.xml,
        // see XmlTvListingsProvider in Jellyfin 10.11). Not a public API: if Jellyfin moves it,
        // clearing simply finds nothing and Jellyfin re-reads the guide after its 1-hour cache.
        JellyfinXmlTvCacheDirectory = Path.Combine(applicationPaths.CachePath, "xmltv");
        EntriesPath = Path.Combine(DataDirectory, IptvOrgConstants.EntriesFileName);
        FiltersPath = Path.Combine(DataDirectory, IptvOrgConstants.FiltersFileName);
        StreamCheckPath = Path.Combine(DataDirectory, IptvOrgConstants.StreamCheckFileName);
        NamesPath = Path.Combine(DataDirectory, IptvOrgConstants.NamesFileName);
        NameMatchesPath = Path.Combine(DataDirectory, IptvOrgConstants.NameMatchesFileName);
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

    /// <summary>Gets the persisted playlist channels path.</summary>
    public string EntriesPath { get; }

    /// <summary>Gets the persisted filter values path.</summary>
    public string FiltersPath { get; }

    /// <summary>Gets the stream check cache path.</summary>
    public string StreamCheckPath { get; }

    /// <summary>Gets the path of the Russian channel names found in the guide.</summary>
    public string NamesPath { get; }

    /// <summary>Gets the path of the guide channels found by name.</summary>
    public string NameMatchesPath { get; }

    /// <summary>Gets Jellyfin's XMLTV cache directory.</summary>
    public string JellyfinXmlTvCacheDirectory { get; }

    /// <summary>Gets the temporary directory for guide downloads.</summary>
    public string GuideDownloadDirectory { get; }
}
