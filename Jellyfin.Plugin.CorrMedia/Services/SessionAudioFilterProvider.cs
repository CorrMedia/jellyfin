#if PATCHED_CORE

using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CorrMedia.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Supplies session-scoped FFmpeg mute/volume/beep filters when there is no cut graph.
/// When skips, video effects, or selective channel edits exist, audio is applied inside
/// <see cref="SessionMediaEditGraphProvider"/> instead.
/// </summary>
public sealed class SessionAudioFilterProvider : MediaBrowser.Controller.MediaEncoding.ISessionAudioFilterProvider
{
    private readonly CorrEditStore _corrEditStore;
    private readonly ILogger<SessionAudioFilterProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionAudioFilterProvider"/> class.
    /// </summary>
    /// <param name="corrEditStore">Sidecar edit store.</param>
    /// <param name="logger">The logger.</param>
    public SessionAudioFilterProvider(
        CorrEditStore corrEditStore,
        ILogger<SessionAudioFilterProvider> logger)
    {
        _corrEditStore = corrEditStore ?? throw new ArgumentNullException(nameof(corrEditStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SessionAudioFilterProvider registered (audio-only path)");
    }

    /// <summary>
    /// Returns an FFmpeg audio filter for mute/volume/beep-only sidecars.
    /// </summary>
    /// <param name="playSessionId">Play session ID.</param>
    /// <param name="deviceId">Device ID.</param>
    /// <param name="startTimeSeconds">Stream start offset.</param>
    /// <returns>Filter string, or null.</returns>
    public string? GetAdditionalAudioFilter(string? playSessionId, string? deviceId, double startTimeSeconds = 0)
    {
        var plan = _corrEditStore.GetPlan(playSessionId, deviceId);
        if (plan is null || plan.MuteRanges.Count == 0)
        {
            return null;
        }

        // Cuts, video effects, and selective channel edits own audio inside filter_complex.
        if (plan.NeedsEditGraph)
        {
            return null;
        }

        var shifted = ShiftRangesToStreamTimeline(plan.MuteRanges, startTimeSeconds);
        if (shifted.Count == 0)
        {
            return null;
        }

        var filter = AudioEditExpressions.BuildFilter(shifted);
        _logger.LogInformation(
            "SessionAudioFilterProvider audio-only filter startSeconds={Start}: {Filter}",
            startTimeSeconds,
            filter);
        return filter;
    }

    private static List<MuteTimeRange> ShiftRangesToStreamTimeline(
        IReadOnlyList<MuteTimeRange> ranges,
        double startTimeSeconds)
    {
        if (startTimeSeconds < 0)
        {
            startTimeSeconds = 0;
        }

        var result = new List<MuteTimeRange>();
        foreach (var r in ranges.OrderBy(x => x.StartTime))
        {
            var relativeStart = r.StartTime - startTimeSeconds;
            var relativeEnd = r.EndTime - startTimeSeconds;
            if (relativeEnd <= 0)
            {
                continue;
            }

            if (relativeStart < 0)
            {
                relativeStart = 0;
            }

            if (relativeEnd > relativeStart)
            {
                result.Add(new MuteTimeRange(relativeStart, relativeEnd, r.Channels, r.Kind, r.Gain, r.Frequency, r.Id));
            }
        }

        return result;
    }
}

#endif
