using System.Collections.Generic;

namespace Jellyfin.Plugin.CorrMedia.Api;

/// <summary>
/// Global category filters plus this user's enabled set.
/// </summary>
public sealed class CorrPlaybackFiltersDto
{
    /// <summary>
    /// Gets or sets a value indicating whether sidecar edits run on playback.
    /// Default is true.
    /// </summary>
    public bool ApplyEdits { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether only enabled categories apply.
    /// False means apply the entire sidecar (uncustomized default).
    /// </summary>
    public bool RestrictToCategories { get; set; }

    /// <summary>
    /// Gets the major/minor filter tree.
    /// </summary>
    public IReadOnlyList<CorrFilterGroupDto> Groups { get; init; } = [];
}
