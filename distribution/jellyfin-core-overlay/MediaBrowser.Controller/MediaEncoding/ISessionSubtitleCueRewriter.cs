#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Lets plugins rewrite external text subtitles onto an edited (post-cut) timeline.
/// </summary>
public interface ISessionSubtitleCueRewriter
{
    /// <summary>
    /// Returns true when subtitle cues for this item should be remapped before serving.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <param name="format">Requested output format (vtt, srt, …).</param>
    /// <returns>True when the plugin will rewrite this request.</returns>
    bool NeedsRewrite(string? itemId, string? format);

    /// <summary>
    /// Remaps a full original-timeline subtitle stream onto the edited clock, then windows it.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <param name="format">Requested output format.</param>
    /// <param name="originalFullTrack">Complete original-timeline track (caller fetched with start/end 0).</param>
    /// <param name="startPositionTicks">Window start on the edited timeline.</param>
    /// <param name="endPositionTicks">Window end on the edited timeline; 0 means no end bound.</param>
    /// <param name="copyTimestamps">When false, subtract the window start from each cue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rewritten subtitle stream.</returns>
    Task<Stream> RewriteAsync(
        string itemId,
        string format,
        Stream originalFullTrack,
        long startPositionTicks,
        long endPositionTicks,
        bool copyTimestamps,
        CancellationToken cancellationToken);
}
