using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Api.Attributes;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Helpers;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Trickplay;
using MediaBrowser.Model;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Trickplay controller.
/// </summary>
[Route("")]
[Authorize]
public class TrickplayController : BaseJellyfinApiController
{
    private readonly ILibraryManager _libraryManager;
    private readonly ITrickplayManager _trickplayManager;
    private readonly ISessionTrickplayRewriter _trickplayRewriter;
    private readonly ITrickplayCellCropper _cellCropper;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrickplayController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/>.</param>
    /// <param name="trickplayManager">Instance of <see cref="ITrickplayManager"/>.</param>
    /// <param name="trickplayRewriter">Instance of <see cref="ISessionTrickplayRewriter"/>.</param>
    /// <param name="cellCropper">Instance of <see cref="ITrickplayCellCropper"/>.</param>
    public TrickplayController(
        ILibraryManager libraryManager,
        ITrickplayManager trickplayManager,
        ISessionTrickplayRewriter trickplayRewriter,
        ITrickplayCellCropper cellCropper)
    {
        _libraryManager = libraryManager;
        _trickplayManager = trickplayManager;
        _trickplayRewriter = trickplayRewriter;
        _cellCropper = cellCropper;
    }

    /// <summary>
    /// Gets an image tiles playlist for trickplay.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="width">The width of a single tile.</param>
    /// <param name="mediaSourceId">The media version id, if using an alternate version.</param>
    /// <response code="200">Tiles playlist returned.</response>
    /// <returns>A <see cref="FileResult"/> containing the trickplay playlist file.</returns>
    [HttpGet("Videos/{itemId}/Trickplay/{width}/tiles.m3u8")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesPlaylistFile]
    public async Task<ActionResult> GetTrickplayHlsPlaylist(
        [FromRoute, Required] Guid itemId,
        [FromRoute, Required] int width,
        [FromQuery] Guid? mediaSourceId)
    {
        var sourceId = mediaSourceId ?? itemId;
        var itemIdN = itemId.ToString("N", CultureInfo.InvariantCulture);
        if (_trickplayRewriter.NeedsRewrite(itemIdN))
        {
            var resolutions = await _trickplayManager.GetTrickplayResolutions(sourceId).ConfigureAwait(false);
            if (resolutions is not null && resolutions.TryGetValue(width, out var info))
            {
                var playlist = _trickplayRewriter.BuildHlsPlaylist(
                    itemIdN,
                    new TrickplayInfoDto(info),
                    sourceId,
                    User.GetToken());
                if (!string.IsNullOrEmpty(playlist))
                {
                    return Content(playlist, MimeTypes.GetMimeType("playlist.m3u8"), Encoding.UTF8);
                }
            }
        }

        string? stock = await _trickplayManager.GetHlsPlaylist(sourceId, width, User.GetToken()).ConfigureAwait(false);

        if (string.IsNullOrEmpty(stock))
        {
            return NotFound();
        }

        return Content(stock, MimeTypes.GetMimeType("playlist.m3u8"), Encoding.UTF8);
    }

    /// <summary>
    /// Gets a trickplay tile image.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="width">The width of a single tile.</param>
    /// <param name="index">The index of the desired tile.</param>
    /// <param name="mediaSourceId">The media version id, if using an alternate version.</param>
    /// <response code="200">Tile image returned.</response>
    /// <response code="200">Tile image not found at specified index.</response>
    /// <returns>A <see cref="FileResult"/> containing the trickplay tiles image.</returns>
    [HttpGet("Videos/{itemId}/Trickplay/{width}/{index}.jpg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesImageFile]
    public async Task<ActionResult> GetTrickplayTileImage(
        [FromRoute, Required] Guid itemId,
        [FromRoute, Required] int width,
        [FromRoute, Required] int index,
        [FromQuery] Guid? mediaSourceId)
    {
        var item = _libraryManager.GetItemById<BaseItem>(mediaSourceId ?? itemId, User.GetUserId());
        if (item is null)
        {
            return NotFound();
        }

        var saveWithMedia = _libraryManager.GetLibraryOptions(item).SaveTrickplayWithMedia;
        var itemIdN = itemId.ToString("N", CultureInfo.InvariantCulture);
        if (_trickplayRewriter.NeedsRewrite(itemIdN))
        {
            var resolutions = await _trickplayManager.GetTrickplayResolutions(item.Id).ConfigureAwait(false);
            if (resolutions is not null && resolutions.TryGetValue(width, out var info))
            {
                var original = new TrickplayInfoDto(info);
                if (_trickplayRewriter.TryMapEditedThumbnail(itemIdN, index, original, out var cell))
                {
                    var mappedPath = await _trickplayManager.GetTrickplayTilePathAsync(item, width, cell.TileIndex, saveWithMedia).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(mappedPath) && System.IO.File.Exists(mappedPath))
                    {
                        if (original.TileWidth <= 1 && original.TileHeight <= 1)
                        {
                            Response.Headers.ContentDisposition = "attachment";
                            return PhysicalFile(mappedPath, MediaTypeNames.Image.Jpeg);
                        }

                        var bytes = _cellCropper.CropCell(
                            mappedPath,
                            original.TileWidth,
                            original.TileHeight,
                            cell.Column,
                            cell.Row);
                        if (bytes is { Length: > 0 })
                        {
                            Response.Headers.ContentDisposition = "attachment";
                            return File(bytes, MediaTypeNames.Image.Jpeg);
                        }
                    }
                }

                return NotFound();
            }
        }

        var path = await _trickplayManager.GetTrickplayTilePathAsync(item, width, index, saveWithMedia).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
        {
            Response.Headers.ContentDisposition = "attachment";
            return PhysicalFile(path, MediaTypeNames.Image.Jpeg);
        }

        return NotFound();
    }
}
