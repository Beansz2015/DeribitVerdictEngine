# `S2-2` — `CalcSpread` split: spec-back (review packet)

**For the reviewing seat.** The outcome record is [`s2-2-calcspread-split-batch-summary.md`](s2-2-calcspread-split-batch-summary.md); this is the working document.
**Spec reported against:** [`s2-2-calcspread-split-proposal.md`](s2-2-calcspread-split-proposal.md), built from its **§4b**.
**Base commit:** `f6a2c08`. **Build commit:** `57b55f9`. **Local only — not pushed.**

⚠ **All dates UTC.** `date -u` was run: **2026-09-06 16:43 UTC**, against a workstation clock reading 2026-09-07 (GMT+8).

**Model + effort for a review of this:** **Sonnet, effort medium.** The judgment is all in the spec and was ruled; what remains is checking that seven cheap handles print what §1 says and that the parity argument's two halves both hold. **Escalate to Opus/high only if handle `H-1` or `H-7` disagrees** — either would mean the parity claim is wrong, which is the one claim in this build that cannot be repaired by a follow-up.

---

## 1. Ranked verification handles

**Every handle below was RUN at `57b55f9` and the printed value pasted.** Ranked by how much of the build each covers.

### ⭐ If you only run one, run `H-1`.

| # | Claim | Handle | Printed at `57b55f9` |
|---|---|---|---|
| **H-1** | Parity holds — no rendered string or colour moved | Rebuild the §3 instrument, run pre/post, `md5sum` both | `fbcafa201a26d45e8c8d72ab5923183a` **both sides**, 3706 bytes both |
| **H-2** | The render code is untouched (parity's *other* half) | `git show --name-only --format="" HEAD \| grep -cE "MainForm_Render_Cards\|MainForm_PlaintextSnapshot\|ScoringEngine_Calculate_Scoring"` | **`0`** |
| **H-3** | Harness green at the right count | run `OrderCheck.dll`; `grep -cE "^PASS "` | **`332`** (`ALL PASS`, `FAIL` = `0`) |
| **H-4** | `CalcSpread` is genuinely gone, not just unreferenced | `grep -rn "CalcSpread(" --include=*.vb . \| grep -v /obj/ \| grep -v /bin/ \| grep -v "CalcSpreadBps(" \| grep -vE ":[0-9]+:[[:space:]]*'" \| wc -l` | **`0`** |
| **H-5** | **All five** guards return `Nothing` — the partial-conversion shape | `sed -n '/Public Shared Function CalcSpreadBps/,/^    End Function/p' Core/Indicators_OrderFlow.vb \| grep -c "Then Return Nothing"` | **`5`** |
| **H-6** | The evaluator no longer reads the spread thresholds — **the point of the build** | `grep -c "Indicators\.Spread" LiveMicrostructureEvaluator.vb`, then the same against `f6a2c08` | **`0`** now · **`2`** at base |
| **H-7** | Settings frozen | `git show --stat --format="" HEAD -- settings.json \| wc -l`; `sed -n '2p' settings.json` | **`0`** · `"version": 68` |

### Notes on why these handles and not the obvious ones

⛔ **`H-4` is deliberately NOT `grep -c "CalcSpread"`.** **MEASURED at `57b55f9`: that grep prints `19` lines, and it always will.** `CalcSpreadBps` contains the string (11 of the 19), and **8** comment lines legitimately narrate the deleted method by name — 3 in `Core/Indicators_OrderFlow.vb`, 1 in `UI/MainForm_Analysis.vb`, 4 in `verify/ordercheck/Program.vb`. ⚠ **A reviewer following a naive name-count would see 19 and reject a correct build.** That is the `_lastTs` failure verbatim (`docs/trade-store-write-guard-spec-back.md` §R1), reproduced here at nearly ten times the magnitude. **Counting a name is a copy of the property and drifts the moment a comment mentions it.** The handle above excludes the successor token and excludes comment lines, so it asserts *executable references* — and it is paired with `grep -c "Sub CalcSpread("` = **`0`** for the declaration itself.

⛔ **`H-3` is a harness RUN, never a source count.** The spec warns that `grep -c '^\s*Check('` prints 333 and the unanchored form 342, and **neither is the harness count**. The 328 baseline was likewise re-measured by running `OrderCheck.dll` at `f6a2c08`, not carried from the spec's `2e8bc76` reading.

⚠ **`H-5` is unanchored on purpose.** All five guards are VB's inline `If … Then Return` form, which a `^\s*Return` anchor misses entirely — CLAUDE.md's VB grep rule, and here the missed form *is* the one the claim is about.

⭐ **`H-1` and `H-2` are one claim in two halves and neither is sufficient alone.** `H-1`'s instrument transcribes the two card bindings (they need WinForms and cannot be linked host-agnostically), so `H-1` alone rests on a transcription. `H-2` proves the real renderers did not move but says nothing about the values fed to them. **Run both or neither.**

---

## 2. Decisions queued, with my read

### `Q-1` — schedule the `D-3` residual, or leave it standing?

`D-3` was ruled **(b)**: leave `HasTopOfBook` alone. That was right for *this* build. It leaves a divergence that is now **visible rather than latent**: `HasTopOfBook` tests 3 conditions, `CalcSpreadBps` tests 6, and on a zero-priced top of book the live strip reads `snap.HasSpread = True` beside a `Nothing` bps — **captured in the parity output, not inferred.**

- **(a)** schedule a one-line follow-up moving `snap.HasSpread` onto `bps.HasValue`
- **(b)** leave it standing as a permanent documented residual

⭐ **My read — hypothesis, not a recommendation: (a), but low priority and NOT bundled with anything.** The spec's own §4 already calls (a) *"defensible on the merits … strictly more truthful"*, and a zero-priced top of book genuinely is *no spread*. What made it wrong for `S2-2` was that it is a **real live-strip behaviour change** and `S2-2`'s whole claim was zero behaviour change on every surface. Standing alone it can carry its own line in the record, which is exactly what §4 said it deserved.

⚠ **Scoping, offered without recommending:** the narrowest version is **one line** at `LiveMicrostructureEvaluator.vb:141`, no settings key, no new method. But it **is** a display change on the live strip, so the display-string parity rule is live for it too and it needs its own before/after capture — which is the actual cost, not the line.

### `Q-2` — does the frequency question stay unanswerable?

The spec's §3.4 states plainly that it did **not** measure how often a degenerate book reaches `UI/MainForm_Analysis.vb:422`, and that it is **not measurable from the book**: `SpreadStatus` has no CSV column, and the degenerate and locked cases both log `0.00`.

⭐ **My read: leave it unanswerable, and do not add a column for it.** §8 correctly calls a new column a **schema rotation**, and the fix did not depend on the frequency — reachability was established from the code, which is all the build needed. **Paying a rotation to measure something that would not change any decision is the wrong trade.** ⚠ **But flag the asymmetry:** if the `D-3` follow-up (`Q-1`) is ever taken, the same blindness applies to *it* and there the frequency **would** matter, because it is a behaviour change rather than a preservation. **`Q-1` and `Q-2` share a root and are cheaper ruled together.**

### `Q-3` — is `[no-engine-change]` the honest token for this commit?

The commit edits `Core/Indicators_OrderFlow.vb`, which `verify-gate.ps1`'s `$enginePrefixes` treats as an engine path, and carries `[no-engine-change]` so the settings-bump nudge passes.

⭐ **My read: yes, and the proof is what makes it honest.** The token's meaning in this repo is *"not a behaviour change requiring a settings bump"*, and that is established here by measurement rather than by claim. ⚠ **I have no read on whether the token should be narrowed** to distinguish "engine file touched, behaviour proved identical" from "engine file not touched at all" — that is a gate-design question and the criterion is the reviewer's.

---

## 3. Spec-back proper — feedback on the spec itself

### What it got right, specifically

⭐⭐ **"§6 therefore names the input shape, not just the assertion" is the sentence that did the real work.** It is the reason `A65a` was built by hand instead of through `MakeBook`, and the reason `A65a` covers two *different* guards rather than the same guard twice. **A fixture written from the assertion alone would have used `MakeBook`, been structurally incapable of expressing an empty ladder, and read as coverage** — the exact shape `A62f` and `A63a` failed in.

⭐⭐ **Naming `A65a` and `A65b` as a PAIR, with the reason, is the single highest-value line in the spec.** Mutation 2 confirmed it by running: with `ClassifySpread` returning `"NORMAL"` unconditionally, **`A65a` still passes**. Without `A65b`, that mutation ships and silently retires the TIGHT state everywhere. A reviewer would not derive that from `A65a`'s text.

⭐ **Ordering §4b before §4, and saying so four times, worked.** Building from §4 would have produced a `Double` return.

⭐ **The §4b.1 self-correction — "the first draft of this section said `:576` and was wrong — re-read the range before deleting, do not trust this line either"** — is the right shape for a spec to carry. The range was re-read (`:568-610`) and `:571` is correct.

### Which assumptions broke

⚠ **§4b.3 was narrower than its own words.** It says *"the stale-prose comment goes with it"*, singular, and shows the block at `:132-134`. **There were two more**, both in the same file, both naming `CalcSpread` as a live reused function:

- `LiveMicrostructureEvaluator.vb:12` — *"reuses the engine's pure indicator functions (CalcTFI / CalcSpread / CalcOFI)"*
- `LiveMicrostructureEvaluator.vb:93` — the same claim in the method's `''' <summary>`

**Both were corrected to `CalcSpreadBps`.** They are outside §4b's literal instruction and inside its intent — a comment naming a deleted method is the rot the split exists to prevent. **Recorded rather than quietly fixed** because the generalisation is worth having: a spec that says "delete the method" should say **"and grep the whole file for its name"**, not point at one comment block.

⛔ **Nothing else in the spec was found wrong.** §2's map, §3's arithmetic, §5's three traps and §6's four input shapes all held on inspection. §3's arithmetic was independently **confirmed by measurement** before any code moved — see the batch summary §0.

### Where a constraint pair nearly conflicted

⚠ **§4b's build order ("fixtures before callers") and the compiler are in tension, and the spec does not say how to resolve it.** Fixtures against the new methods cannot compile until the methods exist, and deleting `CalcSpread` breaks both callers immediately — so a literal reading leaves no order that keeps the tree green.

**The hatch used:** items 4b.1 → 4b.4 → 4b.2 → 4b.3, with a single build at the end. **The fixtures were written from §6's table without reading the new call sites**, which is what §4b's ordering is actually protecting — *"the harness proves the composition instead of following it"*. The tree does not compile between 4b.1 and 4b.3, and that is unavoidable under `D-4` (a). **Worth one sentence in the next spec of this shape**, because the obvious alternative — keeping `CalcSpread` as a temporary shim — is precisely what `D-4` forbids.

### On the spec's own §0 warning about itself

⭐ The spec predicted *"the obvious implementation is wrong in a way that READS CORRECT"* and *"a reviewer reasoning about this split ticks it green."* **That prediction is testable here and it held.** The three-line naive composition in §3.2 looks correct on the page; only running it against shapes 5–8 shows the flip. **Everything asserted in this build about behaviour was run.**

---

## 4. What I did not verify, and cannot

- ⛔ **The live app was never launched.** No verdict ran against a real Deribit order book. The parity proof is over **constructed** book shapes through the shipped composition, not over live traffic. **Nothing here says a degenerate book has ever actually reached `:422` in production** — the spec's §3.4 says the same and calls frequency unestablished.
- ⛔ **The two card bindings' actual `Color` structs were not resolved.** `Theme.ACC_STRONG_LONG` / `ACC_SHORT` / `FG_TERTIARY` are compared as **token names**, because linking `MainForm_Render_Cards.vb` needs WinForms. `H-2` is what covers this gap, and it covers it by showing the file is untouched — **not** by evaluating the colours.
- ⛔ **The MiniMeter's rendered pixels, and `BuildMiniMeter`/`MakeSignalRow` themselves, were not exercised.** Only their *inputs* — text, percentage, colour token — were compared.
- ⚠ **`spreadPct` is captured but is NOT part of the parity claim's risk.** It derives from `r.SpreadBps` alone, never from `SpreadStatus`, so this build could not move it; it is in the capture for completeness and it was identical.
- ⚠ **No performance measurement.** `CalcSpreadBps` returns a `Double?` where the old method wrote a `Double` by reference. **Boxing/allocation was not measured.** It is called once per analysis run and once per live-strip tick, so I do not believe it matters — **but I did not measure it and am not claiming it.**
- ⚠ **The auto-tweaker was not run against the new keys.** The claim that `indicators.spread.` is tweaker-reachable is read from `SettingsDiffApplier.RejectedPathPrefixes` **not containing it** — carried from the spec, checked by reading, **not exercised by running the tweaker**. `A65c` pins the consequence (arm order under an inverted pair) regardless.
- ⚠ **Only `local-fast` was run for `verify-gate.ps1`.** `prepush` and `ci` modes were not, since the change is not being pushed.

---

## 5. ⭐ REVIEWER VERDICT — 2026-09-06 (UTC). **ACCEPTED.** Two findings, neither blocking; three questions ruled.

**Reviewing seat:** Opus, effort **HIGH**. ⛔ **The packet's own recommendation of *"Sonnet, medium"* is CORRECTED — see §5.4; the reason is structural, not a judgement about care.**

**Nothing was taken on report.** Every handle was re-run at `57b55f9` and all three mutations were re-applied, rebuilt and run.

### 5.1 Independently reproduced — printed values, this seat

| Handle | Packet's value | This seat | |
|---|---|---|---|
| **H-2** render files in the diff | `0` | **`0`** | ✅ |
| **H-3** harness | `332`, ALL PASS, FAIL 0 | **`332` PASS · `0` FAIL · `ALL PASS` · 0 warnings** | ✅ |
| **H-4** executable refs to `CalcSpread` | `0` | **`0`**, and `Sub CalcSpread(` = `0` | ✅ |
| **H-4** the naive grep it avoids | `19` | **`19`** — the trap reproduces exactly | ✅ |
| **H-5** guards returning `Nothing` | `5` | **`5`**, and guards returning `0.0` = **`0`** | ✅ |
| **H-6** evaluator ↔ `Indicators.Spread` | `0` now · `2` at base | **`0`** now · **`2`** at `f6a2c08` | ✅ |
| **H-7** settings frozen | `0` · v68 | **`0`** · `"version": 68` | ✅ |
| **H-1** parity MD5 | `fbcafa20…` | ⛔ **NOT RE-RUNNABLE — see `R-1`** | ⚠ |

**Diff scope confirmed:** six files, exactly §4b's six items. No render surface, no `settings.json`, no `IndicatorResults` field type. **No escalation trigger fired**, verified against all three conditions.

### 5.2 The three mutations — RE-RUN by this seat, not accepted on report

⚠ **The first attempt at this used a `python` one-liner. Python is not installed on this box, the edit never applied, and the harness printed four PASS lines against UNMUTATED code.** ⛔ **Recorded because it is the "assert the check RAN" failure, live, in the act of checking someone else's work.** Re-done with the editor and each mutation confirmed present in the file before building.

| Mutation | Packet's claim | This seat's run | |
|---|---|---|---|
| **(1)** empty-ladder guard returns `0.0` | `A65a` FAILS, others PASS | **`A65a` FAILS · whole-harness FAIL = 1** | ✅ |
| **(2)** `ClassifySpread` always `"NORMAL"` | `A65b` FAILS, **`A65a` still PASSES** | **`A65a` PASSES · `A65b`/`A65c`/`A65d` FAIL** | ✅ |
| **(3)** the two arms swapped | `A65c` FAILS alone | **`A65c` FAILS ALONE · whole-harness FAIL = 1** | ✅ |

⭐⭐ **Mutation (1)'s printed detail is better than the packet claims and deserves to be on the record:**

```
emptyAsks: hasValue=True status=TIGHT | zeroBid: hasValue=False status=NORMAL
```

**That is the PARTIAL-CONVERSION signature** — one guard converted, one not — visible only because `A65a` puts two different guards in one assertion and prints both. §4b.1 warned that a half-converted method re-creates the flip through one path alone; `A65a` catches it *and names which path*.

⭐ **Mutation (3) is the non-obvious result and it held: `A65c` fails ALONE, `A65d` passes.** One would expect the boundary fixture to catch arm order. It cannot — at the shipped 5.0/1.5 the arms do not overlap, so only `A65c`'s deliberately inverted MECHANISM literals can express the state. **The packet's design reasoning is correct and is now demonstrated on the harness.**

⭐ **Mutation (2) confirms the pair argument by running it.** `A65a` alone would have licensed a `ClassifySpread` that returns `"NORMAL"` unconditionally — silently retiring the TIGHT state on every surface. **Also confirmed: all 328 pre-existing fixtures pass under mutation (2)**, which independently establishes the packet's claim that `A65` is the first coverage this method has ever had.

### 5.3 ⚠ `R-1` — **a handle only its author can run.** Not blocking; the claim stands by another route

⛔ **`H-1`'s instrument was never committed** (batch summary §3.1: *"a temporary console project, scratchpad only"*). Searched the tree — it is not there. **So the packet's headline handle — *"if you only run one, run `H-1`"* — cannot be run by any reviewer, now or later, and `fbcafa201a26d45e8c8d72ab5923183a` is unreproducible.**

⛔ **And §1's own pairing instruction makes it worse: *"Run both or neither."*** A reviewer obeying that literally runs **neither**, and the one acceptance item the spec called *"not a formality"* goes unchecked. The escalation trigger inherits the flaw — *"escalate if `H-1` disagrees"* cannot fire on a handle nobody can run.

⭐ **This is a NEW shape, and worth naming beside the two rules already in `CLAUDE.md`.** The standing rules cover *"a handle that tests a string, not the property"* (`_lastTs`) and *"a handle that was never run"*. Neither anticipates **a handle whose instrument does not survive the build.** It was run honestly, it was reported honestly, and it is still not a handle — because a handle is a thing the *reader* can execute. **A one-way measurement is evidence; only a re-runnable one is a handle.**

⭐⭐ **The parity claim itself is NOT in doubt, and the durable proof is already in the tree — the packet just ranked it wrong.** The four rendered surfaces are pure functions of `(r.SpreadBps, r.SpreadStatus)` plus one unrelated `cfg` read for the meter percentage. So:

> **`A65a` + `A65b` (the field values are unchanged on the shapes that matter) + `H-2` (the renderers are untouched) ⇒ parity.**

All three are in the tree and all three were re-run by this seat. **That triad should have been ranked first; `H-1` was a build-time instrument, not a review handle.**

### 5.4 ⚠ `R-2` — deleting the instrument left exactly one property unguarded

**Traced, not inferred.** `verify/ordercheck/OrderCheck.vbproj` links `LiveMicrostructureEvaluator.vb` but **does not link `UI/MainForm_Analysis.vb`** (measured: 1 and 0 matches). So:

| Composition line | Coverable by the harness? | Covered on the degenerate path? |
|---|---|---|
| `UI/MainForm_Analysis.vb:423` — `r.SpreadBps = If(spreadBps.HasValue, spreadBps.Value, 0.0)` | ⛔ **No — structurally. `UI/` needs WinForms** | **No.** `H-1` covered it by copying the call-site body verbatim; that instrument is gone |
| `LiveMicrostructureEvaluator.vb:141` — `snap.SpreadBps = If(bps.HasValue, bps.Value, 0.0)` | ✅ yes, the file is linked | **No.** `A19a` uses a healthy book; `A19e` passes an empty `MarketState`, so `GetBook()` returns `Nothing` and the whole `If book IsNot Nothing` block is skipped |

⚠ **Both lines are CORRECT — read, not assumed.** The finding is that nothing guards them: swap either `0.0` for `-1.0` and the harness still prints 332.

⭐ **The symmetry is the point.** `S2-2` removed a threshold divergence *by construction* and left a smaller unguarded composition behind it. **Not a defect and not a blocker** — but it is why `R-1` matters in practice rather than only in principle: the instrument was the only thing covering the `UI/` half, and it was thrown away.

**Disposition: no action required for this build.** If the `Q-1` follow-up is taken it closes the evaluator half for free (see below). The `UI/` half is uncoverable without an instrument and should be accepted as such, explicitly, rather than left looking like an oversight.

### 5.5 The three queued questions — RULED

#### `Q-1` — the `D-3` residual ⇒ ✅ **(a). Schedule it. Low priority, NOT bundled.**

⭐ **The implementer's read is right and the justification is stronger than the one offered.** `UI/MainForm_LiveStrip.vb:221` was read and it settles it:

```vb
parts.Add(If(s.HasSpread, s.SpreadBps.ToString("0.0") & " bps", "-- bps"))
```

**`HasSpread` gates exactly one thing: whether a number or `-- bps` is printed.** It is the *"is this number usable"* gate, not a *"does the ladder exist"* gate. So on a zero-priced top of book the strip prints **`0.0 bps`** where `-- bps` is the honest output.

⛔⛔ **That is the SAME fabricated-measurement class this very build exists to remove** — the absorption row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 states it directly: *"a zero … would be a fabricated measurement the study cannot tell from a real one."* **`S2-2` removed a fabricated `TIGHT` from four surfaces and left a fabricated `0.0 bps` standing on a fifth.** That reframes it from tidy-up to **consistency**, which is why it gets scheduled rather than left.

⚠ **Why it stays LOW priority and unbundled:** a zero-priced top of book on a Deribit BTC-PERP ladder is not a real market state — it is a defensive guard. **Reachability is effectively nil, so nothing is being lost by waiting.** The cost is the display-parity capture, not the line — the implementer identified that correctly.
⭐ **Bonus, and it is why (a) beats (b) on more than principle:** `snap.HasSpread = bps.HasValue` puts the evaluator's composition line under whatever fixture covers `HasSpread`, closing half of `R-2` for free.

#### `Q-2` — the frequency question ⇒ ✅ **Leave it unanswerable. Do NOT add a CSV column.**

**Agreed, and the reasoning is endorsed as written:** a schema rotation to measure something that would not change any decision is the wrong trade, and reachability was established from the code, which is all the build needed.

⭐ **The flagged asymmetry is correct and is now ruled, so it does not have to be re-argued at `Q-1`'s build:** `Q-1` *is* a behaviour change, so frequency would matter there in a way it did not here. ⛔ **It still does not justify a column.** The right instrument for `Q-1` is the before/after parity capture on the two states, not a rotation — the state is synthetic and constructible, which is exactly why it needs no telemetry.

#### `Q-3` — is `[no-engine-change]` honest here ⇒ ✅ **Yes. And no, do not narrow the token.**

The token's meaning in this repo is *"no behaviour change requiring a settings bump."* This build established that by measurement. **Honest.**

⛔ **On the narrowing question, which the packet correctly left to this seat: do not narrow it.** A second token distinguishing *"engine file touched, behaviour proved identical"* from *"engine file not touched"* would be a distinction `verify-gate.ps1` does not act on differently — **a new thing to get wrong, guarding nothing.** The proof belongs in the commit body, where it already is. ⭐ **The general rule: add a token when the gate will branch on it, not to describe a nuance prose already carries.**

### 5.6 On the review-effort recommendation — ⛔ **`Sonnet, medium` was too low, for a structural reason**

**Not a comment on the packet's care, which is high.** The recommendation assumes the handles are runnable and that the escalation trigger can fire. **One of the two headline handles cannot be run at all**, so a medium-effort review following §1 literally would have skipped the parity check under *"run both or neither"* and stopped. **Neither `R-1` nor `R-2` is reachable from the handle list** — both needed reading the `.vbproj` linkage and searching the tree for the instrument.

⭐ **The generalisation for the next packet: rank handles by whether the READER can run them, and mark any measurement that cannot be reproduced as evidence rather than as a handle.** A packet whose top-ranked handle is unrunnable mis-sizes its own review.

### 5.7 On the spec, from the reviewing side

⭐ **§4b.3's under-scoping is confirmed and the implementer's generalisation is adopted.** Two further comments named `CalcSpread` as a live function (`LiveMicrostructureEvaluator.vb:12` and `:93`); both were correctly fixed. **The spec should have said *"delete the method AND grep the whole tree for its name"*, not pointed at one comment block.** ⛔ **Note the sharp edge: the tree-wide grep now returns 19 hits, 8 of them legitimate narration** — so the instruction has to be *"grep and read"*, never *"grep until zero"*, or it recreates the `_lastTs` trap it is meant to prevent.

⭐ **The build-order tension in §3 is real and the hatch was right.** Under `D-4` (a) there is no order that keeps the tree compiling, because the shim that would allow one is the thing `D-4` forbids. **The next spec of this shape should say so in one sentence: "the tree will not compile between the first and last items; that is expected and is the cost of no-wrapper."**

⭐ **One thing the implementer improved on the spec without flagging it, worth recording:** the `'''` XML doc comments escape `&lt;` / `&gt;`, while the plain `'` comment leaves `<=` raw. **The spec's §4b.1 gave the raw form inside `'''`, which would have produced XML-doc parse warnings.** The build is 0 warnings because the implementer noticed. **Corrected silently and correctly.**

### 5.8 Verdict

✅ **ACCEPTED.** The build does what the spec ruled, the parity claim holds by a route that survives in the tree, and every mutation reproduces. **`R-1` and `R-2` are recorded, neither blocks.** `Q-1` scheduled as a low-priority unbundled item; `Q-2` and `Q-3` closed.

⛔ **Nothing is owed by the trader.**
