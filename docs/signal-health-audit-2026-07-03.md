# Post-WS Signal-Health Audit — Evidence Report

**Date:** 2026-07-03 (Fable seat). **Status:** COMPLETE — evidence base for `signal-health-retune-proposal.md` (APPROVED 2026-07-03, D1–D6 all as recommended; ships at the #5 boundary).
**Scope:** roadmap W1 signal-health audit — per-signal fire rates, pairwise agreement, conditional barrier outcomes for OFI/TFI/CVD/MicroCVD + FundingMomentum/RSI-div/OISignal/spread, on the post-v42 (WebSocket) book. Absorbs the §12 threshold-sweep rows per the 2026-07-02 roadmap note.

---

## 1. Data & method

**Book:** `analysis_log.csv` (v0.7 schema), 8,025 rows, 2026-06-17 14:45 → 2026-07-02 16:47 UTC. All timestamps UTC.
**Outcomes:** `analysis_eval_cache.csv` (v4), joined on second-truncated timestamp. Outcome vocabulary: SUCCESS (favourable barrier first), ADVERSE_HIT (stop barrier first), WINDOW_EXPIRED (neither, at T+15 for 1-min / T+45 for 3-min), plus EXCLUDED_* / PENDING (dropped).

**Era slices (UTC):**
| Era | Range | n | Notes |
|---|---|---|---|
| REST | 06-17 14:45 → 06-24 00:00 | 5,275 | pre-cutover, 30 s interval cadence |
| (excluded) | 06-24 00:00 → 15:20 | ~373 | cutover-day ambiguity — dropped from era comparisons |
| WS | 06-24 15:20 → 07-02 16:47 | 2,377 | live on WS; mixed 30/60 s + on-close cadence |

WS-era population mix: NY×1 = 2,071, LONDON×3 = 219, ASIA×3 = 87. **The WS book is NY-dominated; London/Asia numbers are thin and day-skewed** (06-25/06-29 fragments + 07-01/07-02).

**OFI construction sub-eras** (OFI columns only): snapshot-on-WS (→06-30 13:00), arithmetic averaging (→07-01 07:02), geometric (→07-02 16:35), v48 pair (→end, n=13 only).

**Statistical caveats (apply to every table).** Rows are autocorrelated (30–60 s cadence; overlapping 15/45-min eval windows) — effective sample is roughly 5–10× smaller than nominal n. All conditional-outcome tables condition on *fired* verdicts (post-threshold survivorship): a signal can look outcome-null here yet still carry ensemble value, and bonus/penalty arms suffer opposite composition biases (a bonus pushes marginal setups over threshold, diluting its cell; a penalty's survivors are enriched for everything-else-strong). Gradients are treated as directional evidence, not proof; recommendations lean on mechanism + fire-rate structure, with outcomes as corroboration.

---

## 2. Headline findings

| # | Finding | Evidence | Disposition |
|---|---|---|---|
| F1 | **OFIMomentum modifier is always-on and outcome-null-to-inverted** | active 89–94% in every era/population; CONFIRMED arm 53.1% SUCC (n=213) vs SUPPRESSED-fired-anyway 72.5% (n=40) vs FLAT 56.7% (n=30) | **Retire** (R1) |
| F2 | **Funding-momentum semantics silently collapsed at the WS cutover** | `_fundingHistory` appends on-change; REST-sticky funding made 3 changes ≈ hours, WS funding_8h changes on 96.5% of runs → window ≈ 3 min; Step-3b moves scores on 36.8% of WS runs vs 16.0% REST | **Re-derive threshold** (R2) |
| F3 | Funding-momentum direction is still informative | 3b-moved runs 45.3% SUCC vs 56.6% inert (n=236/475) — flagged runs are genuinely worse (regime confound possible) | supports R2 (retune, don't retire) |
| F4 | **Spread-revival premise is refuted** | WS-era p50–p95 all 0.08–0.09 bps (one-tick book); p99 0.59; max 6.4; >5 bps on 0.1% of runs (2 rows). REST era identical shape | **No retune; reject A1** (R3) |
| F5 | **RSI-div penalty validated** | verdicts fired through the penalty: 38.6% SUCC (n=57) vs 55.2% when no divergence (n=621) | Keep (R4) |
| F6 | Pass 2b OI×CVD validated | CONFLICT 38.9% (n=18) vs CONFIRMED 55.6% (n=27); engages on 2.9% of runs | Keep (R5) |
| F7 | CVD slope + MicroCVD accel show no conditional gradient | CVD aligned 49.3% vs against 51.3% vs no-vote 56.5%; MicroCVD aligned 49.0% vs against 50.5% | Keep, re-audit next cycle (R6) |
| F8 | Flow stack is independent, not redundant | OFI×CVD agree 50.2% when co-active (n=775); CVD×MicroCVD 69.3%; no pair >70% | no redundancy retirement warranted |
| F9 | **LiqSignal has never fired — plumbing correct, signal unproven-live** | 8,025/8,025 NONE; LiqLong/ShortSize = 0 on every row; REST+WS parsers both map the `liquidation` field correctly; live tape probe inconclusive (field is optional-on-liq-trades) | Validate via #7; **gate A4 on validation** (R8) |
| F10 | OISignal 91.9% NEUTRAL persists (v34 WATCHING) | when full-aligned with verdict: 60.0% SUCC (n=50) | Keep as-is (R7) |
| F11 | TFI is not logged at all | no TFI column in CSV v0.7 — the 4th flow angle is audit-blind | schema fix rides #5 CSV bump |
| F12 | FundingRate logged at F6 (quantum 1e-6 = 20× threshold) | direct re-derivation from the rate column impossible; FundingDelta (F8) used instead | precision fix, any commit |
| F13 | LONDON×3 adverse anomaly | 24.1% ADVERSE_HIT (14/58) vs NY 0.3% — all 14 in 4 reversal clusters (06-25 12:2x-12:4x, 06-29 09:00, 07-01 08:3x, 07-01 12:4x-12:5x); SUCC 34.5% vs NY 54.9% | feeds (B)/§12 watch; no action, data-gated |
| F14 | NY stop barrier almost never binds | ADV 0.3% of evaluated NY rows; losses are window-expiry | reinforces W1 reach-target calibration (D7) |
| F15 | v36 rider: CVD divergence near-dead on 3-min | 1.3% fire (res=3) vs 10.3% (res=1); RSI-div is NOT over-eager on 3-min (9.8% vs 10.2%); TTM FLAT ≈0% both | route to Phase-2 carry-forward re-measure |

---

## 3. Fire rates — WS era by population

```
--- NY (n=2071, res=1) ---
OFI level: BUY 43.4% SELL 16.4% BAL 40.2%   OFIMom: RISE 45.2% FALL 45.8% FLAT 8.9%
CVD vote: L 28.4% S 27.7% none 43.9%   CVDDiv: 10.3%   MicroCVD: ACC-L 14.0% ACC-S 13.4% DECEL 36.4% FLAT 36.3%
RSIDiv: any 10.2% penaltyArm 3.9%   OI non-NEUTRAL: 13.3%   FundMom: RISE 19.5% FALL 34.2% FLAT 46.3%
TTM: BUILD-L 34.4% BUILD-S 30.3%   OBV vote: L 21.8% S 48.0%   Spread>5bps 0.1%

--- LONDON (n=219, res=3) ---
OFI level: BUY 33.3% SELL 23.7% BAL 42.9%   OFIMom: RISE 44.3% FALL 44.7% FLAT 11.0%
CVD vote: L 26.5% S 25.6% none 47.9%   CVDDiv: 1.4%   MicroCVD: ACC-L 14.2% ACC-S 13.2% DECEL 37.9% FLAT 34.7%
RSIDiv: any 9.1% penaltyArm 5.5%   OI non-NEUTRAL: 15.5%   FundMom: RISE 73.5% FALL 4.1% FLAT 22.4%
TTM: BUILD-L 36.1% BUILD-S 27.4%   OBV vote: L 49.3% S 0.9%   Spread>5bps 0.0%

--- ASIA (n=87, res=3) ---
OFI level: BUY 21.8% SELL 31.0% BAL 47.1%   OFIMom: RISE 43.7% FALL 34.5% FLAT 21.8%
CVD vote: L 32.2% S 18.4% none 49.4%   CVDDiv: 1.1%   MicroCVD: ACC-L 17.2% ACC-S 14.9% DECEL 34.5% FLAT 33.3%
RSIDiv: any 11.5% penaltyArm 9.2%   OI non-NEUTRAL: 34.5%   FundMom: RISE 12.6% FALL 37.9% FLAT 49.4%
TTM: BUILD-L 16.1% BUILD-S 34.5%   OBV vote: L 51.7% S 0.0%   Spread>5bps 0.0%
```

LONDON FundMom RISE 73.5% and the LONDON/ASIA OBV one-sidedness are day-composition artifacts of the thin 3-min book (2–3 session days), not structure.

## 4. OFI by construction era

```
pre    ALL  n=5648  BUY=37.9% SELL=29.4% DOM=67.3%  MomActive=91.0%
wsnap  ALL  n= 694  BUY=36.5% SELL=28.7% DOM=65.1%  MomActive=89.9%
arith  ALL  n= 675  BUY=63.3% SELL= 5.3% DOM=68.6%  MomActive=93.2%   <- the known arith 12:1 buy-skew
geo    ALL  n= 995  BUY=30.9% SELL=18.1% DOM=48.9%  MomActive=88.8%   <- matches v48 spec 49.0% under-fire
v48    ALL  n=  13  BUY=30.8% SELL=30.8% DOM=61.5%                    <- 12 min of data; watch on track, nothing to conclude
```

The era table independently re-confirms both the geometric-construction decision (arith skew visible in-book) and the v48 re-baseline (geo under-fire at the stale pair). MomActive ≈ 90% is invariant to construction, session, and era — the OFIMomentum threshold (0.15) cannot be made meaningful by retuning: the v48 spec-back fitted 0.136 ≈ current, i.e. the ratio-delta distribution is simply fat relative to any sensible threshold.

## 5. Conditional barrier outcomes — WS era, directional, evaluated (n=711)

Baseline: 376 SUCCESS / 16 ADVERSE_HIT / 319 WINDOW_EXPIRED; tier mix WEAK 447 / STD 222 / STRONG 42.
Tier baselines: STRONG 59.5% / STD 58.1% / WEAK 49.7% SUCC — the tier ladder orders correctly.

```
OFI level ALIGNED with verdict      n=283  SUCC=56.2%   |  AGAINST n=157  SUCC=52.2%  |  BALANCED n=271  SUCC=49.8%
  CONFIRMED arm (+1 fired)          n=213  SUCC=53.1%
  SUPPRESSED arm (fired anyway)     n= 40  SUCC=72.5%   <- inverted vs design intent
  NO-MOD arm (momentum FLAT)        n= 30  SUCC=56.7%
CVD vote ALIGNED                    n=223  SUCC=49.3%   |  AGAINST n=189  SUCC=51.3%  |  no vote n=299  SUCC=56.5%
CVD divergence adverse to verdict   n= 51  SUCC=49.0%
MicroCVD ACCEL aligned              n=102  SUCC=49.0%   |  against n=111  SUCC=50.5%
MicroCVD DECEL penalized verdict    n=115  SUCC=57.4%
RSI-div penalty hit verdict side    n= 57  SUCC=38.6%   |  RSI-div NONE n=621  SUCC=55.2%
FundingMomentum 3b moved score      n=236  SUCC=45.3%   |  3b inert n=475  SUCC=56.6%
OI full aligned                     n= 50  SUCC=60.0%
Pass2b CONFIRMED                    n= 27  SUCC=55.6%   |  CONFLICT n=18  SUCC=38.9%
SpreadBps>5 at fire                 n=  2  (no basis for inference)
```

Reading, with the survivorship caveat applied: the **penalty-class signals that flag runs which then underperform anyway** (RSI-div 38.6, Pass2b CONFLICT 38.9, funding-3b 45.3) are doing real filtering work — their flags identify genuinely worse populations even after the penalty. The **OFIMomentum arms point the wrong way** (its suppression marks *better* runs). CVD/MicroCVD vote cells are flat — consistent with either genuine null value or full absorption by the threshold; the audit cannot distinguish these on conditional-on-fired data, so no retirement is proposed for them.

## 6. Pairwise direction agreement — WS era, co-active runs

```
OFI x CVD       coactive=32.6%  agree=50.2%  (n=775)
OFI x MicroCVD  coactive=16.3%  agree=59.2%
OFI x TTM       coactive=38.5%  agree=61.6%
CVD x MicroCVD  coactive=14.6%  agree=69.3%
CVD x OBV       coactive=36.7%  agree=53.3%
OBV x TTM       coactive=44.8%  agree=52.3%
(TFI unmeasurable — not logged, F11)
```

No pair exceeds 70% agreement. The four-angle order-flow stack measures genuinely different things; the accuracy-ceiling redundancy concern (§13 note 2) is **not** supported on this book — retirement decisions rest on individual value, not correlation.

## 7. Funding-momentum re-derivation (for R2)

Window-delta reconstruction from the FundingDelta (F8) change-step stream, validated at **95.1% agreement** with logged states at the current threshold (n=1,934 WS rows; day-boundary + >30-min-gap ring resets).

WS-era |window-delta|: p25=2.0e-8, p50=8.0e-8, p75=2.5e-7, p90=5.5e-7, p95=9.4e-7.

| momentum_threshold | ACTIVE% | FLAT% | Step-3b moves score |
|---|---|---|---|
| 5e-8 (current) | 59.5 | 40.5 | 39.9% |
| 1e-7 | 44.7 | 55.3 | 29.0% |
| 1.5e-7 | 37.3 | 62.7 | 23.3% |
| **2e-7 (recommended)** | **32.4** | **67.6** | **18.8%** |
| 3e-7 | 18.1 | 81.9 | 14.3% |
| 5e-7 | 11.4 | 88.6 | 9.3% |

2e-7 restores the REST-era adjunct engagement profile (16.0%) on the WS distribution. Residual defect either way: the 3-change window is cadence-dependent (30 s interval vs 60 s on-close vs bar-close spacing changes its wall-clock span). The clean fix is a time-anchored window (compare vs the sample ≥N minutes back) — small code spec, deferred (proposal §7).

## 8. Coverage gaps found by the audit

1. **TFI absent from the CSV** (F11) — add `TFIValue`,`TFISignal` columns at the next schema rotation (the #5 aggressor-velocity build bumps v0.7→v0.8 anyway; fold in there — one rotation, not two).
2. **FundingRate at F6** (F12) — bump to F8 in `AnalysisLogger` (same column, no header change, no rotation; any commit).
3. **Liquidation feed unproven-live** (F9) — when #7 (liq cascade alarm) is specced, include a first-liq-seen diagnostic (log line / status count) so one real cascade settles the question. Until then A4 (liq×OFI flip) has an unvalidated input.
4. `SpreadStatus` not logged — recomputable from SpreadBps; no action.

## 9. Items this audit re-homes or discharges

On approval of the retune proposal, these §12 rows get their data-driven verdicts: *Funding momentum thresholds* (→R2), *Bid-ask spread threshold* (→R3, re-homed row), *OI×CVD gate tuning* (→R5 keep), *TFI threshold* (blocked by F11 until logged). The *v48 OFI per-session fire-rate watch* stays open (13 post-flip rows — nothing to evaluate yet). The *v36 Phase-2 carry-forward* row gains the F15 measurement (CVD-div under-fires on 3-min — re-measure, don't blind-scale ×2.1).

**Reproduction:** aggregation + derivation scripts preserved in the session scratchpad (`audit.ps1`, `funding-derive2.ps1`); method summary in this doc is sufficient to re-run — the W1 audit is designed to be re-run each cycle (per-population OFI dominance rates in §4 double as the v48 §4a watch instrument).
