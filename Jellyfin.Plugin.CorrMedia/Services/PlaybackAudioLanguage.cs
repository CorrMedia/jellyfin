using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Resolves the audio-stream language for the current playback.
/// An explicit stream index wins. Otherwise the request query is used, then the user's default track.
/// </summary>
public sealed class PlaybackAudioLanguage
{
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackAudioLanguage"/> class.
    /// </summary>
    /// <param name="userManager">User manager.</param>
    /// <param name="userDataManager">Per-item user data, for a remembered audio index.</param>
    /// <param name="httpContextAccessor">Current request, for a playback-info audio index.</param>
    public PlaybackAudioLanguage(
        IUserManager userManager,
        IUserDataManager userDataManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _userDataManager = userDataManager ?? throw new ArgumentNullException(nameof(userDataManager));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Returns the ISO 639 language of the audio stream that should filter sidecar edits.
    /// </summary>
    /// <param name="item">Library item being played.</param>
    /// <param name="userId">Playing user. Empty skips account preferences.</param>
    /// <param name="audioStreamIndex">Stream index from the encode request, or null to use the request query and then the default track.</param>
    /// <returns>Selected stream language, or null when it cannot be determined.</returns>
    public string? Resolve(BaseItem? item, Guid userId, int? audioStreamIndex)
    {
        if (item is null)
        {
            return null;
        }

        var tracks = item.GetMediaStreams()
            .Where(stream => stream.Type == MediaStreamType.Audio)
            .Select(stream => new PlaybackAudioSelector.AudioTrack(stream.Index, stream.Language, stream.IsDefault, stream.IsOriginal))
            .ToList();

        var requested = audioStreamIndex ?? TryGetRequestAudioStreamIndex();
        var preference = PreferenceFor(item, userId);
        return PlaybackAudioSelector.SelectLanguage(
            tracks,
            preference,
            item.GetInheritedOriginalLanguage(),
            requested);
    }

    private PlaybackAudioSelector.AudioPreference PreferenceFor(BaseItem item, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return new PlaybackAudioSelector.AudioPreference(null, PlayDefaultTrack: true, RememberedIndex: null);
        }

        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return new PlaybackAudioSelector.AudioPreference(null, PlayDefaultTrack: true, RememberedIndex: null);
        }

        int? remembered = null;
        if (user.RememberAudioSelections && item.EnableRememberingTrackSelections)
        {
            var stored = _userDataManager.GetUserData(user, item)?.AudioStreamIndex;
            if (stored.HasValue)
            {
                remembered = stored.Value;
            }
        }

        return new PlaybackAudioSelector.AudioPreference(user.AudioLanguagePreference, user.PlayDefaultAudioTrack, remembered);
    }

    private int? TryGetRequestAudioStreamIndex()
    {
        var value = _httpContextAccessor.HttpContext?.Request.Query["audioStreamIndex"].FirstOrDefault();
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : null;
    }
}
