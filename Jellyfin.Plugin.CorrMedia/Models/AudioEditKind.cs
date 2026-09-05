namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// Kind of duration-preserving audio edit on the original source timeline.
/// </summary>
public enum AudioEditKind
{
    /// <summary>
    /// Silence the targeted channels (gain 0).
    /// </summary>
    Mute,

    /// <summary>
    /// Scale the targeted channels by <c>Gain</c> (0–1).
    /// </summary>
    Volume,

    /// <summary>
    /// Replace the targeted channels with a sine tone.
    /// </summary>
    Beep
}
