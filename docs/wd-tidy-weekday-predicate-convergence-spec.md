# `WD-TIDY` — converge the inline weekday predicates onto `ForwardWindowJoiner.IsWeekdayRow`

> ## ✅✅ BUILT AND SHIPPED — commit `5996f01`, 2026-09-08 (UTC). **THIS DOCUMENT IS A RECORD, NOT AN INSTRUCTION. DO NOT HAND IT TO AN IMPLEMENTER.**
>
> Four sites converged onto `ForwardWindowJoiner.IsWeekdayRow`. Harness **337 → 339** (`A68a`, `A68b`); `GATE PASSED`; `settings.json` untouched at **v68**. `D-2` (a) held — **no rendered value moved**.
>
> ⚠ **A spec assumption below BROKE and the implementer caught it: `A68a` is INERT unless `rangeStartUtc = DateTime.MinValue`**, because `AggregateRange` range-filters **before** the weekday guard — under any realistic start the `MinValue` entry never reaches the code under test and the fold would have passed. **§5 gives no warning of this.** Sibling lesson: [[feedback-fixture-shape-must-admit-the-failure]].
>
> ⚠ **§2.1's claim that no target file declares a `Namespace` is WRONG** — `tools/CeilingAudit/CsvFeatureBuilder.vb` declares `Namespace CeilingAudit`. The conclusion held (VB resolves outward, no new `Imports` needed), but the stated reason did not.
>
> ⚠ **Residual raised by this build and still OWED: `WD-SEMANTICS`** — `WeekendExcluded` means three different things on three surfaces. It is a **decision**, not a build slot.

**Spec status:** RULED AND BUILT. Every decision in §3 is ticked. There is no open D-table.

**Baseline commit: `2da3872`.** Every line number in this spec was read at that commit. ⛔ **Re-read before editing if `HEAD` has moved** — a line number correct when written goes wrong two commits later.

**Queue row:** `WD-TIDY`, ranked 2nd in [`seat-handover-2026-09-07.md`](seat-handover-2026-09-07.md) §0.
**Ruling this discharges:** [`weekday-scope-ruling-2026-08-03.md`](weekday-scope-ruling-2026-08-03.md) §2 — already discharged for *behaviour* by surfaces 1–3; this is the *tidy*, not new scope.

---

## 0. Model and effort

> ### Model: **Sonnet**
> ### Effort: **medium**
> ### One session. Do not split it.

**Why that tier.** The judgment work is done — §3 is ruled, not open. Every edit has an in-repo template: the delegation shape is `analysis/AnalysisRunner.vb:45`, the fixture shape is `A67b` at `verify/ordercheck/Program.vb:12615`, and the CSV-fixture shape is `A59d` at `verify/ordercheck/Program.vb:1783`. Both consuming projects already link the target file, verified — see §2.

**Why it is NOT low.** Two of the four sites carry a **user-visible counter** whose branch semantics must survive exactly. The obvious one-line delegation at those two sites **compiles, passes every existing fixture, and silently changes two rendered numbers.** That is the whole risk in this build and it is not visible from the diff.

### 0.1 Where this model will specifically slip

⛔ **Trap 1 — the counter fold. This is the one that matters.** At `tools/CeilingAudit/CsvFeatureBuilder.vb:198` and `LivePerformanceTracker.vb:757` the two branches have **different side effects**:

- `DateTime.MinValue` → `Continue For`, **silently**, no counter
- Saturday/Sunday → `WeekendExcluded += 1`, **then** `Continue For`

Writing `If Not ForwardWindowJoiner.IsWeekdayRow(ts) Then WeekendExcluded += 1 : Continue For` folds those into one branch and **starts counting unparsed rows as weekend rows.** That number reaches a user on two surfaces — `UI/MainForm_Layout.vb:1743` renders `"Weekend excl.: n={0}"`, and `tools/CeilingAudit/AuditReport.vb:84` renders `| Excluded — weekend | N |`.

⚠ **The existing fixtures cannot catch it.** `A67a` (`verify/ordercheck/Program.vb:12572`) asserts `WeekendExcluded = 2` over four entries, **none of which is `DateTime.MinValue`.** The fold passes it. **You write the new fixtures too, so a misunderstanding here propagates into its own test** — §4 names the exact input that makes the fold fail, and you must confirm the fixture fails before the edit and passes after.

