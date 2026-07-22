# W6-4 Offline Ceiling Audit — Implementer Spec-back

**Date:** 2026-07-23 · **Implementer:** Fable (Opus, highest effort) · **Local-first, unpushed.**
**Source spec:** [`w6-4-ceiling-audit-method-proposal.md`](./w6-4-ceiling-audit-method-proposal.md) — K1–K6 all ticked 2026-07-23 with §2 scored-inputs-only clarification.

**Scope note:** Build only. The RUN waits for the ~early-Aug pooled-book data gate. The engine is unchanged; no `settings.json` bump; the tool never writes settings.

---

## 1. Inventory (what shipped)

| Path | Role | Line count (approx) |
|---|---|---:|
| `tools/CeilingAudit/CeilingAudit.vbproj` | Standalone .NET 8 console project, zero WinForms references. Links the shipped `FailureRateMatrix` / `ForwardWindowJoiner` / `ExecutionResolution` — no copies. | 34 |
| `tools/CeilingAudit/CsvFeatureBuilder.vb` | Pooled-CSV loader (skips repeated headers), v0.8 / weekday / directional / burst-cadence filtering, session-hour bucket partitioning, informational-vs-scored bundle split, placed-vs-placed label attachment via shipped resolvers. | 320 |
| `tools/CeilingAudit/FeatureMatrix.vb` | Train-only-fit schema (levels, mean/std/median, one-hot column list with dropped baseline levels), `Transform` for test rows, median-impute + `_MISSING` indicator for NaN numerics. | 210 |
| `tools/CeilingAudit/L2Logistic.vb` | Batch gradient descent, zero-init, diminishing learning rate, numerically stable sigmoid, per-epoch loss trace exposed for the monotonicity fixture. | 130 |
| `tools/CeilingAudit/AuditMetrics.vb` | Chronological train/test split (min-test-days), λ tuner via internal walk-forward WITHIN train, Mann-Whitney AUC, Brier, success@K, session-hour block bootstrap for ΔAUC CI. | 220 |
| `tools/CeilingAudit/AuditReport.vb` | Markdown writer: §1 load stats, §2 per-population summary, §3 per-population detail (metrics, tier counts, coefficient table sorted by \|coef\|, informational side-column), §4 overall verdict paragraph. §4 three-way verdict line rendered EXPLICITLY per population. | 180 |
| `tools/CeilingAudit/CeilingAuditProgram.vb` | Main entry, CLI args, OHLC fetch, per-population fit + evaluate + write. | 250 |
| `verify/ordercheck/OrderCheck.vbproj` | Added `L2Logistic.vb`, `CsvFeatureBuilder.vb`, `FeatureMatrix.vb`, `AuditMetrics.vb` as Compile Includes (the report writer + Program stay out — Program owns its own `Main`). | +6 lines |
| `verify/ordercheck/Program.vb` | Added `Imports OrderCheck.CeilingAudit` alias + A39a-A39e fixtures. | +250 lines |
| `tools/checks/verify-gate.ps1` | Added a `Build 'tools/CeilingAudit/CeilingAudit.vbproj'` step between the WhatIfRunner build and OrderCheck build (F10 lesson — every standalone tool joins the gate build set). | +5 lines |

**No engine files changed.** `Core/`, `analysis/`, `DynamicNorms.vb`, `settings.json` untouched.

---

## 2. Contract map — spec item → implementation

