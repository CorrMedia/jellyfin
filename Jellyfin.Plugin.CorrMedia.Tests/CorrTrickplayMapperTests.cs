using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.CorrMedia.Models;
using Jellyfin.Plugin.CorrMedia.Services;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrTrickplayMapperTests
{
    private static IReadOnlyList<(double Start, double End)> Skip15To20Keep(double duration = 90)
        => CorrTimeline.BuildKeepRanges([new MuteTimeRange(15, 20)], duration);

    private static TrickplayInfoDto OriginalDto(int thumbnailCount = 90, int intervalMs = 1000, int tileWidth = 10, int tileHeight = 10)
        => new(new TrickplayInfo
        {
            Width = 320,
            Height = 180,
            TileWidth = tileWidth,
            TileHeight = tileHeight,
            ThumbnailCount = thumbnailCount,
            Interval = intervalMs,
            Bandwidth = 10000
        });

    [Fact]
    public void EditedThumbnailCount_MatchesEditedDuration()
    {
        var original = TimeSpan.FromSeconds(90).Ticks;
        var edited = CorrPlaybackEvaluator.EditedRunTimeTicks(original, [new MuteTimeRange(15, 20)]);
        Assert.NotNull(edited);
        Assert.Equal(85, CorrTrickplayMapper.EditedThumbnailCount(edited.Value, 1000, 90));
    }

    [Fact]
    public void RewriteDto_BecomesOneByOneWithEditedCount()
    {
        var dto = CorrTrickplayMapper.RewriteDto(OriginalDto(), TimeSpan.FromSeconds(85).Ticks);
        Assert.Equal(1, dto.TileWidth);
        Assert.Equal(1, dto.TileHeight);
        Assert.Equal(85, dto.ThumbnailCount);
        Assert.Equal(1000, dto.Interval);
        Assert.Equal(100, dto.Bandwidth);
    }

    [Fact]
    public void TryMapEditedIndex_AtFifteenSeconds_MapsToOriginalTwentyNotSkipInterior()
    {
        Assert.True(CorrTrickplayMapper.TryMapEditedIndex(
            15,
            1000,
            90,
            10,
            10,
            Skip15To20Keep(),
            out var tileIndex,
            out var column,
            out var row));

        // Edited slot 15s is the [15,16) bucket; midpoint maps to original 20.5s (after the skip).
        Assert.Equal(0, tileIndex);
        Assert.Equal(0, column);
        Assert.Equal(2, row);
    }

    [Fact]
    public void TryMapEditedIndex_BeforeSkip_Unchanged()
    {
        Assert.True(CorrTrickplayMapper.TryMapEditedIndex(
            14,
            1000,
            90,
            10,
            10,
            Skip15To20Keep(),
            out var tileIndex,
            out var column,
            out var row));

        Assert.Equal(0, tileIndex);
        Assert.Equal(4, column);
        Assert.Equal(1, row);
    }

    [Fact]
    public void TryMapEditedIndex_NeverSamplesSkipInterior()
    {
        var keep = Skip15To20Keep();
        for (var editedIndex = 0; editedIndex < 85; editedIndex++)
        {
            Assert.True(CorrTrickplayMapper.TryMapEditedIndex(
                editedIndex,
                1000,
                90,
                10,
                10,
                keep,
                out var tileIndex,
                out var column,
                out var row));

            var originalIndex = (tileIndex * 100) + (row * 10) + column;
            var originalSeconds = originalIndex;
            Assert.False(
                originalSeconds > 15 && originalSeconds < 20,
                $"Edited index {editedIndex} mapped to original {originalSeconds}s inside the skip.");
        }
    }

    [Fact]
    public void TryMapEditedIndex_ApplyEditsOffKeepAll_StillMaps()
    {
        var keep = CorrTimeline.BuildKeepRanges([], 90);
        Assert.True(CorrTrickplayMapper.TryMapEditedIndex(15, 1000, 90, 10, 10, keep, out var tileIndex, out var column, out var row));
        Assert.Equal(0, tileIndex);
        Assert.Equal(5, column);
        Assert.Equal(1, row);
    }

    [Fact]
    public void BuildHlsPlaylist_DurationMatchesEditedCount()
    {
        var playlist = CorrTrickplayMapper.BuildHlsPlaylist(
            CorrTrickplayMapper.RewriteDto(OriginalDto(), TimeSpan.FromSeconds(85).Ticks),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "token");

        Assert.NotNull(playlist);
        Assert.Contains("#EXT-X-IMAGES-ONLY", playlist, StringComparison.Ordinal);
        Assert.Contains("LAYOUT=1x1", playlist, StringComparison.Ordinal);
        Assert.Equal(85, CountOccurrences(playlist, "#EXTINF:"));
        Assert.Contains("84.jpg?MediaSourceId=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa&ApiKey=token", playlist, StringComparison.Ordinal);
        Assert.DoesNotContain("85.jpg", playlist, StringComparison.Ordinal);
    }

    [Fact]
    public void EditedThumbnailCount_NoCuts_Unchanged()
    {
        Assert.Equal(90, CorrTrickplayMapper.EditedThumbnailCount(TimeSpan.FromSeconds(90).Ticks, 1000, 90));
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}
