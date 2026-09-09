# Kelly CAL + `W6-4` re-run — the pooled-book assembly and read

✅ **BUILD-AUTHORIZED. The Kelly dated trigger is MET.** Measured 2026-09-09 14:04 UTC on the collector: **407 weekday STRONG against ≥406**.

⛔ **TWO INSTRUMENTS, ONE BOOK, ONE FREEZE. They bundle by design** — `docs/kelly-est-honesty-decision-2026-08-02.md` §2.1: *"one pooled freeze, one re-read session, both instruments. That also keeps the overfit counter honest, since both consume the same book span."* **Do not assemble the book twice.**

⛔⛔ **THE TWO INSTRUMENTS HAVE DIFFERENT GATES ON DIFFERENT BASES, AND ONLY ONE IS MET.** See §3. **Assembling the book does not authorise running `W6-4`.**

**Baseline commit: `1aeae5a`.** Line numbers read at that commit.

---

## 0. Model and effort

> ### Model: **Sonnet**
> ### Effort: **HIGH**
> ### One session. Assembly and both reads.

**Why HIGH.** The reads themselves are recipes — `BacktestRunner report --csv <pooled>` and the `CeilingAudit` runner. **The hard part is the book, and a wrong book fails silently in both directions.** A short book under-counts by ~90% with no warning; a double-counted book inflates every rate. Neither instrument validates its own input. **Everything expensive here happens before either tool starts.**

### 0.1 Where this will slip

⛔⛔ **Trap 1 — NEITHER READER DISCOVERS A `.bak`. THIS IS THE ONE THAT HAS ALREADY BITTEN ONCE.** `tools/CeilingAudit/CsvFeatureBuilder.vb:112` (`W6-4`) and `analysis/ForwardWindowJoiner.vb:108` (the Kelly ladder) **each take exactly ONE path**, and nothing in the tree globs `analysis_log.csv*`. **Point either at the live `analysis_log.csv` alone and you read 70 weekday STRONG instead of 407 — a ~90% under-count, silently.** `docs/trader-tick-queue.md` records this as a standing rider: the `.bak` inclusion is still missing from `docs/aws-collector-deploy-checklist.md` §3b step 2. **The concat is MANUAL. Nothing warns you.**

⛔ **Trap 2 — the pre-2026-07-22 rows live ONLY in an untracked build-output directory.** `bin/Debug/net8.0-windows/analysis_log.csv.v0.7.20260901_145200.bak` (**12,311 rows, 2026-07-03 15:57 → 2026-08-20 10:27**) is the sole copy. **AWS did not exist before 2026-07-22** — the earliest AWS row anywhere, on the box or in any copy-back, is `2026-07-22 16:24:54`. ⚠ **A `dotnet clean` destroys it. COPY IT OUT FIRST, before anything else in this build.**

⛔ **Trap 3 — the `W6-4` gate is NOT raw rows.** It is **2,712 ELIGIBLE rows** (`docs/kelly-est-honesty-decision-2026-08-02.md` §2.1), so the doubling gate is **≥5,424 eligible** — the v0.8 evaluable directional weekday population after `CsvFeatureBuilder`'s filters, not the line count. ⚠ **An orchestrator comparing raw rows called this gate "comfortably met" and was wrong.** It is **unmeasured** until §5 runs.

⚠ **Trap 4 — concat ORDER is settled and is not free.** **Put the NEW schema FIRST**, so the header map carries the added column names; pre-rotation short rows then return empty on those columns via the verified bounds checks. `docs/trader-tick-queue.md` records this as RULED, reversing an earlier "keep chronological" ruling.

⚠ **Trap 5 — dedup is AWS-PREFERRED, and the minute-key bound is already ruled.** Where local and AWS both hold a row, AWS wins (2026-07-31 dedup ruling). The minute-key dedup costs the Kelly read ~0.36% (1 row in 281, measured) — **known and accepted, do not re-derive it.**

### 0.2 Escalation trigger

⛔ **If the eligible-row count in §5 comes in under 5,424, STOP. Do not run `W6-4`.** Report the number and hand back. `E2`'s own instruction is *"re-run at the next book doubling, **no spend meanwhile**"*. **Running it under-gate and reporting another INCONCLUSIVE is the failure this gate exists to prevent.** The Kelly CAL read is unaffected and proceeds either way.

⛔ **If the assembled book's weekday STRONG count is not ≥406, STOP.** That means the assembly is wrong — the trigger was measured at 407 on the box on 2026-09-09, and a correct pooled book can only be larger.

---

## 1. What this is

**Assemble one pooled book, freeze it, and run two instruments against it.**

