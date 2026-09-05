namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// A timed, duration-preserving video effect on the original source timeline.
/// </summary>
/// <param name="Kind">Zoom or blur.</param>
/// <param name="StartTime">Range start in seconds on the original timeline.</param>
/// <param name="EndTime">Range end in seconds on the original timeline.</param>
/// <param name="Scale">Zoom factor (&gt; 1). Ignored when <paramref name="Box"/> is set for zoom.</param>
/// <param name="CenterX">Zoom center X at range start, 0–1 (default 0.5).</param>
/// <param name="CenterY">Zoom center Y at range start, 0–1 (default 0.5).</param>
/// <param name="BlurRadius">Boxblur luma radius. Ignored for zoom.</param>
/// <param name="Box">Optional region at range start. Zoom fills the frame from this box; blur applies inside it. Null means full-frame blur or center-based zoom.</param>
/// <param name="BoxEnd">Optional region at range end. X/Y lerp from <paramref name="Box"/> (width/height stay at start). Null means no box pan.</param>
/// <param name="CenterXEnd">Optional zoom center X at range end. Null means no X pan.</param>
/// <param name="CenterYEnd">Optional zoom center Y at range end. Null means no Y pan.</param>
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
    double? CenterYEnd = null);
