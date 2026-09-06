# I17-SWEEP + I17-A6 — the sensitivity measurement and its application — batch summary

**This is the RECORD.** The working document for the reviewing seat is
[`i17-sweep-spec-back.md`](i17-sweep-spec-back.md) — ranked verification handles, the
decisions queued for a ruling, and feedback on the ruling itself. Per
[`batch-review-packet-convention.md`](batch-review-packet-convention.md): two documents,
cross-referenced, never duplicated.

> ### ⛔ SUPERSEDING NOTE, placed at the top because it changes how §0 and §3 read
>
> **The §0 counts published in the first version of this document were WRONG, and the
> error is mine.** It read *"13 changed to synthetic · 5 kept · 8 left alone"*. The correct
> figures, counted mechanically off the diff, are **17 changed · 5 kept · 4 left alone**.
>
> ⚠ **The wrong pair still summed to 26** (13 + 5 + 8), which is why it survived a read-back
> — and it is why the convention asks for **arithmetic identities** rather than totals. The
> error was caught while building verification handle `H3` in the packet, by
> re-deriving the counts from `git diff` instead of restating them.
>
> ⭐ **Nothing else moves.** §2's band table was right throughout — every row's `Was` / `Now`
> is correct, and re-counting the table's own rows is what confirms 17 / 5 / 4. The applied
> values, the harness result and the findings are unaffected. **Only the summary counts were
> wrong, and they were wrong in the direction of understating how much this build changed.**
>
> ⛔ **Commit `1b2adbe`'s message carries the same wrong 13 / 8 figures.** History was not
> rewritten; the correction lives here.

**Ruling:** [`trader-tick-queue.md`](trader-tick-queue.md) §2, rows `I17-A6` (a stale-literal
fix, ruled in full) and `I17-SWEEP` (measure insensitivity per literal, then decide).
**Reasoning under test:** [`a54a-session2-step2-spec-back.md`](a54a-session2-step2-spec-back.md)
§2, session 2's own 26-row MECHANISM table.

**Commits:** `98ed4fd` (`I17-A6`) · this commit (`I17-SWEEP`).

---

## 0. Outcome

