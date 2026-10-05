# Liquidation × trade-flow flip study — pre-registered spec (written 2026-10-05 UTC)

**Owed by:** ruling `A4L-10` option (e), in [`a4-liq-ofi-logged-era-study-spec.md`](a4-liq-ofi-logged-era-study-spec.md) §9 RULED box (trader, 2026-10-03): a separate pre-registered study of trade-flow flips after liquidation clusters, trade store only, 2025-01-01 → 2026-07-02.
**Start commit:** `0af9025`. **Tool commit:** `848be8a`. Counts run at `848be8a`.
**Status:** SESSION 1 DONE. **No outcome has been computed, printed or looked at.**

> ⛔ **ESCALATION TRIGGER MET — STOP, report, do not decide** (the brief's trigger: "the event count is unreadable even on 18 months").
>
> - **413 events measured; FLIP 87, NO-FLIP 326.** The census readability floor is n ≥ 100 per group. FLIP misses it by 13.
> - **The span is closed.** 2025-01-01 → 2026-07-02 never grows, so waiting cannot fix the count. This differs from the sister study, which accrues.
> - **Each chronological half holds 41 and 46 FLIP.** So CONFIRMED (both halves readable) is out of reach even if FLIP reached 100.
> - **Minimum detectable effect (MDE) at 80 % power, Holm worst-case α:** 32.8 bps (conservative σ, carried), 14.1 bps (optimistic σ × the carried ATR scale), 5.7 bps (optimistic σ, unscaled). σ is not measured for this population.
> - The design below is complete and pre-registered as written. What session 2 does is decision `TFS-12` in `docs/liq-tradeflow-flip-study-spec.md` §9, for the trader.

⛔ **This is a READ spec. It changes no setting and no scoring.** A positive result cannot ship: the live liquidation vote is parked (ruling `D-4`), and the live stream never carries the liquidation flag (finding `L-1`). This study is research for the un-park question.

---

## 0. What is tested, and what is not

| Item | Tested here? |
|---|---|
| **A4 as defined** — a liquidation cascade, then OFI flips against it within ~200 ms ([`post-websocket-post-calibration-backlog.md`](post-websocket-post-calibration-backlog.md), "A4. Liquidation × OFI Flip Detector") | ❌ **No.** The history store has no order book. OFI is the sealed sister study |
| **The trade-flow proxy of A4** — after a large liquidation cluster, taker aggressor imbalance of non-liquidation trades turns against the cascade, and the fade then pays | ✅ Yes. This is the study |
| Whether the cascade adds anything over following the same flow state alone | ✅ Yes, test `TFS-H3` |
| Any trade at or after **2026-07-03 00:00:00 UTC** | ❌ **Never read.** That span belongs to the sealed sister study ([`a4-liq-ofi-logged-era-study-spec.md`](a4-liq-ofi-logged-era-study-spec.md)) |
| The engine's TFI signal as computed live | Descriptive only (variant `TFI30`, `docs/liq-tradeflow-flip-study-spec.md` §4.3) |

---

## 1. Model and effort for SESSION 2 (the outcome run)

**Model: Opus 5.5 · Effort: HIGH.**

- **Why that tier:** the design is fixed here. The work is holding it against the tape and refusing to adapt after outcomes are seen. The FLIP arm is small (87), so every per-event judgement shows in the result.
- **Where it will slip:**
  1. **Reading past the fence.** The outcome walk needs prices after D. Every walk must stop before 2026-07-03 00:00 UTC. The population already excludes outcome windows that cross it (`docs/liq-tradeflow-flip-study-spec.md` §4.7). Keep the fenced reader; do not glob the store.
  2. **The fade side.** A `LONG` event (long liquidations, cascade down) is faded LONG. Control rows trade WITH their flow. A swap still gives plausible numbers.
  3. **Entry inside the flow window.** Entry is the first trade at or after D, never a trade inside the window.
  4. **ATR from the wrong candles.** ATR(7) is Wilder on execution-resolution candles: 1 min for NY, 3 min for ASIA and LONDON, by the event's session at T0. Build them from trade prices before D only.
- **The fixtures cannot catch a misunderstanding:** the seat writes the outcome script and its self-test. The guard is `H-3` (`docs/liq-tradeflow-flip-study-spec.md` §6.1): the outcome script must reproduce the counts in sections 1–6 of the `H-1` block before it opens any price after D.
- **Escalate** (stop, report, do not decide) if: `H-3` fails; any rule in `docs/liq-tradeflow-flip-study-spec.md` §4 cannot be applied as written; any outcome-side drop exceeds 10 % for a reason not named in `docs/liq-tradeflow-flip-study-spec.md` §7; or the fee or the ATR multipliers in tracked `settings.json` differ from the pins in `docs/liq-tradeflow-flip-study-spec.md` §8.
- **Session split:** (a) build `tools/ops/liq_tradeflow_read.py` and its self-test, commit it — Opus 5.5 medium; (b) run it — Opus 5.5 high. The script is committed before its first run on outcome columns.
- ⛔ **Session 2 does not start until the trader rules `TFS-12`.**

---

## 2. IDs used here

| ID | Source and kind | Meaning |
|---|---|---|
| A4 (backlog item) | [`post-websocket-post-calibration-backlog.md`](post-websocket-post-calibration-backlog.md), section A | The liquidation × OFI flip detector. ⚠ **Not fixture `A4`** (`A4_TfiWindowFromEnd` in `verify/ordercheck/Program.vb`) |
| `A4L-1` … `A4L-10` | [`a4-liq-ofi-logged-era-study-spec.md`](a4-liq-ofi-logged-era-study-spec.md) §9, the sister study's decisions | `A4L-1`: event onset T0. `A4L-2`: 30-min de-clustering. `A4L-9` = (b), **trader-ruled**: Holm across all tests in a study. `A4L-10` = (a)+(e), **trader-ruled**: sister sealed; this study is (e) |
| `D-4`, `D-5` | [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.3, trader decisions | `D-4`: the live liquidation vote is PARKED. `D-5`: book `T` to the taker's side, `M` to the maker's side, `MT` to both |
| `EF-3` | [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §6, trader decision | An unrecognised liquidation flag is skipped and counted |
| `L-1` | [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R, finding | The live trade stream never carries the liquidation flag |
| `LLS-1` | [`large-liq-size-rederivation-2026-10-02.md`](large-liq-size-rederivation-2026-10-02.md) §4, trader ruling 2026-10-02 | Per-session large-liquidation thresholds. Study values (`D-5` booking): ASIA 69,535 · LONDON 83,250 · NY 49,724 USD |
| **`TFS-H1` … `TFS-H4`** | **This spec, hypotheses (NEW)** | `docs/liq-tradeflow-flip-study-spec.md` §5 |
| **`TFS-1` … `TFS-12`** | **This spec, decisions (NEW)** | `docs/liq-tradeflow-flip-study-spec.md` §9 |
| `H-1` … `H-3`, `E-1`, `E-2` | This spec, handles (`H`, runnable) and evidence (`E`, not runnable as committed) | `docs/liq-tradeflow-flip-study-spec.md` §6.1 |

The `TFS` prefix was checked with `git grep -n -E "TFS-?[0-9]|TFS-H|\bTFS\b"` at `0af9025`: one hit, `.gitignore:124` "`# TFS 2012 Local Workspace`" (Microsoft Team Foundation Server, an ignore-file comment). No ID collision.

---

## 3. The mechanism

- **The cascade** — the sister study's events, unchanged: `IndicatorEngine.CalcLiquidations` (`Core/Indicators_OrderFlow.vb`) over the last 500 trades, dominance 2.0, "large" = dominant sum **>** the `LLS-1` threshold of the trade's session. `LONG` = long liquidations, cascade **down**, fade **LONG**. `SHORT` = short liquidations, cascade **up**, fade **SHORT**.
- **Why trade flow, not OFI.** The history store holds trades, not books. The closest trade-side quantity is taker aggressor imbalance: the engine's `CalcTFI` (`Core/Indicators_OrderFlow.vb`) is `(buy USD − sell USD) / total` over a trade window. `TFI` is PREFERRED in [`trader-profile.md`](trader-profile.md) §3.
- ⛔ **The cascade measures itself unless it is cut out.** A liquidation print is an aggressive order in the cascade direction (a forced long exit is a taker sell). Flow measured over the cascade is WITH by construction. So:
  - flagged trades never enter the measure, and
  - the window starts **after the last liquidation print**, and any later print restarts it (`TFS-1`).
- **"Flow against the cascade"** = taker buying after a `LONG` event (I > upper edge), taker selling after a `SHORT` event (I < lower edge).

---

## 4. Pre-registered design

### 4.1 Data and the date fence

| Item | Rule |
|---|---|
| Store | Dev history store `C:\DeribitData\history\`, files `trades_2025-01.csv` … `trades_2026-07.csv` opened **by explicit name** (19 files). Flags final |
| Columns read | `Timestamp`, `Amount` (USD), `Direction`, `Liquidation`. **`Price` is never converted in session 1** (the self-test writes `NOT_A_PRICE` there) |
| ⛔ Fence | Reading stops at the first line with `Timestamp` ≥ **2026-07-03 00:00:00.000 UTC** (1783036800000 ms). That line is tokenised for its timestamp only. Every trade passed on is asserted < the fence; the run asserts max trade read < fence |
| Span read | 2025-01-01 00:00:01.635 → 2026-07-02 23:58:51.992 UTC; 63,067,819 trades; 0 out of order |
| Outcome fence | An event or control row whose outcome window (`docs/liq-tradeflow-flip-study-spec.md` §4.8) ends at or after the fence is dropped and counted |

### 4.2 Events — the sister's code, imported

| Item | Rule |
|---|---|
| Code | `scan_rows` and `iter_store_rows` from `tools/ops/a4_liq_ofi_counts.py` (factored out of its `scan_store`; parity proved, `E-2`). Not rewritten |
| Booking, window, signal, large | As the sister: `D-5` booking, `EF-3` skip-and-count, last 500 trades evaluated after every trade, dominance 2.0, strict > `LLS-1` |
| Sessions | ASIA 0–7, LONDON 8–12, NY 13–23 UTC, end hour inclusive |
| Onset T0 and de-clustering | `A4L-1` and `A4L-2`: a firing trade with no firing in the 30 min before it, either side. Past-only |
| Event side and session | The firing side and the session at T0. Episodes that also fire on the other side are kept and counted (21 in the population) |

### 4.3 The flow window (`TFS-1` … `TFS-4`)

| Item | Rule |
|---|---|
| Anchor | Starts at T0. **Every liquidation print** (any flag, either side) at or after the anchor moves the anchor to that print |
| Window, primary `Q0L60` | Non-liquidation trades with anchor + Q < `Timestamp` < anchor + Q + L, Q = 0 s, L = 60 s. Open on the left, so same-millisecond legs of the anchoring print are excluded |
| Decision instant D | anchor + Q + L, reached with no liquidation print inside (anchor, D). Knowable in real time at D |
| Imbalance | I = (B − S) / (B + S), B and S the USD `Amount` of taker buys and sells in the window |
| Cap | D − T0 > 30 min → drop `not_quiet_within_cap` |
| Thin | Fewer than 10 non-liquidation trades, or B + S = 0 → drop `thin_flow_window` |
| Fence | D not reached before the fence → drop `flow_window_crosses_fence` |
| Variants (descriptive) | `Q0L30`, `Q0L120`, `Q30L60` (30 s buffer after the last print), `TFI30` (the first 30 non-liquidation trades after the anchor, D = the 30th trade; the engine's window size, read from tracked `settings.json` `indicators.TFI.window_size`) |
| Also recorded | Number of anchor resets; trades in the window; whether the 500-trade window still fires large at the trade before D (engine-mirrored state) |

### 4.4 Flip classification (`TFS-5`, `TFS-7`)

| Item | Rule |
|---|---|
| Threshold source | The reference grid (`docs/liq-tradeflow-flip-study-spec.md` §4.5) only. **No outcome enters** |
| Edges | Per session × variant, over the whole study span: lo = the ⌈n/3⌉-th smallest reference I, hi = the ⌈2n/3⌉-th smallest |
| Classes (strict) | `LONG` event: **FLIP** if I > hi, **WITH** if I < lo, else **BALANCED**. `SHORT` event: FLIP if I < lo, WITH if I > hi. **NO-FLIP** = BALANCED ∪ WITH |
| Sensitivity | Trailing 90-day terciles (same session, reference D in [D − 90 d, D), at least 1,000 windows). Printed now; in session 2 a sign change of any tested difference under them adds "THRESHOLD-SENSITIVE" to the label |
| Engine threshold | `TFI30` is also classed at ± `indicators.TFI.threshold` (0.15 at v69). Descriptive |

### 4.5 Reference grid and the flow-only control (`TFS-6`)

| Item | Rule |
|---|---|
| Instants | g = every 15 min on the UTC clock (:00, :15, :30, :45) |
| Window | Non-liquidation trades with g ≤ `Timestamp` < g + L (time variants); the first 30 non-liquidation trades from g (`TFI30`) |
| Excluded | A liquidation print inside the window · a large firing in [g − 30 min, window end) · thin (same rule as events) |
| Control row (`TFS-H3`) | A primary reference window with I > hi (trades LONG) or I < lo (trades SHORT). Entry at the first trade at or after g + 60 s. Same outcome rules as events |

### 4.6 Arms (`TFS-9`)

| Arm | Rows | Trade |
|---|---|---|
| **FLIP** | Population events in the FLIP state | The fade |
| **NO-FLIP** | Population events BALANCED or WITH | The fade |
| **CONTROL** (flow-only) | Control rows (`docs/liq-tradeflow-flip-study-spec.md` §4.5) | With the flow |

### 4.7 Population

- Primary status `measured`, and the outcome window ends before the fence. At this cut: **413 of 416 events; 0 dropped for the outcome fence.**
- Control: 31,158 rows (1 dropped for the outcome fence).

### 4.8 Outcome — defined, NOT computed (`TFS-8`)

| Item | Rule |
|---|---|
| Entry | The first trade with `Timestamp` ≥ D; its `Price` (for `TFI30`: the first trade after the 30th) |
| ATR | ATR(7), Wilder (`IndicatorEngine.CalcATR`, `Core/Indicators_Momentum.vb:98`), over the last 50 completed execution-resolution candles before D, built from trade prices: **1 min NY, 3 min ASIA and LONDON**, by the event's session at T0. `indicators.ATR.period` = 7 at v69 |
| Levels (the ATR-fallback geometry) | Target = m × ATR: **NY 1.75** (`scoring.atr_target_multiplier`), **LONDON 2.0**, **ASIA 1.25** (`scoring.structural_levels.sessions.*.fallback_target_atr_mult`). Stop = **1.6 × ATR** (`scoring.atr_stop_multiplier`). Distances in bps of entry. ⚠ Not the engine's structural-first placed levels (`TFS-8` option (d)) |
| Window | By session at T0: NY 15 min, ASIA and LONDON 45 min (`AnalysisConstants.HoldWindowsForResolution` maximum, `analysis/AnalysisConstants.vb:74`) |
| Walk | Trades after the entry trade and before entry + window. LONG: first price ≥ target → success; first ≤ stop → stop. SHORT mirrored. Trade-level, so no same-bar ambiguity |
| Net EV per trade, bps | [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a: +target − fee on success · −stop − fee on a stop · mark − fee on a timeout (mark = last trade price before the window end, signed for the side) |
| Fee | `scoring.trade_costs` maker/maker round trip: **3 bps at v69** (1.5 + 1.5). Pinned |
| Explaining numbers | Success rate, gross and net breakeven rates (Σ formulas, `DeribitIndicatorProject.md` §5a rule 1), edges. **Net EV per trade decides** (`DeribitIndicatorProject.md` §5a rule 4) |
| Descriptive | Fixed-horizon mark-to-market net return at the window end |

### 4.9 Statistics

| Item | Rule |
|---|---|
| CI | 95 % percentile bootstrap of whole UTC days, seed **20261005**, **10,000** resamples |
| Re-weighting | `TFS-H1`: NO-FLIP re-weighted to FLIP's session × side mix. `TFS-H3`: CONTROL re-weighted to FLIP's session × side × ATR tercile (edges per session from control-row ATR, printed). Strata without both groups dropped and counted |
| Code | Copy `boot`, `label`, `rw_run`, `holm`, `boot_p` from `tools/ops/burst_outcome_read.py`. Do not re-derive |
| Readable | n ≥ 100 in every group a statistic uses, counted in covered strata ([`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §5) |
| Multiplicity | **Ruling `A4L-9` (b): Holm across `TFS-H1`, `TFS-H2`, `TFS-H3`, familywise α 0.05** |
| Label (`TFS-10`) | The census rule, first match wins: NOT READABLE → CONFIRMED → "NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0" → H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT → H1 FINDING, H2 CONTRADICTS OR NOT READABLE → DISCOVERY ONLY (H2) → NO DIFFERENCE SHOWN ([`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §5) |
| Halves | First ⌊D/2⌋ UTC days holding a population event. At this cut: 212 event days; H1 2025-01-07 → 2025-10-10 (FLIP 41, NO-FLIP 154); H2 2025-10-11 → 2026-06-30 (FLIP 46, NO-FLIP 172) |

### 4.10 Splits (descriptive, CIs printed, never labelled)

- Session · side · period **2025H1 / 2025H2 / 2026H1\*** (\* = 2026-01-01 → 2026-07-02) · weekday/weekend (`TFS-11`) · BALANCED vs WITH.
- Variants `Q0L30`, `Q0L120`, `Q30L60`, `TFI30` (terciles and engine threshold) · trailing-threshold class · "500-window still fires at D" · anchor resets (terciles).
- `TFS-H4`: OLS slope of net EV on fade-signed I (× +1 for `LONG`, × −1 for `SHORT`), and its terciles.

---

## 5. Hypotheses

| ID | Statement | Role |
|---|---|---|
| `TFS-H1` | Net EV per trade of the fade: FLIP minus NO-FLIP (re-weighted) is not zero | Holm family of three |
| `TFS-H2` | Net EV per trade of the FLIP fade is not zero | Holm family of three |
| `TFS-H3` | FLIP minus CONTROL (re-weighted) is not zero: does the cascade add anything over following the same flow state alone? | Holm family of three |
| `TFS-H4` | The splits of `docs/liq-tradeflow-flip-study-spec.md` §4.10 | Descriptive |

All tests are two-sided. "Not zero" in either sign is a result.

---

## 6. Counts and power (outcome-blind)

### 6.1 Handles

| Handle | Command (repo root) | Expected |
|---|---|---|
| `H-1` | `python tools/ops/liq_tradeflow_counts.py` at `848be8a` | The block in `docs/liq-tradeflow-flip-study-spec.md` §6.2, line for line, except the last line (`runtime N s`). Runtime 128 s. Depends on the store files being unchanged (pin: 5,470,786,582 bytes, 63,067,819 trades) |
| `H-2` | `python tools/ops/liq_tradeflow_counts.py --selftest` | `SELFTEST PASS (0 failure(s))`, **27 checks**. ⚠ The tool commit message says "26 checks"; that is a miscount, 27 is the printed number |
| `H-3` | Session 2's outcome script, before it reads any price after D | Reproduces `H-1` sections 1–6 exactly |
| `E-1` (evidence) | 11 one-line mutants of the tool in scratchpad copies, each run with `--selftest` | **All 11 red** (failures in brackets): M1 flagged trades as ordinary flow (4) · M2 no anchor reset (4) · M3 window opens at ≥ anchor, same-ms legs admitted (2) · M4 fence `>` not `>=` (run raised on a poisoned line) · M5 classification not strict (1) · M6 `SHORT` not fade-signed (3) · M7 cap not enforced (1) · M8 thin rule removed (1) · M9 reference windows not excluded near a firing (5) · M10 outcome-fence drop removed (1) · M11 Q buffer ignored (1). To repeat: make the named edit in a copy and run `--selftest` |
| `E-2` (evidence) | The factored `scan_store` vs the start-commit copy, on `trades_2025-01.csv` … `trades_2026-06.csv` (pre-fence only) | `PARITY IDENTICAL`: 416 events, 1,028 spans, identical counters (62,840,728 trades) and identical per-event fields |

### 6.2 Output of `H-1` (pasted, not edited; runtime line omitted)

```
Liquidation x TRADE-FLOW flip - counts (OUTCOME-BLIND). docs/liq-tradeflow-flip-study-spec.md
store files: 19 (trades_2025-01.csv .. trades_2026-07.csv), bytes 5470786582
=== 0. Inputs and pins ===
fence: trades < 2026-07-03 00:00:00 UTC (1783036800000 ms); first trade read 2025-01-01 00:00:01.635000; last trade read 2026-07-02 23:58:51.992000; fence stop hit: yes
assert max trade read < fence: PASS
LLS-1 thresholds (USD, strict >): ASIA=69535 LONDON=83250 NY=49724  window=500 trades  dominance=2.0  de-cluster gap=30 min
store: trades=63067819  flags T=48615 M=5618 MT=66  unrecognised skipped=0  out-of-order=0
flow: variants Q0L60,Q0L30,Q0L120,Q30L60,TFI30 (primary Q0L60)  N_MIN=10  cap D-T0<=30 min  grid=15 min  ref exclusion: firing in [g-30 min, window end)
engine TFI (tracked settings.json v69): window 30 trades, threshold 0.15 (TFI30 descriptive class only)
hold window for the outcome-fence check: NY 15 min, ASIA 45 min, LONDON 45 min
counters: flow_bad_direction=0  flow_flag_out_of_order=0  grid_out_of_order_skipped=0
firing spans=1028  events (onsets)=416

=== 1. Events per session x period (LONG/SHORT = liquidated side; onset T0) ===
period    ASIA L/S      LONDON L/S    NY L/S        all   two-sided
2025H1    43/6          9/7           58/20         143   6
2025H2    39/8          12/5          51/21         136   9
2026H1*   28/9          14/7          55/24         137   6
all       416 events over 548 days = 0.76 per day

=== 2. Flow-window coverage, primary Q0L60 (counts per session x period) ===
session period    events  measured   not_quiet  thin_flow  flow_wind  outcome_fence  population
ASIA    2025H1        49         48          0          1          0              0          48
ASIA    2025H2        47         47          0          0          0              0          47
ASIA    2026H1*       37         37          0          0          0              0          37
ASIA    all          133        132          0          1          0              0         132
LONDON  2025H1        16         16          0          0          0              0          16
LONDON  2025H2        17         16          0          1          0              0          16
LONDON  2026H1*       21         20          0          1          0              0          20
LONDON  all           54         52          0          2          0              0          52
NY      2025H1        78         78          0          0          0              0          78
NY      2025H2        72         72          0          0          0              0          72
NY      2026H1*       79         79          0          0          0              0          79
NY      all          229        229          0          0          0              0         229
ALL     2025H1       143        142          0          1          0              0         142
ALL     2025H2       136        135          0          1          0              0         135
ALL     2026H1*      137        136          0          1          0              0         136
ALL     all          416        413          0          3          0              0         413
ASIA    measured: D-T0 s p50/p90/max 62/121/702  liquidation-print resets p50/p90 9/87  trades in window p10/p50 55/305  500-window still fires at D 81/132
LONDON  measured: D-T0 s p50/p90/max 62/140/282  liquidation-print resets p50/p90 9/73  trades in window p10/p50 60/254  500-window still fires at D 31/52
NY      measured: D-T0 s p50/p90/max 61/123/310  liquidation-print resets p50/p90 8/55  trades in window p10/p50 97/395  500-window still fires at D 125/229
ALL     measured: D-T0 s p50/p90/max 62/124/702  liquidation-print resets p50/p90 9/69  trades in window p10/p50 74/356  500-window still fires at D 237/413

=== 3. Reference grid (signal side): windows kept and tercile edges of signed imbalance I ===
Q0L60   ASIA    kept  15440  edges lo -0.5219 hi +0.5116  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.091
Q0L60   LONDON  kept   9675  edges lo -0.4765 hi +0.5474  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.083
Q0L60   NY      kept  21629  edges lo -0.4218 hi +0.4109  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.063
Q0L60   drops: firing_within_30min=976  flag_in_window=553  thin=4320
Q0L30   ASIA    kept  13028  edges lo -0.6212 hi +0.6676  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.146
Q0L30   LONDON  kept   8183  edges lo -0.6090 hi +0.7100  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.146
Q0L30   NY      kept  19170  edges lo -0.5101 hi +0.5431  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.105
Q0L30   drops: firing_within_30min=1022  flag_in_window=271  thin=10919
Q0L120  ASIA    kept  16590  edges lo -0.3910 hi +0.3683  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.039
Q0L120  LONDON  kept  10343  edges lo -0.3651 hi +0.3669  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.033
Q0L120  NY      kept  22689  edges lo -0.3155 hi +0.3120  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.026
Q0L120  drops: firing_within_30min=919  flag_in_window=930  thin=1122
Q30L60  ASIA    kept  15440  edges lo -0.5219 hi +0.5116  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.091
Q30L60  LONDON  kept   9675  edges lo -0.4765 hi +0.5474  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.083
Q30L60  NY      kept  21629  edges lo -0.4218 hi +0.4109  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.063
Q30L60  drops: firing_within_30min=976  flag_in_window=553  thin=4320
TFI30   ASIA    kept  17251  edges lo -0.6578 hi +0.6910  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.154
TFI30   LONDON  kept  10702  edges lo -0.6613 hi +0.7275  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.160
TFI30   NY      kept  23497  edges lo -0.6291 hi +0.6542  shares <lo 0.333  mid 0.333  >hi 0.333  |I|=1 share 0.145
TFI30   drops: firing_within_30min=1053  flag_in_window=76  thin=14
TFI30 reference share beyond the engine threshold: > +0.15 0.470   < -0.15 0.459

=== 4. Flip state, primary Q0L60, population (measured, outcome window before the fence) ===
session side    period     FLIP  BALANCED  WITH  | NO-FLIP
ASIA    LONG    all          20        75    14  |      89
ASIA    SHORT   all           2        20     1  |      21
ASIA    both    2025H1        7        35     6  |      41
ASIA    both    2025H2       14        26     7  |      33
ASIA    both    2026H1*       1        34     2  |      36
ASIA    both    all          22        95    15  |     110
LONDON  LONG    all           3        25     5  |      30
LONDON  SHORT   all           6         7     6  |      13
LONDON  both    2025H1        4        11     1  |      12
LONDON  both    2025H2        2        11     3  |      14
LONDON  both    2026H1*       3        10     7  |      17
LONDON  both    all           9        32    11  |      43
NY      LONG    all          42       105    17  |     122
NY      SHORT   all          14        44     7  |      51
NY      both    2025H1       14        57     7  |      64
NY      both    2025H2       20        46     6  |      52
NY      both    2026H1*      22        46    11  |      57
NY      both    all          56       149    24  |     173
ALL     LONG    2025H1       19        79    11  |      90
ALL     LONG    2025H2       30        59    12  |      71
ALL     LONG    2026H1*      16        67    13  |      80
ALL     LONG    all          65       205    36  |     241
ALL     SHORT   2025H1        6        24     3  |      27
ALL     SHORT   2025H2        6        24     4  |      28
ALL     SHORT   2026H1*      10        23     7  |      30
ALL     SHORT   all          22        71    14  |      85
ALL     both    2025H1       25       103    14  |     117
ALL     both    2025H2       36        83    16  |      99
ALL     both    2026H1*      26        90    20  |     110
ALL     both    all          87       276    50  |     326
half H1 (first floor(D/2) of 212 event days): 2025-01-07..2025-10-10  FLIP 41  NO-FLIP 154
half H2 (first floor(D/2) of 212 event days): 2025-10-11..2026-06-30  FLIP 46  NO-FLIP 172
weekday FLIP/NO-FLIP 79/273   weekend FLIP/NO-FLIP 8/53
population events whose episode also fired on the other side (kept; counted only): 21

=== 5. Variants and threshold sensitivity (descriptive; signal side) ===
Q0L60   measured 413 (cap 0, thin 3, fence 0)  outcome-fence 0  FLIP 87 BALANCED 276 WITH 50
Q0L30   measured 400 (cap 0, thin 16, fence 0)  outcome-fence 0  FLIP 75 BALANCED 282 WITH 43
Q0L120  measured 415 (cap 0, thin 1, fence 0)  outcome-fence 0  FLIP 88 BALANCED 266 WITH 61
Q30L60  measured 412 (cap 0, thin 4, fence 0)  outcome-fence 0  FLIP 72 BALANCED 290 WITH 50
TFI30   measured 416 (cap 0, thin 0, fence 0)  outcome-fence 0  FLIP 113 BALANCED 171 WITH 132  | engine threshold +-0.15: FLIP 204 BALANCED 31 WITH 181
trailing 90-day terciles (min 1000 refs) vs full-span, primary population:
  full FLIP     -> trailing FLIP 68  BALANCED 10  WITH 0  too_few_trailing_ref 9
  full BALANCED -> trailing FLIP 5  BALANCED 246  WITH 3  too_few_trailing_ref 22
  full WITH     -> trailing FLIP 0  BALANCED 3  WITH 46  too_few_trailing_ref 1
primary vs Q30L60 (30 s buffer after the last print), primary population: FLIP->FLIP=37  FLIP->BALA=44  FLIP->WITH=5  FLIP->none=1  BALA->FLIP=31  BALA->BALA=226  BALA->WITH=18  BALA->none=1  WITH->FLIP=4  WITH->BALA=19  WITH->WITH=26  WITH->none=1

=== 6. Flow-only control pool (TFS-H3): primary reference windows in an outer tercile, traded with the flow ===
ASIA    2025H1 L/S 1662/1606  2025H2 L/S 1734/1743  2026H1* L/S 1750/1797  all 10292
LONDON  2025H1 L/S 1075/975  2025H2 L/S 1077/1152  2026H1* L/S 1073/1097  all 6449
NY      2025H1 L/S 2312/2225  2025H2 L/S 2517/2456  2026H1* L/S 2379/2528  all 14417
control rows dropped for the outcome fence: 1

=== 7. Power (outcome-blind; sigma is a CARRIED proxy, not measured for this population) ===
population: FLIP 87  NO-FLIP 326  (covered session x side strata: FLIP 87  NO-FLIP 326)  control 31158
Holm over 3 tests, familywise 0.05: first-step alpha 0.0167 (worst case), last-step alpha 0.05 (best case)
MDE in sigma units at the worst-case alpha: H1 0.390  H2 0.347  H3 0.347
test                         sigma (bps)          MDE80 worst/best   pow@5bps  pow@10bps  pow@20bps  pow@40bps   readable (n>=100)
TFS-H1 FLIP - NO-FLIP       conservative x2.45  84.0    32.8/28.4         0.03       0.08       0.34       0.94   NO
TFS-H2 FLIP vs 0            conservative x2.45  84.0    29.2/25.2         0.03       0.10       0.43       0.98   NO
TFS-H3 FLIP - control       conservative x2.45  84.0    29.2/25.3         0.03       0.10       0.43       0.98   NO
TFS-H1 FLIP - NO-FLIP       optimistic x2.45    36.0    14.1/12.2         0.11       0.46       0.99       1.00   NO
TFS-H2 FLIP vs 0            optimistic x2.45    36.0    12.5/10.8         0.14       0.58       1.00       1.00   NO
TFS-H3 FLIP - control       optimistic x2.45    36.0    12.5/10.8         0.14       0.58       1.00       1.00   NO
TFS-H1 FLIP - NO-FLIP       optimistic x1       14.7     5.7/5.0          0.66       1.00       1.00       1.00   NO
TFS-H2 FLIP vs 0            optimistic x1       14.7     5.1/4.4          0.78       1.00       1.00       1.00   NO
TFS-H3 FLIP - control       optimistic x1       14.7     5.1/4.4          0.78       1.00       1.00       1.00   NO
period 2025H1   FLIP 25  NO-FLIP 117  readable NO
period 2025H2   FLIP 36  NO-FLIP 99  readable NO
period 2026H1*  FLIP 26  NO-FLIP 110  readable NO
```

### 6.3 What the count says

| Reading | Value | Consequence |
|---|---|---|
| Events | 416 in 548 days (0.76/day); ~140 per half-year, stable | Same events as the sister's whole-store table for these months (its 2026H1 row adds the July days here) |
| Flow coverage | 413 measured; 3 thin; **0 cap, 0 fence drops** | The anchor rule is not losing events |
| D − T0 | p50 62 s, p90 124 s, max 702 s | Most cascades go quiet within a minute of onset; the median event resets the anchor 9 times |
| Window activity | Median 356 non-liquidation trades in the 60 s window (ASIA 305, NY 395) | 30 trades (`TFI30`) is about 5 s of post-cascade tape (`TFS-3`) |
| Flip state | **FLIP 87 · BALANCED 276 · WITH 50** | FLIP is 21 % against ~33 % at random; BALANCED is 67 % |
| ⚠ Signal-side observation | Reference windows hold fewer trades; their I is wider (\|I\| = 1 in 6–9 % of them). A 356-trade window averages toward 0 | Fixed terciles make FLIP harder after a cascade. Recorded, **not acted on** (no redesign for power) |
| Side | 326 `LONG`-side vs 85 `SHORT`-side population events | Re-weighting by side matters |
| Session | NY 229 · ASIA 132 · LONDON 52 events; FLIP NY 56 · ASIA 22 · LONDON 9 | No session is readable alone |
| Variants | `Q0L120` FLIP 88 · `Q0L30` 75 · `Q30L60` 72 · **`TFI30` terciles 113** · `TFI30` engine ±0.15: 204 | Only `TFI30` crosses 100; see `TFS-12` option (b) |
| Q30 buffer | 44 of 87 FLIP become BALANCED with a 30 s buffer | The first 30 s after the last print carries much of the FLIP state. Session 2 prints the Q30 split |
| Trailing terciles | 68 of 78 classifiable FLIP stay FLIP; 9 lack trailing history | The full-span edge is not driving FLIP |
| Control | 31,158 rows | Not the binding constraint |

**Power verdict.**

- **NOT READABLE on every test** by the census rule: FLIP 87 < 100. NO-FLIP (326) and CONTROL are readable.
- **MDE at 80 % power, Holm first-step α 0.0167:** `TFS-H1` 32.8 bps (conservative) · 14.1 bps (optimistic × 2.45) · 5.7 bps (optimistic × 1). In σ units: 0.39 (`TFS-H1`), 0.35 (`TFS-H2`, `TFS-H3`).
- **σ is carried, not measured.** Conservative 34.3 and optimistic 14.7 bps come from [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §3 (directional verdict rows). The ×2.45 scale is the sister's event/median ATR ratio from the logged era ([`a4-liq-ofi-logged-era-study-spec.md`](a4-liq-ofi-logged-era-study-spec.md) §6.2). Neither is a measurement for these fade trades.
- **The span cannot grow.** Unlike the sister study, there is no accrual date. Only a design change or more history changes the count. **No redesign was made to chase power.** The options are `TFS-12`.

---

## 7. STOP conditions for session 2

A STOP is reported before any outcome is read. **A STOP is not an invitation to adapt.**

1. The trader has not ruled `TFS-12`.
2. `H-3` fails.
3. FLIP < 100 or NO-FLIP < 100 in covered strata, **unless** the `TFS-12` ruling says to run anyway.
4. Any outcome-side drop (no trade in the window, fewer than 50 completed candles for ATR, ATR = 0) exceeds 10 % of an arm.
5. Any trade at or after 2026-07-03 00:00 UTC would be read. The fenced reader must refuse it.
6. `scoring.trade_costs` is not maker/maker 3 bps, or any ATR multiplier in `docs/liq-tradeflow-flip-study-spec.md` §4.8 differs in tracked `settings.json`.
7. Any rule needs a judgement call. Record the deviation **before** looking at the affected numbers.

---

## 8. Run pins

| Pin | Value |
|---|---|
| Data | `trades_2025-01.csv` … `trades_2026-07.csv`, 5,470,786,582 bytes; 63,067,819 trades read before the fence |
| Fence | 2026-07-03 00:00:00.000 UTC, exclusive, for events, flow windows, reference windows and outcome windows |
| Tool | `tools/ops/liq_tradeflow_counts.py` at `848be8a`; sister event code `tools/ops/a4_liq_ofi_counts.py` at `53c95e4` |
| Seed / resamples | 20261005 / 10,000 |
| Settings | Tracked `settings.json` version 69: fee 3 bps maker/maker; ATR period 7; target ×1.75 NY, ×2.0 LONDON, ×1.25 ASIA; stop ×1.6; `indicators.TFI` window 30, threshold 0.15 |
| `LLS-1` values | ASIA 69,535 · LONDON 83,250 · NY 49,724 USD. ⚠ They were derived over 2025-01 → 2026-09, which crosses the fence. They are a ruled liquidation-size percentile (signal side), not an outcome. No outcome information enters through them |

---

## 9. Decisions

> ✅ **RULED 2026-10-05 (trader), on the orchestrator's reads:**
> - **`TFS-12` = (c), scoped to 2023-01-01 → 2024-12-31.** Not back to 2020: 2020–2022 is a different market regime (retail-leverage cycle, pre-FTX-collapse venue mix, lower price levels that change what a USD "large" liquidation means). History-host flags were verified present in 2023 and 2024 samples. The backfill goes to a SEPARATE store (`C:\DeribitData\history-2023-2024`, S3 prefix `history-backfill/store-2023-2024`). Then: re-derive the `LLS-1` per-session thresholds per period, re-register this spec on the extended span, and only then session 2.
> - **`TFS-8` = (d):** engine replay of the placed levels (the replay loader reads history-store files, fixture `A94f`); session 2 checks the full replay can run and falls back to (c), saying so, if it cannot.
> - **`TFS-6` = (b):** 5-min reference grid.

Each row went through harness 6, the decision-bias tripwire (`tools/checks/measure/decision-bias/run-decision-bias.ps1`, 5 samples, 60 calls). My labels were written first (shadow-mode rule). Files: `docs/harness-runs/decision-bias-20261005T1150Z-{population,baseline,jev}.json`. **Jev flagged `TFS-6` and `TFS-8` as `gives_up_for_economy` (5/5, p 1.00 and 0.98). My own labels flag the same two.** One item unstable: `TFS-7` (4/5).

| ID | Question | Options | My read | My label · Jev (agreement, mean p) | Status |
|---|---|---|---|---|---|
| **`TFS-1`** | Where the flow window starts | (a) after the last liquidation print, reset by any later print · (b) T0 + fixed offset · (c) after the 30-min episode end · (d) the episode's last firing trade (hindsight) | **(a).** (d) is **mechanically wrong**: it conditions on future absence of liquidations, as the sister's `A4L-1` (b). (c) measures flow 30+ min later — a different, stale state, not A4. (b) overlaps long cascades, so "no flip" by construction | `richer_option_wrong` · `richer_option_wrong` (5/5, 0.96) | Auto-proceeded |
| **`TFS-2`** | Buffer after the last print | (a) Q = 0 primary, Q = 30 s printed · (b) Q = 30 s primary · (c) Q = 0 only | **(a).** Closest to "immediately afterward"; still records the buffered variant. ⚠ The count shows the choice matters (44 of 87 FLIP change); the Q30 split is printed in session 2 | `no_richer_option` · `no_richer_option` (5/5, 0.76) | Auto-proceeded |
| **`TFS-3`** | Window type and length | (a) 60 s primary; 30 s, 120 s, `TFI30` printed · (b) `TFI30` primary · (c) 120 s primary | **(a).** Measured: 30 trades ≈ 5 s after a cascade, often one sweep. A time window has one meaning across sessions | `no_richer_option` · `richer_option_wrong` (5/5, 0.86) | Auto-proceeded |
| **`TFS-4`** | Drops | (a) 30-min cap + thin (< 10 trades), dropped and counted · (b) no cap · (c) 10-min cap | **(a).** The cap equals the ruled de-clustering gap. 0 cap drops, 3 thin | `no_richer_option` · `richer_option_wrong` (5/5, 0.91) | Auto-proceeded |
| **`TFS-5`** | Threshold source (signal side) | (a) per-session reference terciles · (b) engine 0.15 · (c) sign only · (d) top quartile of \|I\| | **(a).** "Unusual for the session", ~⅓ at random. (b) is tuned for 30 trades. (c) calls noise a flip. (d) holds less. Continuous I recorded anyway | `no_richer_option` · `richer_option_wrong` (5/5, 0.84) | Auto-proceeded |
| ⚠ **`TFS-6`** | Reference-grid step | (a) 15 min · (b) 5 min · (c) random instants | **(a).** FLIP (87) binds the MDE, not control (31,158); edges come from 10k–22k windows per session. ⚠ **(b) records more.** My reason is that (a) is adequate and (b) triples session-2 walk work — **a cost argument; step 2 of the three-step test reserves it** | `gives_up_for_economy` · `gives_up_for_economy` (5/5, 1.00) | ⛔ **QUEUED** (both flags). Low stakes |
| **`TFS-7`** | Threshold span | (a) full span decides, trailing 90 d printed and flagged · (b) trailing only · (c) full span only | **(a).** Records both, keeps every event. 68 of 78 FLIP agree | `no_richer_option` · `richer_option_wrong` (4/5, 0.50, **unstable**) | Auto-proceeded |
| ⚠ **`TFS-8`** | Outcome measure | (a) ATR-fallback levels from the tape, tape walk, house net EV · (b) fixed-horizon mark · (c) (a) decides, (b) descriptive · (d) engine replay for structural-first placed levels, then (c) | **(c).** ⚠ **(d) is more faithful** to what the engine would place (swing/HVN/POC levels). I did not verify that the replay runner (`tools/BacktestRunner/`, `ReplayLoop.vb`) can run on the history store. **My reason against (d) is build cost — reserved by step 2** | `gives_up_for_economy` · `gives_up_for_economy` (5/5, 0.98) | ⛔ **QUEUED** (both flags) |
| **`TFS-9`** | Arms | (a) FLIP vs NO-FLIP · (b) + FLIP vs 0 · (c) + FLIP vs flow-only control | **(c)**, the richest | `no_richer_option` · `no_richer_option` (5/5, 0.94) | Auto-proceeded |
| **`TFS-10`** | Replication rule | (a) census label, event-day halves; half-years descriptive · (b) all three half-years must agree | **(a).** (b) is **uninterpretable**: 25–36 FLIP per half-year, NOT READABLE by construction | `richer_option_wrong` · `richer_option_wrong` (5/5, 1.00) | Auto-proceeded |
| **`TFS-11`** | Weekends | (a) all days, split printed · (b) weekdays only | **(a)**, as the sister's `A4L-7` | `no_richer_option` · `richer_option_wrong` (5/5, 0.81) | Auto-proceeded |
| ⛔ **`TFS-12`** | Session 2, given FLIP 87 < 100 on a closed span | (a) run now as registered; all NOT READABLE; CIs bound the effect · (b) re-register a signal-side variant as primary before any outcome (e.g. `TFI30` terciles, FLIP 113) · (c) extend the span backward with a history-host backfill of earlier years, re-register, then run · (d) close the study | **(c).** It keeps the measure and adds data — the only route to the floor without a new design. (a) **unseals the whole span**: any later redesign on it stops being outcome-blind (the `A4L-10` argument). (b) picks the primary after seeing counts — a degree of freedom, even if signal-side only; and `TFI30` halves would hold ~56 FLIP each. (d) discards a nearly readable study. ⚠ (c) carries two unverified premises: the history host holds earlier years with liquidation flags (carried from seat memory, not checked), and the USD `LLS-1` thresholds may not transfer to earlier price levels (a re-derivation question for the re-registration) | `no_richer_option` · `richer_option_wrong` (5/5, 0.91) | ⛔ **QUEUED** (escalation; the brief's trigger) |

No choice here touches `settings.json`, scoring, a rendered value or a CSV schema.

---

## 10. What the result feeds

| Result (when readable) | Reading |
|---|---|
| `TFS-H1` and `TFS-H3` CONFIRMED > 0 | The trade-flow flip adds over the cascade alone and over the flow alone. Argues for an A4-style fade reward at un-park. Still needs a real-time liquidation source (`L-1`) |
| `TFS-H1` CONFIRMED > 0, `TFS-H3` not | The flip matters given a cascade; the flow vote (TFI) may already carry it |
| `TFS-H2` CONFIRMED < 0 | The fade loses even with the flip. Supports the penalty-only design ([`trader-profile.md`](trader-profile.md) §3, Liquidations row) |
| NOT READABLE or anything else | Undecided on outcome evidence; record the CIs |
| Either way | Informs whether the sealed OFI study (`A4L-10` (a)) is worth its wait: a trade-flow null with tight CIs weakens the case for A4 |

---

## 11. Pre-registration record — what the author saw

- **No outcome was computed or read.** The counts tool parses only `Timestamp`, `Amount`, `Direction`, `Liquidation`.
- ⚠ **Two price values were seen by accident:** a `head -3` of `trades_2026-07.csv` printed the first two trades of 2026-07-01 00:00 UTC (prices 58,556.50 and 58,561.50). Inside the study span, a single instant, not after any event, no return computed.
- **No data at or after the fence was read by this seat's tools.** The parity check (`E-2`) used 2025-01 … 2026-06 only. I did **not** run the sister's `H-1` (it reads the sealed span).
- **Read for the design:** [`a4-liq-ofi-logged-era-study-spec.md`](a4-liq-ofi-logged-era-study-spec.md) (all), `tools/ops/a4_liq_ofi_counts.py` (all), [`large-liq-size-rederivation-2026-10-02.md`](large-liq-size-rederivation-2026-10-02.md) (all), [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §3–§5, [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a, [`trader-profile.md`](trader-profile.md) (grep: liquidation, TFI, false positives), `CalcTFI` and `CalcATR` in `Core/`, tracked `settings.json` (`TFI`, `ATR`, multipliers, `trade_costs`, the v51 `change_log` entry), `tools/ops/medium_tier_diagnosis.py` (label helpers). None contains a trade-flow outcome after liquidations.
- ⚠ **Concurrent commits during this session.** Commit `53c95e4` (another seat: the sister's `--data-cut` arg) **swept in my uncommitted refactor** of `tools/ops/a4_liq_ofi_counts.py` (`iter_store_rows`, `scan_rows`, the `hook`). Its message does not mention the refactor. The refactor is mine; parity is `E-2`; the sister's own `--selftest` still passes at `53c95e4`. Commit `4d5c6ae` (another seat, a docs proposal) also landed. I rewrote no history.

---

## 12. Harness log

| Harness | Use | Result |
|---|---|---|
| 5 · doc re-ranker | "Where was it decided that the trade-flow flip study uses the trade store only, 2025-01-01 to 2026-07-02?" | **Hit.** Top 1 (p 0.84) = `a4-liq-ofi-logged-era-study-spec.md` §9, the RULED box. The brief had named it |
| 5 · doc re-ranker | "Where is the engine's ATR fallback target and stop multiplier per session decided?" | **Hit.** Top 1 (p 0.91) = [`placed-geometry-spec-back.md`](placed-geometry-spec-back.md) §1 (v51). It agrees with the `settings.json` v51 `change_log` entry I had read |
| 6 · decision-bias tripwire | The 12 decisions in `docs/liq-tradeflow-flip-study-spec.md` §9 | 60 calls, 1 unstable (`TFS-7`). **Jev flags `TFS-6` and `TFS-8`; so do I.** Other disagreements are `no_richer_option` vs `richer_option_wrong` — both say nothing is given up for economy |

---

## 13. What I did not verify

| Claim | Status |
|---|---|
| No trade before the fence appears after the first at-fence line | The pre-fence span has 0 out-of-order trades, but lines after the stop are, by design, never read |
| The store's 2026-07 file is final and unchanged since the counts run | Pinned by bytes and trade count, not by md5 (md5 of 5.5 GB was skipped) |
| Session-2 ATR from tape candles equals the engine's ATR | Not checked. The engine builds candles from the venue's chart data; tape-built candles should match but were not compared |
| σ for this population | Not measured. Carried proxies, `docs/liq-tradeflow-flip-study-spec.md` §6.3 |
| The history host holds trades with liquidation flags before 2025 (`TFS-12` (c)) | Carried from seat memory; not checked |
| `LLS-1` thresholds applied trade by trade | Derived from minute-close samples; same caveat as the sister study |
| The engine replay can run on the history store (`TFS-8` (d)) | Not checked |
| `MT` handling | 66 `MT` trades; the "both sides, full amount" reading of `D-5`, untested against the venue (as the sister) |
