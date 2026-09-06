using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.CorrMedia.Models;
using Microsoft.Extensions.Logging;

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
    /// Invalid or unreadable files are treated as no edits and logged when <paramref name="logger"/> is set.
    /// </summary>
    /// <param name="corrPath">Corr sidecar path.</param>
    /// <param name="logger">Optional logger for unreadable or invalid sidecars.</param>
    /// <returns>Mute, skip, and video-effect lists.</returns>
    [SuppressMessage("Security", "CA3003:Review code for file path injection vulnerabilities", Justification = "Callers pass a sidecar path derived from a library item file path.")]
    public static CorrEdits Parse(string corrPath, ILogger? logger = null)
    {
        var mutes = new List<MuteTimeRange>();
        var skips = new List<MuteTimeRange>();
        var videoEffects = new List<VideoEffect>();
        var all = new List<CorrEditDescriptor>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                var canonical = CanonicalAction(action);
                if (canonical is null)
                {
                    continue;
                }

                var id = ResolveEditId(edit.Id, canonical, edit.Start, edit.End, usedIds);
                var descriptor = new CorrEditDescriptor(
                    id,
                    canonical,
                    edit.Start,
                    edit.End,
                    string.IsNullOrWhiteSpace(edit.Description) ? null : edit.Description.Trim(),
                    NormalizeCategories(edit.Categories));

                if (string.Equals(canonical, "mute", StringComparison.Ordinal))
                {
                    mutes.Add(new MuteTimeRange(edit.Start, edit.End, NormalizeChannels(edit.Channels), Id: id));
                    all.Add(descriptor);
                }
                else if (string.Equals(canonical, "volume", StringComparison.Ordinal))
                {
                    mutes.Add(new MuteTimeRange(
                        edit.Start,
                        edit.End,
                        NormalizeChannels(edit.Channels),
                        AudioEditKind.Volume,
                        ClampGain(edit.Gain, DefaultVolumeGain),
                        Id: id));
                    all.Add(descriptor);
                }
                else if (string.Equals(canonical, "beep", StringComparison.Ordinal))
                {
                    mutes.Add(new MuteTimeRange(
                        edit.Start,
                        edit.End,
                        NormalizeChannels(edit.Channels),
                        AudioEditKind.Beep,
                        ClampGain(edit.Gain, DefaultBeepGain),
                        edit.Frequency is > 0 ? edit.Frequency.Value : DefaultBeepFrequency,
                        id));
                    all.Add(descriptor);
                }
                else if (string.Equals(canonical, "skip", StringComparison.Ordinal))
                {
                    skips.Add(new MuteTimeRange(edit.Start, edit.End, Id: id));
                    all.Add(descriptor);
                }
                else if (string.Equals(canonical, "zoom", StringComparison.Ordinal))
                {
                    AddIfNotNull(videoEffects, all, descriptor, TryCreateZoomOrCrop(edit, VideoEffectKind.Zoom, id));
                }
                else if (string.Equals(canonical, "crop", StringComparison.Ordinal))
                {
                    AddIfNotNull(videoEffects, all, descriptor, TryCreateZoomOrCrop(edit, VideoEffectKind.Crop, id));
                }
                else if (string.Equals(canonical, "blur", StringComparison.Ordinal))
                {
                    AddIfNotNull(videoEffects, all, descriptor, TryCreateBlur(edit, id));
                }
                else if (string.Equals(canonical, "cover", StringComparison.Ordinal))
                {
                    AddIfNotNull(videoEffects, all, descriptor, TryCreateCover(edit, VideoEffectKind.Cover, id));
                }
                else if (string.Equals(canonical, "blank", StringComparison.Ordinal))
                {
                    AddIfNotNull(videoEffects, all, descriptor, TryCreateCover(edit, VideoEffectKind.Blank, id));
                }
                else if (string.Equals(canonical, "pixelate", StringComparison.Ordinal))
                {
                    AddIfNotNull(videoEffects, all, descriptor, TryCreatePixelate(edit, id));
                }
            }
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Ignoring invalid CorrMedia sidecar {Path}", corrPath);
        }
        catch (IOException ex)
        {
            logger?.LogWarning(ex, "Ignoring unreadable CorrMedia sidecar {Path}", corrPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogWarning(ex, "Ignoring unreadable CorrMedia sidecar {Path}", corrPath);
        }

        return new CorrEdits(mutes, skips, videoEffects, all);
    }

    private static void AddIfNotNull(
        List<VideoEffect> effects,
        List<CorrEditDescriptor> all,
        CorrEditDescriptor descriptor,
        VideoEffect? effect)
    {
        if (effect is not null)
        {
            effects.Add(effect);
            all.Add(descriptor);
        }
    }

    private static string? CanonicalAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        if (string.Equals(action, "mute", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "volume", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "beep", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "skip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "crop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "blank", StringComparison.OrdinalIgnoreCase))
        {
            return action.Trim().ToLowerInvariant();
        }

        if (IsZoomAction(action))
        {
            return "zoom";
        }

        if (IsBlurAction(action))
        {
            return "blur";
        }

        if (IsCoverAction(action))
        {
            return "cover";
        }

        if (IsPixelateAction(action))
        {
            return "pixelate";
        }

        return null;
    }

    private static string ResolveEditId(
        string? sidecarId,
        string canonicalAction,
        double start,
        double end,
        HashSet<string> usedIds)
    {
        var raw = string.IsNullOrWhiteSpace(sidecarId)
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:G}:{2:G}", canonicalAction, start, end)
            : sidecarId.Trim();

        var id = raw;
        var suffix = 2;
        while (!usedIds.Add(id))
        {
            id = string.Format(CultureInfo.InvariantCulture, "{0}#{1}", raw, suffix);
            suffix++;
        }

        return id;
    }

    private static bool IsZoomAction(string? action)
        => string.Equals(action, "zoom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "punch", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlurAction(string? action)
        => string.Equals(action, "blur", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "boxblur", StringComparison.OrdinalIgnoreCase);

    private static bool IsCoverAction(string? action)
        => string.Equals(action, "cover", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "blackout", StringComparison.OrdinalIgnoreCase);

    private static bool IsPixelateAction(string? action)
        => string.Equals(action, "pixelate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "mosaic", StringComparison.OrdinalIgnoreCase);

    private static VideoEffect? TryCreateZoomOrCrop(CorrEdit edit, VideoEffectKind kind, string id)
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
            edit.YEnd is { } yEnd ? Clamp01(yEnd) : null,
            Id: id);
    }

    private static VideoEffect? TryCreateBlur(CorrEdit edit, string id)
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
            TryCreateBoxEnd(edit.BoxEnd, box),
            Id: id);
    }

    private static VideoEffect? TryCreateCover(CorrEdit edit, VideoEffectKind kind, string id)
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
            box is null ? null : TryCreateBoxEnd(edit.BoxEnd, box),
            Id: id);
    }

    private static VideoEffect? TryCreatePixelate(CorrEdit edit, string id)
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
            BlockSize: size,
            Id: id);
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

    private static List<string> NormalizeCategories(IReadOnlyList<string>? categories)
    {
        if (categories is null || categories.Count == 0)
        {
            return [];
        }

        return categories
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        public string? Id { get; set; }

        public string? Description { get; set; }

        public IReadOnlyList<string>? Categories { get; set; }

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
