# Brief — do swing targets beat ATR-fallback targets after fees?

**Written:** 2026-09-14 (UTC) by the orchestrator seat of 2026-09-14b. **For:** a new, single-task analysis seat. **Asked by:** the trader, as part of the strategy review. Vocabulary: `docs/DeribitIndicatorProject.md` §5a (success rate · gross/net breakeven rate · gross/net edge · net EV per trade). **Use those words exactly.**

---

## 0. Model, effort, escalation — read first

| Item | Detail |
|---|---|
| **Model / effort** | **Opus, high.** It is a measurement with a strong confound: a swing target exists only in certain market states, so a raw label split is not causal. Part B needs the what-if runner's counterfactual geometry, which is judgment-heavy |
| **Session split** | **Part A** (the observational split, §3) in session 1. **Part B** (the counterfactual, §4) in session 2, only if Part A is clean. Sequenced by dependency |
| **Where it will slip** | (1) Joining outcomes to rows on the exact second. The eval cache stamps about 1 s after the log row, so an exact join drops ~40 % of rows (§2). (2) Pooling LONDON and ASIA under 3-min bars: their fallback multipliers differ (2.0 vs 1.25). (3) Treating a timeout as flat. Mark it to the window close. (4) Averaging per-signal breakevens instead of distance-weighting (§5a rule 1). (5) Including weekends; the weekday-scope ruling applies. (6) Reading the label split as causal |
| **Escalate / stop** | Stop and ask if any result would justify a geometry or `settings.json` change. Present a D-table; do not apply it. Also stop if the join rate after the fix in §2 stays below 95 % |
| **Scope** | Trader-directed scoped seat. Skip the full `CLAUDE.md` session-start reads. Read §5a of `DeribitIndicatorProject.md` and the inputs below. Run `date -u` first |

## 1. The question

