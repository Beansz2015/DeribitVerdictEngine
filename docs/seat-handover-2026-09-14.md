# Seat handover — 2026-09-14 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-09-11.md`](seat-handover-2026-09-11.md)** — superseded for STATE; its §7 lessons still bind. ⚠ Its §2 absorption row and §3 row count were WRONG and are corrected in place there.

**Settings: v68**, untouched all session. ⛔ **Run `git status -sb` — never inherit a push state.** At close (2026-09-14 08:15 UTC): **clean, in sync with `origin/master`, 0 unpushed, last commit `1f005be`.**

---

## 0. ⛔ Before your first reply

- **Output format:** `C:\Users\user\.claude\CLAUDE.md` + memory `feedback_output_format_is_a_standing_rule`. Point form, tables, **never a bare section number or bare ID**, verified vs carried.
- **Decision rule:** `CLAUDE.md` *"AUTO-PROCEED ON YOUR OWN RECOMMENDATION"*. Take reversible calls and log them one line each. **Reserve** settings, scoring, rendered values, collector writes, schema changes, and any pick that is cheaper AND less truthful.
- ⛔ **Clock trap: GMT+8 workstation, UTC project.** This session the harness announced 09-12, 09-13 and 09-14 while UTC was a day behind twice. **Run `date -u` before your first dated claim.**

---

## 1. ⭐ FIRST TASKS

| # | Task | When | Notes |
|---|---|---|---|
| **1** | ~~⚠ **Get the trader's confirm or overrule on `RIDER-7`**~~ ✅ **DONE — the trader CONFIRMED `RIDER-7` carries with S2, 2026-09-14 (UTC)** | — | **Nothing is owed by the trader** |
| **2** | **Build S1** of [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) | From **2026-09-15 UTC** | **Opus, effort HIGH.** `D-2` + Stage 1 instrument + sidecar. **Commit locally, do NOT deploy** |
| **3** | **Final `D-1` episode-age read** | From **2026-09-16 UTC** (after Tue 09-15 closes) | Commands in §4. Expect the over-window share to stay ~21–32 % |
| **4** | **Build S2** — the rotation, 6 new columns, riders `RIDER-1`–`RIDER-7` | After S1 | **Opus, effort HIGH.** Must mark riders `CONSUMED` in [`csv-rotation-riders.md`](csv-rotation-riders.md) — the gate fails the push otherwise |
| **5** | **ONE deploy** after S2 | After `RIDER-2`b scripts merge | Build spec trap `T-7`: deploying earlier silently truncates the Kelly read. Record the `InstanceId` in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a |

⛔ **Do not offer any other spec as work.** Every earlier spec is BUILT.

---

## 2. What shipped — 9 commits, all `[no-engine-change]`, no `.vb` engine file, no `settings.json`

| Commit | What |
|---|---|
| `828d868` | Four stale state claims corrected: `CLAUDE.md` token figure (**80,999 tokens at 192,695 B**, measured), 09-11 handover §15 row count (21, not 24), queue STATE BANNER, the absorption "4-week tick" row |
| `36b38a3` | NEW [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) — the `D-6d` counting gap, two stages |
| `4ce5a10` · `b2ebae8` · `1bde9bb` · `edbddc7` | `D-6d.3` = **(c)**, `D-6d.1` = **(c)**, `RD-1` = **(b)** ruled. NEW [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md). §4.1 box that would have killed `D-2` refuted |
| `0098a3f` | NEW `tools/ops/absorption-episode-age-read.ps1` + [`absorption-episode-age-read-2026-09-13.md`](absorption-episode-age-read-2026-09-13.md); deploy ledger back-filled 3 rows |
| `3fb6eda` | All riders ruled in the build spec §4.4–§4.5; build spec traps `T-1`, `T-3` corrected; `T-6`, `T-7` added |
| `1f005be` | NEW [`csv-rotation-riders.md`](csv-rotation-riders.md) ledger + `tools/checks/rotation-riders.ps1` + `verify-gate.ps1` section 3c |

**Harness 376 ALL PASS · `GATE PASSED` on every push.** Next free fixture families: **`A78`, `A79`** (0 references in the harness, planned by the build spec).

---

## 3. The absorption arc — state