⚠ **Trap 2 — `ConditionsExtractor`'s fail-closed guards are NOT part of the predicate.** At `tools/AutoTweaker/ConditionsExtractor.vb:141` there is `If Not hasTimestampCol OrElse idx.Timestamp >= parts.Length Then Continue For`. That is **D-3 fail-closed logic** from [`autotweaker-weekday-filter-proposal.md`](autotweaker-weekday-filter-proposal.md) §5.4, added by orchestrator review finding `F1` (2026-08-25) because the per-row form originally failed **open**. **Leave it exactly where it is.** Only the three lines below it (`:145–:149`) are in scope.

⚠ **Trap 3 — `MatchesWeekday`'s signature takes a `CsvRow`, not a `DateTime`.** Keep the signature. Delegate only the body. It has **5 call occurrences** in `verify/ordercheck/Program.vb` across two fixtures (`A59a`, `A59e`) and **2 production call sites** in `tools/AutoTweaker/AutoTweakerCore.vb` (`:133`, `:135`). *(⚠ [`seat-handover-2026-09-07.md`](seat-handover-2026-09-07.md) §4 says "7 harness refs" — the correct figure is 5 call occurrences plus one comment mention. The instruction it carries is still right.)*

⚠ **Trap 4 — do not touch `verify/ordercheck/Program.vb:1670–1671`.** That is the harness's **own independent copy** of the Sat/Sun test, inside `A59a`'s assertion. It must stay independent: a fixture that asserted through the production predicate would be self-referential and could not detect a revert. See §3, `D-5`.

⚠ **Trap 5 — a VB comment inside a collection initializer breaks implicit line continuation.** Put block comments above the block. (Cost real time on 2026-09-07.)

### 0.2 Escalation trigger — stop and move to Opus/high

⛔ **If preserving the counter semantics at the two counter sites needs anything beyond "keep the existing inline `MinValue` line, delegate only the Sat/Sun test"** — if you find yourself wanting to change `IsWeekdayRow`'s signature, add an overload, add a `ByRef` output, or introduce a second predicate — **STOP and report.** That means `D-2` (a) was ruled wrong and the decision must be re-taken, not worked around.

⛔ **Also stop if any existing fixture in the `A59*` or `A67*` families changes its result.** This build is zero-behaviour-change. A moved assertion is a defect, not a rebaseline.

---

## 1. What this is, in one line

**Four production sites test "is this timestamp a weekday" with their own copy of the same two-day test. Converge them onto the one shipped predicate. Change no behaviour and no rendered number.**

---

## 2. The census — measured at `2da3872`, not inherited

⚠ **[`seat-handover-2026-09-07.md`](seat-handover-2026-09-07.md) §4 says "three copies … converge the two inline ones". That is an undercount.** The full census, from an unanchored `grep -rn "DayOfWeek.Saturday\|DayOfWeek.Sunday" --include=*.vb`:

| # | Site | Shape | In scope? |
|---|---|---|---|
| 1 | `analysis/ForwardWindowJoiner.vb:132` | `IsWeekdayRow(ts As DateTime)` | **canonical — the target** |
| 2 | `tools/AutoTweaker/AutoTweakerCore.vb:829` | `MatchesWeekday(r As CsvRow)`; `Return False` on both branches | ✅ **YES** — identical semantics |
| 3 | `tools/AutoTweaker/ConditionsExtractor.vb:145` | inline; `Continue For` on both branches | ✅ **YES** — identical semantics |
| 4 | `tools/CeilingAudit/CsvFeatureBuilder.vb:198` | inline; Sat/Sun bumps `stats.WeekendExcluded` | ✅ **YES** — ⛔ counter site |
| 5 | `LivePerformanceTracker.vb:757` | inline; Sat/Sun bumps `agg.WeekendExcluded` | ✅ **YES** — ⛔ counter site |
| 6 | `tools/BacktestRunner/CoverageReport.vb:629` | on an **hour**, emits `HourClass.OutOfScopeWeekend` | ❌ **NO** — see `D-3` |
| 7 | `UI/TweakSettingsForm.vb:315` | `TryParse` + `AssumeUniversal`, deliberately not unified | ❌ **NO** — see `D-4` |
| 8 | `verify/ordercheck/Program.vb:1670` | harness assertion inside `A59a` | ❌ **NO** — see `D-5` |

