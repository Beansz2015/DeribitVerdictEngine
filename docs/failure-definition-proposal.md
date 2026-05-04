# Spec: Failure Definition for Auto-Tweaker
**Proposed:** 2026-05-05
**Status:** PROPOSED 2026-05-05
**Target files:** `analysis/FailureRateMatrix.vb` (already specced in Bundle 1), `analysis/AnalysisConstants.vb`, `tools/AutoTweaker/*` (consumes this)
**Prerequisites:** `csv-expansion-v0.4-proposal.md` shipped; `analysis-script-proposal.md` shipped
**Unblocks:** `auto-tweaker-pipeline-proposal.md`

---

## 1. Background

The auto-tweaker decides whether settings need adjustment by computing a failure rate over a window of past verdicts and comparing it to a threshold. Both the failure rate and the threshold need a precise definition of "failure."

Per Q10 / Q11 / Q12, failure is **ATR-based forward-return adverse to verdict direction**, evaluated at multiple windows and ATR thresholds, with the auto-tweaker choosing the most stable cell.

---

## 2. Specification

### 2a. Definitions

For row N with verdict tier T at timestamp `t[N]` and price `Price[N]`:

- **Forward return at window W (minutes):** `fr_W = (Price[N+W] - Price[N]) / Price[N]`
- Verdict direction: `LONG` for `STRONG_LONG` / `MEDIUM_LONG`; `SHORT` for `STRONG_SHORT` / `MEDIUM_SHORT`; `NONE` for `NO_TRADE` / `WEAK_*`
- For LONG: failure ≡ `fr_W * Price[N] < -threshold * ATR[N]`
- For SHORT: failure ≡ `fr_W * Price[N] > +threshold * ATR[N]`
- ATR thresholds evaluated: `0.3, 0.5, 0.8`
- Hold windows evaluated: `5, 10, 15` minutes (= 5, 10, 15 rows at 60s cadence)

This produces a 2 × 3 × 3 = 18 cells per LONG/SHORT direction, but evaluation is per verdict tier × window × threshold, not direction-stratified internally — direction is implicit in the verdict.

Note: `NO_TRADE` and `WEAK_*` rows are excluded from the failure-rate denominator. They're tracked in informational counters only (Q12).

### 2b. Tier-specific thresholds

Per Q11, STRONG is held to a tighter standard:

| Tier | ATR thresholds evaluated |
|---|---|
| `STRONG_LONG` / `STRONG_SHORT` | 0.3, 0.5 (tighter — lower threshold = less adverse move counts as failure) |
| `MEDIUM_LONG` / `MEDIUM_SHORT` | 0.5, 0.8 (looser) |

Reason: a STRONG verdict promises high conviction. Half-an-ATR drift against it is failure. A MEDIUM verdict allows more wobble before counting as failure.

The matrix per tier:

```
STRONG: 3 windows × 2 thresholds = 6 cells
MEDIUM: 3 windows × 2 thresholds = 6 cells
```

### 2c. Sample exclusions

A row is excluded from the failure-rate calc if any of the following:

1. Forward window incomplete — fewer than W subsequent rows exist in the CSV
2. Forward window crosses a UTC session boundary (per Q14 session rule, applied here as well — adverse moves driven by session handover are not the verdict's fault)
3. Verdict is `NO_TRADE` or `WEAK_*` — counted in informational column only

The forward-return joiner in `analysis-script-proposal.md` already implements (1) and (2). (3) is a tier filter applied per cell.

### 2d. Cell stability metric

For each cell, compute:

- `n` — sample size
- `failures` — count of failure outcomes
- `rate = failures / n`
- 95% Wilson score CI: `(rate_low, rate_high)`
- `ci_width = rate_high - rate_low`

The "most stable" cell per tier is the one with the **smallest CI width subject to n ≥ 30**.

Ties broken by:
1. Larger sample size
2. If still tied, prefer the smaller window (more recent data, faster auto-tweaker reaction)

### 2e. Picked-cell statistics (for trader display)

Per Q9b, the auto-tweaker reports across runs how often each cell was picked. Stored in `analysis/picked_cell_history.csv` (gitignored), with columns: Timestamp, Tier, Window, Threshold.

Read by the offline analysis report's "Hold Window Selection Stats" section. Tells the trader: "STRONG_LONG verdicts are most reliably held for 10 minutes — 73% of recent auto-tweaker runs picked the (10m, -0.3 ATR) cell."

### 2f. Constants and configuration

These constants live in a single shared file `analysis/AnalysisConstants.vb` so the offline analysis report and the auto-tweaker compute against the same definition.

```
Public Const StrongAtrThresholds As Double() = {0.3, 0.5}
Public Const MediumAtrThresholds As Double() = {0.5, 0.8}
Public Const HoldWindowsMinutes As Integer() = {5, 10, 15}
Public Const MinSamplesPerCell As Integer = 30
Public Const MinSamplesForAutoTweakerTrigger As Integer = 60
```

`MinSamplesForAutoTweakerTrigger` (Q14-related): even within a session-aligned window of 120 verdicts, the auto-tweaker needs at least 60 of those verdicts to be tier-eligible (STRONG_* or MEDIUM_*) before it considers the failure rate trustworthy. Prevents auto-tweaks driven by edge cases where 119/120 verdicts were NO_TRADE.

### 2g. NO_TRADE / WEAK informational tracking

Per Q12, these tiers are excluded from failure-rate calc but tracked as:

- `NO_TRADE` — count of rows. Reported in markdown report. No effect on auto-tweaker.
- `WEAK_LONG` / `WEAK_SHORT` — count of rows AND optional shadow failure rate (computed against them as if they had been traded — purely for the trader's information, never an auto-tweaker input). Per trader-profile, weak verdicts aren't traded, so their failure rate isn't a tuning signal.

---

## 3. Open Design Notes

- **Single-tier collapse?** The v1 spec keeps STRONG and MEDIUM tier matrices separate. After 1000+ rows, if both matrices end up picking nearly identical (window, threshold) combinations, we can collapse to a single tier-agnostic matrix. Defer that decision; data tells us.
- **Direction asymmetry?** STRONG_LONG and STRONG_SHORT use the same threshold set in v1. If the data shows long verdicts and short verdicts have different forward-return distributions (likely in a structural bull/bear period), we might want direction-asymmetric thresholds. Out of scope for v1 — re-spec if observed.

---

## 4. Out of Scope

- Hold-status-based exit windows (use engine's actual exit guidance instead of fixed 5/10/15) — premature; would couple failure definition to the hold-status logic which itself isn't validated
- Volatility-regime-conditional thresholds (ATR-tier-aware) — hold off; see if simple ATR-multiplier already absorbs the volatility variation
- Profit/win-rate evaluation — auto-tweaker optimises against false-positive rate, not win rate. Trader-profile says "prefer no trade over weak signals" — winning trades are not the optimisation target

---

## 5. Acceptance

- `analysis/AnalysisConstants.vb` exists with the constants above
- `FailureRateMatrix.vb` consumes them; produces per-tier × window × threshold failure rate + Wilson CI + sample size
- Picked-cell history CSV writes one line per auto-tweaker run
- Offline analysis report shows the picked cell per tier and its stability
- Auto-tweaker (later spec) consumes the same matrix and the picked-cell heuristic
