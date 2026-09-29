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
    /// <param name="playbackAudio">Resolves the selected audio-stream language.</param>
    /// <param name="itemId">Library item id.</param>
    /// <param name="userId">Playing user; empty uses default (apply all).</param>
    /// <param name="audioStreamIndex">Audio stream index from the encode request, or null to use playback info and the user's default track.</param>
    /// <param name="edits">Applied edits when the method returns true.</param>
    /// <param name="logger">Optional logger for sidecar parse failures.</param>
    /// <returns>True when playback should encode with sidecar edits.</returns>
    public static bool TryGetAppliedEdits(
        ILibraryManager libraryManager,
        EditOverrideStore overrideStore,
        PlaybackAudioLanguage playbackAudio,
        string? itemId,
        Guid userId,
        int? audioStreamIndex,
        out CorrEdits edits,
        ILogger? logger = null)
    {
        edits = CorrEdits.Empty;
        ArgumentNullException.ThrowIfNull(libraryManager);
        ArgumentNullException.ThrowIfNull(overrideStore);
        ArgumentNullException.ThrowIfNull(playbackAudio);

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
        var streamLanguage = playbackAudio.Resolve(item, userId, audioStreamIndex);
        var forLanguage = SpokenLanguageFilter.Apply(parsed, streamLanguage);
        if (forLanguage.All.Count != parsed.All.Count)
        {
            logger?.LogDebug(
                "CorrMedia kept {Kept} of {Total} sidecar edits for audio language {Language}",
                forLanguage.All.Count,
                parsed.All.Count,
                streamLanguage ?? "(none)");
        }

        edits = EditComplianceFilter.Apply(forLanguage, overrideStore.Get(userId));
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
