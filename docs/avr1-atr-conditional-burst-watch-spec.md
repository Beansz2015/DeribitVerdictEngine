# `AVR-1` build — ATR-conditional burst-watch reference — SPEC

**Ruling:** `AVR-1` = (b), trader, 2026-09-26 (UTC). Decision record: [`aggr-vel-burst-rederivation-read-2026-09-26.md`](aggr-vel-burst-rederivation-read-2026-09-26.md) §5. **Author:** orchestrator seat, 2026-09-26. **Class:** tools only. No `settings.json`, no engine code, no CSV schema, no rendered value.

---

## 0. Implementer brief — read this first

**Model + effort: Sonnet 5, medium.**

- **Why that tier:** every design decision is made below, and both mechanical halves have in-repo templates. The loader, population rules and ATR-fifth binning come from [`tools/ops/aggr-vel-regime-read.ps1`](../tools/ops/aggr-vel-regime-read.ps1). The per-day coverage table and slice verdicts come from [`tools/ops/asia-burst-watch-read.ps1`](../tools/ops/asia-burst-watch-read.ps1). The build combines them and adds one lookup table.
- **Where Sonnet will slip:**
  1. **Using the wrong column of the re-derivation read.** The reference is the **"All rows"** column of `aggr-vel-burst-rederivation-read-2026-09-26.md` §2.2 (full table in this spec's §3). **Not** the "From 08-20 only" column.
  2. **Editing `asia-burst-watch-read.ps1`.** It is the frozen instrument of three published reads. **Do not modify it.** Write a new script.
  3. **Re-implementing the loader instead of copying it.** `-VerifyReference` (§4) must reproduce the §3 table from raw data. That works only if population, dedup, session assignment and binning match `aggr-vel-regime-read.ps1` exactly. Copy that code path, don't paraphrase it.
  4. **Reading only two books.** A fetch folder holds `analysis_log.csv.v0.7.bak`, every `analysis_log.csv.*col-*.bak`, and `analysis_log.csv`. Pool all of them, as both templates now do.
  5. **Giving LONDON a verdict.** LONDON has no ruled band. Report it, and print "no ruled band" where the verdict would be.
- **No fixture harness covers PowerShell tools.** The acceptance handles in §6 are the tests. **Run every handle and paste its actual output** (`CLAUDE.md` hard rule). A handle you did not run is a guess.
- **Escalation triggers — stop and come back to the orchestrator:**
  - `-VerifyReference` fails on any bin by more than the §4 tolerance.
  - The build seems to need any `settings.json`, engine (`Core/`, root `.vb`) or CSV change.
  - A ruled number in §3 or §5 looks wrong to you. Do not "fix" it; report it.
- **Sessions:** one.
- **Session start:** a scoped implementer seat. Read `CLAUDE.md`, then this spec, then the two template scripts. The full session-start protocol reads are waived for this seat by trader direction (2026-09-26), to save usage.

---

## 1. What changes, in one paragraph

The burst watches compared a session's fire rate with a **fixed** reference (ASIA 11.0 %, ruling T-1; NY band 8–12 %). The re-derivation read showed the fire rate is **volatility-conditional** in all three sessions, so a fixed reference alarms on the market, not on the engine. From now on, a watch read compares the **observed** fire rate with the **expected** fire rate for the ATR mix of the rows actually read. Each row's expected fire probability is the reference rate of its session's ATR fifth (§3). The band width, same-side bar, length rule and coverage rule do not change.

---

## 2. Deliverables

| # | File | What |
|---|---|---|
| 1 | **New** `tools/ops/burst-watch-read.ps1` | The watch instrument, all three sessions, read-only |
| 2 | **New** `docs/avr1-atr-conditional-burst-watch-spec-back.md` | Spec-back per [`batch-review-packet-convention.md`](batch-review-packet-convention.md): ranked handles with pasted output, decisions you took (one line each), what you did not verify |
| 3 | `docs/trader-tick-queue.md` §4 | In the `AVR-1` / `AVR-2` line, mark owed item (1) as built, with the commit hash and a link to the spec-back. Nothing else in that file |

**Commit locally with `[no-engine-change]`. Do not push.** The trader pushes.

---

## 3. The ruled reference — ATR-fifth table (a RULING, not a settings key)

Source: `aggr-vel-burst-rederivation-read-2026-09-26.md` §2 output, "all rows", weekday rows 2026-08-02 00:00 → 2026-09-25 08:51:01 UTC, fetch `aws_fetch/20260925-085341`. Put this table in the script as literals, with a comment stating "RULED `AVR-1` 2026-09-26 — reference, not tunable; source doc §2". It is `MECHANISM`-class under the fixture-literal provenance rule: a frozen measurement of record, like the old `$BaselineRate`.

**Bin rule for new data:** a row's fifth is the first `i` with `ATR ≤ edge_i`; above the last edge is fifth 5.

| Session | Upper edges (fifths 1–4) | Reference fire rate, fifths 1 → 5 | Rows per fifth |
|---|---|---|---|
| ASIA | 33.6 · 51.2 · 66.2 · 83.9 | 15.46 % · 8.51 % · 5.13 % · 4.69 % · 2.87 % | 1,151 |
| LONDON | 31.9 · 51.5 · 68.4 · 90.7 | 18.65 % · 10.03 % · 3.58 % · 3.72 % · 1.86 % | 697–698 |
| NY | 19.3 · 30.9 · 43.0 · 62.9 | 18.55 % · 9.72 % · 5.78 % · 3.45 % · 1.48 % | 4,927–4,928 |

- **Expected fire rate of a set of rows** = the mean of each row's reference rate (row-weighted). **Expected fires** = the sum.
- ⚠ Session ATR is resolution-specific: NY is 1-minute, ASIA and LONDON are 3-minute. Never apply one session's edges to another.

---

## 4. `-VerifyReference` — the table must reproduce from raw data

A switch that recomputes §3 from the fetch folder, using the reference window above (`-RefFrom 2026-08-02`, `-RefTo '2026-09-25 08:51:01'` inclusive; defaults set to these). It uses the **same equal-row fifths** as `aggr-vel-regime-read.ps1`, not the edge rule. It prints recomputed edges and rates next to the literals.

- **Pass:** every recomputed rate within **0.10 pp** of the literal, and every recomputed edge within **0.1** of the literal. Exit code 0.
- **Fail:** print which bins differ and exit non-zero. **This is an escalation trigger** (§0).
- Why it exists: a transcription error in §3 would silently skew every future read. The switch is the guard, and the spec-back must show it passing.

---

## 5. Read logic — what a normal run prints

**Parameters:** `-FetchFolder` (required), `-Session ASIA|LONDON|NY|All` (default `All`), `-From` / `-To` (the read window, dates, inclusive; `-To` default = last day in the data), `-VerifyReference`.

**Population and rules — unchanged from the templates:**

| Item | Value |
|---|---|
| Rows | Weekday (UTC day-of-week), `AggrVelBurstRatio` non-empty, ATR parses, deduplicated on `Timestamp` across books |
| Session | UTC hour against tracked `settings.json` `session_volume.sessions` (ASIA 0–7, LONDON 8–12, NY 13–23 at v69; read at run time) |
| Fire | `AggrVelSignal` = `BURST_BUY` or `BURST_SELL` |
| Same-side | `BURST_BUY` with TFI `BUY PRESSURE`, or `BURST_SELL` with `SELL PRESSURE` |
| Covered session-day | First row ≤ session start + 6 min, last row ≥ session end hour :54, no gap > 6 min between consecutive rows of that session. Same rule as the ASIA template, applied per session |

**Per covered weekday session-day, print:** date, rows, fires, observed %, expected %, **diff (pp) = observed − expected**, median ATR, in-band flag, InstanceId(s). Also list uncovered weekdays with the reason.

**The slice counts covered weekday session-days only**, as the ASIA template does. An uncovered day is listed, never pooled.

**Per session, for the read window, print the slice:** days, rows, fires, observed %, expected %, diff pp, same-side %, day-level mean and sd of the daily diff, and the verdict below.

| Session | Band (ruled) | Same-side | Length | Verdict rule |
|---|---|---|---|---|
| ASIA | **expected ± 3 pp** (T-2, width kept by `AVR-1`) | ≥ 85 % (T-4) | ≥ 10 covered weekdays (T-3) | PASS if all three hold; else MISS, naming each failed criterion |
| NY | **expected ± 2 pp** (the v52 watch band, `DeribitIndicatorProject.md` §12, width kept) | ≥ 85 % | — | Its own trigger: **MISS when 2 consecutive covered weekday session-days are out of band**. Print every such pair |
| LONDON | **no ruled band** | ≥ 85 % (report only) | — | Print "no ruled band", no verdict |

- Print the band on the slice line as absolute numbers too, for example `expected 4.12 %, band 1.12–7.12 %`. Clip the lower bound at 0.
- ⚠ **Known property, do not change it:** a ±3 pp band around a 3 % expected rate is loose. That is the ruling as made; note it in the spec-back if the data shows it matters.
- The NY engagement arm of the v52 watch (TFI-modifier share of directional votes) is **out of scope**.

---

## 6. Acceptance handles — run each, paste the output into the spec-back

| # | Handle | Pass condition |
|---|---|---|
| `H-1` | `powershell -NoProfile -File tools/ops/burst-watch-read.ps1 -FetchFolder 'aws_fetch\20260925-085341' -VerifyReference` | Exit 0; all 15 rates within 0.10 pp and 12 edges within 0.1 |
| `H-2` | The same fetch, `-From 2026-08-03 -To 2026-09-25` (the reference window's weekdays) | Per session, observed fires within **±2 %** of expected fires. The reference was measured on these rows, so a larger gap means the edge lookup or the population differs from the measurement |
| `H-3` | The same fetch, `-Session ASIA -From 2026-09-14 -To 2026-09-25` | ASIA observed **42 fires, 1,269 rows, 3.31 %, same-side 92.86 %, 8 days** — the numbers of [`d3-asia-burst-watch-read-2026-09-26.md`](d3-asia-burst-watch-read-2026-09-26.md) §0. Report the new expected %, diff and verdict. Expected verdict: MISS on length (8 < 10); state whether the rate is inside its ATR-conditional band |
| `H-4` | `git diff --stat` against the build's start commit | Only the three §2 deliverables changed. `tools/ops/asia-burst-watch-read.ps1` and `tools/ops/aggr-vel-regime-read.ps1` untouched |

The `H-3` observed numbers are a population check against a published read: if they differ, the population differs, and that is an escalation.

---

## 7. Out of scope

- Any threshold change (`AVR-2` = (a), ruled: no change until the outcome read).
- The outcome read and its power count (dated in `trader-tick-queue.md` §4, due on or after 2026-09-28).
- Scheduling future watch reads or editing the watch rows in `DeribitIndicatorProject.md` §12. The orchestrator does that after reviewing the spec-back.
- Re-measuring the §3 table on newer data. The reference is frozen by ruling.
