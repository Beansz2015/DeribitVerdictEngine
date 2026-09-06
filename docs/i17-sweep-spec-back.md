# I17-SWEEP + I17-A6 — review packet for the orchestrator

**This is the WORKING DOCUMENT.** The record — per-literal band table, findings, method,
acceptance evidence — is [`i17-sweep-batch-summary.md`](i17-sweep-batch-summary.md). Per
[`batch-review-packet-convention.md`](batch-review-packet-convention.md): two documents,
cross-referenced, never duplicated. Nothing below restates the table; it points at it.

**Ruling being reported against:** [`trader-tick-queue.md`](trader-tick-queue.md) §2, rows
`I17-A6` and `I17-SWEEP`. **Reasoning under test:**
[`a54a-session2-step2-spec-back.md`](a54a-session2-step2-spec-back.md) §2.

**Commits:** `98ed4fd` (`I17-A6`) · `1b2adbe` (`I17-SWEEP`) · this one (docs restructure +
the §0 count correction).

⛔ **Read the superseding note at the top of
[`i17-sweep-batch-summary.md`](i17-sweep-batch-summary.md) first.** Its published §0 counts
were wrong (13/5/8 → **17/5/4**) and commit `1b2adbe`'s message carries the wrong figures.
Nothing else moves; the band table was right throughout.

---

## 1. Ranked verification handles

⭐ **Rank order here is not the usual one, and the reason matters.** The harness and the gate
do **not** discriminate this build: *every value inside its measured band passes*. So the
green gate confirms soundness and says nothing about the actual claim, which is that the
**right** values were chosen for the **right** reasons. The structural checks outrank the
behavioural one.

⛔ **Every handle below was RUN before it was written here**, and every one that greps
`Program.vb` carries a **comment filter**. That is not fussiness — see `H0`.

> ⚠ **HANDLE MAINTENANCE NOTE — the `A64` build (`D1`) moved two of these numbers.**
> `A64a`/`A64b` collapsed `A2`'s and `A9`/`A14h`'s duplicated data builders and literals into
> shared helpers, so the call-site shape changed. Both handles below are now **pinned to the
> commit they were written against** rather than to `HEAD`:
>
> | Handle | At `3f16a9b` (the build these handles review) | At `HEAD` after the `A64` build |
> |---|---|---|
> | `H1` | 24 lines | unchanged — the range is pinned, so it stays 24 |
> | `H3` | 13 lines | **15 lines** — `A2`'s and `A9`/`A14h`'s call sites became two shared helper call sites |
>
> ⭐ **This is the very failure `H0` warns about, arriving from the other direction:** a handle
> whose expected value was correct when written and is wrong two commits later. **A pinned
> commit range is the fix; `HEAD` is not.** See [`a64-fixtures-spec-back.md`](a64-fixtures-spec-back.md)
> for the `A64` build's own handles.

### H0 — the trap these handles are shaped around. Run this first; it costs one command.

This build's new comments **quote the old values on purpose** ("Was 0.15, which EQUALLED
shipped…"). So a naive value grep reports the old literal as still present when it is gone —
the [`trade-store-write-guard-spec-back.md`](trade-store-write-guard-spec-back.md) `R1`
failure exactly, where a reviewer following a handle literally would have rejected a sound
build. Measured, on the committed tree:

| Naive handle | Prints | Truth |
|---|---|---|
| `grep -c '0\.15' verify/ordercheck/Program.vb` | **6** | zero executable call sites use 0.15 |
| `grep -c '10000' …` | **116** | zero use 10000 |
| `grep -c '20\.0' …` | **38** | zero use 20.0 |
| `grep -c '0\.001' …` | **38** | zero use 0.001 |

**⛔ Do not use any bare value grep on this file. Filter comment lines** — in VB that is a
line whose first non-space character is `'`:

```bash
grep -vE "^[[:space:]]*'" verify/ordercheck/Program.vb
```

