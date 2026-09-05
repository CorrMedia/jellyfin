namespace Jellyfin.Plugin.CorrMedia;

/// <summary>
/// Represents a single EDL range with start time, end time, and action type.
/// </summary>
/// <param name="Start">Start time in seconds.</param>
/// <param name="End">End time in seconds.</param>
/// <param name="Action">Action to perform during this time range.</param>
public record EdlRange(long Start, long End, EdlAction Action)
{
    /// <summary>
    /// Gets a value indicating whether this range is valid (start less than end).
    /// </summary>
    public bool IsValid => Start < End;

    /// <summary>
    /// Gets the duration of this range in seconds.
    /// </summary>
    public long Duration => End - Start;
}
