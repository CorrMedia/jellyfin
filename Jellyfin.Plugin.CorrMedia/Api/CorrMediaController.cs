using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CorrMedia.Api;

/// <summary>
/// Per-user sidecar apply toggle and category filters.
/// </summary>
[ApiController]
[Authorize]
[Route("CorrMedia")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class CorrMediaController : ControllerBase
{
    private readonly EditOverrideStore _editOverrideStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrMediaController"/> class.
    /// </summary>
    /// <param name="editOverrideStore">Override store.</param>
    public CorrMediaController(EditOverrideStore editOverrideStore)
    {
        _editOverrideStore = editOverrideStore ?? throw new ArgumentNullException(nameof(editOverrideStore));
    }

    /// <summary>
    /// Gets this user's apply toggle, category filters, and the shared category tree.
    /// </summary>
    /// <returns>Master toggle plus majors/minors.</returns>
    [HttpGet("Filters")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CorrPlaybackFiltersDto> GetFilters()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(ToFiltersDto(_editOverrideStore.Get(userId)));
    }

    /// <summary>
    /// Saves this user's apply toggle and category filters.
    /// </summary>
    /// <param name="request">Master toggle and enabled category ids.</param>
    /// <returns>Updated filters.</returns>
    [HttpPut("Filters")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CorrPlaybackFiltersDto> SaveFilters([FromBody] CorrOverridesRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var known = new HashSet<string>(EditCategoryCatalog.AllMinorIds, StringComparer.OrdinalIgnoreCase);
        var enabled = (request?.EnabledCategories ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id) && known.Contains(id.Trim()))
            .Select(id => id.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var overrides = new UserItemEditOverrides
        {
            ApplyEdits = request?.ApplyEdits != false,
            RestrictToCategories = request?.RestrictToCategories == true
        };
        foreach (var id in enabled)
        {
            overrides.EnabledCategories.Add(id);
        }

        _editOverrideStore.Set(userId, overrides);
        return Ok(ToFiltersDto(_editOverrideStore.Get(userId)));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.Claims
            .FirstOrDefault(c => string.Equals(c.Type, "Jellyfin-UserId", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }

    private static CorrPlaybackFiltersDto ToFiltersDto(UserItemEditOverrides overrides)
    {
        var unrestricted = !overrides.RestrictToCategories;
        bool LeafOn(string leafId)
            => unrestricted
               || overrides.EnabledCategories.Any(enabled => EditCategoryCatalog.Covers(enabled, leafId));

        CorrFilterMinorDto MapNode(EditCategoryMinor node, EditCategoryChannel inheritedChannel)
        {
            var channel = node.Channel ?? inheritedChannel;
            var children = node.Nested.Select(child => MapNode(child, channel)).ToList();
            var leaves = EditCategoryCatalog.LeafIds(node);
            var onCount = leaves.Count(LeafOn);
            return new CorrFilterMinorDto
            {
                Id = node.Id,
                Label = node.Label,
                Enabled = leaves.Count > 0 && onCount == leaves.Count,
                Mixed = onCount > 0 && onCount < leaves.Count,
                Channel = channel == EditCategoryChannel.Hear ? "hear" : "see",
                SharedWith = string.IsNullOrEmpty(node.SharedWith) ? null : node.SharedWith,
                Children = children
            };
        }

        var groups = new List<CorrFilterGroupDto>();
        foreach (var major in EditCategoryCatalog.Majors)
        {
            var minors = major.Minors.Select(minor => MapNode(minor, major.Channel)).ToList();
            var onCount = minors.Count(m => m.Enabled);
            groups.Add(new CorrFilterGroupDto
            {
                Id = major.Id,
                Label = major.Label,
                Channel = major.Channel == EditCategoryChannel.See ? "see" : "hear",
                Enabled = onCount == minors.Count,
                Mixed = onCount > 0 && onCount < minors.Count,
                Minors = minors
            });
        }

        return new CorrPlaybackFiltersDto
        {
            ApplyEdits = overrides.ApplyEdits,
            RestrictToCategories = overrides.RestrictToCategories,
            Groups = groups
        };
    }
}
