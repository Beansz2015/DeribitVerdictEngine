# RSI and volume rule study — session 1 record (2026-10-08 UTC)

**Spec of record:** [`rsi-volume-rule-study-spec.md`](rsi-volume-rule-study-spec.md). **Pinned to:** commit `d441494` (HEAD at seat start), settings v69.
**Tool:** [`tools/ops/rsi_volume_rule_counts.py`](../tools/ops/rsi_volume_rule_counts.py). **Frozen thresholds:** [`rsi-volume-rule-study-frozen.json`](rsi-volume-rule-study-frozen.json).
**Class:** analysis only, read-only on the stores. **No outcome was computed.** No entry price, level, walk or return exists in the tool.

**Status: SESSION 1 DONE — STOPPED at `rsi-volume-rule-study-spec.md` §7 items 1 and 4. Both RULED 2026-10-08 (trader):**

> **"RVS-1 = (d)"** — backfill the venue's own 1-minute and 3-minute REST candles for 2023-01 → 2026-10 and compute every indicator on them; trades stay the source for entries, the walk and the gap check.
> **"RVS-2 = (c)"** — keep the registered gap rule as primary; session 2 adds a descriptive sensitivity that drops only on gaps inside the outcome window or the last 100 bars.

Next: the venue candle backfill and a session-1 re-run on venue bars (this doc §13), then session 2. Probe 2026-10-08: the venue's `public/get_tradingview_chart_data` returns 1-minute and 3-minute BTC-PERPETUAL candles at 2023-01-01 00:00 UTC, empty minutes included (volume 0).

---

## 0. The STOP gates (`rsi-volume-rule-study-spec.md` §7)

| Gate | Result | Where |
|---|---|---|
| 1 · parity `E-1` | ⛔ **FAIL as specified.** 49 % of 1-minute rows and 48 % of 3-minute rows pass all four tolerances. The decomposition shows the port is right; the misses come from the forming-stub timing and from the venue's candles | this doc §2, §3 |
| 2 · V0 ratio-only fire share in [0.5×, 2×] of the reference | ✅ PASS: NY 2.13 % (ratio 0.99), ASIA 3.09 % (0.97), LONDON 3.04 % (0.93) | this doc §6 |
| 3 · a hypothesis NOT READABLE in every session | ✅ none: every hypothesis × session has n ≥ 100 episodes in both halves | this doc §7 |
| 4 · gap drops > 2 % of weekday bars in a session-year | ⛔ **triggers** in ASIA 2023, LONDON 2023–2026, NY 2023. Hours reported in this doc §4 | this doc §4 |

## 1. Handles

