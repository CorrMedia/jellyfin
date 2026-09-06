# Edited timeline: subtitles, trickplay, chapters

Cuts shorten the delivered clock. Mute, volume, beep, and video effects do **not**. Anything still stamped on **original** time will drift after a skip (cues late, scrubber tiles from the wrong scene, including skipped content).

This file tracks that remap work. Product/phase context: `ROADMAP.md`. Other playback limits: `TODO.md`.

**Does not apply** when the user has no remaining skips (apply-edits off, or filters dropped every cut). Then original time = player time.

---

## Current encode

The edit graph maps only `[vout]` and `[aout]`, then `-map_metadata -1 -map_chapters -1`.

- Burn-in is in `CorrFilterComplexBuilder` for text/ASS (`subtitles=`) and graphical (internal PGS/DVD in the MKV, or external `.sup` / VobSub via a second `-i`). Overlay happens on the original timeline after sidecar video effects and before keep-range cuts, when the client burns that stream in.
- HLS maps the same two pads (`TryGetSessionEditGraphMapArgs`).
- Progressive may still append `GetSubtitleEmbedArguments`, but the subtitle stream is not mapped through the concat, and cue times would stay original even if it were.
- Trickplay is a library tile job on the **item**, not part of playback FFmpeg.

Keep-range math already exists for video (`BuildKeepRanges` / `TruncateKeepRangesForEditedStart`). Remaps should reuse that, including **this user’s** category filters (two users can have different skip sets).

---

## Workstreams

Treat these as separate jobs. Do not ship them as one change.

### 1. HLS / external text (SRT, VTT)

| | |
|--|--|
| **Status** | Done (on-the-fly rewrite) |
| **Difficulty** | Moderate |
| **Why first** | What the web client actually draws |

Patched `SubtitleController.EncodeSubtitles` fetches the **full original** track, then `ISessionSubtitleCueRewriter` remaps with keep-range math: drop skip interiors, shift everything after a cut by skipped duration, split cues that straddle a cut, then apply the HLS window on the **edited** clock. Per-user category filters apply (same as the encode graph). Bitmap / ASS that the client **burns in** go through the edit graph (workstream 2). HLS text tracks that are **not** burned in stay on this rewrite path.

**Done when:** HLS text tracks stay in sync on a title with skips; apply-edits off still uses the original file.

### 2. Burn-in (Encode)

| | |
|--|--|
| **Status** | Done (text/ASS, internal and external graphical) |
| **Difficulty** | Medium–hard |
| **Why** | Only way burned captions stay correct after cuts |

Text/ASS uses stock `subtitles=f='path'` on the **original** timeline after sidecar video effects and before keep-range cuts (no start-time `setpts` shift). Internal PGS/DVD overlays `[0:s]`; external `.sup` / VobSub uses the second `-i` as `[1:s]`, same `overlay=eof_action=pass:repeatlast=0` recipe as stock Jellyfin. That runs when the client would burn the stream in (`SubtitleDeliveryMethod.Encode` / always-burn-in). External graphical/audio extra inputs are sought at original t=0 so they stay aligned with the graph.

Clients that never burn in get nothing from this (HLS text still uses workstream 1).

**Done when:** Encode delivery shows captions at the right edited time; mute-only / no-skip still works.

### 3. Trickplay

| | |
|--|--|
| **Status** | Done (manifest + playlist remap, reuse original tiles) |
| **Difficulty** | Hard |
| **Why last** | Per-item tiles vs per-user skips |

Tiles stay a library job on the **item** (original timeline). When this user has remaining skips, patched `DtoService` and `TrickplayController` rewrite the manifest, HLS image playlist, and `{index}.jpg` requests onto the edited clock.

Keep-range mapping (`TryMapEditedToOriginal`) is used — not linear scaling — so edited time never samples a skip. The DTO becomes **1×1** with `ThumbnailCount` matching edited duration; each request crops the matching cell from the original sprite. No second per-user tile tree.

**Done when:** scrubber duration matches the edited stream, and skip interiors are not shown. Apply-edits off still uses the original tiles.

### 4. Chapters / container metadata

| | |
|--|--|
| **Status** | Dropped on purpose (`-map_chapters -1`) |
| **Difficulty** | Same remap as text cues, plus overlay if we re-inject |

Same clock as subs. Optional after (1) if we want chapter markers on the edited item.

---

## Recommended order

1. VTT/SRT rewrite for HLS when this user has skips. **Done.**
2. Burn-in into the original-timeline overlay stage (only if Encode delivery matters). **Done** (text/ASS, internal and external graphical).
3. Trickplay remap (keep-range mapping, reuse original tiles). **Done.**
4. Chapters if anyone misses them.

Seek on the cut timeline (`ROADMAP.md` Phase 3 leftover) is a **different** job: FFmpeg restart / segment index, not cue rewrite. A full cached HLS tree is also different (`TODO.md` / Phase 5 cache) and is not a shortcut for (1)–(3).

---

## Open decisions

- Rewrite subs on the fly vs cache a sidecar VTT next to media / in plugin data. **Chose on-the-fly** (workstream 1).
- Trickplay: hide skipped content vs “wrong frame, including skips”. **Chose hide** via keep-range mapping (workstream 3).
- Burn-in: text/ASS, or only PGS in the file? **Text/ASS and graphical (internal + external) burn in with the edit graph.**
- Do we re-inject chapters at all? **No — still dropped (`-map_chapters -1`).**

---

## Out of scope here

- Restoring DirectPlay while apply-edits is on.
- Hardware encode of the graph.
- Transcode cache / complete HLS tree for faster seek.
- MediaSegments for apply-edits **off** (that is original-timeline client skip UI).
