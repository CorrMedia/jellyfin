#if PATCHED_CORE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.CorrMedia.Models;
using MediaBrowser.Controller.MediaEncoding;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Builds FFmpeg graphs: duration-preserving effects (mute / zoom / blur) on the original
/// timeline, then length-altering cuts.
/// </summary>
internal static class EdlFilterComplexBuilder
{
    /// <summary>
    /// Builds a session edit graph, or null if neither cuts nor video effects are present.
    /// </summary>
    /// <param name="plan">EDL plan.</param>
    /// <param name="durationSeconds">Media duration.</param>
    /// <param name="editedStartSeconds">Start offset on the edited (post-cut) timeline, e.g. client seek.</param>
    /// <returns>Graph or null.</returns>
    public static SessionMediaEditGraph? Build(EdlEditPlan plan, double durationSeconds, double editedStartSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.NeedsEditGraph)
        {
            return null;
        }

        if (plan.HasCuts && durationSeconds <= 0)
        {
            // Fallback so keep-tail after last skip still works for short test clips.
            durationSeconds = plan.SkipRanges.Max(r => r.EndTime) + 3600;
        }

        IReadOnlyList<(double Start, double End)> keep;
        if (plan.HasCuts)
        {
            keep = BuildKeepRanges(plan.SkipRanges, durationSeconds);
            if (editedStartSeconds > 0.001)
            {
                keep = TruncateKeepRangesForEditedStart(keep, editedStartSeconds);
            }
        }
        else if (editedStartSeconds > 0.001 && durationSeconds > 0)
        {
            keep = TruncateKeepRangesForEditedStart([(0, durationSeconds)], editedStartSeconds);
        }
        else
        {
            keep = [];
        }

        if (plan.HasCuts && keep.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();

        // 1) Duration-preserving effects on the original timeline (before cuts).
        var videoPad = AppendVideoEffects(sb, plan.VideoEffects);
        if (plan.MuteRanges.Count > 0)
        {
            var expr = BuildVolumeExpression(plan.MuteRanges);
            sb.Append("[0:a]volume='").Append(expr).Append("':eval=frame[amuted];");
        }
        else
        {
            sb.Append("[0:a]anull[amuted];");
        }

        if (keep.Count == 0)
        {
            return new SessionMediaEditGraph
            {
                FilterComplex = sb.ToString().TrimEnd(';'),
                VideoMapLabel = videoPad,
                AudioMapLabel = "amuted"
            };
        }

        // FFmpeg labeled pads are single-use — asplit/split before per-segment trim.
        sb.Append(CultureInfo.InvariantCulture, $"[amuted]asplit={keep.Count}");
        for (var i = 0; i < keep.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[am{i}]");
        }

        sb.Append(';');
        sb.Append(CultureInfo.InvariantCulture, $"[{videoPad}]split={keep.Count}");
        for (var i = 0; i < keep.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[vs{i}]");
        }

        sb.Append(';');

        // 2) Cut: trim keep segments from video + muted audio, then concat.
        for (var i = 0; i < keep.Count; i++)
        {
            var (start, end) = keep[i];
            var s = start.ToString(CultureInfo.InvariantCulture);
            var e = end.ToString(CultureInfo.InvariantCulture);
            sb.Append(CultureInfo.InvariantCulture, $"[vs{i}]trim=start={s}:end={e},setpts=PTS-STARTPTS[v{i}];");
            sb.Append(CultureInfo.InvariantCulture, $"[am{i}]atrim=start={s}:end={e},asetpts=PTS-STARTPTS[a{i}];");
        }

        for (var i = 0; i < keep.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[v{i}][a{i}]");
        }

        sb.Append(CultureInfo.InvariantCulture, $"concat=n={keep.Count}:v=1:a=1[vout][aout]");

        return new SessionMediaEditGraph
        {
            FilterComplex = sb.ToString(),
            VideoMapLabel = "vout",
            AudioMapLabel = "aout"
        };
    }

