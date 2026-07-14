# D6 Migration — Spec-Back (implementer report)

**Date:** 2026-07-14 · **Seat:** Opus (implementer, medium effort — one conversation) · **Status:** BUILT, harness green, verify-gate prepush green. Local only — trader tests + pushes; coordinator review follows (Fable seat, reachable ~Jul 19).

Implements `d6-eval-placed-stop-migration-proposal.md` (APPROVED 2026-07-14: **D1=Replace**, D2 rotate, D3 legacy-labelled, D4 both-ways report, D5 any-gap timing) per `d6-migration-implementer-brief.md`. Evidence: `d6-eval-yardstick-divergence-2026-07-08.md` (+ 07-13 addendum).

**One principle held throughout:** the eval adverse barrier = the placed stop the engine emits and the autotrader executes (median 1.6×ATR), not the raw 5m swing stop (median ~9×ATR, unreachable intrabar on 93% of rows). Everything routes through the ONE seam, `SignalEmitter.ComputeSideLevels`, so the perf strip, the CSV `Placed*` columns, the bridge payload, and the offline matrix all read the same geometry.

---

## 1. What shipped, by scope item

### (1) Live tracker — `LivePerformanceTracker.vb`
- **Barriers from the placed levels.** New Friend `BuildLiveEntry(v, r, cfg, nowUtc)` calls `SignalEmitter.ComputeSideLevels(v, r, cfg, isLong)` for both sides and passes the placed target (favourable) + placed stop (adverse) into `BuildEntry`. `UpdateAsync`'s Step 2 now calls `BuildLiveEntry`. FavBar = placed target, AdvBar = placed stop — identical to the CSV `Placed*` for the same run by construction.
- **`BuildEntry` refactored** to take pre-resolved per-side barriers (`favBarLong/advBarLong/favBarShort/advBarShort`) instead of `swingStop*`/`adjTarget*` + the constants. It still owns side selection + the three exclusions (non-directional / ATR≤0 / min-move floor). The min-move backstop now measures the PLACED target — exactly the value the live Step 5c gate checks.
- **`FAV_ATR_MULT` / `ADV_ATR_MULT` deleted** (`:105-106`). `ComputeSideLevels` owns the fallback geometry now.
- **Backfill** (`InitialiseAsync` Step 3) sources barriers via `ResolveBackfillBarriers`: v0.8 rows use the logged `Placed*` columns; pre-v0.8 rows keep the legacy swing-else-ATR formula (D3, no fabrication), with the fallback multipliers read from `cfg` (`AtrTargetMultiplier`/`AtrStopMultiplier` = 1.75/1.6). `LogRow` + `ParseAnalysisLog` gained the four `Placed*` columns + a `HasPlaced` schema flag (parsed like `ExecResolution` — optional, absent ⇒ legacy).

