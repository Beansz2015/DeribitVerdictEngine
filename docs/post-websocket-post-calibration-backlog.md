# Backlog: Post-WebSocket / Post-Calibration Items
**Created:** 2026-04-27
**Purpose:** Record items deferred from the active spec set so they don't get lost when context windows roll over. Each item lists the gating prerequisite, why it's deferred, the trigger condition for revisiting, and the expected payoff if implemented.

This is **not** a wishlist. Every item here was considered against current scope and explicitly deferred for a stated reason. If you re-pick one of these up, satisfy the gating condition first or document why the gating no longer applies.

Active in-flight proposals (not in this backlog): `bid-ask-spread-proposal.md`, `ofi-momentum-proposal.md`, `vpfr-lite-v2-proposal.md`, `swing-pivot-proposal.md`.

---

## Section A — Gated by WebSocket Migration

These items need real-time order book / trade stream quality. REST polling fundamentally blurs the signal and the implementation complexity is not justified until WebSocket is in place. WebSocket is independently the highest-impact non-indicator upgrade per `architecture.md`.

### A1. Spread Momentum (rolling spread delta)

**What:** Like `OFIMomentum` but for `SpreadBps`. Track the rolling delta of spread over N samples. RISING spread = liquidity withdrawing, leading-indicator flush warning.
**Why deferred:** REST snapshots arrive every 30+ seconds. Spread changes meaningfully at sub-second resolution. With REST polling, spread momentum is mostly noise — by the time a "rising" classification fires from a 3-sample REST window, the flush has already happened and recovered.
**Trigger to revisit:** WebSocket migration shipped; spread is read at ≥1Hz.
**Expected payoff:** Moderate — provides actionable warning ~5–30 seconds before flush, vs the snapshot-based v1 which is concurrent with the flush.

### A2. Aggressor Velocity / Trade Rate

**What:** Trades per second, volume per second, or aggressor flow rate (USD/sec). Independent of TFI's normalised aggressor ratio. Catches "burst" entries vs "sustained drift" — same TFI value can mean very different things at different velocities.
**Why deferred:** Trade rate is meaningless from a 100-trade snapshot of `recentTrades` — you don't know over what time window those 100 trades printed. WebSocket trade stream timestamps each trade; rate is then computable.
**Trigger to revisit:** WebSocket trade subscription active.
**Expected payoff:** Small-to-moderate. Mostly improves entry timing during news/volatility bursts; minor for the 2–15 min hold window.

### A3. Order Book Absorption Detection

**What:** Detect large standing bids/asks that absorb significant volume without moving (stops being hit or institutional iceberg). High-quality reversal signal.
**Why deferred:** Requires per-tick order book snapshots to detect "level X had Y size, then took Z volume in trades, but level X is still standing at Y - Z". REST polling can't see the absorption — only the before/after. WebSocket order-book diff feed makes this tractable.
**Trigger to revisit:** WebSocket order book subscription active with depth update events.
**Expected payoff:** High when it fires — absorption events are strong signals — but rare, so the per-run gain is moderate.

### A4. Liquidation × OFI Flip Detector

**What:** Currently `Liquidations` is penalty-only. Real reversal pattern: cascade triggers (liquidations fire, price moves), then **immediately afterward** OFI flips against the cascade direction (smart money fading the forced flow). Reward this transition rather than just penalising the cascade.
**Why deferred:** Pattern requires sub-second sequencing — "liq prints at T0, OFI flips at T0+200ms". REST polling at 30s intervals collapses this into a single observation, losing the sequence.
**Trigger to revisit:** WebSocket liquidation feed + WebSocket order book.
**Expected payoff:** High — captures a known reversal pattern the engine currently misses. Speculative until measured, but the trader-profile mentions this implicitly (Section 3: liquidations as cascade detection, not directional confirmation).

### A5. VPFR Profile Shape Classification (D / P / b / bimodal)

**What:** Classify the volume-profile distribution shape: D = balanced, P = acceptance into a high (reversal coming), b = acceptance into a low (reversal coming), bimodal = two POCs (consolidation between them). Each shape has well-known follow-on price behaviour in volume-profile literature.
**Why deferred:** Shape detection requires distinguishing "real" acceptance patterns from REST-polling artefacts — bucket noise from sparse 1m candles can fake a P-shape. WebSocket trade stream gives bucket precision needed to make shape classification robust.
**Trigger to revisit:** WebSocket trade subscription active **AND** at least 30 days of distribution data to validate shape interpretations against subsequent price action.
**Expected payoff:** Moderate. Shape classification is interpretive — programmatic action requires careful threshold tuning. Expected value is calibration-bound.

