# `A64a` / `A64b` — the two I17-SWEEP mechanisms made permanent — spec-back

**Solo build, one document** — the build is two fixtures, well under the multi-lane threshold
[`batch-review-packet-convention.md`](batch-review-packet-convention.md) governs. Its four
sections are followed anyway, because this goes back to the same reviewing seat.

**Ruled at:** [`i17-sweep-spec-back.md`](i17-sweep-spec-back.md) §5.3, `D1` = **(c), SCOPED**.
**Findings pinned:** [`i17-sweep-batch-summary.md`](i17-sweep-batch-summary.md) §4 — `F1`
(the spare vote), `F2` (partial vacuity), `F5` (the joint dependency).

---

## 0. Outcome

| | Result |
|---|---|
| Fixtures added | **2** — `A64a`, `A64b`. Family `A64` was free (highest existing was `A63b`) |
| Harness | **328 PASS · 0 FAIL · `ALL PASS`** (326 → 328) |
| Mutation proofs | **5, every one RUN** — see §2. All five behave as required |
| Build matrix | Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck`, each **0 errors / 0 warnings** Release, separately |
| `verify-gate.ps1 -Mode local-fast` | **`GATE PASSED`** |
| Settings | Unchanged — still **v68** |
| Production code | **None touched.** `verify/ordercheck/Program.vb` only |
| Display-string parity | **NO OBLIGATION** — see §4 |
| ⚠ Scope taken beyond "two Subs" | **Yes, deliberately — see §1. Flagged for a possible back-out** |

---

## 1. ⚠ THE ONE JUDGEMENT CALL — I extracted shared helpers, which is more than "add two fixtures"

**The problem.** `A64a` guards `A9`'s configuration and `A64b` guards `A2`'s. To do that they
must exercise *those fixtures' actual* datasets and literals. Writing `A64a` the obvious way
means restating `A9`'s 70-candle builder and its four literals (`7 / 7.5 / 2 / 55`), and
`A64b` restating `A2`'s 60-trade burst and its `accelThreshold` / `floorPct`.

⛔ **That is the copy-drift shape `D1` rejected option (b) for.** Retune `A9` and the guard
keeps watching the old values — it would report healthy while guarding nothing. **A guard that
can silently stop guarding is worse than no guard**, because it also reports success.

**What I did instead — collapse, don't copy:**

| Extracted | Callers | Copies before → after |
|---|---|---|
| `MtfBearCandles()` + `RunMtfBearGate()` + 4 `Private Const` | `A9`, `A14h`, `A64a` | **2 → 1** builder, **2 → 1** literal set |
| `MicroCvdBurstTrades()` + `RunMicroCvdBurst()` + 4 `Private Const` | `A2`, `A64b` | **1 → 1** builder, **1 → 1** literal set (no new copy created) |

⭐ **`A9` and `A14h` already carried byte-identical copies of that builder and those four
literals.** The extraction removes a pre-existing duplication rather than only preventing a
new one — and `M5` in §2 proves the coupling is real, not cosmetic.

**Why `Private Const` and not `Public Const`:** CLAUDE.md's rule is about a fixture reading a
**production** constant across assemblies, where `Private` forces the fixture to restate the
number. Here the constants and all three readers live in the same file, so `Private` is
correct and `Public` would be meaningless. ⭐ There is a second reason they are named at all:
**`A64a` asserts `adx >= MtfAdxMin` directly**, so it needs to *read* the threshold — an inline
literal it could not see would have to be restated, which is the defect again.

⚠ **This is the deviation to push back on if you want to.** It touches three passing fixtures
(`A2`, `A9`, `A14h`) that nobody asked me to touch. The behaviour is unchanged — the harness
proves it, and every asserted value is identical — but the diff is 157 insertions rather than
the ~60 "two Subs" would have been. **Back it out and I will restate the literals instead; I
think that is worse, and the reason is `D1`'s own reason.**

---

## 2. ⭐ MUTATION PROOFS — five, every one RUN, none reasoned

`D1`'s condition: *"dropping a vote must fail `A64a`, and restoring `A2`'s masking must fail
`A64b`. Without that they're decoration."*

| # | Mutation | `A2` | `A9` | `A14h` | `A64a` | `A64b` |
|---|---|---|---|---|---|---|
| **M1** | `MtfAdxMin` 7.5 → **101.0** (ADX vote dropped by threshold) | PASS | ✅ **PASS** | ✅ **PASS** | ⛔ **FAIL** | PASS |
| **M2** | `MtfAdxPeriod` 7 → **30** (ADX collapses to 0.0) | PASS | ✅ **PASS** | ✅ **PASS** | ⛔ **FAIL** | PASS |
| **M3** | `MtfCandleLookback` 55 → **30** (EMA stack goes MIXED) | PASS | ✅ **PASS** | ✅ **PASS** | ⛔ **FAIL** | PASS |
| **M4** | `A64b`'s dynamic arm 0.37 → **0.0** (restore the masking) | PASS | PASS | PASS | PASS | ⛔ **FAIL** |
| **M5** | `MicroBurstAccelThreshold` 1234.0 → **74000.0** | ⛔ **FAIL** | PASS | PASS | PASS | ⛔ **FAIL** |

### ⭐⭐ M1–M3 are the whole justification, and the load-bearing column is `A9`, not `A64a`

**In all three vote-drop modes `A9` and `A14h` PASS while `A64a` FAILS.** That is `F2`
demonstrated on the real harness rather than argued: `A9` asserts `trend = BEAR`, which
survives losing any one vote because the series scores `Bear:3` against `need:2`. ⛔ **A
future seat could retune any of those three values, watch the harness stay green, and never
learn that the fixture had stopped exercising the ADX or EMA half of what its own docstring
claims.** `A64a` is the only thing that now says so.

Each mode fails through the channel that identifies it, which is why `A64a` has three:

| Mode | `Bear:3` | `adx >= MtfAdxMin` | `emaAlign = "BEAR"` |
|---|---|---|---|
| M1 `adxMin` 101 | ✗ (reads `Bear:2`) | ✗ (100.0 ≥ 101 false) | ✓ |
| M2 `adxPeriod` 30 | ✗ | ✗ (ADX degrades to 0.00) | ✓ |
| M3 `lookback` 30 | ✗ | ✓ | ✗ (MIXED) |

### M4 — the ruled `A64b` proof

Setting `A64b`'s dynamic arm back to `0.0` collapses the 2×2 to a single column, which is
exactly "the masking returns". Observed failure detail, verbatim:

> `expected BULL_ACCEL/BULL_ACCEL/BULL_ACCEL/FLAT, got base=BULL_ACCEL winAlone=BULL_ACCEL dynAlone=BULL_ACCEL joint=BULL_ACCEL`

### ⭐ M5 — not asked for, and it is the one that validates §1's extraction

Moving the **shared** `accelThreshold` constant makes **`A2` and `A64b` fail together**:

> `A2` — `expected BULL_ACCEL, got FLAT (E=16000 M=32000 L=90000)`
> `A64b` — `got base=FLAT winAlone=FLAT dynAlone=BULL_ACCEL joint=FLAT`

**A copy could not do that.** It is the direct evidence that `A64b` tracks `A2` rather than a
snapshot of it — the property §1 claims and the one `D1` cared about.

---

## 3. Ranked verification handles

⛔ **All run against this tree before being written here**, and pinned to this build's commit
rather than `HEAD` — a lesson from the `H3` handle in
[`i17-sweep-spec-back.md`](i17-sweep-spec-back.md), which was correct when written and wrong
two commits later.

### ⭐ HA1 — if you run only one

```bash
dotnet run --project verify/ordercheck -c Release 2>&1 | grep -cE '^PASS '
```

**Expected `328`** — 326 before, plus exactly two. A third number means something else moved.

### HA2 — the duplication actually went away (§1's claim, not its description)

```bash
grep -c "Close = p - 100, .Low = p - 120" verify/ordercheck/Program.vb   # MTF builder copies
grep -c "window late third" verify/ordercheck/Program.vb                 # A2 burst builder copies
```

**Expected `1` and `1`.** The first was **2** before this build. ⚠ Both are content greps, so
they would also match a comment mentioning the text — neither file position currently does,
but re-read the hits rather than trusting the count if either goes above 1.

### HA3 — every MTF literal exists exactly once

```bash
grep -nE 'adxPeriod:=|adxMin:=|minOf:=|candleLookback:=' verify/ordercheck/Program.vb | grep -vE "^[0-9]+:[[:space:]]*'"
```

**Expected: 2 lines, both inside `RunMtfBearGate`**, and both referencing `Mtf*` constants —
no bare numbers. If a bare literal appears here, a caller has drifted off the shared config
and its guard is no longer guarding it.

### HA4 — `A64b` pins a synthetic, not the shipped, `dynamicPct`

```bash
grep -nE '0\.37([^0-9]|$)' verify/ordercheck/Program.vb | grep -vE "^[0-9]+:[[:space:]]*'"
grep -nE 'dynamicPct' verify/ordercheck/Program.vb | grep -vE "^[0-9]+:[[:space:]]*'" | grep '0\.30'
```

**Expected: 2 lines, both in `A64b`**, then **no output** from the second command.

⛔ **Both halves of this handle were WRONG in first draft, and the way they were wrong is the
point.** The first read `grep -n "0\.37"` and printed **4** lines — `0.37` is a substring of
`0.375`, which is `MicroBurstFloorPct` and `A3`'s `floorPct`. The second asserted *"`0.30`
appears on no executable line"* and printed **3** — all in the **absorption** fixtures'
`proximity_atr_frac`, an unrelated key. ⭐ **Neither is a fault in the build; both were faults
in my expectation**, caught only because every handle was run before publication. That is the
third handle in this arc whose stated expectation was wrong on first draft — see §6.

### HA5 — nothing else moved

```bash
git diff c034c5f..HEAD --stat
```

**Expected: exactly 3 files** — `verify/ordercheck/Program.vb` (190 changed),
`docs/a64-fixtures-spec-back.md` (new), `docs/i17-sweep-spec-back.md` (the handle-maintenance
note). **No `settings.json`, no `Core/`, no `UI/`, no `tools/`.**

⛔ **This handle was ALSO wrong on first draft — the fourth in the arc, and it is now the
worked example for its own §6 entry.** It read `git diff 3f16a9b..HEAD`, which sweeps in
`c034c5f`, the reviewing seat's own commit, and prints **5** files including `CLAUDE.md` and
`docs/DeribitIndicatorProject.md`. Both of those are the `D3` and `D2` actions and are
correct — but a reviewer running the handle as written would see two unexpected files and have
to work out why. **The base must be the commit this build started from, not the one the
previous packet was written against.**

---

## 4. Display-string parity — NO OBLIGATION, stated per the hard rule

**No production code was touched.** The change is confined to
`verify/ordercheck/Program.vb`: two new fixtures, two extracted helpers, eight
fixture-local `Private Const`. No `VerdictResult` or `IndicatorResults` field default moved;
`UI/MainForm_PlaintextSnapshot.vb` and `UI/MainForm_Render_Cards.vb` were not opened.
`verify-gate.ps1`'s own parity check independently reports **`no snapshot/card drift
detected`**.

⚠ **One thing that looks like a parity concern and is not:** `A64a` asserts against
`gateDetails`, a **rendered diagnostic string** built inside `CalcMTFGate`. If that format
ever changes, `A64a` fails — loudly, in the harness, which is the correct outcome and is why
it carries two non-string channels alongside. It is **not** a snapshot or card surface, so
the parity rule does not reach it.

---

## 5. ⛔ Where I departed from the ruling's literal wording — one place, and it is arguable

`D1` says `A64b` should pin *"`A2`'s joint dependency (`microWindowSize` × `dynamicPct`, **the
`F5` pair**)"*. The `F5` pair is `(20, 0.30)`, and **`0.30` is the shipped
`accel_threshold_dynamic_pct`.**

**I used `0.37` instead**, because pinning `0.30` would couple a MECHANISM fixture to a
calibration knob — the exact `A6` failure shape queue item 17 exists to remove, ruled three
commits ago. A fixture asserting *"the shipped value breaks it"* changes meaning the moment
the shipped value is retuned.

**Measured, so the substitution is not a guess** — `0.37` reproduces the `F5` boundary
identically at the current fixture literals (`accelThreshold` 1234.0, `floorPct` 0.375):

| `dynamicPct` | win 15 | win 20 | win 25 | win 30 | win 40 | win 50 |
|---|---|---|---|---|---|---|
| 0.0 | FLAT | ACCEL | ACCEL | ACCEL | ACCEL | ACCEL |
| **0.30 (shipped)** | FLAT | **FLAT** | **FLAT** | **FLAT** | ACCEL | ACCEL |
| **0.37 (pinned)** | FLAT | **FLAT** | **FLAT** | **FLAT** | ACCEL | ACCEL |
| 0.50 | FLAT | FLAT | FLAT | FLAT | FLAT | ACCEL |

**Same failure boundary, between window 30 and 40, for both.** The assertion carries the
mechanism; the call-site comment carries the finding about the shipped value, dated and marked
as measured.

⚠ **Also re-measured rather than inherited:** `F5`'s original grid ran at `accelThreshold`
10000 / `floorPct` 0.25 — **the values `A2` carried before the I17-SWEEP build changed them.**
Writing `A64b` from the finding's own numbers would have pinned a configuration that no longer
exists. The whole 2×2 above was re-measured at what actually ships.

⚠ **Window 20, not 15:** 15 is FLAT even at `dynamicPct` 0, so it sits below `A2`'s own band
floor and would not isolate the *joint* effect — it would fail for its own reason. 20 is the
low edge of the measured `[20, 1000]` band, which is what makes the "neither alone" arm honest.

**If you would rather have `0.30` pinned, say so — it is a one-line change and I will take the
coupling.**

---

## 6. What I did not verify, and cannot

- ⚠ **`A64a`'s `"Bear:3"` literal is a substring of a rendered diagnostic.** There is no
  structured vote-count output on `CalcMTFGate` — `bullScore`/`bearScore` are locals — so the
  vote count is genuinely unobservable except through `gateDetails`. **Adding a `ByRef`
  vote-count out-param would be a production change and is out of scope here**; flagging it as
  the only way to make this assertion structural rather than textual.
- ⚠ **The `need` count is derived from `MtfMinOf`, but `3` is not derived from anything.** It
  is the number of vote *sources* in `CalcMTFGate` (DMI, ADX, EMA). If a fourth vote source
  were ever added, `A64a` would fail and would need updating — correctly, but a reader should
  know that is a design property and not a settings value.
- ⚠ **`A64b` proves the joint dependency exists at four points, not the whole surface.** The
  2×2 is not a claim about every `(window, dynamicPct)` pair; §5's grid is the wider evidence
  and it lives in this document, not in the fixture.
- ⚠ **I did not re-run the `I17-SWEEP` probe against this tree.** The extraction changed how
  the arguments reach `CalcMicroCVD` / `CalcMTFGate`, not what they are; the real harness's
  328/328 and the five mutations are the evidence, which is stronger than the replica anyway.
- ⚠ **No check exists that a *future* fixture will use the shared helpers** rather than adding
  a fourth copy. The extraction removes today's copies; it does not enforce tomorrow's.

### ⛔ A pattern worth naming: FOUR handles in this arc were wrong on first draft

Not the builds — the **handles**, the things a reviewer would have run:

| Handle | First draft said | Actually printed | Why |
|---|---|---|---|
| `H4` ([`i17-sweep-spec-back.md`](i17-sweep-spec-back.md)) | *"appears in no value set"* | collisions on 4 synthetics | The test is per **key**; cross-key matches are noise |
| `H3` (same doc) | 13 lines | **15** two commits later | Pinned to `HEAD`, not to a commit |
| `HA4` (this doc) | 2 lines, and `0.30` absent | **4**, and `0.30` present ×3 | `0.37` ⊂ `0.375`; `0.30` is also an absorption key |
| `HA5` (this doc) | 3 files | **5 files** | Base commit swept in the reviewer's own commit |

⭐ **The first three share one shape: a grep that matches a STRING which resembles the
property, rather than the property.** That is CLAUDE.md's own `R1` rule — *"verification
handles must test the property, not a string that mentions it"* — and I reproduced it three
times **while explicitly writing about it.** `HA5` is the fourth and a different shape: a
correct command with the wrong **baseline**, which is `H3`'s failure again.

⛔ **The only reason none of the four shipped as a wrong handle is the habit "run it before
you publish it."** Knowing `R1` demonstrably did not prevent any of them; running the command
caught all four. ⚠ **`HA5` is the sharpest instance, because it was written AFTER I had
already documented the pattern in this very section** — the rule does not protect you, the
execution does.

**Suggested, not decided:** a line in CLAUDE.md's verification-handle rule saying *"run every
handle against the tree and paste the actual output before publishing it; a handle that has
not been run is a guess."* **Your call — I have not touched CLAUDE.md.**

---

## 7. Queued, not done — unchanged from the ruling

- **`S2-2`** — split `CalcSpread` into `CalcSpreadBps` + `ClassifySpread`. Needs its own
  proposal. ⚠ **The display-parity rule IS live for it**, unlike this build.
- **The `D4` residual** — positional passing, whole-`cfg` builders (`BuildA8Cfg`,
  `BuildResolutionCfg`), and `OfiAccumulator.vb:84`'s `tauSec`, the one same-class mode switch
  pinned by no fixture. Low priority, explicitly queued rather than assumed covered.
- **A `docs/DeribitIndicatorProject.md` §15 row** — none added. `D2` already ruled the A54a row
  is the one row for this arc, and it already names `A64a`/`A64b` as *"STILL OPEN"*. ⭐ **That
  clause is now satisfiable: the row needs its `A64a`/`A64b` mention closed, exactly as `D2`
  closed the item-17 clause.** One clause edit, not a new row — **your call, not mine.**

---

## 8. ⭐ REVIEWER VERDICT — ACCEPTED, 2026-09-06. All four open points answered.

### 8.1 Verified independently

| Check | Result |
|---|---|
| Harness | **328 PASS · 0 FAIL · `ALL PASS`** — both `A64a` and `A64b` **executed**, not merely present |
| `verify-gate.ps1 -Mode local-fast` | ✅ **`GATE PASSED`** |
| **`HA2`** — the duplication actually went away | ✅ MTF builder copies **1** (was 2); burst builder **1** |
| **`HA3`** — MTF literals exist exactly once | ✅ **two lines, both inside `RunMtfBearGate`, both referencing `Mtf*` constants. No bare numbers anywhere** |
| **`HA5`** (corrected base) | ✅ exactly **3 files**. No `settings.json`, no `Core/`, no `UI/`, no `tools/` |
| ⭐ **The `M5` coupling, checked structurally** | ✅ `A2` (`:777`) and `A64b` (`:12282-12285`) both route through `RunMicroCvdBurst`, which reads `MicroBurstAccelThreshold` (`:711`). **The shared constant genuinely reaches both — the coupling is structural, not a coincidence of the mutation run** |

### 8.2 §1 — the extraction: ⭐ ACCEPTED, do NOT back it out

**You obeyed `D1`'s reason rather than its word count, and that was the right call.** Writing
`A64a` as "two Subs" would have restated `A9`'s builder and its four literals — **the copy-drift
shape `D1` rejected option (b) for.** A guard built that way reports healthy while guarding a
snapshot.

⭐ **Three things make this more than a preference:** the extraction removed a **pre-existing**
duplication (`A9`/`A14h` already carried byte-identical copies — net 2 → 1, not +1); `M5`
proves the coupling by failing `A2` and `A64b` **together**, which a copy cannot do; and
`HA3` confirms no bare literal survives at any call site. **`Private Const` is also correct
here** — CLAUDE.md's `Public Const` rule is about a fixture reading a *production* constant
across assemblies; same-file constants have no such problem.

⭐ **Your sentence is the principle and it should outlive this build: *a guard that can
silently stop guarding is worse than no guard, because it also reports success.***

### 8.3 §5 — `0.37` over `0.30`: ⛔ KEEP IT. **`D1`'s wording was mine and it was wrong.**

`D1` said to pin *"the `F5` pair"*, and `F5`'s pair is `(20, 0.30)` — **where `0.30` is the
shipped `accel_threshold_dynamic_pct`.** Pinning it would couple a MECHANISM fixture to a
calibration knob: the `A6` shape.

⛔⛔ **I wrote that ONE DAY after ruling `D3`, which made *"off-shipped means off-EVER-shipped"*
a standing rule in CLAUDE.md — and I still specified a currently-shipped value.** ⭐ **Ninth
reviewer correction in this arc and the most self-implicating: the rule I had just written
did not stop me breaking it in the very next instruction I issued.** Your substitution is
correct and my wording is the defect.

⭐ **And the substitution is measured, not asserted** — the 2×2 grid puts the failure boundary
between window 30 and 40 for both `0.30` and `0.37`. ⭐⭐ **The part that deserves more credit
than the substitution itself: you re-measured because `F5`'s original grid ran at
`accelThreshold` 10000 / `floorPct` 0.25 — values `A2` no longer carries.** Writing the
fixture from the finding's own numbers would have pinned a configuration that does not exist.
**That is the "don't inherit a finding's numbers" discipline applied without being asked.**

### 8.4 §6 — the CLAUDE.md suggestion: ✅ RULED YES, added

**Added to CLAUDE.md above the existing verification-handle rule, 2026-09-06.** ⚠ Scoped as
you'd expect: it governs handles written **about a completed build** — a forward-looking
acceptance criterion in a spec cannot be run yet — and it carries `H3`/`HA5`'s second lesson,
**pin the handle to the build's base commit, not to `HEAD`.**

⭐⭐ **The evidence you assembled is what carries it: four handles wrong on first draft, three
of them the exact shape the existing rule names, and the fourth written AFTER you documented
the pattern in that same section.** *"The rule does not protect you; the execution does"* is
now in CLAUDE.md in those terms.

### 8.5 §7 — the §15 clause: ✅ DONE, by the reviewer, in this commit

The A54a row's *"STILL OPEN … `A64a`/`A64b`"* clause is closed and replaced — same treatment
`D2` gave the item-17 clause. **No new row.** It records the `A9`/`A14h`-pass-while-`A64a`-fails
result and the pre-existing-duplication collapse, because those are the two things that outlive
the build.

### 8.6 No findings against the build

**Nothing to raise.** §6's five "did not verify" entries are all correctly scoped, and the
first — *"`Bear:3` is a substring of a rendered diagnostic; making it structural needs a
production `ByRef` out-param, which is out of scope"* — is the right call **and** the right
thing to have flagged rather than quietly done.
