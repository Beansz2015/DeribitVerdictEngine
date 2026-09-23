# Spec-back — `rider-travel.ps1` single-element unwrap fix

**Date:** 2026-09-23 (UTC) · **Base commit:** `23875fe` · **State:** ⚠ **UNCOMMITTED** in worktree `sharp-carson-96658f`, branch `claude/eager-ramanujan-b78b90`. One file changed: `tools/checks/rider-travel.ps1` (+7 / −3).

**The "spec" was a trader-relayed bug report, not a `docs/` spec.** It said that `Get-LedgerRows` in `tools/checks/rider-travel.ps1` returns a one-element list that PowerShell 5.1 unwraps to a bare `PSCustomObject`. As a result, `RIDERS_IN_LEDGER` and `RIDERS_TRAVELLING` print `0`.

**Advisory tooling only.** `docs/rider-travel-check-spec.md` D-3 (the "advisory, not wired into `verify-gate.ps1`" decision) still holds. No engine, settings, scoring, CSV or rendered-value effect. This is the auto-proceed class in `CLAUDE.md` ("undone by ONE revert with no live-surface and no data effect").

> **Review recommendation**
> **Model: Sonnet · Effort: low.**
> Why that tier: the diff has 3 executable lines, and `H-1` below reproduces the before-and-after matrix in one command.
> Where it could slip: reading `H-1` row `one` as "fine" because `RIDERS_IN_LEDGER=2` is right. The wrong value on that row is `RIDERS_TRAVELLING=0`.
> Escalation trigger: `H-1` prints anything other than the pasted matrix. Then move up to Opus/medium.

---

## 0. What changed, and the finding the report missed

| # | Site in `tools/checks/rider-travel.ps1` | Defect | Fix |
|---|---|---|---|
| 1 | `Get-LedgerRows` → `return $rows` | A one-row `List[object]` unwraps to a bare `PSCustomObject`. `.Count` is `$null` in 5.1 | `return ,$rows` (unary comma) |
| 2 | `$travelling = if (…) { @() } else { @(…) }` | The if-expression unwraps its output again, so the inner `@()` does not survive one match | `$travelling = @(if ($null -ne $allRows) { … })` |
| 3 | ⭐ **`$others = if (…) { @() } else { @(…) }`** — **NOT in the report** | Same unwrap. **With exactly ONE non-travelling row, `$others.Count -gt 0` is `$null -gt 0` = false. `NON_TRAVELLING_RIDERS` and the report's non-TRAVELLING table silently DROP the row** | Same `@(if …)` form |

⛔ **Site 3 is worse than the reported defect.** Sites 1 and 2 misprint a count. Site 3 drops a real row with no signal.

⭐ **The trigger is broader than "a one-row ledger".** It fires on **exactly one TRAVELLING row** (site 2) or **exactly one non-travelling row** (site 3), whatever the ledger size. `H-1` row `one` shows this: a 2-row ledger prints `RIDERS_TRAVELLING=0`. This is a realistic future state:
- `docs/csv-rotation-riders.md` §1 has 2 non-travelling rows today. Archive either one to that doc's §2, and site 3 fires.
- After the S2 rotation consumes `RIDER-1`…`RIDER-7` and `RIDER-9`, a later rotation that carries only `RIDER-8` (the cross-venue lead-lag columns) is exactly one TRAVELLING row. That rotation would print `RIDERS_TRAVELLING=0` without this fix.

---

## 1. Ranked verification handles

All handles are pinned to base `23875fe` plus the uncommitted diff. **Every handle below was run, and its output is pasted verbatim.**

**If you run only one, run `H-1`.** It covers all three sites, the 0-row path, the 2+-row path, and the before/after comparison.

### `H-1` — before/after matrix on synthetic ledgers (reader-runnable)

Bash, from the repo root. It writes only to `%TEMP%\rider-travel-h1` and to a temporary `tools/checks/rider-travel.base.ps1` (the `23875fe` copy, which must sit beside `lib/` for `$PSScriptRoot`). It deletes both at the end. Every rider names its column, so the code decides it and **no Jev call or API key is needed**. Each run goes all the way to the markdown report.

