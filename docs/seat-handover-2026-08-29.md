# Seat handover — 2026-08-29

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) — superseded for STATE, and it is LONG. ⭐ Its §5 is the full hostel-app record and its §1.1/§1.1a/§1.1b are the memory-baseline corrections; both are still the authoritative DETAIL. This document does not repeat them.**

**Settings: v68.** ⛔ **Run `git status -sb` — never inherit a push state from this line.** It was wrong in the last two handovers, both times because the trader pushed mid-session.

⚠ **ALL DATES AND TIMES HERE ARE UTC.** The machine is GMT+8.

---

## 0. ⭐ FIRST TASK — pick one; nothing is blocked and nothing is owed by the trader

**Three are ready to build. None depends on the others.**

| Pick | Item | Model / effort |
|---|---|---|
| ⭐ **A** | **Item 21 — let `coverage` be aimed at a copy-back.** Smallest, and it unblocks every future audit | **Sonnet, low** |
| **B** | Item 6 — `WsTradeProbe` through the shared trade reader (S-1) | Sonnet medium |
| **C** | Item 8 — **A54a**, the JSON↔POCO reflection drift guard. ✅ Already RULED 2026-08-11 (option d + scoped b) | Sonnet medium |

⭐ **A is recommended.** `HistoricalStore.StoreDir` is the const `"backtest_data"` resolved against the working directory, and `coverage`'s evidence paths come from `Directory.GetCurrentDirectory()`. **Three separate ways of setting a child process's working directory failed to redirect it**, so the 2026-08-29 collector-health numbers had to be computed from the store file by hand. ⛔ **An instrument built to audit copy-backs that cannot be aimed at one is why nobody had audited this box in 14 days.** `CoverageReport.BuildResult` **already takes a `storeDir` parameter** — only the CLI hardcodes it, so the fix is an optional `--store-dir` threaded to the existing argument. **The evidence paths need the same treatment or the fix is half a fix.**

