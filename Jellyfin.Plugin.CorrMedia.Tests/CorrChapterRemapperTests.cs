using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrChapterRemapperTests
{
    private static IReadOnlyList<(double Start, double End)> MiddleSkipKeep()
        => CorrTimeline.BuildKeepRanges([new MuteTimeRange(10, 20)], 60);

    private static ChapterInfo Ch(double seconds, string name)
        => new() { StartPositionTicks = (long)(seconds * TimeSpan.TicksPerSecond), Name = name };

    [Fact]
    public void Remap_DropsMarkerInsideSkip()
    {
        var remapped = CorrChapterRemapper.Remap([Ch(15, "inside")], MiddleSkipKeep());
        Assert.Empty(remapped);
    }

    [Fact]
    public void Remap_ShiftsMarkerAfterSkip()
    {
        var remapped = CorrChapterRemapper.Remap([Ch(25, "after")], MiddleSkipKeep());
        Assert.Single(remapped);
        Assert.Equal(15 * TimeSpan.TicksPerSecond, remapped[0].StartPositionTicks);
        Assert.Equal("after", remapped[0].Name);
    }

    [Fact]
    public void Remap_LeavesMarkerBeforeSkipUnchanged()
    {
        var remapped = CorrChapterRemapper.Remap([Ch(5, "before")], MiddleSkipKeep());
        Assert.Single(remapped);
        Assert.Equal(5 * TimeSpan.TicksPerSecond, remapped[0].StartPositionTicks);
        Assert.Equal("before", remapped[0].Name);
    }

    [Fact]
    public void Remap_KeepBoundariesCollapseToSameEditedTick_KeepsFirst()
    {
        var remapped = CorrChapterRemapper.Remap(
            [Ch(10, "at-cut"), Ch(20, "after-cut")],
            MiddleSkipKeep());
        Assert.Single(remapped);
        Assert.Equal(10 * TimeSpan.TicksPerSecond, remapped[0].StartPositionTicks);
        Assert.Equal("at-cut", remapped[0].Name);
    }

    [Fact]
    public void Remap_PreservesImageFields()
    {
        var original = new ChapterInfo
        {
            StartPositionTicks = 25 * TimeSpan.TicksPerSecond,
            Name = "Scene",
            ImagePath = "/img/ch.jpg",
            ImageTag = "abc",
            ImageDateModified = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        var remapped = CorrChapterRemapper.Remap([original], MiddleSkipKeep());
        Assert.Single(remapped);
        Assert.Equal(15 * TimeSpan.TicksPerSecond, remapped[0].StartPositionTicks);
        Assert.Equal("/img/ch.jpg", remapped[0].ImagePath);
        Assert.Equal("abc", remapped[0].ImageTag);
        Assert.Equal(original.ImageDateModified, remapped[0].ImageDateModified);
        Assert.NotSame(original, remapped[0]);
    }

    [Fact]
    public void Remap_EmptyKeep_ReturnsEmpty()
    {
        Assert.Empty(CorrChapterRemapper.Remap([Ch(5, "x")], []));
    }
}
