# Jellyfin EDL Skipper Plugin

A Jellyfin plugin that applies EDL (Edit Decision List) **mute** and **skip** actions. The product direction is **two deliveries** of the same item — untouched original vs server-side EDL-applied stream — not client Seek/Mute/Pause tricks. See [`ROADMAP.md`](ROADMAP.md).

## Features

- **Dual delivery** — When a sidecar `.edl` exists, PlaybackInfo offers **Original** plus **`{sourceName} (Edited)`** (Jellyfin’s normal version label + ` (Edited)`); mute/cut apply only to Edited
- **Skip segments** — Server-side cut (omit from encode) on **patched Jellyfin**; no client Seek
- **Mute audio** — Server-side FFmpeg mute (`-af` when mute-only; inside mute-then-cut graph when skips exist)
- **Mixed actions** — Mute then cut in NLE order from the same `.edl`
- **Prefer Edited** — Config default puts Edited first for clients that take the first media source
- **Backward compatibility** — Skip-only EDL files (two columns, or type `3`) continue to work

**Pause is not supported.** Classic EDL type `2` (scene marker) is ignored.

## How It Works

For each video, the plugin checks for a `.edl` file with the same name. If present, it adds an Edited media source and (when that source is selected) applies mute/skip during encode.

Example: watching `/media/movies/MyMovie.mkv` requires `/media/movies/MyMovie.edl`.

### EDL File Format

```text
start_time end_time action_type
```

**Action types:**

| Code | Meaning |
|------|---------|
| `1` | **Mute** — silence audio for the range |
| `2` | **Scene marker** — ignored (reserved for future POI / MediaSegments) |
| `3` | **Skip** — omit the range from the delivered stream (cut) |

If the third column is omitted, the range is treated as **skip**.

### Example EDL Files

**Skip-only (backward compatible):**
```text
0.00 1200.00 3
3600.00 5400.00 3
```

**Mixed mute + skip:**
```text
0.00 300.00 1
300.00 600.00 3
1800.00 2100.00 1
```

**Mute during commercials:**
```text
1800.00 1860.00 1
3600.00 3660.00 1
```

## Installation

1. Clone this repository or download the release
2. Build the plugin and place the `.dll` in your Jellyfin `plugins` folder
3. Restart Jellyfin
4. Configure the plugin in the Jellyfin dashboard
5. Place `.edl` files next to your videos with the same name and `.edl` extension

**Server-side mute + cut (transcoding):** Requires a Jellyfin server built with the core overlay. See `distribution/APPLY_GUIDE.md`. EDL plans are loaded in-process before the stream starts.

## Configuration

- **Prefer Edited when EDL exists** — Default **on**. Sorts `{Title} (Edited)` ahead of Original for naive clients; turn off to prefer Original. Explicit source pickers still work either way.

## Compatibility

* Tested on Jellyfin 10.10.7 / patched 10.11.x overlay
* Web, mobile, and TV clients that honor `MediaSourceId`
* Backward compatible with existing skip-only EDL files

## Known Limitations

* **Mute/cut require patched Jellyfin** (core overlay). On stock Jellyfin, the Edited source may appear but filters are not applied.
* Edited source forces transcoding (DirectPlay/Stream off); cuts also force HLS for seekability
* Edited `RunTimeTicks` is original duration minus skip totals (approximate for scrubbing)
* EDL files must sit beside media files
* Per-range toggles are not implemented yet (Phase 4)

## EDL File Tips

- Use `#` for comments: `# Skip commercials`
- Empty lines are ignored
- Times are in seconds (decimals supported)
- Action type `3` (skip) is the default if not specified
- Type `2` lines are ignored
- Filenames are case-sensitive and must match the video

## Roadmap

See [`ROADMAP.md`](ROADMAP.md): drop client tricks → solid server mute → dual delivery → server skip/cut → selective EDL compliance.

## Contributing

PRs welcome. Prefer work aligned with the roadmap (server-side delivery over new client playback commands).

## Changelog

### v1.1.0 (in progress)
- Mute (type 1) and skip (type 3); type 2 treated as ignored scene marker
- Pause support removed
- Phase 1: in-process server mute (`EdlEditStore` + `SessionAudioFilterProvider`)
- Phase 3: mute-then-cut via `ISessionMediaEditGraphProvider`; HLS forced when cuts exist so seeking reuses segments
- Dual delivery (original vs EDL-applied): see [`ROADMAP.md`](ROADMAP.md)

### v1.0.0.1
- Initial release with skip-only functionality