| Decision | Ruling | Where |
|---|---|---|
| `D-1`–`D-5` | TICKED 2026-09-01 | [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 |
| `D-6a` · `D-6b` | Ruled 2026-09-01 | same |
| `D-6c` | OPEN, gated on data (needs `SizeStart`/`SizeMin`), not on a tick | same |
| **`D-6d.3`** | **(c)** — `D-2` and `D-6d` Stage 1 ship together; Stage 2 after the read | [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §7 |
| **`D-6d.1`** | **(c)** — sidecar `absorption_episodes.log` AND column `AbsorptionShadowAggrUsd` (forces the rotation) | same |
| **`RD-1`** | **(b)** — separate `SettingsLoadError` column | build spec §9 |
| ✅ **`RIDER-7`** | **Carried** under `D-5` — `RecentTradeCount` column. **CONFIRMED by the trader 2026-09-14** | [`csv-rotation-riders.md`](csv-rotation-riders.md) |

**The rotation adds six columns:** `AbsorptionShadowAggrUsd` · `TriggerMode` · `WsHealth` · `SettingsVersion` · `SettingsLoadError` · `RecentTradeCount`.

⭐ **Mechanism facts a builder must not lose** (all in the build spec):
- `D-2` has **no downside branch**: `CloseEpisode()` (`Core/LevelAbsorptionTracker.vb:117`) and episode open (`:315`) clear the press queue, so episode-cumulative is a superset of what ships.
- The shipped ratio **already divides 10 s of pressing by a whole episode's depletion** on long episodes — `D-2` repairs that.
- ⛔ **Stage 2 before `D-2` makes `absorbRatio` FALL.** Never that order.
- ⛔ Never let `Press` survive a close while `SizeStart` restarts — the span artefact.
- `TriggerMode` = what FIRED the run (`MANUAL`/`INTERVAL`/`ON_CLOSE`/`BACKSTOP`), not `cfg.AutoRun.TriggerMode` (no hot-reload handler touches auto-run).
- `WsHealth` derived ONCE and shared by CSV, payload and `ws_health.log`.
- Production `analysis_log.csv.v0.7.bak` is the **111-column book, 2026-07-22 → 2026-09-01**, already mislabelled. Never rename it. New rotations name the book `<N>col-<hash8>.<UTC stamp>.bak`.

---

## 4. Instruments committed this session

| Instrument | Use |
|---|---|
| `tools/ops/absorption-episode-age-read.ps1` | Built-in calibration against the **dated** fetch `aws_fetch/20260909-143922` (reproduces 840/218/909,850/442,070). Refuses to read if it fails |
| `tools/checks/rotation-riders.ps1` | Called by the gate. Header text changed ⇒ ledger must change. 8 standalone test cases pass |

**Final `D-1` read, from 2026-09-16 UTC:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector.ps1 fetch -InstanceId i-0d6c133058876273e
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/absorption-episode-age-read.ps1 -Path aws_fetch/<new-stamp>/analysis_log.csv
```

⚠ `collector.ps1 fetch` **overwrites the root `analysis_log_aws.csv`**. Never calibrate against the root file.

**8-weekday-day read (2026-09-13):** 1,159 absorption-active reads, over-window share **26.5 % (95 % 21.5–32.2)**, pressing share **36.4 % (95 % 25.8–55.4)**, top 10 reads carry **56 %** of all pressing. Weekday-only, UTC day-of-week.

---

## 5. ⚠ What is OPEN (not owed by the trader)

- `_evalCache` unbounded; its obvious fix is destructive, no spec. Carried from the 09-11 handover.
- Detached 24 h memory counter run; the BUSY half of the per-hour memory table needs re-measuring. Carried.
- `ws_health.log` under-reports an outage — the 09-11 evidence points the opposite way to how that row is worded. Carried.
- Fills-import LATER · `S-1` NOT NOW · S0 `--verify-venue` SUSPENDED · RPC code `11051` unverified against Deribit docs. Carried.
- ⚠ **Deploy ledger rows `e3781e57…` and `03a60e32…` carry settings version NOT VERIFIED.**
- ⚠ **The venue-status instrument, `C-3b`, atomic writes and the coverage cluster are committed but NOT deployed** — the box has run one process (`3fe57c53…`) since 2026-09-01. The next deploy carries all of it.
- ⚠ `D-1`'s other four columns (`AbsorptionPullLB`, `AbsorptionPostLB`, `AbsorptionSizeStart`, `AbsorptionSizeMin`) have **not been read**.

---

## 6. ⭐ Lessons — each one cost a correction this session

| # | Lesson | Instance |
|---|---|---|
| 1 | **A median is not a mass.** Check where the quantity sits, not the middle value | "`D-2` is a no-op on the median episode" — true, and 26 % of reads carry a third to half of all pressing |
| 2 | **A heavy-tailed SUM is not a rate.** Quote an interval | Pressing share moved 48.6 → 36.4 % in two days; 10 reads carry 56 % |
| 3 | **A property claimed about a doc must be read out of that doc** | I claimed §15's untouched rows were uncapped before reading its rule box |
| 4 | **The trader's question is an instrument.** A "what if we did X" can falsify your read | The `D-6d.3` question made me measure, which withdrew my (b) |
| 5 | **A detector can match its own description** | Mojibake grep flagged the `AC-8` rows that contain the mojibake pattern |
| 6 | **String escapes in scripts corrupt paths silently.** Scan for control bytes, not just mojibake | A Python literal turned a Windows-path backslash-b into a BACKSPACE in the deploy ledger. Memory updated |
| 7 | **Calibrate on a file nothing overwrites** | `fetch` overwrote the root CSV the 09-11 read came from |
| 8 | **A rider on an event needs a CHECK when the event fires and a COMPLETE list** | 2026-09-01 rotation carried none; one rider was never listed |
| 9 | **Gate on a file that changes only when the property changes** | Gating on `trader-tick-queue.md` would pass by accident |
| 10 | **Two box facts from two eras can both be "verified"** | The "95-column v0.7 book" was true on the terminated box, false on production |

⭐ **Bias register (for `CLAUDE.md`'s measured-bias table):** `D-6d.1` — my (a) sidecar-only was defeated by (c). `D-6d.3` — my (b) was withdrawn on measurement after the trader's question. `RD-1` — taken as recommended.

---

## 7. ⚠ What I did NOT verify

- Collector health (`collector.ps1 status`) was **not run** this session; only a `fetch` (all transfers verified).
- The Kelly trigger read was **not run**.
- The rider sweep relied on phrase patterns; a deferral worded differently would be missed.
- `DeribitIndicatorProject.md` token figure measured 2026-09-11; **not re-measured** since, and the file did not change this session.
