# Tier-order stability read (Part A): WEAK vs MEDIUM vs STRONG, by target type

**Written:** 2026-09-15 (UTC) by a scoped analysis seat. **Brief:** [`docs/tier-order-stability-check-brief-2026-09-15.md`](tier-order-stability-check-brief-2026-09-15.md), Part A only. **Part B (the forward test) was not started.**

**Vocabulary:** [`docs/DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a (success rate · gross/net breakeven rate · gross/net edge · net EV per trade).

**Instrument:** [`tools/ops/SwingFallbackRead/TierOrderStability.vb`](../tools/ops/SwingFallbackRead/TierOrderStability.vb), run as `--mode stability` of the swing read's instrument. **Full real output:** [`docs/tier-order-stability-read-2026-09-15-output.md`](tier-order-stability-read-2026-09-15-output.md) (run at 2026-09-15 17:54:39 UTC). Tables below copy rows from that output.

**Legend. Every ID in this doc comes from `docs/tier-order-stability-check-brief-2026-09-15.md`:**

| ID | Kind | Meaning |
|---|---|---|
| **C1** | Comparison (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.5) | WEAK vs MEDIUM. Difference = EV(WEAK) − EV(MEDIUM) |
| **C2** | Comparison (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.5) | STRONG vs MEDIUM. Difference = EV(STRONG) − EV(MEDIUM) |
| **C3** | Comparison (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.5) | WEAK vs STRONG. Difference = EV(WEAK) − EV(STRONG) |
| **H1 / H2** | Sub-sample (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.2) | First / second half of the trading days. Split date 2026-08-10 00:00 UTC |
| **R1 / R2** | Sub-sample (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.2) | Before / from 2026-08-20 00:00 UTC, the ATR step in `docs/d3-asia-burst-watch-read-2026-09-14.md` |
| **`none`** | `TargetCapReason` value | ATR-fallback target |
| **n/r** | Marker | Not readable: a cell holds n < 100 (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.4) |

All numbers are **net EV per trade in bps, maker/maker fees (3 bps round trip), main window** (NY 15 min, LONDON and ASIA 45 min), unless marked carried 24 h. CI = 95 % percentile CI from a bootstrap of whole trading days (section 3 of this doc).

---

## 1. Verdict

**The ordering is partly stable and nowhere significant.** Three of 18 comparisons are STABLE on the main window. Three are UNSTABLE. **No readable full-sample difference has a CI that excludes zero.**

| Session | Target set by | Comparison | **Label, main window** | Signs H1; H2; R1; R2 | Full-sample difference [CI] | Full CI excludes 0 | Label, carried 24 h (secondary) |
|---|---|---|---|---|---|---|---|
| NY | none | C1 WEAK vs MEDIUM | **UNSTABLE** | +; +; −; + | +0.5 [−0.9, +2.0] | no | STABLE |
| NY | none | C2 STRONG vs MEDIUM | **STABLE** | +; +; +; + | +2.6 [0.0, +4.9] | no | STABLE |
| NY | none | C3 WEAK vs STRONG | **UNSTABLE** | −; −; −; + | −2.0 [−4.6, +0.8] | no | STABLE |
| NY | swing | C1 WEAK vs MEDIUM | **STABLE** | +; +; +; + | +1.6 [−0.7, +3.6] | no | STABLE |
| NY | swing | C2 STRONG vs MEDIUM | **NOT READABLE** | all n/r | (−4.9, STRONG n 59) | n/r | NOT READABLE |
| NY | swing | C3 WEAK vs STRONG | **NOT READABLE** | all n/r | (+6.5, STRONG n 59) | n/r | NOT READABLE |
| LONDON | none | C1 WEAK vs MEDIUM | **UNSTABLE** | −; +; −; + | −0.9 [−4.0, +2.5] | no | UNSTABLE |
| LONDON | none | C2 STRONG vs MEDIUM | **INCONCLUSIVE** | all n/r | −1.9 [−6.9, +3.7] | no | INCONCLUSIVE |
| LONDON | none | C3 WEAK vs STRONG | **INCONCLUSIVE** | all n/r | +1.0 [−4.5, +5.8] | no | INCONCLUSIVE |
| LONDON | swing | C1 WEAK vs MEDIUM | **INCONCLUSIVE** | all n/r | +0.7 [−2.3, +3.6] | no | INCONCLUSIVE |
| LONDON | swing | C2 STRONG vs MEDIUM | **NOT READABLE** | all n/r | (STRONG n 22) | n/r | NOT READABLE |
| LONDON | swing | C3 WEAK vs STRONG | **NOT READABLE** | all n/r | (STRONG n 22) | n/r | NOT READABLE |
| ASIA | none | C1 WEAK vs MEDIUM | **STABLE** | +; +; +; + | +1.2 [−1.6, +3.8] | no | STABLE |
| ASIA | none | C2 STRONG vs MEDIUM | **NOT READABLE** | all n/r | (+6.2, STRONG n 51) | n/r | NOT READABLE |
| ASIA | none | C3 WEAK vs STRONG | **NOT READABLE** | all n/r | (−5.0, STRONG n 51) | n/r | NOT READABLE |
| ASIA | swing | C1 WEAK vs MEDIUM | **INCONCLUSIVE** | all n/r | +2.3 [−0.6, +5.4] | no | INCONCLUSIVE |
| ASIA | swing | C2 STRONG vs MEDIUM | **NOT READABLE** | all n/r | (STRONG n 12) | n/r | NOT READABLE |
| ASIA | swing | C3 WEAK vs STRONG | **NOT READABLE** | all n/r | (STRONG n 12) | n/r | NOT READABLE |

Values in brackets on NOT READABLE rows are printed for orientation only. They are not interpreted.

**Against the brief's question** (`docs/tier-order-stability-check-brief-2026-09-15.md` §1: "WEAK beats MEDIUM in all four rows"):

| Brief row | C1 WEAK vs MEDIUM | C2 STRONG vs MEDIUM |
|---|---|---|
| NY fallback | **UNSTABLE**, by one flip of **−0.03 bps** in R1 | **STABLE** (+2.6) |
| NY swing | **STABLE** (+1.6) | NOT READABLE |
| ASIA fallback | **STABLE** (+1.2) | NOT READABLE |
| ASIA swing | INCONCLUSIVE: MEDIUM n < 100 in every sub-sample | NOT READABLE |

- **"MEDIUM is the lowest tier" holds in sign where it can be read, with one exception.** STRONG > MEDIUM is STABLE in NY fallback. WEAK > MEDIUM is STABLE in NY swing and ASIA fallback.
- **The exception is LONDON fallback, which is not in the brief's table.** There WEAK < MEDIUM in H1 (−2.76 [−5.9, −0.1]) and R1 (−2.64 [−5.4, −0.2]), with CIs that exclude zero. It is the reverse in H2 (+0.84 [−4.4, +6.4]) and R2 (+1.23 [−4.7, +7.5]), whose CIs straddle zero. The early negative gap is the part with a CI clear of zero, and it did not persist.
- **NY fallback C1 is UNSTABLE by the pre-registered rule, but the flip is at zero.** Sub-sample differences are 0.00, +0.86, −0.03 and +1.22 bps. The honest read is "no WEAK-vs-MEDIUM gap in NY fallback", not "the gap reverses".
- **NY fallback C3 is UNSTABLE for the same kind of reason.** WEAK < STRONG in H1, H2 and R1 (−1.30, −2.51, −3.34). R2 is +0.20 [−4.9, +5.3], because NY fallback STRONG falls from −0.1 (R1) to −4.4 (R2).
- **The carried 24 h labels agree with the main window everywhere except NY fallback C1 and C3.** There the carried labels are STABLE, because the two near-zero flips do not recur carried (R1 C1 +0.1, R2 C3 −0.1).
- **STABLE is a weak filter here.** H1/H2 and R1/R2 are two overlapping cuts of the same 49 days, not four independent samples. With no true gap, two independent halves agree in sign half the time. Among 11 readable comparisons, several STABLE labels are expected by chance alone.
- **No result justifies a scoring, tier or `settings.json` change, so there is no D-table.** Every tier cell in NY is net-negative. No readable difference is significant.

---

## 2. Reproduction check against the swing read

- The instrument parses `docs/swing-vs-fallback-target-read-2026-09-15-output.md` section 6 (main window) and section 8 (carried 24 h). It compares every row before any split.
- **Result: 90 of 90 rows pass. n is equal on every row. Largest |delta| in net EV per trade: 0.049 bps** (the reference prints 0.1 bps). The rule in `docs/tier-order-stability-check-brief-2026-09-15.md` §0 is 0.1 bps.
- The rows cover `none`, `swing` and `hvn` × ALL, S+M, STRONG, MEDIUM and WEAK × both modes. The full table is in output section 1.
- ⚠ **The table in `docs/tier-order-stability-check-brief-2026-09-15.md` §1 differs from the swing read by 0.1 bps in two cells.** NY fallback MEDIUM reads −4.4 in `docs/tier-order-stability-check-brief-2026-09-15.md` §1 against −4.3 in the swing read output. NY swing WEAK reads −2.7 against −2.8. The brief derived those cells from ALL and S+M rows, and rounding explains the gap. This read reproduces the output file, not the brief's derived cells.
- **The default mode is unchanged.** A default-mode run after the change matches the committed swing read output byte for byte, apart from the run timestamp.

---

## 3. Method: split date, CI method, and interpretations fixed before the first run

**Split date: 2026-08-10 00:00 UTC.**

| Sub-sample | Trading days | Span |
|---|---|---|
| H1 | 24 | 2026-07-07 to 2026-08-07 |
| H2 | 25 | 2026-08-10 to 2026-09-11 |
| R1 | 32 | 2026-07-07 to 2026-08-19 |
| R2 | 17 | 2026-08-20 to 2026-09-11 |

- The population is the swing read's, unchanged: 8,810 signals, 49 trading days.
- By coincidence, H2 starts on the day of the v66 OBV scoring edge (2026-08-10 18:36 UTC in `docs/swing-vs-fallback-target-read-2026-09-15.md` §5.2). The split rule produced this date. It was not chosen.

The design is `docs/tier-order-stability-check-brief-2026-09-15.md` §2, unchanged. The brief left the details below open. **I wrote this table to this file before the first stability run.** Git cannot prove the order, because the table and the results land in one commit.

| Open detail in `docs/tier-order-stability-check-brief-2026-09-15.md` §2 | Rule used |
|---|---|
| **Trading day** | The UTC date of the signal. The day list is every distinct date in the whole 8,810-signal population (all sessions, all target types) |
| **Odd number of trading days** (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.2 chronological halves) | H1 = the first ⌊D/2⌋ days; H2 = the remaining days. The split date is the date of day index ⌊D/2⌋ (0-based). With D = 49 days the halves hold 24 and 25 days, so the odd-day rule did not need to fire |
| **R1 / R2** (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.2 regime) | R1 = signal timestamp before 2026-08-20 00:00 UTC; R2 = on or after it. As written in the brief |
| **Bootstrap unit and draw** (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.3) | One group = session × target type × sub-sample × outcome mode. The day pool = the days holding at least one signal of that group, any tier. Each resample draws that many days with replacement and keeps every signal on each drawn day. All three tiers and all three differences come from the same draws, so the tier-to-tier correlation inside a day is kept |
| **Resamples and CI** | 10,000 resamples per group (brief minimum 2,000). Percentile 95 % CI (nearest rank at 2.5 % and 97.5 %). `System.Random`, seed 20260915 + group index, so a re-run is identical. A resample in which a tier draws zero signals is skipped for that tier and its comparisons, and counted |
| **Point estimates** | The plain cell mean, from the swing read's own `Compute` function. The bootstrap supplies only the CIs and the SE |
| **NOT READABLE against INCONCLUSIVE** (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.4, §2.6) | **NOT READABLE** = one of the two cells holds n < 100 in the FULL sample. Then no sub-sample can be readable either. **INCONCLUSIVE** = readable in the full sample, but fewer than three readable sub-samples, or H1 or H2 unreadable |
| **Overlap of UNSTABLE and INCONCLUSIVE** (`docs/tier-order-stability-check-brief-2026-09-15.md` §2.6) | The brief's rules overlap when two readable sub-samples disagree in sign but fewer than three are readable. **UNSTABLE takes precedence.** An observed sign flip is evidence of instability at any count. Order: NOT READABLE → UNSTABLE → STABLE → INCONCLUSIVE. **It did not fire:** all three UNSTABLE comparisons have four readable sub-samples |
| **Sign flip** | At least two readable sub-samples whose differences are not all strictly positive or all strictly negative. A difference of exactly 0 counts as a flip |
| **Which mode sets the label** | The main window (primary, `docs/tier-order-stability-check-brief-2026-09-15.md` §2.1). The carried 24 h label is printed beside it as secondary and never overrides it |
| **Forward sample for Part B candidates** (`docs/tier-order-stability-check-brief-2026-09-15.md` §4) | For each STABLE comparison: full-sample difference d and bootstrap SE (the SD of the 10,000 resampled differences). Required signals in the two cells together = (n_A + n_B) × (z × SE ÷ abs(d))². z = 1.96 gives a 50 % chance that the forward estimate reaches significance if d is the true gap. z = 2.80 gives 80 % power at two-sided 5 %. Forward trading days = required signals ÷ the R2 rate (signals of both cells per R2 trading day). This assumes the SE scales with 1/√n and the tier mix stays the same |
| **HVN target rows** | Full sample, main window, per tier, as context only. No label and no sub-sample (output section 8) |
| **Reproduction gate** (`docs/tier-order-stability-check-brief-2026-09-15.md` §0) | The instrument parses the swing read's committed output, section 6 (main window) and section 8 (carried 24 h). Every row must match n exactly and net EV per trade within 0.1 bps. On any failure the instrument writes the check and exits with code 3 before any split |

- **No rule was mechanically impossible.** No sub-sample is empty. No bootstrap skip fell inside a readable comparison: 18,339 skips in total, all in cells with n < 100.
- **One display change after the first run:** a 2-decimal difference column in output sections 6 and 7, so the near-zero flips are visible. It changes no statistic or label. Run 1 and run 2 match line for line apart from that column.

---

## 4. Per-sub-sample tables, main window

Cells: n · net EV per trade [CI]. Differences to 2 dp come from output section 6. Carried 24 h tables are output sections 5 and 7.

### 4.1 NY, `none` (ATR fallback)

| Sub-sample | STRONG | MEDIUM | WEAK | C1 WEAK − MEDIUM | C2 STRONG − MEDIUM | C3 WEAK − STRONG |
|---|---|---|---|---|---|---|
| FULL | 259 · −1.8 [−4.9, +1.2] | 939 · −4.3 [−6.0, −2.6] | 1654 · −3.8 [−4.8, −2.8] | +0.53 [−0.9, +2.0] | +2.56 [0.0, +4.9] | −2.04 [−4.6, +0.8] |
| H1 | 101 · −2.7 [−5.1, 0.0] | 366 · −4.0 [−5.8, −2.0] | 657 · −4.0 [−5.1, −3.0] | 0.00 (+) [−1.8, +1.8] | +1.30 [−1.3, +4.2] | −1.30 [−3.9, +1.2] |
| H2 | 158 · −1.2 [−6.4, +3.0] | 573 · −4.5 [−7.0, −2.1] | 997 · −3.7 [−5.3, −2.1] | +0.86 [−1.1, +3.1] | +3.37 [−0.8, +6.5] | −2.51 [−6.1, +2.1] |
| R1 | 159 · −0.1 [−3.6, +3.1] | 510 · −3.4 [−5.3, −1.4] | 866 · −3.5 [−4.8, −2.2] | **−0.03** [−1.5, +1.5] | +3.30 [+0.4, +5.6] | −3.34 [−5.7, −0.5] |
| R2 | 100 · −4.4 [−9.7, +0.7] | 429 · −5.4 [−8.3, −2.8] | 788 · −4.2 [−5.7, −2.7] | +1.22 [−1.2, +4.0] | +1.03 [−3.4, +5.7] | **+0.20** [−4.9, +5.3] |

- H1 C1 prints 0.00 at 2 dp; its sign is positive (verdict table, output section 3).

### 4.2 NY, `swing`

| Sub-sample | STRONG | MEDIUM | WEAK | C1 WEAK − MEDIUM |
|---|---|---|---|---|
| FULL | 59 · n/r | 488 · −4.3 [−6.2, −2.3] | 1211 · −2.8 [−4.4, −1.1] | +1.56 [−0.7, +3.6] |
| H1 | 25 · n/r | 231 · −4.6 [−7.1, −1.9] | 527 · −2.5 [−5.3, +0.3] | +2.11 [−1.0, +5.0] |
| H2 | 34 · n/r | 257 · −4.1 [−6.7, −1.1] | 684 · −3.0 [−4.9, −1.1] | +1.09 [−2.1, +3.8] |
| R1 | 40 · n/r | 292 · −4.4 [−6.6, −2.0] | 633 · −2.1 [−4.5, +0.3] | +2.35 [−0.5, +4.9] |
| R2 | 19 · n/r | 196 · −4.2 [−7.5, −0.7] | 578 · −3.5 [−5.7, −1.5] | +0.65 [−2.9, +3.8] |

- C2 and C3 are NOT READABLE: STRONG swing holds n 59 in the full sample. As the brief expected, STRONG swing is unreadable everywhere.

### 4.3 LONDON, `none`

| Sub-sample | STRONG | MEDIUM | WEAK | C1 WEAK − MEDIUM | C2 STRONG − MEDIUM | C3 WEAK − STRONG |
|---|---|---|---|---|---|---|
| FULL | 106 · −1.3 [−5.9, +4.4] | 321 · +0.6 [−2.3, +3.4] | 464 · −0.3 [−3.1, +2.4] | −0.86 [−4.0, +2.5] | −1.9 [−6.9, +3.7] | +1.0 [−4.5, +5.8] |
| H1 | 64 · n/r | 154 · +1.2 [−2.8, +5.2] | 218 · −1.6 [−4.8, +1.3] | **−2.76** [−5.9, −0.1] | n/r | n/r |
| H2 | 42 · n/r | 167 · +0.1 [−4.1, +3.9] | 246 · +0.9 [−3.5, +5.0] | +0.84 [−4.4, +6.4] | n/r | n/r |
| R1 | 73 · n/r | 181 · +0.4 [−3.0, +3.9] | 252 · −2.2 [−5.1, +0.4] | **−2.64** [−5.4, −0.2] | n/r | n/r |
| R2 | 33 · n/r | 140 · +0.8 [−4.0, +5.0] | 212 · +2.1 [−2.8, +6.6] | +1.23 [−4.7, +7.5] | n/r | n/r |

- C2 and C3 are readable only in the full sample (STRONG n 106), so they are INCONCLUSIVE.

### 4.4 ASIA, `none`

| Sub-sample | STRONG | MEDIUM | WEAK | C1 WEAK − MEDIUM |
|---|---|---|---|---|
| FULL | 51 · n/r | 251 · −3.5 [−5.7, −1.4] | 432 · −2.4 [−4.9, 0.0] | +1.17 [−1.6, +3.8] |
| H1 | 16 · n/r | 117 · −4.7 [−7.6, −2.0] | 162 · −2.4 [−6.0, +0.9] | +2.32 [−0.9, +5.2] |
| H2 | 35 · n/r | 134 · −2.5 [−5.7, +0.6] | 270 · −2.3 [−5.7, +0.9] | +0.16 [−4.4, +4.1] |
| R1 | 17 · n/r | 120 · −4.7 [−7.6, −2.0] | 171 · −2.7 [−6.1, +0.6] | +1.96 [−1.1, +4.8] |
| R2 | 34 · n/r | 131 · −2.5 [−5.8, +0.6] | 261 · −2.2 [−5.7, +1.1] | +0.37 [−4.2, +4.4] |

- The ASIA fallback C1 gap shrinks from +2.3 (H1) to +0.2 (H2). MEDIUM improved; WEAK did not move. The sign held, the size did not.

### 4.5 LONDON `swing` and ASIA `swing`

| Session | FULL: MEDIUM | FULL: WEAK | FULL C1 | Sub-sample MEDIUM n (H1 / H2 / R1 / R2) |
|---|---|---|---|---|
| LONDON | 136 · −2.9 [−5.7, −0.4] | 484 · −2.1 [−4.8, +0.1] | +0.7 [−2.3, +3.6] | 49 / 87 / 57 / 79 |
| ASIA | 141 · −3.8 [−7.2, −0.6] | 693 · −1.5 [−3.9, +0.7] | +2.3 [−0.6, +5.4] | 55 / 86 / 65 / 76 |

- MEDIUM swing is below the floor in every sub-sample in both sessions, so C1 is INCONCLUSIVE. STRONG swing (n 22 and 12) makes C2 and C3 NOT READABLE.

---

## 5. Candidates for Part B

**Only the three main-window STABLE comparisons qualify** (`docs/tier-order-stability-check-brief-2026-09-15.md` §4). The R2 rate is signals of both cells per trading day from 2026-08-20 (17 trading days).

| Session | Target set by | Comparison | d bps | Bootstrap SE bps | R2 rate per trading day | z 1.96: signals · trading days | z 2.80 (80 % power): signals · trading days |
|---|---|---|---|---|---|---|---|
| NY | none | C2 STRONG vs MEDIUM | +2.56 | 1.26 | 31.1 | 1,111 · **36** | 2,266 · **73** |
| NY | swing | C1 WEAK vs MEDIUM | +1.56 | 1.08 | 45.5 | 3,158 · **69** | 6,445 · **142** |
| ASIA | none | C1 WEAK vs MEDIUM | +1.17 | 1.38 | 23.1 | 3,636 · **158** | 7,419 · **322** |

- **NY fallback STRONG vs MEDIUM is the only candidate a forward test can reach in a quarter:** about 7 trading weeks for the point estimate to reach significance, and about 15 weeks for 80 % power.
- NY swing WEAK vs MEDIUM needs about 14 to 28 trading weeks. ASIA fallback WEAK vs MEDIUM needs about 32 to 64 trading weeks. That is not a practical forward test.
- ⚠ **These are lower bounds on the sample, not estimates.** Each d was chosen because it looked stable in this same data, so its size is biased upward (winner's curse). A forward gap that is smaller will need more signals.
- ⚠ **The day counts assume the R2 signal rate continues.** A settings change that moves the tier mix or the signal rate breaks the conversion.
- Carried 24 h also labels three more comparisons STABLE: NY fallback C1 (needs about 320 trading days at z 1.96), NY fallback C3 (57) and NY swing C1 (57). They are secondary and are not Part B candidates under the brief. Output section 9 has the full table.

**Model / effort for Part B, if the trader orders it:** Opus, medium.
- **Why:** the comparisons and the instrument now exist. The judgment work is locking the forward window and the stop rule before any new data arrives.
- **Where it will slip:** re-selecting comparisons after looking at the forward data, and pooling the pre-registered NY fallback C2 with the weaker candidates.

---

## 6. What I verified, and what I did not

### Verified, and how

| Claim | How |
|---|---|
| The per-signal outcomes reproduce the swing read | The instrument compares 90 of 90 rows against `docs/swing-vs-fallback-target-read-2026-09-15-output.md` sections 6 and 8: n equal, largest absolute delta 0.049 bps |
| The swing read's default mode is unchanged | A default-mode run after the change matches the committed output byte for byte, apart from the run timestamp |
| The stability output is deterministic | Run 1 and run 2 match line for line. The only difference is the added 2-decimal column |
| The pasted output matches the committed code | `docs/tier-order-stability-read-2026-09-15-output.md` is the run-2 file, byte-identical to run 2's console output. Run 2 was built from the committed `TierOrderStability.vb` |
| No bootstrap skip affects a readable comparison | Counted by the instrument: 0 skips inside readable comparisons |
| The UNSTABLE-precedence interpretation changed no label | All three UNSTABLE comparisons have four readable sub-samples |
| `settings.json` and engine `.vb` files were not touched | `git status` before the commit shows only the two tool files and the two docs. The instrument loads a hash-checked copy of `settings.json` |

### Not verified

- **Independence of the sub-samples.** H1/H2 and R1/R2 overlap. The STABLE rule counts them as four checks, but they carry about two cuts' worth of evidence.
- **Day-level independence.** The bootstrap treats trading days as independent. Adjacent days in one regime are likely correlated, so the CIs may be too narrow.
- **Multiple comparisons.** Eleven readable comparisons, with no correction. The count of STABLE labels expected by chance was not computed.
- **Cause of the LONDON fallback C1 reversal.** H1 and R1 both cover the pre-collector dev runs and the early collector era. I did not check whether the settings version or the process mix explains the change.
- **Cause of the NY fallback STRONG drop in R2** (−0.1 → −4.4). I did not investigate it.
- **The bootstrap CIs differ slightly from the swing read's cluster-robust CIs** (for example NY fallback STRONG +1.2 against +1.4 at the upper bound). The point estimates are identical. I did not reconcile the two methods beyond that.
- **Execution:** the same limits as `docs/swing-vs-fallback-target-read-2026-09-15.md` §6 apply. Fills at the exact level, no slippage (trader-ruled 2026-09-15), same-bar ambiguity counts as a stop.
- **Carried over without checking:** the ATR step date, from `docs/d3-asia-burst-watch-read-2026-09-14.md`.

---

## 7. Re-run

```
dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode stability --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
```

- Reads the candle and funding cache in `backtest_data/swing-fallback-read/` (gitignored). The report writes to `backtest_data/swing-fallback-read/tier-order-stability-output.md`.
- `--reference <path>` overrides the reproduction reference. The exit code is 3 if the reproduction fails.
