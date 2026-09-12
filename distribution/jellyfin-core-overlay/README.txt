Server-side mute + cut: Jellyfin core overlay (10.11.11)
=============================================

Adds:
- ISessionAudioFilterProvider — mute-only -af injection
- ISessionMediaEditGraphProvider — effects-then-cut -filter_complex (mute/zoom/blur first, then cut; times on original source)
- ISessionMuteRangeLoader — pre-stream sidecar load when the user has edits enabled
- ISessionCorrDeliveryHint — apply-edits detection on the primary source, force HLS when cuts exist, shortened runtime
- ISessionSubtitleCueRewriter — remap HLS / external VTT and SRT cues onto the edited timeline
- ISessionTrickplayRewriter / ITrickplayCellCropper — remap trickplay duration and tile cells onto the edited timeline
- ISessionChapterRewriter — remap BaseItemDto chapter markers onto the edited timeline
- SessionEditGraphHwBridge — HW decode hwdownload (when known) + CPU graph → format/hwupload before GetVideoEncoder
- EncodingHelper / VideosController / DynamicHlsController / DynamicHlsHelper / StreamingHelpers / MediaInfoHelper / DtoService / MediaSourceManager / UserLibraryController / SubtitleController / TrickplayController / CoreAppHost wiring

When a `.corr.json` sidecar exists and the user has apply-edits on, the normal library item
transcodes with the graph. Titles and media-source names get " (Edited)". There is no second
MediaSource. PackageController falls back to installed-plugin metadata so sideloaded CorrMedia
does not 404 the dashboard plugin details page.

Apply:
  powershell -File scripts/apply-core-overlay.ps1 -JellyfinSourcePath C:\path\to\jellyfin

Deploy CorrMedia. See distribution/APPLY_GUIDE.md.
