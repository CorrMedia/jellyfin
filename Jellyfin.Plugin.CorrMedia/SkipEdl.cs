using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CorrMedia.Services;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia
{
    /// <summary>
    /// Clears EDL plans when sessions end. Plans are loaded only for Edited sources
    /// via <see cref="SessionMuteRangeLoader"/> (dual delivery).
    /// </summary>
    public sealed class SkipEdl : IHostedService, IDisposable
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<SkipEdl> _logger;
        private readonly EdlEditStore _edlEditStore;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkipEdl"/> class.
        /// </summary>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="edlEditStore">EDL store.</param>
        public SkipEdl(
            ISessionManager sessionManager,
            ILogger<SkipEdl> logger,
            EdlEditStore edlEditStore)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _edlEditStore = edlEditStore ?? throw new ArgumentNullException(nameof(edlEditStore));
            _sessionManager.SessionEnded += OnSessionEnded;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "SkipEdl started — EDL plans load only for Edited media sources; Original stays untouched.");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void OnSessionEnded(object? sender, SessionEventArgs e)
        {
            if (e?.SessionInfo is null)
            {
                return;
            }

            var session = e.SessionInfo;
            _edlEditStore.Clear(session.Id, session.DeviceId);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _sessionManager.SessionEnded -= OnSessionEnded;
            _disposed = true;
        }
    }
}
