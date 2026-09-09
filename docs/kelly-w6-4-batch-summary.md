# Kelly CAL + `W6-4` re-run — batch summary (2026-09-09)

**Spec:** [`kelly-w6-4-pooled-read-spec.md`](kelly-w6-4-pooled-read-spec.md). **Baseline commit:** `1aeae5a`. **Model/effort used:** Sonnet, HIGH, one session, as specified.

> ## Both instruments ran. Both remain INCONCLUSIVE / SUPPRESSED — but both moved, and one population crossed a line it had never crossed before.
> All five pre-flight assertions (`P-1`–`P-5`) passed, so `W6-4` was authorized and run. **Kelly CAL:** the tier ladder still does not separate pooled, and pooled STRONG (47.1 %) still sits marginally below the 47.76 % breakeven — narrower than the 2026-08-01 reading (46.8 %) but not resolved. **`W6-4`:** NY×1 (decisive) is still INCONCLUSIVE, but its CI halved in width (±0.16 vs ±0.32) and no longer contains zero on one side the way it used to skew. ~~**New finding, not asked for by either instrument's headline verdict:** ASIA×3 STRONG's 95 % CI (52 %–75 %, n=62) now sits **entirely above** breakeven for the first time~~ ⛔⛔ **WITHDRAWN BY THE ORCHESTRATOR REVIEW, 2026-09-09. THE CLAIM USED THE WRONG BREAKEVEN AND IS FALSE FOR ASIA.**

> ⛔ **CORRECTION — the ASIA crossing did not happen.** This document compared ASIA against the **global** breakeven of **47.76 %**, which is derived from the global `b` = `atr_target_multiplier` 1.75 ÷ `atr_stop_multiplier` 1.6 = 1.09375. **ASIA does not run on the global `b`.** `settings.json` `scoring.structural_levels.sessions` sets `ASIA.fallback_target_atr_mult` = **1.25**, so ASIA's `b` = 1.25 / 1.6 = **0.78125** and its breakeven is `1/(1+b)` = **56.14 %**.
>
> **ASIA STRONG's CI floor is 52 %, which is BELOW 56.14 %.** The point estimate (64.5 %) clears its own breakeven; **the interval does not**. The "first STRONG-band CI in this project's history to sit entirely above breakeven" framing dissolves.
>
> ⚠ **This document flagged its own input and then relied on it anyway.** [`kelly-w6-4-spec-back.md`](kelly-w6-4-spec-back.md) §4 correctly records that the per-session `b` values (NY 1.094 / LONDON 1.250 / ASIA 0.781) were **not re-derived** — and those values are right, verified against `settings.json` v68 by the review. The headline above then stated the crossing as fact. **The honest caveat and the unsafe claim sat in the same packet.**
>
> ⚠ **Second-order, recorded so it is not missed:** POOLED mixes three sessions carrying three different `b`, so comparing "pooled STRONG 47.1 %" against 47.76 % applies **NY's** breakeven to a mixed population. Directionally it does not change the verdict (still below), but the pooled breakeven is not a single well-defined number.
>
> ✅ **Ruled `D-3` (c) — do nothing.** See the spec-back's §2.

---

## 1. The pooled book

| Field | Value |
|---|---|
| Path | `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv` (gitignored, persists on disk) |
| Rows (data, excl. header) | **47,682** |
| Span | 2026-07-03 15:57:49 → 2026-09-09 14:39:04 UTC |
| MD5 | `e8418846838ff97f3c90f782a95b3523` |
| Bytes | 43,941,908 |
| Header used (Trap 4) | the 116-column live-file header, placed first per the ruled concat order |

**Assembly (Step 3), in file order:**

| Order | Source | Rows contributed | Span |
|---|---|---:|---|
| 1st (new schema, per Trap 4) | fresh AWS live (`aws_fetch/20260909-143922/analysis_log.csv`) | 7,284 | 2026-09-01 15:50:01 → 2026-09-09 14:39:04 |
| 2nd | fresh AWS `.bak` (`aws_fetch/20260909-143922/analysis_log.csv.v0.7.bak`) | 33,911 | 2026-07-22 16:24:54 → 2026-09-01 15:48:01 |
| 3rd | rescued local `.bak` (`AWS-copybacks/local-book-rescue-2026-09-09/…`), AWS-preferred minute-key deduped | 6,487 kept / 12,311 total (5,824 dropped as AWS-preferred collisions) | 2026-07-03 15:57:49 → 2026-08-20 10:27:08 (post-dedup) |

`7,284 + 33,911 + 6,487 = 47,682`. ✅ matches the file's own data-row count.

