using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Rewrites subtitle cues from the original timeline onto the edited (post-cut) clock.
/// Cues inside skips are dropped; cues that straddle a cut are split.
/// </summary>
internal static class CorrCueRemapper
{
    /// <summary>
    /// Maps cues through keep ranges. Output times are on the edited timeline.
    /// </summary>
    /// <param name="cues">Cues stamped on the original source timeline.</param>
    /// <param name="keep">Keep intervals on the original timeline.</param>
    /// <returns>Cues on the edited timeline, in order.</returns>
    public static IReadOnlyList<CorrCue> Remap(
        IReadOnlyList<CorrCue> cues,
        IReadOnlyList<(double Start, double End)> keep)
    {
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(keep);
        if (keep.Count == 0)
        {
            return [];
        }

        var result = new List<CorrCue>();
        foreach (var cue in cues)
        {
            if (cue.End <= cue.Start + 0.001)
            {
                continue;
            }

            var part = 0;
            foreach (var (keepStart, keepEnd) in keep)
            {
                var start = Math.Max(cue.Start, keepStart);
                var end = Math.Min(cue.End, keepEnd);
                if (end <= start + 0.001)
                {
                    continue;
                }

                if (!CorrTimeline.TryMapOriginalToEdited(start, keep, out var editedStart)
                    || !CorrTimeline.TryMapOriginalToEdited(end, keep, out var editedEnd)
                    || editedEnd <= editedStart + 0.001)
                {
                    continue;
                }

                part++;
                var id = part == 1 || string.IsNullOrEmpty(cue.Id)
                    ? cue.Id
                    : string.Concat(cue.Id, "#", part.ToString(CultureInfo.InvariantCulture));
                result.Add(new CorrCue(editedStart, editedEnd, cue.Text, id, cue.Settings));
            }
        }

        return result;
    }

    /// <summary>
    /// Drops cues outside <paramref name="editedStartSeconds"/>–<paramref name="editedEndSeconds"/>
    /// and optionally shifts remaining times to the window start.
    /// </summary>
    /// <param name="cues">Cues on the edited timeline.</param>
    /// <param name="editedStartSeconds">Window start on the edited timeline.</param>
    /// <param name="editedEndSeconds">Window end; 0 or less means no end bound.</param>
    /// <param name="copyTimestamps">When false, subtract the window start from each cue.</param>
    /// <returns>Windowed cues.</returns>
    public static IReadOnlyList<CorrCue> Window(
        IReadOnlyList<CorrCue> cues,
        double editedStartSeconds,
        double editedEndSeconds,
        bool copyTimestamps)
    {
        ArgumentNullException.ThrowIfNull(cues);
        var result = new List<CorrCue>();
        foreach (var cue in cues)
        {
            // Match Jellyfin FilterEvents: drop cues that start or end before the window.
            if (cue.Start < editedStartSeconds - 0.001 || cue.End < editedStartSeconds - 0.001)
            {
                continue;
            }

            if (editedEndSeconds > 0.001 && cue.Start > editedEndSeconds)
            {
                continue;
            }

            if (copyTimestamps)
            {
                result.Add(cue);
                continue;
            }

            result.Add(cue with
            {
                Start = Math.Max(0, cue.Start - editedStartSeconds),
                End = Math.Max(0, cue.End - editedStartSeconds)
            });
        }

        return result;
    }
}
