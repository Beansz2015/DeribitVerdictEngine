# Spec: CSV Expansion v0.4 — Foundation for Auto-Tweaker and Effectiveness Audits
**Proposed:** 2026-05-05
**Status:** PROPOSED 2026-05-05
**Target files:** `AnalysisLogger.vb`, `settings.json`, `UI/MainForm_Render_Header.vb` (CalibrationReport)
**Prerequisite for:** `analysis-script-proposal.md`, `failure-definition-proposal.md`, `auto-tweaker-pipeline-proposal.md`, `d2-volume-weighted-pivots-proposal.md`

---

## 1. Background

The v0.3 CSV expansion (2026-04-29) added three columns: `VerdictContext`, `FundingMomentum`, `OiCvdOutcome`. Several v17-shipped features were not logged at the time and are still missing from `analysis_log.csv`:

- `SpreadBps` (bid-ask-spread spec) — **blocks B5** spread WIDE penalty validation
- `OFIMomentum` (ofi-momentum spec) — can't validate amplification effect
- VPFR-v2 fields: `VPFRVAH`, `VPFRVAL`, `VPFRNearestHvnAbove`, `VPFRNearestHvnBelow` — blocks VPFR NumBuckets tuning, blocks VAH/VAL signal validation
- Swing pivot fields: `LastSwingHigh5m`, `LastSwingLow5m`, `LastSwingHigh15m`, `LastSwingLow15m`, `SwingTargetLong`, `SwingTargetShort`, `SwingStopLong`, `SwingStopShort` — blocks structural-target effectiveness audit
- `TargetCapReason` (Step 5b 3-tier winner label) — can't tell which tier (swing/HVN/POC) is doing the work
- `FundingDelta` (raw period-over-period funding rate change) — explicitly listed in B3, missed in v0.3

Plus one new field driven by `d2-volume-weighted-pivots-proposal.md`:

- `BestPivotByVolume5m`, `BestPivotVolumeRatio5m` — the highest-volume pivot in 5m lookback and its volume vs average ratio

Without these columns, neither the offline analysis script nor the auto-tweaker can audit / tune the affected features.

---

## 2. Specification

### 2a. CSV schema bump v0.3 → v0.4

Append the following columns to the end of the row, after `OiCvdOutcome` (column 68). New schema is **column 1–68 unchanged + columns 69–84 new**.

| # | Column | Type | Source |
|---|---|---|---|
| 69 | `SpreadBps` | Double (4dp) | `r.SpreadBps` |
| 70 | `OFIMomentum` | String (RISING/FALLING/FLAT) | `r.OFIMomentum` |
| 71 | `FundingDelta` | Double (8dp, can be negative) | `r.FundingRate − previousFundingRate` (0 on first row of session) |
| 72 | `VPFRVAH` | Double (2dp) | `r.VPFRVAH` |
| 73 | `VPFRVAL` | Double (2dp) | `r.VPFRVAL` |
| 74 | `VPFRNearestHvnAbove` | Double (2dp) | `r.VPFRNearestHvnAbove` |
| 75 | `VPFRNearestHvnBelow` | Double (2dp) | `r.VPFRNearestHvnBelow` |
| 76 | `LastSwingHigh5m` | Double (2dp) | `r.LastSwingHigh5m` |
| 77 | `LastSwingLow5m` | Double (2dp) | `r.LastSwingLow5m` |
| 78 | `LastSwingHigh15m` | Double (2dp) | `r.LastSwingHigh15m` |
| 79 | `LastSwingLow15m` | Double (2dp) | `r.LastSwingLow15m` |
| 80 | `SwingTargetLong` | Double (2dp) | `r.SwingTargetLong` |
| 81 | `SwingTargetShort` | Double (2dp) | `r.SwingTargetShort` |
| 82 | `SwingStopLong` | Double (2dp) | `r.SwingStopLong` |
| 83 | `SwingStopShort` | Double (2dp) | `r.SwingStopShort` |
| 84 | `TargetCapReason` | String | `v.TargetCapReason` (e.g., `swing`, `hvn`, `poc`, `none`) |
| 85 | `BestPivotByVolume5m` | Double (2dp) | `r.BestPivotByVolume5m` (added by `d2-volume-weighted-pivots-proposal.md`; v0.4 reserves the column even if d2 ships later) |
| 86 | `BestPivotVolumeRatio5m` | Double (2dp) | `r.BestPivotVolumeRatio5m` (same as above) |

