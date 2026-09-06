using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Original-timeline keep/cut math. Skip ranges omit media; remaining keep intervals
/// are the complement on <c>[0, duration)</c>.
/// </summary>
internal static class CorrTimeline
{
    /// <summary>
    /// Resolves uncut source duration. The HLS media source reports the shortened
    /// (post-cut) runtime, which must not be used as trim EOF.
    /// </summary>
    /// <param name="plan">Sidecar edit plan.</param>
    /// <param name="mediaSourceDurationSeconds">Duration from the encoding job (edited when cuts exist).</param>
    /// <returns>Original-timeline duration in seconds.</returns>
    public static double ResolveOriginalDuration(CorrEditPlan plan, double mediaSourceDurationSeconds)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.OriginalDurationSeconds > 0.001)
        {
            return plan.OriginalDurationSeconds;
        }

        if (!plan.HasCuts || mediaSourceDurationSeconds <= 0)
        {
            return mediaSourceDurationSeconds;
        }

        return mediaSourceDurationSeconds + MergedSkipSeconds(plan.SkipRanges);
    }

    /// <summary>
    /// Total seconds omitted by skip ranges after merging overlaps.
    /// </summary>
    /// <param name="skipRanges">Skip ranges to omit.</param>
    /// <returns>Merged skip duration in seconds.</returns>
    public static double MergedSkipSeconds(IReadOnlyList<MuteTimeRange> skipRanges)
    {
        ArgumentNullException.ThrowIfNull(skipRanges);
        var skipSeconds = 0.0;
        foreach (var skip in MergeRanges(skipRanges))
        {
            skipSeconds += skip.EndTime - skip.StartTime;
        }

        return skipSeconds;
    }

    /// <summary>
    /// Computes keep intervals as the complement of skip ranges on [0, duration).
    /// Overlapping and touching skips are merged first.
    /// </summary>
    /// <param name="skipRanges">Skip ranges to omit.</param>
    /// <param name="durationSeconds">Media duration in seconds.</param>
    /// <returns>Keep intervals on the original timeline.</returns>
    public static IReadOnlyList<(double Start, double End)> BuildKeepRanges(
        IReadOnlyList<MuteTimeRange> skipRanges,
        double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(skipRanges);
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
    public static IReadOnlyList<(double Start, double End)> TruncateKeepRangesForEditedStart(
        IReadOnlyList<(double Start, double End)> keep,
        double editedStartSeconds)
    {
        ArgumentNullException.ThrowIfNull(keep);
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

    /// <summary>
    /// Maps a time on the original timeline onto the edited (post-cut) clock.
    /// Returns false when <paramref name="originalSeconds"/> falls inside a skip.
    /// </summary>
    /// <param name="originalSeconds">Time on the original source timeline.</param>
    /// <param name="keep">Keep intervals on the original timeline.</param>
    /// <param name="editedSeconds">Edited-timeline time when the method returns true.</param>
    /// <returns>True when the instant survives the cuts.</returns>
    public static bool TryMapOriginalToEdited(
        double originalSeconds,
        IReadOnlyList<(double Start, double End)> keep,
        out double editedSeconds)
    {
        ArgumentNullException.ThrowIfNull(keep);
        editedSeconds = 0;
        var edited = 0.0;
        foreach (var (start, end) in keep)
        {
            if (originalSeconds < start - 0.001)
            {
                return false;
            }

            if (originalSeconds <= end + 0.001)
            {
                editedSeconds = edited + Math.Max(0, originalSeconds - start);
                return true;
            }

            edited += end - start;
        }

        return false;
    }

    /// <summary>
    /// Maps a time on the edited timeline back onto the original source clock.
    /// </summary>
    /// <param name="editedSeconds">Time on the post-cut timeline.</param>
    /// <param name="keep">Keep intervals on the original timeline.</param>
    /// <param name="originalSeconds">Original-timeline time when the method returns true.</param>
    /// <returns>True when the instant maps into a keep range.</returns>
    public static bool TryMapEditedToOriginal(
        double editedSeconds,
        IReadOnlyList<(double Start, double End)> keep,
        out double originalSeconds)
    {
        ArgumentNullException.ThrowIfNull(keep);
        originalSeconds = 0;
        if (editedSeconds < -0.001)
        {
            return false;
        }

        var cursor = 0.0;
        foreach (var (start, end) in keep)
        {
            var len = end - start;
            if (len <= 0)
            {
                continue;
            }

            if (editedSeconds <= cursor + len + 0.001)
            {
                originalSeconds = start + Math.Max(0, editedSeconds - cursor);
                return true;
            }

            cursor += len;
        }

        return false;
    }

    /// <summary>
    /// Tiny last keep interval so a seek at/past edited duration still maps to source.
    /// </summary>
    /// <param name="keep">Full keep intervals on the original timeline.</param>
    /// <returns>A short tail of the last keep, or the input when empty.</returns>
    public static IReadOnlyList<(double Start, double End)> TailKeepRange(
        IReadOnlyList<(double Start, double End)> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        if (keep.Count == 0)
        {
            return keep;
        }

        var last = keep[^1];
        var start = Math.Max(last.Start, last.End - 0.04);
        return last.End > start + 0.001 ? [(start, last.End)] : keep;
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
}
