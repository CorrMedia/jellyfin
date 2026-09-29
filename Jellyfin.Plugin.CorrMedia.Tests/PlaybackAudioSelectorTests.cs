using Jellyfin.Plugin.CorrMedia.Services;

namespace Jellyfin.Plugin.CorrMedia.Tests;

public sealed class PlaybackAudioSelectorTests
{
    private static readonly PlaybackAudioSelector.AudioTrack[] Tracks =
    [
        new(1, "eng", IsDefault: true, IsOriginal: true),
        new(2, "spa", IsDefault: false, IsOriginal: false)
    ];

    [Fact]
    public void SelectLanguage_UsesTheRequestedStreamIndex()
    {
        var language = PlaybackAudioSelector.SelectLanguage(
            Tracks,
            new PlaybackAudioSelector.AudioPreference("eng", PlayDefaultTrack: true, RememberedIndex: 1),
            "eng",
            requestedIndex: 2);
        Assert.Equal("spa", language);
    }

    [Fact]
    public void SelectLanguage_UsesARememberedIndexBeforeTheAccountPreference()
    {
        var language = PlaybackAudioSelector.SelectLanguage(
            Tracks,
            new PlaybackAudioSelector.AudioPreference("eng", PlayDefaultTrack: true, RememberedIndex: 2),
            "eng",
            requestedIndex: null);
        Assert.Equal("spa", language);
    }

    [Fact]
    public void SelectLanguage_PrefersTheFileDefaultWhenThatSettingIsOn()
    {
        var language = PlaybackAudioSelector.SelectLanguage(
            Tracks,
            new PlaybackAudioSelector.AudioPreference("spa", PlayDefaultTrack: true, RememberedIndex: null),
            "eng",
            requestedIndex: null);
        Assert.Equal("eng", language);
    }

    [Fact]
    public void SelectLanguage_FollowsTheAccountLanguageWhenTheFileDefaultIsOff()
    {
        var language = PlaybackAudioSelector.SelectLanguage(
            Tracks,
            new PlaybackAudioSelector.AudioPreference("es", PlayDefaultTrack: false, RememberedIndex: null),
            "eng",
            requestedIndex: null);
        Assert.Equal("spa", language);
    }

    [Fact]
    public void SelectLanguage_RequestedMissingIndexDoesNotFallBack()
    {
        var language = PlaybackAudioSelector.SelectLanguage(
            Tracks,
            new PlaybackAudioSelector.AudioPreference("spa", PlayDefaultTrack: false, RememberedIndex: null),
            "eng",
            requestedIndex: 9);
        Assert.Null(language);
    }
}
