using System.Collections.Generic;

namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// One playback edit from a sidecar, with a stable id for per-user honor/ignore.
/// </summary>
/// <param name="Id">Sidecar <c>id</c>, or a synthetic id when the sidecar omitted one.</param>
/// <param name="Action">Canonical action (mute, volume, beep, skip, zoom, crop, blur, cover, blank, pixelate).</param>
/// <param name="StartTime">Range start in seconds on the original timeline.</param>
/// <param name="EndTime">Range end in seconds on the original timeline.</param>
/// <param name="Description">Optional sidecar description.</param>
/// <param name="Categories">Optional sidecar categories.</param>
public sealed record CorrEditDescriptor(
    string Id,
    string Action,
    double StartTime,
    double EndTime,
    string? Description,
    IReadOnlyList<string> Categories);
