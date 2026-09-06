namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// A timed, duration-preserving video effect on the original source timeline.
/// Output frame size never changes (crop pads; zoom scales to fill).
/// </summary>
/// <param name="Kind">Zoom, blur, cover, crop, pixelate, or blank.</param>
/// <param name="StartTime">Range start in seconds on the original timeline.</param>
/// <param name="EndTime">Range end in seconds on the original timeline.</param>
/// <param name="Scale">Zoom/crop factor (&gt; 1). Ignored when <paramref name="Box"/> is set.</param>
/// <param name="CenterX">Zoom/crop center X at range start, 0–1 (default 0.5).</param>
/// <param name="CenterY">Zoom/crop center Y at range start, 0–1 (default 0.5).</param>
/// <param name="BlurRadius">Boxblur luma radius. Ignored for other kinds.</param>
/// <param name="Box">Optional region at range start. Zoom/crop keep this box; blur/cover/pixelate apply inside it. Null means full-frame or center-based zoom/crop.</param>
/// <param name="BoxEnd">Optional region at range end. X/Y lerp from <paramref name="Box"/> (width/height stay at start). Null means no box pan.</param>
/// <param name="CenterXEnd">Optional zoom/crop center X at range end. Null means no X pan.</param>
/// <param name="CenterYEnd">Optional zoom/crop center Y at range end. Null means no Y pan.</param>
/// <param name="BlockSize">Pixelate block size in pixels. Ignored for other kinds.</param>
/// <param name="Id">Sidecar edit id (or a stable synthetic id when omitted).</param>
public sealed record VideoEffect(
    VideoEffectKind Kind,
    double StartTime,
    double EndTime,
    double Scale,
    double CenterX,
    double CenterY,
    double BlurRadius,
    NormalizedBox? Box,
    NormalizedBox? BoxEnd = null,
    double? CenterXEnd = null,
    double? CenterYEnd = null,
    double BlockSize = 16,
    string Id = "");
