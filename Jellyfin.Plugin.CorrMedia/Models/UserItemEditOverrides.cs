using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Jellyfin.Plugin.CorrMedia.Models;

/// <summary>
/// Per-user sidecar playback: master apply toggle plus optional category filters.
/// Default is apply every sidecar edit on the normal library item.
/// </summary>
public sealed class UserItemEditOverrides
{
    /// <summary>
    /// Gets or sets a value indicating whether sidecar edits should run on playback.
    /// Default is true. When false, the file plays untouched.
    /// </summary>
    public bool ApplyEdits { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the burned-in "Edited" badge
    /// appears at the start of an edited stream. Default is true.
    /// </summary>
    public bool ShowEditedBadge { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether only <see cref="EnabledCategories"/> should be applied.
    /// When false, every sidecar edit is applied (the uncustomized default).
    /// </summary>
    public bool RestrictToCategories { get; set; }

    /// <summary>
    /// Gets minor category ids this user wants applied when <see cref="RestrictToCategories"/> is true.
    /// </summary>
    public Collection<string> EnabledCategories { get; } = [];

    /// <summary>
    /// Gets a value indicating whether this is the uncustomized default
    /// (apply the entire sidecar, show the start-of-stream badge).
    /// </summary>
    public bool IsEmpty => ApplyEdits && ShowEditedBadge && !RestrictToCategories;

    /// <summary>
    /// Returns true when this edit's categories should be applied.
    /// </summary>
    /// <param name="categories">Sidecar category tags.</param>
    /// <returns>True to include the edit in the encode.</returns>
    public bool IsHonored(IReadOnlyList<string>? categories)
    {
        if (!RestrictToCategories)
        {
            return true;
        }

        var minors = EditCategoryCatalog.ResolveAll(categories);
        return minors.Any(IsResolvedIdHonored);
    }

    /// <summary>
    /// Returns true when a resolved category id is covered by this user's enabled set.
    /// </summary>
    /// <param name="editId">Resolved sidecar category id.</param>
    /// <returns>True to include the edit.</returns>
    private bool IsResolvedIdHonored(string editId)
    {
        if (EnabledCategories.Any(enabled => EditCategoryCatalog.Covers(enabled, editId)))
        {
            return true;
        }

        var node = EditCategoryCatalog.Find(editId);
        if (node is null || node.Nested.Count == 0)
        {
            return false;
        }

        return EditCategoryCatalog.LeafIds(node).All(leaf =>
            EnabledCategories.Any(enabled => EditCategoryCatalog.Covers(enabled, leaf)));
    }
}
