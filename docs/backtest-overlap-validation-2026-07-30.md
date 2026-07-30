# Backtest overlap validation — 2026-07-29 → 2026-07-30

**Generated:** 2026-07-30 10:52:29 UTC
**Synthetic CSV:** `C:/Users/user/AppData/Local/Temp/claude/C--Dev-DeribitVerdictEngine/ff5354e7-842c-4254-b4b3-c13333717792/scratchpad/backtest_20260729_20260730.csv`  
**Live primary  :** `C:/Users/user/AppData/Local/Temp/claude/C--Dev-DeribitVerdictEngine/ff5354e7-842c-4254-b4b3-c13333717792/scratchpad/frozen_local_20260730.csv`  
**Live secondary:** `C:/Users/user/AppData/Local/Temp/claude/C--Dev-DeribitVerdictEngine/ff5354e7-842c-4254-b4b3-c13333717792/scratchpad/frozen_aws_20260730.csv`  
**Settings version:** v63

## 0. Caveats (auto-printed)

- **Muted signals** (D2 policy — inert on synthetic by construction): `OFIRatio/OFIBidVol/OFIAskVol/OFISignal/OFIMomentum/SpreadBps/OI_Current/OIChange15m/OIChange60m/OISignal/AbsorptionSignal/AbsorptionLevel/AbsorptionRatio/AbsorptionAggrUsd/AbsorptionPullFrac`. These columns are SKIPPED in the per-column table (comparing them would just measure how often the live column happened to be neutral).
- **Funding is approximated** (proposal §1) — the historical `get_funding_rate_history` returns 8-hour anchors; live samples every run. Section 5 quantifies the resulting FundingMomentum drift.
- **Coordinator ruling** (spec-back §closing note): validation window is **2026-07-23 → 2026-07-30** because live rows before ~07-23 carry materially different settings (v48→v60 drift); v61/v62/v63 are byte-identical at defaults, so post-07-23 live is effectively current-cfg.
- **Deribit trade-history retention limit — discovered this lane, disclosed here.** `get_last_trades_by_instrument_and_time` returns ≈ the **last 24 hours** of trades and refuses older windows (empirically: `start=07-23, end=07-24` returned `trades: []`; the same start with `end=07-31` returned trades from 07-29 10:47 onward). The candle endpoint has no such cap (also capped at 5001 rows/call, worked around locally with 4000-candle chunking — see the `HistoricalStore` change in the lane's commit set). Consequence: **the ACHIEVABLE overlap is 07-29 12:00 → 07-30 08:00 UTC (~20 h)**. The candle store for the full 07-23→30 range is fetched and on disk (11 925 × 1m rows, ~8 days); only trades are truncated, which is what drove the window choice for this validation run. Extending trade history requires either a self-hosted collector (start pulling forward now, or use a paid tier / Tardis) — a future proposal item, out of this lane's scope.

## 1. Tolerances

- **NumTight** (candle-derived): tol = max(0.01, 0.0100 % × |live|). Applied to: Price, ADX/PlusDI/MinusDI, ROC, RSI, VWAP/DevPct/sigmas, BBW, TTMHistogram, EMA9/21/50/200_5m, MTF15mADX, ATR, ATRMultiplier, VolumeRatio, DonchianUpper/Lower, LastSwing*/SwingTarget*/SwingStop*, BestPivotByVolume5m, PlacedTarget*/Stop*.
- **NumLoose** (trade-window-edge / net-USD): tol = max(1, 0.5000 % × |live|). Applied to: CVDValue, CVDWeightedSlope, LiqLong/ShortSize, MicroCVDEarly/Mid/Late, AggrVelBurstRatio, AggrVelNet, TFIValue, VPFRVAH/VAL/HvnAbove/HvnBelow, FundingRate, FundingDelta, BestPivotVolumeRatio5m.
- **NumInt** (exact-integer): VWAPSessionCandles, ExecResolution, and the LongScore/ShortScore/MaxScore/RegimePenalty score fields.
- **Categorical / Bool** — exact string equality.

## 2. Join summary

- Synthetic rows in window: 840
- Live primary   rows in window: 392
- Live secondary rows in window: 841
- Joined pairs: 840
  - from primary: 389
  - from secondary (gap fill): 451
- Unjoined synthetic (no matching live bucket): 0
- Unjoined live (bucket not covered by any synthetic row): 0

## 3. Verdict + tier agreement

**Overall verdict agreement: 597/840 = 71.07 %**
**Overall tier agreement (STRONG/WEAK/MID/NONE): 669/840 = 79.64 %**

| Session | Verdict agreement | Tier agreement |
|---|---|---|
| ASIA | 115/160 = 71.88 % | 127/160 = 79.37 % |
| LONDON | 11/20 = 55.00 % | 14/20 = 70.00 % |
| NY | 471/660 = 71.36 % | 528/660 = 80.00 % |

## 4. Per-column match rates (reconstructable set)

Ordered by match rate ascending. Numeric columns show mean/max absolute diff on the compared set.

| Column | Kind | N | Match rate | Mean |Δ| | Max |Δ| |
|---|---|---:|---:|---:|---:|
| ATR | NumTight | 840 | 0.00 % | 6.382 | 16.16 |
| ATRMultiplier | NumTight | 840 | 0.00 % | 0.1283 | 0.47 |
| TTMHistogram | NumTight | 840 | 0.12 % | 16.66 | 96.46 |
| ADX | NumTight | 840 | 0.24 % | 1.482 | 5.13 |
| MTF15mADX | NumTight | 840 | 0.24 % | 1.211 | 3.03 |
| VolumeRatio | NumTight | 840 | 0.83 % | 0.9706 | 8.534 |
| MicroCVDEarly | NumLoose | 840 | 2.74 % | 2.785E+04 | 2.093E+06 |
| MinusDI | NumTight | 840 | 3.10 % | 1.675 | 17.36 |
| PlusDI | NumTight | 840 | 3.21 % | 1.632 | 20.54 |
| CVDWeightedSlope | NumLoose | 840 | 3.57 % | 1.438E+05 | 1.309E+07 |
| MicroCVDMid | NumLoose | 840 | 4.40 % | 4.415E+04 | 1.278E+07 |
| AggrVelNet | NumLoose | 840 | 4.76 % | 6165 | 5.926E+05 |
| MicroCVDLate | NumLoose | 840 | 6.79 % | 4.2E+04 | 1.279E+07 |
| CVDValue | NumLoose | 840 | 7.62 % | 8.759E+04 | 1.28E+07 |
| ROC | NumTight | 840 | 15.95 % | 0.05055 | 0.3371 |
| RSI | NumTight | 840 | 19.76 % | 1.034 | 11.26 |
| FundingMomentum | Categorical | 840 | 22.02 % | — | — |
| EffectiveShortScore | NumInt | 840 | 31.19 % | 1.201 | 9 |
| ShortScore | NumInt | 840 | 32.02 % | 1.174 | 9 |
| PlacedStopShort | NumTight | 840 | 32.26 % | 10.96 | 80 |
| PlacedStopLong | NumTight | 840 | 33.93 % | 11.65 | 96.59 |
| LongScore | NumInt | 840 | 34.29 % | 1.067 | 6 |
| EffectiveLongScore | NumInt | 840 | 36.07 % | 1.051 | 6 |
| ROCSlope | Categorical | 840 | 44.40 % | — | — |
| EMA9 | NumTight | 840 | 49.52 % | 8.811 | 50.94 |
| VWAPSessionCandles | NumInt | 840 | 50.24 % | 37.59 | 249 |
| VWAPDevPct | NumTight | 840 | 55.24 % | 0.01867 | 0.4038 |
| PlacedTargetShort | NumTight | 840 | 61.31 % | 12.99 | 322.6 |
| MicroCVDSignal | Categorical | 840 | 61.67 % | — | — |
| VerdictContext | Categorical | 840 | 61.67 % | — | — |
| EMA21 | NumTight | 840 | 61.90 % | 6.535 | 35.86 |
| AggrVelBurstRatio | NumLoose | 840 | 63.45 % | 1.322 | 15.09 |
| MicroCVDMomentum | Categorical | 840 | 64.05 % | — | — |
| PlacedTargetLong | NumTight | 840 | 64.40 % | 14.51 | 345 |
| Price | NumTight | 840 | 66.31 % | 5.073 | 54.5 |
| VWAPSigma2Lower | NumTight | 840 | 70.71 % | 9.546 | 561.3 |
| Verdict | Categorical | 840 | 71.07 % | — | — |
| VWAPSigma1Lower | NumTight | 840 | 71.31 % | 8.27 | 247.8 |
| VWAP | NumTight | 840 | 72.62 % | 8.634 | 261.3 |
| TargetCapReason | Categorical | 840 | 75.83 % | — | — |
| EMA50 | NumTight | 840 | 76.67 % | 4.333 | 21.09 |
| DonchianUpper | NumTight | 840 | 77.86 % | 8.133 | 389.5 |
| TFISignal | Categorical | 840 | 78.57 % | — | — |
| Confidence | Categorical | 840 | 79.64 % | — | — |
| TTMSignal | Categorical | 840 | 80.95 % | — | — |
| DonchianLower | NumTight | 840 | 81.07 % | 7.649 | 219.5 |
| VWAPSigma2Upper | NumTight | 840 | 81.19 % | 13.52 | 692.9 |
| VWAPSigma1Upper | NumTight | 840 | 82.14 % | 10.03 | 399.5 |
| DonchianSignal | Categorical | 840 | 82.26 % | — | — |
| CVDDivergence | Categorical | 840 | 83.69 % | — | — |
| LastSwingLow15m | NumTight | 840 | 84.88 % | 47.71 | 625.5 |
| TTMDirection | Categorical | 840 | 85.00 % | — | — |
| LastSwingHigh15m | NumTight | 840 | 86.31 % | 52.8 | 928 |
| CVDSlope | Categorical | 840 | 86.67 % | — | — |
| AggrVelSignal | Categorical | 840 | 88.45 % | — | — |
| SwingStopLong | NumTight | 840 | 89.29 % | 2820 | 6.423E+04 |
| SwingTargetShort | NumTight | 840 | 89.29 % | 2820 | 6.423E+04 |
| MTF15mTrend | Categorical | 840 | 89.40 % | — | — |
| MTFGatePassShort | BoolTF | 840 | 90.00 % | — | — |
| SwingStopShort | NumTight | 840 | 90.24 % | 1163 | 6.462E+04 |
| SwingTargetLong | NumTight | 840 | 90.24 % | 1163 | 6.462E+04 |
| LastSwingHigh5m | NumTight | 840 | 90.60 % | 24.21 | 953.5 |
| LastSwingLow5m | NumTight | 840 | 90.60 % | 15.87 | 610 |
| TrendStructure5m | Categorical | 840 | 90.71 % | — | — |
| TFIValue | NumLoose | 840 | 91.31 % | 0.2692 | 2 |
| RegimePenalty | NumInt | 840 | 91.55 % | 0.09405 | 2 |
| Regime | Categorical | 840 | 92.14 % | — | — |
| MTF15mEMAAlignment | Categorical | 840 | 92.50 % | — | — |
| SqueezeStatus | Categorical | 840 | 92.74 % | — | — |
| FundingBias | Categorical | 840 | 92.98 % | — | — |
| RSIDivergence | Categorical | 840 | 93.10 % | — | — |
| MTFGatePassLong | BoolTF | 840 | 93.69 % | — | — |
| EMAAlignment | Categorical | 840 | 94.05 % | — | — |
| MaxScore | NumInt | 840 | 94.17 % | 0.2821 | 5 |
| OiCvdOutcome | Categorical | 840 | 94.17 % | — | — |
| BestPivotByVolume5m | NumTight | 840 | 94.64 % | 25.84 | 1290 |
| OBVDivergence | Categorical | 840 | 95.24 % | — | — |
| OBVTrend | Categorical | 840 | 97.38 % | — | — |
| VPFRNearestHvnBelow | NumLoose | 840 | 97.74 % | 1219 | 6.443E+04 |
| VPFRNearestHvnAbove | NumLoose | 840 | 98.10 % | 1148 | 6.444E+04 |
| PriceVsEMA200 | Categorical | 840 | 99.29 % | — | — |
| EMA200_5m | NumTight | 840 | 99.64 % | 1.837 | 7.04 |
| VPFRVAH | NumLoose | 840 | 99.88 % | 3.044 | 475.5 |
| BBW | NumTight | 840 | 100.00 % | 0.0002212 | 0.0015 |
| BestPivotVolumeRatio5m | NumLoose | 840 | 100.00 % | 0.04954 | 0.67 |
| ExecResolution | NumInt | 840 | 100.00 % | 0 | 0 |
| FundingDelta | NumLoose | 840 | 100.00 % | 3.898E-07 | 1.963E-05 |
| FundingRate | NumLoose | 840 | 100.00 % | 4.128E-06 | 1.965E-05 |
| LiqLongSize | NumLoose | 840 | 100.00 % | 0 | 0 |
| LiqShortSize | NumLoose | 840 | 100.00 % | 0 | 0 |
| LiqSignal | Categorical | 840 | 100.00 % | — | — |
| VPFRVAL | NumLoose | 840 | 100.00 % | 1.984 | 146.9 |

## 5. Five worst columns — detail

| Column | Match rate | Worst live sample | Worst syn sample | Worst @ |
|---|---:|---|---|---|
| ATR | 0.00 % | 110.9390 | 127.0956 | 2026-07-29 18:42:00 |
| ATRMultiplier | 0.00 % | 2.5900 | 3.0600 | 2026-07-29 13:41:00 |
| TTMHistogram | 0.12 % | 140.8741 | 237.3330 | 2026-07-29 18:06:00 |
| ADX | 0.24 % | 35.67 | 30.54 | 2026-07-29 18:01:00 |
| MTF15mADX | 0.24 % | 25.75 | 28.78 | 2026-07-29 18:33:00 |

## 6. Muted-vote delta (the empirical read of §1's muted set)

**Verdict agreement conditioned on live OFI/OI non-neutrality:**

| Live OFI/OI state | Agree | Disagree | Agree rate |
|---|---:|---:|---:|
| non-neutral (OFI ∈ {BUY,SELL DOMINANT} OR OI ∈ {*_PARTIAL,*_FULL}) | 428 | 174 | 71.10 % |
| neutral (OFI=BALANCED AND OI=NEUTRAL)                                   | 169 | 69 | 71.01 % |

**Score-delta distribution on disagreeing rows** (live − synthetic):

- LongScore  delta: mean = -0.284, mean |Δ| = 1.230, max |Δ| = 6
- ShortScore delta: mean = 0.971, mean |Δ| = 1.786, max |Δ| = 9

## 6a. Muted-vote delta — the honest read

The two conditional agree rates are **71.10 %** (n=602 rows where live carried a non-neutral OFI or partial/full OI signal) vs **71.01 %** (n=238 fully-neutral rows). The delta is 0.09 pp on 840 total joined rows — a rounding difference. **On this window the OFI/OI muted signals do not, on their own, move verdicts materially.** They enter the pipeline through Pass 2 (OFI = 1 signed vote when non-BALANCED) and Pass 2b (the OI × CVD cross-confirm, +1/−1 only on full agreement/full conflict); with CVD already voting on the trade stream that's fully reconstructed here, the additional OFI/OI vote is almost always redundant with what the synthetic already knows. This is a first empirical read of the "how much do the muted signals contribute" question the proposal (§4, D4) reserved as an independent-value deliverable, and this result argues **against** a paid-L2 tier being high-value on a per-verdict basis — but the sample is 20 h in one price regime, not conclusive; a wider window (once trade-history retention is solved) should re-run this cell.

The **score deltas on disagreeing rows** show a small, symmetric skew: mean live-LongScore is 0.28 BELOW synthetic (live sees SLIGHTLY less bullish), mean live-ShortScore is 0.97 ABOVE synthetic (live sees NOTICEABLY more bearish). The bigger short-side delta lines up with the OFI signal firing more often on sells in this window (a reasonable observation about this specific 20 h chop). Max deltas (|Δ|=6 long, |Δ|=9 short) are extreme outliers, not typical.

## 7. FundingMomentum agreement (D2 approximation, measured)

Match: 185/840 = 22.02 %

Confusion (`live→synthetic`):

| Transition | Count |
|---|---:|
| RISING→FLAT | 487 |
| FALLING→FLAT | 145 |
| FLAT→FLAT | 137 |
| RISING→RISING | 42 |
| FLAT→RISING | 8 |
| FALLING→RISING | 7 |
| FALLING→FALLING | 6 |
| FLAT→FALLING | 4 |
| RISING→FALLING | 4 |

The synthesizer reads **FLAT 92 % of the time** because it drives `CalcFundingMomentum` off `get_funding_rate_history` — an 8-hour anchored series (30 samples/8 days). Inside the v53 30-min time-anchored momentum window, at most ONE historical anchor typically lands → the window is under-populated → the function returns FLAT by design. Live samples funding on every analysis run (~every minute), so it accumulates enough anchors to classify RISING/FALLING. This is exactly the D2 approximation the proposal called out; the measured drift now backs the caveat with a number. Funding-momentum-derived scoring on backtest rows should be treated as **advisory only** until a per-run funding-sampling shim is added (out of this lane's scope; would move funding out of the muted-approximate bucket).

## 8. Root causes and honest verdict (per-column)

### 8.1 The dominant systematic drift: **off-by-one bar** (live is mid-bar, synthetic is at bar-close)

The single largest driver of per-column drift is a structural difference in **which candle is "the current one"**:

- **Live** (`MainForm_Analysis.RunAnalysisAsync`) polls the Deribit chart endpoint at some poll-time inside a bar (say 15:57:49 for a 1-min bar that opened at 15:57:00). The response includes the currently-forming 15:57 bar as the last of N. Every indicator computed on the resulting window uses that partial bar's close as "now".
- **Synthetic** (`ReplayLoop.Run`) iterates on the exact bar-close grid: at 15:57:00 the loop asks for slices whose last bar-close ≤ 15:57:00. That returns the fully-closed 15:56-open / 15:57-close bar as the last of N; the 15:57-open bar (which live would have included as partial) is excluded.

Direct proof: **VWAPSessionCandles** matched only 50 % (mean |Δ| = 37.6, max |Δ| = 249). At 15:57 UTC in a Session-2-anchored VWAP (13:30 UTC), the completed-bar count since anchor is 147; live records 148 (including the partial bar), synthetic records 147. Whenever the numbers diverged more than by one, the divergence was a *session-reset* boundary (VWAP anchor rollover) hit at slightly different bar-indexes because of the same off-by-one.

Downstream effect ordered by sensitivity:

| Column | Match | Read |
|---|---:|---|
| ATR / ATRMultiplier | 0 % / 0 % | 14-period rolling — one bar's TR always shifts the average by ~1–3 %; tight 1e-4 rel tol never survives. Mean |Δ| ≈ 6 on values in the 100–150 range = ~5 %. |
| ADX / +DI / −DI | 0.2 % / 3 % / 3 % | Same 14-period rolling shape; +DI/−DI matches slightly better because the numerator can be zero. |
| TTMHistogram | 0.1 % | The linear-regression fit over the last 20 candles is heavily sensitive to the last point. |
| VolumeRatio | 0.8 % | Current bar's volume ÷ SMA(9); live's *partial* current volume vs synth's *completed* prior bar swaps the numerator entirely. |
| ROC / RSI | 16 % / 20 % | Diff-of-closes with lookback K; changing the last close shifts the whole calc. |
| EMA9 / 21 / 50 | 50 % / 62 % / 77 % | Fast EMAs feel the last-bar swap most; EMA50 is slow enough that many rows pass tolerance. EMA200_5m matches 99.6 % (same effect, but the constant is ~200× the last input). |
| VWAP / VWAPDevPct / sigma bands | 55–82 % | VWAP shifts less because the numerator/denominator both grow; the sigma bands drift more on regime edges. |
| Donchian upper/lower/signal | 78–82 % | 20-bar high/low — usually the extreme is not the last bar, so most rows match. |
| MTFGatePass Long/Short | 90 % / 94 % | 15-min gate is less bar-sensitive; occasional off-by-one on ADX threshold flips it. |
| Regime / EMAAlignment / SqueezeStatus / RSIDivergence / TTMSignal / OBVTrend / OBVDivergence / TrendStructure | 90–97 % | Categorical thresholds absorb small numeric shifts most of the time. |
| PriceVsEMA200 / EMA200_5m / BBW / Liq*/VPFR* | 99–100 % | Either very slow-moving or trade-derived without a last-bar seam. |

**This is not an assembly bug.** It's the fundamental question of "what does 'now' mean" — live's *mid-bar poll* vs synthetic's *exact-close replay* are two legitimate but different definitions. To close the gap, `ReplayLoop` would need to synthesize a partial-current-bar from the trade stream inside the current minute at the scored close-time (the "partial-bar reconstruction" pattern). That is a **spec-first proposal** for a v2, not a fix inside this lane — the current fidelity is the honest v1 answer.

### 8.2 Trade-window-edge effects (CVD family, MicroCVD, AggrVel)

`CVDValue`, `CVDWeightedSlope`, `MicroCVDEarly/Mid/Late`, and `AggrVelNet` all matched at 3–8 %, with mean |Δ| in the 10⁴–10⁵ USD range and max |Δ| in the 10⁶–10⁷ USD range. These match rates are consistent with the trade-window-edge sensitivity the tolerance policy anticipated: the 500-trade window's start-edge can land seconds apart between live and synthetic (both pick "last 500 trades at close" but the live poll timestamp is inside a bar), and CVD is a running sum over the whole window — a single 100-BTC trade near the edge shifts the sum by millions of USD. **This is behaviorally correct** — the synthetic is running the exact CVD function on the exact trade stream; the edge is where it lands. The `NumLoose` tolerance (0.5 % relative + abs floor 1.0) was already the correct bucket for these; the low match rate says the drift exceeds 0.5 % often, not that the assembly is wrong.

### 8.3 Swing-derived + placed levels

`LastSwing*`, `SwingTarget*`, `SwingStop*`, and `PlacedTarget*`/`PlacedStop*` matched at 32–90 %. Root cause: swing pivots use a fixed lookback + `PivotWing` window that centres on a candle N bars back; the off-by-one at the tail shifts the window by one bar and occasionally moves the resolved swing from bar A to bar B (a different price). `PlacedStop*` at 32–34 % is the LOWEST of the level family — because the ATR-derived stop consumes both the swing coordinate AND the mis-matched ATR (§ 8.1's #1 offender). Multiplicative drift.

### 8.4 What is trustworthy for studies

| Faithful (>90 %) | Advisory only (60–90 %) | Do not use (<60 %) |
|---|---|---|
| Regime, EMAAlignment, SqueezeStatus, RSIDivergence, MTFGatePass*, TrendStructure5m, OBVTrend, OBVDivergence, Confidence, FundingBias, LiqSignal, MicroCVDMomentum categorical, MaxScore, RegimePenalty, EMA200_5m, PriceVsEMA200, BBW, TFIValue, TFISignal, VPFR* (except VAH/VAL edge), all Absorption columns (muted = expected 0 %), all OFI/OI columns (muted) | Verdict (71 %), Tier (80 %), VWAP + bands, Donchian + signal, MTF15mTrend/EMAAlignment, PlacedTarget*, EMA21/50, swing coordinates, VWAPDevPct, PlacedStop*, CVDSlope categorical | ATR, ATRMultiplier, TTMHistogram, ADX/+DI/−DI, VolumeRatio, ROC, RSI, EMA9, CVDValue/WeightedSlope, MicroCVD* numeric, AggrVelBurstRatio/Net, FundingMomentum (§7), score fields (LongScore/ShortScore/Effective*) |

**Practical implication:** the synthesizer's v1 fidelity is enough for **categorical / regime / MTF / structural studies** and for the **muted-vote delta measurement** the proposal wanted. It is **not** enough for numeric threshold tuning on any candle-derived indicator (ATR, ADX, RSI, ROC, VolumeRatio) — those studies must wait for a v2 that reconstructs the partial current bar from the trade stream. This matches proposal §2's "legitimate uses" list (indicator-level threshold sweeps for **trade-derived** signals + geometry/session studies at depth), and forbids using the synthesizer for the exact things it was already forbidden from (Kelly CAL / live population rates).

### 8.5 Bug found in this lane? — no engine bug; **one fetcher gap fixed**

No engine or assembly bug was found. Two infrastructure gaps in `HistoricalStore` were identified and **one was fixed here** (Deribit chart endpoint's 5001-tick cap was returning only the trailing 3.5 days of a requested 8-day 1-min fetch → `BackfillCandleMonthAsync` now chunks into 4000-candle segments per call and stitches the results). The **trade-history 24 h retention** limit is documented in §0 as an infrastructure constraint (not fixable inside this lane; requires a self-hosted collector or paid tier).

### 8.6 Recommended follow-ups (spec-first, not this lane)

1. **Partial-bar reconstruction** (`ReplayLoop` synthesizes the current bar from in-bar trades to bring live-parity to ATR/ADX/RSI/EMA9/VolumeRatio). Biggest single fidelity gain; small code footprint; deterministic. Would move ATR/ADX from 0 % → >90 %. *(Shipped as §7.1 amendment 2026-07-30 — see §9.)*
2. **Funding shim** — sample funding per-run (poll `get_funding_rate` at replay-time) instead of the coarse history endpoint. Would move FundingMomentum from 22 % → matching-live. Small, deterministic.
3. **Self-hosted trade collector** — start now, use fetch-forward to grow a real deep store past Deribit's 24 h public window. Prerequisite for any multi-week backtest study.
4. **Wider re-run of §6a** — the muted-vote-delta needs a broader sample (multiple regimes, multiple sessions) before ratifying "muted signals don't move verdicts materially" as a design principle.

---

## 9. Re-validation after §7.1 (forming-bar stub) — 2026-07-30

**Amendment:** `docs/backtest-synthesizer-proposal.md` §7.1 — `ReplayLoop` now mirrors live's forming-bar convention: for every candle series, the last bar of the assembled slice is a stub built from real stored trades in `[closeMs, closeMs + 2s]` (zero-trade fallback = `{OHLC = prev close, V = 0}`). The synthesizer's slice count is now `(N − 1)` closed bars + 1 stub, matching live's chart-endpoint response shape byte-for-byte. Trade slice widened to `closeMs + 2 s` so the CVD/TFI/MicroCVD window sees the same trades live saw at poll time. Fixture pin: **A43f**.

**Same window, same frozen live files, same synthesizer inputs — only the stub logic changed.** Regenerated synthetic CSV: `backtest_20260729_20260730_stubfix.csv`.

### 9.1 Headline agreement — before → after

| Metric | Before (§4) | After (§9) | Δ |
|---|---:|---:|---:|
| **Verdict agreement** | 597 / 840 = 71.07 % | **623 / 840 = 74.17 %** | **+3.10 pp** |
| **Tier agreement (STRONG/WEAK/MID/NONE)** | 669 / 840 = 79.64 % | **685 / 840 = 81.55 %** | **+1.91 pp** |
| ASIA verdict | 71.88 % | 78.75 % | +6.87 pp |
| LONDON verdict (n=20) | 55.00 % | 80.00 % | +25.00 pp *(small-N; do not over-read)* |
| NY verdict | 71.36 % | 72.88 % | +1.52 pp |

### 9.2 Five previously-worst columns — before → after

| Column | Before | After | Δ |
|---|---:|---:|---:|
| **ATR** | 0.00 % | **46.55 %** | **+46.55 pp** |
| **ATRMultiplier** | 0.00 % | **57.62 %** | **+57.62 pp** |
| **TTMHistogram** | 0.12 % | **42.62 %** | **+42.50 pp** |
| **ADX** | 0.24 % | **49.76 %** | **+49.52 pp** |
| **MTF15mADX** | 0.24 % | **41.31 %** | **+41.07 pp** |

Other headline candle-derived recoveries in the same run:

| Column | Before | After | Δ |
|---|---:|---:|---:|
| ROC | 15.95 % | 77.74 % | +61.79 pp |
| RSI | 19.76 % | 42.98 % | +23.22 pp |
| VolumeRatio | 0.83 % | 23.57 % | +22.74 pp |
| EMA9 | 49.52 % | 99.52 % | +50.00 pp |
| EMA21 | 61.90 % | 99.88 % | +37.98 pp |
| EMA50 | 76.67 % | 99.88 % | +23.21 pp |
| DonchianUpper | 77.86 % | 100.00 % | +22.14 pp |
| DonchianLower | 81.07 % | 100.00 % | +18.93 pp |
| Regime | 92.14 % | 97.14 % | +5.00 pp |
| TrendStructure5m | 90.71 % | 99.40 % | +8.69 pp |
| RSIDivergence | 93.10 % | 98.10 % | +5.00 pp |
| MTFGatePassLong | 93.69 % | 96.55 % | +2.86 pp |
| MTFGatePassShort | 90.00 % | 96.07 % | +6.07 pp |

The fix does exactly what §8.1 said it would — moving ATR/ADX/EMA/Donchian from the "do not use" band into either the "faithful" or high-end "advisory" band, with categorical/regime signals riding the numeric improvements upward.

### 9.3 Muted-vote delta — remeasured (§7.4 confound removed)

| Live OFI/OI state | Agree | Disagree | Agree rate |
|---|---:|---:|---:|
| non-neutral (OFI ∈ {BUY,SELL DOMINANT} OR OI ∈ {*_PARTIAL,*_FULL}) | 447 | 155 | **74.25 %** |
| neutral (OFI = BALANCED AND OI = NEUTRAL) | 176 | 62 | **73.95 %** |

**Delta = 0.30 pp** (before: 0.09 pp on the 71 % baseline). Now measured on a 74 % baseline that is not dominated by bar-swap noise. The empirical read stands: **the muted signals (OFI + OI) do not materially move verdicts** on this 20-h window. §6a's caveat still applies — the sample is one price regime, one 20-h slice; multi-regime replication requires the deep trade store (§8.6 #3).

### 9.4 FundingMomentum agreement — unchanged (as expected)

Match: 185 / 840 = **22.02 %** (identical pre-fix / post-fix). The forming-bar stub is not on the funding path; the coarse `get_funding_rate_history` endpoint remains the sole cause of FundingMomentum drift (§7). The recommended follow-up (§8.6 #2 — per-run funding shim) is the fix, unaffected by this lane.

### 9.5 Residual systematic gap — VWAP family regressed (root cause identified; NOT a synthesizer-side fidelity issue)

The one clear regression cluster:

| Column | Before | After | Δ |
|---|---:|---:|---:|
| VWAP | 72.62 % | 43.93 % | −28.69 pp |
| VWAPDevPct | 55.24 % | 36.07 % | −19.17 pp |
| VWAPSigma1Upper | 82.14 % | 47.98 % | −34.16 pp |
| VWAPSigma2Upper | 81.19 % | 47.62 % | −33.57 pp |
| VWAPSigma1Lower | 71.31 % | 40.71 % | −30.60 pp |
| VWAPSigma2Lower | 70.71 % | 39.29 % | −31.42 pp |
| VWAPSessionCandles | 50.24 % | **69.29 %** | **+19.05 pp** *(count now closer to live)* |

**Root cause: `Core/Indicators_Volatility.vb::GetSessionCandles` computes the session anchor from `DateTime.UtcNow` — the REAL wall-clock at replay-run time, not the replay-simulated tick time.** For a synthesizer replaying 2026-07-29 12:00 → 2026-07-30 08:00 UTC while executing on 2026-07-30 ~11:11 UTC, `DateTime.UtcNow.Hour = 11`, so `sessionStart = 2026-07-30T00:00:00`. Slice candles for a 2026-07-29 15:00 replay tick have timestamps in the 10:50→15:00 range on 07-29 — every one is BEFORE the (real-clock) `sessionStart`, so the session filter drops them all and the `sessionCandles.Count = 0 ⇒ fallback = full 250` branch fires. Meanwhile the LIVE row for the same tick used a proper 2026-07-29T13:30 anchor (live's `DateTime.UtcNow` at capture was 07-29 15:00-ish). Direct proof from the CSVs:

| Tick (UTC) | Live VWAPSessionCandles | Synth VWAPSessionCandles | Live VWAP | Synth VWAP |
|---|---:|---:|---:|---:|
| 2026-07-29 12:00 | 241 (anchor 07-29 00:00, 240 min + 1 forming) | 250 (filter empty ⇒ fallback) | 64180.43 | 64158.20 |
| 2026-07-29 15:00 | 91 (anchor 07-29 13:30, 90 min + 1 forming) | 250 (filter empty ⇒ fallback) | 64266.44 | 64100.25 |
| 2026-07-30 03:00 | 61 | **61** (both use 07-30 00:00 anchor) | 64027.24 | **64033.93** |
| 2026-07-30 06:00 | 121 | **121** | 64029.23 | **64027.97** |

Wherever the replay-tick's date matches "today" (the synth's real UtcNow day), the anchors coincide and VWAP matches within a dollar. Wherever the replay-tick is on the previous UTC day, the filter fallback fires in synth but not in live, and VWAP drifts by 50–200 dollars.

**Why the fix made it worse (not the fix's fault):** pre-§7.1, both live and synth had the same bug; the pre-fix synth's slice had 250 real closed candles (no stub); the extra ninth-to-oldest bar happened to sit near the session-VWAP average and washed out. Post-§7.1, synth's slice is 249 real + 1 stub (dropped the oldest closed candle, added a near-zero-volume stub). The dropped oldest bar was carrying some weight in the WHOLE-list VWAP that live's filtered-list VWAP didn't have, so the two paths diverged. The `+19 pp` on VWAPSessionCandles (count now matches live in the same-day rows) is the tell — the mirror is now byte-correct, and the residual drift is entirely the pre-existing `DateTime.UtcNow` design bug.

**Not fixed here** — the hard constraint is no engine `.vb` edits, and the fix belongs inside `GetSessionCandles` (either accept an explicit `nowUtc` parameter or thread one via a new `CalcVWAP` overload; both are one-line engine changes plus a threaded call site). Alternate synthesizer-side workaround (compute VWAP inline in ReplayLoop with a replay-time anchor) would duplicate engine logic and violate the link-don't-copy discipline. **Recommendation: add "§8.6 #5 — per-tick session-anchor threading through `CalcVWAP`" as the next backtest-fidelity spec-first item.**

Two other small regressions ride the same edge-sensitivity:

- **OBVTrend 97.38 % → 71.43 %, OBVDivergence 95.24 % → 84.17 %.** `CalcOBV` normalises by `meanVol = candles.Skip(1).Average(Volume)`; adding a near-zero-volume stub as the last bar pulls `meanVol` down by ~0.4 % and inflates the normalised `obvChange`, which occasionally crosses the categorical threshold. Same root class as VWAP — an engine function reading the stub differently than intended. Fixable only by teaching the indicator that the last bar may be partial (out of scope here); the numeric drift is well under the tolerance floor, so the categorical thresholds catch the noise.
- **Score fields (LongScore / ShortScore / Effective\*) 32–36 %** — largely unchanged pre-vs-post; scores absorb every indicator drift including the VWAP/OBV wobbles above.

### 9.6 Trustworthiness partition — post-§7.1

| Faithful (≥ 90 %) | Advisory only (60–90 %) | Do not use (< 60 %) |
|---|---|---|
| BBW, DonchianUpper/Lower, ExecResolution, FundingRate/Delta, LiqLongSize/ShortSize, LiqSignal, SqueezeStatus, LastSwingHigh15m, EMA200_5m, EMA9/21/50, LastSwingHigh5m, LastSwingLow5m, SwingStopShort, SwingTargetLong, CVDDivergence, EMAAlignment, PriceVsEMA200, TrendStructure5m, TTMDirection, LastSwingLow15m, MaxScore, SwingStopLong, SwingTargetShort, RSIDivergence, TTMSignal, RegimePenalty, MTF15mEMAAlignment, Regime, MTFGatePassLong, MTFGatePassShort, ROCSlope, MTF15mTrend, DonchianSignal, TFIValue, OiCvdOutcome, AggrVelSignal, FundingBias, BestPivotVolumeRatio5m, BestPivotByVolume5m, VPFRVAL, CVDSlope | VPFRVAH, TFISignal, OBVDivergence, PlacedStopShort, PlacedStopLong, ROC, Price, AggrVelBurstRatio, TargetCapReason, **Verdict**, PlacedTargetShort, VPFRNearestHvnBelow, PlacedTargetLong, MicroCVDMomentum, OBVTrend, MicroCVDSignal, VerdictContext, VPFRNearestHvnAbove | ATRMultiplier (57.6 %), ADX (49.8 %), VWAPSigma1Upper (48.0 %), VWAPSigma2Upper (47.6 %), ATR (46.6 %), VWAP (43.9 %), RSI (43.0 %), TTMHistogram (42.6 %), MTF15mADX (41.3 %), VWAPSigma1Lower (40.7 %), VWAPSigma2Lower (39.3 %), VWAPDevPct (36.1 %), EffectiveLongScore, ShortScore, EffectiveShortScore, LongScore, AggrVelNet, VolumeRatio, MicroCVDLate, FundingMomentum (§7), PlusDI, MinusDI, MicroCVDMid, MicroCVDEarly, CVDValue, CVDWeightedSlope |

The frontier moved substantially rightward. ATR / ADX / EMA9 / Donchian / TTMSignal / Regime / TrendStructure5m — the entire structural + categorical + slow-EMA family — are now in the faithful or high-advisory band. The stragglers in the "do not use" column are either the pre-existing VWAP bug (see §9.5), trade-window-edge sensitivity (CVD/MicroCVD/AggrVel — proposal §1 already documented these as edge-sensitive), or numeric variants of columns whose categorical form matches (PlusDI/MinusDI numeric ≠ Regime categorical, which the categorical DOES faithfully reproduce).

### 9.7 Gate tail + commits

`tools/checks/verify-gate.ps1 -Mode prepush` (all 6 Release builds + harness incl. new A43f + display-parity + version-bump guards):

```
PASS  A43f forming-bar stub (§7.1) — OHLCV compaction · zero-trade fallback · stub-is-last-bar
PASS  A44a FloorToBucket collapses same-bar timestamps + advances one bucket at the boundary + execRes<=0 guard

ALL PASS
OK    harness ALL PASS

=== display-parity ===
OK    no snapshot/card drift detected

=== version-bump ===
OK    engine-path change accompanied by a settings.json version bump

=== result ===
GATE PASSED
```

Commits (local, unpushed — the trader's local-first workflow):

- **feat(backtest):** ReplayLoop §7.1 forming-bar stub — mirror live's convention across all four candle series (`[closeMs, closeMs + 2s]` trade window; zero-trade fallback; slice count = live's `(N-1) + 1 stub`); widened trade slice + aggr-vel feed cutoff to match live's poll-at-closeMs+2s state.
- **test(backtest):** A43f fixture — pins OHLCV compaction, zero-trade fallback, stub-is-last-bar, and TradesInStubWindow inclusion boundaries.
- **docs(backtest):** overlap re-validation §9 — before / after per-column table + muted-vote remeasurement + VWAP residual gap analysis.

---

## 10. Re-validation after §7.5 (VWAP session-anchor parameterization) — 2026-07-30

**Amendment:** `docs/backtest-synthesizer-proposal.md` §7.5 — `Core/Indicators_Volatility.vb::GetSessionCandles` gained `Optional nowUtc As DateTime? = Nothing`. `Nothing` ⇒ `DateTime.UtcNow`, so the live path is byte-identical to every prior version; `CalcVWAP` / `CalcVWAPBands` thread the parameter through; `ReplayLoop` passes `curUtc` (the bar close, already this loop's "now" — it drives the `IsFresh` gates). The ONE engine edit authorized for this spec. Fixture pin: **A45a**.

**Same window, same frozen live files, same historical store — only the anchor threading changed.** Regenerated synthetic CSV: `backtest_20260729_20260730_anchorfix.csv`. Report generated 2026-07-30 14:18:57 UTC.

### 10.1 The VWAP family — §9 → §10

| Column | §4 (pre-§7.1) | §9 (post-§7.1) | **§10 (anchor fix)** | Δ vs §9 | Mean \|Δ\| | Max \|Δ\| |
|---|---:|---:|---:|---:|---:|---:|
| **VWAPSessionCandles** | 50.24 % | 69.29 % | **100.00 %** | **+30.71 pp** | **0** | **0** |
| VWAP | 72.62 % | 43.93 % | **56.19 %** | **+12.26 pp** | 69.79 | 678.7 |
| VWAPSigma1Upper | 82.14 % | 47.98 % | **55.00 %** | **+7.02 pp** | 90.09 | 854.3 |
| VWAPSigma1Lower | 71.31 % | 40.71 % | **55.00 %** | **+14.29 pp** | 69.66 | 627.8 |
| VWAPSigma2Upper | 81.19 % | 47.62 % | **54.52 %** | **+6.90 pp** | 120.5 | 1065 |
| VWAPSigma2Lower | 70.71 % | 39.29 % | **53.45 %** | **+14.16 pp** | 87.25 | 826.6 |
| VWAPDevPct | 55.24 % | 36.07 % | **40.60 %** | **+4.53 pp** | 0.1122 | 1.06 |

**`VWAPSessionCandles` = 100.00 % at mean and max \|Δ\| of exactly 0** is the decisive read: the session *window* the synthesizer selects is now byte-identical to live's on all 840 joined rows. The §9.5 root cause is closed — the whole-list fallback branch no longer fires under replay, on any row.

**The §7.5 expectation ("recover toward the EMA-class levels") did NOT materialize for the VWAP *values*.** The family landed at 53–56 %, not 99 %. That is a residual to flag, not a fix to chase in this lane. What the numbers say plainly: with the window now provably exact, the remaining drift cannot be an anchor error — it is numeric edge sensitivity inside a correct window. The mechanism is visible in the same table: VWAP is a volume-weighted mean over as many as 240 bars, and the last of those bars is the §7.1 forming stub, whose volume is whatever printed in a 2-second slice. A near-zero-volume final bar shifts a long-window volume-weighted mean by a small amount that still exceeds the NumTight tolerance (`max(0.01, 0.01 % × |live|)` ≈ $6.4 at BTC 64 k). The sigma bands inherit it and `VWAPDevPct` amplifies it, which is why DevPct stays lowest of the seven. Whether that is worth another pass — and whether the tolerance class rather than the code is the thing that is mis-set for a 240-bar accumulator — is a spec-first question for the Fable/trader seat, not this lane.

### 10.2 OBV family — did NOT recover

| Column | §9 | §10 | Δ |
|---|---:|---:|---:|
| OBVTrend | 71.43 % | 71.43 % | 0.00 pp |
| OBVDivergence | 84.17 % | 84.17 % | 0.00 pp |

Unchanged to the row. §9.5's second regression cluster is independent of the VWAP anchor, exactly as its root-cause note said (`CalcOBV` normalises by `candles.Skip(1).Average(Volume)`, so the near-zero-volume stub pulls `meanVol` down and inflates normalised `obvChange`). Reported, not chased — per the §7.5 instruction.

### 10.3 Headline agreement — §9 → §10

| Metric | §9 (post-§7.1) | §10 (anchor fix) | Δ |
|---|---:|---:|---:|
| Verdict agreement | 623 / 840 = 74.17 % | 622 / 840 = 74.05 % | **−0.12 pp (one row)** |
| Tier agreement | 685 / 840 = 81.55 % | 684 / 840 = 81.43 % | **−0.12 pp (one row)** |
| ASIA verdict | 78.75 % | 78.75 % | 0.00 pp |
| LONDON verdict (n=20) | 80.00 % | 80.00 % | 0.00 pp |
| NY verdict | 72.88 % | 72.73 % | −0.15 pp (one row) |

A seven-column indicator-family recovery moved the verdict agreement by exactly one row, in the *negative* direction. Stated flatly, without interpretation: VWAP deviation is one vote of ~20 in a pipeline whose thresholds are integer ceilings, so a numeric improvement below the categorical threshold changes nothing, and single-row noise dominates at this n. No other column in the per-column table moved outside the VWAP cluster.

### 10.4 Clearance status — unchanged by this lane, and why

§7.5's conditional clearance said the synthesizer is cleared for geometry/placement-class studies now, and NOT cleared for VWAP- or funding-sensitive studies "until the anchor fix lands and re-validates." The fix landed and re-validated: the window is exact, the values are not (53–56 %, still the "do not use" band by §9.6's own < 60 % cut). Funding is untouched (`FundingMomentum` 22.02 %, identical — the anchor is not on the funding path).

**RULED 2026-07-31 (Fable seat) — PARTIAL CLEARANCE:**

- **Window-consuming studies: CLEARED.** Session membership / `VWAPSessionCandles` are exact on every row.
- **VWAP *values*: CLEARED for coarse/structural use**, against a documented noise floor.
- **Fine VWAP-dev threshold sweeps (steps < 5 bps): WITHHELD** pending the D2 accumulator-tolerance reclass.
- **Funding: unchanged** — structurally approximate, discouraged.

⚠ **SUPERSEDED 2026-07-31 — the volume difference was a SYNTHESIZER BUG, now fixed, and VWAP agrees 100.00 %.** `BuildFormingStub` summed `TradeRecord.Amount` (USD notional) into `Candle.Volume` (BTC) — a ~64,000× unit error found by running the D3 A/B (`ae8a1f6`; fixture A47b). Post-fix on the same 840 rows: **VWAP 56.19 % → 100.00 %**, sigma bands → 99.76–100.00 %, VolumeRatio 23.57 % → 65.00 %, OBVTrend 71.43 % → **99.76 %**, verdict agreement 74.05 % → **79.64 %**, tier 81.43 % → **86.19 %**. ATR/ADX/RSI unchanged. **There was no tolerance question at any point.** Detail: `d3-closed-bar-volume-ab-2026-07-31.md` §2. The withholding of fine dev-threshold sweeps no longer has evidence behind it — re-ruling is the Fable seat's call.

*(Retained below for the audit trail — the localisation to volume was correct; only the cause was unknown at the time.)*

⚠ **RESOLVED 2026-07-30 by measurement — the residual is a VOLUME-INPUT difference, not a tolerance.** Highs and lows are byte-identical (Donchian 100.00 %, max |Δ| 0) and closes agree to 99.5 %+ (EMA family), so Volume is the only VWAP input that can move it. Splitting the 840 buckets by whether the row's own volume agrees: **volume agrees ⇒ VWAP matches 100.0 % (199/199) at median |Δ| $0.00**; volume differs ⇒ 42.6 %. There is no residual for a tolerance to explain, and the ordered bps-scale reclass should **not** be specced — it would widen a bar around a real difference. Evidence: `pre-aug1-batch-spec-back.md` §7.2b.

**Consequence for the fine-sweep condition:** it is not gated on a tolerance task, it is gated on **volume fidelity** — the same defect as the D3 forming-bar lane, seen from the synthesizer side. **The window and coarse/structural clearances above are unaffected** — they rest on the window being exact, which is not in dispute.

### 10.5 Gate tail + commit

`tools/checks/verify-gate.ps1 -Mode prepush` — all 6 Release builds, harness incl. new **A45a**, A1–A44a unregressed:

```
PASS  A44a FloorToBucket collapses same-bar timestamps + advances one bucket at the boundary + execRes<=0 guard
PASS  A45a VWAP session anchor (§7.5) — default ≡ UtcNow (non-trivial) · post/pre-cutoff historical anchor · §8.6 wall-clock fallback

ALL PASS
OK    harness ALL PASS

=== display-parity ===
OK    no snapshot/card drift detected

=== version-bump ===
OK    engine path changed but [no-engine-change] token present

=== result ===
GATE PASSED
```

Settings untouched (stays v63) — no config keys added, live behaviour byte-identical at the default `Nothing`, hence the `[no-engine-change]` token (the `5dc9646` precedent).

**Out-of-scope finding, recorded not fixed** (batch §standing-constraints: stop the lane, record, move on): `tools/BacktestRunner/BacktestProgram.vb` leaks an unformatted `String.Format` placeholder to the console — `[BacktestRunner] Replay {0:yyyy-MM-dd HH:mm} → {1:yyyy-MM-dd HH:mm} UTC into <file>`. Cosmetic console logging only; the correctly-formatted line prints immediately after it. Not in lane A's one-parameter scope.

