using System.Collections.Generic;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Mute and skip ranges for one media EDL.
/// </summary>
/// <param name="MuteRanges">Absolute mute windows in seconds.</param>
/// <param name="SkipRanges">Absolute skip (cut) windows in seconds.</param>
public sealed record EdlEditPlan(
    IReadOnlyList<MuteTimeRange> MuteRanges,
    IReadOnlyList<MuteTimeRange> SkipRanges)
{
    /// <summary>
    /// Gets a value indicating whether skip cuts are present.
    /// </summary>
    public bool HasCuts => SkipRanges.Count > 0;
}
