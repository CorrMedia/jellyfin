# CorrMedia Roadmap

## Product vision

Apply sidecar `.corr.json` edits on the **normal library item** when the user has **Apply sidecar edits** on (default). Every client that plays that item gets the edited stream. Titles and media-source names show `{Title} (Edited)`. Turn the toggle off to play the untouched file (DirectPlay-capable). There is no second Version, dummy file, or extra MediaSource.

Primary actions:

- **Mute** — silence audio for a range; video continues (does not change duration).
- **Volume** — reduce audio gain for a range (does not change duration).
- **Beep** — replace audio with a tone for a range (does not change duration).
- **Zoom** — punch in: crop a region and scale it to fill the frame (does not change duration or output size).
- **Crop** — keep a region and pad with black so output size stays the same (does not change duration).
- **Blur / pixelate / cover** — hide a region or the full frame (does not change duration).
- **Blank** — full-frame black video; audio continues (does not change duration).
- **Skip** — remove that range from the delivered timeline (cut / concat), not client seek.

**Timeline rule:** Non-length-altering modifications are applied before length-altering modifications (skip/cut). Every edit’s `start` / `end` is on the original source runtime; times are never rewritten after cuts. Video effects share one original-timeline overlay stage so they can be combined in sidecar order. Output frame size never changes.

**Pause is out of scope and removed (Phase 0).** Extra `action` values in corr.json are ignored until implemented (e.g. scene markers / POIs).

Users can turn **categories** on or off from the dashboard (Phase 4). The sidecar still chooses mute vs beep vs crop vs skip; filters only decide whether a tagged edit runs.

This is **server-side delivery**, not client Seek/Mute/Pause tricks.

---

## Non-goals (for now)

- Client-driven Seek / Pause / volume toggles as the primary mute/skip mechanism.
- A second MediaSource, dummy file, or on-disk rename for “Edited.”
- Relying on Jellyfin MediaSegments alone to rewrite audio/video (segments are metadata + client UX; they do not cut or mute the encode).
- Pre-encoding and storing a second full file on disk as the default approach (stream-time application is preferred; optional cache can come later).

---

## How MediaSegments fits

