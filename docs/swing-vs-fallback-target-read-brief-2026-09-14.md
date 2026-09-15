# Brief — do swing targets beat ATR-fallback targets after fees?

**Written:** 2026-09-14 (UTC) by the orchestrator seat of 2026-09-14b. **Revised the same day** (trader-directed): weekends excluded throughout, and outcomes also carried to their conclusion instead of stopping at a timeout. **For:** a new, single-task analysis seat. **Asked by:** the trader, as part of the strategy review. Vocabulary: `docs/DeribitIndicatorProject.md` §5a (success rate · gross/net breakeven rate · gross/net edge · net EV per trade). **Use those words exactly.**

---

## 0. Model, effort, escalation — read first

| Item | Detail |
|---|---|
| **Model / effort** | **Opus, high.** A measurement with a strong confound: a swing target exists only in some market states, so a raw label split is not causal. The carried-to-conclusion walk and Part B's counterfactual geometry are both judgment-heavy |
| **Session split** | **Session 1:** the join, the forward walk and Part A (§2–§3). **Session 2:** Part B (§4), only if Part A is clean. Sequenced by dependency |
| **Where it will slip** | (1) **The join.** Only 59.5 % of weekday directional outcome rows join, even with a ±3 s tolerance (§2). The cause is unknown; do not analyse a 60 % subset. (2) Pooling LONDON and ASIA: their fallback multipliers differ (2.0 vs 1.25). (3) **The forward walk:** an ambiguous bar, holding across a weekend, or ignoring funding on a long hold (§3.2). (4) Averaging per-signal breakevens instead of distance-weighting (§5a rule 1). (5) Letting a weekend row in anywhere, including through a Friday trade carried into Saturday. (6) Reading the label split as causal |
| **Escalate / stop** | **Stop if the join rate stays below 95 %** after investigating. Stop and ask if a result would justify a geometry or `settings.json` change: present a D-table, do not apply it |
| **Scope** | Trader-directed scoped seat. Skip the full `CLAUDE.md` session-start reads. Read `DeribitIndicatorProject.md` §5a and the inputs below. Run `date -u` first |

## 1. The question

