using System.Collections.Generic;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia;

/// <summary>
/// Parsed corr.json edits that playback currently applies.
/// </summary>
/// <param name="Mutes">Mute ranges.</param>
/// <param name="Skips">Skip/cut ranges.</param>
/// <param name="VideoEffects">Duration-preserving video effects (zoom, crop, blur, cover, pixelate, blank).</param>
/// <param name="All">Every playback edit with a stable id (including those the user may ignore).</param>
internal sealed record CorrEdits(
    IReadOnlyList<MuteTimeRange> Mutes,
    IReadOnlyList<MuteTimeRange> Skips,
    IReadOnlyList<VideoEffect> VideoEffects,
    IReadOnlyList<CorrEditDescriptor> All)
{
    /// <summary>
    /// Gets an empty parse result.
    /// </summary>
    public static CorrEdits Empty { get; } = new([], [], [], []);

    /// <summary>
    /// Gets a value indicating whether any applied edit is present.
    /// </summary>
    public bool HasPlaybackEdits
        => Mutes.Count > 0 || Skips.Count > 0 || VideoEffects.Count > 0;
}
