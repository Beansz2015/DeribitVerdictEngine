# Seat handover — 2026-09-06 (UTC)

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-09-03.md`](seat-handover-2026-09-03.md)** — superseded for STATE. Its collector detail and its §5 lessons still bind.

**Settings: v68**, unchanged all session. ⛔ **Run `git status -sb` — never inherit a push state from this line.**

⚠ **ALL DATES HERE ARE UTC. The workstation is GMT+8.** ⭐ **A LIVE INSTANCE, from this session:
the workstation clock rolled to 2026-09-07 while this document was being written, because
`2026-09-06 15:58 UTC` — the timestamp of every measurement in §1 and §2 — is
`2026-09-06 23:58` local, minutes from midnight.** ⛔ **So a seat reading this on a
GMT+8 clock showing 09-07 is reading measurements taken on 09-06 UTC, and §1's "not before
2026-09-08" is a UTC deadline. Check which clock before calling anything late or impossible.**

---

## 0. ⭐ FIRST TASK — the Kelly read, and it is DATED to Tuesday

| Pick | Item | When | Model / effort |
|---|---|---|---|
| ⭐ **1st** | **The Kelly read** — [`trader-tick-queue.md`](trader-tick-queue.md) §0a's dated trigger | ⛔ **NOT before 2026-09-08.** See §1 | Sonnet, medium |
| **2nd** | **`S2-2` — split `CalcSpread`.** Needs its own proposal, spec-first | No clock | Opus, high — see §4 |
| **3rd** | Item 6 — `WsTradeProbe` through the shared trade reader | No clock. ⛔ Read the code first; nobody has checked whether S-1 still holds | Sonnet, medium |

**Nothing is blocked. Nothing is owed by the trader.**

---

## 1. ⛔ THE KELLY READ — measured today, and the arithmetic is now simple

⭐⭐ **MEASURED 2026-09-06 15:58 UTC, from a fresh copy-back, not projected:**

| Source | weekday STRONG | Status |
|---|---:|---|
| `analysis_log.csv.v0.7.bak` (pre-rotation) | **337** | ⭐ **A CLOSED FILE — this number is FIXED and will never change.** Cross-validated twice |
| `analysis_log.csv` (live, from 2026-09-01 15:50:01) | **49** | Counted locally off today's copy-back |
| **TRUE TOTAL** | **386** | against **≥406** — **shortfall 20** |

⭐⭐ **THE SIMPLIFICATION WORTH KEEPING: the next read does NOT need a copy-back.** The `.bak`
half is a closed file at a known 337. **The whole read is `337 + (weekday STRONG in the live
`analysis_log.csv`)`** — one count, obtainable over SSM in a single command. ⛔ **The two-file
trap the 09-03 handover warned about is now a one-file count plus a constant.**

**When:** 386 today, at a measured **~14/weekday** (49 over ~3.5 weekdays since the rotation).
**Mon 09-07 ends ≈ 400. Tue 09-08 ends ≈ 414.** ⛔ **So it clears DURING Tuesday 2026-09-08 —
one day later than the 09-03 projection's "09-07 to 09-08" optimistic end.** Do not fetch or
read before then. **W6-4 rides with it.**

⚠ **2026-09-05 and 09-06 are Sat/Sun — no weekday STRONG accrues.** The 09-03 projection
already skipped them; a seat reading only the raw totals will not.

### 1.1 ⛔ A DEFECT FOUND TODAY — `collector.ps1 fetch` does NOT collect the rotated `.bak`

**The fetch ran clean** (`aws_fetch/20260906-155831`, all transfers size-verified, FIX 7's
snapshot-then-manifest works). ⛔ **But its box-side manifest lists only four files —
`analysis_log.csv`, `ws_health.log`, `capture_marker.log`, `analysis_eval_cache.csv` — plus
`backtest_data\`. `analysis_log.csv.v0.7.bak` is NOT among them.**

⚠ **So the pre-rotation book is STILL box-only**, five days after the 09-03 handover flagged
it — including the **~11 minutes of rows (15:38 → 15:49 on 2026-09-01) that exist nowhere
else.** ⭐ **This does not endanger the Kelly read** (337 is already cross-validated and the
`.bak` is closed), **but it does mean a box loss would take those 11 minutes with it.**
Queued in [`trader-tick-queue.md`](trader-tick-queue.md) §2 as `OPS-1`. **The fix is one entry
in the script's file list — but it is a ruling, not a patch: a `*.bak` glob would also sweep
future rotations, which may be wanted or not.**

---

## 2. The collector — healthy, read today

| | |
|---|---|
| Instance | **`i-0d6c133058876273e`** |
| exe built | **2026-09-01 15:48** — matches the deploy; no redeploy since |
| Settings | **v68**, overlay `False` |
| Book | 4,602 rows · first `2026-09-01 15:50:01` · last `2026-09-06 15:58:27` |
| Cadence | **38.3 rows/hour** against a 37.0–38.3 baseline — healthy |
| Gap now-to-last-row | **0.8 min** |
| Store | `trades_2026-07` 128 KB · `2026-08` 123.4 MB · `2026-09` 35.6 MB |
| Memory | avail **141 / 162 MB** sustained · `pagesOUT/s` **0.0** · pagefile 37.2 % |
| Feed | ⚠ **Two DEGRADED→OK pairs, both recovered fast:** 09-05 09:04→09:36 (32 min) and 09-06 15:43:43→15:43:59 (16 s). Same instance id throughout — **feed events, not restarts** |

⚠ **An RDP session has been disconnected for 5 days 22 h** (`administrator`, since 08-20).
Prior handovers record an attached session costing 30–50 MB; **avail at 141 MB is at the low
end of the recorded 168 median.** Not acting on it — recorded so the next memory reading is
not attributed to a code change.

⚠ **The 09-05 DEGRADED ran 32 minutes.** Nobody has checked whether it cost tape. **The
downtime-repair prediction is the instrument** — verify from `trade_seq` completeness, not
from the repair log.

---

## 3. What shipped this session — the A54a arc, complete

| Item | Commits |
|---|---|
| **A54a guard** — `WalkPocoVsJson` + `A62a`–`A62g` + **seven POCO re-syncs** | `3a89093` |
| **R-1 / R-2 / R-3** — dict completeness, stale comment, D-R3 (i) ROC nullable seeds | `cc44e9f` |
| **F-1** — POCO-only dict key reported as `Orphans` | `a86a0cf` |
| **Session 2** — **44 `Optional` defaults deleted across 15 methods** | `fded077` · `94b68d5` |
| **`I17-A6` + `I17-SWEEP`** — 26 literals measured, 17 made synthetic | `98ed4fd` · `1b2adbe` |
| **`A64a` / `A64b`** — the spare vote and the joint dependency pinned | `6bfa5de` |

**Harness 326 → 328. Gate PASSED throughout. Settings stayed v68 — `settings.json` is in NONE
of these diffs.** ⭐ **§15 carries ONE row for the whole arc** (queue item 8), per its own
one-item-one-row rule.

### 3.1 ⭐⭐ The three things that outlive the code

1. **A guard's scoping rule can be right about DRIFT and silent about CORRECTNESS.** `R-3`:
   the nullable was legitimately absent and the seed was still wrong, because *the resolution
   it inherits under changed underneath it*. No reflection walk scoped by that rule can catch
   the class — only a behavioural fixture (`A63a`) can.
2. **A wide one-at-a-time band can be a SIBLING ARTEFACT, not insensitivity** — the `MASKED`
   class. `A9.adxMin` probes inert across five orders of magnitude **only because `minOf`
   carries a spare vote.** ⛔ **A naive sweep would have licensed any value.**
3. **"Off-shipped" must mean off-*EVER*-shipped.** Now a standing rule in CLAUDE.md.

### 3.2 ⚠ Two rules added to CLAUDE.md this session

- **Off-*ever*-shipped**, on the fixture-literal provenance rule.
- ⛔ **"Run every handle and paste its actual output before publishing it. A handle that has
  not been run is a guess."** ⭐ **The evidence: FOUR handles in this arc were wrong on first
  draft, three of them the exact string-not-property shape the existing rule names — and the
  fourth was written AFTER its author documented the pattern in that same document.**

---

## 4. `S2-2` — the next real build, and why it is Opus/high

Split `CalcSpread` into `CalcSpreadBps(orderBook)` (pure) + `ClassifySpread(bps, wide, tight)`.
Full analysis at [`a54a-session2-step1-measurement-2026-09-05.md`](a54a-session2-step1-measurement-2026-09-05.md) §9.

⭐ **Why it is worth doing:** `LiveMicrostructureEvaluator` currently passes two cfg thresholds
purely to compute a status it discards. After the split it references them **not at all**, so
the tweaker-retune divergence `S2-1` had to guard becomes **impossible by construction**.

⛔ **Why Opus/high and not the medium the queue row says:** it is the **only** item in this
whole arc where the **display-string parity rule is LIVE** — `SpreadStatus` feeds the Step-2
WIDE penalty, the breakdown note, the snapshot line and **two card bindings**. **Four rendered
surfaces.** Everything else this session was harness-only. **Spec-first: it needs a proposal
with a D-table before any code.**

---

## 5. ⚠ Lessons — and the count is the finding

⛔⛔ **NINE reviewer corrections in one arc, and the reviewer wrote the specs being corrected.**
Not one was caught by care; every one was caught by a measurement or a contradiction.

1. ⛔ **The sharpest: `D1` instructed pinning `0.30` — a SHIPPED value — in a MECHANISM
   fixture, ONE DAY after the same seat ruled *"off-shipped means off-EVER-shipped"* into
   CLAUDE.md.** **The rule did not stop its own author breaking it in the next instruction he
   issued.** The implementer refused it and substituted a measured `0.37`.
2. ⚠ **A spec carried HALF a ruling.** The 2026-08-11 seeded-session-buckets ruling has a
   GUARD half and a RE-SYNC half; the A54a spec used the first, dropped the second, and its
   own §0 arithmetic then contradicted its own D-table unnoticed. **A freshly-written document
   is not evidence that a standing ruling reached it.**
3. ⚠ **Two published claims falsified by measurement** — `slope_pct_of_value` *was* shipped at
   `0.05`; `trend_gate` was **never** `8.0` (invented while writing a queue row).
4. ⚠ **`perl -0777` in-place silently mangles these docs' UTF-8.** Caught by a mojibake grep
   before commit. **Use the editor, not perl, on `docs/`.**

⭐ **What actually worked, twice: *"every mutation RUN, not reasoned."*** `A62f` and `A63a`
were each built exactly to a spec's worked description and each could **not observe a mutation
that spec itself named.** Reasoning would have ticked both green.

---

## 6. Loose ends

- ⛔ **`OPS-1`** — `fetch` does not collect the rotated `.bak` (§1.1). A ruling, not a patch.
- **`S2-2`** and the **`D4` residual** (positional passing · whole-`cfg` builders · `OfiAccumulator.vb:84`'s `tauSec`, the one same-class mode switch pinned by no fixture).
- **Absorption D-2 / §4.1 read — ~2026-09-15.** ⚠ Its first reading already points AGAINST §4.1's premise (median episode 1.7 s against an assumed too-short 10 s). See [`seat-handover-2026-09-03.md`](seat-handover-2026-09-03.md) §2.1.
- **Next free fixture family: `A65`.** Next free hard constraint: **HC29**.
- ✅ **Today's copy-back is filed at `AWS-copybacks/aws-copyback-2026-09-06/`** — root CSV plus the whole `aws_fetch/20260906-155831/`, matching the 09-01 layout exactly. **Move verified byte-for-byte** (173,452,794 B + 4,272,646 B, 7 files, unchanged either side). `AWS-copybacks/` is gitignored (`.gitignore:430`), so it is **local-only — it does not travel with a push and does not exist on any other machine.** ⚠ **The folder is named for the UTC fetch date (09-06), matching the timestamp inside it, NOT the local clock** — see the GMT+8 note at the top.
