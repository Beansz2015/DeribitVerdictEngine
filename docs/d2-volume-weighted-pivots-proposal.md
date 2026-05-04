# Spec: D2 — Volume-Weighted Pivot Ranking (Display-Only v1)
**Proposed:** 2026-05-05
**Status:** PROPOSED 2026-05-05
**Target files:** `Core/Indicators_Structure.vb`, `Core/IndicatorResults.vb`, `UI/MainForm_Render_Sections.vb`, `AnalysisLogger.vb`
**Builds on:** shipped 5m swing pivots
**Cap arbitration impact:** none in v1 (display-only). v2 promotion candidate logged.

---

## 1. Background

Current `CalcSwingPivots` returns the **most recent** confirmed swing high and swing low. In structural trading, not all pivots are equal — a swing high made on 3× normal volume is a stronger reference than a swing high on average volume. The trader-profile preference for structural targets suggests volume-weighted pivots are more reliable retest magnets.

V1 ships display-only: log the highest-volume pivot in lookback alongside the most-recent pivot. Observation period determines whether it earns cap-arbitration in v2.

---

## 2. Specification

### 2a. Computation

Extend `CalcSwingPivots` to also return:

```
Public BestPivotByVolume5m As Double         ' price of the highest-volume pivot in lookback
Public BestPivotVolumeRatio5m As Double      ' total volume across wing window / avg pivot volume
Public BestPivotIsHigh5m As Boolean          ' True if it's a high; False if a low
```

For each confirmed pivot found in the lookback:
1. Sum the volume of all bars in `[pivotIdx - pivotWing, pivotIdx + pivotWing]` — total volume across the wing window. Per Q21.
2. Track the pivot with the highest total wing-window volume.
3. Compute average wing-window volume across all pivots in lookback. `BestPivotVolumeRatio = best / average`.

If fewer than 2 confirmed pivots in lookback: return `0, 0, False`. CalibrationReport excludes these rows from distribution analysis.

### 2b. IndicatorResults additions

Already defined above. Logged to CSV columns 85–86 (reserved in `csv-expansion-v0.4-proposal.md`).

### 2c. Render section

Add a row in MARKET STRUCTURE section under the existing swing pivot rows:

```
Best Vol Pivot 5m: HIGH 102450.0  (vol×2.3 vs avg pivot)
```

Colour: dim cyan (informational, not actionable in v1).

### 2d. CSV columns

Already specified in `csv-expansion-v0.4-proposal.md` (columns 85–86). When this spec ships, populate them. Until then they log as `0,0`.

### 2e. CalibrationReport addition

New `BEST VOLUME PIVOT DISTRIBUTION` section:

- Average ratio: X.X
- 75th percentile ratio: X.X
- 90th percentile ratio: X.X
- Rows where best is also the most-recent: N (Y%)

The "best is also most-recent" stat is the key data: if it's high (e.g., >70%), volume-weighting adds little marginal info. If it's low (e.g., <40%), there's a meaningfully different pivot the trader would prefer to reference, and v2 cap promotion is warranted.

### 2f. No scoring impact

V1 is purely diagnostic. No score, no Pass change, no cap arbitration. Q22 explicitly chose (c) display-only.

---

## 3. v2 Cap Arbitration (Future — Not In This Spec)

Logged in `DeribitIndicatorProject.md` Section 16 as a parked observation:

> **Watch for:** in CalibrationReport `BEST VOLUME PIVOT DISTRIBUTION`, when the
> "best is also most-recent" rate falls below 50% AND there's separate signal
> that volume-weighted pivots correlate with subsequent target hit rate (auto-
> tweaker output), promote BestPivotByVolume to a 4th cap tier above swing
> (best-volume-swing > swing > HVN > POC). Same closest-wins rule retained.

Don't promote without both conditions.

---

## 4. Out of Scope

- 15m volume-weighted pivots — symmetric with D1, defer until 5m version proves valuable.
- Volume-at-pivot-bar-only ranking (Q21 alternative) — wing window chosen; revisit only if data shows it's noisy.
- Cap arbitration impact (Q22 (a) and (b)) — explicitly deferred to v2.

---

## 5. Acceptance

- Build clean.
- `CalcSwingPivots` output extended with three new fields.
- Render section displays the best volume pivot with ratio.
- CSV columns 85–86 populated.
- CalibrationReport renders BEST VOLUME PIVOT DISTRIBUTION.
- 30+ runs show "best is also most-recent" rate as a meaningful number (not 0%, not 100% — those would suggest the calc is broken).
