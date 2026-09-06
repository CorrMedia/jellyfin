#nullable enable

using System;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Lets plugins remap trickplay manifests, HLS image playlists, and tile indices
/// onto an edited (post-cut) timeline.
/// </summary>
public interface ISessionTrickplayRewriter
{
    /// <summary>
    /// Returns true when this item's trickplay should follow the edited clock.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <returns>True when the user has remaining skips.</returns>
    bool NeedsRewrite(string? itemId);

    /// <summary>
    /// Rewrites DTO fields so the client indexes thumbnails on the edited clock.
    /// Tile layout becomes 1×1 so each request is one remapped original cell.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <param name="original">Original-timeline trickplay info.</param>
    /// <returns>Edited-timeline info, or <paramref name="original"/> when rewrite is not needed.</returns>
    TrickplayInfoDto RewriteDto(string? itemId, TrickplayInfoDto original);

    /// <summary>
    /// Builds an HLS image playlist whose duration and entries match the edited stream.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <param name="original">Original-timeline trickplay info.</param>
    /// <param name="mediaSourceId">Media source id used in tile URLs.</param>
    /// <param name="apiKey">Optional API key for tile URLs.</param>
    /// <returns>Playlist text, or null to use the stock playlist.</returns>
    string? BuildHlsPlaylist(string? itemId, TrickplayInfoDto original, Guid mediaSourceId, string? apiKey);

    /// <summary>
    /// Maps an edited-timeline thumbnail index to the original sprite file and cell.
    /// </summary>
    /// <param name="itemId">Library item id.</param>
    /// <param name="editedIndex">Thumbnail index on the edited clock.</param>
    /// <param name="original">Original-timeline trickplay info.</param>
    /// <param name="cell">Original sprite index and cell when the method returns true.</param>
    /// <returns>True when <paramref name="editedIndex"/> maps into a keep range.</returns>
    bool TryMapEditedThumbnail(string? itemId, int editedIndex, TrickplayInfoDto original, out SessionTrickplayCell cell);
}