**Scope ruled by the trader 2026-09-07: all four convergeable sites (rows 2–5).**

### 2.1 Feasibility — verified, not assumed

✅ `..\..\analysis\ForwardWindowJoiner.vb` is a `Compile Include` in **all three** consuming projects:

- `tools/AutoTweaker/AutoTweaker.vbproj`
- `tools/CeilingAudit/CeilingAudit.vbproj`
- `verify/ordercheck/OrderCheck.vbproj` (`:110`)

✅ `LivePerformanceTracker.vb` is at the repo root, and the root project globs `**/*.vb`, so `analysis/ForwardWindowJoiner.vb` is already in the same assembly.

✅ **No `Namespace` declaration in any of the four files** — all compile under `RootNamespace = DeribitVerdictEngine`. `ForwardWindowJoiner.IsWeekdayRow` resolves by simple name. **No new `Imports` is needed anywhere.**

### 2.2 The predicate being converged onto

`analysis/ForwardWindowJoiner.vb:132`:

```vb
Public Shared Function IsWeekdayRow(ts As DateTime) As Boolean
    If ts = DateTime.MinValue Then Return False
    Dim dow As DayOfWeek = ts.DayOfWeek
    Return dow <> DayOfWeek.Saturday AndAlso dow <> DayOfWeek.Sunday
End Function
```

⛔ **Do not modify it.** It is pinned by `A67b` and consumed by `analysis/AnalysisRunner.vb:45` and `tools/WhatIfRunner/WhatIfProgram.vb:97`.

---

## 3. The D-table — ALL RULED. Do not re-open.

| # | Decision | Ruling | Why |
|---|---|---|---|
| **`D-1`** | Scope: the handover's 2 sites, or all 4 convergeable? | ✅ **ALL FOUR** (rows 2–5 of §2) — **trader, 2026-09-07** | Excluding `ConditionsExtractor` is arbitrary: it is the *cleanest* delegation of the four. Excluding `LivePerformanceTracker` leaves the newest copy — shipped 2026-09-07, one day old — as a copy |
| **`D-2`** | At the two counter sites, does a `MinValue` row count as `WeekendExcluded`? | ✅ **(a) PRESERVE EXACTLY.** Keep the inline `MinValue` guard, first, and delegate **only** the Sat/Sun test | It is the only option that makes `WD-TIDY` what it is billed as — a tidy with **zero** behaviour change. ⭐ **The three surfaces already disagree today** and this build does not resolve that; see §3.1 |
| **`D-3`** | Does `CoverageReport.ClassifyHour:629` come in? | ✅ **NO** | Different question. It tests a **constructed `hourStartUtc`**, never a parsed row timestamp, so it needs no `MinValue` guard and has none by design. Its output is a **classification** (`HourClass.OutOfScopeWeekend`), not a filter. Delegating would import a guard for a value that cannot be `MinValue` |
| **`D-4`** | Does `UI/TweakSettingsForm.vb:315` come in? | ✅ **NO** | **Deliberately divergent by a prior ruling** — its own comment cites [`autotweaker-weekday-filter-proposal.md`](autotweaker-weekday-filter-proposal.md) §3.3: it uses `DateTime.TryParse` with `AssumeUniversal`, *"NOT unified with AutoTweakerCore's TryParseExact"*. Also unreachable by the harness — `UI/TweakSettingsForm.vb` is a WinForms file and is **not** a `Compile Include` in `verify/ordercheck/OrderCheck.vbproj` |
| **`D-5`** | Does the harness's own copy at `Program.vb:1670` come in? | ✅ **NO — it must STAY a copy** | A fixture that asserted through the production predicate is **self-referential**: revert the production code and the assertion reverts with it, so it can never fail. This is the sibling of the standing rule *"verification handles must test the property, not a string that mentions it"* in `CLAUDE.md` |

