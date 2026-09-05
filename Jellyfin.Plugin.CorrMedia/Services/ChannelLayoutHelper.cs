using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Maps FFmpeg channel layouts to ordered channel labels for channelsplit/join.
/// </summary>
internal static class ChannelLayoutHelper
{
    private static readonly Dictionary<string, string[]> LayoutChannels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["mono"] = ["FC"],
            ["stereo"] = ["FL", "FR"],
            ["2.1"] = ["FL", "FR", "LFE"],
            ["3.0"] = ["FL", "FR", "FC"],
            ["3.1"] = ["FL", "FR", "FC", "LFE"],
            ["4.0"] = ["FL", "FR", "FC", "BC"],
            ["quad"] = ["FL", "FR", "BL", "BR"],
            ["5.0"] = ["FL", "FR", "FC", "BL", "BR"],
            ["5.0(side)"] = ["FL", "FR", "FC", "SL", "SR"],
            ["5.1"] = ["FL", "FR", "FC", "LFE", "BL", "BR"],
            ["5.1(side)"] = ["FL", "FR", "FC", "LFE", "SL", "SR"],
            ["6.1"] = ["FL", "FR", "FC", "LFE", "BC", "SL", "SR"],
            ["7.0"] = ["FL", "FR", "FC", "BL", "BR", "SL", "SR"],
            ["7.1"] = ["FL", "FR", "FC", "LFE", "BL", "BR", "SL", "SR"],
            ["7.1(wide)"] = ["FL", "FR", "FC", "LFE", "BL", "BR", "SL", "SR"],
        };

    /// <summary>
    /// Resolves ordered channel labels for a layout string / channel count.
    /// </summary>
    /// <param name="layout">Layout string from ffprobe / InferChannelLayout.</param>
    /// <param name="channelCount">Channel count fallback when layout is empty/unknown.</param>
    /// <returns>Channel labels, or empty if unsupported.</returns>
    public static IReadOnlyList<string> GetChannels(string? layout, int channelCount)
    {
        if (!string.IsNullOrWhiteSpace(layout)
            && LayoutChannels.TryGetValue(layout.Trim(), out var named))
        {
            return named;
        }

        var inferred = channelCount switch
        {
            1 => "mono",
            2 => "stereo",
            3 => "2.1",
            4 => "4.0",
            5 => "5.0",
            6 => "5.1",
            7 => "6.1",
            8 => "7.1",
            _ => null
        };

        if (inferred is not null && LayoutChannels.TryGetValue(inferred, out var fromCount))
        {
            return fromCount;
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Layout string suitable for FFmpeg <c>channelsplit</c>/<c>join</c>, or null.
    /// </summary>
    /// <param name="layout">Preferred layout.</param>
    /// <param name="channelCount">Fallback channel count.</param>
    /// <returns>Layout name.</returns>
    public static string? ResolveLayoutName(string? layout, int channelCount)
    {
        if (!string.IsNullOrWhiteSpace(layout))
        {
            var trimmed = layout.Trim();
            foreach (var key in LayoutChannels.Keys)
            {
                if (string.Equals(key, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }
        }

        return channelCount switch
        {
            1 => "mono",
            2 => "stereo",
            3 => "2.1",
            4 => "4.0",
            5 => "5.0",
            6 => "5.1",
            7 => "6.1",
            8 => "7.1",
            _ => null
        };
    }

    /// <summary>
    /// Intersects requested mute channels with the source layout.
    /// Empty request = all channels. Empty intersection after named request = all channels (fallback).
    /// </summary>
    /// <param name="requested">Normalized channel names from the sidecar (empty = all).</param>
    /// <param name="sourceChannels">Source layout channel labels.</param>
    /// <returns>Channels to mute.</returns>
    public static IReadOnlyList<string> ResolveMuteTargets(
        IReadOnlyList<string> requested,
        IReadOnlyList<string> sourceChannels)
    {
        if (sourceChannels.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (requested.Count == 0)
        {
            return sourceChannels.ToList();
        }

        var hits = sourceChannels
            .Where(c => requested.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // FC-only (etc.) on stereo: no named hits → mute everything so dialogue mute still works.
        return hits.Count == 0 ? sourceChannels.ToList() : hits;
    }
}