### ⭐ H1 — IF YOU RUN ONLY ONE, RUN THIS. Every changed value, with zero comment noise.

```bash
git diff 4701ef6..3f16a9b -- verify/ordercheck/Program.vb | grep -E "^[+-]" | grep -vE "^(\+\+\+|---)" | grep -vE "^[+-][[:space:]]*'" | grep ':='
```

**Expected: 24 lines — 12 `-` and 12 `+`**, and that is the *entire* value change set of
both commits in one screen. It covers all 9 call sites and both commits at once, and it is
the check that catches a wrong value, a swapped argument, or an unintended edit.

⚠ **The load-bearing part is the pairing, not the count.** Read the `-`/`+` pairs against
[`i17-sweep-batch-summary.md`](i17-sweep-batch-summary.md) §2's `Was` / `Now` columns. A
correct count with one wrong value passes a count check and fails this one.

### H2 — the arithmetic identity that would expose a silent miscount

From the summary's §2 table, count the `Now` column: **17 changed + 4 kept-as-load-bearing +
1 kept-as-design-constant + 4 left-alone = 26.**

⛔ **Re-derive it from the table; do not take the number on report.** The superseded §0 said
13 + 5 + 8, which also sums to 26 — **two wrong figures that summed correctly is precisely
why the totals check is worthless and the identity is not.** This handle is the one that
caught it.

### H3 — the executable call-site lines (13 at `3f16a9b`, **15 at `HEAD`** — see the note above)

```bash
grep -nE 'slopeMinUsd:=|slopePctOfValue:=|divergencePriceGate:=|SegmentWeight:=|microWindowSize:=|accelThreshold:=|dynamicPct:=|floorPct:=|tfiWindowSize:=|trendGate:=|divergenceGate:=|adxPeriod:=|adxMin:=|minOf:=|candleLookback:=' verify/ordercheck/Program.vb | grep -vE "^[0-9]+:[[:space:]]*'"
```

**Expected at `3f16a9b`: exactly 13 lines** at 691–693, 746–747, 786–787, 820, 887–888, 1054,
1434, 7181 — one group per call site. Use it to confirm no tenth call site was missed and no
comment leaked into the set.

⚠ **At `HEAD` it prints 15**, and that is correct rather than a regression: the `A64` build
replaced `A2`'s inline call and `A9`/`A14h`'s two identical calls with the shared helpers
`RunMicroCvdBurst` (4 lines) and `RunMtfBearGate` (2 lines). **Net effect on the property this
handle checks: three call sites carrying literals became two, and the two duplicated literal
sets became one each.** Run it against `3f16a9b` to review *this* build.

### H4 — no synthetic value is a shipped value, ever. This is the check that would have caught `F3`.

⭐ **`F3` exists because the previous review checked a literal against the CURRENT
`settings.json` only.** The correct check is against the whole tracked history:

```bash
mkdir -p /tmp/sr && i=0; for c in $(git log --format=%H -- settings.json); do i=$((i+1)); git show $c:settings.json | tr -d ' \n' > /tmp/sr/$i.json; done; echo "revisions: $i"
for k in slope_pct_of_value late_segment_weight early_segment_weight accel_threshold accel_threshold_floor_pct threshold divergence_gate adx_period adx_min candle_lookback; do echo -n "$k: "; grep -oh "\"$k\":[0-9.]*" /tmp/sr/*.json | sed 's/.*://' | sort -u -g | tr '\n' ' '; echo; done
```

**Expected: 87 revisions**, and these exact value sets — each of which **excludes the
synthetic now pinned against it**:

