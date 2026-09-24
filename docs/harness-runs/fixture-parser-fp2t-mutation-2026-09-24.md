# Fixture parser — `FP-2` against `FP-2t` (title-stripped arm), 2026-09-24 (UTC)

**Decision:** `SR-D6` in [`../jev-harnesses-second-reader-2026-09-24.md`](../jev-harnesses-second-reader-2026-09-24.md) §6, RULED YES by the trader 2026-09-24. **Fix:** `FP-D33` in [`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1) (revision 5) — `FP-2t` now asks the same question over the same Sub body beside `FP-2`, in the same `-Fp2Only` call, except every `Check("...")` title literal in that body is replaced with the neutral placeholder `"check"` before the call. `FP-2` alone still drives `FP2_BAD_VERDICTS` and the exit code, the same footing `FP-1v2` (`FP-D30`) holds beside `FP-1`.

**Driver:** [`../../tools/checks/selftest/fixture-parser-fp2-mutation.ps1`](../../tools/checks/selftest/fixture-parser-fp2-mutation.ps1) `-RenameTitles` (no new flag needed for `FP-2t` itself — it rides along automatically once the harness carries `FP-D33`). **Harness/driver commit:** working tree at this session's start commit `f7da21c`, uncommitted at run time (committed alongside this record).

**Fixtures and titles:** identical to [`fixture-parser-fp2-mutation-titles-2026-09-24.md`](fixture-parser-fp2-mutation-titles-2026-09-24.md) §0 — same six wrong names, same six rewritten `Check(...)` titles (matching the new, wrong name). `FP-2t`'s own input additionally strips even that rewritten title down to `"check"`, so its only evidence is the body's code and comments.

`verify/ordercheck/Program.vb` was never edited. The driver mutates a temp copy; the harness reads it through `-SourceFile`.

---

## 1. Result — FP-2: 5 of 6 flagged. FP-2t: 5 of 6 flagged, same five.

Run 2026-09-24 12:06:04 UTC. Key loaded from the gitignored `typesafe.local.env`. Resolved model `jev-1.13.0` on all 60 calls (30 `FP-2` + 30 `FP-2t`).

| Original name | Mutated name | FP-2 verdict (of 5) | FP-2 agreement | FP-2 mean top prob | FP-2t verdict (of 5) | FP-2t agreement | FP-2t mean top prob | Flagged (FP-2) |
|---|---|---|---|---|---|---|---|---|
| A1_CvdSlopeRising | A1_KellyInverseLeverage | name_overclaims | 1 | 0.708 | name_overclaims | 1 | 0.746 | yes |
| A3_MicroCvdWindowFromEnd | A3_MonthRolloverSplitsAndHeadersOnCreateOnly | name_overclaims | 1 | 0.772 | name_overclaims | 1 | 0.92 | yes |
| A20a_CalcOfiRefactorEquivalence | A20a_HotReloadReMergesAndDeleteReverts | name_overclaims | 1 | 0.886 | name_overclaims | 1 | 0.884 | yes |
| A20b_CalcOfiEdgeCasesUnchanged | A20b_SequenceGapDetection | name_overclaims | 1 | 0.732 | name_overclaims | 1 | 0.772 | yes |
| **A23a_AggrVelSteadyRate** | **A23a_FundingMergeClipsOverreachButKeepsStored** | **name_matches** | 1 | 0.596 | **name_matches** | **0.6 — UNSTABLE** | 0.404 | **NO** |
| A65c_WideArmIsTestedFirst | A65c_AbsorptionEpisodeLifecycle | name_overclaims | 1 | 0.576 | name_overclaims | 1 | 0.504 | yes |

```
SITES_JUDGED=6           FP2_UNSTABLE=0           FP2_BAD_VERDICTS=5       FP2_WAF_BLOCKED=0
FP2T_ITEMS_SAME_PLURALITY_AS_FP2=6 of 6            FP2T_WAF_BLOCKED=0
JEV_MODEL requested=[jev-latest x60] resolved=[jev-1.13.0 x60]
USAGE_INPUT_TOKENS=70521   USAGE_OUTPUT_TOKENS=5008   WALL_TIME_SEC=31.28   HARNESS_EXIT=1
MUTATIONS_FLAGGED=5 of 6 (named the mismatch: 5)
REAL_PROGRAM_VB_UNCHANGED=True (sha256 674EB3C2423DFE32...)   GIT_DIFF_STAT_PROGRAM_VB=[]
```

## 2. ⛔ The key question — does `FP-2t` flag `A23a`, which `FP-2` missed once its title was renamed?

**No.** Both arms miss it. `FP-2t`'s plurality is still `name_matches` for the steady-tape aggressor-velocity body renamed `A23a_FundingMergeClipsOverreachButKeepsStored`, exactly as `FP-2` calls it with the matching (wrong) title.

- **`FP-2t` IS less confident and less stable on this one row** — agreement drops from 1.0 (`FP-2`) to 0.6 (3 of 5, `UNSTABLE`), and mean top probability drops from 0.596 to 0.404, the lowest of any row in this run on either arm. Stripping the title moved the needle, just not past the plurality.
- **On the other five rows, `FP-2t` matches `FP-2`'s plurality exactly** (`FP2T_ITEMS_SAME_PLURALITY_AS_FP2=6 of 6` — including `A23a`, since plurality-vs-plurality is what that counter compares, not confidence). Title-stripping did not cost or gain a single flag anywhere else in this population.
- **Reading this against the two earlier runs:**

  | Run | A23a arm | Titles say | Verdict | Agreement | Mean top prob |
  |---|---|---|---|---|---|
  | [2026-09-23](fixture-parser-fp2-mutation-2026-09-23.md) | FP-2 | original (`AggrVelSteadyRate`, correct) | name_overclaims | 1 | 0.856 |
  | [2026-09-24 title-rename](fixture-parser-fp2-mutation-titles-2026-09-24.md) | FP-2 | rewritten (`FundingMerge...`, wrong, matches new name) | name_matches | 1 | 0.602 |
  | 2026-09-24 (this run) | FP-2 | same rewritten title | name_matches | 1 | 0.596 |
  | 2026-09-24 (this run) | **FP-2t** | **`"check"` (stripped)** | **name_matches** | **0.6, UNSTABLE** | **0.404** |

  The 2026-09-23 run proves the title CAN carry the detection (original, correct title → flagged). The title-rename run proves a WRONG, matching title can flag a miss into a stable pass. **This run shows the miss does not fully reverse when the title is removed altogether** — `FP-2t` reads the body alone (`AggressorVelocityAccumulator`, `Fold`, `Snapshot`, `GrossFastUsdPerSec`, no comments) and still leans `name_matches`, just with less conviction. The most consistent read: this body's own code evidence is genuinely weak for distinguishing "steady rate" from "funding merge clips overreach" — the title was pushing the same wrong answer as the body's default lean, not creating it from nothing.

## 3. What this does and does not establish

- **It establishes:** `FP-2t`'s agreement rate is a real, working signal here — the one row where the title's information was removed is the one row that went `UNSTABLE`, exactly the shape `harness-shadow-mode-protocol.md` §4d predicts (agreement rate separates classes; top probability alone would not have — 0.404 is not obviously lower than some passing rows in other runs).
- **It does not establish a rate.** Six items, one run per arm.
- **It does not reverse `FP-D33`'s status as informational.** `FP-2` alone still drives `FP2_BAD_VERDICTS`/`HARNESS_EXIT`; `FP-2t` is reported beside it only. This run is exactly the kind of "fresh, baselined population" re-measure `FP-1v2`'s header comment describes arming — here, immediately, on constructed-truth data, not a fresh operator baseline (the truth is constructed, so no seat baseline is needed, per `FP-D3`'s original design carried into this run).
- **Cost:** 60 Jev calls (30 `FP-2` + 30 `FP-2t`), 70,521 input tokens, 5,008 output tokens, 31.28 s.
