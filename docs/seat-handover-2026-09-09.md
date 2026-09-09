# Seat handover — 2026-09-09 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-09-07.md`](seat-handover-2026-09-07.md)** — superseded for STATE. Its collector detail and its §5 lessons still bind.

**Settings: v68**, unchanged all session. ⛔ **Run `git status -sb` — never inherit a push state from this line.** At close it read **ahead 9**.

---

## 0. ⛔⛔ READ THIS BEFORE YOUR FIRST REPLY — the trader's output format

⛔⛔ **THE TRADER'S REPLY-FORMAT RULES HAVE BEEN LOST ACROSS SEVERAL HANDOVERS. They asked on 2026-09-09 that this one carry them.**

**They live in `C:\Users\user\.claude\CLAUDE.md` under *"Output format — applies to every reply, in every project"*. READ THEM THERE.** Also recorded as the memory `feedback-output-format-is-a-standing-rule`, because **memory loads automatically and a handover does not** — that is why the instruction kept dying.

**The short form, so you cannot claim you did not know:**

- ⭐ **Point form and tables. Not paragraphs.** Prose blocks stay to two or three sentences. **A table whenever you compare more than two things.**
- ⛔ **NEVER a bare section number.** Write `` `docs/trader-tick-queue.md` §0a ``, never `§0a`. **Repeat the document name on later mentions in the same reply.**
- ⛔ **NEVER a bare ID.** Give the source, the kind and the meaning on first use **in every reply** — `` `D-2` (a decision row in `docs/kelly-w6-4-spec-back.md`) ``, never `D-2`.
- **Simplified technical English.** One instruction per sentence. Active voice. ~20 words. ⚠ **Sentence STRUCTURE only — keep every domain term.** Simplify the sentence, never the concept.
- **Model + effort on its own labelled line.**
- ⭐ **Separate what you verified from what you carried.**

⚠ **The trader runs three projects at once and cannot hold section numbers in their head.** A bare `§6.10` is unusable alone and dangerous when one reply cites several documents.

⛔ **Before sending: scan for `§` and for capital-letter IDs. Each must carry its document or source.**

---

## 1. ⛔ THE CLOCK TRAP FIRED IN SIX CONSECUTIVE SESSIONS

⛔⛔ **The workstation is GMT+8. The harness announces the LOCAL date. All project dates are UTC.**

**This session it fired twice more, both caught in review:**
- The `S-4` build (a queue item — the eval-cache dedup fix) dated its `docs/DeribitIndicatorProject.md` §15 entry **2026-09-09** when UTC was 2026-09-08.
- The `WD-SEMANTICS` build (a queue item — the unparsed-row counter) dated its §15 entry **2026-09-10** when UTC was 2026-09-09.

⛔ **RUN `date -u`. EVERY TIME. Knowing about the trap does not prevent it — six sessions prove that.**

---

## 2. ⭐ FIRST TASKS

| Pick | Item | Notes |
|---|---|---|
| ⭐ **1st** | **Push the 9 commits**, or confirm the trader has | ⚠ **The gate will emit a NUDGE WARNING** on `ab5600f` — engine-path change without a settings bump. **That is correct, not a failure**: a §15 entry is owed and present, so the commit deliberately omits `[no-engine-change]`. Verified in `tools/checks/verify-gate.ps1:132-143` — that branch is a `Warn`, not a `Fail` |
| **2nd** | **Nothing is owed and no build slot is open.** Read [`trader-tick-queue.md`](trader-tick-queue.md) §0a and verify before offering work | — |

⛔ **Do NOT offer any of the five recent specs as work — they are all BUILT.** Each now opens with a status block saying so. See §6 of this document.

---

## 3. What shipped — 9 commits, `settings.json` in NONE of them

| Item | Commit |
|---|---|
| Kelly trigger MET + the pooled-read spec | `226275e` |
| CAL + `W6-4` packet, three decisions ruled, ASIA claim withdrawn | `d5ce4a5` |
| Specs for `D-1` and `D-2` | `bc0693e` |
| `CeilingAudit --preflight` flag | `1e624ec` |
| `D-2` — the EST advisory re-word | `517f7b6` |
| All five shipped specs marked BUILT | `7163802` |
| `WD-SEMANTICS` spec | `113edbb` |
| Dedup-cost measurement | `f4778dd` |
| `WD-SEMANTICS` build | `ab5600f` |

**Harness 337 → 349.** `GATE PASSED` throughout. **Settings v68 untouched.**

---

## 4. ⭐⭐ THE KELLY TRIGGER IS MET — and what that did and did not settle

**MEASURED 2026-09-09 14:04 UTC on the collector: 407 weekday STRONG against ≥406.** `.bak` 337 + live 70. Calibration passed at 49 first. Spans do not overlap.

⭐ **The instrument is COMMITTED — stop rebuilding it in a scratchpad.** `tools/ops/kelly-trigger-read.ps1`, `-Mode Box`. It **enforces** the calibration and refuses to read the box if it fails.

