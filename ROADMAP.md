# CorrMedia Roadmap

## Product vision

Offer **two deliveries** of the same library item:

1. **Original** — untouched file (direct play / normal remux / normal transcode).
2. **Edited** — a stream produced by the server with sidecar `.corr.json` mute/zoom/blur/skip baked in.

Primary actions:

- **Mute** — silence audio for a range; video continues (does not change duration).
- **Zoom** — punch in: crop a region and scale it to fill the frame (does not change duration).
- **Blur** — box-blur the frame or a region (does not change duration).
- **Skip** — remove that range from the delivered timeline (cut / concat), not client seek.

**Timeline rule:** Non-length-altering modifications (mute, zoom, blur) are applied before length-altering modifications (skip/cut, and later crop). Every edit’s `start` / `end` is on the original source runtime; times are never rewritten after cuts or crops. Zoom and blur share one video-effect stage (timed overlay) so they can be combined in sidecar order.

**Pause is out of scope and removed (Phase 0).** Extra `action` values in corr.json are ignored until implemented (e.g. scene markers / POIs).

Later: let the user choose **which edits to honor** (e.g. skip commercials but keep muted dialogue, or ignore a specific range) when requesting the edited version.

This is **server-side delivery**, not client Seek/Mute/Pause tricks. Clients should only pick which version (and later, which rules) to play.

---

## Non-goals (for now)

- Client-driven Seek / Pause / volume toggles as the primary mute/skip mechanism.
- Relying on Jellyfin MediaSegments alone to rewrite audio/video (segments are metadata + client UX; they do not cut or mute the encode).
- Pre-encoding and storing a second full file on disk as the default approach (stream-time application is preferred; optional cache can come later).

---

## How MediaSegments fits

