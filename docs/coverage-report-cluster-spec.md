# Coverage-report cluster — three defects, one spec

**Status:** ✅ **BUILD-AUTHORIZED for `C-1` and `C-2`.** ⛔ **`C-3a` needs ONE trader tick (`D-6`); `C-3b` is NOT BUILDABLE and is specced as a prerequisite, not a build.**

**Author seat:** Opus, 2026-09-11 (UTC). **Baseline commit: `4f95ac3`.** ⛔ **Every line number here was read at that commit. Re-read if `HEAD` has moved.**

**Origin:** three rows in [`trader-tick-queue.md`](trader-tick-queue.md) §2, all three *"needs a short spec"*, all three in `tools/BacktestRunner/CoverageReport.vb`. **Specced together because two of them change the same function and the third shares its shape.**

---

## 0. Model and effort

> ### Model: **Sonnet** · Effort: **MEDIUM**
> ### Two sessions. `C-1` + `C-2` in session 1; `C-3a` in session 2 once `D-6` is ticked.

**Why MEDIUM.** The judgment is done here and every measurement is already taken on real tape. **What lifts it above LOW is that `C-1` and `C-2` both modify the classifier's decision path, and each has a documented way of trading one wrong answer for a different wrong answer.** The queue rows say so in their own words, and both warnings are reproduced below.

**Why not HIGH.** No derivation is delegated. **The implementer confirms measurements and builds to a settled design.**

### 0.1 ⛔ Where Sonnet will specifically slip

| # | Trap | Why it bites here |
|---|---|---|
| **T-1** | ⛔⛔ **THE MIXED-ERA SPAN** | `C-1` reads `trade_seq`. The store has **three eras** — identity-less, identified-but-incomplete, identified-and-complete. **A span can hold BOTH kinds of row.** ⭐ **The repo's own memory says this is the default failure: *"a migration creates a MIXED population, and both the code and its fixtures default to testing the two PURE states."*** **Test the mixed one** |
| **T-2** | ⛔ **Seeding from `OK` instead** | The obvious fix for `C-2`. ⚠ **It SHRINKS the up-interval, so hours before the first `OK` fall to `before-first` ⇒ `ExpectedMissing` — trading a false defect for a possibly MISSED one.** The queue row flags this explicitly. **Do not do it** |
| **T-3** | ⛔ **A `trade_seq` gap across a LEGACY row is meaningless** | You cannot tell a dropped row from a row that never carried a sequence. **This is why `D-2` is (a) and not (b)** — it is a mechanism argument, not a simplicity one |
| **T-4** | ⚠ **Worst-of precedence** | `ClassifyHour` combines split spans worst-of at `CoverageReport.vb:695-707`. **A new class must be placed in that chain deliberately.** The enum comment says ordinal position is inert — **that is true of the ENUM and false of the COMBINE** |
| **T-5** | ⛔ **The clock** | GMT+8 workstation, UTC project dates. **Run `date -u`.** Eight consecutive sessions |

### 0.2 ⛔ Escalation trigger — stop and ask

- **Any change that makes an hour classify `Captured` where it previously classified `Defect` on tape you have NOT proved complete.** Both fixes must only remove FALSE defects.
- **`C-2`: any hour that moves from `Defect` to `ExpectedMissing` rather than to the new startup class.** That is `T-2` firing.
- **Any need to add a `settings.json` key.** That is a reserved class — see `D-6`.

---

## 1. The three defects, with the measurements already taken

| # | Defect | Measured severity |
|---|---|---|
| **`C-1`** | **`gapMs` is a TIME tolerance standing in for a COMPLETENESS check** — `CoverageReport.vb:569`, `storeClean = stats.RowCount > 0 AndAlso stats.LongestGapMs <= gapMs` | ⭐ **ONE false defect in 63.1 h.** Over tape `trade_seq` proves **100.000 %** complete (134,204 rows, ZERO missing), exactly one gap exceeded 300,000 ms — **302,145 ms at 2026-08-13 21:46:50 UTC, beating the threshold by 2.1 s.** ✅ **Confirmed by the report itself**: hour `2026-08-13 21:00` classified `Defect`, reason `gap-breach(302145ms)` — the exact figure predicted before the run |
| **`C-2`** | **An up-interval starts at the `DOWN` line, so a CONNECT WINDOW reads as capture time** — `BuildUpIntervals`, `CoverageReport.vb:332` | ✅ **Confirmed instance:** hour `2026-08-10 09:00` → `Defect`, reason *split@09:59 — first span `Captured`, second span `Defect(empty)`*. **That second span is 3.4 s long** (marker 09:59:56.601 → hour end) and `ws_health` shows the process connected at **10:00:03.328**. **Zero rows was correct behaviour. 1 of the 4 defects in that window** |
| **`C-3`** | **Intentional downtime and venue outage both resolve to `defect`** | ✅ **Weekends already handled** — `CoverageReport.vb:629-630` classifies Sat/Sun `OutOfScopeWeekend` ahead of every other test. ⚠ **The queue row cites `430-431`; that is WRONG — line 430 is an unrelated `StoreEndMs` doc comment. Corrected here by running it, 2026-09-11**. **The gap is WEEKDAY downtime**, and a venue outage: Deribit `system_maintenance` code 11051, HTTP 503, 2026-08-11 — box up and healthy, store empty, reports `defect` |

