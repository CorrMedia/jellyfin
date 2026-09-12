#nullable enable

using System;

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// Wraps the vendor-agnostic plugin filter_complex for hardware surfaces:
/// optional HW-decode <c>hwdownload</c> before the graph, and
/// <c>format</c> / <c>hwupload</c> before <c>GetVideoEncoder</c>.
/// </summary>
public static class SessionEditGraphHwBridge
{
    private static readonly string[] KnownHwOutputFormats =
    [
        "cuda",
        "qsv",
        "vaapi",
        "d3d11",
        "videotoolbox_vld",
        "drm_prime"
    ];

    /// <summary>
    /// Whether a stock hardware decoder string is safe for the CPU edit graph
    /// (known download path, or FFmpeg already copy-backs to memory).
    /// </summary>
    /// <param name="videoDecoder">FFmpeg decoder args from <c>GetHardwareVideoDecoder</c>.</param>
    /// <returns>False when the decoder emits an unknown HW surface.</returns>
    public static bool CanUseHardwareDecoderForEditGraph(string? videoDecoder)
    {
        if (string.IsNullOrEmpty(videoDecoder))
        {
            return true;
        }

        var outputFormat = TryGetHwaccelOutputFormat(videoDecoder);
        if (outputFormat is null)
        {
            // -hwaccel without -hwaccel_output_format: FFmpeg copy-backs to memory.
            return true;
        }

        return IsKnownHwOutputFormat(outputFormat);
    }

    /// <summary>
    /// Filter chain that downloads HW frames into memory for the CPU graph.
    /// Empty when no download is needed.
    /// </summary>
    /// <param name="videoDecoder">FFmpeg decoder args from <c>GetHardwareVideoDecoder</c>.</param>
    /// <returns>Comma-separated filters with no pad labels, or empty.</returns>
    public static string GetVideoDownloadFilters(string? videoDecoder)
    {
        var outputFormat = TryGetHwaccelOutputFormat(videoDecoder);
        if (outputFormat is null || !IsKnownHwOutputFormat(outputFormat))
        {
            return string.Empty;
        }

        // CUDA and other HW surfaces download as nv12; CPU filters accept that.
        // format=yuv420p on hwdownload itself fails ("Invalid output format yuv420p").
        return "hwdownload,format=nv12";
    }

    /// <summary>
    /// Prepends HW→memory download and retargets plugin <c>[0:v]</c> to the new pad.
    /// Leaves <c>[0:a]</c> and labels like <c>[10:v]</c> unchanged.
    /// </summary>
    /// <param name="filterComplex">Plugin <c>-filter_complex</c> body.</param>
    /// <param name="videoDecoder">FFmpeg decoder args.</param>
    /// <returns>Graph body with download prepended when needed.</returns>
    public static string PrependDecoderBridge(string filterComplex, string? videoDecoder)
    {
        ArgumentException.ThrowIfNullOrEmpty(filterComplex);

        var filters = GetVideoDownloadFilters(videoDecoder);
        if (string.IsNullOrEmpty(filters))
        {
            return filterComplex;
        }

        var body = filterComplex.Trim().TrimStart(';');
        var dest = body.Contains("[vin]", StringComparison.Ordinal) ? "vin2" : "vin";
        var rewritten = body.Replace("[0:v]", "[" + dest + "]", StringComparison.Ordinal);
        return $"[0:v]{filters}[{dest}];{rewritten}";
    }

    /// <summary>
    /// Filter chain that converts the graph's memory frames into what <paramref name="videoEncoder"/> accepts.
    /// </summary>
    /// <param name="videoEncoder">FFmpeg encoder name from <c>GetVideoEncoder</c>.</param>
    /// <returns>Comma-separated filters with no pad labels.</returns>
    public static string GetVideoUploadFilters(string? videoEncoder)
    {
        if (string.IsNullOrEmpty(videoEncoder))
        {
            return "format=yuv420p";
        }

        if (videoEncoder.Contains("qsv", StringComparison.OrdinalIgnoreCase))
        {
            return "format=nv12,hwupload=extra_hw_frames=64";
        }

        if (videoEncoder.Contains("vaapi", StringComparison.OrdinalIgnoreCase)
            || videoEncoder.Contains("rkmpp", StringComparison.OrdinalIgnoreCase))
        {
            return "format=nv12,hwupload";
        }

        return "format=yuv420p";
    }

    /// <summary>
    /// Appends the encoder wrap after <paramref name="videoMapLabel"/> and remaps video to a new pad.
    /// </summary>
    /// <param name="filterComplex">Plugin <c>-filter_complex</c> body.</param>
    /// <param name="videoMapLabel">Current video output pad (no brackets).</param>
    /// <param name="videoEncoder">FFmpeg encoder name.</param>
    /// <returns>Updated graph body and video pad name.</returns>
    public static (string FilterComplex, string VideoMapLabel) AppendEncoderBridge(
        string filterComplex,
        string videoMapLabel,
        string? videoEncoder)
    {
        ArgumentException.ThrowIfNullOrEmpty(filterComplex);
        ArgumentException.ThrowIfNullOrEmpty(videoMapLabel);

        var filters = GetVideoUploadFilters(videoEncoder);
        var dest = string.Equals(videoMapLabel, "vhw", StringComparison.Ordinal) ? "vhw2" : "vhw";
        var body = filterComplex.Trim().TrimEnd(';');
        return ($"{body};[{videoMapLabel}]{filters}[{dest}]", dest);
    }

    private static bool IsKnownHwOutputFormat(string outputFormat)
    {
        foreach (var known in KnownHwOutputFormats)
        {
            if (string.Equals(known, outputFormat, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? TryGetHwaccelOutputFormat(string? videoDecoder)
    {
        if (string.IsNullOrEmpty(videoDecoder))
        {
            return null;
        }

        const string marker = "-hwaccel_output_format";
        var index = videoDecoder.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var after = videoDecoder[(index + marker.Length)..].TrimStart();
        if (after.Length == 0)
        {
            return null;
        }

        var end = 0;
        while (end < after.Length && !char.IsWhiteSpace(after[end]))
        {
            end++;
        }

        return end == 0 ? null : after[..end];
    }
}
