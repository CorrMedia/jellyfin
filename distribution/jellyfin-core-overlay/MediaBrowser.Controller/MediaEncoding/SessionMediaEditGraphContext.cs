#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Context for building a session media edit graph.
/// </summary>
public sealed class SessionMediaEditGraphContext
{
    /// <summary>
    /// Gets the media duration in seconds (from the item), or 0 if unknown.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets the requested stream start offset in seconds on the <em>edited</em> (post-cut) timeline.
    /// </summary>
    public double StartTimeSeconds { get; init; }
}
