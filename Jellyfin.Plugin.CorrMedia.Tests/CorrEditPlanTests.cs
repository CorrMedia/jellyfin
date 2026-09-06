using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrEditPlanTests
{
    [Fact]
    public void NeedsEditGraph_MuteOnly_IsFalse()
    {
        var plan = new CorrEditPlan([new MuteTimeRange(1, 2)], [], []);
        Assert.False(plan.HasCuts);
        Assert.False(plan.HasVideoEffects);
        Assert.False(plan.HasSelectiveMute);
        Assert.False(plan.NeedsEditGraph);
    }

    [Fact]
    public void NeedsEditGraph_SkipOrEffectOrSelectiveMute_IsTrue()
    {
        Assert.True(new CorrEditPlan([], [new MuteTimeRange(1, 2)], []).NeedsEditGraph);
        Assert.True(new CorrEditPlan(
            [],
            [],
            [new VideoEffect(VideoEffectKind.Blank, 1, 2, 1, 0.5, 0.5, 0, null)]).NeedsEditGraph);
        Assert.True(new CorrEditPlan(
            [new MuteTimeRange(1, 2, ["FC"])],
            [],
            []).NeedsEditGraph);
    }

    [Fact]
    public void MuteTimeRange_NormalizesChannels()
    {
        var range = new MuteTimeRange(0, 1, [" fc ", "FC", "fl"]);
        Assert.True(range.IsSelectiveMute);
        Assert.Equal(["FC", "FL"], range.NormalizedChannels);
        Assert.False(new MuteTimeRange(0, 1).IsSelectiveMute);
        Assert.Empty(new MuteTimeRange(0, 1).NormalizedChannels);
    }
}
