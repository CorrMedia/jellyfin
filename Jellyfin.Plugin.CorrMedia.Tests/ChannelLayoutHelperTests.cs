using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class ChannelLayoutHelperTests
{
    [Fact]
    public void GetChannels_Named51Layout()
    {
        Assert.Equal(["FL", "FR", "FC", "LFE", "BL", "BR"], ChannelLayoutHelper.GetChannels("5.1", 6));
    }

    [Fact]
    public void GetChannels_FallsBackToChannelCount()
    {
        Assert.Equal(["FL", "FR"], ChannelLayoutHelper.GetChannels(null, 2));
        Assert.Equal(["FL", "FR", "FC", "LFE", "BL", "BR"], ChannelLayoutHelper.GetChannels("unknown", 6));
        Assert.Empty(ChannelLayoutHelper.GetChannels(null, 0));
    }

    [Fact]
    public void ResolveLayoutName_PrefersExactKey()
    {
        Assert.Equal("5.1(side)", ChannelLayoutHelper.ResolveLayoutName("5.1(side)", 6));
        Assert.Equal("stereo", ChannelLayoutHelper.ResolveLayoutName(null, 2));
        Assert.Null(ChannelLayoutHelper.ResolveLayoutName(null, 12));
    }

    [Fact]
    public void ResolveMuteTargets_NamedMissOnStereo_FallsBackToAll()
    {
        var stereo = ChannelLayoutHelper.GetChannels("stereo", 2);
        Assert.Equal(["FL", "FR"], ChannelLayoutHelper.ResolveMuteTargets(["FC"], stereo));
    }

    [Fact]
    public void ResolveMuteTargets_NamedHitOn51_IsIntersection()
    {
        var layout = ChannelLayoutHelper.GetChannels("5.1", 6);
        Assert.Equal(["FC"], ChannelLayoutHelper.ResolveMuteTargets(["FC"], layout));
        Assert.Equal(["FL", "FR", "FC"], ChannelLayoutHelper.ResolveMuteTargets(["FL", "FR", "FC"], layout));
    }

    [Fact]
    public void ResolveMuteTargets_EmptyRequest_IsAllChannels()
    {
        var layout = ChannelLayoutHelper.GetChannels("stereo", 2);
        Assert.Equal(["FL", "FR"], ChannelLayoutHelper.ResolveMuteTargets([], layout));
    }
}
