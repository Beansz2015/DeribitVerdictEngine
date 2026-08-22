# Seat handover — 2026-08-23

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This document is the STATE read.** Prior handover: [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) — **superseded for STATE, its rulings still bind.** Its §0 first task (the collector migration) is **DONE**.

**Settings: v68, pushed.** Master at `9eb3329`. Verify with `git status -sb` — never inherit a push state.

⚠ **DATES IN THIS DOCUMENT ARE UTC.** The machine is GMT+8, so the whole cutover reads 2026-08-22 in UTC and spans local midnight into 2026-08-23. **Check now-in-UTC before claiming any collection gap** — this project has made two false gap claims from that confusion, one formally withdrawn.

---

## 0. FIRST TASK — the four-site single-instance defect class in `collector.ps1`

⛔ **This is the only thing gating further use of the deploy tooling, and the box it threatens is now production.**

**Model: Opus. Effort: high.** Not for the diff — it is a guard repeated at four call sites. It is because this file has now produced **seven** defects found only by executing it, and because the fix has to be *verified against a deliberately failing deploy*, which is the one test nobody has ever run to completion. A cheap tier will patch the loud site and leave the three silent ones.

### 0.1 What is wrong

**One assumption — "exactly one `DeribitVerdictEngine` process exists" — is unguarded at four sites.** `Get-Process` returns an *array*; every site treats it as scalar.

| Site | Line | Failure |
|---|---|---|
| `Invoke-Fetch` dir resolution | `collector.ps1:262` | `$dir = Split-Path $p.Path` yields an ARRAY → **silent** bogus remote dir on the load-bearing copy-back |
| `Invoke-Deploy` pre-flight | `collector.ps1:431` | same, plus `REMOTE_PID` / `REMOTE_SESSION` render as `"2484 2920"` / `"2 2"` |
| `Start-RemoteApp` | `collector.ps1:782` | reports `LAUNCH_SESSION=2 2` and calls it a success |
| `Wait-DeployGate` | `collector.ps1:824-825` | `[int]"2 2"` **throws every poll**; `$sessionOk` never assigns, so the gate can never pass |

⭐ **Only the gate site is loud. The other three fail silently by producing a wrong string** — which is worse, and is why a fix that only chases the stack trace is a bad fix.

### 0.2 How the box got two processes — the actual root cause

**`Invoke-Rollback` restores WITHOUT stopping the app first.** Measured live 2026-08-22 (see §3.2):

```
FAIL RESTORE ITSELF DID NOT CONFIRM ... The process cannot access the file
'C:\DeribitEngine\DeribitVerdictEngine.exe' because it is being used by another process.
```

Deploy step 8 restarts the app; the gate then fails; `Invoke-Rollback` tries to overwrite the binary it just relaunched. The restore aborts on the lock, `Start-RemoteApp` runs anyway, and the box ends with **two** collectors writing the same book and tape.