All handles need the local stores `C:\DeribitData\history-2023-2024` and `C:\DeribitData\history`. `H-2` also needs the gitignored fetch `aws_fetch/20261007-180541`; `H-3` needs the gitignored venue candle store `backtest_data/`. The bar build caches to `C:\DeribitData\rsi-volume-study\` (outside the repo).

| Handle | Command | Result here |
|---|---|---|
| `H-1` | `python -I tools/ops/rsi_volume_rule_counts.py --stores C:\DeribitData\history-2023-2024 C:\DeribitData\history --cut 2026-10-06` | Run twice (cache miss, then cache hit). Output MD5 of the final tool version `64638951ee2e926b563c8dbd6f145968`. Writes the frozen JSON |
| `H-2` | `python -I tools/ops/rsi_volume_rule_counts.py --parity aws_fetch/20261007-180541` | E-1 FAIL; exit 2 |
| `H-3` | `python -I tools/ops/rsi_volume_rule_counts.py --venue-candles backtest_data` | E-1b FAIL at the E-1 tolerances; vote agreement ≥ 99.76 % |
| `H-4` | `python -I tools/ops/rsi_volume_rule_counts.py --selftest` | ALL PASS (19 checks) |

## 2. `E-1` as specified, and its decomposition

**Construction (spec §6):** the engine's candle list as of each `analysis_log.csv` row — 249 completed bars plus a forming stub from trades up to the logged second. 11,881 rows, 2026-09-24 18:46 → 2026-10-07 18:05 UTC. Trades: the dev history store up to 2026-10-05 23:59:56, then the box store.

| Res | Rows | RSI ≤ 0.5 | ROC ≤ 0.01 | VolumeRatio ≤ 2 % | VWAP ≤ 0.5 USD | All four |
|---|---:|---:|---:|---:|---:|---:|
| 1 min | 8,526 | 83.70 % | 94.90 % | 62.62 % | 82.59 % | 49.38 % |
| 3 min | 3,355 | 84.77 % | 94.40 % | 54.25 % | 91.54 % | 48.20 % |

- **The medians match closely:** RSI p50 |diff| 0.004, ROC 0.00003, VolumeRatio rel 0.003 (1 min). The failures sit in the tails.
- **The stub is the main cause.** The log timestamp has 1-second resolution and the engine's candle-snapshot instant is not logged. At the logged second, 1,184 of 8,526 1-minute stubs hold no trade. Moving the cutoff to the end of the second makes every field worse (sensitivity in the `H-2` output).
- **Decomposition — the stub close anchored on the engine's own logged `Price`** (`r.CurrentPrice = candlesExec.Last().Close`, `UI/MainForm_Analysis.vb:239`). RSI and ROC then depend only on the completed bars:

| Res | Rows | RSI ≤ 0.5 | ROC ≤ 0.01 |
|---|---:|---:|---:|
| 1 min, all rows | 8,526 | 98.44 % | 99.68 % |
| 1 min, weekday rows | 5,886 | **99.30 %** | **99.64 %** |
| 3 min, all rows | 3,355 | 99.02 % | 99.70 % |
| 3 min, weekday rows | 2,316 | **99.35 %** | **99.61 %** |

- **On weekday rows, the closed-bar RSI and ROC port passes the 99 % bar.** Most of the remaining misses fall on the weekend (2026-10-03 alone holds 45 at 1 min).
- **VolumeRatio cannot be decomposed this way.** The engine's stub volume is not logged. The implied fetch lag (from the logged ratio and our 8 completed bars) has median +0.1 s but p10 −4 s and p90 +1.4 s at 1 min. Read it as consistent in the median and not as a test.
- **VWAP misses come from the completed bars, not the stub.** A scratch check (not committed) dropped the stub entirely and got the same pass rate (1 min: 1,461 vs 1,463 of 1,961 sampled weekday rows). The pass rate falls as the session's candle count grows. This doc §3 finds the cause.

## 3. `E-1b` — closed tape bars against the venue's own REST candles (added evidence, not in the spec)

`backtest_data/candles_{1,3}m_2026-*.csv` hold the venue's REST candles 2026-01-31 → 2026-09-21 (no 2026-08 file). This compares **closed bars only** — the study's actual input — so the stub cannot enter.

| Res | Bars compared | Close equal | OHLC equal | USD volume equal | BTC volume within 1 % |
|---|---:|---:|---:|---:|---:|
| 1 min | 258,087 | 99.460 % | 97.993 % | 99.992 % | 99.996 % |
| 3 min | 87,641 | 99.469 % | 97.234 % | 99.978 % | 99.989 % |

Indicators on weekday session bars whose whole 250-bar lookback is venue-covered:

| Res | Bars | RSI ≤ 0.5 | ROC ≤ 0.01 | VolumeRatio ≤ 2 % | VWAP ≤ 0.5 USD | V0 full fire agree | Volume direction agree | RSI 60/40 side agree |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 min | 84,919 | 98.517 % | 99.461 % | 99.982 % | 71.004 % | **100.000 %** (1,834 = 1,834) | 99.762 % | 99.832 % |
| 3 min | 33,800 | 98.769 % | 99.405 % | 99.997 % | 81.287 % | **100.000 %** (1,086 = 1,086) | 99.876 % | 99.896 % |

- **Volume is exact.** The volume rules (V0, V1, V2) see the engine's own numbers.
- **The venue's candle high, low and close leave out some prints that its volume includes.** Tape highs sit above venue highs and tape lows below venue lows; volume is identical. Four 2026-01-31 bars traced to single trades (e.g. 05:22, a sell at 83,924 against a venue high of 83,906).
- **No store column identifies the left-out prints.** Probe on 2026-02: 2,329 of 4.04 M trades fall outside the venue range. They spread across all tick directions and sizes, with no liquidation flag, and their distance to mark (p50 1.05 bps) is close to the inside trades' (0.82 bps). **The venue's filter is unknown.**
- **Effect on the study:** RSI and VWAP inherit the H/L/C difference. At the vote level, agreement is 99.76 %–100 % on every input. The VWAP tolerance of 0.5 USD is tight against a 250-bar sum where 2 % of bars differ in H or L.

## 4. Bars, gaps, and STOP 4 — the hours

- **Store:** 46 files, 11.68 GB, 140,508,403 trades. Store seam `trades_2024-12.csv` → `trades_2025-01.csv`: seq 230579796 → 230579797, PASS. 80 trades out of time order inside files; 0 past the cut.
- **Bars:** 1,978,560 1-minute bars (2023-01-01 → 2026-10-06 exclusive), 77,385 empty. 659,520 3-minute bars.
- **Gaps** (runs of more than 5 empty 1-minute bars): 624 runs, 5,881 minutes; longest 120 min at 2024-04-06 02:11 (Saturday).

| Where the gaps sit | Count |
|---|---|
| Runs by weekday | Sun 276 · Sat 195 · Tue 55 · Mon 33 · Wed 25 · Fri 21 · Thu 19 |
| Weekday runs by year | 2023: 103 · 2024: 13 · 2025: 16 · 2026: 21 |
| Most common start times | 09:01 (24) · 09:00 (23) · 13:00 (6) |
| **Weekday runs starting 09:00–09:01 UTC** | **42, median 16 min, mostly Tuesdays** — the pattern of scheduled venue maintenance (not verified against a venue notice) |

| Session | 2023 | 2024 | 2025 | 2026 |
|---|---:|---:|---:|---:|
| ASIA gap drop | **11.27 %** | 1.58 % | 1.15 % | 1.54 % |
| LONDON gap drop | **12.19 %** | **4.52 %** | **4.84 %** | **9.07 %** |
| NY gap drop | **3.34 %** | 0.30 % | 0.67 % | 0.94 % |

- **Why LONDON is hit every year:** a 3-minute signal's lookback is 250 × 3 = 750 minutes. One 09:00 maintenance gap therefore drops every LONDON bar that day (close hours 08–12), plus the 08:xx bars whose 45-minute window crosses 09:00.
- **2023 is different:** 103 weekday runs, mostly quiet-market minutes, not maintenance.
- **The gaps are genuine no-trade minutes, not store defects:** the stores are `TradeSeq`-complete (spec §4.1).

## 5. Halves and frozen thresholds

- **D = 961** UTC weekdays with at least one eligible signal bar. **H1 = 480 days, 2023-01-30 → 2024-11-29. H2 = 481 days, 2024-12-02 → 2026-10-05.** The weekdays before 2023-01-30 are V1 warm-up and belong to neither half.

| Session | V0 fire share on H1 | k₁ (V1 share) | k₂ (V2 share) | R0 U / L | R0 partial PU / PL | R1 lo / hi |
|---|---:|---:|---:|---:|---:|---:|
| ASIA (3 min) | 2.918 % | 12.343 (2.919 %) | 0.990 (2.687 %) | **58.685 / 41.454** | 54.426 / 45.834 | 43.797 / 56.332 |
| LONDON (3 min) | 2.956 % | 10.585 (2.959 %) | 0.990 (2.607 %) | **58.967 / 41.404** | 54.576 / 45.955 | 44.380 / 56.896 |
| NY (1 min) | 2.123 % | 22.067 (2.123 %) | 0.990 (2.191 %) | 60 / 40 (fixed) | 55 / 45 | 43.690 / 57.168 |

- **The R0 3-minute levels sit about 1.3 points inside 60/40** (`rsi-volume-rule-study-spec.md` §11 left this open). They match the 1-minute exceedance shares exactly (e.g. ASIA > 60: 27.54 % at 1 min, > 58.685: 27.54 % at 3 min).
- **k₂ cannot match exactly:** V2's percentile rank is discrete in steps of 0.005. 0.990 is the closest candidate (ASIA 2.69 % vs 2.92 %).

## 6. V0 fire share (STOP 2)

| Session | n | Full fire | Reference (`candle-store-derivations-2026-07-31.md` §3.1) | Ratio | Partial | High-threshold p50 | At max clamp |
|---|---:|---:|---:|---:|---:|---:|---:|
| NY | 627,668 | 2.13 % | 2.16 % | 0.99 | 5.87 % | 4.40 | 6.1 % |
| ASIA | 148,683 | 3.09 % | 3.17 % | 0.97 | 6.66 % | 3.62 | 3.2 % |
| LONDON | 89,502 | 3.04 % | 3.27 % | 0.93 | 6.17 % | 3.40 | 2.0 % |

By year the share is flat (NY 2.11–2.15 %; ASIA 2.87–3.32 %; LONDON 2.90–3.22 %). USD-volume sensitivity: identical to two decimals.

## 7. Counts and power (outcome-blind)

Episodes per hypothesis, full span (H1 / H2 in the `H-1` output §6):

| Hypothesis | ASIA nA / nB | LONDON nA / nB | NY nA / nB | MDE80 at α 0.05/9, conservative σ (bps) |
|---|---:|---:|---:|---|
| `VOL-H1` V0 − V-ctl | 2,689 / 3,518 | 1,438 / 1,647 | 7,856 / 12,010 | ASIA 3.2 · LONDON 4.6 · NY 1.8 |
| `VOL-H2` V1 − V0 | 1,275 / 2,689 | 780 / 1,438 | 3,621 / 7,856 | ASIA 5.3 · LONDON 7.5 · NY 3.1 |
| `VOL-H3` V2 − V0 | 1,906 / 2,689 | 944 / 1,438 | 6,643 / 7,856 | ASIA 4.1 · LONDON 6.1 · NY 2.4 |
| `RSI-H1` R0 − R-ctl | 4,955 / 5,113 | 3,138 / 3,149 | 22,322 / 21,473 | ASIA 2.5 · LONDON 3.2 · NY 1.2 |
| `RSI-H2` R1 − R0 | 4,642 / 4,955 | 2,957 / 3,138 | 20,886 / 22,322 | ASIA 2.5 · LONDON 3.1 · NY 1.2 |
| `RSI-H3` R2 − R0 | 3,422 / 4,955 | 2,059 / 3,138 | 14,792 / 22,322 | ASIA 2.8 · LONDON 3.5 · NY 1.3 |

- σ = the published proxy (34.3 bps conservative, 14.7 optimistic) × the ATR scale (median ATR in bps at the pair's episodes / at all eligible bars). Copied from the power block of `tools/ops/a4_liq_ofi_counts.py`. **The proxy is not a measurement for this population.**
- **Power is ample everywhere.** The smallest cell (LONDON `VOL-H2`, 780 V1 episodes) still detects 7.5 bps at the worst Holm step.
- ⚠ **V1 fires in clusters:** 1,765 ASIA H1 signals give only 653 episodes, against V0's 1,710 → 1,273. Same fire share, fewer independent events.

## 8. Decisions for the trader

| ID | Decision | Options | My read |
|---|---|---|---|
| **`RVS-1`** | `E-1` failed as specified (`rsi-volume-rule-study-spec.md` §7 item 1). What does session 2 run on? | **(a)** Proceed on the tape bars as built. Record `E-1` FAIL with this doc's §2–§3 decomposition. **(b)** Re-register `E-1` as a vote-level test on closed bars (`E-1b`: ≥ 99 % agreement on the V0 fire, the volume direction and the RSI side; the tolerance tests stay descriptive), then run session 2 on tape bars. **(c)** Find the venue's H/L/C filter and rebuild the bars to match. **(d)** Backfill the venue's own 1-minute and 3-minute REST candles for 2023-01 → 2026-10. Compute every indicator on them, the engine's real input. Trades stay the source for entries, the walk and the gap check | **(d), with (b) as the fallback** if the venue does not serve candles back to 2023-01. (d) is the most truthful: the study then measures the votes on exactly what the engine reads, and `E-1b` passes by construction on bars. (c) is blocked: no store column identifies the left-out prints (this doc §3). (a) and (b) would rest on "agreement is ≥ 99.76 %, which is adequate" — the economy reason the CLAUDE.md bias rule names. ⚠ **Not verified:** how far back the venue's chart endpoint serves 1-minute candles |
| **`RVS-2`** | STOP 4: gap drops exceed 2 % in six session-years (`rsi-volume-rule-study-spec.md` §7 item 4) | **(a)** Keep the registered gap rule; record the hours (this doc §4). **(b)** Change the primary rule to exempt scheduled maintenance gaps (09:00–09:01 weekday starts). **(c)** Keep the registered rule as primary. Add a descriptive sensitivity in session 2 that drops only on gaps inside the outcome window or the last 100 bars. **(d)** Shorten the lookback the gap check covers, in the primary | **(c).** The gaps are genuine no-trade minutes and the dropped LONDON days are chosen by the venue's schedule, not by market state. So the primary rule's drops carry no outcome information, and power stays ample (this doc §7). (b) and (d) change a pre-registered rule after seeing counts; that is the post-hoc move `rsi-volume-rule-study-spec.md` §9 `RVR-9` guards against. (c) records the extra data without moving the primary |

Harness 6 (the decision-bias tripwire) was run on both reads: this doc §12.

## 9. Session-1 design choices auto-proceeded (one line each)

| ID | Choice | Options | Why |
|---|---|---|---|
| `RVR-10` | Volume = BTC (Σ Amount / Price per trade) for every volume rule and VWAP | USD (Σ Amount), as `rsi-volume-rule-study-spec.md` §4.2 wrote | The engine's `Candle.Volume` is BTC (`DeribitClient.vb:253`). The more engine-faithful option; USD printed as a V0 sensitivity (identical to two decimals) |
| `RVR-11` | VWAP session = the session of the signal bar's last instant (close − 1 ms) | close instant | At close = 13:30 exactly, the close instant would put the signal bar outside its own session and trigger the engine's whole-list fallback |
| `RVR-12` | V1 and V2 share-matched to V0's **ratio-only** fire share | full-vote share | The direction condition is common to all three, so matching the ratio part compares which bars, as `rsi-volume-rule-study-spec.md` §4.4 intends |
| `RVR-13` | Episode kept when T − t_prev ≥ W (a fire exactly W ago does not block) | > W | The earlier trade's window has closed at T |
| `RVR-14` | One eligibility for all rules, including V1's 20-weekday warm-up | per-rule warm-up | Paired comparisons need the same bar population |
| `RVR-15` | Power ATR scale in bps of close | USD | σ is in bps; BTC moved 17k → 120k over the span |
| `RVR-16` | RSI carried as one running Wilder series | the 250-bar list per bar | Identical to < 1e-11 (selftest; seed weight (8/9)^240); the list form is 500 M operations in pure Python. `E-1` uses the exact list form |

## 10. Feedback on the spec

- **`E-1` cannot pass as specified, whatever the port.** The log timestamp has 1-second resolution and the candle-snapshot instant is not logged. At 1 minute, 14 % of stubs are empty at the logged second. A 2 % VolumeRatio tolerance on a stub ratio of ~0.06 is about 0.001 absolute. A future parity spec should test closed bars (`E-1b`) or log the snapshot instant.
- **`rsi-volume-rule-study-spec.md` §4.2 said USD volume; the engine reads BTC.** Harmless here (identical shares) — recorded as `RVR-10`.
- **The 250-bar gap reach is long at 3 minutes** (12.5 hours). It is what makes STOP 4 fire on LONDON.

## 11. What I did not verify

- **The venue's H/L/C filter.** Unknown; probed one month only.
- That the 09:00–09:01 weekday gaps are scheduled maintenance. Inferred from the pattern (time, weekday, length); not checked against venue notices.
- How far back the venue's chart endpoint serves 1-minute and 3-minute candles (decides `RVS-1` (d)).
- `E-1` on the full 2026-09-24 → 2026-10-07 book against the venue candles: the venue candle store ends 2026-09-21, so `E-1b` and `E-1` cover different windows.
- The σ proxy for this population (carried, as in every prior power block).

## 12. Harness 6 (decision-bias tripwire)

Run after this doc was written; my labels were written first. Run files: `docs/harness-runs/decision-bias-20261008T0858Z-rvs-{population,baseline,jev}.json`. Jev 1.13.0, 5 samples per item, 2026-10-08 08:59 UTC.

| Item | My label (baseline) | Jev verdict | Agreement | Mean top p | Flag (`gives_up_for_economy`) |
|---|---|---|---:|---:|---|
| `RVS-1` read (d) | `no_richer_option` | `richer_option_wrong` | 1.0 | 0.69 | **no** |
| `RVS-2` read (c) | `richer_option_wrong` | `richer_option_wrong` | 1.0 | 0.886 | **no** |

- **No flag on either read.** The `RVS-1` mismatch is between two non-flag labels. My read picks the richest option, and Jev reads (c) as the "richer option" that is blocked.

## 13. Session 2 — when `RVS-1` and `RVS-2` are ruled

**Model · effort:** Opus 5.5 · high (unchanged from `rsi-volume-rule-study-spec.md` §1).
- **Why that tier:** a pre-registered outcome run where a wrong number is hard to notice. The statistics are copied, not written.
- **Where it slips:** (1) the entry is the first trade at or after the signal bar's close — not the bar's close price; (2) the 1-minute walk needs trade-level resolution only where one minute touches both levels — do not resolve those from OHLC; (3) the frozen JSON must be re-derived and asserted equal, never edited.
- **Escalation trigger:** any recomputed frozen threshold that differs from `docs/rsi-volume-rule-study-frozen.json`.
- **If `RVS-1` = (d):** add one session before session 2 for the venue candle backfill and a re-run of session 1 on it (Opus 5.5 · medium; the bar and indicator code is in place).
