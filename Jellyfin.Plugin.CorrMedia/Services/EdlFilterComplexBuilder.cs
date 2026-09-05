#if PATCHED_CORE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.CorrMedia.Models;
using MediaBrowser.Controller.MediaEncoding;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Builds FFmpeg mute-then-cut graphs (NLE order: mute on original timeline, then omit skip ranges).
/// </summary>
internal static class EdlFilterComplexBuilder
{
    /// <summary>
    /// Builds a session edit graph, or null if there are no cuts.
    /// </summary>
    /// <param name="plan">EDL plan.</param>
    /// <param name="durationSeconds">Media duration.</param>
    /// <param name="editedStartSeconds">Start offset on the edited (post-cut) timeline, e.g. client seek.</param>
    /// <returns>Graph or null.</returns>
    public static SessionMediaEditGraph? Build(EdlEditPlan plan, double durationSeconds, double editedStartSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.HasCuts)
        {
            return null;
        }

        if (durationSeconds <= 0)
        {
            // Fallback so keep-tail after last skip still works for short test clips.
            durationSeconds = plan.SkipRanges.Max(r => r.EndTime) + 3600;
        }

        var keep = BuildKeepRanges(plan.SkipRanges, durationSeconds);
        if (editedStartSeconds > 0.001)
        {
            keep = TruncateKeepRangesForEditedStart(keep, editedStartSeconds);
        }

        if (keep.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();

        // 1) Mute on the original timeline (NLE: effects before cuts).
        // FFmpeg labeled pads are single-use — asplit/split before per-segment trim.
        if (plan.MuteRanges.Count > 0)
        {
            var expr = BuildVolumeExpression(plan.MuteRanges);
            sb.Append("[0:a]volume='").Append(expr).Append("':eval=frame[amuted];");
        }
        else
        {
            sb.Append("[0:a]anull[amuted];");
        }

        sb.Append(CultureInfo.InvariantCulture, $"[amuted]asplit={keep.Count}");
        for (var i = 0; i < keep.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[am{i}]");
        }

        sb.Append(';');
        sb.Append(CultureInfo.InvariantCulture, $"[0:v]split={keep.Count}");
        for (var i = 0; i < keep.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[vs{i}]");
        }

        sb.Append(';');

        // 2) Cut: trim keep segments from video + muted audio, then concat.
        for (var i = 0; i < keep.Count; i++)
        {
            var (start, end) = keep[i];
            var s = start.ToString(CultureInfo.InvariantCulture);
            var e = end.ToString(CultureInfo.InvariantCulture);
            sb.Append(CultureInfo.InvariantCulture, $"[vs{i}]trim=start={s}:end={e},setpts=PTS-STARTPTS[v{i}];");
            sb.Append(CultureInfo.InvariantCulture, $"[am{i}]atrim=start={s}:end={e},asetpts=PTS-STARTPTS[a{i}];");
        }

        for (var i = 0; i < keep.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[v{i}][a{i}]");
        }

        sb.Append(CultureInfo.InvariantCulture, $"concat=n={keep.Count}:v=1:a=1[vout][aout]");

        return new SessionMediaEditGraph
        {
            FilterComplex = sb.ToString(),
            VideoMapLabel = "vout",
            AudioMapLabel = "aout"
        };
    }

    /// <summary>
    /// Computes keep intervals as the complement of skip ranges on [0, duration).
    /// </summary>
    /// <param name="skipRanges">Skip ranges to omit.</param>
    /// <param name="durationSeconds">Media duration in seconds.</param>
    /// <returns>Keep intervals on the original timeline.</returns>
    internal static IReadOnlyList<(double Start, double End)> BuildKeepRanges(
        IReadOnlyList<MuteTimeRange> skipRanges,
        double durationSeconds)
    {
        var merged = MergeRanges(skipRanges);
        var keep = new List<(double, double)>();
        var cursor = 0.0;
        foreach (var skip in merged)
        {
            if (skip.StartTime > cursor)
            {
                keep.Add((cursor, Math.Min(skip.StartTime, durationSeconds)));
            }

            cursor = Math.Max(cursor, skip.EndTime);
            if (cursor >= durationSeconds)
            {
                break;
            }
        }

        if (cursor < durationSeconds)
        {
            keep.Add((cursor, durationSeconds));
        }

        return keep.Where(k => k.Item2 > k.Item1 + 0.001).ToList();
    }

    /// <summary>
    /// Drops keep media that falls before <paramref name="editedStartSeconds"/> on the post-cut timeline.
    /// </summary>
    /// <param name="keep">Keep intervals on the original timeline.</param>
    /// <param name="editedStartSeconds">Offset on the edited timeline to start from.</param>
    /// <returns>Keep intervals starting at the seek point.</returns>
    internal static IReadOnlyList<(double Start, double End)> TruncateKeepRangesForEditedStart(
        IReadOnlyList<(double Start, double End)> keep,
        double editedStartSeconds)
    {
        if (editedStartSeconds <= 0 || keep.Count == 0)
        {
            return keep;
        }

        var result = new List<(double, double)>();
        var editedCursor = 0.0;
        var started = false;
        foreach (var (start, end) in keep)
        {
            var len = end - start;
            if (len <= 0)
            {
                continue;
            }

            if (!started)
            {
                if (editedCursor + len <= editedStartSeconds + 0.001)
                {
                    editedCursor += len;
                    continue;
                }

                var into = Math.Max(0, editedStartSeconds - editedCursor);
                result.Add((start + into, end));
                started = true;
            }
            else
            {
                result.Add((start, end));
            }

            editedCursor += len;
        }

        return result.Where(k => k.Item2 > k.Item1 + 0.001).ToList();
    }

    private static List<MuteTimeRange> MergeRanges(IReadOnlyList<MuteTimeRange> ranges)
    {
        var sorted = ranges.Where(r => r.EndTime > r.StartTime).OrderBy(r => r.StartTime).ToList();
        var merged = new List<MuteTimeRange>();
        foreach (var r in sorted)
        {
            if (merged.Count == 0 || r.StartTime > merged[^1].EndTime)
            {
                merged.Add(r);
            }
            else if (r.EndTime > merged[^1].EndTime)
            {
                merged[^1] = new MuteTimeRange(merged[^1].StartTime, r.EndTime);
            }
        }

        return merged;
    }

    private static string BuildVolumeExpression(IReadOnlyList<MuteTimeRange> ranges)
    {
        var sorted = ranges.OrderBy(r => r.StartTime).ToList();
        var expr = "1";
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            var r = sorted[i];
            var s = r.StartTime.ToString(CultureInfo.InvariantCulture);
            var e = r.EndTime.ToString(CultureInfo.InvariantCulture);
            expr = "if(between(t," + s + "," + e + "),0," + expr + ")";
        }

        return expr;
    }
}

#endif
