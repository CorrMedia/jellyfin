using System;
using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Rewrites chapter markers from the original timeline onto the edited (post-cut) clock.
/// Markers inside skips are dropped; survivors keep Name/image fields.
/// </summary>
internal static class CorrChapterRemapper
{
    /// <summary>
    /// Maps chapter start times through keep ranges. Output ticks are on the edited timeline.
    /// </summary>
    /// <param name="chapters">Chapters stamped on the original source timeline.</param>
    /// <param name="keep">Keep intervals on the original timeline.</param>
    /// <returns>Chapters on the edited timeline, in order. Same-tick duplicates keep the first.</returns>
    public static IReadOnlyList<ChapterInfo> Remap(
        IReadOnlyList<ChapterInfo> chapters,
        IReadOnlyList<(double Start, double End)> keep)
    {
        ArgumentNullException.ThrowIfNull(chapters);
        ArgumentNullException.ThrowIfNull(keep);
        if (keep.Count == 0 || chapters.Count == 0)
        {
            return [];
        }

        var result = new List<ChapterInfo>(chapters.Count);
        long? lastTicks = null;
        foreach (var chapter in chapters)
        {
            ArgumentNullException.ThrowIfNull(chapter);
            var originalSeconds = chapter.StartPositionTicks / (double)TimeSpan.TicksPerSecond;
            if (!CorrTimeline.TryMapOriginalToEdited(originalSeconds, keep, out var editedSeconds))
            {
                continue;
            }

            var editedTicks = (long)Math.Round(editedSeconds * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero);
            if (lastTicks.HasValue && editedTicks == lastTicks.Value)
            {
                continue;
            }

            lastTicks = editedTicks;
            result.Add(CloneWithStart(chapter, editedTicks));
        }

        return result;
    }

    private static ChapterInfo CloneWithStart(ChapterInfo source, long startPositionTicks)
        => new()
        {
            StartPositionTicks = startPositionTicks,
            Name = source.Name,
            ImagePath = source.ImagePath,
            ImageDateModified = source.ImageDateModified,
            ImageTag = source.ImageTag
        };
}
