using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrTimelineTests
{
    [Fact]
    public void BuildKeepRanges_ComplementOfMiddleSkip()
    {
        var keep = CorrTimeline.BuildKeepRanges([new MuteTimeRange(10, 20)], 60);
        Assert.Equal([(0d, 10d), (20d, 60d)], keep);
    }

    [Fact]
    public void BuildKeepRanges_MergesOverlappingAndOutOfOrderSkips()
    {
        var skips = new MuteTimeRange[]
        {
            new(25, 40),
            new(10, 20),
            new(18, 30)
        };
        var keep = CorrTimeline.BuildKeepRanges(skips, 50);
        Assert.Equal([(0d, 10d), (40d, 50d)], keep);
    }

    [Fact]
    public void BuildKeepRanges_SkipAtStartAndEnd()
    {
        var keep = CorrTimeline.BuildKeepRanges(
            [new MuteTimeRange(0, 5), new MuteTimeRange(55, 60)],
            60);
        Assert.Equal([(5d, 55d)], keep);
    }

    [Fact]
    public void BuildKeepRanges_IgnoresInvertedAndClipsPastDuration()
    {
        var keep = CorrTimeline.BuildKeepRanges(
            [new MuteTimeRange(9, 8), new MuteTimeRange(80, 90)],
            60);
        Assert.Equal([(0d, 60d)], keep);
    }

    [Fact]
    public void TruncateKeepRanges_SeeksIntoSecondKeepSegment()
    {
        var keep = CorrTimeline.BuildKeepRanges([new MuteTimeRange(10, 20)], 60);
        // Edited timeline: 0-10 is original 0-10, 10-50 is original 20-60.
        var truncated = CorrTimeline.TruncateKeepRangesForEditedStart(keep, 15);
        Assert.Equal([(25d, 60d)], truncated);
    }

    [Fact]
    public void TruncateKeepRanges_ZeroStart_IsUnchanged()
    {
        var keep = CorrTimeline.BuildKeepRanges([new MuteTimeRange(10, 20)], 60);
        Assert.Equal(keep, CorrTimeline.TruncateKeepRangesForEditedStart(keep, 0));
    }

    [Fact]
    public void TailKeepRange_UsesLastFortyMilliseconds()
    {
        var tail = CorrTimeline.TailKeepRange([(0d, 10d), (20d, 60d)]);
        Assert.Equal([(59.96d, 60d)], tail);
    }

    [Fact]
    public void ResolveOriginalDuration_PrefersPlanValue()
    {
        var plan = new CorrEditPlan([], [new MuteTimeRange(10, 20)], [], 90);
        Assert.Equal(90, CorrTimeline.ResolveOriginalDuration(plan, 80));
    }

    [Fact]
    public void MergedSkipSeconds_OverlapsCountOnce()
    {
        Assert.Equal(15, CorrTimeline.MergedSkipSeconds(
            [new MuteTimeRange(10, 20), new MuteTimeRange(15, 25)]));
    }

    [Fact]
    public void ResolveOriginalDuration_ReconstructsFromSkipTotals()
    {
        var plan = new CorrEditPlan([], [new MuteTimeRange(10, 20), new MuteTimeRange(15, 25)], []);
        Assert.Equal(95, CorrTimeline.ResolveOriginalDuration(plan, 80));
    }

    [Fact]
    public void ResolveOriginalDuration_NoCuts_ReturnsMediaSourceDuration()
    {
        var plan = new CorrEditPlan([new MuteTimeRange(1, 2)], [], []);
        Assert.Equal(80, CorrTimeline.ResolveOriginalDuration(plan, 80));
    }
}
