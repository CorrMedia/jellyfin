Server-side mute + cut: Jellyfin core overlay
=============================================

Adds:
- ISessionAudioFilterProvider — mute-only -af injection
- ISessionMediaEditGraphProvider — mute-then-cut -filter_complex (NLE order)
- ISessionMuteRangeLoader — pre-stream EDL load (Edited MediaSourceId only)
- ISessionEdlDeliveryHint — Edited detection, PreferEdl sort, force HLS when cuts exist
- EncodingHelper / VideosController / DynamicHlsController / MediaInfoHelper wiring

Dual delivery: CorrMedia EdlMediaSourceProvider adds "{Title} (Edited)" (*_edl).
Mute/cut/HLS apply only when that MediaSourceId is selected.
DtoService item MediaSources include dynamic providers so the web Version picker shows
Original vs Edited (static-only previously hid the Edited source).

Apply:
  powershell -File scripts/apply-core-overlay.ps1 -JellyfinSourcePath C:\path\to\jellyfin

Deploy CorrMedia. See distribution/APPLY_GUIDE.md.
