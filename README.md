# Jellyfin CorrMedia Plugin

A Jellyfin plugin that applies sidecar **mute**, **volume**, **beep**, **zoom**, **crop**, **blur**, **pixelate**, **cover**, **blank**, and **skip** from `*.corr.json`. Edits run on the **normal library item** for every client when the user has them enabled — not a second Version / media source, and not client Seek/Mute/Pause tricks. See [`ROADMAP.md`](ROADMAP.md).

## Features

- **Sidecar playback** — When a `.corr.json` exists and **Apply sidecar edits** is on, every play of that title (listing, Continue Watching, apps) gets the edited stream. Titles show `{Name} (Edited)`. Turn the toggle off to play the file untouched.
- **Skip segments** — Server-side cut (omit from encode) on **patched Jellyfin**; no client Seek
- **Mute / volume / beep** — Server-side FFmpeg audio (`-af` when all-channel audio-only; inside the edit graph when selective channels, video effects, or skips exist). Optional `channels` names apply before any stereo downmix.
- **Zoom / punch-in** — Timed crop+scale that fills the frame (does not change duration or output size)
- **Crop** — Timed keep-region with black padding so output size stays the same
- **Box blur / pixelate / cover** — Timed full-frame or regional hide (does not change duration)
- **Blank** — Full-frame black video for a range; audio continues
- **Mixed actions** — Non-length-altering edits run before length-altering edits (skip/cut) from the same sidecar
- **Extensible schema** — Unknown `action` values are ignored so new edit types can be added without breaking playback
- **Per-user treatments** — Default is apply every sidecar edit as written. Optionally turn categories on or off; mute vs beep vs crop vs skip still comes from the sidecar.

## How It Works

For each video, the plugin looks for `{stem}.corr.json` next to the file. If the user has **Apply sidecar edits** on, those edits run during encode of the normal item. The library name is shown as `{Title} (Edited)`. There is no separate Original/Edited Version picker.

Example: watching `/media/movies/MyMovie.mkv` requires `/media/movies/MyMovie.corr.json`.

Schema: [`schema/corr.schema.json`](schema/corr.schema.json).

### corr.json format

All `start` / `end` values are seconds on the **original** source timeline and runtime (fractional values are allowed, e.g. `12.4`). They are never rewritten to account for cuts or other length-changing edits.

**Application order:** non-length-altering modifications (mute, volume, beep, zoom, crop, blur, pixelate, cover, blank) are applied first; length-altering modifications (skip/cut) are applied after. A mute or zoom at `1800–2100` always means those seconds of the original file, even if earlier skips removed other ranges. Output frame size never changes: crop pads with black rather than resizing the stream.

| `action` | Meaning |
|----------|---------|
| `mute` | Silence audio for the range; video continues. Optional `channels` lists FFmpeg labels (`FC`, `FL`, `FR`, `LFE`, …) on the **source** layout before any stereo downmix. Omit/`[]` = all channels. Names missing from the source are ignored; if none remain, the edit falls back to all channels. |
| `volume` | Same as mute but scales audio by `gain` (0–1, default 0.2) instead of silencing. |
| `beep` | Replace audio with a sine tone (`frequency` Hz, default 1000; `gain` amplitude, default 0.3). Optional `channels`. |
| `zoom` | Punch in: crop a region and scale it to fill the frame (`punch` is an alias) |
| `crop` | Keep a region and pad with black so the output size stays the same |
| `blur` | Box-blur the frame or a `box` region (`boxblur` is an alias) |
| `pixelate` | Pixelate the frame or a `box` region (`mosaic` is an alias); `size` is block size in pixels (default 16) |
| `cover` | Solid black rectangle over the frame or a `box` (`blackout` is an alias) |
| `blank` | Full-frame black video; audio continues |
| `skip` | Omit the range from the delivered stream (cut; shortens duration) |

Other `action` values are reserved and ignored. Zoom and crop use `scale` + `x`/`y` (normalized center) or a `box`; pan with `x_end`/`y_end` or `box_end`. Blur uses `radius` and optional `box`; pan a regional blur/cover/pixelate with `box_end`.

**Skip-only:**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 0.0, "end": 1200.0, "action": "skip" },
    { "id": "edit_002", "start": 3600.0, "end": 5400.0, "action": "skip" }
  ]
}
```

**Mixed mute + skip:**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 0.0, "end": 300.0, "action": "mute" },
    { "id": "edit_002", "start": 300.0, "end": 600.0, "action": "skip" },
    { "id": "edit_003", "start": 1800.0, "end": 2100.0, "action": "mute" }
  ]
}
```

**Named-channel mute (5.1 center / fronts; falls back to all channels on stereo):**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 10.25, "end": 20.5, "action": "mute", "channels": ["FC"] },
    { "id": "edit_002", "start": 30.0, "end": 40.0, "action": "mute", "channels": ["FL", "FR", "FC"] }
  ]
}
```

**Zoom (punch in, pan right):**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 10.0, "end": 25.0, "action": "zoom", "scale": 2, "x": 0.3, "y": 0.35, "x_end": 0.7 }
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
      "start": 8.0,
      "end": 20.0,
      "action": "blur",
      "radius": 12,
      "box": { "x": 0.05, "y": 0.75, "width": 0.3, "height": 0.2 },
      "box_end": { "x": 0.65, "y": 0.75 }
    }
  ]
}
```

