#if PATCHED_CORE

using System;
using System.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Forces HLS for items with EDL skip/cut ranges so clients can seek via segments.
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
        _logger.LogInformation("SessionEdlDeliveryHint registered (force HLS when EDL has cuts)");
    }

    /// <inheritdoc />
    public bool RequiresHls(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId) || !Guid.TryParse(itemId, out var guid))
        {
            return false;
        }

        var path = _libraryManager.GetItemById(guid)?.Path;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var edlPath = EdlFile.GetPath(path);
        if (!File.Exists(edlPath))
        {
            return false;
        }

        var (_, skips) = EdlFile.ParseMuteAndSkip(edlPath);
        return skips.Count > 0;
    }
}

#endif