| Instrument | What it answers |
|---|---|
| **Kelly CAL** (`W6-3` / `L4`) | Has the `F1` tier ladder SEPARATED, and does STRONG clear the 47.76% Kelly breakeven at the then-current `b`? |
| **`W6-4`** (queue item `E2`) | Re-run the ceiling audit. Its 2026-08-01 run was INCONCLUSIVE on CI width, not on effect |

**What success retires:** the Kelly EST advisory currently renders a forward promise on screen — *"Actual numbers after next book doubling"*. A completed CAL read retires it.

---

## 2. Measured state — read 2026-09-09, not inherited

### 2.1 The trigger

| Source | Weekday STRONG |
|---|---:|
| Box `analysis_log.csv.v0.7.bak` | **337** |
| Box `analysis_log.csv` (live) | **70** |
| **TOTAL** | **407** against ≥406 |

✅ Calibration passed first (49 exactly). ✅ Spans do not overlap: `.bak` ends `2026-09-01 15:48:01`, live starts `15:50:01`. **Margin is +1.**

### 2.2 The file inventory — every candidate, measured

| File | Rows | Span |
|---|---:|---|
| **LOCAL** `bin/Debug/net8.0-windows/analysis_log.csv.v0.7.20260901_145200.bak` | **12,311** | 2026-07-03 15:57 → 2026-08-20 10:27 |
| **LOCAL** `bin/Debug/net8.0-windows/analysis_log.csv.v0.7.bak` | 8,037 | 2026-06-17 14:45 → 2026-07-03 10:11 |
| **LOCAL** `bin/Debug/net8.0-windows/analysis_log.csv` | 17 | 2026-09-01 → 2026-09-08 |
| **AWS copy-back** `AWS-copybacks/aws-copyback-2026-09-01/analysis_log_aws.csv` | 33,901 | 2026-07-22 16:24 → 2026-09-01 15:38 |
| **AWS copy-back** `AWS-copybacks/aws-copyback-2026-09-06/analysis_log_aws.csv` | 4,602 | 2026-09-01 15:50 → 2026-09-06 15:58 |
| **BOX** `C:\DeribitEngine\analysis_log.csv.v0.7.bak` | 33,911 | 2026-07-22 16:24 → 2026-09-01 15:48 |
| **BOX** `C:\DeribitEngine\analysis_log.csv` | 7,250 | 2026-09-01 15:50 → 2026-09-09 14:05 |

⭐ **This reconciles `E2`'s 14,104-row book exactly:** the local `…20260901_145200.bak` (from 07-03 16:23) plus `bin/Debug/analysis_log_aws.csv` (8,137 rows, stopping **2026-07-31 13:59** — the *"AWS book in hand"* named in `docs/backlog-dependency-map.md` line 58), AWS-preferred deduped.

---

## 3. ⛔ THE TWO GATES — one is met, one is not

| Gate | Basis | Threshold | Status |
|---|---|---|---|
| **Kelly CAL** | `F1`'s own basis: pooled weekday STRONG, **203 evaluable** at the `F1` read | **≥406** (2 × 203) | ✅ **MET — 407** |
| **`W6-4` / `E2`** | Its own separate basis: **2,712 eligible rows** | **≥5,424 eligible** | ⛔ **UNMEASURED — §5 decides** |

⚠ **The 406 is 2 × 203, not 2 × 201.** `docs/kelly-est-honesty-decision-2026-08-02.md` §2.1 measures doubling against the **evaluable** count after post-filters. Recorded here because the raw 201 is quoted nearby and the two differ by 4.

---

## 4. The build list

### Step 1 — RESCUE the untracked local rows. ✅ ALREADY DONE 2026-09-09 by the orchestrator.

⛔ **It was the only copy of the 2026-07-03 → 07-22 rows, sitting in an untracked build-output directory that a `dotnet clean` destroys.** It was rescued the moment this spec identified it, rather than left waiting on the spec being executed.

**Use this path, not the `bin/` original:**

```
AWS-copybacks/local-book-rescue-2026-09-09/analysis_log_local.csv.v0.7.20260901_145200.bak
  12,311 rows · 2026-07-03 15:57:49 -> 2026-08-20 10:27:08 · md5 23e4a220092e83ab40ccf81c2e641cc3
```

✅ **MD5 verified byte-identical to the `bin/Debug` original at copy time.** A `README.txt` beside it records why it exists and what must not be done to it. ⚠ **Do not delete the `bin/Debug` original until the pooled read has actually consumed this copy.**

### Step 2 — Pull a FRESH copy-back from the box

The newest copy-back stops **2026-09-06**; the box holds rows to **2026-09-09**. Use `tools/ops/collector.ps1 fetch`, which since `124e154` collects the rotated `.bak` as well. ⚠ **Verify it brought BOTH files** — that fix exists precisely because it did not before.

