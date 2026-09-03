# A54a — a JSON↔POCO drift guard by reflection walk

**Status:** ✅ **BUILD-AUTHORIZED. §4's D-table is TICKED IN FULL — trader-directed
2026-09-04.** D-1 · D-2 · D-4 · D-5 as recommended; **D-3 ruled AGAINST the spec's
abstention — sync to `"ws"` and fix the comment.**

⚠ **D-3's ruling changes two other sections and they are corrected in place: the D-1
allow-list is now TWO entries, not three, and the escalation trigger moves with it.**

**Implements:** [`trader-tick-queue.md`](trader-tick-queue.md) §0a, row *"A54a scope — GUARD
the third copy, or DELETE it?"* — **RULED 2026-08-11, option (d) + scoped (b).**
Second opinion: [`seam-audit-decisions-second-opinion-2026-08-11.md`](seam-audit-decisions-second-opinion-2026-08-11.md).
Origin finding: [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md) **S-2**.

**Queue position:** item **8** of [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) §2.

⚠ **ALL DATES ARE UTC.** The workstation is GMT+8.

---

## 0. Model + effort

**Model: Sonnet · Effort: HIGH · two sessions.**

⛔ **This is an upgrade from the *"Sonnet, medium, one session"* carried by
[`trader-tick-queue.md`](trader-tick-queue.md) §0a and by
[`seat-handover-2026-09-03.md`](seat-handover-2026-09-03.md) §0, and the upgrade is
evidence-driven — see §3.** That handover bills item 8 as **"Transcription. The ruling is
COMPLETE."** ⚠ **It is not.** The ruling settled *how to find drift*. It did not settle *what
to do when the walk finds a divergence that is deliberate* — and the shipped tree contains
**two** of those, against **seven** real ones. A build that treats all nine alike either fails
the harness on correct code or passes over two live defects.

**Why HIGH rather than medium.** The walk itself is ~60 lines and genuinely mechanical. The
hard part is the **classification rule** in §4 D-1: two of the nine divergences the walk
finds today are documented design choices, and *nothing in the value or the type tells them
apart from drift.* ⚠ **A third looked deliberate and was not — `network.transport` was ruled
DRIFT on 2026-09-04 (D-3), against this spec's own abstention.** That is the measure of how
hard the classification is: the spec author could not call it from the declaration comment. That is the same SHIPPED-BEHAVIOUR-vs-MECHANISM problem CLAUDE.md's
fixture-literal provenance rule exists for, reappearing one level up the stack. Getting it
wrong is expensive and quiet, which is this project's definition of high-effort work.

**Where a Sonnet seat will specifically slip — three named traps:**

