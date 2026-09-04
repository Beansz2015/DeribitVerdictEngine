# A54a JSON↔POCO drift guard — spec-back

**Spec:** [`a54a-json-poco-drift-guard-spec.md`](a54a-json-poco-drift-guard-spec.md).
**Outcome record:** [`a54a-drift-guard-batch-summary.md`](a54a-drift-guard-batch-summary.md) —
read that for what happened; this is what to check, what to decide, and where the spec
(and this session's own escalation) were wrong.

---

## 0. Answering §11.4 first — the `Skipped` diagnostic the reviewer asked for

§11.4 asks: *"does the second walk visit the two derived properties and the two
`resolution_profiles` dict keys and simply not record them, or does it never reach them?"*

**Two different mechanisms, verified by reading the shipped `WalkPocoVsJson`:**

- **The two `<JsonIgnore>` derived properties (`RoundTripFeePct`, `EffectiveMinMovePct`)
  ARE visited.** `pocoType.GetProperties(...)` enumerates all public instance properties,
  including `ReadOnly` computed ones — the loop reaches them. The structural check
  (`jsonIgnoreAttr IsNot Nothing OrElse jsonNameAttr Is Nothing OrElse Not prop.CanWrite`)
  then excludes them and now **records the exclusion into `Skipped`** (as
  `<RoundTripFeePct>` / `<EffectiveMinMovePct>`, bracketed since they carry no JSON key) —
  this session added that recording; the original build did not have it and the
  escalation's probe silently dropped it.
- **The `resolution_profiles` dict keys are NEVER REACHED.** The `Dictionary(Of String, T)`
  recursion (§5 step 6) iterates from the **POCO's own key set**
  (`For Each keyObj In dictObj.Keys`). `EngineSettings.ResolutionProfiles` defaults to an
  **empty** dictionary, so there are zero keys to iterate — the JSON's `"1"`/`"3"` entries
  (if present) are never looked up, never compared, never recorded anywhere. This matches
  §3.4's own reasoning (*"no key is walked… nothing to guard here"*) but is a genuinely
  different mechanism from the derived-property case: one is visit-then-skip, the other is
  never-visited.

**What this means for `A62f`:** it tests the derived-property case only (matching the
fixture table's own wording, *"assert `trade_costs` compares its four real keys and
neither derived one"*) — the `resolution_profiles` case needs no fixture, because there is
nothing there to structurally exclude; an empty seed dictionary already excludes it by
construction, same as `List(Of String)` skips entirely at step 7.

**Corrected count:** with `Skipped` now recording the two derived properties, the shipped
tree's walk reports `Skipped=21` (4 root + 15 nullable + 2 derived), not 19 and not the
spec's own implied 23 — the `resolution_profiles` two are genuinely unreachable, not
merely unrecorded, so they cannot be added to any count this walk produces. **Re-measured,
not assumed:** `Compared=261` still holds exactly.

---

## 1. Ranked verification handles — §11.3, answered in order

| # | Handle | Result |
|---|---|---|
| **1** | A62b written first, mutation RUN | ✅ Written before `A62a` in the file; mutation FAIL/PASS output in batch-summary §2 row 8 |
| **2** | Trap-2 swap produces exactly SEVEN false orphans | ✅ Reproduced exactly — batch-summary §3, same seven paths §3.3 named |
| **3** | D-1 allow-list has exactly TWO entries | ✅ `A62D1AllowList` is a 2-element array; `A62g` proves it is not a type blanket |
| **4** | `Compared`'s floor asserted AND proven | ✅ `A62a` asserts `Compared>=200` (measures 261); mutated `WalkPocoVsJson` to `Return` immediately (visits nothing) — `A62a` FAILs with `compared=0`, exactly as described |
| **5** | A62g proves the allow-list is a list, not a class exemption | ✅ Batch-summary §2 row 13 |
| **6** | `OrderCheck.vbproj` unchanged | ✅ `git diff` on it is empty — no `CopyToOutputDirectory` added |
| **7** | D-3 comment rewritten | ✅ `EngineSettings.vb`'s `Transport` declaration comment no longer claims "Stays 'rest' in P1/P2"; it records the v42 cutover and the §4.1 safety argument inline |
| **8** | One commit; §15 row present; v68/no `change_log` | ✅ §15 row added this session; commit lands everything together |
| **9** | Stacked `<summary>` fixed, not boxed beside | ✅ `MicroCvdSettings.AccelThresholdDynamicPct` now carries one corrected block |

---

## 2. Feedback on the spec's own assumptions

**What the spec got right, specifically.** §5's per-property ordering (structural skip →
root-provenance skip → nullable skip → absent-key test → scalar/dict/list/class) is
correct as written — an independent implementation built from that ordering alone,
without reading the author's own probe, reproduced `Compared=261`, `Orphans=0`,
`JsonOnly=0` exactly on the first attempt, and reproduced the seven-false-orphan trap
exactly when the ordering was deliberately broken. The ordering is the load-bearing part
of the spec and it held under two independent implementations.

**Where the spec's own fixture table under-specified the teeth it asked for — `A62f`.**
The table's mutation ("swap the structural test for a name list → A62f fails") assumes any
name-list swap is distinguishable from the structural check by testing against
`TradeCostSettings` alone. **It is not**, for the current codebase: `RoundTripFeePct` and
`EffectiveMinMovePct` are the *only* two `ReadOnly` properties anywhere in
`EngineSettings.vb` (confirmed by grep), so a name list containing exactly those two names
is behaviorally identical to the structural check for every property that exists today.
The first attempt at this mutation produced **no observable change** — `A62f` PASSED
under a genuinely swapped implementation, which would have been a silent false pass. Fixed
by adding a small fixture-local test type (`A62StructuralTestShape`) carrying an
arbitrarily-named derived property no production-scoped name list could anticipate; the
same mutation against the corrected fixture crashed the harness outright. **Record this
pattern for future "structural, not by name" fixtures in this project: if the current
count of instances the shape rule protects is small (here, two), a black-box test against
production types alone cannot separate the general rule from a list that happens to be
complete today.**

**Where this session's own escalation doc overclaimed — flagged against itself, not just
the spec.** The escalation doc's §2 stated `Skipped=19` *"matches §3's 4 root-provenance +
15 nullable exactly,"* offering it as evidence of agreement between two independent
implementations. §0 above shows the comparison was against the wrong total — §3.4
documents more exclusion categories than root+nullable, and this session's own walk did
not, at the time, record the derived-property skips at all. The count that actually
carried evidentiary weight — `Compared=261` — was correct and remains correct. **The
lesson is the same one CLAUDE.md's fixture-literal-provenance rule and the 2026-08-11
"count of a name is not a handle" ruling both exist for, one level up: a number is only
evidence for the claim it was actually measured against, and stating agreement requires
checking that both sides are counting the same thing** — which this session's own first
pass did not do.

**Constraint pair that nearly conflicted.** §5.1's "no `CopyToOutputDirectory`, no
`Directory.GetCurrentDirectory()` anchor" pair, combined with `A62e`'s requirement to test
a **guaranteed-to-fail** resolution, initially looked like it needed a second resolver
implementation for the test path alone. The escape hatch: `A62ResolveRepoRoot` takes
`startDir` as a parameter rather than reading `AppContext.BaseDirectory` internally, so the
real production call sites and `A62e`'s temp-directory test both go through the identical
function with different inputs — no second copy, no test-only branch.

---

## 3. Decisions — none open

Every D-decision in the spec (D-1 through D-5) is ticked, including the §4.2 correction.
Session 2 (§7) has its own open re-measurement step (*"9 fixture omissions, 0 production
omissions"* — inherited, not yet re-verified) but that is explicitly out of scope for
session 1 and untouched by this batch.

---

## 4. What I did not verify

- **Session 2's inherited "9 fixture omissions, 0 production omissions" figure.** §7 makes
  re-measuring it step 1 of session 2, not this session.
- **That 261 is the complete comparable population** in the sense of "every property that
  *should* be compared, is." It is what this specific walk visits under §5's rules — a
  property added with no `JsonPropertyName` would still be silently skipped, same caveat
  the original spec §9 already carried forward.
- **The live app's behaviour on the parse-failure path**, beyond the static argument in
  spec §4.1 (`ResolveSource`'s REST fallbacks, `WsFallbackToRest` defaulting `True`). No
  live run was made to actually force a `settings.json` parse failure and observe the app.
- **`git diff`'s exact byte count / whether any unrelated whitespace moved** — checked via
  `git diff --stat` and a manual read of the diff, not a byte-for-byte tool.

---

## 5. ⭐ REVIEWER VERDICT — ACCEPTED, 2026-09-04

**Reviewed by the spec author, who wrote no code on this build (trader-directed seat split).**
✅ **ACCEPTED. Two non-blocking findings, neither of which should hold the commit.**

### 5.1 Verified independently, not taken on report

| Check | Result |
|---|---|
| Harness, run by the reviewer | **324 PASS · 0 FAIL · `ALL PASS`** |
| All seven `A62` fixtures **executed**, not merely present | ✅ — `A62b` registered and running **before** `A62a`, confirming handle 1's ordering claim in execution, not just in file order |
| `verify-gate.ps1 -Mode local-fast`, run by the reviewer | ✅ **`GATE PASSED`** · display-parity `no snapshot/card drift detected` · version-bump `no engine-path change` |
| Seven re-syncs present in `Core/Settings/EngineSettings.vb` | ✅ all seven, each carrying provenance at the declaration site |
| ⭐ **The reviewer's own independent probe, re-run against the post-build tree** | ⭐⭐ **Reports exactly TWO divergences — `auto_run.start_engaged` and `signal_bridge.enabled`, i.e. precisely the D-1 allow-list.** The probe has no allow-list, so it reports them; **all seven previously-drifting paths are clean under a second, independently-written instrument** |
| `OrderCheck.vbproj` | ✅ unchanged — handle 6 |
| `settings.json` | ✅ not in the diff; still **v68**, no `change_log` entry |
| Handle 9 — the stacked `<summary>` | ✅ fixed properly: **one** block, and the original line itself now reads *"Default 0.30 (30 %)"* rather than a correction bolted underneath |
| `A62f`'s repair | ✅ read and confirmed real — `A62StructuralTestShape` carries a genuinely arbitrary `<JsonIgnore>` `ReadOnly` property no production-scoped name list could anticipate |

### 5.2 ⭐ The `A62f` finding is the best thing in this packet, and the defect was the spec's

**§2's `A62f` account is correct and the fixture as I specified it had no teeth.** `RoundTripFeePct`
and `EffectiveMinMovePct` are the only two `ReadOnly` properties in `EngineSettings.vb`, so
the mutation I wrote — *"swap the structural test for a name list"* — is **behaviourally
identical to the structural check for every type that exists today.** The implementer ran it,
observed **no change**, recognised the silent false pass rather than recording a green tick,
and rebuilt the fixture around a locally-declared arbitrary shape. ⭐ **That is exactly the
attack §11.2 asked for, and it landed on the spec rather than on the code.**

⭐ **The general rule it produces is worth more than the fixture and should outlive it:**
*when a shape rule protects a small number of instances, a black-box test against production
types alone cannot separate the rule from a name list that happens to be complete today.*
**Recommend promoting this into CLAUDE.md's Collaboration Rules beside the fixture-literal
provenance rule — it is the same defect class at the level of shapes rather than values.**

⭐ **Also credited: the packet flags its own escalation doc's `Skipped=19` over-claim against
itself** (§2, final paragraph) rather than leaving the reviewer to raise it. §11.4 is answered
and closed — see R-2 below for the one part of that answer that generalises wrongly.

### 5.3 Findings — both NON-BLOCKING

| # | Finding | Severity |
|---|---|---|
| **R-1** | ⚠ **The stale line the seam audit originally cited is still standing, directly above its own correction.** `SessionVolumeSettings.Sessions`' comment still opens *"Default buckets aligned to live **v30** (ASIA/LONDON/NY)"* — the buckets are now aligned to live **v68**. ⛔ **This is handle 9's defect class recurring inside the commit that fixed handle 9 elsewhere**, and it is not a generic stale comment: [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md) **S-3** quoted *this exact line* as the evidence of drift. Left as-is, the next auditor re-raises S-3 against a now-correct seed. **Fix: edit the opening line to say v68; do not append.** | **Trivial.** Ride the next commit that opens the file |
| **R-2** | ⚠ **The dictionary branch has a hole, and §0's answer generalises from one dictionary when there are FOUR.** `EngineSettings` carries `ResolutionProfiles` **plus three seeded session dictionaries** — `AggressorVelocitySettings.Sessions` (`:487`), `AbsorptionSettings.Sessions` (`:566`), `StructuralLevelsSettings.Sessions` (`:1062`). Step 6 iterates the **POCO's** keys, so **a JSON dict key with no POCO counterpart is neither recursed NOR recorded as `JsonOnly`** — the recursion that would emit `JsonOnly` is never entered. ⛔ **So the walk's own comment — *"This half proves the POCO is COMPLETE"* — is true at object level and FALSE inside dictionaries.** ⚠ **The hole is already live twice** (`resolution_profiles["1"]`/`["3"]` have no POCO counterpart) **and benign twice** (both `ResolutionProfile` properties are `Double?`, so nothing would be compared anyway) — ⛔ **which is exactly the condition under which it stops being noticed.** A future `session_volume`-style bucket added to a *seeded* session dict would be invisible. **⚠ The defect is the SPEC'S, not the build's** — §5 step 6 says *"recurse only into keys present on BOTH sides"* and the build implements that faithfully | **Scoped follow-up, needs a decision.** ⛔ **Do NOT patch silently** — recording unmatched JSON dict keys as `JsonOnly` would make `A62a` report 2 and FAIL today, so it is a ruling (report / allow-list the two / leave and document), not an edit |

### 5.4 Not accepted as evidence, and not needed

`ALL PASS` and `GATE PASSED` are recorded above because the reviewer re-ran them, **not because
the packet reported them.** ⭐ **The packet did not lead with them** — it led with the mutation
log — which is the right shape and is noted.

---

## 6. ✅ R-1 / R-2 RULED, and ⛔ R-3 recorded with a correction against the reviewer

**Trader-directed 2026-09-04: R-2 = option (b) · R-1 rides it · record R-3.**
**Follow-up build spec: [`a54a-r2-r3-followup-spec.md`](a54a-r2-r3-followup-spec.md).**

- **R-2 = (b)** — populate the `ResolutionProfiles` POCO seed to mirror shipped, which makes
  option (a) free: with the keys matched, recording unmatched JSON dict keys as `JsonOnly`
  leaves `A62a` at zero and the D-1 allow-list at two.
- **R-1 rides that commit** — the `SessionVolumeSettings.Sessions` comment's *"aligned to
  live v30"* opening line is edited, not appended to.
- **R-3 recorded** — see below, and §2 of the follow-up spec.

⛔⛔ **THE REVIEWER'S OWN RECOMMENDATION WAS HALF WRONG AND IS CORRECTED HERE.** §5.3 R-2
argued for (b) partly because *"it fixes R-3 in the same stroke."* **It does not.** The
arithmetic, done afterwards:

| Value | Shipped | POCO today | After (b) | After (b) + seeded nullables |
|---|---:|---:|---:|---:|
| ASIA `roc_magnitude_threshold` | 0.17 | 0.1 | 0.21 | **0.17** |
| LONDON `roc_magnitude_threshold` | 0.11 | 0.1 | ⛔ **0.21** | **0.11** |
| `roc_slope_delta_threshold` | 0.06 | 0.05 | **0.06** | **0.06** |

**(b) fixes the slope half exactly, improves ASIA, and moves LONDON from 0.01 off shipped to
0.10 off — ten times further.** The reviewer recommended (b) on a claim he had not computed,
which is the same failure mode this project keeps recording: a tidy explanation offered
before the measurement.

⭐⭐ **What R-3 exposes is bigger than R-3.** The A54a guard is scoped by the ruled rule *"a
`Double?` = `Nothing` nullable override cannot drift."* That is correct **about drift** and
silent **about correctness**: here the nullable is legitimately absent, and the seed is still
wrong, because the **resolution it inherits under changed underneath it**. **No reflection
walk scoped by that rule can catch this class** — it needs a behavioural fixture that
resolves the value through the shipped resolver, which is what `A63a` is for.

⛔ **ONE DECISION REMAINS OPEN — `D-R3` in the follow-up spec §3:** whether the per-session
ROC nullables get seeded too (ASIA 0.17 / LONDON 0.11). ⭐ **Reviewer's read: yes** — there is
in-file precedent twelve lines from the edit (`AggressorVelocitySessionOverride.BurstRatioThreshold`
is a `Double?` seeded with real values, its comment reading *"MUST mirror settings.json"*).
**Without it, (b) ships LONDON knowingly worse.**

### 6.1 ✅ D-R3 RULED (i) — seed the nullables, 2026-09-04

**ASIA `RocMagnitudeThreshold` = 0.17 · LONDON = 0.11 · NY stays `Nothing`** (shipped carries
no NY override; seeding one would invent a value rather than mirror one). **The parse-failure
path now equals shipped on every ROC value.** Nothing is owed.

⛔⛔ **The consequence that must not be lost: (i) is INVISIBLE to the A54a guard.** Both seeded
values are `Double?`, and the walk's step 3 skips nullables **before** comparing — so `A62a`
passes identically whether (i) is applied, reverted, or mistyped. ⭐ **`A63a` is not "a fixture
for R-3"; it is the only instrument in the tree that can ever detect this class**, which is why
[`a54a-r2-r3-followup-spec.md`](a54a-r2-r3-followup-spec.md) §4 orders it first and requires
its teeth proven by reverting each seeded value separately.

⚠ **One further reviewer correction, made in the same pass:** that follow-up spec's §0 trap 3
originally predicted `Compared` would move off 261 after the seed edit. **It should not** —
every property the edit touches is nullable, so the dict recursion adds reach without adding a
single comparison. Corrected in place, and left as a **falsifiable prediction** (`Compared`
261 · `Skipped` 21 → 25 · `Orphans`/`JsonOnly` 0) rather than a measurement. **Two unverified
claims by the reviewer in one document is the pattern worth naming, not the individual slips.**
