Server-side mute + cut: Jellyfin core overlay
=============================================

Adds:
- ISessionAudioFilterProvider — mute-only -af injection
- ISessionMediaEditGraphProvider — mute-then-cut -filter_complex (NLE order)
- ISessionMuteRangeLoader — pre-stream EDL load
- EncodingHelper / VideosController / DynamicHlsController wiring

Apply:
  powershell -File scripts/apply-core-overlay.ps1 -JellyfinSourcePath C:\path\to\jellyfin

Deploy CorrMedia only (AudioControl not required). See distribution/APPLY_GUIDE.md.
