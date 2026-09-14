# DeribitVerdictEngine — Project Handover Document
**This header deliberately carries NO version number and NO "last updated" date.** It carried *"Last updated: 2026-06-11 | Current version: settings.json v31"* while the tree ran to **v68** — **thirty-seven versions stale, in the document `CLAUDE.md` names as the FIRST session-start read.** Corrected 2026-08-24.

- **For the live settings version, read the tracked repo-root `settings.json` line 2.**
- **For what each version changed, read §15 below** (most recent five) and [`history-archive.md`](history-archive.md) §E (everything older).
- **For when this file last moved, read `git log -1 -- docs/DeribitIndicatorProject.md`.**

> **The rule, same as [`architecture.md`](architecture.md)'s header and for the same reason: a doc header must not carry a number that lives somewhere else.** Both failed identically — silently, and in the place a reader trusts most. Structural facts belong in the document; *current* facts belong at their source, with a pointer.

> **Trimmed 2026-09-14 (UTC).** Superseded and history text in this file moved verbatim to [`history-archive.md`](history-archive.md) §I. Every moved block leaves a pointer carrying its `trim-2026-09-14-NN` id. Ledger: [`doc-trim-log.md`](doc-trim-log.md). Pre-trim original: git tag `doc-trim-2026-09-14-pre`; byte copy under `docs/archive/doc-trim-2026-09-14/originals/`.

Operational reference for any AI conversation continuing this project. Historical content — pre-v27 settings change rationale, full version history back to v0.33, completed spec bundles, resolved parked observations — lives in `docs/history-archive.md`.

**Session start checklist:**
1. Read this file + `docs/architecture.md`.
2. Load the `crypto-trading-context` skill (writing style + a generic trader-profile copy), **then read `docs/trader-profile.md` in full** — ruled 2026-09-14. It was re-synced 2026-09-14 (trader-ruled) and its §5 is the single home of the ATR bands.
3. Do NOT read individual `.vb` files at session start — only open them when a specific edit is required.

---

## 1. Project Purpose

Windows Forms (VB.NET / .NET 8) desktop app. Polls Deribit REST for BTC-PERPETUAL, computes technical indicators on 1m/5m/15m candles, scores them through a multi-tier pipeline, emits a verdict (STRONG LONG / LONG / WEAK LONG / NO TRADE / WEAK SHORT / SHORT / STRONG SHORT) with ATR-based entry/stop/target levels.

