namespace Jellyfin.Plugin.CorrMedia;

/// <summary>
/// Represents the different types of EDL actions that can be performed.
/// </summary>
public enum EdlAction
{
    /// <summary>
    /// No action specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Mute audio during the specified time range.
    /// </summary>
    Mute = 1,

    /// <summary>
    /// Classic EDL scene marker (type 2). Ignored for playback; reserved for future POI / MediaSegments use.
    /// </summary>
    SceneMarker = 2,

    /// <summary>
    /// Skip (seek past / cut) the specified time range.
    /// </summary>
    Skip = 3
}