| Key | Every value it has ever held | Synthetic now pinned | Collides? |
|---|---|---|---|
| `slope_pct_of_value` | 0.01 · **0.05** · 0.10 | 0.037 | no |
| `late_segment_weight` | 2.0 | 7.0 | no |
| `early_segment_weight` | 1.0 | 3.0 | no |
| `accel_threshold` | 5000.0 · 10000.0 | 1234.0 | no |
| `accel_threshold_floor_pct` | 0.25 | 0.375 | no |
| `TFI.threshold` | 0.15 | 0.42 | no |
| `OBV.divergence_gate` | 0.001 | 0.0077 | no |
| `OBV.trend_gate` | 0.001 · 10.0 · 18.0 · 23.0 | 1.0 | no |
| `adx_period` | 9 | 7 | no |
| `adx_min` | 20.0 | 7.5 | no |
| `candle_lookback` | 60 | 55 | no |

⛔ **The test is PER KEY, and the wording matters — my first draft of this handle said
"appears in no value set", which is false and would fail on a broader grep.** Some synthetics
do appear elsewhere in `settings.json` as values of *unrelated* keys (`3.0` is
`absorb_ratio`, `7` is `lin_reg_period`, `55` is `partial_overbought`, `1.0` is
`atr_stop_multiplier`). ⭐ **That is not the defect.** Item 17's confusability is *"a reader
greps this literal, finds it in the history of THE KEY THIS PARAMETER MIRRORS, and cannot
tell stale from deliberate."* `lateSegmentWeight:=7.0` cannot be misread as `absorb_ratio`.
**Run it per key; a cross-key collision is noise.**

⚠ **Worth the reviewer's eye:** seven of the eleven keys have held exactly **one** value ever.
One could argue equality is harmless for a key that never moves. **I did not treat it that
way** — a never-retuned key is precisely the one that gets retuned with nobody checking the
fixtures, which is `A6`'s own history. Flagging the argument rather than burying it.

### H5 — settings untouched

```bash
git diff 4701ef6..3f16a9b -- settings.json | wc -l
```

**Expected: `0`.** Confirms v68 stands and no `change_log` entry is owed.

### ⛔ H6 — the one check that proves the VALUE CHOICE, and it CANNOT be run from the committed tree

The claim the whole build rests on is not "still passes" — it is **"the unasserted
diagnostics are unchanged, so the mechanism the fixture documents is still exercised."**
`A9`/`A14h` must still report `Bull:0 Bear:3 (need 2)`, `ADX:100.0`, `EMA:BEAR`; `A6` must
still report `divA=NONE divB=NONE`.

**The harness prints only `PASS`, so none of that is observable from the tree.** Verifying it
requires the probe, which is not committed. ⛔ **This is a genuine hole in the packet, and it
is decision `D1` below, not a formality.**