On about a third of NY signals the engine already places the target at a 5-minute swing (the trader's own method). On about half it falls back to a fixed ATR multiple. **Does the swing-target population earn a better net EV per trade than the fallback population? And would swing targets do better if applied more widely?**

**Orchestrator's preliminary read (2026-09-14, NOT a result).** Inputs: `aws_fetch/20260913-153704/` (log `.bak` + live log, eval cache). Weekday signals only (UTC). ±3 s join, but only **59.5 %** of weekday outcome rows joined. Timeouts scored flat (not carried). Maker/maker 3 bps. The live tracker's eval window was not identified. The session is taken from resolution and UTC hour (ASIA = 3-min before 07:00).

| Session | Target set by | n | Success rate (CI) | Stop hit | Timeout | Gross breakeven | Net breakeven | **Net edge** | **Net EV / trade** |
|---|---|---|---|---|---|---|---|---|---|
| NY | ATR fallback | 2,284 | 37.5 % (36–40) | 49.8 % | 12.7 % | 47.7 % | 58.0 % | −20.5 pp | −3.9 bps |
| NY | Swing | 1,335 | 32.5 % (30–35) | 47.3 % | 20.1 % | 41.7 % | 51.9 % | −19.4 pp | −3.6 bps |
| NY | HVN | 681 | 40.1 % (36–44) | 42.3 % | 17.6 % | 50.6 % | 60.3 % | −20.2 pp | −3.2 bps |
| LONDON | ATR fallback | 787 | 45.6 % (42–49) | 45.9 % | 8.5 % | 45.1 % | 53.3 % | −7.7 pp | −0.6 bps |
| LONDON | Swing | 612 | 43.6 % (40–48) | 45.9 % | 10.5 % | 43.5 % | 52.4 % | −8.8 pp | −1.9 bps |
| ASIA | ATR fallback | 593 | 51.8 % (48–56) | 43.0 % | 5.2 % | 56.1 % | 66.1 % | −14.3 pp | −3.1 bps |
| ASIA | Swing | 696 | 40.1 % (36–44) | 48.1 % | 11.8 % | 43.8 % | 52.7 % | −12.6 pp | −2.9 bps |

STRONG cells: NY fallback 51.2 % (n = 205), net edge −5.6 pp, net EV −0.7 bps · ASIA fallback 60.6 % (n = 33), +1.1 bps · NY swing 16.7 % (n = 48), −9.1 bps.

**What it suggests, to be tested, not believed:**

- **LONDON is the closest session to breakeven.** Fallback there is about 0.6 bps a trade short.
- **Swing is not better than fallback in any session.** It is roughly even in NY and LONDON and worse in ASIA.
- **Swing targets time out about twice as often,** which is exactly what the carried-to-conclusion walk (§3.2) will resolve.

## 2. Inputs and the join

| Input | Use |
|---|---|
| `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv` (47,682 rows) plus the newest `aws_fetch/<stamp>/analysis_log.csv` for rows after 2026-09-09 | Rows: `Timestamp`, `Verdict`, `Price`, `ATR`, `ExecResolution`, `TargetCapReason` (`swing` · `hvn` · `none` = ATR fallback), `PlacedTarget*`, `PlacedStop*`. **Resolve columns by header name, never by index** |
| `analysis_eval_cache.csv` from the same copy-back | Window-bound outcomes: `EvalOutcome` (`SUCCESS` · `ADVERSE_HIT` · `WINDOW_EXPIRED` · `NO_DATA`), `FavBar`, `AdvBar` |
| 1-minute OHLC candles: `analysis/DeribitOhlcFetcher.vb` (as used by `ForwardWindowJoiner.PopulateForwardBars`), or the `tools/BacktestRunner fetch` candle store | The carried-to-conclusion walk (§3.2). Deribit candles have no retention cap. Confirm full coverage before walking |
| `tools/BacktestRunner report` (the band ladder, `analysis/BandLadder.vb`) | The per-window walk at fixed windows, if the eval cache's window is not the one wanted |
| `tools/WhatIfRunner` | Part B counterfactual geometry, net EV after fees, split-half holdout |

**The join — investigate FIRST.**

- The eval cache stamps about 1 s after the log row (checked: `16:25:05.55Z` against `16:25:04`).
- A ±3 s tolerance, keyed on the same `Verdict`, still joined only **59.5 %** of weekday directional outcome rows (7,223 joined, 4,921 not). So the offset is not the whole cause.
- **Find the cause before any analysis.** Candidates: log rows with empty placed levels; eval rows written by a process whose log rows are not in these files; verdict-string differences.
- The box cache is schema v6, so it carries no `SignalId`.
- ⭐ **The carried-to-conclusion walk does not need the eval cache at all.** It walks candles from each log row's own `Price`, `PlacedTarget*` and `PlacedStop*`, so it covers every row. If the join stays broken, run the walk for both modes (window-bound and carried) from candles, and drop the eval cache.

**Confirm the label meaning** in code (`SignalEmitter.ComputeSideLevels`, `AnalysisLogger`) before relying on it. The data shows NY `none` rows at exactly 1.75×ATR, consistent with the ATR fallback, but that is not proof.

## 3. Part A — the observational split (session 1)

### 3.1 Population

- **Trading-week signals only (trader-ruled 2026-09-14).** The trading week runs from **the start of Monday's ASIA session to the end of Friday's NY session**. A signal outside that span is excluded. The trader does not trade weekends.
  - Read the session hours from `session_volume.sessions` in `settings.json`; never hardcode them. At v68 ASIA is hours 0–7 and NY is hours 13–23 UTC, inclusive, so the span is Monday 00:00:00 to Friday 23:59:59 UTC.
  - At v68 this equals the calendar-weekday filter `ForwardWindowJoiner.IsWeekdayRow`. If the session hours ever move, the session rule wins.
- Directional verdicts with placed levels.
- Split every result by **session (NY · LONDON · ASIA separately) × `TargetCapReason` × tier**, and by side where n ≥ 30.

### 3.2 Outcomes — two modes, both reported

| Mode | Rule |
|---|---|
| **Window-bound** | Target or stop inside a fixed window. NY at 5, 10 and 15 min; LONDON and ASIA at 15, 30 and 45 min. A timeout is marked to the window-close price, not scored flat. This mode matches the trader's 2–15 min hold |
| **Carried to conclusion** | Walk 1-minute candles forward from the signal until the **first touch of the placed target or the placed stop**. No time window |

**Rules for the carried walk:**

1. **Same-bar ambiguity** (target and stop both inside one 1-minute bar) counts as a stop hit, matching `FailureRateMatrix`.
2. **Hard cap: the earlier of 24 h after the signal, or the end of Friday's NY session** (trader-ruled 2026-09-14; from `session_volume.sessions`, 23:59:59 UTC at v68). A trade is never carried across a weekend; the next week starts fresh at Monday's ASIA session open. A trade still open at the cap is marked to the cap-bar close and counted as **UNRESOLVED**. Report the UNRESOLVED share per cell.
3. **A session-end variant as well:** cap at the end of the signal's session (the trader is "always flat at end of session", `trader-profile.md` §2). Report it beside the 24 h cap.
4. **Funding:** a hold that crosses a Deribit funding settlement pays or receives funding. Report its effect in bps per cell; include it in net EV if it moves any cell by more than 0.5 bps.
5. **Time to resolution:** report p50 and p90 minutes per cell. This shows whether a signal's resolution fits a 2–15 min hold at all.

**Why the carried mode matters:** with unlimited time on a market with no drift, the success rate converges to the gross breakeven rate. So **carried-mode gross edge is the cleanest test of whether the direction call is right**, free of the window. Net edge still counts fees.

### 3.3 Per cell, report (per `DeribitIndicatorProject.md` §5a)

- n, success rate with 95 % CI, stop-hit %, timeout % (window-bound) or UNRESOLVED % (carried).
- Gross and net breakeven rate, distance-weighted from each row's own placed distances in bps.
- Gross edge and net edge in pp.
- **Net EV per trade in bps**, in both modes.
- Median target and stop in ATR multiples.

### 3.4 Confound controls (required)

1. **Distance-matched comparison.** Bucket rows by target distance in ATR multiples (e.g. 1.5–2.0, 2.0–2.5, 2.5–3.0) and compare swing against fallback **inside each bucket**. This separates "structure helps" from "wider targets lose more often".
2. **Regime split:** before and after the 2026-08-20 ATR step (found by `d3-asia-burst-watch-read-2026-09-14.md`). Report the v66 OBV scoring edge (2026-08-10) as a boundary.
3. **Fees:** `scoring.trade_costs` (maker/maker, 3 bps). Also report one taker-stop case (maker entry, taker stop, 5 bps on a loss).

## 4. Part B — the counterfactual (session 2, only if Part A is clean)

Use `tools/WhatIfRunner`, in both outcome modes where it supports them:

- **B-1:** On rows where a swing target existed, what net EV would the ATR fallback have earned, and the reverse? Same rows, two geometries.
- **B-2:** A target-multiple grid from 1.75× to 4×ATR for the fallback, per session, stop unchanged. This measures whether the success rate falls more slowly than the gross breakeven rate as targets widen; only that shows a directional edge.
- Split-half holdout on every ranked cell. Flag any result that does not survive it.
- Weekday signals only, as in §3.1.

Prior work to read first: the geometry-modes study (the `roadmap.md` W1 row "Geometry-modes study re-read (v56 instrument)"; nearest-target topped the table but was DIVERGENT).

## 5. Deliverable

1. `docs/swing-vs-fallback-target-read-2026-09-XX.md`: verdict first; the join finding; Part A tables in both modes; the confound controls; Part B if run; what was not verified. Use §5a vocabulary only.
2. The instrument, committed (`tools/ops/` or `analysis/`, host-agnostic) and re-runnable, with its real output pasted.
3. **A D-table only if a geometry change looks justified.** Any `settings.json` or scoring change is reserved to the trader.
4. Commit locally, `[no-engine-change]`. Do not push.

## 6. Report back

At most 10 lines: the join cause and final join rate; the verdict per session in both modes; the distance-matched result; the best cell's net EV per trade with CI and holdout status; commit hash; anything needing a ruling.
