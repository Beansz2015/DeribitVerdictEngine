# Co-locating the hostel app on the collector box — measured assessment

**For:** the hostel app's orchestrator. **From:** the DeribitVerdictEngine seat.
**Measured:** 2026-08-22, ~17:25 UTC. **Status:** assessment only — no decision taken.

**Proposal being assessed:** move the hostel app off its Linux server onto this Windows box, then retire the Linux instance. The attraction is that it removes the need to port the trading collector to Linux.

---

## 1. The box, as it stands today

| | |
|---|---|
| Instance | `i-0d6c133058876273e`, **t2.micro**, eu-west-2b |
| OS | Windows Server 2019 Datacenter — **full desktop, NOT Server Core** |
| RAM | **1024 MB** |
| Disk | 30 GB gp3, **8.5 GB free on C:** |
| Role | Became the **production** trading collector at 2026-08-22 16:02 UTC (it was a test box until then) |

⚠ **This box is ~7 hours old in its current role.** It took over as production during a cutover the same day. Any historical metric older than that describes a different workload.

## 2. Measured memory

```
TotalRAM_MB    : 1024
FreeRAM_MB     : 125
CommitUsed_MB  : 1546
CommitLimit_MB : 2853
FreeDisk_C_GB  : 8.5

Name                 Private_MB WorkingSet_MB
MsMpEng                   298.8         197.9
DeribitVerdictEngine      141.9         111.5
powershell                 74.5          82.5
dwm                        54.1          23.6
SearchUI                   35.7             0
explorer                   34.5          25.3
svchost                    28.5          34.3
ShellExperienceHost        23.5           8.1
ssm-agent-worker             23          22.6
RuntimeBroker              20.5          12.5
amazon-ssm-agent           18.5          13.5
ssm-document-worker        17.7          21.2
```

### Three things to read carefully

⚠ **`FreeRAM_MB: 125` is wrong as a headroom figure.** The `powershell` process at 74.5 MB **is the remote session that ran this script**. Baseline free is **~200 MB**, which matches an independent 30-sample sweep taken on 2026-08-22 (174–208 MB sustained). **Use ~200 MB, not 125.**

⭐ **`MsMpEng` (Windows Defender) is the largest consumer on the box at 298.8 MB private** — more than twice the trading collector's 141.9 MB. It is the single biggest lever available if room is needed. It is also a security-posture decision on a box holding financial data, so it should be a deliberate choice rather than an unexamined 300 MB.

⛔ **`ServerCore: False` cannot be changed.** The trading collector is a **WinForms application launched into an interactive session** via a scheduled task. It requires the desktop shell to exist. The ~168 MB spent on `dwm` + `explorer` + `SearchUI` + `ShellExperienceHost` + `RuntimeBroker` is **structural, not waste** — a Server Core rebuild would break the collector's launch mechanism outright. Please treat "just use Server Core" as unavailable.

**Eviction:** `pagesOUT/s` has read **0.0 in every sample ever taken on this box**, across many readings and one 30-sample sweep. The box does not currently page. Its viability verdict rested on that.

## 3. CPU — not a constraint

`CPUCreditBalance`, 1-hour resolution:

| Period | Balance |
|---|---|
| 2026-08-20 14:25 → 08-21 19:25 | steady climb, 29 → 144 |
| **2026-08-21 20:25 → present** | **pinned at 144.0** — the t2.micro maximum, ~21 hours |
| Dips | five, all **< 1 credit** (min 143.11), each recovered within the hour |

⭐ **The box earns credits faster than it spends them and sits at the cap. It cannot throttle at the current load.** Peak CPU was measured at 52.5 % of 1 vCPU.

⚠ **Two caveats, or this reads better than it is:**

1. **There is no week of history.** The instance launched 2026-08-20, so the series is ~2 days, not 7.
2. **The workload changed on 2026-08-22.** Before 16:02 UTC this box ran a lighter test load on a 30 MB data store, and between 11:12 and ~14:48 its analysis loop was **stopped by a defect**, which understates CPU badly for that stretch. **Only the final ~1.5 hours reflect production load.** That window reads 144.0 — encouraging, but it is one hour.