---

## Section B — Gated by CalibrationReport READY

The CalibrationReport gate (`docs/DeribitIndicatorProject.md` Section 12) requires ≥300 logged rows, ≥3 sessions, ≥3 regimes covered, ≥2 liquidation events. These items need empirical hit-rate data before tuning.

### B1. Per-Indicator Regime Weight Tuning

**What:** Pass 2c currently uses a single `AlignmentBonus` / `ConflictPenalty` scalar per regime. A more granular system would weight each indicator's contribution differently in TRENDING vs RANGE_BOUND (e.g. EMA worth more in TRENDING, VWAP worth more in RANGE_BOUND).
**Why deferred:** Tuning per-indicator weights without data is overfitting. The current single-scalar gate is a deliberate simplification specifically to avoid this.
**Trigger to revisit:** CalibrationReport READY; sufficient per-regime row count to compute per-indicator hit rates.
**Expected payoff:** Small-to-moderate. Risks overfitting on low sample sizes. Spec-first: write `adaptive-regime-weights-v2-proposal.md` before coding.

### B2. Auto-Tuning Weights from CSV Log

**What:** Once enough data exists, programmatically correlate each signal's vote with subsequent N-bar price direction and adjust `settings.json` weights. Currently `scoring.weights` block is gone (removed v15) — would need re-spec for what to weight and how.
**Why deferred:** Already in Section 13 fine-tuning backlog. Same calibration prerequisite as B1.
**Trigger to revisit:** CalibrationReport READY; per-indicator hit rates stable across regimes.
**Expected payoff:** Moderate, with significant overfitting risk. Cross-validation discipline required.

### B3. CSV Column Additions

Three columns currently noted as deferred in Section 12 of `DeribitIndicatorProject.md`. Aggregated here:

- **`VerdictContext`** — log the FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED tag per run. Enables per-tag accuracy correlation.
- **`FundingMomentum`** — log RISING / FALLING / FLAT plus raw delta. Enables Step 3b effectiveness validation.
- **`OiCvdPass2bOutcome`** — log "confirmed [L]", "confirmed [S]", "conflict [L]", "conflict [S]", or "noop". Enables Pass 2b validation directly from CSV instead of inferring from breakdown text.

**Why deferred:** Adding columns is cheap, but the validation analysis requires CalibrationReport-level data volume.
**Trigger to revisit:** Within 30 runs of CalibrationReport approaching READY (≈250 rows).
**Expected payoff:** Cheap to implement; validates already-shipped features. Low risk, modest gain.

### B4. Threshold Tuning Sweeps

Items in Section 12 needing 50+ live runs:

- **TFI threshold** (currently 0.15 vs alternative 0.10)
- **MicroCVD AccelThreshold static value and dynamic_pct** (post-launch of `dynamic-microcvd-accel-proposal.md`: tune `accel_threshold` static floor anchor, `accel_threshold_dynamic_pct` scaling factor, `accel_threshold_floor_pct`)
- **OFI ratios** (current 2.0 / 0.5 — relaxed in v14; review)
- **TTM FlatThreshold** (0.5 vs distribution percentile)
- **VPFR NumBuckets** (50 vs higher resolution)
- **Liquidations DominanceRatio** (currently 2.0)
- **Funding momentum thresholds** (0.0001 / window=3)
- **Session volume multipliers** (ASIA 0.80/0.85, LONDON 1.0/1.0, NY 1.15/1.10)
- **OI × CVD bonus / penalty** (currently 1 / 1)
- **AtrTargetMultiplier** (currently 2.0 vs logged R:R)
- **ContextTag thresholds** (StructuralMin=3, FlowMax=1)
- **Kelly est_prob_floor / est_prob_scale** (0.45 / 0.20)

**Why deferred:** All threshold tuning. Wait for data; do not adjust speculatively.
**Trigger to revisit:** Per-item — typically 50+ runs covering the relevant regime.
**Expected payoff:** Cumulative small gains. Each item alone is marginal; together they meaningfully refine the engine.

