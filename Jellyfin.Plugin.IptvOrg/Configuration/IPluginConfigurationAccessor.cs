namespace Jellyfin.Plugin.IptvOrg.Configuration;

/// <summary>
/// DI-friendly access to the current plugin configuration
/// (replaces the static <c>Plugin.Instance</c> pattern).
/// </summary>
public interface IPluginConfigurationAccessor
{
    /// <summary>
    /// Gets the current configuration snapshot. Never cache it across operations:
    /// the administrator may change settings at any time.
    /// </summary>
    PluginConfiguration Current { get; }

    /// <summary>
    /// Gets the loaded plugin instance, or <c>null</c> when it is not loaded.
    /// </summary>
    Plugin? Instance { get; }
}
