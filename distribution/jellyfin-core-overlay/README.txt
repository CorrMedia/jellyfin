Server-side mute + cut: Jellyfin core overlay (10.11.11)
=============================================

Adds:
- ISessionAudioFilterProvider — mute-only -af injection
- ISessionMediaEditGraphProvider — effects-then-cut -filter_complex (mute/zoom/blur first, then cut; times on original source)
- ISessionMuteRangeLoader — pre-stream sidecar load (Edited MediaSourceId only)
- ISessionEdlDeliveryHint — Edited detection, PreferEdl sort, force HLS when cuts exist
- EncodingHelper / VideosController / DynamicHlsController / DynamicHlsHelper / StreamingHelpers / MediaInfoHelper / UserLibraryController wiring

Dual delivery: CorrMedia EdlMediaSourceProvider adds "{Title} (Edited)" (*_edl).
Mute/cut/HLS apply only when that MediaSourceId is selected.
DtoService item MediaSources include dynamic providers so the web Version picker shows
Original vs Edited (static-only previously hid the Edited source).
PackageController falls back to installed-plugin metadata so sideloaded CorrMedia
does not 404 the dashboard plugin details page.

Apply:
  powershell -File scripts/apply-core-overlay.ps1 -JellyfinSourcePath C:\path\to\jellyfin

Deploy CorrMedia. See distribution/APPLY_GUIDE.md.
