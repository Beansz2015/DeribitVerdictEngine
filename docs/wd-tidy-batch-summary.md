# `WD-TIDY` — batch summary (outcome record)

**Spec:** [`wd-tidy-weekday-predicate-convergence-spec.md`](wd-tidy-weekday-predicate-convergence-spec.md)
**Review packet (the working document):** [`wd-tidy-spec-back.md`](wd-tidy-spec-back.md)
**Baseline commit:** `2da3872` — verified with `git rev-parse --short HEAD` before any edit; the tree was clean apart from the untracked spec.
**Built:** 2026-09-08 (UTC — checked with `date -u`, not the GMT+8 wall clock).
**Model actually used:** Opus / high. The spec's §0 recommends Sonnet / medium; the substitution was the orchestrator's, after the Sonnet quota was exhausted. **No scope changed with the tier.**

**Status: BUILT. NOT COMMITTED** — no `git` write command was run. Committing is the orchestrator's.

---

## 0. Findings that change how this document reads

⚠ **`F-1` — this build created FIVE stale line-number pointers, and did not fix them.** Adding the five-line `D-2 (a)` comment block to `tools/CeilingAudit/CsvFeatureBuilder.vb` shifted every line below it by **exactly +4** (verified by measurement, not assumed). Five comments elsewhere in the tree cite `CsvFeatureBuilder.vb` by line and are now wrong. **Two of them sit in `analysis/ForwardWindowJoiner.vb`, which spec §8 forbids this build from touching.** Full table and the corrected numbers: [`wd-tidy-spec-back.md`](wd-tidy-spec-back.md) §2 `Q-1`. **This is queued as a decision, not fixed unilaterally.**

⚠ **`F-2` — `A67a` was confirmed to be blind to the fold, by running it under the mutation.** The spec asserted this; it is now measured. Under the `LivePerformanceTracker` fold, `A67a` **PASSES** while `A68a` FAILS. The spec's core claim — *"the obvious one-line delegation compiles, passes every existing fixture, and silently changes two rendered numbers"* — is verified, not inherited.

⭐ **`F-3` — `A68a` is inert unless `rangeStartUtc` is `DateTime.MinValue`, and the spec does not say so.** `AggregateRange` applies its **range** filter before the weekday guard, so a `MinValue` entry under any realistic range start is dropped by the range test and never reaches the code under test. The fixture would then pass under both predicates. Named in the fixture's own comment block. This is the `D3-RESIDUAL` lesson recurring on a new surface.

---

## 1. The four edits

All four are exactly as spec §4 specifies. No signature changed, no overload added, no `ByRef` output, no second predicate — **the §0.2 escalation trigger did not fire.**

| # | File | Line (post-edit) | What changed |
|---|---|---|---|
| **Edit 1** | `tools/AutoTweaker/AutoTweakerCore.vb` | `:832–834` | `MatchesWeekday` body → `Return ForwardWindowJoiner.IsWeekdayRow(r.Timestamp)`. Signature and `Friend` accessibility unchanged. Comment block re-pointed; the "why this wrapper still exists" sentence kept |
| **Edit 2** | `tools/AutoTweaker/ConditionsExtractor.vb` | `:150` | Three lines → `If Not ForwardWindowJoiner.IsWeekdayRow(rowTs) Then Continue For`. ⛔ The D-3 fail-closed guard at `:143` and the `TryParseExact` call are **untouched** |
| **Edit 3** | `tools/CeilingAudit/CsvFeatureBuilder.vb` | `:198–207` | ⛔ **COUNTER SITE.** Inline `MinValue` guard kept at `:203`, and kept FIRST; only the Sat/Sun test delegates, at `:204`. Five-line `D-2 (a)` rationale comment added at `:198–202` |
| **Edit 4** | `LivePerformanceTracker.vb` | `:757–765` | ⛔ **COUNTER SITE.** Same shape against `agg.WeekendExcluded` / `e.Timestamp` — guard at `:761`, delegation at `:762`. The `:736–:756` decision record kept; only the two sentences restating the `MinValue` rationale were trimmed |

**Work order followed as spec §4 directs:** Edits 1 and 2 first → build → harness → `A59a` / `A59d` / `A59e` confirmed still PASS at 337 → only then the two counter sites.

### 1.1 Files changed

```
 LivePerformanceTracker.vb                |  14 +++--
 tools/AutoTweaker/AutoTweakerCore.vb     |  20 +++---
 tools/AutoTweaker/ConditionsExtractor.vb |  17 ++---
 tools/CeilingAudit/CsvFeatureBuilder.vb  |   8 ++-
 verify/ordercheck/Program.vb             | 104 +++++++++++++++++++++++++++++++
 5 files changed, 137 insertions(+), 26 deletions(-)
```

⛔ **`settings.json` does not appear. `analysis/ForwardWindowJoiner.vb` does not appear.** Both are spec §8 prohibitions and both hold.

---

## 2. The two new fixtures

| Fixture | Target | Asserts | Registered at |
|---|---|---|---|
| **`A68a`** | `LivePerformanceTracker.AggregateRange` | `TotalRange = 1` **AND `WeekendExcluded = 2`, not 3**, over Mon + Sat + Sun + `DateTime.MinValue` | called `verify/ordercheck/Program.vb:621`, defined `:12666` |
| **`A68b`** | `CsvFeatureBuilder.LoadAndBuild` | `rows = 1` **AND `TotalRows = 3` AND `WeekendExcluded = 1`, not 2**, over a temp CSV of Mon + Sat + an ISO-`T` unparseable row | called `verify/ordercheck/Program.vb:622`, defined `:12717` |

