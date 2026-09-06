#if PATCHED_CORE

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Rewrites HLS / external text subtitles onto the edited (post-cut) timeline
/// when this user has remaining skips.
/// </summary>
public sealed class SessionSubtitleCueRewriter : ISessionSubtitleCueRewriter
{
    private readonly ILibraryManager _libraryManager;
    private readonly EditOverrideStore _editOverrideStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionSubtitleCueRewriter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionSubtitleCueRewriter"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="editOverrideStore">Per-user apply/filter store.</param>
    /// <param name="httpContextAccessor">Current request, for the playing user.</param>
    /// <param name="logger">Logger.</param>
    public SessionSubtitleCueRewriter(
        ILibraryManager libraryManager,
        EditOverrideStore editOverrideStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SessionSubtitleCueRewriter> logger)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _editOverrideStore = editOverrideStore ?? throw new ArgumentNullException(nameof(editOverrideStore));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SessionSubtitleCueRewriter registered (VTT/SRT on edited timeline)");
    }

    /// <inheritdoc />
    public bool NeedsRewrite(string? itemId, string? format)
        => CorrSubtitleText.IsRewritableFormat(format)
            && TryGetAppliedEdits(itemId, out var edits)
            && edits.Skips.Count > 0;

    /// <inheritdoc />
    public async Task<Stream> RewriteAsync(
        string itemId,
        string format,
        Stream originalFullTrack,
        long startPositionTicks,
        long endPositionTicks,
        bool copyTimestamps,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalFullTrack);

        string text;
        using (var reader = new StreamReader(originalFullTrack, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!CorrSubtitleText.IsRewritableFormat(format)
            || !TryGetAppliedEdits(itemId, out var edits)
            || edits.Skips.Count == 0)
        {
            return FromString(text);
        }

        if (string.IsNullOrEmpty(itemId) || !Guid.TryParse(itemId, out var guid))
        {
            return FromString(text);
        }

        var durationTicks = _libraryManager.GetItemById(guid)?.RunTimeTicks;
        if (!durationTicks.HasValue || durationTicks.Value <= 0)
        {
            _logger.LogWarning("CorrMedia subtitle rewrite skipped; missing duration for item {ItemId}", itemId);
            return FromString(text);
        }

        var cues = CorrSubtitleText.Parse(text, format);
        if (cues.Count == 0)
        {
            _logger.LogWarning(
                "CorrMedia subtitle parse produced no cues for item {ItemId} format {Format}; serving remapped-empty track",
                itemId,
                format);
            return FromString(CorrSubtitleText.Write([], format));
        }

        var durationSeconds = durationTicks.Value / (double)TimeSpan.TicksPerSecond;
        var keep = CorrTimeline.BuildKeepRanges(edits.Skips, durationSeconds);
        var remapped = CorrCueRemapper.Remap(cues, keep);
        var windowed = CorrCueRemapper.Window(
            remapped,
            startPositionTicks / (double)TimeSpan.TicksPerSecond,
            endPositionTicks / (double)TimeSpan.TicksPerSecond,
            copyTimestamps);

        _logger.LogInformation(
            "CorrMedia remapped {InputCount} subtitle cues to {OutputCount} (skips={SkipCount}) for item {ItemId} format {Format}",
            cues.Count,
            windowed.Count,
            edits.Skips.Count,
            itemId,
            format);

        return FromString(CorrSubtitleText.Write(windowed, format));
    }

    private bool TryGetAppliedEdits(string? itemId, out CorrEdits edits)
        => CorrPlaybackEvaluator.TryGetAppliedEdits(
            _libraryManager,
            _editOverrideStore,
            itemId,
            TryGetRequestUserId(),
            out edits,
            _logger);

    private Guid TryGetRequestUserId()
    {
        var value = _httpContextAccessor.HttpContext?.User?.Claims
            .FirstOrDefault(c => string.Equals(c.Type, "Jellyfin-UserId", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }

    private static MemoryStream FromString(string text)
        => new MemoryStream(Encoding.UTF8.GetBytes(text));
}

#endif
