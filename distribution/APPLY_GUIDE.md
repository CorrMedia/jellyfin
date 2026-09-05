# Apply guide: server-side mute + cut (patched Jellyfin + CorrMedia)

Use this when you want to build Jellyfin with the EDL mute-then-cut extension and run **CorrMedia**.

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

You should see copies for `ISessionAudioFilterProvider`, `ISessionMediaEditGraphProvider`, `ISessionMuteRangeLoader`, `ISessionEdlDeliveryHint`, `EncodingHelper`, `DynamicHlsController`, `VideosController`, `MediaInfoHelper`, `DtoService`, `MediaSourceManager`, `UserLibraryController`, `ApplicationHost`, and `PackageController`.

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

Enable CorrMedia. Place `{stem}.corr.json` next to media (`action`: `mute`, `zoom`, `blur`, or `skip`). Times are on the original source timeline; mute/zoom/blur are applied before skip/cut.

## Validation

- PlaybackInfo for an item with `.corr.json` should list **Original** and **`{Title} (Edited)`** (Edited first when Prefer Edited is on).
- Item details **Version** dropdown should show both names (same as multi-file versions UX).
- Play **Original**: untouched; no mute filter / edit graph in FFmpeg logs.
- Play **Edited**: mute, zoom/blur, and/or effects-then-cut; cuts force HLS; playhead advances through omitted ranges.
- Mute-only Edited: logs show `SessionAudioFilterProvider` and FFmpeg `volume=...eval=frame`.
- Edited with zoom/blur (no skips): logs show `SessionMediaEditGraphProvider` / `filter_complex` with overlay.
- Edited with skips: logs show `SessionMediaEditGraphProvider` / `filter_complex`.

## Pass/fail signals

| Check | Pass | Fail |
|-------|------|------|
| Core overlay | Script copies listed files | Missing files / apply errors |
| Jellyfin build | Build succeeded | Restore/build errors |
| Plugin deploy | CorrMedia DLL in plugins | Build/copy errors |
| Dual sources | Original + Edited in PlaybackInfo | Only one source / wrong name |
| Original | No corr.json filters | Mute/cut on Original |
| Edited mute | Silent audio in mute ranges | Audio still audible |
| Edited zoom | Punched-in region fills the frame in the range | Full frame unchanged |
| Edited blur | Region or frame is blurred in the range | No blur |
| Edited skip | Content omitted; continuous timeline | Original segment still plays |
| Logs | Mute filter or edit-graph `filter_complex` on Edited only | Filter on Original or stream copy on Edited |

## Docker

```powershell
.\scripts\build-and-run-patched-docker.ps1
```

Open **http://localhost:18096**. Library media lives in `docker\jellyfin\media`. The test clip sidecar is bind-mounted from repo-root `test-movie.corr.json`, so editing that file is what Edited playback reads.

## Rollback

Restore original Jellyfin sources and rebuild; remove CorrMedia from the plugins folder.