### 1.1 ⭐⭐ The finding that changes `C-3`'s shape — the venue arm is NOT BUILDABLE

⛔ **The queue row asserts a venue-maintenance hour is *"detectable"* from `ws_health.log`. It is not, today.** ✅ **Verified in the tree 2026-09-11 (UTC), zero hits:**

- `grep -rn '11051\|system_maintenance' --include=*.vb .` → **nothing.**
- `grep -rn '503\|ServiceUnavailable\|Maintenance' --include=*.vb Core/ *.vb` → **nothing.**
- **`ws_health.log`'s vocabulary is `OK` / `DEGRADED` / `DOWN`, and all three describe OUR SOCKET, not the venue's status.**

⛔ **So `C-3b` cannot be built as written — there is no recorded source to scope from.** **It needs an instrument FIRST: record the venue's maintenance response when it occurs.** ⚠ **That instrument is its own build and is NOT in this spec.** ⭐ **Naming this is the point: a scope source that does not exist cannot be consumed, and specifying the consumer first would produce a build that silently never fires.**

---

## 2. Design

### 2.1 `C-1` — `trade_seq` completeness as a SECOND signal

**`storeClean` gains a prior arm.** For a span whose rows ALL carry `trade_seq`, completeness is measured directly: the sequence is contiguous or it is not. **A contiguous span is `Captured` regardless of `LongestGapMs`.** For any other span, the existing time tolerance is unchanged.

⛔ **It is a SECOND signal, never a replacement** — the queue row rules this and the reason is the mixed store: **legacy hours have no sequence and still need the time heuristic.**

### 2.2 `C-2` — two timestamps, not a moved one

**`UpInterval` gains `CaptureCapableFromMs` alongside the existing `FirstUtcMs`.**

- **`FirstUtcMs` is UNCHANGED** — it still seeds from the earliest evidence, so interval membership and the `before-first` test behave exactly as today. ⭐ **This is what defeats `T-2`.**
- **`CaptureCapableFromMs` is the earliest evidence that proves the process could CAPTURE**, not merely that it existed.
- **A span lying entirely before `CaptureCapableFromMs` within an up-interval is a STARTUP WINDOW, not a defect.**

⛔ **This requires `EvidencePoint` to carry its KIND, which it does not today** (`CoverageReport.vb:98-105` holds only `UtcMs` and `InstanceId`).

⚠⚠ **`ParseWsHealthEvidence`'s doc comment currently states the defect as a design decision, and BOTH halves of it must survive the edit:**

> *"The STATE value is deliberately IGNORED — even a DOWN line proves the app was alive to write it (`WsHealthLog.LogStart` fires before the socket connects), so every line is equally valid 'app was up at this instant' evidence regardless of state."*

⭐ **That reasoning is CORRECT for interval MEMBERSHIP and WRONG for capture capability.** **Keep the sentence, scope it to `FirstUtcMs`, and say plainly that `CaptureCapableFromMs` is the reason the state is no longer ignored everywhere.**

### 2.3 `C-3a` — a declared operating schedule

⭐ **The distinction that is the whole of this item, and it is already ruled:** [`j-b-scoping-ruling-2026-08-02.md`](j-b-scoping-ruling-2026-08-02.md) **rejected an expected-uptime BASELINE, and that rejection still binds** — a baseline built from observed behaviour cannot, even in principle, flag that behaviour as wrong. ⛔ **But a DECLARED OPERATING SCHEDULE is not a baseline. It is a positive record of intent, which is exactly what J-B asks for.**

