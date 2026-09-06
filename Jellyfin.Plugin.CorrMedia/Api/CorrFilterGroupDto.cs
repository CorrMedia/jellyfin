using System.Collections.Generic;

namespace Jellyfin.Plugin.CorrMedia.Api;

/// <summary>
/// A major filter group with nested minors.
/// </summary>
public sealed class CorrFilterGroupDto
{
    /// <summary>
    /// Gets or sets the major id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display label.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets see or hear.
    /// </summary>
    public string Channel { get; set; } = "see";

    /// <summary>
    /// Gets or sets a value indicating whether every minor is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether some but not all minors are enabled.
    /// </summary>
    public bool Mixed { get; set; }

    /// <summary>
    /// Gets the nested minors.
    /// </summary>
    public IReadOnlyList<CorrFilterMinorDto> Minors { get; init; } = [];
}