1. ⚠ **Writing the guard as a bare `POCO = JSON` assertion.** It reads correct and it is
   correct for 252 of 261 scalars. It then fails on `signal_bridge.enabled`, whose POCO
   default is `False` **by ruling** (`EngineSettings.vb:1320` — *"Default False (§8 D3) —
   flipping it on is the trader's dated action"*). The obvious repair — flip the POCO to
   match — **reverses a trader decision to satisfy a test.**
2. ⚠ **Ordering the nullable test after the absent-key test.** Do it in that order and the
   walk reports **7 false orphans**, every one a nullable session override legitimately
   absent from its JSON object. **This is not hypothetical — the probe written for this spec
   did exactly that on its first run** (§3.3). The nullable test must run FIRST.
3. ⚠ **Resolving the JSON key case-sensitively.** `SettingsLoader` deserialises with
   `PropertyNameCaseInsensitive = True`, and the POCO's own key convention is **mixed** —
   `"ADX"`, `"RSI"`, `"CVD"` upper-case beside `"spread"`, `"swing"`, `"funding"` lower-case
   (`EngineSettings.vb:166-205`). A case-sensitive `TryGetProperty` agrees with the shipped
   file **today** and would disagree with the loader the first time anyone re-cases a key.
   **A guard whose matching rule differs from the loader's is guarding something the engine
   does not do.**

⚠ **The fixtures cannot be relied on to catch trap 1.** The implementer writes both the walk
and its fixture; if they encode "any inequality is drift" in the walk they will encode the
same belief in the fixture, and it will pass. **A62b (§6) is specified as the mutation
fixture precisely to break that loop — it must be written BEFORE the allow-list.**

**⛔ ESCALATION TRIGGER — stop and move to Opus/high.** Two conditions, either one:

- ⚠ **The D-1 allow-list needs a THIRD entry** beyond the **two** named in §3.2 during the
  build. **Two is a fixed, documented set** — D-3's ruling removed the third. A growing one is
  a hand-maintained table that can drift — **which is the exact objection that killed option
  (a)**, reappearing inside option (d). Stop and re-take D-1. *(This trigger read "a FOURTH
  entry beyond the three" before D-3 was ruled; corrected 2026-09-04, and it is now a
  TIGHTER trigger than the one written.)*
- **The walk needs a hand-written exception to resolve a property to its JSON path.** This
  is the ruling's own trigger, carried forward verbatim: *"(d) has quietly become (a) and the
  decision must be re-taken."* ⭐ **Measured 2026-09-04: it does not fire today** — 0 orphans,
  0 JSON-only keys, no path table needed (§3.1).

**Session split — sequenced by dependency, not by size:**

| Session | Scope | Effort |
|---|---|---|
| **1** | The reflection walk + fixtures **A62a–g** + **THREE** POCO re-syncs (D-2's two, plus D-3's `network.transport`) + the D-3 comment correction | Sonnet, **high** |
| **2** | The scoped-(b) dead-code removal (§7) | Sonnet, **medium** |

⛔ **Session 2 depends on session 1 and must not lead.** Session 1's guard is what proves a
(b) edit changed nothing; running (b) first removes the defaults with no instrument watching.

---

## 1. What this guards, and the two-month defect that named it

`settings.json` and `Core/Settings/EngineSettings.vb` are two independent statements of the
same numbers. Nothing compares them.

**The defect that produced this item:** `ObvSettings.TrendGate` sat at **10.0** — the pre-v33
value — against a shipped JSON of **18.0**, from **2026-06-13 to 2026-08-11. Two months.** It
was found by an audit, not by a test, and it survived a commit (v66) whose own message
claimed the POCO moved *"in lockstep"*.

**Why it is latent rather than live, and why that is not a reason to skip it:** the app always
reads JSON, and every production call site passes the cfg value by name. So a stale POCO
default reaches production only on the `settings.json`-parse-failure path — which is real, not
theoretical: `SettingsLoader.vb:44` seeds `_current = New EngineSettings()` and the parse
handler deliberately keeps it (`:460-461`). **The harness is affected unconditionally** — every
fixture builds its cfg from `New EngineSettings()`, so app and harness pin different behaviour
and nothing notices.

---

## 2. The ruling — pointer, not restatement

[`trader-tick-queue.md`](trader-tick-queue.md) §0a is the decision text and wins over anything
here. In one line: **option (d)** — a reflection walk comparing `New EngineSettings()` against
the deserialised shipped `settings.json` — **plus scoped (b)** as dead-code removal, *not* as
the fix.

Two constraints from that ruling that this spec must not weaken:

- ⛔ **No fifth copy.** The walk reads the `JsonPropertyName` attributes the serialiser itself
  uses. No hand-maintained parameter→key table. (Option (a) was killed on exactly this.)
- ⛔ **Scope by the derived rule: a concrete POCO default can drift; a `Double?` = `Nothing`
  nullable override cannot.** That rule is why v40/v41/v48 produced no drift while
  v33/v34/v36/v58 did.

⚠ **Carried forward from the ruling and still true: copy 4 — fixture literals — is fixed by
NOTHING here.** `Core/Indicators_OrderFlow.vb:291` declares `Optional slopePctOfValue As
Double = 0.05`, a value **neither** the POCO (0.01) **nor** the JSON (0.10) has ever carried.
Three copies, three different numbers. This guard sees only two of them.

---

## 3. ⚠ What I MEASURED, and where it contradicts the inherited claim

**Method:** a throwaway console project in the session scratchpad, `Compile Include`-ing the
real `Core/Settings/EngineSettings.vb`, walking `New EngineSettings()` against the tracked
repo-root `settings.json` (v68). Run 2026-09-04. It is **not** committed — it exists so the
numbers below are measured rather than inherited, and the shipping fixture replaces it.

**The inherited claim** (from the reviewing seat's prototype, via
[`trader-tick-queue.md`](trader-tick-queue.md) §0a): *"found all four session-bucket drifts
and both known drifts, zero false positives, zero orphans."*

| Quantity | Inherited | **Measured 2026-09-04** |
|---|---|---|
| Scalars compared | not stated | **261** |
| Divergences found | 6 (4 session + 2 known) | ⛔ **9** |
| Orphans (POCO property, no JSON key) | 0 | **0** ✅ *(but see §3.3)* |
| JSON-only keys (no POCO property) | not stated | **0** |
| Nullable properties skipped | not stated | **15** |

⭐ **The four session-bucket drifts reproduce exactly.** ⛔ **The "both known drifts" does not:
one of the two is `ObvSettings.TrendGate`, which v66 FIXED on 2026-08-11. What the walk
actually finds is five divergences beyond the session buckets, and they are not one class.**

### 3.1 The nine divergences, classified

| # | Path | POCO | JSON | Class |
|---|---|---:|---:|---|
| 1 | `indicators.CVD.slope_pct_of_value` | **0.01** | **0.10** | ⛔ **DRIFT** |
| 2 | `indicators.MicroCVD.accel_threshold_dynamic_pct` | **0.03** | **0.30** | ⛔ **DRIFT** |
| 3 | `session_volume.sessions.ASIA.high_multiplier` | 0.8 | 1.00 | ⛔ **DRIFT** |
| 4 | `session_volume.sessions.ASIA.mid_multiplier` | 0.85 | 1.00 | ⛔ **DRIFT** |
| 5 | `session_volume.sessions.ASIA.execution_resolution` | 1 | 3 | ⛔ **DRIFT** |
| 6 | `session_volume.sessions.LONDON.execution_resolution` | 1 | 3 | ⛔ **DRIFT** |
| 7 | `auto_run.start_engaged` | False | True | ✅ **DELIBERATE** |
| 8 | `network.transport` | "rest" | "ws" | ⛔ **DRIFT — RULED D-3, 2026-09-04.** Sync to `"ws"` |
| 9 | `signal_bridge.enabled` | False | True | ✅ **DELIBERATE** |

⭐ **Rows 1 and 2 are new. Nobody has recorded them before, and both are ten times apart.**

⚠ **Row 8 was written CONTESTED and is now ruled DRIFT. So the split is SEVEN re-syncs and
TWO deliberate divergences — not six and three.** The allow-list in §5 is two entries.

**Provenance, traced in git rather than assumed:**

- `indicators.MicroCVD.accel_threshold_dynamic_pct` was **born in agreement** — commit
  `f02b3b2` added `0.03` to *both* files in one commit. The JSON moved to `0.30` at
  **`1e9df84`, settings v33, 2026-06-13**. The POCO never followed.
- `indicators.CVD.slope_pct_of_value` was likewise born in agreement at `0.01` (`f3a6f36` /
  `d044d6e`). The JSON moved to `0.10` at **`61b4532`, settings v34, 2026-06-13**. The POCO
  never followed.

⚠ **Same day, same pair of re-baseline commits, and the same class as the OBV defect** — §0a's
own superseded row already names v33/v34 as drift-producing. **These are the two survivors of
that pair, still live 2026-09-04. Both are scoring inputs, not display keys.**

**Blast radius, verified in the tree rather than reasoned from docs:** all three production
call sites of each key pass the cfg value by name —
`UI/MainForm_Analysis.vb:434`/`:500`, `ExitGuardEvaluator.vb:114`/`:85`,
`tools/BacktestRunner/ReplayLoop.vb:487`/`:500`. **So the app is unaffected and this is NOT a
live scoring change and NOT a dataset boundary.** ⚠ **And `grep` finds ZERO fixture references
to either key — so no fixture pins them, and no fixture would ever have noticed.**

### 3.2 Why rows 7 and 9 are not drift, from the code

Each POCO default here is deliberately the **safe / off** value while the shipped file turns
the feature on. The comments say so at the declaration site — **these two, and only these
two, are the D-1 allow-list:**

- `auto_run.start_engaged` (`EngineSettings.vb:772`) — *"Default False ⇒ byte-identical to
  every prior version on a box that hasn't set it."* §15's v68 row states the routing: tracked
  JSON ships `true` so collectors run hands-off; the dev box opts out via `settings.local.json`.
- `signal_bridge.enabled` (`EngineSettings.vb:1320`) — *"Default False (§8 D3) — flipping it
  on is the trader's dated action after the consumer's log-only validation."*

~~`network.transport` — whether that is drift or a deliberate safe degraded default is **D-3**,
and it is the one of the three I will not rule.~~ ⛔ **RULED 2026-09-04 — it is DRIFT. Sync to
`"ws"` and fix the comment.** Kept struck rather than deleted, per the quote-and-label
convention. See §4.1 for the verification that made the ruling safe to act on.

### 3.3 ⚠ The seven orphans that were mine, not the design's

The probe's **first** run reported **7 orphans**, contradicting the prototype's "zero
orphans". Every one was a nullable session override absent from its JSON object
(`absorption.sessions.*.min_aggr_usd`, `aggressor_velocity.sessions.*.norm_window_sec`,
`structural_levels.sessions.NY.fallback_target_atr_mult`,
`session_volume.sessions.NY.roc_magnitude_threshold`).

**Cause: my check order, not the tree.** I tested *absent from JSON* before *is nullable*. An
absent nullable override means **inherit** — it is the design, not a hole. Reordering the two
tests takes orphans to **0** and reproduces the prototype exactly.

⭐ **Recorded rather than quietly fixed, because it IS trap 2 of §0 and I walked into it
myself on the first attempt.** It is the cheapest possible demonstration that this build is
not transcription.

### 3.4 What the walk correctly does not compare

| Excluded | Count | Why |
|---|---:|---|
| Root provenance keys — `version`, `last_modified`, `modified_by`, `change_log` | 4 | The POCO seeds `Version = 1` against a shipped 68. **Permanently and correctly different.** |
| `<JsonIgnore>` read-only derived properties — `scoring.trade_costs.RoundTripFeePct`, `EffectiveMinMovePct` | 2 | Computed, never serialised (`EngineSettings.vb:920-950`). ⛔ **Exclude them STRUCTURALLY — by `JsonIgnore`-or-not-writable — never by name.** A name list is a fifth copy. |
| `resolution_profiles["1"]` / `["3"]` | 2 | The POCO default is an **empty** dictionary by design (*"an absent block = pure 1-min behaviour"*), so no key is walked. Both its properties are `Double?` anyway. **Nothing to guard here.** |
| Nullable overrides | 15 | The ruling's derived rule. |

---

## 4. ✅ D-table — TICKED IN FULL, trader-directed 2026-09-04

**Four as recommended; D-3 ruled against the spec's abstention.**

| # | Decision | ✅ Ruling |
|---|---|---|
| **D-1** | **How is a DELIBERATE divergence declared?** (a) a small explicit allow-list of paths in the guard, each carrying its reason and the doc that ruled it; (b) a `<PocoDefaultDiffers("reason")>` attribute on the POCO property itself; (c) no distinction — every inequality fails | ✅ **(a) AS RECOMMENDED.** ⚠ **TWO entries, not three — D-3 removed `network.transport`:** `auto_run.start_engaged` and `signal_bridge.enabled`, each citing its ruling. **(b) edits the POCO for a test's benefit** — guards do not reshape the thing they guard. ⛔ **(c) rejected: it reverses two trader decisions to make a fixture green** |
| **D-2** | **The two real calibration drifts (rows 1–2) — re-sync the POCO to shipped?** | ✅ **YES, RE-SYNC, as recommended.** `CvdSettings.SlopePctOfValue` 0.01 → **0.10**; `MicroCvdSettings.AccelThresholdDynamicPct` 0.03 → **0.30**. Precedent twofold — v66 moved the OBV POCO in step, v57 synced `trigger_mode` for the same reason (*"stomp-proofing"*) |
| **D-3** | **`network.transport` — sync the POCO to `"ws"`, or keep `"rest"` and fix the stale comment?** | ⛔ **RULED AGAINST THE SPEC'S ABSTENTION: SYNC TO `"ws"` AND FIX THE COMMENT.** `NetworkSettings.Transport` `"rest"` → **`"ws"`**, and the comment at `EngineSettings.vb:1150` (*"cutover flag; P3 flips the default. Stays 'rest' in P1/P2"*) is rewritten to record that **P3 shipped at cutover v42, 2026-06-24**. ⭐ **Verified safe before acting — §4.1** |
| **D-4** | **Does a drift FAIL the harness or WARN?** | ✅ **FAIL, as recommended** — a warning inside a 317-check run is not read. ⛔ **All THREE re-syncs must land in the SAME commit as the guard**, or the harness ships red |
| **D-5** | **Does scoped-(b) ship here or as its own session?** | ✅ **Its own session, second, as recommended.** Session 1's guard is the instrument that proves a (b) edit changed nothing |

⚠ **All three re-syncs are POCO-default edits with NO settings-key change: no version bump,
settings stays v68, no `change_log` entry, and NOT a dataset boundary** — the app reads JSON
and every production call site passes by name. They still earn a §15 row, because they change
what the **parse-failure path** does.

### 4.1 ⭐ Why D-3 is safe to act on — verified in the tree 2026-09-04, not reasoned from the ruling

The spec abstained on D-3 because syncing to `"ws"` changes what a box does when
`settings.json` fails to parse. **That concern is answered by the code, and the answer is that
the ruling improves the degraded path rather than risking it.**

`ResolveSource` (`UI/MainForm_Analysis.vb:746-759`) carries **two independent REST fallbacks**
on the `transport="ws"` branch: `WsFallbackToRest AndAlso _wsFeed.IsDegraded()` → REST, and
`_wsSource Is Nothing` → REST. **`WsFallbackToRest`'s POCO default is `True`**
(`EngineSettings.vb:1160`). ⭐ **And the probe measured ZERO drift on every other `network.*`
key** — `ws_url`, `ws_heartbeat_sec`, `ws_stale_after_sec`, `ws_cooldown_sec`,
`ws_fallback_to_rest` all already carry the shipped values in the POCO. So a parse-failure box
running on POCO defaults gets the correct endpoint and cadences and still falls back to REST
on degradation.

⭐⭐ **And D-3 repairs an inconsistency nobody had named.** `auto_run.trigger_mode`'s POCO
default is **`"on_close"`** (synced at v57 for stomp-proofing) while `network.transport`'s is
`"rest"` — and `OnCloseModeActive()` (`UI/MainForm_AutoRun.vb:187-192`) returns `False` unless
transport is `"ws"`. **So today's POCO defaults ask for bar-close firing over a transport that
cannot deliver it, and the box silently falls back to interval mode.** Syncing `transport`
makes the two v57-era defaults agree. **This was found while checking D-3, not before it.**

---

## 5. Mechanism — the walk

**Location:** `verify/ordercheck/Program.vb`, as private helpers beside the fixtures. It is
harness-only; nothing in `Core/` or `tools/` gains a dependency.

**Signature.** A parameterised walk, **not** an inline body — A62b–e must call it with a
different POCO/JSON pair:

```
Private Function WalkPocoVsJson(poco As Object, el As JsonElement, prefix As String,
                                isRoot As Boolean, result As DriftWalkResult) As …
```

`DriftWalkResult` carries four lists — `Drifts`, `Orphans`, `JsonOnly`, `Skipped` — plus a
`Compared` counter. ⭐ **`Compared` is load-bearing, not diagnostics: A62a asserts a floor on
it, so a walk that silently visits nothing cannot report clean.** That is this project's
standing *"assert the check RAN"* lesson made structural rather than remembered.

**Per public instance property, in this exact order:**

1. **No `JsonPropertyName`, or `<JsonIgnore>`, or not writable** → skip (§3.4). Structural test.
2. **Root provenance key** (`version` · `last_modified` · `modified_by` · `change_log`) and
   `isRoot` → skip.
3. ⛔ **Nullable (`Nullable.GetUnderlyingType` is not `Nothing`) → skip. THIS RUNS BEFORE THE
   ABSENT-KEY TEST** (§0 trap 2, §3.3).
4. **Key absent from the JSON object** → `Orphans`.
5. **Scalar** (`String` · `Double` · `Integer` · `Boolean` · `Long`) → compare. `Double` on
   `Math.Abs(a - b) < 1e-9`, never string equality; every render and parse through
   `CultureInfo.InvariantCulture`. On mismatch → `Drifts`, **unless the path is on the D-1
   allow-list.**

   ⭐ **The D-1 allow-list is EXACTLY TWO entries. Adding a third is the §0 escalation
   trigger, not a build step:**

   | Path | Why it is not drift | Ruled by |
   |---|---|---|
   | `auto_run.start_engaged` | POCO `False` keeps a box byte-identical to v67 until it opts in; tracked JSON ships `true` for hands-off collectors | `collector-ops-tooling-proposal.md` §1.4 |
   | `signal_bridge.enabled` | POCO `False`; flipping it on is the trader's own dated action | signal-bridge §8 **D3** |
6. **`Dictionary(Of String, T)`** → recurse only into keys present on **both** sides.
7. **`List(Of T)`** where `T` is a settings class → **match elements by their `name` property
   where the type has one, falling back to index.** ⛔ **Name-matching is required, not a
   nicety: index-matching silently compares ASIA against LONDON the first time anyone reorders
   the `sessions` array.** `List(Of String)` → skip.
8. **Class** → recurse.

After the property loop, enumerate the JSON object's own keys and record any not seen →
`JsonOnly`. **That half is what proves the POCO is complete**, and it is measured at 0 today.

**Key resolution is CASE-INSENSITIVE** — matching `SettingsLoader`'s
`PropertyNameCaseInsensitive = True` (§0 trap 3).

### 5.1 Locating the tracked `settings.json` — and failing loudly if it cannot

⛔ **Do not add a `CopyToOutputDirectory` item to `OrderCheck.vbproj`.** That creates a build
artefact copy which lags the tracked file — a **fifth copy**, and precisely the drift class
this guard exists to catch. CLAUDE.md already warns that the app's `bin\` copy legitimately
lags.

⛔ **Do not anchor on `Directory.GetCurrentDirectory()`.** It is **not stable here**:
`verify-gate.ps1:76` runs `dotnet run --project verify/ordercheck/OrderCheck.vbproj` from the
repo root, while `OrderCheck.vbproj`'s own header documents running it *from
`verify/ordercheck`*. ⚠ Queue item 21 is on record that *"three separate ways of setting a
child process's working directory failed to redirect it."*

**Use `AppContext.BaseDirectory`** — always `verify/ordercheck/bin/<Config>/net8.0/` — and walk
**up** until a directory holds **both** `DeribitVerdictEngine.sln` **and** `settings.json`.
Requiring both makes the anchor unambiguous.

⛔ **If the walk-up finds nothing, the fixture FAILS with that message. It must never skip,
warn, or pass.** A guard that silently does nothing when it cannot find its input is the
"reports success it never performed" defect this project has now recorded five times.

---

## 6. Fixtures — family **A62**

**A61 is the highest family in use** (`A61a`–`A61f`, items 17/18, 2026-09-03). **A62 is free.**

⛔ **Write A62b FIRST.** It is the only one that breaks the write-the-test-and-the-code-with-
the-same-misunderstanding loop (§0).

| Fixture | Asserts | Mutation that must make it FAIL |
|---|---|---|
| **A62b** ⭐ | **Teeth, independent of any real drift.** Take a `New EngineSettings()`, mutate exactly one scalar in memory, walk it against the shipped JSON, and assert the result names **that path and no other** | Neuter the scalar comparison → A62b fails while a shipped-tree-only fixture would still pass |
| **A62a** ⭐ | **The shipped tree is clean.** Walk `New EngineSettings()` against the tracked `settings.json`: unexplained drifts = 0 · orphans = 0 · JSON-only = 0 · **`Compared` ≥ 200** | Revert **any of the three** re-syncs (D-2's two, or D-3's `transport`) → A62a fails naming that path. **Run all three separately** |
| **A62g** ⚠ | **The allow-list is a list, not a blanket.** `auto_run.start_engaged` and `signal_bridge.enabled` are tolerated; assert that a THIRD mismatch on any other Boolean path is still reported | Widen the allow-list to "any Boolean" → A62g fails. ⛔ **Without this, the allow-list can silently become a class exemption** |
| **A62c** | **The nullable rule, both arms.** A nullable override ABSENT from JSON is not an orphan; one PRESENT in JSON is not compared | Move the nullable test after the absent-key test → 7 false orphans (§3.3) |
| **A62d** | **Case-insensitive resolution.** A hand-built JSON object whose key casing differs from the `JsonPropertyName` still resolves | Switch to case-sensitive `TryGetProperty` → false orphan |
| **A62e** | **The resolver fails loudly.** Point the walk-up at a temp directory with no marker; assert a FAIL with the path in the message | Make the resolver return a default or skip → A62e fails |
| **A62f** | **Structural exclusion.** A `<JsonIgnore>` read-only property is skipped by shape, not by name — assert `trade_costs` compares its four real keys and neither derived one | Swap the structural test for a name list → A62f fails |

⚠ **Every mutation above must be RUN — reverted, confirmed FAIL, restored, confirmed PASS —
and the result recorded in the spec-back.** [`seat-handover-2026-09-03.md`](seat-handover-2026-09-03.md)
§5 is unambiguous that nothing here has ever been caught by care.

---

## 7. Session 2 — scoped (b), dead-code removal

**The ruling:** delete the `Optional` default from method parameters that mirror a settings
key, making them required. **This is dead-code removal, NOT the fix** — the fix is the guard.

⛔ **Step 1 is to RE-MEASURE, not to edit.** The ruling's figure — **9 fixture omissions, 0
production omissions** — is **inherited from the second-opinion doc and I did NOT re-measure
it.** It is load-bearing in one direction only: if any *production* site omits one of these
arguments, deleting the default is a live behaviour change, not a cleanup. Enumerate every
call site of each affected method across `UI/`, `Core/`, `analysis/` and `tools/` and record
the two counts before touching a signature.

**Exclusions, carried from the ruling:** leave `maxAgeMs`
(`Indicators_OrderFlow.vb:527`), `nowUtc` (`Indicators_Volatility.vb:27`/`:47`/`:67`) and the
documented `CalcSpread` discard alone — internal conveniences, ⚠ **though note both
`CalcSpread` parameters DO have settings counterparts** (`SpreadSettings.WideThresholdBps` /
`TightThresholdBps`), so read `Indicators_OrderFlow.vb:558-562` before deciding what the
"discard" refers to rather than assuming.

**VB positional rules do not bite** — the settings-mirroring optionals already precede the
trailing genuinely-optional parameters, so promoting one to required keeps the ordering legal.
**Confirm this per method rather than trusting the general claim.**

---

## 8. Out of scope — named so they are not lost

- ⛔ **Copy 4, the fixture literals.** `A6` pins `trendGate:=10.0` against a shipped 23.0 and
  is **stale**; `A20a`/`A20b` pass off-spec OFI thresholds **legitimately**. Machine-
  indistinguishable. Item **17** ruled the convention (MECHANISM, 2026-09-03); applying it to
  `A6` is its own slot and is still open.
- ⛔ **The method `Optional` default of `CalcCVD.slopePctOfValue` (0.05).** Session 2 removes
  the default; it does not re-baseline it.
- **`OrderCheck.vbproj`'s header comment calls `verify/` gitignored. It is tracked**
  (`git ls-files verify/` returns both files). Doc-only; a rider for the next commit that
  opens that file.

---

## 9. What I verified, and what I did not

**Verified this session, by running or reading the tree:**

- The 9 divergences, 261 scalars, 0 orphans, 0 JSON-only, 15 nullable skips — **measured** by
  a scratchpad probe linking the real `EngineSettings.vb` against the tracked v68
  `settings.json`, 2026-09-04.
- Rows 1–2 born in agreement and diverged at `1e9df84` (v33) and `61b4532` (v34), both
  2026-06-13 — **traced in `git show`**, not inferred.
- All six production call sites of those two keys pass by name — **`grep` across the tree**.
- Zero fixture references to either key — **`grep` over `verify/ordercheck/Program.vb`**.
- The three deliberate divergences' declaration-site comments — **read at
  `EngineSettings.vb:772`, `:1150`, `:1320`.**
- The two derived properties are `<JsonIgnore>` `ReadOnly` — **read at `:920-950`.**
- `A61a`–`A61f` are the highest fixture ids in use; **A62 is free.**
- `verify-gate.ps1:76` invokes the harness from the repo root; `verify/` is tracked.

**NOT verified — carried forward and flagged:**

- ⛔ **The "9 fixture omissions, 0 production omissions" figure behind session 2.** Inherited
  from [`seam-audit-decisions-second-opinion-2026-08-11.md`](seam-audit-decisions-second-opinion-2026-08-11.md).
  §7 makes re-measuring it step 1.
- **That 261 is the complete comparable population.** It is what this walk visits under §5's
  rules. A property added with no `JsonPropertyName` would be skipped silently — A62f guards
  the derived case, not that general one.
- ~~**Whether `network.transport` staying `"rest"` is deliberate.**~~ ✅ **CLOSED 2026-09-04 —
  RULED DRIFT (D-3), sync to `"ws"`.** Before acting I verified the degraded path in the tree
  (§4.1): `ResolveSource`'s two REST fallbacks, `WsFallbackToRest` defaulting `True`, and zero
  drift on every other `network.*` key. **That verification is new this session and is
  recorded as measured.**
- **Nothing was built or run against the harness.** No `dotnet build`, no fixture added. This
  session produced a spec and a throwaway probe.

---

## 10. Build record

| Event | Date | Detail |
|---|---|---|
| Spec written | **2026-09-04** | `6e1753b`. Probe measured 261 scalars / 9 divergences against tracked v68 |
| D-table ticked | **2026-09-04** | Trader-directed. D-1/D-2/D-4/D-5 as recommended; **D-3 ruled against the abstention** — sync `transport` to `"ws"` |
| Session 1 built | — | ⛔ Not started |
| Session 2 built | — | ⛔ Not started |
