# LIVE Microstructure Strip — Proposal (P4 #3)

**Status:** PROPOSED — awaiting trader sign-off (2026-06-25)
**Target:** settings **v44 → v45** (one new `live_strip` block)
**Scoring impact:** **none** — display-only. Never calls `Calculate()`, never writes the CSV, never emits a verdict. No re-baseline.
**Item:** #3 in `websocket-migration-proposal.md` §11 (unmarked = display-only).
**Gate to build:** safe anytime — WS validated, zero engine coupling. Third P4 feature; pairs with #1 (exit guard) and #2 (on-close).

---

## 1. Summary

A continuously-updating one-line **TAPE** strip showing the fast streaming microstructure *between* full analysis runs — last price vs the nearest structural level, TFI, spread, top-book imbalance, and tape speed — at ~2s freshness. It's situational awareness, not a signal: the §11 intent is explicit — "scoped to the fast indicators so the **verdict itself stays a deliberate, full-pipeline product**." The strip never produces or implies a directional call.

It completes the P4 live-trio: **#1** watches an open position (exit alarm), **#2** times the entry verdict to bar close, **#3** gives always-on "what's the tape doing right now" between those deliberate runs.

---

## 2. Motivation & profile alignment

- The trader watches the tape while waiting for an entry (is price approaching the swing? is flow turning? is the tape accelerating into the level?) and during a hold. Between full runs — now bar-close-aligned (#2) or on the interval — there's no engine readout. The strip fills that gap with the fast, streaming-reconstructable indicators at tick freshness.
- **Deliberately NOT a verdict** (profile §6, conservative; "quality over quantity"). Surfacing a live *score* between runs would invite over-trading on provisional, forming-bar noise — exactly what the full-pipeline-on-close discipline avoids. The strip shows raw microstructure *inputs*, visually distinct from the verdict, so it informs without tempting a marginal entry.
- Reuses indicators already in the trader's PREFERRED set (TFI, spread, order-flow imbalance) — no new indicator, no correlation increase.

---

## 3. Scope & non-goals

**In scope**
- A host-agnostic evaluator that reads the live `MarketState` + the last run's carried levels and returns a microstructure snapshot.
- A WinForms strip (toggle-able) that renders it on a ~2s timer.
- One new `live_strip` settings block.

**Explicit non-goals**
- **No verdict, no score, no direction call.** The strip shows inputs (TFI, spread, imbalance, tape speed, level proximity) — never a LONG/SHORT/score. The full pipeline stays the only verdict source.
- **No scoring/CSV/dataset change.** Never calls `Calculate()`, never logs, no schema change, **no re-baseline.**
- **Not the #5 scoring feature.** Tape speed here is a **display readout only**. §11 #5 (aggressor velocity / tape burst) would fold velocity into *scoring* — a ⚠ re-baseline item with its own spec. Building the readout now does **not** preempt or constrain that; it just shows the number.
- **No new data source.** Reads the existing `MarketState` (trades + book) + the carried `IndicatorResults` levels.

---

## 4. Design

### 4.1 Lifecycle — always-on when enabled (NOT position-gated)

Unlike the exit guard (position-gated), the strip is **general** awareness — useful flat (watching a level for entry) or in a position. So it renders whenever:
1. `live_strip.enabled` is true, **and**
2. the WS feed is healthy + fresh (`_wsFeed.IsConnected AndAlso Not IsCoolingDown`, book/trades age ≤ `ws_stale_after_sec`).

A ~2s `System.Windows.Forms.Timer` (UI thread — pure display, light windowed compute, no async trigger, so no marshalling needed; simpler than #2's `Threading.Timer`) recomputes + repaints. Created at form load, disposed on close; self-gates each tick. WS-mode feature: at `transport=rest` / no `_marketState` it shows `TAPE · WS only` (or hides — §9). Independent of the exit-guard timer and the on-close watcher.

### 4.2 Fields & sources (all from `MarketState` + carried levels)

| Field | Source |
|---|---|
| **Last price** | `MarketState.GetTrades().Last().Price` (streaming tail) |
| **Nearest level + Δ** | from the last full run's carried `_lastSuccessfulIndicators`: `LastSwingHigh5m` / `LastSwingLow5m` / `VPFRNearestHvnAbove` / `VPFRNearestHvnBelow` (0 = none). Pick the nearest to the live price; label `SH`/`SL`/`HVN↑`/`HVN↓`; show signed Δ. |
| **TFI** | `CalcTFI(MarketState.GetTrades())` → `TFISignal` + `TFIValue` (the same pure fn the full run + exit guard use) |
| **Spread** | `MarketState.GetBook()` top-of-book → `(ask−bid)/mid × 10000` bps (the `SpreadBps` formula) |
| **Top-book imbalance** | `MarketState.GetBook()` L1 (or top-N) bid vs ask size → ratio + dominant side (the `CalcOFI` basis / `OFISignal` vocabulary) |
| **Tape speed** | scan `GetTrades()` from the tail while `Timestamp ≥ now − window`: **trades/sec** = count/window; **USD/sec** = Σ`Amount`/window (`Amount` is the Deribit inverse-perp USD notional). `now` = wall-clock, so a lull correctly reads ~0. |

Levels are **carried, not recomputed** (5m swing / VPFR HVN are slow; they refresh each full run). The price, TFI, spread, imbalance, and tape speed are recomputed live. Identical methodology to the full run (same `CalcTFI`, same spread formula, same OFI basis) — only fresher.

### 4.3 Host-agnostic evaluator

```vb
' LiveMicrostructureEvaluator.vb (root or Core/, host-agnostic — no WinForms)
Public Shared Function Evaluate(state As MarketState, lastRun As IndicatorResults, cfg As EngineSettings) As MicrostructureSnapshot
```

`MicrostructureSnapshot` carries the §4.2 fields (LastPrice, NearestLevelPrice/Label/Delta, TfiSignal/TfiValue, SpreadBps, ImbalanceRatio/Side, TradesPerSec, UsdPerSec). Reads a `MarketState` snapshot (copy-on-read) + 4 level fields off `lastRun`; never throws; degenerate/empty buffer → safe blanks (`--`). The Linux port reuses it as-is.

### 4.4 Strip render

A compact `·`-separated line, e.g.:

```
TAPE · 62450 · +18 → SH 62468 · TFI BUY +0.42 · 1.3 bps · book 1.8× bid · 22 tr/s ($0.9M/s)
```

- Visually distinct from the verdict (label `TAPE`, neutral/dim styling — never the verdict colour ramp), so it reads as a readout, not a call.
- `--` for any field with no data yet (e.g. no carried level, thin buffer).
- Recomputed + repainted each ~2s tick; no animation, no flicker (set text only when changed).

### 4.5 Placement

A dedicated status-bar strip near the verdict header (glanceable alongside the verdict), or beside the exit-guard row — **§9 decision**. Default recommendation: a thin full-width line directly under the verdict header, so the trader reads "deliberate verdict + live tape" together. Not an RTF/snapshot/card surface (see §6).

---

## 5. Config — `live_strip` block (settings v45)

```json
"live_strip": {
  "enabled": false,
  "refresh_sec": 2,
  "tape_window_sec": 10
}
```

- `LiveStripSettings` POCO: `Enabled` (default **false** — see §9), `RefreshSec` (2), `TapeWindowSec` (10).
- Reuses `cfg.Indicators.OFI.BookDepth` for the imbalance depth — no duplicate key.
- **Off the auto-tweaker surface:** add `"live_strip."` to `SettingsDiffApplier.RejectedPathPrefixes` + a PromptBuilder HARD CONSTRAINT (display preference, no failure-rate linkage — same class as `kelly.*` / `exit_guard.*` / `auto_run.*`).
- Runtime toggle saves `bumpVersion:=False` (operational); only the one-time key add bumps v44→v45 + `change_log` + §15.

---

## 6. Display-parity rule

The strip is a live status-bar element like `BuildWsStatusSegment` / the exit-guard row / the perf strip — **not** the RTF verdict, **not** `BuildPlaintextSnapshot`, **not** a card. So **no card-binding obligation** (flagged for the commit). The verdict output is wholly unaffected.

---

## 7. Edge cases & safety

- **Feed stale/down / `transport=rest` →** `TAPE · WS only` (or hidden); never renders stale numbers as live.
- **Thin/empty trade buffer (just-connected) →** fields blank (`--`); tape speed 0; never throws.
- **No carried levels yet (first run not done) →** level field `--`; the rest still render.
- **Disabled →** hidden, timer no-ops (kept alive for instant re-enable on hot-reload, mirroring the exit guard).
- **Pure display →** never blocks, never logs, never touches the verdict path; an evaluator exception is swallowed (advisory).

---

## 8. Acceptance

- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck.
- **No scoring change:** A1–A18e unregressed (engine path untouched; the strip never calls `Calculate()`).
- **Evaluator harness (new):** given a `MarketState` + levels — TFI/spread/imbalance computed correctly; nearest-level selection (above vs below, nearest wins); tape-speed window (trades in last `window` counted, older excluded; lull → 0); empty buffer → blanks, no throw.
- **`transport=rest` / disabled →** strip inert; verdict path byte-identical.
- Host-agnostic: `LiveMicrostructureEvaluator` references no `System.Windows.Forms`.

---

## 9. Open decisions for trader sign-off

1. **Default `enabled`** — recommend **false** (opt-in). Unlike the exit guard (position-gated → invisible when flat), this strip is *always visible* when on; defaulting off keeps the UI clean until you flip it. (The exit guard defaulting on was safe precisely because it self-hides.)
2. **Placement** — recommend a thin full-width line **under the verdict header** (verdict + live tape read together). Alt: beside the exit-guard row in the SETTINGS & TOOLS card.
3. **Field set / order** — recommend the §4.4 set (price · Δ-to-nearest-level · TFI · spread · book imbalance · tape speed). Drop/reorder any.
4. **Nearest level: single nearest vs above+below** — recommend **single nearest** (compact). Alt: show both nearest-above and nearest-below.
5. **Tape speed metric** — recommend show **both** `tr/s` and `$/s` (both cheap; `$/s` is the richer flow read). Or pick one.
6. **`refresh_sec` / `tape_window_sec`** — recommend 2s / 10s.

---

## 10. Implementation map (files)

- **New, host-agnostic — `LiveMicrostructureEvaluator.vb`** (root or `Core/`): `Evaluate(state, lastRun, cfg) → MicrostructureSnapshot`; reuses `CalcTFI`, the `SpreadBps` formula, the `CalcOFI`/`OFISignal` imbalance basis, + a small tape-speed window scan. No WinForms.
- **`Core/Settings/EngineSettings.vb` + `settings.json`** — `LiveStripSettings` POCO + the `live_strip` block; bump v44→v45 + change_log + §15.
- **`UI/MainForm_*`** — a `System.Windows.Forms.Timer` (~2s) + the strip label; created at form load, disposed on close; the §4.1 gate; carried levels from `_lastSuccessfulIndicators` (already populated for the exit guard). A toggle (checkbox/menu) for `enabled`.
- **`tools/AutoTweaker/`** — `"live_strip."` in `SettingsDiffApplier.RejectedPathPrefixes` + a new PromptBuilder HARD CONSTRAINT.
- **`verify/ordercheck/`** — the §8 evaluator fixtures.

---

## 11. Out of scope / follow-ons

- **#5 aggressor velocity / tape burst** (⚠ re-baseline) is the *scoring* sibling of this strip's tape-speed readout — its own spec, sequenced after the display items per §11.
- **#4 time-averaged OFI** (⚠ re-baseline) remains the first scoring upgrade after the display trio (#1–#3 + #10 already shipped).
- With #3, the zero-scoring-impact display wave from the §11 catalogue is complete; what remains in P4 are the ⚠ re-baseline indicator upgrades (#4/#5/#6), each gated on its own spec + re-baseline.
