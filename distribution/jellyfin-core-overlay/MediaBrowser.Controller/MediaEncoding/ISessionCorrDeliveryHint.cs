#nullable enable

using System;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Lets plugins apply sidecar edits on the normal library item (transcode, HLS, runtime).
/// </summary>
public interface ISessionCorrDeliveryHint
{
    /// <summary>
    /// Returns true when sidecar edits should apply for this library item.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <returns>True when this user's playback of the item should be edited.</returns>
    bool ShouldApplySidecarEdits(string? itemId);

    /// <summary>
    /// Returns true when this item's edited stream should be delivered via HLS (not progressive HTTP).
    /// Progressive streams cannot mid-file seek (<c>Accept-Ranges: none</c>).
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <returns>True to force HLS transcoding URL.</returns>
    bool RequiresHls(string? itemId);

    /// <summary>
    /// Returns the shortened runtime when applied skip/cut ranges change duration.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <param name="runTimeTicks">Edited duration in ticks.</param>
    /// <returns>True when ticks were computed from remaining skips.</returns>
    bool TryGetEditedRunTimeTicks(string? itemId, out long runTimeTicks);
}
