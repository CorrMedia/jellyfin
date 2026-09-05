#nullable enable

using System;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Lets plugins force seekable HLS delivery for items that need server-side EDL edits.
/// </summary>
public interface ISessionEdlDeliveryHint
{
    /// <summary>
    /// Returns true when <paramref name="mediaSourceId"/> is the EDL-applied Edited source.
    /// </summary>
    /// <param name="mediaSourceId">Media source id from PlaybackInfo / stream request.</param>
    /// <param name="itemId">Optional library item id so detection does not depend on in-memory cache.</param>
    /// <returns>True for Edited sources.</returns>
    bool IsEdlAppliedMediaSource(string? mediaSourceId, string? itemId = null);

    /// <summary>
    /// Maps an Edited media source id back to the library item it was generated for.
    /// Used when jellyfin-web calls GetItem with the Version dropdown id.
    /// </summary>
    /// <param name="mediaSourceId">Media source id from the client.</param>
    /// <param name="itemId">Library item id when this is an Edited source.</param>
    /// <returns>True when <paramref name="mediaSourceId"/> is an Edited source with a known parent.</returns>
    bool TryGetLibraryItemId(string? mediaSourceId, out Guid itemId);

    /// <summary>
    /// Returns true when this Edited source should be delivered via HLS (not progressive HTTP).
    /// Progressive streams cannot mid-file seek (<c>Accept-Ranges: none</c>).
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <param name="mediaSourceId">Media source id; Original sources never require EDL HLS.</param>
    /// <returns>True to force HLS transcoding URL.</returns>
    bool RequiresHls(string? itemId, string? mediaSourceId);

    /// <summary>
    /// When true, Edited (<c>*_edl</c>) media sources should sort ahead of Original for naive clients.
    /// </summary>
    /// <returns>Prefer Edited ordering.</returns>
    bool PreferEdlAppliedMediaSources();
}
