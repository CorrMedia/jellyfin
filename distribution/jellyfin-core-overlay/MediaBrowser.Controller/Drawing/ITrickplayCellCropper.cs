#nullable enable

namespace MediaBrowser.Controller.Drawing;

/// <summary>
/// Crops one thumbnail cell out of a trickplay sprite JPEG.
/// </summary>
public interface ITrickplayCellCropper
{
    /// <summary>
    /// Crops the cell at <paramref name="column"/>, <paramref name="row"/> from a sprite sheet.
    /// </summary>
    /// <param name="spritePath">Path to the original tile JPEG.</param>
    /// <param name="tileWidth">Thumbnails per row.</param>
    /// <param name="tileHeight">Thumbnails per column.</param>
    /// <param name="column">Zero-based column.</param>
    /// <param name="row">Zero-based row.</param>
    /// <param name="quality">JPEG encode quality.</param>
    /// <returns>JPEG bytes, or null when the crop cannot be performed.</returns>
    byte[]? CropCell(string spritePath, int tileWidth, int tileHeight, int column, int row, int quality = 90);
}
