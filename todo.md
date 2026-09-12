## Product (roadmap)

**Done:** sidecar parse, apply-edits on the primary item, `{Title} (Edited)` label, server mute / volume / beep, video effects, server skip/cut, per-user category filters.

**Optional later (Phase 5):**

- Publish sidecar ranges as Jellyfin MediaSegments on Original
- Cache / reuse of Edited transcodes
- Upstream Jellyfin PR so this is not a long-lived fork
- Scene-marker / POI `action` (schema already ignores unknown actions)

## Playback gaps (not stubs, but real limits)

- **Patched server required.** On stock Jellyfin the encode providers compile out (`#if PATCHED_CORE`). Sidecar edits are not applied.
- **Edited always transcodes.** DirectPlay / DirectStream are off while apply-edits is on. Cuts also force HLS. Mid-stream seek on the cut timeline uses demuxer `-ss` at the original-mapped time; mute/effect/trim clocks are shifted by that seek (filter `t` resets after `-ss`). Text burn-in temporarily restores absolute PTS around `subtitles=`.
- **Duration is approximate.** Edited `RunTimeTicks` is original minus merged skip totals (skips past EOF are not clipped).
- **HLS / external VTT and SRT**, **trickplay**, and **chapters** are remapped onto the cut timeline. Burn-in (text/ASS, internal PGS/DVD, external graphical) is on the original timeline before cuts. Encode still drops mux metadata/chapters (`-map_metadata -1 -map_chapters -1`); clients use the remapped DTO. Tracking: [`EDITED-TIMELINE.md`](EDITED-TIMELINE.md).
- **Hardware encode (graph):** HW decode (when known) + `hwdownload` + CPU corr.json filters + `GetVideoEncoder` (NVENC/QSV/VAAPI/…). Mute-only still uses the full stock HW `-vf` path. Overlay/concat stay CPU-heavy; HDR/10-bit through the graph is flattened to 8-bit. Apply-edits off remains the escape hatch for titles that only play via HW transcode.
- `box_end` **pan only.** Width/height stay at the start box (FFmpeg crop size is not per-frame). Documented in the schema.
- `schema_version`, `media`, and `severity` are present in JSON but unused. `categories`, `id`, and `description` are used (Phase 4 filters / UI).

## Engineering debt

- **Long-lived core overlay** (`DtoService` name suffix, `PackageController` sideload 404 fix, encode/HLS hooks). Phase 5 upstream work is the exit.
