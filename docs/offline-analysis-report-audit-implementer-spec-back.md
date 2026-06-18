# Offline Analysis Report — Resolution-Segmentation Fix — Implementer Spec-Back

**Spec:** `docs/offline-analysis-report-audit-proposal.md` (§5 D1–D3 trader-approved 2026-06-18).
**Seat:** implementer (fresh Opus). **Status:** code complete + verified locally; **routes to coordinator review → local commit** (trader tests + pushes — do NOT push).
**Layer:** `analysis/` only — host-agnostic, zero scoring votes / thresholds / vetoes / CSV-schema changes. **No `settings.json` bump.**

---

## 1. What was implemented (against §8 commit checklist)

### 1.1 `AnalysisReport.vb` (checklist #1) — container schema
- **Removed** the top-level barrier fields (`ExcludedRows`, `AtrInvalidExcluded`, `BelowMinMoveExcluded`, `StructuralStopRows`, `AtrFallbackRows`, `FailureCells`, `ContextOutcomes`).
- **Kept global:** `TotalRows`, `VerdictCounts`, `FundingDiagnostic`, `OfiAudit`, `OiCvdAudit`, `MarkdownText`/`MarkdownFilePath`/`SummaryCsvPath`.
- **Added** `Public Property Populations As New List(Of PopulationReport)()`.
- **Added** the `PopulationReport` class exactly per §2.3 — `PopulationKey`/`SessionName`/`Resolution`/`RowCount`, the five barrier counters, the per-population `FailureCells`/`ContextOutcomes`, and the caption fields `DirAtrN`/`DirAtrP25`/`DirAtrP50`/`DirAtrP75`/`MoveFloorUsd`.

### 1.2 `AnalysisRunner.vb` (checklist #2) — partition + per-population compute
- After `PopulateForwardBars`, rows are partitioned into an **ordered** list of `(session × resolution)` populations:
  - Session **derived** from `row.Timestamp.Hour` via the shared `ExecutionResolution.MatchSessionBucket` (inclusive boundary — the same matcher the Phase-2a tweaker uses; hour ranges NOT reimplemented).
  - Resolution is the **logged authoritative** `row.ExecResolution` (never re-derived), per the Phase-2a contract.
  - `popKey = sessionName & "|" & ExecResolution` → `"NY|1"`, `"ASIA|3"`, `"LONDON|3"`, phantom `"UNKNOWN|…"` last.
  - Display order is highest-data-first via `PopulationRank` (NY=0, LONDON=1, ASIA=2, else 9), tie-broken by key.
- **Per population:** `ExcludedRows` (no-OHLC), `FailureRateMatrix.Compute(popRows, …, cfg.Scoring.MinTradeableMovePct, cfg.Scoring.AtrTargetMultiplier)` (engine **byte-unchanged**), the VerdictContext cross-tab (extracted into `ComputeContextOutcomes`, now keyed off **this population's** recommended cell), and the ATR caption stats.
- **Caption stats:** computed over the population's **directional** rows (`ATR > 0 AND` tier-eligible verdict, via the new private `IsDirectionalVerdict` — mirrors `FailureRateMatrix.ToTier`'s positive set without coupling to that private member). `DirAtrP25/50/75` = ATR percentiles; `MoveFloorUsd = cfg.Scoring.MinTradeableMovePct × median(directional entry price)`.
- Diagnostics (§5/§6/§7) stay computed over **all** rows (D2 global).

### 1.3 `MarkdownReportWriter.vb` (checklist #3) — tier-major render
Full rewrite of `BuildMarkdown` into composable section helpers; layout per §2.4:
- **Global Summary** — rows, global verdict counts, "Populations detected" line, and a per-population barrier-diagnostics table carrying the **§4.1 clarification** inline ("below-min-move=0 is expected for an all-post-v35 book").
- **§2 Failure-Rate Matrix** — outer loop tier, inner loop population. Each `#### {session} · {res}-min · ATR p50=… (p25–p75 …–…) · move-floor $…` sub-table renders the **full** 5/10/15 × ATR-threshold grid (req 1), resolution-labelled (req 2), ATR-captioned (req 3), with `★◆` picked **within** the sub-table (req 4 — falls out free since `Compute` runs per population), rendered **even at n<30** (req 5); a tier with no rows in a population renders `#### … · (no {tier} rows yet)`.
- **§3 / §4a / §8 / §9** — same tier→session nesting (§9 lists pending cells per population).
- **§4 Verdict Context × Outcome** — segmented per session (barrier-based), from `pop.ContextOutcomes`.
- **Global Diagnostics** — §5/§6/§7 grouped under one heading, unchanged bodies (now `###`).
- `BuildSummaryCsv(report, …)` gains a **leading `Population` column**; one row per population × tier × window × threshold.