### Step 3 — Assemble the pooled book

**Inputs:** the rescued local `.bak` · the fresh AWS `.bak` · the fresh AWS live file.

- ⛔ **NEW schema FIRST** in the concat (Trap 4).
- ⛔ **Strip embedded header lines** — a pooled book is a manual concat and the second file's header survives into the middle. `CsvFeatureBuilder` guards this and counts `RepeatedHeadersSkipped`; `ForwardWindowJoiner` gained the same guard in `124e154`'s era. **Verify, do not assume.**
- ⛔ **Dedup AWS-preferred** (2026-07-31 ruling), bounded minute-key.
- **Freeze it.** One file, one path, both instruments read it. Record its MD5.

### Step 4 — Kelly CAL read

Recipe: `docs/f1-tier-ladder-read-2026-08-01.md` — `BacktestRunner report --csv <pooled>`. Then re-work `docs/kelly-est-honesty-decision-2026-08-02.md` §2.1's f\* table at the then-current `b`.

### Step 5 — `W6-4` re-run — **ONLY IF §5's gate passes**

---

## 5. ⛔ PRE-FLIGHT — run these BEFORE either instrument

| # | Assertion | If it fails |
|---|---|---|
| **P-1** | Pooled weekday STRONG **≥406**, counted with `tools/ops/kelly-trigger-read.ps1 -Mode Local -Path <pooled>` | **STOP — the assembly is wrong.** The box read 407; a correct pool can only be larger |
| **P-2** | Pooled row count **> 41,161** (the box's two files alone) | **STOP** — the local rows did not make it in |
| **P-3** | Span starts **≤ 2026-07-03 16:23** and ends at the fresh copy-back's last row | **STOP** — a file is missing from the concat |
| **P-4** | **ELIGIBLE row count ≥ 5,424**, from `CsvFeatureBuilder.LoadAndBuild`'s `LoadStats` on the pooled book | ⛔ **STOP. DO NOT RUN `W6-4`.** Report the number. The Kelly CAL read still proceeds |
| **P-5** | `RepeatedHeadersSkipped` accounted for, and no duplicate `(InstanceId, SignalId)` pairs | **STOP** — the dedup did not work |

⭐ **`P-4` is the whole reason this spec has a pre-flight section.** It is the `E2` gate, it is unmeasured today, and it is measurable in one run of `CsvFeatureBuilder` **without spending anything on the audit itself.**

---

## 6. Acceptance criteria

Forward-looking. Run them and paste actual output.

| # | Criterion |
|---|---|
| **AC-1** | The rescued local `.bak` exists outside `bin/`, with MD5 and row count recorded |
| **AC-2** | The fresh copy-back brought **both** the live file and the rotated `.bak` |
| **AC-3** | All five `P-n` pre-flight assertions run, **with pasted output** — including `P-4`'s eligible count whether it passes or fails |
| **AC-4** | The frozen pooled book's path, row count, span and MD5 are recorded, and **both instruments read that same path** |
| **AC-5** | Kelly CAL: the `F1` ladder re-read, with the f\* table re-worked at the then-current `b`, and an explicit statement whether STRONG clears the **47.76%** breakeven |
| **AC-6** | `W6-4`: either the re-run's ΔAUC and CI, **or** a statement that `P-4` failed with the measured eligible count and it was not run |
| **AC-7** | `settings.json` untouched — verify with `git diff --stat` |
| **AC-8** | Mojibake check and an **unescaped-pipe** count on any doc row touched: `grep -o '[^\\]|'`, compared against a neighbouring row |

---

## 7. What to report back

Two documents per `docs/batch-review-packet-convention.md`: `docs/kelly-w6-4-batch-summary.md` and `docs/kelly-w6-4-spec-back.md`.

⛔ **Label reader-runnable handles `H-n` and build-time evidence `E-n`. Never rank an `E-n` first.** ⚠ **A handle that depends on the frozen pooled book is only an `H-n` if that book still exists at the recorded path — otherwise it is an `E-n`, and say so.**

**State plainly:** whether the ladder separated, whether `P-4` passed, and what was carried rather than measured.

---

## 8. What this must NOT do

- ⛔ Point either reader at a single `analysis_log.csv` — the ~90% artefact
- ⛔ Run `W6-4` if `P-4` fails — *"no spend meanwhile"*
- ⛔ Assemble the book twice, or let the two instruments read different books
- ⛔ Delete or `dotnet clean` over `bin/Debug/net8.0-windows/` before Step 1 completes
- ⛔ Force an `analysis_log.csv` header rotation — the five riders attach to the next one and the rule is never force one
- ⛔ Touch `settings.json`
- ⛔ Run any `git` write command — commit is the orchestrator's
