#nullable enable

using System;
using System.IO;
using MediaBrowser.Controller.Drawing;
using SkiaSharp;

namespace Jellyfin.Drawing.Skia;

/// <summary>
/// Crops a single trickplay thumbnail from a sprite sheet with Skia.
/// </summary>
public sealed class SkiaTrickplayCellCropper : ITrickplayCellCropper
{
    /// <inheritdoc />
    public byte[]? CropCell(string spritePath, int tileWidth, int tileHeight, int column, int row, int quality = 90)
    {
        if (string.IsNullOrEmpty(spritePath) || !File.Exists(spritePath))
        {
            return null;
        }

        tileWidth = Math.Max(1, tileWidth);
        tileHeight = Math.Max(1, tileHeight);
        if (column < 0 || row < 0 || column >= tileWidth || row >= tileHeight)
        {
            return null;
        }

        using var bitmap = SKBitmap.Decode(spritePath);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return null;
        }

        var cellWidth = bitmap.Width / tileWidth;
        var cellHeight = bitmap.Height / tileHeight;
        if (cellWidth <= 0 || cellHeight <= 0)
        {
            return null;
        }

        var x = column * cellWidth;
        var y = row * cellHeight;
        if (x + cellWidth > bitmap.Width || y + cellHeight > bitmap.Height)
        {
            return null;
        }

        var rect = new SKRectI(x, y, x + cellWidth, y + cellHeight);
        using var subset = new SKBitmap(cellWidth, cellHeight);
        if (!bitmap.ExtractSubset(subset, rect))
        {
            return null;
        }

        using var data = subset.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(quality, 1, 100));
        return data?.ToArray();
    }
}