**Step 1 (local rescue) and Step 2 (fresh copy-back)** were already done / performed this session — Step 2's `collector.ps1 fetch` verified all six transfer targets against the box-side manifest (all `OK`, no mismatches). **AC-1** and **AC-2** are both satisfied: the rescue's MD5+row-count are recorded in its own `README.txt`, and the fetch log confirms both the live file and the rotated `.bak` landed.

---

## 2. Pre-flight (§5) — all five assertions, run and pasted

| # | Assertion | Result | Verdict |
|---|---|---|---|
| **P-1** | Pooled weekday STRONG ≥ 406 | **518** (`tools/ops/kelly-trigger-read.ps1 -Mode Local`) | ✅ PASS (margin +112 over the box's own 407) |
| **P-2** | Pooled row count > 41,161 | **47,682** | ✅ PASS (margin +6,521) |
| **P-3** | Span starts ≤ 2026-07-03 16:23, ends at the fresh copy-back's last row | min **2026-07-03 15:57:49**, max **2026-09-09 14:39:04** (exact match to the live file's last row) | ✅ PASS |
| **P-4** | Eligible rows ≥ 5,424 (`CsvFeatureBuilder.LoadAndBuild`) | **8,269** | ✅ PASS (margin +2,845, +52 %) |
| **P-5** | `RepeatedHeadersSkipped` accounted for; no duplicate `(InstanceId, SignalId)` | **0** embedded headers, **0** duplicate pairs | ✅ PASS |

**P-1 raw output:**
```
KELLY_ROWS=47682
KELLY_SPAN=2026-09-01 15:50:01 -> 2026-08-19 09:43:13   (file order, not chronological — see note below)
KELLY_WEEKDAY_STRONG=518
KELLY_EXCL=weekend:36 unparsed:0
```
*Note: `kelly-trigger-read.ps1`'s printed `KELLY_SPAN` is first-line/last-line, not min/max — because Trap 4 puts the pooled file in non-chronological order (new schema first), that line reads "2026-09-01 → 2026-08-19", which looks like an inversion but isn't a defect. The actual chronological span (computed separately for `P-3`) is the row above.*

**P-4 raw output** (throwaway harness calling `CsvFeatureBuilder.LoadAndBuild` directly — see spec-back §1 for why this is `E-1`, not `H-n`):
```
P4_TOTAL_ROWS=47682
P4_ELIGIBLE_ROWS=8269
P4_REPEATED_HEADERS_SKIPPED=0
P4_NON_V08_EXCLUDED=0
P4_WEEKEND_EXCLUDED=9792
P4_NON_DIRECTIONAL_EXCLUDED=29053
P4_BURST_INSTANCE_PREFIX_EXCLUDED=192
P4_BURST_CADENCE_INSTANCES_EXCLUDED=2
P4_BURST_CADENCE_ROWS_EXCLUDED=568
```
Identity check: `47682 − 568(burst) − 0(non-v0.8) − 9792(weekend) − 29053(non-directional) = 8269`. ✓ Independently confirmed by the real `W6-4` run below, which printed the identical `8269` from the shipped `CeilingAudit.exe`.

**P-5 raw output:**
```
InstanceId col index: 109   SignalId col index: 110   (of 116 cols)
Total data rows scanned: 47682
Embedded header lines found mid-file: 0
Duplicate (InstanceId,SignalId) pairs: 0
```

---

## 3. Kelly CAL read (`W6-3`/`L4`) — Step 4

