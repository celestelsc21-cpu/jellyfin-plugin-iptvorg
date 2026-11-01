using System;

namespace Jellyfin.Plugin.IptvOrg;

/// <summary>
/// Fixed values shared by the plugin.
/// </summary>
internal static class IptvOrgConstants
{
    /// <summary>
    /// Name of the HTTP client registered with <c>IHttpClientFactory</c>.
    /// </summary>
    public const string HttpClientName = "IptvOrg";

    /// <summary>
    /// Name of the HTTP client used for third-party guide files.
    /// </summary>
    public const string GuideHttpClientName = "IptvOrgGuide";

    /// <summary>
    /// Name of the HTTP client used for stream checks.
    /// </summary>
    public const string ProbeHttpClientName = "IptvOrgProbe";

    /// <summary>
    /// File name of the persisted playlist channels (read by the guide update).
    /// </summary>
    public const string EntriesFileName = "channels.json";

    /// <summary>
    /// File name of the persisted filter values for the settings page.
    /// </summary>
    public const string FiltersFileName = "filters.json";

    /// <summary>
    /// File name of the Russian channel names found in the guide.
    /// </summary>
    public const string NamesFileName = "names.json";

    /// <summary>
    /// File name of the stream check cache.
    /// </summary>
    public const string StreamCheckFileName = "streamcheck.json";

    /// <summary>
    /// Folder name used under the server data and cache directories.
    /// </summary>
    public const string FolderName = "iptvorg";

    /// <summary>
    /// File name of the generated playlist.
    /// </summary>
    public const string PlaylistFileName = "playlist.m3u";

    /// <summary>
    /// File name of the generated XMLTV guide.
    /// </summary>
    public const string GuideFileName = "guide.xml";

    /// <summary>
    /// Key of Jellyfin's built-in XMLTV listings provider.
    /// </summary>
    public const string XmlTvListingsType = "xmltv";

    /// <summary>
    /// File name of the persisted build status.
    /// </summary>
    public const string StatusFileName = "status.json";

    /// <summary>
    /// Friendly name of the tuner the plugin registers in Live TV.
    /// </summary>
    public const string TunerFriendlyName = "IPTV-org";

    /// <summary>
    /// Jellyfin's built-in M3U tuner type.
    /// </summary>
    public const string M3uTunerType = "m3u";

    /// <summary>
    /// Key of Jellyfin's "Refresh Guide" scheduled task.
    /// </summary>
    public const string RefreshGuideTaskKey = "RefreshGuide";

    /// <summary>
    /// Pseudo category for channels without categories in iptv-org.
    /// </summary>
    public const string UndefinedCategory = "undefined";

    /// <summary>
    /// The iptv-org adult category.
    /// </summary>
    public const string AdultCategory = "xxx";

    /// <summary>
    /// Base address of the iptv-org API. Fixed on purpose: the plugin never requests
    /// user-supplied URLs, which rules out SSRF through the settings page.
    /// </summary>
    public static readonly Uri ApiBaseUri = new("https://iptv-org.github.io/api/");
}
