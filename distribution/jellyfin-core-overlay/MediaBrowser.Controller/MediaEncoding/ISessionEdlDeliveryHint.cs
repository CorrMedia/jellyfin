#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Lets plugins force seekable HLS delivery for items that need server-side EDL edits.
/// </summary>
public interface ISessionEdlDeliveryHint
{
    /// <summary>
    /// Returns true when this item should be delivered via HLS (not progressive HTTP).
    /// Progressive streams cannot mid-file seek (<c>Accept-Ranges: none</c>).
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <returns>True to force HLS transcoding URL.</returns>
    bool RequiresHls(string? itemId);
}
