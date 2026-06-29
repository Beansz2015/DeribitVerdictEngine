# Time-Averaged OFI — Proposal (P4 #4)

**Status:** PROPOSED — awaiting trader sign-off (2026-06-29)
**Target:** settings **v45 → v46** (averaging keys) + a **scoring re-baseline** of the OFI dominance thresholds.
**Scoring impact:** ⚠ **YES — this is the first P4 re-baseline upgrade.** It changes the value of `OFIRatio` (the input to the OFI vote + the OFI-momentum ring), so the OFI thresholds must be re-derived and a dataset boundary marked. **Not** a display-only feature like #1–#3.
**Item:** #4 in `websocket-migration-proposal.md` §11 (⚠ re-baseline-flagged).
**Gate:** WS-only (needs the streaming book over time); build is safe now, but the **re-baseline is data-gated** on post-build accumulation (like v40/v41).

---

## 1. Summary

Today OFI is computed from a **single order-book snapshot** per run — one moment in time, the noisiest input in the engine (§11; the trader-profile lists OFI as a PREFERRED *leading* indicator, so its quality matters). The WS feed now streams the book ~100ms, so instead of one snapshot we can compute a **time-weighted average of the top-book imbalance over the run window** and feed *that* to the existing OFI vote. Same indicator slot, same downstream mechanism (OFISignal → Step-2 Microstructure vote → OFI-momentum ring) — materially less noise.

The catch, and why this is a re-baseline: averaging **tightens the `OFIRatio` distribution** (transient 2× spikes wash out), so the current dominance thresholds (`BuyDominantRatio` 2.0 / `SellDominantRatio` 0.5) — tuned for the spiky snapshot distribution — would fire far less often on the averaged ratio. The thresholds must be re-derived so OFI fires at the **same rate on cleaner triggers** (sustained imbalance, not a one-tick spike).

---

## 2. Motivation & profile alignment

