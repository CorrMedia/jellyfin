#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Optional service implemented by plugins (e.g. CorrMedia) to record a seek position
/// when the plugin sends a server-initiated Seek (e.g. EDL skip). The next stream request
/// for that session that does not include startTimeTicks will use this position so
/// transcoding starts at the correct offset even when the client does not send it.
/// </summary>
public interface IPendingSeekStore
{
    /// <summary>
    /// Records a pending seek position for the given play session/device. The next stream request
    /// with this playSessionId (or deviceId if playSessionId is null) and no startTimeTicks will use this value.
    /// </summary>
    /// <param name="playSessionId">Play session ID (session.Id).</param>
    /// <param name="deviceId">Device ID for fallback when the client does not send playSessionId.</param>
    /// <param name="positionTicks">Seek position in ticks.</param>
    void SetPendingSeek(string playSessionId, string? deviceId, long positionTicks);

    /// <summary>
    /// Gets the pending seek position without removing it, so multiple stream requests (e.g. probe then actual) can all use the same position. Clear when the client sends startTimeTicks.
    /// </summary>
    /// <param name="playSessionId">Play session ID from the stream request (may be null).</param>
    /// <param name="deviceId">Device ID from the stream request for fallback.</param>
    /// <returns>The pending position in ticks, or null if none.</returns>
    long? GetPendingSeek(string? playSessionId, string? deviceId);

    /// <summary>
    /// Clears the pending seek for this session/device. Call when the client sends startTimeTicks so we don't use a stale value later.
    /// </summary>
    /// <param name="playSessionId">Play session ID from the stream request (may be null).</param>
    /// <param name="deviceId">Device ID from the stream request for fallback.</param>
    void ClearPendingSeek(string? playSessionId, string? deviceId);
}
