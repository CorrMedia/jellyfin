using System.Collections.Generic;

namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// One category node. Leaves are specific filters (including words); parents nest children.
/// </summary>
/// <param name="Id">Stable id stored in user overrides and sidecar <c>categories</c>.</param>
/// <param name="Label">UI label.</param>
/// <param name="Channel">See vs hear; null inherits the parent.</param>
/// <param name="SharedWith">Other major this id also appears under, when shown as a cross-link.</param>
/// <param name="Children">Nested filters. Empty or null means this node is a leaf.</param>
public sealed record EditCategoryMinor(
    string Id,
    string Label,
    EditCategoryChannel? Channel = null,
    string? SharedWith = null,
    IReadOnlyList<EditCategoryMinor>? Children = null)
{
    /// <summary>
    /// Gets nested filters; never null.
    /// </summary>
    public IReadOnlyList<EditCategoryMinor> Nested => Children ?? [];
}