### 1.4 `FundingMomentumDiagnostic.vb` (checklist #4) — live threshold
- `Compute(rows, cfg As EngineSettings)`; single call site updated.
- `currentThresholdBp = cfg.Indicators.Funding.MomentumThreshold * 10000` (was hardcoded `0.001 * 10000`).
- Reworded the `Else` branch and the middle (`< 0.5 bp`) branch to compare the implied threshold against the **live** value rather than emit the stale absolute "0.0005" advice. The empirical `0.5 bp` REST-ceiling cutoffs (branch conditions) are unchanged — they are measurement facts, not the stale setting.

### 1.5 Docs (checklist #6)
- `architecture.md` analysis/ block notes the report is now per-`(session × resolution)`, tier-major, matrix engine byte-unchanged.
- `DeribitIndicatorProject.md` §15 entry intentionally **left for the coordinator** to add at commit time (the entry references the commit hash; this is offline-analysis-only, no engine-behaviour change, no `settings.json` bump).

---

## 2. Decisions / deviations

- **§4.4 (`AdverseFallbackAtrMultiplier` cfg pass-through) — SKIPPED** (spec says "optional… skip if it widens the diff"). Implementing it would change `FailureRateMatrix.Compute`'s signature, violating the spec's load-bearing "matrix engine the tweaker depends on is byte-unchanged" invariant (§2). It is moot for this book (all directional rows used structural stops; fallback=0). Left as-is.
- **Stale comment fixed:** `MarkdownReportWriter`'s header said the summary CSV is "consumed by the auto-tweaker". Verified false — `AutoTweakerCore` has no reference to `analysis_summary_*.csv`; it computes its own matrix via `FailureRateMatrix.Compute` over its filtered rows (this is also why adding the `Population` column is safe — the proposal §2.3 asserts the same). Comment corrected to say so. (`docs/UserManual.md:1795` carries the same stale claim — flagged for the coordinator, not touched here.)
- **Directional-row definition for the caption:** `ATR > 0 AND verdict ∈ {STRONG LONG, LONG, STRONG SHORT, SHORT}`. This is the matrix's tier-eligible set (it does **not** subtract the row-level below-min-move EXCLUDE, to avoid duplicating `Compute`'s internals) — i.e. the rows that *feed* the matrices. Documented in-code.

---

## 3. Verification

- **Builds (0W/0E):** `DeribitVerdictEngine.sln`, `tools/AutoTweaker/AutoTweaker.vbproj`, `verify/ordercheck/OrderCheck.vbproj`.
- **Acceptance harness:** `OrderCheck` → **ALL PASS** (A1–A15, incl. A14/A15 resolution + population-filter checks). This change does not touch scoring fixtures.
- **Offline render smoke test (throwaway, since removed):** a synthetic 3-population `AnalysisReport` (NY×1 n=586 full data, ASIA×3 n=406 with n=12 cells, LONDON×3 n=0 empty) rendered through `MarkdownReportWriter.Write` with no network. Confirmed: tier-major layout; per-session sub-tables with resolution labels + ATR captions + move-floor; `★◆` within sub-tables; `(no STRONG_LONG rows yet)` for the empty population; n<30 cells rendered; "Populations detected" + barrier-diagnostics table with the §4.1 note; §5/§6/§7 grouped global; summary CSV leading `Population` column (`NY|1,…` / `ASIA|3,…`; LONDON|3 correctly contributes no CSV rows). Project was under gitignored `verify/` and has been deleted.

---

## 4. Left for coordinator / trader (acceptance §7 remainder)

These require the **live book + Deribit OHLC network** and are the coordinator/trader steps, not implementer-runnable here:
1. Regenerate the report against the live `analysis_log.csv` (992 rows, v0.7); confirm separate **NY×1 / LONDON×3 / ASIA×3** blocks, NY×1 MEDIUM_LONG ≈ n=44 (≥30), Asia/London mostly "insufficient sample", global diagnostics unchanged vs the 893-row run.
2. Hand spot-check one population's matrix against a few CSV rows (1-min OHLC barrier walk) to confirm per-population numbers equal the old pooled math restricted to that population (e.g. NY 44 + ASIA 34 = the old pooled 78).
3. Add the `DeribitIndicatorProject.md` §15 version-history entry at commit time.

