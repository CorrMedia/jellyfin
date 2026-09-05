using System.Collections.Generic;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Mute, skip, and video-effect ranges for one media sidecar.
/// </summary>
/// <param name="MuteRanges">Absolute mute windows in seconds.</param>
/// <param name="SkipRanges">Absolute skip (cut) windows in seconds.</param>
/// <param name="VideoEffects">Duration-preserving video effects (zoom, blur) on the original timeline.</param>
public sealed record EdlEditPlan(
    IReadOnlyList<MuteTimeRange> MuteRanges,
    IReadOnlyList<MuteTimeRange> SkipRanges,
    IReadOnlyList<VideoEffect> VideoEffects)
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
    /// Gets a value indicating whether a filter_complex graph is required (cuts and/or video effects).
    /// </summary>
    public bool NeedsEditGraph => HasCuts || HasVideoEffects;
}
