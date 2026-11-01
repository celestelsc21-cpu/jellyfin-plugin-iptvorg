using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Dictionary of iptv-org grabber sites whose "parser" is simply one public XMLTV file in
/// which every programme's <c>channel</c> attribute is the channel's <c>site_id</c>
/// (see <c>sites/&lt;site&gt;/&lt;site&gt;.config.js</c> in iptv-org/epg). For these sites the
/// plugin reads the file itself, using the <c>site</c>/<c>site_id</c> pairs from
/// <c>guides.json</c>, so no grabber has to run anywhere.
/// </summary>
internal static class SiteGuideFiles
{
    private static readonly FrozenDictionary<string, Uri> Files = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase)
    {
        // Russia and neighbouring countries (about 450 channels).
        ["epg.iptvx.one"] = new("https://iptvx.one/epg/epg_noarch.xml.gz"),
        ["epg.112114.xyz"] = new("https://epg.112114.xyz/pp.xml"),
        ["vantagetv.ee"] = new("http://vantagetv.ee/epg.xml"),
        ["makrodigitaltelevision.com"] = new("https://makrodigitaltelevision.com/epg.xml"),
        ["epg.tmacaraibes.com"] = new("https://epg.tmacaraibes.com/Epg/xmltv.xml"),
        ["x1co.com.br"] = new("https://x1co.com.br/epg/epg.xml"),
        ["energeek.cl"] = new("https://raw.githubusercontent.com/luisms123/tdt/master/guiaenergeek.xml")
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the XMLTV file of a site.
    /// </summary>
    /// <param name="site">Site from <c>guides.json</c>.</param>
    /// <param name="url">The file URL.</param>
    /// <returns><c>true</c> when the site is in the dictionary.</returns>
    public static bool TryGet(string? site, out Uri? url)
    {
        url = null;
        return !string.IsNullOrEmpty(site) && Files.TryGetValue(site, out url);
    }
}