- **OFI is PREFERRED, leading** (profile §3): "real-time buy/sell pressure… shows imbalance before price moves." A single snapshot captures one instant of a continuously-churning book — a transient iceberg/spoof or a momentary sweep can flip the snapshot. A time-weighted average over the window captures *sustained* pressure, which is the actionable signal.
- **Quality, not quantity** (profile §6): the re-baseline targets the *same fire rate* (firing-rate-matched to the snapshot OFI's history, the v40/v41 method) — so OFI doesn't fire more or less often, it fires on **better-quality** imbalances. Fewer false Microstructure votes from book noise.
- **No new indicator, no new correlation** — same OFI slot, same vote, same momentum derivative. Strictly a cleaner *input*.

---

## 3. Scope & non-goals

**In scope**
- A time-weighted average of the top-book imbalance over the run window, fed to the existing OFI path in place of the single snapshot.
- The OFI dominance-threshold **re-baseline** (the ⚠ core — §5).
- A dataset-boundary marker for the OFIRatio semantics change.

**Explicit non-goals**
- **Not a new vote or category.** OFISignal still votes one full Microstructure signal; the OFI-momentum modifier is unchanged in mechanism. Only the `OFIRatio` *value* changes (averaged vs snapshot).
- **Not the other OFI work.** OFI *momentum* (cross-run RISING/FALLING) stays as-is — it just operates on averaged ratios. Aggressor velocity (#5) and absorption (#6) are separate ⚠ specs.
- **Not a CSV-schema change.** The existing `OFIRatio`/`OFISignal`/`OFIBidVol`/`OFIAskVol` columns keep their names; their **semantics** shift (averaged) — marked as a dataset boundary, same no-rotation rule as v31/v36 (the header is the schema; semantics shift is documented, not a new column).

---

## 4. Design

### 4.1 What "time-averaged" means

The top-book imbalance (the `CalcOFI` weighted bid/ask ratio over `OFI.BookDepth` levels) sampled **repeatedly across the run window** and averaged, rather than once. Two mechanism options — **§10 decision**:

- **(a) Feed-side rolling accumulator (recommended).** As each WS book update arrives (~100ms), the feed folds the current top-book imbalance into a time-decayed/windowed running average held on `MarketState` (e.g. an EMA over `ofi_avg_window_sec`, or a simple mean of the last *N* updates). At run time, `RunAnalysisAsync` reads the **averaged** imbalance instead of computing a one-shot `CalcOFI`. True time-weighting over every update; O(1) per update; one new accumulator field. Keeps the *imbalance math* host-agnostic (a small `OfiAccumulator` the feed feeds; the feed stays dumb about scoring).
- **(b) Sampled ring + average-at-run.** `MarketState` retains a short ring of recent book-imbalance samples (sampled every ~Xs); the run averages the window. Simpler state, coarser (samples, not every update).

Either way the **output is still an `OFIRatio`** consumed exactly as today — so `CalcOFI`'s signal/threshold logic, the Step-2 vote, and the momentum ring are untouched in mechanism.

### 4.2 Integration

- `RunAnalysisAsync` (`MainForm_Analysis.vb:363`): when on the WS path, source `r.OFIRatio`/`r.OFISignal`/`r.OFIBidVol`/`r.OFIAskVol` from the time-averaged imbalance (the accumulator → the same dominance-ratio classification into BUY/SELL DOMINANT/BALANCED). The `_ofiHistory` ring then holds **averaged** ratios; `CalcOFIMomentum` is unchanged.
- **WS-only.** Time-averaging needs intra-run book updates. At `transport=rest` (or a per-run REST fallback) there's one book/run → **fall back to the snapshot `CalcOFI`** (today's path). So a small fraction of runs (REST-fallback) carry snapshot OFI; the WS majority carries averaged OFI. Flagged for the re-baseline (calibrate on the WS-averaged majority; the rare fallback rows are a known minor heterogeneity — §5).

### 4.3 Host-agnostic core

The averaging accumulator (`OfiAccumulator` or equivalent) is host-agnostic (fed by `DeribitWsFeed`, read by `RunAnalysisAsync`); `CalcOFI`'s classification stays the pure function it is. No WinForms.

---

## 5. The re-baseline (the ⚠ core)

**Why:** averaging shrinks the `OFIRatio` variance — a snapshot that hits 2.0 on a momentary sweep averages back toward ~1. Keeping `BuyDominantRatio=2.0` would make BUY DOMINANT fire far less often → OFI's Microstructure vote quietly under-fires → a silent scoring shift. So the thresholds must move toward 1.0 to restore the fire rate on the tighter distribution.

**Method (firing-rate-match — the v40/v41 precedent, trader-validated):**
1. **Build behind the boundary.** Ship the averaging; `OFIRatio` now logs the averaged value (CSV column semantics shift → dated §15 / `change_log` dataset-boundary marker, no rotation).
2. **Collect** post-build data across the operating cadence + sessions (multi-day, like v40/v41 — data-gated).
3. **Re-derive** `BuyDominantRatio` / `SellDominantRatio` so the time-averaged OFI's BUY/SELL-DOMINANT fire-rate **matches the snapshot OFI's historical rate** (same selectivity, cleaner trigger). Cross-check the dominance split is symmetric and the Microstructure-vote rate is stable.
4. **Review the momentum threshold** — `_ofiHistory` now holds averaged ratios (less jumpy), so RISING/FALLING may fire differently; re-confirm or adjust `OFI.Momentum*`.
5. **Ship the re-baselined thresholds** as a settings bump with the measured values + rationale (v40/v41-style change_log).

**The thresholds stay on the auto-tweaker surface** (they're scoring thresholds — the tweaker tunes `OFI.*` legitimately). The re-baseline sets the new anchor; the tweaker can refine later. The averaging *window* key is OFF the tweaker surface (a feed-mechanism config, not a failure-rate threshold).

**Sequence:** build + boundary marker → trader runs/collects → coordinator re-derives on the post-build book → trader signs the new thresholds → ship. The build and the re-baseline are **two commits/versions** (v46 build, then v47-ish re-baseline), mirroring v36→v40.

---

## 6. Config — `indicators.ofi` additions (settings v46)

```json
"OFI": {
  "book_depth": 5,
  "buy_dominant_ratio": 2.0,     // re-baselined post-build (§5)
  "sell_dominant_ratio": 0.5,    // re-baselined post-build
  "averaging_enabled": true,     // NEW — time-average on the WS path
  "avg_window_sec": 10           // NEW — averaging window / EMA horizon
}
```

- `averaging_enabled` + `avg_window_sec` are new (feed-mechanism; OFF the tweaker surface — add `"OFI.averaging"`/`"OFI.avg_window"` handling or a targeted exclusion so the tweaker can't toggle the mechanism, only tune the dominance ratios). `book_depth` + the dominance ratios stay tweaker-tunable.
- Bump v45→v46 + change_log + §15 (the build); the re-baseline is a later bump.

---

## 7. Display-parity

The OFI **SIGNAL BREAKDOWN row** (`Ratio:{OFIRatio} | {OFISignal} | MOM:{OFIMomentum}`) and any OFI card binding render the **averaged** ratio now — same line/format, different value. No new or removed rendered line, so the structure is unchanged, but per the hard parity rule the OFI card binding (`MainForm_Render_Cards.vb`) must be re-verified to show the same averaged value as the snapshot/breakdown (the value source is the same `r.OFIRatio`, so this should hold automatically — confirm in the build).

---

## 8. Edge cases & safety

- **transport=rest / REST-fallback run →** snapshot `CalcOFI` (today's path); never blocks.
- **Cold feed / thin book history (just-connected) →** until the window fills, fall back to the latest snapshot (don't emit a half-window average that misreads).
- **No semantic change to the vote** — averaging only changes the ratio's *value*; STRONG/false-positive behaviour is governed by the re-baselined thresholds.
- **Reversibility:** `averaging_enabled=false` reverts to snapshot OFI (hot) — the rollback if the re-baseline surfaces a problem.

---

## 9. Acceptance

- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck.
- **`averaging_enabled=false` byte-identical** to v45 snapshot OFI (the rollback path; prove via a regression).
- Harness: the accumulator/averaging math (a fed sequence of imbalances → the expected time-weighted average; window eviction; cold-start fallback). `CalcOFI` classification unchanged (existing OFI fixtures hold).
- **Re-baseline acceptance (later commit):** the post-build OFI fire-rate matches the snapshot history within tolerance; before/after dominance-split + Microstructure-vote-rate table in the re-baseline spec-back (v40/v41 format).
- Host-agnostic: the accumulator references no `System.Windows.Forms`.

---

## 10. Open decisions for trader sign-off

1. **Averaging mechanism** — recommend **(a) feed-side rolling accumulator** (true per-update time-weighting, O(1)). Alt: (b) sampled ring (simpler, coarser).
2. **Window / weighting** — recommend an **EMA over `avg_window_sec` = 10s** (smooth, recency-weighted) vs a flat mean of the window. (10s ≈ a third of the old 30s cadence / aligns with the tape-window in #3.)
3. **Re-baseline method** — recommend **firing-rate-match to snapshot-OFI history** (v40/v41 precedent) vs a distribution-percentile anchor.
4. **REST-fallback OFI** — recommend **keep snapshot OFI** on fallback runs (never silent) + accept the minor cadence heterogeneity (calibrate on the WS-averaged majority). Alt: skip OFI scoring on fallback runs.
5. **Build/re-baseline split** — recommend **two versions** (v46 build behind the boundary → collect → v47 re-baselined thresholds), mirroring v36→v40. Confirm you want the build to land first (data starts accumulating immediately) vs waiting.

---

## 11. Implementation map (files)

- **New host-agnostic accumulator** (e.g. `OfiAccumulator.vb`, root/`Core/`) — folds each book update's top-book imbalance into a time-weighted average; read at run time. Fed by `DeribitWsFeed` on book updates; exposed via `MarketState`.
- **`DeribitWsFeed.vb` / `MarketState.vb`** — wire the accumulator into the book-update path; expose the current averaged imbalance.
- **`UI/MainForm_Analysis.vb`** (~line 363) — on the WS path, source `r.OFIRatio`/`OFISignal`/`OFIBidVol`/`OFIAskVol` from the averaged imbalance; snapshot `CalcOFI` on REST-fallback. `_ofiHistory`/momentum unchanged.
- **`Core/Settings/EngineSettings.vb` + `settings.json`** — `averaging_enabled` + `avg_window_sec` under `indicators.OFI`; bump v45→v46 + change_log + §15 dataset-boundary marker.
- **`tools/AutoTweaker/`** — ensure the tweaker can't flip the averaging mechanism (exclude `OFI.averaging_enabled`/`OFI.avg_window_sec`) while still tuning `OFI.buy/sell_dominant_ratio` + `book_depth`.
- **`Core/IndicatorResults.vb`** — no new fields (OFIRatio etc. exist); a comment noting the WS-path semantics.
- **`verify/ordercheck/`** — accumulator/averaging fixtures + the `averaging_enabled=false` byte-identical regression.
- **(Later) re-baseline spec-back** — the measured threshold re-derivation, v40/v41 format.

---

## 12. Out of scope / follow-ons

- **#5 aggressor velocity / tape burst** and **#6 book absorption** are the remaining ⚠ re-baseline upgrades, each its own spec.
- **§8 sub-minute cadence** is deprioritized (conflicts with the #2 bar-close discipline; worst calibration cadence — see `on-close-analysis-mode-proposal.md` §12).
