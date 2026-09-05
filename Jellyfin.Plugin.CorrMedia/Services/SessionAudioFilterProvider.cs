#if PATCHED_CORE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.CorrMedia.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Supplies session-scoped FFmpeg mute filters when there is no cut graph (mute-only EDL).
/// When skips or video effects exist, mute is applied inside <see cref="SessionMediaEditGraphProvider"/> instead.
/// </summary>
public sealed class SessionAudioFilterProvider : MediaBrowser.Controller.MediaEncoding.ISessionAudioFilterProvider
{
    private readonly EdlEditStore _edlEditStore;
    private readonly ILogger<SessionAudioFilterProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionAudioFilterProvider"/> class.
    /// </summary>
    /// <param name="edlEditStore">EDL store.</param>
    /// <param name="logger">The logger.</param>
    public SessionAudioFilterProvider(
        EdlEditStore edlEditStore,
        ILogger<SessionAudioFilterProvider> logger)
    {
        _edlEditStore = edlEditStore ?? throw new ArgumentNullException(nameof(edlEditStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SessionAudioFilterProvider registered (mute-only path)");
    }

    /// <summary>
    /// Returns an FFmpeg volume filter for mute-only EDLs.
    /// </summary>
    /// <param name="playSessionId">Play session ID.</param>
    /// <param name="deviceId">Device ID.</param>
    /// <param name="startTimeSeconds">Stream start offset.</param>
    /// <returns>Filter string, or null.</returns>
    public string? GetAdditionalAudioFilter(string? playSessionId, string? deviceId, double startTimeSeconds = 0)
    {
        var plan = _edlEditStore.GetPlan(playSessionId, deviceId);
        if (plan is null || plan.MuteRanges.Count == 0)
        {
            return null;
        }

        // Cuts and video effects own mute inside filter_complex.
        if (plan.NeedsEditGraph)
        {
            return null;
        }

        var shifted = ShiftRangesToStreamTimeline(plan.MuteRanges, startTimeSeconds);
        if (shifted.Count == 0)
        {
            return null;
        }

        var expr = BuildVolumeExpression(shifted);
        if (string.IsNullOrEmpty(expr))
        {
            return null;
        }

        var filter = "volume='" + expr + "':eval=frame";
        _logger.LogInformation(
            "SessionAudioFilterProvider mute-only filter startSeconds={Start}: {Filter}",
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
                result.Add(new MuteTimeRange(relativeStart, relativeEnd));
            }
        }

        return result;
    }

    private static string? BuildVolumeExpression(List<MuteTimeRange> ranges)
    {
        if (ranges.Count == 0)
        {
            return null;
        }

        var sorted = ranges.OrderBy(r => r.StartTime).ToList();
        var expr = "1";
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            var r = sorted[i];
            var s = r.StartTime.ToString(CultureInfo.InvariantCulture);
            var e = r.EndTime.ToString(CultureInfo.InvariantCulture);
            expr = "if(between(t," + s + "," + e + "),0," + expr + ")";
        }

        return expr;
    }
}

#endif
