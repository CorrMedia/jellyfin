using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrCueRemapperTests
{
    private static IReadOnlyList<(double Start, double End)> MiddleSkipKeep()
        => CorrTimeline.BuildKeepRanges([new MuteTimeRange(10, 20)], 60);

    [Fact]
    public void TryMapOriginalToEdited_BeforeSkip_Unchanged()
    {
        Assert.True(CorrTimeline.TryMapOriginalToEdited(5, MiddleSkipKeep(), out var edited));
        Assert.Equal(5, edited, 3);
    }

    [Fact]
    public void TryMapOriginalToEdited_InsideSkip_ReturnsFalse()
    {
        Assert.False(CorrTimeline.TryMapOriginalToEdited(15, MiddleSkipKeep(), out _));
    }

    [Fact]
    public void TryMapOriginalToEdited_AfterSkip_ShiftsBySkippedDuration()
    {
        Assert.True(CorrTimeline.TryMapOriginalToEdited(25, MiddleSkipKeep(), out var edited));
        Assert.Equal(15, edited, 3);
    }

    [Fact]
    public void TryMapOriginalToEdited_AtKeepBoundaries()
    {
        var keep = MiddleSkipKeep();
        Assert.True(CorrTimeline.TryMapOriginalToEdited(10, keep, out var atCut));
        Assert.Equal(10, atCut, 3);
        Assert.True(CorrTimeline.TryMapOriginalToEdited(20, keep, out var afterCut));
        Assert.Equal(10, afterCut, 3);
    }

    [Fact]
    public void TryMapEditedToOriginal_RoundTripsKeepPoints()
    {
        var keep = MiddleSkipKeep();
        Assert.True(CorrTimeline.TryMapEditedToOriginal(5, keep, out var originalBefore));
        Assert.Equal(5, originalBefore, 3);
        Assert.True(CorrTimeline.TryMapEditedToOriginal(15, keep, out var originalAfter));
        Assert.Equal(25, originalAfter, 3);
    }

    [Fact]
    public void Remap_DropsCuesInsideSkip()
    {
        var remapped = CorrCueRemapper.Remap(
            [new CorrCue(12, 18, "skip me")],
            MiddleSkipKeep());
        Assert.Empty(remapped);
    }

    [Fact]
    public void Remap_ShiftsCuesAfterSkip()
    {
        var remapped = CorrCueRemapper.Remap(
            [new CorrCue(25, 30, "after")],
            MiddleSkipKeep());
        Assert.Single(remapped);
        Assert.Equal(15, remapped[0].Start, 3);
        Assert.Equal(20, remapped[0].End, 3);
        Assert.Equal("after", remapped[0].Text);
    }

    [Fact]
    public void Remap_SplitsCueThatStraddlesCut()
    {
        var remapped = CorrCueRemapper.Remap(
            [new CorrCue(8, 22, "straddle", "c1")],
            MiddleSkipKeep());
        Assert.Equal(2, remapped.Count);
        Assert.Equal(8, remapped[0].Start, 3);
        Assert.Equal(10, remapped[0].End, 3);
        Assert.Equal("c1", remapped[0].Id);
        Assert.Equal(10, remapped[1].Start, 3);
        Assert.Equal(12, remapped[1].End, 3);
        Assert.Equal("c1#2", remapped[1].Id);
        Assert.Equal("straddle", remapped[1].Text);
    }

    [Fact]
    public void Remap_PreservesVttSettingsOnSplit()
    {
        var remapped = CorrCueRemapper.Remap(
            [new CorrCue(8, 22, "hi", "c1", "align:start")],
            MiddleSkipKeep());
        Assert.Equal("align:start", remapped[0].Settings);
        Assert.Equal("align:start", remapped[1].Settings);
    }

    [Fact]
    public void Window_MatchesJellyfinFilterEvents()
    {
        var cues = new CorrCue[]
        {
            new(5, 9, "before"),
            new(12, 18, "inside"),
            new(25, 30, "after")
        };
        var windowed = CorrCueRemapper.Window(cues, 10, 20, copyTimestamps: true);
        Assert.Single(windowed);
        Assert.Equal("inside", windowed[0].Text);
    }

    [Fact]
    public void Window_ShiftsWhenNotCopyingTimestamps()
    {
        var windowed = CorrCueRemapper.Window(
            [new CorrCue(12, 18, "inside")],
            10,
            0,
            copyTimestamps: false);
        Assert.Single(windowed);
        Assert.Equal(2, windowed[0].Start, 3);
        Assert.Equal(8, windowed[0].End, 3);
    }
}
