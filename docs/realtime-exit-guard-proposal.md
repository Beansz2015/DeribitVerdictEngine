# Realtime Exit Guard — Proposal (P4 #1)

**Status:** APPROVED — ready for implementer (trader sign-off 2026-06-24; all §9 decisions confirmed as recommended). Build only this approved spec; do not invent design decisions mid-code (CLAUDE.md / trader-profile §7). Local-first — commit as you go, never push (the trader tests + pushes).
**Target:** settings **v42 → v43** (one new `exit_guard` block, off the auto-tweaker surface)
**Scoring impact:** **none** — display/alert only. Never calls `Calculate()`, never writes the CSV, never changes the verdict. Item 1 in `websocket-migration-proposal.md` §11 (unmarked = no re-baseline flag).
**Gate to build:** safe to build *during* early-WS monitoring (zero dataset impact). The first P4 feature per §11/§8 sequencing.

---

## 1. Summary

When a position is declared (`posState ≠ None`), re-run the **fast microstructure exit checks** — the two EXIT-producing layers of `CalcHoldStatus` (2+-adverse fast exit + structural-break exit) plus the single-adverse soft warning — every few seconds against the live `MarketState`, instead of only once per full analysis run. Surface the result as a status-strip line with an optional audible alarm on EXIT.

The full analysis run already computes hold/exit guidance (Step 6, `CalcHoldStatus`), but only on the auto-run cadence (10–60 s). During a 2–15 min hold the microstructure (MicroCVD / TFI / OFI / CVD slope) and the price-vs-swing structural break move faster than that. The exit guard closes the gap: **exit cues at tick freshness instead of poll freshness** — the highest-value single feature WS enables for this trading style (`websocket-migration-proposal.md` §11.1).

It is an *overlay*, not a new signal. It re-evaluates existing logic on fresher data and alerts. It does not vote, score, log, or place orders.

---

## 2. Motivation & profile alignment

- **Profile §2 / §5:** hold 2–15 min, exit on structural break (price through prior swing low/high) or momentum loss, "clear exit rules." The guard is the tick-resolution version of the exit discipline the trader already runs manually on the chart.
- **Profile §6 (conservative, low false-positive tolerance):** a *premature* exit on a noisy single tick is the failure mode to avoid (the same reason Stochastic is rejected — flags exits during valid swings). Addressed by (a) the same 2+-independent-signal bar the full run uses, (b) a short debounce, and (c) a hard feed-health gate so a frozen/stale buffer never fires a false EXIT.
- **No double-counting / no padding (profile §4):** the guard is display/alert only. It adds nothing to any score and introduces no new scoring component. It reuses the *identical* adverse-signal definitions the engine already uses — there is no second, drifting copy of "what counts as adverse."

---

## 3. Scope & non-goals

**In scope**
- A host-agnostic evaluator that recomputes the fast microstructure signals from a `MarketState` snapshot and returns an exit-guard state.
- A WinForms timer + status-strip + optional sound (the thin host layer).
- One new `exit_guard` settings block.
- A behaviour-preserving refactor of `CalcHoldStatus` to share the adverse-count / structural-break primitives (single source of truth).

**Explicit non-goals**
- **No scoring change.** The guard never calls `ScoringEngine.Calculate()`, never touches `ScoreState`, never alters the verdict or any breakdown row.
- **No CSV / eval-cache / dataset impact.** Nothing the guard computes is logged. No schema change, no dataset boundary, no re-baseline.
- **No order management.** The guard alarms; the trader acts. Consistent with the engine being advisory (Kelly is display-only; there is no position-management integration).
- **No new indicator.** It reuses `CalcMicroCVD` / `CalcTFI` / `CalcOFI` / `CalcCVD` unchanged.
- **Not the slower exit layers.** OBV-divergence and RSI/ROC structural layers stay full-run-only — they are candle/RSI-derived and adequately served at run cadence. The guard targets only the streaming-driven layers (see §4.2).

---

## 4. Design

### 4.1 Trigger / lifecycle

