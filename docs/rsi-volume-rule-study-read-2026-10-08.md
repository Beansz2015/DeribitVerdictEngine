# RSI and volume rule study — the outcome read (2026-10-08 UTC)

**Spec of record:** [`rsi-volume-rule-study-spec.md`](rsi-volume-rule-study-spec.md). **Session 1 and the rulings:** [`rsi-volume-rule-study-session1-2026-10-08.md`](rsi-volume-rule-study-session1-2026-10-08.md) (`RVS-1` = (d) venue candles; `RVS-2` = (c) gap sensitivity).
**Tool:** [`tools/ops/rsi_volume_rule_read.py`](../tools/ops/rsi_volume_rule_read.py), committed at `25f3468` before its single run. **Full output:** [`harness-runs/rsi-volume-rule-read-2026-10-08.md`](harness-runs/rsi-volume-rule-read-2026-10-08.md).
**Class:** analysis only. **It changes nothing in the engine.** Any engine change it led to would be a scoring change, reserved to the trader and after 2026-11-25.

---

## 0. Answer

**Neither rule should change. No candidate replacement beats the engine's rule in any session.**

| Question (trader, 2026-10-07) | Answer from the data, 2023-01 → 2026-10, weekdays |
|---|---|
| Is there a more accurate replacement for "volume ≥ 3× SMA(9)"? | **No.** Time-of-day relative volume (`VOL-H2`) and a trailing percentile (`VOL-H3`) show NO DIFFERENCE from the engine's closed-bar dynamic-threshold rule in every session (one DISCOVERY ONLY, NY `VOL-H3`) |
| Is there a more accurate replacement for "RSI 60/40"? | **No.** Terciles (`RSI-H2`) and the midline (`RSI-H3`) show NO DIFFERENCE from the mapped 60/40 in every session |
| Do the votes add anything over their own direction conditions? | **Volume: ASIA only, not confirmed.** V0 beats its ROC + VWAP condition by +1.42 bps in ASIA (H1 FINDING, H2 same sign, not significant). NO DIFFERENCE in LONDON and NY. **RSI: no.** NO DIFFERENCE in LONDON and NY; DISCOVERY ONLY (H2) in ASIA |

- **Every rule loses money at this geometry** (net EV −1.4 to −3.8 bps per trade, all CIs below 0). That is the ATR-fallback geometry's cost, which `rsi-volume-rule-study-spec.md` §9 `RVR-7` anticipated. The study compares rules against each other, not against zero.

## 1. The pre-registered labels (`rsi-volume-rule-study-spec.md` §5; Holm within each family of 9)

| Hypothesis | ASIA | LONDON | NY |
|---|---|---|---|
| `VOL-H1` V0 − V-ctl | **+1.42** [+0.57, +2.30] · H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (d > 0) | +0.32 [−1.20, +1.87] · NO DIFFERENCE SHOWN | −0.44 [−0.91, +0.02] · NO DIFFERENCE SHOWN |
| `VOL-H2` V1 − V0 | +0.65 [−0.70, +1.95] · NO DIFFERENCE SHOWN | −0.89 [−2.92, +1.15] · NO DIFFERENCE SHOWN | +0.17 [−0.57, +0.93] · NO DIFFERENCE SHOWN |
| `VOL-H3` V2 − V0 | +0.01 [−0.87, +0.84] · NO DIFFERENCE SHOWN | −0.50 [−1.94, +0.92] · NO DIFFERENCE SHOWN | +0.22 [−0.21, +0.67] · DISCOVERY ONLY (H2) (d > 0) |
| `RSI-H1` R0 − R-ctl | +0.70 [+0.07, +1.34] · DISCOVERY ONLY (H2) (d > 0) | −0.60 [−1.55, +0.32] · NO DIFFERENCE SHOWN | −0.08 [−0.37, +0.20] · NO DIFFERENCE SHOWN |
| `RSI-H2` R1 − R0 | −0.22 [−0.57, +0.13] · NO DIFFERENCE SHOWN | −0.02 [−0.51, +0.47] · NO DIFFERENCE SHOWN | +0.01 [−0.16, +0.18] · NO DIFFERENCE SHOWN |
| `RSI-H3` R2 − R0 | −0.15 [−0.83, +0.53] · NO DIFFERENCE SHOWN | −0.44 [−1.44, +0.54] · NO DIFFERENCE SHOWN | +0.02 [−0.27, +0.30] · NO DIFFERENCE SHOWN |

FULL-span d in bps, net EV per trade, 95 % day-bootstrap CI. H1, H2, boot p, Holm α and the Holm-adjusted CI per cell are in the full output §2.
- ⚠ **`VOL-H1` ASIA passes Holm on the full span** (p 0.0010 ≤ α 0.0056; Holm-adjusted CI [+0.21, +2.66]). It is not CONFIRMED only because its H2 CI touches 0 (+1.08 [−0.09, +2.27]). **The label rule decides: an H1 finding is not a finding to act on** (`rsi-volume-rule-study-spec.md` §1, slip 4).
- **The power was there.** The worst cell could detect 7.4 bps (session record §14). These are small effects measured precisely, not missing data: NY `RSI-H2`'s full CI is ±0.17 bps.

## 2. What the results change — the spec's own table, applied