### (2) Eval cache rotation (D2) — schema v4 → v5
- `EVAL_SCHEMA_COMMENT` bumped to `# schema=v5 (placed-level barriers; min-tradeable-move floor; exec resolution)`. Keeps the `min-tradeable-move` substring (so `IsPreV3Schema` still classifies v5 as ≥v3) and adds the `placed-level` marker (the v5 gate). **Column header UNCHANGED** — FavBar/AdvBar change meaning, not shape, so no new column and `IsPreV4Schema` stays False on v5.
- New Friend `IsPreV5Schema` (comment-line detection) + `RotatePreV5Cache` (moves the cache to `.v4.bak`, timestamp-suffixed on collision). New Step 2.0 in `InitialiseAsync` rotates a pre-v5 cache **before** the load, so the v1→v4 migration probes and the load all see a fresh install and the cold-start backfill rebuilds every row on placed barriers. Perf-strip history resets (documented, D2). The raw `analysis_log.csv` + its `.bak` history are untouched — only the derived eval sidecar rotates.
- **Boolean serialization:** no new boolean column was added; `TargetEverHit` still serialises `1`/`0`/empty (the acceptance note's "eval cache writes booleans as 0/1" convention is preserved).

### (3) Offline — `analysis/`
- `CsvRow` + `ForwardWindowJoiner.Load` gained the four `Placed*` columns + `HasPlaced` (schema-level flag, parsed only when all four columns are present).
- New `AdverseBarrierMode` enum (`Placed` | `Legacy`) + `FailureRateMatrix.Compute` gained a trailing `Optional adverseMode As AdverseBarrierMode = AdverseBarrierMode.Placed`. Adverse resolution extracted into **public** `FailureRateMatrix.ResolveAdverseBarrier` (`Placed` = placed stop when `HasPlaced`, else legacy swing-else-ATR; `Legacy` = raw swing on every row). The per-tier favourable ATR grid is **unchanged** (out of scope).
- `AnalysisRunner` partition splits rows on `HasPlaced`: placed rows keep the `NY|1` popKey; legacy rows get a `|LEGACY_YARDSTICK` suffix + `PopulationReport.BarrierLabel`. The main matrix runs in the population's mode; `ComputeContextOutcomes` adverse also migrated to `ResolveAdverseBarrier(Placed)`.
- `MarkdownReportWriter` labels `LEGACY_YARDSTICK` populations (`PopLabel`/`SubTableHeader`/global summary) and the interpretation hint now describes the placed-stop adverse.

### (4) D4 before/after report
- `PopulationReport.LegacyFailureCells` = a second `Compute` pass in `Legacy` mode on the SAME rows. New `## D6. Placed-Stop Migration` section (`AppendD4Comparison`, exposed via public `BuildD4Section` for the fixture) renders `before% → after% (Δ)` per session×resolution×tier. Ships with the commit; the trader regenerates it against the live corpus post-merge.

### (5) Auto-tweaker
- **No code change.** `AutoTweakerCore`'s `FailureRateMatrix.Compute` call omits the new trailing optional, so it defaults to `AdverseBarrierMode.Placed` and its NY×1 rows (all v0.8) score against the placed stop. First-fire gate readings (>40%-failure window) re-base — read them against post-migration rates, not mixed ones.

---

## 2. Deviations & judgment calls (for coordinator review)

1. **`BuildEntry` signature changed** (per-side placed barriers) rather than adding a barrier-source flag. Cleaner: BuildEntry no longer computes any geometry, and `BuildLiveEntry` is the single testable seam for A27a. The two call sites (live/backfill) resolve barriers before calling it.
2. **Pre-v0.8 backfill legacy multipliers sourced from `cfg`** (1.75/1.6), not the deleted local consts (2.0/1.2) and not the proposal §5 "re-sync consts to 1.75/1.6" (superseded by the brief's "delete"). The live `analysis_log.csv` is entirely v0.8 (yardstick §1), so **this branch never fires on the live corpus** — it is defensive for old-CSV analysis only. Flagged rather than assumed.
3. **`LEGACY_YARDSTICK` is a report/population label, not an eval-cache tag.** The live perf-strip eval cache has no population dimension and the live file has zero pre-v0.8 rows, so "no silent mixing" is enforced where it can actually occur — the offline report. The tracker's pre-v0.8 rows simply use the honest legacy formula (D3 "no fabrication"); they age out of the rolling perf windows regardless.
4. **`structuralStopRows` semantics widened** in Placed mode: for a PLACED population it now counts placed-stop rows (a real stop level was used). The barrier-diagnostics table caption states this.
5. **`MarkdownReportWriter.vb` added to `OrderCheck.vbproj`** so A27c can assert `BuildD4Section` renders both barrier bases. Host-agnostic (StringBuilder only); `AnalysisRunner` stays out (needs a live OHLC fetch).
6. **No settings-version bump.** Code-only, eval/measurement layer, no config keys added/changed → `version` stays 51. A dated `eval-semantics boundary` change_log note + a §15 row document it (brief constraint). The verify-gate version-bump nudge is WARN-only and does not block prepush.

---

## 3. Acceptance evidence

- **Builds 0/0** — solution (Release) + `AutoTweaker` + `OrderCheck`. Release-only throughout (the collector holds the Debug exe; constraint honoured).
- **Harness ALL PASS** — A1–A26g unregressed + new **A27a–d**:
  - **A27a** — live tracker barriers ≡ `ComputeSideLevels` on fallback / structural-placed / noise-suppressed cases (through `BuildLiveEntry`).
  - **A27b** — offline `ResolveAdverseBarrier` routing: placed row → placed stop; legacy row → swing fallback; Legacy mode forces raw swing on a placed row; no-swing-no-placed → ATR fallback.
  - **A27c** — `BuildD4Section` renders `before% → after% (Δ)` (both barrier bases, +40% delta, n=40).
  - **A27d** — eval-cache v4→v5: `IsPreV5Schema` classifies v4 pre-v5 / v5 current; `RotatePreV5Cache` moves the file to `.v4.bak` (original gone), so the cold-start backfill rebuilds.
- **verify-gate `-Mode prepush` → GATE PASSED** (build + harness + display-parity clean + version-bump OK).
- **Display parity:** perf-strip is a status element — no card/snapshot line changed (stated per the parity rule).

---

## 4. Not verified by the implementer (runtime, trader-observed)

The **expected effect** — failure rates rise materially once the 1.6×ATR stop binds, and `[B]`/`[T]` perf-strip modes diverge again — is a live-runtime observation that needs the running Debug collector + live/rebuilt data. It is not reproducible from a Release build alone, so it is **not** asserted here. On first launch after this lands: the pre-v5 eval cache rotates to `.v4.bak`, the strip rebuilds on placed barriers (history resets), and the strip should read visibly redder. The D4 report section, once regenerated against the live corpus, quantifies the before→after delta per session×resolution×tier.

## 5. Coordinator review checklist (handover §5)
- [ ] Barriers = `ComputeSideLevels` on the live path; CSV `Placed*` ≡ perf-strip FavBar/AdvBar by construction (A24a still pins CSV≡payload; A27a pins tracker≡ComputeSideLevels).
- [ ] Eval cache v5 rotate-and-rebuild; raw `analysis_log.csv` + `.bak`s untouched.
- [ ] Offline adverse = placed-when-present, legacy-labelled otherwise; favourable ATR grid unchanged.
- [ ] D4 before/after ships and renders both bases.
- [ ] Auto-tweaker unchanged in code; gate readings re-base (documented).
- [ ] Zero scoring impact; no settings-version bump; change_log + §15 note present.
- [ ] Deviations in §2 above accepted.
