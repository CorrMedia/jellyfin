using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// A time range in seconds for mute / volume / beep (or skip) EDL-driven filters.
/// </summary>
/// <param name="StartTime">Range start in seconds on the original timeline.</param>
/// <param name="EndTime">Range end in seconds on the original timeline.</param>
/// <param name="Channels">
/// Optional FFmpeg channel names (<c>FC</c>, <c>FL</c>, <c>FR</c>, …).
/// Null or empty means all channels. Ignored for skip ranges.
/// </param>
/// <param name="Kind">Mute, volume, or beep. Ignored for skip ranges.</param>
/// <param name="Gain">
/// Volume: linear source gain 0–1. Beep: tone amplitude 0–1. Ignored for mute and skip.
/// </param>
/// <param name="Frequency">Beep tone frequency in Hz. Ignored for other kinds.</param>
public sealed record MuteTimeRange(
    double StartTime,
    double EndTime,
    IReadOnlyList<string>? Channels = null,
    AudioEditKind Kind = AudioEditKind.Mute,
    double Gain = 0,
    double Frequency = 1000)
{
    /// <summary>
    /// Gets a value indicating whether this edit targets specific named channels.
    /// </summary>
    public bool IsSelectiveMute => Channels is { Count: > 0 };

    /// <summary>
    /// Gets normalized uppercase channel names, or an empty list for all-channel mute.
    /// </summary>
    public IReadOnlyList<string> NormalizedChannels
    {
        get
        {
            if (Channels is null || Channels.Count == 0)
            {
                return Array.Empty<string>();
            }

            return Channels
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