| Spec § | Requirement | Implementation |
|---|---|---|
| §1 population | v0.8 evaluable directional, weekday-only, burst `8706ebae` excluded, median inter-row gap <45s excluded, NO_DATA excluded, WEAK included, per-population NY×1 / LONDON×3 / ASIA×3 | `CsvFeatureBuilder.LoadAndBuild` filters (schema check via `hasPlacedSchema` + `HasPlaced` + `MaxScore>0`); `DayOfWeek` filter; burst-prefix set + per-instance median-inter-row-gap computed once at load; `PartitionIntoPopulations` uses `ExecutionResolution.MatchSessionBucket` — the engine's own bucket definition. WEAK explicitly allowed in filter loop with a distinct branch (`isWeak`). |
| §1 label | Placed-vs-placed SUCCESS at the population's tracker horizon (res-1→15m, res-3→45m — the BandLadder horizon) | `AttachLabels` calls the SHIPPED `ResolveFavourableBarrier` / `ResolveAdverseBarrier` + `WalkBars` in `AdverseBarrierMode.Placed`; horizon from `AnalysisConstants.HoldWindowsForResolution(row.ExecResolution).Max()`; SUCCESS → 1, else 0. Same eval semantics as the offline matrix / BandLadder / D4 / What-If — one label, one truth. |
| §2 scored features | Per-indicator signal states one-hot + named numerics + regime + session-hour | `FeatureBundle.ScoredCategoricals` populated for the 24 categorical CSV columns listed in §2's scoring input catalogue; `ScoredNumerics` for the 6 named numerics (ATR, VolumeRatio, ADX, VWAPDevPct, SpreadBps, OFIRatio); Regime + SessionHour added as separate categoricals in `FitSchema`. AggrVel joins the SCORED set only on populations whose bucket carries an explicit `burst_ratio_threshold` — per-population armed check via `ExecutionResolution.HasExplicitAggrVelBurstThreshold`, so the LONDON/ASIA populations correctly leave AggrVel out of X (matches the live scoring scope). |
| §2 amendment (2026-07-23 tick) | Display-only surfaces EXCLUDED; logged-but-unscored (Absorption*, un-armed AggrVel) in an informational SIDE-COLUMN that never enters §4 | `FeatureBundle.InfoCategoricals` / `InfoNumerics` carry Absorption* on every population + AggrVel on un-armed populations. `FeatureMatrix.FitSchema` reads ONLY `ScoredCategoricals` + `ScoredNumerics` — informational fields are provably absent (A39e pins this). VerdictContext / Kelly / exit-guard / TAPE fields never touch a bundle. `AuditReport` writes the informational side-column as univariate AUC per feature — REPORTED, never in the design matrix. |
| §3 walk-forward | Train early / test late; test block ≥1 full week spanning all sessions; λ chosen on internal walk-forward WITHIN train | `AuditMetrics.MakeChronologicalSplit` sorts by timestamp and walks back from the end until it finds the first row before `endUtc - minTestDays` (default 7). Reports `TestSpansSessions` = (span ≥ minTestDays AND ≥3 distinct hours). `TuneLambda` uses an internal 80/20 chronological split within TRAIN; test slice is never touched during selection. Grid: `{0.01, 0.1, 1.0, 10.0}` — a coarse geometric sweep (see deviation #2). |
| §3 uncertainty | Block bootstrap over session-hour blocks | `AuditMetrics.AssignBlocks` keys blocks by `(UTC date, UTC hour)`; `BootstrapDeltaAucCi` resamples INTACT blocks with replacement (all rows in a drawn block come along together), B=1000 by default, seeded `System.Random(42)` for determinism, 2.5%/97.5% percentiles for the CI. A39d pins the no-straddle contract. |
| §3 report | Brier + AUC + lift at the actionable operating point (STRONG+MEDIUM share) | `MetricResult` carries all three; K = STRONG+MEDIUM count in test (WEAK excluded from K — it's the pipeline's own tradeable count, per the spec). Baseline and challenger evaluated at the SAME K, so lift is like-for-like. |
| §4 decision rule | ΔAUC ±0.03; three-way verdict rendered explicitly | `AuditReport.VerdictFor` implements the three-way test literally; `AuditReport.Build` prints the verdict line (`ΔAUC = ... 95% CI [..., ...] → **CEILING DECLARED / B1 PRIZE MEASURED / INCONCLUSIVE**`) per population and again in an overall §4 paragraph for the decisive NY×1. Margin defaults to 0.03, CLI-overridable via `--margin`. |
| §5 tooling | Dep-free standalone console, joins verify-gate | `CeilingAudit.vbproj` is net8.0, zero WinForms, own project. `verify-gate.ps1` builds it between WhatIfRunner and OrderCheck. `Program.vb`'s `Main` returns 0/1 like the AutoTweaker / WhatIfRunner programs. |
| §5 fixtures | (i) logistic loss monotone + direction · (ii) label-shuffle AUC≈0.5 · (iii) chronology · (iv) blocks no straddle · (v) info extras absent from X | A39a–A39e, all PASS. See the fixture list in the harness run below. |

---

## 3. Deviations (every one with rationale)

**1. `AuditPopulationReport` / `AuditReportModel` naming.** `analysis/AnalysisReport.vb` already exports `PopulationReport` and `ReportModel` in the root namespace; both are linked into the standalone project (transitive dep of `MarkdownReportWriter`). Naming my types the same led to VB2's `List(Of PopulationReport) cannot be converted to List(Of PopulationReport)` ambiguity, so my POCOs became `AuditPopulationReport` / `AuditReportModel`. Content unchanged; the report doc still reads "population report" in prose.

**2. λ grid = {0.01, 0.1, 1.0, 10.0}.** Spec §3 says "regularization strength chosen on the train block only (small internal walk-forward)" without a specific grid. A four-point geometric sweep is a defensible default: logistic AUC curves are near-flat between neighbouring decades, and a wider grid without a Bayesian search is guessing. Trader-overridable if a run turns up a boundary hit — currently no CLI knob for it, easy to add.

**3. Absorption/AggrVel informational rendering = univariate AUC per feature.** Spec §2 amendment says "reported coefficients, never in the §4 decision delta." A coefficient in the CHALLENGER model would require entering the informational fields into a separate fit that has its own subtle statistical choices (multivariate vs univariate, penalty, imputation). The safer, less-arguable summary is a per-feature univariate AUC on the test slice — the same metric the challenger is scored on, one number per informational feature. If the trader wants a multivariate informational fit later, adding a second `L2Logistic.Fit` call over `InfoCategoricals` + `InfoNumerics` is one method call.

**4. Missing-value handling = train-median impute + paired `_MISSING` indicator.** Spec §2 doesn't prescribe imputation. Simple median + missing flag matches what the auto-tweaker prompt builder does implicitly and is the standard cheap-and-correct choice for logistic regression with mildly missing features (e.g. AggrVel numerics on REST-fallback rows). The `_MISSING` column lets the fit distinguish "value happens to equal the median" from "value was never logged" — the LinkedIn-tutorial imputation trap.

**5. Session-hour + regime one-hot with a dropped baseline level per categorical.** Standard dummy-coding — drops one level per categorical to avoid perfect multicollinearity with the intercept, which the L2 penalty can't paper over cleanly. Not called out in the spec but implicit in "hand-rolled L2 logistic on standardized features."

**6. `TargetCapReason` included as a scored one-hot.** Spec §2's list includes "structure" and the placed-cap reason IS the structural attribution the pipeline emitted for that row. Excluding it would drop information the pipeline itself scored on.

**7. `MTF15mTrend` / `MTF15mEMAAlignment` / `PriceVsEMA200` included.** Not listed by name in §2, but they are inputs the engine's MTF gate + regime alignment consume. Included as SCORED one-hots because they meet the spec's rule ("everything that votes or modifies").

**8. The tool skips a population when n<40 or `train<30 / test<20`.** No point running a logistic on 10 rows. The population still appears in the summary table with a `SkippedReason` cell; the decisive verdict paragraph handles the "NY×1 skipped" case explicitly. Cutoff numbers are conservative — a first ~4-week book at NY×1 with 25%/25% tier mix easily clears 40 total.

**9. Version-check WARN, not FAIL.** `settings.json` is at v59 today (build-time expected value baked in). Any settings bump between build and run WARNs on load ("expected 59, got N") and continues — the D6 precedent. The audit reads the settings for `MinTradeableMovePct` / `AtrTargetMultiplier` / session bucket names, all of which are stable surfaces; a version bump on unrelated blocks doesn't invalidate the run.

**10. No smoke run on the live book.** Spec explicitly says "DO NOT RUN the audit on the live book — the data gate is ~early Aug; build + fixtures only." The five fixtures A39a–A39e exercise the L2 fitter, loss monotonicity + direction recovery, label-shuffle AUC canary, chronological split, block-bootstrap boundary discipline, and informational-extras absence — the complete stats machinery. No synthetic-CSV smoke was added; the fixtures cover the same code paths with tighter oracles.

---

## 4. What the CLI does

```
CeilingAudit <csvPath> [--out <dir>] [--settings <settings.json>]
                       [--min-test-days N] [--margin 0.03]
                       [--bootstrap-b 1000] [--seed 42]
```

Writes `ceiling_audit_report_<utcStamp>.md` in `--out` (default `.`). Exit codes: 0 report written, 1 error (bad CSV, no rows survive filters, OHLC fetch failed).

The pooled local+AWS book is passed via `<csvPath>` — a single concatenated CSV; the loader tolerates repeated header lines and skips them (`RepeatedHeadersSkipped` reports the count).

---

## 5. Harness results (verbatim)

```
=== build DeribitVerdictEngine.sln ===
OK    build DeribitVerdictEngine.sln
=== build tools/AutoTweaker/AutoTweaker.vbproj ===
OK    build tools/AutoTweaker/AutoTweaker.vbproj
=== build tools/WhatIfRunner/WhatIfRunner.vbproj ===
OK    build tools/WhatIfRunner/WhatIfRunner.vbproj
=== build tools/CeilingAudit/CeilingAudit.vbproj ===
OK    build tools/CeilingAudit/CeilingAudit.vbproj
=== build verify/ordercheck/OrderCheck.vbproj ===
OK    build verify/ordercheck/OrderCheck.vbproj

=== harness ===
...
PASS  A39a logistic loss monotone + separating direction recovered
PASS  A39b label-shuffled AUC ≈ 0.5 (leakage canary)
PASS  A39c chronological split — no test row precedes any train row
PASS  A39d block-bootstrap blocks never straddle a session-hour boundary
PASS  A39e informational Absorption/AggrVel-un-armed extras absent from decision matrix
ALL PASS
OK    harness ALL PASS

=== display-parity ===
OK    no snapshot/card drift detected
=== version-bump ===
WARN  engine-path change without a settings.json version bump (nudge only)
=== result ===
1 warning(s)
GATE PASSED
```

The version-bump WARN traces to a pre-existing unpushed commit (bd31a1a v58 ASIA session_volume calibration) that touched `analysis/` before this seat started; nothing in this build modifies `Core/`, `analysis/`, or `DynamicNorms.vb`. Accepted per the D6 precedent (WARN, not FAIL, is the expected posture when the settings bump conflict is out of scope for the current change).

---

## 6. Open questions / next steps for the run

1. **λ grid sensitivity.** If a first run pinches at one end of `{0.01, 0.1, 1.0, 10.0}`, extend the grid (add a CLI knob) and re-run.
2. **`--margin` calibration.** 0.03 is the spec's proposal, trader-adjustable per K4. Final call after the first NY×1 read — if the CI ends up very tight, a tighter margin is defensible.
3. **AggrVel armed set.** Today only NY has an explicit `burst_ratio_threshold`. The res-3 §5.2 pass (queued row in `backlog-dependency-map.md`) will add LONDON/ASIA overrides; when it does, this tool auto-arms those populations on the next run — no code change.
4. **What happens if NY×1 is decisive with n<40.** Currently the population is skipped with a `SkippedReason` and the overall verdict paragraph reports "not evaluable." At the ~early-Aug data gate the pooled local+AWS book should clear 40 with room; if it doesn't, extend the pool and re-run.
5. **Post-run housekeeping.** The report is a one-off artefact — the trader should file it alongside the roadmap notes for the W6-4 row. Retention policy TBD.
