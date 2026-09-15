# Brief — why does the MEDIUM tier underperform? Bug hunt, then diagnosis

**Written:** 2026-09-16 (UTC) by the orchestrator seat, at max effort (trader-directed). **For:** an analysis seat, two sessions. **Asked by:** the trader. **Replaces, for now:** the Part B forward test, which is parked in `docs/trader-tick-queue.md` §2 until the tiers are fixed.

**Evidence that started this:**
- [`tier-order-stability-read-2026-09-15.md`](tier-order-stability-read-2026-09-15.md): MEDIUM is the lowest or joint-lowest tier in every readable NY and ASIA row.
- The Kelly calibration reads ([`f1-tier-ladder-read-2026-08-01.md`](f1-tier-ladder-read-2026-08-01.md), [`kelly-w6-4-batch-summary.md`](kelly-w6-4-batch-summary.md)): NY MEDIUM's success rate sits below WEAK's in both.

**Vocabulary:** `docs/DeribitIndicatorProject.md` §5a (success rate · gross/net breakeven rate · gross/net edge · net EV per trade). **Use those words exactly.**

---

## 0. Model, effort, escalation — read first

| Item | Detail |
|---|---|
| **Model / effort** | **Opus, high, both sessions.** Session 1 escalates to **max** if the re-score reconstruction (§2.1) leaves more than 1 % of rows mismatched for reasons it cannot name. Why this tier: a sign bug and a miscalibrated gate produce the SAME outcome pattern as a genuinely weak tier, so telling them apart takes code-level reasoning, not table-reading. And the analysis has dozens of cuts, so false discoveries are the default without the §4 discipline |
| **Session split** | **Session 1: the bug hunt** (§2). **Session 2: the diagnosis** (§3), only after Session 1 reports. Sequenced by dependency: a live scoring bug invalidates every tier statistic, so it must be ruled in or out first |
| **Where it will slip** | (1) **Rebuilding `IndicatorResults` from a CSV row and silently defaulting a field the CSV does not log.** A mismatch then reads as a bug, or a real bug is hidden. Record per-field provenance. (2) **Re-scoring old rows with today's `settings.json`.** Use the settings in force for each row's era, from git history. (3) **A mirror test that flips inputs incompletely** (funding sign, OI change sign, book bid/ask, trade direction, divergence labels, EMA alignment, VWAP side, swing levels). That creates false asymmetry, or misses a real one. (4) **Calling an anti-predictive vote a bug.** A vote can be correct code and still point the wrong way on outcomes (mean reversion). A bug needs the code audit to agree. (5) **Declaring a cause from one half of the data** (§4) |
| **Stop immediately and report** | If Session 1 finds a vote or gate whose code does the opposite of its documented intent, stop there. **It is a live scoring defect affecting verdicts now.** Report it with the file, the line, a failing fixture and the affected share of rows. **Do not fix it.** Any engine `.vb` change or `settings.json` change is reserved to the trader |
| **Scope** | Trader-directed scoped seat. Skip the full `CLAUDE.md` session-start reads. Read `DeribitIndicatorProject.md` §5a and §7 (the `ScoringEngine` behaviours), `architecture.md` (the Data Flow pipeline), the two reads above, and this brief. Run `date -u` first |

## 1. Facts already established (orchestrator, read in the tree 2026-09-16 UTC)

