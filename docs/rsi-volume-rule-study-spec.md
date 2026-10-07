# RSI and volume rule study — pre-registration (2026-10-07 UTC)

**Status:** PRE-REGISTERED, NOT RUN. No outcome has been computed. Written at `da5b047` (HEAD at writing); settings v69.
**Asked by:** the trader, 2026-10-07: *"The 2 trading rules: RSI 60/40 and the 3x SMA(9) - is allowed to be changed if it means a more accurate analysis … What is the most accurate changes/replacements that can be made to those 2 rules?"*, then *"Yes to both, write the pre-registration now"*.
**Class:** analysis only, read-only on the dev history stores. Auto-proceed class. **Every engine change it could lead to is a scoring change, reserved to the trader, and after 2026-11-25.**

**Rulings this study builds on (all 2026-10-07, trader):** the volume vote follows the engine's dynamic high threshold, on closed bars (`ICR-1` (a) and the volume design point); the RSI levels on 3-minute bars are mapped to the state 60/40 marks on 1-minute bars (`ICR-6` RSI half = (a)); measure before a scoring slot (`ICR-2` (a)). Source: [`indicator-calibration-review-adversarial-2026-10-07.md`](indicator-calibration-review-adversarial-2026-10-07.md) ruling box and §1b.

---

## 0. What is tested, and what is not

- **Tested:** whether each candidate rule picks direction well enough to earn money at a fixed, engine-like geometry, against the engine's mechanism-fixed rule (V0, R0) and against its own direction condition without the indicator.
- **Not tested:** the engine's structural-first placed levels, its score, or its verdicts. The geometry is the ATR fallback (§4.7), as in [`liq-tradeflow-flip-study-spec.md`](liq-tradeflow-flip-study-spec.md) §4.8. The engine's own placed geometry is net-negative at present (`medium-tier-diagnosis-read-2026-09-17.md`), and the audit stop-geometry fixes come first in the merged order (`indicator-calibration-review-adversarial-2026-10-07.md` §4, step 4).
- **Not tested:** order-flow replacements for volume (taker imbalance). They re-vote TFI and CVD, which already vote in Step 2: double counting (`trader-profile.md` §4).

## 1. Model and effort

| Session | Content | Model · effort |
|---|---|---|
| **1** | Bar builder, ported indicators, the parity check (§6, `E-1`), the frozen thresholds (§4.4, §4.5), outcome-blind counts and power (§6). **Opens no outcome** | Opus 5.5 · high |
| **2** | The outcome run (§5), once session 1's STOP gates (§7) pass | Opus 5.5 · high |

- **Why that tier:** a derivation on pre-registered rules where a wrong number is hard to notice. The statistics code is copied, not written (§4.8), and the bar builder has a template in `tools/ops/liq_tradeflow_counts.py`.
- **Where it slips:**
  1. **Building bars that include the in-progress bar.** Every signal uses completed bars only. The bar that closes at T is the signal bar; entry is the first trade at or after T.
  2. **Look-ahead in a trailing baseline.** V1's time-of-day median and V2's percentile use prior bars only.
  3. **Pooling sessions or resolutions.** Every statistic is per session at its own resolution.
  4. **Quoting H1 as a finding.** The label rule (§4.8) decides; an H1-only result is DISCOVERY ONLY.
- **Escalation trigger:** V0's ratio-only full-fire share outside [0.5×, 2×] of `candle-store-derivations-2026-07-31.md` §3.1 (NY 2.16 %, ASIA 3.17 %, LONDON 3.27 %) — STOP and reconcile before any outcome is opened. Also: parity `E-1` failing.

## 2. IDs used here