[Media segments](https://jellyfin.org/docs/general/server/metadata/media-segments/) store typed ranges (Intro, Outro, Commercial, …) for clients. They are complementary:

| Concern | MediaSegments | CorrMedia (this roadmap) |
|--------|---------------|---------------------------|
| Data | Typed ranges in Jellyfin DB | Sidecar `.corr.json` (and later user overrides) |
| Who acts | Client (skip button / auto-skip) | Server (FFmpeg mute / cut) |
| Delivery | Same file, client jumps | Alternate “edited” stream |

Optional later work: publish EDL ranges as MediaSegments so stock clients can show skip UI on the **original** version, while the **EDL-applied** version remains the hard cut/mute path.

---

## Current state (baseline)

| Area | Status |
|------|--------|
| corr.json parse (mute / zoom / blur / skip; other actions ignored) | Done |
| Client Seek skip | **Removed** — cuts are server-side effects-then-cut |
| Server mute via FFmpeg | Done — mute-only `-af`, or inside edit graph when zoom/blur/cuts exist |
| Server zoom / boxblur | Done — shared original-timeline video-effect overlays in the edit graph |
| Server skip (cut) | Done (Phase 3) — `ISessionMediaEditGraphProvider` effects-then-cut `filter_complex` |
| Dual version (original vs edited) | Done (Phase 2) — Original + `{Title} (Edited)` MediaSources; edits only on Edited |
| Per-range user overrides | Not implemented |
| Core overlay | Mute `-af` + edit-graph hooks; CorrMedia registers both providers |

See `distribution/APPLY_GUIDE.md` for the patched-server path.

---

## Phased roadmap

### Phase 0 — Clean slate for the vision ✅

**Goal:** Align code and docs with mute + skip only, server delivery.

- [x] Remove **Pause** from config UI, README, examples.
- [x] Sidecar format is `.corr.json`; unknown actions ignored.
- [x] Document that client Seek/mute loops are transitional and will be removed once server delivery works.
- [x] Collapse AudioControl HTTP mute bridge into CorrMedia (AudioControl removed from the repo).

**Exit:** Plugin no longer offers or documents pause; roadmap and README agree on dual-delivery vision.

---

### Phase 1 — Reliable server-side mute ✅

**Goal:** When an “EDL-applied” (or mute-enabled) stream is requested, mute ranges are always applied in the encode — no client mute.

- [x] Keep / harden Jellyfin core overlay for `ISessionAudioFilterProvider` (+ mute loader / pending seek / HLS+progressive force-transcode).
- [x] Apply mute filters on **all** relevant encode paths (HLS video + audio-only, progressive), not only progressive.
- [x] **Force audio transcode** when mute ranges apply (`AllowAudioStreamCopy = false` via loader + `HasSessionAudioFilter*`).
- [x] Load corr.json before the first stream request (`SessionMuteRangeLoader` → in-process `EdlEditStore`).
- [x] Fold mute into CorrMedia (`EdlEditStore` + `SessionAudioFilterProvider`); drop HTTP mute bridge.
- [x] Remove dead client-mute stubs. AudioControl removed from the repo.

**Exit:** Patched server + CorrMedia; item with mute-only corr.json; playback silent in mute ranges on web HLS; logs show FFmpeg `volume=…:eval=frame`.

---

### Phase 2 — Dual delivery (original vs EDL-applied) ✅

**Goal:** The system can offer **two versions** of a movie; user (or client) chooses.

Implemented via Jellyfin multi-source UX:

- **Original** — static media source; no EDL mute/cut/HLS.
- **Edited** — dynamic `IMediaSourceProvider` source: `Id = {itemId:N}_edl`, `Name = "{jellyfinSourceName} (Edited)"` (same label Jellyfin uses for the file, plus ` (Edited)`), DirectPlay/Stream off, `RunTimeTicks` shortened by skip totals.

EDL mute / mute-then-cut / force-HLS apply **only** when `MediaSourceId` is the Edited source.

- [x] Extra `MediaSource` (`EdlMediaSourceProvider`) when sidecar `.corr.json` has mute and/or skip.
- [x] Gate loaders, filters, edit graph, and HLS hints on Edited `MediaSourceId`.
- [x] Config `PreferEdlApplied` (default **true**) so Edited sorts first for clients that take `[0]`.
- [x] Shortened duration on Edited source for scrubbing.
- [x] Item DTO `MediaSources` include dynamic providers (`DtoService` + PreferEdl sort) so web **Version** picker matches multi-file versions UX.

**Exit:** User can play the same movie once untouched and once with mutes/cuts applied, without changing the file on disk.

---

### Phase 3 — Server-side skip (cut) ✅

**Goal:** Skip ranges shorten the **EDL-applied** timeline (content removed), matching “edited version” expectations.

Application order: **mute / zoom / blur on the original timeline, then cut** (omit skip ranges via trim/concat). Sidecar times stay on the original source; they are not rewritten after cuts.

- [x] Map skip ranges to FFmpeg mute-then-cut `filter_complex` (`EdlFilterComplexBuilder` + `ISessionMediaEditGraphProvider`).
- [x] Timeline semantics: muted ranges keep duration; skipped ranges remove duration.
- [x] Retire client Seek-based skip (EDL load only → `EdlEditStore`).
- [x] Overlap: skip removes media; mute applies only on remaining keep segments (mute first on full timeline, then trim).
- [ ] Mid-stream seek on the shortened timeline (best-effort today; encode often starts at 0 when cuts exist).
- [x] Dual delivery so original still plays full length without EDL (Phase 2).

**Exit:** EDL with mute + skip produces a continuous edited stream; no client Seek.

---

### Phase 4 — Selective EDL compliance

**Goal:** User chooses which EDL changes to honor for the EDL-applied (or a custom) version.

- [ ] Persist per-user (or per-session) overrides: honor / ignore by range and/or by action type (mute vs skip).
- [ ] UI: list ranges from the item’s EDL; toggles for each (and bulk “all mutes” / “all skips”).
- [ ] Build the FFmpeg graph from **filtered** ranges only.
- [ ] Optional: presets (“family”, “no commercials”, “mutes only”).

**Exit:** Two users can get different edited streams from the same EDL without editing the sidecar file.

---

### Phase 5 — Polish and ecosystem (optional)

- [ ] Optional MediaSegment provider from EDL for original-version client skip buttons.
- [ ] Cache / reuse of EDL-applied transcodes for repeated playback.
- [ ] Upstream Jellyfin PR for extension points (reduce need for a long-lived fork).
- [ ] Broader client support for dual-source selection if custom clients are required.
- [ ] Scene-marker / POI `action` in corr.json if product wants chapter-like ranges.

---

## Suggested sequencing

```text
Phase 0 ✅ → Phase 1 ✅ (server mute) → Phase 3 ✅ (mute-then-cut)
    → Phase 2 (dual delivery selection)
        → Phase 4 (per-range / per-type overrides)
            → Phase 5 (optional polish)
```

Phase 2 is next so clients can still choose an untouched original.

---

## Success criteria (north star)

1. Library item with a sidecar `.corr.json` exposes **Original** and **Edited** playback.
2. Edited applies **mute**, **zoom**, **blur**, and **skip** in the server encode; no client Seek/Mute/Pause required for those actions.
3. Original playback is bit-identical in intent to today’s normal Jellyfin playback (no sidecar side effects).
4. Users can later narrow which rules apply without editing the `.corr.json` file.
5. Pause is not part of the product.

---

## Open decisions

- Exact dual-delivery API/UX (extra `MediaSource` vs playback flag vs other).
- Whether EDL-applied is always transcoded, or only when mute/skip ranges remain after filters.
- Whether to keep a thin patched Jellyfin long-term or upstream extension points.
- Additional corr.json `action` types beyond mute/zoom/blur/skip.
- Whether MediaSegments are published for the original version, the edited version, both, or neither.

Update this file as phases complete or decisions land.
