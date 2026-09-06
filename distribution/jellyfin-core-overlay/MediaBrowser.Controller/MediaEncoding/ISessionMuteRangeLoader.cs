#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Optional service implemented by plugins (e.g. CorrMedia) to ensure sidecar mute ranges
/// are loaded for a session before the first stream request checks for a session audio filter.
/// Called by the API immediately before HasSessionAudioFilterForRequest so the first
/// stream request gets mute ranges applied.
/// </summary>
public interface ISessionMuteRangeLoader
{
    /// <summary>
    /// Ensures mute ranges for the given play session/device are loaded (e.g. from the sidecar)
    /// and sent to the session audio filter provider. Called before checking for a session filter.
    /// Loads sidecar edits when they apply for this user; clears the plan otherwise.
    /// </summary>
    /// <param name="playSessionId">Play session ID from the stream request (may be null or empty).</param>
    /// <param name="deviceId">Device ID from the stream request (may be null or empty).</param>
    /// <param name="itemId">Item ID from the stream request (may be null); used to resolve media path when session's now-playing path is not set yet.</param>
    /// <param name="mediaSourceId">Media source id from the stream request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.</returns>
    System.Threading.Tasks.Task EnsureMuteRangesLoadedAsync(
        string? playSessionId,
        string? deviceId,
        string? itemId,
        string? mediaSourceId,
        System.Threading.CancellationToken cancellationToken);
}
