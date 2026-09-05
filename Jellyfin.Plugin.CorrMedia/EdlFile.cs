using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia;

/// <summary>
/// Shared EDL parsing helpers.
/// </summary>
internal static class EdlFile
{
    private static readonly char[] Separators = { ' ', '\t' };

    /// <summary>
    /// Returns the sidecar .edl path for a media file path.
    /// </summary>
    /// <param name="mediaPath">Media file path.</param>
    /// <returns>EDL path.</returns>
    public static string GetPath(string mediaPath)
    {
        var basePath = Path.ChangeExtension(mediaPath, null);
        return $"{basePath}.edl";
    }

    /// <summary>
    /// Parses mute (type 1) and skip (type 3) ranges from an EDL file.
    /// </summary>
    /// <param name="edlPath">EDL path.</param>
    /// <returns>Mute and skip lists.</returns>
    public static (List<MuteTimeRange> Mutes, List<MuteTimeRange> Skips) ParseMuteAndSkip(string edlPath)
    {
        var mutes = new List<MuteTimeRange>();
        var skips = new List<MuteTimeRange>();
        try
        {
            foreach (var line in File.ReadLines(edlPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var parts = trimmed.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2
                    || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
                    || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var end)
                    || end <= start)
                {
                    continue;
                }

                var action = 3; // default skip when type omitted
                if (parts.Length >= 3)
                {
                    if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out action))
                    {
                        continue;
                    }
                }

                switch (action)
                {
                    case 1:
                        mutes.Add(new MuteTimeRange(start, end));
                        break;
                    case 2:
                        // scene marker — ignore
                        break;
                    case 3:
                    default:
                        skips.Add(new MuteTimeRange(start, end));
                        break;
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return (mutes, skips);
    }

    /// <summary>
    /// Parses mute ranges (EDL type 1) from an EDL file.
    /// </summary>
    /// <param name="edlPath">Path to the EDL file.</param>
    /// <returns>Mute ranges in seconds.</returns>
    public static List<MuteTimeRange> ParseMuteRanges(string edlPath)
    {
        return ParseMuteAndSkip(edlPath).Mutes;
    }
}
