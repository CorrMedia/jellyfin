namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// Axis-aligned box in normalized source coordinates (0–1).
/// </summary>
/// <param name="X">Left edge, 0–1.</param>
/// <param name="Y">Top edge, 0–1.</param>
/// <param name="Width">Width, 0–1.</param>
/// <param name="Height">Height, 0–1.</param>
public sealed record NormalizedBox(double X, double Y, double Width, double Height);
