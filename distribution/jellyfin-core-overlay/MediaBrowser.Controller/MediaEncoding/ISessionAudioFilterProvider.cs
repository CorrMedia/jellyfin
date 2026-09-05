#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Provides session-scoped additional audio filters for FFmpeg transcoding.
/// Used by plugins (e.g. CorrMedia) to inject mute or other filters per play session.
/// </summary>
public interface ISessionAudioFilterProvider
{
    /// <summary>
    /// Returns an additional FFmpeg audio filter string for the given play session, or null if none.
    /// The filter is appended to the existing audio filter chain (e.g. volume mute expression).
    /// </summary>
    /// <param name="playSessionId">The play session ID from the request, or null if not available.</param>
    /// <param name="deviceId">The device ID from the request; use as fallback when playSessionId does not match.</param>
    /// <param name="startTimeSeconds">
    /// Stream start offset in seconds (from <c>-ss</c> / StartTimeTicks). Mute ranges are absolute
    /// media times; when this is non-zero they must be shifted into FFmpeg filter time <c>t</c>
    /// which resets at the start of the output after input seeking.
    /// </param>
    /// <returns>Filter string or null.</returns>
    string? GetAdditionalAudioFilter(string? playSessionId, string? deviceId, double startTimeSeconds = 0);
}
