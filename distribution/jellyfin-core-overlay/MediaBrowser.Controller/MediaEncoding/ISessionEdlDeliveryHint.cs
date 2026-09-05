#nullable enable

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
    /// <returns>True for Edited sources.</returns>
    bool IsEdlAppliedMediaSource(string? mediaSourceId);

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