| ID | Source and kind | Meaning |
|---|---|---|
| `V0`, `V1`, `V2` | This doc §4.4, volume candidate rules | V0 = engine rule on closed bars · V1 = time-of-day relative volume · V2 = trailing percentile |
| `R0`, `R1`, `R2` | This doc §4.5, RSI candidate rules | R0 = 60/40 (55/45) mapped across resolution · R1 = per-session terciles · R2 = midline 50 |
| `VOL-H1`…`VOL-H3`, `RSI-H1`…`RSI-H3` | This doc §5, hypotheses | Prefixes checked free with `git grep` 2026-10-07 |
| `RVR-1`…`RVR-9` | This doc §9, design choices auto-proceeded | Prefix checked free 2026-10-07 |
| `ICR-1`, `ICR-2`, `ICR-6` | Decisions in `indicator-calibration-review-adversarial-2026-10-07.md` §1 | The forming-bar spec scope · measure first · the ADX/RSI read framing |
| `F-2` | Fix class in `medium-tier-diagnosis-read-2026-09-17.md` §3 | The vote-value study. ⚠ Not `F2` in `DeribitIndicatorProject.md` §15 |
| `H-n` / `E-n` | This doc §6 | Handle the reader can run · evidence they cannot |

## 3. The mechanism (code read at `da5b047`)

| Item | Engine today | Where |
|---|---|---|
| Volume ratio | `candlesExec.Last().Volume / SMA(9)` — the last candle is the in-progress bar | `UI/MainForm_Analysis.vb:266-269` |
| Volume thresholds | Last 100 completed bars: high = clamp((mean + 2·sd)/mean, 2.0, 6.0), mid = clamp((mean + 1·sd)/mean, 1.5, 4.0); sd < 0.05·mean ⇒ static 3.0 / 2.0; then × session multipliers (NY 1.15 / 1.10, ASIA and LONDON 1.00) | `DynamicNorms.vb:36-60`; `settings.json` `indicators.Volume`, `session_volume` |
| Volume vote | Long: ratio ≥ high ∧ ROC > 0 ∧ price > VWAP. Short mirrored. Partial: mid ≤ ratio < high | `Core/ScoringEngine_Calculate_Scoring.vb:215-219` |
| RSI | RSI(9), Wilder, on `candlesExec` | `UI/MainForm_Analysis.vb:264`, `Core/Indicators_Momentum.vb:127-145` |
| RSI vote | Long: RSI > 60; partial long 55 < RSI ≤ 60. Short mirrored (40, 45) | `Core/ScoringEngine_Calculate_Scoring.vb:182-185` |
| ROC | ROC(9) = (close / close₋₉ − 1) × 100 | `Core/Indicators_Momentum.vb:282-283` |
| VWAP | Dual anchor 00:00 and 13:30 UTC, typical price (H+L+C)/3 × volume | `Core/Indicators_Volatility.vb:58-60` |
| Execution resolution | NY (13–23 UTC) 1 min; ASIA (0–7) and LONDON (8–12) 3 min | `settings.json` `session_volume.sessions` |

## 4. Pre-registered design

### 4.1 Data

