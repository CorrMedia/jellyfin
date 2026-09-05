#if PATCHED_CORE

using System;
using System.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Forces HLS for Edited sources with EDL skip/cut ranges so clients can seek via segments.
/// </summary>
public sealed class SessionEdlDeliveryHint : ISessionEdlDeliveryHint
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<SessionEdlDeliveryHint> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionEdlDeliveryHint"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="logger">Logger.</param>
    public SessionEdlDeliveryHint(ILibraryManager libraryManager, ILogger<SessionEdlDeliveryHint> logger)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SessionEdlDeliveryHint registered (HLS only for Edited + cuts)");
    }

    /// <inheritdoc />
    public bool IsEdlAppliedMediaSource(string? mediaSourceId, string? itemId = null)
        => EdlMediaSourceIds.IsEdited(mediaSourceId, itemId);

    /// <inheritdoc />
    public bool TryGetLibraryItemId(string? mediaSourceId, out Guid itemId)
        => EdlMediaSourceIds.TryGetItemId(mediaSourceId, out itemId);

    /// <inheritdoc />
    public bool PreferEdlAppliedMediaSources()
        => Plugin.Instance?.Configuration?.PreferEdlApplied ?? true;

    /// <inheritdoc />
    public bool RequiresHls(string? itemId, string? mediaSourceId)
    {
        if (!EdlMediaSourceIds.IsEdited(mediaSourceId, itemId))
        {
            return false;
        }

        if (string.IsNullOrEmpty(itemId) || !Guid.TryParse(itemId, out var guid))
        {
            return false;
        }

        var path = _libraryManager.GetItemById(guid)?.Path;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var corrPath = CorrFile.GetPath(path);
        if (!File.Exists(corrPath))
        {
            return false;
        }

        return CorrFile.Parse(corrPath).Skips.Count > 0;
    }
}

#endif
