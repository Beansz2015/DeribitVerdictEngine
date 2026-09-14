# Brief — ASIA aggressor-velocity burst: re-derivation read (owed by ruling T-5)

**Written by:** the orchestrator seat of 2026-09-14b. **For:** a new, single-task conversation. **Trigger:** the second watch read, `docs/d3-asia-burst-watch-read-2026-09-14.md`, MISSED on fire rate. Ruling T-5 in `docs/d3-asia-burst-watch-read-2026-08-10.md` §10 says a miss fires a re-derivation read, and the re-derivation decides whether the threshold moves.

---

## 0. Model, effort, escalation — read first

| Item | Detail |
|---|---|
| **Model / effort** | **Opus, high.** This is a derivation. The hard part is judging whether the fire rate is regime-conditioned, and what unit the tolerance should use. There is no template for that part |
| **Where it will slip** | (1) Reading r = −0.72 as a slope. With two clusters (08-11 → 08-19 and 08-20 onward), r can come from the step alone; test within each regime. (2) Pooling across the 08-20 step. (3) Treating fire rate as an outcome. It is a selectivity proxy only. (4) Writing a threshold change. That is reserved |
| **Escalate / stop** | Stop and ask the trader if the answer needs a `settings.json` change, a code change to the accumulator, or a new tolerance ruling. Present these as a D-table with your read. Do not apply them |
| **Scope** | Trader-directed scoped seat. Skip the full `CLAUDE.md` session-start reads. Read only §2. Run `date -u` first |
| **When** | Run on the copy-back taken **on or after 2026-09-16 UTC**; that fetch also serves the absorption episode-age read. If you run earlier, use `aws_fetch/20260913-153704/`. Always read from a dated `aws_fetch/<stamp>/` folder, never the root `analysis_log_aws.csv` |

## 1. What the second read found (verified by the orchestrator: script re-run 2026-09-14)

| Slice | Days | Fire rate | Same-side |
|---|---|---|---|
| (b) 2026-08-11 → 09-11 | 23 | **7.75 %** (band 8–14 %) | 89.75 % |
| (a) 2026-08-03 → 09-11 | 28 | 8.50 % | 89.68 % |
| 08-11 → 08-19 (median ATR ~27) | — | 15.97 % | — |
| 08-20 onward (median ATR ~74) | — | 4.85 % | — |

- Day-level sd of the fire rate rose from 2.71 pp (first read) to 5.37 pp.
- No deploy, restart or settings edge sits on the 08-20 step; it falls inside process `e3781e57…`.
- Instrument: `tools/ops/asia-burst-watch-read.ps1 -FetchFolder <folder>`. Extend it or add a sibling script; keep it re-runnable.

## 2. Inputs

- `docs/d3-asia-burst-watch-read-2026-09-14.md` — the read that triggered this.
- `docs/asia-burst-threshold-derivation-2026-08-01.md` §1 (ASIA candidate table), §2 (res-3 sessions share one distribution), §5.2 (the AUC 0.5179 caveat).
- `docs/d3-asia-burst-watch-read-2026-08-10.md` §10 — rulings T-1 to T-5. T-2's false-alarm argument assumed the old day-level sd.
- `Core/AggressorVelocityAccumulator.vb` — how the fast and norm horizons build the ratio. Read it; do not infer the mechanism.
- `docs/DeribitIndicatorProject.md` §12, row "v52 aggressor-velocity wire-in post-ship watch (S5.2)" — the NY band (8–12 %).

## 3. Questions, in order

| # | Question | Why it matters |
|---|---|---|
| **Q1** | **Market-wide or ASIA-only?** Compute the weekday daily fire rate for NY (res-1) and LONDON (res-3) on the same days, before and after 2026-08-20. | If NY and LONDON also dropped, this is a market regime, not an ASIA calibration fault. ⚠ If NY is outside its own 8–12 % band, report it: that is a second watch miss nobody has read |
| **Q2** | **Where does the drop live?** Compare `AggrVelBurstRatio` quantiles (p50, p90, p95, p99) per regime. Did the ratio distribution shift, or did the eligible population change? Tie the answer to the accumulator's code | Separates "fewer bursts" from "the ratio compresses when sustained flow lifts the norm horizon" |
| **Q3** | **Slope or step?** Report r(daily ATR, daily rate) within each regime separately | Decides whether an ATR-conditioned rule is justified or the data only shows two regimes |
| **Q4** | **Candidate thresholds.** Which threshold restores about 11 % in the post-08-20 regime, and what would it do in the pre-08-20 regime? | Feeds a D-table. **No change is applied** |
| **Q5** | **Tolerance (ruling T-2).** At sd 5.37 pp, what false-alarm rate does ±3 pp give at a 10-day read? Options to lay out: keep · widen · longer read · ATR-conditioned band | The trader asked whether T-2 still holds |
| **Q6** | Confirm the CSV `ATR` column's units (the second read did not) | The regime labels depend on it |

**Out of scope:** outcomes and AUC, not measured by either watch read. Also out: the cause of the 2026-08-15 16:11 → 08-17 16:23 CSV hole (list it only).

## 4. Deliverable

1. `docs/asia-burst-rederivation-read-2026-09-XX.md` — verdict first; Q1–Q6 answered; a D-table (threshold · tolerance · unit) with your read on each row; what you did not verify.
2. The script, committed and run, with its real output pasted.
3. Same commit: update `docs/trader-tick-queue.md` §4 (the ASIA watch paragraph) and add a pointer in the `docs/DeribitIndicatorProject.md` §12 row named above.
4. Commit locally, `[no-engine-change]`. Do not push.

## 5. Rules that bind this seat

- **Reserved:** `settings.json`, anything that affects scoring, any tolerance re-ruling. D-table only.
- **The trader's prior:** lead with the more truthful option. If your read is the cheaper option, argue for it explicitly (see `CLAUDE.md`, the three-step test).
- **Dense-doc edits:** Edit tool only. No scripted in-place edits.
- **Output format:** `C:\Users\user\.claude\CLAUDE.md` — point form, no bare section numbers or IDs, verified separated from carried.

## 6. Report back

At most 10 lines for the orchestrator: Q1 answer, Q2 mechanism, Q3 slope-or-step, the D-table rows with your reads, commit hash, anything unverified that matters.
