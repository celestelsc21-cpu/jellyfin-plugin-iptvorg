using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Jellyfin.Plugin.IptvOrg.Configuration;
using Jellyfin.Plugin.IptvOrg.Guide;
using Jellyfin.Plugin.IptvOrg.Playlist;

namespace Jellyfin.Plugin.IptvOrg.Foss;

/// <summary>
/// Builds a separate OTT-play FOSS playlist and per-channel JSON guide from the existing Jellyfin files.
/// </summary>
internal sealed class FossExporter : IFossExporter
{
    private readonly PluginPaths _paths;
    private readonly IPluginConfigurationAccessor _config;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes the exporter.
    /// </summary>
    /// <param name="paths">Plugin data paths.</param>
    /// <param name="config">Current plugin configuration.</param>
    public FossExporter(PluginPaths paths, IPluginConfigurationAccessor config)
    {
        _paths = paths;
        _config = config;
    }

    /// <summary>Gets the separate FOSS export folder.</summary>
    public string DirectoryPath => Path.Combine(_paths.DataDirectory, "foss");

    /// <summary>Gets the FOSS M3U file.</summary>
    public string PlaylistPath => Path.Combine(DirectoryPath, "playlist.m3u");

    /// <summary>Gets the FOSS JSON EPG folder.</summary>
    public string EpgDirectoryPath => Path.Combine(DirectoryPath, "epg");

    /// <summary>
    /// Creates both FOSS outputs from the current Jellyfin channel list and XMLTV guide.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of channels with programmes, or <c>null</c> if a build is running.</returns>
    public async Task<int?> BuildAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            if (!File.Exists(_paths.EntriesPath) || !File.Exists(_paths.GuidePath))
            {
                throw new InvalidOperationException("Сначала сформируйте каналы и XMLTV-телегид Jellyfin.");
            }