**Crop (keep a region, pad to original size):**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 10.0, "end": 20.0, "action": "crop", "scale": 2, "x": 0.5, "y": 0.45 }
  ]
}
```

**Cover, pixelate, blank, volume, beep:**
```json
{
  "schema_version": "1.0",
  "edits": [
    { "id": "edit_001", "start": 8.0, "end": 16.0, "action": "cover", "box": { "x": 0.1, "y": 0.2, "width": 0.3, "height": 0.4 } },
    { "id": "edit_002", "start": 20.0, "end": 28.0, "action": "pixelate", "size": 24, "box": { "x": 0.6, "y": 0.3, "width": 0.25, "height": 0.3 } },
    { "id": "edit_003", "start": 30.0, "end": 34.0, "action": "blank" },
    { "id": "edit_004", "start": 40.0, "end": 50.0, "action": "volume", "gain": 0.2 },
    { "id": "edit_005", "start": 55.0, "end": 58.0, "action": "beep", "frequency": 1000 }
  ]
}
```

See [`test-movie.corr.json`](test-movie.corr.json) and [`examples/`](examples/).

## Installation

Site: [corrmedia.github.io](https://corrmedia.github.io) ([source](https://github.com/CorrMedia/CorrMedia.github.io)). Plugin catalog: [github.com/CorrMedia/jellyfin](https://github.com/CorrMedia/jellyfin).

1. Download [CorrMedia.zip](https://github.com/CorrMedia/jellyfin/releases/latest) or build this repository
2. Extract the zip into your Jellyfin `plugins` folder (so `Jellyfin.Plugin.CorrMedia.dll` sits in a plugin directory)
3. Restart Jellyfin
4. Configure the plugin in the Jellyfin dashboard
5. Place `{stem}.corr.json` next to each video

**Server-side mute + cut (transcoding):** Requires a Jellyfin server built with the core overlay. See [`distribution/APPLY_GUIDE.md`](distribution/APPLY_GUIDE.md). Edit plans are loaded in-process before the stream starts.

## Configuration

- **Apply sidecar edits when a .corr.json file exists** — Default **on**, per user. Every client plays the edited stream; titles show `{Title} (Edited)`. Turn off to play the untouched file. Restart playback after saving.
- **Apply every treatment in the .corr.json sidecar** — Default **on**. Every sidecar edit runs as written. Turn it off to choose categories. A camera or speaker with no strike means that class plays original; a struck icon means apply the sidecar’s treatment (mute, beep, crop, skip, etc.). These controls do not change how a treatment is done. Categories are a shared taxonomy; sidecar `edits[].categories` map into it.

## Compatibility

* Tested on Jellyfin 10.11.11 (plugin ABI) / patched 10.11.11 overlay
* Web, mobile, and TV clients that honor `MediaSourceId`

## Known Limitations

* **Edited encode requires patched Jellyfin** (core overlay). On stock Jellyfin, filters are not applied.
* Sidecar playback forces transcoding (DirectPlay/Stream off); cuts also force HLS for seekability.
* Skip and picture edits cannot stay entirely on the GPU. NVENC, QSV, VAAPI, and similar can decode and encode, but they do not expose a portable filter set for overlay, punch-in crop, concat cuts, or timed boxes. Those run on the CPU after a download from the decoder; the encoder can still be hardware. Mute-only (no cuts or picture edits) still uses Jellyfin’s stock hardware video path. HDR/10-bit through the graph is flattened to 8-bit. Turn apply-edits off if a title only plays via a full hardware transcode.
* Edited `RunTimeTicks` is original duration minus merged skip totals (approximate for scrubbing)
* HLS / external VTT and SRT cues, trickplay tiles, and chapter markers are remapped onto the cut timeline; burned-in text/ASS and graphical subs (internal or external) overlay before cuts.
* Sidecars must sit beside media files (`MyMovie.mkv` → `MyMovie.corr.json`)
* Invalid or unreadable `.corr.json` is treated as no edits (a warning is logged)
* Sidecar `edits[].categories` map onto the shared dashboard taxonomy. Language groups drill down to specific words (`word_ass`, `word_damn`, …). Unknown tokens count as Other.

## Roadmap

See [`ROADMAP.md`](ROADMAP.md): server mute → effects-then-cut → apply-edits on the primary item → per-user category filters.

## Contributing

PRs welcome. Prefer work aligned with the roadmap (server-side delivery over new client playback commands).

## Changelog

### v1.1.0.0
- Sidecar format is `*.corr.json` (EDL files are no longer read)
- Mute, volume, and beep via `edits[].action`; zoom/crop/blur/pixelate/cover/blank as duration-preserving video effects; unknown actions ignored
- Pause support removed
- Phase 1: in-process server mute (`CorrEditStore` + `SessionAudioFilterProvider`)
- Phase 3: mute-then-cut via `ISessionMediaEditGraphProvider`; HLS forced when cuts exist so seeking reuses segments
- Zoom, crop, boxblur, pixelate, cover, and blank share the same original-timeline video-effect stage (before cuts)
- Dual delivery (original vs edited): replaced by apply-edits on the primary item; titles show `(Edited)`
- Phase 4: per-user apply toggle and content-category filters (major/minor; no presets)

### v1.0.0.1
- Initial release with skip-only functionality
