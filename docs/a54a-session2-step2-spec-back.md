# A54a session 2, step 2 — the build — spec-back

**Solo build; one document, per [`batch-review-packet-convention.md`](batch-review-packet-convention.md)'s
own scope note (it governs multi-lane batches).**

**Spec:** [`a54a-json-poco-drift-guard-spec.md`](a54a-json-poco-drift-guard-spec.md) §7,
§7.2. **Measurement:** [`a54a-session2-step1-measurement-2026-09-05.md`](a54a-session2-step1-measurement-2026-09-05.md).
**Ruling:** [`trader-tick-queue.md`](trader-tick-queue.md) §2, rows `S2-1` (ruled, built) ·
`S2-2` (queued, not touched).

**Commits:** `fded077` (13 fixture-only methods) · `94b68d5` (`CalcSpread`, the one
production edit).

---

## 0. Outcome

| | Result |
|---|---|
| Signatures edited | 15 methods (14 in-scope + `CalcSpread`), 44 parameters made required |
| Fixture call sites edited | 9 (the exact 9 the measurement predicted) |
| Production call sites edited | 1 (`LiveMicrostructureEvaluator.vb:135`) |
| Build matrix | Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck`, each 0/0 Release |
| Harness | 326/326 `PASS`, `ALL PASS` |
| `verify-gate.ps1 -Mode local-fast` | `GATE PASSED` |
| Settings | Unchanged — still v68, `git diff` on `settings.json` is empty |
| Display-string parity | Checked per commit — see §4 |

---

## 1. Trap A — the per-call-site diff review, and how it was done

**Method:** every new or edited argument list was written using **named arguments
exclusively** (`paramName:=value`) — zero positional arguments were added anywhere in
either commit. This is the strongest available defence against trap A's silent-swap risk:
a positional mistake compiles cleanly and is wrong; a named-argument mismatch either
compiles correctly or fails to compile (unknown parameter name), so the only residual risk
is a **correctly-named but wrong-*value*** argument, which is what the review below checks.

**Commit 1 — the 9 fixture call sites, argument name vs. signature, checked one by one:**

| Call site | Signature order | Arguments supplied | Match |
|---|---|---|---|
| `A1` `CalcCVD` | `slopeMinUsd, slopePctOfValue, divergencePriceGate, lateSegmentWeight, earlySegmentWeight` | same order, same names, `:=` | ✅ |
| `A2`/`A3` `CalcMicroCVD` | `microWindowSize, accelThreshold, dynamicPct, floorPct` | same order, same names, `:=` | ✅ |
| `A4`/`A43b` `CalcTFI` | `tfiWindowSize, threshold` | same order, same names, `:=` | ✅ |
| `A6` ×2 `CalcOBV` | `trendGate, divergenceGate` | same order, same names, `:=` | ✅ |
| `A9`/`A14h` `CalcMTFGate` | `adxPeriod, adxMin, minOf, candleLookback` | same order, same names, `:=` | ✅ |

**Commit 2 — the one production call site:** `CalcSpread(book, sBps, sStatus,
wideThresholdBps:=cfg.Indicators.Spread.WideThresholdBps,
tightThresholdBps:=cfg.Indicators.Spread.TightThresholdBps)` — copied letter-for-letter
from the pattern already in `UI/MainForm_Analysis.vb:422-424`, then diffed against the
`SpreadSettings` class (`WideThresholdBps`/`TightThresholdBps`, `EngineSettings.vb:649-651`)
to confirm the property names. ✅ Match.

**The signature edits themselves** (`git diff` reviewed in full for all 4
`Core/Indicators_*.vb` files plus `Core/Indicators_OrderFlow.vb`'s `CalcSpread`): every
edit is a pure `Optional X As Type = default` → `X As Type` transformation. Zero
parameters were reordered, renamed, or retyped. The five-adjacent-`Double`s case
(`CalcVPFRLite`) and the inversion-risk pairs (`CalcCVD`'s late/early,
`CalcOFI`'s buy/sell dominant ratio) all fall in this category — signature-only, no new
argument written, so their trap-A risk in this build is zero (their call sites were
already fully supplied before this build and none were touched).

---

## 2. Trap B — the 26 provenance decisions, MECHANISM vs. SHIPPED BEHAVIOUR

**Verdict: all nine call sites, all 26 omitted arguments, are MECHANISM.** None is
SHIPPED BEHAVIOUR. Reasoning per site:

| # | Site | Values supplied | Verdict | Why |
|---|---|---|---|---|
| 1 | `A1` `CalcCVD` | `50000, 0.05, 0.002, 2.0, 1.0` | MECHANISM | Asserts only `cvdSlope = "RISING"` (chronological old-sells→recent-buys classification). `weightedSlope` ≈ 1.8M clears any plausible `slopeMinUsd` by two orders of magnitude; `cvdDiv` is never asserted, so `divergencePriceGate` is inert. Literals = the method's own former defaults |
| 2 | `A2` `CalcMicroCVD` | `50, 10000, 0.0, 0.25` | MECHANISM | Asserts `signal = "BULL_ACCEL"`. **Verified by hand-computation, not assumed:** `dynamicPct`'s method default (0.0, static mode) and shipped cfg (0.30, dynamic mode) compute *different* `effThreshold` (10000 vs. 41400) — but both clear `microLate(90000) > microEarly(16000) + effThreshold`, so both produce `ACCELERATING`. Genuinely insensitive, checked not assumed |
| 3 | `A3` `CalcMicroCVD` | `50, 10000, 0.0, 0.25` | MECHANISM | Asserts only `e = 16000` and `e+m+l = 50000` — `momentum`/`signal` are computed but never read, so `accelThreshold`/`dynamicPct`/`floorPct` are wholly inert; only `microWindowSize=50` matters (excludes the 10 huge sells) |
| 4 | `A4` `CalcTFI` | `30, 0.15` | MECHANISM | `tfiWindowSize=30` is **load-bearing** — the fixture's 30-sell/30-buy split is built around it; a different window would mix sells in and fail the `tfiValue=1.0 ± 1e-6` assertion. `threshold` is inert (`tfiValue=1.0` clears anything `<1.0`) |
| 5 | `A43b` `CalcTFI` | `threshold:=0.15` (`tfiWindowSize:=30` pre-existing) | MECHANISM | Same reasoning as #4 — the window's last 30 trades are all buys, `tfiVal=1.0` |
| 6, 7 | `A6` ×2 `CalcOBV` | `divergenceGate:=0.001` | MECHANISM | `Check()` asserts `trendA`/`trendB` only, never `divA`/`divB` — wholly inert. **`trendGate:=10.0` NOT touched — see §3, trap C** |
| 8, 9 | `A9`/`A14h` `CalcMTFGate` | `9, 20.0, 2, 60` | MECHANISM | 70-candle series falling 100/bar every bar — built to classify `BEAR` unambiguously under any reasonable gate parameter, not a calibration-sensitivity test |

**Pattern across all nine:** every value supplied is the **method's own former `Optional`
default**, not a value derived from `cfg`. This was a deliberate, uniform choice —
CLAUDE.md's rule requires MECHANISM values to be literals with a comment saying so and
why, and using the pre-existing implicit default (rather than some other arbitrary
MECHANISM literal) makes each edit **byte-identical to the fixture's pre-S2 behaviour**,
which is the cleanest possible instance of "a literal is correct here" — nothing about
what these fixtures test changed, only that the value moved from implicit (a default) to
explicit (a comment-justified literal).

**Why none is SHIPPED BEHAVIOUR:** every one of the nine fixtures (`A1`–`A9`/`A14h`) is
part of the original engine-correctness-pass family (`A1`–`A9`) or its direct descendants
— fixtures that construct a deliberately extreme, synthetic dataset to expose an
**algorithm-correctness** bug (chronological ordering, window-selection-from-end,
classification-threshold logic), not fixtures that assert "the shipped calibration
produces verdict X on realistic data" (that is `A8`'s job, which already builds its cfg
via `BuildA8Cfg()`). Deriving these nine from `cfg` would not make them more correct — it
would silently couple a MECHANISM test's outcome to a calibration knob it was never
designed to track, the same failure shape `A2`'s hand-computation was checked against.

---

## 3. Trap C — the queue item 17 collision, named not silently fixed

`A6`'s two `CalcOBV` calls (`Program.vb`, `A6_ObvNormalisation`) already pinned
`trendGate:=10.0` before this build — a value already on record as **stale** against the
shipped `23.0` (spec §8, queue item 17, MECHANISM ruled 2026-09-03 but never applied to
`A6`). Adding the now-required `divergenceGate` forced touching this call site.

**What was done:** `divergenceGate:=0.001` was added; `trendGate:=10.0` was left
byte-identical. A comment was added at the call site naming the collision explicitly
(quoted in §2 above), and it is named here again per §7.2 trap C's explicit instruction —
*"name it in the spec-back rather than editing `A6` silently."* **`A6`'s `trendGate` is
NOT touched by this build.** Applying item 17's convention to it remains its own open
slot.

---

## 4. Display-string parity — checked per commit

**Commit 1:** no production call site was touched. Every rendered surface
(`UI/MainForm_PlaintextSnapshot.vb`, `UI/MainForm_Render_Cards.vb`) reads exactly the
values it read before this commit, because the production paths that feed those surfaces
were never edited. **NO OBLIGATION**, and it is trivially true rather than merely stated.

**Commit 2 — checked, not assumed, because this one touches a rendered value's feeder:**
`SpreadStatus` feeds the Step-2 WIDE scoring penalty, the breakdown note, the snapshot
line, and two card bindings (per queue row `S2-2`'s own analysis, §9). The check:

- The tracked `settings.json` was read directly (not inferred): `wide_threshold_bps: 5.0`,
  `tight_threshold_bps: 1.5` — identical to the method's former hardcoded defaults.
- `LiveMicrostructureEvaluator.vb`'s `CalcSpread` call now passes these same two values
  explicitly instead of implicitly; the `sStatus` it computes is discarded exactly as
  before (never read — `snap.HasSpread` comes from `HasTopOfBook`, unchanged).
- `UI/MainForm_Analysis.vb:422-424` (the call site that actually feeds `r.SpreadStatus`,
  and from there the four rendered surfaces) was **not edited at all** — it already
  supplied both parameters by name before this build.

**Conclusion: zero rendered-surface change, on both the value and the shape/presence
axes**, verified against the tracked settings file and the unedited feeder call site, not
assumed from "the numbers happen to match."

---

## 5. What was NOT done, and why

- **`S2-2`** (splitting `CalcSpread` into `CalcSpreadBps` + `ClassifySpread`) — explicitly
  out of scope per this build's own instructions; queued separately, needs its own
  proposal + D-table per CLAUDE.md's spec-first rule.
- **Queue item 17 applied to `A6`'s `trendGate:=10.0`** — named in §3, not fixed. Its own
  open slot.
- **A `docs/DeribitIndicatorProject.md` §15 entry** — not added. Both commits are verified
  zero-behaviour-change on every runtime path, including the settings-parse-failure path
  (commit 2's `New EngineSettings()` POCO defaults for `SpreadSettings` also read
  `5.0`/`1.5`, identical to the tracked JSON), so neither meets CLAUDE.md's "commit that
  changes engine behaviour" trigger for a version-history row. This build's own acceptance
  criteria did not name a §15 entry as required, unlike session 1's.
- **A separate `*-batch-summary.md`** — this is a solo build, not a multi-lane batch;
  `batch-review-packet-convention.md`'s own scope note excludes it, so this single
  spec-back carries everything.

---

## 6. What was verified, and how

- **The compiler's error list matched the measurement's predicted 9 call sites exactly**
  (26 `BC30455` errors, resolving to the same 9 file:line locations §2.1 of the
  measurement doc names) — an independent cross-check that both the measurement and the
  edit are internally consistent.
- **`A2`'s dynamic-vs-static threshold divergence was hand-computed**, not assumed
  insensitive by pattern-matching against the other eight sites (§2, row 2).
- **The shipped `settings.json` was read directly** for `CalcSpread`'s "zero behaviour
  change" claim (§4), not inferred from the ruling's own text.
- **Every build in the acceptance matrix was actually run**, each separately, in the order
  given, after each commit and again on the final combined state.
- **`git diff` on `settings.json` across both commits is empty** — confirmed via `git diff
  f1e216a HEAD -- settings.json`, not assumed from "no key changed."

**Not verified:**

- Whether any OTHER method in the codebase (outside the 14+1 in this arc) carries a
  similar Optional-default-vs-cfg-drift risk — out of scope for this build, which is
  confined to the population step 1 measured.
- The live app's behaviour under an actual forced `settings.json` parse failure — the
  claim in §5 (POCO defaults match tracked JSON) is a static read of both sources, not a
  live-triggered parse failure.

---

## 7. ⭐ REVIEWER VERDICT — ACCEPTED, 2026-09-05. One finding, and it is the reviewer's omission.

**Reviewed by the spec author, who wrote no code on this build.**

### 7.1 Verified independently, not taken on report

| Check | Result |
|---|---|
| Harness, re-run | **326 PASS · 0 FAIL · `ALL PASS`** |
| Solution Release build, re-run | **0 errors** |
| `verify-gate.ps1 -Mode local-fast`, re-run | ✅ **`GATE PASSED`** — display-parity clean, version-bump clean |
| `settings.json` across both commits | ✅ **`git diff f1e216a HEAD -- settings.json` is 0 lines.** Still v68 |
| Two commits, correctly split | ✅ `fded077` harness-only + 4 indicator files; `94b68d5` `CalcSpread` + the one production call site. **Each self-compiles** |
| ⭐ **Trap A defence** | ⭐⭐ **Confirmed by reading the diff: every added argument is named (`:=`). ZERO positional arguments were introduced anywhere.** That is a stronger defence than the per-site review I asked for — it converts the silent-swap class into a compile error, leaving only wrong-*value*, which §1's table then covers |
| **Trap C** | ✅ `A6`'s `trendGate:=10.0` is **byte-identical**; `divergenceGate:=0.001` added beside it with the collision named in a call-site comment. **Not silently fixed** |
| ⭐ **Trap B's comments** | ⭐ **Present, at every call site, and they are the real thing** — each names MECHANISM, says *why* the value is inert or load-bearing, and cites the rule. **`A2`'s carries the actual arithmetic** (`effThreshold` 10000 vs 41400; 90000 clears `microEarly` + either), not an assertion of insensitivity |

### 7.2 The provenance work is sound — spot-checked on the two hardest cases

- **`A1`'s `slopePctOfValue:=0.05`** now pins a value that is **off-shipped** (POCO and JSON
  both read 0.10 since the R-2/R-3 build). ⭐ **That is correct and is exactly the
  `A20a`/`A20b` case CLAUDE.md names as legitimate** — a MECHANISM literal, declared as one,
  in a fixture that tests chronological classification rather than calibration.
- **`A9`/`A14h`'s `adxPeriod:=9, adxMin:=20.0, minOf:=2, candleLookback:=60`** — the reviewer
  read the tracked `settings.json`: shipped `mtf_gate` is **9 / 20.0 / 2 / 60**, identical.
  So nothing moved, and the asserted insensitivity is moot in fact. ⚠ **Noted only as an
  asymmetry:** `A2` earned a hand-computation while `A9`/`A14h` earned an assertion. It costs
  nothing here because the values match, but *"built to classify BEAR unambiguously under any
  reasonable gate parameter"* is reasoning, and this arc's standing lesson is that reasoning
  about insensitivity is what `A62f` and `A63a` both defeated. **Not a finding; a note.**

### 7.3 ⛔ THE FINDING — a §15 row IS owed, and the acceptance list that omitted it was mine

§5 declines a `docs/DeribitIndicatorProject.md` §15 entry on the grounds that neither commit
changes engine behaviour, and notes — **correctly** — that *"this build's own acceptance
criteria did not name a §15 entry as required, unlike session 1's."* ⭐ **That is factually
right: the reviewer's own acceptance list omitted it. Sixth reviewer correction in this arc.**

⛔ **But the conclusion does not hold against §15's own practice.** The reviewer counted the
rows: **ten of the current thirteen are marked `settings-untouched`**, and several change no
engine behaviour whatever — *"Trade identity in the trade store"* states outright **"ZERO
scoring impact, NO rendered surface, NOT a dataset boundary"**; `SH-1` changed a coverage
report's hour classification; `C1` added a CLI verb. ⭐⭐ **§15's operating bar is "a notable
change worth recording in the version history", not CLAUDE.md's narrower
"changes engine behaviour" trigger.**

**And this build clears that bar more clearly than most rows already there:** it deleted **44
`Optional` defaults across 15 methods**, removing **copy 1 of the three-copies-plus-JSON
problem for the entire indicator surface.** ⛔ **A future seat reading §15 sees session 1's
guard row and has no way to learn that the method-default copy is gone.** That is precisely
the gap §15 exists to close.

**Action: add one §15 row covering both commits.** Settings stays **v68**, no `change_log`
entry — the row records the structural change, not a version bump. **Not blocking; it is a
doc row, and the code is accepted as it stands.**

### 7.4 Everything else

**No other findings.** The build is exactly the ruled scope, both traps A and C are handled
better than specified, the 26 provenance decisions are individually reasoned rather than
batch-labelled, and §6's compiler cross-check — **26 `BC30455` errors resolving to the same 9
file:line locations step 1 predicted** — is a genuinely good independent corroboration of the
measurement and the edit at once. ⭐ **Nobody asked for that check; it is the best thing in
§6.**
