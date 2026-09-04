# A54a R-2/R-3 follow-up — spec-back

**Spec:** [`a54a-r2-r3-followup-spec.md`](a54a-r2-r3-followup-spec.md).
**Commit:** `cc44e9f`, local — not pushed. Solo build, not a multi-lane batch, so this is
the single document per `docs/batch-review-packet-convention.md`'s shape rather than a
summary+packet pair — the outcome facts that a summary would otherwise carry sit in §0.

---

## 0. Outcome, for the record

Solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck build
**0/0** Release, each run separately. Harness **ALL PASS**, `A1`–`A62g` unregressed +
`A63a`–`A63b` new. `verify-gate.ps1 -Mode local-fast` **GATE PASSED** (display-parity no
snapshot/card drift; version-bump the pre-existing `[no-engine-change]` token from an
earlier docs-only commit already in range — advisory-only in this mode, not load-bearing).
Settings stays **v68** — no key added or changed, `settings.json` not in the diff. One
commit, three files (`Core/Settings/EngineSettings.vb`, `verify/ordercheck/Program.vb`,
`docs/DeribitIndicatorProject.md`).

---

## 1. Ranked verification handles

**If you only run one: handle 3.** It is the one that demonstrates the whole reason this
follow-up exists — the guard's blind spot and the new fixture's teeth, in one pair of runs.

| # | Handle | Result |
|---|---|---|
| **1** | `Compared` stays exactly 261; `Skipped` moves 21→25 | ✅ Measured via a temporary `Console.WriteLine` probe in `A62a`, removed before commit — not inferred. `+2` per recursed `ResolutionProfile` key (`"1"`, `"3"`), matching the spec's own corrected §0 prediction exactly |
| **2** | `A62a` stays GREEN under all five teeth-proof mutations | ✅ Reverting ASIA magnitude, LONDON magnitude, `resolution_profiles["3"]` magnitude, `resolution_profiles["3"]` slope, and commenting out the new `Else` arm — `A62a` PASSED unchanged under every one. Proves the walk genuinely cannot see any of this class, not merely that no one ran the mutation |
| **3** | `A63a` FAILS under all four seed-value mutations; `A63b` FAILS with the `Else` arm removed | ✅ All five run for real (reverted → build → run → observed FAIL → restored → build → run → observed PASS), output captured each time — see §3.2 below for the one that was NOT a false positive on the first draft |
| **4** | The reverse dict-orphan direction (POCO key absent from JSON) is 0 today | ✅ Measured by direct key-set comparison, not a fixture: `ResolutionProfiles` {"1","3"}={"1","3"}; `AggressorVelocitySettings.Sessions`, `AbsorptionSettings.Sessions`, `StructuralLevelsSettings.Sessions` all {NY,LONDON,ASIA}={NY,LONDON,ASIA} against the tracked `settings.json`. Per spec §1: measured and reported, no rule invented |
| **5** | One commit; `settings.json` untouched; §15 row present | ✅ `git diff --stat` before commit showed exactly the three intended files; `git show --stat cc44e9f` confirms |
| **6** | Solution + all four standalone tools build 0/0 Release separately | ✅ Each `dotnet build` run individually, not only via `verify-gate.ps1`'s bundled subset (which covers AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck but not the main solution in `local-fast` mode) |

---

## 2. Decisions queued

**None open.** Every decision the spec required (R-2 = (b), R-1 rides it, D-R3 = (i)) was
already trader-ruled 2026-09-04 before this build started — the spec's own banner says
"BUILD-AUTHORIZED," and nothing in the build surfaced a new fork requiring a ruling. The
one in-build judgment call (§3.2 below, strengthening `A63a`) was a fixture-coverage fix
within the spec's own stated intent, not a design decision — recorded as feedback, not
queued as a decision.

---

## 3. Feedback on the spec's own assumptions

### 3.1 What the spec got right, specifically

**§2's arithmetic table.** The spec predicted, before any code was touched, that (b) alone
would leave LONDON magnitude at 0.21 — ten times further from shipped 0.11 than the
pre-fix 0.1 was — while (b)+(i) would restore every value exactly. Both halves were
confirmed by direct measurement during the mutation-proof pass (§1 handle 3): reverting
just the `RocMagnitudeThreshold` seed on LONDON reproduces `londonMag=0.21` precisely,
matching the table's "After (b)" column. **The table was not a sanity-check gloss; it was
load-bearing and correct**, in contrast to the spec-back it is itself correcting, where the
prior draft's "it fixes R-3" claim for the same option had not been computed at all before
being written down.

**§0 trap 3's corrected prediction.** The spec (after its own in-place correction) claimed
`Compared` would stay at exactly 261 because every property the seed edits touch is
`Double?` and step 3 skips nullables before comparing. Confirmed exactly — see §1 handle 1.
The spec explicitly labelled this a **falsifiable prediction, not a measurement**, and said
to report rather than adjust the floor if it moved. It did not move; nothing to report
beyond confirming it.

