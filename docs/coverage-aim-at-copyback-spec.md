# `coverage` — let it be aimed at a copy-back

**Status:** SPEC. Buildable. No decision outstanding.
**Item:** queue item **21** ([`trader-tick-queue.md`](trader-tick-queue.md) §2 · [`seat-handover-2026-08-29.md`](seat-handover-2026-08-29.md) §0, which recommends it first).
**Author:** the orchestrator seat of 2026-09-03.

⚠ **Why this is first:** an instrument built to audit copy-backs cannot be pointed at one. **The 2026-08-29 collector-health numbers had to be computed from the store by hand, and that is why nobody had audited the box in 14 days.**

---

## 0. Implementer brief — model, effort, and where it slips

> ### Model: **Sonnet.** Effort: **LOW.**
>
> **Why that tier.** ⭐ **`CoverageReport.BuildResult` already takes all five paths as parameters** — `(opts, storeDir, analysisLogPath, wsHealthPath, markerPath)`. **Only the CLI hardcodes them.** The change is two option cases in an existing `Select Case` plus five assignments. **No new logic, no new file, no signature change to any callee.**
>
> ⭐ **And the load-bearing risk was checked before this spec was written, so the implementer does not have to.** See §2's trap table: **`CoverageReport` never touches `HistoricalStore.StoreDir`.** Every `HistoricalStore.*` call it makes is `EnumerateMonths`, `ExpectedFundingSamples` or `FetchTradesByTimeAsync` — date maths and one HTTP path, none of them store-relative. **Threading the parameter IS sufficient; there is no second cwd leak to chase.**
>
> **Where it will slip — three named traps:**
>
> | | Trap |
> |---|---|
> | **T1** | ⛔ **Do not change `HistoricalStore.StoreDir`.** It is `Public Const StoreDir As String = "backtest_data"` and **five other call sites depend on it** (`:58`, `:65`, `:69`, `:73`, and `:234`'s own `If String.IsNullOrWhiteSpace(storeDir)` fallback). **This spec adds a CLI override; it does not touch the const or its default** |
> | **T2** | ⚠ **`--verify-venue` takes `storeDir` too** — `RunVenueDiffAsync(storeDir, …)` at `BacktestProgram.vb:300`. **Pass the resolved value there as well, or the flag silently audits the local store while the report audits the copy-back.** Half a fix, and a silent one |
> | **T3** | ⚠ **The copy-back's book is named `analysis_log.csv` inside `aws_fetch/<stamp>/`, but `analysis_log_aws.csv` one level up.** Resolve from the `aws_fetch/<stamp>/` directory, which carries the collector's own filenames. **Do not add filename guessing** |
>
> **Escalation trigger — stop and move to Opus/high if:**
>
> - Any path in §2's list turns out to be read through `HistoricalStore.StoreDir` after all. **That would mean §2's trap check is wrong and the whole design premise with it.**
> - Redirecting the store changes any number in a same-input run (§4 acceptance item 2). **A pure re-aiming must be byte-identical on identical inputs.**
>
> **One session. No split.**

---

## 1. The defect, verified in the tree 2026-09-03

[`../tools/BacktestRunner/BacktestProgram.vb`](../tools/BacktestRunner/BacktestProgram.vb) `:282-291`, the `coverage` case:

```vb
Dim storeDir As String = HistoricalStore.StoreDir          ' the const, cwd-relative
Dim repoRoot As String = Directory.GetCurrentDirectory()
Dim analysisLogPath As String = Path.Combine(repoRoot, "analysis_log.csv")
Dim wsHealthPath   As String = Path.Combine(repoRoot, "ws_health.log")
Dim markerPath     As String = Path.Combine(repoRoot, "capture_marker.log")
```

**All four are then handed to a function that accepts them as arguments.** ⛔ **Three separate ways of setting a child process's working directory failed to redirect this**, because the store const is relative and the evidence paths are taken from the process cwd rather than from an argument.

---

## 2. ✅ The trap check, done in advance

**Every `HistoricalStore.*` call inside [`../tools/BacktestRunner/CoverageReport.vb`](../tools/BacktestRunner/CoverageReport.vb):**

| Line(s) | Call | Store-relative? |
|---|---|---|
| 440 · 489 · 724 · 768 · 787 · 893 · 943 | `EnumerateMonths` | ❌ **No** — pure date iteration |
| 795 | `ExpectedFundingSamples` | ❌ **No** — arithmetic on two ms bounds |
| 937 | `FetchTradesByTimeAsync` | ❌ **No** — the venue HTTP path |

⭐ **None reads `HistoricalStore.StoreDir`.** File paths inside `CoverageReport` are built from its own `storeDir` parameter via `TradeStoreWriter.TradeFileFor(storeDir, …)` (`:441`, `:490`, `:725`) and `CoverageCandleFileFor(storeDir, …)` (`:750`). ✅ **The parameter is honoured all the way down.**

---

## 3. The change

### R1 Add `--evidence-dir`, and derive all four paths from it

⭐ **The copy-back's `aws_fetch/<stamp>/` directory already has exactly the layout `coverage` wants** — `analysis_log.csv`, `ws_health.log`, `capture_marker.log`, and `backtest_data/`. **One option is therefore enough for the normal case.**

| Resolved value | When `--evidence-dir <dir>` is given | Default (flag absent) |
|---|---|---|
| `storeDir` | `Path.Combine(dir, HistoricalStore.StoreDir)` | `HistoricalStore.StoreDir` — **unchanged** |
| `analysisLogPath` | `Path.Combine(dir, "analysis_log.csv")` | `Path.Combine(Directory.GetCurrentDirectory(), …)` — **unchanged** |
| `wsHealthPath` | `Path.Combine(dir, "ws_health.log")` | unchanged |
| `markerPath` | `Path.Combine(dir, "capture_marker.log")` | unchanged |

⛔ **Defaults must be byte-identical to today.** A run with no new flag is not a behaviour change and must not read as one.

### R2 Add `--store-dir` as an independent override

**Applied AFTER R1**, so `--evidence-dir` can aim the evidence while `--store-dir` aims the store elsewhere. **Both optional; either alone is valid.**

### R3 Pass the resolved `storeDir` to `RunVenueDiffAsync` as well

[`../tools/BacktestRunner/BacktestProgram.vb`](../tools/BacktestRunner/BacktestProgram.vb) `:300`. **This is trap T2 and it is the half-a-fix the handover warned about.**

### R4 Follow the existing option idiom exactly

The parser is a `Select Case` over `args(i)` at `BacktestProgram.vb` `:78-113` — `Case "--from"`, `Case "--gap-ms"`, and so on. **Add two cases in that block. Do not introduce a second parsing style.**

### R5 Update `PrintUsage`

The `coverage` usage line at `BacktestProgram.vb` `:340`. ⚠ **An option that works and is undocumented is the same as no option** — the whole defect is that the instrument could not be aimed, and discoverability is part of aimable.

### R6 Fail loudly on a bad directory

⛔ **If `--evidence-dir` or `--store-dir` names a path that does not exist, print the resolved path and return 1.** Do NOT fall back to the default. **A silent fallback would audit the local box while the operator believes they audited the copy-back** — the exact class of silent-wrong-answer this project keeps finding, and worse than a crash.

### R7 No settings key, no version bump, no §15 entry

**CLI-only, `tools/` only.** No engine behaviour changes, no rendered surface, no CSV column. ⚠ **State in the commit message that no card surface is affected**, per the parity rule.

---

## 4. Acceptance

| | Check |
|---|---|
| 1 | `dotnet build` clean. ⚠ On `MSB3021` naming a locked `.exe`, the app is running — build `verify/ordercheck/OrderCheck.vbproj` instead |
| 2 | ⭐ **`coverage` with NO new flag produces byte-identical output to before the change.** This is R1's guard and the one that matters |
| 3 | ⭐ **`coverage --evidence-dir AWS-copybacks/aws-copyback-2026-09-01/aws_fetch/20260901-153838 --from … --to …` runs and reports on the COPY-BACK**, not the local store. Confirm by a figure that differs between the two |
| 4 | `--store-dir` alone redirects only the store |
| 5 | A non-existent `--evidence-dir` returns **1** and prints the resolved path. **No fallback** |
| 6 | `--verify-venue` with `--evidence-dir` diffs the copy-back's store (T2) |
| 7 | Harness green · `tools/checks/verify-gate.ps1` green |

**Fixtures:** ⚠ **none required, and that is deliberate.** `OrderCheck.vbproj` does not compile `BacktestProgram.vb` — confirmed from the project file's own comments at `:149`, `:160` and `:163`, each reading *"BacktestProgram stays OUT"* — and the change is CLI argument wiring with no new logic. ⚠ **Verify that by reading those comments or the `Compile Include` set, NOT by `grep -c BacktestProgram`, which returns 3 from the comments alone and reads as the opposite.** **Acceptance items 2–6 are the coverage.** ⛔ **Do not invent a fixture family for this** — a fixture that cannot reach the code under test is the false-coverage shape this project has recorded twice.

**Report per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).**

---

## 5. What this spec does not know

- ⛔ **Whether `coverage` produces a correct report against a copy-back once aimed.** This spec makes it *aimable*. **If the report then disagrees with the hand-computed 2026-08-29 numbers, that is a finding for a separate pass, not a bug in this change.**
- ⛔ **Item 18** (`ObservedLongestTrailingMs` over-reports on a split hour) is **NOT in scope** and carries an unmade decision. ⚠ **It becomes testable once this ships** — a purpose-built store can be aimed at — **but do not fix it here.**
- ⚠ **The three evidence filenames are hardcoded strings today and stay hardcoded.** Parameterising them individually is not in scope and was not asked for.