**This section no longer carries current state.** For the version, read the tracked repo-root `settings.json` line 2; for recent changes, `DeribitIndicatorProject.md` §15; for state, the current seat handover named in the [`trader-tick-queue.md`](trader-tick-queue.md) state banner. Since v42 (2026-06-24) market data arrives over WebSocket and REST is the fallback (see [`architecture.md`](architecture.md) Design Decisions). The v30-era state paragraph and the May 2026 shipment list moved verbatim: [`history-archive.md` §I, `trim-2026-09-14-01`](history-archive.md#trim-2026-09-14-01).

---

## 2. Repository

- **GitHub:** https://github.com/Beansz2015/DeribitVerdictEngine
- **Branch:** `master`
- **Solution file:** `DeribitVerdictEngine.sln`
- **Target framework:** .NET 8, Windows Forms
- **Working tree:** `C:\Dev\DeribitVerdictEngine` (root). Visual Studio solution points here. All code edits target root, not any `.claude\worktrees\` subdirectory.

---

## 3. File Inventory

**The file map is [`architecture.md`](architecture.md) Directory Layout.** This section duplicated it and had gone stale: it listed `MainForm_Render_Header.vb` and `MainForm_Render_Sections.vb` (both deleted in P5b) and `settings.json` v30. For a file the Directory Layout does not list, check the tree. The old inventory, including the April-May spec-doc list, moved verbatim: [`history-archive.md` §I, `trim-2026-09-14-02`](history-archive.md#trim-2026-09-14-02).

---

## 4. Indicator Signal Map

### Core Signals (always scored)
| Indicator | Method | Config keys |
|---|---|---|
| ROC(9) | CalcROCSeries | `cfg.Indicators.ROC.SlopeDeltaThreshold` (0.05) — bar-to-bar delta for RISING/FALLING/FLAT. `cfg.Indicators.ROC.MagnitudeThreshold` (0.1) — gates partial scoring + Pass 2c ROC-active check. Split in v21. |
| RSI(9) | CalcRSI | `Overbought` (60) / `Oversold` (40) / `PartialOverbought` (55) / `PartialOversold` (45). |
| RSI Divergence | CalcRSIDivergence | v21 semantic rewrite: BEARISH fires when current price ≥ prior pivot AND prior pivot RSI ≥ `DivergenceOverboughtThreshold` (65). Mirror for BULLISH. Most-recent confirmed pivot used. `DivergenceRsiDelta` 5.0. |
| DMI/ADX | CalcDMI | 5m candles. `cfg.Indicators.ADX.TrendThreshold` (25). |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms, session-adjusted via `session_volume`. |

### Tier 1
| Indicator | Method | Config keys |
|---|---|---|
| VWAP Dev | CalcVWAP | Dual-session. `cfg.Indicators.VWAP.WarmupCandles` (15). |
| VWAP σ Bands | CalcVWAPBands | σ1/σ2 bands; PARTIAL→UPGRADED when price between bands. |
| BBW / TTM Squeeze | CalcBBW + CalcTTMSqueeze | `cfg.Scoring.BbwSqueezePenalty` (2); `cfg.Indicators.TTM.FlatThreshold` (0.5). |
| EMA Ribbon | CalcEMA | 9/21/50 on 1m → BULL/BEAR/MIXED; 5m EMA(200) regime anchor. |
| Funding Rate | GetFundingRateAsync | Step 3 baseline funding modifier from cfg thresholds. |
| Funding Momentum | CalcFundingMomentum | `cfg.Indicators.Funding.MomentumEnabled/WindowMinutes/Threshold/Amplify/Soften`. **v53: time-anchored** — delta vs the newest sample ≥ `momentum_window_minutes` (5) old, not a 3-*change* count window. Cadence-independent by construction. |
| OI Change | OiSnapshot | 15m + 60m delta → NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL. |
| OI × CVD Cross-Confirm | Pass 2b | `cfg.Indicators.OiCvd.Enabled/UpgradeBonus/ConflictPenalty`. |
| Trend Structure (D1) | ClassifyTrendStructure | HH/HL/LH/LL classification on 5m. Pass 2c `StructureBonus` capped at regimeMax. |

### Tier 2
| Indicator | Method | Config keys |
|---|---|---|
| Bid-Ask Spread | order book | `cfg.Indicators.Spread.WideThresholdBps` (JSON `wide_threshold_bps`, default 5.0). WIDE penalty in Step 2. ⚠ **This cell named `WidePenaltyThresholdBps` until 2026-08-25 — no such property has ever existed.** Anyone grepping the documented name found nothing, and it cost the 2026-08-11 seam auditor a false finding mid-sweep. Verified against `Core/Settings/EngineSettings.vb:632`. |
| OFI | CalcOFI | `cfg.Indicators.OFI.BookDepth` (5); `BuyDominantRatio` (2.0) / `SellDominantRatio` (0.5). |
| OFI Momentum | CalcOFIMomentum | RISING/FALLING/FLAT modifier. Ring buffer `_ofiHistory` (max=10). |
| Liquidations | CalcLiquidations | `cfg.Indicators.Liquidations.DominanceRatio` (2.0). |
| CVD | CalcCVD | 3-segment slope (late ×2 / early ×1 from cfg). `SlopeMinUsd` (12000). |
| MicroCVD | CalcMicroCVD | BULL/BEAR_ACCEL/DECEL + FLAT stall penalty. Dynamic accel threshold (`totalWindowUsd × pct` with static floor). |
| TFI | CalcTFI | BUY/SELL PRESSURE. Window=30, threshold=0.15. |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW regime anchor. |

### Tier 3
| Indicator | Method | Config keys |
|---|---|---|
| Donchian(20) | CalcDonchian | Full LONG/SHORT + quartile partial (`cfg.Indicators.Donchian.QuartilePct`=0.25). |
| OBV | CalcOBV | Trend + divergence gate. Adverse divergence blocks cross-category upgrade. |
| VPFR-lite v2 | CalcVPFRLite | POC proximity; VAH/VAL; `VPFRNearestHvnAbove/Below` for 3-tier target cap. Exp decay (0.985). |
| Swing Pivots | CalcSwingPivots | 5m primary (wing=3, lookback=30) + 15m context (wing=2, lookback=20). Direction-aware bookkeeping. |
| Volume-Weighted Pivots (D2) | extension of CalcSwingPivots | `BestPivotByVolume5m`, `BestPivotVolumeRatio5m`, `BestPivotIsHigh5m`. Display-only v1. v2 cap promotion parked as P1. |

### Multi-Timeframe Gate
| Indicator | Method | Notes |
|---|---|---|
| MTF Gate (15m) | CalcMTFGate | 15m DMI/ADX + EMA alignment; PASS/BLOCK; forces NO TRADE on BLOCK. TTL cache 60s. 1-bar regime hysteresis. Three Reason formats by design (see `architecture.md`). |

---

## 5. Verdict Levels

| Verdict (displayed) | Stored / wire string | Meaning |
|---|---|---|
| STRONG LONG   | STRONG LONG  | High-confidence long |
| MEDIUM LONG   | LONG         | Standard long |
| WEAK LONG     | WEAK LONG    | Low-confidence long |
| NO TRADE      | NO TRADE     | Insufficient signal or MTF block |
| WEAK SHORT    | WEAK SHORT   | Low-confidence short |
| MEDIUM SHORT  | SHORT        | Standard short |
| STRONG SHORT  | STRONG SHORT | High-confidence short |

**Display ↔ stored mapping (v55, 2026-07-21).** The plaintext snapshot's `VERDICT:` line and the verdict card render the middle band as `MEDIUM LONG` / `MEDIUM SHORT` so the on-screen ladder reads STRONG / MEDIUM / WEAK explicitly. The **stored/wire** strings stay bare `LONG` / `SHORT` for the middle band — the CSV `Verdict` column, the bridge payload `verdict` field, the eval cache, and every string-matching site (`AnalysisLogger`, `SignalEmitter.DeriveDirection`, `LivePerformanceTracker`, `FailureRateMatrix.CanonicalTier`, `AutoTweakerCore`, etc.) are UNCHANGED. The frozen bridge contract routes actionability through `direction` + `confidence`; neither is touched. Divergence is deliberate on the two render surfaces only (precedented by the cap-reason rich string vs the CSV bucket).

`VerdictContext` tag (always rendered as a CONTEXT: line):
- **CONFIRMED** — directional call with cross-category support (only for directional verdicts)
- **ALIGNED** — sub-threshold bias has cross-category support (NO TRADE only, v30)
- **FLOW_UNCONFIRMED** — score qualifies but order-flow indicators contradict
- **MOMENTUM_FADING** — score qualifies but momentum is decaying
- **STRUCTURALLY_WEAK** — swing data exists but no clean target+stop pair

---

## 6. settings.json — operational pointer

**Source of truth:** `settings.json` itself + its inline `change_log` array.

**Current version: read the tracked repo-root `settings.json` line 2** — ⚠ **this line read *"v64"* while the tree ran to v68**, the same rot the header above now documents, in the same file. Corrected 2026-08-24. **Name which copy you read:** `bin\Debug\net8.0-windows\settings.json` is a build artefact and legitimately lags the tracked file until the next build.

The 17 top-level blocks (verified against the tracked file 2026-08-24 — this is a *structural* list, so it belongs here; the version does not):
- `indicators` (per-indicator parameter blocks; incl. `aggressor_velocity` — v50 #5 build, three-tier tweaker surface, HARD CONSTRAINT 19; `OFI.momentum_*` fenced off the tweaker surface — v50 retune R1, HARD CONSTRAINT 20)
- `session_volume` (UTC bucket multipliers + per-session `execution_resolution` — v36; + per-session `roc_magnitude_threshold` — v40 Asia/London (B) re-baseline)
- `resolution_profiles` (per-resolution ROC threshold overrides keyed by "1"/"3"/"5" — v36; 3-min `roc_slope_delta_threshold` re-baselined 0.105→0.06 in v40)
- `mtf_gate` (15m gate configuration)
- `auto_run` (auto-run timer + `trigger_mode` — `interval` | `on_close` bar-close firing, v44; off the auto-tweaker surface)
- `scoring` (verdict thresholds, regime max scores, tier floors, context tag thresholds, hold thresholds — the `hold_*` keys are off the auto-tweaker surface, HARD CONSTRAINT 17 — v47)
- `kelly` (display-only sizing block)
- `regime_gates` (TRANSITIONAL ADX penalties)
- `regime_weights` (Pass 2c alignment bonus/penalty)
- `network` (HttpClient timeout, retry config)
- `performance_display` (live perf strip + OHLC gap-fill + metric mode)
- `analysis_logging` (output dump)
- `exit_guard` (realtime exit-guard overlay — display/alert only, off the auto-tweaker surface — v43)
- `live_strip` (live microstructure TAPE strip — display/awareness only, NOT a verdict, off the auto-tweaker surface — v45)
- `signal_bridge` (verdict_signal.json emission to the order app — transport plumbing, off the auto-tweaker surface, HARD CONSTRAINT 18 — v49)
- `alerts` (#7 liq-cascade alarm + #8 level-approach alerts — display/alert only, off the auto-tweaker surface, HARD CONSTRAINT 25 — v59)
- `trade_store` (raw-trade capture to the backtest store — streaming capture + gap repair; **no rendered surface at all**, off the auto-tweaker surface, HARD CONSTRAINT 27 — v64)

**Workflow when adding new config keys:**
1. Add the corresponding POCO field in `Core/Settings/EngineSettings.vb` with `<JsonPropertyName(...)>` attribute and a sensible default.
2. Bump the top-level `version` integer in `settings.json`.
3. Append a new entry to the `change_log` array (newest first), referencing the spec doc.
4. Add a one-line entry to §15 *Recent Changes* below.

Pre-v22 settings change rationale and earlier audit-trail commentary lives in `docs/history-archive.md` §A. Settings change history beyond the last five versions is in the `change_log` array within `settings.json` itself.

---

## 7. ScoringEngine — Key Behaviours

- **MaxScore:** base values from `cfg.Scoring.RegimeMaxScore.*` — TRENDING (19), RANGE_BOUND (18), TRANSITIONAL (15). With `RegimeWeights.Enabled` (default), TRENDING → 20 and RANGE_BOUND → 19 (base + AlignmentBonus). TRANSITIONAL unchanged.
- **Verdict thresholds:** `Math.Ceiling(regimeMax * pct)`.
- **Step 2:** Score signals into ScoreState. All thresholds from cfg. Includes bid-ask spread WIDE penalty and OFI momentum modifier.
- **Pass 2:** Upgrade partials on cross-category confirmation; OBV upgrade blocked on adverse divergence.
- **Pass 2b:** OI × CVD cross-confirm gate. Full OI + matching CVD → `UpgradeBonus`. Full OI conflict with CVD → `ConflictPenalty`. Partial OI confirms upgrade-eligible; partial conflict non-penalising.
- **Pass 2c:** Regime alignment gate. Suppressed in TRANSITIONAL and on zero-net scores. TRENDING checks EMA ribbon + ROC (active when |ROC|≥`MagnitudeThreshold`) + CVD slope+sign. RANGE_BOUND checks VWAP dev (warmup-gated) + RSI vs `Pass2cMidline` (50) + Donchian. All aligned → `+AlignmentBonus` (capped at regimeMax). All conflict → `-ConflictPenalty`. D1 trend structure contributes `+StructureBonus` when structure agrees with dominant side.
- **Step 3:** Baseline funding-rate modifier.
- **Step 3b:** Funding-momentum modifier (soften crowding when momentum falling; amplify when momentum rising into crowding).
- **Step 4:** Regime veto + TRANSITIONAL ADX penalty.
- **Step 4b:** MTF gate veto → forces NO TRADE.
- **Step 5:** Threshold comparison → verdict.
- **Step 5b (placed-level arbitration — v51 B4b structural-first):** delegates to the ONE shared seam `SignalEmitter.ComputeSideLevels`. Target ladder swing → nearest HVN → POC (HVN-gated) → ATR fallback, priority-with-looseness-bound (`structural_levels.target_max_atr_mult`); stop = min(structural swing stop, `stop_max_atr_mult`×ATR) per DG1. Copies the placed outputs onto `AdjustedLongTarget`/`AdjustedShortTarget` + `TargetCapReasonLong`/`TargetCapReasonShort` (split B1 2026-05-12); **Step 5c (v35 min-move gate) evaluates the PLACED target.** With `structural_levels.enabled:false`: the legacy 3-tier closest-wins CAP (winner = closest cap to entry), byte-identical to v50.
- **Step 5b (VerdictContext):** FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED. **NO TRADE special case (v30):** CONFIRMED relabels to ALIGNED. Decay ratios + count thresholds from `cfg.Scoring.ContextTagThresholds.*`.
- **Step 6 (CalcHoldStatus — layered exit):** Layer 1 microstructure (2+ adverse → fast EXIT) → Layer 1.5 structural break (prior swing breached) → Layer 2 momentum break (ROC crosses 0) then OBV divergence → Layer 3 RSI divergence / single adverse / RSI+ROC structural. Only renders when `posState ≠ None`.
- **Step 7:** placed levels rendered from `ComputeSideLevels` (fallback multipliers `cfg.Scoring.AtrTargetMultiplier`/`AtrStopMultiplier` 1.75/1.6 + `structural_levels` bounds/sessions — see §8). **Structural rows** rendered in UI alongside (cyan when both target+stop exist, dim when partial). v30 `FormatRR` uses `< 0.1` literal for sub-1dp ratios.
- **CalcKellySizing():** called from `RenderOutputHeader` after ATR levels. Display-only, zero scoring impact.

For full annotated `Calculate()` pipeline detail, see `docs/architecture.md`.

---

## 8. ATR Entry / Stop / Target Display

- **Entry price** = `candles1m.Last().Close`.
- **Last transacted price** = `recentTrades.Last().Price` — displayed above ATR block, not used as entry. (Trade lists are chronological ascending since the v31 correctness pass; the most recent trade is the LAST element.)
- **[v51 B4b] Placed levels are STRUCTURAL-FIRST** — every consumer (snapshot, card, bridge payload, CSV `Placed*`) reads the ONE shared arbitration `SignalEmitter.ComputeSideLevels`. Target ladder: swing → nearest HVN → POC (HVN-gated) → ATR fallback (first tier with 0 < dist ≤ `structural_levels.target_max_atr_mult`×ATR places; structure wins even when farther than the ATR level). Stop (DG1): min(structural swing stop, `stop_max_atr_mult`×ATR) — labels `SWING_STOP` / `STOP_CLAMPED` / `FALLBACK_ATR` on the rendered rows. `structural_levels.enabled:false` reverts byte-identically to the legacy geometry below.
- Fallback/legacy: Long Stop = price − ATR × `AtrStopMultiplier` (1.6); Target = price + ATR × `AtrTargetMultiplier` (1.75 global; LONDON 2.0 / ASIA 1.25 via `structural_levels.sessions`). Short mirrored. Fallback R:R ≈ 1:1.1.
- **Structural placement display:** fallback target shown ahead of an arrow to the placed value with reason label (e.g. `PLACED @ 95200.0 (SWING_HIGH_5M)`; legacy path renders `CAPPED @ …`). **v30 sub-tick suppression:** when `|fallback − placed| < max(0.5, ATR × 0.02)`, the label is hidden; target renders as a normal value (CSV `TargetCapReason` still populated for analytics).
- **Multipliers read from cfg** — labels and R:R display are dynamic, not hardcoded.
- **Structural rows** below ATR block: `Long structural: Stop X | Entry X | Target X  R:R 1:N  (risk X / rwd X)` in cyan when both target+stop exist; dim with per-side missing-data note when only one side (v30 F12 wording). Mirror for short.
- **Kelly Sizing block** rendered after ATR levels. Half-Kelly, 5% hard cap, $1,000 account, $10 contract face. Advisory label notes R:R is ATR-basis (not structural). EST mode only — CAL mode will return when backtesting module ships empirical per-tier win rates. Suppressed when KellyF = 0. v30 plural fix: `1 contract` / `N contracts`.
- **Funding display** (FUNDING section): rate row + momentum row. v30 negative-zero clamp at both display sites.

---

## 9. Open Position Guidance (CalcHoldStatus)

Priority order (v47 N2 — corrected to match the code, pinned by harness A17g): (1) 2+ adverse microstructure signals → fast EXIT → (1.5) structural break exit (price closed at/below prior swing low for long; at/above prior swing high for short) → (2) momentum-break exit (ROC crosses zero against the position) → (3) OBV divergence exit → (4) RSI divergence evaluate → (5) single adverse microstructure warning → (6) RSI/ROC structural assessment.

All RSI/ROC thresholds read from cfg (`HoldRoc*`, `HoldRsi*`).

**Render gate:** the `HOLD \ EXIT:` line and the CalcHoldStatus output render in UI only when a position has been declared via the position radio buttons (`posState ≠ None`). When no position is held, the entire hold-guidance block is suppressed. See `architecture.md` *Display Behaviour Clarifications*.

---

## 10. CSV Logging & Auto-Run

- `AnalysisLogger.LogRun(r, verdict)` → `analysis_log.csv` in exe directory. v0.4 schema (87 columns).
- `CalibrationReport` summarises recent directional accuracy.
- Auto-run timer driven by `MainForm_AutoRun.vb`; interval configurable from UI (min 10s).
- `VerdictContext` column may carry `ALIGNED` on post-v30 NO TRADE rows.
- OI × CVD Pass 2b outcome surfaced in `OiCvdOutcome` CSV column (Bundle 1).

---

## 11. DynamicNorms

`DynamicNorms.Compute(candles1m, r.ATR)` computes per-run:
- `ATRScaleFactor` — current ATR vs reference; scales stop/target distances.
- `VolHighThreshold` / `VolMidThreshold` — regime-adjusted volume thresholds.
- `VWAPDevThreshold` — dynamic VWAP deviation threshold (clamped from settings).
- `ApplySessionVolume()` — session-aware post-adjustment that applies ASIA / LONDON / NY bucket multipliers from `SessionVolumeSettings`.

---

## 12. WATCHING / Calibration Backlog

Currently-open items pending live-data review. **Roadmap absorption (2026-07-02):** the Low-priority threshold-sweep rows below (funding momentum, session multipliers, OI×CVD, TFI, TTM, VPFR buckets, liq ratio, ContextTag, Kelly, swing wing) are collectively absorbed by the **roadmap W1 signal-health audit** (`docs/roadmap.md`) — they get data-driven verdicts there rather than one-by-one passes. Rows with a specific re-home are annotated in place. Resolved rows move to `history-archive.md` §G.

| Item | Description | Priority |
|---|---|---|
| **v52 aggressor-velocity wire-in post-ship watch (S5.2)** | NY-by-1-min watch: burst fire rate 8-12 %, same-side share (TFI on the burst side) at least 85 %, TFI-modifier engagement about 5-10 % of NY directional votes. Trigger: out of band on 2 consecutive weekday sessions, then re-run the derivation in `aggressor-velocity-s52-derivation-2026-07-13.md` §5.2 and §7. The res-3 display-only clause is spent (LONDON armed v60, ASIA v65). Full cell: [`history-archive.md` §I, `trim-2026-09-14-03`](history-archive.md#trim-2026-09-14-03) | Medium (watch) |
| **Funding momentum — time-anchored window post-ship watch** (v53, SHIPPED 2026-07-15) | Per-resolution check on post-v53 rows: FLAT 60-70 % and Step-3b engagement 15-25 %, with res-1 AND res-3 both in band. Trigger for a T re-fit: both resolutions out of band in the same direction across 2 weekday sessions, not one hot week. Pre-v53 rows are not comparable. Spec: `funding-momentum-time-anchored-window-proposal.md`. Full cell: [`history-archive.md` §I, `trim-2026-09-14-04`](history-archive.md#trim-2026-09-14-04) | Medium (watch) |
| **Session volume multipliers** | **PARKED 2026-07-31 behind the D3 forming-bar ruling (JOB 2 decision D-C).** The volume vote's numerator is the in-progress bar that the threshold excludes, so the vote fires on 0.69 % of NY runs and 2.66 % at ExecRes 3; a multiplier tuned now tunes a dial on nothing. The `auto_run.trigger_mode` rider now travels as the `TriggerMode` column in the next rotation ([`csv-rotation-riders.md`](csv-rotation-riders.md)). Full cell: [`history-archive.md` §I, `trim-2026-09-14-05`](history-archive.md#trim-2026-09-14-05) | Blocked (D3) |
| TFI threshold | BLOCKED — the W1 audit (2026-07-03, F11) found TFI is **not logged at all** (no CSV column), so the sweep has no data. `TFIValue`/`TFISignal` columns ride #5's v0.7→v0.8 rotation (retune spec C1, APPROVED); becomes measurable at the next audit re-run. | Blocked (data) |
| **v51 placed-geometry post-ship watch (B4b)** | Read 2026-07-31 ([`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) §3): STOP_CLAMPED binds on 95-100 % of structural-stop rows, so stops are de facto ATR stops and the live question moves to the L9 un-clamp (gated on L3). BELOW_MIN_MOVE NY 23.35 % / LONDON 15.16 % / ASIA 18.28 % is the recorded baseline. The reach-rate and LONDON structural-TARGET arms (the B4b F3 watch) have no instrument; that watch was RETIRED 2026-08-12 (`trader-tick-queue.md` §0a). Full cell: [`history-archive.md` §I, `trim-2026-09-14-06`](history-archive.md#trim-2026-09-14-06) | Medium (watch - F3 arm retired 2026-08-12) |
| **TTM flatThreshold** | **Re-derived 2026-07-31, then PARKED 2026-08-02.** The shipped 0.5 sits below the 1st percentile of the 7-bar drift, so the FLAT band is inert (recorded and deliberate). The unit is wrong, not the value: the fix is ATR-relative (k about 0.25-0.30), a code change with its own spec and dataset boundary. Do not inherit the 25.0/40.0 ladder or the 1.45 ratio (measured 1.774). See `trader-tick-queue.md` §0a "Explicitly NOT owed". Full cell: [`history-archive.md` §I, `trim-2026-09-14-07`](history-archive.md#trim-2026-09-14-07) | Medium (parked) |
| VPFR numBuckets | Default 50; review POC resolution on quiet sessions. | Low |
| Liq dominanceRatio | Default 2.0; review false signals; consider raising/lowering after live observations. | Low |
| ContextTag thresholds | Review FLOW_UNCONFIRMED hit rate after 50+ trades. | Low |
| **Kelly `est_prob_floor`/`scale` + the EST advisory promise** | **Dated trigger MET 2026-09-09: 407 pooled weekday STRONG against at least 406.** The tier ladder still does not separate (pooled STRONG 47.1 %, below the 47.76 % breakeven), so the on-screen "next book doubling" promise was retired and the advisory re-worded (`DeribitIndicatorProject.md` §15, row dated 2026-09-08). CAL stays parked. Decision record: [`kelly-est-honesty-decision-2026-08-02.md`](kelly-est-honesty-decision-2026-08-02.md). Full cell: [`history-archive.md` §I, `trim-2026-09-14-08`](history-archive.md#trim-2026-09-14-08) | Medium (watch - trigger spent) |
| **v28 target-hit vs barrier-hit gap** | Post-v28 data should show ~30-50pp gap between target-hit and barrier-hit rates. If gap is small, direction calls themselves are bad; if large, stops are too tight. Probe on 2026-05-15 showed +35pp on 67-row sample — needs validation on larger sample. | Medium |
| **First auto-tweaker live fire (supervised)** | The fixed-mode tweaker has never fired live. All original gates are long since met (v35 de-confound ✅, Phase-2a NY×1 population filter ✅ + replay-validated, `window_size_verdicts` 75 interim ✅); the remaining gate is **data**: a real >40%-failure NY×1 window must appear in the live book. When it does: supervised **dry-run first** (`dry_run_enabled: true`, `auto_commit_enabled: false`), validate snapshot/apply/revert/streak, diff reviewed before any apply. Roadmap W5. *(Status text refreshed 2026-07-02 — the old "gated on v35" wording predated v35 shipping.)* | Medium (data-gated) |
| **Auto-tweaker tier-eligibility floor vs the v35 book (MinTier mismatch)** | v29's MinTier auto-scale assumes about 50 % tier-eligible rows; post-v35 NY measured about 23 %, so a 30-row window can never clear MinTier 15 and every block skips. Interim 2026-06-15: `window_size_verdicts` 30 to 75 in `tweaker_config.json`. Proper fix: recalibrate the window and MinTier pair per session against the real directional rate. Full cell: [`history-archive.md` §I, `trim-2026-09-14-09`](history-archive.md#trim-2026-09-14-09) | Medium |
| **v30 ALIGNED frequency** | Post-v30, NO TRADE rows that previously rendered CONFIRMED now render ALIGNED. Watch the CSV's `VerdictContext` distribution. | Low |
| **v36 Phase-2 threshold carry-forward (deferred 3-min scaling)** | Two refinements deferred from v36 Phase 1: (1) `TTM.flat_threshold` and the CVD/RSI `divergence_price_gate`s stayed at 1-min values on 3-min bars (risk: over-suppression); (2) DynamicNorms' 100-bar volume and 50-bar VWAP-dev baselines span 3x the wall-clock on 3-min bars, so LONDON never gets a pure-session volume baseline. Source: `session-timeframe-resolution-implementer-handoff.md` §1 and §4. Full cell: [`history-archive.md` §I, `trim-2026-09-14-10`](history-archive.md#trim-2026-09-14-10) | Medium |
| **Auto-tweaker resolution-awareness (Phase-2 precondition)** | The fixed-window slicer is resolution-blind; it must filter by (session x resolution) before tuning post-v36 data. **Phase-2a done 2026-06-17:** load-time population filter (initial population NY x 1) and a code-level off-surface reject in `SettingsDiffApplier.Validate`. **Phase-2b open:** per-population cursor, window, MinTier, threshold and picked-cell history; the schema home for session-specific tuned values; and the revert-vs-manual `resolution_profiles["3"]` interaction. Full cell: [`history-archive.md` §I, `trim-2026-09-14-11`](history-archive.md#trim-2026-09-14-11) | Medium (Phase-2a done; Phase-2b open) |
| **v48 OFI per-session fire-rate watch (spec §4a)** | Global pair 1.60/0.625 shipped 2026-07-03 with LONDON's per-session fit at +11.7% vs pooled (n=200 — within percentile-fit sampling error) and ASIA unfittable (geo n=69; zero reference-period Asia rows). After **≥2 further weekday session-days** on the shipped pair: recompute per-population BUY/SELL/combined dominance rates at 1.60/0.625 (recipe in `v48-ofi-dominance-rebaseline-spec-back.md` §4). **Trigger:** any population's combined dominance rate outside **[0.6×, 1.5×] of the fitted 63.2% target across 2 consecutive weekday sessions.** Response ladder: (a) verdict-impact check (OFI is 1 vote of ~20) → (b) pooled retune incl. the new data → (c) per-session bucket overrides (last resort — new nullable `session_volume.sessions[].ofi_*` keys, hand-tuned/off-tweaker by construction, own small spec). The W1 signal-health audit reports these rates automatically on every re-run. | Medium (watch) |
| **WS 3-min closed-bar volume undercount (cutover watch)** | 12h soak (2026-06-23) found WS `chart.trades` 3-min bars undercount REST volume by ~2.5% (OHLC exact; systematic, always ws-low; 8 bars). DECISION (`websocket-migration-p3-cutover-spec.md` §7): accept via a relative-volume tolerance on `ShadowParityComparer` — immaterial to scoring in normal flow. **Watch during volume spikes** (breakout-confirm, vol > 3× SMA-9): check whether the undercount could pull a WS reading below the 3× gate when REST clears it — the one place 2.5% could flip a signal. Live only post-cutover / under `shadow_parity`; escalate to fixing the WS bar aggregation (P3 §7 option b) if it trips. | Medium |

Earlier ✅ Done items moved to `history-archive.md`.

---

## 13. Future Upgrades

> **Sequencing authority: `docs/roadmap.md`** (2026-07-02) — the cross-project strategic roadmap (workstreams, execution order, the DeribitOrderPlacementApp signal bridge, Linux port). §13/§16 remain the engine-local backlog detail; when they disagree with the roadmap on *order*, the roadmap wins.

Ranked by expected accuracy / reliability gain. Items marked 🔍 require a spec decision before coding begins.

### High-Impact (deferred until post-WebSocket or post-calibration)

| Item | Description | Status |
|---|---|---|
| WebSocket migration | Real-time order book + trade stream vs REST snapshot polling. Was the single highest-impact non-indicator upgrade; gated the Section-A microstructure class. | ✅ **SHIPPED** — P1 v38 → P2 v39 → cutover **v42 (2026-06-24, live on WS)**. The unlocked items it gated are now sequenced in `docs/roadmap.md` W1/W2 (spread revival, #5 aggressor velocity, #6 absorption, A4 liq×OFI). |

### Moderate-Impact (post-calibration spec work)

| Item | Description | Status |
|---|---|---|
| RSI divergence on 5m candles | 1m + 5m divergence simultaneously = stronger penalty. Requires `CalcRSIDivergence` on `candles5m` + combined gate. | 🔍 Deferred — see `post-websocket-post-calibration-backlog.md` D3. |
| Donchian × BBW state cross-reference | Wide-channel vs tight-channel breakout differentiation. | 🔍 Deferred — see `post-websocket-post-calibration-backlog.md` D4. |
| B1 per-indicator regime weights | Full per-indicator weighting scheme. STUB blocked on Bundle 1 empirical hit-rate output. | 🔍 STUB. |

### Auto-tweaker calibration

| Item | Description | Status |
|---|---|---|
| Auto-tweaker first-fire validation | Once `LastEvaluatedRowIndex` clears WindowSize threshold and a round fires, verify streak / snapshot / revert paths end-to-end on real data. | Pending data accumulation. |
| Auto-tweaker tuning | Once 50–100 rounds have completed, recalibrate `failure_rate_threshold_pct` (default 40), `min_tier_eligible_rows` formula, `streak_weight` (1.5). | Pending data. |

### Accuracy Ceiling Note

*(Premise refreshed 2026-07-02 — the original note predated the WS cutover.)* The REST-polling latency floor is **gone** (v42: 100ms-fresh book/trades/ticker, on-close bar triggering). The binding constraints on further accuracy are now the three risk thresholds themselves, unchanged and more relevant than ever:

1. **Overfit risk** — the number of tunable parameters continues to rise; session-bucket multipliers and per-tier thresholds should be validated against forward runs, not a tiny historical slice.
2. **Signal redundancy** — OFI + TFI + CVD + MicroCVD already cover order flow from four angles; the roadmap W1 **signal-health audit** is the standing instrument for this (fire rates, pairwise agreement, conditional outcomes → keep/retune/retire with evidence), and any new flow signal (#5/#6) must be specced against it.
3. **Interpretability** — adjustments should remain easy to reason about from DynamicNorms and settings. If traders cannot quickly tell which bucket is active and why a threshold moved, the engine becomes harder to trust — doubly so now that the O2 signal bridge lets an autotrader act on the output.

---

## 14. Backlog

*(cleared — all spec bundles 1–3 + v27-v30 features shipped; remaining items are calibration review or future spec work tracked in §12 and §13)*

---

## 15. Recent Changes

Most recent five settings.json versions. Full history (v0.33 through v26) lives in `docs/history-archive.md` §E.

| Version | Date | Summary |
|---|---|---|
| **Venue-status instrument — `Core/VenueStatusLog.vb` + `DeribitClient` helper + `collector.ps1` + fixtures A74a–A75b** (display/observation only, settings-untouched) | 2026-09-11 | Venue-status instrument per [`venue-status-instrument-spec.md`](venue-status-instrument-spec.md) (D-1 ruled (d)): new `Core/VenueStatusLog.vb` writes transition-only `venue_status.log` lines. A 5xx logs `VENUE_<http-code>`; an HTTP-200 JSON-RPC error logs `VENUE_RPC_<rpc-code>` or `VENUE_RPC_UNKNOWN`; no response and 4xx log nothing. All 6 `DeribitClient.vb` GETs route through `GetStringOrRecordAsync`. C-3b Part A adds `VENUE_OK` on the first success after an error ([`c3b-venue-scoping-spec.md`](c3b-venue-scoping-spec.md)). Fixtures A74a-A76c, harness 361 to 371. Display parity does not fire. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-12`](history-archive.md#trim-2026-09-14-12) |
| **Atomic writes — the partial primitive swapped for a TOTAL one at all SIX call sites** (settings-untouched) | 2026-09-10 | `File.Replace` swapped for the total primitive `File.Move(..., overwrite:=True)` at all SIX atomic-write sites; the spec row said five, and the uncounted one was `OhlcCache.vb` `RollingTrim`. Atomicity holds because every `.tmp` is a same-volume sibling. One deliberate change, verified inert: the destination's ACLs and creation time are no longer preserved. Fixtures A72a-A72c, harness 349 to 352; the two `tools/AutoTweaker` copies and `SettingsLoader.AtomicWriteAllText` are covered by review only. Display parity does not fire. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-13`](history-archive.md#trim-2026-09-14-13) |
| **`WD-SEMANTICS` — `UnparsedExcluded` counter separated from `WeekendExcluded` across three surfaces; offline-report row-count label fixed** (settings-untouched) | 2026-09-09 | `UnparsedExcluded` separated from `WeekendExcluded` on `LivePerformanceTracker.WindowAggregate`, `tools/CeilingAudit/CsvFeatureBuilder.LoadStats` and `analysis/AnalysisReport`, per [`wd-semantics-unparsed-counter-spec.md`](wd-semantics-unparsed-counter-spec.md). The `MinValue` guard runs first because `DateTime.MinValue.DayOfWeek` is Monday. New `ForwardWindowJoiner.ClassifyLoadedRows`. The offline report's `Rows in CSV` line now sums to rows loaded. Fixtures A71a-A71c, harness 346 to 349. Display parity does not fire (tooltip only). Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-14`](history-archive.md#trim-2026-09-14-14) |
| **`D-2` — the Kelly EST advisory line is re-worded; the "next book doubling" promise is retired** (display-only, settings-untouched) | 2026-09-08 | The Kelly EST advisory line is re-worded and the "next book doubling" promise retired: the trigger was met (407 against at least 406) and the tier ladder still does not separate (pooled STRONG 47.1 %, below the 47.76 % breakeven). New text: *"p(win) is ASSUMED from the confidence tier - the calibration read did not separate the tiers."* (the shipped string uses an em dash). Both surfaces changed in one commit: `UI/MainForm_PlaintextSnapshot.vb:252` and `UI/MainForm_Render_Cards.vb:1589`. The line is not fixture-pinned. Spec: [`kelly-est-advisory-reword-spec.md`](kelly-est-advisory-reword-spec.md). Harness 346 unchanged. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-15`](history-archive.md#trim-2026-09-14-15) |
| **`S-4` — eval-cache backfill dedup keyed on identity, and a v6→v7 `analysis_eval_cache.csv` schema change** (D-1–D-3 built together per D-1 (b); settings-untouched) | 2026-09-08 | Eval-cache backfill dedup keyed on the `(InstanceId, SignalId)` identity, with a `Timestamp` fallback for identity-less rows; `analysis_eval_cache.csv` schema v6 to v7 (two appended columns, a plain re-stamp migration). A persisted-schema change, NOT tagged `[no-engine-change]`; no settings key. Spec: [`s4-eval-cache-identity-proposal.md`](s4-eval-cache-identity-proposal.md). Review finding: the first build's per-row key namespace re-admitted 3,308 duplicates on every engine start; fixed with two lookup sets (`existingTs` and `existingLegacyTs`). Fixtures A69a-A69e, harness 339 to 345. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-16`](history-archive.md#trim-2026-09-14-16) |
| **Loose-end sweep — `F2` · `F3` · `G12` · and ELEVEN stale queue rows** (settings-untouched) | 2026-09-07 | One commissioned sweep. The `ResetBufferState` lock-gap finding: `TradeStoreWriter.ResetBufferState` now holds one lock across flush and clear, closing a tape-loss window (not fixture-observable; regression cover is A48b). The User-Agent finding: the repair `User-Agent` now resolves from the entry assembly (fixture A66c). The manual-gaps item: three manual gaps closed and both tracked PDFs regenerated. Plus ELEVEN stale `trader-tick-queue.md` §2 rows closed against the tree. Harness 335. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-17`](history-archive.md#trim-2026-09-14-17) |
| **`S2-2` — `CalcSpread` split into `CalcSpreadBps` (`Double?`) + `ClassifySpread`** (D-1–D-5, settings-untouched) | 2026-09-06 | `CalcSpread` deleted and replaced by `CalcSpreadBps` (`Double?`) plus `ClassifySpread`, so a degenerate book stays `NORMAL` instead of collapsing to `TIGHT` ([`s2-2-calcspread-split-proposal.md`](s2-2-calcspread-split-proposal.md), D-1 to D-5). Zero scoring and rendered change, proved MD5-identical over eight book shapes; fixtures A65a-A65d, harness 328 to 332. Extended 2026-09-07: the live strip now prints `-- bps` on a zero-priced top of book (a deliberate strip-only change, A66a), and the full-run composition moved into `IndicatorEngine.ApplySpread` (A66b), harness to 334. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-18`](history-archive.md#trim-2026-09-14-18) |
| **A54a — JSON↔POCO drift guard, its three review findings, and the scoped-(b) method-default removal** (queue item 8; D-1–D-5 + D-R3 + R-1/R-2/R-3 + F-1 + S2-1, settings-untouched) | 2026-09-05 | JSON-to-POCO drift guard `WalkPocoVsJson` (fixtures A62a-A62g and A63a-A63b), seven POCO re-syncs on the parse-failure path, per-session ROC nullables seeded (ASIA 0.17, LONDON 0.11, NY none), and 44 method `Optional` defaults deleted with 26 fixture literals ruled MECHANISM. Five commits; zero runtime behaviour change; not a dataset boundary. Queue item 17 closed on top (the `A6` literal made synthetic; 26 literals measured, 17 made synthetic) and A64a/A64b built, harness to 328. Lessons kept: a masked sibling gives the widest one-at-a-time band, and "off-shipped" means off-EVER-shipped. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-19`](history-archive.md#trim-2026-09-14-19) |
| **Absorption instrumentation — five appended `analysis_log.csv` columns** (spec R1–R9, settings-untouched) | 2026-09-01 | Five appended `analysis_log.csv` columns after `SignalId` (`AbsorptionEpisodeSec`, `AbsorptionPullLB`, `AbsorptionPostLB`, `AbsorptionSizeStart`, `AbsorptionSizeMin`) per [`absorption-instrumentation-spec.md`](absorption-instrumentation-spec.md). A schema rotation but NOT a comparability boundary: no existing column moves, and an empty value means no episode. The three-file schema twin was updated; `A43e` now resolves identity columns by name. CSV-only; scoring untouched. Fixtures A60a-A60e. `EnsureLogFile` rotated the book to the mislabelled `analysis_log.csv.v0.7.bak`, left alone and now a rotation rider. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-20`](history-archive.md#trim-2026-09-14-20) |
| **C1-coverage F1 — trailing-edge gap mis-attribution fix** (D-1–D-6 + D-5.1–D-5.5, settings-untouched) | 2026-08-26 | Coverage-report trailing-edge fix per [`coverage-trailing-edge-f1-proposal.md`](coverage-trailing-edge-f1-proposal.md) §4b: new `HourClass.TrailingEdge` flags an hour that is clean but silent from its last trade to its observed end past `gap_ms`. `HourStoreStats.LastTsMs As Long?`. The observed end is bounded by the span end, the request boundary and the store's last in-range trade. `--strict` stays keyed on `Defect`; the VERDICT line now gates on `TrailingEdge` too. Fixtures F1-a to F1-f, harness 306. Tools-only. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-21`](history-archive.md#trim-2026-09-14-21) |
| **Weekday-only row filter — AutoTweaker** (D-1–D-5, settings-untouched) | 2026-08-25 | The weekday-scope ruling ([`weekday-scope-ruling-2026-08-03.md`](weekday-scope-ruling-2026-08-03.md)) reaches all three surfaces. AutoTweaker (2026-08-25, fixtures A59a-A59e): weekday filter in the load-time `Where`; the population key gains a `WD` term; the Friday-to-Monday window burn is accepted; the `ConditionsExtractor` leak is fixed. `LivePerformanceTracker` (2026-09-07, A67a): filter in `AggregateRange` on UTC day-of-week; the tooltip shows `Weekend excl.`. `AnalysisRunner` and `WhatIfRunner` (2026-09-07, A67b): shared `ForwardWindowJoiner.IsWeekdayRow`, with the `MinValue` guard first. Harness to 337. Settings v68 untouched. Full cell: [`history-archive.md` §I, `trim-2026-09-14-22`](history-archive.md#trim-2026-09-14-22) |
| **v68 · auto-run on start** (D1–D2, Part A) | 2026-08-21 | New `auto_run.start_engaged` (default `false`): on form load the app calls the existing `StartAutoRun()`, so a scripted deploy needs no RDP click. Tracked `settings.json` ships `true`; the dev box opts out via `settings.local.json` (a per-key overlay admit). Auto-start cannot arm autotrade (verified by reading; not fixture-testable). Defect found live 2026-08-22 and fixed: SINGLE mode made each process collect exactly ONE row; `rbRepeat` is now set before auto-start, and the deploy gate must see two rows. Fixtures A58a-A58c. Spec: [`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md). Full cell: [`history-archive.md` §I, `trim-2026-09-14-23`](history-archive.md#trim-2026-09-14-23) |
| **v67 · thin-trade-window skip gate** (D1–D5) | 2026-08-20 | New `scoring.min_trades_for_scoring_override` (default 0 = derived). A run SKIPS with reason `recent trades thin (n<m)` when the trade list is shorter than `ScoringEngine.MinTradesForScoring(cfg)`, which is max(TFI window, MicroCVD window) = 50 at defaults; this closes the thin-ring path after a failed `SeedAsync`. Not ruled a dataset boundary (revisit if the skip rate is non-negligible). `ExitGuardEvaluator` mirrors it. Tweaker fence HARD CONSTRAINT 28. The deferred trade-count CSV column now travels with the next rotation ([`csv-rotation-riders.md`](csv-rotation-riders.md)). Fixtures A57a-A57d. Spec: [`thin-trade-window-skip-gate-proposal.md`](thin-trade-window-skip-gate-proposal.md). Full cell: [`history-archive.md` §I, `trim-2026-09-14-24`](history-archive.md#trim-2026-09-14-24) |
| **SH-1 — split the coverage hour at a capture-state marker (settings-untouched)** | 2026-08-14 | **An hour containing a capture-state marker mid-hour was scored entirely by whichever scope governed `:00`, hiding real defects or false-clean reads on any deploy/toggle hour.** `CoverageReport.ClassifyHour` now splits such an hour at the marker, classifies each part against its own scope, and reports `Defect` if either part is — still one row per hour (D-1). **D-3** [ruled 2026-08-13, `coverage-split-hour-implementer-brief.md` §5a] sets the no-Defect/no-Captured residual order `UnknownScope > ExpectedMissing > NotCapturing`, so an uncharacterisable span is never laundered into a confident label. NO settings keys, so NO version bump — settings stays v66. Fixtures `A49o`–`A49w`, mutation-proven; see `coverage-split-hour-sh1-spec-back.md`. |
| **Gap repair can heal DOWNTIME — hole-derived repair windows, + follow-ups DR-1/DR-2/DR-3** (settings-untouched) | 2026-08-13 | Gap repair can heal downtime. `ResolveResumeCursorMs` seeded from the last written row, so every hole behind a reconnected tail read as covered forever (a 60.3-minute hole survived seven hours). New `TradeStoreWriter.ResolveRepairWindowsMs` returns the `trade_seq`-bracketed holes behind the tail; detection uses `trade_seq` alone, never a time gap. Follow-ups: DR-1 removed the 2,000 ms width floor, DR-2 made scan truncation time-contiguous, DR-3 fixed the rows-appended count. Fixtures A56a-A56g. Deployed 2026-08-13 (AWS `e551f15e...`). Part B stays deferred. Spec: [`trade-store-downtime-repair-proposal.md`](trade-store-downtime-repair-proposal.md). Settings untouched (then v66). Full cell: [`history-archive.md` §I, `trim-2026-09-14-25`](history-archive.md#trim-2026-09-14-25) |
| **Trade-store write guard keyed on IDENTITY, not on a millisecond (settings-untouched)** | 2026-08-11 | The streaming write guard `If t.Timestamp <= _lastTs` discarded every same-millisecond sibling: 49.2 % of the live tape, biased toward multi-leg sweeps. `_lastTs` is deleted; `Buffer` now tests membership of a 20,000-trade recent window (`RecentWindowCapacity`, a public constant) on `TradeId`, falling back to `LegacyRowKey`, and ADMITS when uncertain. `trade_seq` was rejected as a high-water mark. Fixtures A55a-A55g, mutation-proved. Deployed 2026-08-11 (AWS `a5d701ad...`); the 2026-08-14 copy-back shows 134,204 rows with zero missing. Spec: [`trade-store-write-guard-identity-proposal.md`](trade-store-write-guard-identity-proposal.md). Settings untouched (then v66). Full cell: [`history-archive.md` §I, `trim-2026-09-14-26`](history-archive.md#trim-2026-09-14-26) |
| **v66 · OBV `trend_gate` re-anchor** (D2) | 2026-08-11 | `indicators.OBV.trend_gate` 18.0 to 23.0, one global key. **LIVE SCORING CHANGE AND A DATASET BOUNDARY.** It restores v33's own ~50 % directional design point on six months of store data (p50 22.2-24.2 across sessions and resolutions). OBV needs NO per-session and NO per-resolution split. The POCO moved in lockstep; the method default and one fixture literal still read 10.0 at the time (`seam-audit-2026-08-11.md` finding S-2, later closed by the A54a arc). Deployed to both boxes 2026-08-10 18:35 UTC. Evidence: [`candle-store-derivations-2026-07-31.md`](candle-store-derivations-2026-07-31.md) §2. Settings v65 to v66. Full cell: [`history-archive.md` §I, `trim-2026-09-14-27`](history-archive.md#trim-2026-09-14-27) |
| **Trade identity in the trade store — `trade_id` + `trade_seq` (settings-untouched)** | 2026-08-08 | Trade-store rows gain appended `TradeId` and `TradeSeq` columns, so a stored trade has an identity. Backward-compatible both ways (`TryParseRow` guards on fewer than 5 fields). One dedup contract, `TradeStoreWriter.DedupTrades`: identity first, five-field fallback, never key on an empty id. The venue diff reports identity and fallback matches separately; `trade_seq` gap detection makes completeness a local computation. Fixtures A53a-A53h. Deployed 2026-08-10 (AWS `d8678d2b...`). Spec: [`trade-store-trade-identity-proposal.md`](trade-store-trade-identity-proposal.md). Settings untouched (then v65). Full cell: [`history-archive.md` §I, `trim-2026-09-14-28`](history-archive.md#trim-2026-09-14-28) |
| **C1 · trade-store coverage report — `coverage` verb, live TAPE STORE strip, dead-path escalation** (settings-untouched) | 2026-08-05 | One row for queue item C1: the `coverage` verb in `tools/BacktestRunner` (six hour classes, candle and funding completeness, optional S0 venue diff); the live TAPE STORE strip (`ClassifyTapeStoreTier`, amber past 3x and red past 10x `flush_seconds`); and the dead-path rider (a never-flushed store escalates UNKNOWN to AMBER to RED on a second clock). Fixtures A49a-A49n. Its remainders shipped: F1 on 2026-08-26 and F2 as SH-1. Proposal: [`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md). Settings untouched (then v65). Full cell: [`history-archive.md` §I, `trim-2026-09-14-29`](history-archive.md#trim-2026-09-14-29) |
| **v65 · ASIA aggressor-velocity arming** (D3) | 2026-08-02 | `indicators.aggressor_velocity.sessions.ASIA.burst_ratio_threshold` = 5.5 (previously absent). **LIVE SCORING CHANGE AND A DATASET BOUNDARY:** ASIA rows now carry the burst-modified TFI vote; NY 4.5 and LONDON 5.5 unchanged. Threshold presence arms the session; the POCO moved in lockstep. Fire rate 9.7 %, same-side 91.0 %; on res-3 the modifier is in practice upgrade-only. Honest caveat: the only outcome read, AUC 0.5179 (n=217), is neutral at best. Fixtures: A28c re-pinned, new A52a. Evidence: [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) §5. Settings v64 to v65. Full cell: [`history-archive.md` §I, `trim-2026-09-14-30`](history-archive.md#trim-2026-09-14-30) |
| **v64 · in-app trade-store capture** (D1–D5) | 2026-07-31 | In-app trade-store capture: a streaming append off the WS trades stream (buffered; its monotonic guard was later replaced by the identity guard) plus an in-app gap-repair timer (fires on start, then every 6 h over a 20 h lookback). The network-free `Core/TradeStoreWriter.vb` was split out of `HistoricalStore`. D1 ruled AWS-ONLY. New top-level `trade_store` block; HARD CONSTRAINT 27. Zero scoring impact, no rendered surface, not a dataset boundary. Fixtures A48a-A48h. Spec: [`in-app-trade-store-capture-proposal.md`](in-app-trade-store-capture-proposal.md). Settings v63 to v64. Full cell: [`history-archive.md` §I, `trim-2026-09-14-31`](history-archive.md#trim-2026-09-14-31) |

Older entries are **not** deleted — **v63 down to v27** live verbatim in [`history-archive.md`](history-archive.md) §E, and v26 back to v0.33 below them.

**THE CAP GOVERNS THE WHOLE TABLE, not just versioned rows (tightened 2026-08-05).** The old wording — *"keep this table at five settings versions"* — capped only one of the two row types, and the **settings-untouched** rows grew unchecked underneath it: by 2026-08-05 there were five versions (at the cap, correctly) and **five untouched rows beside them**. The rule now reads:

> **Keep five settings versions, plus settings-untouched entries ONLY while they are newer than the oldest kept version. Everything older moves to §E.**
>
> **And one item gets ONE row.** C1 took three (Session 1, Session 2, a rider) for a single queue item — that is the growth mechanism, not the row count itself. Sessions and riders of the same item **extend that item's row**; they do not add rows. It reached 56 rows and 69 % of this file before the 2026-08-02 trim, which is why the rule is restated here rather than only in the header. **And one cell stays short (added 2026-09-14).** A cell carries what changed, the settings version, any live-scoring or dataset-boundary flag, the fixture IDs and the spec link; aim for under about 1,000 B. The build narrative belongs in the spec-back. If it must be kept, move it to [`history-archive.md`](history-archive.md) §I and leave a pointer. The row and age caps held while this file grew 30 % in five days, because cell length was the one dimension nothing capped.

**Trim history for this table.** The 2026-08-14 trim collapsed 14 rows to 10 by enforcing the one-item-one-row rule; nothing was old enough to archive. Its record moved verbatim: [`history-archive.md` §I, `trim-2026-09-14-32`](history-archive.md#trim-2026-09-14-32). The 2026-09-14 trim moved each long cell's build narrative out and kept a summary row; see [`doc-trim-log.md`](doc-trim-log.md).

**Detail is not lost: every collapsed row cites its own spec-back, which is where the build record belongs.** §15 is a version-history table, not a build archive — that distinction is the actual defence against regrowth.

---

## 16. Future Direction — Auto-Tweaking & Dual Interface

> **Sequencing authority: `docs/roadmap.md`** — adds a third strategic objective alongside the CLI port (16.2): the **signal bridge to DeribitOrderPlacementApp** (verdict/score/direction/ATR feeding its autotrade function; human display stays the primary output, the machine contract is additive).

Longer-arc plans. **All items here are KIV** while the team is in the live-data accumulation phase post-v30. Recorded so architectural prerequisites stay visible and groundwork can be laid opportunistically.

### 16.1 Auto-Tweaking via Frontier-LLM API

**Status:** ✅ Shipped as `tools/AutoTweaker/` (Bundle 2, 2026-05-06; extended by `settings-snapshot-history-proposal.md` 2026-05-12 and `auto-tweaker-fixed-window-proposal.md` 2026-05-17 / v29). Not yet fired in live mode — pending data accumulation under v29 fixed-window semantics.

When the engine's analysis fails to predict outcomes at a defined rate, the engine sends `settings.json` (plus a window of the recent `analysis_log.csv`) to a frontier-LLM API. The model returns a tweaked `settings.json` which replaces the local file. This is the long-arc replacement for manual calibration sweeps — automated, data-driven, runs unattended.

**Trigger** (v29 fixed-window): per-round failure rate within a disjoint WindowSize-row window. If failure rate > `failure_rate_threshold_pct` (default 40), a tweak round fires. Snapshot system tracks 3-in-a-row below-threshold streaks for revert candidates.

**Audit prerequisite:** every scoring-affecting parameter reachable through `settings.json`. ✅ Closed by `settings-exposure-pass-proposal.md` (v17).

**Display-only fields are NOT exposed for tweaking.** The audit explicitly excludes display formatting, colour palettes, calibration-report thresholds, MTF cache TTL. The tweaking surface is scoring decisions only.

### 16.2 Dual Interface — CLI (Linux) + WinForm (Windows)

**Purpose.** Two host targets sharing the same engine code:

- **CLI (Linux server, headless)** — runs on a remote host, drives the auto-tweaking loop unattended.
- **WinForm (Windows desktop)** — the existing manual-usage interface.

**Architecture status — partly satisfied.** Most of the codebase is already host-agnostic: scoring engine, indicators, settings, `DeribitClient`, `DynamicNorms`, `AnalysisLogger`, `OiSnapshot`, `AnalysisOutputDump`, `LivePerformanceTracker`, `OhlcCache`, the entire `tools/AutoTweaker/` and `analysis/` subtrees.

**Still WinForms-coupled:**
- Output rendering (`MainForm_Render_*.vb`) is RTF-based. CLI host needs a parallel renderer (ANSI plaintext or structured JSON).
- State plumbing (`_oiHistory`, `_fundingHistory`, `_ofiHistory`, MTF cache, `_prevRegime`, `_metricMode`) on `MainForm`. CLI host needs an equivalent state container.
- Auto-run scheduling. `WinFormsAutoRunTimer` uses `Control.Invoke`; CLI variant uses `System.Threading.Timer` callbacks (interface already defined).

**Constraint:** all new code in `analysis/` and `tools/` MUST be host-agnostic. Form-side viewers are allowed but must be thin wrappers around host-agnostic core classes. The auto-tweaker console app already builds with zero WinForms references.

### 16.3 KIV Prerequisites

Before 16.2 (CLI port) should be specced and scheduled:

1. **All four v27-v30 features stable in production.** ✅ Long since stable (statuses refreshed 2026-07-02).
2. **Auto-tweaker proven on live data.** ⏳ Still open — the first supervised fire remains data-gated on a real >40%-failure NY×1 window (§12 row; roadmap W5). The only prerequisite still outstanding.
3. **CalibrationReport READY threshold met under new metrics.** ✅ Met at v34 (975 rows, 3 regimes ≥50, 3 session days).
4. **WebSocket migration decision.** ✅ Decided and SHIPPED (v38–v42, cutover 2026-06-24).

The port's own groundwork is now scheduled independently of prerequisite 2: the roadmap W4 **run-state extraction spec** (host-agnostic run-context + headless runner skeleton) is behaviour-neutral and de-risks the port without waiting on the tweaker.

### 16.4 Long-Arc Architectural Ceiling

*(Refreshed 2026-07-02.)* The ceiling this section described — **WebSocket migration** — SHIPPED (v38–v42, cutover 2026-06-24). The microstructure class it gated (backlog Section A: real-time spread, aggressor velocity, order-book absorption, liquidation × OFI flip, VPFR shape) is unblocked and sequenced in `docs/roadmap.md` W1/W2. The next architectural ceiling, if one emerges, is the full-depth **incremental order-book channel** (public, change_id semantics) — deliberately deferred behind #6-v1 proving absorption on snapshots (roadmap W4); authenticated/raw feeds were evaluated and ruled out (W4).

### 16.5 Active Spec Bundle Status

Bundles 1-3 shipped 2026-05-05 to 2026-05-06. Of the deferred bundles, Bundle 4 dissolved into [`roadmap.md`](roadmap.md) W1 and the auto-tweaker; Bundles 5 (multi-session VPFR and anchored VWAP) and 6 (Smart OBV and MFI) stay deferred and off the roadmap. Full text: [`history-archive.md` §I, `trim-2026-09-14-33`](history-archive.md#trim-2026-09-14-33).

### 16.6 Parked Observations (Watch For)

Items not currently scheduled but with concrete promotion conditions.

**P1. Promote BestPivotByVolume to cap arbitration (D2 v2).**
*Condition:* CalibrationReport's `BEST VOLUME PIVOT DISTRIBUTION` shows "best is also most-recent" rate < 50% AND auto-tweaker output shows volume-weighted pivots correlate with target-hit rate. Both required.
*Action:* re-spec `d2-volume-weighted-pivots-v2-proposal.md`. Promote to 4th cap tier above swing.

**P2. Funding momentum threshold v23+ tuning.**
*Condition:* offline analysis `FundingMomentumDiagnostic` shows FundingDelta percentiles such that a threshold below 1 bp would meaningfully change the RISING/FALLING/FLAT distribution.
*Action:* simple settings-only follow-up pass. If percentiles show 1 bp is genuinely above all observed deltas at REST cadence, accept as polling-cadence ceiling.

**P3.** RESOLVED 2026-05-08 — OI×CVD asymmetry (`priceUp` was biased by 1bp against `MarkPrice`). See `history-archive.md` §D.

**P4. STRONG/MEDIUM tier collapse in failure-rate matrix.**
*Condition:* after 1000+ tier-eligible rows, both STRONG and MEDIUM matrices pick (window, threshold) combinations within 1 cell of each other.
*Action:* revise `failure-definition-v2-proposal.md` to a single tier-agnostic matrix.

**P5. Liquidation count window.**
*Condition:* CalibrationReport still shows 0 liquidation events 1000+ rows after Bundle 1 ships.
*Action:* small spec re-introducing `cfg.Indicators.Liquidations.TradeCount` removed in v15.

**P6. STRUCTURAL_RR_LOW context tag.**
*Condition:* a directional verdict fires with the verdict-direction structural R:R below a threshold (default candidate: 1:1). Currently the engine only fires `STRUCTURALLY_WEAK` when no clean target+stop pair can be placed at all.
*Action:* spec `structural-rr-low-context-tag-proposal.md`. New VerdictContext value `STRUCTURAL_RR_LOW` (display-only). Threshold reads from `cfg.Scoring.ContextTagThresholds`.

**P7.** RESOLVED 2026-05-13 — Live per-analysis success/fail display shipped as v26. See `history-archive.md` §D.

**P8. Live performance display — WEAK tier filtering.**
*Condition:* after ~1 week of live data, if `WEAK LONG`/`WEAK SHORT` inclusion produces visibly different headline rates vs STRONG+MEDIUM-only filter AND trader observes the WEAK-included rate is misleading.
*Action:* small spec changing `LivePerformanceTracker`'s eligibility filter. Optionally expose as `performance_display.tier_filter` (`all_directional` | `actionable_only`).

**P9. Auto-tweaker SKIPPED_SESSION_BOUNDARY waste.**
*Condition:* v29 fixed-mode advances `LastEvaluatedRowIndex` by full WindowSize on session-boundary skip, losing up to `WindowSize-1` rows. If RoundHistory shows lots of SKIPPED_SESSION_BOUNDARY after a week of running, the boundary-aware skip could be smartened to advance only up to the boundary itself.
*Action:* small follow-up patch in `AutoTweakerCore`.

**P10. POC tier 3 of target cap never fires.**
*Condition:* if 1000+ runs with `CAPPED @` events show 0 POC selections AND the `hvnAbove`/`hvnBelow` gate on POC is the bottleneck (rather than POC just being geometrically dominated by HVN). Investigation 2026-05-17 showed code path is reachable but conditions are narrow.
*Action:* consider removing the HVN gate so POC fires as a true "no swing + no HVN" fallback. Re-spec if pursued.

**P11. ATR-band recalibration for the current price regime.** RESOLVED 2026-06-17 (settings v37): `static_ref` 115 to 38, and the trader-profile ATR bands recalibrated (current bands: `trader-profile.md` §5, the ATR thresholds block, which is their single home). Full text: [`history-archive.md` §I, `trim-2026-09-14-34`](history-archive.md#trim-2026-09-14-34).

**P12. Reduced size in TRANSITIONAL / low-vol (sizing-advisory).**
*Condition:* trader-profile §6 says "Transitional = reduced size, extra caution." The engine honors the caution via the Step-4 ADX-proximity *score* penalty (fewer/weaker verdicts) but applies no *size* haircut — a transitional trade that passes is Kelly-sized like a clean trend. Display-only (Kelly is advisory). **Design tension to resolve first:** competes with the profile's vol-normalization (`Base × AvgATR/CurrATR` → low ATR = *bigger* size); a transitional/low-vol caution multiplier would layer *on top* and the interaction must be specified (which signal wins when). Backlogged 2026-06-14.
*Action:* if transitional trades that pass still size too aggressively in practice, spec a regime/vol caution multiplier on the Kelly advisory. Display-only; low priority.

**P13. Document the tweaker-tunable vs hand-tuned settings split in the User Manual.**
*Condition:* `settings.json` keys fall into three de-facto ownership tiers that today are only encoded developer-facing (in `PromptBuilder` HARD CONSTRAINTs 11–16 + `SettingsDiffApplier` rejects), never documented for the trader: **(1) auto-tweaker-tunable** failure-rate levers (verdict thresholds, `OFI.avg_window_sec` + dominance ratios, etc.); **(2) hand-tuned re-baseline overrides** — the per-session / per-resolution keys (`session_volume.sessions[].roc_magnitude_threshold`, `resolution_profiles.*`, and the future `aggressor_velocity.sessions[].*`), set by manual firing-rate-match, never auto-tuned; **(3) hand-toggle feature switches** (`OFI.averaging_enabled`, `exit_guard.*`, `network.*`, `aggressor_velocity.enabled`/`scoring_enabled`). Raised 2026-07-01 during the P4 #5 spec (trader asked which knobs the tweaker owns).
*Action:* add a UserManual section/table listing each `settings.json` block's keys by tier (tweaker-tunable / hand-tuned re-baseline / hand-toggle switch), sourced from the `PromptBuilder` HC 11–16 + `SettingsDiffApplier` reject lists. Doc-only, ~30-min pass. Backlogged 2026-07-01.

**P14. Auto-tweaker Phase-2b — per-population auto-tuning (workstream C).** Draft spec exists: `auto-tweaker-phase2b-per-population-autotuning-proposal.md` (DRAFT, living). Lifts the tweaker from **one** designated population (NY×1, Phase-2a) to **many** `(session × resolution)` populations — each with its own evaluated-row cursor / window / MinTier / picked-cell history and its own tunable home, so an Asia/London tune lands in `resolution_profiles` and never overwrites the global keys NY depends on. Not part of the P4/WebSocket upgrade catalogue — this is the *auto-tweaker* arc's workstream (C), which is why it doesn't appear in the P4 list.
*Condition:* build only if the manual (B) Asia/London re-baseline cadence proves heavy enough to be worth automating. Blocked on three gates (spec §1): (A) population filter shipped ✅; ≥50 weekday-3-min rows per session (Asia/London separately); (B) the manual `resolution_profiles["3"]` re-baseline done + the §3 schema-home decision signed off. **Lowest-priority in the v36 arc — may never be built.**
*Action:* finalize the `[FILL IN]` sections of the draft with the accumulated-data findings; trader signs off §3. Pointer added here 2026-07-01 so (C) is visible from the main doc, not just its own file.

### 16.7 Portability Constraint Reaffirmed

The Linux CLI port (16.2) is the long-term target. All new code under `analysis/` and `tools/` MUST be host-agnostic — no WinForms references, no `Control.Invoke`, no `MainForm` coupling. Form-side viewers are allowed but must be thin wrappers around host-agnostic core classes.

Enforced in `CLAUDE.md` Collaboration Rules and is a hard PR-review check.

The port itself happens **after** auto-tweaker proves on live data (16.3) AND analysis accuracy reaches a plateau. WebSocket migration is independent — may or may not happen before the port.
