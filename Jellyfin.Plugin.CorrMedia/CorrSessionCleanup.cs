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
    /// Clears corr.json edit plans when sessions end. Plans are loaded only for Edited sources.
    /// </summary>
    public sealed class CorrSessionCleanup : IHostedService, IDisposable
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<CorrSessionCleanup> _logger;
        private readonly EdlEditStore _edlEditStore;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrSessionCleanup"/> class.
        /// </summary>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="edlEditStore">Edit plan store.</param>
        public CorrSessionCleanup(
            ISessionManager sessionManager,
            ILogger<CorrSessionCleanup> logger,
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
                "CorrSessionCleanup started — corr.json plans load only for Edited media sources; Original stays untouched.");
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