| | Result |
|---|---|
| Literals swept | 26 (session 2's full set), plus `A6.trendGate` and `A43b.tfiWindowSize` for context |
| Probe points run | 608 one-at-a-time + 5 joint grids |
| Fixtures that could NOT be swept safely | **0** — see §6 |
| Literals changed to synthetic | **17** of 26 |
| Literals kept, with an INCIDENTAL-equality comment | **5** of 26 |
| Literals left alone (never a shipped value) | **4** of 26 |
| Session 2 classifications **corrected** | **11** of 26 — see §4 |
| Session 2 classifications **confirmed** | **15** of 26 |
| Build matrix | Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck`, each **0 errors / 0 warnings** Release, each built separately |
| Harness | **326 PASS · 0 FAIL · `ALL PASS`** |
| `verify-gate.ps1 -Mode local-fast` | **`GATE PASSED`** |
| Settings | Unchanged — still **v68**, `git diff` on `settings.json` is empty |
| Display-string parity | **NO OBLIGATION** — see §7 |

---

## 1. Method, and why it is not the naive sweep the ruling warned about

The ruling's trap is that **a fixture can keep passing for the wrong reason at an extreme
value**, so the sweep must assert the *asserted values*, not the pass/fail bit.

**What was built:** a standalone probe (`Sweep.vb` + `Sweep.vbproj`) that links the same
shipped sources `OrderCheck.vbproj` links, replicates each of the nine call sites' data
builders **verbatim**, and re-evaluates each fixture across a probe range per literal.
Every probe prints the **exact tuple the real `Check()` reads**, plus the **unasserted**
outputs alongside it — which is what caught the two vacuity findings in §4.

**⛔ The probe is a replica, so it was validated against the real harness before any
conclusion was drawn from it — twice, in two directions:**

- **Baseline agreement.** At the as-found literals the replica reproduces each fixture's
  asserted values exactly: `A3` `e=16000 net=50000` · `A4` `BUY PRESSURE / 1.000000` ·
  `A9` `BEAR / passL=False / passS=True` · `A6` `trendA=RISING trendB=RISING`.
- **⭐ Differential agreement.** Four predicted outcomes were applied to the **real**
  `verify/ordercheck/Program.vb`, rebuilt, and run:

| Mutation applied to the real fixture | Replica predicted | Real harness produced |
|---|---|---|
| `A6` `trendGate` → 48.0 | FAIL, `trendA=FLAT trendB=FLAT` | **FAIL** — *"got equal-pair=FLAT distinct-pair=FLAT"* |
| `A2` `microWindowSize` → 20, `dynamicPct` → 0.3 | FAIL, `signal=FLAT` | **FAIL** — *"expected BULL_ACCEL, got FLAT"* |
| `A9` `adxMin` → 101.0, `minOf` → 3 | FAIL, `Bull:0 Bear:2 (need 3)` | **FAIL** — *"`Bull:0 Bear:2 (need 3)`"*, byte-identical |
| `A4` `threshold` → 0.999999 (positive control) | PASS | **PASS** |

  `A3`, `A14h` and `A43b` stayed PASS throughout, confirming the mutations were
  site-scoped. The tree was then restored from a byte-exact backup before any real edit.

**⚠ The probe is NOT committed** — scratchpad-only, because it duplicates fixture data and
would itself rot. ⛔ **That has a cost the reviewer must price:** verification handle `H6`
in [`i17-sweep-spec-back.md`](i17-sweep-spec-back.md) — *"the unasserted diagnostics are
unchanged"*, the check that proves the **value choice** was right rather than merely
in-band — **cannot be run from the tree as committed.** Queued as decision `D1` in that
packet. Not decided here.

---

## 2. ⭐ THE PER-LITERAL BAND TABLE — the deliverable

**"Band" = the range over which the fixture's ASSERTED VALUES are unchanged**, not merely
where it still passes. `A14h`'s four are omitted as rows 23–26: same data, same assertion,
byte-identical results to `A9`'s.

| # | Call site | Param | Was | Now | Shipped | Class | Measured band | Edge |
|---|---|---|---|---|---|---|---|---|
| 1 | `A1` `CalcCVD` | `slopeMinUsd` | 50000 | *50000* | 12000.0 | BAND-INERT | `[0, 5.999e6]` | 6.0e6 → FLAT |
| 2 | `A1` | `slopePctOfValue` | 0.05 | **0.037** | 0.10 | STRUCTURALLY INERT | `[0, 1000]` | none found |
| 3 | `A1` | `divergencePriceGate` | 0.002 | *0.002* | 0.0005 | STRUCTURALLY INERT | `[0, 1000]` | none found |
| 4 | `A1` | `lateSegmentWeight` | 2.0 | **7.0** | **2.0** | BAND-INERT | `[0.001, 1e6]` | only joint `(0, 0)` |
| 5 | `A1` | `earlySegmentWeight` | 1.0 | **3.0** | **1.0** | BAND-INERT | `[0.001, 1e6]` | only joint `(0, 0)` |
| 6 | `A2` `CalcMicroCVD` | `microWindowSize` | 50 | *50* ⛔kept | **50** | DESIGN CONSTANT | `[20, 1000]` | ≤10 → FLAT |
| 7 | `A2` | `accelThreshold` | 10000 | **1234.0** | **10000.0** | BAND-INERT | `[0, 73999]` | 74000 → FLAT |
| 8 | `A2` | `dynamicPct` | 0.0 | *0.0* | 0.30 | BAND-INERT | `[0, 0.53]` | 0.54 → FLAT |
| 9 | `A2` | `floorPct` | 0.25 | **0.375** | **0.25** | ⚠ **MASKED** | `[0, 1000]` *(artefact)* | none 1-at-a-time |
| 10 | `A3` `CalcMicroCVD` | `microWindowSize` | 50 | *50* ⛔kept | **50** | ⛔ **LOAD-BEARING** | **`{50}` — one point** | 49 → net 49000; 51 → e −984000 |
| 11 | `A3` | `accelThreshold` | 10000 | **1234.0** | **10000.0** | STRUCTURALLY INERT | `[0, 1e12]` | none found |
| 12 | `A3` | `dynamicPct` | 0.0 | *0.0* | 0.30 | STRUCTURALLY INERT | `[0, 1e9]` | none found |
| 13 | `A3` | `floorPct` | 0.25 | **0.375** | **0.25** | STRUCTURALLY INERT | `[0, 1e9]` | none found |
| 14 | `A4` `CalcTFI` | `tfiWindowSize` | 30 | *30* ⛔kept | **30** | ⛔ **LOAD-BEARING** | `[1, 30]` | 31 → val 0.935484 |
| 15 | `A4` | `threshold` | 0.15 | **0.42** | **0.15** | BAND-INERT | `[0, 1)` | 1.0 → NEUTRAL |
| 16 | `A43b` `CalcTFI` | `threshold` | 0.15 | **0.42** | **0.15** | BAND-INERT | `[0, 1)` | 1.0 → NEUTRAL |
| 17 | `A6` site 1 | `divergenceGate` | 0.001 | **0.0077** | **0.001** | STRUCTURALLY INERT | `[0, 1000]` | none found |
| 18 | `A6` site 2 | `divergenceGate` | 0.001 | **0.0077** | **0.001** | STRUCTURALLY INERT | `[0, 1000]` | none found |
| 19 | `A9` `CalcMTFGate` | `adxPeriod` | 9 | **7** | **9** | ⚠ BAND-INERT, **vacuous ≥30** | `[1, 68]` | 69 → FLAT |
| 20 | `A9` | `adxMin` | 20.0 | **7.5** | **20.0** | ⚠ **MASKED** | `[0, 1000]` *(artefact)* | none 1-at-a-time |
| 21 | `A9` | `minOf` | 2 | *2* ⛔kept | **2** | ⛔ **LOAD-BEARING** | `[1, 3]` | 0 → **BULL**; 4 → FLAT |
| 22 | `A9` | `candleLookback` | 60 | **55** | **60** | ⚠ BAND-INERT, partly vacuous 20–49 | `[20, 1000+]` | ≤12 → FLAT |

**Bold in the `Shipped` column = the literal equalled the shipped value.** That is 21 of
26 as the queue row measured — plus one more the queue did not catch, `A1.slopePctOfValue`
(§4, `F3`). **The confusable set is 22 of 26, not 21.**

### 2.1 The four classes the measurement produced

The ruling anticipated two outcomes (wide band ⇒ inert ⇒ synthetic · narrow band ⇒
load-bearing ⇒ keep). The data needs four, and the extra two are where the value is:

| Class | What it means | Is a wide band evidence of insensitivity? |
|---|---|---|
| **STRUCTURALLY INERT** | The parameter cannot reach the asserted output at all — it gates something `Check()` never reads, or a term that is exactly zero | **Yes**, and it is the strongest form |
| **BAND-INERT** | It can reach the assertion, but the fixture's data sits far from the boundary | **Yes**, within the measured edge |
| ⚠ **MASKED** | Its band is wide **only because a sibling literal at the same call site disables or dominates it**. Change the sibling and it becomes load-bearing at once | ⛔ **NO.** The width is an artefact |
| ⛔ **LOAD-BEARING** | Single point, or the literal sits on an edge | n/a — keep the value |

---

## 3. What was applied, and the rule used to decide

**Not every wide band was cashed in for a synthetic value.** The rule applied, stated so a
reviewer can disagree with it rather than reverse-engineer it:

1. **Band is a single point, or the literal sits on an edge** → **KEEP**, comment says the
   equality with shipped is **INCIDENTAL**. *(rows 10, 14, 21, and `A14h`'s `minOf` twin at
   row 25 — **4 literals**)*
2. **The literal is a DATASET DESIGN CONSTANT** — the fixture's data construction is built
   around that number and its own comments name it → **KEEP + INCIDENTAL**, whatever the
   band. ⭐ **Band width licenses a change; it does not compel one.** *(row 6)*
3. **Wide band, not a design constant, and the value equals a shipped value (current OR
   historical)** → **make it obviously synthetic**, choosing a value that preserves the
   *mechanism the fixture documents*, not merely the assertion. *(rows 2, 4, 5, 7, 9, 11,
   13, 15, 16, 17, 18, 19, 20, 22, plus `A14h`'s three at rows 23, 24, 26 — **17 literals**)*
4. **The value was never a shipped value** → leave it; item 17's confusability defect is
   simply absent. *(rows 1, 3, 8, 12 — **4 literals**. Rows 8 and 12's `dynamicPct:=0.0` is
   never-shipped because `accel_threshold_dynamic_pct` has only ever held 0.03 and 0.30)*

**Identity: 17 + 4 + 4 + 1 = 26** — changed · kept as load-bearing (rule 1) · left alone
(rule 4) · kept as a design constant (rule 2). ⭐ **Re-derive this from the table rather than
taking it on report; restating it is exactly how the superseded §0 figures went wrong.**

**⭐ Rule 3's "preserves the mechanism" clause is doing real work, not decoration.** Two
values were chosen against band-width alone:

- **`adxPeriod` → 7, deliberately below 30**, though the band runs to 68 — because at ≥30
  `ADX` collapses to `0.0` and the ADX vote silently stops being cast.
- **`adxMin` → 7.5, deliberately low**, though every probed value passed — because a high
  one drops the same vote.

**Confirmed at the chosen values, all applied together** (so interactions between the new
values are exercised, not just each against the old baseline): every asserted tuple is
byte-identical to the baseline, **and so is every unasserted diagnostic** — `A9`/`A14h`
still report `Bull:0 Bear:3 (need 2)`, `ADX:100.0`, `EMA:BEAR`; `A6` still reports
`divA=NONE divB=NONE`.

**Every synthetic value was checked against the full tracked history** — all 87 revisions
of `settings.json` — so none can be misread as a settings claim. Historical value sets:
`slope_min_usd {1000, 12000}` · `slope_pct_of_value {0.01, 0.05, 0.10}` ·
`divergence_price_gate {0.0005, 0.001}` · `late/early_segment_weight {2.0} / {1.0}` ·
`accel_threshold {5000, 10000}` · `accel_threshold_dynamic_pct {0.03, 0.30}` ·
`accel_threshold_floor_pct {0.25}` · `TFI {window_size 30, threshold 0.15}` ·
`OBV.divergence_gate {0.001}` · `mtf_gate {9, 20.0, 2, 60}`.

---

## 4. ⛔ FINDINGS — where the measurement contradicts session 2

**This is the section the measurement was commissioned for. It is reported, not reconciled
away.**

### F1 ⭐⭐ The MTF four are not inert. They are covered by a SPARE VOTE. (8 of the 26)

Session 2, rows 8–9: *"a strongly one-sided series built to classify BEAR unambiguously
under any reasonable gate parameter — not a calibration-sensitivity test."*

**The measurement agrees with the conclusion and rejects the reason, and the reason is what
a future seat will reuse.** `CalcMTFGate` scores three independent bear votes — DMI,
ADX-strong, EMA stack — and compares the total against `minOf`. This fixture scores
**`Bear:3` against `need:2`. One spare vote.** That spare is the entire mechanism:

| Probe | Bear votes | Result |
|---|---|---|
| baseline `9 / 20.0 / 2 / 60` | 3 | BEAR ✅ |
| `adxMin` 101 (ADX vote drops) | 2 | BEAR ✅ — **still clears `minOf` 2** |
| `adxPeriod` 30 (ADX → 0.0, vote drops) | 2 | BEAR ✅ — **still clears `minOf` 2** |
| ⛔ `adxMin` 101 **× `minOf` 3** | 2 | **FLAT ❌** |
| ⛔ `adxPeriod` 30 **× `minOf` 3** | 2 | **FLAT ❌** |
| ⛔ `candleLookback` 30 **× `minOf` 3** | 2 | **FLAT ❌** |

So `adxMin`'s one-at-a-time band is **unbounded across five orders of magnitude** — and
that is an artefact of a sibling literal, not evidence about `adxMin`. ⛔ **A naive sweep
would have read "totally inert" and licensed any synthetic value at all.**

**And `minOf` itself is outright LOAD-BEARING** (band `[1, 3]`; at 0 the verdict **inverts
to BULL**), which *"unambiguously under any reasonable gate parameter"* directly denies.

### F2 ⚠ The fixture goes partly vacuous inside its own band — `A9`'s docstring stops being true

`A9`'s docstring claims it exercises *"DMI bearish, **ADX strong**, EMA stack bearish."*
At `adxPeriod ≥ 30` the ADX reads **0.00** and the ADX vote is not cast; at
`candleLookback` 20–49 the EMA stack degrades to `MIXED` and *that* vote is not cast. The
**assertion is unchanged in both cases.** This is the ruling's vacuous-pass trap in a form
it did not name: not "the fixture passes for the wrong reason", but **"the fixture still
passes while quietly testing less than it says it does."** It was only visible because the
probe printed the unasserted `adx`, `emaAlign` and vote counts next to the assertion.

### F3 ⛔ `A1.slopePctOfValue:=0.05` IS a shipped value — the confusable set is 22, not 21

The session-2 reviewer's §7.2 spot-checked this literal and cleared it: *"now pins a value
that is **off-shipped** (POCO and JSON both read 0.10 since the R-2/R-3 build) … exactly
the `A20a`/`A20b` case CLAUDE.md names as legitimate."*

⛔ **That check was against the CURRENT value only.** Across all 87 tracked revisions,
`slope_pct_of_value` has held **{0.01, 0.05, 0.10}**. `0.05` is a *former shipped value* —
**structurally the same defect as `A6`'s `10.0`**, which is the pre-v33 shipped value and
which item 17 exists to remove. A reader greps `0.05`, finds it in the version history, and
cannot tell stale from deliberate.

⭐ **The lesson generalises past this literal: "off-shipped" must mean off-*ever*-shipped.**
Checking a fixture literal against today's `settings.json` is exactly the check that lets
the `A6` shape survive.

### F4 ⚠ `A2.floorPct` is dead code, not inert

`floorPct` lives inside `If dynamicPct > 0.0 Then …` in `CalcMicroCVD`. `A2` pins
`dynamicPct:=0.0`, so **that branch never executes** and `floorPct` is unreachable. Its
`[0, 1000]` band is an artefact of the sibling. Measured jointly, `(accelThreshold 10000,
dynamicPct 0.30, floorPct 10)` **FAILS**. Session 2's A2 row hand-computed the
static-vs-dynamic divergence correctly for `dynamicPct` and did not notice that the same
pinned `0.0` silently kills `floorPct`.

### F5 ⚠ A joint dependency that one-at-a-time misses entirely

`A2`'s `microWindowSize` band is `[20, 1000]` and its `dynamicPct` band is `[0, 0.53]`.
Both `20` and the **shipped** `0.30` are inside their own bands. **Together they FAIL.**
Same for `(30, 0.30)`. This is the ruling's second trap, confirmed to exist rather than
merely guarded against.

### F6 ⚠ The queue row's `trend_gate` history is wrong in its first element

`trader-tick-queue.md` §2, row `I17-A6`, states the history is *"8.0 → 10.0 → 18.0 →
23.0"*. Measured across all 87 tracked `settings.json` revisions plus the method-local
default's own git history, the values are **{0.001, 10.0, 18.0, 23.0}** in settings
(`0.001` → `10.0` at v31 → `18.0` at v33 → `23.0` at v66) and **{0.01, 10.0}** as the
former method default. **`8.0` never existed anywhere.**

**This does not change the `I17-A6` ruling** — `1.0` is still a value never shipped in
either place, which is the property the ruling needed. Reported because the row will be
read again.

### F7 ✅ Where session 2 was right, and precisely right

Not everything moved, and the confirmations matter as much as the corrections:

| Session 2 said | Measurement |
|---|---|
| `A3`: *"only `microWindowSize=50` matters"* | ⭐ Exactly right, and stronger than stated — the band is a **single point**. 49 and 51 both break it |
| `A4`: *"`threshold` is inert (`tfiValue=1.0` clears anything `<1.0`)"* | ⭐ Exactly right, to the boundary: `0.999999` passes, `1.0` fails |
| `A4`: *"`tfiWindowSize=30` is load-bearing"* | Confirmed — it sits on the upper edge |
| `A2`: `dynamicPct` hand-computation | Confirmed by measurement: band `[0, 0.53]`, shipped `0.30` inside |
| `A1`: *"`cvdDiv` is not asserted, so `divergencePriceGate` is inert"* | Confirmed, and sharpened: it is **structurally** unreachable |
| `A1`: *"`weightedSlope` ~1.8M clears any plausible `slopeMinUsd`"* | Confirmed; exact edge is 1.8e6 (6.0e6 after the weight change) |
| `A6`: *"`divergenceGate` wholly inert"* | Confirmed, and sharpened: `CalcOBV` sets `obvTrend` **before** it reads the gate, so it is structurally unreachable |

**15 of 26 confirmed, 11 corrected.** Session 2's per-literal reasoning was sound wherever
it reasoned about *this fixture's data*; it went wrong wherever it reasoned about *the
gate parameters being generous* — which is `A9`/`A14h`, and `A2`'s `floorPct`.

---

## 5. `I17-A6` — commit `98ed4fd`

Ruled in full, applied as ruled, nothing re-litigated.

- `trendGate:=10.0` → **`1.0`** at both `CalcOBV` call sites.
- **Measured band `[0, 47.9]`; `48.0` flips both paths to FLAT** — the ruling's derived
  `obvChange = 48` confirmed by measurement, and by the differential test against the real
  harness (§1).
- The session-2 comment at that call site was **extended, not duplicated**, as instructed.
- `1.0` verified never-shipped in `settings.json` (87 revisions) **and** never held as the
  method-local default (`{0.01, 10.0}`).

---

## 6. The escalation condition — checked, and it did not fire

The ruling: *"any fixture whose `Check()` compares two computed values without pinning
either to a literal expectation must be flagged, because it cannot be swept safely."*

**Zero of the nine match.** Every `Check()` pins at least one computed value to a literal:

| Fixture | `Check()` | Sweepable? |
|---|---|---|
| `A1` | `cvdSlope = "RISING"` | ✅ pinned |
| `A2` | `signal = "BULL_ACCEL"` | ✅ pinned |
| `A3` | `e = 16000 AndAlso (e+m+l) = 50000` | ✅ pinned |
| `A4` | `tfiSignal = "BUY PRESSURE" AndAlso \|tfiValue − 1.0\| < 1e−6` | ✅ pinned |
| `A43b` | four booleans over pinned counts, directions and `tfiVal` | ✅ pinned |
| `A6` | `trendA = "RISING" AndAlso trendA = trendB` | ✅ **survives on its first conjunct** — exactly as the ruling predicted |
| `A9` / `A14h` | `trend = "BEAR" AndAlso passLong = False AndAlso passShort = True` | ✅ pinned |

⭐ **`A6` is worth naming again:** it is the ruling's own exemplar of the vacuous shape, and
it is safe **only** because `trendA = "RISING"` is conjoined to the comparative half. Drop
that one conjunct and the fixture becomes unsweepable and, at `trendGate ≥ 48`, silently
vacuous. **That is an argument for the conjunct, not a defect.**

---

## 7. Display-string parity — NO OBLIGATION, stated explicitly per the hard rule

**Neither commit touches any production code path.** Both are confined to
`verify/ordercheck/Program.vb` — fixture literals and fixture comments only. No
`VerdictResult` or `IndicatorResults` field default changed; `BuildPlaintextSnapshot`
(`UI/MainForm_PlaintextSnapshot.vb`) and `UI/MainForm_Render_Cards.vb` were not opened and
read exactly the values they read before. `verify-gate.ps1`'s own display-parity check
independently reports **`no snapshot/card drift detected`**.

---

## 8. What was verified, and how

- **Every build in the acceptance matrix was actually run**, separately, in the order given,
  after each commit — 6 projects × 2 commits, all `0 errors / 0 warnings` Release.
- **The harness was run and counted**, not inferred: `grep -c '^PASS '` → **326**,
  `grep -c '^FAIL '` → **0**.
- **`verify-gate.ps1 -Mode local-fast` was run** after each commit: `GATE PASSED` both times.
- **`settings.json` is untouched** — `git diff` on it is empty across both commits; still v68.
- ⭐ **The measuring instrument was validated against the thing it measures** — baseline
  agreement plus four differential mutations applied to the real harness (§1). The `A9`
  case matched down to the `details` string, `Bull:0 Bear:2 (need 3)`.
- **Every synthetic value was checked against the complete tracked settings history** (87
  revisions), not against the current file only — which is precisely the check whose
  absence produced finding `F3`.
- **The chosen set was confirmed applied all-at-once**, so interactions among the new
  values are exercised, and the unasserted diagnostics were compared too, not just the
  assertions.

**Not verified:**

- **Whether the same masking shape exists in fixtures outside these nine.** `CalcMTFGate`'s
  vote-redundancy structure is not unique to `A9`/`A14h`; any fixture pinning a threshold
  next to a vote-count or an enable flag can be masked the same way. Out of scope here, and
  it is a real candidate for a follow-up sweep.
- **The bands' behaviour under a `settings.json` parse failure** (POCO-default path). Not
  reachable from these fixtures, which pass every parameter explicitly — that is the whole
  point of session 2's work.
- **Non-numeric probe space.** Only the four numeric/integer parameter types were swept; no
  probe covers `Nothing`/empty-list inputs, which are a different fixture's job.

---

## 9. Open items and feedback — MOVED to the packet, not duplicated here

Per [`batch-review-packet-convention.md`](batch-review-packet-convention.md), decisions and
spec-back feedback are the reviewing seat's working material, not the record's. They live in
[`i17-sweep-spec-back.md`](i17-sweep-spec-back.md):

| In the packet | What it holds |
|---|---|
| §1 | Ranked verification handles — **and which single check to run if you run only one** |
| §2 | The four decisions queued: `D1` commit the probe · `D2` the §15 row · `D3` the "off-EVER-shipped" rule · `D4` sweep the rest. Each with my read, labelled as a hypothesis, where I have one |
| §3 | Spec-back proper — what the ruling got right, which of its assumptions broke, and **the method substitution I made and what it cost** |
| §4 | What I did not verify, and cannot |

**`S2-2`** (splitting `CalcSpread`) stays out of scope, queued separately, and needs its own
proposal per CLAUDE.md's spec-first rule. It is not one of the four decisions above.
