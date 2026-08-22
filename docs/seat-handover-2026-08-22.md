# Seat handover — 2026-08-22

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This document is the STATE read for the collector migration.** Prior handover: [`seat-handover-2026-08-14.md`](seat-handover-2026-08-14.md) — superseded for state, its rulings still bind.

**Settings: v68, pushed.** Verify with `git status -sb` — never inherit a push state.

---

## 0. FIRST TASK — the collector migration, in four steps that must not be reordered

The trader is replacing the production collector (t3.small / Server 2025, `i-08c740e22d507667d`) with the cheaper test box (t2.micro / Server 2019, `i-0d6c133058876273e`). **The test box becomes production; the old box is retired.**

| # | Step | When | Blocks |
|---|---|---|---|
| 1 | ✅ **DONE 2026-08-22 — 5 readings + a 30-sample sweep. VERDICT: VIABLE.** Evidence in §0.2 | complete | — |
| 2 | ✅ **DONE 2026-08-22 — `aws-cli/2.36.29` installed via `ssm-install-awscli.json`.** MSI signature verified Valid (signer `Amazon Web Services, Inc.`) BEFORE running it; msiexec exit 0; **app PID 4388 unchanged, collector undisturbed**; box lists `s3://deribit-engine-bucket/` under its instance role, so the S3 path `deploy` needs is proven end-to-end from the box | complete | — |
| 3 | **Deploy v68 to the test box** via `collector.ps1` | ⚠ **TWO ATTEMPTS, BOTH FAILED SAFELY. Nothing lost, TAPE NEVER STOPPED.** Attempt 1 found FIX 8 + FIX 9 (+ a 4th marker defect); **attempt 2 proved both fixes WORK** and found ⚠ **FIX 10 (fonts nesting)**. See §0.3 and §0.3b | step 4 |
| 4 | **Cutover** — [`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) **§5.4**, ordered, do not reorder | the weekend | — |

⭐ **A simplification worth not losing: production never needs a v68 deploy.** It is on v66. The test box will be on v68 and *becomes* production, so the old box — the one holding 57.8 MB of irreplaceable tape — is never stopped for a deploy at all. **Do not "helpfully" bring production up to v68 first.** The cost is that production lacks v67's thin-trade gate for a few more days; that path needs a REST seed failure to fire and is rare. The trade is deliberate.

### 0.1 What step 1 is actually asking

⛔ **NOT "wait 24 hours".** The question is whether a box *under memory pressure* plateaus or creeps, and that needs **sample spread, not elapsed time.**

Gap repair fires every 6 h from app start (~2026-08-20 15:51 UTC). On 2026-08-22 that is roughly **11:51 / 17:51 / 23:51 MYT** (±10 min; **re-derive from `app up` in the probe — a restart shifts the whole schedule**).

- **Clean baseline read: 14:00–16:00 MYT**, squarely between repairs.
- ⭐ **Optional sharp read: ~17:50 MYT**, *during* a repair — this is what would confirm the 24 h page-file spike was repair-driven rather than a creep.

**Probes are committed and reusable:** `ssm-mem.json` (memory + eviction), `ssm-apphealth.json` (rows/hour, cadence lag, store, session state), `ssm-memprofile.json` (private/WS/handles/threads — the leak discriminator), `rows24.json` (24 h row-rate comparison), `ssm-stagecheck.json`, `ssm-prebackup.json`, `ssm-tapecheck.json`, `ssm-kelly.json`. All read-only, none needs RDP.

⚠ **Do not restart the test box before step 1 completes.** A restart resets the only memory-pressure evidence that exists.

### 0.2 ✅ Step 1 evidence — t2.micro is VIABLE

**Five readings plus a 30-sample sweep, all via SSM with no RDP session attached.**

| Reading | app up | avail (sust) | **pagesOUT/s** | pagesIN/s | **pagefile** | handles |
|---|---|---:|---:|---:|---:|---:|
| 1 | 1 h | 115 | **0.0** | 2.7 | 36.6 % | — |
| 2 | 24 h | 112 | **0.0** | 1,321 | **67.3 %** ← outlier | 548 |
| 3 | 25 h | 220 | **0.0** | 7.7 | 39.7 % | — |
| 4 | 41.7 h | 206 | **0.0** | 67.5 | 39.8 % | 540 |
| **5 (sweep)** | **42.0 h** | **174–208** | **0.0 ×30** | **0.0 on 29/30** | **39.6 % ×30** | — |

⭐ **Zero eviction in every sample ever taken. The page file did not move by one tick across thirty consecutive 10-second samples.** Handles went **548 → 540** (declining) and app private **62 → 57.6 MB**; measured twice, **there is no leak**.

⭐ **What actually carries the verdict is the work, not the memory:** **38.6 rows/hour against production's 38.3**, 1.0 min behind, **zero DEGRADED events in 42 hours**, six-plus gap repairs survived, and the 24 h head-to-head was **919 rows vs production's 920**.

⚠ **The honest caveat: ~206 MB sustained headroom against production's 496.** Viable is not roomy.

⛔ **AND ONE THING I GOT WRONG — do not repeat it.** I attributed reading 2's spike (67.3 %, 1,321 pagesIN) to a gap-repair pass. **The sweep refutes that.** A repair triggered at 09:41:45 UTC, ~100 s before the sweep, and the sweep shows **nothing** — store growth over 5 minutes was 50,562 bytes, which is streaming capture almost exactly (~1.4 trades/s × 300 s × ~120 B/row), with no backfill signature. **A routine repair on a healthy box finds no holes and costs essentially nothing.** So reading 2 is a **one-off transient of unknown cause** — not recurring, not explained. ⚠ **That was the fourth time this session I offered a tidy explanation for a surprising number and the measurement did not support it.** See §5.1.

**CPU is a settled non-issue.** Credit balance **pinned at 144.0 — the t2.micro maximum — for 12 h straight**, so the app spends less than the box earns and cannot throttle. Peak CPU **52.5 % of 1 vCPU** vs production's **26.2 % of 2** — identical absolute work. ⛔ **t3.micro would buy nothing; the second vCPU would sit idle.** Memory, not CPU, is the thin dimension.

### 0.3 ⚠ Step 3 — ATTEMPTED, FAILED SAFELY. Read this before re-running it.

**First live `deploy`, test box, 2026-08-22. NOTHING WAS LOST.** Every guard fired in order: pre-flight (all three gates) → plan with hashes → y/N answered → app stopped and confirmed → **backup written, all six items verified present (FIX 2 works)** → uploaded to S3 → **hash check CAUGHT the failure** → rollback restored → **`Start-RemoteApp` relaunched the REAL engine binary into SESSION 2.** ⭐ **That last one was the item flagged as most likely to fail. It did not.** The §2.1 mechanism holds for a WinForms app with a live WS connection, not just `notepad.exe`.

**⚠ FIX 8 — the place step reported success it never checked.** Bare `aws s3 cp` on the box; **`aws` does not resolve in an SSM session** (measured: *"The term 'aws' is not recognized"*; `C:/Program Files/Amazon/AWSCLIV2/aws.exe` exists — **the SSM agent's process PATH predates the CLI install**). Every cp failed to stderr, `$ErrorActionPreference` is `Continue` remotely, and `'PLACED=done'` printed anyway. ⭐ **The diagnosis is airtight without re-running anything: the three files that MATCHED (`deps.json`, `runtimeconfig.json`, `OFL.txt`) are exactly the three byte-identical between v67 and v68; the three that MISMATCHED (`exe`, `dll`, `settings.json`) are exactly the three that differ.**

**⛔⛔ FIX 9 — the serious one, and it is NOT about the test box.** The rollback restored v67, relaunched it, printed `OK relaunched`, and exited. **v67 has no `auto_run.start_engaged`, so the app came back RUNNING AND STOPPED** — measured 3.4 min past a 3-min cadence with no new row. **The rollback verified the PROCESS started, never that COLLECTION resumed.** ⚠⚠ **Production is on v66 and equally has no auto-run — so ANY rollback there would leave the live collector running but IDLE, reporting success, while tape quietly stops.** ⚠ **Q2 deliberately skipped the gate on the rollback path to avoid a 5-minute wait. That trade was wrong; I ratified it in review and should not have.**

**STATE AS OF HANDOVER:** implementer claims FIX 8 + FIX 9 applied, **plus a FOURTH marker defect they found by sweeping (`Restore-DeployBackup`'s `RESTORED=done`)** — the sweep was recommended precisely because FIX 2 / FIX 7 / FIX 8 were three instances of one class. ⛔ **NOT REVIEWED, NOT RE-RUN.** Spot-check before trusting: `PLACED=done` is now conditional (`collector.ps1:553`) and the rollback now calls `Wait-DeployGate` (`:645`) — **but confirm FIX 8a, the full-path/PATH-refresh half, is genuinely present; a grep for `AWSCLIV2` returned nothing.**

⚠ **The test box is currently IDLE** (running v67, auto-run stopped after the rollback). Its book is discarded at cutover anyway, so this costs nothing — one RDP click resumes it, or a successful v68 deploy brings it back collecting.

⭐ **THE SEQUENCING PAID FOR ITSELF.** Run against production first, this deploy would have failed identically **and the rollback would have left the live collector silently not capturing tape.** Test box first was the right call.

### 0.3b ⚠ Step 3, SECOND attempt — FIX 8 and FIX 9 both WORKED. One new defect (FIX 10), and one refinement that matters more.

**Re-run 2026-08-22 after FIX 8/9 landed.** ⭐ **FIX 8 WORKED — all six items placed and ALL SIX HASHES MATCHED.** The PATH refresh and the `$LASTEXITCODE` checks did their job. ⭐ **FIX 9 WORKED TOO — it correctly detected that collection had not resumed and escalated instead of reporting `OK`.** Both fixes are proven by execution, not by reading.

**⚠ FIX 10 — `fonts\` nests one level deeper on every deploy attempt.** Measured on the box:

```
\fonts\OFL.txt
\fonts\fonts\OFL.txt
\fonts\fonts\fonts\OFL.txt
```

`aws s3 cp s3://.../fonts <dir>\fonts --recursive` copies the **source prefix itself** into the destination, so each run adds a level — and the backup then preserves the nesting and re-seeds it. **The hash check caught it** (`fonts\fonts\OFL.txt` has no local counterpart, so `local` was blank) and the restore's new file-count check caught it too (`fonts (file count 3 vs backup 2)`). **Harmless in itself** — `OFL.txt` is a licence text file and the `.ttf` fonts are `EmbeddedResource` inside the exe — **but it fails the deploy every time and it must be fixed before the cutover.** Fix: trailing-slash / destination form on the recursive copy, and clean the existing nesting on the box by hand.

⭐⭐ **THE REFINEMENT THAT MATTERS MORE THAN THE DEFECT — and it corrects FIX 9's own alarm text.** `Invoke-Rollback` reports *"The box is up and NOT capturing."* **That is not true, and the distinction is important.** Measured at the same moment: `trades_2026-08.csv` at **29,829,967 bytes, mtime 11 seconds old**. **The TAPE never stopped.** WS streaming capture starts at form load and is **independent of auto-run**; only the ANALYSIS loop needs auto-run engaged. So a rollback stops the **book**, not the **tape**. ⚠ **The tape is the thing this project treats as unrecoverable past ~24 h, and it was never at risk.** **`Wait-DeployGate` checks for a new CSV row, which is the right gate — but its FAILURE MESSAGE overstates what has gone wrong and would send an operator into an emergency that isn't one.** Reword it: *"analysis is not running — the tape is still capturing; engage auto-run"*.

**BOX STATE AS OF HANDOVER (verified read-only, no further automated writes — the script said stop and investigate by hand, and that was honoured):** v67 correctly restored (exe `95064B0B…`, dll 1,126,400 B, settings v67) · app running PID 4152, session 2 · **tape capturing normally** · **analysis loop stopped — needs ONE RDP click on Start, or a successful v68 deploy** · cosmetic 3-level `fonts` nesting. ⛔ **Nothing is lost.**

### 0.4 Three things to have ready for step 4

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
- **`_evalCache`** — see [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §4 (NOT §5 — corrected). ⛔ **Do not "just trim it".** ⚠ **NO SPEC EXISTS FOR THIS WORK.** The problem, the trap and the corrected urgency are recorded; nobody has written the spec. It is NOT a memory leak — handles and threads are flat across a 4x process-age gap — it is ONE unbounded static cache growing ~0.33 MB/day with the book. **Years of headroom on t3.small; it blocks nothing. If it is ever built, the spec must decouple the in-memory list from the file FIRST.**
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
