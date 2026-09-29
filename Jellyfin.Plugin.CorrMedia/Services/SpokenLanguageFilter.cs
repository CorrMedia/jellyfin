using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Drops sidecar edits whose spoken language is not the selected audio track.
/// Edits that omit <c>language</c> are kept.
/// </summary>
internal static class SpokenLanguageFilter
{
    /// <summary>
    /// Returns edits that apply for <paramref name="streamLanguage"/>.
    /// </summary>
    /// <param name="edits">Parsed sidecar edits.</param>
    /// <param name="streamLanguage">Language of the selected audio stream. Null, empty, <c>und</c>, and <c>mul</c> keep only untagged edits.</param>
    /// <returns>Language-filtered edits.</returns>
    public static CorrEdits Apply(CorrEdits edits, string? streamLanguage)
    {
        ArgumentNullException.ThrowIfNull(edits);
        if (!edits.All.Any(descriptor => !string.IsNullOrWhiteSpace(descriptor.Language)))
        {
            return edits;
        }

        var kept = new HashSet<string>(
            edits.All.Where(descriptor => SpokenLanguage.Applies(descriptor.Language, streamLanguage)).Select(descriptor => descriptor.Id),
            StringComparer.OrdinalIgnoreCase);

        return new CorrEdits(
            edits.Mutes.Where(mute => kept.Contains(mute.Id)).ToList(),
            edits.Skips.Where(skip => kept.Contains(skip.Id)).ToList(),
            edits.VideoEffects.Where(effect => kept.Contains(effect.Id)).ToList(),
            edits.All.Where(descriptor => kept.Contains(descriptor.Id)).ToList());
    }
}
