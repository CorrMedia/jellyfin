using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Builds FFmpeg audio-filter strings for mute / volume / beep ranges.
/// Uses flat <c>between</c> sums/products (not nested <c>if</c>) so large sidecars parse.
/// </summary>
internal static class AudioEditExpressions
{
    /// <summary>
    /// FFmpeg <c>enable=</c> / eval expressions fail or OOM with huge flat sums; keep chunks small.
    /// </summary>
    private const int MaxBetweensPerExpression = 20;

    /// <summary>
    /// Builds a single-input audio filter (<c>volume</c> or <c>aeval</c>) for the given ranges.
    /// </summary>
    /// <param name="ranges">Audio edits on the timeline used by FFmpeg <c>t</c>.</param>
    /// <returns>Filter string without pad labels.</returns>
    public static string BuildFilter(IReadOnlyList<MuteTimeRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var valid = ValidRanges(ranges);
        if (valid.Count == 0)
        {
            return "anull";
        }

        if (valid.Exists(r => r.Kind == AudioEditKind.Beep))
        {
            return "aeval='" + BuildAevalExprs(valid) + "':c=same";
        }

        var mutes = valid.Where(r => r.Kind == AudioEditKind.Mute).ToList();
        var volumes = valid.Where(r => r.Kind == AudioEditKind.Volume).ToList();

        if (volumes.Count == 0)
        {
            return BuildMuteEnableFilter(mutes);
        }

        var volumeFilter = "volume='" + BuildVolumeProductExpression(volumes) + "':eval=frame";
        if (mutes.Count == 0)
        {
            return volumeFilter;
        }

        // Volume first, then mute (mute wins on overlap).
        return volumeFilter + "," + BuildMuteEnableFilter(mutes);
    }

    /// <summary>
    /// Flat source-gain expression (0–1). Mute and beep windows force 0; volume uses a product of
    /// <c>(1+(g-1)*between)</c> terms. Used by <c>aeval</c> and tests.
    /// </summary>
    /// <param name="ranges">Audio edits.</param>
    /// <returns>FFmpeg expression evaluating to 0–1.</returns>
    public static string BuildSourceGainExpression(IReadOnlyList<MuteTimeRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var valid = ValidRanges(ranges);
        if (valid.Count == 0)
        {
            return "1";
        }

        var volumes = valid.Where(r => r.Kind == AudioEditKind.Volume).ToList();
        var zeroWindows = valid.Where(r => r.Kind is AudioEditKind.Mute or AudioEditKind.Beep).ToList();
        var product = volumes.Count == 0 ? "1" : BuildVolumeProductExpression(volumes);
        if (zeroWindows.Count == 0)
        {
            return product;
        }

        // Product of chunked mute masks (commas escaped for eval=).
        var sb = new StringBuilder(product);
        foreach (var chunk in Chunk(zeroWindows, MaxBetweensPerExpression))
        {
            sb.Append("*max(0\\,1-(").Append(BuildBetweenSum(chunk)).Append("))");
        }

        return sb.ToString();
    }

    private static string BuildAevalExprs(IReadOnlyList<MuteTimeRange> ranges)
        => "val(ch)*" + BuildSourceGainExpression(ranges) + "+" + BuildBeepExpression(ranges);

    private static string BuildBeepExpression(IReadOnlyList<MuteTimeRange> ranges)
    {
        var beeps = ranges
            .Where(r => r.Kind == AudioEditKind.Beep && r.EndTime > r.StartTime)
            .OrderBy(r => r.StartTime)
            .ToList();
        if (beeps.Count == 0)
        {
            return "0";
        }

        var parts = new List<string>();
        foreach (var chunk in Chunk(beeps, MaxBetweensPerExpression))
        {
            var sb = new StringBuilder();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('+');
                }

                var r = chunk[i];
                sb.Append('(')
                    .Append(F(r.Gain))
                    .Append("*sin(2*PI*")
                    .Append(F(r.Frequency))
                    .Append("*t)*")
                    .Append(Between(r))
                    .Append(')');
            }

            parts.Add(sb.ToString());
        }

        return string.Join("+", parts);
    }

    private static string BuildMuteEnableFilter(List<MuteTimeRange> mutes)
    {
        if (mutes.Count == 0)
        {
            return "anull";
        }

        // Chain chunked enables so each expression stays under FFmpeg's eval limits.
        var parts = new List<string>();
        foreach (var chunk in Chunk(mutes, MaxBetweensPerExpression))
        {
            parts.Add("volume=0:enable='" + BuildBetweenSum(chunk) + "'");
        }

        return string.Join(",", parts);
    }

    private static string BuildVolumeProductExpression(List<MuteTimeRange> volumes)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < volumes.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('*');
            }

            var r = volumes[i];
            sb.Append("(1+(")
                .Append(F(r.Gain))
                .Append("-1)*")
                .Append(Between(r))
                .Append(')');
        }

        return sb.ToString();
    }

    private static string BuildBetweenSum(List<MuteTimeRange> ranges)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < ranges.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('+');
            }

            sb.Append(Between(ranges[i]));
        }

        return sb.ToString();
    }

    private static IEnumerable<List<MuteTimeRange>> Chunk(List<MuteTimeRange> ranges, int size)
    {
        for (var i = 0; i < ranges.Count; i += size)
        {
            yield return ranges.GetRange(i, Math.Min(size, ranges.Count - i));
        }
    }

    private static string Between(MuteTimeRange r)
        => "between(t," + F(r.StartTime) + "," + F(r.EndTime) + ")";

    private static List<MuteTimeRange> ValidRanges(IReadOnlyList<MuteTimeRange> ranges)
        => ranges.Where(r => r.EndTime > r.StartTime).ToList();

    private static string F(double value)
        => value.ToString(CultureInfo.InvariantCulture);
}
