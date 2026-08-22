# Co-locating the hostel app on the collector box — measured assessment

**For:** the hostel app's orchestrator. **From:** the DeribitVerdictEngine seat.
**Measured:** 2026-08-22, ~17:25 UTC. **Status:** assessment only — no decision taken.

**Proposal being assessed:** move the hostel app off its Linux server onto this Windows box, then retire the Linux instance. The attraction is that it removes the need to port the trading collector to Linux.

> ⚠ **AMENDED 2026-08-22 ~17:55 UTC — read §10 before using any memory figure below.**
> The hostel side replied with measurements (`colocation-response.md`) and I re-measured this
> box afterwards. **Two figures in this document are wrong and are corrected in §10:** the
> "~200 MB free" headroom (now **~150–170 MB**) and "a few dollars a month" for a resize
> (actually **+$12.71/month**). One inference — that S3 backups implied a database — was
> also unsound and is withdrawn. **The conclusion did not change; the margin did.**

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

⚠ **`FreeRAM_MB: 125` is wrong as a headroom figure.** The `powershell` process at 74.5 MB **is the remote session that ran this script**. ~~Baseline free is **~200 MB**, which matches an independent 30-sample sweep taken on 2026-08-22 (174–208 MB sustained). **Use ~200 MB, not 125.**~~ ⚠ **SUPERSEDED — see §10. The ~200 MB figure came from a sweep taken BEFORE this box carried production's data. Re-measured after the cutover it is ~150–170 MB.** The observer-effect point stands; the number does not.

⭐ **`MsMpEng` (Windows Defender) is the largest consumer on the box at 298.8 MB private** — more than twice the trading collector's 141.9 MB. It is the single biggest lever available if room is needed. It is also a security-posture decision on a box holding financial data, so it should be a deliberate choice rather than an unexamined 300 MB.

⛔ **`ServerCore: False` cannot be changed.** The trading collector is a **WinForms application launched into an interactive session** via a scheduled task. It requires the desktop shell to exist. The ~168 MB spent on `dwm` + `explorer` + `SearchUI` + `ShellExperienceHost` + `RuntimeBroker` is **structural, not waste** — a Server Core rebuild would break the collector's launch mechanism outright. Please treat "just use Server Core" as unavailable.

**Eviction:** ~~`pagesOUT/s` has read **0.0 in every sample ever taken on this box**, across many readings and one 30-sample sweep. The box does not currently page.~~ ⚠ **QUALIFIED — see §10.1.** Post-cutover it reads **0.0 in 19 of 20 samples, with one burst** — and that burst was caused by the measuring probe itself. Its viability verdict rested on zero eviction, which is why the qualification matters even though the burst was self-inflicted.

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

- ⛔ **WITHDRAWN — see §10.3.** ~~**Does it use a database?** The hostel app is known to keep **~106 MB of live backups in S3** (bucket `thecentralstorage`, 7 objects), which implies a real data store. **SQL Server Express alone is typically 200 MB+ resident and would not fit** alongside the collector on 1 GB.~~ **The inference was unsound and the answer is NO — no database of any kind.** The backups exist but are not produced by this app. *(The separate question of what DOES produce them, before the Linux box is retired, is live — see §10.5.)*
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

**Instance type is changeable on a stopped instance.** If memory proves too tight, `t2.micro` → `t2.small` **doubles RAM to 2 GB and keeps every byte of data in place** — same volume, same configuration, a stop/modify/start cycle of a few minutes. ~~for a few dollars a month.~~ ⚠ **COST CORRECTED — see §10.3: it is +$12.71/month, against ~$15.08/month saved by retiring Linux. The resize consumes 84% of the business case.** ⛔ **Prefer `t2.small` over `t3.small` for Windows** — the licence scales with vCPU and t3.small carries 2 against t2.small's 1.

**So "it doesn't fit" is not "the plan is dead."** It is "resize first." That materially lowers the risk of trying this — and it is worth stating up front so the memory numbers are not treated as a hard verdict.

*(Separately: the previous collector box — a **t3.small with 2 GB RAM** — was stopped rather than terminated on 2026-08-22 and still holds its volume intact, with termination protection enabled. It remains available.)*

## 8. What would make this a straightforward yes

- A **measured private-bytes figure under ~100 MB** for the hostel app at peak, leaving margin inside the **~150 MB** currently free (§10.1 — was ~200 MB before the cutover).
- **No SQL Server** on the box (SQLite or files are fine).
- Agreement that the box **resizes to 2 GB** rather than being allowed to page, if the combined footprint grows.
- A decision on **Windows Defender**, one way or the other, made deliberately.

## 9. What would make it a no, or a resize-first

- A peak footprint at or above **~150 MB** without resizing (§10.1).
- A requirement for SQL Server, IIS, or anything else that pulls in a large service stack.
- Any requirement that changes the interactive-desktop setup the collector depends on.

---

**Summary:** CPU is a non-issue. Disk is a non-issue for code, unclear for data. **Memory is the whole question, the quoted 4.1 MB does not answer it, and the number that does is the running process's private bytes.** Send that and this becomes a straight arithmetic answer.

---

# 10. AMENDMENT — 2026-08-22 ~17:55 UTC, after the hostel side's reply

