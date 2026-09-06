using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jellyfin.Plugin.CorrMedia.Models;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Maps trickplay thumbnail indices from the edited clock onto original sprite cells.
/// </summary>
internal static class CorrTrickplayMapper
{
    /// <summary>
    /// Thumbnail count on the edited timeline, never more than the original extract.
    /// </summary>
    /// <param name="editedTicks">Edited duration in ticks.</param>
    /// <param name="intervalMs">Milliseconds between original thumbnails.</param>
    /// <param name="originalThumbnailCount">Original extract count.</param>
    /// <returns>Edited thumbnail count.</returns>
    public static int EditedThumbnailCount(long editedTicks, int intervalMs, int originalThumbnailCount)
    {
        if (originalThumbnailCount <= 0)
        {
            return 0;
        }

        if (intervalMs <= 0 || editedTicks <= 0)
        {
            return originalThumbnailCount;
        }

        var editedMs = editedTicks / (double)TimeSpan.TicksPerMillisecond;
        var count = (int)Math.Ceiling(editedMs / intervalMs);
        return Math.Clamp(count, 1, originalThumbnailCount);
    }

    /// <summary>
    /// 1×1 DTO whose count matches the edited duration so clients never index skip interiors.
    /// </summary>
    /// <param name="original">Original-timeline trickplay info.</param>
    /// <param name="editedTicks">Edited duration in ticks.</param>
    /// <returns>DTO for the edited clock.</returns>
    public static TrickplayInfoDto RewriteDto(TrickplayInfoDto original, long editedTicks)
    {
        ArgumentNullException.ThrowIfNull(original);
        var cells = Math.Max(1, original.TileWidth * original.TileHeight);
        return original with
        {
            ThumbnailCount = EditedThumbnailCount(editedTicks, original.Interval, original.ThumbnailCount),
            TileWidth = 1,
            TileHeight = 1,
            Bandwidth = Math.Max(1, original.Bandwidth / cells)
        };
    }

    /// <summary>
    /// Maps an edited thumbnail index to the original sprite file and cell.
    /// Edited time never lands inside a skip because keep-range mapping is used.
    /// </summary>
    /// <param name="editedIndex">Thumbnail index on the edited clock.</param>
    /// <param name="intervalMs">Milliseconds between thumbnails.</param>
    /// <param name="originalThumbnailCount">Original extract count.</param>
    /// <param name="tileWidth">Original thumbnails per row.</param>
    /// <param name="tileHeight">Original thumbnails per column.</param>
    /// <param name="keep">Keep intervals on the original timeline.</param>
    /// <param name="tileIndex">Original sprite file index when the method returns true.</param>
    /// <param name="column">Zero-based column inside the sprite.</param>
    /// <param name="row">Zero-based row inside the sprite.</param>
    /// <returns>True when the index maps into a keep range.</returns>
    public static bool TryMapEditedIndex(
        int editedIndex,
        int intervalMs,
        int originalThumbnailCount,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<(double Start, double End)> keep,
        out int tileIndex,
        out int column,
        out int row)
    {
        tileIndex = 0;
        column = 0;
        row = 0;
        if (editedIndex < 0 || intervalMs <= 0 || originalThumbnailCount <= 0 || keep is null || keep.Count == 0)
        {
            return false;
        }

        var step = intervalMs / 1000.0;
        var editedDuration = 0.0;
        foreach (var (start, end) in keep)
        {
            editedDuration += end - start;
        }

        var editedSeconds = (editedIndex + 0.5) * step;
        if (editedDuration > 0.001)
        {
            editedSeconds = Math.Min(editedSeconds, editedDuration - 0.001);
        }

        if (!CorrTimeline.TryMapEditedToOriginal(editedSeconds, keep, out var originalSeconds)
            && !CorrTimeline.TryMapEditedToOriginal(editedIndex * step, keep, out originalSeconds))
        {
            return false;
        }

        var originalIndex = (int)Math.Floor(originalSeconds * 1000.0 / intervalMs);
        originalIndex = Math.Clamp(originalIndex, 0, originalThumbnailCount - 1);

        tileWidth = Math.Max(1, tileWidth);
        tileHeight = Math.Max(1, tileHeight);
        var cells = tileWidth * tileHeight;
        tileIndex = originalIndex / cells;
        var inTile = originalIndex % cells;
        column = inTile % tileWidth;
        row = inTile / tileWidth;
        return true;
    }

