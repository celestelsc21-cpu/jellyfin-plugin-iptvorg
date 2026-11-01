using System;
using MediaBrowser.Common.Plugins;

namespace Jellyfin.Plugin.IptvOrg.Configuration;

/// <summary>
/// Resolves the plugin instance through <see cref="IPluginManager"/>.
/// </summary>
internal sealed class PluginConfigurationAccessor : IPluginConfigurationAccessor
{
    private static readonly Guid PluginId = Guid.Parse(Plugin.PluginGuid);
    private readonly IPluginManager _pluginManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationAccessor"/> class.
    /// </summary>
    /// <param name="pluginManager">Plugin manager.</param>
    public PluginConfigurationAccessor(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    /// <inheritdoc />
    public Plugin? Instance => _pluginManager.GetPlugin(PluginId)?.Instance as Plugin;

    /// <inheritdoc />
    public PluginConfiguration Current
        => Instance?.Configuration
           ?? throw new InvalidOperationException("IPTV-org plugin instance is not loaded.");
}
