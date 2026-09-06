using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class AudioEditExpressionsTests
{
    [Fact]
    public void BuildFilter_Empty_IsAnull()
    {
        Assert.Equal("anull", AudioEditExpressions.BuildFilter([]));
    }

    [Fact]
    public void BuildFilter_MuteOnly_UsesVolumeEvalFrame()
    {
        var filter = AudioEditExpressions.BuildFilter([new MuteTimeRange(10, 20)]);
        Assert.StartsWith("volume='", filter, StringComparison.Ordinal);
        Assert.Contains("if(between(t,10,20),0,1)", filter, StringComparison.Ordinal);
        Assert.EndsWith("':eval=frame", filter);
    }

    [Fact]
    public void BuildFilter_VolumeUsesGain()
    {
        var range = new MuteTimeRange(1, 2, Kind: AudioEditKind.Volume, Gain: 0.25);
        var expr = AudioEditExpressions.BuildSourceGainExpression([range]);
        Assert.Equal("if(between(t,1,2),0.25,1)", expr);
    }

    [Fact]
    public void BuildFilter_Beep_UsesAevalAndTone()
    {
        var range = new MuteTimeRange(5, 6, Kind: AudioEditKind.Beep, Gain: 0.3, Frequency: 1000);
        var filter = AudioEditExpressions.BuildFilter([range]);
        Assert.StartsWith("aeval='", filter, StringComparison.Ordinal);
        Assert.Contains("val(ch)*", filter, StringComparison.Ordinal);
        Assert.Contains("0.3*sin(2*PI*1000*t)", filter, StringComparison.Ordinal);
        Assert.Contains("if(between(t,5,6),0,", filter, StringComparison.Ordinal);
        Assert.EndsWith("':c=same", filter);
    }

    [Fact]
    public void BuildSourceGainExpression_BeepWinsOverMuteOverVolume()
    {
        var ranges = new MuteTimeRange[]
        {
            new(0, 10, Kind: AudioEditKind.Volume, Gain: 0.5),
            new(4, 8),
            new(5, 6, Kind: AudioEditKind.Beep, Gain: 0.3, Frequency: 440)
        };
        var expr = AudioEditExpressions.BuildSourceGainExpression(ranges);
        Assert.StartsWith("if(between(t,5,6),0,", expr, StringComparison.Ordinal);
        Assert.Contains("if(between(t,4,8),0,", expr, StringComparison.Ordinal);
        Assert.Contains("if(between(t,0,10),0.5,1)", expr, StringComparison.Ordinal);
    }
}
