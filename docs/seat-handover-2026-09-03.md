# Seat handover — 2026-09-03 (UTC)

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-08-29.md`](seat-handover-2026-08-29.md)** — superseded for STATE. Its detail on the collector, the hostel-app thread and its lessons still binds.

**Settings: v68**, unchanged all session. ⛔ **Run `git status -sb` — never inherit a push state from this line.**

⚠ **ALL DATES HERE ARE UTC. The workstation is GMT+8.** This session spanned that boundary — it was 09-04 locally while 09-03 UTC. **Check which before calling a sequence impossible.**

---

## 0. ⭐ FIRST TASK — write two specs, and they are NOT symmetric

| Pick | Item | What it needs | Model / effort |
|---|---|---|---|
| ⭐ **1st** | **Item 8 — A54a JSON↔POCO reflection drift guard** | ⭐ **Transcription.** The ruling is COMPLETE — [`trader-tick-queue.md`](trader-tick-queue.md) §0a, option **(d) + scoped (b)**, ruled 2026-08-11: a reflection walk comparing `New EngineSettings()` against the deserialised shipped `settings.json`. **~40 lines against the `A52a` template**, no parameter mapping, no fifth copy. **The reviewing seat PROTOTYPED it — found all four session-bucket drifts and both known drifts, zero false positives, zero orphans.** Its escalation trigger is written too | **Sonnet, medium** |
| **2nd** | **Item 6 — `WsTradeProbe` through the shared trade reader (S-1)** | ⛔ **NOT transcription. Read the code FIRST.** Nobody has checked what "the shared trade reader" resolves to today, or whether [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md)'s S-1 finding still holds | **Sonnet, medium** |

⛔ **Do not batch these into one task.** ⚠ **Three specs written this session carried a premise the author had not checked, and the implementer caught all three. Item 6 is exactly that shape.**

**Neither is dated. Nothing is blocked. Nothing is owed by the trader.**

---

## 1. ⛔ THE TWO DATED THINGS

### 1.1 Kelly read — due ~2026-09-07/09-08, and it has a TRAP

⛔⛔ **THE COLLECTOR'S BOOK ROTATED 2026-09-01 15:49 UTC. A ONE-FILE COUNT UNDER-REPORTS BY 90 %.**

| File on `i-0d6c133058876273e` | weekday STRONG |
|---|---:|
| `C:\DeribitEngine\analysis_log.csv` (live) | **39** |
| `C:\DeribitEngine\analysis_log.csv.v0.7.bak` | **337** |
| **TRUE TOTAL** | **376** against **≥406** — shortfall **30** |

**Measured 2026-09-03 17:54 UTC.** The two files do **not** overlap — the rotation split 09-01 cleanly at 15:49/15:50. ⭐ **The 337 was cross-validated against the local copy-back: independent read, same number.**

⚠ **376 is a MEASUREMENT AT A MOMENT, not a standing figure. Re-run the two-file count; never quote this one as current.** Recent weekdays: 08-28 = 23 · 08-31 = 4 · 09-01 = 11 · 09-02 = 14 · **09-03 = 21 and still running when measured.** At ~14/weekday it clears 406 around **09-07 to 09-08**. **W6-4 waits with it.**

### 1.2 Absorption D-2 / §4.1 read — ~2026-09-15

**D-1's instrumentation shipped 2026-09-01, and D-1 ruled ~2 weekday-weeks before reading.** See §2.1 — **the first reading already points against §4.1's premise.**

---

## 2. ⭐ THE ABSORPTION ARC — the biggest thing that moved

**The D-table in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 was ticked 2026-09-01.**

| Row | State |
|---|---|
| **D-1…D-5** | ✅ **TICKED** — the trader's own definition: *"ticked means follow as recommended"* |
| **D-6a** | ✅ **RULED — the 0.30 / 0.10 pair is INTENDED.** Arm-early / measure-tight: the episode arms on proximity (`LevelAbsorptionTracker.vb:251`) but `SizeStart` samples the band (`:295`), capturing depth **before price arrives**. Write-up is that proposal's **§4.3a** |
| **D-6b** | ✅ **RULED — the "largest single leak" claim is WITHDRAWN.** The annulus presses at 2.6–2.8 % on both books |
| **D-6c** | ⛔ **OPEN until the §5 read (~09-15)** — it needs `SizeStart` / `SizeMin` logged, which now happens |
| **D-6d** | ⚠ **RAISED, NOT RULED.** §4.3 box (b)'s 31 % counting gap. **Opus, high, separate session** |

### 2.1 ⚠⚠ The instrumentation's FIRST reading points AGAINST §4.1

```
AbsorptionEpisodeSec   n=289   min=0   median=1.7 s   max=135.2 s
```

⛔ **§4.1's premise is that 10 seconds is too SHORT. At a 1.7 s median, episode-cumulative accumulation gives a SHORTER span than the 10-second rolling window** — so §4.1 as designed would **shrink** the numerator, not extend it. ⚠ **And the true episodes are shorter still: the reading is length-biased twice** — a poll is likelier to land inside a long episode, and an episode living entirely between two polls is never seen at all.

⛔ **PRELIMINARY — 2 weekdays of a ruled ~10. Do NOT tick on it.** ⭐ **What it changes about the 09-15 read: the first question is no longer "how much does §4.1 help", it is "does §4.1's premise survive."**

### 2.2 ⭐ The re-map churn hypothesis is FALSIFIED — and by free data

**I proposed logging open/close counters. Before speccing them I tested whether already-logged columns answered the question. They did, and they killed my own hypothesis:**

| Previous row | n | median `EpisodeSec` |
|---|---:|---:|
| same `AbsorptionLevel` | 28 | **3.7 s** |
| different `AbsorptionLevel` | 35 | **4.1 s** |
| no episode at all | 226 | 1.3 s |

⛔ **Re-map does not explain it — same-level and different-level are indistinguishable.** ⭐⭐ **The decisive number: rows arrive ~97 s apart, so a SAME-LEVEL episode observed at 3.7 s old is a NEW episode. It died and was reborn with the level never moving.**

⭐ **The leading candidate is now `Not gateOpen` — price leaving and re-entering the proximity shell. Proximity-boundary chatter, which was on nobody's list.** ⚠ **It reframes D-6a without overturning it:** the arm-early design assumes ONE arming per approach and may be getting many, each discarding `SizeStart`, `SizeMin`, `PullLB`, `PostLB` and `PressSum`.

⭐ **The counters were NOT built.** They would have measured the falsified hypothesis. **Any future counter must split closes BY CAUSE, and gate-close is the one to watch.**

---

## 3. The collector — deployed, healthy, and its anchor MOVED

| | |
|---|---|
| Deployed | **2026-09-01 15:49:02 UTC**, commit `49d0098`, via `tools/ops/collector.ps1 deploy` |
| Process | **PID 4728**, one life since the deploy |
| ⛔ **Gap-repair anchor** | **03:49:02 · 09:49:02 · 15:49:02 · 21:49:02 UTC** — ⚠ **MOVED by the deploy. Every pre-09-01 anchor time is stale.** Re-read live: `(Get-Process DeribitVerdictEngine).StartTime.ToUniversalTime()`, then every 6 h |
| Schema | **116 columns**, the five new ones appended after `SignalId` |
| Cadence | **37.0–37.2 rows/hour** against a 37.0 baseline — healthy |
| Rotation | `analysis_log.csv.v0.7.bak`, 31,234,053 B — **9,229 B larger than the pre-deploy copy-back, exactly the rows between fetch and restart.** ⚠ **No timestamp in the name** (none pre-existed); it is a queued decision — **do not "tidy" it** |

⚠ **The `.bak` is a single copy on the box.** The 2026-09-01 copy-back covers everything to 15:38; **the last ~11 minutes live only there. Fold it into the next fetch.**

⭐ **`coverage` can now be aimed at a copy-back** — `--evidence-dir`, shipped this session. **Use it instead of hand-computing**, which is what item 21 existed to fix.

---

## 4. What shipped this session

| Item | Commit |
|---|---|
| Absorption instrumentation — 5 CSV columns, fixtures A60a–e | `38c1e56` |
| Queue **21** — `coverage --evidence-dir` / `--store-dir`, plus R8's every-run banner | `7794320` |
| Queue **17 + 18** — MECHANISM literals; per-span trailing gap | `5cd7269` |
| **R7** — trailing gap is the MAX across an hour's trailing spans, + A61f | `6a6f93e` |
| **R8** — named failure when the combine selects a class no span carries | `57b7a79` |

**Harness: 317 PASS, 0 FAIL. Gate PASSED. Settings stayed v68 — no engine-path change in 17 / 18 / 21.**

---

## 5. ⚠ Lessons — and it is the same pattern this project keeps finding

⛔⛔ **NOT ONE of this session's errors was caught by care. Every one was caught by a CONTRADICTION, or by a check built so it could fail.**

1. ⛔ **THREE of my specs asserted behaviour I had not read the code for, and the implementer caught all three** — acceptance item 4 named the wrong gate (`AbsorptionSignal` where it is `HasEpisode`); A60c's trap described a failure mode that cannot occur; the discriminating-row condition omitted its numerator terms, twice. ⭐ **The pattern: I reached for the book when the answer was a code path — which is exactly what my own spec-back criticised the proposal's author for doing.**
2. ⭐ **Checking the FREE instrument before speccing a paid one killed a hypothesis and saved a rotation, a restart and two weeks of window.** The counters would have measured the wrong thing.
3. ⚠ **The count-a-name trap, twice in one hour** — `grep -c BacktestProgram` returned 3, from comments saying it stays OUT; `grep -c FirstOrDefault` returned 1, from a comment saying it was deliberately not used. **Assert the declaration, not the name.**
4. ⚠ **A `sed` splice LOOKED like it left a duplicated `End If`. The build said 0 errors.** **"Fixing" the apparent duplicate would have broken a working file. Check before correcting.**
5. ⚠ **The queue banner and a box I added below it carried two different Kelly numbers for a day** — in the document that IS the state read, with the stale one first. **Correct the line; do not add a box beside it.**

---

## 6. Loose ends

- ⚠ **`OutOfScopeWeekend` / `spans.First`.** R8 added a named tripwire. ⭐ **The architecturally better fix — invert the derivation so the class is impossible by construction — was REJECTED on risk and is recorded in [`coverage-trailing-split-span-spec.md`](coverage-trailing-split-span-spec.md) §8 as the right shape if that combine is ever rewritten anyway.**
- **Hostel app:** three deploys plus `Heartbeat__Url`, a machine env var, properly noticed under the shared-workstation rule. ⭐ **Their dead-man's switch is now the first automated liveness monitor on the collector box — but it watches THEIR app, so it would NOT fire if our collector died while the box stayed up.** Ask to be looped in when it fires.
- **`A6`'s `trendGate:=10.0` is still stale** — same class as item 17, and it needs its own MECHANISM-or-SHIPPED ruling before anyone touches it.
- **`_evalCache` unbounded** — no spec, destructive obvious fix. ⭐ **RE-GROUND IT ON THE BOX, not the port:** the CLI-port reversal was cancelled 2026-09-03 (O3 stays DEFERRED LAST, possibly indefinitely), so *"the port carries it across unchanged"* is no longer the argument. **~150 MB free on a 1 GB t2.micro is.**