    /// <summary>
    /// HLS image playlist for a rewritten (typically 1×1) trickplay DTO.
    /// </summary>
    /// <param name="info">Edited-timeline trickplay info.</param>
    /// <param name="mediaSourceId">Media source id used in tile URLs.</param>
    /// <param name="apiKey">Optional API key for tile URLs.</param>
    /// <returns>m3u8 text, or null when there are no thumbnails.</returns>
    public static string? BuildHlsPlaylist(TrickplayInfoDto info, Guid mediaSourceId, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.ThumbnailCount <= 0 || info.Interval <= 0)
        {
            return null;
        }

        const string urlFormat = "{0}.jpg?MediaSourceId={1}&ApiKey={2}";
        const string decimalFormat = "{0:0.###}";

        var resolution = string.Create(CultureInfo.InvariantCulture, $"{info.Width}x{info.Height}");
        var layout = string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, info.TileWidth)}x{Math.Max(1, info.TileHeight)}");
        var thumbnailsPerTile = Math.Max(1, info.TileWidth) * Math.Max(1, info.TileHeight);
        var thumbnailDuration = info.Interval / 1000d;
        var infDuration = thumbnailDuration * thumbnailsPerTile;
        var tileCount = (int)Math.Ceiling((decimal)info.ThumbnailCount / thumbnailsPerTile);

        var builder = new StringBuilder(128);
        builder
            .AppendLine("#EXTM3U")
            .Append("#EXT-X-TARGETDURATION:")
            .AppendLine(tileCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine("#EXT-X-VERSION:7")
            .AppendLine("#EXT-X-MEDIA-SEQUENCE:1")
            .AppendLine("#EXT-X-PLAYLIST-TYPE:VOD")
            .AppendLine("#EXT-X-IMAGES-ONLY");

        for (var i = 0; i < tileCount; i++)
        {
            var thumbsThisTile = thumbnailsPerTile;
            var thisInf = infDuration;
            if (i == tileCount - 1)
            {
                thumbsThisTile = info.ThumbnailCount - (i * thumbnailsPerTile);
                thisInf = thumbnailDuration * thumbsThisTile;
            }

            builder
                .Append("#EXTINF:")
                .AppendFormat(CultureInfo.InvariantCulture, decimalFormat, thisInf)
                .AppendLine(",");

            builder
                .Append("#EXT-X-TILES:RESOLUTION=")
                .Append(resolution)
                .Append(",LAYOUT=")
                .Append(layout)
                .Append(",DURATION=")
                .AppendFormat(CultureInfo.InvariantCulture, decimalFormat, thumbnailDuration)
                .AppendLine();

            builder
                .AppendFormat(
                    CultureInfo.InvariantCulture,
                    urlFormat,
                    i.ToString(CultureInfo.InvariantCulture),
                    mediaSourceId.ToString("N"),
                    apiKey)
                .AppendLine();
        }

        builder.AppendLine("#EXT-X-ENDLIST");
        return builder.ToString();
    }

    /// <summary>
    /// Edited duration in ticks from original runtime and remaining skips.
    /// </summary>
    /// <param name="originalTicks">Uncut item duration.</param>
    /// <param name="skips">Applied skip ranges.</param>
    /// <returns>Edited ticks, or null when original is missing.</returns>
    public static long? EditedTicks(long? originalTicks, IReadOnlyList<MuteTimeRange> skips)
        => CorrPlaybackEvaluator.EditedRunTimeTicks(originalTicks, skips);
}
