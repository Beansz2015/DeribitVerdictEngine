# Spec — Kelly as one class, with b from the placed levels net of fees

**Written:** 2026-09-25 (UTC; `date -u` read 08:53) by the Kelly spec seat, trader-directed 2026-09-24. **Status:** ⏸ **SPEC ONLY — NOT BUILDABLE until the trader ticks the decisions in section 4.** Nothing is built. **Baseline commit:** `b99fce8`; every `file:line` below was read at that commit.

**It combines two items:**

1. **The one-class change**, RULED 2026-09-24 by the trader: `QD-1` = (a) + (c), `QD-2` = (a). Decision text: [`docs/q1d-tier-geometry-spec-back.md`](q1d-tier-geometry-spec-back.md) §3 (it wins over this summary). Evidence: [`docs/q1d-tier-geometry-read-2026-09-24.md`](q1d-tier-geometry-read-2026-09-24.md) §6.
2. **The parked placed-payoff proposal**, [`docs/kelly-placed-payoff-proposal.md`](kelly-placed-payoff-proposal.md), decisions `K-1` to `K-4`. NOT ticked; parked 2026-09-18; the trader asked on 2026-09-24 to settle them in one pass with item 1.

**Vocabulary:** [`docs/DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a (success rate, gross and net breakeven rate, net EV per trade). Fees: `scoring.trade_costs`, maker/maker, 3.00 bps round trip at settings v68.

**Legend. IDs used in this doc:**

| ID | Source and kind | Meaning |
|---|---|---|
| `QD-1`, `QD-2` | Decisions in `docs/q1d-tier-geometry-spec-back.md` §3, RULED 2026-09-24 | `QD-1` (a)+(c): tiers are not re-cut; Kelly treats all tiers as one class. `QD-2` (a): p = the measured pooled success rate |
| `K-1` … `K-4` | Decisions in `docs/kelly-placed-payoff-proposal.md` §4, NOT ticked | `K-1` payoff basis · `K-2` settings key · `K-3` sequencing · `K-4` advisory wording |
| `KO-1` … `KO-5` | This doc, new decisions (section 4) | Questions the two items raise together. Named `KO-` so they do not collide with `K-n` |
| `F-1` … `F-7` | This doc, findings (section 2) | What reading the tree and measuring the book showed |
| `H-n` / `E-n` | This doc, section 9 | `H-n` = a check the reader can run; `E-n` = evidence the reader cannot re-run as committed |
| `A10`, `A22a` | Harness fixtures, `verify/ordercheck/Program.vb` | `A10` = Kelly inverse-contract sizing and leverage cap; `A22a` = bridge payload field by field |
| P0 | The prior diagnosis read's population | 8,810 weekday directional rows, 2026-07-06 to 2026-09-11, halves H1 / H2. Export `backtest_data/swing-fallback-read/diagnosis-rows.csv`, MD5 `e7e93f2fa66e60a3119ce09947b2e063` (the MD5 `docs/q1d-tier-geometry-2026-09-24-output.md` names) |
| Book | This doc, newly coined | The eval cache rows that feed the runtime p and b: see section 3.1 |

---

## 0. Implementer recommendation (read first)

| Item | Detail |
|---|---|
| **Model / effort** | **Sonnet, high.** The judgment is in section 4; once the trader ticks it, every piece has an in-repo template. The book is a pure fold over the eval cache like `LivePerformanceTracker.ComputeWindows` (`LivePerformanceTracker.vb:583`). The fee-aware ratio is `BuildNetRRLine` (`UI/MainForm_Render_Cards.vb:39-61`). The placed levels are `SignalEmitter.ComputeSideLevels` (`Core/SignalEmitter.vb:262`) |
| **Session split** | **Session 1 (Sonnet, high):** the book function, the new `CalcKellySizing`, `VerdictResult` fields, fixtures. All harness-reachable (`Core/`, root `.vb`). **Session 2 (Sonnet, high):** both render surfaces, the payload comment, settings v69 if ruled, docs, the screen check. Session 2 depends on session 1's fields |
| **Where it will slip** | **(1) The gate.** The block is gated on `KellyPWin > 0`, not on `KellyF > 0` (finding `F-1`). Changing p alone leaves the block ON SCREEN with a negative f* on every run. **(2) The population filter.** Copy `AggregateRange`'s weekday and MinValue guards (`LivePerformanceTracker.vb:780-787`) but NOT its WEAK exclusion (`:799`, the E2a strip rule): the ruling pools all three tiers. **(3) Display parity.** Every Kelly line exists twice: `UI/MainForm_PlaintextSnapshot.vb:239-279` and `UI/MainForm_Render_Cards.vb:1540-1643`. No harness fixture reaches either surface. The `CLAUDE.md` parity hard rule applies. **(4) Fixture literals.** `A10`'s comment claims b = 2.0/1.2; that is the pre-v51 geometry (finding `F-4`). A new literal must be checked against every tracked revision of `settings.json` |
| **Fixtures cannot catch** | The rendered strings (UI is outside the harness). The implementer writes the fixtures, so a misread of section 3.1's filter propagates into its own test. Section 6 names a mutation per fixture; run each |
| **Escalate to Opus, high** | (a) The runtime p on any real eval cache sits above the book's breakeven in any session: the block would size, which contradicts the ruling's premise. Stop and re-raise. (b) Any edit is needed in `Core/ScoringEngine_Calculate_*.vb` or `AnalysisLogger.vb` (scoring and CSV are reserved). (c) The call order "snapshot before card bind before payload" (`UI/MainForm_Analysis.vb:693`, `:715`, `:774`) must move |
| **Reserved classes touched** | **Moves a rendered value** (snapshot, Kelly card, payload advisory `kelly` block, output dump). **A `settings.json` change only if `KO-3` or `KO-5` is ruled (a).** No scoring change, no CSV change. The payload `confidence` field is not touched |

---

## 1. What is ruled, and what this spec adds

- **Ruled (`QD-1` (c), `QD-2` (a)):** one class for Kelly, p = the measured pooled success rate. The spec-back expected: *"the Kelly block is silent on every signal until the book supports an edge"*.
- **This spec finds that the ruled outcome does not follow from the ruled p alone.** Two mechanisms break it: the render gate (`F-1`) and the pairing with a per-row b (`F-2`). Section 4 queues the decisions that restore it.
- **Kelly has zero scoring impact.** Verified: `grep -n -i kelly` over `Core/ScoringEngine_Calculate_Scoring.vb`, `Core/ScoringEngine_Calculate_Verdict.vb` and `Core/ScoringEngine_Helpers.vb` returns nothing (handle `H-3`). `CalcKellySizing` has one call site, `UI/MainForm_PlaintextSnapshot.vb:149`, after `ScoringEngine.Calculate`.

---

## 2. Findings

### F-1 — the Kelly block is not silent at f* ≤ 0 today

- `CalcKellySizing` writes `KellyF` and `KellyPWin` BEFORE its no-edge exit: `Core/ScoringEngine_Kelly.vb:83-88`.
- Both surfaces gate on `KellyPWin`, not on `KellyF`: snapshot `UI/MainForm_PlaintextSnapshot.vb:240`, card `UI/MainForm_Render_Cards.vb:1547`.
- The gate moved from `KellyF > 0` to `KellyPWin > 0` in `9becdbc` (2026-04-20, "Show Kelly block for directional NO TRADE as bias-only").
- **So today the block renders on every run with a non-empty verdict.** A NO TRADE row has `Confidence = "N/A"` (`Core/ScoringEngine_Calculate_Verdict.vb:37`, `:160`, `:171`), which maps to p = 0.45. That includes plain NO TRADE and `[TIE]`, which carry no side.
- **A directional row with f* ≤ 0 renders a false reason:** `Contracts: < 1 contract  (stop too wide for min size)` (`UI/MainForm_PlaintextSnapshot.vb:268`, `UI/MainForm_Render_Cards.vb:1618`). The real reason is "no edge".
- **Consequence for the ruling:** with p = the measured rate (0.40–0.48) the block goes on rendering, on every signal, with a negative f* and $0.00 rows. It is not silent. Decision `KO-4`.
- **Docs that state the opposite** (each is stale against `9becdbc`):

| Doc | Line | Text |
|---|---|---|
| `CLAUDE.md` | "Key design invariants" | "Kelly sizing is display-only — suppressed when KellyF ≤ 0" |
| `docs/DeribitIndicatorProject.md` §8 | Kelly Sizing bullet | "Suppressed when KellyF = 0" |
| `docs/architecture.md` | `:446` | "EST mode only; suppressed when KellyF ≤ 0" |
| `docs/q1d-tier-geometry-spec-back.md` §3 | `QD-2` read | "Today … only WEAK is silent" |
| `Core/SignalEmitter.vb` | `:172-173` | "Fields are 0/false when the display suppresses the Kelly block (no edge)" |

- `docs/UserManual.md` §3 (`:290`) is correct: "the block renders for every real verdict, including `NO TRADE`".

### F-2 — a pooled p with a per-row b sizes the rows where p is most wrong

`K-1` (a) makes b per row. `QD-2` (a) makes p pooled. Paired, a row sizes when its net b exceeds (1 − p) ÷ p. Measured on P0 (handle `H-1`), net b = (T − fee) ÷ (S + fee), fee 3.00 bps:

| Cell | n | Pooled p | Pooled net b | f* at pooled p and b | Row net b needed to size | Rows that size | Their success rate | Their net EV per trade, bps |
|---|---|---|---|---|---|---|---|---|
| NY | 5,417 | 0.404 | 0.767 | −0.373 | > 1.47 | 109 (2.0 %) | 0.202 | −2.35 |
| LONDON | 1,665 | 0.434 | 0.875 | −0.214 | > 1.31 | 119 (7.1 %) | 0.244 | −0.12 |
| ASIA | 1,728 | 0.478 | 0.707 | −0.260 | > 1.09 | 267 (15.5 %) | 0.303 | −1.34 |
| NY H1 / H2 | 2,160 / 3,257 | 0.399 / 0.407 | 0.756 / 0.773 | −0.396 / −0.359 | > 1.51 / > 1.45 | 29 / 80 | 0.414 / 0.138 | +5.44 / −4.49 |
| LONDON H1 / H2 | 754 / 911 | 0.443 / 0.426 | 0.800 / 0.914 | −0.253 / −0.202 | > 1.26 / > 1.35 | 38 / 78 | 0.184 / 0.244 | −2.12 / −0.97 |
| ASIA H1 / H2 | 661 / 1,067 | 0.460 / 0.489 | 0.664 / 0.727 | −0.353 / −0.213 | > 1.17 / > 1.04 | 66 / 203 | 0.273 / 0.315 | −0.71 / −1.39 |

- **The pairing switches Kelly ON for 5.0 % of P0 rows** (441 of 8,810). The ruling expected zero.
- **Those rows hit their target at 0.20–0.30, far below the p the pairing gives them** (0.40–0.48). Far targets hit less often; the pooled p ignores that. §5a rule 4 in `docs/DeribitIndicatorProject.md` names this trap: "when success correlates with distance, an edge in pp can mislead".
- Their net EV per trade is negative in 8 of 9 cells. The exception is NY H1 (n = 29, +5.44 bps). It is less negative than the session's pooled net EV; it is still not an edge.
- **At the pooled p and the pooled b of the same rows, f* is negative in every cell.** That pairing gives the ruled outcome. Decision `K-1`, new option (e).
- ⚠ No confidence intervals: I did not bootstrap this table. The 5.0 % share and the success gap are point estimates.

### F-3 — the runtime book is not the offline population

- The live eval cache (`analysis_eval_cache.csv`) already scores every directional run on its placed levels. The barriers come from `ComputeSideLevels` (`LivePerformanceTracker.vb:1226-1238`). The window is 15 min for 1-min rows and 45 min for 3-min rows (`:1187-1188`). The outcomes are SUCCESS / ADVERSE_HIT / AMBIGUOUS / WINDOW_EXPIRED (`:809`). That is the §5a success rate definition in `docs/DeribitIndicatorProject.md`, so the cache can supply p and b at runtime.
- **But it does not reproduce the offline number.** On the collector's cache as copied back 2026-09-06 (evidence `E-1`), weekday, all three tiers, resolved rows only:

| Session | Book rows | p (book) | p (P0) | Pooled net b (book) | f* at book p and b |
|---|---|---|---|---|---|
| NY | 6,717 | 0.367 | 0.404 | 0.767 | −0.458 |
| LONDON | 2,076 | 0.451 | 0.434 | 0.876 | −0.176 |
| ASIA | 2,332 | 0.455 | 0.478 | 0.719 | −0.302 |
| All sessions | 11,125 | 0.401 | 0.424 | 0.778 | −0.369 |

- NY differs by −3.7 pp. The spans differ (book 2026-07-22 → 2026-09-04; P0 2026-07-06 → 2026-09-11), and the book holds more NY rows over a shorter span. **I did not find the cause.** Candidates, all unverified: the cache's T+3 window start (`LivePerformanceTracker.vb:1206`) against the offline candle walk; the P0 population funnel; duplicate or backfilled rows.
- **f* is negative in every session on either source,** so the ruled outcome does not depend on the gap. The number rendered does. Acceptance item `AC-4` makes the build measure it.

### F-4 — fixture `A10` carries a stale geometry comment

- `verify/ordercheck/Program.vb:1347` reads "b=2.0/1.2=1.6667 → f*=0.44". The POCO defaults are 1.75 / 1.6 since v51 (`Core/Settings/EngineSettings.vb:850`, `:855`). At b = 1.094 and p = 0.65, f* = 0.330 and half-Kelly = 0.165, so the 5 % cap still binds and `A10` still passes.
- Under the ruling, `Confidence = "HIGH"` no longer maps to p = 0.65. `A10` changes meaning (section 6).

### F-5 — no CSV surface; the payload block already zeroes on no edge

- `AnalysisLogger.vb` has no Kelly reference (`grep -c -i kelly` = 0). **Not an `analysis_log.csv` boundary.**
- `CalcKellySizing` resets every output first (`Core/ScoringEngine_Kelly.vb:36-44`). On f* ≤ 0 it exits before `KellyContracts`, `KellyRiskUsd` and `KellyLevCapped` are set (`:88`). The payload copies those three (`Core/SignalEmitter.vb:174-178`). So on no edge the payload already reads 0 / 0 / false. **Keys and `schema_version` do not change.**

### F-6 — `est_prob_*` keys have a second reader

- `tools/ops/q1d_tier_geometry.py:713-715` reads `kelly.est_prob_floor` and `kelly.est_prob_scale` from the tracked `settings.json`. Removing the keys (`KO-5` (a)) raises a `KeyError` there. Any version bump already changes that script's output line 395, so `docs/q1d-tier-geometry-spec-back.md` handle `H-2` will print "DIFFERS" on the version line after v69.

### F-7 — the card height is fixed

- `KELLY_CARD_H = 220` (`UI/MainForm_Layout.vb:232`), sized for header + advisory + six KV rows (`:226-230`). Any new line needs a screen check.

---

## 3. Proposed behaviour (at the reads in section 4)

Each part names the decision that governs it. If the trader rules differently, the part changes with it.

### 3.1 p at runtime (`KO-1`, `KO-2`, `KO-3`)

- **Source:** the live eval cache. A new pure function in `LivePerformanceTracker.vb` (host-agnostic, as the file header states at `:6`) folds the cache into a `KellyBook`: `N`, `Successes`, `P = Successes ÷ N`, `NetPayoff`, `Session`, `SpanStartUtc`, `Sufficient`. Take the entries as a parameter so fixtures can pass their own list; a thin wrapper reads `_evalCache`, as `ComputeWindows` does.
- **Population:** all six directional verdicts (`IsEligibleVerdict`, `:1446-1451`) — STRONG, MEDIUM and WEAK pooled, per `QD-1` (c). Outcome in {SUCCESS, ADVERSE_HIT, AMBIGUOUS, WINDOW_EXPIRED}; PENDING, NO_DATA and every EXCLUDED_* row stay out. Weekday rows only, through the shipped `ForwardWindowJoiner.IsWeekdayRow` with the MinValue guard first. Session = the bucket of the row's UTC hour (`ExecutionResolution.MatchSessionBucket`), matched to the current run's session.
- **Timing:** the snapshot runs before `LivePerformanceTracker.UpdateAsync` (`UI/MainForm_Analysis.vb:693`, `:719`). The book therefore reflects the cache as of the previous run. Keep that order.
- **Floor:** `Sufficient = N ≥ kelly.min_book_rows` (`KO-3`). Below it, Kelly does not size; the block says why (section 3.4).

### 3.2 b (`K-1`)

- **At my read `K-1` (e):** b = the book's pooled net payoff, Σ (targetᵢ − feeᵢ) ÷ Σ (stopᵢ + feeᵢ), over the same rows as p. Distances per row from `EntryPrice`, `FavBar`, `AdvBar`; fee = `scoring.trade_costs.round_trip_fee_pct` × entry, the `BuildNetRRLine` formula.
- The row's own placed R:R stays on screen where it already is: the `ATR ENTRY LEVELS` rows (`AppendPlacedAtrRow`, `UI/MainForm_PlaintextSnapshot.vb:288-299`).
- **At `K-1` (a):** b = this row's placed target and stop for the Kelly side, net of fees, per the proposal §3. Section 2 finding `F-2` states what that sizes.

### 3.3 f*, sizing and the stop

- f* = (b × p − (1 − p)) ÷ b, unchanged. Half-Kelly, the 5 % cap and the leverage cap unchanged (`Core/ScoringEngine_Kelly.vb:90-122`).
- **Kelly side:** the verdict side, or the lean side on `NO TRADE [WEAK LONG]` / `[WEAK SHORT]`. Plain NO TRADE and `[TIE]` have no side: Kelly is not computed (all outputs stay at their reset values).
- **Stop for contract sizing:** the placed stop distance of the Kelly side, |`Entry` − `StopPx`| from `ComputeSideLevels`, per the proposal §3 table. Today it is ATR × 1.6 (`UI/MainForm_PlaintextSnapshot.vb:48`, `:149`).
- **Mode:** `KellyPMode` changes from `EST` to the string `K-4` picks. The card and the snapshot already read it from the field (`UI/MainForm_Render_Cards.vb:1596`, `UI/MainForm_PlaintextSnapshot.vb:255`).
- **New `VerdictResult` fields** (display-only, beside the existing Kelly fields at `Core/ScoringEngine_Types.vb:87-109`): `KellyB`, `KellyBreakevenP`, `KellyBookN`, `KellyBookSession`, `KellyBookSufficient`. They exist so both surfaces render one computed value.

### 3.4 What renders (`KO-4`, `K-4`)

At my read `KO-4` (b). "Computed" means a Kelly side exists and the book is sufficient.

| State | Block | Sizing rows (Half-Kelly, Applied, Risk $, Contracts / Lean, Notional) | Payload `kelly` |
|---|---|---|---|
| No side (plain NO TRADE, `[TIE]`) | Hidden | — | 0 / 0 / false |
| Book below the floor | Shown: header, basis lines, `p(win)` row reading "— (book N rows, needs M)" | Hidden | 0 / 0 / false |
| Computed, f* ≤ 0 (**every signal on today's book**) | Shown with a `[NO EDGE]` tag: basis lines, `p(win)` with n and session, breakeven p, f*, "no size" | Hidden | 0 / 0 / false |
| Computed, f* > 0 | Shown as today, with the new basis lines and rows | Shown | Contracts / risk / lev cap |

- The false reason "(stop too wide for min size)" can then appear only when f* > 0, where it is true.
- The payload `confidence` field is not touched in any state.

---

## 4. Decisions queued (all for the trader)

⚠ **Every decision here is reserved** (rendered value, and `KO-3` / `KO-5` (a) are settings changes). None is ticked. My read leads with the more truthful option, per the `CLAUDE.md` auto-proceed prior.

### KO-1 — where the one-class p comes from at runtime

| Option | What it is | Settings change | Records |
|---|---|---|---|
| **(a)** | The live eval cache, folded per run (section 3.1) | No | The measured rate on this box's book, now and as it grows |
| (b) | A new key, e.g. `kelly.measured_p`, set by hand from a read | **Yes**, v69 | One number, as of the read that set it |
| (c) | A `Public Const` in `Core/ScoringEngine_Kelly.vb` | No | The same, hidden from `settings.json` |
| (d) | Re-use `kelly.est_prob_floor` (0.45) with the scale ignored | No | Not a measured value: this is `QD-2` option (b), which the trader did not pick |

- **Read: (a).** Only (a) can reach "silent until the book supports an edge" without a person re-measuring. (b) and (c) freeze a measured number, which goes stale as the book grows. That is the failure `docs/kelly-est-advisory-reword-spec.md` Trap 2 names for text; a frozen p is the same failure in a value. (a) is self-describing: the rendered n and session tell a reader what the number is.
- **Cost, stated:** (a) couples Kelly to the eval cache, and its p differs from the offline read (`F-3`). The cheaper (b) avoids both, and it records less, so it is the reserved class.

### KO-2 — the book's session scope

| Option | p and b pooled over | Records |
|---|---|---|
| **(a)** | Rows in the current run's session | Session differences: NY p 0.367 against LONDON 0.451 on the 2026-09-06 book (`F-3`); ASIA's fallback payoff 0.78 against NY 1.09 |
| (b) | All sessions | One number for every run |

- **Read: (a).** `QD-2` pools across tiers; it does not say across sessions. Sessions differ in both p and b, and the 2026-09-09 review already corrected ASIA's breakeven once (`docs/kelly-placed-payoff-proposal.md` §1). (b) records less.
- **Fixed by the ruling, not queued:** all three tiers pool (`QD-1` (c)); weekday rows only (the shipped scope rule). **Also my read, not queued separately:** no rolling window — the whole cache, with its start date rendered. A rolling window adds a knob and records no more.

### KO-3 — the small-book floor

| Option | Rule | Settings change |
|---|---|---|
| **(a)** | New key `kelly.min_book_rows`, value 400 | **Yes**, v69 (the `kelly.*` prefix is already fenced off the auto-tweaker, `tools/AutoTweaker/SettingsDiffApplier.vb:67`) |
| (b) | The same value as a `Public Const` | No |
| (c) | No floor; render n and let the reader judge | No |
| (d) | No floor; use the lower bound of a 95 % interval on p | No, but the rendered p is no longer the measured rate |

- **Read: (a).** A fresh cache (a new box, a restored workstation) holds few rows, and a point estimate on 20 rows can size hard. The trader profile (`docs/trader-profile.md` §6) asks for conservative false-positive tolerance. `CLAUDE.md` puts every threshold in `settings.json`, and the version bump records the edge honestly.
- **The value:** at n = 400 the standard error of p is at most 0.025, so the 95 % interval is about ±5 pp. That is the width at which a 5–10 pp gap to breakeven reads cleanly. A value, not a derivation from a target; the trader may prefer another.
- (d) is the most conservative, but it changes the ruled p, so it would need its own ruling.

### KO-4 — what "silent" renders

| Option | At f* ≤ 0 | Records |
|---|---|---|
| (a) | Hide the block and the card (gate on `KellyF > 0`, the pre-`9becdbc` rule) | Nothing: "no edge" and "not computed" look the same |
| **(b)** | A compact `[NO EDGE]` block: basis, measured p with n and session, breakeven p, f*; sizing rows hidden (section 3.4) | Why there is no size, on screen, every run |
| (c) | Today's full block with $0 rows; only fix the false contracts reason | Everything, with four rows of zeros |

- **Read: (b).** The trader profile (`docs/trader-profile.md` §6) asks that a weak directional bias "must still be rendered, to help form a future opinion". (a) removes that and cannot tell "no edge" from "not computed" — the `WD-SEMANTICS` lesson that a reading of 0 is the tripwire. (c) keeps rows that say nothing.
- ⚠ **(b) reads "silent" as "silent on sizing", not "invisible".** If the trader meant invisible, the answer is (a).

### KO-5 — the retired `est_prob_*` keys

| Option | What happens | Settings change |
|---|---|---|
| **(a)** | Remove `kelly.est_prob_floor` and `kelly.est_prob_scale` from `settings.json` and the POCO (`Core/Settings/EngineSettings.vb:773`, `:775`), with a `change_log` entry. Patch `tools/ops/q1d_tier_geometry.py:713-715` to carry the v68 values as a documented read of that version | **Yes**, v69 |
| (b) | Keep both keys; say "unused since v69" in `change_log` | No key change |

- **Read: (a).** A dead key that still reads 0.45 / 0.20 tells a future seat the tier map is live. Docs rot; code and config survive. The JSON-to-POCO drift guard (`A62a`–`A62g`) stays green if both sides go in one commit.

### K-1 — the payoff basis (verbatim from `docs/kelly-placed-payoff-proposal.md` §4, plus two new options)

| Option | b | Records |
|---|---|---|
| (a) Placed levels, net of fees | Per row, dynamic | The actual trade and its cost |
| (b) Placed levels, gross | Per row, dynamic | The actual trade, cost ignored |
| (c) Per-session fallback multipliers, net of fees | Static per session (NY 1.094, LONDON 1.25, ASIA 0.781 gross) | The median trade only |
| (d) Keep the global 1.75 / 1.6 | Static global | Neither |
| **(e) NEW: the book's pooled placed net b** (same rows as p) | Per session, from the book | The placed trades and their cost, consistent with p |
| (f) NEW: per-row placed net b, with p shifted per row: p_row = row gross breakeven + the book's pooled gross edge | Per row | The row's geometry and the book's edge |

- The proposal's read was (a). It predates the one-class ruling.
- **Read now: (e).** (a) paired with the ruled p sizes 5 % of rows, and those rows hit their target at 0.20–0.30 against the 0.40–0.48 the pairing assumes (`F-2`). That is a rendered edge the book contradicts. (e) uses only measured values, gives f* < 0 in every session and half, and the row's own R:R stays on the `ATR ENTRY LEVELS` rows.
- **Why not (f), which records more:** it rests on an assumption no read has tested — that the gross edge in pp is constant across geometry. The q1d read's within-target-type cut (`docs/q1d-tier-geometry-read-2026-09-24.md` §3) is the nearest evidence, and it is single-half. (f) sizes 0 rows on P0 in all six session × half cells, so it would be silent today too. It needs a calibration read (p_row buckets against realised success) before it can be trusted. That is a mechanism argument (unvalidated), not a cost argument.

### K-2 — does this need a settings key for the basis?

- **Options (verbatim):** (a) no key: the fee and levels already live in `settings.json`; (b) a `kelly.payoff_basis` switch for rollback.
- **Read: (a), unchanged.** A switch records nothing more; it is a rollback path for a display-only change that one revert undoes. A hot-reloaded switch would also land a version edge mid-`InstanceId`.

### K-3 — sequencing

- **Options (verbatim):** (a) ship `K-1` alone now; (b) ship it together with `F-4a` (one flat, measured win probability) after `Q-1` option (d) measures payoff and success rate by tier; (c) wait.
- **Read: (b).** Its gate is discharged: `Q-1` (d) is done (`5c91cca`) and `F-4a` is ruled as `QD-1` (c). The trader's 2026-09-24 instruction to settle both in one pass is (b) in substance. Tick it to record that.

### K-4 — the advisory wording (both surfaces; the trader signs off exact strings)

The two current lines become false: `UI/MainForm_PlaintextSnapshot.vb:251-252` and `UI/MainForm_Render_Cards.vb:1588-1589`. Candidates, written for `K-1` (e); none carries a measured number (the rendered numbers come from fields):

| Line | Today | Candidate |
|---|---|---|
| Basis | `Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets.` | `Advisory — b is the book's pooled placed R:R, net of the round-trip fee.` |
| p(win) | `p(win) is ASSUMED from the confidence tier — the calibration read did not separate the tiers.` | `p(win) is the book's measured success rate, all tiers pooled.` |
| Bias | `Treat as directional bias indicator only.` | Unchanged |
| Net R:R | `BuildNetRRLine` on ATR multiples (`UI/MainForm_Render_Cards.vb:39-61`) | Same composer, fed the book's pooled distances; label e.g. `Net R:R (book):` |
| Mode tag | `p(win) [EST]:` | `p(win) [BOOK]:` (my candidate; `CAL` was reserved for a per-tier calibration that is now ruled out) |
| No-edge tag | — | Header `KELLY SIZING  [NO EDGE]`; last row `f*: -NN.NN %  — no size` |
| Book row | — | `Book: N rows, SESSION, since YYYY-MM-DD` and `Breakeven p(win): NN.N %` |

- **At `K-1` (a)** the basis line would read instead: `Advisory — b is this signal's placed target and stop, net of the round-trip fee.`
- ⚠ **`KO-4` (b) must fit the 220 px card** (`F-7`). Count lines against today's worst case (header + four advisory lines + six rows) and check on screen.

### KO decision summary

| ID | Question | My read | Settings change at my read |
|---|---|---|---|
| `KO-1` | p source | (a) runtime eval cache | No |
| `KO-2` | Session scope | (a) current session | No |
| `KO-3` | Small-book floor | (a) `kelly.min_book_rows` 400 | Yes |
| `KO-4` | What "silent" renders | (b) compact `[NO EDGE]` block | No |
| `KO-5` | Retired `est_prob_*` keys | (a) remove | Yes |
| `K-1` | b basis | (e) book-pooled placed net b | No |
| `K-2` | Basis switch key | (a) none | No |
| `K-3` | Sequencing | (b) one pass (already directed) | No |
| `K-4` | Wording | Candidates above; trader signs | No |

### ✅ RULINGS — 2026-09-25 (UTC), trader

| ID | Ruled | Note |
|---|---|---|
| `KO-1` | (a) runtime eval cache | as read |
| `KO-2` | (a) current session | as read |
| `KO-3` | (a) `kelly.min_book_rows` = 400 | settings v68 → v69 |
| `KO-4` | **(b) compact `[NO EDGE]` block** | trader confirmed (b) explicitly |
| `KO-5` | (a) remove `est_prob_*` | settings v69, same bump |
| **`K-1`** | ⭐ **(g), NEW — measured p and b per geometry bucket** | Trader: *"what is the most truthful option? Pick that."* Defined below |
| `K-2` | (a) no basis switch | as read |
| `K-3` | (b) one pass with the one-class change | records the 2026-09-24 pairing |
| `K-4` | the candidate strings above, **adjusted for (g)** | as read; the basis line and the book row must name the bucket (see below) |

#### `K-1` (g) — why it is the most truthful option, and its definition

**Why.** Every other option either pairs numbers that do not describe the same trades, or describes a trade other than this one:

- (a) pairs a true per-row b with a pooled p that is wrong for far targets (`F-2`: those rows succeed at 0.20–0.30, not 0.40–0.48). It renders an edge the book contradicts.
- (e) is internally consistent and fully measured, but it describes the session's AVERAGE trade, not this row's geometry.
- (f) gives a per-row p from an untested assumption (a constant edge in pp across geometry). A modelled number rendered as if measured.
- **(g) conditions on the variable that `docs/DeribitIndicatorProject.md` §5a rule 4 names — success correlates with distance — and uses only measured values for rows like this one.** It records more than (e), and it degrades to (e), never to a guess.

**Definition** (the orchestrator's; the build seat must not re-open it, but may raise a mechanism problem):

1. **Bucket key:** the row's placed net payoff b_row = (T − fee) ÷ (S + fee) for the Kelly side, from the same fields as the book rows (spec section 3.2).
2. **Buckets:** TERCILES of b_row over the current session's book (`KO-2` (a)), recomputed from the book each run. No settings key and no fixed edges: equal row counts per bucket, self-updating as the book grows. The live row falls in the tercile whose b range holds its b_row (clamped to the end buckets).
3. **Values:** p = the bucket's measured success rate; b = the bucket's pooled net payoff Σ(T − fee) ÷ Σ(S + fee). Same outcome classes and population rules as section 3.1.
4. **Floor and fallback:** if the bucket holds fewer than `kelly.min_book_rows` rows, use the session-pooled p and b (option (e)) and render that the fallback applied. If the session book itself is below the floor, the "book below the floor" state in section 3.4 applies.
5. **Rendering (`K-4` adjusted):** the basis line names the bucket, e.g. `Advisory — p and b are measured on the book's rows with a similar placed R:R (bucket k of 3, b ∈ [lo, hi]), net of the round-trip fee.`; the book row adds the bucket's n. The exact strings follow `K-4` as ruled; the build seat keeps them measured-number-free and reads every number from fields.
6. **Fixtures (additions to section 6):** a book whose far-target bucket has a LOWER p than the near bucket must give the far-target row the lower p (the `F-2` shape); a bucket below the floor must fall back to (e) and say so; a tercile boundary row must land deterministically.

⚠ **Not measured before this ruling:** the per-bucket p and b on the current book. Terciles give NY ~2,200, LONDON ~690, ASIA ~780 rows per bucket on the 2026-09-06 cache counts (`F-3`), all above the 400 floor. `F-2` shows the far-target rows lose, so f* is expected ≤ 0 in every bucket today; **acceptance item `AC-4` must now measure f* per session AND per bucket**, and the Opus escalation trigger in section 0 (f* > 0 anywhere) applies per bucket.

- **At my reads: one settings bump, v68 → v69, carrying `KO-3` and `KO-5`.** If both go (b), settings stay untouched.

---

## 5. Every rendered line that moves

| Surface | Site (at `b99fce8`) | Change |
|---|---|---|
| Snapshot | `UI/MainForm_PlaintextSnapshot.vb:149` | New call signature: book, Kelly-side levels, entry, cfg |
| Snapshot | `:239-240` | Gate: show when a Kelly side exists; sizing rows only when f* > 0 (`KO-4`) |
| Snapshot | `:246-250` | Header gains `[NO EDGE]` |
| Snapshot | `:251-252` | Basis and p(win) lines (`K-4`) |
| Snapshot | `:254` | Net R:R line moves to the Kelly basis |
| Snapshot | `:255` | Mode tag `[EST]` → the `K-4` string; value = the book p |
| Snapshot | New | Book row, breakeven row |
| Snapshot | `:256-278` | f* row always; Half-Kelly, Applied, Risk $, Contracts / Lean and Notional only when f* > 0 |
| Card | `UI/MainForm_Render_Cards.vb:1547-1552` | Same gate |
| Card | `:1569-1571` | Same header tag |
| Card | `:1587-1591` | Same advisory lines |
| Card | `:1596-1599` | Same KV rows plus the two new ones |
| Card | `:1603-1639` | Same f* > 0 condition on the sizing rows |
| Both | `BuildNetRRLine`, `:39-61` | Basis input; its doc comment ("The ATR-multiple geometry is the Kelly block's own basis") |
| Payload | `Core/SignalEmitter.vb:172-178` | Keys unchanged; values 0 / 0 / false on every no-edge run; fix the comment |
| Output dump | Via the snapshot | Same text as the snapshot |
| Layout | `UI/MainForm_Layout.vb:226-232`, `:867` | Comments on the rows and the hide rule; `KELLY_CARD_H` only if the screen check clips |

- **Also changes on screen:** plain NO TRADE and `[TIE]` rows lose the block they show today (p 45.0 %, f* −5.29 %).
- **Commit rule:** both surfaces in one commit; the message names both `file:line` sets (`CLAUDE.md` parity hard rule). A shared composer for the Kelly rows, like `BuildNetRRLine`, makes the pair identical by construction; the implementer may choose it.

---

## 6. Fixtures

Fixture IDs below are a plan. The highest family in the tree at `b99fce8` is `A89`; take the next free family at build start and record it.

| Fixture | Status | Asserts | Class (fixture-literal rule) | Mutation that must turn it red |
|---|---|---|---|---|
| `A10` | **Changes meaning** | Inverse-contract sizing and the leverage cap, given an explicit book p and b and a placed stop distance | MECHANISM: literal p and b. Check each against every tracked `settings.json` revision (`git log -p -- settings.json`); 1.094 and 1.6667 are ever-shipped ratios. Fix the stale comment (`F-4`) | Drop the leverage cap branch |
| `A22a` | Unchanged | Copies injected `Kelly*` fields to the payload (`verify/ordercheck/Program.vb:2861`, `:2957-2962`) | — | — |
| New: book filter | New | A mixed list — weekend, MinValue, NO TRADE, PENDING, NO_DATA, EXCLUDED_*, other-session and WEAK rows — gives N and Successes by hand count; WEAK counts | SHIPPED BEHAVIOUR for the scope rules | Copy the E2a WEAK exclusion; drop the weekday guard |
| New: book outcome | New | AMBIGUOUS and WINDOW_EXPIRED count as failures | SHIPPED BEHAVIOUR | Count WINDOW_EXPIRED as neither |
| New: book payoff | New | Pooled net b = Σ (T − fee) ÷ Σ (S + fee), fee from `cfg.Scoring.TradeCosts.RoundTripFeePct` | SHIPPED BEHAVIOUR: derive the fee from cfg | Average per-row ratios instead of Σ ÷ Σ |
| New: floor | New (if `KO-3` (a) or (b)) | N one below the floor → not sufficient; at the floor → sufficient. Floor read from cfg (or the `Public Const`) | SHIPPED BEHAVIOUR | Off-by-one `>` for `≥` |
| New: one class | New | Same book and levels → identical `Kelly*` for STRONG, MEDIUM and WEAK | SHIPPED BEHAVIOUR | Restore the tier `Select Case` (`Core/ScoringEngine_Kelly.vb:65-69`) — the fixture must go red |
| New: no edge | New | Book p below breakeven → `KellyF` ≤ 0, `KellyContracts` 0, `KellyRiskUsd` 0, `KellyLevCapped` False; `BuildOk` payload `kelly` = 0 / 0 / false; `confidence` = the verdict's | SHIPPED BEHAVIOUR | Move the f* exit below the sizing block |
| New: edge | New | Book p above breakeven → sizes, contracts from the placed stop distance | MECHANISM (a synthetic book) | Use ATR × 1.6 for the stop |
| New: side | New | `NO TRADE [WEAK SHORT]` uses the short levels; plain NO TRADE and `[TIE]` → not computed | SHIPPED BEHAVIOUR | Default no-side rows to long |

- **Rendered strings have no fixture** (the harness links `Core/`, `analysis/` and root `.vb`, not `UI/`). They are covered by `AC-5` and `AC-6`.
- Do not add a fixture that pins a literal advisory string (`docs/kelly-est-advisory-reword-spec.md` `AC-2` gives the reason).

---

## 7. Dataset boundary

| Surface | Boundary? |
|---|---|
| `analysis_log.csv` | **No.** No Kelly column (`F-5`) |
| `analysis_eval_cache.csv` | **No.** Read only; nothing written differs |
| Bridge payload `kelly` block | **A value edge, not a schema change.** STRONG and MEDIUM rows read contracts > 0 before the deploy and 0 after, while the book shows no edge. Contract D5 in `docs/signal-bridge-v1-proposal.md` (`:121`) says the block is advisory only, never sizing in v1 |
| Bridge payload `engine.settings_version` | 68 → 69 if the bump happens |
| Output dump | Text change only |

- **Carried, not re-checked:** that the order app reads only `direction` + `confidence` (`docs/kelly-est-honesty-decision-2026-08-02.md` §5). I did not open the order-app repo.

---

## 8. `docs/DeribitIndicatorProject.md` §15 row plan and deploy note

**§15 row (for the build commit; one row, under about 1,000 B):**

> **Kelly as one class, b from the book's placed levels net of fees** (RENDERED-VALUE CHANGE on the snapshot, the Kelly card and the payload's advisory `kelly` block; NOT an `analysis_log.csv` boundary; no scoring impact; settings v69 [`kelly.min_book_rows` added, `kelly.est_prob_*` removed] — or settings-untouched if `KO-3` and `KO-5` go (b)) | date | [`kelly-one-class-placed-payoff-spec.md`](kelly-one-class-placed-payoff-spec.md). p = the eval cache's weekday success rate, all tiers pooled, current session; b = the same rows' pooled placed net payoff; the tier map is gone. On today's book f* < 0 in every session: the block shows `[NO EDGE]` and no size; payload `kelly` reads 0 / 0 / false. Plain NO TRADE and `[TIE]` lose the block. Fixtures: the family from section 6.

- If v69 lands: bump `version`, add a `change_log` entry (newest first), and add the keys to the POCO in the same commit.
- **Doc corrections that ride with the build:** `docs/DeribitIndicatorProject.md` §7 and §8 (the Kelly bullets), `docs/architecture.md:446`, `docs/UserManual.md` §3, the `Core/SignalEmitter.vb:172-173` comment. **`CLAUDE.md`'s "suppressed when KellyF ≤ 0" invariant is the orchestrator's or the trader's edit, not the implementer's.**

**Deploy note (⛔ reserved to the trader):**

- The collector box runs the same executable, so a deploy moves its output dump and its payload `kelly` values.
- **If v69 lands, deploy the executable and `settings.json` together at one restart.** A hot-reloaded settings edit on the running box puts the version edge inside one `InstanceId`.
- Bundle it with the next deploy the trader schedules, per `docs/aws-collector-deploy-checklist.md`. No separate deploy is needed for a display-only change.

---

## 9. Handles and evidence

All commands run from the repo root in Git Bash.

### H-1 — the pooled-p × row-b table (finding `F-2`)

Prerequisite: `backtest_data/swing-fallback-read/diagnosis-rows.csv` at MD5 `e7e93f2fa66e60a3119ce09947b2e063`, regenerated per `docs/medium-tier-diagnosis-read-2026-09-17.md` §6 if absent.

```
python - <<'EOF'
import csv, json
FEE = 2.0 * json.load(open("settings.json", encoding="utf-8-sig"))["scoring"]["trade_costs"]["maker_fee_bps"]
rows = list(csv.DictReader(open("backtest_data/swing-fallback-read/diagnosis-rows.csv", encoding="utf-8")))
for half in ("ALL", "H1", "H2"):
    for sess in ("NY", "LONDON", "ASIA"):
        c = [r for r in rows if r["Session"] == sess and half in ("ALL", r["Half"])]
        n = len(c); T = [float(r["TBps"]) for r in c]; S = [float(r["SBps"]) for r in c]
        w = [r["MainOutcome"] == "1" for r in c]; ev = [float(r["MainNetEv"]) for r in c]
        p = sum(w) / n; bp = (sum(T) - n * FEE) / (sum(S) + n * FEE); thr = (1 - p) / p
        k = [i for i in range(n) if (T[i] - FEE) / (S[i] + FEE) > thr]
        print("| %s %s | %d | %.3f | %.3f | %+.3f | > %.2f | %d (%.1f %%) | %.3f | %+.2f |" % (
            sess, half, n, p, bp, (bp * p - (1 - p)) / bp, thr, len(k), 100.0 * len(k) / n,
            sum(w[i] for i in k) / len(k), sum(ev[i] for i in k) / len(k)))
EOF
```

```
| NY ALL | 5417 | 0.404 | 0.767 | -0.373 | > 1.47 | 109 (2.0 %) | 0.202 | -2.35 |
| LONDON ALL | 1665 | 0.434 | 0.875 | -0.214 | > 1.31 | 119 (7.1 %) | 0.244 | -0.12 |
| ASIA ALL | 1728 | 0.478 | 0.707 | -0.260 | > 1.09 | 267 (15.5 %) | 0.303 | -1.34 |
| NY H1 | 2160 | 0.399 | 0.756 | -0.396 | > 1.51 | 29 (1.3 %) | 0.414 | +5.44 |
| LONDON H1 | 754 | 0.443 | 0.800 | -0.253 | > 1.26 | 38 (5.0 %) | 0.184 | -2.12 |
| ASIA H1 | 661 | 0.460 | 0.664 | -0.353 | > 1.17 | 66 (10.0 %) | 0.273 | -0.71 |
| NY H2 | 3257 | 0.407 | 0.773 | -0.359 | > 1.45 | 80 (2.5 %) | 0.138 | -4.49 |
| LONDON H2 | 911 | 0.426 | 0.914 | -0.202 | > 1.35 | 78 (8.6 %) | 0.244 | -0.97 |
| ASIA H2 | 1067 | 0.489 | 0.727 | -0.213 | > 1.04 | 203 (19.0 %) | 0.315 | -1.39 |
```

- Columns: cell, n, pooled p, pooled net b, f* at pooled p and b, row net b needed to size, rows that size, their success rate, their net EV per trade (bps).
- `MainOutcome` 1 = target hit (the coding in `tools/ops/q1d_tier_geometry.py:205`).

### H-2 — the render gate (finding `F-1`)

```
grep -n "KellyPWin > 0\|KellyPWin <= 0" UI/MainForm_PlaintextSnapshot.vb UI/MainForm_Render_Cards.vb; sed -n '83,88p' Core/ScoringEngine_Kelly.vb
```

```
UI/MainForm_PlaintextSnapshot.vb:240:        If v.KellyPWin > 0 Then
UI/MainForm_Render_Cards.vb:1532:    ' Hidden entirely when v.KellyPWin <= 0. Otherwise renders header (with
UI/MainForm_Render_Cards.vb:1547:        If v.KellyPWin <= 0 Then
        v.KellyF = fStar
        v.KellyPWin = p
        v.KellyPMode = "EST"

        ' No edge -> silent block
        If fStar <= 0 Then Exit Sub
```

### H-3 — Kelly has no scoring or CSV reference

```
grep -n -i "kelly" Core/ScoringEngine_Calculate_Scoring.vb Core/ScoringEngine_Calculate_Verdict.vb Core/ScoringEngine_Helpers.vb; echo "scoring hits: $?"; grep -c -i "kelly" AnalysisLogger.vb
```

```
scoring hits: 1
0
```

- `grep` exit status 1 = no match in the three scoring files.

### E-1 — the runtime book on the collector's 2026-09-06 cache (finding `F-3`)

- Input: `AWS-copybacks/aws-copyback-2026-09-06/aws_fetch/20260906-155831/analysis_eval_cache.csv`, MD5 `4307e25957bb782dd4b21d904c09f606`. `AWS-copybacks/` is git-ignored, so a reader on another machine cannot re-run it. Session buckets from `settings.json` `session_volume.sessions` (ASIA 0–7, LONDON 8–12, NY 13–23 UTC, end hour inclusive, as `ExecutionResolution.MatchSessionBucket` applies them at `Core/ExecutionResolution.vb:44`).
- Output (weekday, six directional verdicts, resolved rows only): NY n 6,717, p 0.367, pooled net b 0.767, f* −0.458; LONDON n 2,076, p 0.451, b 0.876, f* −0.176; ASIA n 2,332, p 0.455, b 0.719, f* −0.302; all n 11,125, p 0.401, b 0.778, f* −0.369. Span 2026-07-22 16:25 → 2026-09-04 21:59 UTC. Directional outcome counts: ADVERSE_HIT 5,918, SUCCESS 4,926, WINDOW_EXPIRED 1,679, NO_DATA 737 (AMBIGUOUS 0).

---

## 10. Acceptance criteria (for the build)

| # | Criterion |
|---|---|
| **AC-1** | Solution `-t:Rebuild` Release: 0 errors, 0 warnings |
| **AC-2** | Harness ALL PASS; the count rises by exactly the new fixtures. Paste each section 6 mutation and its red result |
| **AC-3** | `verify-gate.ps1` GATE PASSED, run after the commit (the pre-commit run checks parity vacuously) |
| **AC-4** | Run the book function on the newest cache the build seat can read. Paste N, p, b, f* per session beside the P0 values in `F-3`. **If any session's f* > 0, stop (escalation (a) in section 0)** |
| **AC-5** | Both surfaces seen on screen in two states: a directional run (`[NO EDGE]`) and a lean NO TRADE run. The card must not clip at 220 px. Delete screenshots afterwards |
| **AC-6** | Paste the snapshot's Kelly block and the card's rows side by side; the wording matches the signed-off `K-4` strings on both |
| **AC-7** | `settings.json`: v69 with the ruled keys, or `git diff --stat` does not list it |
| **AC-8** | `git diff --stat` lists no `Core/ScoringEngine_Calculate_*.vb` and no `AnalysisLogger.vb` |

---

## 11. What I verified, and what I did not

### Verified, and how

| Claim | How |
|---|---|
| The render gate and the write order (`F-1`) | Read `Core/ScoringEngine_Kelly.vb`, `UI/MainForm_PlaintextSnapshot.vb:239-279`, `UI/MainForm_Render_Cards.vb:1540-1643`; handle `H-2`; `git show 9becdbc` |
| NO TRADE maps to p 0.45 | Read `Core/ScoringEngine_Calculate_Verdict.vb` `Confidence = "N/A"` sites and `Core/ScoringEngine_Kelly.vb:65-69` |
| Zero scoring impact, no CSV column | Handle `H-3` |
| The payload block and its zeroing | Read `Core/SignalEmitter.vb:172-178` and `Core/ScoringEngine_Kelly.vb:36-44`, `:88` |
| The eval cache uses placed levels and the 15 / 45 min windows | Read `LivePerformanceTracker.vb:1187-1238`, `:1446-1451` |
| The pooled-p × row-b table | Handle `H-1` on the P0 export (MD5 matches the one the q1d output names) |
| `A10`'s stale comment | Read `verify/ordercheck/Program.vb:1345-1363` against `Core/Settings/EngineSettings.vb:850`, `:855` |
| The q1d instrument reads `est_prob_*` | Read `tools/ops/q1d_tier_geometry.py:95`, `:713-715` |
| The tweaker fences `kelly.*` | Read `tools/AutoTweaker/SettingsDiffApplier.vb:67` |

### Not verified

- **Why the runtime book's NY p (0.367) differs from P0's (0.404)** (`F-3`). Candidates listed there; none tested.
- **That the eval cache's T+3 window start matches the offline candle walk.** Not compared.
- **Any confidence interval on `F-2`.** Point estimates only.
- **Option (f) of `K-1` beyond the zero-rows count.** No calibration read.
- **Thread safety of reading `_evalCache` from the snapshot.** I assumed the `ComputeWindows` precedent (called from `UI/MainForm_Layout.vb:1664`) is safe; I did not check how `UpdateAsync` and the UI thread share the list.
- **Carried without checking:** the order app reads only `direction` + `confidence`; the proposal's section 2 percentiles; the q1d read's per-tier values.
