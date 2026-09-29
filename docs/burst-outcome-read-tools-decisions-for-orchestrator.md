# Burst outcome read tools — decisions for the orchestrator (2026-09-29 UTC)

**From:** the build seat for session 1 of [`docs/burst-outcome-read-spec.md`](burst-outcome-read-spec.md) (the tools build).
**Full evidence:** [`docs/burst-outcome-read-tools-spec-back.md`](burst-outcome-read-tools-spec-back.md), which holds the pasted handles `H-1` to `H-6`, the mutation evidence `E-1` and the "not verified" list. This packet only brings up the decisions and the session's state. It repeats no evidence.

**Recommended seat for the review: Opus 5.5, medium.**
- **Why:** there is one open ruling (`TB-D5`) and eleven auto-proceeded decisions, each one line with a stated reason. The handles are already run and pasted.
- **Move up to high** if you overrule `TB-D5`, `TB-D6` or `TB-D8`. Each changes how run 1 reports, and run 1 must not adapt after it sees outcomes.

---

## 0. State in one table

| Item | State |
|---|---|
| Tools | ✅ Committed `8943324` (not pushed). `SwingFallbackRead` reads the rotated `*col-*.bak` books; new `tools/ops/burst_outcome_read.py` |
| Acceptance | ✅ `H-1` parity (only the timestamp line differs) · ✅ `H-2` arm tables identical to the power count, 9,091 rows, 0 export drops · ✅ `H-3` selftest passes · ✅ `H-4` build 0/0 and `GATE PASSED` |
| Pre-registration | ⛔ No real outcome was printed, aggregated or looked at. The full (outcome) mode has never run on real data |
| Docs | This packet, the spec-back and queue row 1 (marked BUILT). Committed together in the docs commit that follows `8943324` |
| Escalations | None. The trigger for the brief's `H-2` check (a difference larger than the counted drops) could not fire: there were 0 drops |

`TB-D` = "tools build decision". These are new IDs defined in [`docs/burst-outcome-read-tools-spec-back.md`](burst-outcome-read-tools-spec-back.md) §2. No other document uses them.

---

## 1. Pending — needs a ruling before run 1

| ID | Question | Options | Built | Build seat's read (a hypothesis) |
|---|---|---|---|---|
| `TB-D5` | `docs/burst-outcome-read-spec.md` §5 adds a clause to CONFIRMED: *"and the full-sample Holm-adjusted CI excludes 0"*. It does not say which label follows when **only that clause fails** (both halves significant, same sign, but the Holm-adjusted full CI includes 0) | **(a)** Literal first-match-wins. The next rule, "H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT", matches, because the copied `label` checks only that H2 is readable and has the same sign. The label then says H2 is not significant when it is. **(b)** A new label: *"NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (d > 0 / d < 0)"* | **(b)** | **(b)**. Under the three-step test (`CLAUDE.md`, the auto-proceed ruling) (b) is the more truthful option: (a) prints a false statement. I still queue it rather than rule it myself, because it adds a label to a **pre-registered** label set. The spec's author should own that. Reverting to (a) is one `if` in `finalize_label` |

**Shared root:** `TB-D5` and `TB-D4` (below) exist only because the CONFIRMED rule in `docs/burst-outcome-read-spec.md` §5 carries a Holm clause that the copied `label` from `tools/ops/medium_tier_diagnosis.py` never had. If you rule `TB-D5`, check `TB-D4` in the same pass.

**Also worth a look, but not a ruling:** where `docs/burst-outcome-read-spec.md` §5 says "Readable: n ≥ 100 in every group", the copied `rw_run` counts **all** rows of each arm. That includes A rows in strata that have no B row, which the re-weighted statistic drops. I kept the copy verbatim, and the read prints the dropped A rows per test. If you want readability counted on covered strata only, that is a spec change and should be ruled before run 1.

---

## 2. Ruled — auto-proceeded by the build seat (one line each)

