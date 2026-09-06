using System.Collections.Generic;

namespace Jellyfin.Plugin.CorrMedia.Api;

/// <summary>
/// Body for saving which minor categories to apply.
/// </summary>
public sealed class CorrOverridesRequest
{
    /// <summary>
    /// Gets a value indicating whether sidecar edits should run on playback.
    /// Default is true.
    /// </summary>
    public bool ApplyEdits { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether only <see cref="EnabledCategories"/> should apply.
    /// False (default) means apply every sidecar edit.
    /// </summary>
    public bool RestrictToCategories { get; init; }

    /// <summary>
    /// Gets minor category ids to apply when restricting.
    /// </summary>
    public IReadOnlyList<string>? EnabledCategories { get; init; }
}
