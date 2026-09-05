#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Context for building a session media edit graph.
/// </summary>
public sealed class SessionMediaEditGraphContext
{
    /// <summary>
    /// Gets the media-source duration in seconds, or 0 if unknown.
    /// For Edited sources with cuts this is the shortened runtime; keep-range math
    /// must restore the original duration.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets the requested stream start offset in seconds on the <em>edited</em> (post-cut) timeline.
    /// </summary>
    public double StartTimeSeconds { get; init; }

    /// <summary>
    /// Gets the source audio channel layout string (e.g. <c>5.1</c>, <c>stereo</c>), or empty if unknown.
    /// </summary>
    public string InputChannelLayout { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source audio channel count, or 0 if unknown.
    /// </summary>
    public int InputChannelCount { get; init; }

    /// <summary>
    /// Gets the requested output audio channel count, or 0 if unknown / unchanged.
    /// </summary>
    public int OutputAudioChannels { get; init; }

    /// <summary>
    /// Gets an optional FFmpeg stereo-downmix filter (e.g. <c>pan=stereo|...</c>) to apply
    /// after channel mute when the output is stereo and the source is multi-channel.
    /// </summary>
    public string? StereoDownmixFilter { get; init; }
}