**`A68b` is the first fixture `CsvFeatureBuilder` has ever had.** It was built probe-first, as spec §5 requires: an all-weekday two-row CSV was written and asserted to yield a **non-zero** row count (`rows=2, weekend=0`, PASS) **before** the weekend and `MinValue` rows were added. Without that step a thin CSV returns an empty parse and `WeekendExcluded = 1` reads off nothing at all.

**Header the parse requires** (all load-bearing — `LoadAndBuild` returns early without the four placed columns, and drops rows with `MaxScore <= 0` or a non-directional `Verdict` **before** the weekday guard runs):

```
Timestamp,Price,Verdict,MaxScore,EffectiveLongScore,EffectiveShortScore,PlacedTargetLong,PlacedStopLong,PlacedTargetShort,PlacedStopShort
```

**Fixture-literal provenance:** neither fixture passes a settings-derived value. Dates are calendar facts (2026-01-01 is a Thursday, independently computed — 2024-01-01 Mon, +366 → 2025 Wed, +365 → 2026 Thu). `MaxScore = 19` and the placed prices are arbitrary shape-satisfiers. **MECHANISM**, stated in a comment at each fixture per spec §6.1.

---

## 3. Harness count

| Point | Count | Result |
|---|---|---|
| Baseline, `2da3872`, before any edit | **337** | `ALL PASS`, 0 FAIL |
| After Edits 1 + 2 only | **337** | `ALL PASS`, 0 FAIL |
| After all four edits, before fixtures | **337** | `ALL PASS`, 0 FAIL |
| **Final** | **339** | **`ALL PASS`, 0 FAIL** |

⛔ **Every count was measured by RUNNING the harness** (`grep -cE '^PASS'` over its real stdout), never by grepping `Check(`. **337 → 339 matches spec AC-2 exactly.**

---

## 4. Acceptance criteria

| # | Criterion | Result |
|---|---|---|
| **AC-1** | Solution + `AutoTweaker` + `CeilingAudit` + `OrderCheck` all `Build succeeded`, 0 warnings 0 errors | ✅ **PASS** — all four, verbatim output in [`wd-tidy-spec-back.md`](wd-tidy-spec-back.md) §1 `H-3` |
| **AC-2** | Harness `ALL PASS`, count 337 → 339, measured by running | ✅ **PASS** — 339 PASS / 0 FAIL |
| **AC-3** | `A59a` `A59d` `A59e` `A67a` `A67b` still PASS, unchanged | ✅ **PASS** — all five, no assertion edited, no expected value moved |
| **AC-4** | `verify-gate.ps1 -Mode local-fast` → `GATE PASSED` | ✅ **PASS** — also `no snapshot/card drift detected` and `no engine-path change` |
| **AC-5** | The Sat/Sun census returns exactly four sites (five raw lines) | ✅ **PASS** — real output pasted in the packet; the `A59a` assertion spans two lines as predicted |
| **AC-6** | Both mutation proofs ran, with pasted failure output | ✅ **PASS** — both failed as required, both restored by **file copy** with MD5 verified |
| **AC-7** | `settings.json` untouched, absent from `git diff --stat` | ✅ **PASS** |
| **AC-8** | Mojibake check returns nothing new | ✅ **PASS** — see the packet; the only matches are the AC-8 command quoting **itself** |

---

## 5. Mutation proofs (spec §5.1)

Both mutations were applied **with the editor** — `python` is not installed on this box and a `python -c` mutation would have silently done nothing. Both restores were done by **file copy from a backup taken before mutating**, never `git checkout -- <file>`, and each restore was confirmed by MD5 **and** by a clean 339 / `ALL PASS` re-run.

| Mutation | Fixture | Result | Sibling behaviour |
|---|---|---|---|
| Fold at `LivePerformanceTracker.vb` — delete the inline `MinValue` line | **`A68a`** | ⛔ **FAILED**, `WeekendExcluded=3` | **`A67a` still PASSED** — the spec's claim, now measured |
| Fold at `CsvFeatureBuilder.vb` — delete the inline `MinValue` line | **`A68b`** | ⛔ **FAILED**, `weekend=2` | `A68a` still PASSED — the two sites are independently pinned |

Verbatim failure lines: [`wd-tidy-spec-back.md`](wd-tidy-spec-back.md) §1 `E-1` / `E-2`.

---

## 6. Rules applied at commit time (spec §6.1)

- **Engine display-string parity rule.** No line emitted by `BuildPlaintextSnapshot` changes and no card binding changes — `verify-gate.ps1` reports `no snapshot/card drift detected`. **But `UI/MainForm_Layout.vb:1743` renders a value whose computation this build touches.** The commit message must therefore state: *"`WeekendExcluded` semantics preserved exactly per `D-2` (a) — no rendered value moves; pinned by `A68a`."*
- **`docs/DeribitIndicatorProject.md` §15.** No entry owed. This is a no-engine-change refactor; `verify-gate.ps1` independently reports `no engine-path change`. Tag the commit subject **`[no-engine-change]`**.
- **`settings.json`.** No version bump, no `change_log` entry, file untouched.

---

## 7. Follow-up queued, NOT built

**`WD-SEMANTICS` — the three-way `WeekendExcluded` disagreement.** Named and scoped in [`wd-tidy-spec-back.md`](wd-tidy-spec-back.md) §2 `Q-2`, from spec §3.1. Resolving it **moves a user-visible number on at least one surface**, so it needs its own spec and its own trader tick. `D-2` (a) deliberately preserves the disagreement and this build does not touch it.
