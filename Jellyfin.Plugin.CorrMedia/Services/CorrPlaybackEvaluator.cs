using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.CorrMedia.Models;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Resolves whether sidecar edits apply for a library item and user.
/// </summary>
internal static class CorrPlaybackEvaluator
{
    /// <summary>
    /// Display suffix appended to item and media-source names when edits apply.
    /// </summary>
    public const string EditedNameSuffix = " (Edited)";

    /// <summary>
    /// Returns applied sidecar edits when the user wants them and any remain after filters.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="overrideStore">Per-user apply/filter store.</param>
    /// <param name="itemId">Library item id.</param>
    /// <param name="userId">Playing user; empty uses default (apply all).</param>
    /// <param name="edits">Applied edits when the method returns true.</param>
    /// <param name="logger">Optional logger for sidecar parse failures.</param>
    /// <returns>True when playback should encode with sidecar edits.</returns>
    public static bool TryGetAppliedEdits(
        ILibraryManager libraryManager,
        EditOverrideStore overrideStore,
        string? itemId,
        Guid userId,
        out CorrEdits edits,
        ILogger? logger = null)
    {
        edits = CorrEdits.Empty;
        ArgumentNullException.ThrowIfNull(libraryManager);
        ArgumentNullException.ThrowIfNull(overrideStore);

        if (string.IsNullOrEmpty(itemId) || !Guid.TryParse(itemId, out var guid))
        {
            return false;
        }

        var item = libraryManager.GetItemById(guid);
        if (item is null || string.IsNullOrEmpty(item.Path))
        {
            return false;
        }

        var corrPath = CorrFile.GetPath(item.Path);
        if (!File.Exists(corrPath))
        {
            return false;
        }

        var parsed = CorrFile.Parse(corrPath, logger);
        edits = EditComplianceFilter.Apply(parsed, overrideStore.Get(userId));
        return edits.HasPlaybackEdits;
    }

    /// <summary>
    /// Shortens original runtime by merged skip totals (overlaps count once).
    /// </summary>
    /// <param name="originalTicks">Uncut item duration.</param>
    /// <param name="skips">Applied skip ranges.</param>
    /// <returns>Edited duration, or null when original is missing.</returns>
    public static long? EditedRunTimeTicks(long? originalTicks, IReadOnlyList<MuteTimeRange> skips)
    {
        if (!originalTicks.HasValue || originalTicks.Value <= 0)
        {
            return originalTicks;
        }

        ArgumentNullException.ThrowIfNull(skips);
        var skipTicks = (long)(CorrTimeline.MergedSkipSeconds(skips) * TimeSpan.TicksPerSecond);
        return Math.Max(TimeSpan.TicksPerSecond, originalTicks.Value - skipTicks);
    }
}