All are tools-only and undone by one revert. None touches scoring, `settings.json`, a rendered value, the collector or a CSV header.

| ID | Decision | Pick | Why |
|---|---|---|---|
| `TB-D1` | `H-1` parity target. No committed default-mode output exists for `aws_fetch/20260924-084613`. The only committed default output (`docs/swing-vs-fallback-target-read-2026-09-15-output.md`) is fetch `20260913-153704` at settings v68 | Old binary vs new binary on the named fetch, then `diff` | Step 3: a byte match against the v68 doc is impossible at v69. The binary diff is the stronger check |
| `TB-D2` | Merge order once the rotated books are added | pooled → rotated books (by rotation stamp) → live; the first row seen wins | Matches the books' time order. It only matters where a Timestamp is in two books; today there are 0 verdict conflicts |
| `TB-D3` | `--mode diagexport` read raw columns from pooled + live only | Rotated books added to its raw loader | Without it, every rotated-only row has no logged row and the export exits 3 |
| `TB-D4` | Holm-adjusted CI from a percentile bootstrap | Two-sided bootstrap p = 2 × min tail share; Holm step-down; the adjusted CI is the percentile CI of the FULL draws at level 1 − α_i. A result passes Holm only if the step-down has not stopped AND that CI excludes 0 | The spec names Holm but not the mechanics. This needs the draws, so the copied `boot` has **one added, marked line** |
| `TB-D6` | Holm family sizes | `BO-H1` (the spec's primary A − B test): m = 3 always; a NOT READABLE session enters with p = 1. `BO-H2` (the option (c) test): m = the number of readable sessions | The wording of `docs/burst-outcome-read-spec.md` §4: "Three tests" for `BO-H1`, "Holm across the sessions that are readable" for `BO-H2` |
| `TB-D7` | Reuse `rw_run` verbatim for A − B | B takes rw_run's `MEDIUM` (re-weighted) slot, A takes its `WEAK` slot; the result is negated and the copied `label` re-run | No re-derivation of the statistic. Mutation `m2` shows the negation is load-bearing |
| `TB-D8` | Halves | Recomputed on the analysis population after the data window and every exclusion (first ⌊D/2⌋ UTC trading days) | The export's `Half` column is computed over its whole population, including 1,527 dropped rows and rows outside a run's window. It would split run 1 and run 2 wrongly |
| `TB-D9` | Which labels can gain EDGE-SENSITIVE | Every label except NOT READABLE and NO DIFFERENCE SHOWN; the test is the sign of the pre-edge FULL point | The spec says "a labelled result" without listing labels. Run 2 is wholly post-edge and prints n/a |
| `TB-D10` | Where the arm fields come from | TFISignal, AggrVelBurstRatio, AggrVelNet and AggrVelSignal from the fetch books; side, band, ATR and scores from the export. A verdict mismatch is dropped and counted (0 today) | The spec's join pattern. The export has no TFISignal, ratio or net |
| `TB-D11` | Seed | 20260928, fixed. The selftest also uses 10,000 resamples | The spec says "seed fixed" without a value |
| `TB-D12` | Columns that explain each arm | Raw, not re-weighted: n, success %, gross and net breakeven, net edge, net EV per trade with a day-bootstrap CI | Descriptive only; net EV decides, per `docs/burst-outcome-read-spec.md` §5. ⚠ I did not re-check the breakeven forms against `docs/DeribitIndicatorProject.md` §5a |
| `TB-D13` | `BO-H3` (the spec's descriptive comparisons) content | A − B for low / mid / high and for each fifth; `S_add` − (B − `S_add`); crossed A − uncrossed A; A − B for era E1 vs E2–E4 | `docs/burst-outcome-read-spec.md` §4 and §6.2. CIs only, no label |

---

## 3. Riders and cautions for the orchestrator

- ⚠ **Do not run `SwingFallbackRead --mode census`, `rescore`, `pocgate` or `liqflag` on a fetch that has a rotated book** until their raw loaders also read the rotated books. BUILD 1 put the rotated rows into the shared population. Those four modes still load raw columns from pooled + live only, so rotated-only rows will miss their raw look-ups. On a fetch without a rotated book they are unchanged. Out of this brief's scope; a small follow-up.
- ⚠ **Interleaved commits.** Another seat committed three docs-only commits during this build (`43d9545`, `b4755e2`, `3d9376b`). One of them touched `docs/trader-tick-queue.md`. My queue edit is one row on top of theirs, and the diff shows only that row. `H-5` in the spec-back is scoped to this build's commits for that reason.
- The selftest checks values for `BO-H1` only. For `BO-H2` and `BO-H3` it checks that the tables print, not their values.

---

## 4. Shell availability during this session

- **What happened:** intermittently from about 14:05 UTC (estimated from the run timestamps either side; not logged), both the `Bash` and the `PowerShell` tools failed repeatedly with the harness error *"the server-side auto mode classifier gave no verdict"*. That is the permission check that approves each shell command. It failed before any command ran. It failed eight times in a row near the end of the build, which blocked the docs commit until the next turn. Earlier commands in the same session ran normally, and the shell worked again when this packet was written.
- **Trader's hypothesis (not verified):** the trader thinks PowerShell was busy with another job running at the same time. Another seat did commit during this window (section 3), which fits a second session being active.
- ⚠ **What I checked:** only the error text. The error names the permission check, not the shell process, and `Bash` failed the same way as `PowerShell`. So I cannot confirm the cause. A load-related failure of the check is as likely as a busy PowerShell.
- **Effect on the work:** no handle was skipped. `H-1` to `H-4` and `H-6` ran before or between the failures. `H-5` ran after the shell came back and is pasted in the spec-back. The only effect was the delayed docs commit.
- **For future seats:** if the check fails, stop after two or three retries and do read-only work. After ten failures in a row the turn ends.

---

## 5. Orchestrator rulings — 2026-09-29 (UTC), before any outcome was seen

**Build ACCEPTED.** Re-run by the orchestrator: `--selftest` → `SELFTEST PASSED (0 failed)`; `--counts-only` on `aws_fetch/20260928-121255` → arm table identical to the power count (ASIA 79 / 943, LONDON 77 / 845, NY 169 / 2,862), and counts-only carries an explicit guard (`FORBIDDEN_IN_COUNTS`) against opening outcome columns.

| ID | Ruling |
|---|---|
| `TB-D5` | ✅ **(b)**, the new label. It is the truthful option, and it is added to the pre-registered label set now, before any outcome — recorded in `docs/burst-outcome-read-spec.md` §5 (Label row) |
| `TB-D4` | ✅ Accepted as built (two-sided bootstrap p, Holm step-down, adjusted CI at 1 − α_i) |
| `TB-D6` | ✅ Accepted: it follows the spec's own wording for each family |
| `TB-D8` | ✅ Accepted: halves must be recomputed on the analysis population, or run 1 and run 2 would split wrongly |
| `TB-D1`–`D3`, `D7`, `D9`–`D13` | ✅ Accepted as logged |
| Readability ("worth a look") | ✅ **Ruled: count covered strata only** — the spec's own words are "every group a statistic uses". Implemented by the orchestrator in `rw_run` as a marked deviation from the verbatim copy; `--selftest` re-run: PASSED. Recorded in `docs/burst-outcome-read-spec.md` §5 (Readable row) |
| Rider: `census` / `rescore` / `pocgate` / `liqflag` modes on a fetch with a rotated book | Noted, not built. Do not run those modes on such a fetch until their raw loaders read the rotated books (small follow-up, Sonnet 5, low); queued in `trader-tick-queue.md` §2 |
| Shell outage note (§4) | Consistent with a second seat active at the same time; the same "no safety verdict" failure stopped one of this seat's agents on 2026-09-28. Advice kept: stop after 2–3 retries, do read-only work |
