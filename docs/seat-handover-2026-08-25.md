# Seat handover — 2026-08-25

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-08-24.md`](seat-handover-2026-08-24.md) — superseded for STATE. Its §2.1 is CLOSED, not carried** (see §4.1 below).

**Settings: v68. Master is `1bd268f`, ⛔ AHEAD 5 AND UNPUSHED.** Verify with `git status -sb` — never inherit a push state.

⚠ **ALL DATES AND TIMES HERE ARE UTC.** The machine is GMT+8.

---

## 0. ✅ **#5, C1-coverage F1 — BUILT, REVIEWED, ACCEPTED 2026-08-26 (`4032f9c`). It is DONE bar the push.**

⛔ **THE ONLY THING OUTSTANDING ON IT IS THE TRADER'S TEST + PUSH.** Local commit only, per `docs/trader-profile.md` §8.

⭐ **What the review verified by RUNNING, not by reading:** harness **306/306** rebuilt from a clean tree · all six `F1-` fixtures confirmed to have **executed** · **both** silent-trap mutations re-applied independently, each proving its fixture is the **only one of 306** that catches it · the real-store run reproduced exactly (100 / 4 / 0 / 112 / 96, exit 1) · repo root confirmed clean.

⚠ **Three review findings, none blocking.** **F-1** — the batch summary's reproduction recipe named the wrong store and does not reproduce; **fixed in place**, with a verified recipe. **F-2** — only ONE of `D-4` (c)'s three bounds ever binds, so `F1-c` passes for a different reason than the spec claims; **recorded as [`coverage-trailing-edge-f1-proposal.md`](coverage-trailing-edge-f1-proposal.md) §3a, no code change.** **F-3** — a display-only over-report, now **item 18** in §2.

⭐ **The build also caught a real defect in the SPEC:** "between `Defect` and `Captured`" conflated combine precedence with enum declaration order, and the enum declares `Captured` first. The implementer derived the right slot from the spec's own "shifts four ordinals" note instead of guessing.

*(Superseded framing follows, per the quote-and-label convention.)* ~~**FIRST TASK — #5, C1-coverage F1. RULED IN FULL. It is a BUILD, and it is ready to hand over.**~~

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

### 1.1a ✅ INDEPENDENTLY VERIFIED 2026-08-26 — and the NY/non-NY split is ITSELF too coarse

⭐ **We now hold the raw file and re-derived everything from it. Every headline figure reproduces EXACTLY** — overall 9.10 %, NY 1.83 %, non-NY 14.29 %, availMB min 31 / max 276 / mean 158.0, `<100 MB` 5.27 % · `<80` 0.57 % · `<60` 0.05 %, page file max 74.79 %, max `pagesOUT` 11522.1 at 15:30:47, **and all 24 hourly rows.** ⛔ **Drop the old "not independently verified by us" caveat — it no longer applies.**

⚠ **One figure did NOT reproduce exactly, stated because everything else did:** their excursion-excluded burst rate reads **9.07 %**, ours **9.08 %**. ~~*a boundary-inclusion difference of one sample*~~ ⛔ **THAT DIAGNOSIS WAS WRONG — corrected 2026-08-27, verified from raw.** The true rate is **9.0760 %**: it **rounds to 9.08** and **truncates to 9.07**. Both boundary treatments give the identical n and the identical rate, so boundary inclusion **cannot** produce 9.07 in either direction. **It is a truncation-vs-rounding artefact in their tooling**, carried into their handover and forward again without re-derivation. ⭐ **Our value was right, our reason was wrong, and naming the gap instead of rounding it away is what found a real defect — just not the one we were looking for.**

⛔⛔ **THE SAME POOLING ERROR, ONE LEVEL UP — and they found it, not us.** The NY/non-NY split we adopted **five sections ago** is itself too coarse. The real shape is three bands, not two:

| Band | Hours (UTC) | Rate | |
|---|---|---|---|
| **Busy** | 00–12 | **11.4–18.3 %** | |
| **Transitional** | 13–15 | **4.7–5.6 %** | ⛔ classified NY, but **3× the NY average** |
| **Near-silent** | 16–23 | **0.0–1.1 %** | ⛔ hour 23 is classified non-NY and reads **0.56 %**, dragging that average |

**Per-hour, verified — this table is the stop condition, not the band summary:**

| H | % | H | % | H | % | H | % |
|---|---|---|---|---|---|---|---|
| 00 | 12.22 | 06 | 18.33 | 12 | 15.56 | 18 | **0.00** |
| 01 | 11.39 | 07 | 15.28 | 13 | 5.56 | 19 | 1.11 |
| 02 | 13.33 | 08 | 18.33 | 14 | 5.56 | 20 | 0.28 |
| 03 | 12.22 | 09 | 17.50 | 15 | 4.72 | 21 | 0.28 |
| 04 | 17.22 | 10 | 17.50 | 16 | 0.28 | 22 | 0.56 |
| 05 | 15.28 | 11 | 15.28 | 17 | **0.00** | 23 | 0.56 |

⛔ **THE RULE, REPLACING THE SESSION SPLIT: COMPARE AGAINST THE PER-HOUR ROW, NEVER A SESSION AVERAGE.** Post-change data at 13:00–15:00 judged against NY's 1.83 % reads as a **3× breach that is only the wrong denominator**; the same comparison at 16:00–22:00 **hides a real one**. ⭐ **They proposed this themselves and it is stricter on them.**

⚠⚠ **THIS IS THE SECOND INSTANCE OF ONE ERROR IN ONE ARC.** We pooled 80 minutes and called it a day; then we pooled 10 hours across a 3×-varying range and called it a session. **Segmenting once is not segmenting correctly — check that the segments are homogeneous, not merely that segments exist.**

**Remaining stop conditions, verified:**

| Metric | Baseline | Investigate |
|---|---|---|
| Burst rate | **the per-hour row above** | above ~1.5× that hour |
| availMB routine floor | 55 MB | below 55 |
| Page file routine max | 42.9 % | above 43 % |

### 1.1b ⛔ THREE OF OUR OWN OBSERVATIONS WERE WRONG — corrected 2026-08-27, all re-verified from raw

⚠ **We published three claims about the `:25` minute-of-hour dip. The hostel-app seat checked all three against their copy and all three were wrong.** Re-derived from our own copy and **every correction holds.** Quoted rather than deleted, per the convention.

| Our claim | ⛔ Truth | How we got it wrong |
|---|---|---|
| ~~"the whole between-minute spread is ~6 MB"~~ | **27.9 MB** — lowest `:30` = 145.8, highest `:17` = 173.7 | ⛔ **We read the range off a `head -8` of a sorted list.** The 8 rows we printed spanned ~6 MB; the other 52 were never looked at |
| ~~"three of the twelve lowest fall at :26–:27"~~ | **Four** | A miscount of **our own printed output** |
| ~~"their perf probe ran hourly at `:25`"~~ | ⛔ **It did not.** The PLA collector sampled **uniformly every 10 s** — we measure **8,639 gaps of 10 s and exactly one of 11 s** across the full 24 h | **Inherited from §5's own *"their window: hourly at `:25`, ~10 s"* line, which described the PROVING-period agreement, not the Change-1 collector.** We carried a doc line into a data claim without checking it against the data sitting in front of us |

⛔ **AND TWO OF OUR "SEPARATE FINDINGS" ARE THE SAME SAMPLE.** `08/25 14:26:06` (availMB 55) is the sample the **55 MB routine floor** rests on **AND** one of the two `:26–:27` events. **They cannot corroborate each other, and we presented them as independent in the same section.**

⚠ **The cluster is TWO EVENTS, not four samples.** `06:27:17` / `:27` / `:37` are **three consecutive 10-second samples of one ~20-second dip** (verified: the surrounding samples read 123 → 102 → 66 → 65 → 65 → 70). Plus `14:26:06`. **n = 2.**

⛔ **The strongest argument against instrument-relatedness is one we never made, and they did:** **`:30` (145.8 MB) and `:45` (145.9 MB) are BOTH lower than `:25` (147.0 MB)**, and nothing of theirs runs at either. **Minute-of-hour variation exists on that box independently of anyone's software.**

⭐ **THE PREDICTION SURVIVES, AND ON A BETTER FOOTING THAN WE GAVE IT.** Because their baseline collector had **no `:25` periodicity at all**, the baseline `:25` figure is a **CLEAN CONTROL**, not a contaminated reading. Change 2's task **does** run at `:25`. So there is a genuine before-and-after with an uncontaminated "before":

- **If the `:25`–`:27` dip DEEPENS in the soak data ⇒ it is their app, and nothing else.**
- **If it does not deepen ⇒ the baseline dip was noise and this is closed.**

⚠ **But the PRIOR is weaker than we implied.** n = 2 events · one of them is the floor sample · two other minutes sit lower. **Record the prediction; do not lean on it.**

⛔ **All four sub-60 MB samples, since "routine floor 55" rests on them:** three are inside the 15:30 excursion (32 · 56 · 31, all with `pagesOUT` 3,100–5,900); **the fourth is `08/25 14:26:06` — availMB 55, `pagesOUT` 0, page file flat at 39.24 %.** A quiet dip with **no paging pressure behind it at all**, and it is where the floor actually comes from. **The floor and the excursion are two different phenomena.**

⚠⚠ **THE LESSON, AND IT IS THE THIRD INSTANCE IN THIS ARC OF ONE FAILURE:** we pooled 80 minutes and called it a baseline · we pooled 10 hours across a 3×-varying range and called it a session · **and then we read a 60-value range off the 8 rows we happened to print.** ⛔ **Each time the error was a TRUNCATED VIEW mistaken for the whole. Not a reasoning failure — a looking failure. Recompute from raw; do not read conclusions off a convenience view.**

---

## 2. ⭐ THE OUTSTANDING ITEMS

**Ready to build — nothing owed from the trader:**

| # | Item | Model / effort | Note |
|---|---|---|---|
| ~~**5**~~ | ✅ ~~**C1-coverage F1 trailing edge**~~ | — | ✅ **BUILT, REVIEWED AND ACCEPTED 2026-08-26** (`4032f9c`). Local commit — **trader tests and pushes.** Both silent traps guarded, each by exactly ONE fixture of 306, **both mutations re-run independently by the reviewing seat.** Real-store run reproduced exactly (100/4/0/112/96, exit 1). Packets: [`coverage-trailing-edge-f1-batch-summary.md`](coverage-trailing-edge-f1-batch-summary.md) · [`coverage-trailing-edge-f1-spec-back.md`](coverage-trailing-edge-f1-spec-back.md). **Three review findings, none blocking — F-1 and F-2 fixed in the docs, F-3 is item 18 below** |
| **18** | ⚠ **`ObservedLongestTrailingMs` can over-report on a split hour** | Sonnet low | ⚠ **NEW 2026-08-26, from the F1 review. Display-only — no classification, no `--strict`, no gate.** `BuildResult` computes the figure from **whole-hour** `HourStoreStats`, so a split hour whose trailing span is NOT the last one — span 0 ON with trailing silence, a marker turns capture off, span 1 `NotCapturing` with no trades — reports a trailing edge measured to the HOUR end, i.e. **a value no span actually had.** ⛔ **The build's own spec-back characterised this as matching the sibling counters' pre-existing imprecision. It does not: `ObservedLongestGapMs` measures a genuine whole-hour quantity; this one can invent a number.** ⚠ **Reachable by inspection, NOT demonstrated — nobody has constructed the case, and the first job is a fixture that produces it.** `D-6` (c) only ever required the counter to exist, so this is a refinement, not a defect against the ruling. **Decide first whether the honest fix is per-span stats or simply not reporting the figure on split hours** |
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
| ~~**19 — the shared-workstation rule**~~ (§5.6) | ✅ **RULED 2026-08-27 (trader). See §5.6 for the rule as it now stands — it is narrower than either draft.** |

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

## 5. The hostel-app parallel run — Change 1 CLOSED, Change 2 DEPLOYING

### 5.1 ✅ Change 1 is DONE and it delivered. Its three answers are in §1.1

~~*Change 1 (a detached `logman` perf-baseline collector) has been RUNNING since 2026-08-24 15:01:10 UTC and stops itself ~15:01 UTC today.*~~ **It stopped cleanly — 8,640 samples against 8,640 expected, no truncation, no reboot.** All three of our questions are answered and the results are the corrected baseline in §1.1 above: bursts are **time-of-day dependent**, the **138 → 118 MB decline was our own SSM session**, and the **daily quick scan is not the driver** (n=5, correctly not called either way).

⛔ **THE RAW CSV IS ALREADY ON OUR BOX AND NEVER LEFT IT** — `C:\RedInnPricing\logs\perf\baseline_08241501.csv`, 8,640 rows, plus `context_start_*.txt`. **We do not need them to send it.** ⚠ **But reading it ON the box attaches a session and costs the same 30–50 MB this whole exercise measured — copy it off first.** The baseline is finished so there is nothing left to contaminate; the cost is real anyway.

**Still coming from them, generated OFF the box:** the hourly histogram (24 hours, NY/non-NY marked) · the burst timestamps with values · the raw samples inside the 04:22:42–04:23:32 scan window. **Their analyzer ships to `C:\RedInnPricing\deploy\analyze-perf.ps1` with Change 2**, and it **refuses to apply a burst threshold unless one is passed explicitly** — it will not inherit a number from anywhere, including from them. Good design given how this arc went.

### 5.2 ⛔ Change 2 — DEPLOYING, on the conditional acknowledgement. TWO credentials land

**Acknowledged conditionally on two questions; both answered 2026-08-26, so it proceeds without a second acknowledgement — that was explicit in our reply.** ⚠ **Both questions found something, which is the point of having asked.**

**Q2 found a real gap: `MultipleInstances` and `ExecutionTimeLimit` did not exist.** Not at defaults — **no task-creation script had been written at all.** Now `IgnoreNew` / `PT10M`. ⛔ **And they read their own source rather than assume: NO timeout is set on any remote call** — `HttpClient`, `SheetsService` (plus default backoff retries) and `SmtpClient` all run to 100 s defaults or unbounded, across eight Sheets call sites and one HTTP call per run. ⚠⚠ **`SmtpClient.Timeout` is documented as applying to SYNCHRONOUS sends only, so it does not bound `SendMailAsync` — they flagged this as untested and it is correct. It does not change the conclusion, because they never set `Timeout` anyway.** ⭐ **`ExecutionTimeLimit` is therefore the ONLY bound on a hung run, not a backstop.**

⚠ **`IgnoreNew` converts stacking into SILENCE** — one hung instance and every later run is skipped with nothing raising an error. **They named this themselves before we did**, and it is covered: their acceptance criterion **counts `RIP-RESULT` lines with `status=OK` across all 48 hours**, so a missing run FAILS the soak. **They assert the run happened rather than infer it from the absence of an error** — the discipline this project keeps writing lessons about.

**Q1 was a concession, not a confirmation.** Machine scope for the Gmail app password was **a default written up as a decision** — the stated reason ("so the SYSTEM task inherits them") never distinguished machine scope from SYSTEM's own user scope, which would inherit too. **It is now an owner DECISION**: machine scope, because the narrowing does not change *who* can reach the box (anyone who can read the machine environment there is already an administrator) and the alternative means carrying an untested path plus a fallback branch through a migration. **DPAPI stays out of reach — the app reads a plain env var, so it is a code change.**

⛔⛔ **AND THE SCOPE WAS WORSE THAN THEIR OWN NOTICE DESCRIBED, until hours before deployment.** Their repo's `appsettings.json` holds the **OLDER** Gmail app password — **the one that was publicly exposed.** Anyone deploying would have copied it straight out of the repo, and **if it still authenticated it would have WORKED, so nothing would ever have surfaced the mistake.** ⭐ **They disclosed this unprompted rather than quietly swapping it.** What lands is a **brand-new password created for this box, never in a repo, tarball, backup or anywhere public**; the leaked one is being revoked. ⚠ **"Being revoked tonight" is a FUTURE-TENSE claim — get it confirmed, do not carry it as done.**

**WHAT WE ARE ACCEPTING, stated as scope rather than as reassurance:**

| # | Credential | Form | Protection |
|---|---|---|---|
| 1 | Google service-account private key | **File**, `C:\RedInnPricing\Credentials\` | `icacls /inheritance:r`, granted to SYSTEM + Administrators only. ⚠ **Would have been left at inherited permissions — i.e. `BUILTIN\Users: Read & Execute` — had we not asked** |
| 2 | Gmail app password | **Machine-scope environment variable** | **None. Readable by every process on the box.** Accepted knowingly |

**Blast radius of both is theirs** — hostel spreadsheets and a hostel mailbox. Nothing of ours is reachable from either.

⚠ **OPEN, and worth an answer: what happens to both credentials if the soak fails or the migration is abandoned?** They state the new password is *"the one we keep"* and is excluded from their post-cutover rotation sweep — **so a credential landing for a 48-hour soak is in fact permanent unless someone says otherwise.**

### 5.3 ✅ DEPLOYED 2026-08-26. Everything promised was read back, not asserted

**They delivered all three read-backs. ⚠ We can verify NONE of the on-box ones — no access from here — so these are their readings, labelled as such:**

| Read back | Value |
|---|---|
| ACL on `C:\RedInnPricing\Credentials` | `BUILTIN\Administrators:(OI)(CI)(F)` + `NT AUTHORITY\SYSTEM:(OI)(CI)(F)`, inheritance broken, **no `BUILTIN\Users`** |
| Task | `IgnoreNew` · `PT10M` · SYSTEM / `ServiceAccount` (non-interactive) · trigger 00:25 UTC repeat PT1H |
| Our collector | **PID 4180, start `2026-08-22T16:02:46Z` — unchanged, re-read TWICE** (at deploy and again after task registration). Their 15:25:25 is **22m39s clear** of the 16:02:46 repair pass |
| Footprint | 51 files, all under `C:\RedInnPricing\` |

⚠ **Self-corrected: "41 published files" was wrong, it is 43** — their own handover carried two irreconcilable counts side by side. Minor, and volunteered.

**Manual first run PASSED** — `exit=0`, 6.7 s against a predicted 7.2 s, `fallback=False` (so Sheets answered, proving credential + ACL in one shot). ⭐ **And they did NOT count the SMTP leg as proven**: the run fell inside their quiet period, so `email=suppressed-quiet` — *"a run that sends nothing proves nothing."* They exercised the credential separately and confirmed it **from Google's own "last used" timestamp rather than from their log claiming success.** That is the assert-the-check-ran discipline, applied to themselves.

⚠ **ONE THING WE MAY SEE FIRST.** Our box's Task Scheduler service was running **before** the environment variables were set. If it passes a stale environment block, **the first `:25` run fails SMTP** while their manual run succeeded. **The tell is `email=SEND-FAILED` in the `RIP-RESULT` line.** Their configuration problem, not ours; fix is a service restart.

**Their `RIPPerfBaseline` collector is left REGISTERED AND STOPPED** for the post-deployment comparison. They asked whether we want it removed. ⭐ **Keep it** — a post-soak comparison needs the same instrument, and a stopped collector costs nothing.

✅ **CLOSED 2026-08-26 — the leaked older Gmail password IS revoked, confirmed by the trader.** ⚠ **Provenance, stated precisely: this is the trader's confirmation, not a read-back we performed against Google.** The credential never reached our box in any case; what it closes is the disclosure, not a risk to us.

✅ **ANSWERED 2026-08-27, and the suspicion was right — nobody had decided it.** *"The one we keep"* was describing a **rotation-sweep exclusion**, not a choice that a credential placed for 48 hours becomes a permanent resident of our box. **Same shape as the machine-scope answer: an outcome nobody chose.** Now an owner decision, written into their plan and runbook:

| Outcome | Credential end state |
|---|---|
| **Soak passes, they migrate** | **Both stay**, reviewed at their Phase 5 — **a decision, not a default** |
| **Soak fails, or abandoned** | **Both go**, five ordered steps: delete the task · delete `C:\RedInnPricing\` entirely incl. `Credentials\` · remove all four machine env vars · **REVOKE `RedInnWindows` in the Google app-password list** · **REISSUE the service-account key** |

⭐ **Steps 4 and 5 are the whole point, and 5 is the one that gets skipped.** Deleting the key FILE from our box is **not** the key being dead — it sat on a machine they do not own. **Only reissuing closes that, and only that is checkable from outside.** Neither step is conditional on anyone believing something went wrong: they are what makes the removal **verifiable rather than asserted**.

### 5.4 Soak status — running, and one leg still unproven

**First scheduled run fired 15:25:25 UTC:** `LAST_RESULT=0x0`, `NUM_MISSED=0`, `exit=0`, `fallback=False`.

✅ **The stale-environment risk is RESOLVED, and by inference rather than luck.** `fallback=False` means the scheduled run read `GoogleSheets__CredentialsPath` out of the machine environment — without it Sheets fails and the app drops to hardcoded rates. All four variables live in the same registry key, **so `Email__Password` is reaching the task too.** ⛔ **Stop watching for `email=SEND-FAILED` on that account.**

⚠ **SMTP THROUGH THE APP ON A SCHEDULE IS STILL UNPROVEN.** Both runs so far read `email=suppressed-quiet` — their quiet period is 20:59–07:00 business time and both landed inside it. **The first run that CAN send is 23:25 UTC, and a real change is queued for it.** The credential is proven from our box; **the app's own send path on a schedule is not, and they are not counting it.** ⛔ **Treat the soak as having an untested leg until that line comes back.**

### 5.5 ✅ `RIPPerfBaseline` comparison run — ACKNOWLEDGED 2026-08-27, starting 15:01 UTC

**Trader-approved as scoped:** the **existing** collector, same counters, same 10 s interval, same PLA service, **nothing attached**, 24 h self-stopping, output to `C:\RedInnPricing\logs\perf\`. **Nothing of ours touched.** ⭐ **They refused to read our "soak away" as covering this — correct, it was not.**

⭐ **Start time is 15:01 UTC by our request, and the reason is phase-matching, not neatness.** Their baseline began **15:01:07 UTC**, and **hour 15 in that dataset is not one visit but TWO partial ones** (15:01→16:00 on 08/24 plus ~15:00→15:01 on 08/25) pooled into its 360 samples. **Any other start gives hour 15 a different composition — and hour 15 is a transitional hour at 4.72 %, where composition matters most.** It also sits fully inside the soak window and captures 24 consecutive `:25` runs, which is what the `:25` test needs.

**What this run settles**, per §1.1b: **if the `:25`–`:27` dip DEEPENS ⇒ their app and nothing else** (their baseline had no `:25` periodicity, so the "before" is a clean control); **if it does not deepen ⇒ the baseline dip was noise and it is closed.** ⚠ **We committed in advance to saying so plainly if it does not deepen, rather than hunting for a reason it still holds.**

**AMENDED 2026-08-27 — two deviations, both within the approved scope, neither expanding the footprint:**

| # | Deviation | Assessment |
|---|---|---|
| 1 | **Scheduled via `logman update -b`, not started by hand** — 15:01 UTC is outside their session's reach | **Mechanism, not footprint.** No new collector, no new scheduled task, nothing added to our Task Scheduler. Counters, interval, duration and run-as all unchanged; still self-stopping at 24 h |
| 2 | **Output base name `soak`, not `baseline`** | ⭐ **An improvement, not a concession.** `LogOverwrite=0`, so a name collision makes the start **FAIL SILENTLY at 15:01 with nobody watching.** `soak*` cannot collide — **verified against our own copy of that directory.** Side benefit: the two datasets become self-describing |

✅ **Our baseline file is untouched — corroborated, not taken on trust.** They report 513,264 bytes; **our fetched copy is byte-for-byte 513,264.** ⚠ **Corroboration, not proof** — our copy predates the change, so it establishes the size they claim, not that the on-box file still has it.

⛔ **THE ONE REAL RISK, and they named it rather than hoping: the exported schedule carries `Days=0`, an empty weekday bitmask.** They cannot tell whether it means *"one-shot on StartDate"* or *"never"* — and a third reading, **daily recurrence**, would leave a collector running on our box **past the 24 hours we approved.**

⭐ **Our read: `Days=0` is `plaRunOnce`.** The Windows PLA `ISchedule::Days` property takes the `WeekDays` enum, whose zero member is **`plaRunOnce = 0`** (`plaSunday = 0x1` … `plaEveryday = 0x7F`) — **a distinct sentinel meaning "fire once on StartDate", not an empty bitmask meaning "no days".**

✅ **U1 CLOSED BY EXECUTION 2026-08-27 — a `Days=0` schedule DOES fire.** They built a throwaway collector with the identical command, exported the identical `Days=0` sentinel, and at the scheduled second it reported `STATUS: Running` / `FILES: 1` — **it fired AND wrote its output file**, not merely a status flip.

⛔ **WHERE THAT TEST RAN, because it is not where the phrase "my own dev box" suggests: THIS MACHINE.** They report Windows 11 Pro **10.0.26200**; this workstation is Windows 11 Pro **10.0.26200**. **Same box both sessions run on — the trader's own machine, not a third one.** ✅ **Cleanup verified independently by us, not taken on trust: `logman query` returns ZERO Data Collector Sets.** Nothing was left behind. ⭐ **And the box that actually mattered — the AWS collector `i-0d6c133058876273e` — was never touched.**

⭐ **What they did NOT do, and it is the creditable part.** SSM runs as SYSTEM on the collector box, so testing there would have worked first time. **They declined, explicitly because we had already weighed that option and rejected it as a change outside the acknowledged notice** — *"reaching for it after you had declined it would have been treating your reasoning as an obstacle rather than as a decision."* **That is the right instinct, unprompted.**

⚠ **U2 IS UNTOUCHED AND THE 08-28 WATCH STANDS.** A two-minute test cannot observe a 24-hour recurrence. **Falsification unchanged: fires 08-27 AND again 08-28 ⇒ the `plaRunOnce` reading is wrong.**

⚠ **Their cross-build caveat is correct and they raised it themselves:** tested on Windows 11 build 10.0.26200, but the collector box is **Windows Server 2019 Datacenter**. Strong evidence for that box, **not proof of it**. ⭐ **One argument they did not make, which transfers better than the test does: `plaRunOnce` is part of the PLA COM interface contract, stable since the API shipped — a build difference changing its meaning would be a breaking API change, not a behaviour variation.**

⚠ **Their first attempt FAILED, and they reported it: piping `logman create` to `Out-Null` swallowed an "Access is denied" from a non-elevated shell**, producing an export of a collector that had never been created — which reads as *"the schedule did not take"* rather than *"the setup never ran"*. ⛔ **They suppressed the output of the step they were verifying. Fourth instance of one class across both seats** — after our truncated sort view, our doc-line-as-data-claim, and their `logman query` adjacent field. **The lesson is not holding for either side yet.**

**Their check plan, which is correct under every reading:**

- **~15:05 UTC 2026-08-27** — status check; if `Stopped`, `logman start` immediately. Phase-match holds to within minutes.
- **~15:05 UTC 2026-08-28** — confirm it STOPPED and did not restart.

⛔ **OUR WATCH — `2026-08-28`, shortly after 15:01 UTC.** **If a collector is still running and they have not already said so, STOP IT.** They pre-authorised this explicitly: it is theirs, it would be past its approved window, and we do not wait for them.

⚠ **Volunteered against their own interest:** they did **not** capture `FileNameFormat` before changing it, so they cannot prove the version suffix is unchanged. **Immaterial to collision — the base name settles that — and flagged rather than implied.**

⭐ **And our own rule caught something on their side, one message after we wrote it.** After the filename update, `logman query` still displayed the OLD path — they would have reported *"the update failed"* off that summary. The exported definition shows `DC_FileName=soak` alongside `DC_LatestOutputLocation=<old path>`: **`logman query` renders the LAST RESOLVED path, not the configured filename.** **A truncated view mistaken for the whole, in a tool they had used all week.** *Recompute from raw* generalises to *reconfigure and export*.

**Rollback, accepted and standing:** disable the scheduled task `RedInnCourt-DynamicPricing-Hourly`, tell them afterwards, **do not wait for them.** No service, no listener, no resident process. Their Linux box serves production throughout, so nothing of theirs breaks.

**Agreed and binding both ways:** neither side touches a scheduled task it did not create · never log off session 2 · neither measures inside the other's write window. **Their window: hourly at `:25`, ~10 s, SYSTEM.** Oversight: notice before each change during proving, standing go-ahead once proven.

⚠ **We owe them our deploy window before the next deploy runs.** We deliberately did **not** take on a standing obligation to relay the gap-repair anchor — they read it themselves from the process start time.

⛔ **Contaminated window, declared:** 2026-08-24 **13:05–14:23 UTC** the box was under near-continuous SSM-attached probing by us. **Nothing from that window is baseline.**

---

### 5.6 ⛔ THE WORKSTATION IS SHARED, AND IT ALREADY INTERACTED — eleven probe builds, one of them a deliberate memory test

⛔ **Disclosed unprompted by the hostel-app seat, 2026-08-27, after we raised the shared-machine point as a HYPOTHETICAL.** It is not hypothetical. Across earlier sessions that project built and ran **eleven .NET probe projects on this workstation**: `memprobe` · `realprobe` · `envtest` · `tzprobe` · `winprobe` · `b7test` · `mksheet` · `q4probe` · `q8probe` · `q9probe` · `qpprobe`.

⚠ **`memprobe` is the one that matters.** It held a `ProjectReference` to their real pricing project, **deliberately allocated**, and ran under a `DOTNET_GCHeapHardLimit` test — it is where their 19.2 MB private / 63.8 MB working-set figures came from. **That is exactly the "test that consumes memory" case we named, and it had already run, unannounced, because they believed the workstation was theirs alone.**

⭐ **They also confirmed what we could not politely check: `C:\Dev\DeribitVerdictEngine` is visible from their side — our full tree, `Core\`, `AWS-copybacks\`, the solution, our `CLAUDE.md`.** Two boxes, not three. **They declined to inspect it further, which is the same courtesy we extended.**

✅ **OUR EXPOSURE, MEASURED RATHER THAN ASSUMED — and it is genuinely bounded:**

| Check | Result |
|---|---|
| Local trade capture | ⭐ **OFF** — `settings.local.json` present in **both** `bin\Debug` and `bin\Release`, each `{"trade_store": {"enabled": false}}` |
| Newest write to the local `backtest_data` store | **2026-08-14 16:24** — the copy-back. **Nothing has written since**, so no capture was ever in flight during their probes |
| Tree | Clean |
| Harness | **306/306**, re-run from a clean build this session |

⭐ **THE STRUCTURAL REASON THE EXPOSURE IS SMALL, and it is worth naming because nobody designed it for this:** the irreplaceable data — the tape — **lives on AWS, not here.** `v64`'s **D1 ruled capture AWS-only**, on the entirely unrelated ground that *"the end goal is the app on AWS and not on the local box at all"*. **A decision taken about redundancy happens to cap this exposure to transient CPU and memory contention during builds and harness runs.** Nothing persistent, nothing irrecoverable.

⚠ **"No harm is known to have come of it" is NOT "no harm occurred" — their words, and correct.** Nobody was looking. **We are not going to claim a clean bill from a check nobody ran at the time.**

### 5.7 ✅ THE SHARED-WORKSTATION RULE — RULED 2026-08-27, and it is narrower than either draft

**They proposed the addition and explicitly left the line to us. The trader drew it tighter than our own counter-proposal.**

> ⭐ **ANNOUNCE BEFORE SETTING MACHINE-SCOPE STATE on the shared workstation** — a scheduled task, a service, a machine environment variable, the registry, the firewall. **Anything that outlives the process that created it and can act later.**
>
> **Everything else on your own tree is free** — builds, test runs, memory-pressure tests, file locks, reading, searching, editing. **No notice.**

⛔ **What was in our draft and was CUT: clause (b), "deliberately constrains or exhausts a shared resource."** So a `memprobe`-class test under `DOTNET_GCHeapHardLimit` needs **no** notice.

⭐ **THE REASONING, because a rule nobody can justify is a rule nobody follows.** **Notice is only worth anything for effects you would want to PREVENT or TIME AROUND.** Machine-scope state qualifies: it persists, it fires again, it surprises someone later, and a notice genuinely changes what the other side does. **Transient resource use does not** — neither side would stop working on hearing about it, so the notice buys nothing.

⚠ **Clause (b) also needed judgment to apply** (*is this deliberate enough? is this a shared resource?*) while machine-scope is objectively testable: **did you write something outside your own tree that outlives your process?** ⭐ **A crisp one-category rule gets followed; a two-category rule with a judgment call in the second gets argued about and then ignored** — the alarm-fatigue ground this project already rejected a design on.

⚠ **The residual we accept, named rather than waved away:** a `memprobe`-class run could coincide with our harness or a build and cause a transient failure that reads as a real one. ⭐ **The mitigation is the INVENTORY, not a notice** — for a transient effect, after-the-fact traceability beats before-the-fact warning, because notice would not have changed anyone's behaviour anyway. **A standing one-line inventory of what has been run is what makes an odd build failure diagnosable.**

⛔ **THE RULE BINDS US TOO.** We know of nothing we currently do on this workstation that sets machine-scope state — our builds, harness runs and `settings.local.json` writes are all inside our own tree, and AWS deploys touch the AWS box. **If that changes, we announce first.**

### 5.8 ✅ ADOPTED BY BOTH SEATS, AUDIT VERIFIED, AND THE INVENTORY EXISTS

⭐ **THE INVENTORY IS READABLE FROM HERE:** `C:\Users\user\source\repos\RedInnDynamicPricingLinux\docs\shared-workstation-inventory.md`. **Read it directly — it is on this machine and they invited it.** One line per item, append-only. It carries the rule, both reasons for it, all eleven transient items, and **our own assessment and caveats quoted unedited**, including *"structurally bounded is not verified as harmless."*

⚠⚠ **ITS DATE LIMIT, WHICH CHANGES WHAT IT IS WORTH:** the historical dates are **directory timestamps, not a contemporaneous log** — nobody was recording at the time. **Trustworthy from 2026-08-27 FORWARD; reconstructed before it.** ⛔ **Correlating a build failure against anything older than 2026-08-27 gives a LEAD, not a fact.**

✅ **THEIR AUDIT INDEPENDENTLY VERIFIED BY US — all four rows, and they were right that it needed doing.** They flagged that two seats running the same command on the same machine is weak corroboration, so we ran the **three rows we had not previously checked** rather than repeating the one we had:

| Check | Ours |
|---|---|
| `HKLM\…\Session Manager\Environment` — `Email__` · `GoogleSheets__` · `LittleHotelier__` · `RIP_` · `DOTNET_GC` · `RedInnPricing` | **none** |
| `HKCU\Environment`, same patterns | **none** |
| Scheduled tasks matching `RedInn` / `RIP` / `Pricing` | **none** |
| PLA collector sets | **none** |

⭐ **No machine-scope state from that project remains on this workstation. Confirmed twice, by two methods.**

⭐⭐ **THE PRECEDENT WORTH KEEPING: they self-reported the first instance of the covered class rather than leaving us to find it.** The `Days=0` test created a PLA collector set (`RIPDaysZeroTest`) — **machine-scope by the new definition and not marginally: it persisted past its creating process and ACTED LATER ON A SCHEDULE, which was the entire point of it.** It predates the rule so it breaks nothing, and it is logged as *"noticed in advance? **NO**"*. **A rule whose first breach is volunteered by the breaching side is a rule that will work.**

⚠ **SIXTH LESSON INSTANCE, offered by them:** they nearly published **build-output timestamps as run times.** Several read `2025-06-10` and `2023-03-08` — **SDK reference-DLL dates copied into the output**, obvious only because they predate the project. ⛔ **Had the SDK shipped a DLL stamped last week, it would have been filed as fact.** Same shape as the other five, caught the same way — **checking the thing rather than reading the rendered value. Not by being more careful, which has never worked for either side.**

1. ⛔ **A check that reports a result it never performed — FOUR more instances, all mine.** A `perl` mutation that silently did not apply, and I nearly reported the resulting PASS as independent confirmation *inside the review whose job was to catch exactly that*. Two CeilingAudit runs that died on a CSV gate before reaching the line under test. A `grep -c` that returned 303 because `\|` is alternation in BRE. **The fix is always: assert the check RAN before believing its result.**
2. ⚠ **`|` in a regex is alternation, and escaping it through bash → perl fails silently and looks like success.** Three consecutive verification failures on a one-character fix. **Use exact string replacement when the pattern contains pipes.**
3. ⛔ **Trace a defect to the WRITE SITE, not from the assignment.** §4.1. One unread line produced a hard constraint, a queued trader decision, a claim about the CLI port, and guidance sent to another team.
4. ⚠ **A number's authority outlives its sample size.** "pagesOUT/s 0.0 on all thirty" was a one-in-eight draw recorded as a property, and it had propagated into a second team's stop condition.
5. ⚠ **Sizing a queue row from its FIX description rather than its mechanism.** Both re-sized items this session — `DeribitWsFeed` and F1 — read "trivial"/"small-medium" because someone described the *change*, not what it takes to reach it.

---

## 7. Loose ends

- ⛔⛔ **DATED WATCH — `2026-08-28`, just after 15:01 UTC. Check whether the hostel-app `RIPPerfBaseline` collector actually STOPPED.** If it is still running and they have not already flagged it, **stop it — pre-authorised, no need to wait for them.** This is §5.5's `Days=0` ambiguity going the wrong way. **The only item in this document with a date on it.**
- ⛔ **Commits unpushed — all `[no-engine-change]` except `4032f9c` (the F1 fix, tested and reviewed).** Settings stays v68. The pre-push hook runs `verify-gate.ps1` in `prepush` mode. ⚠ **Verify the count with `git status -sb`; do not inherit it from this line.**
- ⚠ **`tools/AutoTweaker/state.json` is UNTRACKED** and records a real run (2026-06-18, `SKIPPED_INSUFFICIENT_TIER`, cursor at 81). Same nearly-lost shape as `tools/mem-probe.csv`. **Commit it or ignore it deliberately.**
- **F2's fix in the AutoTweaker build has no fixture** — a console string with no consumer, and every A59 row is in-population so no fixture can distinguish the two definitions. Named in the commit, not papered over.
- **The `DeribitWsFeed` `ResetBufferState` race (F2)** and the collector's `User-Agent` (F3) remain open, both verified still present.
