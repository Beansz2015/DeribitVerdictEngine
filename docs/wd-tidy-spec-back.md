# `WD-TIDY` — spec-back (review packet)

**Reports against:** [`wd-tidy-weekday-predicate-convergence-spec.md`](wd-tidy-weekday-predicate-convergence-spec.md)
**Outcome record (the what-happened):** [`wd-tidy-batch-summary.md`](wd-tidy-batch-summary.md)
**Every handle below is pinned to baseline `2da3872`** and was **run**, with its real output pasted. Nothing here is a guess.
**Built on Opus / high**, substituted for the spec's Sonnet / medium after a quota exhaustion. Scope unchanged.

⛔ **Run from the repo root, `C:\Dev\DeribitVerdictEngine`.** The tree is **uncommitted** — these handles read the working tree, not a commit.

---

## 1. Ranked verification handles

**If you only run one, run `H-1`.** It is one command, it covers the whole build, and it is the only check that exercises the actual mutation this build exists to prevent.

### `H-1` — the harness: 339 and `ALL PASS`

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Debug 2>&1 | grep -cE '^PASS'
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Debug 2>&1 | tail -1
```

**Actual output:**

```
339
ALL PASS
```

⛔ **Count measured by running, never by grepping `Check(`.** Baseline was **337**, measured the same way at `2da3872` before any edit. **337 → 339, exactly as spec AC-2 requires.**

### `H-2` — the load-bearing pair, and the arithmetic that would expose a silent error

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Debug 2>&1 | grep -E '^(PASS|FAIL)  (A59a|A59d|A59e|A67a|A67b|A68a|A68b)'
```

**Actual output:**

```
PASS  A59a weekday rows survive, Sat/Sun excluded (5 rows -> 3 kept)
PASS  A59d ConditionsExtractor excludes weekend rows from the regime mix (UP:100, not UP:50/RB:50)
PASS  A59e malformed timestamp parses to MinValue and is excluded, not admitted as Monday
PASS  A67a perf strip excludes weekend rows — TotalRange counts weekdays only, and WeekendExcluded reports what was dropped
PASS  A67b IsWeekdayRow — Mon/Fri in, Sat/Sun out, and MinValue REJECTED despite its DayOfWeek being Monday
PASS  A68a WD-TIDY counter fold — the MinValue row drops SILENTLY: WeekendExcluded=2 (the fold reads 3), TotalRange=1
PASS  A68b WD-TIDY counter fold (audit) — the unparseable row drops SILENTLY: WeekendExcluded=1 (the fold reads 2), 1 row kept off 3 parsed
```

**The arithmetic identity that matters, and it is inside `A68b`:** `TotalRows = 3` and `rows kept = 1` and `WeekendExcluded = 1`. **`3 − 1 − 1 = 1`, and that 1 is the unparsed row — dropped and NOT counted.** If the fold were present the identity would read `3 − 1 − 2 = 0` and no row would be unaccounted for. **That missing 1 is the whole property.**

### `H-3` — all four projects build clean

```
dotnet build DeribitVerdictEngine.sln
dotnet build tools/AutoTweaker/AutoTweaker.vbproj
dotnet build tools/CeilingAudit/CeilingAudit.vbproj
dotnet build verify/ordercheck/OrderCheck.vbproj
```

**Actual output — all four identical in the lines that matter:**

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### `H-4` — the census (spec AC-5)

```
grep -rn "DayOfWeek.Saturday\|DayOfWeek.Sunday" --include=*.vb .
```

**Actual output — four sites, five raw lines, exactly as the spec predicted:**

```
./analysis/ForwardWindowJoiner.vb:135:        Return dow <> DayOfWeek.Saturday AndAlso dow <> DayOfWeek.Sunday
./tools/BacktestRunner/CoverageReport.vb:629:        If hourStartUtc.DayOfWeek = DayOfWeek.Saturday OrElse hourStartUtc.DayOfWeek = DayOfWeek.Sunday Then
./UI/TweakSettingsForm.vb:315:                If dow = DayOfWeek.Saturday OrElse dow = DayOfWeek.Sunday Then Continue For
./verify/ordercheck/Program.vb:1675:              Not kept.Any(Function(r) r.Timestamp.DayOfWeek = DayOfWeek.Saturday OrElse
./verify/ordercheck/Program.vb:1676:                                        r.Timestamp.DayOfWeek = DayOfWeek.Sunday),
```

`CoverageReport.vb` = `D-3`. `TweakSettingsForm.vb` = `D-4`. The two `Program.vb` lines = `D-5`, the harness's own independent copy, **deliberately still a copy**. **All four convergeable sites are gone.**

### `H-5` — the property the whole build turns on: the guard is still INLINE and still FIRST

⚠ **This asserts the executable line, not a comment mentioning it.** Both counter sites must show the `MinValue` guard immediately above the delegation.

```
grep -n -A1 "Timestamp = DateTime.MinValue Then Continue For" LivePerformanceTracker.vb tools/CeilingAudit/CsvFeatureBuilder.vb
```

**Actual output:**

```
LivePerformanceTracker.vb:761:            If e.Timestamp = DateTime.MinValue Then Continue For
LivePerformanceTracker.vb-762-            If Not ForwardWindowJoiner.IsWeekdayRow(e.Timestamp) Then
--
tools/CeilingAudit/CsvFeatureBuilder.vb:203:                If w.Row.Timestamp = DateTime.MinValue Then Continue For
tools/CeilingAudit/CsvFeatureBuilder.vb-204-                If Not ForwardWindowJoiner.IsWeekdayRow(w.Row.Timestamp) Then
```

**Two hits, and in each the delegation is the very next line.** A fold would remove the `:761` / `:203` line and this handle would return the delegation with no guard above it.

### `H-6` — the delegation census

⚠ **The comment filter is load-bearing and is NOT optional.** Six comments in these files now mention `ForwardWindowJoiner.IsWeekdayRow` by name — including ones this build wrote. **An unfiltered grep counts a name, not a call**, which is the failure `CLAUDE.md`'s *"test the property, not a string that mentions it"* rule exists for. `Function IsWeekdayRow` is matched separately so the declaration is counted once.

```
grep -rn "ForwardWindowJoiner\.IsWeekdayRow\|Function IsWeekdayRow" --include=*.vb . \
  | grep -v "^\./verify/" | grep -v ":[0-9]*: *'"
```

**Actual output:**

```
./analysis/AnalysisRunner.vb:45:            Function(r) ForwardWindowJoiner.IsWeekdayRow(r.Timestamp)).ToList()
./analysis/ForwardWindowJoiner.vb:132:    Public Shared Function IsWeekdayRow(ts As DateTime) As Boolean
./LivePerformanceTracker.vb:762:            If Not ForwardWindowJoiner.IsWeekdayRow(e.Timestamp) Then
./tools/AutoTweaker/AutoTweakerCore.vb:833:        Return ForwardWindowJoiner.IsWeekdayRow(r.Timestamp)
./tools/AutoTweaker/ConditionsExtractor.vb:150:                If Not ForwardWindowJoiner.IsWeekdayRow(rowTs) Then Continue For
./tools/CeilingAudit/CsvFeatureBuilder.vb:204:                If Not ForwardWindowJoiner.IsWeekdayRow(w.Row.Timestamp) Then
./tools/WhatIfRunner/WhatIfProgram.vb:97:        Dim weekdayRows = allRows.Where(Function(r) ForwardWindowJoiner.IsWeekdayRow(r.Timestamp)).ToList()
```

**Seven lines: one declaration (`ForwardWindowJoiner.vb:132`) plus six production call sites** — the two pre-existing surface-3 callers (`AnalysisRunner.vb:45`, `WhatIfProgram.vb:97`) and **the four this build converged**. Cross-check against `H-4`: four convergeable copies removed, four call sites added.

### `H-7` — the gate (spec AC-4)

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/verify-gate.ps1 -Mode local-fast
```

**Actual tail:**

```
ALL PASS
OK    harness ALL PASS

=== display-parity ===
OK    no snapshot/card drift detected

=== version-bump ===
OK    no engine-path change

=== result ===
GATE PASSED
```

⭐ **`no engine-path change` is the gate independently agreeing that no `docs/DeribitIndicatorProject.md` §15 entry and no `settings.json` bump are owed.**

### `H-8` — the prohibitions (spec AC-7, spec §8)

```
git diff --stat
```

**Actual output:**

```
 LivePerformanceTracker.vb                |  14 +++--
 tools/AutoTweaker/AutoTweakerCore.vb     |  20 +++---
 tools/AutoTweaker/ConditionsExtractor.vb |  17 ++---
 tools/CeilingAudit/CsvFeatureBuilder.vb  |   8 ++-
 verify/ordercheck/Program.vb             | 104 +++++++++++++++++++++++++++++++
 5 files changed, 137 insertions(+), 26 deletions(-)
```

⛔ **`settings.json` is absent. `analysis/ForwardWindowJoiner.vb` is absent. `tools/BacktestRunner/CoverageReport.vb` and `UI/TweakSettingsForm.vb` are absent.** Four spec §8 prohibitions, all held, in one command.

### `H-9` — mojibake (spec AC-8)

```
grep -n "â\|Ã\|â€" docs/*.md
```

**Actual output — four lines, and every one of them is this criterion quoting ITSELF:**

```
docs/wd-tidy-spec-back.md:176:grep -n "â\|Ã\|â€" docs/*.md
docs/wd-tidy-spec-back.md:183:docs/wd-tidy-spec-back.md:<n>:grep -n "â\|Ã\|â€" docs/*.md
docs/wd-tidy-spec-back.md:184:docs/wd-tidy-weekday-predicate-convergence-spec.md:239:| **AC-8** | ... |
docs/wd-tidy-weekday-predicate-convergence-spec.md:239:| **AC-8** | Mojibake check on any doc touched: `grep -n "â\|Ã\|â€" docs/*.md` returns nothing new |
```

⚠ **The pattern contains the very bytes it hunts, so any document stating the criterion self-matches — including this handle's own output block, recursively.** Baseline at `2da3872` was exactly one such self-match (the spec's `:239`). **`docs/wd-tidy-batch-summary.md` does NOT appear at all.**

**The decisive form — run this one:**

```
grep -n "â\|Ã\|â€" docs/*.md | grep -v "wd-tidy"
```

**Actual output: EMPTY, exit 1.** ✅ **No real mojibake anywhere in `docs/`.** The `⚠ ⛔ ⭐ · →` markers in both new documents survived intact — had any been mangled by a scripted edit they would carry `â` and appear here. (Both documents were written with `Write`, never `perl -0777 -i`.)

---

### Build-time evidence — the mutation proofs. ⚠ These are `E-n`: you must EDIT the tree to reproduce them.

Both are reproducible (the mutation is a single named line deletion in a committed file), but neither is a command you can paste, so neither is ranked above. Both were applied **with the editor** — `python` is not on this box — and both were restored by **file copy with MD5 verified**, never `git checkout`.

#### `E-1` — the `LivePerformanceTracker` fold

**Mutation:** delete `LivePerformanceTracker.vb:761`, `If e.Timestamp = DateTime.MinValue Then Continue For`.
**Build:** `Build succeeded. / 0 Error(s)` — asserted before believing the harness.
**Actual harness output:**

```
PASS=338 FAIL=1
PASS  A67a perf strip excludes weekend rows — TotalRange counts weekdays only, and WeekendExcluded reports what was dropped
PASS  A67b IsWeekdayRow — Mon/Fri in, Sat/Sun out, and MinValue REJECTED despite its DayOfWeek being Monday
FAIL  A68a WD-TIDY counter fold — the MinValue row drops SILENTLY: WeekendExcluded=2 (the fold reads 3), TotalRange=1 — TotalRange=1 (expected 1) WeekendExcluded=3 (expected 2; the fold gives 3)
PASS  A68b WD-TIDY counter fold (audit) — the unparseable row drops SILENTLY: WeekendExcluded=1 (the fold reads 2), 1 row kept off 3 parsed

1 FAILURE(S)
```

⭐⭐ **The most valuable line here is not the FAIL — it is `PASS A67a`.** The spec claimed the existing fixture is blind to the fold. **That is now measured, not asserted.** `A67a` passes while the rendered number is wrong.

**Restore:** file copy, `md5sum` → `003e14af5b99fa50831b9198d08aa48a`, matching the pre-mutation value; re-run → `PASS=339 FAIL=0 ALL PASS`.

#### `E-2` — the `CsvFeatureBuilder` fold

**Mutation:** delete `tools/CeilingAudit/CsvFeatureBuilder.vb:203`, `If w.Row.Timestamp = DateTime.MinValue Then Continue For`.
**Build:** `Build succeeded. / 0 Error(s)`.
**Actual harness output:**

```
PASS=338 FAIL=1
PASS  A68a WD-TIDY counter fold — the MinValue row drops SILENTLY: WeekendExcluded=2 (the fold reads 3), TotalRange=1
FAIL  A68b WD-TIDY counter fold (audit) — the unparseable row drops SILENTLY: WeekendExcluded=1 (the fold reads 2), 1 row kept off 3 parsed — rows=1 (expected 1) totalRows=3 (expected 3) weekend=2 (expected 1; the fold gives 2) nonV08=0 nonDir=0

1 FAILURE(S)
```

⭐ **`A68a` still passes**, which is what proves the two counter sites are **independently** pinned rather than one fixture covering both.

**Restore:** file copy, `md5sum` → `275929c583df7de21353678b87074361`, matching; re-run → `PASS=339 FAIL=0 ALL PASS`.

#### `E-3` — the `A68b` probe step

Before the weekend and `MinValue` rows were added, an **all-weekday** two-row CSV with the same header was asserted to yield a non-zero parse:

```
PASS  A68b PROBE all-weekday CSV yields a non-zero row count
```
with `rows=2 totalRows=2 weekend=0 nonV08=0 nonDir=0`. **Without this step `WeekendExcluded = 1` could have been read off an empty parse.** The probe form no longer exists in the tree — this is why it is `E`, not `H`.

---

## 2. Decisions queued

### `Q-1` — ⚠ **This build created five stale line-number pointers. Decide whether to fix them, and where.**

Adding the five-line `D-2 (a)` comment to `tools/CeilingAudit/CsvFeatureBuilder.vb` moved every line below it by **exactly +4** (measured: file 502 → 506 lines; `AtrTargetMultiplier` moved `:409` → `:413`). Five comments elsewhere cite that file by line and are now wrong:

| Pointer | Says | Correct now | Notes |
|---|---|---|---|
| `analysis/ForwardWindowJoiner.vb:119` | `CsvFeatureBuilder.vb:198` | **`:203`** | ⛔ spec §8 forbids this build touching this file |
| `analysis/ForwardWindowJoiner.vb:122` | `CsvFeatureBuilder.vb:199-200` | **`:204`** — and it is now **one** line, not two | ⛔ same file |
| `LivePerformanceTracker.vb:746` | `CsvFeatureBuilder.vb:199-200` | **`:204`**, one line | inside the `:736–:756` decision record the spec said to KEEP |
| `verify/ordercheck/Program.vb:12612` | `CsvFeatureBuilder.vb:198` | **`:203`** | `A67b`'s comment |
| `tools/CeilingAudit/CeilingAuditProgram.vb:90–92` | `:409` / `:408` / `:395` | **`:413` / `:412` / `:399`** | all three verified correct at `2da3872` and all three now off by 4 |

**Options.** (a) leave all five and accept the drift · (b) correct the numbers in place · (c) **de-line-number them** — drop the `:NNN` and cite the file and symbol only.

**My read, labelled a hypothesis: (c), in a separate commit, not this one.** Reasoning: this is the fifth pointer-drift instance the repo has recorded, and correcting numbers in place (option b) buys a fix that rots again on the next comment edit — this build is itself the proof. Option (c) is the only one that stops recurring. **But it is out of `WD-TIDY`'s scope**, it touches `analysis/ForwardWindowJoiner.vb` which spec §8 explicitly fences, and doing it silently inside a zero-behaviour-change tidy is exactly the unilateral-design-decision the spec-first rule forbids. **I did not fix any of the five.**

**Scoping, supplied without recommending it:** option (c) is 5 comment lines across 4 files, no executable line, no fixture. Option (b) is the same 5 lines with different text.

### `Q-2` — the follow-up row spec §7 requires. **Named, not built.**

**Proposed row id: `WD-SEMANTICS` — unify the three-way `WeekendExcluded` disagreement.**

The three surfaces do not mean the same thing by `WeekendExcluded` today, and `D-2` (a) deliberately preserves that:

| Surface | How computed | Does a `MinValue` row count as weekend? |
|---|---|---|
| `analysis/AnalysisRunner.vb:46` | `report.WeekendExcluded = loadedRows.Count - rows.Count` — a **subtraction** | ⚠ **YES.** The subtraction cannot tell why a row went |
| `LivePerformanceTracker.vb:762` | explicit counter, Sat/Sun branch only | **NO** — pinned by `A68a` |
| `tools/CeilingAudit/CsvFeatureBuilder.vb:204` | explicit counter, Sat/Sun branch only | **NO** — pinned by `A68b` |

`analysis/AnalysisReport.vb:18` states the subtraction contract explicitly: *"`TotalRows` is the POST-filter count, so `TotalRows` + `WeekendExcluded` = rows loaded."* (Read and confirmed at that line; spec §3.1 cites it as `:17–18`.)

⛔ **Resolving this moves a user-visible number on at least one of `UI/MainForm_Layout.vb:1743`, `tools/CeilingAudit/AuditReport.vb:84`, or the analysis report. It therefore needs its own spec and its own trader tick.** **I have no read on which of the three definitions should win** — that is a question about what the trader wants the number to mean, not a technical one. I can say the *cheapest* option is to make `AnalysisRunner` count explicitly rather than subtract, because it is the only surface whose contract is documented and therefore the only one whose change is auditable; I am not recommending it.

### `Q-3` — two files are LF-only in the worktree. **Almost certainly cosmetic; confirm and close.**

`tools/CeilingAudit/CsvFeatureBuilder.vb` and `verify/ordercheck/Program.vb` are **LF-only on disk**; the other three edited files are CRLF. `git` warns *"LF will be replaced by CRLF the next time Git touches it"* on both.

- **It is invisible in the commit.** `.gitattributes` gives `text=auto` and `core.autocrlf=true`, so git normalises to LF in the index either way. `git diff --numstat` shows `104 0` for `Program.vb` — a pure content addition, **not** a whole-file rewrite.
- ⚠ **I could not establish whether these two files were already LF-only at `2da3872`.** The Edit tool preserved CRLF on the other three files I edited the same way, which argues the LF was pre-existing — **but that is an inference, not a measurement**, and git's normalisation makes the prior worktree state unrecoverable without a re-checkout.
- **My read: leave it.** Converting worktree line endings changes nothing in the commit and would be churn.

⚠ **Worth recording separately: my first check of this was WRONG.** `grep -c $'\r$'` reported 506/506 CRLF for a file with **zero** CR bytes. `tr -cd '\r' | wc -c` gave the truth. **A `grep` for a line ending is not a measurement of line endings.**

---

## 3. Spec-back proper

### 3.1 What the spec got right, specifically

- ⭐⭐ **§0.1 Trap 1 is the reason this build is correct.** It did not say "be careful with the counters" — it named the exact mutation, named the two rendered strings it moves, and named the fixture that would fail to catch it. **`E-1` then measured `A67a` passing under the fold, precisely as predicted.** A spec that names the false-negative by name is worth more than one that names the risk.
- ⭐ **§5's *"build the CSV until an all-weekday version yields a non-zero row count FIRST"*** was the single most useful instruction in the document. `LoadAndBuild` has four separate early-exit paths ahead of the weekday guard; a fixture written straight to final form would plausibly have asserted `WeekendExcluded = 1` off an empty parse and proved nothing. **This instruction converted a likely silent-inertness bug into a two-minute step.**
- ⭐ **Ordering Edits 1–2 before 3–4 (§4)** meant the safe pair was independently confirmed at 337 / `ALL PASS` before the risky pair was touched. That is worth keeping in any spec with a mixed-risk edit list.
- **§2's re-measured census.** The spec caught that the inherited handover said "three copies" when there were four, and said so with the grep it used. The census was **correct as written** — I re-ran it and found the same eight rows.
- **§0.1 Trap 3's own correction** — *"the handover says 7 harness refs; the correct figure is 5 call occurrences plus one comment mention"* — is a small thing that saved a wrong number propagating into `AutoTweakerCore`'s replacement comment. I copied the corrected figure.

### 3.2 Which assumptions broke

⛔ **`A68a` cannot be built the obvious way, and the spec does not warn about it.** Spec §5 specifies `A68a`'s entries but says nothing about the **range window**. `AggregateRange` filters on `e.Timestamp < rangeStartUtc` **before** reaching the weekday guard, so a `DateTime.MinValue` entry is discarded by the *range* test under any realistic start — the fixture then passes under both the correct code and the fold, and proves **nothing**.

**What I substituted:** `rangeStartUtc = DateTime.MinValue`, so `MinValue < MinValue` is `False` and the entry survives to the code under test. Cost: the range test is no longer exercised by this fixture, which is fine — `A67a` covers it.

⭐ **This is the `D3-RESIDUAL` lesson recurring, and it is a general one: a fixture whose distinguishing input is a SENTINEL value must check that no earlier guard treats the sentinel specially.** `MinValue` is simultaneously "unparsed" and "the smallest possible timestamp", and range filters care about the second meaning. The spec's own §5 note cites `D3-RESIDUAL` — **it just applied it to `A68b`'s CSV shape and not to `A68a`'s range argument, where the trap was sharper.**

### 3.3 Where the spec was narrower than its own words

- **§4 Edit 4 says "keep the `:735–:756` decision record" and separately §8 says "do not touch `ForwardWindowJoiner.vb`".** Together those two instructions **guarantee** the `Q-1` pointer drift: the build must add lines to `CsvFeatureBuilder.vb`, and is forbidden from repairing two of the five references that breaks. The spec did not notice that its own edit list invalidates comments it protects. **Not a deadlock — the escape hatch is "report it" — but a future spec that adds a comment block to a file should ask what cites that file by line.**
- **§6.1's fixture-literal note says the rule "does not bite" because no settings value is passed.** True, but `A68b` passes `MaxScore = 19`, which **is** the shipped `regime_max_score.trending` value. It is not settings-*derived* here — nothing reads settings — but it is a number a reader could mistake for one, which is precisely the confusion the provenance rule exists to prevent. **I declared it MECHANISM in the fixture comment and said why.** A spec that says "the rule does not bite" is inviting the implementer to skip the comment; §6.1's last clause (*"say so in a one-line comment rather than leaving it silent"*) saved it.

### 3.4 Constraint pairs that nearly conflicted

- **"Delegate to the one predicate" vs "change no rendered number"** at the two counter sites. These only coexist because `IsWeekdayRow`'s `MinValue` branch is **unreachable** from those call sites — the inline guard consumes that input first. Spec §4 Edit 3 names this and calls it *"correct, not waste"*. ⚠ **Without that sentence the natural review reaction is "this guard is redundant, delete it"** — which is the fold. **The comment blocks I added at both sites restate it at the call site**, because that is where the deletion would be attempted.
- **"Run the mutation for real" vs "never `git checkout -- <file>`" vs "run no git write command."** Together these leave exactly one restore route: copy the file aside first, copy it back. I did that and verified both restores by MD5. **The hatch is named in spec §5.1 step 4; a spec omitting it would leave an implementer with no legal restore.**

---

## 4. What I did NOT verify

- ⛔ **Nothing was run in the live WinForms app.** `UI/MainForm_Layout.vb:1743` and `tools/CeilingAudit/AuditReport.vb:84` were verified **by reading the source**, and both do render `WeekendExcluded` as this build's comments claim. **I did not observe either string on screen.** The claim "no rendered value moves" rests on `A68a` / `A68b` plus the unchanged computation, not on a screenshot.
- ⛔ **`tools/AutoTweaker` and `tools/CeilingAudit` were built but never RUN end-to-end.** `ConditionsExtractor` is exercised by `A59d` and `MatchesWeekday` by `A59a` / `A59e`, but `CeilingAuditProgram` and `AutoTweakerProgram` (both carry their own `Main` and are outside the harness) were not executed against a real `analysis_log.csv`.
- ⛔ **`analysis/AnalysisRunner.vb:46`'s subtraction behaviour is quoted from spec §3.1, not measured.** I read the line and it is a subtraction; I did **not** run it to confirm a `MinValue` row is counted as weekend there. That claim is the spec's, carried forward unverified, and it is the premise of `Q-2`.
- ⛔ **The three-way disagreement's user impact is unquantified.** Nobody has counted how many `MinValue` rows exist in the live book, so the size of the discrepancy `Q-2` describes is unknown. It may be zero rows in practice.
- ⚠ **Whether the two LF-only files were already LF at `2da3872`** — see `Q-3`. Inferred, not measured, and the measurement is no longer available.
- **No commit was made and nothing was pushed.** `git status -sb` reads `## master...origin/master` with five modified files and three untracked docs. Every `git` command I ran was read-only.

---

## 5. Escalation

**The §0.2 trigger did not fire.** Preserving the counter semantics needed exactly what `D-2` (a) specifies — keep the inline `MinValue` line, delegate only the Sat/Sun test. No signature change, no overload, no `ByRef` output, no second predicate.

**The second §0.2 trigger did not fire either.** No fixture in the `A59*` or `A67*` families changed its result; no assertion was edited and no expected value moved.

⚠ **One thing made me pause and is reported rather than worked around: `Q-1`.** The build could not both do what §4 requires and leave the tree's line references correct, and repairing them crosses a §8 prohibition. **I stopped at the boundary and queued it.**
