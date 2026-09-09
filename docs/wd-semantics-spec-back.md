# WD-SEMANTICS spec-back

Spec: `docs/wd-semantics-unparsed-counter-spec.md` · RULED (c) 2026-09-09 UTC · BUILD-AUTHORIZED  
Baseline: `113edbb`  
Build date: 2026-09-10  
Harness before: **346** · Harness after: **349** · Delta: **+3** (A71a, A71b, A71c)

---

## Handles

Labels: **H-n** = reader-runnable · **E-n** = build-time evidence (cannot be re-run by reviewer)

| # | Label | Command / evidence | Property tested |
|---|---|---|---|
| H-1 | H | `dotnet build verify/ordercheck/OrderCheck.vbproj -c Release -t:Rebuild` | 0 errors, 0 warnings |
| H-2 | H | `dotnet run --project verify/ordercheck/OrderCheck.vbproj` (look for A71a, A71b, A71c lines) | All three new fixtures PASS |
| H-3 | H | `grep -n "UnparsedExcluded" analysis/AnalysisReport.vb analysis/ForwardWindowJoiner.vb analysis/MarkdownReportWriter.vb LivePerformanceTracker.vb tools/CeilingAudit/CsvFeatureBuilder.vb tools/CeilingAudit/AuditReport.vb tools/CeilingAudit/CeilingAuditProgram.vb UI/MainForm_Layout.vb` | All eight files carry the new field/reference |
| H-4 | H | `grep -n "UnparsedExcluded" analysis/AnalysisRunner.vb` | Should print the `ClassifyLoadedRows` call site (no direct field reference) |
| H-5 | H | `grep -n "ClassifyLoadedRows" analysis/ForwardWindowJoiner.vb analysis/AnalysisRunner.vb verify/ordercheck/Program.vb` | Defined on ForwardWindowJoiner; called from AnalysisRunner and A71c |
| E-1 | E | Release build output: `Build succeeded.` `0 Error(s)` `0 Warning(s)` | Clean Release build |
| E-2 | E | Harness output lines: `PASS A71a ...`, `PASS A71b ...`, `PASS A71c ...` | All three fixtures pass |
| E-3 | E | `verify-gate.ps1` output: `GATE PASSED` | Full gate |
| E-4 | E | AC-5 preflight output (see §AC-5 below) | UnparsedExcluded=0 on pooled book |
| E-5 | E | AC-6 markdown row (see §AC-6 below) | Row-count label correct |

---

## Six build steps as built

### Step 1 — Add `UnparsedExcluded` to `AnalysisReport`

`analysis/AnalysisReport.vb`: added `Public Property UnparsedExcluded As Integer` after `WeekendExcluded`. Updated contract comment: `TotalRows + WeekendExcluded + UnparsedExcluded = rows loaded`.

### Step 2 — Add `UnparsedExcluded` to `LivePerformanceTracker.WindowAggregate`

`LivePerformanceTracker.vb`: added `UnparsedExcluded As Integer` to `WindowAggregate` struct. At the counter site replaced `If e.Timestamp = DateTime.MinValue Then Continue For` with:

```vb
If e.Timestamp = DateTime.MinValue Then
    agg.UnparsedExcluded += 1    ' [WD-SEMANTICS] data defect, not scope exclusion
    Continue For
End If
```

MinValue guard placed BEFORE the `IsWeekdayRow` check (Trap 2: `DateTime.MinValue.DayOfWeek` = Monday).

### Step 3 — Add `UnparsedExcluded` to `CsvFeatureBuilder.LoadStats`

`tools/CeilingAudit/CsvFeatureBuilder.vb`: same pattern. `UnparsedExcluded As Integer` in `LoadStats`. Counter site:

```vb
If w.Row.Timestamp = DateTime.MinValue Then
    stats.UnparsedExcluded += 1    ' [WD-SEMANTICS] data defect, not scope exclusion
    Continue For
End If
```

### Step 4 — Replace `Where + subtraction` in `AnalysisRunner` with `ForwardWindowJoiner.ClassifyLoadedRows`

`analysis/ForwardWindowJoiner.vb`: added `Public Shared Function ClassifyLoadedRows`:

