# Apply guide: server-side mute + cut (patched Jellyfin + CorrMedia)

Use this when you want to build Jellyfin with the EDL mute-then-cut extension and run **CorrMedia**.

## Prerequisites

- .NET 9 SDK
- A clone of [Jellyfin server](https://github.com/jellyfin/jellyfin) (same major version as this overlay, e.g. 10.11.x), **or** this repo’s `jellyfin-source`
- This repo with `distribution/jellyfin-core-overlay` and `scripts/`

## Steps

### 1. Apply the core overlay

From this repo root:

```powershell
.\scripts\apply-core-overlay.ps1 -JellyfinSourcePath C:\path\to\jellyfin
```

You should see copies for `ISessionAudioFilterProvider`, `ISessionMediaEditGraphProvider`, `ISessionMuteRangeLoader`, `ISessionEdlDeliveryHint`, `EncodingHelper`, `DynamicHlsController`, `VideosController`, `MediaInfoHelper`, and `ApplicationHost`.

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

Enable CorrMedia. Place `.edl` files next to media (`1` = mute, `3` = skip).

## Validation

- Play an item with mute and/or skip ranges (transcode is forced when EDL applies; cuts force HLS).
- Mute-only: logs show `SessionAudioFilterProvider` and FFmpeg `volume=...eval=frame`.
- With skips: logs show `SessionMediaEditGraphProvider` / `filter_complex` with mute then trim/concat.
- Playhead should advance continuously through former skip ranges (content omitted).

## Pass/fail signals

| Check | Pass | Fail |
|-------|------|------|
| Core overlay | Script copies listed files | Missing files / apply errors |
| Jellyfin build | Build succeeded | Restore/build errors |
| Plugin deploy | CorrMedia DLL in plugins | Build/copy errors |
| Mute at runtime | Silent audio in mute ranges | Audio still audible |
| Skip at runtime | Content omitted; continuous timeline | Original segment still plays |
| Logs | Mute filter or edit-graph `filter_complex` | No filter; stream copy |

## Docker

```powershell
.\scripts\build-and-run-patched-docker.ps1
```

Open **http://localhost:18096**. Put media + EDL in `docker\jellyfin\media`.

## Rollback

Restore original Jellyfin sources and rebuild; remove CorrMedia from the plugins folder.