On about a third of NY signals the engine already places the target at a 5-minute swing (the trader's own method). On about half it falls back to a fixed ATR multiple. **Does the swing-target population earn a better net EV per trade than the fallback population, and would swing targets do better if applied more widely?**

**Orchestrator's preliminary read (2026-09-14, NOT a result — the method has the six slips in §0).** Inputs: `aws_fetch/20260913-153704/` (log `.bak` + live, eval cache), exact-second join (~60 % of directional rows), weekends included, timeouts scored flat, maker/maker 3 bps, the live tracker's eval window (not identified).

| Bars | Target set by | n | Success rate | Stop hit | Timeout | Net breakeven rate | Net edge | Net EV / trade |
|---|---|---|---|---|---|---|---|---|
| NY 1-min | ATR fallback | 2,532 | 38.6 % | 49.3 % | 12.1 % | 58.4 % | −19.8 pp | −3.6 bps |
| NY 1-min | Swing | 1,419 | 29.4 % | 50.0 % | 20.6 % | 52.6 % | −23.2 pp | −4.3 bps |
| NY 1-min | HVN | 754 | 38.1 % | 45.8 % | 16.2 % | 59.6 % | −21.5 pp | −4.0 bps |
| 3-min (ASIA + LONDON) | ATR fallback | 1,477 | 48.3 % | 45.0 % | 6.7 % | 58.2 % | −9.9 pp | −1.9 bps |
| 3-min (ASIA + LONDON) | Swing | 1,449 | 38.4 % | 48.4 % | 13.3 % | 52.4 % | −14.1 pp | −3.1 bps |

**What it suggests, to be tested, not believed:** swing targets are wider (a lower net breakeven rate), but their success rate falls further than the width justifies, and they time out more. The one positive cell was 3-min fallback STRONG (+0.6 bps, n = 142, success rate CI 43–60 %).

## 2. Inputs and the join

| Input | Use |
|---|---|
| `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv` (47,682 rows) plus the newest `aws_fetch/<stamp>/analysis_log.csv` for rows after 2026-09-09 | Rows: `Timestamp`, `Verdict`, `Price`, `ATR`, `ExecResolution`, `TargetCapReason` (`swing` · `hvn` · `none` = ATR fallback), `PlacedTarget*`, `PlacedStop*`. Resolve columns by header name, never by index |
| `analysis_eval_cache.csv` from the same copy-back | Outcomes: `EvalOutcome` (`SUCCESS` · `ADVERSE_HIT` · `WINDOW_EXPIRED` · `NO_DATA`), `FavBar`, `AdvBar`, `ExecResolution` |
| `tools/BacktestRunner report` (the band ladder, `analysis/BandLadder.vb`) | The per-window walk at 15/30/45 min, if the eval cache's window is not the one wanted |
| `tools/WhatIfRunner` | Part B counterfactual geometry and net EV after fees, with split-half holdout |

**The join.** The eval cache timestamp runs about 1 s after the log row (checked 2026-09-14: `16:25:05.55Z` vs `16:25:04`). Join by nearest eval row within +0 to +3 s with the same `Verdict` and `ExecResolution`, and report the join rate. The box cache is schema v6, with no `SignalId`.

**Confirm the label meaning** in code (`SignalEmitter.ComputeSideLevels` and `AnalysisLogger`) before relying on it. The data shows NY `none` rows at exactly 1.75×ATR, which is consistent with the ATR fallback, but that is not proof.

## 3. Part A — the observational split (session 1)

Population: weekday only (UTC), directional verdicts, placed geometry. Split each result by **session (NY · LONDON · ASIA separately) × `TargetCapReason` × tier**, and by side where n ≥ 30.

For every cell report, per `DeribitIndicatorProject.md` §5a:

- n, success rate with 95 % CI, stop-hit %, timeout %.
- Gross and net breakeven rate, distance-weighted from each row's own placed distances in bps.
- Gross edge and net edge in pp.
- **Net EV per trade in bps, with timeouts marked to the window-close price**, not scored flat.
- Median target and stop in ATR multiples.

**Windows:** NY at 5, 10 and 15 min; LONDON and ASIA at 15, 30 and 45 min. State which window each table uses. The trader holds 2–15 min.

**Confound controls (required, not optional):**

1. **Distance-matched comparison.** Bucket rows by target distance in ATR multiples (e.g. 1.5–2.0, 2.0–2.5, 2.5–3.0). Compare swing against fallback **inside each bucket**. This separates "structure helps" from "wider targets lose more often".
2. **Regime split.** Before and after the 2026-08-20 ATR step (the ASIA watch read `d3-asia-burst-watch-read-2026-09-14.md` found it). Also report the v66 OBV scoring edge (2026-08-10) as a boundary.
3. **Fees:** `scoring.trade_costs` (maker/maker, 3 bps). Also report one taker-stop case (maker entry, taker stop, 5 bps on a loss).

## 4. Part B — the counterfactual (session 2, only if Part A is clean)

Use `tools/WhatIfRunner` to answer what a raw split cannot:

- **B-1:** On rows where a swing target existed, what net EV would the ATR fallback have earned, and the reverse? Same rows, two geometries.
- **B-2:** A target-multiple grid from 1.75× to 4×ATR for the fallback, per session, stop unchanged. This measures whether success rate falls more slowly than the gross breakeven rate as targets widen. Only that shows a directional edge.
- Split-half holdout on every ranked cell. Flag any result that does not survive the holdout.

Prior work to read first: the geometry-modes study (the `roadmap.md` W1 row "Geometry-modes study re-read (v56 instrument)": nearest-target topped the table but was DIVERGENT).

## 5. Deliverable

1. `docs/swing-vs-fallback-target-read-2026-09-XX.md`: verdict first, then Part A tables, confound controls, Part B (if run), and what was not verified. Use §5a vocabulary only.
2. The instrument, committed (`tools/ops/` or `analysis/`, host-agnostic) and re-runnable, with its real output pasted.
3. **A D-table only if a geometry change looks justified.** Any `settings.json` or scoring change is reserved to the trader.
4. Commit locally, `[no-engine-change]`. Do not push.

## 6. Report back

At most 10 lines: the verdict per session, the distance-matched result, the best cell's net EV per trade with CI and holdout status, commit hash, and anything needing a ruling.
