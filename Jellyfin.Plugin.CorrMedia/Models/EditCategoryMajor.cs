using System.Collections.Generic;

namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// A major category with nested minors.
/// </summary>
/// <param name="Id">Stable major id.</param>
/// <param name="Label">UI label.</param>
/// <param name="Channel">See vs hear.</param>
/// <param name="Minors">Nested options.</param>
public sealed record EditCategoryMajor(
    string Id,
    string Label,
    EditCategoryChannel Channel,
    IReadOnlyList<EditCategoryMinor> Minors);
