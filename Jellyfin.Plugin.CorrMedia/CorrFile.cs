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
    /// Parses mute and skip ranges from a corr.json sidecar.
    /// Unknown <c>action</c> values are ignored so the schema can grow.
    /// </summary>
    /// <param name="corrPath">Corr sidecar path.</param>
    /// <returns>Mute and skip lists.</returns>
    public static (List<MuteTimeRange> Mutes, List<MuteTimeRange> Skips) ParseMuteAndSkip(string corrPath)
    {
        var mutes = new List<MuteTimeRange>();
        var skips = new List<MuteTimeRange>();
        try
        {
            using var stream = File.OpenRead(corrPath);
            var document = JsonSerializer.Deserialize<CorrDocument>(stream, JsonOptions);
            if (document?.Edits is null)
            {
                return (mutes, skips);
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

        return (mutes, skips);
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
    }
}
