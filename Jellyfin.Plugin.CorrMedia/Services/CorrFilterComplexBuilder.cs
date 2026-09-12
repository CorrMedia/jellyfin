#if PATCHED_CORE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.CorrMedia.Models;
using MediaBrowser.Controller.MediaEncoding;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Builds FFmpeg graphs: duration-preserving effects (audio + video) on the original
/// timeline, then length-altering cuts. Output frame size never changes.
/// </summary>
internal static class CorrFilterComplexBuilder
{
    /// <summary>
    /// Seconds the start-of-stream "Edited" badge stays on the delivered clock.
    /// </summary>
    internal const double EditedBadgeDurationSeconds = 4;

    /// <summary>
    /// Builds a session edit graph, or null when <see cref="CorrEditPlan.NeedsEditGraph"/> is false.
    /// </summary>
    /// <param name="plan">Sidecar edit plan.</param>
    /// <param name="durationSeconds">Uncut source duration in seconds.</param>
    /// <param name="editedStartSeconds">Start offset on the edited (post-cut) timeline, e.g. client seek.</param>
    /// <param name="inputChannelLayout">Source audio layout (e.g. 5.1).</param>
    /// <param name="inputChannelCount">Source audio channel count.</param>
    /// <param name="outputAudioChannels">Requested output channel count.</param>
    /// <param name="stereoDownmixFilter">Optional pan/aformat filter when downmixing to stereo after mute.</param>
    /// <param name="burnInGraphicalSubtitleInputIndex">FFmpeg input file index (0 = main, 1 = external graphical).</param>
    /// <param name="burnInGraphicalSubtitleStreamIndex">Stream index within that input of a PGS/DVD stream to overlay before cuts.</param>
    /// <param name="burnInGraphicalSubtitleFilters">Scale/format filters for that subtitle stream.</param>
    /// <param name="burnInTextSubtitleFilter">FFmpeg <c>subtitles=</c> filter for text/ASS burn-in before cuts.</param>
    /// <returns>Graph or null.</returns>
    public static SessionMediaEditGraph? Build(
        CorrEditPlan plan,
        double durationSeconds,
        double editedStartSeconds = 0,
        string? inputChannelLayout = null,
        int inputChannelCount = 0,
        int outputAudioChannels = 0,
        string? stereoDownmixFilter = null,
        int burnInGraphicalSubtitleInputIndex = 0,
        int? burnInGraphicalSubtitleStreamIndex = null,
        string? burnInGraphicalSubtitleFilters = null,
        string? burnInTextSubtitleFilter = null)
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
        double inputSeekSeconds = 0;
        if (plan.HasCuts)
        {
            var fullKeep = CorrTimeline.BuildKeepRanges(plan.SkipRanges, durationSeconds);
            if (editedStartSeconds > 0.001
                && CorrTimeline.TryMapEditedToOriginal(editedStartSeconds, fullKeep, out var originalSeek))
            {
                inputSeekSeconds = originalSeek;
            }

            keep = editedStartSeconds > 0.001
                ? CorrTimeline.TruncateKeepRangesForEditedStart(fullKeep, editedStartSeconds)
                : fullKeep;

            if (keep.Count == 0)
            {
                // Seek at/past the edited duration — keep a tiny tail so HLS does not
                // fall back to the uncut source (which breaks the last playlist segments).
                keep = CorrTimeline.TailKeepRange(fullKeep);
            }
        }
        else if (editedStartSeconds > 0.001)
        {
            // Mute/effects-only (no cuts): demuxer -ss is enough. Do not invent a
            // trim/concat keep range — that resets PTS via setpts and makes HLS
            // restart-thrash when the client requests a prior segment after seek.
            inputSeekSeconds = editedStartSeconds;
            keep = [];
        }
        else
        {
            keep = [];
        }

        if (plan.HasCuts && keep.Count == 0)
        {
            return null;
        }

        // Demuxer -ss resets filter t near 0; shift original-absolute times into post-seek clock.
        var mutes = plan.MuteRanges;
        var effects = plan.VideoEffects;
        if (inputSeekSeconds > 0.001)
        {
            mutes = ShiftMuteRanges(mutes, inputSeekSeconds);
            effects = ShiftVideoEffects(effects, inputSeekSeconds);
            keep = ShiftKeepRanges(keep, inputSeekSeconds);
        }