- **Stores:** `C:\DeribitData\history-2023-2024\` and `C:\DeribitData\history\` (BTC-PERPETUAL trades from the history host, `trade_seq`-complete; comparisons #1–#3 PASS). `Amount` is USD.
- **Span:** 2023-01-01 00:00 → **2026-10-06 00:00 UTC exclusive** (the dev store's last settled day is 2026-10-05). The seam at 2025-01-01 joins two stores; a signal whose lookback crosses it is kept only if both stores cover the minutes (counted).
- **Scope:** weekdays (UTC Monday–Friday) by the signal bar's close time, the house scope. Session by the UTC hour of the signal bar's close.

### 4.2 Bars

- 1-minute and 3-minute OHLCV on the UTC grid (3-minute bars aligned to :00, :03, …), built from trades: open = first trade price, close = last, volume = Σ `Amount`. A bar with no trade: OHLC = previous close, volume 0.
- **A gap:** any run of more than 5 consecutive empty 1-minute bars. A signal whose 250-bar lookback, or whose outcome window, touches a gap is dropped (counted per session).
- Lookback per signal: the last 250 completed bars at its resolution (the engine fetches 250 1-minute candles).

### 4.3 Direction conditions and episodes

- **Volume direction:** long when ROC(9) > 0 ∧ close > VWAP; short mirrored (the engine's vote condition).
- **RSI direction:** the rule's own side.
- **Episodes:** a signal is kept only if no same-side signal of the same rule fired in the previous W minutes (W = the session's outcome window, §4.7). Rows are never counted as trades (`DeribitIndicatorProject.md` §5a rule 3a).

### 4.4 Volume candidates (full vote only; partials descriptive)

| Rule | Fires when | Threshold |
|---|---|---|
| **V0** | closed-bar ratio = volume / SMA(9) of the 9 bars ending at the signal bar, ≥ the engine's dynamic high threshold computed over the 100 bars BEFORE the signal bar, × the session multiplier | Engine formula (§3), at v69 values |
| **V1** | volume / median volume of the same bar-of-day slot over the previous 20 weekdays, ≥ k₁ | k₁ per session set so V1's fire share on **H1** days equals V0's fire share on H1 days; frozen |
| **V2** | percentile rank of the bar's volume within the previous 100 bars ≥ k₂ | k₂ per session, share-matched the same way; frozen |
| Control **V-ctl** | the volume direction condition holds and the candidate does NOT fire | — |

Share matching makes V1 and V2 fire on as many bars as V0, so the comparison is about WHICH bars, not how many.

### 4.5 RSI candidates

| Rule | Long / short (partial descriptive) | Levels |
|---|---|---|
| **R0** | RSI(9) > U / < L | NY (1 min): U = 60, L = 40 (partials 55 / 45). ASIA and LONDON (3 min): the levels whose exceedance share over the session's 3-minute bars equals the share of the same minutes with 1-minute RSI(9) > 60 (< 40, > 55, < 45), computed per session on **H1** days, frozen. This is the `ICR-6` (a) mapping: a time-matched quantile match, no outcome |
| **R1** | RSI(9) above its upper tercile / below its lower tercile | Per session and resolution, on H1 days, frozen |
| **R2** | RSI(9) > 50 / < 50 | Fixed |
| Control **R-ctl** | ROC(9) points the side (ROC > 0 long, < 0 short) and the candidate does NOT fire | — |

R-ctl answers the overlap question: does RSI add direction skill beyond ROC, which `trader-profile.md` §3 keeps for "different timing roles"?

### 4.6 Population counts

Every rule × session × half: signals, episodes, dropped (gap, seam, warm-up). Printed in session 1 before any outcome.

### 4.7 Outcome — defined, NOT computed

Copied from [`liq-tradeflow-flip-study-spec.md`](liq-tradeflow-flip-study-spec.md) §4.8 (`TFS-8`), unchanged:

| Item | Rule |
|---|---|
| Entry | The first trade with timestamp ≥ the signal bar's close; its price |
| ATR | ATR(7), Wilder, over the last 50 completed bars at the session's resolution |
| Levels | Target = m × ATR: NY 1.75, LONDON 2.0, ASIA 1.25; stop = 1.6 × ATR (`scoring.atr_target_multiplier`, `structural_levels.sessions.*.fallback_target_atr_mult`, `scoring.atr_stop_multiplier`, v69) |
| Window W | NY 15 min; ASIA and LONDON 45 min |
| Walk | Trade by trade; first target or stop touch wins; timeout marks at the last trade before the window end |
| Fee | 3 bps round trip (`scoring.trade_costs` maker/maker, v69). Pinned |
| Metric | **Net EV per trade, bps** (`DeribitIndicatorProject.md` §5a). Success rate, breakeven rates and edges explain it. Descriptive: the fixed-horizon net return at W |

### 4.8 Statistics

| Item | Rule |
|---|---|
| CI | 95 % percentile bootstrap of whole UTC days, seed **20261008**, **10,000** resamples. A difference between two rules resamples days once and recomputes both means |
| Code | Copy `boot`, `label`, `holm`, `boot_p` from `tools/ops/burst_outcome_read.py`. Do not re-derive |
| Readable | n ≥ 100 episodes in every group a statistic uses |
| Halves | First ⌊D/2⌋ UTC weekdays of the span = H1, the rest = H2. D is counted after gap drops and printed |
| Multiplicity | Holm within each family (volume: 9 tests; RSI: 9 tests), familywise α 0.05, on the full-span p values |
| Label | The census rule, first match wins: NOT READABLE → CONFIRMED → "NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0" → H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT → H1 FINDING, H2 CONTRADICTS OR NOT READABLE → DISCOVERY ONLY (H2) → NO DIFFERENCE SHOWN ([`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §5) |
| Splits (descriptive, never labelled) | Side · year · daylight-saving regime (EU and US summer vs winter; sessions are fixed UTC) · ATR tercile · partial votes |

