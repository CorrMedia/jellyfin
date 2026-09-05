using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private const double DefaultVolumeGain = 0.2;
    private const double DefaultBeepGain = 0.3;
    private const double DefaultBeepFrequency = 1000;
    private const double DefaultPixelSize = 16;

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
                    mutes.Add(new MuteTimeRange(edit.Start, edit.End, NormalizeChannels(edit.Channels)));
                }
                else if (string.Equals(action, "volume", StringComparison.OrdinalIgnoreCase))
                {
                    mutes.Add(new MuteTimeRange(
                        edit.Start,
                        edit.End,
                        NormalizeChannels(edit.Channels),
                        AudioEditKind.Volume,
                        ClampGain(edit.Gain, DefaultVolumeGain)));
                }
                else if (string.Equals(action, "beep", StringComparison.OrdinalIgnoreCase))
                {
                    mutes.Add(new MuteTimeRange(
                        edit.Start,
                        edit.End,
                        NormalizeChannels(edit.Channels),
                        AudioEditKind.Beep,
                        ClampGain(edit.Gain, DefaultBeepGain),
                        edit.Frequency is > 0 ? edit.Frequency.Value : DefaultBeepFrequency));
                }
                else if (string.Equals(action, "skip", StringComparison.OrdinalIgnoreCase))
                {
                    skips.Add(new MuteTimeRange(edit.Start, edit.End));
                }
                else if (IsZoomAction(action))
                {
                    AddIfNotNull(videoEffects, TryCreateZoomOrCrop(edit, VideoEffectKind.Zoom));
                }
                else if (IsCropAction(action))
                {
                    AddIfNotNull(videoEffects, TryCreateZoomOrCrop(edit, VideoEffectKind.Crop));
                }
                else if (IsBlurAction(action))
                {
                    AddIfNotNull(videoEffects, TryCreateBlur(edit));
                }
                else if (IsCoverAction(action))
                {
                    AddIfNotNull(videoEffects, TryCreateCover(edit, VideoEffectKind.Cover));
                }
                else if (string.Equals(action, "blank", StringComparison.OrdinalIgnoreCase))
                {
                    AddIfNotNull(videoEffects, TryCreateCover(edit, VideoEffectKind.Blank));
                }
                else if (IsPixelateAction(action))
                {
                    AddIfNotNull(videoEffects, TryCreatePixelate(edit));
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

    private static void AddIfNotNull(List<VideoEffect> effects, VideoEffect? effect)
    {
        if (effect is not null)
        {
            effects.Add(effect);
        }
    }

    private static bool IsZoomAction(string? action)
        => string.Equals(action, "zoom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "punch", StringComparison.OrdinalIgnoreCase);

    private static bool IsCropAction(string? action)
        => string.Equals(action, "crop", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlurAction(string? action)
        => string.Equals(action, "blur", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "boxblur", StringComparison.OrdinalIgnoreCase);

    private static bool IsCoverAction(string? action)
        => string.Equals(action, "cover", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "blackout", StringComparison.OrdinalIgnoreCase);

    private static bool IsPixelateAction(string? action)
        => string.Equals(action, "pixelate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "mosaic", StringComparison.OrdinalIgnoreCase);

    private static VideoEffect? TryCreateZoomOrCrop(CorrEdit edit, VideoEffectKind kind)
    {
        var box = TryCreateBox(edit.Box);
        var scale = edit.Scale is > 1 ? edit.Scale.Value : DefaultZoomScale;
        if (box is null && scale <= 1)
        {
            return null;
        }

        return new VideoEffect(
            kind,
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

    private static VideoEffect? TryCreateCover(CorrEdit edit, VideoEffectKind kind)
    {
        var box = kind == VideoEffectKind.Blank ? null : TryCreateBox(edit.Box);
        return new VideoEffect(
            kind,
            edit.Start,
            edit.End,
            1,
            0.5,
            0.5,
            0,
            box,
            box is null ? null : TryCreateBoxEnd(edit.BoxEnd, box));
    }

    private static VideoEffect? TryCreatePixelate(CorrEdit edit)
    {
        var box = TryCreateBox(edit.Box);
        var size = edit.Size is > 1 ? Math.Clamp(edit.Size.Value, 2, 128) : DefaultPixelSize;
        return new VideoEffect(
            VideoEffectKind.Pixelate,
            edit.Start,
            edit.End,
            1,
            0.5,
            0.5,
            0,
            box,
            TryCreateBoxEnd(edit.BoxEnd, box),
            BlockSize: size);
    }

    private static double ClampGain(double? gain, double fallback)
        => Math.Clamp(gain ?? fallback, 0, 1);

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

    private static List<string>? NormalizeChannels(IReadOnlyList<string>? channels)
    {
        if (channels is null || channels.Count == 0)
        {
            return null;
        }

        var normalized = channels
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return normalized.Count == 0 ? null : normalized;
    }

    private sealed class CorrDocument
    {
        public IReadOnlyList<CorrEdit>? Edits { get; set; }
    }

    private sealed class CorrEdit
    {
        public double Start { get; set; }

        public double End { get; set; }

        public string? Action { get; set; }

        public IReadOnlyList<string>? Channels { get; set; }

        public double? Scale { get; set; }

        public double? X { get; set; }

        public double? Y { get; set; }

        public double? XEnd { get; set; }

        public double? YEnd { get; set; }

        public double? Radius { get; set; }

        public double? Gain { get; set; }

        public double? Frequency { get; set; }

        public double? Size { get; set; }

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