```bash
R=$(git rev-parse --show-toplevel); T="$(cygpath -m "$TEMP")/rider-travel-h1"; mkdir -p "$T"
sed 's/"Timestamp,Price,/"Timestamp,ZzTestCol,Price,/' "$R/AnalysisLogger.vb" > "$T/after.vb"
L='## 1. The ledger\n\n| ID | Rider | Source ruling | Status | Lands in |\n|---|---|---|---|---|\n'
A='| `RIDER-A` | `ZzTestCol` column | x | TRAVELLING | header |\n'
B='| `RIDER-B` | `Timestamp` column | x | TRAVELLING | header |\n'
C='| `RIDER-C` | other | x | CONDITIONAL | - |\n'
printf "$L\n## 2. Archive\n"       > "$T/zero.md"
printf "$L$A\n## 2. Archive\n"     > "$T/solo.md"
printf "$L$A$C\n## 2. Archive\n"   > "$T/one.md"
printf "$L$A$B$C\n## 2. Archive\n" > "$T/two.md"
echo '{ "RIDER-A": "arrived", "RIDER-B": "arrived" }' > "$T/b.json"
git -C "$R" show 23875fe:tools/checks/rider-travel.ps1 > "$R/tools/checks/rider-travel.base.ps1"
for S in rider-travel.base rider-travel; do for M in zero solo one two; do
  out=$(powershell -NoProfile -File "$R/tools/checks/$S.ps1" -AfterFile "$T/after.vb" -LedgerPath "$T/$M.md" -BaselinePath "$T/b.json" -OutPath "$T/rep.md" 2>&1); rc=$?
  printf '%-18s %-4s exit=%s  %s  %s  NON_TRAVELLING_listed=%s  report_cell=[%s]\n' "$S" "$M" "$rc" \
    "$(echo "$out" | grep -m1 '^RIDERS_IN_LEDGER')" "$(echo "$out" | grep -m1 '^RIDERS_TRAVELLING')" \
    "$(echo "$out" | grep -c 'RIDER-C status')" "$( [ -f "$T/rep.md" ] && grep '| RIDERS_TRAVELLING |' "$T/rep.md")"
  rm -f "$T/rep.md"
done; done
rm -f "$R/tools/checks/rider-travel.base.ps1"; rm -rf "$T"
```

Actual output:

```
rider-travel.base  zero exit=2  RIDERS_IN_LEDGER=0  RIDERS_TRAVELLING=0  NON_TRAVELLING_listed=0  report_cell=[]
rider-travel.base  solo exit=0  RIDERS_IN_LEDGER=0  RIDERS_TRAVELLING=0  NON_TRAVELLING_listed=0  report_cell=[| RIDERS_TRAVELLING |  |]
rider-travel.base  one  exit=0  RIDERS_IN_LEDGER=2  RIDERS_TRAVELLING=0  NON_TRAVELLING_listed=0  report_cell=[| RIDERS_TRAVELLING |  |]
rider-travel.base  two  exit=0  RIDERS_IN_LEDGER=3  RIDERS_TRAVELLING=2  NON_TRAVELLING_listed=0  report_cell=[| RIDERS_TRAVELLING | 2 |]
rider-travel       zero exit=2  RIDERS_IN_LEDGER=0  RIDERS_TRAVELLING=0  NON_TRAVELLING_listed=0  report_cell=[]
rider-travel       solo exit=0  RIDERS_IN_LEDGER=1  RIDERS_TRAVELLING=1  NON_TRAVELLING_listed=0  report_cell=[| RIDERS_TRAVELLING | 1 |]
rider-travel       one  exit=0  RIDERS_IN_LEDGER=2  RIDERS_TRAVELLING=1  NON_TRAVELLING_listed=1  report_cell=[| RIDERS_TRAVELLING | 1 |]
rider-travel       two  exit=0  RIDERS_IN_LEDGER=3  RIDERS_TRAVELLING=2  NON_TRAVELLING_listed=1  report_cell=[| RIDERS_TRAVELLING | 2 |]
```

How to read it. Ledgers: `zero` = 0 rows · `solo` = 1 TRAVELLING · `one` = 1 TRAVELLING + 1 other · `two` = 2 TRAVELLING + 1 other.