        var sb = new StringBuilder();

        // 1) Duration-preserving effects on the (possibly seek-relative) timeline, then cuts.
        var videoPad = AppendVideoEffects(sb, effects);
        videoPad = AppendGraphicalSubtitleBurnIn(
            sb,
            videoPad,
            burnInGraphicalSubtitleStreamIndex,
            burnInGraphicalSubtitleFilters,
            burnInGraphicalSubtitleInputIndex);
        videoPad = AppendTextSubtitleBurnIn(sb, videoPad, burnInTextSubtitleFilter, inputSeekSeconds);
        var audioPad = AppendAudioEdits(
            sb,
            mutes,
            inputChannelLayout,
            inputChannelCount,
            outputAudioChannels,
            stereoDownmixFilter);

        var showBadge = editedStartSeconds <= 0.001;

        if (keep.Count == 0)
        {
            videoPad = AppendEditedBadge(sb, videoPad, showBadge);
            return new SessionMediaEditGraph
            {
                FilterComplex = sb.ToString().TrimEnd(';'),
                VideoMapLabel = videoPad,
                AudioMapLabel = audioPad,
                InputSeekSeconds = inputSeekSeconds
            };
        }

        // FFmpeg labeled pads are single-use — asplit/split before per-segment trim.
        sb.Append(CultureInfo.InvariantCulture, $"[{audioPad}]asplit={keep.Count}");
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
            // Last keep that runs to original EOF: omit end so A/V both drain the file.
            var isTail = i == keep.Count - 1 && end >= durationSeconds - 0.001;
            if (isTail)
            {
                sb.Append(CultureInfo.InvariantCulture, $"[vs{i}]trim=start={s},setpts=PTS-STARTPTS[v{i}];");
                sb.Append(CultureInfo.InvariantCulture, $"[am{i}]atrim=start={s},asetpts=PTS-STARTPTS[a{i}];");
            }
            else
            {
                var e = end.ToString(CultureInfo.InvariantCulture);
                sb.Append(CultureInfo.InvariantCulture, $"[vs{i}]trim=start={s}:end={e},setpts=PTS-STARTPTS[v{i}];");
                sb.Append(CultureInfo.InvariantCulture, $"[am{i}]atrim=start={s}:end={e},asetpts=PTS-STARTPTS[a{i}];");
            }
        }

        for (var i = 0; i < keep.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[v{i}][a{i}]");
        }

        sb.Append(CultureInfo.InvariantCulture, $"concat=n={keep.Count}:v=1:a=1[vout][aout]");
        var videoOut = AppendEditedBadge(sb, "vout", showBadge);

        return new SessionMediaEditGraph
        {
            FilterComplex = sb.ToString().TrimEnd(';'),
            VideoMapLabel = videoOut,
            AudioMapLabel = "aout",
            InputSeekSeconds = inputSeekSeconds
        };
    }

    /// <summary>
    /// Appends mute / volume / beep (all-channel or selective channelsplit) then optional stereo downmix.
    /// </summary>
    /// <param name="sb">Filter graph being built.</param>
    /// <param name="mutes">Audio edit ranges on the original timeline.</param>
    /// <param name="inputChannelLayout">Source audio layout (e.g. 5.1).</param>
    /// <param name="inputChannelCount">Source audio channel count.</param>
    /// <param name="outputAudioChannels">Requested output channel count.</param>
    /// <param name="stereoDownmixFilter">Optional pan/aformat filter when downmixing to stereo after edits.</param>
    /// <returns>Output audio pad name without brackets.</returns>
    internal static string AppendAudioEdits(
        StringBuilder sb,
        IReadOnlyList<MuteTimeRange> mutes,
        string? inputChannelLayout,
        int inputChannelCount,
        int outputAudioChannels,
        string? stereoDownmixFilter)
    {
        string mutedPad;
        if (mutes.Count == 0)
        {
            sb.Append("[0:a]anull[amuted];");
            mutedPad = "amuted";
        }
        else if (!mutes.Any(m => m.IsSelectiveMute)
                 || !TryAppendSelectiveMute(sb, mutes, inputChannelLayout, inputChannelCount, out mutedPad))
        {
            sb.Append("[0:a]").Append(AudioEditExpressions.BuildFilter(mutes)).Append("[amuted];");
            mutedPad = "amuted";
        }

        // Mute on the source layout, then downmix if the client asked for stereo.
        if (outputAudioChannels == 2
            && inputChannelCount > 2
            && !string.IsNullOrWhiteSpace(stereoDownmixFilter))
        {
            sb.Append(CultureInfo.InvariantCulture, $"[{mutedPad}]{stereoDownmixFilter}[adown];");
            return "adown";
        }

        return mutedPad;
    }

    private static bool TryAppendSelectiveMute(
        StringBuilder sb,
        IReadOnlyList<MuteTimeRange> mutes,
        string? inputChannelLayout,
        int inputChannelCount,
        out string mutedPad)
    {
        mutedPad = "amuted";
        var layoutName = ChannelLayoutHelper.ResolveLayoutName(inputChannelLayout, inputChannelCount);
        var sourceChannels = ChannelLayoutHelper.GetChannels(layoutName, inputChannelCount);
        if (string.IsNullOrEmpty(layoutName) || sourceChannels.Count == 0)
        {
            return false;
        }

        // Per source channel: edits that target it (after layout resolution).
        var editsByChannel = new Dictionary<string, List<MuteTimeRange>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ch in sourceChannels)
        {
            editsByChannel[ch] = [];
        }

        var anySelectiveHit = false;
        foreach (var mute in mutes)
        {
            var targets = ChannelLayoutHelper.ResolveMuteTargets(mute.NormalizedChannels, sourceChannels);
            // Named-channel edits always use the split path when the source has more than
            // one channel (so we never collapse them to volume-all on a multi-channel file).
            if (mute.IsSelectiveMute && sourceChannels.Count > 1)
            {
                anySelectiveHit = true;
            }

            foreach (var ch in targets)
            {
                editsByChannel[ch].Add(mute);
            }
        }

        // If every channel ends up fully muted for the same windows, volume-all is simpler —
        // but selective path is still correct. Prefer channelsplit only when at least one
        // channel stays unmuted for part of the timeline.
        if (!anySelectiveHit)
        {
            return false;
        }

        // channelsplit into labeled mono pads.
        sb.Append(CultureInfo.InvariantCulture, $"[0:a]channelsplit=channel_layout={layoutName}");
        foreach (var ch in sourceChannels)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[{ch}]");
        }

        sb.Append(';');

        var joinInputs = new List<string>();
        foreach (var ch in sourceChannels)
        {
            var channelEdits = editsByChannel[ch];
            if (channelEdits.Count == 0)
            {
                joinInputs.Add(ch);
                continue;
            }

            var outPad = ch + "m";
            sb.Append(CultureInfo.InvariantCulture, $"[{ch}]{AudioEditExpressions.BuildFilter(channelEdits)}[{outPad}];");
            joinInputs.Add(outPad);
        }

        foreach (var pad in joinInputs)
        {
            sb.Append(CultureInfo.InvariantCulture, $"[{pad}]");
        }

        sb.Append(CultureInfo.InvariantCulture, $"join=inputs={joinInputs.Count}:channel_layout={layoutName}[amuted];");
        return true;
    }

    /// <summary>
    /// Overlays a graphical subtitle (PGS/DVD, internal or external) onto the original-timeline
    /// video pad. Must run after sidecar video effects and before keep-range trims.
    /// </summary>
    /// <param name="sb">Filter graph being built.</param>
    /// <param name="videoPad">Current video pad name without brackets.</param>
    /// <param name="streamIndex">Stream index within the input, or null to skip.</param>
    /// <param name="filters">Scale/format chain for the subtitle stream.</param>
    /// <param name="inputIndex">FFmpeg input file index (0 = main, 1 = external graphical).</param>
    /// <returns>Video pad after overlay, or <paramref name="videoPad"/> when skipped.</returns>
    internal static string AppendGraphicalSubtitleBurnIn(
        StringBuilder sb,
        string videoPad,
        int? streamIndex,
        string? filters,
        int inputIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(sb);
        if (string.IsNullOrEmpty(videoPad) || !streamIndex.HasValue || streamIndex.Value < 0)
        {
            return videoPad;
        }

        if (inputIndex is not 0 and not 1)
        {
            inputIndex = 0;
        }

        var subFilters = SanitizeSubtitleFilters(filters);
        sb.Append(CultureInfo.InvariantCulture, $"[{inputIndex}:{streamIndex.Value}]{subFilters}[psub];");
        sb.Append(CultureInfo.InvariantCulture, $"[{videoPad}][psub]overlay=eof_action=pass:repeatlast=0[vburn];");
        return "vburn";
    }

    /// <summary>
    /// Burns a text/ASS subtitle file onto the video pad via <c>subtitles=</c>.
    /// When <paramref name="inputSeekSeconds"/> &gt; 0, temporarily restores absolute PTS
    /// so cue times in the subtitle file still line up after demuxer <c>-ss</c>.
    /// </summary>
    /// <param name="sb">Filter graph being built.</param>
    /// <param name="videoPad">Current video pad name without brackets.</param>
    /// <param name="filter">Complete <c>subtitles=...</c> filter, or null to skip.</param>
    /// <param name="inputSeekSeconds">Demuxer seek on the original timeline.</param>
    /// <returns>Video pad after burn-in, or <paramref name="videoPad"/> when skipped.</returns>
    internal static string AppendTextSubtitleBurnIn(
        StringBuilder sb,
        string videoPad,
        string? filter,
        double inputSeekSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(sb);
        var sanitized = SanitizeTextSubtitleFilter(filter);
        if (string.IsNullOrEmpty(videoPad) || sanitized is null)
        {
            return videoPad;
        }

        var outPad = UniqueBurnInPad(videoPad);
        if (inputSeekSeconds > 0.001)
        {
            var absPad = outPad + "abs";
            var subPad = outPad + "sub";
            var seek = F(inputSeekSeconds);
            sb.Append(CultureInfo.InvariantCulture, $"[{videoPad}]setpts=PTS+{seek}/TB[{absPad}];");
            sb.Append(CultureInfo.InvariantCulture, $"[{absPad}]{sanitized}[{subPad}];");
            sb.Append(CultureInfo.InvariantCulture, $"[{subPad}]setpts=PTS-STARTPTS[{outPad}];");
        }
        else
        {
            sb.Append(CultureInfo.InvariantCulture, $"[{videoPad}]{sanitized}[{outPad}];");
        }

        return outPad;
    }

    /// <summary>
    /// Burns a corner "Edited" badge onto the delivered video pad for the first
    /// <see cref="EditedBadgeDurationSeconds"/> of a play-from-start encode.
    /// Seek / HLS restarts omit it so it does not reappear mid-title.
    /// </summary>
    /// <param name="sb">Filter graph being built.</param>
    /// <param name="videoPad">Current video pad name without brackets.</param>
    /// <param name="fromEditedStart">True when the client started at edited t≈0.</param>
    /// <returns>Video pad after the badge, or <paramref name="videoPad"/> when skipped.</returns>
    internal static string AppendEditedBadge(StringBuilder sb, string videoPad, bool fromEditedStart)
    {
        ArgumentNullException.ThrowIfNull(sb);
        if (!fromEditedStart || string.IsNullOrEmpty(videoPad))
        {
            return videoPad;
        }

        var outPad = string.Equals(videoPad, "vmark", StringComparison.Ordinal) ? "vmark2" : "vmark";
        var end = F(EditedBadgeDurationSeconds);
        var font = GetDrawtextFontOption();
        if (sb.Length > 0 && sb[^1] != ';')
        {
            sb.Append(';');
        }

        sb.Append(CultureInfo.InvariantCulture, $"[{videoPad}]drawtext=text='Edited':x=w-tw-w*0.03:y=h*0.04:fontsize=h/28:fontcolor=white@0.92:box=1:boxcolor=black@0.55:boxborderw=12:enable='between(t,0,{end})'{font}[{outPad}];");
        return outPad;
    }

    /// <summary>
    /// Resolves <c>fontfile</c> when a system TTF exists; otherwise fontconfig <c>Sans</c>.
    /// </summary>
    /// <returns>A <c>drawtext</c> font option including the leading colon.</returns>
    private static string GetDrawtextFontOption()
    {
        foreach (var candidate in EnumerateDrawtextFontCandidates())
        {
            if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
            {
                return ":fontfile='" + EscapeDrawtextPath(candidate) + "'";
            }
        }

        return ":font=Sans";
    }

    /// <summary>
    /// Common Linux and Windows sans-serif files for <c>drawtext</c>.
    /// </summary>
    /// <returns>Candidate font paths, first existing wins.</returns>
    private static IEnumerable<string> EnumerateDrawtextFontCandidates()
    {
        yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
        yield return "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";
        yield return "/usr/share/fonts/truetype/freefont/FreeSans.ttf";

        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrEmpty(fontsDir))
        {
            yield return Path.Combine(fontsDir, "segoeui.ttf");
            yield return Path.Combine(fontsDir, "arial.ttf");
        }
    }

    /// <summary>
    /// Escapes a filesystem path for an FFmpeg <c>drawtext</c> <c>fontfile</c> value.
    /// </summary>
    /// <param name="path">Absolute font path.</param>
    /// <returns>Filter-safe path (forward slashes, escaped colon and quote).</returns>
    private static string EscapeDrawtextPath(string path)
        => path.Replace('\\', '/')
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);

    private static List<MuteTimeRange> ShiftMuteRanges(
        IReadOnlyList<MuteTimeRange> ranges,
        double seekSeconds)
    {
        var result = new List<MuteTimeRange>();
        foreach (var r in ranges)
        {
            var start = r.StartTime - seekSeconds;
            var end = r.EndTime - seekSeconds;
            if (end <= 0.001)
            {
                continue;
            }

            if (start < 0)
            {
                start = 0;
            }

            if (end > start + 0.001)
            {
                result.Add(new MuteTimeRange(start, end, r.Channels, r.Kind, r.Gain, r.Frequency, r.Id));
            }
        }

        return result;
    }

    private static List<VideoEffect> ShiftVideoEffects(
        IReadOnlyList<VideoEffect> effects,
        double seekSeconds)
    {
        var result = new List<VideoEffect>();
        foreach (var e in effects)
        {
            var start = e.StartTime - seekSeconds;
            var end = e.EndTime - seekSeconds;
            if (end <= 0.001)
            {
                continue;
            }

            if (start < 0)
            {
                start = 0;
            }

            if (end > start + 0.001)
            {
                result.Add(e with { StartTime = start, EndTime = end });
            }
        }

        return result;
    }

    private static List<(double Start, double End)> ShiftKeepRanges(
        IReadOnlyList<(double Start, double End)> keep,
        double seekSeconds)
    {
        var result = new List<(double, double)>();
        foreach (var (start, end) in keep)
        {
            var s = start - seekSeconds;
            var e = end - seekSeconds;
            if (e <= 0.001)
            {
                continue;
            }

            if (s < 0)
            {
                s = 0;
            }

            if (e > s + 0.001)
            {
                result.Add((s, e));
            }
        }

        return result;
    }

    private static string UniqueBurnInPad(string videoPad)
        => string.Equals(videoPad, "vburn", StringComparison.Ordinal) ? "vtext" : "vburn";

    private static string SanitizeSubtitleFilters(string? filters)
    {
        if (string.IsNullOrWhiteSpace(filters)
            || filters.Contains('[', StringComparison.Ordinal)
            || filters.Contains(']', StringComparison.Ordinal)
            || filters.Contains(';', StringComparison.Ordinal)
            || filters.Contains('"', StringComparison.Ordinal)
            || filters.Contains('\'', StringComparison.Ordinal))
        {
            return "format=yuva420p";
        }

        return filters.Trim();
    }

    private static string? SanitizeTextSubtitleFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var trimmed = filter.Trim();
        if (!trimmed.StartsWith("subtitles=", StringComparison.Ordinal)
            || trimmed.Contains('[', StringComparison.Ordinal)
            || trimmed.Contains(']', StringComparison.Ordinal)
            || trimmed.Contains(';', StringComparison.Ordinal)
            || trimmed.Contains('\n', StringComparison.Ordinal)
            || trimmed.Contains('\r', StringComparison.Ordinal))
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>
    /// Appends duration-preserving video overlays in sidecar order. Output size never changes
    /// (zoom scales to fill; crop pads; cover/blur/pixelate overlay a region or the full frame).
    /// </summary>
    /// <param name="sb">Filter graph builder.</param>
    /// <param name="effects">Video effects on the original timeline.</param>
    /// <returns>The output video pad name (without brackets).</returns>
    internal static string AppendVideoEffects(StringBuilder sb, IReadOnlyList<VideoEffect> effects)
    {
        // EncodingHelper maps -map "[label]". A bare "0:v" is an input specifier,
        // not a filter pad, so audio-only graphs must still emit a labeled video pad.
        if (effects is null || effects.Count == 0)
        {
            sb.Append("[0:v]null[vout];");
            return "vout";
        }

        var current = "0:v";

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
        return effect.Kind switch
        {
            VideoEffectKind.Blur => BuildBlurFilters(effect, srcPad, fxPad),
            VideoEffectKind.Cover or VideoEffectKind.Blank => BuildCoverFilters(effect, srcPad, fxPad),
            VideoEffectKind.Pixelate => BuildPixelateFilters(effect, srcPad, fxPad),
            VideoEffectKind.Crop => BuildCropFilters(effect, srcPad, fxPad),
            _ => BuildZoomFilters(effect, srcPad, fxPad),
        };
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

    private static string BuildCropFilters(VideoEffect effect, string srcPad, string fxPad)
    {
        // Same keep-region as zoom, then pad back to the original frame size (letterbox/pillarbox).
        if (effect.Box is { } box)
        {
            var end = effect.BoxEnd ?? box;
            var w = F(box.Width);
            var h = F(box.Height);
            var xExpr = "max(0,min(iw-ow,iw*" + Unit(effect.StartTime, effect.EndTime, box.X, end.X) + "))";
            var yExpr = "max(0,min(ih-oh,ih*" + Unit(effect.StartTime, effect.EndTime, box.Y, end.Y) + "))";
            return string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]crop=iw*{1}:ih*{2}:x='{3}':y='{4}',pad=2*trunc(iw/(2*{1})):2*trunc(ih/(2*{2})):2*trunc((ow-iw)/4):2*trunc((oh-ih)/4):black[{5}];",
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
            "[{0}]crop=iw/{1}:ih/{1}:x='max(0,min(iw-ow,iw*{2}-ow/2))':y='max(0,min(ih-oh,ih*{3}-oh/2))',pad=2*trunc(iw*{1}/2):2*trunc(ih*{1}/2):2*trunc((ow-iw)/4):2*trunc((oh-ih)/4):black[{4}];",
            srcPad,
            scale,
            cx,
            cy,
            fxPad);
    }

    private static string BuildCoverFilters(VideoEffect effect, string srcPad, string fxPad)
    {
        const string fill = "drawbox=0:0:iw:ih:black:t=fill";
        if (effect.Kind != VideoEffectKind.Blank && effect.Box is { } box)
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
                fill,
                fxPad);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]{1}[{2}];",
            srcPad,
            fill,
            fxPad);
    }

    private static string BuildPixelateFilters(VideoEffect effect, string srcPad, string fxPad)
    {
        var size = F(Math.Clamp(effect.BlockSize, 2, 128));
        var pix = "pixelize=w=" + size + ":h=" + size + ":mode=avg";
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
                pix,
                fxPad);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]{1}[{2}];",
            srcPad,
            pix,
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
        if (IsRegionalOverlay(effect) && effect.Box is { } box)
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

    private static bool IsRegionalOverlay(VideoEffect effect)
        => effect.Box is not null
           && effect.Kind is VideoEffectKind.Blur or VideoEffectKind.Cover or VideoEffectKind.Pixelate;

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
}

#endif
