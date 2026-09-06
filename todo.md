## Product (roadmap)

**Done:** sidecar parse, apply-edits on the primary item, `{Title} (Edited)` label, server mute / volume / beep, video effects, server skip/cut, per-user category filters.

**Optional later (Phase 5):**

- Publish sidecar ranges as Jellyfin MediaSegments on Original
- Cache / reuse of Edited transcodes
- Upstream Jellyfin PR so this is not a long-lived fork
- Scene-marker / POI `action` (schema already ignores unknown actions)

## Playback gaps (not stubs, but real limits)

- **Patched server required.** On stock Jellyfin the encode providers compile out (`#if PATCHED_CORE`). Sidecar edits are not applied.
- **Edited always transcodes.** DirectPlay / DirectStream are off while apply-edits is on. Cuts also force HLS.
- **Duration is approximate.** Edited `RunTimeTicks` is original minus merged skip totals (skips past EOF are not clipped).
- **Chapters / metadata dropped** on the edit graph (`-map_chapters -1 -map_metadata -1`). See `EDITED-TIMELINE.md`.
- **HLS / external VTT and SRT** and **trickplay** are remapped onto the cut timeline. Burn-in (text/ASS, internal PGS/DVD, external graphical) is on the original timeline before cuts. Chapters are still dropped. Tracking: [`EDITED-TIMELINE.md`](EDITED-TIMELINE.md).
- **Hardware encode** is not a first-class path for the graph; copy codecs are forced to `libx264` / `aac`.
- `box_end` **pan only.** Width/height stay at the start box (FFmpeg crop size is not per-frame). Documented in the schema.
- `schema_version`, `media`, and `severity` are present in JSON but unused. `categories`, `id`, and `description` are used (Phase 4 filters / UI).

The GitHub repository is still named `jellyfin`. Catalog image and zip use CorrMedia names.

## Engineering debt

- **Long-lived core overlay** (`DtoService` name suffix, `PackageController` sideload 404 fix, encode/HLS hooks). Phase 5 upstream work is the exit.
