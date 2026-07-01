# Time-Averaged OFI — Proposal (P4 #4)

**Status:** **BUILD IMPLEMENTED 2026-06-30 (settings v46) — see `time-averaged-ofi-spec-back.md`.** Build 0/0 (solution Release + AutoTweaker + OrderCheck); harness A1–A19e unregressed + new A20a–h. Local commit only — trader tests + pushes. The ⚠ dominance-threshold **re-baseline remains OPEN** (the later, data-gated v47-ish pass with its own trader sign-off). Original approval: trader sign-off 2026-06-29; §10 all-recommended + the tweaker-exposure directive.
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

- **(a) Feed-side rolling accumulator (recommended).** As each WS book update arrives (~100ms), the feed folds the current top-book imbalance into a time-decayed/windowed running average held on `MarketState` (e.g. an EMA over `avg_window_sec`, or a simple mean of the last *N* updates). At run time, `RunAnalysisAsync` reads the **averaged** imbalance instead of computing a one-shot `CalcOFI`. True time-weighting over every update; O(1) per update; one new accumulator field. Keeps the *imbalance math* host-agnostic (a small `OfiAccumulator` the feed feeds; the feed stays dumb about scoring).
- **(b) Sampled ring + average-at-run.** `MarketState` retains a short ring of recent book-imbalance samples (sampled every ~Xs); the run averages the window. Simpler state, coarser (samples, not every update).

**Settled formula (§10.1 + §10.2 — build to this):** mechanism **(a)**, with a **time-aware EMA** so the window means what it says under irregular arrivals. On each book update fold the current top-book imbalance `x` into the running average with `alpha = 1 - exp(-dt / tau)`, where `dt` = seconds since the previous update and `tau = avg_window_sec`. (A fixed-alpha EMA would let the effective horizon drift with the update rate — avoid it.) **Warmup / cold-start:** until the accumulator has seen at least `avg_window_sec` of wall-clock coverage (or a small minimum update count), emit the latest single-snapshot `CalcOFI` — the §8 fallback — rather than a half-filled average. State lives on `MarketState` under its existing SyncLock and **resets on (re)connect** so a stale pre-disconnect average can't bleed across a gap. State exactly this `dt`/warmup/reset handling in the spec-back: it fixes the `OFIRatio` distribution the v47 re-baseline calibrates against (§5).

Either way the **output is still an `OFIRatio`** consumed exactly as today — so `CalcOFI`'s signal/threshold logic, the Step-2 vote, and the momentum ring are untouched in mechanism.

