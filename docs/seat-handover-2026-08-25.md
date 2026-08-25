# Seat handover — 2026-08-25

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-08-24.md`](seat-handover-2026-08-24.md) — superseded for STATE. Its §2.1 is CLOSED, not carried** (see §4.1 below).

**Settings: v68. Master is `1bd268f`, ⛔ AHEAD 5 AND UNPUSHED.** Verify with `git status -sb` — never inherit a push state.

⚠ **ALL DATES AND TIMES HERE ARE UTC.** The machine is GMT+8.

---

## 0. ⭐ FIRST TASK — **#5, C1-coverage F1.** ✅ RULED IN FULL. It is a BUILD, and it is ready to hand over.

**Spec: [`coverage-trailing-edge-f1-proposal.md`](coverage-trailing-edge-f1-proposal.md) — status BUILD-AUTHORIZED 2026-08-25.**

~~*Step 1 is the trader ruling on §4 — D-1…D-6, six COUPLED decisions. Do not open an implementer session until they are ticked.*~~ ✅ **DONE — and it took TWO ticks, not one.** The first ruled D-1…D-6. A post-tick re-read of `tools/BacktestRunner/CoverageReport.vb` then **re-opened D-3 and D-6** and raised **five follow-on sub-decisions (D-5.1…D-5.5)** that the ruling of D-5 had silently created. All eleven are now closed.

⛔ **Hand the implementer that spec's §4b — the single build list. Its §4 is kept as the pre-tick record and still recommends the LOSING option on D-3 and D-6.** A reader who builds from §4 produces a `-1` sentinel and folds the header counters, which is the exact opposite of the ruling. The spec says so at the top and again at §4, but say it in the brief too.

⭐ **D-5 went to (c), the new `HourClass`** — so ~~*"changes the CLI's `--strict` exit code and could fail a scheduled job"*~~ **no longer applies.** `--strict` stays keyed on `HourClass.Defect` alone, and §7 of that spec now requires **proving** the exit code unmoved rather than managing it as a risk.

⚠⚠ **The finding worth carrying, because it overturned that spec's own recommendation:** D-6's argument was that leaving `ObservedLongestGapMs`/`GapBreachHours` alone makes the report under-report. **It is false by construction** — `AccumulateHourStats` charges the WHOLE gap to the hour containing the ENDING trade, so the full gap is always ≥ the trailing edge and is already recorded. Folding is a **no-op** on one counter and a **double-count** on the other. ⭐ **A recommendation written from the mechanism can still be wrong about the arithmetic; the fixture that settled it (`A49u`, 420,000 ms vs 299,999 ms) was already in the tree.**

**The build. Sonnet, effort HIGH, one session.** Unchanged by the amendment — the added items are mechanical. **What keeps it at HIGH is that two ways to get it wrong fail SILENTLY:** `D-5.1`'s precedence (a split hour would report `Captured`, reproducing the SH-1 defect) and `D-4`'s superset trap (a store-end taken by `Max()` over the stats dictionary un-exempts the true last hour).

⭐ **The one thing to carry into the ruling: the crux is NOT the bound the origin ruling names.** [`c1-session1-review-2026-08-04.md`](c1-session1-review-2026-08-04.md) §3 says bound the trailing gap against `ResolveBoundaryUtc`. **That is necessary and NOT sufficient** — it returns `toUtc` unchanged when there is no evidence (`CoverageReport.vb:665`), so it does not protect `A49e` or `A49g`. **"We stopped observing" and "the tape simply ends" are two different exclusions**, and a fix with only the first false-flags the last hour of every run without `ws_health` evidence — which is most manual invocations.

⚠ **The queue's old sizing — "Small-medium; one new fixture" — was wrong in both halves and is corrected in place.** Six existing fixtures break, three more are at risk, and `A49u`'s exact assertion `span0.LongestGapMs = 0` **forecloses the obvious implementation**. The fixture decides, not taste.

---

## 1. State — verified 2026-08-25 09:41 UTC, not inherited

| | |
|---|---|
| Settings | **v68**, tracked `settings.json` line 2 |
| Push state | ⛔ **ahead 5, UNPUSHED** — five `[no-engine-change]` commits |
| Next free fixture family | **A60** (`A59e` is the high-water mark) |
| Next free hard constraint | **HC29** (HC28 is the high-water mark) |
| Production collector | **`i-0d6c133058876273e`**, session 2, unchanged. Started **2026-08-22 16:02:46 UTC**, no restart |
| Gap-repair anchor | **04:02:46 · 10:02:46 · 16:02:46 · 22:02:46 UTC** — ⚠ derived from process start; **re-read it, do not quote these** |
| Defender on the box | ✅ **Original configuration, verified 2026-08-24** — `ScanScheduleDay 0`, **0 exclusions**, RTP on |
| Boundary state | Last ⚠ boundary was v66 (2026-08-10). v67, v68 and everything this session are non-boundary |

### 1.1 ⛔ The memory baseline — CORRECTED 2026-08-25. The bursts are TIME-OF-DAY dependent

⛔⛔ **THE 4.4 % IS WITHDRAWN AS A BASELINE.** It was a correct measurement of **the quietest 80 minutes of the day**, generalised to 24 hours.

~~*Measured 2026-08-24, two independent 30-sample sweeps: 4 non-zero in 90 samples = 4.4 % of samples, bursts of 169–468 pages/sec, steady state 0. Available ~139 MB mean, 114–119 MB trough on 1024 MB; page file flat 39.5 %.*~~ ⚠ *(Struck, not deleted. The `pagesOUT/s 0.0` correction it carried — that the old figure was a one-in-eight draw recorded as a property — still stands; only the replacement number was wrong.)*

**Source of the correction: the hostel-app team's DETACHED 24-hour `logman` collector — 8,640 samples at 10 s, 8,640 expected, no truncation.** ⭐ **It is a better instrument than anything we pointed at this box, for exactly the reason §5 predicted: it is detached, so it does not perturb what it measures.**

| | Ours — attached, 13:05–14:23 UTC | Theirs — detached, 24 h |
|---|---|---|
| Burst rate | 4.4 % (n=90) | **9.10 % pooled — but never quote the pooled figure, see the split** |
| — NY, 13:00–23:00 UTC | — | **1.83 %** (n=3,600) |
| — non-NY, 23:00–13:00 UTC | — | **14.29 %** (n=5,040) |
| availMB | ~139 mean, 114–119 trough | **168 median · 55 routine floor** · 31 absolute |
| Page file | flat 39.5 % | **42.9 % routine max** · 74.79 % absolute |

⭐ **Our 4.4 % is NOT refuted as a measurement** — their hours 13 and 14 read ~5.6 %, which brackets it closely. **NY runs 8× fewer bursts than non-NY, and all four of our sweeps landed inside NY's quietest stretch.**

⚠ **Both absolute extremes (31 MB, 74.79 %) are ONE 60-second excursion, seven samples, 2026-08-24 15:30:37–15:31:37.** Excluding it moves the burst rate 9.10 % → 9.07 % and the page-file max to 42.9 %. **UNATTRIBUTED** — it fell 29 minutes after their collector started, which is suggestive and is not evidence.

⚠⚠ **THE LESSON IS NEW AND IT IS NOT §6's LESSON 4.** Lesson 4 says a number's authority outlives its sample size. **This was not a sample-size failure.** We went **30 → 90 → 180 samples** and every increase felt like more rigour — **but all of them were drawn from the same 80-minute window, so the extra samples bought precision on the wrong quantity.** ⛔ **A BIGGER SAMPLE FROM THE SAME WINDOW DOES NOT FIX A TIME-OF-DAY CONFOUND.**

⛔ **And this project of all projects should have caught it.** The **engine is session-bucketed throughout** — `session_volume`, per-session ROC magnitude, per-session `burst_ratio_threshold`, the ASIA/LONDON/NY split behind half of `DeribitIndicatorProject.md` §15. **We segment every market number we touch by time of day, and then pooled an 80-minute window for a box number.**

⚠ **The typical state is BETTER than we believed; the TAIL is WORSE.** Median availMB **168** against our 139 — **the difference is us: an SSM-attached PowerShell session costs this box 30–50 MB.** But the routine floor is **55 MB**, below anything our 80 minutes ever saw. Below 100 MB on 5.27 % of samples; below 60 MB on 0.05 %.

⚠ **The cause is STILL UNATTRIBUTED, and a THIRD hypothesis died.** Gap repair (refuted 08-22) · Defender scan-on-write (refuted 08-24, 180-sample A/B) · ⛔ **our own file-write volume (refuted 08-25 — our writes are 3× HIGHER during NY, which has 8× FEWER bursts, so paging is ANTI-CORRELATED with them).** **Do not offer a fourth without measuring it.**

**RE-GROUNDED STOP CONDITIONS — from the detached instrument. These REPLACE every sweep-derived number, including the ones relayed to the hostel-app seat:**

| Metric | Baseline | Investigate |
|---|---|---|
| Burst rate, NY | 1.83 % | above ~2.5 % |
| Burst rate, non-NY | 14.29 % | above ~16 % |
| availMB floor | 55 MB | below 55 |
| Page file max | 42.9 % | above 43 % |

⛔ **THE RULE THAT FALLS OUT OF THE SPLIT: COMPARE LIKE-FOR-LIKE BY TIME OF DAY.** A few hours of post-change data cannot be held against a pooled 24-hour figure — **in either direction.**

⚠ **NOT independently verified by us.** These are their numbers, from their instrument, on our box. **The raw CSV, the burst timestamps and the hourly histogram were each offered and none has been requested.** Worth taking — the box is ours and the data is as much ours as theirs.

---

## 2. ⭐ THE OUTSTANDING ITEMS

**Ready to build — nothing owed from the trader:**

| # | Item | Model / effort | Note |
|---|---|---|---|
| **5** | ⭐ **C1-coverage F1 trailing edge** | **Sonnet HIGH** | ✅ **RULED IN FULL — BUILD-AUTHORIZED.** Build from that spec's **§4b**, never its §4. §0 above |
| 6 | `WsTradeProbe` through the shared trade reader (S-1) | Sonnet medium | Fourth trade-parse site; the probe is a delivery gate |
| 7 | Eval-cache backfill — identity key **and** the loop bug (S-4) | Sonnet medium | ⚠ Both halves, or state why one |
| 8 | **A54a** — JSON↔POCO reflection drift guard | Sonnet medium | ✅ **RULED 2026-08-11** (option d + scoped b). Two confirmed instances of the class |
| 9 | Atomic-write total-primitive swap, 5 sites | Sonnet medium | Includes `SettingsLoader.Save`, so it wants fixtures |
| 10 | Weekday filters — **`LivePerformanceTracker`**, then `AnalysisRunner`/`WhatIfRunner` | Sonnet medium | ✅ AutoTweaker **DONE this session**; two surfaces remain |
| 11 | `CoverageReport`'s `gapMs` — a TIME tolerance for a COMPLETENESS check | Sonnet medium | ⚠ **Related to #5. Do NOT bundle** — #5 keeps `gapMs` as-is |
| 12 | `ws_health.log` under-reports a capture outage | Sonnet medium | Needs a route decision (a) or (b) first |
| 13 | An up-interval starts at the `DOWN` line, so a connect window reads as capture time | Sonnet medium | ⚠ The obvious fix trades a false defect for a missed one |
| 14 | Intentional-downtime + venue-outage scoping | Sonnet medium | Spec them together |
| 15 | `DeribitWsFeed` subscribe-reply verification | Sonnet medium | ⛔ **Re-sized — NOT trivial.** Bundle into the next commit that opens that file |
| 16 | **G12** — three manual gaps | Sonnet medium | ⛔ **Re-sized — NOT trivia.** Must end with the `tools/build-manual-pdfs.ps1` regen |
| 17 | `BuildResolutionCfg` stale fixture literal | Sonnet low | ⚠ Decide what it asserts first — the decision is the work |

**Awaiting a TRADER decision, not a build:**

| Item | State |
|---|---|
| **Absorption mechanism revision** — [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 | ⛔ **Written and blind-checked. §6 D-table awaits a tick.** Do not open an implementer session until ticked. **Opus, high** |
| **`_evalCache` unbounded** | ⛔ **No spec exists, and the obvious fix is destructive** — `WriteEvalCache` rewrites the whole file with `append:=False` from five call sites. **Decoupling the list from the file IS the work and comes first.** ~8–17 month runway |
| **CLI-port reversal** | ⚠ Still unwritten into [`roadmap.md`](roadmap.md). **The only known-stale item in that document** — O3's "DEFERRED LAST" is unconfirmed until someone writes it |

**Gated — do not start:**

- **A4** liquidation × OFI — market-gated (needs one real CASCADE line).
- **A5** VPFR shape — data-gated **and** must clear the W6 new-indicator bar.
- **Kelly dated trigger + W6-4 re-run** — ⭐ **ETA ~2026-08-30, five days out.** Needs ≥406 pooled weekday STRONG; bundle both into **one pooled freeze on one span**. ⚠ **The Kelly EST advisory renders a forward promise on screen** — if the ladder still does not separate, the line must be re-worded or the block suppressed.
- **Downtime-repair Part B** — ⛔ forbidden until Part A heals one real outage.
- **Auto-tweaker first live fire** — data-gated (>40 %-failure NY×1 window).

---

## 3. What shipped this session

| Item | Commit | Note |
|---|---|---|
| **AutoTweaker weekday-only filter** (#1) | `d1acb2d` + `9bb46c5` | ✅ **PUSHED.** The weekday-scope ruling finally reaches the only surface that writes `settings.json`. Fixtures A59a–e, mutation-proven, reviewed, three review findings fixed before ship |
| CeilingAudit `expectedVersion` 59 → 68 | `6450911` | The **confirm** step done properly — three consumed values, none changed v59→v68 |
| CeilingAudit **provenance record** | `beb18f7` | Replaced the version-equality WARN entirely. It had rotted three times |
| `ForwardWindowJoiner` embedded-header guard + Spread key name | `5b8515e` | |
| Pipe escaping in the v67 queue row | `6b3eae6` | It was rendering as 5 cells, not 3 |
| **C1-coverage F1 spec** | `1bd268f` | §0 above |

**Earlier the same session, already pushed:** the stale-number sweep across the session-start read set, and the `pagesOUT` baseline correction.

---

## 4. ⛔ Two standing beliefs were REFUTED. Both had already been acted on.

### 4.1 The book's `Timestamp` column is UTC — the "local time" defect does not exist

⛔ **[`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md) §7 is refuted; its §7.0 carries the full correction. One of the two open trader decisions is CLOSED — there is nothing to rule.**