⚠ **Prefer a declared schedule over a shutdown marker: an EC2 stop can kill the process without running any shutdown code, so a shutdown marker is unreliable by construction.**

---

## 3. ⭐⭐ D-table — the FIRST spec under the auto-proceed ruling

**`CLAUDE.md`'s *"AUTO-PROCEED ON YOUR OWN RECOMMENDATION"* (RULED 2026-09-11) applies.** ✅ **Reserved-class test run against this cluster, and it clears on every axis except one:**

| Reserved class | This cluster |
|---|---|
| Affects scoring | ✅ **No** — `CoverageReport` is a read-only diagnostic; it does not reach `ScoringEngine` |
| Moves a rendered value | ✅ **No** — verified: `grep -c 'CoverageReport' UI/MainForm_Layout.vb` → **0**. The TAPE STORE strip does NOT use it. Consumers are `BacktestProgram.vb` (console + markdown) and the harness |
| Writes the live collector or store | ✅ **No** — read-only |
| Schema or CSV-header change | ✅ **No** for `C-1`/`C-2` — `EvidencePoint` and `UpInterval` are in-memory types |
| `settings.json` change | ✅ **No** for `C-1`/`C-2` — `gapMs` is a CLI option defaulting to `300000L` at `BacktestProgram.vb:79`; `settings.json` holds **zero** `gap_ms` keys. ⛔ **`D-6` may breach this — reserved** |
| My pick is the cheaper / less-information option | ⛔ **FIRES ON `D-4` — see it** |

### Decisions TAKEN under the ruling — recorded, not asked

| # | Decision | Options | ✅ TAKEN, and why |
|---|---|---|---|
| **`D-1`** | Does `trade_seq` replace the time tolerance? | (a) replace · **(b) second signal** | ✅ **(b).** The queue row already rules it and the mixed store forces it — legacy hours have no sequence |
| **`D-2`** | A span with SOME rows carrying `trade_seq` and some not | (a) any missing sequence ⇒ fall back wholly to the time tolerance · (b) evaluate sequence over the rows that have it | ✅ **(a) — and this is a MECHANISM argument, not a simplicity one.** ⛔ **A sequence gap across a legacy row is UNINTERPRETABLE: you cannot distinguish a dropped row from a row that never carried a sequence.** (b) would compute a number that means nothing |
| **`D-3`** | `C-2`'s shape | (a) move `FirstUtcMs` to the first capture-capable evidence · **(b) add a second timestamp** | ✅ **(b).** (a) is `T-2` — it shrinks the interval and trades a false defect for a possibly missed one |
| **`D-5`** | Build `C-3b` (venue outage) in this spec? | (a) yes · **(b) no — specify the missing instrument instead** | ✅ **(b).** §1.1 of this document proves there is no recorded source. **A consumer built against absent data would silently never fire** |

### ⛔ Decisions RESERVED — these come to the trader

| # | Decision | Options | My read |
|---|---|---|---|
| **`D-4`** | What class does a startup window get? | (a) reuse `ExpectedMissing` · **(b) a NEW `HourClass.StartupWindow`** | ⚠⚠ **RESERVED BY THE RULING'S OWN LAST CLASS, and I am flagging it rather than taking it.** My first instinct was **(a)** — fewer classes, no precedence work. ⛔ **That is the CHEAPER, LESS-INFORMATION option, which is the exact class the ruling reserves**, and it conflates *"before we ever ran"* with *"during startup"*. ⭐ **So my considered read is (b)** — but the ruling says a pick arrived at this way goes to you. ⚠ **If (b): state the worst-of precedence explicitly (`T-4`); it must NOT outrank `Defect`** |
| **`D-6`** | Where does the declared operating schedule live? | (a) a `settings.json` block · (b) a separate declared file read by `BacktestRunner` · (c) a CLI option | ⛔ **RESERVED — (a) is a `settings.json` change, a reserved class in its own right.** **My read: (b)** — it is operational intent, not engine configuration, and it keeps the tweaker fence out of it. ⚠ **`C-3a` does not start until this is ticked** |

---

## 4. Build list — the single authoritative one

### Session 1 — `C-1` + `C-2`