**Local-first — do not push.**

---

> ## Coordinator review — APPROVED (2026-06-18)
>
> Independently re-ran builds + harness and audited the diff line-by-line.
>
> **Builds (all 0W/0E):** `DeribitVerdictEngine.sln` (forced `-t:Rebuild`, so the changed `analysis/*.vb` recompiled from scratch), `tools/AutoTweaker/AutoTweaker.vbproj`, `verify/ordercheck/OrderCheck.vbproj`.
> **Harness:** `OrderCheck` → **ALL PASS**, 38 checks (A1–A13 + A14a–A14i + A15a–A15g). No scoring fixture touched, as expected.
> **Invariant confirmed at source (`git diff`):** `analysis/FailureRateMatrix.vb`, all of `Core/`, and `tools/AutoTweaker/` are **byte-unchanged** — the matrix engine the tweaker depends on is untouched, so the tweaker is unaffected. Only the 4 `analysis/` files + `architecture.md` carry the implementer's changes.
> **Diff audit:**
> - `AnalysisReport.vb` — schema matches §2.3; top-level barrier fields removed, `PopulationReport` added with all caption fields. ✓
> - `AnalysisRunner.vb` — partition by `popKey = MatchSessionBucket(cfg, hr).Name & "|" & row.ExecResolution` (session derived, resolution logged-authoritative); per-population `Compute` with the live `cfg.Scoring.MinTradeableMovePct`/`AtrTargetMultiplier`; context cross-tab re-scoped to each population's recommended cell; caption stats over directional rows; diagnostics global. Helpers (`PopulationRank`, `IsDirectionalVerdict`, `Percentile`) null-guarded. ✓
> - `MarkdownReportWriter.vb` — tier-major (outer tier / inner population); `SubTableHeader` carries resolution label + ATR caption + move-floor; `★◆` read from per-population cells; "(no {tier} rows yet)" for empty; §3/§4a/§8/§9 nested, §4 per-session, §5/§6/§7 global; summary CSV gains leading `Population` column; §4.1 clarification inline. ✓
> - `FundingMomentumDiagnostic.vb` — `Compute(rows, cfg)` reads live `MomentumThreshold` (null-guarded fallback to the `0.00001` POCO default); branch logic correct. ✓
> **Deviations accepted:** §4.4 (`AdverseFallbackAtrMultiplier` pass-through) correctly **skipped** — implementing it would have changed `Compute`'s signature, breaking the byte-unchanged invariant; moot here (fallback=0). The stale "summary CSV consumed by the auto-tweaker" comment fix is correct (verified: `AutoTweakerCore` has no reference to `analysis_summary_*.csv`). Directional-row definition for the caption is sound.
> **Partition arithmetic (static, vs the live v0.7 book, 992 rows):** bucket bounds ASIA 0–7 / LONDON 8–12 / NY 13–23 + the logged `ExecResolution` yield exactly three populations — **NY×1 n=586, LONDON×3 n=220, ASIA×3 n=186** (display order NY/LONDON/ASIA via `PopulationRank`). MEDIUM_LONG splits 44 NY×1 + (34 across LONDON×3/ASIA×3), so NY×1 MEDIUM_LONG stays n=44 (≥30) as the spec predicted.
> **Coordinator additions (this commit):** `UserManual.md` §18 brought current — the implementer-flagged "consumed by the auto-tweaker" line, the report-sections description (now tier-major per-session), AND two **pre-existing** doc-rot items found during review: the failure model was still documented as v1 forward-return (now v2 barrier-hit) and the Strong/Medium ATR-threshold constants were **swapped** vs the code (manual said Strong `{0.3,0.5}` / Medium `{0.5,0.8}`; code is the reverse). `DeribitIndicatorProject.md` §15 entry added (post-v37, code-only, no settings bump).
> **Remaining (trader, per §7):** (1) regenerate the report against the live book + Deribit OHLC — confirm the three population blocks + global diagnostics unchanged vs the 893-row run; (2) hand spot-check one population's matrix (per-pop sums to the old pooled total, e.g. 44+34=78); (3) **push** (never the coordinator). Local commit recorded below.