```vb
Public Shared Function ClassifyLoadedRows(loadedRows As List(Of CsvRow),
                                          report As AnalysisReport) As List(Of CsvRow)
    Dim kept As New List(Of CsvRow)()
    For Each r In loadedRows
        If r.Timestamp = DateTime.MinValue Then
            report.UnparsedExcluded += 1    ' MinValue guard FIRST (Trap 2)
        ElseIf Not IsWeekdayRow(r.Timestamp) Then
            report.WeekendExcluded += 1
        Else
            kept.Add(r)
        End If
    Next
    report.TotalRows = kept.Count
    Return kept
End Function
```

Placed on `ForwardWindowJoiner` (not `AnalysisRunner`) because `verify/ordercheck/OrderCheck.vbproj` deliberately excludes `AnalysisRunner.vb` (live OHLC network calls). The harness includes `ForwardWindowJoiner.vb` at project file line 110.

`analysis/AnalysisRunner.vb`: replaced `Where + subtraction` block (lines 44-47) with:

```vb
Dim rows As List(Of CsvRow) = ForwardWindowJoiner.ClassifyLoadedRows(loadedRows, report)
```

### Step 5 — Update rendered surfaces

- `UI/MainForm_Layout.vb`: tooltip shows `Unparsed excl.: n=N` when `w.UnparsedExcluded > 0`.
- `tools/CeilingAudit/AuditReport.vb`: markdown table row `| Excluded — unparsed timestamp | N |` after the weekend row.
- `tools/CeilingAudit/CeilingAuditProgram.vb`: emits `PREFLIGHT_UNPARSED_EXCLUDED=N`.
- `analysis/MarkdownReportWriter.vb` line 191: row-count now includes `UnparsedExcluded` in the total and the label (`unparsed excl.` segment).

### Step 6 — Write fixtures A71a, A71b, A71c

`verify/ordercheck/Program.vb`:

- **A71a** — extends A68a's existing `Sub` with a second `Check()`: same batch as A68a, asserts `agg.UnparsedExcluded = 1`. Trap 3 guard: A68a still passes (WeekendExcluded counter unaffected).
- **A71b** — new `Sub`, exercises `CsvFeatureBuilder` with a CSV carrying Monday + Saturday + broken-timestamp row. Asserts `kept.Count=1 AndAlso stats.WeekendExcluded=1 AndAlso stats.UnparsedExcluded=1`.
- **A71c** — new `Sub`, calls `ForwardWindowJoiner.ClassifyLoadedRows` directly with 4 synthetic `CsvRow` objects (mon, tue, sat, DateTime.MinValue). Asserts `splitCorrect` (`TotalRows=2, WeekendExcluded=1, UnparsedExcluded=1`) AND `identity` (`TotalRows + WeekendExcluded + UnparsedExcluded = loadedRows.Count`). Both required: identity alone holds even when the split is wrong (arithmetic tautology).

A71b and A71c called from `Main()` before the A69 block. A71a has no separate `Main()` call — runs inside A68a's `Sub`.

---

## Acceptance criteria

### AC-1 — Build

Release `-t:Rebuild`: **0 errors, 0 warnings.**  
Verified: ran `dotnet build verify/ordercheck/OrderCheck.vbproj -c Release -t:Rebuild` and the main solution. Both succeeded.

### AC-2 — Harness count

Before: **346** · After: **349** · All PASS.  
Output (excerpt):

```
PASS A71a WD-SEMANTICS — same AggregateRange batch: UnparsedExcluded=1 ...
PASS A71b WD-SEMANTICS (audit) — unparsed row in UnparsedExcluded=1, weekend in WeekendExcluded=1 ...
PASS A71c AnalysisRunner classification identity — ...
```

### AC-3 — Prior fixtures unbroken

A67a, A68a, A68b, A69a–A69e: all PASS after the build.  
Trap 3 (A68a specifically): the A68a `Check()` on `WeekendExcluded` was NOT changed by A71a — A68a still asserts what it always did. Confirmed by mutation proof 1 (see §Mutation proofs).

### AC-4 — Gate

`verify-gate.ps1 -Mode local-fast`: **GATE PASSED**.  
All six projects (solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck) built 0/0 Release. Display-parity clean — no snapshot/card surface touched.

### AC-5 — Preflight on pooled book

File: `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv`  
MD5 (verified before run): `E8418846838FF97F3C90F782A95B3523`

Preflight output:

```
PREFLIGHT_ELIGIBLE_ROWS=8269
PREFLIGHT_WEEKEND_EXCLUDED=9792
PREFLIGHT_UNPARSED_EXCLUDED=0
```

Result: `UnparsedExcluded=0` on this corpus — consistent with expectation (production tape has no known broken timestamps).

