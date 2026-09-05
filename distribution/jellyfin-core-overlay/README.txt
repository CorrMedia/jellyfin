Server-side mute + cut: Jellyfin core overlay
=============================================

Adds:
- ISessionAudioFilterProvider — mute-only -af injection
- ISessionMediaEditGraphProvider — mute-then-cut -filter_complex (NLE order)
- ISessionMuteRangeLoader — pre-stream EDL load
- ISessionEdlDeliveryHint — force HLS when cuts exist (seekable segments)
- EncodingHelper / VideosController / DynamicHlsController / MediaInfoHelper wiring

Apply:
  powershell -File scripts/apply-core-overlay.ps1 -JellyfinSourcePath C:\path\to\jellyfin

Deploy CorrMedia. See distribution/APPLY_GUIDE.md.