### B5. Spread WIDE Penalty Validation

**What:** Once `bid-ask-spread-proposal.md` ships, validate that WIDE-spread runs pushed to NO TRADE underperformed when traded anyway.
**Trigger to revisit:** 50+ runs after spread feature ships, with at least 5 WIDE events.
**Expected payoff:** Either confirms the threshold or motivates raising `wide_threshold_bps` from 5.0 to 7.0+.

---

## Section C — Gated by Multi-Session State Plumbing

These items need persistent state across sessions, which the engine doesn't currently track. The infrastructure is straightforward but not in scope for the current pass.

### C1. Multi-Session VPFR (Naked POCs)

**What:** A "naked POC" is a prior session's POC that hasn't been re-tested in the current session. These are well-known magnet levels — price often returns to test them.
**Why deferred:** Requires persisting POC values across session boundaries (UTC 00:00 and 13:30). Engine currently treats each run as standalone with respect to VPFR.
**Trigger to revisit:** When VPFR-lite v2 ships AND user expresses interest in multi-session profile work. Spec-first.
**Expected payoff:** Moderate. Naked POCs are a real signal in volume-profile trading, but the marginal gain on top of v2's nearest-HVN/LVN is unclear without data.

### C2. Anchored VWAP from Session High/Low or Last Major Event

**What:** In addition to dual-session VWAP, anchor a VWAP from the session high / session low / last major liquidation cascade. Provides additional structural reference levels for entry/exit.
**Why deferred:** Requires session high/low tracking across runs (the current high/low can be computed per run, but anchoring requires "remember this anchor going forward"). Marginal vs current dual-session VWAP + VPFR POC + swing pivots once those ship.
**Trigger to revisit:** After swing pivots ship and prove valuable, AND if there's still appetite for additional anchored references.
**Expected payoff:** Small. Diminishing returns territory.

---

## Section D — Spec-Only Deferred (No Infrastructure Prereq)

These items have no technical gating but are not yet specified. They require a proposal `.md` before any implementation.

### D1. Higher High / Higher Low / Lower High / Lower Low Trend Structure

**What:** Building on `swing-pivot-proposal.md`, classify the sequence of recent swings: HH+HL = uptrend structure; LH+LL = downtrend structure; HH+LL = expansion / divergence; LH+HL = contraction.
**Why deferred from swing-pivot v1:** Adds another layer of structural interpretation. The v1 spec is already large with three integration points; v1 ships with single-pivot detection, v2 builds the pattern recognition on top.
**Trigger to revisit:** Swing pivots shipped and stable for 30+ runs.
**Expected payoff:** Moderate. HH/HL structure is the bedrock of price-action trading; engine surfacing it would be aligned with trader-profile but currently the trader does this manually on chart.

### D2. Volume-Weighted Pivot Ranking

**What:** Not all swing pivots are equal — a swing high with 3× normal volume is a stronger reference than a swing high on average volume. Rank pivots by volume at the pivot bar (or within the wing window) and prefer high-volume pivots in cap arbitration.
**Why deferred from swing-pivot v1:** Adds complexity to the pivot scan. v1 ships with equal-weighted pivots; v2 layers volume weighting.
**Trigger to revisit:** Swing pivots shipped; observation that low-volume pivots are getting selected over higher-volume ones farther back in lookback.
**Expected payoff:** Small-to-moderate. Refines an already-shipped feature.

### D3. 5m RSI Divergence

**What:** Currently RSI divergence is detected on 1m only. Adding 5m divergence would catch slower-developing reversals that 1m noise hides.
**Why deferred:** Already in Section 13 backlog. Not gated by data — could spec and ship anytime, but lower priority than the four active proposals.
**Trigger to revisit:** After active proposals ship; review whether 1m RSI divergence is firing too often / too rarely.
**Expected payoff:** Small. RSI divergence is already weighted modestly in scoring; adding 5m just multiplies that small gain.

### D4. Donchian × BBW State Cross-Reference

**What:** Already in Section 13 backlog. A breakout from a tight (low-BBW) Donchian channel is a different signal class than a breakout from a wide channel. Cross-reference the BBW squeeze state when scoring Donchian.
**Why deferred:** Spec needed. Risk of double-counting (BBW already scores; Donchian already scores) — proposal must demonstrate it adds information.
**Trigger to revisit:** When user wants to refine breakout detection.
**Expected payoff:** Small-to-moderate. Refines an existing signal.

