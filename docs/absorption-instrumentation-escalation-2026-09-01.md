# Absorption instrumentation build — handoff to Opus for the FULL remaining build

> ## ⛔ SUPERSEDED 2026-09-01. THE HANDOFF WAS TAKEN UP AND THE BUILD IS DONE. DO NOT ACT ON THIS DOCUMENT.
>
> **This is a HANDOFF BRIEF, written by the Sonnet seat that stopped. It was accurate when written and it is now a historical record.**
>
> ⛔ **Its status line below — *"Build not started. Zero `.vb` files changed"* — is TRUE OF THE SONNET SEAT AND FALSE OF THE TREE.** The Opus seat took the handoff and executed the whole build. **485 insertions across 10 files.** Acting on the line below means rebuilding or clobbering work that is already there.
>
> **Read these instead:**
>
> | Document | What it is |
> |---|---|
> | [`absorption-instrumentation-batch-summary.md`](absorption-instrumentation-batch-summary.md) | The outcome record — what was built |
> | [`absorption-instrumentation-spec-back.md`](absorption-instrumentation-spec-back.md) | The review packet — what to check and what to decide |
> | [`absorption-instrumentation-spec.md`](absorption-instrumentation-spec.md) | The spec itself, still authoritative for the rulings |
>
> ⭐ **What is still worth reading HERE, and only here:** §2's 17-file reader audit and §1's `A43e` trap analysis. **Those were the Sonnet seat's real output** — it wrote no code, by the trader's instruction, so nothing of its work lives in the tree. **It lives in this document.**
>
> ✅ **§1's scope claim was independently re-checked 2026-09-01 and it HOLDS, on a wider sweep than §1 ran.** §1 grepped `verify/ordercheck/Program.vb` alone. A tree-wide sweep for trailing-position row indexing found **no second instance**: every remaining `.Length - N` hit is either a row COUNT or a dotted settings-key split, neither of which indexes a CSV column. **`A43e` was the only one.**
>
> ⚠ **Why this banner exists rather than a deletion.** Two seats, one document: **Sonnet wrote it, Opus executed it.** Neither was wrong to leave the headline alone — it was not Opus's document — but the result is a live instruction to redo finished work. **Supersede in place, keep the record.**

**Status:** ⛔ **SEE THE BANNER ABOVE — this line is superseded.** ~~Build not started. Zero `.vb` files changed.~~ This doc plus the spec below are
the complete brief — the reader audit and trap discovery do not need to be re-derived.

**Spec:** [`absorption-instrumentation-spec.md`](absorption-instrumentation-spec.md), authorised
by D-1 in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6.

