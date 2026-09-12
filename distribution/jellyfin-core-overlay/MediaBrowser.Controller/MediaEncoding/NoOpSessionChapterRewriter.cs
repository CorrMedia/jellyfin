#nullable enable

using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// No-op <see cref="ISessionChapterRewriter"/>.
/// </summary>
public sealed class NoOpSessionChapterRewriter : ISessionChapterRewriter
{
    /// <inheritdoc />
    public bool NeedsRewrite(string? itemId) => false;

    /// <inheritdoc />
    public IReadOnlyList<ChapterInfo> RewriteChapters(string? itemId, IReadOnlyList<ChapterInfo> original)
        => original;
}