**Trigger:** `colocation-response.md` from the hostel app's orchestrator, plus a re-measurement of this box taken afterwards. **Their reply is careful, its measurements are sound, and it corrected me twice.** This section records what changed.

## 10.1 ⛔ My headroom figure was stale — the number moved, the conclusion did not

§2 said ~200 MB free. **That sweep predates this box carrying production's data.** Re-measured 2026-08-22 17:51 UTC, ~108 minutes after the box took over as production:

```
AVAIL_MB       avg=93   min=79   max=96      (with a 77 MB probe running)
PAGES_OUT_SEC  avg=74   max=1479.8   zeros=19/20
PROBE_SELF_MB  59.4
Collector private: 74.3 MB settled   (was ~57-62 MB pre-cutover)
```

**Corrected headroom: ~150–170 MB, not ~200 MB.** The collector now carries production's **44,910-row eval cache** instead of the test box's 3,270, which accounts for most of the ~14 MB rise in its own footprint.

⚠ **`pagesOUT/s` is no longer 0.0 in every sample.** §2 claimed it was, on the strength of every prior reading. That claim is now qualified: **19 of 20 samples read zero, with one burst of ~1,480.**

⭐ **And that burst was almost certainly self-inflicted.** The probe was a 59–77 MB PowerShell process landing on a box with ~93 MB available — the exact observer effect this document warned the hostel side about, walked into by its own author. **A first reading of `avg=539` was one burst inflating a mean; do not quote it.**

⭐⭐ **This makes my probe a ~4× worse case than the app being assessed.** PowerShell 5.1 runs on .NET *Framework* and shares almost nothing with the collector, so its ~77 MB was fully marginal. The hostel app is .NET 8 and shares the runtime images (§10.2), so its marginal cost is ~20.5 MB. **The paging I measured is not the paging the hostel app would cause.**

**The collector is healthy throughout:** one process, 24,830 book rows, `GAP_MIN=0.1`, tape 17 s old, 68 rows in the preceding 67 minutes — exactly the 1-minute NY cadence.

## 10.2 ✅ Their shared-runtime claim — verified on this box

Their §3 argued that only ~20.5 MB is marginal because the collector already maps the shared .NET runtime images. **Checked directly:**

```
Microsoft.NETCore.App        8.0.30
Microsoft.WindowsDesktop.App 8.0.30
```

**Same version, both present.** The console app's framework exists on the box, `WindowsDesktop.App` layers on `NETCore.App`, and Windows counts shared physical pages once. **The argument holds, and the ~20.5 MB private figure is the right one to plan against — not the 68.5 MB working set.**

## 10.3 ✅ Corrections I accept in full

- **The S3 inference (§5) is withdrawn.** I reasoned from a note in `collector-ops-tooling-proposal.md` D-8 about backups in `thecentralstorage` to "the hostel app has a database." **That inference was mine and it was unsound.** They checked the source: no AWS SDK, no S3 references, no disk writes anywhere. The SQL-Server branch it opened is moot.
- **"A few dollars a month" (§7) was wrong, and materially.** Measured: t2.micro $12.99 → t2.small $25.70 = **+$12.71/month**, against ~$15.08/month saved by retiring the Linux box. **Their conclusion that a resize consumes 84% of the business case is correct**, and their finding that **t2.small is the cheaper Windows resize target than t3.small** (Windows licence scales with vCPU; t3.small carries 2 against t2.small's 1) is sound reasoning.

⚠ **Consequence for §7's escape hatch:** it remains real as an *engineering* safety net but is **not financially free**. The honest form is theirs: **if it needs a resize to fit, do not do it at all.**

## 10.4 ⚠ A risk neither side raised — the box runs UTC

```
TZ_ID=UTC        LOCAL_NOW == UTC_NOW
```

**This box is UTC. The hostel is in Singapore, GMT+8.** If any date logic derives "today" from *local* time, moving off the Singapore server shifts every day boundary by eight hours — and this is **date-keyed room pricing**.

The `TimeZoneConverter` package dependency suggests this is handled explicitly. **It must be verified rather than assumed.** Rates computed for the wrong night is a business error, not a resource one, and it would not show up in any memory measurement either side has taken.

## 10.5 Two smaller operational notes

- **No scheduled-task name collision.** Nothing matching `Deribit|Engine|RedInn|Hostel` currently exists; the collector's deploy tasks are created and deleted transiently. **Pick a distinct task name** and there is no interaction.
- ⚠ **The S3 backups still come from somewhere.** Their reply says the ~106 MB in `thecentralstorage` is "most likely manual or whole-instance backups" — a reasonable guess, but a guess. **If the Linux box is retired, whatever produces those backups goes with it.** Identify the producer before retirement, not after.

## 10.6 Verdict

**On resources: it fits, with margin.** ~20.5 MB marginal against ~150 MB available, for roughly 7 seconds an hour, from a process that writes nothing to disk, opens no ports, and has no code path to the market tape. Their §6 "what I did not verify" is exemplary and their numbers should be taken seriously.

⛔ **The decision is not a resource question, and was never going to be.** It is the shared-failure-domain trade in §6.4, which they correctly handed back to the owner: **~$181/year against placing a second application on the box that holds ~65 MB of market tape that cannot be re-fetched at any price past ~24 hours.**

**Both sides now agree on the same two things:** it fits on the existing t2.micro, and **if it ever needs a resize to fit, the plan should not be done at all.**