> ### Model: **Opus.** Effort: **HIGH.**
>
> **Scope: the entire remaining build, in one session** — clearing `A43e` (§1-§3 below)
> AND the rest of the spec (§4 below: R2-R8, the §3 struct chain, fixtures A60a-e). Do
> **not** clear `A43e` and stop for a handback to a Sonnet seat. The original spec's own
> §0 already argued against splitting a session across a dependency chain ("splitting
> separates the writers from the fixture that proves they agree") — that logic applies
> just as much across a tier boundary as within one seat.
>
> **Why Opus for all of it, not just `A43e`.** Only `A43e` forced the tier bump — see §1.
> Everything in §4 was already scoped Sonnet/high in the original spec's own brief and
> that assessment stands: every remaining piece threads through an existing in-repo
> template (`PullFrac`, named in the spec as the pattern to follow). Handing the whole
> build to one Opus seat isn't because the rest got harder — it removes a handback seam
> for no real cost, and the seat that clears `A43e` will already have the freshest read
> of `verify/ordercheck/Program.vb`'s fixture conventions, which §4's fixtures (A60a-e)
> also live in.
>
> **Session plan, in order:** §1-§3 below (confirm + fix `A43e`) → spec §3 (the type
> chain, dependency order fixed: `SideState` → `ReadSide` → `AbsorptionSideRead` →
> `AbsorptionSnapshot` → `ClassifyAbsorption` → `AbsorptionRead` → `r.Absorption*` →
> `IndicatorResults` → `AnalysisLogger`) → spec §4 (writers + reader re-audit, already
> done once this session — spot-check, don't redo from scratch) → spec §5 fixtures,
> **A60e first** (it is the only one that catches the schema-twin traps T1/T2 the
> original brief named). Full detail: **§4 below**.

**Trader decision, 2026-09-01:** the earlier version of this doc handed off only `A43e`
and expected a Sonnet/high seat to pick up the rest. **That has changed** — the trader
asked for the whole remaining spec to go to the same Opus/high seat. This revision
reflects that.

---

## 1. What triggered the stop

`absorption-instrumentation-spec.md` §0 names three escalation triggers. This build hit
the first one:

> A reader in §4's audit list indexes columns positionally past column 105 and cannot be
> made safe by R1's append rule.

**The reader:** [`verify/ordercheck/Program.vb`](../verify/ordercheck/Program.vb) `:6829-6836`,
fixture `A43e` (a fixture in the harness — an assert-style check identified by its own ID;
`A43e` checks header byte-parity between `AnalysisLogger` and `BacktestRowWriter.vb`, plus
`BacktestRowWriter.vb`'s row-provenance stamping).

```vb
Dim r1 = lines(1).Split(","c)
Dim r2 = lines(2).Split(","c)
Dim iid1 = r1(r1.Length - 2)
Dim iid2 = r2(r2.Length - 2)
Dim sid1 = Integer.Parse(r1(r1.Length - 1))
Dim sid2 = Integer.Parse(r2(r2.Length - 1))
```

**Why R1 does not cover it.** `absorption-instrumentation-spec.md` R1 rules that the five
new columns append after `SignalId` (positions 112-116) specifically because appending
cannot move an *absolute* index — `InstanceId`/`SignalId` stay at their current positions
(110/111) and every header-name or absolute-index reader stays correct.

`A43e` does not index absolutely — it indexes **relative to the end of the row**
(`Length - 2`, `Length - 1`). Appending five columns after `SignalId` moves what "the last
column" means: `Length - 1` would resolve onto the new `AbsorptionSizeMin` (a formatted
double) instead of `SignalId`, and `Integer.Parse` on that value fails — or, on an unlucky
value, parses a truncated number without throwing. This is exactly the "cannot be made
safe by R1" case the trigger names: no ordering of the append avoids it, because the
break is structural to trailing-position indexing, not to *where* the five columns land.

**Scope check — this is the only instance.** `grep -n '\.Length - [12]\)' verify/ordercheck/Program.vb`
returns exactly these four lines (plus one unrelated hit at `:6713`, an array-sizing
expression on a local test fixture, not a CSV row). No other reader in §4's list uses
trailing-position indexing — see the audit below.

---

## 2. Reader audit — all 17 files, verified this session

Per `absorption-instrumentation-spec.md` §4/R9, "must be re-verified, not assumed." Full
result, not a sample:

| File | Access pattern | Safe under R1? |
|---|---|---|
| `analysis/ForwardWindowJoiner.vb` | header-name (`colIdx` dict, `:111-114`) | ✅ |
| `Core/Settings/SettingsLoader.vb` | does not read `analysis_log.csv` (comment mention only) | N/A |
| `AnalysisLogger.vb` | **the writer** — in scope for the build itself, not an audit target | — |
| `LivePerformanceTracker.vb` | header-name (`colIdx` dict, `:1409-1415`) | ✅ |
| `tools/AutoTweaker/ConditionsExtractor.vb` | header-name (`ResolveColumns`, `:272-290`) | ✅ |
| `tools/AutoTweaker/TweakerConfig.vb` | holds `CsvPath` string only, no row parsing | N/A |
| `tools/BacktestRunner/BacktestProgram.vb` | delegates to `BacktestRowWriter`/`OverlapValidator` (both audited below); no direct parsing | ✅ |
| `tools/BacktestRunner/BacktestRowWriter.vb` | **the schema twin** — in scope for the build itself, not an audit target | — |
| `tools/BacktestRunner/CoverageReport.vb` | header-name (`Array.IndexOf`, `:291-293`) | ✅ |
| `tools/CeilingAudit/CeilingAuditProgram.vb` | delegates to `CsvFeatureBuilder.vb` (audited below); no direct parsing | ✅ |
| `tools/CeilingAudit/CsvFeatureBuilder.vb` | header-name (`colIdx` dict, `:118-121`) | ✅ |
| `tools/WhatIfRunner/WhatIfProgram.vb` | holds `csvPath` string only in the audited file; no row parsing here | ✅ |
| `UI/MainForm_Calibration.vb` | header-name (`colIdx` dict, `:36-40`) | ✅ |
| `UI/MainForm_Layout.vb` | `lnkAnalysisReport_LinkClicked` — schema-marker `.Contains("TrendStructure5m")` check only, no column indexing | ✅ |
| `UI/TweakSettingsForm.vb` | header-name, three separate resolutions (`:287-290`, `:379-382`, `:450-453`) | ✅ |
| `UI/WhatIfLauncherForm.vb` | holds `_csvPath` string only; hands off to `WhatIfRunner.exe` via `Process.Start`, no in-process parsing | ✅ |
| `verify/ordercheck/Program.vb` | ⛔ **mixed** — `A31f` (`:3993-3996`) reads `AbsorptionSignal` by `Array.IndexOf(header, ...)`, correctly. `A43e` (`:6829-6836`) reads `InstanceId`/`SignalId` by trailing position — **the trap** | ⛔ |

**16 of 17 are safe by construction (header-name resolution or no direct parsing).
One fixture in the 17th file is not**, and it is the reason this build stopped.

---

## 3. What the fix looks like (not yet applied)

Not applied — left for the Opus/high seat, per the trader's instruction to hand off
rather than have this session apply it under the "it looks mechanical" read.

The shape, for reference: resolve `InstanceId`/`SignalId` from the header (`lines(0)`)
the same way `A31f` two fixtures above it already does for `AbsorptionSignal` —
`Array.IndexOf(header, "InstanceId")` / `Array.IndexOf(header, "SignalId")` — rather than
`Length - 2` / `Length - 1`. That changes nothing about what `A43e` asserts (the
`"BACKTEST-"` prefix, id equality across rows, monotonic `SignalId`); it only changes how
the two columns are located.

**Before touching it, the Opus/high seat should independently confirm** this is in fact
mechanical and does not mask a reason the original author chose trailing-position
indexing (e.g. an implicit "no extra columns exist yet" assertion this fixture also
happened to be carrying). That confirmation is exactly the judgment call the escalation
trigger was written to route away from Sonnet.

---

## 4. Build plan for the rest of THIS session (after §1-§3 clear `A43e`)

Nothing else in the spec has been built. Every section below R1/R9 is unstarted. **Build
in this order** — it is the original spec's own §0 dependency order, unchanged by the
`A43e` detour:

1. **R8 first, while `Core/LevelAbsorptionTracker.vb` is already open for R2.** Add the
   D-6a comment at `:292` (text given verbatim in the spec §2 R8). Comment only — do not
   touch `proximity_atr_frac`/`band_atr_frac`/behaviour at that line.
2. **R2** — the only new state. Add `EpisodeStartMs` to `SideState`, set it at episode
   open (`:294`, `tsMs` already in scope), clear it in `CloseEpisode()` (`:104`), thread
   `nowMs` through `ReadSide` (its one call site is `Snapshot`, which already has it).
3. **§3 the type chain** — `SideState` → `ReadSide` → `AbsorptionSideRead` →
   `AbsorptionSnapshot` → `IndicatorEngine.ClassifyAbsorption` → `AbsorptionRead` →
   `r.Absorption*` → `IndicatorResults` → `AnalysisLogger`. Follow `PullFrac` as the
   template (the spec names it explicitly — already threaded end-to-end, same shape:
   `Double`, episode-scoped, meaningless when idle). Every `New … With {…}` initialiser
   on `AbsorptionSideRead`/`AbsorptionRead` needs the two new fields added — an omission
   defaults to 0 silently rather than failing to compile.
4. **R5 the three-file schema-twin rotation** — `AnalysisLogger.vb` (header `:108`,
   values `:295-299`), `BacktestRowWriter.vb` (header `:54`, values `:207-209`),
   `OverlapValidator.vb` (`ColSpec` list `:169-171`, five new entries, `ColKind.Muted`),
   `ReplayLoop.vb` (`:470`, five fields set to `Nothing`). **R1: append after `SignalId`,
   positions 112-116 — do not group with the existing `Absorption*` columns at 101-105.**
5. **R4** — inherits for free from the `Double?`/`InvOpt` discipline already used by the
   five existing `Absorption*` columns; nothing extra to write, just verify it holds.
6. **R6** — `docs/DeribitIndicatorProject.md` §15 entry, carrying R6's "rotation, not a
   comparability boundary" statement. No `settings.json` bump — no config keys added.
7. **§5 fixtures, family A60 — write A60e FIRST**, before A60a-d. It is the only fixture
   that catches the schema-twin traps (byte-equality between `AnalysisLogger.Header` and
   `BacktestRowWriter.Header`, same pattern `A43e` already uses for the reflection read).
   Then A60a (round-trip), A60b (idle ⇒ empty), A60c (episode measure-and-reset — build
   the failing input FIRST per the spec's own note: open → advance `nowMs` → close via
   `CloseEpisode()` → re-open at the SAME level → read again), A60d (`InstanceId`/
   `SignalId` header index unchanged — this is R1's own guard, and directly adjacent in
   spirit to the `A43e` trap this session just fixed).
8. **Acceptance** — spec §7, unchanged by this handoff: clean `dotnet build` (or
   `verify/ordercheck/OrderCheck.vbproj` if the app `.exe` is locked), harness green
   including the five new A60 fixtures, `tools/checks/verify-gate.ps1` green, one live
   run with the new columns read back, the §4 reader-audit lines recorded (already done
   in §2 above — carry it into the spec-back verbatim), the §15 entry written.

**Report back per [`batch-review-packet-convention.md`](batch-review-packet-convention.md)**
when the whole build (§1-§3 here plus this list) is done — one spec-back covering both
the `A43e` fix and the rest of the spec, not two separate packets.

---

## 5. Prior-session verification notes

**Files read this session, already verified against the spec's line-number claims**
(so the next seat does not need to re-open them just to get oriented):
[`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) (full file),
[`Core/Indicators_OrderFlow.vb`](../Core/Indicators_OrderFlow.vb) `:182-237`
(`ClassifyAbsorption`), [`Core/IndicatorResults.vb`](../Core/IndicatorResults.vb) `:88-96`,
[`UI/MainForm_Analysis.vb`](../UI/MainForm_Analysis.vb) `:466-487` and `:686-695`,
[`AnalysisLogger.vb`](../AnalysisLogger.vb) (full file),
[`tools/BacktestRunner/BacktestRowWriter.vb`](../tools/BacktestRunner/BacktestRowWriter.vb)
(full file), [`tools/BacktestRunner/OverlapValidator.vb`](../tools/BacktestRunner/OverlapValidator.vb)
`:1-230`, [`tools/BacktestRunner/ReplayLoop.vb`](../tools/BacktestRunner/ReplayLoop.vb)
`:1-25`, `:460-476`.

**Confirmed:** the current header is 111 columns (`Timestamp` through `SignalId`); the
spec's "positions 112-116" for the five new columns is consistent with a manual recount
against the live `AnalysisLogger.vb` header string.

**Not verified this session:** anything about `EpisodeSec` behaviour, the D-6a comment
text's technical accuracy, or the fixture literal values A60c will need — none of that
was reached before the stop.
