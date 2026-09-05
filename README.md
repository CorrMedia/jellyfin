# Jellyfin EDL Skipper Plugin

A Jellyfin plugin that applies EDL (Edit Decision List) **mute** and **skip** actions. The product direction is **two deliveries** of the same item — untouched original vs server-side EDL-applied stream — not client Seek/Mute/Pause tricks. See [`ROADMAP.md`](ROADMAP.md).

## Features

- **Skip segments** — Server-side cut (omit from encode) on **patched Jellyfin**; no client Seek
- **Mute audio** — Server-side FFmpeg mute (`-af` when mute-only; inside mute-then-cut graph when skips exist)
- **Mixed actions** — Mute then cut in NLE order from the same `.edl`
- **Session management** — Cleanup and state tracking across sessions
- **Backward compatibility** — Skip-only EDL files (two columns, or type `3`) continue to work

**Pause is not supported.** Classic EDL type `2` (scene marker) is ignored.

## How It Works

For each video being played, the plugin checks for a `.edl` file with the same name. If present, it parses mute/skip ranges and applies them during playback.

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

**Server-side mute (transcoding):** Requires a Jellyfin server built with the core overlay. See `distribution/APPLY_GUIDE.md`. Mute ranges are loaded in-process before the stream starts; AudioControl is not required.

## Configuration

- **Session Check Interval** — How often to refresh mute ranges and check for skip (default: 50ms)

## Compatibility

* Tested on Jellyfin 10.10.7
* Web, mobile, and TV clients
* Backward compatible with existing skip-only EDL files

## Known Limitations

* **Mute requires patched Jellyfin** (core overlay). On stock Jellyfin, mute ranges are stored but not applied to FFmpeg.
* Client Seek for skip is **transitional** (see roadmap Phase 3)
* Direct play / audio copy is forced off when mute ranges exist (CPU cost)
* No frame-perfect accuracy for skip polling
* EDL files must sit beside media files
* Dual “original vs EDL-applied” selection is not implemented yet (Phase 2)

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
- Phase 1: in-process server mute (MuteRangeStore + SessionAudioFilterProvider); AudioControl no longer required
- Dual-delivery and server-side cut planned (roadmap)

### v1.0.0.1
- Initial release with skip-only functionality
