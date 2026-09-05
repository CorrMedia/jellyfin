namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// A time range in seconds for mute (or other) EDL-driven audio filters.
/// </summary>
public sealed record MuteTimeRange(double StartTime, double EndTime);
