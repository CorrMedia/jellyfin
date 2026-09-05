# CorrMedia Roadmap

## Product vision

Offer **two deliveries** of the same library item:

1. **Original** — untouched file (direct play / normal remux / normal transcode).
2. **EDL-applied** — a stream produced by the server with Edit Decision List changes baked in.

Primary EDL actions:

- **Mute** (EDL type 1) — silence audio for a range; video continues.
- **Skip** (EDL type 3) — remove that range from the delivered timeline (cut / concat), not client seek.

**Pause is out of scope and removed (Phase 0).** Classic EDL type `2` is a scene marker, not “pause playback.” Type `2` may later map to MediaSegments scene/annotation metadata; it will not drive playback control.

Later: let the user choose **which EDL ranges to honor** (e.g. skip commercials but keep muted dialogue, or ignore a specific range) when requesting the EDL-applied version.

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
| Data | Typed ranges in Jellyfin DB | Sidecar `.edl` (and later user overrides) |
| Who acts | Client (skip button / auto-skip) | Server (FFmpeg mute / cut) |
| Delivery | Same file, client jumps | Alternate “EDL-applied” stream |

Optional later work: publish EDL ranges as MediaSegments so stock clients can show skip UI on the **original** version, while the **EDL-applied** version remains the hard cut/mute path.

---

## Current state (baseline)

| Area | Status |
|------|--------|
| EDL parse (mute / skip; type 2 ignored) | Done (Phase 0) |
| Client Seek skip | **Removed** — cuts are server-side mute-then-cut |
| Server mute via FFmpeg | Done — mute-only `-af`, or inside edit graph when cuts exist |
| Server skip (cut) | Done (Phase 3) — `ISessionMediaEditGraphProvider` mute-then-cut `filter_complex` |
| Dual version (original vs EDL) | Not implemented — EDL-applied is forced when sidecar `.edl` has ranges |
| Per-range user overrides | Not implemented |
| Core overlay | Mute `-af` + edit-graph hooks; CorrMedia registers both providers |

See `distribution/APPLY_GUIDE.md` for the patched-server path.

---

## Phased roadmap

### Phase 0 — Clean slate for the vision ✅

**Goal:** Align code and docs with mute + skip only, server delivery.

- [x] Remove **Pause** from `EdlAction`, `SkipEdl`, config UI, README, examples.
- [x] Treat EDL type `2` as ignored scene marker (`EdlAction.SceneMarker`), not pause.
- [x] Document that client Seek/mute loops are transitional and will be removed once server delivery works.
- [x] Mark AudioControl HTTP bridge and stub mute services as tech debt to collapse.

**Exit:** Plugin no longer offers or documents pause; roadmap and README agree on dual-delivery vision.

---

### Phase 1 — Reliable server-side mute ✅

**Goal:** When an “EDL-applied” (or mute-enabled) stream is requested, mute ranges are always applied in the encode — no client mute.

- [x] Keep / harden Jellyfin core overlay for `ISessionAudioFilterProvider` (+ mute loader / pending seek / HLS+progressive force-transcode).
- [x] Apply mute filters on **all** relevant encode paths (HLS video + audio-only, progressive), not only progressive.
- [x] **Force audio transcode** when mute ranges apply (`AllowAudioStreamCopy = false` via loader + `HasSessionAudioFilter*`).
- [x] Load mute ranges before the first segment request (`SessionMuteRangeLoader` → in-process `MuteRangeStore`).
- [x] Fold AudioControl filter provider into CorrMedia (`MuteRangeStore` + `SessionAudioFilterProvider`); drop HTTP `SetMuteRanges` / `MuteSession` happy path.
- [x] Remove dead stubs (`MuteService`, `MuteController`, mute timer / client mute toggles). AudioControl plugin deprecated (do not deploy for mute).

**Exit:** Patched server + CorrMedia; item with mute-only EDL; playback silent in mute ranges on web HLS; logs show FFmpeg `volume=…:eval=frame`.

---

### Phase 2 — Dual delivery (original vs EDL-applied)

**Goal:** The system can offer **two versions** of a movie; user (or client) chooses.

Design sketch (implementation may vary):

- **Version A — Original:** normal Jellyfin media source; no EDL filters/cuts.
- **Version B — EDL-applied:** dedicated media source / stream options that force transcoding with EDL graph attached.

Possible UX / API shapes (pick one in design spike):

1. Extra `MediaSource` (e.g. “Original” / “Edited”) on the item.
2. Playback flag / profile / plugin API that clients pass when starting playback.
3. Virtual item or linked alternate version (heavier; avoid unless needed).

Requirements:

- [ ] Design spike: how Jellyfin clients discover and select the alternate source.
- [ ] Wire “EDL-applied” selection to force-transcode + Phase 1 mute pipeline.
- [ ] Default library behavior configurable (e.g. prefer original vs prefer EDL-applied).
- [ ] No client Seek required for mute on version B.

**Exit:** User can play the same movie once untouched and once with mutes applied, without changing the file on disk.

---

### Phase 3 — Server-side skip (cut) ✅

**Goal:** Skip ranges shorten the **EDL-applied** timeline (content removed), matching “edited version” expectations.

NLE order: **mute on the original timeline, then cut** (omit skip ranges via trim/concat).

- [x] Map skip ranges to FFmpeg mute-then-cut `filter_complex` (`EdlFilterComplexBuilder` + `ISessionMediaEditGraphProvider`).
- [x] Timeline semantics: muted ranges keep duration; skipped ranges remove duration.
- [x] Retire client Seek-based skip (EDL load only → `EdlEditStore`).
- [x] Overlap: skip removes media; mute applies only on remaining keep segments (mute first on full timeline, then trim).
- [ ] Mid-stream seek on the shortened timeline (best-effort today; encode often starts at 0 when cuts exist).
- [ ] Dual delivery so original still plays full length without EDL (Phase 2).

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
- [ ] Scene-marker handling for EDL type `2` if product wants chapter-like POIs.

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

1. Library item with a sidecar `.edl` exposes (or can start) **Original** and **EDL-applied** playback.
2. EDL-applied applies **mute** and **skip** in the server encode; no client Seek/Mute/Pause required for those actions.
3. Original playback is bit-identical in intent to today’s normal Jellyfin playback (no EDL side effects).
4. Users can later narrow which EDL rules apply without editing the `.edl` file.
5. Pause is not part of the product.

---

## Open decisions

- Exact dual-delivery API/UX (extra `MediaSource` vs playback flag vs other).
- Whether EDL-applied is always transcoded, or only when mute/skip ranges remain after filters.
- Whether to keep a thin patched Jellyfin long-term or upstream extension points.
- Mapping of commercial EDL type `3` vs cut type `0` if we expand beyond current mute/skip codes.
- Whether MediaSegments are published for the original version, the edited version, both, or neither.

Update this file as phases complete or decisions land.
