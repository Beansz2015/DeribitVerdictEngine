# Spec: Offline Analysis Script — CSV-to-Forward-Returns Diagnostic
**Proposed:** 2026-05-05
**Status:** PROPOSED 2026-05-05
**Target files:** new — `analysis/AnalysisRunner.vb`, `analysis/ForwardReturnJoiner.vb`, `analysis/FailureRateMatrix.vb`, `analysis/FundingMomentumDiagnostic.vb`, `analysis/OutlierAudit.vb`, `analysis/MarkdownReportWriter.vb`; `UI/MainForm_Render_Header.vb` (new link)
**Prerequisites:** `csv-expansion-v0.4-proposal.md` shipped; ≥300 v0.4 rows accumulated
**Unblocks:** `failure-definition-proposal.md`, `b1-per-indicator-regime-weights-proposal.md`

---

## 1. Background

The auto-tweaker (`auto-tweaker-pipeline-proposal.md`) needs a failure-definition that's empirically calibrated, not guessed. The failure definition needs forward-return distributions per verdict tier × hold window × ATR threshold.

The same data also answers several open questions surfaced in the 2460-row CalibrationReport audit:

- **FundingMomentum 100% FLAT** — is the v22 1bp threshold still too high, or does 60s polling genuinely miss everything? (B3 raw funding delta column in v0.4 unblocks this.)
- **OFI Ratio max 4950.64** — outlier or persistent? (Need distribution analysis.)
- **OI×CVD 24:1 long:short asymmetry** — regime-period bias or asymmetric algorithm? (Join against `Regime` column.)

This script produces a markdown report + summary CSV the user can read in-app and the auto-tweaker consumes programmatically.

---

## 2. Specification

### 2a. Project structure

New top-level folder: **`analysis/`**.

```
analysis/
├── AnalysisRunner.vb              Public entry point. Static class, host-agnostic.
│                                  Method: Run(csvPath, outputDir, cfg) → AnalysisReport
├── ForwardReturnJoiner.vb         Loads CSV, computes T+5/T+10/T+15 forward returns
│                                  per row by joining row N with rows N+5/N+10/N+15.
│                                  Skips rows where forward window crosses a session boundary
│                                  (per cfg.SessionVolume.Sessions[]).
├── FailureRateMatrix.vb           For each verdict tier (STRONG_LONG, MEDIUM_LONG, etc.)
│                                  × each window (5/10/15 min) × each ATR threshold
│                                  (-0.3, -0.5, -0.8 × ATR), compute failure rate +
│                                  sample size + 95% binomial CI.
│                                  Excludes NO_TRADE and WEAK rows from rate calc;
│                                  tracks them in informational counters.
├── FundingMomentumDiagnostic.vb   Reads FundingDelta column (v0.4). Computes:
│                                  - Distribution of |FundingDelta| in bps buckets
│                                  - Empirical 50/75/90/95th percentile of |Delta|
│                                  - Implied threshold to achieve target firing rate
│                                    (e.g., what threshold gives 30% non-FLAT?)
│                                  Output answers: should v23 lower threshold further?
├── OutlierAudit.vb                Two passes:
│                                  - OFI Ratio: count rows where OFIRatio > 100,
│                                    show worst 10 by Timestamp + ratio + raw OFIBidVol
│                                    + OFIAskVol. Identifies if outliers are isolated
│                                    or recurring.
│                                  - OI×CVD asymmetry: confirmed_long vs confirmed_short
│                                    counts broken down by Regime column. If asymmetry
│                                    survives regime stratification, it's algorithmic;
│                                    if it disappears, it's regime-period bias.
├── MarkdownReportWriter.vb        Renders AnalysisReport → markdown file.
└── AnalysisReport.vb              POCO container for all analysis sections.
                                   No I/O. Pure data class.
```

**Portability constraint (Q4):** all classes in `analysis/` are **host-agnostic**. No references to `MainForm`, `Control.Invoke`, `System.Windows.Forms.*`. Future Linux CLI port reuses these directly.

### 2b. Inputs

`AnalysisRunner.Run(csvPath As String, outputDir As String, cfg As EngineSettings) As AnalysisReport`

- `csvPath`: path to `analysis_log.csv` (v0.4 schema)
- `outputDir`: where the markdown + summary CSV are written
- `cfg`: passed in for `cfg.SessionVolume.Sessions` (session bucket boundaries)

### 2c. Output files

Written to `outputDir/`:

- `analysis_report_<yyyyMMdd_HHmmss>.md` — full markdown report
- `analysis_summary_<yyyyMMdd_HHmmss>.csv` — failure-rate matrix as flat CSV (columns: VerdictTier, WindowMin, AtrThreshold, FailureRate, SampleSize, CiLow, CiHigh)

