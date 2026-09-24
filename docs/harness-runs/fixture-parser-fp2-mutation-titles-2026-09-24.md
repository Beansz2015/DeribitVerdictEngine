# Fixture parser — the `FP-2` mutation test with the titles renamed, 2026-09-24 (UTC)

**Harness:** [`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1) at `ad73b7e`. **Driver:** [`../../tools/checks/selftest/fixture-parser-fp2-mutation.ps1`](../../tools/checks/selftest/fixture-parser-fp2-mutation.ps1) at `ad73b7e`, run with `-RenameTitles`. **Detector:** `FP-2` only (the name-against-assertion check), `-Fp2Only`, 5 samples. **Resolved model:** `jev-1.13.0` on all 30 calls (requested `jev-latest`).

**Why this run exists.** The first run, [`fixture-parser-fp2-mutation-2026-09-23.md`](fixture-parser-fp2-mutation-2026-09-23.md), renamed each Sub but left its `Check("...")` title carrying the ORIGINAL name. That run's §3 says it could not tell whether `FP-2` read the body or only compared the name against the title. Close-list step 6 in [`../trader-tick-queue.md`](../trader-tick-queue.md) §2 owed this re-run.

**What changed from the first run.** Same six fixtures, same six wrong names, same 5 samples. In addition, each mutated Sub's one `Check` title is rewritten to spell out the NEW (wrong) name. The only evidence against the name is now the body's code and comments. Per-item verdicts are reported on purpose: the truth is constructed (every name is wrong), so no seat baseline is needed.

| Original name | Mutated name | Title after the rename |
|---|---|---|
| A1_CvdSlopeRising | A1_KellyInverseLeverage | `A1 Kelly inverse leverage` |
| A3_MicroCvdWindowFromEnd | A3_MonthRolloverSplitsAndHeadersOnCreateOnly | `A3 month rollover splits and headers on create only` |
| A20a_CalcOfiRefactorEquivalence | A20a_HotReloadReMergesAndDeleteReverts | `A20a hot reload re-merges and delete reverts` |
| A20b_CalcOfiEdgeCasesUnchanged | A20b_SequenceGapDetection | `A20b sequence gap detection` |
| A23a_AggrVelSteadyRate | A23a_FundingMergeClipsOverreachButKeepsStored | `A23a funding merge clips overreach but keeps stored` |
| A65c_WideArmIsTestedFirst | A65c_AbsorptionEpisodeLifecycle | `A65c absorption episode lifecycle` |

`verify/ordercheck/Program.vb` was never edited. The driver mutates a temp copy and the harness reads it through `-SourceFile`.

---

## 1. Result — 5 of 6 flagged (the first run: 6 of 6)

Run 2026-09-24 10:01:35 → 10:02:08 UTC. Key loaded from the gitignored `typesafe.local.env`.

| Mutated name | Verdict (plurality of 5) | Agreement | Mean top prob | Min top prob | Flagged | First run (titles NOT renamed) |
|---|---|---|---|---|---|---|
| A1_KellyInverseLeverage | name_overclaims | 1 | 0.692 | 0.65 | yes | name_overclaims · 1 · 0.65 |
| A3_MonthRolloverSplitsAndHeadersOnCreateOnly | name_overclaims | 1 | 0.75 | 0.7 | yes | name_overclaims · 1 · 0.834 |
| A20a_HotReloadReMergesAndDeleteReverts | name_overclaims | 1 | 0.896 | 0.87 | yes | name_overclaims · 1 · 0.888 |
| A20b_SequenceGapDetection | name_overclaims | 1 | 0.736 | 0.69 | yes | name_overclaims · 1 · 0.618 |
| **A23a_FundingMergeClipsOverreachButKeepsStored** | **name_matches** | **1 — STABLE** | 0.602 | 0.57 | **NO** | name_overclaims · 1 · 0.856 |
| A65c_AbsorptionEpisodeLifecycle | name_overclaims | 1 | 0.542 | 0.48 | yes | name_understates · **0.6, UNSTABLE** · 0.372 |

```
SITES_JUDGED=6   FP2_BAD_VERDICTS=5   FP2_UNSTABLE=0   FP2_WAF_BLOCKED=0   SCOPE_JEV_CALLS=0
JEV_MODEL requested=[jev-latest x30] resolved=[jev-1.13.0 x30]
USAGE_INPUT_TOKENS=35367   USAGE_OUTPUT_TOKENS=2500   WALL_TIME_SEC=14.95   HARNESS_EXIT=1
MUTATIONS_FLAGGED=5 of 6 (named the mismatch: 5)
REAL_PROGRAM_VB_UNCHANGED=True (sha256 674EB3C2423DFE32...)   GIT_DIFF_STAT_PROGRAM_VB=[]
```

- ⛔ **Fewer mutations are flagged than in the first run: 5 of 6 against 6 of 6.** This is a finding, not something to tune away.
- ⛔ **`A23a` is a STABLE WRONG row.** All 5 samples said `name_matches` for a steady-tape aggressor-velocity body named *"funding merge clips overreach but keeps stored"*. With the original title it was flagged 5 of 5 at 0.856. So on this fixture the title carried the detection. The body has no comments; its evidence is code only (`AggressorVelocityAccumulator`, `Fold`, `Snapshot`, `GrossFastUsdPerSec`).
- This is the `harness-shadow-mode-protocol.md` §4g class (stable and wrong) on `FP-2`, the first instance on that detector. The agreement gate cannot catch it: the row is 5 of 5. Only the constructed truth shows it.
- **`A65c` moved the other way:** UNSTABLE 3 of 5 `name_understates` in the first run, STABLE 5 of 5 `name_overclaims` now, at a lower mean top probability (0.542). One run each; do not read a trend into it.
- On the 5 flagged rows, mean top probability rose on 4 (A1, A20a, A20b, A65c) and fell on 1 (A3). So the title rename did not weaken the detections that survived.
- ✅ The escalation trigger in the driver (*"flags fewer than half"*) did not fire.
- **Cost:** 30 Jev calls, 35,367 input tokens, 14.95 s. The dollar figure is not verified against a price list.

## 2. What this does and does not establish

- **It establishes:** the title is part of what `FP-2` relies on. On 1 of 6 obviously-wrong names, removing the title's contradiction turned a stable detection into a stable miss.
- **It does not establish a rate.** Six items, one run each side. The 6/6 → 5/6 change is one item.
- **Residual cues remain in two bodies.** `A1` and `A3` keep comments that name their real subject (`cvdDivergence`, `dynamicPct`). Only the `Check` titles were renamed, as the close-list row specified.
- **Subtle faults are still untested.** Every mutation names an entirely unrelated property.
- **Comparability.** Harness at `ad73b7e` against `ca566c5` for the first run. The `FP-2` question and state are unchanged between the two commits; the only harness change on this path is the model tally line. The first run's resolved model was not recorded (the tally did not exist), so whether the model changed between the runs is **unknown**.