1. **`EvidencePoint` gains a kind** sufficient to distinguish capture-capable evidence from mere liveness. `ParseWsHealthEvidence` stops discarding the state; `ParseAnalysisLogEvidence`'s rows are capture-capable by construction (a completed analysis run).
2. **`UpInterval` gains `CaptureCapableFromMs`.** `FirstUtcMs` is untouched.
3. **`ClassifySpan` gains the startup-window arm**, before the `Defect` return at `CoverageReport.vb:594`.
4. **`ClassifySpan`'s `storeClean` gains the sequence arm** per `D-1`/`D-2`.
5. **Update the two doc comments that now misstate the design** — `ParseWsHealthEvidence`'s *"deliberately IGNORED"* paragraph per §2.2, and `ClassifySpan`'s summary.
6. **Worst-of precedence** at `CoverageReport.vb:695-707` updated per `D-4` once ticked.

### Session 2 — `C-3a`, only after `D-6` is ticked

7. The declared-schedule source, and an `OutOfScopeDeclared` arm ahead of the uptime tests, mirroring how `OutOfScopeWeekend` sits at `CoverageReport.vb:629-630` (inside `ClassifyHour`, ahead of every other test).

### Not in this spec

- ⛔ **`C-3b` venue-outage scoping** — blocked on an instrument that does not exist. §1.1 of this document.
- ⛔ **Do not touch `ResolveScope` or the SH-1 split logic.** Both shipped and are covered.

---

## 5. Fixtures — family `A73`

✅ **`A73` is the next free family, verified 2026-09-11: `A72c` is the high-water mark.** ⚠ **`roadmap.md` §2 said `A59` until 2026-09-10 and was thirteen families stale — count it, do not inherit it.**

| # | Asserts | ⛔ The mutation that must fail it |
|---|---|---|
| **`A73a`** | A span with contiguous `trade_seq` and `LongestGapMs` **above** `gapMs` classifies **`Captured`** | Remove the sequence arm → reverts to `Defect` |
| **`A73b`** | ⛔⛔ **THE MIXED SPAN (`T-1`)** — some rows carry `trade_seq`, some do not; the span falls back **wholly** to the time tolerance | Evaluate the sequence over only the rows that have it → the span wrongly reads complete |
| **`A73c`** | A legacy span with **no** sequences anywhere behaves **exactly as today** | Any change → a regression in the legacy path |
| **`A73d`** | A span entirely before `CaptureCapableFromMs` inside an up-interval is a **startup window**, not `Defect` | Seed `CaptureCapableFromMs` from the `DOWN` line → `Defect` returns |
| **`A73e`** | ⛔ **`T-2` GUARD** — an hour BEFORE the first `OK` but inside the interval must **NOT** become `ExpectedMissing` | Move `FirstUtcMs` instead of adding the second timestamp → this fixture fails |

⭐ **`A73b` and `A73e` are the two with teeth. If only two are written, write those.**

---

## 6. Acceptance

| # | Check | Expected |
|---|---|---|
| `AC-1` | The two confirmed instances re-classify: `2026-08-13 21:00` and `2026-08-10 09:00` | **both leave `Defect`** |
| `AC-2` | The 63.1 h window that `trade_seq` proves 100 % complete | ⭐ **ZERO defects** |
| `AC-3` | A legacy-era run classifies **identically** to before | **byte-identical report** |
| `AC-4` | Harness | **352 → 357, ALL PASS** |
| `AC-5` | Solution Release `-t:Rebuild` | **0 errors, 0 warnings** |
| `AC-6` | `verify-gate.ps1 -Mode local-fast` | **GATE PASSED** |
| `AC-7` | `settings.json` | **untouched at v68** |

⚠ **`AC-3` is the one that catches an over-reach.** A run over identity-less tape must not move at all — **if it does, the sequence arm is firing where it has no data.**

**Tag `[no-engine-change]`** — `tools/BacktestRunner` is not an engine path and no scoring, rendered value or settings key moves. ⚠ **A `DeribitIndicatorProject.md` §15 entry is NOT owed for the same reason; say so in the commit rather than leaving it implied.**

---

## 7. What I did NOT verify

- ⚠ **I did not re-run the coverage report.** The two confirmed instances and the 63.1 h / 134,204-row / ZERO-missing figure are **carried from the queue rows**, which state they were measured on the copy-back.
- ⚠ **I did not check whether the 2026-08-13 and 2026-08-10 windows are still present** in the current copy-backs. **`AC-1` assumes they are reachable — confirm before relying on it.**
- ⚠ **I read `ClassifySpan`, `BuildUpIntervals` and both parsers. I did NOT read `ClassifyUptimeSpan` or `ResolveBoundaryUtc` in full**, and `C-2` touches the first of those indirectly.
- ⚠ **The harness delta in `AC-4` assumes five new fixtures and no others.**
