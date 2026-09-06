#if PATCHED_CORE

using System;
using System.Linq;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Tells patched Jellyfin when sidecar edits apply on the normal library item:
/// force transcode, shorten runtime, and use HLS when cuts remain.
/// </summary>
public sealed class SessionCorrDeliveryHint : ISessionCorrDeliveryHint
{
    private readonly ILibraryManager _libraryManager;
    private readonly EditOverrideStore _editOverrideStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionCorrDeliveryHint> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCorrDeliveryHint"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="editOverrideStore">Per-user apply/filter store.</param>
    /// <param name="httpContextAccessor">Current request, for the playing user.</param>
    /// <param name="logger">Logger.</param>
    public SessionCorrDeliveryHint(
        ILibraryManager libraryManager,
        EditOverrideStore editOverrideStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SessionCorrDeliveryHint> logger)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _editOverrideStore = editOverrideStore ?? throw new ArgumentNullException(nameof(editOverrideStore));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SessionCorrDeliveryHint registered (sidecar edits on primary source)");
    }

    /// <inheritdoc />
    public bool ShouldApplySidecarEdits(string? itemId)
        => TryGetAppliedEdits(itemId, out _);

    /// <inheritdoc />
    public bool RequiresHls(string? itemId)
        => TryGetAppliedEdits(itemId, out var edits) && edits.Skips.Count > 0;

    /// <inheritdoc />
    public bool TryGetEditedRunTimeTicks(string? itemId, out long runTimeTicks)
    {
        runTimeTicks = 0;
        if (!TryGetAppliedEdits(itemId, out var edits) || edits.Skips.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrEmpty(itemId) || !Guid.TryParse(itemId, out var guid))
        {
            return false;
        }

        var original = _libraryManager.GetItemById(guid)?.RunTimeTicks;
        var edited = CorrPlaybackEvaluator.EditedRunTimeTicks(original, edits.Skips);
        if (!edited.HasValue)
        {
            return false;
        }

        runTimeTicks = edited.Value;
        return true;
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