If d2 ships after v0.4, columns 85–86 are written as `0` in the interim. Reserving them now avoids a v0.5 bump for a single-feature addition.

### 2b. Log file rotation

On engine startup, if the existing `analysis_log.csv` header reads anything other than the v0.4 header, rename it to `analysis_log.csv.v0.3.bak` (or append timestamp if the .bak already exists) and start a fresh file with the new header.

Rotation logic centralised in `AnalysisLogger.EnsureLogFile()`. No new setting required — the schema version is the source of truth.

### 2c. Settings.json bump

`settings.json` `version`: 22 → 23.

`modified_by`: `"csv-expansion-v0.4"`

`change_log` entry:

```
"v23 — csv-expansion-v0.4: schema bump from v0.3 to v0.4. Added 18 columns:
SpreadBps, OFIMomentum, FundingDelta, VPFRVAH, VPFRVAL, VPFRNearestHvnAbove,
VPFRNearestHvnBelow, LastSwingHigh5m, LastSwingLow5m, LastSwingHigh15m,
LastSwingLow15m, SwingTargetLong, SwingTargetShort, SwingStopLong,
SwingStopShort, TargetCapReason, BestPivotByVolume5m, BestPivotVolumeRatio5m.
Old log rotated to analysis_log.csv.v0.3.bak on first startup."
```

No other settings keys added or changed.

### 2d. CalibrationReport adjustment

`BuildCalibrationReport()` reads the CSV header to detect schema version. On v0.4, add three new distribution sections to the report:

- **SPREAD DISTRIBUTION** — count of rows in (≤ 2 bps), (2–5 bps), (5–10 bps), (>10 bps) buckets
- **OFI MOMENTUM DISTRIBUTION** — RISING / FALLING / FLAT counts (parallel to existing FUNDING MOMENTUM section)
- **TARGET CAP REASON DISTRIBUTION** — count by cap tier (swing / hvn / poc / none)

These are diagnostic only — they don't gate the READY verdict.

---

## 3. Implementation Notes

- `r.FundingDelta` is a new field on `IndicatorResults`. Computed in `MainForm_Analysis.RunAnalysisAsync()` immediately after `_fundingHistory.Add(fundingRate)`: `r.FundingDelta = If(_fundingHistory.Count >= 2, _fundingHistory(_fundingHistory.Count - 1) - _fundingHistory(_fundingHistory.Count - 2), 0)`.
- `v.TargetCapReason` already exists on `VerdictResult` (per `swing-pivot-proposal.md`). Just needs to be passed through to `LogRun()`.
- `r.BestPivotByVolume5m` and `r.BestPivotVolumeRatio5m` will be `0` until `d2-volume-weighted-pivots-proposal.md` ships. Logger writes `0,0` until then.
- Rotation: `If File.Exists(logPath) AndAlso ReadFirstLine(logPath) <> v04Header Then RenameWithTimestamp(logPath); WriteHeader(v04Header); End If`. Simple, idempotent.

---

## 4. Out of Scope

- Per-row entry/stop/target prices (already covered via swing fields above)
- Hold-status output column (deferred — not needed until auto-tweaker hold-window analysis surfaces a need)
- WebSocket-only fields (e.g., per-tick spread momentum) — gated by Section A

---

## 5. Acceptance

Build clean (0 warnings, 0 errors). On first launch after upgrade:
- Old `analysis_log.csv` renamed to `analysis_log.csv.v0.3.bak`
- New `analysis_log.csv` exists with the v0.4 header
- Subsequent runs append rows with all 86 columns populated
- CalibrationReport renders the three new distribution sections without errors

User runs calibration accumulation post-rotation. Once 300+ v0.4 rows accumulated, downstream specs (`analysis-script-proposal.md`, `failure-definition-proposal.md`, `auto-tweaker-pipeline-proposal.md`) unblock.
