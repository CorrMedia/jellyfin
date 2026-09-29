using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Picks the audio-stream language Jellyfin would play.
/// A requested index wins, then a remembered index, then the default-track and language-preference rules.
/// </summary>
internal static class PlaybackAudioSelector
{
    /// <summary>
    /// Returns the language of the audio stream that should drive sidecar filtering.
    /// </summary>
    /// <param name="tracks">Audio streams on the item.</param>
    /// <param name="preference">User audio preference.</param>
    /// <param name="originalLanguage">Item original language, used when the preference is <c>OriginalLanguage</c>.</param>
    /// <param name="requestedIndex">Stream index from the playback request, if the client sent one.</param>
    /// <returns>The selected stream language, or null when it is missing or the requested index is not an audio stream.</returns>
    public static string? SelectLanguage(
        IReadOnlyList<AudioTrack> tracks,
        AudioPreference preference,
        string? originalLanguage,
        int? requestedIndex)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        if (tracks.Count == 0)
        {
            return null;
        }

        if (requestedIndex.HasValue)
        {
            return LanguageOf(FindIndex(tracks, requestedIndex.Value));
        }

        if (preference.RememberedIndex is int remembered)
        {
            var rememberedTrack = FindIndex(tracks, remembered);
            if (rememberedTrack.HasValue)
            {
                return LanguageOf(rememberedTrack);
            }
        }

        return LanguageOf(ChooseDefault(tracks, preference, originalLanguage));
    }

    private static AudioTrack? ChooseDefault(
        IReadOnlyList<AudioTrack> tracks,
        AudioPreference preference,
        string? originalLanguage)
    {
        if (string.Equals(preference.Language, "OriginalLanguage", StringComparison.OrdinalIgnoreCase))
        {
            if (preference.PlayDefaultTrack)
            {
                return FirstDefault(tracks) ?? BestMatch(tracks, originalLanguage) ?? tracks[0];
            }

            var original = FirstOriginal(tracks);
            if (original.HasValue
                && (string.IsNullOrWhiteSpace(originalLanguage)
                    || string.IsNullOrWhiteSpace(original.Value.Language)
                    || SpokenLanguage.Same(original.Value.Language, originalLanguage)))
            {
                return original;
            }

            return BestMatch(tracks, originalLanguage) ?? tracks[0];
        }

        if (preference.PlayDefaultTrack)
        {
            var preferredDefault = FirstDefault(tracks);
            if (preferredDefault.HasValue)
            {
                return preferredDefault;
            }
        }

        if (!string.IsNullOrWhiteSpace(preference.Language))
        {
            var match = BestMatch(tracks, preference.Language);
            if (match.HasValue)
            {
                return match;
            }
        }

        return FirstDefault(tracks) ?? tracks[0];
    }

    private static AudioTrack? BestMatch(IReadOnlyList<AudioTrack> tracks, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        AudioTrack? fallback = null;
        foreach (var track in tracks)
        {
            if (!SpokenLanguage.Same(language, track.Language))
            {
                continue;
            }

            if (track.IsDefault)
            {
                return track;
            }

            fallback ??= track;
        }

        return fallback;
    }

    private static AudioTrack? FirstDefault(IReadOnlyList<AudioTrack> tracks)
    {
        foreach (var track in tracks)
        {
            if (track.IsDefault)
            {
                return track;
            }
        }

        return null;
    }

    private static AudioTrack? FirstOriginal(IReadOnlyList<AudioTrack> tracks)
    {
        foreach (var track in tracks)
        {
            if (track.IsOriginal)
            {
                return track;
            }
        }

        return null;
    }

    private static AudioTrack? FindIndex(IReadOnlyList<AudioTrack> tracks, int index)
    {
        foreach (var track in tracks)
        {
            if (track.Index == index)
            {
                return track;
            }
        }

        return null;
    }

    private static string? LanguageOf(AudioTrack? track)
    {
        if (!track.HasValue || string.IsNullOrWhiteSpace(track.Value.Language))
        {
            return null;
        }

        return track.Value.Language.Trim();
    }

    /// <summary>
    /// One audio stream that can be selected for playback.
    /// </summary>
    /// <param name="Index">Jellyfin media stream index.</param>
    /// <param name="Language">ISO 639 code stored on the stream, if any.</param>
    /// <param name="IsDefault">True when the file marks this stream as the default.</param>
    /// <param name="IsOriginal">True when the file marks this stream as the original language.</param>
    internal readonly record struct AudioTrack(int Index, string? Language, bool IsDefault, bool IsOriginal);

    /// <summary>
    /// The user's audio choice, already resolved from account settings and remembered per-title data.
    /// </summary>
    /// <param name="Language">Account audio language, or <c>OriginalLanguage</c>. Null when unset.</param>
    /// <param name="PlayDefaultTrack">True when Jellyfin should prefer the file's default audio track.</param>
    /// <param name="RememberedIndex">Per-title audio index when remembering selections is on and the index is still valid.</param>
    internal readonly record struct AudioPreference(string? Language, bool PlayDefaultTrack, int? RememberedIndex);
}
