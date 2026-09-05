using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Jellyfin.Plugin.CorrMedia;

/// <summary>
/// Helpers for the dual-delivery Edited media source id.
/// Jellyfin HLS/transcode paths parse MediaSourceId as a Guid, so the Edited
/// id must be a valid Guid (not <c>{itemId}_edl</c>).
/// </summary>
internal static class EdlMediaSourceIds
{
    private const string LegacySuffix = "_edl";

    /// <summary>
    /// Plugin id used as RFC 4122 name-based UUID namespace.
    /// </summary>
    private static readonly Guid NamespaceId = Guid.Parse("d477f9fe-bde6-467c-88ba-5336048f955c");

    private static readonly ConcurrentDictionary<string, Guid> EditedIdToItemId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Builds the Edited media source id for a library item.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <returns>Id string (Guid N-format).</returns>
    public static string ForItem(Guid itemId)
    {
        var n = CreateEditedId(itemId).ToString("N", CultureInfo.InvariantCulture);
        EditedIdToItemId[n] = itemId;
        return n;
    }

    /// <summary>
    /// Returns true when <paramref name="mediaSourceId"/> is an Edited/EDL source.
    /// </summary>
    /// <param name="mediaSourceId">Media source id from playback/stream request.</param>
    /// <param name="itemId">Optional library item id so detection does not depend on in-memory cache.</param>
    /// <returns>True for Edited sources.</returns>
    public static bool IsEdited(string? mediaSourceId, string? itemId = null)
    {
        if (string.IsNullOrEmpty(mediaSourceId))
        {
            return false;
        }

        if (mediaSourceId.EndsWith(LegacySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Guid.TryParse(itemId, out var itemGuid))
        {
            var expected = CreateEditedId(itemGuid);
            EditedIdToItemId[expected.ToString("N", CultureInfo.InvariantCulture)] = itemGuid;
            if (TryParseGuid(mediaSourceId, out var sourceGuid) && sourceGuid == expected)
            {
                return true;
            }
        }

        return TryNormalize(mediaSourceId, out var n) && EditedIdToItemId.ContainsKey(n);
    }

    /// <summary>
    /// Resolves the library item id that owns an Edited media source id.
    /// </summary>
    /// <param name="mediaSourceId">Media source id from the client.</param>
    /// <param name="itemId">Library item id when known.</param>
    /// <returns>True when this is an Edited source with a known parent item.</returns>
    public static bool TryGetItemId(string? mediaSourceId, out Guid itemId)
    {
        itemId = default;
        return TryNormalize(mediaSourceId, out var n) && EditedIdToItemId.TryGetValue(n, out itemId);
    }

    private static Guid CreateEditedId(Guid itemId)
        => CreateVersion5(NamespaceId, itemId.ToString("N", CultureInfo.InvariantCulture) + LegacySuffix);

    private static bool TryNormalize(string? mediaSourceId, out string n)
    {
        n = string.Empty;
        if (string.IsNullOrEmpty(mediaSourceId))
        {
            return false;
        }

        if (TryParseGuid(mediaSourceId, out var g))
        {
            n = g.ToString("N", CultureInfo.InvariantCulture);
            return true;
        }

        n = mediaSourceId;
        return true;
    }

    private static bool TryParseGuid(string value, out Guid guid)
        => Guid.TryParse(value, out guid);

    private static Guid CreateVersion5(Guid namespaceId, string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(namespaceId.ToString("N") + ":" + name));
        var bytes = new byte[16];
        Buffer.BlockCopy(hash, 0, bytes, 0, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
