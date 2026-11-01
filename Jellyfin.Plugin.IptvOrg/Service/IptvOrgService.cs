using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Jellyfin.Plugin.IptvOrg.Catalog;
using Jellyfin.Plugin.IptvOrg.Configuration;
using Jellyfin.Plugin.IptvOrg.Guide;
using Jellyfin.Plugin.IptvOrg.Localization;
using Jellyfin.Plugin.IptvOrg.Playlist;
using Jellyfin.Plugin.IptvOrg.Streams;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvOrg.Service;

/// <summary>
/// Default <see cref="IIptvOrgService"/> implementation.
/// </summary>
internal sealed class IptvOrgService : IIptvOrgService, IDisposable
{
    /// <summary>
    /// Largest guide file accepted (compressed size), to protect disk and network.
    /// </summary>
    internal const long MaxGuideFileBytes = 500L * 1024 * 1024;

    private const string LiveTvConfigurationKey = "livetv";

    private static readonly JsonSerializerOptions StatusJsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IIptvOrgClient _client;
    private readonly IStreamChecker _streamChecker;
    private readonly IPluginConfigurationAccessor _config;
    private readonly PluginPaths _paths;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITaskManager _taskManager;
    private readonly ITunerHostManager _tunerHostManager;
    private readonly IListingsManager _listingsManager;
    private readonly IConfigurationManager _configurationManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IptvOrgService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _statusLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="IptvOrgService"/> class.
    /// </summary>
    /// <param name="client">iptv-org API client.</param>
    /// <param name="streamChecker">Stream checker.</param>
    /// <param name="config">Configuration accessor.</param>
    /// <param name="paths">Plugin paths.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="taskManager">Task manager.</param>
    /// <param name="tunerHostManager">Tuner host manager.</param>
    /// <param name="listingsManager">Listings manager.</param>
    /// <param name="configurationManager">Server configuration manager.</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="logger">Logger.</param>
    public IptvOrgService(
        IIptvOrgClient client,
        IStreamChecker streamChecker,
        IPluginConfigurationAccessor config,
        PluginPaths paths,
        IHttpClientFactory httpClientFactory,
        ITaskManager taskManager,
        ITunerHostManager tunerHostManager,
        IListingsManager listingsManager,
        IConfigurationManager configurationManager,
        TimeProvider timeProvider,
        ILogger<IptvOrgService> logger)
    {
        _client = client;
        _streamChecker = streamChecker;
        _config = config;
        _paths = paths;
        _httpClientFactory = httpClientFactory;
        _taskManager = taskManager;
        _tunerHostManager = tunerHostManager;
        _listingsManager = listingsManager;
        _configurationManager = configurationManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string PlaylistPath => _paths.PlaylistPath;

    /// <inheritdoc />
    public string GuidePath => _paths.GuidePath;

    /// <inheritdoc />
    public bool HasCatalog => CatalogLoader.IsComplete(_paths.CacheDirectory);

    /// <inheritdoc />
    public async Task SyncAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            progress.Report(0);
            try
            {
                await _client.DownloadAllAsync(_paths.CacheDirectory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
            {
                UpdateStatus(s => s.LastError = "Не удалось скачать каталог iptv-org: " + ex.Message);
                throw;
            }

            UpdateStatus(s =>
            {
                s.LastSyncUtc = _timeProvider.GetUtcNow().UtcDateTime;
                s.LastError = null;
            });
            progress.Report(30);

            await RebuildPlaylistCoreAsync(new Progress<double>(p => progress.Report(30 + (0.7 * p))), cancellationToken).ConfigureAwait(false);
            progress.Report(100);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RebuildPlaylistAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RebuildPlaylistCoreAsync(new Progress<double>(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateGuideAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var config = _config.Current;
        if (!config.EnableGuide)
        {
            _logger.LogInformation("IPTV-org guide is disabled in the plugin settings");
            return;
        }

        if (!HasCatalog || !File.Exists(_paths.EntriesPath))
        {
            _logger.LogInformation("IPTV-org playlist is not built yet; skipping the guide update");
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await UpdateGuideCoreAsync(config, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<LiveTvRegistration> RegisterInLiveTvAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_paths.PlaylistPath))
            {
                throw new InvalidOperationException("Плейлист ещё не создан: сначала обновите каталог iptv-org.");
            }

            if (!File.Exists(_paths.GuidePath))
            {
                // The XMLTV provider requires an existing file; the guide task fills it later.
                await WriteEmptyGuideAsync(cancellationToken).ConfigureAwait(false);
            }

            var current = GetRegistration();
            var tunerId = current.TunerId;
            if (tunerId is null)
            {
                var tuner = await _tunerHostManager.SaveTunerHost(new TunerHostInfo
                {
                    Type = IptvOrgConstants.M3uTunerType,
                    Url = _paths.PlaylistPath,
                    FriendlyName = IptvOrgConstants.TunerFriendlyName,
                    ImportFavoritesOnly = false,
                    TunerCount = 0
                }).ConfigureAwait(false);
                tunerId = tuner.Id;
                _logger.LogInformation("Registered the IPTV-org M3U tuner in Live TV");
            }

            var listingsId = current.ListingsProviderId;
            if (listingsId is null)
            {
                var listings = await _listingsManager.SaveListingProvider(
                    new ListingsProviderInfo
                    {
                        Type = IptvOrgConstants.XmlTvListingsType,
                        Path = _paths.GuidePath,
                        EnableAllTuners = false,
                        EnabledTuners = [tunerId]
                    },
                    validateLogin: false,
                    validateListings: true).ConfigureAwait(false);
                listingsId = listings.Id;
                _logger.LogInformation("Registered the IPTV-org XMLTV guide in Live TV");
            }

            return new LiveTvRegistration(tunerId, listingsId);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public LiveTvRegistration GetRegistration()
    {
        var options = _configurationManager.GetConfiguration<LiveTvOptions>(LiveTvConfigurationKey);

        var tuner = options.TunerHosts?.FirstOrDefault(t =>
            string.Equals(t.Type, IptvOrgConstants.M3uTunerType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(t.Url, _paths.PlaylistPath, StringComparison.Ordinal));
        var listings = options.ListingProviders?.FirstOrDefault(l =>
            string.Equals(l.Type, IptvOrgConstants.XmlTvListingsType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(l.Path, _paths.GuidePath, StringComparison.Ordinal));

        return new LiveTvRegistration(tuner?.Id, listings?.Id);
    }

    /// <inheritdoc />
    public PluginStatus GetStatus()
    {
        lock (_statusLock)
        {
            return ReadStatus();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await CatalogLoader.LoadCategoriesAsync(_paths.CacheDirectory, cancellationToken).ConfigureAwait(false);
        var counts = GetStatus().CatalogCategoryCounts;

        var result = categories
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .Select(c => new CategoryInfo(
                c.Id.ToLowerInvariant(),
                CategoryNames.Get(c.Id, c.Name),
                counts.GetValueOrDefault(c.Id),
                string.Equals(c.Id, IptvOrgConstants.AdultCategory, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        result.Add(new CategoryInfo(
            IptvOrgConstants.UndefinedCategory,
            CategoryNames.Get(IptvOrgConstants.UndefinedCategory),
            counts.GetValueOrDefault(IptvOrgConstants.UndefinedCategory),
            false));

        return result;
    }

    /// <inheritdoc />
    public async Task<FilterCatalog> GetFiltersAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.FiltersPath))
        {
            return new FilterCatalog();
        }

        try
        {
            var stream = new FileStream(_paths.FiltersPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<FilterCatalog>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                    ?? new FilterCatalog();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not read the IPTV-org filter values");
            return new FilterCatalog();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ClearGuideCacheAsync(CancellationToken cancellationToken)
    {
        // Never make the settings page wait for a long catalog sync or guide build.
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            var removed = 0;
            if (Directory.Exists(_paths.GuideDownloadDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(_paths.GuideDownloadDirectory))
                {
                    File.Delete(file);
                    removed++;
                }
            }

            foreach (var path in new[] { _paths.GuidePath, _paths.NamesPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    removed++;
                }
            }

            var listingsId = GetRegistration().ListingsProviderId;
            if (listingsId is not null)
            {
                if (DeleteJellyfinGuideCopy(listingsId))
                {
                    removed++;
                }

                // The XMLTV provider needs an existing file until the guide is rebuilt.
                await WriteEmptyGuideAsync(cancellationToken).ConfigureAwait(false);

                // Jellyfin keeps programmes in its own database; only its guide refresh removes
                // them. With the empty guide it rebuilds nothing, it just drops the old programmes.
                QueueJellyfinGuideRefresh();
            }

            UpdateStatus(s =>
            {
                s.LastGuideUtc = null;
                s.GuideChannelCount = 0;
                s.GuideProgrammeCount = 0;
                s.GuideSourcesUsed = 0;
                s.GuideSourcesFailed = 0;
            });

            _logger.LogInformation("IPTV-org guide cache cleared ({Count} files removed)", removed);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task RebuildPlaylistCoreAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (!HasCatalog)
        {
            _logger.LogInformation("IPTV-org catalog is not downloaded yet; the playlist is not built");
            return;
        }

        var config = _config.Current;
        var options = ChannelFilterOptions.FromConfiguration(config);
        var catalog = await CatalogLoader.LoadAsync(_paths.CacheDirectory, cancellationToken).ConfigureAwait(false);
        var groups = PlaylistBuilder.Collect(catalog, options);
        var counts = PlaylistBuilder.CountByCategory(catalog, includeNsfw: true);
        var filters = FilterCatalogBuilder.Build(catalog, config.IncludeNsfw);
        var languages = LanguageCodes.Normalize(config.PreferredLanguages);
        IReadOnlyDictionary<string, string> descriptions = config.ChannelDescriptions
            ? ChannelDescriptionBuilder.BuildAll(catalog, groups, LanguageCodes.PrefersRussian(languages))
            : new Dictionary<string, string>();

        // Keep only what the final step needs, so the large lists can be collected during
        // the (possibly long) stream checks. Async locals live until the method returns.
        var slim = catalog with
        {
            Channels = [],
            Feeds = [],
            Streams = [],
            Regions = [],
            Guides = [],
            Countries = [],
            Languages = []
        };
        catalog = null;

        // The settings page lists countries and languages from this file; write it before the
        // (possibly long) stream checks so the lists appear right after the catalog download.
        Directory.CreateDirectory(_paths.DataDirectory);
        await WriteFileAtomicallyAsync(_paths.FiltersPath, JsonSerializer.Serialize(filters), cancellationToken).ConfigureAwait(false);
        progress.Report(5);

        IReadOnlySet<string>? dead = null;
        var check = new StreamCheckSummary(0, 0, 0);
        if (config.CheckStreams)
        {
            (dead, check) = await CheckStreamsAsync(groups, config, new Progress<double>(p => progress.Report(5 + (0.9 * p))), cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<PlaylistEntry> entries = PlaylistBuilder.Materialize(groups, slim, options, dead)
            .Select(e => e.ChannelId is not null && descriptions.TryGetValue(e.ChannelId, out var d) ? e with { Description = d } : e)
            .ToArray();

        // Keep the original names in channels.json (the guide maps by tvg-id, not by name)
        // and apply the guide names (preferred language) from the last guide build only to the playlist.
        var baseEntries = entries;
        if (config.UseGuideChannelNames)
        {
            entries = ChannelNames.Apply(entries, await LoadGuideNamesAsync(languages, cancellationToken).ConfigureAwait(false), config.PrefixCategoryInName);
        }

        var content = M3uWriter.Write(entries);

        var changed = !File.Exists(_paths.PlaylistPath)
            || !string.Equals(
                await File.ReadAllTextAsync(_paths.PlaylistPath, cancellationToken).ConfigureAwait(false),
                content,
                StringComparison.Ordinal);

        if (changed)
        {
            await WriteFileAtomicallyAsync(_paths.PlaylistPath, content, cancellationToken).ConfigureAwait(false);
        }

        await WriteFileAtomicallyAsync(_paths.EntriesPath, JsonSerializer.Serialize(baseEntries), cancellationToken).ConfigureAwait(false);

        UpdateStatus(s =>
        {
            s.LastPlaylistUtc = _timeProvider.GetUtcNow().UtcDateTime;
            s.ChannelCount = entries.Count;
            s.CatalogCategoryCounts = counts;
            s.StreamsChecked = check.Checked;
            s.DeadStreams = check.Dead;
            s.DroppedChannels = check.DroppedChannels;
            s.StreamCheckEnabled = config.CheckStreams;
        });

        _logger.LogInformation(
            "IPTV-org playlist built: {Count} channels ({State}); streams checked {Checked}, dead {Dead}, channels without a working stream {Dropped}",
            entries.Count,
            changed ? "updated" : "unchanged",
            check.Checked,
            check.Dead,
            check.DroppedChannels);

        if (changed && GetRegistration().TunerId is not null)
        {
            QueueJellyfinGuideRefresh();
        }

        progress.Report(100);
    }

    private async Task<(IReadOnlySet<string> Dead, StreamCheckSummary Summary)> CheckStreamsAsync(
        IReadOnlyList<ChannelCandidates> groups,
        PluginConfiguration config,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var cache = await StreamCheckCache.LoadAsync(_paths.StreamCheckPath, cancellationToken).ConfigureAwait(false);
        var timeout = TimeSpan.FromSeconds(config.StreamCheckTimeoutSeconds);
        var dead = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        var probed = 0;
        var dropped = 0;
        var done = 0;

        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.StreamCheckConcurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(groups, parallel, async (group, token) =>
        {
            var found = false;
            foreach (var candidate in group.Streams)
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (!cache.TryGetFresh(candidate.Url, now, out var alive))
                {
                    alive = await _streamChecker.IsAliveAsync(candidate.Url, timeout, token).ConfigureAwait(false);
                    cache.Set(candidate.Url, alive, _timeProvider.GetUtcNow().UtcDateTime);
                    Interlocked.Increment(ref probed);
                }

                if (alive)
                {
                    // Streams after the first working one are not needed.
                    found = true;
                    break;
                }

                dead[candidate.Url] = true;
            }

            if (!found)
            {
                Interlocked.Increment(ref dropped);
            }

            progress.Report(100.0 * Interlocked.Increment(ref done) / Math.Max(1, groups.Count));
        }).ConfigureAwait(false);

        try
        {
            await cache.SaveAsync(_paths.StreamCheckPath, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not save the IPTV-org stream check cache");
        }

        var deadSet = new HashSet<string>(dead.Keys, StringComparer.Ordinal);
        return (deadSet, new StreamCheckSummary(probed, deadSet.Count, dropped));
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadGuideNamesAsync(IReadOnlyList<string> languages, CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.NamesPath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var stream = new FileStream(_paths.NamesPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                var file = await JsonSerializer.DeserializeAsync<GuideNamesFile>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

                // Names collected for other language preferences are not used.
                return file is not null && string.Equals(file.Languages, string.Join(',', languages), StringComparison.Ordinal)
                    ? file.Names
                    : new Dictionary<string, string>();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not read the IPTV-org channel names");
            return new Dictionary<string, string>();
        }
    }

    private async Task ApplyGuideNamesAsync(
        IReadOnlyList<PlaylistEntry> baseEntries,
        IReadOnlyDictionary<string, string> found,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        // Names found earlier stay when a source is temporarily unavailable.
        var languages = LanguageCodes.Normalize(config.PreferredLanguages);
        var names = new Dictionary<string, string>(await LoadGuideNamesAsync(languages, cancellationToken).ConfigureAwait(false), StringComparer.Ordinal);
        foreach (var (tvgId, name) in found)
        {
            names[tvgId] = name;
        }

        var file = new GuideNamesFile { Languages = string.Join(',', languages), Names = names };
        await WriteFileAtomicallyAsync(_paths.NamesPath, JsonSerializer.Serialize(file), cancellationToken).ConfigureAwait(false);

        var renamed = ChannelNames.Apply(baseEntries, names, config.PrefixCategoryInName);
        var content = M3uWriter.Write(renamed);
        var current = File.Exists(_paths.PlaylistPath)
            ? await File.ReadAllTextAsync(_paths.PlaylistPath, cancellationToken).ConfigureAwait(false)
            : null;
        if (string.Equals(current, content, StringComparison.Ordinal))
        {
            return;
        }

        await WriteFileAtomicallyAsync(_paths.PlaylistPath, content, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "IPTV-org playlist: guide names in the preferred language applied to {Count} channels",
            renamed.Count(e => e.TvgId is not null && names.ContainsKey(e.TvgId)));
    }

    private static async Task WriteFileAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, Utf8NoBom, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }

    private async Task UpdateGuideCoreAsync(PluginConfiguration config, IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);

        // Planned in a separate method so the large catalog lists can be collected before the downloads.
        var (entries, sources) = await PlanGuideAsync(config, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "IPTV-org guide: {Sources} guide files planned for {Channels} channels",
            sources.Count,
            entries.Count);

        Directory.CreateDirectory(_paths.DataDirectory);
        Directory.CreateDirectory(_paths.GuideDownloadDirectory);

        var tempGuidePath = _paths.GuidePath + ".tmp";
        var used = 0;
        var failed = 0;
        int programmes;
        int coveredChannels;
        IReadOnlyDictionary<string, string> guideNames;

        var output = new FileStream(tempGuidePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await using (output.ConfigureAwait(false))
        {
            var writer = XmlWriter.Create(output, new XmlWriterSettings { Async = true, Encoding = Utf8NoBom, Indent = false });
            await using (writer.ConfigureAwait(false))
            {
                var merger = new XmltvMerger(writer, entries, _timeProvider.GetUtcNow(), LanguageCodes.Normalize(config.PreferredLanguages));
                await merger.WriteHeaderAsync().ConfigureAwait(false);

                for (var i = 0; i < sources.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = sources[i];

                    try
                    {
                        var copied = await AddGuideSourceAsync(merger, source, cancellationToken).ConfigureAwait(false);
                        used++;
                        _logger.LogInformation(
                            "IPTV-org guide: {Host} gave {Programmes} programmes",
                            source.Url.Host,
                            copied);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or IOException or XmlException or InvalidDataException
                        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
                    {
                        // One broken third-party host must not stop the others.
                        failed++;
                        _logger.LogWarning("IPTV-org guide: {Host} failed: {Reason}", source.Url.Host, ex.Message);
                    }

                    progress.Report(100.0 * (i + 1) / Math.Max(1, sources.Count));
                }

                await merger.WriteFooterAsync().ConfigureAwait(false);
                programmes = merger.ProgrammeCount;
                coveredChannels = merger.CoveredChannels.Count;
                guideNames = merger.ChannelNames.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            }
        }

        File.Move(tempGuidePath, _paths.GuidePath, overwrite: true);

        if (config.UseGuideChannelNames)
        {
            await ApplyGuideNamesAsync(entries, guideNames, config, cancellationToken).ConfigureAwait(false);
        }

        UpdateStatus(s =>
        {
            s.LastGuideUtc = _timeProvider.GetUtcNow().UtcDateTime;
            s.GuideChannelCount = coveredChannels;
            s.GuideProgrammeCount = programmes;
            s.GuideSourcesUsed = used;
            s.GuideSourcesFailed = failed;
        });

        _logger.LogInformation(
            "IPTV-org guide built: {Programmes} programmes for {Covered} of {Channels} channels ({Used} files read, {Failed} failed)",
            programmes,
            coveredChannels,
            entries.Count,
            used,
            failed);

        var listingsId = GetRegistration().ListingsProviderId;
        if (listingsId is not null)
        {
            // Without this Jellyfin keeps reading its own copy of the old guide for up to an hour.
            DeleteJellyfinGuideCopy(listingsId);
            QueueJellyfinGuideRefresh();
        }

        progress.Report(100);
    }

    private async Task<(IReadOnlyList<PlaylistEntry> Entries, IReadOnlyList<GuideSource> Sources)> PlanGuideAsync(
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        // The guide follows the last built playlist exactly (including stream checks),
        // and only the small guides.json is parsed here.
        var stream = new FileStream(_paths.EntriesPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        IReadOnlyList<PlaylistEntry> entries;
        await using (stream.ConfigureAwait(false))
        {
            entries = await JsonSerializer.DeserializeAsync<List<PlaylistEntry>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
        }

        var guides = await CatalogLoader.LoadGuidesAsync(_paths.CacheDirectory, cancellationToken).ConfigureAwait(false);
        var sources = GuideSourcePlanner.Plan(entries, guides, config.MaxGuideSources);
        return (entries, sources);
    }

    private async Task<int> AddGuideSourceAsync(XmltvMerger merger, GuideSource source, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(_paths.GuideDownloadDirectory, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp");
        try
        {
            var client = _httpClientFactory.CreateClient(IptvOrgConstants.GuideHttpClientName);
            using (var response = await client.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxGuideFileBytes)
                {
                    throw new InvalidDataException("The guide file is too large.");
                }

                var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (input.ConfigureAwait(false))
                {
                    var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                    await using (file.ConfigureAwait(false))
                    {
                        await CopyWithLimitAsync(input, file, MaxGuideFileBytes, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            var content = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (content.ConfigureAwait(false))
            {
                return await merger.AddSourceAsync(source, content, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static async Task CopyWithLimitAsync(Stream input, Stream output, long limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > limit)
            {
                throw new InvalidDataException("The guide file is too large.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteEmptyGuideAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var tempPath = _paths.GuidePath + ".tmp";
        await File.WriteAllTextAsync(
            tempPath,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<tv generator-info-name=\"Jellyfin IPTV-org plugin\"></tv>\n",
            Utf8NoBom,
            cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _paths.GuidePath, overwrite: true);
    }

    private bool DeleteJellyfinGuideCopy(string listingsId)
    {
        // The id comes from Jellyfin's own configuration (a GUID in "N" format); refuse
        // anything else so it can never point outside the cache directory.
        if (listingsId.Length == 0 || !listingsId.All(char.IsAsciiHexDigit))
        {
            return false;
        }

        var path = Path.Combine(_paths.JellyfinXmlTvCacheDirectory, listingsId + ".xml");
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete Jellyfin's cached copy of the IPTV-org guide");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Could not delete Jellyfin's cached copy of the IPTV-org guide");
            return false;
        }
    }

    private void QueueJellyfinGuideRefresh()
    {
        var worker = _taskManager.ScheduledTasks.FirstOrDefault(t =>
            string.Equals(t.ScheduledTask.Key, IptvOrgConstants.RefreshGuideTaskKey, StringComparison.Ordinal));
        if (worker is null)
        {
            _logger.LogWarning("Jellyfin's guide refresh task was not found; refresh the guide manually");
            return;
        }

        _taskManager.QueueScheduledTask(worker.ScheduledTask, new TaskOptions());
    }

    private void UpdateStatus(Action<PluginStatus> update)
    {
        lock (_statusLock)
        {
            var status = ReadStatus();
            update(status);
            try
            {
                Directory.CreateDirectory(_paths.DataDirectory);
                var tempPath = _paths.StatusPath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(status, StatusJsonOptions), Utf8NoBom);
                File.Move(tempPath, _paths.StatusPath, overwrite: true);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not save the IPTV-org status");
            }
        }
    }

    private PluginStatus ReadStatus()
    {
        try
        {
            if (File.Exists(_paths.StatusPath))
            {
                return JsonSerializer.Deserialize<PluginStatus>(File.ReadAllText(_paths.StatusPath, Utf8NoBom)) ?? new PluginStatus();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read the IPTV-org status; starting fresh");
        }

        return new PluginStatus();
    }
}
