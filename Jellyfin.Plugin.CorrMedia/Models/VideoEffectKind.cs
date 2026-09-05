namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// Kind of duration-preserving video effect.
/// </summary>
public enum VideoEffectKind
{
    /// <summary>
    /// Punch in / zoom (crop a region and scale it to fill the frame).
    /// </summary>
    Zoom,

    /// <summary>
    /// Box blur (full frame or a normalized region).
    /// </summary>
    Blur
}