**Recipe run:** `dotnet tools/BacktestRunner/bin/Release/net8.0/BacktestRunner.dll report --csv AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv` (settings v68, unchanged). Report: `AWS-copybacks/pooled-book-2026-09-09/analysis_report_20260909_144728.md`. Rows loaded by this instrument: 37,518 (a different filter set than `CeilingAudit`'s 8,269 — expected; the two tools apply different population/exclusion logic and were never meant to agree on row count).

### 3.1 §9 band ladder — does it separate?

| Population | Band | n | Success | 95% CI |
|---|---|---:|---:|---|
| **POOLED** | STRONG | 518 | **47.1 %** | [43–51] |
| POOLED | MEDIUM | 2,346 | 42.5 % | [41–45] |
| POOLED | WEAK | 5,493 | 42.4 % | [41–44] |
| NY×1 (15m) | STRONG | 323 | 45.5 % | [40–51] |
| NY×1 | MEDIUM | 1,475 | 39.5 % | [37–42] |
| NY×1 | WEAK | 3,307 | 40.4 % | [39–42] |
| LONDON×3 (45m) | STRONG | 133 | 42.9 % | [35–51] |
| LONDON×3 | MEDIUM | 466 | **46.4 %** | [42–51] |
| ASIA×3 (45m) | STRONG | **62** | **64.5 %** | **[52–75]** |
| ASIA×3 | MEDIUM | 405 | 49.1 % | [44–54] |

**Verdict: the ladder still does not separate.** Pooled STRONG edges MEDIUM by +4.6 pp — the identical gap magnitude as the 2026-08-01 read — but the CIs still overlap (43–45 is common ground), and MEDIUM/WEAK remain statistically indistinguishable (42.5 vs 42.4). **LONDON still inverts at the top** (MEDIUM 46.4 % > STRONG 42.9 %), and **NY's MEDIUM is still below its own WEAK** (39.5 % vs 40.4 %) — both the same non-monotonic shapes flagged in the original F1 read.

**What changed: ASIA.** STRONG's n grew from 15 (unusable) to **62**, and its point estimate is **64.5 %**. ⛔ ~~its CI (52–75 %) is now the first STRONG-band CI in this project's history to sit **entirely above** the 47.76 % breakeven~~ — **WITHDRAWN, see the correction block at the top of this document. 47.76 % is the GLOBAL breakeven; ASIA's own is 56.14 % at `b`=0.78125, and its CI floor of 52 % is BELOW that.** The point estimate clears its own breakeven; the interval does not. ASIA is still not the `W6-4` decisive population, and 62 rows is below the n≥150 threshold the original `F1` read wanted. ✅ **Ruled `D-3` (c) — do nothing.**

### 3.2 The f\* table, re-worked at the then-current `b`

`b = atr_target_multiplier / atr_stop_multiplier = 1.75 / 1.6 = 1.09375` — **checked against tracked `settings.json` v68, line 425–426: unchanged since the 2026-08-02 decision doc.** Breakeven `p = 1/(1+b) = 47.76 %` — also unchanged.

| Source | p | f\* | half-Kelly | applied |
|---|---:|---:|---:|---|
| **POOLED STRONG, CI low** (0.430) | 0.430 | −0.0911 | — | SUPPRESSED |
| **POOLED STRONG, point** (0.471) | 0.471 | **−0.0127** | — | **SUPPRESSED** |
| POOLED STRONG, CI high (0.510) | 0.510 | +0.0620 | 0.0310 | **3.10 %** (not capped) |
| NY×1 STRONG, point (0.455) | 0.455 | −0.0433 | — | SUPPRESSED |
| LONDON×3 STRONG, point (0.429) | 0.429 | −0.0930 | — | SUPPRESSED |
| ~~**ASIA×3 STRONG, CI low** (0.520)~~ | ~~0.520~~ | ~~+0.0811~~ | ~~0.0406~~ | ⛔ ~~**4.06 %**~~ **WRONG — recomputed at ASIA's own `b`=0.78125: f\* = −0.0944 ⇒ SUPPRESSED** |
| ~~ASIA×3 STRONG, point (0.645)~~ | ~~0.645~~ | ~~+0.3204~~ | ~~0.1602~~ | ⚠ ~~CAPPED 5 %~~ **recomputed at `b`=0.78125: f\* = +0.1906, half-Kelly 0.0953 ⇒ still CAPPED 5 %** |

⛔⛔ **BOTH ASIA ROWS ABOVE WERE COMPUTED AT THE GLOBAL `b`=1.09375 AND ARE WRONG. Corrected by the orchestrator review 2026-09-09 — see the correction block at the top of this document.** ASIA's `b` is **0.78125** (`ASIA.fallback_target_atr_mult` 1.25 ÷ `atr_stop_multiplier` 1.6) and its breakeven is **56.14 %**, not 47.76 %.

| Corrected — ASIA at its OWN `b`=0.78125 | p | f\* | half-Kelly | applied |
|---|---:|---:|---:|---|
| **ASIA×3 STRONG, CI low** (0.520) | 0.520 | **−0.0944** | — | ⛔ **SUPPRESSED** (was reported as 4.06 %) |
| ASIA×3 STRONG, point (0.645) | 0.645 | +0.1906 | 0.0953 | **CAPPED 5 %** (same verdict, different f\*) |

⭐ **The CI-low row FLIPS.** The pessimistic end of ASIA's interval does **not** clear its own breakeven and sizes to zero, where this table reported 4.06 %. **That flip is the whole of the `D-3` case, and it does not survive.**

**Explicit statement (AC-5): pooled STRONG does NOT clear the 47.76 % breakeven at the then-current `b`.** The point estimate (47.1 %) sits 0.66 pp below breakeven — closer than 2026-08-01's 1.0 pp gap, but still on the suppressed side, and the 95 % CI [43–51] still straddles breakeven the same way it did before (underpowered, not null — same framing as the original doc). ⛔ ~~**The one number that moved past the old ceiling is ASIA's CI floor** (52 % — even the *pessimistic* end of ASIA's interval now clears breakeven, applying 4.06 %)~~ **WITHDRAWN. At ASIA's own breakeven of 56.14 %, the 52 % floor does NOT clear and sizes to SUPPRESSED.** Only ASIA's *point* estimate (64.5 %) clears, and ASIA was never the decisive population for either instrument, with n=62 thin against the `F1` method's own n≥150 bar. Kelly CAL still should not ship on pooled STRONG; this is a decision queued in the spec-back, not a ruling made here.

---

## 4. `W6-4` re-run — Step 5 (authorized by `P-4`)

**Recipe run:** `dotnet tools/CeilingAudit/bin/Release/net8.0/CeilingAudit.dll AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --out AWS-copybacks/pooled-book-2026-09-09` — defaults (margin ±0.030, bootstrap B=1000, seed 42, min-test-days 7), same as the 2026-08-01 run. Report: `AWS-copybacks/pooled-book-2026-09-09/ceiling_audit_report_20260909_144845.md`.

| Population | Decisive | N labelled | N test | Test days | Baseline AUC → Challenger | ΔAUC | 95% CI | Verdict |
|---|---|---:|---:|---:|---|---:|---|---|
| **NY×1** | **yes** | 5,017 | 636 | 6.99 | 0.4777 → 0.5100 | **+0.0356** | **[−0.0452, +0.1145]** | **INCONCLUSIVE** |
| LONDON×3 | no | 1,601 | 206 | 7.00 | 0.3968 → 0.4311 | +0.0395 | [−0.1199, +0.1910] | INCONCLUSIVE |
| ASIA×3 | no | 1,651 | 326 | 6.31 | 0.4839 → 0.4732 | −0.0104 | [−0.1202, +0.1039] | INCONCLUSIVE |

**Verdict unchanged (INCONCLUSIVE), but the CI narrowed materially.** NY×1's 95 % CI is now [−0.045, +0.114] — width **0.160** — against the 2026-08-01 reading's [−0.197, +0.124], width **0.321**. The interval halved. It still straddles the ±0.030 margin (its own decision threshold), so the formal verdict does not move, but this is real progress toward resolution rather than noise: at the prior book's 484 test rows the CI could not distinguish "no edge" from "meaningful edge"; at 636 test rows it now excludes the most extreme prior readings on the downside. **Per §4 of the ceiling-audit method: re-run at the next book doubling, no spend meanwhile** — same instruction as before, now with a visibly-tightening instrument behind it.

Baseline test AUCs also moved: NY 0.4777 (was 0.5407 — now *below* chance on this split, though a single split shift at this sample size is not itself news), LONDON 0.3968 (was 0.5489), ASIA 0.4839 (was 0.5671). These are directional-ranking AUCs on a fresh chronological train/test split of a much larger book and are not comparable point-for-point across the two runs; flagged as a decision-queue item (spec-back §2) rather than interpreted here.

---

## 5. Acceptance criteria — status

| # | Criterion | Status |
|---|---|---|
| AC-1 | Rescued local `.bak` outside `bin/`, MD5+rows recorded | ✅ done pre-session, verified this session (`README.txt`) |
| AC-2 | Fresh copy-back brought both live file and rotated `.bak` | ✅ `collector.ps1 fetch` verified both, no mismatch |
| AC-3 | All five `P-n` run with pasted output | ✅ §2 above |
| AC-4 | Pooled book path/rows/span/MD5 recorded, both instruments read the same path | ✅ §1 above; both `BacktestRunner` and `CeilingAudit` pointed at the identical file |
| AC-5 | Kelly CAL: ladder re-read + f\* table + explicit breakeven statement | ✅ §3 above |
| AC-6 | `W6-4`: ΔAUC/CI, or `P-4`-failed statement | ✅ §4 above (`P-4` passed, so the re-run — not a failure statement — is what's reported) |
| AC-7 | `settings.json` untouched | ✅ `git diff --stat` — empty; `git diff -- settings.json` — empty |
| AC-8 | Mojibake + unescaped-pipe check on touched doc rows | ✅ see spec-back §4 |

---

## 6. What was NOT done (per spec §8)

- Did not point either reader at a single `analysis_log.csv` alone.
- Did not run `W6-4` before `P-4` passed (`P-4` was measured first via a throwaway harness, §2, before the real audit — which then independently reproduced the identical 8,269).
- Assembled the book exactly once; both instruments read the identical frozen path.
- Did not touch `bin/Debug/net8.0-windows/` before Step 1 (already done pre-session).
- Did not force a CSV header rotation.
- Did not touch `settings.json`.
- Did not run any `git` write command.

---

**Full detail, ranked verification handles, decisions queued, and spec feedback:** [`kelly-w6-4-spec-back.md`](kelly-w6-4-spec-back.md).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