**AMENDMENT (2026-07-01) — averaging space is geometric, not arithmetic.** The paragraph above describes an arithmetic EMA fold of the raw ratio; that construction shipped in the v46 build (`ab97f40`) but was superseded before push. A throwaway NY DIAG instrument (2026-06-30, commit `eee6e4b`, reverted) measured both constructions in lockstep over a 616-row / 5.1h net-flat session and found the arithmetic mean of the multiplicatively-symmetric bid/ask ratio manufactures a severe buy-bias (AM≥GM / Jensen) that firing-rate-matching cannot correct (it's distribution *shape*, not level) — 12:1 buy-dominant arithmetic vs 1.4:1 geometric on the same book. The **settled construction is geometric (log-ratio)**: fold `ln(ratio)` with the same time-aware `alpha`, read back `Ratio = exp(emaLn)` at snapshot time. Same alpha/dt/tau formula, applied in log space — everything else in this section (mechanism (a), warmup/reset, host-agnostic accumulator) is unchanged. See `docs/ofi-geometric-construction-spec.md` for the full decision record.

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

**The OFI scoring params are on the auto-tweaker surface** — the dominance ratios **and** `avg_window_sec` (trader directive 2026-06-29: expose the new function's params so a future tweak can optimise them; the window shapes the OFI signal → a genuine failure-rate linkage, unlike pure-mechanism configs). The re-baseline sets the initial anchors; the tweaker refines later. **Coupling caveat:** the dominance ratios are re-derived *for* a given window, so changing the window shifts the ratio distribution — the iterative tweaker (one validated change per round) re-tunes the ratios over subsequent rounds, but a *manual* window change should be followed by a ratio re-check. Only `averaging_enabled` (the feature on/off switch) stays OFF the tweaker surface — exposed + hand-toggleable, but a structural feature flag isn't something the numeric tweaker should flip.

**Sequence:** build + boundary marker → trader runs/collects → coordinator re-derives on the post-build book → trader signs the new thresholds → ship. The build and the re-baseline are **two commits/versions** (v46 build, then v47-ish re-baseline), mirroring v36→v40.

---

## 6. Config — `indicators.ofi` additions (settings v46)

**Add the two NEW keys to the existing `indicators.OFI` block — do not replace the block.** `book_depth` and the four `momentum_*` keys (live block at `settings.json:238`) stay exactly as they are; only `averaging_enabled` + `avg_window_sec` are added. The full block after the edit:

```json
"OFI": {
  "book_depth": 5,
  "buy_dominant_ratio": 2.0,     // EXISTING — re-baselined post-build (§5); unchanged in v46
  "sell_dominant_ratio": 0.5,    // EXISTING — re-baselined post-build; unchanged in v46
  "momentum_enabled": true,      // EXISTING — unchanged
  "momentum_window": 3,          // EXISTING — unchanged
  "momentum_threshold": 0.15,    // EXISTING — unchanged
  "momentum_bonus": 1,           // EXISTING — unchanged
  "averaging_enabled": true,     // NEW — time-average on the WS path
  "avg_window_sec": 10           // NEW — averaging window / EMA horizon (seconds)
}
```

- `averaging_enabled` + `avg_window_sec` are new and **both exposed in `settings.json`** (no hardcoded magic numbers — project principle). `avg_window_sec` is **on** the tweaker surface (tunable — it shapes the OFI signal), alongside `book_depth` + the dominance ratios. Only `averaging_enabled` (a feature on/off switch, not a threshold) is excluded from the tweaker surface — exposed + hand-toggleable, but not tweaker-flippable.
- Bump v45→v46 + change_log + §15 (the build); the re-baseline is a later bump.

---

## 7. Display-parity

The OFI **SIGNAL BREAKDOWN row** (`Ratio:{OFIRatio} | {OFISignal} | MOM:{OFIMomentum}`) and any OFI card binding render the **averaged** ratio now — same line/format, different value. No new or removed rendered line, so the structure is unchanged, but per the hard parity rule the OFI card binding (`MainForm_Render_Cards.vb`) must be re-verified to show the same averaged value as the snapshot/breakdown (the value source is the same `r.OFIRatio`, so this should hold automatically — confirm in the build).

---

## 8. Edge cases & safety

- **transport=rest / REST-fallback run →** snapshot `CalcOFI` (today's path); never blocks.
- **Cold feed / thin book history (just-connected) →** until warmup completes (≥ `avg_window_sec` wall-clock coverage, or the minimum update count — §4.1), fall back to the latest snapshot (don't emit a half-window average that misreads). The accumulator resets on (re)connect, so this fallback re-arms after every reconnect.
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

## 10. Settled decisions (trader-approved 2026-06-29)

All five confirmed **as recommended** below. **Plus a trader directive:** expose the new params in `settings.json` (no hardcoding) and keep them **tweaker-tunable** — `avg_window_sec` goes **on** the auto-tweaker surface (it shapes the OFI signal → real failure-rate linkage) alongside the dominance ratios + `book_depth`, so a future tweak can optimise it; only `averaging_enabled` (a feature on/off switch) stays off-surface (exposed + hand-toggleable). Reflected in §5/§6/§11. The chosen options:

1. **Averaging mechanism** — recommend **(a) feed-side rolling accumulator** (true per-update time-weighting, O(1)). Alt: (b) sampled ring (simpler, coarser).
2. **Window / weighting** — recommend an **EMA over `avg_window_sec` = 10s** (smooth, recency-weighted) vs a flat mean of the window. (10s ≈ a third of the old 30s cadence / aligns with the tape-window in #3.) **Build it time-aware** — `alpha = 1 - exp(-dt / tau)`, `tau = avg_window_sec` — not fixed-alpha; see §4.1 for the settled formula + warmup/reset gate.
3. **Re-baseline method** — recommend **firing-rate-match to snapshot-OFI history** (v40/v41 precedent) vs a distribution-percentile anchor.
4. **REST-fallback OFI** — recommend **keep snapshot OFI** on fallback runs (never silent) + accept the minor cadence heterogeneity (calibrate on the WS-averaged majority). Alt: skip OFI scoring on fallback runs.
5. **Build/re-baseline split** — recommend **two versions** (v46 build behind the boundary → collect → v47 re-baselined thresholds), mirroring v36→v40. Confirm you want the build to land first (data starts accumulating immediately) vs waiting.

---

## 11. Implementation map (files)

- **New host-agnostic accumulator** (e.g. `OfiAccumulator.vb`, root/`Core/`) — folds each book update's top-book imbalance into a time-weighted average; read at run time. Fed by `DeribitWsFeed` on book updates; exposed via `MarketState`.
- **`DeribitWsFeed.vb` / `MarketState.vb`** — wire the accumulator into the book-update path; expose the current averaged imbalance.
- **`UI/MainForm_Analysis.vb`** (~line 363) — on the WS path, source `r.OFIRatio`/`OFISignal`/`OFIBidVol`/`OFIAskVol` from the averaged imbalance; snapshot `CalcOFI` on REST-fallback. `_ofiHistory`/momentum unchanged.
- **`Core/Settings/EngineSettings.vb` + `settings.json`** — `averaging_enabled` + `avg_window_sec` under `indicators.OFI`; bump v45→v46 + change_log + §15 dataset-boundary marker.
- **`tools/AutoTweaker/`** — exclude **only** `OFI.averaging_enabled` (the feature switch) from the tweaker surface; keep `OFI.avg_window_sec` + the dominance ratios + `book_depth` tweaker-tunable (trader directive — the window is a scoring-affecting threshold). Confirm `avg_window_sec` is reachable by the tweaker's `OFI.*` numeric path and appears in the `PromptBuilder` exposure.
- **`Core/IndicatorResults.vb`** — no new fields (OFIRatio etc. exist); a comment noting the WS-path semantics.
- **`verify/ordercheck/`** — accumulator/averaging fixtures + the `averaging_enabled=false` byte-identical regression.
- **(Later) re-baseline spec-back** — the measured threshold re-derivation, v40/v41 format.

---

## 12. Out of scope / follow-ons

- **#5 aggressor velocity / tape burst** and **#6 book absorption** are the remaining ⚠ re-baseline upgrades, each its own spec.
- **§8 sub-minute cadence** is deprioritized (conflicts with the #2 bar-close discipline; worst calibration cadence — see `on-close-analysis-mode-proposal.md` §12).
