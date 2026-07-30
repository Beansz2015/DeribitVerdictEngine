# Pooled-file report runner — spec-back

**Spec:** `docs/pre-aug1-opus-batch-2026-07-31.md` item B (the batch section *is* the spec).
**Built:** 2026-07-30 (Opus implementer, pre-Aug-1 batch). **Settings untouched — stays v63**, no config keys.
**Purpose:** unblock the F1 §9 read by making the offline report runnable over an arbitrary pooled CSV. **The READ ITSELF IS FENCED** — this lane produces the numbers and attaches them raw.

---

## 1. Host decision — a `report` verb on BacktestRunner, not a new project

The spec left the host to the implementer's call with an instruction to state it. **Chosen: a fourth verb on `tools/BacktestRunner/`.**

`BacktestRunner.vbproj` already links six of the eleven `analysis/` files — including `DeribitOhlcFetcher`, which *is* the forward-bar fetch path the spec points at — plus the whole shipped indicator/scoring set. Completing `AnalysisRunner`'s dependency set costs **four `<Compile Include>` lines**. A standalone `tools/ReportRunner/` would have needed its own `.vbproj` duplicating ~30 of those includes, plus a new entry in the verify-gate build set — the F10 lesson (a project outside the gate's build list silently rots) re-learned for zero benefit.

The four files added to the project: `FundingMomentumDiagnostic.vb`, `OutlierAudit.vb`, `MarkdownReportWriter.vb`, `AnalysisRunner.vb`. All host-agnostic. **`AnalysisReportForm.vb` is deliberately excluded** — it is the WinForms viewer, and BacktestRunner is a zero-WinForms Linux-capable console app (portability constraint, `architecture.md` §Design Decisions).

## 2. Mechanism

```
BacktestRunner report --csv <analysisLogCsv> [--settings <settings.json>]
```

The verb is a thin shell over the **shipped** seam:

```
AnalysisRunner.Run(csvFull, outDir, cfg)
```

— the same call the in-app status-bar link makes (`UI/MainForm_Layout.vb:1931`). **Zero changes to that in-app path.** The verb differs from it in exactly two respects, both of which are the point of the lane:

1. **The CSV is an argument**, not the engine's own working-directory `analysis_log.csv`.
2. **`outputDir` is the input's own directory**, so the markdown + summary CSV land *beside* the pooled snapshot rather than in the repo root. A pooled snapshot normally lives in a scratch directory and its report belongs with it.

Everything downstream — the (session × resolution) partition, `FailureRateMatrix.Compute`, `BandLadder.Compute`, `MarkdownReportWriter` — is untouched shipped code. One seam, no copies.

### 2.1 One structural change to argument handling

`--from` / `--to` were validated as required for **all** verbs before the command dispatch. `report` derives its own range from the CSV's row timestamps (`AnalysisRunner` computes `min(ts)` → `max(ts) + maxHoldWindow + 1`), so the guard is now gated on `cmd <> "report"`. `fetch` / `replay` / `validate` are byte-unchanged.

### 2.2 Failure surfacing

`AnalysisRunner` writes an error banner and leaves `MarkdownFilePath` empty when the forward-OHLC fetch fails. The verb treats that as **exit 1** with the banner text on stderr — a silent exit 0 on an unfetchable range would be the worst possible outcome for an unattended run.

## 3. Fixture — A46a

New family **A45 was consumed by the VWAP anchor lane; A46 is this lane's** (next free after this build: **A47**).

A46a writes a small v0.8-shaped CSV to a temp file and drives the **real** chain the verb runs:

```
ForwardWindowJoiner.Load → PopulateForwardBars → FailureRateMatrix.Compute
                         → BandLadder.Compute  → MarkdownReportWriter
```

Six rows at 14:00–15:40 UTC on 2026-07-28 (NY hours ⇒ population NY|1, res 1), two per band, one hitting the placed target and one hitting the placed stop — so every band reads exactly 50 % and the assertions are arithmetic, not approximate.

Asserted: the CSV genuinely round-trips (row count, `HasPlaced` true — it is what routes the barriers — `ExecResolution` 1, first timestamp exact); forward bars populate at the res-1 horizon; the rendered document carries **`## 2. Success-Rate Matrix`** with the `STRONG_LONG` sub-table, the placed-geometry grid header and a real `n=2` cell; and **`## 9. Band ladder`** with all three band rows at n=2 / 50 %. One structural pin rides along: **WEAK appears in the ladder but never in the matrix tier set** (the A35c invariant, re-checked from this lane's own data).

**What A46a does not cover, stated plainly:** the network hop. The forward-OHLC map is supplied synthetically so the fixture is offline and deterministic. The hop it stands in for is the pre-existing shared `DeribitOhlcFetcher` path, unchanged by this lane and exercised for real by the RUN below.

## 4. The RUN — pooled snapshot + report

### 4.1 Snapshot construction (`aws-collector-deploy-checklist.md` §4.3b)

Inputs: the frozen 2026-07-30 pair (`frozen_local_20260730.csv`, `frozen_aws_20260730.csv`), copied into this session's scratchpad before use (freeze rule).

- **Header equality verified first** (§4.3b step 2): both files are byte-identical 111-column v0.8 headers. Both parse ragged-free — every line in both files has exactly 111 fields, so the naive comma split the loader uses is safe here.
- **Dedup rule applied: local-preferred per UTC session-hour** (§4.3b step 3b). For any `yyyy-MM-dd HH` bucket where the local book has rows, only local rows are kept; AWS rows fill the buckets local missed.

| Quantity | Value |
|---|---:|
| Local rows | 8,338 |
| AWS rows (raw) | 7,079 |
| Local UTC session-hour buckets | 206 |
| AWS rows kept (hours local missed) | 5,001 |
| AWS rows dropped (same-hour as local) | 2,078 |
| **Pooled snapshot rows** | **13,339** |
| Range | 2026-07-03 15:57:49 → 2026-07-30 08:48:02 UTC |

Output: `pooled_dedup_20260730.csv`, body sorted by timestamp. Constructed at read time with **no tool changes**, exactly as §4.3b prescribes.

### 4.2 Report

```
BacktestRunner report --csv <scratchpad>/pooled_dedup_20260730.csv
```

`[DeribitOhlcFetcher] Fetched 38498 bars across 8 chunk(s) for 2026-07-03T15:57Z → 2026-07-30T09:34Z.`

| Population | Rows | Excluded |
|---|---:|---:|
| NY\|1 | 9,780 | 0 |
| LONDON\|3 | 1,897 | 0 |
| ASIA\|3 | 1,662 | 0 |

Artifacts: `analysis_report_20260730_142821.md` + `analysis_summary_20260730_142821.csv`, beside the input.

The §9 band-ladder table is reproduced **raw** in `docs/pre-aug1-batch-summary.md`. **No interpretation here or there** — the F1 read is fenced to the Fable/trader seat.

## 5. Gate

`tools/checks/verify-gate.ps1 -Mode prepush` — **GATE PASSED**. All 6 Release builds 0/0; harness A1–A45a unregressed + new **A46a**; display-parity OK; version-bump reports no engine-path change (this lane is `tools/` + `verify/` only).

## 6. Deviations from the written scope

None. The one thing the spec left open (host choice) is decided and stated in §1; the one structural edit outside the new verb's own code (the `--from`/`--to` guard, §2.1) was unavoidable to add a verb that has no date range, and leaves the other three verbs byte-unchanged.
