using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.CorrMedia.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Persists per-user category filters under the plugin data folder.
/// Missing files mean apply the entire sidecar.
/// </summary>
public sealed class EditOverrideStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ConcurrentDictionary<string, UserOverrideFile> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<EditOverrideStore> _logger;
    private readonly object _ioLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EditOverrideStore"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public EditOverrideStore(ILogger<EditOverrideStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets this user's filters. Missing files mean apply every sidecar edit.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>Overrides; never null.</returns>
    public UserItemEditOverrides Get(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return new UserItemEditOverrides();
        }

        return Clone(LoadUserFile(userId));
    }

    /// <summary>
    /// Saves or clears this user's apply toggle and category filters.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <param name="overrides">Settings; default apply-all clears the file.</param>
    public void Set(Guid userId, UserItemEditOverrides? overrides)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var cleaned = Normalize(overrides);
        lock (_ioLock)
        {
            Persist(userId, ToFile(cleaned));
        }

        _logger.LogInformation(
            "CorrMedia overrides User={UserId} applyEdits={ApplyEdits} showBadge={ShowBadge} restrict={Restrict} enabled={EnabledCount}",
            userId,
            cleaned.ApplyEdits,
            cleaned.ShowEditedBadge,
            cleaned.RestrictToCategories,
            cleaned.EnabledCategories.Count);
    }

    private UserOverrideFile LoadUserFile(Guid userId)
    {
        var cacheKey = userId.ToString("N", CultureInfo.InvariantCulture);
        return _cache.GetOrAdd(cacheKey, _ => ReadFromDisk(userId));
    }

    private UserOverrideFile ReadFromDisk(Guid userId)
    {
        var path = GetFilePath(userId);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return new UserOverrideFile();
        }

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<UserOverrideFile>(json, JsonOptions);
            if (parsed is null)
            {
                return new UserOverrideFile();
            }

            parsed.EnabledCategories ??= [];
            return parsed;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ignoring unreadable CorrMedia override file {Path}", path);
            return new UserOverrideFile();
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Ignoring unreadable CorrMedia override file {Path}", path);
            return new UserOverrideFile();
        }
    }

    private void Persist(Guid userId, UserOverrideFile file)
    {
        var path = GetFilePath(userId);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var cacheKey = userId.ToString("N", CultureInfo.InvariantCulture);
        if (file.ApplyEdits != false && file.ShowEditedBadge != false && !file.RestrictToCategories)
        {
            _cache.TryRemove(cacheKey, out _);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        _cache[cacheKey] = file;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
    }

    private static string? GetFilePath(Guid userId)
    {
        var root = Plugin.Instance?.DataFolderPath;
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        return Path.Combine(root, "overrides", userId.ToString("N", CultureInfo.InvariantCulture) + ".json");
    }

    private static UserItemEditOverrides Normalize(UserItemEditOverrides? overrides)
    {
        var ids = overrides?.EnabledCategories?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        var result = new UserItemEditOverrides
        {
            ApplyEdits = overrides?.ApplyEdits != false,
            ShowEditedBadge = overrides?.ShowEditedBadge != false,
            RestrictToCategories = overrides?.RestrictToCategories == true
        };
        foreach (var id in ids)
        {
            result.EnabledCategories.Add(id);
        }

        if (!result.RestrictToCategories)
        {
            result.EnabledCategories.Clear();
        }

        return result;
    }

    private static UserItemEditOverrides Clone(UserOverrideFile stored)
    {
        var clone = new UserItemEditOverrides
        {
            ApplyEdits = stored.ApplyEdits ?? true,
            ShowEditedBadge = stored.ShowEditedBadge ?? true,
            RestrictToCategories = stored.RestrictToCategories
        };
        foreach (var id in stored.EnabledCategories ?? [])
        {
            clone.EnabledCategories.Add(id);
        }

        return clone;
    }

    private static UserOverrideFile ToFile(UserItemEditOverrides overrides)
    {
        var file = new UserOverrideFile
        {
            ApplyEdits = overrides.ApplyEdits,
            ShowEditedBadge = overrides.ShowEditedBadge,
            RestrictToCategories = overrides.RestrictToCategories,
            EnabledCategories = [.. overrides.EnabledCategories]
        };
        return file;
    }

    /// <summary>
    /// On-disk per-user filter file.
    /// </summary>
    private sealed class UserOverrideFile
    {
        /// <summary>
        /// Gets or sets whether sidecar edits run on playback. Null means default true.
        /// </summary>
        public bool? ApplyEdits { get; set; }

        /// <summary>
        /// Gets or sets whether the start-of-stream "Edited" badge is burned in.
        /// Null means default true.
        /// </summary>
        public bool? ShowEditedBadge { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether only <see cref="EnabledCategories"/> apply.
        /// </summary>
        public bool RestrictToCategories { get; set; }

        /// <summary>
        /// Gets or sets minor category ids to apply when restricting.
        /// </summary>
        public List<string>? EnabledCategories { get; set; } = [];
    }
}
