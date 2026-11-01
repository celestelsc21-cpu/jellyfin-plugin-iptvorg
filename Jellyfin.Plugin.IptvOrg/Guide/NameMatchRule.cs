using System.Collections.Frozen;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Area of a guide site in which channels may be linked to its guide channels by name.
/// </summary>
/// <param name="Language">ISO 639-3 broadcast language of the site's channel names (for example <c>rus</c>).</param>
/// <param name="Countries">ISO 3166-1 alpha-2 countries the site covers.</param>
internal sealed record NameMatchRule(string Language, FrozenSet<string> Countries);
