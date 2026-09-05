using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Builds FFmpeg audio-filter strings for mute / volume / beep ranges.
/// </summary>
internal static class AudioEditExpressions
{
    /// <summary>
    /// Builds a single-input audio filter (<c>volume</c> or <c>aeval</c>) for the given ranges.
    /// </summary>
    /// <param name="ranges">Audio edits on the timeline used by FFmpeg <c>t</c>.</param>
    /// <returns>Filter string without pad labels.</returns>
    public static string BuildFilter(IReadOnlyList<MuteTimeRange> ranges)
    {
        if (ranges.Count == 0)
        {
            return "anull";
        }

        if (ranges.Any(r => r.Kind == AudioEditKind.Beep))
        {
            return "aeval='" + BuildAevalExprs(ranges) + "':c=same";
        }

        return "volume='" + BuildSourceGainExpression(ranges) + "':eval=frame";
    }

    /// <summary>
    /// Nested <c>if(between(t,…),gain,…)</c> expression. Beep and mute use gain 0.
    /// Earlier-precedence ranges (beep, then mute, then volume) win on overlap.
    /// </summary>
    /// <param name="ranges">Audio edits.</param>
    /// <returns>FFmpeg expression evaluating to 0–1.</returns>
    public static string BuildSourceGainExpression(IReadOnlyList<MuteTimeRange> ranges)
    {
        var ordered = OrderForPrecedence(ranges);
        var expr = "1";
        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            var r = ordered[i];
            var gain = r.Kind == AudioEditKind.Volume ? F(r.Gain) : "0";
            expr = "if(between(t," + F(r.StartTime) + "," + F(r.EndTime) + ")," + gain + "," + expr + ")";
        }

        return expr;
    }

    private static string BuildAevalExprs(IReadOnlyList<MuteTimeRange> ranges)
        => "val(ch)*" + BuildSourceGainExpression(ranges) + "+" + BuildBeepExpression(ranges);

    private static string BuildBeepExpression(IReadOnlyList<MuteTimeRange> ranges)
    {
        var beeps = OrderForPrecedence(ranges.Where(r => r.Kind == AudioEditKind.Beep).ToList());
        var expr = "0";
        for (var i = beeps.Count - 1; i >= 0; i--)
        {
            var r = beeps[i];
            var tone = F(r.Gain) + "*sin(2*PI*" + F(r.Frequency) + "*t)";
            expr = "if(between(t," + F(r.StartTime) + "," + F(r.EndTime) + ")," + tone + "," + expr + ")";
        }

        return expr;
    }

    private static List<MuteTimeRange> OrderForPrecedence(IReadOnlyList<MuteTimeRange> ranges)
        => ranges
            .Where(r => r.EndTime > r.StartTime)
            .OrderBy(r => r.Kind switch
            {
                AudioEditKind.Beep => 0,
                AudioEditKind.Mute => 1,
                _ => 2
            })
            .ThenBy(r => r.StartTime)
            .ToList();

    private static string F(double value)
        => value.ToString(CultureInfo.InvariantCulture);
}
