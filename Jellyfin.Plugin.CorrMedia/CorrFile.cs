using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia;

/// <summary>
/// Sidecar <c>*.corr.json</c> path and parse helpers.
/// </summary>
internal static class CorrFile
{
    private const string SidecarSuffix = ".corr.json";
    private const double DefaultZoomScale = 2.0;
    private const double DefaultBlurRadius = 8.0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Returns the sidecar <c>.corr.json</c> path for a media file.
    /// </summary>
    /// <param name="mediaPath">Media file path.</param>
    /// <returns>Corr sidecar path.</returns>
    public static string GetPath(string mediaPath)
    {
        var basePath = Path.ChangeExtension(mediaPath, null);
        return basePath + SidecarSuffix;
    }

    /// <summary>
    /// Parses applied edits from a corr.json sidecar.
    /// Unknown <c>action</c> values are ignored so the schema can grow.
    /// </summary>
    /// <param name="corrPath">Corr sidecar path.</param>
    /// <returns>Mute, skip, and video-effect lists.</returns>
    public static CorrEdits Parse(string corrPath)
    {
        var mutes = new List<MuteTimeRange>();
        var skips = new List<MuteTimeRange>();
        var videoEffects = new List<VideoEffect>();
        try
        {
            using var stream = File.OpenRead(corrPath);
            var document = JsonSerializer.Deserialize<CorrDocument>(stream, JsonOptions);
            if (document?.Edits is null)
            {
                return CorrEdits.Empty;
            }

            foreach (var edit in document.Edits)
            {
                if (edit is null || edit.End <= edit.Start)
                {
                    continue;
                }

                var action = edit.Action?.Trim();
                if (string.Equals(action, "mute", StringComparison.OrdinalIgnoreCase))
                {
                    mutes.Add(new MuteTimeRange(edit.Start, edit.End));
                }
                else if (string.Equals(action, "skip", StringComparison.OrdinalIgnoreCase))
                {
                    skips.Add(new MuteTimeRange(edit.Start, edit.End));
                }
                else if (IsZoomAction(action))
                {
                    var zoom = TryCreateZoom(edit);
                    if (zoom is not null)
                    {
                        videoEffects.Add(zoom);
                    }
                }
                else if (IsBlurAction(action))
                {
                    var blur = TryCreateBlur(edit);
                    if (blur is not null)
                    {
                        videoEffects.Add(blur);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Invalid sidecar is treated as no edits.
        }
        catch (IOException)
        {
            // Unreadable sidecar is treated as no edits.
        }
        catch (UnauthorizedAccessException)
        {
            // Unreadable sidecar is treated as no edits.
        }

        return new CorrEdits(mutes, skips, videoEffects);
    }

    private static bool IsZoomAction(string? action)
        => string.Equals(action, "zoom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "punch", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlurAction(string? action)
        => string.Equals(action, "blur", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "boxblur", StringComparison.OrdinalIgnoreCase);

    private static VideoEffect? TryCreateZoom(CorrEdit edit)
    {
        var box = TryCreateBox(edit.Box);
        var scale = edit.Scale is > 1 ? edit.Scale.Value : DefaultZoomScale;
        if (box is null && scale <= 1)
        {
            return null;
        }

        return new VideoEffect(
            VideoEffectKind.Zoom,
            edit.Start,
            edit.End,
            scale,
            Clamp01(edit.X ?? 0.5),
            Clamp01(edit.Y ?? 0.5),
            0,
            box,
            TryCreateBoxEnd(edit.BoxEnd, box),
            edit.XEnd is { } xEnd ? Clamp01(xEnd) : null,
            edit.YEnd is { } yEnd ? Clamp01(yEnd) : null);
    }

    private static VideoEffect? TryCreateBlur(CorrEdit edit)
    {
        var box = TryCreateBox(edit.Box);
        var radius = edit.Radius is > 0 ? edit.Radius.Value : DefaultBlurRadius;
        return new VideoEffect(
            VideoEffectKind.Blur,
            edit.Start,
            edit.End,
            1,
            0.5,
            0.5,
            radius,
            box,
            TryCreateBoxEnd(edit.BoxEnd, box));
    }

    private static NormalizedBox? TryCreateBox(CorrBox? box)
    {
        if (box is null)
        {
            return null;
        }

        var width = box.Width;
        var height = box.Height;
        if (width <= 0.001 || height <= 0.001)
        {
            return null;
        }

        var x = Clamp01(box.X);
        var y = Clamp01(box.Y);
        width = Math.Min(width, 1 - x);
        height = Math.Min(height, 1 - y);
        if (width <= 0.001 || height <= 0.001)
        {
            return null;
        }

        return new NormalizedBox(x, y, width, height);
    }

    private static NormalizedBox? TryCreateBoxEnd(CorrBox? end, NormalizedBox? start)
    {
        if (end is null)
        {
            return null;
        }

        var width = end.Width > 0.001 ? end.Width : start?.Width ?? 0;
        var height = end.Height > 0.001 ? end.Height : start?.Height ?? 0;
        if (width <= 0.001 || height <= 0.001)
        {
            return null;
        }

        var x = Clamp01(end.X);
        var y = Clamp01(end.Y);
        width = Math.Min(width, 1 - x);
        height = Math.Min(height, 1 - y);
        if (width <= 0.001 || height <= 0.001)
        {
            return null;
        }

        return new NormalizedBox(x, y, width, height);
    }

    private static double Clamp01(double value)
        => Math.Clamp(value, 0, 1);

    private sealed class CorrDocument
    {
        public IReadOnlyList<CorrEdit>? Edits { get; set; }
    }

    private sealed class CorrEdit
    {
        public double Start { get; set; }

        public double End { get; set; }

        public string? Action { get; set; }

        public double? Scale { get; set; }

        public double? X { get; set; }

        public double? Y { get; set; }

        public double? XEnd { get; set; }

        public double? YEnd { get; set; }

        public double? Radius { get; set; }

        public CorrBox? Box { get; set; }

        public CorrBox? BoxEnd { get; set; }
    }

    private sealed class CorrBox
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }
}