**Awaiting a TRADER decision, not a build:** the **absorption mechanism revision** ([`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 D-table). **Opus, high.** Do not open an implementer session until it is ticked.

---

## 1. State — verified 2026-08-29, not inherited

| | |
|---|---|
| Settings | **v68**, tracked `settings.json` line 2 |
| Next free fixture family | **A60** |
| Next free hard constraint | **HC29** |
| Production collector | **`i-0d6c133058876273e`**, one process life since **2026-08-22 16:02:46 UTC**, no restart |
| Gap-repair anchor | 04:02:46 · 10:02:46 · 16:02:46 · 22:02:46 UTC — ⚠ **derived from process start; re-read it** |
| Freshest copy-back | ⭐ **`AWS-copybacks/aws-copyback-2026-08-28/aws_fetch/20260828-153838/`** — book to `2026-08-28 15:38:03`, store 108.3 MB |
| Boundary state | Last ⚠ boundary was v66 (2026-08-10). Everything since is non-boundary |

### 1.1 ✅ The collector is HEALTHY — measured 2026-08-29, not assumed

| Measure | Result |
|---|---|
| Row cadence, post-migration | **37.0/hour** over 160 h vs the **36.9** migration baseline |
| `trade_seq` completeness, post-migration | **99.9953 %** — 33 missing of a 709,579 span |
| Feed incidents since migration | 2 DEGRADED + 1 DOWN — **none cost tape** |

⛔ **The migration watch's "zero DEGRADED" is STALE — do not quote it.** Three incidents have occurred since.

⭐ **The 2026-08-13 downtime-repair prediction HELD on its first real test.** The 08-27 00:51→00:54 DOWN is a **genuine feed outage, not a restart** — the instance id is identical either side, where every prior DOWN carries a new one. Across the outage window: **188 rows, 188 distinct `trade_seq`, span 188, MISSING 0.**

⚠ **The 33 missing sit in exactly two runs and BOTH post-date the last repair pass**, so no hole has survived a full cycle. **One run brackets the 10:02:46 pass instant to the second — a ~1-in-800 coincidence — but 22 of 23 passes produced no gap, so it is not a reliable effect. Unattributed; re-measure on the next copy-back.**

---

## 2. ⭐ THE OUTSTANDING ITEMS

**All of §2 in [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) carries forward unchanged except where noted below.** Items 6–17 stand as written there.

**Changed since:**

| # | Item | State |
|---|---|---|
| ~~5~~ | C1-coverage F1 | ✅ **SHIPPED AND PUSHED** (`4032f9c`), reviewed and accepted, three findings recorded |
| **20** | AWS copy-back + coverage run | ✅ **DONE 2026-08-28.** One SSM attach, all transfers verified |
| **21** | `coverage` cannot be aimed at a copy-back | ⛔ **NEW — §0 above, recommended first task** |

⛔ **Gated, and the gate MOVED:**

- **Kelly dated trigger + W6-4 re-run — READ 2026-08-29: NOT MET. 311 weekday STRONG against ≥406.** ⚠ **The old "ETA ~2026-08-30" is wrong.** Measured against the 201 on file at the 2026-08-01 read, the realised rate is **~5.8 STRONG/weekday, under half the projected 12.4** — putting the 95-row shortfall around **mid-September**. ⚠ **The NOT-MET verdict is robust; the RATE assumes the two counts are the same population, which was not verified.** **Re-read the count before scheduling — two lines of awk over the book's `Verdict` column.**
- **A4** liquidation × OFI — market-gated. **A5** VPFR shape — data-gated. **Downtime-repair Part B** — ⛔ still forbidden; Part A has now healed one real outage, so **the G-1 gate can be re-argued, but it has not been.** **Auto-tweaker first live fire** — data-gated.

---

## 3. What shipped this session

| Item | Commit |
|---|---|
| **C1-coverage F1 trailing-edge fix** | `4032f9c` — the only source commit; everything else is docs |
| F1 spec amendments, ruling, review record | `3d0b8e1` · `eb7d9bf` · `2fdae32` · `e498a0e` |
| Memory-baseline withdrawal and corrections | `f77cd52` · `e181c9e` · `6a83d95` |
| Shared-workstation rule + hostel-app record | `1872b44` · `a712e49` · `5a6e6bc` · `ce63744` · `45898da` · `8116cf6` · `b2d90eb` |

---

## 4. ⛔ The hostel-app thread is CLOSED. Two things bind past it

**Full record: [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) §5.** Their migration cut over 2026-08-29. **Nothing of theirs runs on our collector but an hourly 7-second task at `:25`; nothing is owed either way.**

**1. ⭐ THE SHARED-WORKSTATION RULE — still binding, both sides:**

> **Announce before setting MACHINE-SCOPE STATE** on the workstation — scheduled task, service, machine env var, registry, firewall. **Anything that outlives the process that created it and can act later.** Everything else on your own tree is free.

**Their inventory is readable at** `C:\Users\user\source\repos\RedInnDynamicPricingLinux\docs\shared-workstation-inventory.md`. ⚠ **Trustworthy from 2026-08-27 forward; reconstructed before it.**

**2. ⛔ UNATTRIBUTED, AND IT SHOULD STAY THAT WAY.** The collector box's burst rate moved **9.10 % → 14.20 %** and available memory fell **15–25 MB** between two 24-hour windows. **Their app is excluded by three independent tests.** The 15:30 excursion **is** attributed — a Defender signature-update install, from the event log — ⛔ **but that runs identically in both windows and CANNOT explain the delta. Never let the excursion attribution be read as the delta attribution.**

⚠ **n = 2 days, and we hold no day-to-day variance baseline for this box** — everything we have predates it. **What is supported: two windows differ materially and nobody can say why. Nothing more.**

---

## 5. Lessons — twelve between two seats in five days, and the pattern is the finding

⛔⛔ **NOT ONE WAS CAUGHT BY CARE. Every single one was caught by a CONTRADICTION** — identical values labelled as mismatches, arithmetic pointing the wrong way, a date predating the project, a status line disagreeing with an exported definition, a list sitting under the wrong heading. **Vigilance has never once been the mechanism. Build things that can contradict themselves, then look at the contradiction.**

**Ours this session:**

1. ⛔ **A truncated view mistaken for the whole — THREE TIMES.** We pooled 80 minutes and called it a baseline; pooled 10 hours across a 3×-varying range and called it a session; then read a 60-value range off the 8 rows we happened to print. **A looking failure, not a reasoning failure.**
2. ⛔ **We deleted a section header by using it as an edit anchor.** The edit reported success because the anchor matched — it succeeded at something other than what we meant. **Read the result, not the exit status.**
3. ⚠ **We turned our own doc line into a data claim** without checking it against the data in front of us.
4. ⭐ **Conceding a limit we could have argued is what made a prediction land as caught rather than as a surprise** (the `plaRunOnce` cross-build split — we were half right, and the half we conceded was the half that decided it).
5. ⚠ **An instrument that cannot be aimed produces no answer, and no answer looks like nothing being wrong.** Four days of memory measurement happened because the cheaper check could not be run.

---

## 6. Loose ends

- ⛔ **Commits unpushed — verify with `git status -sb`, do not inherit.** All `[no-engine-change]` except `4032f9c`.
- ⚠ **`tools/AutoTweaker/state.json` and `tools/mem-probe.csv`** are untracked but have **explicit named `.gitignore` entries** (lines 374 and 433) — **deliberately ignored, not accidentally. That loose end is closed.**
- ⚠ **`ssm-mem.json` / `ssm-memprofile.json`** sit untracked in the repo root — single-shot probes from the old box, superseded. **Commit or ignore deliberately.**
- **`DeribitWsFeed` `ResetBufferState` race (F2)** and the collector `User-Agent` (F3) remain open.
- ⚠ **A silent-fallback residual we share with the hostel app's N8:** our settings banner covers a **parse failure** only. A `settings.json` that parses cleanly while **missing a block** produces no exception, no banner, and the seed applies silently — [`trader-tick-queue.md`](trader-tick-queue.md) §0a, unfixed since 2026-08-11.
