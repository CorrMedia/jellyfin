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
    Blur,

    /// <summary>
    /// Solid black cover (full frame or a normalized region).
    /// </summary>
    Cover,

    /// <summary>
    /// Keep a region and pad with black so output size stays the same.
    /// </summary>
    Crop,

    /// <summary>
    /// Pixelate (full frame or a normalized region).
    /// </summary>
    Pixelate,

    /// <summary>
    /// Full-frame black video; audio continues.
    /// </summary>
    Blank
}
