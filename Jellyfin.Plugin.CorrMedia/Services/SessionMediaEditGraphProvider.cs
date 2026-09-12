#if PATCHED_CORE

using System;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CorrMedia.Services;

/// <summary>
/// Supplies FFmpeg graphs (video/audio effects, then cut) for patched Jellyfin.
/// </summary>
public sealed class SessionMediaEditGraphProvider : ISessionMediaEditGraphProvider
{
    private readonly CorrEditStore _corrEditStore;
    private readonly ILogger<SessionMediaEditGraphProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionMediaEditGraphProvider"/> class.
    /// </summary>
    /// <param name="corrEditStore">Sidecar edit store.</param>
    /// <param name="logger">Logger.</param>
    public SessionMediaEditGraphProvider(CorrEditStore corrEditStore, ILogger<SessionMediaEditGraphProvider> logger)
    {
        _corrEditStore = corrEditStore ?? throw new ArgumentNullException(nameof(corrEditStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("SessionMediaEditGraphProvider registered (effects-then-cut)");
    }

    /// <inheritdoc />
    public bool HasEditGraph(string? playSessionId, string? deviceId)
    {
        var plan = _corrEditStore.GetPlan(playSessionId, deviceId);
        return plan?.NeedsEditGraph == true;
    }

    /// <inheritdoc />
    public SessionMediaEditGraph? GetEditGraph(string? playSessionId, string? deviceId, SessionMediaEditGraphContext context)
    {
        var plan = _corrEditStore.GetPlan(playSessionId, deviceId);
        if (plan is null || !plan.NeedsEditGraph)
        {
            return null;
        }

        // Prefer ffprobe'd layout from plan load — library AudioStream can stay stale after remux.
        var inputLayout = !string.IsNullOrWhiteSpace(plan.SourceChannelLayout)
            ? plan.SourceChannelLayout
            : context.InputChannelLayout;
        var inputChannels = plan.SourceChannelCount > 0
            ? plan.SourceChannelCount
            : context.InputChannelCount;
        // Downmix only when the real source is multi-channel and the client asked for stereo.
        var outputChannels = context.OutputAudioChannels;
        var stereoDownmix = inputChannels > 2 && outputChannels == 2
            ? context.StereoDownmixFilter
            : null;
        if (stereoDownmix is null && inputChannels > 2 && outputChannels == 2)
        {
            stereoDownmix = "aformat=channel_layouts=stereo";
        }

        var originalDuration = CorrTimeline.ResolveOriginalDuration(plan, context.DurationSeconds);
        var graph = CorrFilterComplexBuilder.Build(
            plan,
            originalDuration,
            context.StartTimeSeconds,
            inputLayout,
            inputChannels,
            outputChannels,
            stereoDownmix,
            context.BurnInGraphicalSubtitleInputIndex ?? 0,
            context.BurnInGraphicalSubtitleStreamIndex,
            context.BurnInGraphicalSubtitleFilters,
            context.BurnInTextSubtitleFilter);
        if (graph is not null)
        {
            _logger.LogInformation(
                "Sidecar edit graph PlaySessionId={PlaySessionId} originalDuration={Duration} mediaSourceDuration={SourceDuration} editedStart={Start} inputSeek={InputSeek} layout={Layout} channels={Channels}->{OutChannels} selectiveMute={Selective} pgsBurnIn={PgsInput}:{PgsStream} textBurnIn={Text} complexLength={Len}",
                playSessionId ?? "(null)",
                originalDuration,
                context.DurationSeconds,
                context.StartTimeSeconds,
                graph.InputSeekSeconds,
                inputLayout ?? "(null)",
                inputChannels,
                outputChannels,
                plan.HasSelectiveMute,
                context.BurnInGraphicalSubtitleInputIndex,
                context.BurnInGraphicalSubtitleStreamIndex,
                !string.IsNullOrEmpty(context.BurnInTextSubtitleFilter),
                graph.FilterComplex.Length);
        }

        return graph;
    }
}

#endif