⚠ **Why three prior reviews missed it:** attempts 1–2 (recorded in [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §0.3/§0.3b) rolled back from a **pre-restart** failure — a hash mismatch, when the app was still stopped from step 4. The gate-timeout path is the **post-restart** rollback, and it is the only one that leaves the app running. **The two paths differ in exactly the property that matters, and only one had ever been exercised.**

⛔ **FIX 10's `robocopy /MIR` restore STILL has never successfully executed.** It aborted on the lock before doing any work. [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §7's gap has moved, not closed.

### 0.3 The fix, in order

1. **Extract the stop-and-confirm block** from `Invoke-Deploy` step 4 (`collector.ps1:477-485`) into a `Stop-RemoteApp` function, and **call it from `Invoke-Rollback` before `Restore-DeployBackup`.** That is the root-cause fix; the rest are defence in depth.
2. **`Start-RemoteApp` must refuse to launch when an instance already exists.** Its callers reach it only after a confirmed stop, so a live process means an earlier step failed — launching a second is never the right answer.
3. **Guard all four sites on COUNT.** Report the process count explicitly, and treat `count <> 1` as a *named* failure ("2 instances running") rather than casting an array to `[int]` or concatenating it into a path.
4. **Sweep, do not spot-fix.** `grep -n "Get-Process DeribitVerdictEngine" tools/ops/collector.ps1` returns six hits across four functions. This project has burned this exact lesson before — FIX 2 / FIX 7 / FIX 8 were three instances of one class, and the sweep that followed found a fourth.

### 0.4 ⚠ How to verify it, and why the obvious way is wrong

**Testing a rollback fix requires a deploy that FAILS.** Do not do that against the new production box.

⭐ **Use the retired old box as the test target.** It is now ideal, and for a non-obvious reason: **it runs v66, which has no `auto_run.start_engaged`, so it comes up running-but-idle and will NEITHER collect NOR fork the book.** Its data is archived and snapshotted, so it is fully disposable.

1. `aws ec2 start-instances --region eu-west-2 --instance-ids i-08c740e22d507667d`
2. Deploy a knowingly-bad build to it (the V2 recipe in §3.3 below) → the gate must fail → **watch the rollback actually stop the app, restore, and relaunch ONE instance.**
3. Confirm exactly one process afterwards, and that `robocopy /MIR` genuinely ran.
4. Stop the instance again.

⛔ **Do not deploy anything during the step-8 watch** (§1.2). Writing and reviewing the fix during the watch is fine — it is a local script with no engine change. **Verifying it needs a deploy, so verification waits until the watch completes.**

---

## 1. The collector migration — DONE

**The t2.micro `i-0d6c133058876273e` IS production.** The old t3.small `i-08c740e22d507667d` is stopped and retired.

### 1.1 What was executed, with evidence

Procedure: [`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) §5.4, in order, not reordered.

| Step | Evidence |
|---|---|
| 1 — fetch + verify | Ran twice. Final pre-stop fetch at **15:36:26Z**, cutting the delta to 67 s |
| 2 — stop old production | **15:37:33Z**, asserted `POST_COUNT=0` |
| 3 — capture the final delta | 6/6 items **hash-for-hash**; delta was exactly 1 book row, 1 eval-cache row, 3,796 B of tape |
| 4 — stop new box, delete its book + store | Host-guarded, delete asserted item by item |
| 5 — copy production's book + store across | Hash-verified **on the box** against production-computed SHA256s |
| 6 — verify the copy | Met and exceeded — hashes, not just sizes |
| 7 — start, confirm append + cadence | **Append proven**, gate passed, tape healed |
| **8 — watch a full day** | ⬜ **OUTSTANDING** |

**Carried state:** book 24,721 rows (first row `2026-07-22 16:24:54`), eval cache 44,910 rows, store **65,088,339 B across 2 files**, both sidecars.

### 1.2 ⭐ The tape gap healed completely — measured, not assumed

Production stopped 15:37:33Z; the new box started 16:02:54Z. **A 25-minute hole.** Gap repair fired once on start (`gap_repair_lookback_hours` is 20) and the result was:

```
WINDOW_ROWS=1586   15:25:01Z -> 16:04:48Z
MAX_TIME_GAP_SEC=69.1 at 15:38:38Z
SEQ_MIN=296958647 SEQ_MAX=296960232 DISTINCT=1586 SPAN=1586 MISSING=0
```

**Zero trades missing across the cutover.**

⚠ **The byte count nearly produced a false alarm and `trade_seq` settled it.** The store grew only ~54 KB where the morning's measured rate (~168 B/s) predicted ~270 KB. The market was simply quieter — 1,586 trades over ~40 min is ~0.66/s. **A volume- or time-based detector gives false comfort or false alarm here; `trade_seq` gives the answer.** That is D-2's whole point, now demonstrated twice on live data.

### 1.3 ⚠ A real ordering defect in the procedure document

[`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) §5.4 step 3 says *"re-`fetch` to capture the final rows written between step 1 and step 2."* **`fetch` cannot run with the app stopped** — `Invoke-Fetch` resolves the install dir from `Get-Process` and exits 1 (`collector.ps1:261`). **Step 2 makes step 3 impossible as written.**

**Worked around, not fixed:** a fresh `fetch` was taken immediately *before* the stop, and the post-stop delta was captured by a hand-run SSM snapshot. With the app stopped the files are static, so that capture is exact and race-free — safer than the running case FIX 7 exists to handle. **If §5.4 is ever run again, either patch `fetch` to accept an `-InstallDir` override or write the hand-run step into the document.**

---

## 2. ⛔ The v68 defect — the thing that would have ruined the migration

**v68's auto-run-on-start collected EXACTLY ONE analysis row per process start, then went silent for the life of the process.** Measured on the test box: one row at 11:12:03Z, then **175 minutes** of nothing while the WS tape kept capturing normally.

**Mechanism.** `InitAutoRunControls` sets `rbSingle.Checked = True` unconditionally (`UI/MainForm_AutoRun.vb:22`), and **SINGLE poisons every branch of `StartAutoRun`'s mode selection, not only the interval one.** In `on_close` mode the bar-close watcher starts normally and `RunAutoAnalysis` then disposes it on its own first fire (`UI/MainForm_AutoRun.vb:136`) — which also removes the feed-stall backstop, because the backstop lives inside the tick that no longer runs. Only the final `Else` branch repeats, and it needs `rbSingle` unchecked.

⭐ **Why nobody saw it:** a human clicking Start picks REPEAT by hand. **Auto-start had no such step, so the defect existed only on the capability v68 added.**

**Fix:** `rbRepeat.Checked = True` immediately before the `StartAutoRun` call (`UI/MainForm_Layout.vb:445`). Commit `9e9fe33`. No settings key, no version bump. Recorded inside the existing v68 row of [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) Section 15 — **a defect in that item, not a new version.**

⚠⚠ **A first diagnosis was WRONG and is worth knowing.** I attributed it to `StartAutoRun` racing the WS feed construction. It does not: `InitMarketDataSources()` runs at `UI/MainForm_Layout.vb:409`, **before** `StartAutoRun()` at 445, so the feed exists and on-close activates normally. **The wrong diagnosis produced a plausible fix that would have changed nothing.** Reading the call site beat reading the file top-to-bottom.

---

## 3. The acceptance gate was blind to it — fixed and PROVEN

### 3.1 The gap

[`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) §2.6 defined acceptance as **one** new CSV row within 5 minutes. **A single-shot start satisfies that exactly.** The gate passed the v68 deploy and [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §7 recorded *"Part A is proven in production conditions"* on the strength of it. **The gate did not merely miss the defect — it reported the opposite.**

**Now:** ≥2 rows, ≥45 s apart, 12-minute deadline. Spec: [`deploy-acceptance-gate-cadence-spec.md`](deploy-acceptance-gate-cadence-spec.md). Packet: [`deploy-acceptance-gate-cadence-spec-back.md`](deploy-acceptance-gate-cadence-spec-back.md). Commit `9eb3329`.

⚠ **The deadline is the load-bearing half.** ASIA and LONDON run 3-minute `execution_resolution`, so two rows can legitimately take ~7 minutes. **Raising the row count while keeping 5 minutes would false-fail healthy boxes and trigger a rollback on a box that was fine.**

### 3.2 V2 — the negative case, PASSED

Deployed a build **without** the fix. 28 polls across the full 12 minutes, every one `rowsAfterRestart=1 spanSec=0`. **Gate correctly failed, exit 2.**

### 3.3 V1 — the positive case, PASSED

```
poll: rowsAfterRestart=0 spanSec=0
poll: rowsAfterRestart=0 spanSec=0
poll: rowsAfterRestart=1 spanSec=0     <-- the OLD gate would have PASSED here
poll: rowsAfterRestart=1 spanSec=0
poll: rowsAfterRestart=1 spanSec=0
poll: rowsAfterRestart=2 spanSec=61    <-- the NEW gate passes here
```

⭐⭐ **A controlled experiment: same box, same tooling, same gate, one line of engine code as the only variable. The old one-row gate would have passed BOTH.**

**To reproduce the negative build:** on a throwaway branch, `git checkout 9e9fe33~1 -- UI/MainForm_Layout.vb`, commit, push (pre-flight rejects a dirty or unpushed tree), `dotnet build -c Release`. Delete the branch afterwards.

---

## 4. State — verified, not inherited

| Thing | State |
|---|---|
| **New production** `i-0d6c133058876273e` | t2.micro, Server 2019, `C:\DeribitEngine`, **v68 + the rbRepeat fix** (exe built 2026-08-22 15:27Z), collecting at ~0.6 min gap |
| **Old production** `i-08c740e22d507667d` | **`stopped`** 16:43:34Z. Volume `vol-03c79bb3716c94809` attached and intact |
| **Termination protection** | ✅ **`DisableApiTermination = True`** — set because `DeleteOnTermination` is `true`, so one mistaken terminate would have destroyed the volume with no confirmation step |
| **Snapshot** | ✅ **`snap-0a17195c58e850cbd`**, `completed` 100%, 30 GB, standard tier |
| **Local archive** | `C:\Dev\collector-cutover-2026-08-22\production-full-archive\` — **all 28 files, hash-verified**, 112,328,075 B |
| **Local cutover copies** | same parent dir: the 14:03 fetch, the 15:36 pre-stop fetch, and the hash-verified `final\` set |

⭐ **Four independent layers stand behind the irreplaceable tape:** the running new box, the stopped old volume, the snapshot, and three verified local copies.

**Snapshot tier:** standard, deliberately. Archive tier is ~4× cheaper per GB but carries a 90-day minimum and a **24–72 h restore**, useless as a backstop during a watch period. **`modify-snapshot-tier` after step 8 passes** if the saving is wanted.

⚠ **The archive holds things the migration deliberately did NOT carry** — `analysis_output_dump.md` (16.8 MB), production's **v66 `settings.json`**, the July `analysis_report_*` / `analysis_summary_*` pairs, `ohlc_1m_cache.csv`, `tools\ws_trade_probe_20260811-144742.csv` from the `trade_seq` investigation, and `_manual_backup\`. None irreplaceable; all preserved.

---

## 5. Lessons, each earned by an error made this session

### 5.1 ⚠ A background task's reported exit code is the LAST command's, not the one that matters

Bit twice in one session. The V2 deploy notification said **"exit code 0"** while the output held `V1_DEPLOY_EXIT=2`. The snapshot waiter notification said **"exit code 0"** while the output held `WAIT_EXIT=255 — Max attempts exceeded`. **Both times the real answer was inside the output.** A trailing `echo` swallows the status of everything before it. **Read the output, never the summary line.**

### 5.2 ⛔ A checker that reports success it never performed — THREE instances, all mine, all in one session

| Checker | Claimed | Actually |
|---|---|---|
| The v68 acceptance gate | box healthy | one row, then 175 min of silence |
| My gate-review harness | `ALL CASES PASSED` | **9 of 10 cases never ran** — `R` is the built-in alias for `Invoke-History` and outranks a function |
| My archive verifier | `ARCHIVE VERIFIED — all 0 files` | a path substitution failed; the loop ran zero times |

⭐ **The fix that works is the same every time: assert the check RAN.** Both harnesses now throw when the item count is zero or short of expected. **"It didn't complain" is not "it passed."**

### 5.3 A ready explanation for a surprising number is a warning sign

The tape grew 54 KB where I predicted 270 KB. The tidy explanation would have been "gap repair failed." **Measuring `trade_seq` instead returned `MISSING=0`** — the market was quiet. [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §5.1 records the same trap catching the prior seat four times in one day.

### 5.4 Execution finds what parsing structurally cannot — now SEVEN for seven

FIX 1, 6, 7, 8, 10, plus this session's rollback-lock defect and the two-instance defect. **Every one was found by running `collector.ps1`. None was reachable by reading it, and several survived multiple careful reviews.** Do not accept "it parses" or "I traced it" for anything in that file.

---

## 6. What is NOT done

- ⬜ **Step 8** — a full day of `status` on the new collector. **This is where V4 belongs** ([`deploy-acceptance-gate-cadence-spec.md`](deploy-acceptance-gate-cadence-spec.md) §5): the box is now in its final production state, so a soak here means something. **Do not deploy during it.**
- ⛔ **The four-site defect class** — §0 above.
- ⛔ **The rollback path has still never completed successfully**, and `robocopy /MIR` has still never executed.
- **Open trader decisions, untouched:** the absorption D-table ([`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 — attack `pullFrac` first), the CLI-port reversal not yet written into [`roadmap.md`](roadmap.md), and `_evalCache` (still no spec; **do not "just trim it"** — see [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §4).
- **Cosmetic:** `tools/checks/verify-gate.ps1:144` prints *"no engine-path change"* on commits that change engine behaviour. Its engine prefixes are `Core/`, `DynamicNorms.vb`, `analysis/` — `UI/` is excluded, correctly, because the check is a *settings-version-bump* nudge and a new key must touch `Core/Settings/EngineSettings.vb`. **The logic is sound; only the message overclaims.** Reword if that file is ever touched.
