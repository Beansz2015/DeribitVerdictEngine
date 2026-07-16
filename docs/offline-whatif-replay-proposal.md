# Offline What-If Replay Runner — Proposal

**Date:** 2026-07-16 · **Seat:** Fable coordinator · **Status:** PROPOSED — D-table awaits trader
**Type:** analysis-only instrument (`tools/` + `analysis/` class) — **zero scoring impact, no ⚠ boundary, never writes settings.**
**Driver:** trader request 2026-07-16 — the calibrate-forward loop cannot answer *"which settings would have been optimal on the book so far"*; hypotheses (e.g. LONDON stop bounds, verdict selectivity) currently need one-off coordinator analyses. This tool makes that a repeatable, guard-railed instrument the trader drives.
**Prototype evidence:** the D6 D4 both-ways report and the 07-15 F3 evaluation are exactly this computation, hand-run.

---

## 1. What it is

A host-agnostic console runner: take the logged book (CSV v0.8+ rows) + 1m OHLC, apply a **settings overlay** (a small JSON fragment of hypothesis values), re-derive per row the placed levels and/or verdict tier under the overlay, re-walk outcomes, and print a **baseline-vs-overlay failure report** on identical rows — per session × resolution × tier, with trade counts and confidence intervals.

```
WhatIfRunner <overlay.json> [--from 2026-07-08] [--to 2026-07-31]
→ whatif_report_<stamp>.md   (baseline vs overlay, side by side)
```

## 2. v1 knob whitelist (backtestable-from-logs — everything else is REJECTED loudly)

| Knob | Replay inputs (all logged) |
|---|---|
| `scoring.atr_target_multiplier` / `atr_stop_multiplier` | entry(2), ATR(65) |
| `scoring.structural_levels.sessions[].fallback_target_atr_mult` | + session from timestamp hour |
| `scoring.structural_levels.target_max_atr_mult` / `stop_max_atr_mult` / `stop_min_floor_ticks` | + SwingTarget/Stop (81–84), HVN above/below (75/76) |
| `scoring.verdict_strong_pct` / `med_pct` / `weak_pct` (+ tier floors) | EffectiveLong/ShortScore (7/8), MaxScore (9), MTFGatePassLong/Short (59/60) |
| `scoring.min_tradeable_move_pct` | replayed placed target + entry |
| eval window (the existing 5/10/15-scaled matrix dimension) | OHLC |

**Explicitly NOT in v1:** any indicator-computation knob (raw streams unlogged — OFI windows, burst windows, funding rings are forward-calibrated by necessity) and any Step-2/Pass-2x score *recomposition* (raw scores are replayed as logged, never rebuilt). The whitelist is enforced in code: an overlay key outside it fails the run with a named error — no silent no-ops (the v47-F1 lesson).

## 3. Mechanism — one seam, no copies

- **CsvRow → minimal `IndicatorResults` adapter** (the fields `ComputeSideLevels` reads), then the runner calls the **actual shipped `SignalEmitter.ComputeSideLevels`** (pure, host-agnostic, linked source) with a cfg = live settings + overlay. The replay arbitration cannot drift from production because it *is* production.
- Verdict re-derivation: `Math.Ceiling(MaxScore × pct)` thresholds against the logged effective scores; the logged per-side MTF flags re-apply the veto; min-move gates on the replayed placed target. Population shifts (rows entering/leaving directionality) are expected and **reported**, never hidden.
- Outcome walk: the existing `FailureRateMatrix.WalkBars` on 1m OHLC (fetch per `AnalysisRunner` precedent when the span exceeds the 7-day cache), resolution-scaled windows as shipped.
- **POC-tier caveat:** `VPFRPoc`/`VPFRSignal` are not logged, so the replay ladder is swing → HVN → fallback; rows whose LIVE placement was `poc` (TargetCapReason bucket) are excluded and counted in the report header. Measured population: near-zero (the POC tier is a documented rarity).
- Rows: **v0.8+ only** (the columns exist; the book is growing fast; no legacy fabrication).

## 4. Output & guardrails (binding)

Per session × resolution × tier, baseline vs overlay on the same rows: `n directional | SUCC% | ADVERSE-first% | EXPIRED% | BELOW_MIN excluded`, each rate with a Wilson 95% CI; cells n<30 flagged. Plus a **population-shift line** (directional count baseline → overlay) whenever verdict knobs are in the overlay.

1. **Motivates, never is.** A what-if result feeds a spec proposal; the runner never writes `settings.json`, and no live change ships without the normal spec-first + trader-tick + own-watch discipline.
2. **Overfit banner.** The report header states how many overlays have been run against this book span (a counter file) and the expected-false-winners arithmetic. Trying many knobs on one book *will* find phantom winners — this tool makes the temptation cheap, so the report makes the risk loud.
3. **Touch-based caveat** printed on every report (mid-price barriers, no fills/slippage — W6-6 closes that loop).
4. First registered use cases: **W6-1 LONDON `stop_max` 2.0/2.2** and **LONDON STRONG-only selectivity** — the two named candidates decide on evidence from this instrument.

## 5. Home & relationship to existing instruments

`tools/WhatIfRunner/` — separate net8.0 console project (zero WinForms; AutoTweaker/ordercheck precedent; root vbproj already excludes `tools/**`), linked-source `SignalEmitter` + `analysis/` pieces. Manual, hypothesis-driven counterpart to the auto-tweaker (which optimizes without hypotheses) and to W6-4 (which measures the ceiling without proposing knobs). Fixtures ride `verify/ordercheck` (adapter fidelity via the **empty-overlay reproduction test**: replayed placements ≡ logged `Placed*` on real rows; whitelist rejection; a verdict-threshold replay case) — **serialize against the #6 lane on the shared fixture file; #6 has priority.**

## 6. D-table (trader sign-off)

| # | Decision | Recommendation |
|---|---|---|
| **W1** | v1 knob whitelist | §2 as listed (geometry + ladder bounds + verdict boundaries + min-move + window) |
| **W2** | Overlay format | Partial `settings.json` fragment, same key paths (familiar; whitelist-validated) |
| **W3** | POC tier | Exclude-and-label (unlogged inputs; near-zero population) |
| **W4** | Guardrails | §4 all four, binding |
| **W5** | Sequencing / model | Build **after #6 lands** (shared fixture file); **Opus, medium**, one conversation; no boundary |

## 7. Acceptance

Builds 0/0 (new project + solution untouched); ordercheck unregressed + the new fixtures (empty-overlay reproduction on real captured rows, whitelist reject/accept, threshold-replay population shift, POC-row exclusion); a live smoke: one real overlay (LONDON stop_max 2.0) produces a report whose baseline column matches the standing failure matrix for the same span.
