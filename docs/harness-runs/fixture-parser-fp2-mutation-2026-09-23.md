# Fixture parser — the `FP-2` mutation test, 2026-09-23 (UTC)

**Harness:** [`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1) at `ca566c5` (revision 4). **Driver:** [`../../tools/checks/selftest/fixture-parser-fp2-mutation.ps1`](../../tools/checks/selftest/fixture-parser-fp2-mutation.ps1) at `51ae1aa`. **Detector:** `FP-2` only (the name-against-assertion check), run with `-Fp2Only`. **Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md).

**What this is:** decision `FP-D3` in [`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §3 — *"Mutation. Corrupt a known-good fixture's name, confirm the detector flags it, restore."* It is the only route to known POSITIVES for `FP-2`. Close-list item 4 of the Jev programme.

⭐ **Per-item verdicts are reported here on purpose.** The truth is CONSTRUCTED: every mutated name is wrong by design. No seat baseline is needed, and nothing here contaminates a fresh population (next section).

---

## 1. The six, and why these six

Each fixture below was judged `name_matches` by a past `FP-2` run, and the seat agreed:

| Fixture | Past run |
|---|---|
| `A1_CvdSlopeRising` · `A3_MicroCvdWindowFromEnd` · `A65c_WideArmIsTestedFirst` | [`fixture-parser-clean-run-2026-09-22.md`](fixture-parser-clean-run-2026-09-22.md) — every agreement STABLE 5/5 |
| `A20a_CalcOfiRefactorEquivalence` · `A20b_CalcOfiEdgeCasesUnchanged` | [`fixture-parser-a20-a23b-run-2026-09-22.md`](fixture-parser-a20-a23b-run-2026-09-22.md) — STABLE 5/5 |
| `A23a_AggrVelSteadyRate` | [`fixture-parser-a23a-run-2026-09-22.md`](fixture-parser-a23a-run-2026-09-22.md) — rate 1.0 |

**The mutation.** Each name keeps its ID prefix, which keeps it unique in the file; the harness finds a Sub's body by name, and the first match wins. Each takes its descriptive part from another real fixture that tests an unrelated property. The truth: any verdict other than `name_matches` is a detection.

**`verify/ordercheck/Program.vb` was never edited.** The driver applies the six renames to a copy in a temp directory. It checks each rename hit exactly one declaration, that no new name collides with an existing Sub, and that the copy has the same line count. The harness then reads the copy through `-SourceFile`.

---

## 2. Result — 6 of 6 flagged

Run 2026-09-23 19:03:22 → 19:04:47 UTC. Key loaded from the gitignored `typesafe.local.env`.

| Original name | Mutated name | Verdict (plurality of 5) | Agreement rate | Mean top prob | Min top prob | Flagged |
|---|---|---|---|---|---|---|
| A1_CvdSlopeRising | A1_KellyInverseLeverage | name_overclaims | 1 | 0.65 | 0.58 | yes |
| A3_MicroCvdWindowFromEnd | A3_MonthRolloverSplitsAndHeadersOnCreateOnly | name_overclaims | 1 | 0.834 | 0.79 | yes |
| A20a_CalcOfiRefactorEquivalence | A20a_HotReloadReMergesAndDeleteReverts | name_overclaims | 1 | 0.888 | 0.86 | yes |
| A20b_CalcOfiEdgeCasesUnchanged | A20b_SequenceGapDetection | name_overclaims | 1 | 0.618 | 0.55 | yes |
| A23a_AggrVelSteadyRate | A23a_FundingMergeClipsOverreachButKeepsStored | name_overclaims | 1 | 0.856 | 0.83 | yes |
| A65c_WideArmIsTestedFirst | A65c_AbsorptionEpisodeLifecycle | name_understates | **0.6 — UNSTABLE** | **0.372** | 0.35 | yes |

```
SITES_JUDGED=6          FP2_BAD_VERDICTS=6      FP2_UNSTABLE=1      FP2_WAF_BLOCKED=0
SCOPE_JEV_CALLS=0       USAGE_INPUT_TOKENS=35826                    USAGE_OUTPUT_TOKENS=2509
WALL_TIME_SEC=66.99     HARNESS_EXIT=1
MUTATIONS_FLAGGED=6 of 6 (named the mismatch: 6)
REAL_PROGRAM_VB_UNCHANGED=True (sha256 674EB3C2423DFE32...)
GIT_DIFF_STAT_PROGRAM_VB=[]
```

- ✅ **The escalation trigger did NOT fire.** It was *"`FP-2` flags fewer than half of the mutations"*; it flagged all six.
- **Five of six are STABLE 5/5 on `name_overclaims`**, at a mean top probability of 0.62–0.89.
- ⚠ **`A65c` is the weak row.** Its plurality is `name_understates` at 3 of 5, and its mean top probability is 0.372. The other two samples' verdicts were NOT captured: the driver printed only the plurality. It now prints every sample's verdict, from the commit that adds this record. **So whether `A65c`'s other two samples said `name_matches` is unknown.**
- ⭐ **The agreement gate caught the weak row:** it is the only UNSTABLE row. That is `harness-shadow-mode-protocol.md` §4d working as ruled: a trust gate on the agreement rate, not on the probability band.
- **Cost:** 30 Jev calls; 35,826 input tokens; ≈ $0.0015 at the rate the 2026-09-23 funding run implies (226,167 tokens ≈ $0.0095). The rate is carried over, not checked against a price list.

---

## 3. ⚠ What this does NOT establish

- **`FP-2` on SUBTLE name faults.** Every mutation here names an entirely unrelated property: the easiest positive there is. A name that overclaims by one clause, like `A6_ObvNormalisation` in [`fixture-parser-clean-run-2026-09-22.md`](fixture-parser-clean-run-2026-09-22.md) §3.2, is the realistic fault. It is not tested here.
- **That the detector read the NAME.** Each body still carries its original `Check("A1 CVD ...")` titles, so "name versus body" is also "name versus the body's own titles". A detector that only compared those two strings would score the same.
- **A rate.** Six items, one run.
- **`A65c`'s sample split** (§2).
