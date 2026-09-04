# A54a follow-up — R-2 (dictionary completeness), R-1 (stale seed comment), R-3 (degraded-path ROC)

**Status:** ✅ **BUILD-AUTHORIZED. Everything is ruled — R-2 = (b), R-1 rides it, and
`D-R3` = (i) SEED THE NULLABLES (trader-directed 2026-09-04). Nothing is owed.**

**Parent:** [`a54a-json-poco-drift-guard-spec.md`](a54a-json-poco-drift-guard-spec.md).
**Findings:** [`a54a-drift-guard-spec-back.md`](a54a-drift-guard-spec-back.md) §5.3.
**Rulings:** trader-directed 2026-09-04 — **R-2 = option (b)**, **R-1 rides it**, **record R-3**.

⚠ **ALL DATES UTC.** The workstation is GMT+8.

---

## 0. Model + effort

**Model: Sonnet · Effort: MEDIUM · one session.**

**Why medium, not high.** The judgment work is done and every piece has an in-repo template:
the seed edit mirrors an existing seeded-nullable block twelve lines away
(`AggressorVelocitySettings.Sessions`), the `JsonOnly` change is one `Else` arm in a walk that
already exists and is fixture-covered, and the R-3 fixture is a three-line assertion against a
shipped resolver. **Nothing here is a derivation.**

**Where a Sonnet seat will slip — three named traps:**

1. ⛔ **Assuming (b) alone fixes R-3. It does not, and for LONDON it makes the number worse.**
   §2's table is the arithmetic. Read it before touching the seed.
2. ⚠ **Shipping the `JsonOnly` change without checking the other THREE dictionaries.**
   `EngineSettings` has four dict properties, not one — `ResolutionProfiles` (`:113`),
   `AggressorVelocitySettings.Sessions` (`:487`), `AbsorptionSettings.Sessions` (`:566`),
   `StructuralLevelsSettings.Sessions` (`:1062`). **Measure all four before and after**; the
   spec author's probe says they match today, **but that is a 2026-09-04 measurement, not a
   property.**
3. ⚠ **Expecting `Compared` to move. It should NOT.** ⛔ *(This trap read "the seed edit adds
   new comparable scalars, so `Compared` moves off 261" until 2026-09-04 — **that was the spec
   author's, and it was wrong.** Corrected in place; the reasoning is below and it is a
   falsifiable prediction, not a measurement.)* **Every property the seed edits touch is
   `Double?`** — both `ResolutionProfile` properties and both seeded `SessionBucketSettings.RocMagnitudeThreshold`
   values — **and step 3 skips nullables before comparing.** So the walk should report:

   | Counter | Before | **Predicted after (b) + (i)** |
   |---|---:|---:|
   | `Compared` | 261 | **261 — unchanged** |
   | `Skipped` | 21 | **25** (+2 per recursed `ResolutionProfile`) |
   | `Orphans` / `JsonOnly` | 0 / 0 | **0 / 0** |

   ⭐ **`Compared` staying at 261 is the interesting part and is worth asserting:** the dict
   recursion adds reach without adding a single comparison, because everything inside it is
   nullable. ⚠ **If `Compared` moves, something is wrong — report it, do not adjust the
   floor.**

**⛔ ESCALATION TRIGGER.** If closing R-3 requires seeding a nullable whose absence is
*load-bearing somewhere else* — i.e. some code path keys on `HasValue` rather than reading the
value, the way `HasExplicitAggrVelBurstThreshold` arms a session — **stop.** Seeding it would
arm behaviour, not correct a default. ⭐ **Checked for `session_volume.sessions[].roc_magnitude_threshold`
2026-09-04 and it does NOT fire**: the only reader is `ResolveRocMagnitudeForHour`
(`Core/ExecutionResolution.vb:79-83`), which uses `HasValue` purely as inherit-or-not, with no
arming semantics. **Re-check rather than inherit that.**

---

## 1. R-2 — RULED option (b), plus (a) riding it free

**(b) Populate the `ResolutionProfiles` POCO seed to mirror shipped**
(`Core/Settings/EngineSettings.vb:113`):

```
{"1", New ResolutionProfile()},
{"3", New ResolutionProfile With {.RocMagnitudeThreshold = 0.21, .RocSlopeDeltaThreshold = 0.06}}
```

⚠ **This overrides a stated design intent and the comment must move with it.** The current
comment reads *"Default-empty dict so an absent block = pure 1-min behaviour."* That was
coherent while the session seed said `execution_resolution = 1`. **After D-2 it is not** — see
§2. Rewrite the comment; do not append to it (spec-back §5.3 R-1 is that same defect).

**(a) rides free.** With the keys matching on both sides, add the missing `Else` arm to the
walk's step 6 so an unmatched JSON dict key is recorded as `JsonOnly`:

- **Today** the branch is `If A62TryGetPropertyCI(...) Then recurse` with no `Else`, so a JSON
  dict key with no POCO counterpart is **neither compared nor reported**. The `JsonOnly` tail
  only runs *inside* `WalkPocoVsJson`, which is never entered for that key.
