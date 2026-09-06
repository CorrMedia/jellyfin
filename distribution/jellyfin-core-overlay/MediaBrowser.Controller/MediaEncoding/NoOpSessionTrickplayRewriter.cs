#nullable enable

using System;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// No-op <see cref="ISessionTrickplayRewriter"/>.
/// </summary>
public sealed class NoOpSessionTrickplayRewriter : ISessionTrickplayRewriter
{
    /// <inheritdoc />
    public bool NeedsRewrite(string? itemId) => false;

    /// <inheritdoc />
    public TrickplayInfoDto RewriteDto(string? itemId, TrickplayInfoDto original) => original;

    /// <inheritdoc />
    public string? BuildHlsPlaylist(string? itemId, TrickplayInfoDto original, Guid mediaSourceId, string? apiKey)
        => null;

    /// <inheritdoc />
    public bool TryMapEditedThumbnail(string? itemId, int editedIndex, TrickplayInfoDto original, out SessionTrickplayCell cell)
    {
        cell = default;
        return false;
    }
}