            var entries = JsonSerializer.Deserialize<List<PlaylistEntry>>(
                await File.ReadAllTextAsync(_paths.EntriesPath, cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidDataException("Не удалось прочитать список каналов.");
            var settings = _config.Current;
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            if (settings.UseGuideChannelNames && File.Exists(_paths.NamesPath))
            {
                var cached = JsonSerializer.Deserialize<GuideNamesFile>(
                    await File.ReadAllTextAsync(_paths.NamesPath, cancellationToken).ConfigureAwait(false));
                if (cached is not null)
                {
                    names = cached.Names;
                }
            }

            var cleaned = entries.Select(e =>
            {
                var prefix = "[" + e.CategoryName + "] ";
                var name = e.Name.StartsWith(prefix, StringComparison.Ordinal)
                    ? e.Name[prefix.Length..]
                    : e.Name;
                if (e.TvgId is not null && names.TryGetValue(e.TvgId, out var guideName)
                    && !string.IsNullOrWhiteSpace(guideName))
                {
                    name = guideName.Trim();
                }

                return e with { Name = name };
            }).ToArray();

            var validIds = cleaned.Where(e => e.TvgId is not null)
                .Select(e => e.TvgId!)
                .ToHashSet(StringComparer.Ordinal);
            var programmes = new Dictionary<string, List<FossProgramme>>(StringComparer.Ordinal);
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
                IgnoreComments = true
            };
            using (var input = File.OpenRead(_paths.GuidePath))
            using (var reader = XmlReader.Create(input, readerSettings))
            {
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "programme")
                    {
                        continue;
                    }

                    var id = reader.GetAttribute("channel");
                    if (id is null || !validIds.Contains(id))
                    {
                        reader.Skip();
                        continue;
                    }

                    var element = (XElement)XNode.ReadFrom(reader);
                    if (!XmltvMerger.TryParseXmltvTime((string?)element.Attribute("start"), out var from)
                        || !XmltvMerger.TryParseXmltvTime((string?)element.Attribute("stop"), out var to)
                        || to <= from)
                    {
                        continue;
                    }

                    var title = element.Element("title")?.Value ?? string.Empty;
                    if (title.Length == 0)
                    {
                        continue;
                    }

                    if (!programmes.TryGetValue(id, out var list))
                    {
                        list = [];
                        programmes[id] = list;
                    }

                    list.Add(new FossProgramme(
                        title,
                        element.Element("desc")?.Value ?? string.Empty,
                        from.ToUnixTimeSeconds(),
                        to.ToUnixTimeSeconds(),
                        (string?)element.Element("icon")?.Attribute("src")));
                }
            }

            var staging = DirectoryPath + ".building";
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            Directory.CreateDirectory(Path.Combine(staging, "epg"));
            try
            {
                var m3u = WritePlaylist(cleaned, programmes.Keys.ToHashSet(StringComparer.Ordinal));
                await File.WriteAllTextAsync(Path.Combine(staging, "playlist.m3u"), m3u, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                foreach (var (id, list) in programmes)
                {
                    var hashed = HashChannelId(id).ToString(CultureInfo.InvariantCulture);
                    var path = Path.Combine(staging, "epg", hashed + ".json");
                    var output = JsonSerializer.Serialize(new FossGuide(list.OrderBy(e => e.Time).ToArray()));
                    await File.WriteAllTextAsync(path, output, cancellationToken).ConfigureAwait(false);
                }

                var old = DirectoryPath + ".old";
                if (Directory.Exists(old))
                {
                    Directory.Delete(old, recursive: true);
                }

                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Move(DirectoryPath, old);
                }

                Directory.Move(staging, DirectoryPath);
                if (Directory.Exists(old))
                {
                    Directory.Delete(old, recursive: true);
                }
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }
            }

            return programmes.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Writes the separate M3U without category prefixes and with explicit direct FOSS-EPG bindings.
    /// </summary>
    /// <param name="entries">Selected Jellyfin channels.</param>
    /// <param name="guideIds">Channel IDs with generated FOSS EPG files.</param>
    /// <returns>Unlinked M3U, ready for the HTTP endpoint to attach its own base URL.</returns>
    internal static string WritePlaylist(IReadOnlyList<PlaylistEntry> entries, IReadOnlySet<string> guideIds)
    {
        var output = new StringBuilder("#EXTM3U\n");
        foreach (var entry in entries)
        {
            output.Append("#EXTINF:-1");
            Attribute("tvg-id", entry.TvgId);
            Attribute("tvg-name", entry.Name);
            Attribute("tvg-chno", entry.Number.ToString(CultureInfo.InvariantCulture));
            Attribute("tvg-logo", entry.LogoUrl);
            Attribute("group-title", entry.CategoryName);
            if (entry.TvgId is not null && guideIds.Contains(entry.TvgId))
            {
                Attribute("tvg-source", "=iptvorg");
            }

            output.Append(',').Append(M3uWriter.SanitizeName(entry.Name)).Append('\n')
                .Append(entry.Url.Replace('\r', ' ').Replace('\n', ' ')).Append('\n');
        }

        return output.ToString();

        void Attribute(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                output.Append(' ').Append(key).Append("=\"")
                    .Append(value.Replace('\r', ' ').Replace('\n', ' ').Replace("\"", string.Empty, StringComparison.Ordinal).Trim())
                    .Append('"');
            }
        }
    }

    /// <summary>
    /// Computes the exact xxHash32 used for OTT-play FOSS channel filenames.
    /// </summary>
    /// <param name="id">XMLTV channel ID.</param>
    /// <returns>Unsigned 32-bit hash.</returns>
    internal static uint HashChannelId(string id)
    {
        const uint p1 = 2654435761U;
        const uint p2 = 2246822519U;
        const uint p3 = 3266489917U;
        const uint p4 = 668265263U;
        const uint p5 = 374761393U;
        var bytes = Encoding.UTF8.GetBytes(id);
        var offset = 0;
        uint h;
        if (bytes.Length >= 16)
        {
            uint v1 = unchecked(p1 + p2);
            uint v2 = p2;
            uint v3 = 0;
            uint v4 = unchecked(0U - p1);
            while (offset <= bytes.Length - 16)
            {
                v1 = Round(v1, Read());
                v2 = Round(v2, Read());
                v3 = Round(v3, Read());
                v4 = Round(v4, Read());
            }

            h = unchecked(Rotate(v1, 1) + Rotate(v2, 7) + Rotate(v3, 12) + Rotate(v4, 18));
        }
        else
        {
            h = p5;
        }

        h = unchecked(h + (uint)bytes.Length);
        while (offset <= bytes.Length - 4)
        {
            h = unchecked(Rotate(h + (Read() * p3), 17) * p4);
        }

        while (offset < bytes.Length)
        {
            h = unchecked(Rotate(h + ((uint)bytes[offset++] * p5), 11) * p1);
        }

        h ^= h >> 15;
        h = unchecked(h * p2);
        h ^= h >> 13;
        h = unchecked(h * p3);
        h ^= h >> 16;
        return h;

        uint Read()
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            offset += 4;
            return value;
        }

        static uint Round(uint acc, uint val)
            => unchecked(Rotate(acc + (val * p2), 13) * p1);

        static uint Rotate(uint value, int count)
            => (value << count) | (value >> (32 - count));
    }

    private sealed record FossGuide([property: JsonPropertyName("epg_data")] IReadOnlyList<FossProgramme> Data);

    private sealed record FossProgramme(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("descr")] string Description,
        [property: JsonPropertyName("time")] long Time,
        [property: JsonPropertyName("time_to")] long TimeTo,
        [property: JsonPropertyName("icon")] string? Icon);
}
