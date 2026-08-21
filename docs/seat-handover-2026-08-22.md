# Seat handover — 2026-08-22

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This document is the STATE read for the collector migration.** Prior handover: [`seat-handover-2026-08-14.md`](seat-handover-2026-08-14.md) — superseded for state, its rulings still bind.

**Settings: v68, pushed.** Verify with `git status -sb` — never inherit a push state.

---

## 0. FIRST TASK — the collector migration, in four steps that must not be reordered

The trader is replacing the production collector (t3.small / Server 2025, `i-08c740e22d507667d`) with the cheaper test box (t2.micro / Server 2019, `i-0d6c133058876273e`). **The test box becomes production; the old box is retired.**

| # | Step | When | Blocks |
|---|---|---|---|
| 1 | **3–4 memory readings on the test box**, spread out | 2026-08-22 afternoon MYT | everything |
| 2 | **Install the AWS CLI on the test box** — Server 2019 lacks it, Server 2025 ships it | after step 1 (an MSI install pollutes the memory reading) | step 3 |
| 3 | **Deploy v68 to the test box** via `collector.ps1` — this is BOTH the write-path proof AND the version upgrade | after step 2 | step 4 |
| 4 | **Cutover** — [`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) **§5.4**, ordered, do not reorder | the weekend | — |

⭐ **A simplification worth not losing: production never needs a v68 deploy.** It is on v66. The test box will be on v68 and *becomes* production, so the old box — the one holding 57.8 MB of irreplaceable tape — is never stopped for a deploy at all. **Do not "helpfully" bring production up to v68 first.** The cost is that production lacks v67's thin-trade gate for a few more days; that path needs a REST seed failure to fire and is rare. The trade is deliberate.

### 0.1 What step 1 is actually asking

⛔ **NOT "wait 24 hours".** The question is whether a box *under memory pressure* plateaus or creeps, and that needs **sample spread, not elapsed time.**

Gap repair fires every 6 h from app start (~2026-08-20 15:51 UTC). On 2026-08-22 that is roughly **11:51 / 17:51 / 23:51 MYT** (±10 min; **re-derive from `app up` in the probe — a restart shifts the whole schedule**).

- **Clean baseline read: 14:00–16:00 MYT**, squarely between repairs.
- ⭐ **Optional sharp read: ~17:50 MYT**, *during* a repair — this is what would confirm the 24 h page-file spike was repair-driven rather than a creep.

**Probes are committed and reusable:** `ssm-mem.json` (memory + eviction), `ssm-apphealth.json` (rows/hour, cadence lag, store, session state), `ssm-memprofile.json` (private/WS/handles/threads — the leak discriminator), `rows24.json` (24 h row-rate comparison), `ssm-stagecheck.json`, `ssm-prebackup.json`, `ssm-tapecheck.json`, `ssm-kelly.json`. All read-only, none needs RDP.

⚠ **Do not restart the test box before step 1 completes.** A restart resets the only memory-pressure evidence that exists.

### 0.2 Three things to have ready for step 4

- ⚠ **The test box's own book and store are DISCARDED at cutover** ([`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) §5.3). Production covered the same period, and the test box collected under a different settings version. **Deliberate, not an oversight.**
- ⛔ **Stop the old instance. NEVER terminate it.** Stopping preserves the EBS volume.
- ⭐ **Snapshot the 30 GB volume before terminating** — ~$1.50/month, the only backstop against a slip in a procedure whose failure mode is permanent.

⭐ **The number that makes a weekend cutover safe: `gap_repair_lookback_hours` is 20 and repair fires once on start.** Provided the new box collects within 20 h of the old one stopping, **the tape self-heals across the gap.** The weekday-scope ruling means weekend *rows* feed no dated trigger — but **the TAPE has no weekend exemption**, which is why the 20 h number is the one that matters.

---

## 1. State — what shipped this session

| Item | State |
|---|---|
| **v67** thin-trade skip gate | ✅ shipped, pushed, **24 h soak proven** — see §2 |
| **v68** auto-run on start | ✅ shipped, pushed. **Never run anywhere yet** |
| `tools/ops/collector.ps1` | ✅ committed, pushed, FIXes 1–7 applied |
| S3 `deribit-engine-bucket` (eu-west-2) | ✅ created, lifecycle 7 d + 1 d multipart abort, public access blocked |
| IAM `DeribitCollectorS3Access` | ✅ on both instance roles, **scoping proven live** |
| AWS copy-back | ✅ **23,858 rows + 57.8 MB tape** — first since 2026-08-10 |
| Pre-deploy backup on production | ✅ `C:\DeribitVerdictEngine\_manual_backup`, six items |

---

## 2. ⚠ Verified vs assumed — read this before trusting anything above

**Proven by EXECUTION against real boxes:**

- `status` — both boxes, full output.
- `fetch` — production, **all five targets verified exactly** after FIX 7.
- `deploy -DryRun` — production, clean plan, **stopped before the prompt, nothing touched**.
- The three pre-flight gates all fired correctly on real conditions: dirty tree, unpushed HEAD, named target.
- **v67's post-ship prediction held**: the test box on v67 produced **919 rows in 24 h against production's 920 on v66**. The thin-trade gate is not over-firing.

**NEVER EXECUTED — this is the honest gap:**