### 3.2 Where the spec's own fixture description was narrower than its words — `A63a`

**The finding.** §4's table describes `A63a` as asserting `ResolveRocMagnitudeForHour` and
`ResolveRocSlopeDelta` return shipped values for an ASIA, a LONDON, and an NY hour, and its
own "mutation that must FAIL it" column says to **revert each of the four seeded values
separately — including both `ResolutionProfile` fields.** Built exactly as described, the
first draft of `A63a` could not detect a revert of `resolution_profiles["3"]`'s own
`RocMagnitudeThreshold`. Reason: `ResolveRocMagnitudeForHour` checks the ASIA/LONDON
**bucket-level** override first, and only falls through to the resolution-profile chain
when no bucket override exists — but ASIA and LONDON both carry bucket overrides as of
this same commit, so no live per-hour test path this fixture exercises ever reaches
`resolution_profiles["3"]`'s magnitude field at all. NY does not help either — NY resolves
through `resolution_profiles["1"]`, never `["3"]`. **The field is masked for every
currently-live session; the mutation was invisible, and the first draft PASSED under a
genuinely broken seed.**

**Caught, not assumed clean.** §4's closing instruction — *"Every mutation RUN, not
reasoned"* — is what surfaced this: running the fourth of the four required mutations
produced no observable change, which is the same signal the parent build's `A62f` finding
(`a54a-drift-guard-spec-back.md` §5.2) was built on. Fixed by adding a direct assertion —
`ExecutionResolution.ResolveRocMagnitude(cfg, 3)` against `resolution_profiles["3"]`'s own
expected value, bypassing the bucket-override masking — and re-running all four mutations
against the strengthened fixture; all four now fail correctly (§1 handle 3).

**Why this is worth recording as a pattern, not just a fix.** This is the same defect
class as [[feedback_fixture_shape_must_admit_the_failure]] one level removed: there, a
fixture's *shape* couldn't produce the trap it claimed to guard; here, a fixture built
exactly to a spec's own worked description couldn't reach the input the spec's own
mutation table named, because a **layered nullable-override chain** had a nearer layer
that masked the one under test — and that masking relationship was itself created by a
different part of the same commit (the bucket overrides seeded by D-R3 (i)). **Recommend
naming this explicitly for any future spec touching a resolver with more than one
fallback layer: state which layer each test hour is meant to exercise, not just which
final value it should return** — a passing assertion on the outward-facing value does not
prove every named input was reachable.

### 3.3 Constraint pairs — none that nearly conflicted

Unlike the parent build (§5.1's `CopyToOutputDirectory` / guaranteed-failure test-path
tension), this build had no comparable near-conflict. The one candidate — `A62a`'s
`JsonOnly=0` assertion depending on (b) shipping in the *same* commit as the `Else` arm,
so `A62a` cannot independently corroborate the `Else` arm's correctness — is not a
conflict; it is exactly what the spec's own §4 table already names (*"`A62a` alone does
NOT catch this... same lesson as `A62b`"*), and `A63b` was built specifically to cover it.

---

## 4. What I did not verify

- **No live run forcing an actual `settings.json` parse failure.** The parse-failure path
  is argued statically via `ExecutionResolution.vb`'s resolver chain and `A63a`'s direct
  calls against a bare `New EngineSettings()` — the same posture the parent spec's §4.1
  took, not independently strengthened here.
- **Per-field fallback on a partially-valid user `settings.json`.** The acceptance claim
  ("the seed edits move only the parse-failure path") is correct for a *total* parse
  failure and for the *shipped* file (confirmed gap-free by `A62a`'s `Orphans`/`JsonOnly`
  both 0), but I did not verify `SettingsLoader`'s behaviour on a successful parse of a
  user file that omits some of these specific keys — an inherited framing from the parent
  spec, not independently re-derived this session.
- **No second, independently-written probe of the dict-orphan reverse direction.** §1
  handle 4 is a manual key-set comparison read from the two source files, not an automated
  fixture — if a future edit adds a POCO-only key to any of the four seeded dicts, nothing
  in the tree will flag it. This is the spec's own explicit choice (§1: "report the count
  and stop rather than inventing a rule for it"), not an oversight, but it is a live blind
  spot going forward, not a closed one.
- **Byte-level diff review.** Checked via `git diff --stat` plus a full manual read of
  both file diffs, not a byte-for-byte tool.
- **Whether any doc outside this build's scope (`trader-profile.md`, `roadmap.md`, the
  ROC re-baseline proposal) quotes the pre-seed POCO defaults as current state.** Not
  searched — out of scope per the spec, flagged here only so it isn't assumed checked.

---

## 5. ⭐ REVIEWER VERDICT — ACCEPTED, 2026-09-05

**Reviewed by the spec author, who wrote no code on this build.**
✅ **ACCEPTED. One item to queue, one factual note. Neither holds the commit.**

### 5.1 Verified independently, not taken on report