`AnalysisLogger.vb:180` writes the CSV `Timestamp` from **`DateTime.UtcNow`**, and `AnalysisLogger.vb` **never reads `VerdictResult.Timestamp` at all**. Eval cache and bridge are UTC too. The local value reaches only the rendered `TIME` line and the output dump — **both display, the latter deliberately local**.

**What fell with it:** the "two different clocks" claim · "changing the box timezone would shift every new row" (it would shift zero) · the CLI-port risk · and the justification under the *"collector hosts run UTC"* hard constraint. ⚠ **That constraint was relayed to the hostel-app seat on this basis and a correction has been sent.** It stays sound practice; it needs re-grounding.

⭐ **What survives: §7.6's taxonomy** — *persist > transmit >> elapsed / display*. **Line 613 is a display site, exactly the class the rule says not to sweep up.** Good rule, mis-classified instance.

⚠⚠ **The mechanism, because it is the transferable part:** the finding was traced from the field's **assignment** to an **assumed** consumer, and the consumer was never read. §7.4 named `AnalysisLogger` as needing checking *before a fix* — it needed checking *before the finding*. **Trace to the write site, not from the assignment.**

### 4.2 Defender is not the cause of the memory bursts

A 180-sample A/B with five path exclusions applied: **observed 7 against a null prediction of 8**, `p` ≈ 0.83. **No effect.** ⛔ **That also kills the case for disabling Defender**, which was only attractive on the assumption it was doing something. Box reverted and verified.

