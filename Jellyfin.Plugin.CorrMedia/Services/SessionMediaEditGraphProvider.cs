using System;
using Jellyfin.Plugin.CorrMedia.Models;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Supplies mute-then-cut FFmpeg graphs for patched Jellyfin.
/// </summary>
#if PATCHED_CORE
public sealed class SessionMediaEditGraphProvider : ISessionMediaEditGraphProvider
#else
public sealed class SessionMediaEditGraphProvider
#endif
{
    private readonly EdlEditStore _edlEditStore;
    private readonly ILogger<SessionMediaEditGraphProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionMediaEditGraphProvider"/> class.
    /// </summary>
    /// <param name="edlEditStore">EDL store.</param>
    /// <param name="logger">Logger.</param>
    public SessionMediaEditGraphProvider(EdlEditStore edlEditStore, ILogger<SessionMediaEditGraphProvider> logger)
    {
        _edlEditStore = edlEditStore ?? throw new ArgumentNullException(nameof(edlEditStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SessionMediaEditGraphProvider registered (mute-then-cut)");
    }

    /// <inheritdoc />
    public bool HasEditGraph(string? playSessionId, string? deviceId)
    {
        var plan = _edlEditStore.GetPlan(playSessionId, deviceId);
        return plan?.HasCuts == true;
    }

    /// <inheritdoc />
    public SessionMediaEditGraph? GetEditGraph(string? playSessionId, string? deviceId, SessionMediaEditGraphContext context)
    {
        var plan = _edlEditStore.GetPlan(playSessionId, deviceId);
        if (plan is null || !plan.HasCuts)
        {
            return null;
        }

        var graph = EdlFilterComplexBuilder.Build(plan, context.DurationSeconds, context.StartTimeSeconds);
        if (graph is not null)
        {
            _logger.LogInformation(
                "EDL edit graph PlaySessionId={PlaySessionId} duration={Duration} editedStart={Start} complexLength={Len}",
                playSessionId ?? "(null)",
                context.DurationSeconds,
                context.StartTimeSeconds,
                graph.FilterComplex.Length);
        }

        return graph;
    }
}