- **Consequence:** the walk's own comment — *"This half proves the POCO is COMPLETE"* — is
  true at object level and **false inside dictionaries**.
- ⭐ **After (b), `A62a` still asserts `JsonOnly = 0` and still passes**, because the only two
  unmatched keys in the tree were `resolution_profiles["1"]`/`["3"]` and (b) removes them.
  **That is the whole reason (b) comes first.**

⚠ **Direction matters and only one direction is ruled here.** (a) covers *JSON key with no
POCO counterpart*. The reverse — a **POCO** dict key absent from JSON — is still skipped
silently. **Measure it; if any exist today, report the count and stop rather than inventing a
rule for it.**

**D-R3 (i) — seed the per-session ROC nullables**, in the same `SessionVolumeSettings.Sessions`
initialiser (`EngineSettings.vb:673-683`):

| Bucket | `RocMagnitudeThreshold` | Why |
|---|---|---|
| ASIA | `0.17` | mirrors shipped |
| LONDON | `0.11` | mirrors shipped |
| **NY** | ⛔ **leave `Nothing`** | shipped carries **no** NY override — seeding one invents a value |

⚠ **Carry the `AggressorVelocitySessionOverride` comment's discipline across:** say at the
declaration that these **MUST mirror `settings.json`**, and say that **no guard enforces it**
(§3) — only `A63a` does.

**R-1 rides this commit** — `SessionVolumeSettings.Sessions`' comment still opens *"Default
buckets aligned to live **v30** (ASIA/LONDON/NY)"* against buckets now aligned to **v68**.
⛔ **Edit that opening line. Do not append a correction below it** — that is the exact defect
handle 9 caught, and this is the very line [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md)
**S-3** quoted as its evidence.

---

## 2. R-3 — recorded, and ⛔ the spec author's own recommendation was HALF WRONG

**The finding.** D-2 synced ASIA and LONDON `ExecutionResolution` 1 → 3 in the POCO seed. On
the parse-failure path the engine therefore runs **3-minute bars**. But
`ResolveRocMagnitudeForHour` (`Core/ExecutionResolution.vb:79-83`) resolves
bucket-override → `resolution_profiles` → global base, and **both of the first two are empty
in the seed** — the bucket overrides are `Double?` = `Nothing`, and `ResolutionProfiles` is an
empty dict. So it falls through to the **1-minute globals** (`magnitude_threshold` 0.1,
`slope_delta_threshold` 0.05). **Three-minute bars scored against one-minute ROC thresholds.**

**Before D-2 the seed said resolution 1 and was internally consistent. D-2 created this.**

⛔⛔ **AND (b) DOES NOT CLOSE IT. The spec-back recommended (b) partly on "it fixes R-3"; that
claim is wrong for the magnitude half and BACKWARDS for LONDON.** The arithmetic:

| Value | Shipped | POCO today | After **(b)** | After **(b)+(i)** |
|---|---:|---:|---:|---:|
| ASIA `roc_magnitude_threshold` | **0.17** | 0.1 | 0.21 *(closer)* | **0.17** ✅ |
| LONDON `roc_magnitude_threshold` | **0.11** | 0.1 | ⛔ **0.21** *(10× further)* | **0.11** ✅ |
| `roc_slope_delta_threshold`, both | **0.06** | 0.05 | **0.06** ✅ | **0.06** ✅ |

**(b) fixes the slope half exactly, improves ASIA's magnitude, and moves LONDON's magnitude
from 0.01 off to 0.10 off.** Recorded plainly because the reviewer recommended (b) without
doing this arithmetic first.

**Why the guard cannot see any of this.** The per-session overrides are `Double?`, and the
ruling's derived rule — *"a `Double?` = `Nothing` nullable override cannot drift"* — is
correct **about drift** and says nothing about whether the seed is *right*.
⭐⭐ **R-3 is the case where "cannot drift" and "is correct" come apart: the nullable is
legitimately absent, and the seed is still wrong, because the RESOLUTION it inherits under
changed underneath it.** No reflection walk scoped by that rule can ever catch this class.

---

## 3. ✅ D-R3 — RULED (i), trader-directed 2026-09-04

| # | Decision | ✅ Ruling |
|---|---|---|
| **D-R3** | **Do the per-session nullable ROC overrides get seeded too?** | ✅ **(i) — SEED THE NULLABLES, as recommended.** `SessionBucketSettings.RocMagnitudeThreshold`: **ASIA = 0.17**, **LONDON = 0.11**, ⛔ **NY stays `Nothing`** — the shipped file carries no NY override, and seeding one would be inventing a value, not mirroring. Precedent, twelve lines from the edit: `AggressorVelocitySessionOverride.BurstRatioThreshold` is a `Double?` seeded with real values under a comment reading *"**MUST mirror settings.json** — the harness builds cfgs from these defaults."* **The rule already exists in this file; `session_volume` was the exception.** ⛔ **(ii) rejected — it would have shipped LONDON knowingly 10× further off shipped than it is today.** (iii) was arbitrary |