| Spec §5 row | Result | Recommendation |
|---|---|---|
| `VOL-H2` or `VOL-H3` CONFIRMED positive in a session | None | No replacement for V0 |
| `VOL-H1` CONFIRMED positive | None (ASIA is an H1 FINDING) | — |
| `VOL-H1` NO DIFFERENCE SHOWN | **LONDON, NY** | **Keep V0** (the ruled closed-bar design point). Record: closed-bar volume adds nothing over its ROC + VWAP condition at this geometry in LONDON and NY; its weight goes to the vote-value study (`F-2`, `medium-tier-diagnosis-read-2026-09-17.md` §3) |
| `VOL-H1` ASIA (no row matches an H1 FINDING) | +1.42 bps, not confirmed | Keep V0. Carry the ASIA effect to the vote-value study as an unconfirmed lead |
| `RSI-H2` or `RSI-H3` CONFIRMED positive | None | No change to R0's levels |
| `RSI-H1` CONFIRMED negative | None | The RSI entry vote stays. ("No difference shown" is not evidence against the vote — spec §5) |
| Otherwise | — | **Keep R0** (the ruled `ICR-6` (a) mapping: NY 60/40; 3-minute levels ASIA 58.669 / 41.474, LONDON 58.961 / 41.424) |

**Net: no scoring change comes out of this study.** The two ruled mechanism fixes stand as ruled: volume on closed bars at the dynamic threshold (`ICR-1` (a)), and RSI levels mapped across resolution (`ICR-6` (a)). This study measured both and found no better rule. Both still sit in the post-holiday order (`indicator-calibration-review-adversarial-2026-10-07.md` §4).

## 3. Descriptive (never labelled)

- **`RVS-2` gap sensitivity** (full output §3.1): adding back the gap-dropped bars (1–3 % more episodes) moves no FULL d by more than 0.11 bps. One sign flip: `VOL-H3` ASIA, +0.01 → −0.03, both indistinguishable from 0. **The primary gap rule did not shape any result.**
- **Where the ASIA volume effect sits** (full output §3.2): both sides (long +1.50, short +1.34); 2023–2025 positive, 2026 −0.66; concentrated in the top ATR tercile (+3.22 [+1.02, +5.53]; terciles 1–2 ≈ +0.5).
- **NY volume leans slightly negative** in every split cell except 2023 (+0.26): −0.11 to −0.84, and the ATR tercile 1 cell −0.33 [−0.58, −0.08]. This matches the earlier read that momentum votes lean negative in NY (`medium-tier-diagnosis-read-2026-09-17.md`). It is not significant at the full span.
- **Partial votes** (full output §1) sit within 1 bps of their full votes: ASIA V0 partial −3.01 vs V0 −2.07; NY R0 partial −2.81 vs R0 −2.90.
- **Walks:** 147,615 unique (signal bar, side, session): 67,768 success, 69,626 stop, 10,221 timeout. No episode lacked an entry trade or ran past the data.

## 4. Handles

| Handle | Command | Result here |
|---|---|---|
| `H-7` | `python -I tools/ops/rsi_volume_rule_read.py --stores C:\DeribitData\history-2023-2024 C:\DeribitData\history --cut 2026-10-06 --venue-bars C:/DeribitData/venue-candles --out docs/harness-runs/rsi-volume-rule-read-2026-10-08.md` | Run once at `25f3468`. Output MD5 `c7016874d297ea533fe569aeb9448efb`. The tool first asserts that the recomputed frozen thresholds equal `docs/rsi-volume-rule-study-frozen.json` (it did) |
| `H-8` | `python -I tools/ops/rsi_volume_rule_read.py --selftest` | ALL PASS, 18 checks (walk, outcome formulas, calendar, copied statistics) |

- Needs the local stores, the venue candle backfill at `C:\DeribitData\venue-candles\` (re-creatable with `tools/ops/venue_candle_backfill.py`) and the settings at v69.
- **Copied statistics verified:** `pctl`, `boot`, `diff_of`, `halves`, `compare`, `readable`, `sig`, `label`, `boot_p`, `holm` and `finalize_label` are AST-source-identical to `tools/ops/burst_outcome_read.py` (checked with `ast.get_source_segment` before the run).
- **Walk mutations:** three single mutations (target touch `>=` → `>`; window anchored at T instead of the entry trade; the window-end mark not tracked) each fail the self-test. The second survived the first self-test; one check was added before the run.

## 5. Choices made in session 2 (one line each)

| ID | Choice | Options | Why |
|---|---|---|---|
| `RVR-20` | One walk per (signal bar close, side, session), shared by every rule that fired there | one walk per rule episode | The outcome depends only on those three; identical by construction |
| `RVR-21` | After a touch, the walk keeps tracking the price to the window end for the fixed-horizon mark | stop at the touch | `rsi-volume-rule-study-spec.md` §4.7 asks for the fixed-horizon net return at W, whatever the exit |
| `RVR-22` | A touched walk whose data ends before the window end closes on its touch | drop it | The exit is decided; only the descriptive mark is affected. None occurred |
| `RVR-23` | ATR tercile edges per session from an even sample of eligible bars (outcome-blind), exact rank rule | episode ATR | The split must not depend on which rule fired |

## 6. What I did not verify

- **A second run's output MD5.** `rsi-volume-rule-study-spec.md` §8 pins one run. The bootstrap is seeded and the walk is deterministic, but identity was not re-measured.
- That the ATR-fallback geometry stands in for the engine's placed ladder. `rsi-volume-rule-study-spec.md` §0 states it does not; the study compares rules at a fixed geometry.
- The `F-2` vote-value study's scope and whether it takes the ASIA lead. Not opened.
- The venue candle backfill against the live engine beyond the `E-1` window (2026-09-24 → 2026-10-07).
