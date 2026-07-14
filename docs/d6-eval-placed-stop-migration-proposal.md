# D6 — Eval Barrier Migration onto Placed Levels (Proposal)

**Date:** 2026-07-13 · **Seat:** Fable coordinator · **Status:** **APPROVED 2026-07-14 — trader ticked D1–D5 (D1 = Replace; D2–D5 as recommended).** Raw-book safety confirmed at sign-off: only the derived eval sidecar rotates; `analysis_log.csv` + `.bak`s untouched. Implementation handoff: `d6-migration-implementer-brief.md`.
**Evidence:** `d6-eval-yardstick-divergence-2026-07-08.md` (+ its 07-13 re-verification addendum). **Scope:** eval/measurement code only — **zero scoring impact, no ⚠ dataset boundary** (the verdict pipeline is untouched); it IS an eval-semantics boundary (every stored outcome re-bases).
**Problem being fixed:** both eval surfaces score the adverse barrier against the raw 5m swing stop (median ~9×ATR away; >3×ATR on 93% of directional rows, 98% on the offline matrix's STRONG+standard population) while the engine places — and the autotrader executes — a stop clamped to 1.6×ATR. "Failure" has collapsed to window-expiry; SUCCESS rates say nothing about executed stop-out risk now that the bridge acts on the placed levels.

---

## 1. Mechanism (one principle: barriers = the placed levels, through the one seam)

**Live tracker** (`LivePerformanceTracker.vb:929-935`): `BuildEntry` stops deriving barriers from `Adjusted*Target`/`SwingStop*` + the 2.0/1.2 constants. Instead it consumes the run's **`SignalEmitter.ComputeSideLevels`** outputs (the same arbitration the snapshot, card, payload, and CSV `Placed*` columns already read): `FavBar = placed target`, `AdvBar = placed stop`. The `FAV_ATR_MULT`/`ADV_ATR_MULT` constants (`:105-106`) become dead and are removed. This closes the two divergences in one move: fallback rows currently eval at 2.0×ATR vs the displayed 1.75×; all rows currently eval against the raw swing instead of the clamped stop.

**Offline** (`analysis/FailureRateMatrix.vb:158-170`, `analysis/AnalysisRunner.vb:241-245`, constants `analysis/AnalysisConstants.vb:28/:44`): rows carrying the v0.8 `PlacedTargetLong/Short` + `PlacedStopLong/Short` columns use them directly as the barriers. Pre-v0.8 rows (no `Placed*`) keep the legacy swing-else-fallback formula and are **labelled** in the report (`LEGACY_YARDSTICK` population split) — no silent mixing. The per-tier favourable ATR-threshold grid (`StrongAtrThresholds` etc.) is a separate instrument and is out of scope here.

**Auto-tweaker:** `AutoTweakerCore` consumes `FailureRateMatrix` — its NY×1 failure rates shift with the new barriers. No code change beyond the matrix itself; the shift is the point (it starts measuring the executed geometry). Noted so the first-fire gate (>40%-failure window) is re-read against post-migration rates, not mixed ones.

## 2. Expected effect (directional, to be measured by D4)

Stop-outs become recordable: the adverse barrier moves from ~9×ATR (median) to 1.6×ATR on ~96% of directional rows. **Failure rates rise materially**; the SUCCESS number becomes "reached target before the executed stop" — the autotrade-relevant metric. The perf-strip's green/red read re-bases accordingly (expect visibly redder strips; that is honesty, not regression).

## 3. D-table (trader sign-off)

| # | Decision | Recommendation |
|---|---|---|
| **D1** | Replace vs dual-track the swing-stop metric | **Replace in live code; continuity via a one-shot offline comparison** — D4 re-walks the same v0.8 rows under BOTH barrier sets and reports the deltas side-by-side, then the code runs single-track on placed levels. Permanent dual-track (extra cache columns, two report spines) buys little once the delta is quantified; the discretionary swing lens remains reconstructible offline from the logged `SwingStop*` columns any time |
| **D2** | Eval-cache boundary | **Rotate**: eval-cache schema tag bump, old cache → `.bak`, rebuild from the 7-day OHLC cache on next launch (existing cold-start path). Perf-strip history resets — dated `change_log` note, no settings-version ambiguity (code-only change; `performance_display` keys untouched) |
| **D3** | Pre-v0.8 offline rows | Legacy formula, **labelled** population; no backfill fabrication |
| **D4** | Deliverable | **Before/after failure-rate report** on the same post-v51 rows (both barrier sets, per session×resolution×tier) — the continuity bridge and the first honest read of executed stop-out risk. Ships with the code commit |
| **D5** | Timing | Any gap — no ⚠ boundary, independent of the #5 wire-in / funding ladder. Only constraint: don't land it mid-way through someone's open failure-rate reading; a dated commit is enough |

## 4. Acceptance

Builds 0/0; harness unregressed + new fixtures pinning (a) tracker barriers ≡ `ComputeSideLevels` outputs on capped/fallback/suppressed cases, (b) offline Placed*-vs-legacy row routing, (c) the D4 report renders both populations. §15 row + change_log note. No card/snapshot line changes (display parity untouched — the perf-strip is a status element).
