#if PATCHED_CORE

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services
{
    /// <summary>
    /// Loads corr.json edits into <see cref="CorrEditStore"/> before the first stream request
    /// when this user has sidecar edits enabled for the item.
    /// </summary>
    public sealed class SessionMuteRangeLoader : ISessionMuteRangeLoader
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly CorrEditStore _corrEditStore;
        private readonly EditOverrideStore _editOverrideStore;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SessionMuteRangeLoader> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionMuteRangeLoader"/> class.
        /// </summary>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="corrEditStore">Sidecar edit store.</param>
        /// <param name="editOverrideStore">Per-user apply/filter store.</param>
        /// <param name="httpContextAccessor">Current request, for the playing user.</param>
        /// <param name="logger">Logger.</param>
        public SessionMuteRangeLoader(
            ISessionManager sessionManager,
            ILibraryManager libraryManager,
            CorrEditStore corrEditStore,
            EditOverrideStore editOverrideStore,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SessionMuteRangeLoader> logger)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _corrEditStore = corrEditStore ?? throw new ArgumentNullException(nameof(corrEditStore));
            _editOverrideStore = editOverrideStore ?? throw new ArgumentNullException(nameof(editOverrideStore));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
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
            var userId = session?.UserId ?? TryGetRequestUserId();

            if (!CorrPlaybackEvaluator.TryGetAppliedEdits(
                    _libraryManager,
                    _editOverrideStore,
                    itemId,
                    userId,
                    out var edits,
                    _logger))
            {
                _corrEditStore.Clear(playSessionId, deviceId, session?.Id, session?.DeviceId);
                return Task.CompletedTask;
            }

            BaseItem? item = null;
            if (!string.IsNullOrEmpty(itemId) && Guid.TryParse(itemId, out var guid))
            {
                item = _libraryManager.GetItemById(guid);
            }

            var path = session?.NowPlayingItem?.Path;
            if (string.IsNullOrEmpty(path))
            {
                path = item?.Path;
            }

            if (string.IsNullOrEmpty(path))
            {
                _corrEditStore.Clear(playSessionId, deviceId, session?.Id, session?.DeviceId);
                return Task.CompletedTask;
            }

            _logger.LogDebug(
                "SessionMuteRangeLoader: user overrides User={UserId} applied={Applied}",
                userId,
                edits.Mutes.Count + edits.Skips.Count + edits.VideoEffects.Count);

            var originalDuration = item?.RunTimeTicks is long ticks && ticks > 0
                ? TimeSpan.FromTicks(ticks).TotalSeconds
                : 0;

            string? layout = null;
            var channelCount = 0;
            if (edits.Mutes.Any(m => m.IsSelectiveMute))
            {
                (layout, channelCount) = AudioLayoutProbe.TryProbe(path);
                if (channelCount > 0)
                {
                    _logger.LogInformation(
                        "SessionMuteRangeLoader: probed audio layout={Layout} channels={Channels} for selective mute Path={Path}",
                        layout ?? "(null)",
                        channelCount,
                        path);
                }
                else
                {
                    _logger.LogWarning(
                        "SessionMuteRangeLoader: could not probe audio layout for selective mute Path={Path}; library metadata may be stale",
                        path);
                }
            }

            _corrEditStore.SetPlan(
                new CorrEditPlan(edits.Mutes, edits.Skips, edits.VideoEffects, originalDuration)
                {
                    SourceChannelLayout = layout,
                    SourceChannelCount = channelCount,
                    ShowEditedBadge = _editOverrideStore.Get(userId).ShowEditedBadge,
                },
                playSessionId,
                deviceId,
                session?.Id,
                session?.DeviceId);

            _logger.LogDebug(
                "SessionMuteRangeLoader: sidecar edits mutes={MuteCount} skips={SkipCount} videoEffects={VideoEffectCount} PlaySessionId={PlaySessionId} MediaSourceId={MediaSourceId}",
                edits.Mutes.Count,
                edits.Skips.Count,
                edits.VideoEffects.Count,
                playSessionId ?? "(null)",
                mediaSourceId);

            return Task.CompletedTask;
        }

        private Guid TryGetRequestUserId()
        {
            var value = _httpContextAccessor.HttpContext?.User?.Claims
                .FirstOrDefault(c => string.Equals(c.Type, "Jellyfin-UserId", StringComparison.OrdinalIgnoreCase))
                ?.Value;
            return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
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