| Fact | Where |
|---|---|
| **The tier walk uses EFFECTIVE scores.** Thresholds are `Math.Ceiling(regimeMax × pct)` with STRONG 0.70, MEDIUM 0.53, WEAK 0.35 (`settings.json` v68) | `Core/ScoringEngine_Calculate_Verdict.vb` Step 5 (~lines 148–176) |
| **Regime max at v68:** TRENDING 20, RANGE_BOUND 19, TRANSITIONAL 15. So MEDIUM is effective score 11–13 in TRENDING and RANGE_BOUND, and 8–10 in TRANSITIONAL | `ScoringEngine_Helpers.RegimeMaxScore`, `Threshold` |
| ⛔ **A downgrade gate exists (trader's recollection confirmed).** In TRANSITIONAL, Step 4 sets `effective = Max(raw − adxPenalty, TierFloor(raw))`. The penalty is 2 below ADX 22.5 and 1 from 22.5 to 25. `TierFloor` is 9 for raw ≥ 12, 6 for raw ≥ 9, 3 for raw ≥ 6, else 0. **So a raw 8–9 MEDIUM becomes 7, which is WEAK.** The floors are absolute numbers, not regime-scaled | `_Verdict.vb` ~lines 64–78; `Helpers.TierFloor`; `settings.json` `scoring.tier_floor`, `regime_gates` |
| **Hard NO-TRADE gates** (not downgrades): the regime veto (Step 4 early returns), the MTF veto (Step 4b), and the BELOW_MIN_MOVE gate (Step 5c, which flips the verdict to NO TRADE after the tier is set) | `_Verdict.vb` ~lines 36, 52, 126, 313–319 |
| **Score mutations inside Steps 2–3b:** 33 vote and mutation sites. Penalties clamp at 0 (`Math.Max(0, …)`) and bonuses cap at regime max (`Math.Min(…, regimeMax)`). They include: a BBW squeeze penalty on BOTH sides (−`bbw_squeeze_penalty`); Pass 2c alignment bonus and conflict penalty; the CVD divergence penalty; MicroCVD decel and stall penalties; the aggressor-velocity upgrade and contra penalty; liquidation penalties; the spread WIDE penalty; the OI × CVD conflict penalty; the funding modifiers (Step 3 and 3b) | `Core/ScoringEngine_Calculate_Scoring.vb` |
| ⚠ **One site to check explicitly, not a finding:** `_Scoring.vb` ~lines 380–381 apply the MicroCVD decel penalty to the SHORT side on `BULL_DECEL` and to the LONG side on `BEAR_DECEL`. Whether that is right depends on what `CalcMicroCVD` means by those labels. **Confirm the label semantics before concluding anything** | `_Scoring.vb`, `Core/Indicators_OrderFlow.vb` `CalcMicroCVD` |
| **Per-vote attribution exists in the engine but is not logged.** `VerdictResult.SignalBreakdown` items carry each emission's signed contribution, and `CheckLedger` asserts the items sum to the raw score through Step 3b. ⚠ The ledger cannot catch a sign bug that flips a vote AND its breakdown item together | `Core/ScoringEngine_Types.vb`; the Spec C ledger guard |
| **The CSV logs** `LongScore`, `ShortScore` (raw, through Step 3b), `EffectiveLongScore`, `EffectiveShortScore`, `MaxScore`, `RegimePenalty`, `Regime`, `ADX`, `VerdictContext`, and nearly every indicator's value and signal state. It does **not** log the breakdown, the VPFR POC or signal, or the MTF gate details | `AnalysisLogger.vb` `Header` |
| **Scoring-affecting settings eras inside the book:** v65 (2026-08-02, ASIA aggressor-velocity arming) and v66 (2026-08-10 18:35 UTC deploy, OBV `trend_gate` 18 → 23). v67 and v68 did not change scoring | `DeribitIndicatorProject.md` §15 |

## 2. Session 1 — the bug hunt

### 2.1 Re-score reconstruction (does the logged score equal what the code computes?)

- For every trading-week directional row, rebuild `IndicatorResults` from the CSV columns and run `ScoringEngine.Calculate` under **that row's era settings** (`git show <commit>:settings.json` for the version in force). Compare against the logged `LongScore`, `ShortScore`, `EffectiveLongScore`, `EffectiveShortScore`, `RegimePenalty` and `Verdict`.
- **Report:** the match rate per field. For every mismatch, a class: *unlogged input* (name the field) · *settings era* · *unexplained*. Keep the rebuilt `SignalBreakdown` for every row; Session 2 needs per-vote attribution.
- A host-agnostic console instrument under `tools/ops/` or `analysis/`, re-runnable, output pasted.

### 2.2 Mirror-symmetry property test (does a mirrored market give a mirrored verdict?)

- Harness fixtures in `verify/ordercheck/Program.vb`. Build a directional `IndicatorResults` state and its exact **mirror**: every directional field flipped, every symmetric field kept. **Assert:** mirrored `LongScore` equals original `ShortScore` (and the reverse), the effective scores swap, and the verdict tier mirrors (STRONG LONG ↔ STRONG SHORT, and so on).
- **Cover each vote site on its own** (one indicator's state varied, the rest neutral), then a set of joint states that reach MEDIUM and STRONG in each regime.
- **Enumerate the directional fields from `IndicatorResults.vb` itself**, and list them in the read, so an incomplete flip is visible.
- **Prove the fixture can fail:** flip one vote's side in a scratch copy of `_Scoring.vb`, run it, watch it fail, restore it (MD5 identical). Name the mutation in a comment.
- ⚠ Some mechanisms are asymmetric by design (for example the NO TRADE lean display). State each exemption and why.

### 2.3 Static vote-site audit

A table of all 33 mutation sites in `_Scoring.vb` plus the Step 4 penalty: the site, the signal state that triggers it, the side it changes, the sign and magnitude, the clamp or cap, the **intended semantics quoted from the indicator function or its spec**, and **agrees / disagrees**. Every *disagrees* is a stop-and-report per §0.

### 2.4 Tier-demotion census (the gate the trader remembered)

For every directional row: **raw tier** (the raw score against the thresholds) and **effective tier** (the tier actually assigned). Count, per session × regime:

- raw MEDIUM → effective WEAK;
- raw STRONG → effective MEDIUM;
- unchanged.

Report outcomes (net EV per trade, main window, per §4) for **native WEAK · demoted-to-WEAK · native MEDIUM · demoted-to-MEDIUM · native STRONG**. If demoted rows beat native rows of their new tier, the TRANSITIONAL penalty or the tier floor is miscalibrated. That is a scoring finding, so a D-table, not a fix.

**Session 1 deliverable:** `docs/medium-tier-bug-hunt-2026-09-XX.md` (verdict first: bug found or not), the fixtures, and the instrument. **Report and stop.**

## 3. Session 2 — the diagnosis (only after Session 1 reports)

Use the per-vote attribution from §2.1 and the candle-walk outcomes from `tools/ops/SwingFallbackRead` (main window and carried).

| # | Question | Method |
|---|---|---|
| **D-1** | **Is the score even monotone in quality?** | Net EV per trade and success rate by **effective score as a share of regime max** (bins), and by **dominant-minus-opposite margin**. If EV is not monotone in score, no tier cut-off can fix it. Name that outcome explicitly |
| **D-2** | **Where exactly does it break?** | Net EV by exact effective integer score per regime (for example TRENDING 7…20), not by tier |
| **D-3** | **Which votes lift a signal into MEDIUM, and do they help?** | From the attribution: vote composition of MEDIUM vs WEAK vs STRONG. Per vote, a conditional table: net EV when the vote **agrees** with the trade direction, **opposes** it, or is **absent**. A vote whose *agree* predicts worse than *oppose* is anti-predictive (cross-check against the §2.3 audit) |
| **D-4** | **Which penalties and bonuses cross a tier boundary, and are those rows better or worse?** | Per mutation (BBW squeeze, Pass 2c conflict and bonus, CVD divergence, MicroCVD decel and stall, liquidation, spread, OI × CVD, aggressor velocity, funding Step 3/3b): rows whose tier changed because of that mutation, against rows it did not touch |
| **D-5** | **Do the clamps and caps bind unevenly by tier?** | Frequency of `Math.Max(0, …)` floors and `Math.Min(…, regimeMax)` caps binding, per tier |
| **D-6** | **Dead and rare votes: is the score range compressed?** | Fire rate of every vote. Votes that almost never fire (the forming-bar volume vote, 0.69 % of NY runs; the inert TTM flat band; the spread WIDE tail guard) shrink the reachable score, so max-relative thresholds may sit in the wrong place. Report the achievable score distribution per regime against the thresholds |
| **D-7** | **Is MEDIUM just a worse mix?** | Composition per tier: regime, session hour, ATR regime (before/after 2026-08-20), side (LONG/SHORT), target type, `ExecResolution`. Then **re-weight MEDIUM to WEAK's mix** and check whether the gap survives |
| **D-8** | **Is MEDIUM a late entry?** | By tier, at entry: `VWAPDevPct` in ATR units, `ROC` magnitude, `RSI`, EMA extension, distance to the last swing high or low, `TTMHistogram` |
| **D-9** | **Which path led into MEDIUM?** | Episodes: consecutive same-direction runs inside the hold window. Tier path: rising into MEDIUM from WEAK, or decaying into it from STRONG. Also report first-signal-in-episode results, which removes the autocorrelation that inflates n |
| **D-10** | **Context and horizon** | `VerdictContext` mix and outcomes per tier. Time to resolution per tier in the carried mode: is MEDIUM a slower signal judged on too short a window? |
| **D-11** | **Direction asymmetry** | LONG vs SHORT per tier and session. A one-sided sign bug often shows up first as a LONG/SHORT asymmetry in one tier |
| **D-12** | **Era edges** | Did the MEDIUM gap start or change at v65 (2026-08-02) or v66 (2026-08-10)? |

**Session 2 deliverable:** `docs/medium-tier-diagnosis-read-2026-09-XX.md`:
- verdict first, a ranked list of causes, each marked **CONFIRMED** (holds on both halves) or **DISCOVERY ONLY**;
- for every confirmed cause, the candidate fix class (for example re-scale the tier floor, change a penalty, re-cut thresholds) as a **D-table row with no values chosen**;
- what was not verified.

Candidate fixes are tested later with `tools/WhatIfRunner` (split-half holdout, net EV). That is Step 2 of the sequence and not in this brief.

## 4. Discipline (both sessions)

- **Population:** trading-week signals only (Monday ASIA open to Friday NY close; hours from `session_volume.sessions`). Directional verdicts with placed levels. **Sessions separate.**
- **Outcomes:** the candle walk from the swing read. Main window (NY 15 min, LONDON and ASIA 45 min) primary; carried to conclusion secondary.
- **Fees:** maker/maker from `scoring.trade_costs`. ⛔ **No slippage case** (trader-ruled 2026-09-15).
- **Discovery and confirmation:** use the chronological halves from `tier-order-stability-read-2026-09-15.md`. **Look for causes in H1; a cause counts only if it also holds in H2.** State any cause seen only in H2 as DISCOVERY ONLY.
- **CIs:** bootstrap resampling whole trading days, as in the stability read.
- **Readability floor:** n ≥ 100 per cell, or mark it NOT READABLE.
- **Reserved, never do:** edit `settings.json`; change any engine `.vb` file (even to fix a bug you found); deploy; push. Fixtures in `verify/ordercheck` and instruments under `tools/` or `analysis/` are allowed.

## 5. Inputs

| Input | Use |
|---|---|
| `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv`, `aws_fetch/20260913-153704/` | The rows (gitignored, main checkout). Resolve columns by header name |
| `tools/ops/SwingFallbackRead/` (`989b89c`, extended `b2d30fc`) | Candle-walk outcomes and the H1/H2 split |
| `git log -- settings.json` and `git show <commit>:settings.json` | Era settings for re-scoring |
| `Core/ScoringEngine_*.vb`, `Core/Indicators_*.vb`, `Core/IndicatorResults.vb` | The audit and the rebuild |

## 6. Report back

After each session, at most 10 lines. **Session 1:** bug found or not (file and line), reconstruction match rate, mirror-test result, demotion census headline, commit hash. **Session 2:** ranked confirmed causes, DISCOVERY-ONLY causes, the candidate fix classes, commit hash, anything needing a trader ruling.

Commit locally with `[no-engine-change]`. Stage only your own files; other sessions commit to this repo. Output format: point form and tables; no bare section numbers without the document name; no bare IDs without source and meaning; a section separating verified from not verified.
