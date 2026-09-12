#nullable enable

using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Lets plugins remap chapter markers onto an edited (post-cut) timeline.
/// </summary>
public interface ISessionChapterRewriter
{
    /// <summary>
    /// Returns true when this item's chapters should follow the edited clock.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <returns>True when the user has remaining skips.</returns>
    bool NeedsRewrite(string? itemId);

    /// <summary>
    /// Rewrites chapter start times onto the edited timeline.
    /// Markers inside skips are dropped; Name and image fields are preserved.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <param name="original">Original-timeline chapters.</param>
    /// <returns>Edited-timeline chapters, or <paramref name="original"/> when rewrite is not needed.</returns>
    IReadOnlyList<ChapterInfo> RewriteChapters(string? itemId, IReadOnlyList<ChapterInfo> original);
}