### 3.1 ⭐ Recorded so it is not re-discovered: the three surfaces already disagree

**`WeekendExcluded` does not mean the same thing on all three surfaces today, and `D-2` (a) deliberately preserves that.**

| Surface | How it is computed | Does a `MinValue` row count as weekend? |
|---|---|---|
| `analysis/AnalysisRunner.vb:46` | `report.WeekendExcluded = loadedRows.Count - rows.Count` — a **subtraction** | ⚠ **YES.** `IsWeekdayRow` rejects `MinValue`, and the subtraction cannot tell why a row went |
| `LivePerformanceTracker.vb:757` | explicit counter on the Sat/Sun branch only | **NO** |
| `tools/CeilingAudit/CsvFeatureBuilder.vb:198` | explicit counter on the Sat/Sun branch only | **NO** |

⚠ `analysis/AnalysisReport.vb:17–18` states the subtraction contract explicitly: *"`TotalRows` is the POST-filter count, so `TotalRows` + `WeekendExcluded` = rows loaded."*

⛔ **This build does NOT fix that.** Unifying the three would change a user-visible number on at least one surface, which is a real change and belongs in its own spec. **Record it as a follow-up queue row** (§6) — do not fold it into `WD-TIDY`.

---

## 4. The build list — four edits

⚠ **Order them as written.** Edit 1 and 2 are the safe pair; do them, build, run the harness, and confirm `A59a`/`A59d`/`A59e` still pass before touching the counter sites.

### Edit 1 — `tools/AutoTweaker/AutoTweakerCore.vb:829–834`

Keep the signature. Replace the body only.

```vb
Friend Shared Function MatchesWeekday(r As CsvRow) As Boolean
    Return ForwardWindowJoiner.IsWeekdayRow(r.Timestamp)
End Function
```

**Semantics are identical**: the current body returns `False` on `MinValue`, `False` on Sat/Sun, `True` otherwise — exactly `IsWeekdayRow`.

⚠ **Update the comment block above it.** It currently says *"Matches `CsvFeatureBuilder.vb:198-203` deliberately — same guard order, same two-day test"* and restates the `MinValue` rationale. That justification now lives in `ForwardWindowJoiner.vb`. Replace it with a pointer, and **keep** the sentence explaining why the method still exists (`Friend` so the harness can exercise it directly; 5 harness call occurrences).

### Edit 2 — `tools/AutoTweaker/ConditionsExtractor.vb:145–149`

⛔ **Leave `:141` (`If Not hasTimestampCol OrElse idx.Timestamp >= parts.Length Then Continue For`) and the `TryParseExact` call untouched.** Replace only:

```vb
If rowTs = DateTime.MinValue Then Continue For
Dim rowDow As DayOfWeek = rowTs.DayOfWeek
If rowDow = DayOfWeek.Saturday OrElse rowDow = DayOfWeek.Sunday Then Continue For
```

with:

```vb
If Not ForwardWindowJoiner.IsWeekdayRow(rowTs) Then Continue For
```

⚠ **Trim the comment above to match** — its *"same guard order (MinValue first)"* clause now describes `IsWeekdayRow`, not this site. **Keep** the D-3 fail-closed explanation; that still describes `:141`, which is unchanged.

### Edit 3 — `tools/CeilingAudit/CsvFeatureBuilder.vb:198–203` ⛔ COUNTER SITE

```vb
' [WD-TIDY, D-2 (a)] The MinValue guard stays INLINE and stays FIRST. It is NOT
' redundant with IsWeekdayRow's own MinValue branch: an unparsed row must drop
' SILENTLY, while a genuine weekend row must bump stats.WeekendExcluded, which
' AuditReport.vb:84 renders as "| Excluded — weekend | N |". Folding the two into
' one call changes a number a user reads. Pinned by A68b.
If w.Row.Timestamp = DateTime.MinValue Then Continue For
If Not ForwardWindowJoiner.IsWeekdayRow(w.Row.Timestamp) Then
    stats.WeekendExcluded += 1
    Continue For
End If
```