## 5. Hypotheses (each per session: ASIA, LONDON, NY)

| ID | Statistic | What a CONFIRMED result means |
|---|---|---|
| `VOL-H1` | Net EV: V0 − V-ctl | Closed-bar volume adds direction skill over its own ROC + VWAP condition |
| `VOL-H2` | Net EV: V1 − V0 | Time-of-day relative volume picks better bars than the engine's rule |
| `VOL-H3` | Net EV: V2 − V0 | A trailing percentile picks better bars than the engine's rule |
| `RSI-H1` | Net EV: R0 − R-ctl | RSI at the mapped 60/40 adds skill over ROC alone |
| `RSI-H2` | Net EV: R1 − R0 | Tercile levels beat the mapped 60/40 |
| `RSI-H3` | Net EV: R2 − R0 | The midline beats the mapped 60/40 |

**What each result changes (stated before any outcome):**

| Result | Recommendation to the trader (a scoring change, reserved) |
|---|---|
| `VOL-H2` or `VOL-H3` CONFIRMED positive in a session | Replace V0 with that rule in that session, inside the forming-bar spec (`ICR-1`) |
| Neither, but `VOL-H1` CONFIRMED positive | Keep V0 (the ruled design point). The volume vote has measured value |
| `VOL-H1` NO DIFFERENCE SHOWN | Keep V0; record that closed-bar volume adds nothing over its direction condition at this geometry; its weight goes to `F-2` |
| `VOL-H1` CONFIRMED negative | A finding for the trader: the volume vote, fixed, would cost money at this geometry |
| `RSI-H2` or `RSI-H3` CONFIRMED positive in a session | Replace R0's levels in that session |
| `RSI-H1` CONFIRMED negative in a session | Propose dropping the RSI entry vote in that session (R-null). ⚠ Only on CONFIRMED negative: "no difference shown" is not evidence against the vote |
| Otherwise | Keep R0 (the ruled `ICR-6` (a) mapping) |

## 6. Session 1 handles (to be run and pasted by session 1)

- **`H-1`:** `python -I tools/ops/rsi_volume_rule_counts.py --stores C:\DeribitData\history-2023-2024 C:\DeribitData\history --cut 2026-10-06` — bars, gaps, frozen thresholds (k₁, k₂, the R0 3-minute levels, R1 terciles), counts per rule × session × half, power (MDE80 per hypothesis at the observed n, σ proxy from the outcome-blind ATR scale as in the power block (output section 6) of `tools/ops/a4_liq_ofi_counts.py`). **Opens no outcome.**
- **`E-1` (parity, evidence):** on the live book 2026-09-24 → 2026-10-07 (fetch `aws_fetch/20261007-180541`), rebuild the engine's candle list as of each row's `Timestamp` (completed bars plus the forming stub from trades up to that instant) and compute RSI(9), ROC(9), `VolumeRatio` and VWAP with the ported code. PASS: ≥ 99 % of rows within 0.5 RSI points, 0.01 ROC, 2 % of `VolumeRatio`, 0.5 USD of VWAP. ⚠ The live engine reads venue candles over WS, which undercount 3-minute volume by about 2.5 % (`DeribitIndicatorProject.md` §12, WS undercount row); a volume mismatch near that size is that, not a port error — report it.