- ⛔ **`deploy` steps 4–9**: stop, backup, upload, place, hash-verify, restart, acceptance gate. **FIX 1 (`Wait-DeployGate`) and FIX 2 (backup verification) live entirely inside this untested region.**
- ⛔ **`Start-RemoteApp` with the real engine binary.** The session-2 launch PoC used `notepad.exe`. A WinForms app with a WS connection and a settings load is not notepad. **This is the first thing to watch in step 3.**
- v68's auto-run path in a live process.

---

## 3. Open decisions — trader's, untouched

- **Absorption D-table** — [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6. ⚠ **Attack `pullFrac` first, not the anchors**: measured 2026-08-21, the D8 pull veto kills **77.3 %** of real-depletion episodes and **56.6 %** of floor-bound ones, outranking both anchor gates. The population is **bimodal** and the two halves are killed by *different* gates, so no single-anchor fix reaches both.
- **CLI port reversal** — still not written into [`roadmap.md`](roadmap.md); O3 and W4 both still read "DEFERRED LAST". The trader is holding it until the queue drains, which the `_evalCache` finding supports.
- **`_evalCache`** — see §5. ⛔ **Do not "just trim it".**
- **Kelly: 340 / 406, ETA ~2026-09-01** at 9.4/weekday. ⭐ **Readable without a copy-back** — `ssm-kelly.json` counts it on the box; regenerate the embedded local key list first.

---

## 4. ⛔ The landmine I planted and defused — read before touching `LivePerformanceTracker`

I wrote *"Small; a rolling-window trim"* into the queue for `_evalCache`. **It would have destroyed data.**

`WriteEvalCache` (`LivePerformanceTracker.vb:1325`) rewrites the **entire** file with `StreamWriter(path, append:=False)` iterating `_evalCache`, from **five** call sites. **Trimming the in-memory list truncates `analysis_eval_cache.csv` on the next write** — the file Kelly and F1 both read.

That is verbatim this project's own recorded lesson: *"`append:=False` over a whole file from a partial window is how it becomes destructive."* **The list and the file are the same thing by construction; decoupling them IS the work.**

⚠ **And I over-stated the urgency twice.** `EvalCacheEntry` is 8 small fields — 44,047 entries is **~8 MB**, not 46. Growth ~**0.33 MB/day**. **Years of headroom.** The code fact (unbounded, survives the port) is solid; the size attribution was not.

---

## 5. ⚠⚠ Three lessons, each earned the hard way this session

### 5.1 Two points is not a trend — I got this wrong THREE times in one day

| Alarm I raised | Next reading |
|---|---|
| App private "121 → 134 MB, growing" | **113.7 MB** |
| Page file "67.3 % and climbing" | **39.7 %** |
| The ramp implied by both | **18 hours flat, 1,828–1,884 MB** |

⭐ **The instrument that settled it was already in the working tree** — `tools/mem-probe.csv`, untracked, a **21-hour series** from 2026-08-10. Hourly means: 2209 → 2243 (peak) → 1998 (release) → then **eighteen consecutive hours between 1,828 and 1,884 MB**, with available memory *rising* 216 → 671.

**On a fluctuating metric, take three readings spanning a known-quiet interval before asserting a direction.** Saying "two points is not a trend" and stopping is the whole discipline. *(The file is now gitignored by trader decision; the analysis survives in commit `bbea796`.)*

### 5.2 Execution finds what parsing structurally cannot — 3 for 3

| Defect | Found by | Catchable by parse? |
|---|---|---|
| FIX 1 — `(if ...)` as an expression | executing it on 5.1 | ❌ parses clean, throws at runtime |
| FIX 6 — BOM from `Set-Content -Encoding utf8` | running `status` | ❌ the payload is a string until SSM runs it |
| FIX 7 — manifest races the live collector | running `fetch` | ❌ needs a box actively appending |

**Every one would have shipped through a review that only read and parsed.** ⚠ **FIX 6 is the sharpest: I hit that exact BOM trap myself the day before and wrote the warning into a commit message — and it still reached the script.** Knowing a trap is not the same as testing for it.

### 5.3 A marker you print is not a property you checked

`BACKUP=done` was emitted unconditionally. `SNAPSHOT_CLEANED=true` is a self-report. The tape-check probe printed *"Gap repair healed the DEGRADED window"* — a **cause it cannot observe**. All three were corrected to assert the property instead. **When a check reports success, ask what would have to be true for it to report failure.**

---

## 6. Two things that are NOT what they look like

- **The 2026-08-18 venue halt** — an 87-minute void in `trades_2026-08.csv`, `09:04:04Z → 10:26:55Z`. **NOT capture loss.** `trade_seq` is perfectly contiguous across it (338 rows, span 338, **0 missing**), so the venue assigned no sequence numbers. Resume burst 122/109 per minute against a ~22/min baseline confirms a halt. ⭐ **This validated D-2 on live data** — a time-based detector reports catastrophe here and `trade_seq` correctly reports zero missing. ⛔ **And the downtime-repair prediction did NOT pass — it did not FIRE.** No hole means nothing to heal. **Do not tick it.**
- **`trade_seq` skip claim** — ✅ **RESOLVED as unsourced.** Both candidate sources checked; the API reference documents only *"The sequence number of the trade within instrument"*. **Treat as strictly sequential. D-2 needs no caveat.**