### AC-6 — Backtest markdown row

Ran `BacktestRunner report --csv` against the pooled book. The markdown report row:

```
- Rows in CSV: 47682  (weekday-scoped: 37518 analysed · 10164 weekend excl. · 0 unparsed excl.)
```

The total 47682 = 37518 + 10164 + 0. Previously this line summed only to the post-filter weekday count; now it correctly sums to rows loaded. The console `[Report] Rows loaded: 37518` line shows `rpt.TotalRows` (post-filter weekday count, unchanged) — that is a separate line outside this spec's change scope.

### AC-7 — settings.json untouched

`settings.json` remains at **v68**. No key added, no version bump needed. Verified: `git diff -- settings.json` is empty.

### AC-8 — Non-ASCII characters in added lines

Added lines contain: `—` (em dash), `·` (middle dot), `⚠`, `⛔`, `──`. All intentional. No mojibake. Verified with `grep -P '[\x80-\xFF]'` across all modified files.

---

## Mutation proofs

All three mutations were run (not reasoned). Files restored by file copy, not by `git checkout`.

### Mutation 1 — A71a (LivePerformanceTracker)

Changed `agg.UnparsedExcluded += 1` to `agg.WeekendExcluded += 1` in `LivePerformanceTracker.vb`.  
Harness result: **A68a FAIL** (`WeekendExcluded=3`, expected 2), **A71a FAIL** (`UnparsedExcluded=0`, expected 1).  
File restored. A68a passes again (it is sensitive to the counter — Trap 3 confirmed as non-issue).

### Mutation 2 — A71b (CsvFeatureBuilder)

Same fold: `stats.UnparsedExcluded += 1` → `stats.WeekendExcluded += 1`.  
Harness result: **A68b FAIL** (weekend=2, expected 1), **A71b FAIL** (unparsed=0, expected 1).  
File restored.

### Mutation 3 — A71c (ForwardWindowJoiner)

Swapped the MinValue branch to increment `WeekendExcluded` instead of `UnparsedExcluded` in `ForwardWindowJoiner.ClassifyLoadedRows`.  
Harness result:  
- `identity=True` (arithmetic tautology always holds — all counts still sum to `loadedRows.Count`)  
- `splitCorrect=False` (`WeekendExcluded=2`, expected 1; `UnparsedExcluded=0`, expected 1)  
- **A71c FAIL**

Confirms that both `identity` and `splitCorrect` checks in A71c are needed. `identity`-alone cannot catch the wrong split.  
File restored.

---

## Engine display-string parity rule

**DOES NOT FIRE for this build.**

The rule requires that any change to a line emitted by `BuildPlaintextSnapshot` (`UI/MainForm_PlaintextSnapshot.vb`) must be mirrored in the card bindings (`UI/MainForm_Render_Cards.vb`) in the same commit.

The perf-strip tooltip added in `UI/MainForm_Layout.vb` is neither `BuildPlaintextSnapshot` output nor a `Render_Cards` binding. No text rendered by the tooltip appears on either of those surfaces. The rule's trigger condition is not met.

---

## settings.json version bump

Not needed. No config key was added or changed. `settings.json` stays at v68.

---

## What was NOT verified

- The `Unparsed excl.: n=N` tooltip line was not observed rendered in the running app. The line fires only when `UnparsedExcluded > 0`, and no corpus with a broken timestamp was available for a live run. The code path is covered by A71a (LivePerformanceTracker batch) and A71b (audit), which exercise the counter site directly. The tooltip conditional is three lines and structurally identical to the existing `WeekendExcluded` block above it.
- AC-5 and AC-6 both returned `UnparsedExcluded=0` — the counter is correct but dormant on the available corpus. The correctness of the non-zero path is covered by the fixtures.

---

## Spec feedback

The spec correctly named both traps (MinValue guard order, `Where + subtraction`). The constraint that `ClassifyLoadedRows` must live on `ForwardWindowJoiner` rather than `AnalysisRunner` was not in the spec — it was discovered when the first `A71c` draft caused `BC30451: 'AnalysisRunner' is not declared` in the harness build. The project file comment at `OrderCheck.vbproj` states the reason (*"AnalysisRunner stays OUT — it needs a live Deribit OHLC fetch"*). Moving the function to `ForwardWindowJoiner` resolved the issue with no functional change; `AnalysisRunner.Run()` calls it via `ForwardWindowJoiner.ClassifyLoadedRows(...)`.