The guard timer ticks every `exit_guard.interval_sec` and evaluates **only when all hold**:
1. `exit_guard.enabled` is true, **and**
2. `posState ≠ None` (a position is declared via the existing radio buttons), **and**
3. the WS feed is healthy — `_wsFeed.IsConnected AndAlso Not _wsFeed.IsCoolingDown` and the streams are not stale (`age ≤ network.ws_stale_after_sec`), mirroring the connection-health gate already used by `WsMarketDataSource` and `BuildWsStatusSegment`.

When (2) fails the strip is hidden and the timer does no work (dormant when flat — zero overhead). When (3) fails the strip shows `paused (feed stale/down)` and **does not evaluate** — a stale buffer must never fire a false EXIT.

The guard requires the WS feed (it reads `MarketState`). At `transport=rest` with no feed running, `MarketState` is unpopulated → the strip shows `WS only` / hidden. This is a post-WS feature by nature; that's expected.

### 4.2 What it evaluates — exactly the streaming-driven layers of `CalcHoldStatus`

`CalcHoldStatus` (`Core/ScoringEngine_Helpers.vb`) computes, for a LONG position (mirror for SHORT):

```
microAdverse = MicroCVDSignal ∈ {BEAR_ACCEL, BEAR_DECEL}
ofiAdverse   = OFISignal = "SELL DOMINANT"
tfiAdverse   = TFISignal = "SELL PRESSURE"
cvdAdverse   = CVDSlope = "FALLING" AND CVDValue < 0
adverseCount = microAdverse + ofiAdverse + tfiAdverse + cvdAdverse

Layer 1   : adverseCount ≥ 2                          → EXIT (fast)
Layer 1.5 : LastSwingLow5m > 0 AND CurrentPrice ≤ LastSwingLow5m → EXIT (structural break)
Layer 3   : microAdverse alone (count == 1 via micro) → EVALUATE (soft warning)
```

Every input is reconstructible from `MarketState`:

| Input | Source on a guard tick |
|---|---|
| `MicroCVDSignal` | `CalcMicroCVD(MarketState.trades)` |
| `OFISignal` | `CalcOFI(MarketState.ladder, bookDepth:=cfg.Indicators.OFI.BookDepth)` |
| `TFISignal` | `CalcTFI(MarketState.trades)` |
| `CVDSlope` / `CVDValue` | `CalcCVD(MarketState.trades, MarketState.candles("1"), …)` |
| `CurrentPrice` | latest streaming trade price (`MarketState` tail) |
| `LastSwingLow5m` / `LastSwingHigh5m` | **carried from the last full run** (5m pivots change only on a confirmed 5m pivot — slow; carrying is simpler than recomputing each tick, and the break is detected against the *streaming* price regardless) |

The window-consuming functions take their window from the **end** of the ascending trade buffer (`IndicatorEngine.LastN`), exactly as the full run does — identical methodology, only fresher data.

### 4.3 Shared primitive (no reimplementation)

To guarantee the guard and the full-run `CalcHoldStatus` never drift on "what counts as adverse," extract the primitives into one host-agnostic helper:

```vb
' Core/ScoringEngine_Helpers.vb (host-agnostic, pure)
Friend Shared Function ComputeFastExitPrimitives(r As IndicatorResults, posState As PositionState) _
    As (AdverseCount As Integer, AdverseSignals As String(), StructuralBreak As Boolean, BreakLevel As Double)
```

- `CalcHoldStatus` is refactored to consume this for its Layer 1 / 1.5 / 3 branches — **output byte-identical** (a pure refactor; proven by the existing hold-status harness fixtures).
- The exit-guard evaluator consumes the *same* helper, so the guard's adverse-count is, by construction, the engine's adverse-count.

### 4.4 Host-agnostic evaluator

```vb
' ExitGuardEvaluator.vb (root, host-agnostic — no WinForms; reused by the Linux port)
Public Function Evaluate(state As MarketState,
                         posState As PositionState,
                         lastSwingLow5m As Double, lastSwingHigh5m As Double,
                         cfg As EngineSettings) As ExitGuardResult
```

`Evaluate` recomputes the four streaming signals from `state`, fills a lightweight `IndicatorResults`, calls `ComputeFastExitPrimitives`, and maps to an `ExitGuardResult { Kind ∈ {Clear, Warn, Exit}, Reason, AdverseSignals, BreakLevel }`. No I/O, no WinForms, never throws into the caller.