---

## 5. The hostel-app parallel run — live, and something is due today

**Change 1 (a detached `logman` perf-baseline collector) has been RUNNING since 2026-08-24 15:01:10 UTC and stops itself ~15:01 UTC today.** Scope: `C:\RedInnPricing\logs\perf\` and one PLA collector. No app, no scheduled task, nothing in `C:\DeribitEngine`, session 2 untouched.

⭐ **Their analyzer output, both context snapshots, and an NY / non-NY burst split are owed to us when it completes.** Worth reading for our own question, not just theirs — **their instrument is detached, so it is the best one anyone has pointed at this box.** It answers three things we cannot:

- Whether the **4.4 % burst rate survives a detached instrument**, or was partly our own SSM probing.
- Whether the **138 → 118 MB mean decline** across 80 minutes was real or was us.
- Whether the **daily quick scan** shows as a burst at all.

**Agreed and binding both ways:** neither side touches a scheduled task it did not create · never log off session 2 · neither measures inside the other's write window. **Their window: hourly at `:25`, ~10 s, SYSTEM.** Oversight: notice before each change during proving, standing go-ahead once proven.

⚠ **We owe them our deploy window before the next deploy runs.** We deliberately did **not** take on a standing obligation to relay the gap-repair anchor — they read it themselves from the process start time.

⛔ **Contaminated window, declared:** 2026-08-24 **13:05–14:23 UTC** the box was under near-continuous SSM-attached probing by us. **Nothing from that window is baseline.**

---

## 6. Lessons — each cost something this session

1. ⛔ **A check that reports a result it never performed — FOUR more instances, all mine.** A `perl` mutation that silently did not apply, and I nearly reported the resulting PASS as independent confirmation *inside the review whose job was to catch exactly that*. Two CeilingAudit runs that died on a CSV gate before reaching the line under test. A `grep -c` that returned 303 because `\|` is alternation in BRE. **The fix is always: assert the check RAN before believing its result.**
2. ⚠ **`|` in a regex is alternation, and escaping it through bash → perl fails silently and looks like success.** Three consecutive verification failures on a one-character fix. **Use exact string replacement when the pattern contains pipes.**
3. ⛔ **Trace a defect to the WRITE SITE, not from the assignment.** §4.1. One unread line produced a hard constraint, a queued trader decision, a claim about the CLI port, and guidance sent to another team.
4. ⚠ **A number's authority outlives its sample size.** "pagesOUT/s 0.0 on all thirty" was a one-in-eight draw recorded as a property, and it had propagated into a second team's stop condition.
5. ⚠ **Sizing a queue row from its FIX description rather than its mechanism.** Both re-sized items this session — `DeribitWsFeed` and F1 — read "trivial"/"small-medium" because someone described the *change*, not what it takes to reach it.

---

## 7. Loose ends

- ⛔ **Five commits unpushed.** All `[no-engine-change]`, settings stays v68. The pre-push hook runs `verify-gate.ps1` in `prepush` mode.
- ⚠ **`tools/AutoTweaker/state.json` is UNTRACKED** and records a real run (2026-06-18, `SKIPPED_INSUFFICIENT_TIER`, cursor at 81). Same nearly-lost shape as `tools/mem-probe.csv`. **Commit it or ignore it deliberately.**
- **F2's fix in the AutoTweaker build has no fixture** — a console string with no consumer, and every A59 row is in-population so no fixture can distinguish the two definitions. Named in the commit, not papered over.
- **The `DeribitWsFeed` `ResetBufferState` race (F2)** and the collector's `User-Agent` (F3) remain open, both verified still present.