⭐ **`IsWeekdayRow`'s own `MinValue` branch is unreachable from this call site, and that is correct, not waste.** The inline guard is what carries the counter semantics.

### Edit 4 — `LivePerformanceTracker.vb:757–762` ⛔ COUNTER SITE

Same shape, against `agg.WeekendExcluded` and `e.Timestamp`:

```vb
' [WD-TIDY, D-2 (a)] MinValue guard stays INLINE and FIRST — an unparsed row drops
' SILENTLY, a weekend row bumps agg.WeekendExcluded, which MainForm_Layout.vb:1743
' renders as "Weekend excl.: n={0}". Folding them changes a number a user reads.
' Pinned by A68a.
If e.Timestamp = DateTime.MinValue Then Continue For
If Not ForwardWindowJoiner.IsWeekdayRow(e.Timestamp) Then
    agg.WeekendExcluded += 1
    Continue For
End If
```

⚠ **The long comment block at `:735–:756` is a decision record** — it carries the UTC-vs-UTC+8 consequence statement and the "storage is untouched" note. **Keep it.** Trim only the two sentences that restate the `MinValue` rationale and the "both existing implementations carry this guard" list, and point at `ForwardWindowJoiner.IsWeekdayRow` instead.

---

## 5. Fixtures — family `A68` (verified free at `2da3872`)

⛔ **The two new fixtures exist to catch Trap 1 and nothing else. Both MUST contain a `MinValue` row — that is the input the fold gets wrong.**

⛔ **Run the mutation for real, per fixture, using the editor.** `python` is **not installed on this box** — a `python -c` mutation silently does nothing and the harness then reports PASS against unmutated code. ⛔ **And assert `Build succeeded` before believing any harness result** — a failed build leaves the harness running the stale binary.

| Fixture | Target | What it must assert |
|---|---|---|
| **`A68a`** | `LivePerformanceTracker.AggregateRange` | Entries: 1 Monday + 1 Saturday + 1 Sunday + **1 with `.Timestamp = DateTime.MinValue`**. Assert `TotalRange = 1` **AND `WeekendExcluded = 2`, not 3.** The `MinValue` row is dropped and **uncounted** |
| **`A68b`** | `CsvFeatureBuilder.LoadAndBuild` | A temp CSV with a weekday row, a weekend row, and a row whose timestamp is unparseable by `TryParseExact` (use the ISO `T` separator, as `A59e` does). Assert `stats.WeekendExcluded = 1`, **not 2** |

**Templates:** `A67a` (`verify/ordercheck/Program.vb:12572`) for the `AggregateRange` entry shape; `A59d` (`:1783`) and `A59e` (`:1806`) for the temp-CSV + `Try/Finally` delete shape.

⚠ **`A68b` is the effort cost in this build.** `CsvFeatureBuilder` has **no existing fixture coverage** — `A68b` will be the first. `LoadAndBuild(csvPath, ByRef stats)` returns a 3-tuple, and its filter chain drops rows that are not v0.8-placed and directional, so the CSV header must carry all four placed-level columns (`PlacedTargetLong`, `PlacedStopLong`, `PlacedTargetShort`, `PlacedStopShort`) plus a `MaxScore > 0` and a directional `Verdict`. **Build the CSV until an all-weekday version yields a non-zero row count first**, then add the weekend and `MinValue` rows — otherwise a fixture that reads `WeekendExcluded = 1` may be reading it off an empty parse and proving nothing. *(This is the `D3-RESIDUAL` lesson: a fixture built from the obvious input can be inert under both predicates.)*

### 5.1 The mutation proof — required, per fixture

For **each** of `A68a` and `A68b`:

1. Apply the **fold** mutation by hand — replace the two-line guard with the single `If Not IsWeekdayRow(...) Then WeekendExcluded += 1 : Continue For`.
2. Build. **Confirm `Build succeeded`.**
3. Run the harness. **The fixture MUST FAIL.** Paste the actual failure line into the spec-back.
4. Restore. ⛔ **Restore by FILE COPY, not `git checkout -- <file>`** — that also reverts uncommitted feature work in the same file, and it silently deleted `ApplySpread` on 2026-09-07.
5. Build, re-run, confirm PASS.

