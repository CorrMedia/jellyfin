using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Mute/volume/beep, skip, and video-effect ranges for one media sidecar.
/// </summary>
/// <param name="MuteRanges">Absolute mute / volume / beep windows in seconds.</param>
/// <param name="SkipRanges">Absolute skip (cut) windows in seconds.</param>
/// <param name="VideoEffects">Duration-preserving video effects on the original timeline.</param>
/// <param name="OriginalDurationSeconds">Uncut item duration in seconds, or 0 if unknown.</param>
public sealed record CorrEditPlan(
    IReadOnlyList<MuteTimeRange> MuteRanges,
    IReadOnlyList<MuteTimeRange> SkipRanges,
    IReadOnlyList<VideoEffect> VideoEffects,
    double OriginalDurationSeconds = 0)
{
    /// <summary>
    /// Gets a value indicating whether skip cuts are present.
    /// </summary>
    public bool HasCuts => SkipRanges.Count > 0;

    /// <summary>
    /// Gets a value indicating whether duration-preserving video effects are present.
    /// </summary>
    public bool HasVideoEffects => VideoEffects.Count > 0;

    /// <summary>
    /// Gets a value indicating whether any audio edit targets specific named channels.
    /// </summary>
    public bool HasSelectiveMute
        => MuteRanges.Any(m => m.IsSelectiveMute);

    /// <summary>
    /// Gets a value indicating whether a filter_complex graph is required
    /// (cuts, video effects, and/or selective channel mute).
    /// </summary>
    public bool NeedsEditGraph => HasCuts || HasVideoEffects || HasSelectiveMute;

    /// <summary>
    /// Gets probed source channel layout (e.g. 5.1), when known from the media file.
    /// Preferred over stale library metadata for selective mute.
    /// </summary>
    public string? SourceChannelLayout { get; init; }

    /// <summary>
    /// Gets probed source channel count, when known from the media file.
    /// </summary>
    public int SourceChannelCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the burned-in "Edited" badge should
    /// appear at the start of a play-from-start encode. Default is true.
    /// </summary>
    public bool ShowEditedBadge { get; init; } = true;
}
