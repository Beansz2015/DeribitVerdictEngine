# MEDIUM-tier diagnosis, session 2 — no confirmed cause: the score does not rank outcomes, so MEDIUM has no gap of its own to explain

**Written:** 2026-09-17 (UTC; `date -u` read 12:16) by the session 2 seat. **Brief:** [`docs/medium-tier-diagnosis-brief-2026-09-16.md`](medium-tier-diagnosis-brief-2026-09-16.md) §3 (questions D-1 to D-12) and §4 (discipline). **Session 1 read:** [`docs/medium-tier-bug-hunt-2026-09-16.md`](medium-tier-bug-hunt-2026-09-16.md). **Review packet:** [`docs/medium-tier-diagnosis-spec-back.md`](medium-tier-diagnosis-spec-back.md). **Model / effort:** Opus, high.

**Vocabulary:** [`docs/DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a (success rate, net EV per trade). **Full instrument output:** [`docs/medium-tier-diagnosis-2026-09-17-output.md`](medium-tier-diagnosis-2026-09-17-output.md). "Output section N" below points into that file.

**Legend. IDs and terms used in this doc:**

| ID or term | Source and kind | Meaning |
|---|---|---|
| `D-1` … `D-12` | Questions in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §3 | The twelve diagnosis questions |
| `F-1` … `F-4` | This doc, candidate fix classes (section 3) | D-table rows with no values chosen |
| H1, H2 | The stability read's chronological halves | H1 = the first 24 UTC trading days (before 2026-08-10 00:00 UTC); H2 = the other 25 |
| CONFIRMED | Label rule in `tools/ops/medium_tier_diagnosis.py` header (the census rule) | Both halves readable (n ≥ 100 per cell), both 95 % CIs exclude 0, same sign |
| DISCOVERY ONLY (H2) | Same rule | Significant in H2 only. Not a cause |
| H1 FINDING … | Same rule | Significant in H1 only; H2 same sign but not significant, or contradicts |
| Vote lean | This doc | Trade-side points minus other-side points of one breakdown label. Agree > 0, oppose < 0, absent = 0 |
| `L-1` | Finding in `docs/medium-tier-bug-hunt-2026-09-16.md` section R.2 | The live stream never delivers the liquidation flag; the liquidation vote never fires |

---

## 1. Verdict

- **No CONFIRMED cause of a MEDIUM gap.** The reason is upstream of MEDIUM: **the score does not rank outcomes in any session** (`D-1`). No tier cut-off can fix that.
- **The MEDIUM gap itself does not replicate.** MEDIUM − WEAK net EV per trade, main window: NY −1.0 [−2.2, +0.2], LONDON +0.4 [−2.0, +2.7], ASIA −1.5 [−3.5, +0.6]. No session reaches CONFIRMED; ASIA is significant in H1 only (output section 1.1). The brief's evidence ("lowest or joint-lowest") is a point-estimate ordering, and this read agrees with its direction in NY and ASIA only.
- **The VPFR score vote is NOT anti-predictive in any session.** Session 1's 64.1 % "points against the verdict side" is reproduced (2,844 of 4,440) and is a geometry fact, not an outcome fact. On NY LONG trades the vote is **predictive**, CONFIRMED (section 2.3 of this doc).
- **Late entry is CONFIRMED as composition, NOT as a cause.** MEDIUM enters more extended than WEAK in every session. Re-weighting NY MEDIUM to WEAK's extension mix leaves the gap unchanged (−1.0 → −1.0).
- **Multiplicity:** 780 labelled comparisons, 445 readable, expected false CONFIRMED about 0.56. Of 21 CONFIRMED labels, 19 are mechanical extension differences; 2 are outcome findings (NY VPFR long, ASIA OFI carried). Treat each of those 2 as one replication away from certain.

### 1.1 Ranked causes

| Rank | Candidate cause of MEDIUM underperformance | Label | Evidence (main window unless named) |
|---|---|---|---|
| 1 | **The score carries no outcome information** (`D-1`, `D-2`) — MEDIUM is not a defective tier; every tier is the same trade | **CONFIRMED as a null in both halves** (see the note below) | Slope of net EV on score share, per 0.1 of regime max: NY −0.3 [−0.8, +0.2], H1 −0.3, H2 −0.3; LONDON +0.1; ASIA +0.2. Every boundary integer comparison: NO DIFFERENCE SHOWN |
| 2 | Momentum votes that agree with the trade predict worse: ROC and BBW/TTM, NY | DISCOVERY ONLY (H2) | ROC agree − oppose −3.3 [−5.4, −1.1] (H1 −2.3 [−4.5, +0.1]); BBW/TTM −2.1 [−3.6, −0.5] (H1 −2.0 [−4.3, +0.5]) |
| 3 | Extension at entry predicts worse, NY | DISCOVERY ONLY (H2) | VWAP distance top − bottom tercile −1.7 [−3.5, −0.1] (H1 −0.8); ROC tercile −1.6 |
| 4 | Decaying path into MEDIUM (after a STRONG signal), NY | NO DIFFERENCE SHOWN (H2 significant, H1 not) | MEDIUM after WEAK − MEDIUM after STRONG +2.1 [−0.2, +4.2]; H2 +4.1 [+1.9, +6.1] |
| 5 | Funding Step 3 and 3b modifiers vote against the edge, NY | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT | FUNDING agree − oppose −1.5 [−3.1, −0.0] |
| — | VPFR vote, TRANSITIONAL demotion, clamps and caps, mix (regime, hour, side, ATR regime, target type, ExecResolution), horizon, side asymmetry, era edges | No finding | Sections 2.3–2.11 of this doc |

- ⚠ **Note on rank 1.** The label rule tests for a difference, so a flat slope reads NO DIFFERENCE SHOWN, not CONFIRMED. I call it a confirmed null because the slope is near 0 with the same sign in FULL, H1 and H2 in NY, and both halves' CIs bound it. **The NY upper bound, +0.2 bps per 0.1 of regime max, means one full tier step (about 0.17 of regime max) is worth at most about +0.35 bps.** That is the measured size of the tier ladder's information in NY.
- **Explanation sets, not first match (output section 13a).** Candidate predicates overlap heavily on NY MEDIUM rows: 1,175 of 1,552 rows (75.7 %) sit in 3 or more candidate sets. Only 19 rows sit in none. So no single predicate can be "the" cause even descriptively.

---

## 2. Answers per question

### 2.1 `D-1` — is the score monotone in quality? **No, in any session.**

| Session | Share bins < 0.45 / 0.45–0.53 / 0.53–0.60 / 0.60–0.70 / ≥ 0.70: net EV | Slope per 0.1 share [95 % CI] | Slope per margin point [95 % CI] | Label (both) |
|---|---|---|---|---|
| NY | −3.3 / −3.1 / −3.4 / −4.8 / −3.2 | −0.3 [−0.8, +0.2] | −0.2 [−0.3, +0.0] | NO DIFFERENCE SHOWN |
| LONDON | output section 2 | +0.1 [−0.9, +1.2] | +0.1 [−0.3, +0.5] | NO DIFFERENCE SHOWN |
| ASIA | output section 2 | +0.2 [−0.9, +1.2] | +0.1 [−0.2, +0.5] | NO DIFFERENCE SHOWN |

- Carried 24 h gives the same labels (NY share slope −0.4 [−0.9, +0.2]).
- **Named outcome, as the brief asks: EV is not monotone in score, so no tier cut-off can fix MEDIUM.** A re-cut only moves rows between cells that carry the same net EV.
- Success rate does rise at the top (NY ≥ 0.70: 45.9 % against 38.7–41.5 % below), but net EV does not follow. Why is not measured here (see section 5 of this doc).

### 2.2 `D-2` — where does it break? **Nowhere specific; it is flat.**

- NY TRENDING (MaxScore 20), effective 7 → 15: −3.2, −3.4, −2.9, −4.1, −3.7, −4.8, −4.5, −4.1, −1.1 (output section 3). Above 15: NOT READABLE.
- NY TRANSITIONAL (MaxScore 15): 6 −2.4, 7 −3.0, 8 −4.0; 9 and 10 NOT READABLE (−5.3, −6.3). Point estimates fall with score.
- Boundary comparisons (MEDIUM floor − WEAK top, STRONG floor − MEDIUM top): every readable one is NO DIFFERENCE SHOWN. NY TRENDING 11 − 10: +0.4 [−1.6, +2.3].
- RANGE_BOUND (MaxScore 19) cells are NOT READABLE except NY score 7 (n 119, −2.3).

### 2.3 `D-3` — which votes lift a signal into MEDIUM, and do they help?

**The VPFR score vote (priority).** Verified profiles only (8,508 rows). Output section 4.4.

| Session | NEAR_HVN labels: agree − oppose | Within tier | LONG trades only | SHORT trades only | IN_LVN labels |
|---|---|---|---|---|---|
| NY | +1.5 [−0.5, +3.4]; H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT | +1.4 [−0.5, +3.3] | **+5.0 [+2.4, +7.2]; CONFIRMED** (H1 +4.4, H2 +5.5) | −1.3 [−3.3, +1.0]; NO DIFFERENCE SHOWN | Agrees on 1,188 rows, opposes on 64: agree − oppose NOT READABLE. Agree − NEUTRAL label −0.3 [−1.9, +1.5]: NO DIFFERENCE SHOWN |
| LONDON | −0.1 [−4.1, +3.8]; NO DIFFERENCE SHOWN | −0.2 | NOT READABLE | −0.5; NO DIFFERENCE SHOWN | Agree − NEUTRAL +0.8; NO DIFFERENCE SHOWN |
| ASIA | +2.4 [−1.7, +6.4]; NO DIFFERENCE SHOWN | +2.4 | +1.9; NO DIFFERENCE SHOWN | +3.2; NO DIFFERENCE SHOWN | Agree − NEUTRAL −1.4; NO DIFFERENCE SHOWN |

- **Answer: the VPFR vote is not anti-predictive in NY, LONDON or ASIA.** Where it has a measurable effect (NY LONG trades), agreeing is better, CONFIRMED.
- NY LONG cells: NEAR_HVN_SUPPORT (price just below the POC, vote LONG) +1.4 net EV, n 445; NEAR_HVN_RESIST (price at or just above the POC, vote SHORT) −3.6, n 841. **The NY SUPPORT-long cell is the only positive-EV NY cell in the VPFR table.**
- **Why it "points against the verdict" 64.1 % of the time:** a trend verdict usually has price moving away from the POC, and the vote points back toward the POC. The vote is contrarian by construction. That costs points in the score; it does not cost outcome.
- **IN_LVN is nearly collinear with the trade side** (NY 1,188 agree against 64 oppose). It adds a point to the trend side on most rows and has no measured outcome effect against the NEUTRAL label.
- **The label-name trap from session 1 (`docs/medium-tier-bug-hunt-spec-back.md` decision D-2) still applies:** SUPPORT names a POC above price.

**Other votes, NY (output section 4.2; the within-tier version in section 4.3 agrees in sign on every row below):**

| Vote | n agree / oppose / absent | Net EV agree / oppose / absent | Agree − oppose [95 % CI] | Label |
|---|---|---|---|---|
| ROC | 3,417 / 606 / 1,394 | −4.0 / −0.7 / −3.5 | −3.3 [−5.4, −1.1] | DISCOVERY ONLY (H2) |
| BBW/TTM | 2,744 / 1,023 / 1,650 | −4.0 / −2.0 / −3.5 | −2.1 [−3.6, −0.5] | DISCOVERY ONLY (H2) |
| Funding (Step 3 + 3b) | output section 4.2 | output section 4.2 | −1.5 [−3.1, −0.0] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT |
| OFI | 1,808 / 1,527 / 2,082 | −2.4 / −4.3 / −3.8 | +1.8 [+0.6, +3.1] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT |
| TFI | output section 4.2 | output section 4.2 | +0.7 [−0.0, +1.5] | H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT |

- **ASIA OFI:** agree − oppose +2.4 [+0.5, +4.1] main (H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT); **carried 24 h +3.0 [+1.3, +4.8], CONFIRMED.** The only CONFIRMED order-flow vote.
- **No vote is CONFIRMED anti-predictive in any session.** The pattern across NY is order flow (OFI, TFI) positive and momentum (ROC, BBW/TTM, funding) negative, but only in single halves.
- **Which votes lift rows into MEDIUM (output section 4.5):** the lift test collapses to the MEDIUM floor integer. Of 1,552 NY MEDIUM rows, 650 are lifted by 3 or more votes at once (they sit on the floor, where any agreeing point lifts them), 65 by one or two votes, and 837 by no single vote. NY floor-integer MEDIUM rows do slightly better than the rest (for example lifted by TFI +2.4 [+0.5, +4.2]), DISCOVERY ONLY (H2) for every vote. The per-vote question is better answered by the agree / oppose table above.
- **Composition by tier (output section 4.1):** MEDIUM carries more agreeing trend votes than WEAK (ROC, RSI, EMA, Donchian, trend structure), as the score construction requires.

### 2.4 `D-4` — mutations that cross a tier boundary. **No crossing differs.**

| Mutation, NY (output section 5) | Crossings | Readable comparisons | Result |
|---|---|---|---|
| Pass 2 partial upgrades (+1 per cross-confirmed partial) | WEAK → MEDIUM 1,137; below → WEAK 1,461; MEDIUM → STRONG 269 | Kb WEAK → MEDIUM: −1.9 [−4.1, +0.6] | NO DIFFERENCE SHOWN |
| Trend structure bonus | WEAK → MEDIUM 298; MEDIUM → STRONG 114; below → WEAK 417 | 4 | NO DIFFERENCE SHOWN on all |
| Pass 2c alignment bonus | WEAK → MEDIUM 252; MEDIUM → STRONG 135 | 4 | Ka WEAK → MEDIUM +1.4, DISCOVERY ONLY (H2); rest NO DIFFERENCE SHOWN |
| Funding Step 3 + 3b | MEDIUM → WEAK 342; STRONG → MEDIUM 120; WEAK → MEDIUM 86 | 5 | NO DIFFERENCE SHOWN on all |
| BBW squeeze penalty | MEDIUM → WEAK 154 | 2 | NO DIFFERENCE SHOWN |
| MicroCVD decel penalty | MEDIUM → WEAK 103 | 2 | NO DIFFERENCE SHOWN |
| TRANSITIONAL ADX penalty | MEDIUM → WEAK 220 | 2 | NO DIFFERENCE SHOWN (matches the session 1 census) |
| Stall, CVD divergence, RSI divergence, OI × CVD, burst, spread, Pass 2c conflict | < 100 per crossing | 0 | NOT READABLE |

- **Pass 2 partial upgrades are the largest single lift into MEDIUM:** 1,137 of 1,552 NY MEDIUM rows (73 %) would be WEAK without them.
- LONDON funding MEDIUM → WEAK rows beat untouched WEAK by +5.4 [+1.2, +9.4], but H1 is not significant: NO DIFFERENCE SHOWN.
- Liquidation penalty: 0 rows (`L-1`). OFI momentum: disabled in every era.

### 2.5 `D-5` — do clamps and caps bind unevenly by tier? **On the trade side, almost never.**

| Session | Tier | Trade side: rows with a binding clamp | Other side: rows with a binding clamp | Trade-side raw = regime max (cap possible) |
|---|---|---|---|---|
| NY | STRONG | 0 (0.0 %) | 116 (33.9 %) | 0 |
| NY | MEDIUM | 4 (0.3 %) | 426 (27.4 %) | 0 |
| NY | WEAK | 22 (0.6 %) | 795 (22.6 %) | 0 |

- **Caps never bind on the trade side:** 0 population rows reach regime max.
- Trade-side clamps are squeeze clamps only, and WEAK carries more. They cannot demote MEDIUM.
- **Other-side clamps bind more often at higher tiers** (mostly the funding clamp at 0, then the decel clamp). They raise the other side's floor, so they narrow the margin. The tier reads the trade side only, so they move no tier.
- Method limits: exact for the label-only mutations; RSI divergence, OI × CVD and burst use nominal magnitudes (output section 6). Parser check for funding: 0 unexplained sides.

### 2.6 `D-6` — dead and rare votes. **One dead addable point; the range is only slightly compressed.**

| Vote | Fire rate, all trading-week rows (NY / LONDON / ASIA) | Adds a point? |
|---|---|---|
| Liquidation | 0.00 / 0.00 / 0.00 % | No (penalty only; `L-1`) |
| Volume (forming bar) | 0.28 / 0.09 / 0.07 % | **Yes: the only dead addable vote** |
| Spread WIDE | 0.01 / 0.09 / 0.02 % | No (penalty only) |
| OI delta | 8.3 / 9.8 / 9.3 % | Yes (rare) |

- **Reachable score:** NY TRENDING (MaxScore 20) p50 7, p90 12, p99 15, max observed 19. Share ≥ WEAK 59.9 %, ≥ MEDIUM 16.2 %, ≥ STRONG 3.0 %. RANGE_BOUND (19) max observed 16; STRONG 0.6 %. TRANSITIONAL (15) max 13 (output section 7.2).
- **The dead Volume vote costs one point of reach** (19 of 20). Against a threshold ladder of 7 / 11 / 14, that is not what separates MEDIUM from WEAK. Range compression is not a cause.

### 2.7 `D-7` — is MEDIUM just a worse mix? **No.**

| NY re-weighting (MEDIUM to WEAK's mix) | Strata | Raw d | Re-weighted d [95 % CI] | Label |
|---|---|---|---|---|
| regime × side × ATR regime × target type | 36 | −1.0 [−2.2, +0.2] | −0.6 [−1.8, +0.5] | NO DIFFERENCE SHOWN |
| regime × hour bucket × side | 18 | −1.0 | −0.8 | NO DIFFERENCE SHOWN |
| … × ExecResolution | 36 | −1.0 | −0.6 | NO DIFFERENCE SHOWN |
| VWAP-extension tercile × side | 6 | −1.0 | −1.0 [−2.1, +0.2] | NO DIFFERENCE SHOWN |
| RSI tercile × EMA21-extension tercile × side | 14 | −1.0 | −1.0 [−2.4, +0.5] | NO DIFFERENCE SHOWN |

- Mix explains at most 0.4 bps of the NY point estimate. Covered WEAK rows: ≥ 99.6 % in every stratum set.
- ASIA: every re-weighting stays H1-only (for example −2.1 [−4.1, −0.1] by hour; H2 not significant).

### 2.8 `D-8` — is MEDIUM a late entry? **Yes in composition (CONFIRMED); not shown in outcome.**

| MEDIUM − WEAK at entry, mean | NY | LONDON | ASIA | Label |
|---|---|---|---|---|
| Distance from VWAP, ATR, signed to side | +0.6 | +1.0 | +0.6 | CONFIRMED all |
| Distance from EMA21, ATR | +1.0 | +0.8 | +0.8 | CONFIRMED all |
| RSI toward the side | +8.9 | +7.5 | +7.3 | CONFIRMED all |
| Room to the same-side 5m swing, ATR | −1.7 | −1.7 | −1.5 | CONFIRMED all |
| TTM histogram, ATR | +1.3 | +1.0 | +1.0 | CONFIRMED all |

- **Does extension cost outcome?** NY VWAP distance top − bottom tercile −1.7 [−3.5, −0.1]: DISCOVERY ONLY (H2). Every other feature and session: NO DIFFERENCE SHOWN.
- **Does extension explain the gap?** No. Re-weighted to WEAK's extension mix, NY stays at −1.0 (section 2.7 of this doc).
- Reading (hypothesis): the extension difference is mechanical. The momentum votes that raise the score fire on extended price.

### 2.9 `D-9` — which path led into MEDIUM? **Paths do not separate in both halves.**

| NY MEDIUM path | n | Net EV [95 % CI] |
|---|---|---|
| First in episode | 110 | −3.6 [−6.0, −1.0] |
| After WEAK (rising) | 655 | −3.6 [−4.6, −2.6] |
| After MEDIUM | 625 | −4.5 [−6.5, −2.7] |
| After STRONG (decaying) | 162 | −5.7 [−7.8, −3.6] |

- Rising − decaying +2.1 [−0.2, +4.2]; H2 +4.1 [+1.9, +6.1], H1 −0.7: NO DIFFERENCE SHOWN.
- **First signal in episode only** (removes autocorrelation; NY averages 12.6 signals per episode): MEDIUM − WEAK −0.5 [−3.3, +2.4], NO DIFFERENCE SHOWN. LONDON and ASIA: NOT READABLE.

### 2.10 `D-10` — context and horizon. **Not a slow signal; context does not separate.**

- MEDIUM − WEAK within each VerdictContext, NY: CONFIRMED −1.1, MOMENTUM_FADING −1.2, STRUCTURALLY_WEAK −0.6; all NO DIFFERENCE SHOWN. FLOW_UNCONFIRMED: NOT READABLE.
- **Horizon:** a longer window does not close the gap. (MEDIUM − WEAK carried) − (MEDIUM − WEAK main), NY −0.1 [−0.5, +0.2]: NO DIFFERENCE SHOWN; same in LONDON and ASIA.
- MEDIUM resolves slightly faster than WEAK, not slower: NY −0.5 min [−0.9, −0.1], DISCOVERY ONLY (H2). Median minutes to resolution, carried: NY 4 / 4, LONDON 9 / 12, ASIA 9 / 12 (MEDIUM / WEAK).

### 2.11 `D-11` — direction asymmetry. **None in NY.**

- NY MEDIUM − WEAK: LONG −1.0 [−2.4, +0.4], SHORT −0.8 [−2.7, +1.0]. LONG − SHORT within MEDIUM +0.8. All NO DIFFERENCE SHOWN.
- ASIA SHORT MEDIUM − WEAK −3.6 [−6.1, −0.7]: H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT.
- The mirror fixtures (`A82b`, session 1) already rule out a one-sided sign bug in the scoring code.

### 2.12 `D-12` — era edges. **The NY gap did not start at v65 or v66.**

| NY segment | MEDIUM n / WEAK n | d = MEDIUM − WEAK [95 % CI] |
|---|---|---|
| v51 edge (2026-07-06) → v52 | 159 / 320 | +0.0 [−2.0, +2.8] |
| v52 NY burst modifier armed (2026-07-14) → v66 | 506 / 1,106 | −1.6 [−3.3, +0.5] |
| v66 collector deploy (2026-08-10 18:36) → ATR step | 200 / 388 | −1.8 [−3.4, +1.3] |
| ATR step (2026-08-20) → end | 687 / 1,709 | −0.7 [−3.0, +1.3] |

- No segment's CI excludes 0 in NY. The point estimate turns negative at v52; the burst flag touches MEDIUM and WEAK at the same rate (6.9 % / 7.1 %), so the burst modifier does not explain it.
- v58 (ASIA volume multipliers) moved nothing measurable: the Volume vote fires on 0.0 % of ASIA rows before and after.
- v66 raised the NY OBV vote fire rate 72 % → 83 %, then 61 % after the ATR step.
- LONDON and ASIA era segments are mostly NOT READABLE (output section 13).
- ⚠ **The H1/H2 split date (2026-08-10 00:00 UTC) sits 18.6 h before the v66 deploy.** H1 against H2 is almost the same cut as before against after v66. Every "H1 only" or "H2 only" label in this doc may be an era effect.

---

## 3. Candidate fix classes (D-table rows, no values chosen)

- The brief asks for a fix class per CONFIRMED cause. **No MEDIUM-specific cause is CONFIRMED.** The rows below follow from the rank-1 null and the two CONFIRMED outcome findings. They are reserved scoring decisions, tested later with `tools/WhatIfRunner` (split-half, net EV), per `docs/medium-tier-diagnosis-brief-2026-09-16.md` §3.

| ID | Fix class | Follows from | What it would change | Values |
|---|---|---|---|---|
| `F-1` | **Do not re-cut the tier thresholds or the tier floor** | Rank 1 (`D-1`, `D-2`, `D-4`) | Nothing. Records that a cut-off change cannot create a monotone ladder | None to choose |
| `F-2` | **Re-weight or gate votes by measured predictive value per session**, starting with the momentum votes (ROC, BBW/TTM, funding) and the order-flow votes (OFI, TFI) | Rank 2, rank 5; ASIA OFI carried CONFIRMED | Scoring weights (reserved) | Not chosen |
| `F-3` | **Make the VPFR vote side-aware in NY** (the long arm is predictive; the short arm shows nothing) | NY VPFR LONG-only CONFIRMED | One vote's arms (reserved) | Not chosen |
| `F-4` | **Collapse the tier ladder for sizing** (treat WEAK, MEDIUM and STRONG as one class for Kelly and the bridge until a score ranks outcomes) | Rank 1 | Kelly sizing and the payload tier (display and bridge; reserved) | Not chosen |

- **Not a fix class, a measurement gap:** success rate rises at the top score bins while net EV does not. Whether target geometry by tier offsets it is not measured (section 5 of this doc).

---

## 4. Method

| Item | Rule |
|---|---|
| Population | The swing read population: 8,810 trading-week directional rows with valid placed levels from the v51 edge. Sessions separate |
| Outcomes | The swing read's candle walk, maker/maker 3.00 bps round trip. Main window (NY 15 min, LONDON and ASIA 45 min) primary; carried 24 h secondary. No slippage case |
| Tier and scores | The logged verdict tier and logged scores. Self-check: logged tier = tier of the logged effective score, 0 mismatches |
| Votes | Per-row signed breakdown points from `--mode rescore` (session 1). Trade-side points sum to the logged raw score on every matching row (0 exceptions). 61 non-matching rows kept; sensitivity in output section 14 changes no label in any session |
| VPFR | Verified profiles only (8,508 rows). Label-to-points geometry check: 0 disagreements |
| CIs and labels | Day bootstrap, 10,000 resamples, fixed seed; n ≥ 100 per cell; the census label rule (legend) |
| Nominal magnitudes | Read from `settings.json`; identical in all 18 era settings files v51–v68 (output section 0) |
| Instruments | `tools/ops/SwingFallbackRead` `--mode diagexport` (`MediumTierDiagnosisExport.vb`, outcomes and context per row) and `tools/ops/medium_tier_diagnosis.py` (every statistic; Python 3 standard library) |

---

## 5. What I verified, and what I did not

### Verified, and how

| Claim | How |
|---|---|
| The export's outcomes are the swing read's | NY MEDIUM main-window net EV from the export −4.19 bps = the stability read's three NY MEDIUM target-type cells combined ((939 × −4.32 + 488 × −4.32 + 125 × −2.72) ÷ 1,552) |
| The new mode changes no existing mode | `--mode swing` and `--mode census` re-run after the edit: identical to their committed outputs apart from the timestamp |
| Session 1's 64.1 % VPFR share | Reproduced by the script: 2,844 of 4,440 |
| The attribution CSV is read correctly | 56 rows carry an unquoted comma in the Class field; the reader rejoins them; every row then has 65 fields; ledger check 0 exceptions |
| The funding clamp detection | 0 matching sides whose funding points differ from the note's nominal while the final raw score is above 1 |
| The full output is deterministic | A second 10,000-resample run, diffed against the committed output: identical apart from the run timestamp (spec-back handle `H-2`) |
| Magnitudes constant across eras | Script check over `backtest_data/swing-fallback-read/rescore-eras/settings-v51.json` … `v68.json` |

### Not verified

- **Why success rate rises at the top score bins while net EV does not.** Target and stop distance by tier were not cut. This decides whether `F-4` is right or whether the ladder carries information that the target geometry spends.
- **Which clamps bind for RSI divergence, OI × CVD and burst:** nominal magnitudes, not applied points.
- **The 302 unverified VPFR rows:** excluded from the VPFR analysis only.
- **Mechanism of the NY VPFR long finding** (for example whether the POC acts as a magnet above price). The POC value is not in the attribution file.
- **Era effects versus H1/H2 effects:** not separable, because the split sits on the v66 edge.
- **LONDON and ASIA** for most cuts: NOT READABLE at n < 100.
- **Carried over without checking:** the swing read's population funnel, candle walk and trading-week rule; session 1's re-score attribution (MD5 `816853b8864456e97a5485fc8b3a2273`, as the orchestrator re-ran it on commit `85e11d5`).

---

## 6. Re-run

```
dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode rescore --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode diagexport --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
python tools/ops/medium_tier_diagnosis.py --root .
```

- About 15 minutes for the last step at 10,000 resamples; `--resamples 200` runs in about 20 s with identical point estimates.