---

## 6. Acceptance criteria

These are **forward-looking** and cannot be run until the build exists. The implementer runs them and pastes actual output into the spec-back.

| # | Criterion |
|---|---|
| **AC-1** | `dotnet build` on the solution reports **`Build succeeded`, 0 errors 0 warnings** — and the same for `tools/AutoTweaker/AutoTweaker.vbproj`, `tools/CeilingAudit/CeilingAudit.vbproj` and `verify/ordercheck/OrderCheck.vbproj` |
| **AC-2** | The harness runs and reports **`ALL PASS`**. Count goes **337 → 339**. ⛔ **Measure by RUNNING it, never by grepping `Check(`** |
| **AC-3** | `A59a`, `A59d`, `A59e`, `A67a`, `A67b` all still **PASS, unchanged** — no assertion edited, no expected value moved |
| **AC-4** | `tools/checks/verify-gate.ps1 -Mode local-fast` reports **`GATE PASSED`** |
| **AC-5** | `grep -rn "DayOfWeek.Saturday\|DayOfWeek.Sunday" --include=*.vb .` returns exactly **four** lines: `analysis/ForwardWindowJoiner.vb`, `tools/BacktestRunner/CoverageReport.vb`, `UI/TweakSettingsForm.vb`, and the two-line harness assertion in `verify/ordercheck/Program.vb`. ⚠ **Paste the real output** — the harness assertion spans two lines, so the raw line count is five |
| **AC-6** | Both mutation proofs in §5.1 ran, **with pasted failure output** for each |
| **AC-7** | `settings.json` is **untouched**. No version bump, no `change_log` entry. **Verify with `git diff --stat`** — the file must not appear |
| **AC-8** | Mojibake check on any doc touched: `grep -n "â\|Ã\|â€" docs/*.md` returns nothing new |

### 6.1 Rules that apply to the commit

- ⚠ **Engine display-string parity rule.** No line emitted by `BuildPlaintextSnapshot` changes, and no card binding changes. **But `UI/MainForm_Layout.vb:1743` renders a value this build touches the computation of.** State explicitly in the commit message: *"`WeekendExcluded` semantics preserved exactly per `D-2` (a) — no rendered value moves; pinned by `A68a`."*
- ⚠ **`docs/DeribitIndicatorProject.md` §15.** This is a **no-engine-change refactor** — no version-history entry is owed. Tag the commit subject `[no-engine-change]`.
- ⚠ **Fixture-literal provenance rule.** `A68a`/`A68b` pass **no settings-derived value**, so the rule does not bite. The dates are calendar facts. Say so in a one-line comment rather than leaving it silent.

---

## 7. What to report back

Per [`batch-review-packet-convention.md`](batch-review-packet-convention.md), **two documents**:

- `docs/wd-tidy-batch-summary.md` — the outcome record
- `docs/wd-tidy-spec-back.md` — the review packet

⛔ **Rank handles by whether the READER can run them.** Label reader-runnable handles **`H-n`** and build-time evidence **`E-n`**, and never rank an `E-n` first. Every handle must be **run, with its actual output pasted, pinned to `2da3872`** — a handle that has not been run is a guess.

**Queue this as a follow-up row, do not build it here:** the §3.1 three-way `WeekendExcluded` disagreement. Name it, name the three surfaces, and state that resolving it moves a user-visible number and therefore needs its own spec and its own trader tick.

---

## 8. What this build must NOT do

- ⛔ Modify `ForwardWindowJoiner.IsWeekdayRow`
- ⛔ Touch `tools/BacktestRunner/CoverageReport.vb` or `UI/TweakSettingsForm.vb` (`D-3`, `D-4`)
- ⛔ Touch the harness's own Sat/Sun assertion at `verify/ordercheck/Program.vb:1670–1671` (`D-5`)
- ⛔ Change `MatchesWeekday`'s signature or accessibility
- ⛔ Move `ConditionsExtractor`'s fail-closed guards at `:141`
- ⛔ Change any `WeekendExcluded` value on any surface
- ⛔ Touch `settings.json`
- ⛔ Run any `git` write command — **commit is the orchestrator's, not the implementer's**
