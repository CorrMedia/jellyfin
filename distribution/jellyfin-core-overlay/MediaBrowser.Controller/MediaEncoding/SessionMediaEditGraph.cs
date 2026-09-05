#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// A session-scoped FFmpeg edit graph (mute then cut) supplied by a plugin.
/// </summary>
public sealed class SessionMediaEditGraph
{
    /// <summary>
    /// Gets the -filter_complex body (no prefix).
    /// </summary>
    public required string FilterComplex { get; init; }

    /// <summary>
    /// Gets the labeled video output pad name (e.g. vout).
    /// </summary>
    public required string VideoMapLabel { get; init; }

    /// <summary>
    /// Gets the labeled audio output pad name (e.g. aout), or null if video-only.
    /// </summary>
    public string? AudioMapLabel { get; init; }
}
