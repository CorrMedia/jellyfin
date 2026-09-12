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
    public void BuildFilter_MuteOnly_UsesFlatEnable()
    {
        var filter = AudioEditExpressions.BuildFilter([new MuteTimeRange(10, 20)]);
        Assert.Equal("volume=0:enable='between(t,10,20)'", filter);
        Assert.DoesNotContain("if(between", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFilter_VolumeUsesProduct()
    {
        var range = new MuteTimeRange(1, 2, Kind: AudioEditKind.Volume, Gain: 0.25);
        var filter = AudioEditExpressions.BuildFilter([range]);
        Assert.Equal("volume='(1+(0.25-1)*between(t,1,2))':eval=frame", filter);
        Assert.Equal("(1+(0.25-1)*between(t,1,2))", AudioEditExpressions.BuildSourceGainExpression([range]));
    }

    [Fact]
    public void BuildFilter_MuteAndVolume_ChainsProductThenEnable()
    {
        var ranges = new MuteTimeRange[]
        {
            new(0, 10, Kind: AudioEditKind.Volume, Gain: 0.5),
            new(4, 8)
        };
        var filter = AudioEditExpressions.BuildFilter(ranges);
        Assert.Equal(
            "volume='(1+(0.5-1)*between(t,0,10))':eval=frame,volume=0:enable='between(t,4,8)'",
            filter);
    }

    [Fact]
    public void BuildFilter_Beep_UsesFlatAeval()
    {
        var range = new MuteTimeRange(5, 6, Kind: AudioEditKind.Beep, Gain: 0.3, Frequency: 1000);
        var filter = AudioEditExpressions.BuildFilter([range]);
        Assert.StartsWith("aeval='", filter, StringComparison.Ordinal);
        Assert.Contains("val(ch)*", filter, StringComparison.Ordinal);
        Assert.Contains("0.3*sin(2*PI*1000*t)*between(t,5,6)", filter, StringComparison.Ordinal);
        Assert.Contains("*max(0\\,1-(between(t,5,6)))", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("if(between", filter, StringComparison.Ordinal);
        Assert.EndsWith("':c=same", filter);
    }

    [Fact]
    public void BuildSourceGainExpression_BeepAndMuteZeroSource()
    {
        var ranges = new MuteTimeRange[]
        {
            new(0, 10, Kind: AudioEditKind.Volume, Gain: 0.5),
            new(4, 8),
            new(5, 6, Kind: AudioEditKind.Beep, Gain: 0.3, Frequency: 440)
        };
        var expr = AudioEditExpressions.BuildSourceGainExpression(ranges);
        Assert.StartsWith("(1+(0.5-1)*between(t,0,10))*max(0\\,1-(", expr, StringComparison.Ordinal);
        Assert.Contains("between(t,4,8)", expr, StringComparison.Ordinal);
        Assert.Contains("between(t,5,6)", expr, StringComparison.Ordinal);
        Assert.DoesNotContain("if(between", expr, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFilter_ManyMutes_ChainsChunkedEnables()
    {
        var ranges = Enumerable.Range(0, 80)
            .Select(i => new MuteTimeRange(i * 10, (i * 10) + 1))
            .ToArray();
        var filter = AudioEditExpressions.BuildFilter(ranges);
        Assert.DoesNotContain("if(between", filter, StringComparison.Ordinal);
        Assert.Equal(4, filter.Split("volume=0:enable=", StringSplitOptions.None).Length - 1); // 80/20
        Assert.Contains("between(t,0,1)", filter, StringComparison.Ordinal);
        Assert.Contains("between(t,790,791)", filter, StringComparison.Ordinal);
        Assert.Equal(80, filter.Split("between(t,", StringSplitOptions.None).Length - 1);
    }
}
