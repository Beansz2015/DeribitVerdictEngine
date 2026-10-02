# A4 liquidation × OFI flip — logged-era study, pre-registered spec (written 2026-10-02 UTC)

**Owed by:** [`trader-tick-queue.md`](trader-tick-queue.md) §1 Cluster E row `E7` (the A4 study row; 2026-10-02 scheduling note: session 1 = this spec plus an outcome-blind power count).
**Start commit:** `e8a132c`. Tool commit: `a42804a`. (`34f0af5` and three orchestrator doc commits landed on `master` during this session; none touches the files here.)
**Status:** SESSION 1 DONE. **No outcome has been computed, printed or looked at.**

> ⛔ **ESCALATION TRIGGER MET — STOP, report, do not redesign** (the brief's trigger: "the event count makes the study clearly unreadable at any reasonable effect size").
>
> - **28 joined events in the logged era: 8 FLIP, 20 NO-FLIP.** The census readability floor is n ≥ 100 per group.
> - **FLIP reaches 100 about 2028-09-18** at the collector-era accrual rate (0.128 FLIP per covered day).
> - **Minimum detectable effect (MDE) at 80 % power on the primary test: 98.6 bps** (conservative σ) **or 42.2 bps** (optimistic σ). Power at a 10 bps effect is 0.05 to 0.10.
> - The spec below is complete and pre-registered as designed. Whether session 2 runs, waits or is replaced is decision `A4L-10` in `docs/a4-liq-ofi-logged-era-study-spec.md` §9, for the trader.

⛔ **This is a READ spec. It changes no setting and no scoring.** A positive result **cannot ship** until a real-time liquidation source exists: the live vote is parked (ruling `D-4`, below), and the park's un-park checklist ([`liquidation-park-spec.md`](liquidation-park-spec.md) §5.2) needs a measured real-time source first. This study is research that gates un-parking; it is not a build.

---

## 0. What is tested, and what is not

| Item | Tested here? |
|---|---|
| **A4 as defined** — a liquidation cascade, then OFI flips against it within ~200 ms ([`post-websocket-post-calibration-backlog.md`](post-websocket-post-calibration-backlog.md), section "A4. Liquidation × OFI Flip Detector") | ❌ **No.** The logged era has one OFI value per run (every 60 s in NY, every 180 s in ASIA and LONDON). It cannot see 200 ms |
| **The coarse proxy** — after a large liquidation cluster, the logged `OFIRatio` at the next logged run sits against the cascade, and that predicts a profitable fade | ✅ Yes. This is the study |
| Whether the cascade adds anything over the OFI vote alone | ✅ Yes, secondary test `A4L-H3` |
| Liquidations before 2026-07-03, or any span without a logged run | ❌ No. The OFI half exists only as logged per-run values (`HSR-11`, below) |
| The live engine's liquidation signal | ❌ No. Live never carries the flag (finding `L-1`, below). The events come from the history store's final flags |

---

## 1. Model and effort for SESSION 2 (the outcome run)

**Model: Opus 5.5 · Effort: HIGH.**

- **Why that tier:** the design is fixed here. The work is holding it against the data and refusing to adapt after seeing outcomes. A small population (tens of events) makes every per-event judgement visible in the result.
- **Where it will slip:**
  1. **The wrong side's placed levels.** The fade of a `LONG` liquidation cascade is a LONG trade: use `PlacedTargetLong` / `PlacedStopLong`. The fade of a `SHORT` cascade uses the `*Short` columns. A swap still produces plausible numbers.
  2. **Reading the logged `OFISignal` instead of re-thresholding `OFIRatio`.** Rows before the v48 deploy on 2026-07-03 carry `OFISignal` at the old 2.0/0.5 pair. The design re-thresholds the ratio by value at 1.60/0.625.
  3. **Starting the outcome walk inside R1's second.** The run instant inside that second is unknown. The walk starts at R1 `Timestamp` + 1 s (`docs/a4-liq-ofi-logged-era-study-spec.md` §4.6).
  4. **Halves on calendar days instead of event days.** The label rule splits the first ⌊D/2⌋ UTC days **that hold a population event**.
- **The fixtures cannot catch a misunderstanding:** the seat writes the outcome script and its self-test. The counts parity handle (`H-3`, `docs/a4-liq-ofi-logged-era-study-spec.md` §6.1) is the guard: the outcome script must reproduce the event, join and arm counts before it opens any outcome column.
- **Escalate** (stop, report, do not decide) if: `H-3` fails; any rule in `docs/a4-liq-ofi-logged-era-study-spec.md` §4 cannot be applied as written; a store day inside an outcome window is not `COMPLETE` in `status_final.txt`; or any outcome-side drop exceeds 10 % of events for a reason not named in `docs/a4-liq-ofi-logged-era-study-spec.md` §7.
- **Session split:** (a) build `tools/ops/a4_liq_ofi_read.py` and its self-test, commit it, Opus 5.5 medium; (b) run it, Opus 5.5 high. The script is committed before its first run on outcome columns.

---

## 2. IDs used here

| ID | Source and kind | Meaning |
|---|---|---|
| A4 (backlog item) | [`post-websocket-post-calibration-backlog.md`](post-websocket-post-calibration-backlog.md), section A | The liquidation × OFI flip detector. ⚠ **Not fixture `A4`** (`A4_TfiWindowFromEnd` in `verify/ordercheck/Program.vb`). Two things share the name |
| `E7` | [`trader-tick-queue.md`](trader-tick-queue.md) §1 Cluster E, queue row | The A4 study row |
| `HSR-11` | [`history-store-queue-reshape-evaluation.md`](history-store-queue-reshape-evaluation.md) §1, trader ruling 2026-09-29 | Replays may join logged per-run OFI values: join by the run row, use the logged run instant, re-threshold the logged `OFIRatio` freely, never a different book depth |
| `HSR-13` | same doc, trader ruling 2026-09-29 | A4's gate = the `D-4` re-ruling (which resolved it to this collector-era study) |
| `D-4`, `D-5` | [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.3, trader decisions | `D-4`: the live liquidation vote, re-ruled 2026-09-30 to PARK. `D-5`: book `T` to the taker's side, `M` to the maker's side, `MT` to both |
| `EF-3` | [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §6, trader decision | An unrecognised liquidation flag is skipped and counted |
| `L-1` | [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R, finding | The live trade stream never carries the liquidation flag |
| `LLS-1` | [`large-liq-size-rederivation-2026-10-02.md`](large-liq-size-rederivation-2026-10-02.md) §4, trader ruling 2026-10-02 | Per-session `large_liq_size`. Study values (`D-5` booking): ASIA 69,535 · LONDON 83,250 · NY 49,724 USD |
| **`A4L-H1`–`A4L-H4`** | **This spec, hypotheses (NEW)** | `docs/a4-liq-ofi-logged-era-study-spec.md` §5 |
| **`A4L-1`–`A4L-10`** | **This spec, decisions (NEW)** | `docs/a4-liq-ofi-logged-era-study-spec.md` §9 |
| `H-1`–`H-3`, `E-1` | This spec, handles and evidence | `docs/a4-liq-ofi-logged-era-study-spec.md` §6.1 |

The `A4L` prefix was checked free with `git grep -n -E "A4L-?[0-9]|A4L\b"` at `e8a132c`: 0 hits.

---

## 3. The mechanism, from the code

- **The liquidation signal** — `IndicatorEngine.CalcLiquidations` (`Core/Indicators_OrderFlow.vb`): sums the USD `Amount` of flagged trades in the last 500 trades, per liquidated side. `LONG LIQS` if L > 0 and L ≥ 2.0 × S; `SHORT LIQS` if S > 0 and S > 2.0 × L.
- **"Large"** — the penalty site (`Core/ScoringEngine_Calculate_Scoring.vb:400-405`) compares the dominant sum with `large_liq_size` using strict `>`.
- **Direction.** `LONG LIQS` = longs force-sold, the cascade pushes price **down**, the fade is **LONG**. `SHORT LIQS` = shorts force-bought, cascade **up**, fade **SHORT**. The live penalty hits the liquidated side's score; A4 proposes to reward the fade instead.
- **OFI** — `IndicatorEngine.ComputeOfiImbalance` (`Core/Indicators_OrderFlow.vb`): ratio = weighted bid depth ÷ weighted ask depth over `book_depth` 5 levels. `ClassifyOfiRatio`: `BUY DOMINANT` if ratio > 1.60, `SELL DOMINANT` if ratio < 0.625 (strict). Since v46 (2026-06-30; geometric from 2026-07-01 07:02 UTC) the WS path logs a 10 s time-weighted geometric average. On warm-up and REST fallback it reverts to a single snapshot (`settings.json` `change_log`, v46 entry).
- **So "OFI against the cascade"** = bids heavier after a long-liquidation cascade (`OFIRatio` > 1.60), or asks heavier after a short-liquidation cascade (`OFIRatio` < 0.625). It is a resting-book state, not traded flow.

---

## 4. Pre-registered design

### 4.1 Data

| Item | Rule |
|---|---|
| Liquidations | Dev history store `C:\DeribitData\history\trades_YYYY-MM.csv`, 2025-01 → 2026-09. Flags final. `status_final.txt`: 638 days OK, 0 GAP, 71,766,893 rows. **Only `Timestamp`, `Amount`, `Direction`, `Liquidation` are read for events** |
| OFI (the run grid) | `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv`, and in `aws_fetch/20261002-121123/`: `analysis_log.csv.v0.7.bak`, `analysis_log.csv.116col-83564b1b.20260924_184606.bak`, `analysis_log.csv`. Read by column **name** |
| Mixed widths | The pooled book holds 40,398 111-column rows under a 116-column header. The v0.7 header equals the pooled header's first 111 names (checked); short rows map by that prefix |
| De-duplication | (1) Same `InstanceId` + `SignalId` in two books: first wins, in the order pooled → rotated → live → v0.7 (41,195 dropped; this equals the 7,284 + 33,911 rows the pooled book shares with the rotated and v0.7 books). (2) The concurrent-instance rule of `tools/ops/SwingFallbackRead/SwingFallbackRead.vb`: from the collector start (2026-07-22 16:24:54), drop rows of an instance not in the box books (12). (3) Same `Timestamp`: first wins (0) |
| Data cut | `Timestamp` < **2026-10-01 00:00:00 UTC** (the store's end). 1,146 rows after it dropped |
| Span | Runs 2026-07-03 15:57:49 → 2026-09-30 23:59:03: 64,373 runs. Every day, weekends included (the collector runs 24/7) |

### 4.2 Events (the liquidation side)

| Item | Rule |
|---|---|
| Booking | `D-5`: `T` + sell → long liquidation; `T` + buy → short; `M` + buy → long (the maker sold); `M` + sell → short; `MT` → the full amount to both sides. Unrecognised flag or direction → skipped and counted (`EF-3`; 0 at this cut) |
| Window | The last 500 trades, evaluated after **every** trade while the window holds any liquidation (so a threshold change at a session boundary is seen) |
| Fires large | Dominant side by the `CalcLiquidations` rule, and dominant sum **>** the `LLS-1` threshold of the session of the trade's UTC hour |
| Sessions | ASIA 0–7, LONDON 8–12, NY 13–23 UTC, end hour inclusive (`Core/ExecutionResolution.vb:44`, read) |
| Onset T0 (`A4L-1`) | A trade at which the window fires large **and** no firing happened in the 30 min before it, on either side. Past-only: nothing after T0 decides whether T0 is an event |
| De-clustering (`A4L-2`) | Firing within 30 min of the previous firing continues the episode; it is not a new event |
| Event side and session | The firing side and the session at T0. Episodes that later also fire on the other side are kept and counted (dropping them would select on the future) |

### 4.3 Join and the flip (the OFI side, `HSR-11`)

| Item | Rule |
|---|---|
| R1 (`A4L-4`) | The first run whose `Timestamp` second is **strictly after** T0's second. The run instant inside a second is unknown, so the same second is never used |
| Window W | R1 − T0 ≤ 90 s for an NY event, ≤ 210 s for ASIA and LONDON (one cadence + 30 s) |
| R0 | The last run whose second is strictly before T0's second, within W. Descriptive only |
| R2 | The run after R1, within 2W. Descriptive only |
| Drops, named | `no_run_in_W_named_gap` (T0's date inside 2026-08-08..08-10 intentional stop, 08-15..08-17, or 09-18..09-21 venue outage) · `no_run_in_W_dev_era` (before the collector start; the dev machine did not run 24 h) · `no_run_in_W_other` (unnamed — printed one by one) · `r1_ofi_invalid` · `r1_health_snapshot_proxy` (`A4L-8`: R1's `AggrVelBurstRatio` empty, or `WsHealth` not `OK`; on those rows OFI may be a snapshot) |
| Flip (`A4L-3`) | R1's logged `OFIRatio`, re-thresholded by value at the v48 pair, strict. `LONG` event: **FLIP** if > 1.60, **WITH** if < 0.625, else **BALANCED**. `SHORT` event: FLIP if < 0.625, WITH if > 1.60, else BALANCED. **NO-FLIP** = BALANCED ∪ WITH |

### 4.4 Arms (`A4L-5`)

| Arm | Rows |
|---|---|
| **FLIP** | Joined events, R1 in the FLIP state |
| **NO-FLIP** | Joined events, R1 BALANCED or WITH |
| **CONTROL** (OFI-only) | Healthy runs (same health rule) with no large firing at the run or in the 30 min before it, `OFIRatio` > 1.60 (trades LONG) or < 0.625 (trades SHORT). Same placed-level and v51 rules as events |

### 4.5 Population for outcomes

- R1 at or after the v51 placed-geometry edge, **2026-07-06 13:08:51** (`V51Edge`, `tools/ops/SwingFallbackRead/SwingFallbackRead.vb:58`). 0 events fall before it.
- R1's placed target and stop for the fade side must sit on the correct side of R1's `Price` (target distance > 0, stop distance > 0). Otherwise drop and count.

### 4.6 Outcome — defined, NOT computed (`A4L-6`)

| Item | Rule |
|---|---|
| Trade | The fade, entered at R1: LONG for a `LONG` event, SHORT for a `SHORT` event. Control rows trade their OFI side |
| Entry | R1's logged `Price` |
| Levels | R1's logged placed target and stop for the trade side (`PlacedTargetLong`/`PlacedStopLong` or `PlacedTargetShort`/`PlacedStopShort`). Distances in bps of entry |
| Window | By R1's `ExecResolution`, `AnalysisConstants.HoldWindowsForResolution` maximum: res 1 → 15 min, res 3 → 45 min (`analysis/AnalysisConstants.vb:74`) |
| Walk | History-store trades with `Timestamp` ≥ R1 `Timestamp` + 1 s and < R1 + window. LONG: first trade price ≥ target → success; first ≤ stop → stop. SHORT mirrored. Trade-level, so no same-bar ambiguity exists |
| Net EV per trade, bps | [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a: +target − fee on success · −stop − fee on a stop · mark − fee on a timeout (mark = the last trade price before the window end, signed for the side) |
| Fee | `scoring.trade_costs` maker/maker round trip, read from tracked `settings.json` at the run: **3 bps at v69**. Pinned: if the value differs at run time, STOP |
| Explaining numbers | Success rate, gross and net breakeven rates (Σ formulas, `DeribitIndicatorProject.md` §5a rule 1), edges. **Net EV per trade decides** (`DeribitIndicatorProject.md` §5a rule 4) |
| Descriptive only | Fixed-horizon mark-to-market net return at the window end |

### 4.7 Statistics

| Item | Rule |
|---|---|
| CI | 95 % percentile bootstrap of whole UTC days (event days), seed **20261002**, **10,000** resamples |
| Re-weighting | `A4L-H1`: NO-FLIP re-weighted to FLIP's session × side mix. `A4L-H3`: CONTROL re-weighted to FLIP's session × side × ATR tercile (tercile edges: per session, from R1-row `ATR` over all runs of the grid, printed by the run). Strata without both groups are dropped and counted (the `rw_run` method in `tools/ops/medium_tier_diagnosis.py`, copied, not re-derived) |
| Readable | n ≥ 100 in every group a statistic uses, counted in covered strata (the census rule; the clarification in [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §5) |
| Multiplicity (`A4L-9`) | `A4L-H1` alone at α 0.05. `A4L-H2` and `A4L-H3` as one secondary family, Holm, familywise α 0.05 |
| Label | The census rule, first match wins: NOT READABLE → CONFIRMED (both halves readable, both CIs exclude 0, same sign, full-sample Holm-adjusted CI excludes 0) → "NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0" → H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT → H1 FINDING, H2 CONTRADICTS OR NOT READABLE → DISCOVERY ONLY (H2) → NO DIFFERENCE SHOWN. Copied from `tools/ops/burst_outcome_read.py` (`label`, `finalize_label`) |
| Halves | First ⌊D/2⌋ UTC days holding a population event; the rest is H2. At this cut: 20 event days, H1 = 16 events (2026-07-13 → 08-24), H2 = 12 |
| Splits (descriptive) | Session · side · weekday/weekend (`A4L-7`) · BALANCED vs WITH · R0 state · R1 → R2 state · "R1 window still fires large" · R1 lag < 10 s |

### 4.8 Eras

| Edge | Handling |
|---|---|
| v46/v48 OFI construction and pair (2026-06-30 → 07-03) | All rows are post-geometric. The ratio is re-thresholded by value, so the pair change is absorbed (`HSR-11`) |
| v51 placed geometry, 2026-07-06 13:08:51 | Outcome population starts here (`docs/a4-liq-ofi-logged-era-study-spec.md` §4.5) |
| v66 OBV scoring edge (2026-08-10) | Pooled. The study uses no verdict and no score |
| POC-gate fix, 2026-09-24 18:46:06 | Moves the placed target on 3.33 % of rows ([`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §6.2). Pooled; a pre-edge-only sensitivity is printed; a sign flip adds "EDGE-SENSITIVE" |
| Daylight-saving seams (2026-10-25, 2026-11-01) | After this cut. A later cut that crosses them adds the seam treatment of [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §6.2 |

---

## 5. Hypotheses

| ID | Statement | Role |
|---|---|---|
| `A4L-H1` | Net EV per trade of the fade at R1: FLIP minus NO-FLIP (re-weighted) is not zero | **Primary.** α 0.05, two-sided |
| `A4L-H2` | Net EV per trade of the FLIP fade is not zero | Secondary, Holm with `A4L-H3` |
| `A4L-H3` | FLIP minus CONTROL (re-weighted) is not zero: does the cascade add anything over the same OFI state alone? | Secondary, Holm with `A4L-H2` |
| `A4L-H4` | The splits of `docs/a4-liq-ofi-logged-era-study-spec.md` §4.7, plus fade-signed ln(`OFIRatio`) at R1 (× +1 for `LONG` events, × −1 for `SHORT`): OLS slope of net EV on it, and its terciles | Descriptive. CIs printed, no label |

All tests are two-sided.

---

## 6. Counts and power (outcome-blind)

### 6.1 Handles

| Handle | Command (repo root) | Expected |
|---|---|---|
| `H-1` | `python tools/ops/a4_liq_ofi_counts.py` at `a42804a` | The block in `docs/a4-liq-ofi-logged-era-study-spec.md` §6.2, line for line, except the last line (`runtime N s`). Data frozen; no clock dependence. Checked for this spec: a fresh run at `a42804a`, diffed against this block (blank lines ignored), printed nothing |
| `H-2` | `python tools/ops/a4_liq_ofi_counts.py --selftest` | `SELFTEST PASS (0 failure(s))`, 29 checks |
| `H-3` | Session 2's outcome script, before it opens any outcome column | Reproduces `H-1` sections 2–5 counts exactly at the same cut (events, drops by reason, FLIP/BALANCED/WITH per session × side, control counts) |
| `E-1` (evidence, not re-runnable as committed) | Three one-line mutants of the tool in a scratchpad copy, each run with `--selftest` | All three red: (1) `M` booked by taker direction → 3 failures; (2) R1 not strict (`bisect_left`) → 2 failures; (3) threshold `>=` instead of `>` → 2 failures. To repeat: make the named one-line edit in a copy and run `--selftest` |

### 6.2 Output of `H-1` (pasted, not edited; runtime line omitted)

```
A4 liquidation x OFI flip - logged-era counts (OUTCOME-BLIND). docs/a4-liq-ofi-logged-era-study-spec.md
store files: 21 (trades_2025-01.csv .. trades_2026-09.csv)
book md5 e8418846838ff97f3c90f782a95b3523  AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
book md5 2721073ae9d13e09acaaab117adcf481  aws_fetch/20261002-121123/analysis_log.csv.v0.7.bak
book md5 fe6bfa4fe148e03edd424fdca8b38648  aws_fetch/20261002-121123/analysis_log.csv.116col-83564b1b.20260924_184606.bak
book md5 5d0bb3aa91aaff0607d54c165e5a281d  aws_fetch/20261002-121123/analysis_log.csv
=== 0. Inputs and pins ===
LLS-1 thresholds (USD, strict >): ASIA=69535 LONDON=83250 NY=49724
window=500 trades  dominance=2.0  de-cluster gap=30 min  join W: NY 90s ASIA 210s LONDON 210s  OFI pair 1.60/0.625
store: trades=71766893  flags T=51557 M=5711 MT=66  unrecognised skipped=0  out-of-order=76
grid: runs kept=64373  first=2026-07-03 15:57:49  last=2026-09-30 23:59:03  collector start=2026-07-22 16:24:54
grid drops: after_data_cut=1146  concurrent_instance_dropped=12  dup_key_dropped=41195  pooled_short_row_w111=40398
grid shape: holes > 10 min, collector era=11  holes > 10 min, dev era=31  runs < 50 s apart, collector era=3098  runs < 50 s apart, dev era=1022
firing spans=1091  events (onsets)=450

=== 1. Events per session x half-year, whole store (no join; LONG/SHORT = liquidated side) ===
period   ASIA L/S      LONDON L/S    NY L/S        all   two-sided
2025H1   43/6          9/7           58/20         143   6
2025H2   39/8          12/5          51/21         136   9
2026H1   28/9          14/7          55/24         137   6
2026H2   5/4           6/2           12/5          34    1
all      450 events over 630 days = 0.71 per day

=== 2. Join to the logged run grid (events with onset inside the logged span) ===
events inside the logged span: 34   (outside: 416)
  joined                       28  (82.4 %)
  no_run_in_W_named_gap         3  (8.8 %)
  no_run_in_W_dev_era           2  (5.9 %)
  no_run_in_W_other             0  (0.0 %)
  r1_ofi_invalid                0  (0.0 %)
  r1_health_snapshot_proxy      1  (2.9 %)
    named-gap drop: T0=2026-09-18 13:47:17 NY (venue outage 09-18..09-21)
    named-gap drop: T0=2026-09-20 03:02:24 ASIA (venue outage 09-18..09-21)
    named-gap drop: T0=2026-09-21 08:38:50 LONDON (venue outage 09-18..09-21)
drop rate: all reasons 17.6 %; reasons not named in the spec 0.0 %

=== 3. Joined events: per session x side x month (onset month) ===
session side   2026-07  2026-08  2026-09  all
ASIA    LONG   1        1        0        2
ASIA    SHORT  0        2        2        4
LONDON  LONG   0        3        3        6
LONDON  SHORT  0        1        0        1
NY      LONG   1        6        5        12
NY      SHORT  1        2        0        3
event days=20  halves (first floor(D/2) days): H1 16 events (2026-07-13..2026-08-24)  H2 12 events

=== 4. Flip state at R1 (signal side only) ===
session  side    FLIP  BALANCED  WITH   | R1 fires large  R1 lag p50/p90 s  R1 lag<10s | R0 present  R0 FLIP/BAL/WITH
ASIA     LONG       0         0     2   |              2      85/85                 0 |          2  0/0/2
ASIA     SHORT      0         2     2   |              1     115/153                0 |          4  2/1/1
ASIA     both       0         2     4   |              3     110/153                0 |          6  2/1/3
LONDON   LONG       1         3     2   |              4     153/175                0 |          6  1/3/2
LONDON   SHORT      1         0     0   |              0     132/132                0 |          1  0/1/0
LONDON   both       2         3     2   |              4     132/175                0 |          7  1/4/2
NY       LONG       6         4     2   |             10      24/59                 1 |         12  2/5/5
NY       SHORT      0         2     1   |              2      26/58                 1 |          3  0/2/1
NY       both       6         6     3   |             12      24/59                 2 |         15  2/7/6
ALL      LONG       7         7     6   |             16      54/168                1 |         20  3/8/9
ALL      SHORT      1         4     3   |              3     110/153                1 |          8  2/4/2
ALL      both       8        11     9   |             19      58/153                2 |         28  5/12/11
R1 before the v51 placed-geometry edge (excluded from the outcome population): 0
weekday FLIP/NO-FLIP: 8/18   weekend FLIP/NO-FLIP: 0/2
R1 -> R2 state (descriptive): FLIP->FLIP=5  FLIP->BALANCED=2  FLIP->WITH=1  BALANCED->FLIP=1  BALANCED->BALANCED=7  BALANCED->WITH=3  WITH->FLIP=0  WITH->BALANCED=5  WITH->WITH=4
joined events whose episode also fired on the other side (kept; counted only): 0

=== 5. OFI-only control pool (A4L-H3): healthy runs, no large firing in the 30 min before ===
runs within 30 min after a firing (excluded from the pool): 788
ASIA    BUY-DOM (long)   3328   SELL-DOM (short)   3326   balanced   3568   | pre-v51 excluded 0
LONDON  BUY-DOM (long)   2326   SELL-DOM (short)   2336   balanced   2347   | pre-v51 excluded 14
NY      BUY-DOM (long)  15085   SELL-DOM (short)  14009   balanced  16576   | pre-v51 excluded 199

=== 6. Power (outcome-blind; sigma is a published proxy, scaled by ATR) ===
ASIA    median ATR (USD): all runs 47.24  events at R0 82.87 (n=6)  scale 1.75
LONDON  median ATR (USD): all runs 42.34  events at R0 61.29 (n=7)  scale 1.45
NY      median ATR (USD): all runs 30.19  events at R0 82.85 (n=15)  scale 2.74
pooled scale 2.45
outcome population (R1 at/after the v51 edge): FLIP=8  NO-FLIP=20  control=40410
test                              alpha  sigma(bps, xATR)   MDE80 bps  pow@5bps  pow@10bps  pow@20bps  pow@40bps   readable (n>=100)
A4L-H1 FLIP - NO-FLIP             0.050  conservative   84.1        98.6       0.03       0.05       0.08       0.21   NO
A4L-H2 FLIP vs 0                  0.025  conservative   84.1        91.7       0.02       0.03       0.06       0.19   NO
A4L-H3 FLIP - OFI-only control    0.025  conservative   84.1        91.7       0.02       0.03       0.06       0.19   NO
A4L-H1 FLIP - NO-FLIP             0.050  optimistic     36.0        42.2       0.05       0.10       0.26       0.76   NO
A4L-H2 FLIP vs 0                  0.025  optimistic     36.0        39.3       0.03       0.07       0.25       0.82   NO
A4L-H3 FLIP - OFI-only control    0.025  optimistic     36.0        39.3       0.03       0.07       0.25       0.82   NO
collector-era accrual: 62.5 covered days (span 70.3 days minus grid holes > 10 min); FLIP 0.128/day  NO-FLIP 0.304/day
  FLIP reaches 100 at this rate after 719 more covered days (from 2026-10-01: 2028-09-18)
  NO-FLIP reaches 100 at this rate after 263 more covered days (from 2026-10-01: 2027-06-21)
```

### 6.3 What the count says

| Reading | Value | Consequence |
|---|---|---|
| Events, whole store | 450 over 630 days (0.71/day); ~140 per half-year until 2026 H1 | Large clusters are rare even at full history |
| Events, 2026 H2 | **34**, all inside the logged span | The logged era is the quiet half (the `LLS-1` read's 2026 H2 p90 was about half the pooled value) |
| Join | 28 of 34 joined (82.4 %). Drops: 3 venue outage, 2 dev era, 1 snapshot proxy. **0 unnamed** | The join is sound. The drop trigger (> 10 % unnamed) is not met |
| One named-gap drop is the 2026-09-21 08:38 cascade | The first real-data `D-5` case ([`liquidation-park-spec.md`](liquidation-park-spec.md) §2) | It falls inside the venue outage; no logged run exists for it |
| Flip state | FLIP 8 · BALANCED 11 · WITH 9. NY holds 6 of the 8 FLIP; ASIA has 0 | No session is readable; no session × side cell exceeds 12 |
| Side | 20 `LONG` (long liquidations) vs 8 `SHORT` | Re-weighting by side matters; the `SHORT` arm is near-empty |
| R1 lag | NY p50 24 s; ASIA/LONDON p50 110–132 s | ASIA and LONDON R1 is often 2 minutes after onset. The 3-min grid is coarse for a "flip" |
| R1 still fires large | 19 of 28 | The engine-mirrored variant (`A4L-1` option (c)) would hold 19 events |
| ATR at events | 1.45–2.74× the session median (pooled 2.45×) | σ is scaled up; placed distances are wide at events |
| Control pool | 40,410 runs | Not the binding constraint; FLIP is |

**Power verdict.**

- **NOT READABLE on every test**, by the census rule (FLIP n = 8 against a floor of 100).
- **MDE at 80 % power: 92–99 bps (conservative σ) or 39–42 bps (optimistic σ).** The engine's largest published tier gap is +2.6 bps (NY STRONG vs MEDIUM, [`tier-order-stability-read-2026-09-15.md`](tier-order-stability-read-2026-09-15.md) §1, quoted from [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §3, not re-read). A plausible A4 effect is far below either MDE.
- **Readability date:** FLIP reaches 100 about **2028-09-18** at the collector-era rate. If activity returned to the pre-2026-H2 rate (416 events in 546 days = 0.76/day), and the join rate (82 %) and FLIP share (8 of 28) held: about 0.18 FLIP/day, so about 555 days, **~2028-04**. Hand-computed from the output; not a tool line.
- **No redesign was made to chase power** (the brief's rule). The options are in decision `A4L-10`.

---

## 7. STOP conditions for session 2

A STOP is reported to the trader before any outcome is read. **A STOP is not an invitation to adapt.**

1. `H-3` fails: any event, drop, arm or control count differs from `H-1` at the same cut.
2. FLIP < 100 or NO-FLIP < 100 in covered strata, **unless** the trader ruled `A4L-10` option (b) in writing first. Without that ruling the run does not open outcome columns.
3. An outcome window contains a store day that is not `COMPLETE` in `C:\DeribitData\history\status_final.txt`.
4. Any outcome-side drop (bad placed levels, no trade in the window) exceeds 10 % of the population for a reason this spec does not name.
5. `scoring.trade_costs` is not maker/maker 3 bps round trip in tracked `settings.json`.
6. The book md5s or the store row count differ from the pins in `docs/a4-liq-ofi-logged-era-study-spec.md` §8 while the run claims this cut.
7. Any rule here needs a judgement call. Record the deviation **before** looking at the affected numbers.

---

## 8. Run pins

| Pin | Value |
|---|---|
| Data cut | `Timestamp` < 2026-10-01 00:00:00 UTC (both the run grid and the outcome walk's entries) |
| Store | 21 files `trades_2025-01.csv` … `trades_2026-09.csv`, 71,766,893 rows, `status_final.txt` 638 OK / 0 GAP / 0 FAILED |
| Books | The four md5s in the `H-1` output, `docs/a4-liq-ofi-logged-era-study-spec.md` §6.2 |
| Tool | `tools/ops/a4_liq_ofi_counts.py` at `a42804a` |
| Seed / resamples | 20261002 / 10,000 |
| Settings | Tracked `settings.json` version 69; fee 3 bps maker/maker |
| A later cut | A run on a later cut (after a store refresh and a new fetch) is a **new pre-registration of the same design**: record the new cut, md5s and `H-1` output in this spec before opening outcomes |

---

## 9. Decisions

Each row was run through harness 6, the decision-bias tripwire (`tools/checks/measure/decision-bias/run-decision-bias.ps1`, 5 samples). My labels were written before the run (shadow-mode rule). Files: `docs/harness-runs/decision-bias-20261002T1527Z-{population,baseline,jev}.json`. **Jev flagged no row as `gives_up_for_economy`. My own label flags one (`A4L-9`).**

| ID | Question | Options | My read | My label · Jev (agreement, mean p) | Status |
|---|---|---|---|---|---|
| **`A4L-1`** | Event anchor | (a) onset T0, R1 after it · (b) the episode's last firing trade · (c) the first run whose own window fires large | **(a).** (b) is **mechanically wrong**: it conditions entry on the future absence of more liquidations, which biases toward reversal. (c) is a subset of (a), printed (19 of 28) | `richer_option_wrong` · `richer_option_wrong` (5/5, 0.88) | Auto-proceeded |
| **`A4L-2`** | De-cluster gap | (a) 30 min, any side · (b) 15 min · (c) the session's outcome window · (d) 60 min | **(a).** No option records more: shorter adds dependent events from one cascade; longer merges separate cascades. Days are the bootstrap cluster | `no_richer_option` · `richer_option_wrong` (5/5, 0.56) | Auto-proceeded |
| **`A4L-3`** | Flip definition | (a) state at R1, re-thresholded · (b) transition R0 → R1 · (c) continuous fade-signed ln OFI | **(a) primary, (c) descriptive.** (a) is the engine's own class, the rule an un-park would deploy. (b) is **not more faithful**: A4's flip starts from the during-cascade book, and R0 is the pre-cascade book. R0 is printed anyway | `richer_option_wrong` · `richer_option_wrong` (5/5, 0.91) | Auto-proceeded |
| **`A4L-4`** | R1 rule | (a) strictly later second, within W, same for both arms · (b) the first fade-side run within N min · (c) (a) plus a 10 s minimum lag | **(a).** (b) gives the arms different entry instants and selects on future OFI: **mechanically wrong**. (c) is printed (2 events under 10 s) | `richer_option_wrong` · `richer_option_wrong` (5/5, 0.88) | Auto-proceeded |
| **`A4L-5`** | Comparison | (a) FLIP vs NO-FLIP · (b) plus FLIP vs 0 · (c) plus FLIP vs OFI-only control | **(c)**, the richest. It asks whether the cascade adds anything over the OFI vote the engine already has | `no_richer_option` · `no_richer_option` (5/5, 0.95) | Auto-proceeded |
| **`A4L-6`** | Outcome measure and instrument | (a) house net EV per trade from R1's placed levels, house window, tape walk · (b) fixed-horizon mark · (c) (a) decides, (b) descriptive · (d) (a) on 1-min candles | **(c), tape walk.** The tape resolves target and stop at trade level, so no ambiguity is scored as a stop: more truthful than (d). (d)'s only advantage is comparability with candle-walk reads | `no_richer_option` · `richer_option_wrong` (5/5, 0.89) | Auto-proceeded |
| **`A4L-7`** | Weekends | (a) all days, split printed · (b) weekdays only | **(a).** Liquidations and the collector run every day; queue row `E7` says a cascade on any day counts. The split shows any difference | `no_richer_option` · `richer_option_wrong` (5/5, 0.53) | Auto-proceeded |
| **`A4L-8`** | R1 rows that may carry snapshot OFI | (a) drop and count · (b) keep | **(a).** Those rows hold a different construction under the same column ([`overlay-d7-and-row-source-stamp-2026-07-31.md`](overlay-d7-and-row-source-stamp-2026-07-31.md) §2: REST fallback reverts OFI to a snapshot, unmarked). 1 event | `richer_option_wrong` · `richer_option_wrong` (5/5, 0.98) | Auto-proceeded |
| ⚠ **`A4L-9`** | Multiplicity | (a) `A4L-H1` alone at 0.05; `A4L-H2`/`A4L-H3` Holm at 0.05 · (b) Holm across all three · (c) Bonferroni across all three | **(a)**, the burst read's structure. ⚠ **(b) guarantees more:** it caps the familywise error at 0.05 over all three tests; (a) allows up to about 0.10 across both families. My reason for (a) is power on the primary — **a cost argument, so step 2 of the three-step test reserves it** | `gives_up_for_economy` · `no_richer_option` (5/5, 0.43) | ⛔ **QUEUED for the trader** (my own flag) |
| ⛔ **`A4L-10`** | Session 2, given the count is unreadable | (a) keep the outcome sealed: park session 2, re-run `H-1` at each store refresh, run when FLIP ≥ 100 or after a trader-ruled redesign · (b) run now as pre-registered; every label NOT READABLE; CIs bound only effects above ~40–100 bps · (c) redesign for power now (lower threshold, or the standard liquidation signal) and re-register · (d) close the logged-era study; A4 waits for a real-time source and raw book | **(a).** (b) is **uninterpretable** under the census rule, and it unseals these days: any later redesign over the same span would no longer be outcome-blind. (c) departs from the ruled `LLS-1` study values, and the brief forbids this seat a redesign for power. (d) discards a cheap, outcome-blind accrual check. ⚠ (b) is the option that "records more" now; I reject it on mechanism (contamination of a later design), not cost | `richer_option_wrong` · `richer_option_wrong` (5/5, 0.99) | ⛔ **QUEUED for the trader** (escalation; scope of a ruled study) |

No choice here touches `settings.json`, scoring, a rendered value or a CSV schema.

---

## 10. What the result feeds

| Result (when readable) | Reading |
|---|---|
| `A4L-H1` CONFIRMED > 0 and `A4L-H3` CONFIRMED > 0 | Argues for an A4-style fade reward **at un-park**: the flip adds over both the cascade alone and the OFI vote alone. Still needs a real-time liquidation source |
| `A4L-H1` CONFIRMED > 0, `A4L-H3` not | The flip matters given a cascade, but the existing OFI vote may already carry it. No new reward without a further spec |
| `A4L-H2` CONFIRMED < 0 | The fade loses even with the flip. Supports the current penalty-only design ([`trader-profile.md`](trader-profile.md) §3, Liquidations row) |
| NOT READABLE, NO DIFFERENCE SHOWN, or anything else | Undecided on outcome evidence. The CIs bound the effect; record them |

---

## 11. Pre-registration record — what the author saw

- **No outcome was computed or read.** The counts tool never parses a store price column (the self-test writes `NOT_A_PRICE` there and passes). From the logged books it reads only `Timestamp`, `OFIRatio`, `ATR`, `AggrVelBurstRatio`, `WsHealth`, `InstanceId`, `SignalId`.
- **Exploration scripts** (scratchpad, not committed) read book headers, row widths, instance ids, `OFIRatio` precision (4 decimals), `LiqSignal` (`NONE` on every row, as `L-1` predicts), and the fill rate of `ATR`, `Price` and the four placed-level columns on NO TRADE rows (non-empty on every row). They printed **5 example rows** containing `Price` and `ATR` as signal-time values. No price path, return or level-vs-price comparison was computed.
- **Read for the design:** `burst-outcome-read-spec.md` (all), `large-liq-size-rederivation-2026-10-02.md` (all) and its `derive.py`, `history-store-queue-reshape-evaluation.md` (all), the relevant parts of `liquidation-park-spec.md`, `DeribitIndicatorProject.md` §5a, `trader-profile.md` (grep for liquidation, OFI, false positives), `settings.json` v46/v48 `change_log` entries, `overlay-d7-and-row-source-stamp-2026-07-31.md` (grep). None contains an A4 outcome; the only A4 numbers anywhere are counts.

---

## 12. Harness log

| Harness | Use | Result |
|---|---|---|
| 5 · doc re-ranker | "Where was the A4 gate decided, and the `HSR-11` join rules?" | **Hit.** Top 1 = `trader-tick-queue.md` Cluster E (row `E7`); ranks 3 and 5 = `history-store-queue-reshape-evaluation.md`. All three were docs the brief had named |
| 5 · doc re-ranker | "Where is it decided that time-averaged OFI falls back to a snapshot, and is it flagged in `analysis_log.csv`?" | **Partial hit, useful.** Rank 3 = `time-averaged-ofi-proposal.md` §4.2. Rank 1 = `overlay-d7-and-row-source-stamp-2026-07-31.md` §2, which I had not found: REST-fallback rows are unmarked in the older books. It supports `A4L-8` |
| 6 · decision-bias tripwire | The 10 decisions in `docs/a4-liq-ofi-logged-era-study-spec.md` §9 | 50 calls, 0 unstable, **0 Jev flags**. Disagreements with my labels on `A4L-2`, `A4L-6`, `A4L-7` (benign: both labels say "nothing given up") and `A4L-9` (I flag it; Jev does not, at p 0.43). I keep my flag |

The re-ranker's index revision read `34f0af5` (one commit after my start commit), as printed by the harness.

---

## 13. What I did not verify

| Claim | Status |
|---|---|
| The logged `Timestamp` is the run instant truncated to the second | Assumed. The strict-second R1 rule is safe either way, but a rounded (not truncated) timestamp could shift R0 by one run |
| 3,098 collector-era runs < 50 s apart are legitimate runs (backstop or manual triggers) | Not traced. R1 takes whichever run comes first; their trigger was not checked |
| `AggrVelBurstRatio` empty marks warm-up or REST fallback | Carried from [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §5 and the D7 doc. Not checked in code |
| The 10 s OFI average's own warm-up after a reconnect is always flagged by that proxy | Not verified. The two warm-ups need not coincide |
| σ for this population | No published value exists. Both σ values are proxies from [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §3 (directional verdict rows, not fade trades), scaled by an ATR ratio. The ATR-scaling of σ is an assumption |
| The 76 out-of-order trades in the store | Counted, not investigated. The window treats file order as time order, as `derive.py` does |
| `LLS-1` thresholds applied trade by trade | They were derived from minute-close samples. Trade-by-trade evaluation fires a little more often than minute sampling would; not quantified |
| The +2.6 bps tier gap | Quoted through [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §3; I did not open the source read |
| `MT` handling | 66 `MT` trades in the store; the "both sides, full amount" rule is the park spec's reading of `D-5` ([`liquidation-park-spec.md`](liquidation-park-spec.md) §7.1), untested against the venue |
