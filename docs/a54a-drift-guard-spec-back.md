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