### H7 — the behavioural floor (soundness, not correctness of choice)

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/verify-gate.ps1 -Mode local-fast
```

**Expected `GATE PASSED`** — 6 builds, harness `ALL PASS`, display-parity clean, version-bump
clean. ⚠ **Ranked last on purpose:** it passes for any in-band value, so it cannot distinguish
this build from a careless one.

---

## 2. Decisions queued, with my read where I have one

### `D1` — commit the sweep probe, or not?

| Option | What it is |
|---|---|
| **(a)** | Leave scratchpad-only. Status quo. `H6` stays unrunnable |
| **(b)** | Commit the probe as its own project under `verify/` |
| **(c)** | Fold the probe's *diagnostics* into the harness as 1–2 new fixtures that pin the unasserted values |

**My read, as a hypothesis: (c).** (b) commits ~330 lines that duplicate every fixture's data
builder — it would rot on the first fixture edit, and it is the copy-drift shape CLAUDE.md's
fixture-literal provenance rule exists to prevent. (c) keeps the *property* and discards the
scaffolding: a fixture asserting `A9`'s `details` contains `Bear:3 (need 2)` makes `F1`'s
spare-vote finding permanent and would fail loudly if a future edit silently drops a vote.
**That is worth more than an archived probe** — it converts a one-off measurement into a
standing guard.

**Scoping, supplied without recommending it:** (c) touches `verify/ordercheck/Program.vb`
only — no new project, no `.vbproj`, no solution change. (b) needs a new `.vbproj`; per
CLAUDE.md the root project already carries a `Compile Remove` for `verify/**`, so it would
not break the solution build — but it does need the same `Compile Include` list
`OrderCheck.vbproj` carries, which is a second copy of *that*.

### `D2` — the `docs/DeribitIndicatorProject.md` §15 row

| Option | What it is |
|---|---|
| **(a)** | No row. Fixture-only commits, zero runtime reach |
| **(b)** | **One** row covering session 2 **and** this arc |
| **(c)** | Two rows — one for session 2's still-owed row, one for this |

**My read, as a hypothesis: (b).** Session 2's reviewer already ruled a row is owed
([`a54a-session2-step2-spec-back.md`](a54a-session2-step2-spec-back.md) §7.3) on the grounds
that §15's operating bar is *"a notable change worth recording"*, not CLAUDE.md's narrower
*"changes engine behaviour"* — **and that row was never added.** This arc is the second half
of the same movement: session 2 deleted the `Optional` defaults, this build fixed the
literals that deletion made explicit. One item, one row, which is §15's own rule.

⚠ **CLAUDE.md's session-start note argues against (c) independently:** §15's growth is now in
**cell content**, not row count, and a second row costs more than it buys.

⭐ **`D2` shares a root with session 2's open §7.3 finding — ruling them together is cheaper
and avoids one arc getting a row while its other half does not.** `D1` and `D2` do **not**
share a root.

### `D3` — does `F3` become a standing rule?

`F3`: `A1`'s `slopePctOfValue:=0.05` was cleared as *"off-shipped"* against the **current**
`0.10`, but the key has historically held `{0.01, 0.05, 0.10}`. **"Off-shipped" must mean
off-*ever*-shipped.**

| Option | What it is |
|---|---|
| **(a)** | Leave it as a finding in this packet |
| **(b)** | Add one sentence to CLAUDE.md's fixture-literal provenance rule |

**My read, as a hypothesis: (b), one sentence.** The existing rule's own worked examples
(`A20a`/`A20b` at 2.0/0.5 vs the shipped 1.6/0.625) reason explicitly about **current**
values, so a careful reader following the rule as written reproduces `F3`. ⛔ **But CLAUDE.md
is yours, not mine — I have not touched it and will not without a ruling.**

### `D4` — is the masking shape elsewhere? Do we sweep the rest?

**I have no read on priority — that is a scheduling call, and it is yours.** What I can
supply is the scoping, which I measured rather than guessed:

| Shape | Where it exists in `Core/` | Does a fixture pin it? |
|---|---|---|
| **(a) N-of-M votes vs a minimum** — the `F1` shape | `Indicators_Structure.vb:505-507` (`CalcMTFGate`) · `AlertsTracker.vb:152,190` · `Indicators_OrderFlow.vb:209,213` · `ScoringEngine_Calculate_Scoring.vb:97` · `ScoringEngine_Calculate_Verdict.vb:92` | **Only `CalcMTFGate`** — `grep 'minOf:=\|minTrades:=\|minAggrUsd:='` over the harness returns the two sites already fixed |
| **(b) a parameter guarded by another parameter** — the `F4` shape | `Indicators_OrderFlow.vb:441` (`If dynamicPct > 0.0`) — **the only instance**. `OfiAccumulator.vb:84` (`If tauSec <= 0.0`) is the same *class* (a mode switch that kills the path) | `tauSec` is pinned by **no** fixture, by name |

⭐ **So within named-argument call sites the population looks closed at the two instances
already found** — which is a direct dividend of session 2's work making these parameters
required and named.

⚠ **The residual, stated so it is not assumed covered:** this scan sees **named** arguments
only. It does not cover positional passing, nor fixtures that build a whole `cfg` POCO
(`BuildA8Cfg`, `BuildResolutionCfg`), where the same masking can hide with no `:=` to grep
for. ⚠ **My first pass at this scan was line-anchored (`^\s*If`) — the exact VB trap CLAUDE.md
warns about — and I redid it unanchored.** The anchored version missed nothing here, but that
was luck, not method.

---

## 3. Spec-back proper — feedback on the ruling

### What the ruling got right, specifically

- ⭐ **"Do NOT decide from the packet's argued insensitivity — MEASURE it per literal."** This
  is the whole value of the exercise. **Eleven of 26 classifications moved**, and every one
  came from a probe, not from re-reading the argument. Had this been ruled the other way, all
  eight MTF literals would have been made synthetic on reasoning that the measurement shows
  is wrong about *why* they are safe.
- ⭐ **"Assert the ASSERTED VALUES, not the pass/fail bit."** Necessary, and it is what made
  the band table a table of *values* rather than of booleans.
- ⭐ **"Any fixture whose `Check()` compares two computed values without pinning either …
  cannot be swept safely — flag it, don't measure it."** Correctly specified, correctly
  checked, and it did not fire. Naming `A6` as the exemplar was what made the check fast: I
  knew the shape to look for.
- ⭐ **"Bundle `I17-A6` into the same session"** paid off for a reason the ruling did not
  anticipate: `A6` became the **differential-validation case for the whole instrument** (its
  boundary at 48.0 is sharp and independently derivable), which is what let me trust the
  replica before drawing conclusions from it.

### Which assumptions broke

- ⛔ **The two-way outcome is one category short.** The ruling maps *wide band ⇒ inert ⇒
  synthetic is safe* and *narrow band containing shipped ⇒ load-bearing ⇒ keep*. The data
  needs a third: ⚠ **MASKED** — a literal whose band is wide **only because a sibling at the
  same call site disables or dominates it**. `adxMin` reads inert across five orders of
  magnitude and is load-bearing the moment `minOf` moves; `floorPct` reads inert across
  `[0, 1000]` and is unreachable dead code. **Masked literals produce the WIDEST bands of
  all** — so under the ruling as written, the most confident "genuinely inert" verdicts are
  the least trustworthy ones. **Band width is not evidence; the reason for the width is.**
- ⚠ **"The sweep hands you the value as a by-product — pick from the middle, far from
  shipped" needs a guard.** It hands you a value that preserves the *assertion*. It does not
  hand you one that preserves the *mechanism*. `adxPeriod:=40` sits comfortably mid-band and
  silently switches off the ADX vote, in a fixture whose own docstring claims to exercise
  "ADX strong". Two of my values (`adxPeriod:=7`, `adxMin:=7.5`) were chosen **against**
  band-width for exactly this reason.
- ⚠ **"Assert the asserted values" is necessary but not sufficient.** It catches the trap the
  ruling names — passing for the wrong reason. It does **not** catch `F2`: a fixture whose
  asserted values are stable while a documented component quietly stops being exercised.
  ⭐ **Printing the UNASSERTED outputs beside the asserted ones is what caught that, and it
  cost nothing.** Worth writing into any future sweep instruction as a third line.

### ⛔ Where I substituted a method, and what it cost

**The ruling says: *"re-run the harness with the literal set across a range."*** Taken
literally that is one rebuild per probe — roughly 150 builds for the one-at-a-time pass alone,
before any joint grid. **I substituted an in-process replica** that links the same shipped
sources and re-evaluates the fixtures without rebuilding, which made 608 probes plus five
joint grids affordable and is the only reason `F1`, `F4` and `F5` were found at all — they
all needed grids, not points.

**What it cost:** the replica can drift from the fixtures it copies, a risk the literal method
does not have. **I paid for it explicitly** — baseline agreement on every asserted tuple, plus
four differential mutations applied to the *real* fixtures, rebuilt and run
([`i17-sweep-batch-summary.md`](i17-sweep-batch-summary.md) §1). ⚠ **Flagging it because a
substitution that works is still a substitution, and the next seat should know the ruling's
literal method was not the one executed.**

### Constraint pair that nearly conflicted — and the hatch

Three standing rules point in different directions at the same call site:

1. CLAUDE.md's **fixture-literal provenance rule**: MECHANISM ⇒ a literal is *correct*.
2. Queue **item 17 option (c)**: the literal must *also* be obviously synthetic.
3. Session 2's own choice: every literal is *byte-identical to the pre-S2 implicit default* —
   the deliberate **opposite** of (2).

⭐ **The escape hatch is that they are sequential, not simultaneous**, and the `I17-SWEEP`
queue row already names it: session 2 was right to freeze behaviour *during* the edit;
item 17 applies *after*. **That sentence did real work and should survive into whatever
becomes the standing rule** — without it a reader sees a contradiction between the repo's own
conventions and picks one.

⚠ **One rule that looks applicable and is not:** the *"a value ruled into a CONSTANT goes
`Public Const`"* rule. These are fixture-local call arguments, not constants, so nothing here
should become a `Const`. Naming it so the next seat does not reach for it.

---

## 4. What I did not verify, and cannot

- ⛔ **`H6` — the unasserted diagnostics — is not independently verifiable from the committed
  tree.** I verified it with the probe and the probe is not committed. This is the single
  largest gap in the packet, and it is `D1`.
- ⚠ **Positional and cfg-built call sites** were not scanned for the masking shape (`D4`
  residual). The named-argument scan is close to complete for the indicator surface, and says
  nothing about the rest.
- ⚠ **Bands are measured, not proved.** A band is the range over the *probed points*; between
  two adjacent probes the fixture could in principle break. Edges were bracketed tightly where
  one was found (73999/74000, 47.9/48.0, 0.53/0.54, 30/31, 0.999999/1.0), but the "none found"
  rows are *no edge within the probe range*, not *no edge*.
- ⚠ **The `settings.json` parse-failure path (POCO defaults)** is not reachable from these
  fixtures, which now pass every parameter explicitly. Unverifiable by nature here, and that
  is the point of session 2's work rather than a gap in it.
- ⚠ **Non-numeric probe space** — no probe covers `Nothing` or empty-list inputs. A different
  fixture's job.
- ⚠ **`A14h` was measured, not assumed identical to `A9`** — but it is the same data and the
  same assertion, so its four rows carry no independent information. If `A9`'s reasoning is
  wrong, `A14h`'s is wrong the same way.
- ⛔ **I did not verify the corrected 17/5/4 counts against anything but the diff and the band
  table.** They agree with each other. Given that the first published figures were wrong,
  **`H2` is worth running rather than trusting this sentence.**

---

## 5. ⭐ REVIEWER VERDICT — ACCEPTED, 2026-09-06. D-table ruled.

**Reviewed by the seat that wrote the ruling and wrote no code on this build.**

### 5.1 Verified independently

| Check | Result |
|---|---|
| Harness, re-run | **326 PASS · 0 FAIL · `ALL PASS`** |
| `verify-gate.ps1 -Mode local-fast`, re-run | ✅ **`GATE PASSED`** |
| ⭐ **`F3` — was `slope_pct_of_value` ever `0.05`?** | ⛔ **CONFIRMED. Walked every tracked revision: `{0.01, 0.05, 0.10}`. `0.05` WAS shipped.** The session-2 reviewer cleared that literal as *"off-shipped"* against the current `0.10` and **was wrong** |
| ⭐ **`F6` — was `trend_gate` ever `8.0`?** | ⛔ **CONFIRMED. History is `{0.001, 10.0, 18.0, 23.0}`. `8.0` never existed** — the reviewer invented it when writing the `I17-A6` queue row |

⭐⭐ **Two of the reviewer's own published claims, falsified by measurement. That is precisely
what this build was commissioned to do, and it did it.**

### 5.2 The build is accepted

`F1`'s spare-vote finding, `F4`'s dead-`floorPct`, and `F5`'s joint failure are the three
that justify the whole exercise — **each is a case where a wide one-at-a-time band is an
artefact of a sibling literal, and a naive sweep would have licensed any value at all.** The
four-class model (STRUCTURALLY INERT / BAND-INERT / MASKED / LOAD-BEARING) is a better
instrument than the two-class one the ruling supplied, and **`MASKED` is the class that did
the work.**

⭐ **Rule 2 — *"band width licenses a change; it does not compel one"* — is right and should
outlive this build.** Keeping `A2.microWindowSize` at 50 because the fixture's data is built
around it, despite a `[20, 1000]` band, is the correct call.

⭐ **The self-caught count error (13/5/8 → 17/5/4), corrected in place with the note that the
commit message still carries the wrong figures, is the right handling.** History not
rewritten, correction where a reader will meet it.

### 5.3 ⛔ D-TABLE RULED

| # | Ruling |
|---|---|
| **`D1`** | ✅ **(c), as recommended — but SCOPED.** Not a general diagnostic layer: **two fixtures in family `A64`** pinning exactly the mechanisms this build exposed. **`A64a`** asserts `A9`'s `details` carries the spare vote (`Bear:3` against `need:2`) — that makes `F1` permanent and fails loudly if a future edit silently drops a vote, and it is the only thing that stops `F2`'s partial-vacuity recurring. **`A64b`** pins `A2`'s joint dependency (`microWindowSize` × `dynamicPct`, the `F5` pair) so the masking cannot silently return. ⛔ **(b) rejected for the reason given — ~330 lines duplicating every fixture's data builder is the copy-drift shape the provenance rule exists to prevent.** (a) rejected: it leaves the value choice unpinned |
| **`D2`** | ⚠ **(b) in substance, but the premise is WRONG and the action is different.** The packet says session 2's §15 row *"was never added"*. **It was — by the reviewer, on 2026-09-05 in commit `5c34d39`**, which collapsed the whole A54a arc into ONE row per §15's own one-item-one-row rule; that row already carries *"SESSION 2 (the ruling's scoped (b)): 44 `Optional` defaults deleted across 15 methods."* ⭐ **So: do NOT add a row. UPDATE the existing A54a row** — close its *"STILL OPEN … queue item 17 applied to `A6`"* clause, and add **only** the two findings that outlive the build: **`F1`'s MASKED lesson** (a wide one-at-a-time band can be a sibling artefact) and **`F3`'s off-*ever*-shipped rule**. ⛔ **The band table stays in the packet — §15's growth is cell content, and this row is already large** |
| **`D3`** | ✅ **(b), YES — add the sentence.** ⭐ **The evidence is that the rule as written misled its own reviewer:** `F3` is exactly the error the session-2 review made, following the rule's own worked examples, which reason about **current** values. **Draft, to be inserted in CLAUDE.md's fixture-literal provenance rule:** *"⚠ **MECHANISM means off-EVER-shipped, not off-currently-shipped.** Check a literal against every tracked revision of `settings.json`, not today's — a former shipped value is exactly as confusable as a current one, and is how `A6`'s `10.0` survived."* |
| **`D4`** | ✅ **(a) — NO further sweep now, and the reason is the scoping, not fatigue.** The packet measured the named-argument population **closed** at the two instances already fixed — ⭐ **a direct dividend of session 2 making these parameters required and named.** ⛔ **But the residual is queued, not assumed covered:** positional passing and whole-`cfg` builders (`BuildA8Cfg`, `BuildResolutionCfg`) are outside that scan, and **`OfiAccumulator.vb:84`'s `tauSec` is the one same-class mode-switch pinned by NO fixture.** Queued at low priority |

### 5.4 One correction to the packet, and it is small

§2's note *"the confusable set is 22 of 26, not 21"* is right, and the reviewer's queue row
said 21. **Corrected by measurement, accepted.** ⚠ The reviewer's `I17-A6` row also carried
the invented `8.0` (`F6`) — **both queue-row errors are the reviewer's, both are now on
record, and neither changes a ruling.**