### 4.5 Host layer (WinForms, thin)

- A `System.Windows.Forms.Timer` on `MainForm`, interval `exit_guard.interval_sec`, started/stopped with the auto-run lifecycle; the tick handler runs §4.1 gating → `ExitGuardEvaluator.Evaluate` → strip update + sound.
- The strip is a status-bar element (sibling of the WS-health line / perf strip), rendered in the LOG cascade region, visible only when `posState ≠ None`.
- Sound: `System.Media` on EXIT *transition* only (host-specific; the evaluator returns the state, the host decides to play). Linux port wires its own alert.

### 4.6 Strip states & alarm stability

| State | Strip text (example) | Sound |
|---|---|---|
| Clear | `EXIT GUARD · clear` | — |
| Warn (1 adverse) | `EXIT GUARD · 1 adverse (TFI SELL)` | — |
| Exit (2+ adverse) | `EXIT GUARD · ⚠ EXIT — 2 adverse (MicroCVD BEAR_ACCEL, TFI SELL)` | ✔ |
| Exit (structural) | `EXIT GUARD · ⚠ EXIT — structural break (swing low 64210.0)` | ✔ |
| Paused | `EXIT GUARD · paused (feed stale)` | — |

- **Debounce:** an EXIT must hold for `exit_guard.debounce_evals` consecutive ticks before the strip latches EXIT and the sound fires — kills single-tick jitter (anti-premature-exit, profile §6). At the default 3 s interval × 2 evals ≈ 6 s confirmation, still far tighter than the 10–60 s poll.
- **Latch / re-arm:** once EXIT, the strip stays EXIT and the sound does **not** repeat each tick; it auto-clears (and re-arms the alarm) after the condition resolves for `debounce_evals` consecutive ticks, or immediately when the position is cleared (radio → None).

### 4.7 Direction awareness

`posState` carries Long/Short; the evaluator uses the matching adverse set (bearish signals + swing-low break for a long; bullish + swing-high break for a short), exactly as `CalcHoldStatus` already branches.

---

## 5. Config — new `exit_guard` block (settings v43)

```json
"exit_guard": {
  "enabled": true,
  "interval_sec": 3,
  "debounce_evals": 2,
  "sound_enabled": true
}
```

- `EngineSettings.vb` gains an `ExitGuardSettings` POCO with these defaults.
- Reuses `cfg.Indicators.OFI.BookDepth` for the OFI recompute — **no** duplicate depth key.
- **OFF the auto-tweaker surface** — a trader-risk/display preference, not a failure-rate-linked threshold. Same exclusion class as `kelly.*`, `scoring.min_tradeable_move_pct`, `execution_resolution`: add to `SettingsDiffApplier` `RejectedPathPrefixes` (`"exit_guard."`) + a PromptBuilder HARD CONSTRAINT.
- Hot-reloadable (rides the existing `FileSystemWatcher`); UI toggle for `sound_enabled` optional.
- Version bump v42 → v43 + `change_log` entry on implementation.

---

## 6. Display-parity rule — explicitly out of scope

Per the CLAUDE.md engine display-string parity rule (card ↔ `BuildPlaintextSnapshot`): the EXIT GUARD strip is a **live status-bar element**, like the WS-health line (`BuildWsStatusSegment`) and the perf strip — it is **not** part of the RTF verdict output, **not** emitted by `BuildPlaintextSnapshot`, and **not** a card surface. So it carries **no card-binding obligation**. (Flagged here so the implementing commit can state "no card surface affected" with the reason on record.)

---

## 7. Edge cases & safety

- **Feed degraded/down/stale →** `paused`, no evaluation, no alarm (never fire on stale data).
- **No WS feed (transport=rest) →** strip hidden / `WS only`. The guard is a WS-mode feature.
- **Flat (posState=None) →** dormant; timer does no work.
- **Thin/early trade buffer (just after connect/reconnect) →** the recompute functions already handle short windows; treat an empty/degenerate result as `Clear` (no false EXIT), and the staleness gate covers the reconnect window.
- **Advisory only →** no auto-flatten, no orders, ever.

