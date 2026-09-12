#if PATCHED_CORE

using MediaBrowser.Controller.MediaEncoding;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class SessionEditGraphHwBridgeTests
{
    [Theory]
    [InlineData(null, "format=yuv420p")]
    [InlineData("libx264", "format=yuv420p")]
    [InlineData("h264_nvenc", "format=yuv420p")]
    [InlineData("h264_amf", "format=yuv420p")]
    [InlineData("h264_videotoolbox", "format=yuv420p")]
    [InlineData("h264_v4l2m2m", "format=yuv420p")]
    [InlineData("h264_qsv", "format=nv12,hwupload=extra_hw_frames=64")]
    [InlineData("h264_vaapi", "format=nv12,hwupload")]
    [InlineData("hevc_rkmpp", "format=nv12,hwupload")]
    public void GetVideoUploadFilters_MatchesEncoder(string? encoder, string expected)
    {
        Assert.Equal(expected, SessionEditGraphHwBridge.GetVideoUploadFilters(encoder));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" -hwaccel cuda -threads 1", "")]
    [InlineData(" -hwaccel cuda -hwaccel_output_format cuda -threads 1", "hwdownload,format=nv12")]
    [InlineData(" -hwaccel qsv -hwaccel_output_format qsv", "hwdownload,format=nv12")]
    [InlineData(" -hwaccel vaapi -hwaccel_output_format vaapi", "hwdownload,format=nv12")]
    [InlineData(" -hwaccel d3d11va -hwaccel_output_format d3d11", "hwdownload,format=nv12")]
    [InlineData(" -hwaccel videotoolbox -hwaccel_output_format videotoolbox_vld", "hwdownload,format=nv12")]
    [InlineData(" -hwaccel rkmpp -hwaccel_output_format drm_prime", "hwdownload,format=nv12")]
    public void GetVideoDownloadFilters_MatchesDecoder(string? decoder, string expected)
    {
        Assert.Equal(expected, SessionEditGraphHwBridge.GetVideoDownloadFilters(decoder));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" -hwaccel cuda -threads 1", true)]
    [InlineData(" -hwaccel cuda -hwaccel_output_format cuda", true)]
    [InlineData(" -hwaccel qsv -hwaccel_output_format qsv", true)]
    [InlineData(" -hwaccel vaapi -hwaccel_output_format mystery", false)]
    public void CanUseHardwareDecoderForEditGraph_GatesUnknownSurfaces(string? decoder, bool expected)
    {
        Assert.Equal(expected, SessionEditGraphHwBridge.CanUseHardwareDecoderForEditGraph(decoder));
    }

    [Fact]
    public void AppendEncoderBridge_RemapsVideoPad()
    {
        var (body, pad) = SessionEditGraphHwBridge.AppendEncoderBridge(
            "[0:v]null[vout];[0:a]anull[aout]",
            "vout",
            "h264_nvenc");

        Assert.Equal("vhw", pad);
        Assert.Contains("[vout]format=yuv420p[vhw]", body, StringComparison.Ordinal);
        Assert.Contains("[0:a]anull[aout]", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PrependDecoderBridge_RetargetsVideoInputOnly()
    {
        var body = SessionEditGraphHwBridge.PrependDecoderBridge(
            "[0:v]split=2[vx0b][vx0s];[0:a]anull[aout];[10:v]null[ignored]",
            " -hwaccel cuda -hwaccel_output_format cuda -threads 1");

        Assert.StartsWith("[0:v]hwdownload,format=nv12[vin];", body, StringComparison.Ordinal);
        Assert.Contains("[vin]split=2[vx0b][vx0s]", body, StringComparison.Ordinal);
        Assert.Contains("[0:a]anull[aout]", body, StringComparison.Ordinal);
        Assert.Contains("[10:v]null[ignored]", body, StringComparison.Ordinal);
        Assert.DoesNotContain("[0:v]split", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PrependDecoderBridge_NoOpWithoutHwSurface()
    {
        const string graph = "[0:v]null[vout];[0:a]anull[aout]";
        var body = SessionEditGraphHwBridge.PrependDecoderBridge(graph, " -hwaccel cuda -threads 1");
        Assert.Equal(graph, body);
    }

    [Fact]
    public void DownloadThenEncode_ComposeInOrder()
    {
        var withDownload = SessionEditGraphHwBridge.PrependDecoderBridge(
            "[0:v]null[vout];[0:a]anull[aout]",
            " -hwaccel cuda -hwaccel_output_format cuda");
        var (body, pad) = SessionEditGraphHwBridge.AppendEncoderBridge(withDownload, "vout", "h264_nvenc");

        Assert.Equal("vhw", pad);
        Assert.StartsWith("[0:v]hwdownload,format=nv12[vin];", body, StringComparison.Ordinal);
        Assert.Contains("[vin]null[vout]", body, StringComparison.Ordinal);
        Assert.Contains("[vout]format=yuv420p[vhw]", body, StringComparison.Ordinal);
        Assert.Contains("[0:a]anull[aout]", body, StringComparison.Ordinal);
    }
}

#endif
