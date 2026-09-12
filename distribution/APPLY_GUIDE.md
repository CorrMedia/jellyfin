# Apply guide: server-side mute + cut (patched Jellyfin + CorrMedia)

Use this when you want to build Jellyfin with the EDL mute-then-cut extension and run **CorrMedia**.

**Before you install:** backup your Jellyfin config directory (and keep a path back to stock) before applying the overlay. There is no installer or built-in upgrade path — this is a manual rebuild and plugin copy. Needs more testing; it has not been tried on every OS, client, or hardware path.

## Prerequisites

- .NET 9 SDK
- A clone of [Jellyfin server](https://github.com/jellyfin/jellyfin) at **v10.11.11**, **or** this repo’s `jellyfin-source` (same tag)
- This repo with `distribution/jellyfin-core-overlay` and `scripts/`

## Steps

### 1. Apply the core overlay

From this repo root:

```powershell
.\scripts\apply-core-overlay.ps1 -JellyfinSourcePath C:\path\to\jellyfin
```

You should see copies for `ISessionAudioFilterProvider`, `ISessionMediaEditGraphProvider`, `ISessionMuteRangeLoader`, `ISessionCorrDeliveryHint`, `ISessionSubtitleCueRewriter`, `ISessionTrickplayRewriter`, `ITrickplayCellCropper`, `SessionEditGraphHwBridge`, `EncodingHelper`, `DynamicHlsController`, `VideosController`, `SubtitleController`, `TrickplayController`, `MediaInfoHelper`, `DtoService`, `MediaSourceManager`, `UserLibraryController`, `ApplicationHost`, `CoreAppHost`, and `PackageController`.

### 2. Build Jellyfin

```powershell
.\scripts\build-jellyfin-with-mute.ps1 -JellyfinSourcePath C:\path\to\jellyfin
```

### 3. Deploy CorrMedia

```powershell
.\scripts\deploy-patched-plugins.ps1 -TargetPluginsPath C:\path\to\jellyfin\plugins
```

Plugins build against patched core when `jellyfin-source` exists in this repo.

### 4. Run Jellyfin

Enable CorrMedia. Place `{stem}.corr.json` next to media. Times are on the original source timeline; non-length-altering edits are applied before skip/cut. Output frame size never changes. Each user can turn sidecar treatments on or off by category from the CorrMedia dashboard (the sidecar still chooses mute vs beep vs crop vs skip). Restart playback after changing.

## Validation

- PlaybackInfo for an item with `.corr.json` (apply-edits on) should list the normal source named `{Title} (Edited)`, DirectPlay off.
- Library cards and item details should show `{Title} (Edited)` for that user.
- Play from the listing: mute, zoom/blur, and/or effects-then-cut; cuts force HLS.
- Mute-only: logs show `SessionAudioFilterProvider` and FFmpeg `volume=...eval=frame`.
- Zoom/blur (no skips): logs show `SessionMediaEditGraphProvider` / `filter_complex` with overlay.
- Skips: logs show `SessionMediaEditGraphProvider` / `filter_complex`.
- HLS / external VTT or SRT: cue times follow the cut timeline (skip interiors omitted).
- Internal PGS (in the MKV) or external graphical (`.sup` / VobSub), when burned in: captions stay on the cut stream.
- Text/ASS burn-in (Encode / always-burn-in): captions stay on the cut stream.
- Trickplay / scrubber preview: duration matches the cut stream; skip interiors are not shown.
- Turn **Apply sidecar edits** off, restart playback: untouched file, no `(Edited)` suffix.
- Turn **Show "Edited" on screen only on sidecar-edited titles** off, restart from the start: no burned-in corner badge. Untouched files never show it. Mid-title seek never shows it.

## Pass/fail signals

| Check | Pass | Fail |
|-------|------|------|
| Core overlay | Script copies listed files | Missing files / apply errors |
| Jellyfin build | Build succeeded | Restore/build errors |
| Plugin deploy | CorrMedia DLL in plugins | Build/copy errors |
| Dual sources | (removed) one source, name `(Edited)` when applying | Extra Original/Edited picker |
| Apply off | No corr.json filters; name unsuffixed | Mute/cut still on |
| Apply on mute | Silent audio in mute ranges | Audio still audible |
| Edited zoom | Punched-in region fills the frame in the range | Full frame unchanged |
| Edited blur | Region or frame is blurred in the range | No blur |
| Edited skip | Content omitted; continuous timeline | Original segment still plays |
| Edited VTT/SRT | Cue times match the cut stream | Cues late/early after a skip |
| Edited PGS (in MKV) | Burned-in captions stay on the cut stream | Captions missing or late after a skip |
| Edited external graphical | Burned-in `.sup` / VobSub stay on the cut stream | Captions missing or late after a skip |
| Edited text/ASS burn-in | Burned-in captions stay on the cut stream | Captions missing or late after a skip |
| Edited trickplay | Scrubber tiles follow the cut stream; skips omitted | Original-timeline tiles / skip frames |
| Logs | Mute filter or edit-graph `filter_complex` on Edited only | Filter on Original or stream copy on Edited |

## Docker

```powershell
.\scripts\build-and-run-patched-docker.ps1
```

Open **http://localhost:18096**. Library media lives in `docker\jellyfin\media`. The test clip sidecar is bind-mounted from repo-root `test-movie.corr.json`, so editing that file is what Edited playback reads.

Edit-graph jobs are HW decode (when known) + `hwdownload` + CPU filters + `GetVideoEncoder`. GPU encode is available; GPU filters are not a portable replacement for overlay, punch-in, concat cuts, or timed boxes. To use NVENC locally, set Dashboard → Playback → hardware acceleration to NVIDIA NVENC, then start with GPU passthrough:

```powershell
docker compose -f docker-compose.patched.yml -f docker-compose.patched.nvidia.yml up -d
```

## Rollback

Restore original Jellyfin sources and rebuild; remove CorrMedia from the plugins folder.