---

## 8. Acceptance

- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck.
- **Refactor regression:** `CalcHoldStatus` output byte-identical after the `ComputeFastExitPrimitives` extraction — existing hold-status harness fixtures unchanged.
- **Evaluator harness (new):** 2 adverse + long → Exit; structural break (price ≤ swing low) → Exit; 1 adverse → Warn; 0 adverse → Clear; mirror for short; feed-stale stub → Paused (no eval); empty buffer → Clear.
- **Dormancy:** posState=None → no evaluation/allocation.
- **`transport=rest` byte-identical** — no feed, guard inert; the live verdict path is untouched.
- Host-agnostic check: `ExitGuardEvaluator` + `ComputeFastExitPrimitives` reference no `System.Windows.Forms`.

---

## 9. Settled decisions (trader-approved 2026-06-24)

All six confirmed **as recommended** — the §5 config defaults are final. Rationale retained for the implementer:

1. **`enabled` = true** — active whenever a position is declared; dormant (and harmless) when flat.
2. **`interval_sec` = 3** (range 2–5) — recompute is cheap (in-memory `MarketState`).
3. **`debounce_evals` = 2** — ~6 s confirmation at the 3 s interval; anti-jitter (profile §6 false-positive aversion).
4. **`sound_enabled` = true** — audible cue when not watching the screen; expose a UI toggle.
5. **Single-adverse Warn state surfaced** — informational, no sound; the "watch" precursor below EXIT.
6. **Latch = auto-clear** after the condition resolves for `debounce_evals` consecutive ticks, plus immediate clear on position-flat (no manual ack).

---

## 10. Out of scope / natural follow-ons

- **#2 On-close analysis mode** and **#3 LIVE microstructure strip** (`websocket-migration-proposal.md` §11) reuse the same streaming-consume-`MarketState` pattern this spec establishes — each its own spec.
- This spec deliberately does **not** add latency/health metrics — item #10 (connection-quality strip) already ships as `BuildWsStatusSegment` (v39).

---

## 11. Implementation map (files)

- **New, host-agnostic — `ExitGuardEvaluator.vb` (root):** `Evaluate(state As MarketState, posState, lastSwingLow5m, lastSwingHigh5m, cfg) As ExitGuardResult`. Recomputes MicroCVD/TFI/OFI/CVD from `state` via the existing pure indicator functions, calls the shared primitive, maps to `{Clear, Warn, Exit}`. No WinForms. Reused by the Linux port.
- **`Core/ScoringEngine_Helpers.vb`:** extract `ComputeFastExitPrimitives(r, posState) → (AdverseCount, AdverseSignals, StructuralBreak, BreakLevel)`; refactor `CalcHoldStatus` Layers 1 / 1.5 / 3 to consume it — **output byte-identical** (regression-proven).
- **`Core/Settings/EngineSettings.vb` + `settings.json`:** new `ExitGuardSettings` POCO (`enabled`/`interval_sec`/`debounce_evals`/`sound_enabled`) + the `exit_guard` block; bump **v42 → v43** + `change_log` entry + `DeribitIndicatorProject.md` §15 row.
- **`tools/AutoTweaker/` — `SettingsDiffApplier` + `PromptBuilder`:** add `"exit_guard."` to `RejectedPathPrefixes` + a new HARD CONSTRAINT (off the tweaker surface, same class as `kelly.*` / `min_tradeable_move_pct`).
- **`UI/MainForm_*`:** a `System.Windows.Forms.Timer` started/stopped with the auto-run lifecycle; the §4.1 gating; the status strip in the LOG-cascade region (visible only when `posState ≠ None`); `System.Media` on the EXIT transition; carry the last full-run swing levels (`LastSwingLow5m`/`LastSwingHigh5m`) for the structural-break check.
- **`verify/ordercheck/` (gitignored harness):** the §8 evaluator fixtures (2-adverse → Exit, structural break → Exit, 1-adverse → Warn, 0 → Clear, mirror short, feed-stale → Paused, empty buffer → Clear) + the `CalcHoldStatus` byte-identical regression.
