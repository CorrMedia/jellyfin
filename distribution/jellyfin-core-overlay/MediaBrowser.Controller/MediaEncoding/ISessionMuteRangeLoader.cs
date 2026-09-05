#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Optional service implemented by plugins (e.g. CorrMedia) to ensure EDL mute ranges
/// are loaded for a session before the first stream request checks for a session audio filter.
/// Called by the API immediately before HasSessionAudioFilterForRequest so the first
/// stream request gets mute ranges applied.
/// </summary>
public interface ISessionMuteRangeLoader
{
    /// <summary>
    /// Ensures mute ranges for the given play session/device are loaded (e.g. from EDL)
    /// and sent to the session audio filter provider. Called before checking for a session filter.
    /// Loads only when <paramref name="mediaSourceId"/> is the Edited EDL source; clears the plan for Original.
    /// </summary>
    /// <param name="playSessionId">Play session ID from the stream request (may be null or empty).</param>
    /// <param name="deviceId">Device ID from the stream request (may be null or empty).</param>
    /// <param name="itemId">Item ID from the stream request (may be null); used to resolve media path when session's now-playing path is not set yet.</param>
    /// <param name="mediaSourceId">Media source id; EDL edits apply only for Edited (<c>*_edl</c>) sources.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.</returns>
    System.Threading.Tasks.Task EnsureMuteRangesLoadedAsync(
        string? playSessionId,
        string? deviceId,
        string? itemId,
        string? mediaSourceId,
        System.Threading.CancellationToken cancellationToken);
}
