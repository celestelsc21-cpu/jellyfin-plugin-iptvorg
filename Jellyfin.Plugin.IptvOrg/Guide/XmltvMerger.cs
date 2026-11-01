using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Jellyfin.Plugin.IptvOrg.Playlist;

namespace Jellyfin.Plugin.IptvOrg.Guide;

/// <summary>
/// Streams XMLTV guide files and writes one merged guide that only contains the
/// playlist channels. The first source that has programmes for a channel wins;
/// later sources only fill channels that are still empty (fallback).
/// </summary>
internal sealed partial class XmltvMerger
{
    private static readonly string[] LocalizedElements = ["title", "sub-title", "desc"];
    private static readonly HashSet<string> CyrillicLanguages = new(StringComparer.OrdinalIgnoreCase) { "ru", "uk", "be", "bg", "sr", "mk", "kk", "ky", "tg", "mn" };
    private static readonly string[] TimeFormats = ["yyyyMMddHHmmss zzz", "yyyyMMddHHmmsszzz", "yyyyMMddHHmmss", "yyyyMMddHHmm zzz"];

    private readonly XmlWriter _writer;
    private readonly IReadOnlyList<PlaylistEntry> _entries;
    private readonly HashSet<string> _covered = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _channelNames = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<string> _preferredLanguages;
    private readonly DateTimeOffset _keepFrom;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmltvMerger"/> class and writes the
    /// guide header and the channel list.
    /// </summary>
    /// <param name="writer">Target writer (async-enabled).</param>
    /// <param name="entries">Playlist entries.</param>
    /// <param name="now">Current time; programmes that ended more than 6 hours earlier are dropped.</param>
    /// <param name="preferredLanguages">Preferred languages (ISO 639-1), most preferred first; empty keeps everything as is.</param>
    public XmltvMerger(XmlWriter writer, IReadOnlyList<PlaylistEntry> entries, DateTimeOffset now, IReadOnlyList<string>? preferredLanguages = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entries);

