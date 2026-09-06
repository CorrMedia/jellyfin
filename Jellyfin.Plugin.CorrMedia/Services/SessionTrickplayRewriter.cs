#if PATCHED_CORE

using System;
using System.Linq;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Remaps trickplay manifests and tile requests onto the edited (post-cut) timeline
/// when this user has remaining skips.
/// </summary>
public sealed class SessionTrickplayRewriter : ISessionTrickplayRewriter
{
    private readonly ILibraryManager _libraryManager;
    private readonly EditOverrideStore _editOverrideStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionTrickplayRewriter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionTrickplayRewriter"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="editOverrideStore">Per-user apply/filter store.</param>
    /// <param name="httpContextAccessor">Current request, for the playing user.</param>
    /// <param name="logger">Logger.</param>
    public SessionTrickplayRewriter(
        ILibraryManager libraryManager,
        EditOverrideStore editOverrideStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SessionTrickplayRewriter> logger)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _editOverrideStore = editOverrideStore ?? throw new ArgumentNullException(nameof(editOverrideStore));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SessionTrickplayRewriter registered (trickplay on edited timeline)");
    }

    /// <inheritdoc />
    public bool NeedsRewrite(string? itemId)
        => TryGetAppliedEdits(itemId, out var edits) && edits.Skips.Count > 0;

    /// <inheritdoc />
    public TrickplayInfoDto RewriteDto(string? itemId, TrickplayInfoDto original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!TryGetContext(itemId, out var edits, out var originalTicks))
        {
            return original;
        }

        var editedTicks = CorrTrickplayMapper.EditedTicks(originalTicks, edits.Skips);
        if (!editedTicks.HasValue)
        {
            return original;
        }

        return CorrTrickplayMapper.RewriteDto(original, editedTicks.Value);
    }

    /// <inheritdoc />
    public string? BuildHlsPlaylist(string? itemId, TrickplayInfoDto original, Guid mediaSourceId, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!NeedsRewrite(itemId))
        {
            return null;
        }

        return CorrTrickplayMapper.BuildHlsPlaylist(RewriteDto(itemId, original), mediaSourceId, apiKey);
    }

    /// <inheritdoc />
    public bool TryMapEditedThumbnail(string? itemId, int editedIndex, TrickplayInfoDto original, out SessionTrickplayCell cell)
    {
        ArgumentNullException.ThrowIfNull(original);
        cell = default;
        if (!TryGetContext(itemId, out var edits, out var originalTicks) || !originalTicks.HasValue)
        {
            return false;
        }

        var durationSeconds = originalTicks.Value / (double)TimeSpan.TicksPerSecond;
        var keep = CorrTimeline.BuildKeepRanges(edits.Skips, durationSeconds);
        if (!CorrTrickplayMapper.TryMapEditedIndex(
            editedIndex,
            original.Interval,
            original.ThumbnailCount,
            original.TileWidth,
            original.TileHeight,
            keep,
            out var tileIndex,
            out var column,
            out var row))
        {
            return false;
        }

        cell = new SessionTrickplayCell(tileIndex, column, row);
        return true;
    }

    private bool TryGetContext(string? itemId, out CorrEdits edits, out long? originalTicks)
    {
        originalTicks = null;
        if (!TryGetAppliedEdits(itemId, out edits) || edits.Skips.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrEmpty(itemId) || !Guid.TryParse(itemId, out var guid))
        {
            return false;
        }

        originalTicks = _libraryManager.GetItemById(guid)?.RunTimeTicks;
        return originalTicks.HasValue && originalTicks.Value > 0;
    }

    private bool TryGetAppliedEdits(string? itemId, out CorrEdits edits)
        => CorrPlaybackEvaluator.TryGetAppliedEdits(
            _libraryManager,
            _editOverrideStore,
            itemId,
            TryGetRequestUserId(),
            out edits,
            _logger);

    private Guid TryGetRequestUserId()
    {
        var value = _httpContextAccessor.HttpContext?.User?.Claims
            .FirstOrDefault(c => string.Equals(c.Type, "Jellyfin-UserId", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}

#endif