| Check | Result |
|---|---|
| Harness, re-run by the reviewer | **326 PASS · 0 FAIL · `ALL PASS`** |
| `A63a` / `A63b` **executed**, not merely present | ✅ both, and `A62a` still green beside them |
| `verify-gate.ps1 -Mode local-fast`, re-run | ✅ **`GATE PASSED`** — display-parity clean, and version-bump read a clean **`OK no engine-path change`** (see §5.4) |
| The seven-value seed | ✅ `resolution_profiles` `{"1": empty, "3": {0.21, 0.06}}`; ASIA `RocMagnitudeThreshold` **0.17**, LONDON **0.11**, ⛔ **NY untouched** — exactly as D-R3 (i) ruled |
| **R-1** | ✅ *"aligned to live v30"* → **v68**, **edited in place**, not appended |
| ⭐ **R-1 adjacent, unasked** | ⭐ **The `RocMagnitudeThreshold` docstring's *"Default buckets leave it Nothing"* was also corrected.** That line would have gone stale the moment the seed landed — **handle 9's defect prevented rather than repeated.** Not in the spec; caught anyway |
| ⭐ **The reviewer's own probe, re-run post-build** | ⭐⭐ **`Compared` = 261 — UNCHANGED, confirming the spec's falsifiable prediction under a second instrument.** Drifts = 2 (both allow-listed) · Orphans 0 · JsonOnly 0 · nullable skips 15 → 19, i.e. **the same +4 delta** the packet reports as 21 → 25 under its own bookkeeping. **Two instruments, same delta** |
| `settings.json` | ✅ not in the diff; still **v68**, no `change_log` entry |
| Commit shape | ✅ one commit, three files, `docs/DeribitIndicatorProject.md` §15 row present |

### 5.2 ⭐ The `A63a` masking finding is correct, and it is the best thing in this build

**§3.2 is right, and the reviewer verified the mechanism rather than accepting the account.**
`ResolveRocMagnitudeForHour` (`Core/ExecutionResolution.vb:79-83`) tests the bucket override
**first**; after D-R3 (i) both ASIA and LONDON carry one, and NY resolves through
`resolution_profiles["1"]`. ⛔ **So `resolution_profiles["3"].roc_magnitude_threshold` is
unreachable from every per-hour path this fixture exercises — and the masking was created by
the same commit's own seeding.** A fixture built exactly to the spec's worked description
passed under a genuinely broken seed, and only the required fourth mutation exposed it.

⭐ **The repair is right too:** a direct `ResolveRocMagnitude(cfg, 3)` assertion that bypasses
the masking, with the reasoning left at the call site. And `A63a` derives **every** expected
value from the tracked `settings.json` via `A63ReadDouble` — no literals, the fixture-literal
provenance rule's SHIPPED-BEHAVIOUR arm satisfied properly.

⭐⭐ **The generalisation in §3.2 should outlive this build and is endorsed:** *for any spec
touching a resolver with more than one fallback layer, state which layer each test input is
meant to exercise, not just which final value it should return.* **This is the second time in
this arc that a spec-described mutation turned out to be unobservable** — `A62f` was the
first. **Two instances is a pattern, and both were caught only because "every mutation RUN,
not reasoned" is a hard requirement rather than advice.**

### 5.3 ⚠ F-1 — the one item to queue

**§4's third bullet discloses a live blind spot and it must not be archived with this
packet.** The **reverse** dictionary direction — a **POCO** dict key absent from JSON — is
still silently skipped. Handle 4 measured it as **0 today** across all four dictionaries by a
manual key-set comparison; **nothing in the tree will flag it if that changes.**

⭐ **The disclosure is correct and the spec caused it** — [`a54a-r2-r3-followup-spec.md`](a54a-r2-r3-followup-spec.md)
§1 said *"measure it; if any exist today, report the count and stop rather than inventing a
rule for it."* The implementer did exactly that. ⛔ **But an honest disclosure inside a
spec-back is not a tracked item**, and this project's own §1b sweeps exist because that is
how work rots. **Queued to [`trader-tick-queue.md`](trader-tick-queue.md) §2.** Not urgent,
inert today, and it needs a ruling rather than a patch — the same shape R-2 had.

### 5.4 Factual note, not a finding

§0 describes the version-bump check as flagging a pre-existing `[no-engine-change]` token and
being *"advisory-only in this mode, not load-bearing."* **The reviewer's own run returned a
clean `OK no engine-path change` with no such line** — most likely because the packet's run
predated the commit and saw a different range. **Nothing is wrong**; recorded only because
"the warning is advisory" is a sentence worth being able to check later.

### 5.5 Packet shape — correct, and confirmed so it is not re-litigated

**One document rather than a summary+packet pair is right here.**
[`batch-review-packet-convention.md`](batch-review-packet-convention.md) is titled
*"reporting a **multi-lane batch**"*, and CLAUDE.md scopes it the same way. **This was a solo
build; §0 carries the outcome facts a summary would have.** No finding.
