#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// No-op <see cref="ISessionSubtitleCueRewriter"/>.
/// </summary>
public sealed class NoOpSessionSubtitleCueRewriter : ISessionSubtitleCueRewriter
{
    /// <inheritdoc />
    public bool NeedsRewrite(string? itemId, string? format) => false;

    /// <inheritdoc />
    public Task<Stream> RewriteAsync(
        string itemId,
        string format,
        Stream originalFullTrack,
        long startPositionTicks,
        long endPositionTicks,
        bool copyTimestamps,
        CancellationToken cancellationToken)
        => Task.FromResult(originalFullTrack);
}
