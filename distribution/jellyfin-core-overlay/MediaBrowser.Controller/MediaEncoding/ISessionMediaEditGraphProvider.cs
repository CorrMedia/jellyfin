#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Provides a mute-then-cut FFmpeg filter_complex graph for sidecar-edited delivery.
/// </summary>
public interface ISessionMediaEditGraphProvider
{
    /// <summary>
    /// Returns true when this session needs an A/V edit graph (skip cuts present).
    /// </summary>
    /// <param name="playSessionId">Play session id.</param>
    /// <param name="deviceId">Device id.</param>
    /// <returns>True if an edit graph should be applied.</returns>
    bool HasEditGraph(string? playSessionId, string? deviceId);

    /// <summary>
    /// Builds the edit graph, or null if none.
    /// </summary>
    /// <param name="playSessionId">Play session id.</param>
    /// <param name="deviceId">Device id.</param>
    /// <param name="context">Duration / start context.</param>
    /// <returns>The graph, or null.</returns>
    SessionMediaEditGraph? GetEditGraph(string? playSessionId, string? deviceId, SessionMediaEditGraphContext context);
}
