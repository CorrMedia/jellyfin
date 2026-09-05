# Jellyfin CorrMedia Plugin

A Jellyfin plugin that applies sidecar **mute**, **zoom**, **blur**, and **skip** from `*.corr.json`. The product direction is **two deliveries** of the same item — untouched original vs server-side edited stream — not client Seek/Mute/Pause tricks. See [`ROADMAP.md`](ROADMAP.md).

## Features

- **Dual delivery** — When a sidecar `.corr.json` exists, PlaybackInfo offers **Original** plus **`{sourceName} (Edited)`**; edits apply only to Edited
- **Skip segments** — Server-side cut (omit from encode) on **patched Jellyfin**; no client Seek
- **Mute audio** — Server-side FFmpeg mute (`-af` when mute-only; inside the edit graph when zoom/blur/skips exist)
- **Zoom / punch-in** — Timed crop+scale that fills the frame (does not change duration)
- **Box blur** — Timed full-frame or regional blur (does not change duration)
- **Mixed actions** — Non-length-altering edits (mute, zoom, blur) run before length-altering edits (skip/cut) from the same sidecar
- **Prefer Edited** — Config default puts Edited first for clients that take the first media source
- **Extensible schema** — Unknown `action` values are ignored so new edit types can be added without breaking playback

## How It Works

For each video, the plugin looks for `{stem}.corr.json` next to the file. If it has mute, zoom, blur, and/or skip edits, the plugin adds an Edited media source and (when that source is selected) applies them during encode.

Example: watching `/media/movies/MyMovie.mkv` requires `/media/movies/MyMovie.corr.json`.

Schema: [`schema/corr.schema.json`](schema/corr.schema.json).

### corr.json format

All `start` / `end` values are seconds on the **original** source timeline and runtime. They are never rewritten to account for cuts, crops, or other length-changing edits.

**Application order:** non-length-altering modifications (mute, zoom, blur) are applied first; length-altering modifications (skip/cut, and later crop) are applied after. A mute or zoom at `1800–2100` always means those seconds of the original file, even if earlier skips removed other ranges.

| `action` | Meaning |
|----------|---------|
| `mute` | Silence audio for the range; video continues (does not change duration) |
| `zoom` | Punch in: crop a region and scale it to fill the frame (`punch` is an alias) |
| `blur` | Box-blur the frame or a `box` region (`boxblur` is an alias) |
| `skip` | Omit the range from the delivered stream (cut; shortens duration) |

Other `action` values are reserved and ignored. Zoom uses `scale` + `x`/`y` (normalized center) or a `box` to fill the frame; pan with `x_end`/`y_end` or `box_end`. Blur uses `radius` and optional `box` (one FFmpeg boxblur pass; radius is the only strength control); pan a regional blur with `box_end`.

**Skip-only:**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 0, "end": 1200, "action": "skip" },
    { "id": "edit_002", "start": 3600, "end": 5400, "action": "skip" }
  ]
}
```

**Mixed mute + skip:**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 0, "end": 300, "action": "mute" },
    { "id": "edit_002", "start": 300, "end": 600, "action": "skip" },
    { "id": "edit_003", "start": 1800, "end": 2100, "action": "mute" }
  ]
}
```

**Zoom (punch in, pan right):**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 10, "end": 25, "action": "zoom", "scale": 2, "x": 0.3, "y": 0.35, "x_end": 0.7 }
  ]
}
```

**Regional blur (pan left to right):**
```json
{
  "schema_version": "1.0",
  "edits": [
    {
      "id": "edit_001",
      "start": 8,
      "end": 20,
      "action": "blur",
      "radius": 12,
      "box": { "x": 0.05, "y": 0.75, "width": 0.3, "height": 0.2 },
      "box_end": { "x": 0.65, "y": 0.75 }
    }
  ]
}
```

See [`test-movie.corr.json`](test-movie.corr.json) and [`examples/`](examples/).

## Installation

1. Clone this repository or download the release
2. Build the plugin and place the `.dll` in your Jellyfin `plugins` folder
3. Restart Jellyfin
4. Configure the plugin in the Jellyfin dashboard
5. Place `{stem}.corr.json` next to each video

**Server-side mute + cut (transcoding):** Requires a Jellyfin server built with the core overlay. See `distribution/APPLY_GUIDE.md`. Edit plans are loaded in-process before the stream starts.

## Configuration

- **Prefer Edited when a .corr.json sidecar exists** — Default **on**. Sorts `{Title} (Edited)` ahead of Original for naive clients; turn off to prefer Original. Explicit source pickers still work either way.

## Compatibility

* Tested on Jellyfin 10.11.11 (plugin ABI) / patched 10.11.11 overlay
* Web, mobile, and TV clients that honor `MediaSourceId`

## Known Limitations

* **Mute/zoom/blur/cut require patched Jellyfin** (core overlay). On stock Jellyfin, the Edited source may appear but filters are not applied.
* Edited source forces transcoding (DirectPlay/Stream off); cuts also force HLS for seekability
* Edited `RunTimeTicks` is original duration minus skip totals (approximate for scrubbing)
* Sidecars must sit beside media files (`MyMovie.mkv` → `MyMovie.corr.json`)
* Per-range toggles are not implemented yet (Phase 4)

## Roadmap

See [`ROADMAP.md`](ROADMAP.md): solid server mute → dual delivery → server skip/cut → selective edit compliance.

## Contributing

PRs welcome. Prefer work aligned with the roadmap (server-side delivery over new client playback commands).

## Changelog

### v1.1.0 (in progress)
- Sidecar format is `*.corr.json` (EDL files are no longer read)
- Mute and skip via `edits[].action`; zoom/blur as duration-preserving video effects; unknown actions ignored
- Pause support removed
- Phase 1: in-process server mute (`EdlEditStore` + `SessionAudioFilterProvider`)
- Phase 3: mute-then-cut via `ISessionMediaEditGraphProvider`; HLS forced when cuts exist so seeking reuses segments
- Zoom and boxblur share the same original-timeline video-effect stage (before cuts)
- Dual delivery (original vs edited): see [`ROADMAP.md`](ROADMAP.md)

### v1.0.0.1
- Initial release with skip-only functionality
