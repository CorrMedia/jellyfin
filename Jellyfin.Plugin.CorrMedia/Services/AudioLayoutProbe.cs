using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Probes real audio channel layout from a media file (avoids stale library metadata).
/// </summary>
internal static class AudioLayoutProbe
{
    private static readonly string[] FfprobeCandidates =
    [
        "/usr/lib/jellyfin-ffmpeg/ffprobe",
        "/usr/lib/jellyfin-ffmpeg/ffprobe.exe",
        "ffprobe",
        "ffprobe.exe",
    ];

    /// <summary>
    /// Reads channel count and layout for the first audio stream.
    /// </summary>
    /// <param name="mediaPath">Media file path.</param>
    /// <returns>Layout name and channel count, or nulls when probe fails.</returns>
    public static (string? Layout, int Channels) TryProbe(string mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
        {
            return (null, 0);
        }

        var ffprobe = ResolveFfprobe();
        if (ffprobe is null)
        {
            return (null, 0);
        }

        try
        {
            using var process = new Process();
            process.StartInfo.FileName = ffprobe;
            process.StartInfo.ArgumentList.Add("-v");
            process.StartInfo.ArgumentList.Add("error");
            process.StartInfo.ArgumentList.Add("-select_streams");
            process.StartInfo.ArgumentList.Add("a:0");
            process.StartInfo.ArgumentList.Add("-show_entries");
            process.StartInfo.ArgumentList.Add("stream=channels,channel_layout");
            process.StartInfo.ArgumentList.Add("-of");
            process.StartInfo.ArgumentList.Add("csv=p=0");
            process.StartInfo.ArgumentList.Add(mediaPath);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var stdout = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(8000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
                catch (NotSupportedException)
                {
                }

                return (null, 0);
            }

            if (process.ExitCode != 0)
            {
                return (null, 0);
            }

            var lines = stdout.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0)
            {
                return (null, 0);
            }

            var parts = lines[0].Split(',', StringSplitOptions.TrimEntries);
            var channels = 0;
            string? layout = null;
            if (parts.Length > 0 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                channels = parsed;
            }

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                layout = parts[1];
            }

            if (string.IsNullOrWhiteSpace(layout) && channels > 0)
            {
                layout = ChannelLayoutHelper.ResolveLayoutName(null, channels);
            }

            return (layout, channels);
        }
        catch (Exception)
        {
            return (null, 0);
        }
    }

    private static string? ResolveFfprobe()
    {
        foreach (var candidate in FfprobeCandidates)
        {
            if (candidate is "ffprobe" or "ffprobe.exe")
            {
                return candidate;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
