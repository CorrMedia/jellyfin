#if PATCHED_CORE

using System.Text;
using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrFilterComplexBuilderTests
{
    [Fact]
    public void Build_NoGraphNeeded_ReturnsNull()
    {
        var plan = new CorrEditPlan([new MuteTimeRange(1, 2)], [], []);
        Assert.Null(CorrFilterComplexBuilder.Build(plan, 60));
    }

    [Fact]
    public void Build_SkipOnly_TrimsThenConcats()
    {
        var plan = new CorrEditPlan([], [new MuteTimeRange(10, 20)], [], 60);
        var graph = CorrFilterComplexBuilder.Build(plan, 60);
        Assert.NotNull(graph);
        Assert.Equal("vmark", graph.VideoMapLabel);
        Assert.Equal("aout", graph.AudioMapLabel);
        Assert.Contains("[0:v]null[vout]", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("concat=n=2:v=1:a=1[vout][aout]", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("[vout]drawtext=text='Edited'", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("enable='between(t,0,4)'", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("[0:a]anull[amuted]", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("trim=start=0:end=10", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("atrim=start=0:end=10", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("trim=start=20", graph.FilterComplex, StringComparison.Ordinal);
        Assert.DoesNotContain("end=60", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Equal(0, graph.InputSeekSeconds);
        var concatAt = graph.FilterComplex.IndexOf("concat=n=2:v=1:a=1[vout][aout]", StringComparison.Ordinal);
        var badgeAt = graph.FilterComplex.IndexOf("[vout]drawtext=text='Edited'", StringComparison.Ordinal);
        Assert.True(concatAt >= 0 && badgeAt > concatAt);
    }

    [Fact]
    public void Build_EditedSeek_SetsInputSeekAndTruncatesTrim()
    {
        // Skip 15-20 on a 90s source: edited 42 → original 47.
        // Demuxer -ss resets filter t≈0, so trims are shifted (47→0).
        var plan = new CorrEditPlan([], [new MuteTimeRange(15, 20)], [], 90);
        var graph = CorrFilterComplexBuilder.Build(plan, 90, editedStartSeconds: 42);
        Assert.NotNull(graph);
        Assert.Equal(47, graph.InputSeekSeconds, 3);
        Assert.Contains("trim=start=0", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("atrim=start=0", graph.FilterComplex, StringComparison.Ordinal);
        Assert.DoesNotContain("trim=start=47", graph.FilterComplex, StringComparison.Ordinal);
        Assert.DoesNotContain("trim=start=0:end=15", graph.FilterComplex, StringComparison.Ordinal);
        Assert.DoesNotContain("drawtext=", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Equal("vout", graph.VideoMapLabel);
    }

    [Fact]
    public void Build_MuteOnlySeek_SetsInputSeekWithoutTrimConcat()
    {
        var plan = new CorrEditPlan(
            [new MuteTimeRange(50, 55, ["FC"])],
            [],
            [])
        {
            SourceChannelLayout = "5.1",
            SourceChannelCount = 6
        };
        var graph = CorrFilterComplexBuilder.Build(
            plan,
            90,
            editedStartSeconds: 42,
            inputChannelLayout: "5.1",
            inputChannelCount: 6);
        Assert.NotNull(graph);
        Assert.Equal(42, graph.InputSeekSeconds, 3);
        Assert.Contains("between(t,8,13)", graph.FilterComplex, StringComparison.Ordinal);
        Assert.DoesNotContain("concat=", graph.FilterComplex, StringComparison.Ordinal);
        Assert.DoesNotContain("trim=", graph.FilterComplex, StringComparison.Ordinal);
        Assert.DoesNotContain("atrim=", graph.FilterComplex, StringComparison.Ordinal);
        Assert.DoesNotContain("drawtext=", graph.FilterComplex, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EditedSeek_ShiftsMuteAndEffectsOntoPostSeekClock()
    {
        var plan = new CorrEditPlan(
            [new MuteTimeRange(50, 55)],
            [new MuteTimeRange(15, 20)],
            [new VideoEffect(VideoEffectKind.Blur, 51, 56, 1, 0.5, 0.5, 8, null)],
            90);
        var graph = CorrFilterComplexBuilder.Build(plan, 90, editedStartSeconds: 42);
        Assert.NotNull(graph);
        Assert.Equal(47, graph.InputSeekSeconds, 3);
        Assert.Contains("between(t,3,8)", graph.FilterComplex, StringComparison.Ordinal); // mute 50-55 → 3-8
        Assert.Contains("between(t,4,9)", graph.FilterComplex, StringComparison.Ordinal); // blur 51-56 → 4-9
        Assert.DoesNotContain("drawtext=", graph.FilterComplex, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_MuteThenCut_AppliesVolumeBeforeConcat()
    {
        var plan = new CorrEditPlan(
            [new MuteTimeRange(0, 5)],
            [new MuteTimeRange(10, 20)],
            [],
            60);
        var graph = CorrFilterComplexBuilder.Build(plan, 60);
        Assert.NotNull(graph);
        Assert.Contains("volume=0:enable='between(t,0,5)'", graph.FilterComplex, StringComparison.Ordinal);
        var volumeAt = graph.FilterComplex.IndexOf("volume=", StringComparison.Ordinal);
        var concatAt = graph.FilterComplex.IndexOf("concat=", StringComparison.Ordinal);
        Assert.True(volumeAt >= 0 && concatAt > volumeAt);
    }

    [Fact]
    public void Build_VideoEffects_StayOnOriginalTimeline()
    {
        var zoom = new VideoEffect(VideoEffectKind.Zoom, 1, 2, 2, 0.3, 0.4, 0, null, CenterXEnd: 0.7);
        var crop = new VideoEffect(VideoEffectKind.Crop, 3, 4, 2, 0.5, 0.45, 0, null);
        var blur = new VideoEffect(VideoEffectKind.Blur, 5, 6, 1, 0.5, 0.5, 12, new NormalizedBox(0.1, 0.2, 0.3, 0.4));
        var cover = new VideoEffect(VideoEffectKind.Cover, 7, 8, 1, 0.5, 0.5, 0, new NormalizedBox(0.2, 0.2, 0.2, 0.2));
        var pixel = new VideoEffect(VideoEffectKind.Pixelate, 9, 10, 1, 0.5, 0.5, 0, null, BlockSize: 24);
        var blank = new VideoEffect(VideoEffectKind.Blank, 11, 12, 1, 0.5, 0.5, 0, null);
        var plan = new CorrEditPlan([], [], [zoom, crop, blur, cover, pixel, blank], 30);
        var graph = CorrFilterComplexBuilder.Build(plan, 30);
        Assert.NotNull(graph);
        Assert.DoesNotContain("concat=", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("overlay=0:0:enable='between(t,1,2)'", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("pad=2*trunc", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("boxblur=", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("drawbox=0:0:iw:ih:black:t=fill", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("pixelize=w=24:h=24:mode=avg", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("[vx5]drawtext=text='Edited'", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Equal("vmark", graph.VideoMapLabel);
    }

    [Fact]
    public void Build_SelectiveMute_UsesChannelsplitJoin()
    {
        var plan = new CorrEditPlan(
            [new MuteTimeRange(30, 35, ["FC"])],
            [],
            [])
        {
            SourceChannelLayout = "5.1",
            SourceChannelCount = 6
        };
        var graph = CorrFilterComplexBuilder.Build(plan, 60, inputChannelLayout: "5.1", inputChannelCount: 6);
        Assert.NotNull(graph);
        Assert.Contains("channelsplit=channel_layout=5.1", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("[FC]volume=0:enable='between(t,30,35)'[FCm]", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("join=inputs=6:channel_layout=5.1[amuted]", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("[FL][FR][FCm][LFE][BL][BR]join=", graph.FilterComplex, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_StereoDownmix_AfterSourceLayoutMute()
    {
        var plan = new CorrEditPlan([], [new MuteTimeRange(10, 20)], [], 60);
        var graph = CorrFilterComplexBuilder.Build(
            plan,
            60,
            inputChannelLayout: "5.1",
            inputChannelCount: 6,
            outputAudioChannels: 2,
            stereoDownmixFilter: "aformat=channel_layouts=stereo");
        Assert.NotNull(graph);
        Assert.Contains("[amuted]aformat=channel_layouts=stereo[adown]", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("[adown]asplit=", graph.FilterComplex, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_InternalPgs_OverlaysBeforeConcat()
    {
        var plan = new CorrEditPlan([], [new MuteTimeRange(15, 20)], [], 90);
        var graph = CorrFilterComplexBuilder.Build(
            plan,
            90,
            burnInGraphicalSubtitleStreamIndex: 2,
            burnInGraphicalSubtitleFilters: "scale=1920:1080:fast_bilinear,format=yuva420p");
        Assert.NotNull(graph);
        Assert.Contains("[0:2]scale=1920:1080:fast_bilinear,format=yuva420p[psub]", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("[vout][psub]overlay=eof_action=pass:repeatlast=0[vburn]", graph.FilterComplex, StringComparison.Ordinal);
        var overlayAt = graph.FilterComplex.IndexOf("overlay=eof_action=pass", StringComparison.Ordinal);
        var splitAt = graph.FilterComplex.IndexOf("[vburn]split=", StringComparison.Ordinal);
        Assert.True(overlayAt >= 0 && splitAt > overlayAt);
        Assert.Contains("concat=n=2:v=1:a=1[vout][aout]", graph.FilterComplex, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_InternalPgs_AfterVideoEffects()
    {
        var zoom = new VideoEffect(VideoEffectKind.Zoom, 1, 2, 2, 0.3, 0.4, 0, null);
        var plan = new CorrEditPlan([], [], [zoom], 30);
        var graph = CorrFilterComplexBuilder.Build(plan, 30, burnInGraphicalSubtitleStreamIndex: 3);
        Assert.NotNull(graph);
        var effectAt = graph.FilterComplex.IndexOf("overlay=0:0:enable=", StringComparison.Ordinal);
        var pgsAt = graph.FilterComplex.IndexOf("[0:3]format=yuva420p[psub]", StringComparison.Ordinal);
        Assert.True(effectAt >= 0 && pgsAt > effectAt);
        Assert.Contains("[vburn]drawtext=text='Edited'", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Equal("vmark", graph.VideoMapLabel);
    }

    [Fact]
    public void Build_ExternalPgs_UsesSecondInput()
    {
        var plan = new CorrEditPlan([], [new MuteTimeRange(15, 20)], [], 90);
        var graph = CorrFilterComplexBuilder.Build(
            plan,
            90,
            burnInGraphicalSubtitleInputIndex: 1,
            burnInGraphicalSubtitleStreamIndex: 0,
            burnInGraphicalSubtitleFilters: "format=yuva420p");
        Assert.NotNull(graph);
        Assert.Contains("[1:0]format=yuva420p[psub]", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Contains("[vout][psub]overlay=eof_action=pass:repeatlast=0[vburn]", graph.FilterComplex, StringComparison.Ordinal);
        var overlayAt = graph.FilterComplex.IndexOf("overlay=eof_action=pass", StringComparison.Ordinal);
        var splitAt = graph.FilterComplex.IndexOf("[vburn]split=", StringComparison.Ordinal);
        Assert.True(overlayAt >= 0 && splitAt > overlayAt);
    }

    [Fact]
    public void Build_TextAss_BurnsInBeforeConcat()
    {
        var plan = new CorrEditPlan([], [new MuteTimeRange(15, 20)], [], 90);
        var graph = CorrFilterComplexBuilder.Build(
            plan,
            90,
            burnInTextSubtitleFilter: "subtitles=f='C\\:/tmp/test.ass':fontsdir='C\\:/tmp/fonts'");
        Assert.NotNull(graph);
        Assert.Contains("[vout]subtitles=f='C\\:/tmp/test.ass':fontsdir='C\\:/tmp/fonts'[vburn]", graph.FilterComplex, StringComparison.Ordinal);
        var burnAt = graph.FilterComplex.IndexOf("[vout]subtitles=", StringComparison.Ordinal);
        var splitAt = graph.FilterComplex.IndexOf("[vburn]split=", StringComparison.Ordinal);
        Assert.True(burnAt >= 0 && splitAt > burnAt);
    }

    [Fact]
    public void Build_TextAss_AfterVideoEffects()
    {
        var zoom = new VideoEffect(VideoEffectKind.Zoom, 1, 2, 2, 0.3, 0.4, 0, null);
        var plan = new CorrEditPlan([], [], [zoom], 30);
        var graph = CorrFilterComplexBuilder.Build(
            plan,
            30,
            burnInTextSubtitleFilter: "subtitles=f='movie.ass'");
        Assert.NotNull(graph);
        var effectAt = graph.FilterComplex.IndexOf("overlay=0:0:enable=", StringComparison.Ordinal);
        var textAt = graph.FilterComplex.IndexOf("[vx0]subtitles=f='movie.ass'[vburn]", StringComparison.Ordinal);
        Assert.True(effectAt >= 0 && textAt > effectAt);
        Assert.Contains("[vburn]drawtext=text='Edited'", graph.FilterComplex, StringComparison.Ordinal);
        Assert.Equal("vmark", graph.VideoMapLabel);
    }

    [Fact]
    public void AppendTextSubtitleBurnIn_WithInputSeek_RestoresAbsolutePts()
    {
        var sb = new StringBuilder("[0:v]null[vout];");
        var pad = CorrFilterComplexBuilder.AppendTextSubtitleBurnIn(
            sb,
            "vout",
            "subtitles=f='movie.ass'",
            inputSeekSeconds: 47);
        Assert.Equal("vburn", pad);
        Assert.Contains("[vout]setpts=PTS+47/TB[vburnabs]", sb.ToString(), StringComparison.Ordinal);
        Assert.Contains("[vburnabs]subtitles=f='movie.ass'[vburnsub]", sb.ToString(), StringComparison.Ordinal);
        Assert.Contains("[vburnsub]setpts=PTS-STARTPTS[vburn]", sb.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AppendTextSubtitleBurnIn_RejectsGraphBreakingFilter()
    {
        var sb = new StringBuilder("[0:v]null[vout];");
        var pad = CorrFilterComplexBuilder.AppendTextSubtitleBurnIn(sb, "vout", "subtitles=f='x';[malicious]");
        Assert.Equal("vout", pad);
        Assert.Equal("[0:v]null[vout];", sb.ToString());
    }

    [Fact]
    public void AppendEditedBadge_FromStart_DrawsCornerText()
    {
        var sb = new StringBuilder("[0:v]null[vout];");
        var pad = CorrFilterComplexBuilder.AppendEditedBadge(sb, "vout", fromEditedStart: true);
        Assert.Equal("vmark", pad);
        Assert.Contains("[vout]drawtext=text='Edited'", sb.ToString(), StringComparison.Ordinal);
        Assert.Contains("enable='between(t,0,4)'", sb.ToString(), StringComparison.Ordinal);
        Assert.Contains("x=w-tw-w*0.03:y=h*0.04", sb.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AppendEditedBadge_AfterSeek_IsNoOp()
    {
        var sb = new StringBuilder("[0:v]null[vout];");
        var pad = CorrFilterComplexBuilder.AppendEditedBadge(sb, "vout", fromEditedStart: false);
        Assert.Equal("vout", pad);
        Assert.Equal("[0:v]null[vout];", sb.ToString());
    }

    [Fact]
    public void AppendGraphicalSubtitleBurnIn_NullIndex_IsNoOp()
    {
        var sb = new StringBuilder("[0:v]null[vout];");
        var pad = CorrFilterComplexBuilder.AppendGraphicalSubtitleBurnIn(sb, "vout", null, "format=yuva420p");
        Assert.Equal("vout", pad);
        Assert.Equal("[0:v]null[vout];", sb.ToString());
    }

    [Fact]
    public void AppendAudioEdits_NoMutes_EmitsAnull()
    {
        var sb = new StringBuilder();
        var pad = CorrFilterComplexBuilder.AppendAudioEdits(sb, [], null, 0, 0, null);
        Assert.Equal("amuted", pad);
        Assert.Equal("[0:a]anull[amuted];", sb.ToString());
    }
}

#endif