| Property | Base rows | Fixed rows |
|---|---|---|
| 0-row ledger stays `LEDGER_PARSE_FAILED`, exit 2 | `zero` exit=2 | `zero` exit=2 — **unchanged** |
| Site 1 (`RIDERS_IN_LEDGER`) | `solo` prints 0 | `solo` prints 1 |
| Site 2 (`RIDERS_TRAVELLING`) | `solo`, `one` print 0; the report cell is **blank**, not 0 | 1 · 1 |
| Site 3 (non-travelling row listed) | `one`, `two` = 0 — **dropped even in the 3-row ledger** | 1 · 1 |
| The report's claim that the parse gate does not fire on one row | `solo` exit=0 — **confirmed** | exit=0 |
| 2+ travelling rows | `two` = 3 / 2 | `two` = 3 / 2 — **unchanged** |

⚠ `NON_TRAVELLING_listed=0` on the `solo` row is correct, because that ledger has no non-travelling row.

### `H-2` — the real ledger still parses

```bash
powershell -NoProfile -File tools/checks/rider-travel.ps1 -AfterFile AnalysisLogger.vb | grep -E "^RIDERS_IN_LEDGER|^RIDERS_TRAVELLING|^EXIT_REASON"
```

```
RIDERS_IN_LEDGER=10
RIDERS_TRAVELLING=8
EXIT_REASON=NO_ROTATION
```

`NO_ROTATION` is expected: `-AfterFile` is the unrotated header. It stops before the baseline check and makes no Jev call. The 8 travelling rows match the count in the bug report.

### `H-3` — the PowerShell 5.1 mechanism behind each fix choice

```bash
powershell -NoProfile -Command 'function f { return $null }; function g { $l = New-Object System.Collections.Generic.List[object]; $l.Add([PSCustomObject]@{a=1}); return $l }; "at(null).Count=" + @(f).Count; "g().Count=[" + (g).Count + "]"; $x = if ($true) { @([PSCustomObject]@{a=1}) }; "ifexpr.Count=[" + $x.Count + "]"'
```

```
at(null).Count=1
g().Count=[]
ifexpr.Count=[]
```

- `g().Count=[]` is the site 1 defect. `ifexpr.Count=[]` is the site 2 and site 3 defect: the inner `@()` does not survive.
- `at(null).Count=1` shows why the report's alternative `$allRows = @(Get-LedgerRows …)` was **not** taken. See §3 of this doc.

### `H-4` — scope of the diff

```bash
git status --short; git diff --stat
```

```
 M tools/checks/rider-travel.ps1
 tools/checks/rider-travel.ps1 | 10 +++++++---
 1 file changed, 7 insertions(+), 3 deletions(-)
```

(Run before this spec-back was written. This doc adds one untracked file.)

---

## 2. Decisions queued

| ID | Decision | Options | My read |
|---|---|---|---|
| `RT-U1` | Commit the fix | (a) commit on this branch with `[no-engine-change]` · (b) hold | **(a).** Auto-proceed class. It is uncommitted only because the trader did not ask for a commit |
| `RT-U2` | Harden `$beforeCols` / `$afterCols`. Both use the same `x = if (…) { $h.Split(',') } else { @() }` shape | (a) wrap each in `@(if …)` · (b) leave them | ⚠ **Reserved under `CLAUDE.md`'s three-step test.** My reason for leaving them is *"a single `string` has an intrinsic `.Count` = 1 in 5.1, so a one-column header does not misprint"*. That is the **"adequate" tell**. (a) guarantees more and costs 2 × `@()`. **Lean: (a)**, for one uniform idiom. It is not needed for correctness today. I did not apply it |
| `RT-U3` | Commit `H-1` as a regression script | (a) under `tools/checks/measure/` (it holds `decision-bias` and `doc-scanner` today) · (b) this doc only | **No read.** Whether advisory tools get committed regression scripts is the orchestrator's criterion. Scope: one new ~20-line `.sh`, no other file |
| `RT-U4` | Sweep the sibling scripts for the same two shapes | (a) a low-effort sweep of `commit-walker.ps1`, `fixture-parser.ps1`, `doc-scanner.ps1`, `rotation-riders.ps1`, `lib/*.ps1` · (b) skip | **(a).** `fixture-parser.ps1` is armed for a one-shot run, and a silently dropped row there spends a population. See §4 of this doc for the small part I checked |

