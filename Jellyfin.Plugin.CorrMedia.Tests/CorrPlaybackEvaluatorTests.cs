using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrPlaybackEvaluatorTests
{
    [Fact]
    public void EditedRunTimeTicks_NullOriginal_StaysNull()
    {
        Assert.Null(CorrPlaybackEvaluator.EditedRunTimeTicks(null, [new MuteTimeRange(1, 2)]));
    }

    [Fact]
    public void EditedRunTimeTicks_MergesOverlappingSkips()
    {
        var original = TimeSpan.FromSeconds(90).Ticks;
        var edited = CorrPlaybackEvaluator.EditedRunTimeTicks(
            original,
            [new MuteTimeRange(10, 20), new MuteTimeRange(15, 25)]);
        Assert.Equal(TimeSpan.FromSeconds(75).Ticks, edited);
    }

    [Fact]
    public void EditedRunTimeTicks_FloorsToOneSecond()
    {
        var original = TimeSpan.FromSeconds(5).Ticks;
        var edited = CorrPlaybackEvaluator.EditedRunTimeTicks(original, [new MuteTimeRange(0, 5)]);
        Assert.Equal(TimeSpan.TicksPerSecond, edited);
    }

    [Fact]
    public void EditedNameSuffix_IsStable()
    {
        Assert.Equal(" (Edited)", CorrPlaybackEvaluator.EditedNameSuffix);
    }
}