The auto-tweaker consumes the summary CSV programmatically; the user reads the markdown.

### 2d. Markdown report sections

```
# Analysis Report — <timestamp>
## 1. Summary
   - Rows in CSV / rows excluded (forward window incomplete or session boundary)
   - Verdict tier counts
   - Headline failure rates (best window×threshold per tier)

## 2. Failure-Rate Matrix
   Tabular: rows = verdict tiers, columns = (5m,10m,15m) × (-0.3,-0.5,-0.8 ATR)
   Each cell shows "rate% (n=sample) [ci_low - ci_high]"
   Highlighted: the most stable cell per tier (lowest CI width with sufficient n).

## 3. Recommended (window, threshold) per tier
   Auto-pick logic: lowest CI width subject to n ≥ 30. Reported as the candidate
   the auto-tweaker should use. User reads this to know which trading windows
   the engine is empirically most reliable on.

## 4. Verdict Context Tag × Outcome
   For each VerdictContext (CONFIRMED, FLOW_UNCONFIRMED, MOMENTUM_FADING,
   STRUCTURALLY_WEAK), the failure rate at the recommended cell.
   Validates the v0.3 context-tag premise.

## 5. Funding Momentum Diagnostic
   - Empirical FundingDelta distribution
   - Percentile table
   - Recommended v23 funding_momentum_threshold (or "no change — ceiling is polling cadence")

## 6. OFI Outlier Audit
   - Count of OFIRatio > 100 rows, > 1000 rows
   - Top 10 outliers by ratio with timestamps
   - Recommendation: cap or persistent-bug investigation

## 7. OI×CVD Asymmetry Audit
   - Confirmed long/short counts overall
   - Same broken down by Regime column
   - Same broken down by Funding Bias
   - Verdict: regime-period bias / asymmetric algorithm / inconclusive

## 8. Hold Window Selection Stats (for trader use)
   Per verdict tier, percentage of runs where the recommended window was
   5m / 10m / 15m. Lets the trader know "STRONG LONG verdicts are most
   reliable when held 10m" etc. — directly answers Q9b in the spec.

## 9. Pending data
   Tier × window cells where n < 30 (insufficient sample). User waits for
   more rows before treating recommendation as stable.
```

### 2e. Reachable from MainForm

New link in MainForm next to existing `lnkCalibCheck`: `lnkAnalysisReport`. Click handler:

1. Confirms CSV is at v0.4
2. Calls `AnalysisRunner.Run(csvPath, outputDir, cfg)`
3. Reads back the just-generated markdown
4. Renders it in a new non-modal `AnalysisReportForm` (rich-text view, scrollable, monospaced for tables)
5. Footer of the form shows the markdown file path so user can open it externally too

`AnalysisReportForm` is the only file in `analysis/` that depends on Windows.Forms — kept separate so the rest of the folder stays portable. The form is essentially a thin viewer; all logic is in the host-agnostic classes.

### 2f. Settings additions

None. Hold windows (5/10/15 min) and ATR thresholds (-0.3/-0.5/-0.8) are constants in `FailureRateMatrix.vb`. If we later want them configurable, that's a v2.

The auto-tweaker uses the same constants — they're shared via a single `AnalysisConstants.vb` so the auto-tweaker reads what the report computed against.

---

## 3. Implementation Notes

- ForwardReturnJoiner must reject row N if rows N+W don't exist or cross a session boundary. Session boundary check uses `cfg.SessionVolume.Sessions[]` UTC bucket lookup.
- Failure rate per tier × window × threshold uses simple Wilson score 95% CI (`zsq = 3.8416`). No external stats library.
- Report uses fixed-width pipe tables for the matrix; renders cleanly in monospaced RTF view.
- Outlier audit "top 10 by ratio" is a simple sorted-list-take-10. No need for percentile estimators on this small sample.
- Generation should complete in <2s on 5000 rows. No async needed; synchronous click handler is fine.

---

## 4. Out of Scope

- Per-indicator hit-rate analysis — that's `b1-per-indicator-regime-weights-proposal.md`, blocked on this output but separate spec
- Tweak proposals or settings diff generation — that's `auto-tweaker-pipeline-proposal.md`
- Re-fetching historical price from Deribit — sequential CSV rows give us forward price for free
- Live updates — reports are generated on demand, not auto-refreshing

---

## 5. Acceptance

- `analysis/` folder exists, all classes Form-agnostic except `AnalysisReportForm`
- `lnkAnalysisReport` link in MainForm opens the viewer
- Markdown + summary CSV both written on click
- Report renders correctly with sample CSV (post-300-row v0.4 accumulation)
- Auto-tweaker (later spec) consumes summary CSV without further parsing tools