`RT-U2` and `RT-U4` share a root: one idiom (`@(if …)` / `,$list`) across the `tools/checks` family. Rule them together.

---

## 3. Feedback on the bug report

**What it got right:**
- The diagnosis was exact. `H-3` confirms both unwrap points.
- It was careful about the gate. It said the parse-failure gate does not fire because `$null -eq 0` is false. `H-1` base `solo` exit=0 confirms this.
- The `,$rows` suggestion was taken as written.
- The instruction to *"read the full file first to avoid missing a second instance"* found site 3.

**Where it was wrong or narrower than its own words:**
- ⛔ **The alternative fix `$allRows = @(Get-LedgerRows …)` is mechanically wrong.** `@($null).Count` is 1 (`H-3`). A missing ledger would print `RIDERS_IN_LEDGER=1`. The gate would still fire, because no row matches `TRAVELLING`, so the damage is a misprint. The report did hedge: it offered this "with `Get-LedgerRows` returning `$null` only when the file/heading truly can't be found". But the wrapped call site erases exactly that distinction.
- ⭐ **The trigger was scoped as "the parsed ledger has EXACTLY ONE row".** The real triggers are exactly one TRAVELLING row or exactly one non-travelling row (§0 of this doc). The narrower scope made the bug look unreachable at today's ledger. It is reachable by one archive edit.
- It named only the console coverage lines. The markdown report's cells print **blank**, not `0` (`H-1` `report_cell`). The report table reads the raw variables, not the `[int]`-coerced `Write-Coverage` parameters.

---

## 4. What I did not verify

| Item | Why not |
|---|---|
| The Jev-judged path with a single `JEV` rider | It needs `TYPESAFE_API_KEY` and spends real calls. `$jevRiders` is built with a direct `@(…)` assignment, which is already safe, and the diff does not touch it. Inferred safe, not run |
| PowerShell 7 behaviour | Not run. `PSCustomObject` gains an intrinsic `.Count` in 7, so the defect likely does not reproduce there. The script is `#requires -Version 5.1` and the usage line runs `powershell` (5.1) |
| The sibling scripts (`RT-U4`) | **Partly checked.** Three `return $<var>` sites: `commit-walker.ps1` `Get-AppCompiledToolsFiles` returns an array, but its only caller wraps it with `@()`, so it is safe. `fixture-parser.ps1` returns `$ctx` and `$result`, both single objects by design. **The if-expression form was not swept in any sibling** |
| Any other array assignment in `rider-travel.ps1` | **Checked by a full read of all 551 lines.** Every other one is a direct `@(…)` assignment or a `List`. `$beforeCols` / `$afterCols` are the only other if-expression arrays (`RT-U2`) |

---

## 5. Orchestrator review and rulings — 2026-09-23 (UTC)

**Accepted.** `H-1` re-run on the fixed file reproduced the fixed rows exactly (`zero` exit 2 · `solo` 1/1 · `one` 2/1 + 1 listed · `two` 3/2 + 1 listed). Site 3 is a real silent-drop and the most useful catch in this packet.

| ID | Ruling | Why |
|---|---|---|
| `RT-U1` | **(a) commit** | Auto-proceed class |
| `RT-U2` | **(a) applied by the orchestrator:** `$beforeCols` / `$afterCols` now use `@(if …)` | It guarantees more at the cost of two wrappers. No trade exists, so the "adequate" reason loses. `H-1` (with `COLUMNS_ADDED=1` on every synthetic ledger) and `H-2` (`10` / `8` / `NO_ROTATION`) were re-run after the edit and are unchanged |
| `RT-U3` | **(b) this doc only** | The handle is already committed text and reader-runnable from here. A second copy as a `.sh` adds a file that can drift from this one. Nothing is given up |
| `RT-U4` | **(a), but assigned to the second reader** (close-list item 6 in `trader-tick-queue.md` §2) | `fixture-parser.ps1` is being edited by the harness 3 batch right now, so a concurrent sweep would collide. The second reader reviews every `tools/checks` harness after that batch lands, which is the natural place for one idiom sweep |
