using System;
using System.Globalization;

namespace Jellyfin.Plugin.CorrMedia;

/// <summary>
/// Helpers for the dual-delivery Edited media source id.
/// </summary>
internal static class EdlMediaSourceIds
{
    private const string Suffix = "_edl";

    /// <summary>
    /// Builds the Edited media source id for a library item.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <returns>Id string.</returns>
    public static string ForItem(Guid itemId)
        => itemId.ToString("N", CultureInfo.InvariantCulture) + Suffix;

    /// <summary>
    /// Returns true when <paramref name="mediaSourceId"/> is an Edited/EDL source.
    /// </summary>
    /// <param name="mediaSourceId">Media source id from playback/stream request.</param>
    /// <returns>True for Edited sources.</returns>
    public static bool IsEdited(string? mediaSourceId)
        => !string.IsNullOrEmpty(mediaSourceId)
           && mediaSourceId.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);
}
