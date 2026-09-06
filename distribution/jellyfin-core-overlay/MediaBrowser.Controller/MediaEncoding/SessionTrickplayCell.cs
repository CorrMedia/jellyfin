#nullable enable

namespace MediaBrowser.Controller.MediaEncoding;

/// <summary>
/// One thumbnail cell inside an original trickplay sprite sheet.
/// </summary>
/// <param name="TileIndex">Original sprite file index (<c>{index}.jpg</c>).</param>
/// <param name="Column">Zero-based column inside the sprite.</param>
/// <param name="Row">Zero-based row inside the sprite.</param>
public readonly record struct SessionTrickplayCell(int TileIndex, int Column, int Row);