### 4.1 What the read produced

- **Kelly CAL: the tier ladder still does NOT separate.** Pooled STRONG **47.1 %** (n=518) against MEDIUM 42.5 % and WEAK 42.4 %, CIs overlapping. Pooled STRONG stays **below** the 47.76 % breakeven.
- **`W6-4` (the ceiling audit): still INCONCLUSIVE.** NY×1 ΔAUC +0.0356, CI [−0.045, +0.114]. ⭐ **The CI HALVED in width** (0.160 against 0.321). **Its standing instruction is unchanged: re-run at the next book doubling, no spend meanwhile.**
- ✅ **`D-2` shipped**: the on-screen promise *"Actual numbers after next book doubling"* is retired.

### 4.2 ⛔ TWO GATES ON DIFFERENT BASES — do not conflate them

| Gate | Basis | Threshold | State |
|---|---|---|---|
| **Kelly CAL** | 2 × **203 EVALUABLE** (not 2 × the raw 201) | **≥406** | ✅ **MET at 407** |
| **`W6-4`** | 2 × **2,712 ELIGIBLE rows** | **≥5,424 eligible** | ✅ **8,269 measured** — met on this book |

⚠ **"Eligible" is NOT a raw row count.** It is the v0.8 evaluable directional weekday population after `CsvFeatureBuilder`'s filters. **An orchestrator compared raw rows, called the gate "comfortably met", and was wrong.**

---

## 5. ⛔ The frozen pooled book — do not lose it

**`AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv`** · 47,682 rows · MD5 `E8418846838FF97F3C90F782A95B3523` · **gitignored, persists on disk.**

⛔⛔ **NEITHER READER DISCOVERS A `.bak`.** `tools/CeilingAudit/CsvFeatureBuilder.vb` and `analysis/ForwardWindowJoiner.vb` each take **ONE path**, and nothing globs `analysis_log.csv*`. **Point either at the live `analysis_log.csv` alone and you read 70 weekday STRONG instead of 407 — a ~90 % under-count, silently.** The concat is manual, **NEW schema FIRST**, AWS-preferred dedup.

⚠ **`AWS-copybacks/local-book-rescue-2026-09-09/` holds the ONLY copy of the 2026-07-03 → 07-22 rows.** AWS did not exist before 2026-07-22. It was rescued out of untracked `bin/Debug/` where a `dotnet clean` would have destroyed it. **It is still untracked and unbacked-up.**

---

## 6. ⛔ Every recent spec is BUILT — none is available work

| Spec | Shipped |
|---|---|
| [`wd-tidy-weekday-predicate-convergence-spec.md`](wd-tidy-weekday-predicate-convergence-spec.md) | `5996f01` |
| [`s4-eval-cache-identity-proposal.md`](s4-eval-cache-identity-proposal.md) | `1aeae5a` |
| [`kelly-w6-4-pooled-read-spec.md`](kelly-w6-4-pooled-read-spec.md) | executed, packet `d5ce4a5` |
| [`ceiling-audit-preflight-flag-spec.md`](ceiling-audit-preflight-flag-spec.md) | `1e624ec` |
| [`kelly-est-advisory-reword-spec.md`](kelly-est-advisory-reword-spec.md) | `517f7b6` |
| [`wd-semantics-unparsed-counter-spec.md`](wd-semantics-unparsed-counter-spec.md) | `ab5600f` |

⚠ **All five of the first batch read "BUILD-AUTHORIZED" with no BUILT marker until 2026-09-09.** The trader caught it by asking whether one was safe to hand over. **It was not — it was already built.** Each now opens with a status block naming its commit and saying **"THIS DOCUMENT IS A RECORD, NOT AN INSTRUCTION."**

⛔ **When you write a spec, write its BUILT banner the moment it ships. Do not leave it for a sweep.**

---

## 7. ⚠ What is OPEN

- **Coverage-report cluster — 3 rows.** `gapMs` as a TIME tolerance (the third instance of that pattern) · an up-interval starting at the `DOWN` line · intentional-downtime scoping. All need short specs.
- **`ws_health.log` under-reports an outage** — it and the tape disagree by ~34 minutes.
- **Atomic writes** — 5 sites, mechanical, but includes `SettingsLoader.Save` so it wants fixtures.
- ⚠ **The 2026-09-05 DEGRADED event — STILL unchecked across FOUR handovers.** 32 minutes; nobody has verified whether it cost tape. **Verify from `trade_seq` completeness, NOT the repair log.**
- **Absorption** — date-gated, D-2 read ~2026-09-15.
- **`S-1`** — ruled (a) as direction, **NOT NOW**. **Nothing is owed on it.**
- **Fills-import** — queued as LATER. **S0 `--verify-venue`** — SUSPENDED.
- ⛔ **FIVE RIDERS that cannot travel alone** — the stale `.bak` rotation name, `.bak` in the pooled-concat rule, the `TriggerMode` column, the effective-source stamp, the `SettingsVersion` column. **They attach to the next `analysis_log.csv` header rotation, and the standing rule is NEVER FORCE ONE.**
- ⚠ **Pre-flight `P-5`'s duplicate-`(InstanceId, SignalId)` half is uncovered by ruling.** `LoadStats` has no such counter. **That scan stays manual and any spec needing it must say so.**

