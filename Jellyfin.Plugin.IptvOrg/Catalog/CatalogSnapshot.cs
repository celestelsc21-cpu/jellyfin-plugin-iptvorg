using System.Collections.Generic;
using Jellyfin.Plugin.IptvOrg.Catalog.Models;

namespace Jellyfin.Plugin.IptvOrg.Catalog;

/// <summary>
/// The iptv-org datasets needed to build a playlist. Loaded from the local cache only
/// for the duration of a build, so the large lists are not kept in memory.
/// </summary>
/// <param name="Channels">Channels.</param>
/// <param name="Feeds">Feeds.</param>
/// <param name="Streams">Streams.</param>
/// <param name="Categories">Categories, in API order.</param>
/// <param name="Logos">Logos.</param>
/// <param name="Regions">Regions.</param>
/// <param name="Guides">Guide sources.</param>
/// <param name="Countries">Countries.</param>
/// <param name="Languages">Languages.</param>
internal sealed record CatalogSnapshot(
    IReadOnlyList<ApiChannel> Channels,
    IReadOnlyList<ApiFeed> Feeds,
    IReadOnlyList<ApiStream> Streams,
    IReadOnlyList<ApiCategory> Categories,
    IReadOnlyList<ApiLogo> Logos,
    IReadOnlyList<ApiRegion> Regions,
    IReadOnlyList<ApiGuide> Guides,
    IReadOnlyList<ApiCountry> Countries,
    IReadOnlyList<ApiLanguage> Languages);