**Conclusion: CPU has ample headroom. Memory is the binding constraint.**

## 4. ⚠ The 4.1 MB figure is the wrong metric

The footprint quoted was *"the entire hostel app's DLLs running at any time is about 4.1 MB."* That is assembly size on disk, not runtime memory.

**Evidence from this very box:**

| | On disk | Running |
|---|---:|---:|
| `DeribitVerdictEngine.dll` | **1.07 MB** | **141.9 MB private** |

A ratio of roughly **130×** — CLR runtime, GC heap, JIT-compiled code, network and file buffers. If the hostel app is .NET, 4.1 MB of DLLs is consistent with anything from ~60 MB to well over 200 MB resident.

**What is actually needed to answer the question:**

- **Private bytes and working set of the running process(es)** under normal load, and at peak.
- Whether it is **one process or several**.
- **Managed heap size** if it is .NET, since that is what grows under load.

## 5. Other unknowns that matter more than the DLLs

- ⭐ **Does it use a database?** The hostel app is known to keep **~106 MB of live backups in S3** (bucket `thecentralstorage`, 7 objects), which implies a real data store. **SQL Server Express alone is typically 200 MB+ resident and would not fit** alongside the collector on 1 GB. SQLite or file-based storage would be a very different answer.
- **Does it need IIS or a web front end?** IIS worker processes add 50–100 MB and pull in more of the OS.
- **Background services or scheduled jobs**, and their peak footprint rather than idle.
- **Data on disk.** 8.5 GB free is plenty for 4.1 MB of code, but not necessarily for a growing database plus its backups.
- **Ports and firewall**, if anything listens externally.

## 6. Non-negotiables on the collector's side

These are not preferences; violating them breaks the trading system.

1. **The desktop session must persist.** The collector runs WinForms in interactive session 2. Anything that forces Server Core, or that disturbs the interactive session, stops collection.
2. **The box must not begin paging.** Its entire viability assessment rested on zero eviction across every sample. Sustained `pagesOUT/s > 0` is the failure signal.
3. **The market tape is unrecoverable past roughly 24 hours.** The collector currently holds ~65 MB of trade tape going back to 2026-07-22 that cannot be re-fetched from the exchange at any price. Anything that risks stopping the process for an extended period risks permanent data loss.
4. ⚠ **Consolidation creates a shared failure domain.** Today the trading collector and the hostel app fail independently. On one instance, one failure takes both — and only one of them has irreplaceable data.

## 7. ⭐ The escape hatch — this decision is not one-way

**Instance type is changeable on a stopped instance.** If memory proves too tight, `t2.micro` → `t2.small` (or `t3.small`) **doubles RAM to 2 GB and keeps every byte of data in place** — same volume, same configuration, a stop/modify/start cycle of a few minutes for a few dollars a month.

**So "it doesn't fit" is not "the plan is dead."** It is "resize first." That materially lowers the risk of trying this — and it is worth stating up front so the memory numbers are not treated as a hard verdict.

*(Separately: the previous collector box — a **t3.small with 2 GB RAM** — was stopped rather than terminated on 2026-08-22 and still holds its volume intact, with termination protection enabled. It remains available.)*

## 8. What would make this a straightforward yes

- A **measured private-bytes figure under ~100 MB** for the hostel app at peak, leaving margin inside the ~200 MB currently free.
- **No SQL Server** on the box (SQLite or files are fine).
- Agreement that the box **resizes to 2 GB** rather than being allowed to page, if the combined footprint grows.
- A decision on **Windows Defender**, one way or the other, made deliberately.

## 9. What would make it a no, or a resize-first

- A peak footprint at or above ~200 MB without resizing.
- A requirement for SQL Server, IIS, or anything else that pulls in a large service stack.
- Any requirement that changes the interactive-desktop setup the collector depends on.

---

**Summary:** CPU is a non-issue. Disk is a non-issue for code, unclear for data. **Memory is the whole question, the quoted 4.1 MB does not answer it, and the number that does is the running process's private bytes.** Send that and this becomes a straight arithmetic answer.