        _writer = writer;
        _keepFrom = now.AddHours(-6);
        _preferredLanguages = preferredLanguages ?? [];
        _entries = entries;
    }

    /// <summary>
    /// Gets the number of programmes written.
    /// </summary>
    public int ProgrammeCount { get; private set; }

    /// <summary>
    /// Gets the playlist channels that have at least one programme.
    /// </summary>
    public IReadOnlyCollection<string> CoveredChannels => _covered;

    /// <summary>
    /// Gets the channel names in the preferred language found in the guide files, by playlist <c>tvg-id</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> ChannelNames => _channelNames;

    /// <summary>
    /// Writes the document start and the channel elements.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task WriteHeaderAsync()
    {
        await _writer.WriteStartDocumentAsync().ConfigureAwait(false);
        await _writer.WriteStartElementAsync(null, "tv", null).ConfigureAwait(false);
        await _writer.WriteAttributeStringAsync(null, "generator-info-name", null, "Jellyfin IPTV-org plugin").ConfigureAwait(false);

        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            if (entry.TvgId is null || !written.Add(entry.TvgId))
            {
                continue;
            }

            await _writer.WriteStartElementAsync(null, "channel", null).ConfigureAwait(false);
            await _writer.WriteAttributeStringAsync(null, "id", null, entry.TvgId).ConfigureAwait(false);
            await _writer.WriteElementStringAsync(null, "display-name", null, entry.Name).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(entry.LogoUrl))
            {
                await _writer.WriteStartElementAsync(null, "icon", null).ConfigureAwait(false);
                await _writer.WriteAttributeStringAsync(null, "src", null, entry.LogoUrl).ConfigureAwait(false);
                await _writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            await _writer.WriteEndElementAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Copies the programmes of still uncovered playlist channels from one guide file.
    /// </summary>
    /// <param name="source">The source description.</param>
    /// <param name="content">Raw (possibly gzip-compressed) file content; must be seekable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of programmes copied from this source.</returns>
    public async Task<int> AddSourceAsync(GuideSource source, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(content);

        // Only channels that no earlier source filled take part (fallback order).
        var active = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (sourceId, targets) in source.IdMap)
        {
            foreach (var target in targets)
            {
                if (_covered.Contains(target))
                {
                    continue;
                }

                if (!active.TryGetValue(sourceId, out var list))
                {
                    list = [];
                    active[sourceId] = list;
                }

                list.Add(target);
            }
        }

        if (active.Count == 0)
        {
            return 0;
        }

        var filledNow = new HashSet<string>(StringComparer.Ordinal);
        var copied = 0;

        // Trust the bytes, not the label: some hosts serve .gz files with Content-Encoding
        // (already decoded by HttpClient), others serve .xml files gzip-compressed.
        var isGzip = await IsGzipAsync(content, cancellationToken).ConfigureAwait(false);
        Stream input = isGzip ? new GZipStream(content, CompressionMode.Decompress, leaveOpen: true) : content;
        try
        {
            var settings = new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                IgnoreProcessingInstructions = true,
                CloseInput = false
            };

            using var reader = XmlReader.Create(input, settings);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Keep the loop on the outer level: channels and programmes are direct children of <tv>.
                while (reader.NodeType == XmlNodeType.Element && reader.Depth == 1)
                {
                    if (string.Equals(reader.LocalName, "channel", StringComparison.Ordinal))
                    {
                        var id = reader.GetAttribute("id");
                        if (id is null || !source.IdMap.TryGetValue(id, out var named))
                        {
                            await reader.SkipAsync().ConfigureAwait(false);
                            continue;
                        }

                        var channel = (XElement)await XNode.ReadFromAsync(reader, cancellationToken).ConfigureAwait(false);
                        var preferred = FindPreferredName(channel, _preferredLanguages);
                        if (preferred is not null)
                        {
                            foreach (var target in named)
                            {
                                _channelNames.TryAdd(target, preferred);
                            }
                        }

                        continue;
                    }

                    if (!string.Equals(reader.LocalName, "programme", StringComparison.Ordinal))
                    {
                        break;
                    }

                    var channelId = reader.GetAttribute("channel");
                    if (channelId is null || !active.TryGetValue(channelId, out var targets) || !IsCurrent(reader.GetAttribute("stop")))
                    {
                        await reader.SkipAsync().ConfigureAwait(false);
                        continue;
                    }

                    var programme = (XElement)await XNode.ReadFromAsync(reader, cancellationToken).ConfigureAwait(false);
                    KeepPreferredLanguage(programme, _preferredLanguages);
                    foreach (var target in targets)
                    {
                        programme.SetAttributeValue("channel", target);
                        await programme.WriteToAsync(_writer, cancellationToken).ConfigureAwait(false);
                        filledNow.Add(target);
                        copied++;
                    }
                }
            }
        }
        finally
        {
            if (!ReferenceEquals(input, content))
            {
                await input.DisposeAsync().ConfigureAwait(false);
            }
        }

        _covered.UnionWith(filledNow);
        ProgrammeCount += copied;
        return copied;
    }

    /// <summary>
    /// Closes the document.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task WriteFooterAsync()
    {
        await _writer.WriteEndElementAsync().ConfigureAwait(false);
        await _writer.WriteEndDocumentAsync().ConfigureAwait(false);
        await _writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Parses an XMLTV time stamp such as <c>20261101180000 +0300</c>.
    /// </summary>
    /// <param name="value">Time stamp.</param>
    /// <param name="result">Parsed value.</param>
    /// <returns><c>true</c> when parsed.</returns>
    internal static bool TryParseXmltvTime(string? value, out DateTimeOffset result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        // "+0300" is not understood by "zzz"; insert the colon.
        if (text.Length >= 5 && (text[^5] == '+' || text[^5] == '-') && char.IsAsciiDigit(text[^1]))
        {
            text = string.Concat(text.AsSpan(0, text.Length - 2), ":", text.AsSpan(text.Length - 2));
        }

        return DateTimeOffset.TryParseExact(
            text,
            TimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out result);
    }

    private static async Task<bool> IsGzipAsync(Stream content, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
        {
            return false;
        }

        var start = content.Position;
        var header = new byte[2];
        var read = await content.ReadAtLeastAsync(header, 2, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        content.Position = start;
        return read == 2 && header[0] == 0x1F && header[1] == 0x8B;
    }

    /// <summary>
    /// Picks the channel's <c>display-name</c> in the most preferred language: first one marked
    /// with that language, otherwise (for the first preference only) an unmarked name written
    /// in the matching script (Cyrillic for <c>ru</c>, <c>uk</c>, <c>be</c>…; Latin otherwise).
    /// </summary>
    /// <param name="channel">The channel element.</param>
    /// <param name="preferredLanguages">Preferred languages, most preferred first.</param>
    /// <returns>The name, or <c>null</c>.</returns>
    internal static string? FindPreferredName(XElement channel, IReadOnlyList<string> preferredLanguages)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(preferredLanguages);

        if (preferredLanguages.Count == 0)
        {
            return null;
        }

        var names = channel.Elements("display-name")
            .Select(e => (Name: e.Value.Trim(), Lang: (string?)e.Attribute("lang")))
            .Where(n => n.Name.Length > 0)
            .ToArray();

        foreach (var language in preferredLanguages)
        {
            foreach (var (name, lang) in names)
            {
                if (LanguageMatches(lang, language))
                {
                    return name;
                }
            }
        }

        var cyrillic = CyrillicLanguages.Contains(preferredLanguages[0]);
        foreach (var (name, lang) in names)
        {
            if (string.IsNullOrEmpty(lang) && CyrillicRegex().IsMatch(name) == cyrillic)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// Keeps one title, sub-title and description per programme, in the most preferred language
    /// available. Elements stay untouched when none of them is in a preferred language.
    /// </summary>
    /// <param name="programme">Programme element (modified in place).</param>
    /// <param name="preferredLanguages">Preferred languages, most preferred first.</param>
    internal static void KeepPreferredLanguage(XElement programme, IReadOnlyList<string> preferredLanguages)
    {
        ArgumentNullException.ThrowIfNull(programme);
        ArgumentNullException.ThrowIfNull(preferredLanguages);

        if (preferredLanguages.Count == 0)
        {
            return;
        }

        foreach (var elementName in LocalizedElements)
        {
            var elements = programme.Elements(elementName).ToList();
            if (elements.Count <= 1)
            {
                continue;
            }

            XElement? keep = null;
            foreach (var language in preferredLanguages)
            {
                keep = elements.Find(e => LanguageMatches((string?)e.Attribute("lang"), language));
                if (keep is not null)
                {
                    break;
                }
            }

            if (keep is null)
            {
                continue;
            }

            foreach (var element in elements)
            {
                if (!ReferenceEquals(element, keep))
                {
                    element.Remove();
                }
            }
        }
    }

    private static bool LanguageMatches(string? lang, string preferred)
    {
        if (string.IsNullOrEmpty(lang))
        {
            return false;
        }

        // "ru" matches "ru" and "ru-RU".
        return lang.StartsWith(preferred, StringComparison.OrdinalIgnoreCase)
            && (lang.Length == preferred.Length || lang[preferred.Length] == '-' || lang[preferred.Length] == '_');
    }

    [GeneratedRegex(@"\p{IsCyrillic}", RegexOptions.CultureInvariant)]
    private static partial Regex CyrillicRegex();

    private bool IsCurrent(string? stop)
    {
        // Unknown format: keep the programme rather than lose data.
        return !TryParseXmltvTime(stop, out var end) || end >= _keepFrom;
    }
}
