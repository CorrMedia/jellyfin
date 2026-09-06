using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Minimal SRT / WebVTT parse and write for cue-time remapping.
/// </summary>
internal static class CorrSubtitleText
{
    private static readonly Regex TimestampLine = new(
        @"^(?:(\d+):)?(\d{1,2}):(\d{2})[.,](\d{3})\s*-->\s*(?:(\d+):)?(\d{1,2}):(\d{2})[.,](\d{3})(.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Returns true when <paramref name="format"/> is a text subtitle format we can rewrite.
    /// </summary>
    /// <param name="format">Requested output format (vtt, srt, …).</param>
    /// <returns>True for WebVTT, SubRip, and Jellyfin's JSON <c>Stream.js</c> track.</returns>
    public static bool IsRewritableFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        return format.Equals("vtt", StringComparison.OrdinalIgnoreCase)
            || format.Equals("webvtt", StringComparison.OrdinalIgnoreCase)
            || format.Equals("srt", StringComparison.OrdinalIgnoreCase)
            || format.Equals("subrip", StringComparison.OrdinalIgnoreCase)
            || IsJson(format);
    }

    /// <summary>
    /// Parses SRT or WebVTT into cues. Unknown layouts yield an empty list.
    /// </summary>
    /// <param name="text">Subtitle file text.</param>
    /// <param name="format">vtt or srt.</param>
    /// <returns>Cues on the file's timeline.</returns>
    public static IReadOnlyList<CorrCue> Parse(string text, string format)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsJson(format))
        {
            return ParseJson(text);
        }

        var isVtt = IsVtt(format);
        var cues = new List<CorrCue>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (line.Length == 0
                || line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("STYLE", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("REGION", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            var id = string.Empty;
            if (!TimestampLine.IsMatch(line) && i + 1 < lines.Length && TimestampLine.IsMatch(lines[i + 1].Trim()))
            {
                id = line;
                i++;
                line = lines[i].Trim();
            }

            var match = TimestampLine.Match(line);
            if (!match.Success)
            {
                i++;
                continue;
            }

            var start = ParseTimestamp(match, 1);
            var end = ParseTimestamp(match, 5);
            var settings = match.Groups[9].Value.Trim();
            i++;
            var payload = new StringBuilder();
            while (i < lines.Length && lines[i].Trim().Length > 0)
            {
                if (payload.Length > 0)
                {
                    payload.Append('\n');
                }

                payload.Append(lines[i].TrimEnd());
                i++;
            }

            var textPayload = payload.ToString();
            if (end > start)
            {
                cues.Add(new CorrCue(start, end, textPayload, id, isVtt ? settings : string.Empty));
            }
        }

        return cues;
    }

    /// <summary>
    /// Serializes cues as WebVTT or SubRip.
    /// </summary>
    /// <param name="cues">Cues to write.</param>
    /// <param name="format">vtt or srt.</param>
    /// <returns>Subtitle file text.</returns>
    public static string Write(IReadOnlyList<CorrCue> cues, string format)
    {
        ArgumentNullException.ThrowIfNull(cues);
        if (IsJson(format))
        {
            return WriteJson(cues);
        }

        var isVtt = IsVtt(format);
        var sb = new StringBuilder();
        if (isVtt)
        {
            sb.Append("WEBVTT\n\n");
        }

        var index = 1;
        foreach (var cue in cues)
        {
            if (!isVtt)
            {
                sb.Append(index.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            else if (!string.IsNullOrEmpty(cue.Id) && !TimestampLine.IsMatch(cue.Id))
            {
                sb.Append(cue.Id).Append('\n');
            }

            sb.Append(FormatTimestamp(cue.Start, isVtt))
                .Append(" --> ")
                .Append(FormatTimestamp(cue.End, isVtt));
            if (isVtt && !string.IsNullOrEmpty(cue.Settings))
            {
                sb.Append(' ').Append(cue.Settings);
            }

            sb.Append('\n')
                .Append(cue.Text)
                .Append("\n\n");
            index++;
        }

        return sb.ToString();
    }

    private static bool IsVtt(string format)
        => format.Equals("vtt", StringComparison.OrdinalIgnoreCase)
            || format.Equals("webvtt", StringComparison.OrdinalIgnoreCase);

    private static bool IsJson(string format)
        => format.Equals("json", StringComparison.OrdinalIgnoreCase)
            || format.Equals("js", StringComparison.OrdinalIgnoreCase);

    private static List<CorrCue> ParseJson(string text)
    {
        try
        {
            var file = JsonSerializer.Deserialize<JsonSubtitleFile>(text, JsonOptions);
            if (file?.TrackEvents is null)
            {
                return [];
            }

            var cues = new List<CorrCue>(file.TrackEvents.Count);
            foreach (var ev in file.TrackEvents)
            {
                var start = ev.StartPositionTicks / (double)TimeSpan.TicksPerSecond;
                var end = ev.EndPositionTicks / (double)TimeSpan.TicksPerSecond;
                if (end <= start)
                {
                    continue;
                }

                cues.Add(new CorrCue(start, end, ev.Text ?? string.Empty, ev.Id ?? string.Empty));
            }

            return cues;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string WriteJson(IReadOnlyList<CorrCue> cues)
    {
        var file = new JsonSubtitleFile();
        foreach (var cue in cues)
        {
            file.TrackEvents.Add(new JsonSubtitleEvent
            {
                Id = cue.Id,
                Text = cue.Text,
                StartPositionTicks = (long)Math.Round(cue.Start * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero),
                EndPositionTicks = (long)Math.Round(cue.End * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero)
            });
        }

        return JsonSerializer.Serialize(file, JsonOptions);
    }

    private static double ParseTimestamp(Match match, int hourGroup)
    {
        var hours = match.Groups[hourGroup].Success && match.Groups[hourGroup].Length > 0
            ? int.Parse(match.Groups[hourGroup].Value, CultureInfo.InvariantCulture)
            : 0;
        var minutes = int.Parse(match.Groups[hourGroup + 1].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[hourGroup + 2].Value, CultureInfo.InvariantCulture);
        var ms = int.Parse(match.Groups[hourGroup + 3].Value, CultureInfo.InvariantCulture);
        return (hours * 3600) + (minutes * 60) + seconds + (ms / 1000.0);
    }

    private static string FormatTimestamp(double seconds, bool vtt)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        var ts = TimeSpan.FromSeconds(seconds);
        var sep = vtt ? "." : ",";
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}:{1:00}:{2:00}{3}{4:000}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds,
            sep,
            ts.Milliseconds);
    }

    private sealed class JsonSubtitleFile
    {
        public List<JsonSubtitleEvent> TrackEvents { get; set; } = [];
    }

    private sealed class JsonSubtitleEvent
    {
        public string? Id { get; set; }

        public string? Text { get; set; }

        public long StartPositionTicks { get; set; }

        public long EndPositionTicks { get; set; }
    }
}
