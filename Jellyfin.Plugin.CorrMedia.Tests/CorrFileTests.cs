using Jellyfin.Plugin.CorrMedia.Models;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class CorrFileTests
{
    [Fact]
    public void GetPath_ReplacesExtensionWithCorrJson()
    {
        var sidecar = CorrFile.GetPath(Path.Combine("movies", "Title.mkv"));
        Assert.Equal("Title.corr.json", Path.GetFileName(sidecar));
        Assert.Equal("movies", Path.GetFileName(Path.GetDirectoryName(sidecar)));
    }

    [Fact]
    public void Parse_MissingFile_ReturnsEmpty()
    {
        var edits = CorrFile.Parse(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".corr.json"));
        Assert.False(edits.HasPlaybackEdits);
        Assert.Empty(edits.All);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmpty()
    {
        using var file = new TempCorrFile("{ not json");
        var logger = new CollectingLogger();
        var edits = CorrFile.Parse(file.FilePath, logger);
        Assert.False(edits.HasPlaybackEdits);
        Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
        Assert.Contains(logger.Entries, e => e.Exception is System.Text.Json.JsonException);
    }

    [Fact]
    public void Parse_MissingFile_LogsWarningWhenLoggerProvided()
    {
        var logger = new CollectingLogger();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".corr.json");
        var edits = CorrFile.Parse(path, logger);
        Assert.False(edits.HasPlaybackEdits);
        Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmpty()
    {
        using var file = new TempCorrFile("""{ "schema_version": "1.0" }""");
        var edits = CorrFile.Parse(file.FilePath);
        Assert.Same(CorrEdits.Empty, edits);
    }

    [Fact]
    public void Parse_IgnoresUnknownActionsAndInvertedRanges()
    {
        using var file = new TempCorrFile(
            """
            {
              "edits": [
                { "id": "ok", "start": 1, "end": 2, "action": "mute" },
                { "id": "poi", "start": 3, "end": 4, "action": "poi" },
                { "id": "bad", "start": 9, "end": 8, "action": "skip" },
                { "id": "blank", "start": 10, "end": 10, "action": "mute" }
              ]
            }
            """);
        var edits = CorrFile.Parse(file.FilePath);
        Assert.Single(edits.Mutes);
        Assert.Empty(edits.Skips);
        Assert.Equal("ok", edits.Mutes[0].Id);
        Assert.Single(edits.All);
    }

    [Fact]
    public void Parse_CanonicalizesAliasesAndDefaults()
    {
        using var file = new TempCorrFile(
            """
            {
              "edits": [
                { "start": 1, "end": 2, "action": "punch", "scale": 3, "x": 0.2, "y": 0.4 },
                { "start": 3, "end": 4, "action": "boxblur" },
                { "start": 5, "end": 6, "action": "blackout" },
                { "start": 7, "end": 8, "action": "mosaic" },
                { "start": 9, "end": 10, "action": "volume" },
                { "start": 11, "end": 12, "action": "beep" }
              ]
            }
            """);
        var edits = CorrFile.Parse(file.FilePath);
        Assert.Equal(4, edits.VideoEffects.Count);
        Assert.Equal(VideoEffectKind.Zoom, edits.VideoEffects[0].Kind);
        Assert.Equal(3, edits.VideoEffects[0].Scale);
        Assert.Equal(VideoEffectKind.Blur, edits.VideoEffects[1].Kind);
        Assert.Equal(8, edits.VideoEffects[1].BlurRadius);
        Assert.Equal(VideoEffectKind.Cover, edits.VideoEffects[2].Kind);
        Assert.Equal(VideoEffectKind.Pixelate, edits.VideoEffects[3].Kind);
        Assert.Equal(16, edits.VideoEffects[3].BlockSize);
        Assert.Equal(2, edits.Mutes.Count);
        Assert.Equal(AudioEditKind.Volume, edits.Mutes[0].Kind);
        Assert.Equal(0.2, edits.Mutes[0].Gain);
        Assert.Equal(AudioEditKind.Beep, edits.Mutes[1].Kind);
        Assert.Equal(0.3, edits.Mutes[1].Gain);
        Assert.Equal(1000, edits.Mutes[1].Frequency);
        Assert.Equal(["zoom", "blur", "cover", "pixelate", "volume", "beep"], edits.All.Select(d => d.Action).ToArray());
    }

    [Fact]
    public void Parse_NormalizesChannelsAndDedupesIds()
    {
        using var file = new TempCorrFile(
            """
            {
              "edits": [
                { "id": "same", "start": 0, "end": 1, "action": "mute", "channels": ["fc", " FC ", "fl"] },
                { "id": "same", "start": 2, "end": 3, "action": "skip" }
              ]
            }
            """);
        var edits = CorrFile.Parse(file.FilePath);
        Assert.Equal(["FC", "FL"], edits.Mutes[0].NormalizedChannels);
        Assert.Equal("same", edits.Mutes[0].Id);
        Assert.Equal("same#2", edits.Skips[0].Id);
    }

    [Fact]
    public void Parse_AcceptsCommentsTrailingCommasAndCategories()
    {
        using var file = new TempCorrFile(
            """
            {
              // comments are allowed
              "edits": [
                {
                  "id": "e1",
                  "start": 1,
                  "end": 2,
                  "action": "mute",
                  "categories": ["Profanity", "profanity", ""],
                  "description": " dialogue "
                },
              ]
            }
            """);
        var edits = CorrFile.Parse(file.FilePath);
        Assert.Equal(["Profanity"], edits.All[0].Categories);
        Assert.Equal("dialogue", edits.All[0].Description);
    }

    [Fact]
    public void Parse_ClampsBoxToFrameAndIgnoresDegenerateBox()
    {
        using var file = new TempCorrFile(
            """
            {
              "edits": [
                { "start": 1, "end": 2, "action": "cover", "box": { "x": 0.8, "y": 0.8, "width": 0.5, "height": 0.5 } },
                { "start": 3, "end": 4, "action": "cover", "box": { "x": 0.1, "y": 0.1, "width": 0, "height": 0.2 } }
              ]
            }
            """);
        var edits = CorrFile.Parse(file.FilePath);
        Assert.Equal(2, edits.VideoEffects.Count);
        var box = edits.VideoEffects[0].Box;
        Assert.NotNull(box);
        Assert.Equal(0.8, box.X);
        Assert.Equal(0.2, box.Width, 3);
        Assert.Null(edits.VideoEffects[1].Box);
    }

    [Fact]
    public void Parse_TestMovieSidecar_LoadsEveryPlaybackAction()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "test-movie.corr.json");
        var edits = CorrFile.Parse(path);
        Assert.True(edits.HasPlaybackEdits);
        Assert.Equal(5, edits.Mutes.Count);
        Assert.Single(edits.Skips);
        Assert.Equal(6, edits.VideoEffects.Count);
        Assert.Equal(12, edits.All.Count);
        Assert.Contains(edits.Mutes, m => m.Kind == AudioEditKind.Volume);
        Assert.Contains(edits.Mutes, m => m.Kind == AudioEditKind.Beep);
        Assert.Contains(edits.Mutes, m => m.IsSelectiveMute && m.NormalizedChannels.SequenceEqual(["FC"]));
        Assert.Contains(edits.VideoEffects, v => v.Kind == VideoEffectKind.Zoom && v.CenterXEnd == 0.7);
        Assert.Contains(edits.VideoEffects, v => v.Kind == VideoEffectKind.Crop);
        Assert.Contains(edits.VideoEffects, v => v.Kind == VideoEffectKind.Cover && v.BoxEnd is not null);
        Assert.Contains(edits.VideoEffects, v => v.Kind == VideoEffectKind.Pixelate && v.BlockSize == 24);
        Assert.Contains(edits.VideoEffects, v => v.Kind == VideoEffectKind.Blank);
        Assert.Contains(edits.VideoEffects, v => v.Kind == VideoEffectKind.Blur && v.BlurRadius == 150);
    }

    [Fact]
    public void Parse_ExampleMixed_SeparatesMuteAndSkip()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "example_mixed.corr.json");
        var edits = CorrFile.Parse(path);
        Assert.Equal(2, edits.Mutes.Count);
        Assert.Single(edits.Skips);
        Assert.Equal(300, edits.Skips[0].StartTime);
        Assert.Equal(600, edits.Skips[0].EndTime);
    }

    [Fact]
    public void Parse_ExampleExtraActions_LoadsCoverCropPixelateBlankVolumeBeep()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "example_extra_actions.corr.json");
        var edits = CorrFile.Parse(path);
        Assert.Equal(["cover", "crop", "pixelate", "blank"], edits.VideoEffects.Select(v => v.Kind.ToString().ToLowerInvariant()).ToArray());
        Assert.Equal(AudioEditKind.Volume, edits.Mutes[0].Kind);
        Assert.Equal(AudioEditKind.Beep, edits.Mutes[1].Kind);
    }
}