    /// <summary>
    /// Appends zoom/blur overlays in sidecar order. Each effect is a timed overlay so output size never changes.
    /// </summary>
    /// <param name="sb">Filter graph builder.</param>
    /// <param name="effects">Video effects on the original timeline.</param>
    /// <returns>The output video pad name (without brackets).</returns>
    internal static string AppendVideoEffects(StringBuilder sb, IReadOnlyList<VideoEffect> effects)
    {
        var current = "0:v";
        if (effects is null || effects.Count == 0)
        {
            return current;
        }

        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            var basePad = "vx" + i.ToString(CultureInfo.InvariantCulture) + "b";
            var srcPad = "vx" + i.ToString(CultureInfo.InvariantCulture) + "s";
            var fxPad = "vx" + i.ToString(CultureInfo.InvariantCulture) + "f";
            var outPad = "vx" + i.ToString(CultureInfo.InvariantCulture);
            var enable = BuildEnable(effect.StartTime, effect.EndTime);

            sb.Append(CultureInfo.InvariantCulture, $"[{current}]split=2[{basePad}][{srcPad}];");
            sb.Append(BuildEffectFilters(effect, srcPad, fxPad));
            sb.Append(BuildOverlay(effect, basePad, fxPad, outPad, enable));
            current = outPad;
        }