**Outcome: the parse-failure path now equals shipped on every ROC value.** §2's fourth column
is the shipped state after this build.

⛔⛔ **AND THE CONSEQUENCE THE IMPLEMENTER MUST CARRY: (i) IS COMPLETELY INVISIBLE TO THE A54a
GUARD.** Both seeded values are `Double?`, and step 3 skips nullables **before** it compares —
so the walk sees no change at all, `Compared` stays 261, and `A62a` passes identically whether
(i) is applied, reverted, or typo'd to 1.7. ⭐ **`A63a` is therefore not "a fixture for R-3" —
it is the ONLY thing in the tree that can ever detect this class.** Build it first, and prove
its teeth by reverting the seed rather than by reasoning.

---

## 4. Fixtures — family **A63**

**A62 is fully used** (`A62a`–`A62g`). **A63 is free.**

| Fixture | Asserts | Mutation that must FAIL it |
|---|---|---|
| **A63a** ⭐⭐ | ⛔ **THE ONLY THING IN THE TREE THAT CAN DETECT THIS CLASS — write it FIRST.** From a bare `New EngineSettings()`, assert `ExecutionResolution.ResolveRocMagnitudeForHour` and `ResolveRocSlopeDelta` return the **shipped** values for an ASIA hour (0–7), a LONDON hour (8–12) and an NY hour (13–23). **Derive every expected value from the tracked `settings.json`, never as a literal** — CLAUDE.md's fixture-literal provenance rule, SHIPPED BEHAVIOUR arm. ⚠ **NY is the arm that proves inheritance still works**: no bucket override, so it must resolve through `resolution_profiles` → global, not to 0.17 or 0.11 | ⛔ **Revert EACH of the four seeded values separately** — ASIA magnitude, LONDON magnitude, and both `ResolutionProfile` fields. **A62a stays GREEN under every one of them** (§3), which is the whole reason A63a exists |
| **A63b** | **The dictionary completeness half.** A hand-built POCO/JSON pair where the JSON dict carries a key the POCO lacks → reported as `JsonOnly`; the matching case → not | Remove the new `Else` arm → A63b fails. ⛔ **`A62a` alone does NOT catch this** — after (b) there is no unmatched key left in the tree, so a clean-tree fixture proves nothing here. Same lesson as `A62b` |
| **A62a** *(existing, re-proved)* | Still clean; `JsonOnly = 0`; record the **new** `Compared` count | Re-run the full §6 mutation list — the seed edit changes what the walk visits |

⚠ **Every mutation RUN, not reasoned** — reverted, confirmed FAIL, restored, confirmed PASS,
output recorded. Standing requirement, [`seat-handover-2026-09-03.md`](seat-handover-2026-09-03.md) §5.

---

## 5. Acceptance

**Display-string parity: NO OBLIGATION**, stated explicitly per CLAUDE.md's hard rule — the
walk is harness-only and the seed edits move **only** the parse-failure path. No line is
added, removed, renamed or re-formatted; `UI/MainForm_PlaintextSnapshot.vb` and
`UI/MainForm_Render_Cards.vb` are untouched.

- Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck`
  build **0/0 Release**, each run separately.
- Harness **ALL PASS**, `A1`–`A62g` unregressed + `A63a`–`A63b`.
- `verify-gate.ps1` **GATE PASSED**.
- **Settings stays v68** — no key added or changed, no `change_log` entry, no dataset
  boundary. ⚠ **A `docs/DeribitIndicatorProject.md` §15 row is owed** — the seed edits change
  what the parse-failure path does.
- **ONE commit** — seed, `JsonOnly` arm, R-1 comment, fixtures, §15 row.

---

## 6. What the reviewer verified, and what he did not

**Verified 2026-09-04, in the tree:**

- The resolver chain and every number in §2's table — `Core/ExecutionResolution.vb:65-93`,
  `EngineSettings.vb` ROC globals (0.1 / 0.05), tracked `settings.json` (ASIA 0.17, LONDON
  0.11, profile "3" = 0.21 / 0.06).
- Four dictionary properties, at the four line numbers named in §0 trap 2.
- The seeded-nullable precedent at `EngineSettings.vb:487-492`, including its
  *"MUST mirror settings.json"* comment.
- `ResolveRocMagnitudeForHour` is the only reader of the session ROC nullable, and uses
  `HasValue` as inherit-or-not with **no arming semantics** (§0's escalation trigger does not
  fire).

**NOT verified:**

- **That the other three dictionaries have no unmatched keys in either direction.** The
  author's probe found none on 2026-09-04; it was not re-run for this document. §0 trap 2
  makes measuring it a build step.
- **Any live run.** No app was started; the parse-failure path is argued statically, as it was
  in the parent spec's §4.1.
