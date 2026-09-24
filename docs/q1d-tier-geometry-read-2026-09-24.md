# Q-1 option (d) — tier geometry: the higher success rate at the top tier is bought with lower payoff and fewer timeouts, not with direction

**Written:** 2026-09-24 (UTC; `date -u` read 17:48) by the Q-1 (d) analyst seat. **Question:** [`docs/medium-tier-diagnosis-spec-back.md`](medium-tier-diagnosis-spec-back.md) decision `Q-1`, option (d), ruled "(d) first" by the trader 2026-09-18. **Prior read:** [`docs/medium-tier-diagnosis-read-2026-09-17.md`](medium-tier-diagnosis-read-2026-09-17.md). **Review packet:** [`docs/q1d-tier-geometry-spec-back.md`](q1d-tier-geometry-spec-back.md). **Full instrument output:** [`docs/q1d-tier-geometry-2026-09-24-output.md`](q1d-tier-geometry-2026-09-24-output.md) ("output section N" below points into it). **Model / effort:** Opus, high. **No engine code, no settings change, nothing on the AWS box.**

⚠ **File date.** The brief named this file `...-2026-09-25.md`. That is the GMT+8 date. `date -u` read 2026-09-24 17:48, and project dates are UTC, so the file carries 2026-09-24.

**Vocabulary:** [`docs/DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a (success rate, gross and net breakeven rate, gross and net edge, net EV per trade). Fees: maker/maker, 3.00 bps round trip (`scoring.trade_costs`, settings v68).

**Legend. IDs and terms used in this doc:**

| ID or term | Source and kind | Meaning |
|---|---|---|
| `Q-1` | Decision in `docs/medium-tier-diagnosis-spec-back.md` §3 | What to do with the tier ladder. (a) = `F-1`; (c) = `F-4a`; (d) = this measurement; (e) = `F-4b`, REJECTED 2026-09-18 |
| `F-1` | Fix class in `docs/medium-tier-diagnosis-read-2026-09-17.md` §3 | Do not re-cut the tier thresholds or the tier floor. Changes nothing |
| `F-4a` | Same doc, same table | Treat all tiers as one class for Kelly sizing only. The payload `confidence` field stays unchanged |
| P0 | This doc, population | The prior read's 8,810 rows (v51 edge 2026-07-06 to 2026-09-11), with its halves H1 / H2. The labelled population |
| H1, H2 | The stability read's halves, carried unchanged from the prior read | H1 = before 2026-08-10 00:00 UTC; H2 = after. ⚠ The split sits 18.6 h before the v66 deploy |
| H3 | This doc, forward segment | 1,710 new rows, 2026-09-14 to 2026-09-24 08:45 UTC, from fetch `aws_fetch/20260924-084613`. Reported apart, never pooled with P0 |
| E1, E2, E3 | This doc, era segments of P0 | E1 = v51 edge to the v66 deploy (2026-08-10 18:36:01); E2 = v66 to the ATR step (2026-08-20); E3 = ATR step to 2026-09-11 |
| CONFIRMED, NO DIFFERENCE SHOWN, … | Label rule in `tools/ops/medium_tier_diagnosis.py` header (the census rule), copied unchanged | CONFIRMED = both halves n ≥ 100 per group, both 95 % CIs exclude 0, same sign |
| Payoff ratio | This doc | Σ target distance ÷ Σ stop distance, per cell (the placed R:R, distance-weighted) |
| Distance-weighted success rate | This doc, newly coined | Σ over target hits of (T + S) ÷ Σ (T + S). With it, net EV per trade = mean(T + S) × (distance-weighted success − gross breakeven rate) + timeout term − fee, exactly |
| Timeout term | This doc, newly coined | (1 ÷ n) × Σ over timeouts of (stop distance + mark). What the timeouts earn compared with a stop hit |
| ATR fallback target | Target ladder, `SignalEmitter.ComputeSideLevels` | Entry ± k × ATR; k = 1.75 NY, 2.0 LONDON, 1.25 ASIA. Logged as `TargetCapReason` = `none` |
| POC-fix slice | This doc | P0 rows that the undeployed POC-gate fix (`a6b33fe`) can move: 176 rows, a superset of the 143 population rows the fix turns to NO TRADE |

---

## 1. Answer to (d)

- **Why success rate rises at the top score bins while net EV per trade does not: two geometry effects pay for it.** Neither is direction information.
  1. **Higher tiers get a lower payoff ratio.** They lack a swing target far more often, so they place the ATR fallback target instead of a farther swing target. The stop is 1.6 × ATR on 92–100 % of rows in every session and tier (98–100 % in NY). So the payoff ratio falls and the gross breakeven rate rises with the tier.
  2. **STRONG times out less often.** The success rate counts a timeout as a failure. But a NY WEAK timeout is marked in profit on average, so turning timeouts into target hits adds success rate and almost no net EV.
- **Is the "nearer targets" hypothesis right?** In ATR and R terms, yes, in every readable session. In bps, only in ASIA. NY STRONG targets are not nearer in bps (+1.2 bps, NO DIFFERENCE SHOWN), because STRONG fires at a higher ATR (+1.5 bps of entry, CONFIRMED).
- **The opposite direction (farther targets or a better payoff at higher tiers) appears in no readable comparison.** The only "wider" result is the NY STRONG stop in bps (+2.4 bps, CONFIRMED). It comes from the higher ATR, not from a different stop rule.
- **No tier gap in success rate, gross edge or net EV per trade is CONFIRMED in any session.** The only CONFIRMED outcome differences are timeout shares.

### 1.1 Key numbers per session (P0, main window: NY 15 min, LONDON and ASIA 45 min; output sections 1–3)

| Session | Tier | n | ATR fallback target % | Payoff ratio | Gross BE % | Success % | Timeout % | Gross edge pp | Net EV bps [95 % CI] |
|---|---|---|---|---|---|---|---|---|---|
| NY | STRONG | 342 | 75.7 | 1.08 | 48.1 | 45.9 | 2.3 | −2.2 | −3.2 [−5.2, −1.4] |
| NY | MEDIUM | 1,552 | 60.5 | 1.14 | 46.7 | 39.9 | 7.3 | −6.8 | −4.2 [−5.3, −3.1] |
| NY | WEAK | 3,523 | 46.9 | 1.19 | 45.7 | 40.1 | 8.1 | −5.6 | −3.2 [−4.0, −2.4] |
| LONDON | STRONG | 131 | 80.9 | 1.21 | 45.3 | 41.2 | 6.1 | −4.1 | −1.6 [−5.8, +3.2] |
| LONDON | MEDIUM | 485 | 66.2 | 1.19 | 45.6 | 46.2 | 7.6 | +0.6 | −0.8 [−3.0, +1.3] |
| LONDON | WEAK | 1,049 | 44.2 | 1.27 | 44.1 | 42.3 | 10.2 | −1.8 | −1.2 [−2.8, +0.3] |
| ASIA | STRONG | 65 (NOT READABLE) | 78.5 | 0.81 | 55.3 | 64.6 | 7.7 | +9.3 | +2.5 [−2.0, +6.7] |
| ASIA | MEDIUM | 420 | 59.8 | 0.89 | 52.9 | 48.3 | 5.5 | −4.5 | −3.6 [−5.4, −2.0] |
| ASIA | WEAK | 1,243 | 34.8 | 1.10 | 47.5 | 46.7 | 8.2 | −0.8 | −2.2 [−3.8, −0.7] |

### 1.2 Tier comparisons that carry the answer (P0, labelled; output sections 2.1 and 5)

| Session | Comparison | No swing target, pp | Payoff ratio | Gross BE, pp | Success, pp | Timeout share, pp | Gross edge, pp | Net EV, bps |
|---|---|---|---|---|---|---|---|---|
| NY | STRONG − WEAK | +31.2, CONFIRMED | −0.11 [−0.15, −0.06], CONFIRMED | +2.4 [+1.3, +3.3], CONFIRMED | +5.8 [+0.2, +10.8], NO DIFFERENCE SHOWN | −5.8 [−7.5, −3.9], CONFIRMED | +3.4 [−2.2, +8.7], NO DIFFERENCE SHOWN | −0.0 [−1.8, +1.6], NO DIFFERENCE SHOWN |
| NY | MEDIUM − WEAK | +15.1, CONFIRMED | −0.05, CONFIRMED | +1.0, CONFIRMED | −0.2, NO DIFFERENCE SHOWN | −0.8, NO DIFFERENCE SHOWN | −1.2, NO DIFFERENCE SHOWN | −1.0 [−2.2, +0.2], NO DIFFERENCE SHOWN |
| LONDON | STRONG − WEAK | +40.1, NO DIFFERENCE SHOWN | −0.06, NO DIFFERENCE SHOWN | +1.2, NO DIFFERENCE SHOWN | −1.1, NO DIFFERENCE SHOWN | −4.1, NO DIFFERENCE SHOWN | −2.3, NO DIFFERENCE SHOWN | −0.4, NO DIFFERENCE SHOWN |
| LONDON | MEDIUM − WEAK | +26.1, CONFIRMED | −0.07, CONFIRMED | +1.5, CONFIRMED | +3.9, H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT | −2.6, NO DIFFERENCE SHOWN | +2.4, NO DIFFERENCE SHOWN | +0.4, NO DIFFERENCE SHOWN |
| ASIA | STRONG − WEAK | +44.5, NOT READABLE | −0.30, NOT READABLE | +7.8, NOT READABLE | +17.9, NOT READABLE | −0.5, NOT READABLE | +10.1, NOT READABLE | +4.7, NOT READABLE |
| ASIA | MEDIUM − WEAK | +26.3, CONFIRMED | −0.21, CONFIRMED | +5.3, CONFIRMED | +1.6, NO DIFFERENCE SHOWN | −2.7, H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT | −3.7, NO DIFFERENCE SHOWN | −1.5, H1 FINDING, H2 CONTRADICTS OR NOT READABLE |

- The LONDON STRONG geometry gaps point the same way as MEDIUM − WEAK but miss CONFIRMED on n = 131 (the H2 CIs are wide).
- ASIA STRONG is the only positive-EV tier cell, and it is NOT READABLE (n = 65). The forward segment H3 does not repeat it: ASIA STRONG n = 21, success 42.9 %, net EV −3.9 bps (output section 8.1).

---

## 2. The mechanism, measured

### 2.1 Why higher tiers get the ATR fallback target (output section 2.1)

| Session | Tier | No swing target % | Swing target beyond the 3.5 × ATR bound % | Swing target qualifies % | Median qualifying swing target, ATR |
|---|---|---|---|---|---|
| NY | STRONG / MEDIUM / WEAK | 71.9 / 55.9 / 40.8 | 10.8 / 12.4 / 24.2 | 17.3 / 31.8 / 35.0 | 2.20 / 2.21 / 2.50 |
| LONDON | STRONG / MEDIUM / WEAK | 80.2 / 66.2 / 40.0 | 3.1 / 4.9 / 13.3 | 16.8 / 28.9 / 46.6 | 1.78 / 1.78 / 2.19 |
| ASIA | STRONG / MEDIUM / WEAK | 81.5 / 63.3 / 37.0 | 0.0 / 2.4 / 6.8 | 18.5 / 34.3 / 56.2 | 1.29 / 1.76 / 2.16 |

- **At higher tiers the logged swing target is usually absent.** The engine logs no swing target beyond price, so the ladder falls through to the ATR fallback (the HVN tier catches few). No row has a swing target on the wrong side of entry.
- This fits the prior read's CONFIRMED composition finding: MEDIUM enters more extended than WEAK, with less room to the same-side 5m swing (`docs/medium-tier-diagnosis-read-2026-09-17.md` §2.8, question `D-8`). **The momentum votes that lift the score fire after price has left the last swing, so the structural target is gone.** The link between "extended" and "no swing target" is my reading; I did not test it row by row.
- **The fallback target sits nearer than a swing target in ATR** (NY 1.75 against a 2.2–2.5 median swing target). In ASIA the fallback is 1.25 × ATR against a 1.6 × ATR stop: a payoff ratio of 0.78 and a gross breakeven rate of 56.1 % on every fallback row.

### 2.2 Stops do not move with the tier (output section 2)

- STOP_CLAMPED (a swing stop exists but is wider than 1.6 × ATR, so the stop is clamped to 1.6 × ATR): NY 99.4 / 99.2 / 98.0 %, LONDON 97.7 / 94.8 / 90.7 %, ASIA 100.0 / 97.6 / 92.5 % (STRONG / MEDIUM / WEAK). SWING_STOP is 0–7.8 %.
- So the stop in bps is 1.6 × ATR in bps. The NY STRONG stop is wider in bps (+2.4, CONFIRMED) only because NY STRONG fires at a higher ATR (+1.5 bps of entry, CONFIRMED).
- Stop labels are derived from the logged `SwingStop*`, `Price` and `ATR` by the shipped rule; they reproduce the logged placed stop on 10,520 of 10,520 rows (output section 0).

### 2.3 Where the NY STRONG success rate goes (the net EV identity, output section 3)

| NY cell | Success % | Timeout % | Mean T + S, bps | Distance-weighted gross edge, pp | Mean(T + S) × that edge, bps | Timeout term, bps | Fee, bps | Net EV, bps |
|---|---|---|---|---|---|---|---|---|
| STRONG | 45.9 | 2.3 | 31.9 | −3.1 | −0.99 | +0.74 | −3.00 | −3.2 |
| WEAK | 40.1 | 8.1 | 28.3 | −5.8 | −1.63 | +1.43 | −3.00 | −3.2 |
| STRONG − WEAK | +5.8 | −5.8 | +3.6 | +2.7 | +0.64 | −0.69 | 0 | −0.0 |

- The success rate gain (+5.8 pp) is almost exactly the timeout loss (−5.8 pp); the stop-hit share is flat (51.8 against 51.7 %).
- After the payoff shift the distance-weighted gross edge gains only +2.7 pp (NO DIFFERENCE SHOWN). That is +0.64 bps per trade.
- The lost timeouts cost −0.69 bps: NY WEAK timeouts end, on average, in profit (timeout term +1.43 bps on 8.1 % of rows).
- **Net: −0.0 bps.** The identity holds on every session × tier cell to 3 × 10⁻¹⁵ bps (output section 0).

### 2.4 Carried 24 h, no timeouts (output section 10)

- With every trade carried to its target or its stop, the success rate gain shrinks to the payoff shift. NY STRONG − WEAK: success +2.6 pp, gross breakeven +2.4 pp, **gross edge +0.2 [−5.4, +5.4]**, net EV −0.3 bps. All NO DIFFERENCE SHOWN.
- NY STRONG / MEDIUM / WEAK gross edge carried: −1.6 / −3.6 / −1.8 pp. **Flat.**

---

## 3. Does the ladder carry direction that the geometry spends?

**Not shown.** Two geometry controls, and a residual that stays at single-half strength.

| Control (output section) | NY STRONG − WEAK success, pp | NY STRONG − WEAK net EV, bps | Label |
|---|---|---|---|
| None (raw, section 5) | +5.8 [+0.2, +10.8] | −0.0 [−1.8, +1.6] | NO DIFFERENCE SHOWN |
| Same per-signal breakeven quintile × T + S tercile (section 6) | −2.3 [−9.3, +4.2] | −1.9 [−4.0, +0.1] | NO DIFFERENCE SHOWN |
| Same target type: ATR fallback only (section 6.1) | +10.2 [+1.2, +18.4]; H1 +6.3, H2 +12.7, neither significant; H3 +10.7 [−9.6, +26.9] | +2.0 [−0.8, +4.6]; H3 +4.6 | NO DIFFERENCE SHOWN |

- **Every MEDIUM − WEAK control in every session reads NO DIFFERENCE SHOWN** on the success rate (section 6, section 6.1).
- **The one hint: NY STRONG rows with an ATR fallback target.** Success 51.4 % against WEAK's 41.2 % on the same geometry, the same sign in FULL, H1, H2 and H3. It does not reach CONFIRMED, and the bps-stratified control removes it. The two controls disagree because NY STRONG fires at a higher ATR, so at the same R geometry it sits in bigger bps strata. **Even this best NY STRONG cell has net EV −1.8 bps.**
- NY STRONG rows with a swing target do worse than WEAK (success −14.1 pp, net EV −6.5 bps), NOT READABLE (n = 59).
- **Reading:** the ladder mostly selects geometry. Any direction information it carries is at most one single-half effect in one cell, and it does not survive fees.

---

## 4. Era edges and the forward segment

- **The H1/H2 split sits 18.6 h before the v66 deploy.** I kept the prior read's halves so the labels stay comparable. Section 7 of the output then repeats the key tier gaps inside each era, where no settings edge touches the rows.
- **Geometry is stable across eras.** NY STRONG − WEAK payoff ratio: E1 −0.11, E2 −0.10, E3 −0.10, H3 −0.05. Gross breakeven: +2.2 / +2.4 / +2.1 / +1.1 pp. MEDIUM − WEAK payoff ratio is negative in every era in all three sessions.
- **Outcome gaps are not significant inside any era.** NY STRONG − WEAK net EV: −0.0 / −0.3 / −0.6 / +3.4 bps, every CI across 0.
- **H3 (forward, v68 throughout, no settings edge).** NY net EV STRONG / MEDIUM / WEAK: −2.0 (n 65, NOT READABLE) / −4.7 / −5.3 bps. Success 44.6 / 41.2 / 37.2 %. The payoff ratio still falls with the tier in NY (1.11 / 1.14 / 1.16), LONDON (1.25 / 1.24 / 1.44) and ASIA (0.95 / 0.88 / 1.12).
- ⚠ H3 contains the 2026-09-21 15:38 UTC collector deploy (instance `ee159d03…`), a code edge but not a settings or scoring edge (per `docs/aws-collector-deploy-checklist.md`, the row for that instance). I did not split H3 at it; H3 has too few rows.
- **The 3-day collector hole (2026-09-18 00:41 → 2026-09-21 15:39 UTC)** removes those days from H3; H3 holds 1,710 rows over 8 trading days. It does not touch P0.

## 5. POC-fix sensitivity

- The logged book was written by the pre-fix POC gate. The fix (`a6b33fe`, not deployed) moves 1,288 of 38,665 placed targets across all rows. **In the population it moves 143 verdict-side targets, and all 143 become NO TRADE through Step 5c** (`docs/engine-fix-session-a-batch-summary.md`). They leave the population; they do not change its geometry.
- I removed a 176-row superset of them (P0 rows whose verified VPFR label opens the POC tier only under the fix, with an ATR fallback target). **No CONFIRMED label changes, and no point estimate moves by more than 0.5** (output section 9). Two single-half labels shift, both among non-findings: LONDON MEDIUM − WEAK success rate (H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT → NO DIFFERENCE SHOWN) and ASIA MEDIUM − WEAK net EV (H1 FINDING, H2 CONTRADICTS OR NOT READABLE → H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT). Example: NY STRONG − WEAK success +5.8 → +5.3 pp; gross breakeven +2.4 → +2.4 pp; net EV −0.0 → −0.2 bps.
- H3 has no VPFR attribution, so the slice is not tested there.

---

## 6. What this means for `Q-1` (a) against (c)

**Read (the decision is the trader's; both options are reserved):**

- **(a) `F-1` holds, with a second reason.** A re-cut moves rows between cells that trade success rate for payoff one to one. A threshold cannot fix that.
- **(c) `F-4a` is supported by the measurement.** Kelly EST (`Core/ScoringEngine_Kelly.vb`, Step 1 and Step 2) assumes the win probability rises 10 pp per tier (0.45 / 0.55 / 0.65) at a fixed payoff b = 1.75 ÷ 1.6 = 1.094. The book contradicts both halves of that:

| Session | Tier | Kelly p | Measured success rate | Kelly b | Measured payoff ratio | Kelly f* | f* at measured p and b |
|---|---|---|---|---|---|---|---|
| NY | STRONG / MEDIUM / WEAK | 0.65 / 0.55 / 0.45 | 0.459 / 0.399 / 0.401 | 1.094 | 1.08 / 1.14 / 1.19 | +0.330 / +0.139 / −0.053 | −0.041 / −0.128 / −0.103 |
| LONDON | STRONG / MEDIUM / WEAK | same | 0.412 / 0.462 / 0.423 | 1.094 | 1.21 / 1.19 / 1.27 | same | −0.075 / +0.011 / −0.032 |
| ASIA | STRONG / MEDIUM / WEAK | same | 0.646 (NOT READABLE) / 0.483 / 0.467 | 1.094 | 0.81 / 0.89 / 1.10 | same | +0.208 / −0.096 / −0.015 |

- The tier-to-p step is +20 pp STRONG over WEAK. The measured step is +5.8 pp in NY (not replicated), −1.1 pp in LONDON, and it is paid for by a payoff ratio that falls with the tier.
- **My read: record (a) and adopt (c).** They are not exclusive. The prior in `CLAUDE.md` (the auto-proceed ruling) says to lead with the more truthful option. (c) removes a rendered claim the book contradicts; (a) alone keeps it. (a) alone is also the cheaper option, which is the reserved class, so I do not recommend it alone.
- ⚠ **(c) has an open value, and it decides what the Kelly block shows.** At b = 1.094 the Kelly breakeven is p = 1 ÷ (1 + b) = 0.478. A single class at the measured pooled success rate (0.40–0.48 in every readable cell) gives f* ≤ 0: **the Kelly block goes silent on every signal.** A single class at 0.55 keeps it at the 5 % cap on every signal. Today STRONG and MEDIUM both already hit the 5 % cap (half-Kelly 0.165 and 0.069); only WEAK is silent. The parked Kelly placed-payoff proposal ([`docs/kelly-placed-payoff-proposal.md`](kelly-placed-payoff-proposal.md)) touches the same b; one pass could settle both.
- None of this touches the payload `confidence` field (`F-4b`, REJECTED).

---

## 7. Method

| Item | Rule |
|---|---|
| Population | P0 = the prior read's 8,810 rows, unchanged (0 of 8,810 rows differ, output section 0). H3 = 1,710 new rows from `aws_fetch/20260924-084613`. Weekday rows only (the trading-week rule in the export). Pooled book + box live log; the rotated `analysis_log.csv.v0.7.bak` (33,911 rows) is fully inside the pooled book (0 bak timestamps missing from it, checked) |
| Outcomes | The swing read's candle walk, exported by `SwingFallbackRead --mode diagexport`. Main window primary; carried 24 h secondary, P0 only |
| Distances | Per signal, bps of entry, from logged `Price` and `Placed*`. Pooled rates by the Σ formulas in `docs/DeribitIndicatorProject.md` §5a |
| CIs and labels | Day bootstrap, 10,000 resamples, seed 20260924; n ≥ 100 per group; the census label rule, two-sided |
| Geometry control | Per-signal breakeven S ÷ (T + S) quintile × (T + S) tercile, cut points from the session's H1 rows. Tied quintiles (the fallback geometry) merge strata; all 15 are occupied |
| Instrument | `tools/ops/q1d_tier_geometry.py` (new, committed; Python 3 standard library; host-agnostic; reads files only). Export: the existing `SwingFallbackRead --mode diagexport`, unchanged |
| Settings | Geometry keys (`atr_target_multiplier`, `atr_stop_multiplier`, `structural_levels.*`) are identical in all 18 era files v51–v68 (checked against `backtest_data/swing-fallback-read/rescore-eras/`) |
| Multiplicity | 237 labelled comparisons, 179 readable; about 0.22 false CONFIRMED expected. Of 29 CONFIRMED, 26 are geometry or composition differences and 3 are timeout shares. None is a success rate, a gross edge or a net EV |

---

## 8. What I verified, and what I did not

### Verified, and how

| Claim | How |
|---|---|
| P0 is the prior read's population, outcomes included | The new export's 8,810 P0 rows equal the prior export field for field (Half excluded); the prior export's MD5 equals the one the prior output names |
| The stop label derivation | Reproduces the logged placed stop on 10,520 of 10,520 rows |
| The net EV identity | Residual ≤ 3 × 10⁻¹⁵ bps over every session × tier cell; target and stop rows equal ± distance − fee exactly |
| The full output is deterministic | A second 10,000-resample run, diffed (spec-back handle `H-2`) |
| The extended export regenerates | Re-run into the same cache: MD5 identical (spec-back handle `H-6`) |
| The .bak book is inside the pooled book | Timestamp set difference = 0 |
| Kelly's p and b mapping | Read in `Core/ScoringEngine_Kelly.vb` this session |

### Not verified

- **That "no swing target" is caused by extension at entry.** It is consistent with the prior read's `D-8` result; I did not join the two row by row.
- **Why NY STRONG times out less.** A higher realised volatility against a lagging ATR is a hypothesis; not measured.
- **The POC-fix slice in H3:** no VPFR attribution exists for those rows.
- **The WebSocket feed state before the 2026-09-21 deploy.** The deploy checklist says the feed first reported OK on 2026-09-21; I did not check what that means for P0 or H3 inputs.
- **LONDON STRONG and ASIA STRONG:** NOT READABLE or not CONFIRMED on n.
- **Carried over without checking:** the swing read's candle walk and population funnel; the 143 and 1,288 POC-fix counts (quoted from `docs/engine-fix-session-a-batch-summary.md`); the collector-hole times (read from the log's row dates, not from the box).

---

## 9. Re-run

```
dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode diagexport --fetch aws_fetch/20260924-084613 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --cache backtest_data/q1d-tier-geometry --out backtest_data/q1d-tier-geometry/diagnosis-rows-20260924.csv
python tools/ops/q1d_tier_geometry.py --root .
```

- The P0 inputs (`backtest_data/swing-fallback-read/diagnosis-rows.csv`, `rescore-attribution.csv`) come from the prior read's re-run (`docs/medium-tier-diagnosis-read-2026-09-17.md` §6).
- ⚠ **Use a separate `--cache` for the newest fetch.** The export writes weekly 1-minute candle files; the week of 2026-09-21 was fetched mid-week (bars to about 17:50 UTC on 09-24). In the shared `backtest_data/swing-fallback-read/` cache that partial week would be reused by later runs.
- The analysis takes about 7 minutes at 10,000 resamples; `--resamples 200` runs in about 8 s with identical point estimates.
