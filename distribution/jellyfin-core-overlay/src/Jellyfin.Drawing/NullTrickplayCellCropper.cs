#nullable enable

using MediaBrowser.Controller.Drawing;

namespace Jellyfin.Drawing;

/// <summary>
/// Fallback cropper when Skia is unavailable.
/// </summary>
public sealed class NullTrickplayCellCropper : ITrickplayCellCropper
{
    /// <inheritdoc />
    public byte[]? CropCell(string spritePath, int tileWidth, int tileHeight, int column, int row, int quality = 90)
        => null;
}