[Media segments](https://jellyfin.org/docs/general/server/metadata/media-segments/) store typed ranges (Intro, Outro, Commercial, …) for clients. They are complementary:

| Concern | MediaSegments | CorrMedia (this roadmap) |
|--------|---------------|---------------------------|
| Data | Typed ranges in Jellyfin DB | Sidecar `.corr.json` and per-user category filters |
| Who acts | Client (skip button / auto-skip) | Server (FFmpeg mute / effects / cut) |
| Delivery | Same file, client jumps | Same library item; encode is edited when apply-edits is on |

Optional later work: publish sidecar skip ranges as MediaSegments so stock clients can show skip UI when apply-edits is **off** (untouched file). The apply-edits path remains the hard cut/mute encode.

---

## Current state (baseline)

| Area | Status |
|------|--------|
| corr.json parse (mute / volume / beep / zoom / crop / blur / pixelate / cover / blank / skip; other actions ignored) | Done |
| Client Seek skip | **Removed** — cuts are server-side effects-then-cut |
| Server mute via FFmpeg | Done — mute-only `-af`, or inside edit graph when zoom/blur/cuts exist |
| Server zoom / crop / boxblur / pixelate / cover / blank | Done — shared original-timeline video-effect overlays in the edit graph |
| Server skip (cut) | Done (Phase 3) — `CorrFilterComplexBuilder` + `ISessionMediaEditGraphProvider` effects-then-cut |
| Dual Version / extra MediaSource | **Rejected** — apply-edits on the primary item; `{Title} (Edited)` label |
| Per-user filters | Done (Phase 4) — apply toggle plus major/minor category filters; no presets; not per-range honor/ignore |
| Core overlay | Mute `-af`, edit-graph, mute loader, `ISessionCorrDeliveryHint` (transcode / HLS / shortened ticks / name suffix) |

See `distribution/APPLY_GUIDE.md` for the patched-server path. Known playback limits (always transcode, approximate duration, subs/chapters, hardware encode, …) live in `TODO.md`.

---

## Phased roadmap

### Phase 0 — Clean slate for the vision ✅

**Goal:** Align code and docs with mute + skip, server delivery. Pause is out.

- [x] Remove **Pause** from config UI, README, examples.
- [x] Sidecar format is `.corr.json`; unknown actions ignored.
- [x] Document that client Seek/mute loops are transitional and will be removed once server delivery works.
- [x] Collapse AudioControl HTTP mute bridge into CorrMedia (AudioControl removed from the repo).

**Exit:** Plugin no longer offers or documents pause.

---

### Phase 1 — Reliable server-side mute ✅

**Goal:** When sidecar edits apply, mute ranges are always applied in the encode — no client mute.

- [x] Keep / harden Jellyfin core overlay for `ISessionAudioFilterProvider` (+ mute loader / HLS+progressive force-transcode).
- [x] Apply mute filters on **all** relevant encode paths (HLS video + audio-only, progressive), not only progressive.
- [x] **Force audio transcode** when mute ranges apply (`AllowAudioStreamCopy = false` via loader + `HasSessionAudioFilter*`).
- [x] Load corr.json before the first stream request (`SessionMuteRangeLoader` → in-process `CorrEditStore`).
- [x] Fold mute into CorrMedia (`CorrEditStore` + `SessionAudioFilterProvider`); drop HTTP mute bridge.
- [x] Remove dead client-mute stubs. AudioControl removed from the repo.

**Exit:** Patched server + CorrMedia; item with mute-only corr.json; playback silent in mute ranges on web HLS; logs show FFmpeg `volume=…:eval=frame`.

---

### Phase 2 — Dual delivery (original vs edited) ✅ → superseded

**Original goal:** two versions of a movie; user (or client) chooses via MediaSources.

**Landed instead:** one library item. Per-user **Apply sidecar edits** (default on) runs the graph on the primary source for every client. Item/source names get ` (Edited)`. Toggle off for the untouched file. Extra `{Title} (Edited)` MediaSource and Prefer-Edited sort were removed because listing play and most apps pin the item id and never selected the second source.

---

### Phase 3 — Server-side skip (cut) ✅

**Goal:** Skip ranges shorten the edited timeline (content removed).

Application order: **mute / zoom / blur on the original timeline, then cut** (omit skip ranges via trim/concat). Sidecar times stay on the original source; they are not rewritten after cuts.

- [x] Map skip ranges to FFmpeg mute-then-cut `filter_complex` (`CorrFilterComplexBuilder` + `ISessionMediaEditGraphProvider`).
- [x] Timeline semantics: muted ranges keep duration; skipped ranges remove duration.
- [x] Retire client Seek-based skip (sidecar load only → `CorrEditStore`).
- [x] Overlap: skip removes media; mute applies only on remaining keep segments (mute first on full timeline, then trim).
- [ ] Mid-stream seek on the shortened timeline (best-effort today; encode often starts at 0 when cuts exist).
- [x] Apply-edits off still plays the full original (Phase 2 landing).

**Exit:** Sidecar with mute + skip produces a continuous edited stream; no client Seek.

---

### Phase 4 — Selective sidecar compliance ✅

**Goal:** User chooses which sidecar changes to honor.

- [x] Persist per-user overrides: honor / ignore by **content category** (Language, Sex, Nudity, Kissing, Violence, Drugs, Medical, Credits; sidecar `edits[].categories`).
- [x] UI: master “apply every sidecar treatment” (default) or toggle categories (struck icon = apply the authored mute/beep/crop/skip; unstruck = original).
- [x] Build the FFmpeg graph from **filtered** ranges only (edits whose tags intersect enabled minors).

Presets (“family”, “no commercials”, “mutes only”) are out of scope. Per-edit honor/ignore is not implemented (sidecar `id` is used internally after filters).

**Exit:** Two users can get different edited streams from the same sidecar without editing the sidecar file.

---

### Phase 5 — Polish and ecosystem (optional)

- [ ] Optional MediaSegment provider from sidecar skips for client skip buttons when apply-edits is off.
- [ ] Cache / reuse of edited transcodes for repeated playback.
- [ ] Upstream Jellyfin PR for extension points (reduce need for a long-lived fork).
- [ ] Scene-marker / POI `action` in corr.json if product wants chapter-like ranges.
- [x] HLS / external VTT and SRT on the cut timeline (see `EDITED-TIMELINE.md`).
- [x] Trickplay on the cut timeline (see `EDITED-TIMELINE.md`).
- [x] Internal PGS/DVD burn-in on the cut timeline (see `EDITED-TIMELINE.md`).
- [x] Text/ASS and external graphical burn-in on the cut timeline (see `EDITED-TIMELINE.md`).
- [ ] Chapters on the cut timeline (see `EDITED-TIMELINE.md`).
- [ ] Mid-stream seek on the shortened timeline (carried from Phase 3).

Dual-source / custom-client Version pickers are **out** (Phase 2 superseded).

---

## Suggested sequencing

```text
Phase 0 ✅ → Phase 1 ✅ (server mute) → Phase 3 ✅ (effects-then-cut)
    → Phase 2 ✅ superseded (apply-edits on the primary item, not dual MediaSources)
        → Phase 4 ✅ (per-user category filters, no presets)
            → Phase 5 (optional polish)
```

Phase 5 is optional polish (MediaSegments, transcode cache, upstream hooks, seek on the cut timeline).

---

## Success criteria (north star)

1. Library item with a sidecar `.corr.json` plays edited for every client when the user has apply-edits on; `{Title} (Edited)` in the UI.
2. Sidecar actions run in the server encode; no client Seek/Mute/Pause required for those actions.
3. Apply-edits off is bit-identical in intent to normal Jellyfin playback (no sidecar side effects).
4. Users can narrow which categories apply without editing the `.corr.json` file.
5. Pause is not part of the product.

---

## Open decisions

- Apply-edits toggle vs extra MediaSource — **landed** as primary-source apply + `{Title} (Edited)` label.
- Whether Edited is always transcoded, or only when edits remain after filters — still always transcoded while apply-edits is on.
- Whether to keep a thin patched Jellyfin long-term or upstream extension points.
- Additional corr.json `action` types beyond the current playback set (e.g. scene markers / POIs).
- Whether MediaSegments are published when apply-edits is off, on, both, or neither.

Update this file as phases complete or decisions land.
