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
    /// Clears corr.json edit plans when sessions end.
    /// </summary>
    public sealed class CorrSessionCleanup : IHostedService, IDisposable
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<CorrSessionCleanup> _logger;
        private readonly CorrEditStore _corrEditStore;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrSessionCleanup"/> class.
        /// </summary>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="corrEditStore">Edit plan store.</param>
        public CorrSessionCleanup(
            ISessionManager sessionManager,
            ILogger<CorrSessionCleanup> logger,
            CorrEditStore corrEditStore)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _corrEditStore = corrEditStore ?? throw new ArgumentNullException(nameof(corrEditStore));
            _sessionManager.SessionEnded += OnSessionEnded;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "CorrSessionCleanup started — corr.json plans load when sidecar edits apply for the user.");
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
            _corrEditStore.Clear(session.Id, session.DeviceId);
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
