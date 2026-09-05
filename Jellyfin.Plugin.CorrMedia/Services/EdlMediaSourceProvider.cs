using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CorrMedia.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Adds a "{Title} (Edited)" media source when a sidecar EDL exists.
/// </summary>
public sealed class EdlMediaSourceProvider : IMediaSourceProvider
{
    private readonly ILogger<EdlMediaSourceProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EdlMediaSourceProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public EdlMediaSourceProvider(ILogger<EdlMediaSourceProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("EdlMediaSourceProvider registered (dual delivery)");
    }

    /// <inheritdoc />
    public Task<IEnumerable<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (item is null || item is Audio || string.IsNullOrEmpty(item.Path))
        {
            return Task.FromResult(Enumerable.Empty<MediaSourceInfo>());
        }

        var edlPath = EdlFile.GetPath(item.Path);
        if (!File.Exists(edlPath))
        {
            return Task.FromResult(Enumerable.Empty<MediaSourceInfo>());
        }

        var (mutes, skips) = EdlFile.ParseMuteAndSkip(edlPath);
        if (mutes.Count == 0 && skips.Count == 0)
        {
            return Task.FromResult(Enumerable.Empty<MediaSourceInfo>());
        }

        var source = BuildEditedSource(item, skips);
        _logger.LogDebug(
            "EDL media source for {ItemName}: mutes={Mutes} skips={Skips} runTimeTicks={Ticks}",
            source.Name,
            mutes.Count,
            skips.Count,
            source.RunTimeTicks);

        return Task.FromResult<IEnumerable<MediaSourceInfo>>([source]);
    }

    /// <inheritdoc />
    public Task<ILiveStream> OpenMediaSource(string openToken, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    private static MediaSourceInfo BuildEditedSource(BaseItem item, IReadOnlyList<MuteTimeRange> skips)
    {
        // Match Jellyfin's static MediaSource name (filename stem), then append (Edited).
        // MediaSourceManager may refine this from the primary static source Name.
        var baseName = GetJellyfinStyleSourceName(item);
        var editedTicks = ComputeEditedRunTimeTicks(item.RunTimeTicks, skips);

        var info = new MediaSourceInfo
        {
            Id = EdlMediaSourceIds.ForItem(item.Id),
            Name = baseName + " (Edited)",
            Path = item.Path,
            Protocol = item.PathProtocol ?? MediaProtocol.File,
            Container = item.Container,
            Size = item.Size,
            RunTimeTicks = editedTicks,
            Type = MediaSourceType.Default,
            SupportsDirectPlay = false,
            SupportsDirectStream = false,
            SupportsTranscoding = true,
            RequiresOpening = false,
            MediaStreams = item.GetMediaStreams().ToList(),
        };

        if (string.IsNullOrEmpty(info.Container) && !string.IsNullOrEmpty(item.Path))
        {
            info.Container = Path.GetExtension(item.Path).TrimStart('.');
        }

        if (item is Video video)
        {
            info.VideoType = video.VideoType;
            info.IsoType = video.IsoType;
            info.Video3DFormat = video.Video3DFormat;
            info.Timestamp = video.Timestamp;
        }

        return info;
    }

    /// <summary>
    /// Mirrors Jellyfin's typical static source label: file stem when on disk, else item name.
    /// </summary>
    private static string GetJellyfinStyleSourceName(BaseItem item)
    {
        if (item.IsFileProtocol && !string.IsNullOrEmpty(item.Path))
        {
            var stem = Path.GetFileNameWithoutExtension(item.Path);
            if (!string.IsNullOrWhiteSpace(stem))
            {
                return stem;
            }
        }

        return string.IsNullOrWhiteSpace(item.Name)
            ? Path.GetFileNameWithoutExtension(item.Path) ?? "Edited"
            : item.Name;
    }

    private static long? ComputeEditedRunTimeTicks(long? originalTicks, IReadOnlyList<MuteTimeRange> skips)
    {
        if (!originalTicks.HasValue || originalTicks.Value <= 0)
        {
            return originalTicks;
        }

        var skipSeconds = skips.Sum(s => Math.Max(0, s.EndTime - s.StartTime));
        var skipTicks = (long)(skipSeconds * TimeSpan.TicksPerSecond);
        return Math.Max(TimeSpan.TicksPerSecond, originalTicks.Value - skipTicks);
    }
}
