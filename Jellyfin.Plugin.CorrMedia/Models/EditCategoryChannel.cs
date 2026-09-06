namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// Whether a category edits picture or sound.
/// </summary>
public enum EditCategoryChannel
{
    /// <summary>
    /// Video effects, covers, skips of visual content.
    /// </summary>
    See,

    /// <summary>
    /// Mute, volume, beep, language.
    /// </summary>
    Hear
}
