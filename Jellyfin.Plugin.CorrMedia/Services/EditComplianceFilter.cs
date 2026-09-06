using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Drops sidecar edits whose categories this user did not enable.
/// </summary>
internal static class EditComplianceFilter
{
    /// <summary>
    /// Returns a copy of <paramref name="edits"/> with disabled-category ranges removed.
    /// Descriptors in <see cref="CorrEdits.All"/> stay complete for UI.
    /// </summary>
    /// <param name="edits">Parsed sidecar edits.</param>
        /// <param name="overrides">User filters; null or unrestricted means apply all.</param>
    /// <returns>Filtered edits for encode.</returns>
    public static CorrEdits Apply(CorrEdits edits, UserItemEditOverrides? overrides)
    {
        ArgumentNullException.ThrowIfNull(edits);
        if (overrides is null)
        {
            return edits;
        }

        if (!overrides.ApplyEdits)
        {
            return new CorrEdits([], [], [], edits.All);
        }

        if (!overrides.RestrictToCategories)
        {
            return edits;
        }

        var honoredIds = new HashSet<string>(
            edits.All.Where(d => overrides.IsHonored(d.Categories)).Select(d => d.Id),
            StringComparer.OrdinalIgnoreCase);

        return new CorrEdits(
            edits.Mutes.Where(m => honoredIds.Contains(m.Id)).ToList(),
            edits.Skips.Where(s => honoredIds.Contains(s.Id)).ToList(),
            edits.VideoEffects.Where(v => honoredIds.Contains(v.Id)).ToList(),
            edits.All);
    }
}