### D5. Smart OBV (Volume-Weighted by Price Change Magnitude)

**What:** Standard OBV adds volume on up bars, subtracts on down bars. Smart OBV weights by the price change magnitude — a 0.1% move with 1000 BTC volume contributes less than a 0.5% move with the same volume.
**Why deferred:** OBV is in trader-profile's "Neutral" category — useful but not core. Recalibrating divergence thresholds against a smart-OBV distribution is real work for a non-core indicator.
**Trigger to revisit:** Specifically when OBV divergence false-positives are observed in CalibrationReport.
**Expected payoff:** Marginal.

### D6. MFI Replacement of RSI

**What:** Money Flow Index = volume-weighted RSI. Could replace `r.RSI` to incorporate volume into the momentum oscillator.
**Why deferred:** RSI's main value to this engine is divergence detection — that logic is robust and well-calibrated. Replacing the input series with MFI would require re-validating `DivPenaltyRsiHigh = 65` / `DivPenaltyRsiLow = 35` against an MFI distribution that runs differently. Calibration cost real, gain unclear.
**Trigger to revisit:** Specifically when RSI divergence false-positives are observed AND there's appetite for re-tuning.
**Expected payoff:** Net unclear. Could be neutral or slightly positive.

---

## Section E — Rejected (Do Not Re-Propose Without New Evidence)

Per `trader-profile.md` Section 4, these have been explicitly rejected for documented reasons. Do not re-propose without citing what has changed.

| Indicator / Approach | Rejection Reason |
|---|---|
| **Stochastic (8,3,3)** | Flags overbought during valid breakout swings; would cause premature exits on the best trades |
| **MACD (6,13,5)** | Redundant with ROC; lags 2-3 candles during swing transitions; noisy in ranges. RSI covers divergence more cleanly |
| **CMF (20)** | 20-bar lag too slow; redundant with Volume SMA + VPFR for volume context |
| **Fixed-% profit targets** | Misalign with market structure; structural swing targets preferred |
| **ATR-based stops for execution** | Swing structure defines natural invalidation better; ATR retained for sizing and reference display only |
| **Pure scalping (fixed 0.1–0.5% targets)** | Too small for swing-to-swing volatility; ignores multi-timeframe context |
| **Pure momentum (ride indefinitely)** | Doesn't suit part-time monitoring or intraday-only constraint |
| **Non-directional rewards** | Any score component that pays out on flat/calm/no-signal conditions regardless of direction. Removed deliberately in v0.18 |
| **Double-counting the same signal across scoring layers** | If a signal already modifies final confidence, it shouldn't also appear in raw scoring |
| **Blunt flat penalties for regime transitions** | Use scaled penalties (e.g. ADX-proximity scale) rather than flat values that don't reflect severity |

---

## Section F — Not Backlog: Architectural Ceiling

### F1. WebSocket Feed (Real-time Order Book + Trade Stream)

This is the **single highest-impact non-indicator upgrade** per `architecture.md` Design Decisions. It's not in the backlog — it's the structural ceiling that gates Section A entirely.

**Status:** Recognised. Not specified. Deliberately deferred until indicator work above it is complete.

**Why not specified yet:** Implementing WebSocket touches the entire data layer (`DeribitClient.vb`, the analysis pipeline, possibly `DynamicNorms`, the auto-run timer cadence). It's a foundation rebuild, not a feature addition. Worth doing only when:
1. Active indicator backlog is exhausted (or close to it) — otherwise WebSocket benefit is diluted
2. There's appetite for a multi-session refactor with regression risk
3. Section A items become the priority — they're the ones that actually need WebSocket

**Trigger to revisit:** When the four in-flight specs (spread, OFI momentum, VPFR-lite v2, swing pivots) have shipped and stabilised, and either Section A items become attractive or the REST-polling latency becomes the binding constraint on accuracy.

---

## Maintenance

When an item moves out of this backlog (specced, implemented, or rejected with new evidence):

1. Update its entry to note the disposition and date
2. If shipped: cross-reference the implementing spec
3. If rejected: cite the new evidence in `trader-profile.md` Section 4 if the rejection rule changed

Do not delete entries. The history is the value — it shows what was considered and why deferred.
