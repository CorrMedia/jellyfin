using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// In-process EDL mute + skip ranges keyed by play session / device / session id.
/// </summary>
public sealed class EdlEditStore
{
    private readonly ConcurrentDictionary<string, EdlEditPlan> _plans = new(StringComparer.Ordinal);
    private readonly ILogger<EdlEditStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EdlEditStore"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public EdlEditStore(ILogger<EdlEditStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stores an edit plan under each non-empty key.
    /// </summary>
    /// <param name="plan">Mute and skip ranges.</param>
    /// <param name="keys">Lookup keys.</param>
    public void SetPlan(EdlEditPlan plan, params string?[] keys)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            _plans[key] = plan;
        }

        _logger.LogInformation(
            "Stored EDL plan mutes={MuteCount} skips={SkipCount}",
            plan.MuteRanges.Count,
            plan.SkipRanges.Count);
    }

    /// <summary>
    /// Gets a plan for any of the keys.
    /// </summary>
    /// <param name="keys">Lookup keys.</param>
    /// <returns>Plan or null.</returns>
    public EdlEditPlan? GetPlan(params string?[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrEmpty(key) && _plans.TryGetValue(key, out var plan))
            {
                return plan;
            }
        }

        var all = _plans.Values.Distinct().ToList();
        return all.Count == 1 ? all[0] : null;
    }

    /// <summary>
    /// Clears plans for the given keys.
    /// </summary>
    /// <param name="keys">Keys.</param>
    public void Clear(params string?[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _plans.TryRemove(key, out _);
            }
        }
    }
}
