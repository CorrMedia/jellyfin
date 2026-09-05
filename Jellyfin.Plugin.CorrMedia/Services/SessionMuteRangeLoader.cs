#if PATCHED_CORE

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services
{
    /// <summary>
    /// Loads EDL mute/skip into <see cref="EdlEditStore"/> before the first stream request,
    /// only when the client selected the Edited media source.
    /// </summary>
    public sealed class SessionMuteRangeLoader : ISessionMuteRangeLoader
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly EdlEditStore _edlEditStore;
        private readonly ILogger<SessionMuteRangeLoader> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionMuteRangeLoader"/> class.
        /// </summary>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="edlEditStore">EDL store.</param>
        /// <param name="logger">Logger.</param>
        public SessionMuteRangeLoader(
            ISessionManager sessionManager,
            ILibraryManager libraryManager,
            EdlEditStore edlEditStore,
            ILogger<SessionMuteRangeLoader> logger)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _edlEditStore = edlEditStore ?? throw new ArgumentNullException(nameof(edlEditStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task EnsureMuteRangesLoadedAsync(
            string? playSessionId,
            string? deviceId,
            string? itemId,
            string? mediaSourceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var session = FindSession(playSessionId, deviceId);

            // Original (or unknown) source: clear any prior Edited plan so mute/cut cannot leak.
            if (!EdlMediaSourceIds.IsEdited(mediaSourceId))
            {
                _edlEditStore.Clear(playSessionId, deviceId, session?.Id, session?.DeviceId);
                return Task.CompletedTask;
            }

            var path = session?.NowPlayingItem?.Path;
            if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(itemId) && Guid.TryParse(itemId, out var guid))
            {
                path = _libraryManager.GetItemById(guid)?.Path;
            }

            if (string.IsNullOrEmpty(path))
            {
                return Task.CompletedTask;
            }

            var edlPath = EdlFile.GetPath(path);
            if (!File.Exists(edlPath))
            {
                return Task.CompletedTask;
            }

            var (mutes, skips) = EdlFile.ParseMuteAndSkip(edlPath);
            if (mutes.Count == 0 && skips.Count == 0)
            {
                return Task.CompletedTask;
            }

            _edlEditStore.SetPlan(
                new EdlEditPlan(mutes, skips),
                playSessionId,
                deviceId,
                session?.Id,
                session?.DeviceId);

            _logger.LogDebug(
                "SessionMuteRangeLoader: Edited source mutes={MuteCount} skips={SkipCount} PlaySessionId={PlaySessionId} MediaSourceId={MediaSourceId}",
                mutes.Count,
                skips.Count,
                playSessionId ?? "(null)",
                mediaSourceId);

            return Task.CompletedTask;
        }

        private SessionInfo? FindSession(string? playSessionId, string? deviceId)
        {
            foreach (var s in _sessionManager.Sessions)
            {
                if (!string.IsNullOrEmpty(playSessionId) && string.Equals(s.Id, playSessionId, StringComparison.Ordinal))
                {
                    return s;
                }

                if (!string.IsNullOrEmpty(deviceId) && string.Equals(s.DeviceId, deviceId, StringComparison.Ordinal))
                {
                    return s;
                }
            }

            return null;
        }
    }
}

#endif
