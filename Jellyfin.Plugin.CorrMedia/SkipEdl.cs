using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Jellyfin.Plugin.CorrMedia.Configuration;
using Jellyfin.Plugin.CorrMedia.Services;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia
{
    /// <summary>
    /// Loads sidecar EDL plans into <see cref="EdlEditStore"/> for server-side mute-then-cut.
    /// Does not drive the client playhead.
    /// </summary>
    public sealed class SkipEdl : IHostedService, IDisposable
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<SkipEdl> _logger;
        private readonly System.Timers.Timer _sessionTimer;
        private readonly PluginConfiguration _configuration;
        private readonly EdlEditStore _edlEditStore;
        private readonly ConcurrentDictionary<string, string> _lastPlanSignature = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkipEdl"/> class.
        /// </summary>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="configuration">Config.</param>
        /// <param name="edlEditStore">EDL store.</param>
        public SkipEdl(
            ISessionManager sessionManager,
            ILogger<SkipEdl> logger,
            PluginConfiguration configuration,
            EdlEditStore edlEditStore)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _edlEditStore = edlEditStore ?? throw new ArgumentNullException(nameof(edlEditStore));

            _sessionTimer = new System.Timers.Timer(_configuration.SessionCheckInterval)
            {
                AutoReset = true
            };
            _sessionTimer.Elapsed += OnSessionTimerElapsed;
            _sessionManager.SessionEnded += OnSessionEnded;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "SkipEdl started — server-side EDL mute-then-cut via EdlEditStore (no client Seek).");
            _sessionTimer.Start();
            RefreshAllSessions();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _sessionTimer.Stop();
            return Task.CompletedTask;
        }

        private void OnSessionTimerElapsed(object? sender, ElapsedEventArgs e) => RefreshAllSessions();

        private void RefreshAllSessions()
        {
            foreach (var session in _sessionManager.Sessions)
            {
                var path = session.NowPlayingItem?.Path;
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var edlPath = EdlFile.GetPath(path);
                if (!File.Exists(edlPath))
                {
                    continue;
                }

                var (mutes, skips) = EdlFile.ParseMuteAndSkip(edlPath);
                if (mutes.Count == 0 && skips.Count == 0)
                {
                    continue;
                }

                var signature = $"{mutes.Count}:{skips.Count}:{edlPath}";
                if (_lastPlanSignature.TryGetValue(session.Id, out var prev)
                    && string.Equals(prev, signature, StringComparison.Ordinal))
                {
                    continue;
                }

                _edlEditStore.SetPlan(new EdlEditPlan(mutes, skips), session.Id, session.DeviceId);
                _lastPlanSignature[session.Id] = signature;
            }
        }

        private void OnSessionEnded(object? sender, SessionEventArgs e)
        {
            if (e?.SessionInfo is null)
            {
                return;
            }

            var session = e.SessionInfo;
            _lastPlanSignature.TryRemove(session.Id, out _);
            _edlEditStore.Clear(session.Id, session.DeviceId);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _sessionTimer.Stop();
            _sessionTimer.Dispose();
            _sessionManager.SessionEnded -= OnSessionEnded;
            _disposed = true;
        }
    }
}
