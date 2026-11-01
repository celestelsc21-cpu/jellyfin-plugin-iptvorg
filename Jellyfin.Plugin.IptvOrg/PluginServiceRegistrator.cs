using System;
using System.Net;
using System.Net.Http;
using Jellyfin.Plugin.IptvOrg.Catalog;
using Jellyfin.Plugin.IptvOrg.Configuration;
using Jellyfin.Plugin.IptvOrg.Guide;
using Jellyfin.Plugin.IptvOrg.Service;
using Jellyfin.Plugin.IptvOrg.Streams;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jellyfin.Plugin.IptvOrg;

/// <summary>
/// Registers plugin services with the server's DI container.
/// Scheduled tasks are discovered by the server itself.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    private const string UserAgent = "Jellyfin-Plugin-IptvOrg";

    // Many stream servers answer players only; Jellyfin itself plays streams through FFmpeg/VLC-like clients.
    private const string ProbeUserAgent = "VLC/3.0.21 LibVLC/3.0.21";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPluginConfigurationAccessor, PluginConfigurationAccessor>();
        serviceCollection.AddSingleton<PluginPaths>();
        serviceCollection.TryAddSingleton(TimeProvider.System);

        // iptv-org API: one fixed host; large files on slow links need a generous timeout.
        serviceCollection
            .AddHttpClient(IptvOrgConstants.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                // The API never redirects; do not follow redirects off the fixed host.
                AllowAutoRedirect = false
            });

        // Third-party guide hosts: public addresses only, limited redirects.
        serviceCollection
            .AddHttpClient(IptvOrgConstants.GuideHttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                ConnectTimeout = TimeSpan.FromSeconds(20),
                ConnectCallback = PublicOnlyConnector.ConnectAsync
            });

        // Stream checks: public addresses only, like the guide client. Per-check timeouts
        // come from the settings; this is only an upper bound.
        serviceCollection
            .AddHttpClient(IptvOrgConstants.ProbeHttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(ProbeUserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                ConnectCallback = PublicOnlyConnector.ConnectAsync
            });

        serviceCollection.AddSingleton<IIptvOrgClient, IptvOrgClient>();
        serviceCollection.AddSingleton<IStreamChecker, StreamChecker>();
        serviceCollection.AddSingleton<IIptvOrgService, IptvOrgService>();
        serviceCollection.AddHostedService<StartupService>();
    }
}
