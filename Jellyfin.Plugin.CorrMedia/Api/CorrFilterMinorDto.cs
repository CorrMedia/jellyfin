using System.Collections.Generic;

namespace Jellyfin.Plugin.CorrMedia.Api;

/// <summary>
/// One minor filter option.
/// </summary>
public sealed class CorrFilterMinorDto
{
    /// <summary>
    /// Gets or sets the minor id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display label.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this category is applied.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets see or hear for the status icon. Empty inherits the major.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the other major this id is linked from, when shown as a cross-link.
    /// </summary>
    public string? SharedWith { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether some but not all nested filters are enabled.
    /// </summary>
    public bool Mixed { get; set; }

    /// <summary>
    /// Gets nested filters. Empty on leaves.
    /// </summary>
    public IReadOnlyList<CorrFilterMinorDto> Children { get; init; } = [];
}