**Next free fixture family: `A72`.** Next free hard constraint: **HC29**.

---

## 8. ⭐ The lessons — the count is again the finding

### 8.1 ⛔⛔ A build passed five fixtures, the gate and a clean build — and would have corrupted production

**`R-4` (a review finding on the `S-4` build).** `KeyFor` chose its dedup namespace **per row** (`"ID|"` against `"TS|"`) and called the two spaces *"provably disjoint"* as though that were a feature. **It was the defect.** Measured on the box: **74,518 cached rows with ZERO identities against 6,696 log rows with ALL identities, and 3,308 already in the cache by timestamp** — re-admitted as duplicates on **every** engine start.

⭐ **Why the fixtures missed it, proven not argued: `A69a`–`A69d` each built a cache in the SAME namespace as the incoming rows. Production was NEITHER.** Under the single-set mutation, `A69e` fails **alone**. **A migration creates a MIXED population; test that, not the two pure states.**

### 8.2 ⭐⭐ A packet can be honest about its input and wrong about its output

The `W6-4` packet flagged the per-session `b` values as un-re-derived in its own "did not verify" list — **and then its headline stated a crossing as fact using the global breakeven.** ASIA's `b` is 0.78125, breakeven **56.14 %**, and its CI floor of 52 % is **below** it. **The honest caveat and the unsafe claim sat in the same document.** ⛔ **Read the "did not verify" section BEFORE the headline.**

### 8.3 ⛔ An incremental build HIDES a new warning

The `S-4` build introduced a `BC42109` and reported it as pre-existing. **The baseline builds 0/0 — proven by restoring it and rebuilding.** ⛔ **Use `-t:Rebuild` before claiming zero warnings.**

### 8.4 ⛔ Counting a NAME is not testing a property

The `D-2` spec's own acceptance criterion demanded `grep -rn "book doubling"` return zero hits. **It returned one — the new code COMMENT explaining the change.** That is the standing defect already in `CLAUDE.md`, where `grep -c "_lastTs"` printed 2 from comments about a removal. **Exclude comment lines, or assert the executable reference.**

### 8.5 ⚠ Markdown tables do not protect pipes inside code spans

A `` `|` `` inside backticks **still splits a table cell**. The `S-4` §15 entry carried five unescaped pipes and rendered as 8 cells instead of 3; the reviewer then added four more. ⛔ **Count UNESCAPED pipes — `grep -o '[^\\]|'` — and compare against a NEIGHBOURING row. A raw count blames the wrong row.**

### 8.6 ⭐ Deferring a schema fix ACCRETES cost

On `S-4` the orchestrator recommended deferring the identity key. **The trader overruled and was right.** Every day of delay writes more identity-less rows that cannot be honestly retrofitted. ⛔ **"Defer until something else forces it" is a bet, and the five parked riders prove this repo loses it.**

### 8.7 ⭐ A silent hole beats no field — the `WD-SEMANTICS` ruling

The orchestrator recommended letting an unparsed row drop silently, on the economics that a counter reading `0` is a field bought for nothing. **The trader ruled the separate counter instead, and was right: under the cheaper option a data defect vanishes with NO record on ANY surface.** ⭐ **A counter that reads 0 is the tripwire, not waste.**

### 8.8 ⚠ Measure the right denominator

The pooled-read spec warned that the minute-key dedup costs *"~0.36 %"*. A packet compared that against its **47.3 % raw-row drop** and called the assumption broken. **Different numerator AND denominator.** ✅ **Now measured properly: of 81 dropped local weekday STRONG, 76 survive via AWS's own copy (39 at the same second) and 5 are genuine losses — ~1.0 %.** ⭐ **The mechanism is NOT an outage: AWS coverage is flat at ~921 rows/day. The five are two boxes genuinely disagreeing on the same minute.** ⭐⭐ **Hard bound: restoring all five gives a best case of 47.61 % against a 47.76 % breakeven — the dedup CANNOT flip the CAL verdict.**

---

## 9. Collector health

**Read 2026-09-09 14:05 UTC.** One process. Live file `analysis_log.csv` spans `2026-09-01 15:50:01 → 2026-09-09 14:05:01`, 7,250 rows at that read. `.bak` closed at 33,911 rows. **`unparsed=0` on both files.** No redeploy this session; **settings v68 throughout.**

⚠ **Accrual is running well below the handover-2026-09-07 model of ~13–14 weekday STRONG per day: 09-07 = 13 · 09-08 = 5 · 09-09 = 3 at 14:04 UTC.** **Do not quote that model as current.**