## 7. STOP conditions (session 1, before session 2 opens outcomes)

1. `E-1` fails.
2. V0 ratio-only full-fire share outside [0.5×, 2×] of `candle-store-derivations-2026-07-31.md` §3.1.
3. Any hypothesis NOT READABLE in every session → report; do not run session 2 for that family.
4. Gap drops above 2 % of weekday bars in any session-year → report the hours first.

## 8. Run pins

Settings values from v69 (listed in §3 and §4.7) · seed 20261008 · 10,000 resamples · span and cut §4.1 · code copied per §4.8 · session 2 runs the committed `tools/ops/rsi_volume_rule_read.py` once; a re-run after a bug fix reruns everything and is recorded.

## 9. Design choices auto-proceeded (one line each; all reversible by one revert of this doc)

| ID | Choice | Options considered | Why |
|---|---|---|---|
| `RVR-1` | ATR-fallback geometry, not the placed ladder | placed ladder replay · fixed-horizon return only | The placed ladder needs swings, HVNs and the POC gate re-derived offline (the what-if runner's job); fixed horizon ignores the stop. The `TFS-8` geometry is the house precedent. Step 3: the ladder replay is not wrong but answers a different question (geometry, not the rule) |
| `RVR-2` | Span 2023-01-01 → 2026-10-05, halves by day count | 2025+ only · 2023–2024 as a separate replication | More episodes per cell; the census label already demands H1 and H2 agree |
| `RVR-3` | Weekdays only | all days | House scope for engine votes |
| `RVR-4` | Share-matched V1 and V2 thresholds | free thresholds | Compares which bars, not how many; no tuning on outcome |
| `RVR-5` | R1 = terciles, R2 = midline | percentile grids | Two fixed, stated rules; a grid search would be a fit |
| `RVR-6` | Full votes primary, partials descriptive | partials primary | The full vote is the design point the trader named |
| `RVR-7` | Controls = direction condition without the indicator | vs zero | Net EV is negative in most cells at this geometry; vs zero would test the geometry, not the indicator |
| `RVR-8` | Episode rule = no same-side fire in the previous W minutes | strict consecutive rows | W is the trade's own life |
| `RVR-9` | Thresholds and the R0 mapping frozen on H1 days | full span | No H2 data, even outcome-blind, shapes a level |

## 10. Pre-registration record — what the author saw

- **No outcome of this design was computed or read.** No bar was built for this study.
- **Read for the design:** `DynamicNorms.vb:30-75`; `Core/ScoringEngine_Calculate_Scoring.vb:164-219`; `Core/Indicators_Momentum.vb:127-145`, `:282-283`; `Core/Indicators_Volatility.vb:58-60`; `settings.json` (`indicators.Volume`, `indicators.RSI`, `session_volume`, `scoring.trade_costs`); `candle-store-derivations-2026-07-31.md` §3.1 (fire rates, no outcomes); `liq-tradeflow-flip-study-spec.md` §4.8–§4.9 and §11; `medium-tier-diagnosis-read-2026-09-17.md` §2.
- ⚠ **Prior outcome knowledge that overlaps the span:** the medium-tier read (live book 2026-07 → 2026-09) found the NY ROC vote's agree − oppose net EV at −3.3 bps (DISCOVERY ONLY) and momentum votes leaning negative in NY. ROC is part of the volume direction condition and R-ctl. The liquidation × trade-flow study read outcomes on 2023-01 → 2026-07 for a different event population. Neither read RSI or volume rules.
- One `head -3` of `trades_2025-01.csv` printed three trades of 2025-01-01 00:00 UTC (price 93,445). No return computed.

## 11. What I did not verify

- That 250 bars of lookback suffice for RSI(9) to converge to the engine's value (Wilder smoothing from a 9-bar seed). `E-1` tests it.
- The per-session episode counts, so the power is unknown; session 1 computes it.
- How far the R0 3-minute levels will sit from 60/40.
