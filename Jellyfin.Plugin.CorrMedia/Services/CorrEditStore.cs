using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// In-process sidecar mute + skip ranges keyed by play session / device / session id.
/// </summary>
public sealed class CorrEditStore
{
    private readonly ConcurrentDictionary<string, CorrEditPlan> _plans = new(StringComparer.Ordinal);
    private readonly ILogger<CorrEditStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrEditStore"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public CorrEditStore(ILogger<CorrEditStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stores an edit plan under each non-empty key.
    /// </summary>
    /// <param name="plan">Mute and skip ranges.</param>
    /// <param name="keys">Lookup keys.</param>
    public void SetPlan(CorrEditPlan plan, params string?[] keys)
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
            "Stored sidecar plan mutes={MuteCount} skips={SkipCount} videoEffects={VideoEffectCount}",
            plan.MuteRanges.Count,
            plan.SkipRanges.Count,
            plan.VideoEffects.Count);
    }

    /// <summary>
    /// Gets a plan for any of the keys.
    /// </summary>
    /// <param name="keys">Lookup keys.</param>
    /// <returns>Plan or null.</returns>
    public CorrEditPlan? GetPlan(params string?[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrEmpty(key) && _plans.TryGetValue(key, out var plan))
            {
                return plan;
            }
        }

        // Exact key match only — never fall back to a lone plan.
        return null;
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
