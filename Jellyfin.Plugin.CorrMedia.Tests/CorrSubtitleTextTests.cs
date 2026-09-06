using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrSubtitleTextTests
{
    [Fact]
    public void IsRewritableFormat_AcceptsVttAndSrt()
    {
        Assert.True(CorrSubtitleText.IsRewritableFormat("vtt"));
        Assert.True(CorrSubtitleText.IsRewritableFormat("webvtt"));
        Assert.True(CorrSubtitleText.IsRewritableFormat("srt"));
        Assert.True(CorrSubtitleText.IsRewritableFormat("subrip"));
        Assert.True(CorrSubtitleText.IsRewritableFormat("json"));
        Assert.True(CorrSubtitleText.IsRewritableFormat("js"));
        Assert.False(CorrSubtitleText.IsRewritableFormat("pgs"));
        Assert.False(CorrSubtitleText.IsRewritableFormat("ass"));
    }

    [Fact]
    public void ParseAndWrite_RoundTripsWebVttCue()
    {
        const string vtt = """
            WEBVTT

            cue1
            00:00:05.000 --> 00:00:08.500 align:start
            Hello
            world
            """;

        var cues = CorrSubtitleText.Parse(vtt, "vtt");
        Assert.Single(cues);
        Assert.Equal("cue1", cues[0].Id);
        Assert.Equal(5, cues[0].Start, 3);
        Assert.Equal(8.5, cues[0].End, 3);
        Assert.Equal("align:start", cues[0].Settings);
        Assert.Equal("Hello\nworld", cues[0].Text);

        var written = CorrSubtitleText.Write(cues, "vtt");
        var again = CorrSubtitleText.Parse(written, "vtt");
        Assert.Equal(cues[0].Start, again[0].Start, 3);
        Assert.Equal(cues[0].End, again[0].End, 3);
        Assert.Equal(cues[0].Text, again[0].Text);
        Assert.Equal(cues[0].Settings, again[0].Settings);
    }

    [Fact]
    public void ParseAndWrite_RoundTripsSrtCue()
    {
        const string srt = """
            1
            00:00:01,000 --> 00:00:02,250
            Line one
            Line two
            """;

        var cues = CorrSubtitleText.Parse(srt, "srt");
        Assert.Single(cues);
        Assert.Equal(1, cues[0].Start, 3);
        Assert.Equal(2.25, cues[0].End, 3);
        Assert.Equal("Line one\nLine two", cues[0].Text);

        var written = CorrSubtitleText.Write(cues, "srt");
        Assert.Contains("00:00:01,000 --> 00:00:02,250", written, StringComparison.Ordinal);
        Assert.Contains("Line one\nLine two", written, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RemappedVttUsesEditedTimes()
    {
        var keep = CorrTimeline.BuildKeepRanges(
            [new Models.MuteTimeRange(10, 20)],
            60);
        var cues = CorrSubtitleText.Parse(
            """
            WEBVTT

            00:00:25.000 --> 00:00:27.000
            after skip
            """,
            "vtt");
        var remapped = CorrCueRemapper.Remap(cues, keep);
        var written = CorrSubtitleText.Write(remapped, "vtt");
        Assert.Contains("00:00:15.000 --> 00:00:17.000", written, StringComparison.Ordinal);
        Assert.Contains("after skip", written, StringComparison.Ordinal);
    }

    [Fact]
    public void Remap_JsonStreamJs_DropsSkipInteriorAndShiftsAfter()
    {
        var json = """
            {"TrackEvents":[
              {"Id":"inside","Text":"INSIDE SKIP (must not appear)","StartPositionTicks":160000000,"EndPositionTicks":180000000},
              {"Id":"after","Text":"AFTER SKIP (orig 22s = edited 17s)","StartPositionTicks":220000000,"EndPositionTicks":250000000}
            ]}
            """;
        var keep = CorrTimeline.BuildKeepRanges([new Models.MuteTimeRange(15, 20)], 90);
        var remapped = CorrCueRemapper.Remap(CorrSubtitleText.Parse(json, "json"), keep);
        Assert.Single(remapped);
        Assert.Equal("after", remapped[0].Id);
        Assert.Equal(17, remapped[0].Start, 3);
        Assert.Equal(20, remapped[0].End, 3);

        var written = CorrSubtitleText.Write(remapped, "js");
        var again = CorrSubtitleText.Parse(written, "json");
        Assert.Single(again);
        Assert.Equal(17, again[0].Start, 3);
        Assert.DoesNotContain("INSIDE SKIP", written, StringComparison.Ordinal);
    }
}