        return current;
    }

    private static string BuildEffectFilters(VideoEffect effect, string srcPad, string fxPad)
    {
        return effect.Kind == VideoEffectKind.Blur
            ? BuildBlurFilters(effect, srcPad, fxPad)
            : BuildZoomFilters(effect, srcPad, fxPad);
    }

    private static string BuildZoomFilters(VideoEffect effect, string srcPad, string fxPad)
    {
        if (effect.Box is { } box)
        {
            var end = effect.BoxEnd ?? box;
            var w = F(box.Width);
            var h = F(box.Height);
            var xExpr = "max(0,min(iw-ow,iw*" + Unit(effect.StartTime, effect.EndTime, box.X, end.X) + "))";
            var yExpr = "max(0,min(ih-oh,ih*" + Unit(effect.StartTime, effect.EndTime, box.Y, end.Y) + "))";
            return string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]crop=iw*{1}:ih*{2}:x='{3}':y='{4}',scale=2*trunc(iw/(2*{1})):2*trunc(ih/(2*{2}))[{5}];",
                srcPad,
                w,
                h,
                xExpr,
                yExpr,
                fxPad);
        }

        var scale = F(effect.Scale);
        var cx = Unit(effect.StartTime, effect.EndTime, effect.CenterX, effect.CenterXEnd ?? effect.CenterX);
        var cy = Unit(effect.StartTime, effect.EndTime, effect.CenterY, effect.CenterYEnd ?? effect.CenterY);
        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]crop=iw/{1}:ih/{1}:x='max(0,min(iw-ow,iw*{2}-ow/2))':y='max(0,min(ih-oh,ih*{3}-oh/2))',scale=2*trunc(iw*{1}/2):2*trunc(ih*{1}/2)[{4}];",
            srcPad,
            scale,
            cx,
            cy,
            fxPad);
    }

    private static string BuildBlurFilters(VideoEffect effect, string srcPad, string fxPad)
    {
        var radius = F(effect.BlurRadius);
        // One boxblur pass (lp/cp=1). Strength is sidecar radius only; chroma radius is clamped separately.
        var blur = string.Format(
            CultureInfo.InvariantCulture,
            "boxblur=lr='min({0}\\,floor((min(w\\,h)-1)/2))':lp=1:cr='min({0}\\,floor((min(cw\\,ch)-1)/2))':cp=1",
            radius);

        if (effect.Box is { } box)
        {
            var end = effect.BoxEnd ?? box;
            var w = F(box.Width);
            var h = F(box.Height);
            var xExpr = "max(0,min(iw-ow,iw*" + Unit(effect.StartTime, effect.EndTime, box.X, end.X) + "))";
            var yExpr = "max(0,min(ih-oh,ih*" + Unit(effect.StartTime, effect.EndTime, box.Y, end.Y) + "))";
            return string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]crop=2*trunc(iw*{1}/2):2*trunc(ih*{2}/2):x='{3}':y='{4}',{5}[{6}];",
                srcPad,
                w,
                h,
                xExpr,
                yExpr,
                blur,
                fxPad);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]{1}[{2}];",
            srcPad,
            blur,
            fxPad);
    }

    private static string BuildOverlay(VideoEffect effect, string basePad, string fxPad, string outPad, string enable)
    {
        if (effect.Kind == VideoEffectKind.Blur && effect.Box is { } box)
        {
            var end = effect.BoxEnd ?? box;
            var x = Unit(effect.StartTime, effect.EndTime, box.X, end.X);
            var y = Unit(effect.StartTime, effect.EndTime, box.Y, end.Y);
            return string.Format(
                CultureInfo.InvariantCulture,
                "[{0}][{1}]overlay=x='main_w*{2}':y='main_h*{3}':enable='{4}'[{5}];",
                basePad,
                fxPad,
                x,
                y,
                enable,
                outPad);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0}][{1}]overlay=0:0:enable='{2}'[{3}];",
            basePad,
            fxPad,
            enable,
            outPad);
    }

    /// <summary>
    /// Linear 0–1 unit over [start,end] on the original timeline (FFmpeg expr).
    /// </summary>
    private static string Unit(double start, double end, double from, double to)
    {
        if (Math.Abs(to - from) < 0.0005)
        {
            return F(from);
        }

        return "(" + F(from) + "+(" + F(to - from) + ")*clip((t-" + F(start) + ")/" + F(end - start) + ",0,1))";
    }

    private static string BuildEnable(double start, double end)
        => "between(t," + F(start) + "," + F(end) + ")";

    private static string F(double value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Computes keep intervals as the complement of skip ranges on [0, duration).
    /// </summary>
    /// <param name="skipRanges">Skip ranges to omit.</param>
    /// <param name="durationSeconds">Media duration in seconds.</param>
    /// <returns>Keep intervals on the original timeline.</returns>
    internal static IReadOnlyList<(double Start, double End)> BuildKeepRanges(
        IReadOnlyList<MuteTimeRange> skipRanges,
        double durationSeconds)
    {
        var merged = MergeRanges(skipRanges);
        var keep = new List<(double, double)>();
        var cursor = 0.0;
        foreach (var skip in merged)
        {
            if (skip.StartTime > cursor)
            {
                keep.Add((cursor, Math.Min(skip.StartTime, durationSeconds)));
            }

            cursor = Math.Max(cursor, skip.EndTime);
            if (cursor >= durationSeconds)
            {
                break;
            }
        }

        if (cursor < durationSeconds)
        {
            keep.Add((cursor, durationSeconds));
        }

        return keep.Where(k => k.Item2 > k.Item1 + 0.001).ToList();
    }

    /// <summary>
    /// Drops keep media that falls before <paramref name="editedStartSeconds"/> on the post-cut timeline.
    /// </summary>
    /// <param name="keep">Keep intervals on the original timeline.</param>
    /// <param name="editedStartSeconds">Offset on the edited timeline to start from.</param>
    /// <returns>Keep intervals starting at the seek point.</returns>
    internal static IReadOnlyList<(double Start, double End)> TruncateKeepRangesForEditedStart(
        IReadOnlyList<(double Start, double End)> keep,
        double editedStartSeconds)
    {
        if (editedStartSeconds <= 0 || keep.Count == 0)
        {
            return keep;
        }

        var result = new List<(double, double)>();
        var editedCursor = 0.0;
        var started = false;
        foreach (var (start, end) in keep)
        {
            var len = end - start;
            if (len <= 0)
            {
                continue;
            }

            if (!started)
            {
                if (editedCursor + len <= editedStartSeconds + 0.001)
                {
                    editedCursor += len;
                    continue;
                }

                var into = Math.Max(0, editedStartSeconds - editedCursor);
                result.Add((start + into, end));
                started = true;
            }
            else
            {
                result.Add((start, end));
            }

            editedCursor += len;
        }

        return result.Where(k => k.Item2 > k.Item1 + 0.001).ToList();
    }

    private static List<MuteTimeRange> MergeRanges(IReadOnlyList<MuteTimeRange> ranges)
    {
        var sorted = ranges.Where(r => r.EndTime > r.StartTime).OrderBy(r => r.StartTime).ToList();
        var merged = new List<MuteTimeRange>();
        foreach (var r in sorted)
        {
            if (merged.Count == 0 || r.StartTime > merged[^1].EndTime)
            {
                merged.Add(r);
            }
            else if (r.EndTime > merged[^1].EndTime)
            {
                merged[^1] = new MuteTimeRange(merged[^1].StartTime, r.EndTime);
            }
        }

        return merged;
    }

    private static string BuildVolumeExpression(IReadOnlyList<MuteTimeRange> ranges)
    {
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
