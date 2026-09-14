# `analysis_log.csv` rotation riders — THE LEDGER

> **Created 2026-09-13 (UTC).** **The one place every change parked on the next `analysis_log.csv` header rotation is recorded, so the rotation that fires carries all of them.**
>
> ⛔⛔ **Why it exists: the 2026-09-01 absorption-instrumentation rotation fired and NONE of its riders travelled** — not the five listed in [`trader-tick-queue.md`](trader-tick-queue.md) §3, and not a sixth (`RIDER-7`, the trade-count column) that was ruled onto that very rotation and **had never been added to any rider list.** Nothing checked the list when the event fired, and the list was incomplete anyway.
>
> ⭐ **Enforced in code, not by memory:** [`../tools/checks/rotation-riders.ps1`](../tools/checks/rotation-riders.ps1), called by [`../tools/checks/verify-gate.ps1`](../tools/checks/verify-gate.ps1). **When the `AnalysisLogger.Header` string changes in a commit range, THIS FILE must change in the same range** — WARN on `local-fast`, FAIL on `prepush` and `ci`. It compares the extracted header TEXT, so a comment edit to `AnalysisLogger.vb` does not trip it.

---

## 0. The rules

1. **A spec that defers anything to "the next rotation" adds a row here IN THE SAME COMMIT.** A deferral recorded only in its own spec's D-table is invisible to everyone who is not reading that spec — `RIDER-7` is the proof.
2. **The commit that changes the header marks every row it carries `CONSUMED` with its commit**, and re-parks any row it does not carry, with a reason. ⚠ **The gate forces this file to be TOUCHED; it cannot force the CONTENT to be right. That stays a review item.**
3. **Never force a rotation for a rider.** A rider waits for a rotation that has its own reason to happen.
4. **This file is the ledger of record.** Any other list of riders — a spec, a handover, the queue — is a pointer, or it is wrong.

---

## 1. The ledger

| ID | Rider | Source ruling | Status | Lands in |
|---|---|---|---|---|
| `RIDER-1` | `.bak` name derived from the superseded header, never a literal | [`trader-tick-queue.md`](trader-tick-queue.md) §3 (archived in §2 below) · specced in [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §4.5 | ⭐ **TRAVELLING** — the `D-2` + Stage 1 rotation, session S2 | `AnalysisLogger.vb` `EnsureLogFile` |
| `RIDER-2` | Rotated books included in every pooled read: **(2a)** the pooled-concat doc rule, **(2b)** `tools/ops/collector.ps1` fetch and `tools/ops/kelly-trigger-read.ps1` | queue §3 · **widened 2026-09-13** (build spec §4.5) | ⭐ **TRAVELLING** — S2. ⛔ **Blocks the deploy** (build spec trap `T-7`) | [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §4 step 2 · `tools/ops/` |
| `RIDER-3` | `TriggerMode` column — what FIRED the run | [`pre-aug1-batch-spec-back.md`](pre-aug1-batch-spec-back.md) `D3` · value ruled 2026-09-13 | ⭐ **TRAVELLING** — S2 | header |
| `RIDER-4` | `WsHealth` column — `DeriveWsHealth`'s pinned enum, derived once | `J-E`, [`fable-seat-close-handover-2026-08-01.md`](fable-seat-close-handover-2026-08-01.md) §2 | ⭐ **TRAVELLING** — S2 | header |
| `RIDER-5` | `SettingsVersion` column | queue §3 | ⭐ **TRAVELLING** — S2 | header |
| `RIDER-6` | `SettingsLoadError` column | `RD-1` = (b), trader 2026-09-13 — build spec §9 | ⭐ **TRAVELLING** — S2 | header |
| `RIDER-7` | `RecentTradeCount` column | [`thin-trade-window-skip-gate-proposal.md`](thin-trade-window-skip-gate-proposal.md) `D-5` (ticked) | ⛔ **LOST on the 2026-09-01 rotation — never listed anywhere. Found 2026-09-13 by sweeping specs for deferrals.** ⭐ **TRAVELLING** — S2, **confirmed by the trader 2026-09-14** | header |
| `RIDER-8` | Cross-venue lead-lag CSV columns | [`cross-venue-lead-lag-proposal.md`](cross-venue-lead-lag-proposal.md) `D6` | ⏸ **CONDITIONAL — does NOT ride S2.** The feature is not scheduled, and its column names are not specified. **If roadmap item W6-7 is scheduled, its spec names the columns and they ride the next rotation after that** | — |
| ~~`change_log` v64 reversibility wording~~ | A settings-touch rider, not a CSV rider | queue §3 | ✅ **CONSUMED 2026-08-02** with D3 / v65 — kept for history | — |

⚠ **The S2 commit must mark `RIDER-1` to `RIDER-7` `CONSUMED`, with its commit hash.**

---

## 2. Archive — the rows of `trader-tick-queue.md` §3, moved VERBATIM 2026-09-13

*The evidence behind `RIDER-1` to `RIDER-5` as it stood in the queue, moved here byte-for-byte by script so there is one list. The queue's §3 is now a pointer to this file. The anchors inside these rows (for example "§3b step 2") are historical and some are corrected in place.*

| Rider | Attaches to |
|---|---|
| ⚠ **The rotation backup name is STALE and mislabels the book it saves** — ~~`AnalysisLogger.vb:143`~~ `AnalysisLogger.vb:159` *(anchor corrected 2026-09-13)* | **The next CSV header rotation — which absorption `D-1` IS**, since any header change rotates (`EnsureLogFile` compares line 0 to `Header`). ⚠ **Must land IN that commit, not after it — once the rotation runs, the misnamed file exists.** The path is **hardcoded** to `analysis_log.csv.v0.7.bak`; that was correct for v0.7→v0.8 and is now stale for every future rotation. **Verified 2026-08-20: the name is already TAKEN** — the existing file holds **95 columns**, the real v0.7 schema — so a v0.9 rotation takes the timestamped branch and writes the **v0.8** book (111 columns) into `analysis_log.csv.v0.7.<ts>.bak`. ⛔⛔ **CORRECTED 2026-09-13 (UTC) — TRUE ONLY OF THE OLD, NOW-TERMINATED BOX.** On production (`i-0d6c133058876273e`) the 2026-09-01 absorption-instrumentation rotation **already wrote the 111-column book under `analysis_log.csv.v0.7.bak`** — verified from the fetched file: 111 columns, 33,911 rows, 2026-07-22 16:24:54 → 2026-09-01 15:48:01. **The mislabel this row set out to prevent now exists, because this rider did not travel on that rotation.** ⭐ **Ruled and specced: [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §4.5 — the name is derived from the superseded header (column count + header hash + UTC stamp), and the existing file is never renamed.** **Anyone later hunting "the v0.8 book" will not find it under that name.** **Fix: derive the suffix from the superseded header, never a literal** — the same defect class as the fixture-literal provenance rule. **Model: Sonnet, effort low** — bundle into `D-1`, do not give it a session |
| ⚠ **`.bak` inclusion is missing from the pooled-concat rule** — [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) ~~§3b step 2~~ **§4 step 2** *(anchor corrected 2026-09-13 — no §3b exists)*. ⛔ **WIDENED 2026-09-13: NOT doc-only. `tools/ops/collector.ps1` and `tools/ops/kelly-trigger-read.ps1` both hardcode the single `.bak` name, so after this rotation the Kelly read would silently drop the 2026-09-01 → deploy book. Build spec §4.5, trap `T-7`: it blocks the deploy.** | **The same rotation. Doc-only, and it is the half that actually costs rows.** §3b step 2 already handles *"a rotation happened on ONE BOX only ⇒ DO NOT pool"* — it says **nothing** about including the rotated `.bak` from the **same** box. ⚠ **Verified 2026-08-20: NEITHER reader discovers `.bak` files.** `CsvFeatureBuilder.vb:112` (W6-4) and `ForwardWindowJoiner.vb:108` (the Kelly ladder) each take **one path**; nothing in the tree globs `analysis_log.csv*`. **Pooling is a MANUAL concat, so nothing warns when the `.bak` is omitted and the population silently truncates to post-rotation rows.** ⚠ **Cost if dropped: the Kelly trigger needs ≥406 pooled weekday STRONG; at the measured 12.4/weekday, re-accumulating ~400 is ~32 weekday-days ≈ 6.5 weeks.** ✅ **CONCAT ORDER IS FREE — SETTLED 2026-08-20, and it settled the OPPOSITE way to this row's first draft.** The earlier ruling ("keep chronological order") was written while `PopulateForwardBars` was untraced. **It has now been read: `ForwardWindowJoiner.vb:190-210` iterates rows independently, floors EACH row to its own UTC minute, and looks bars up from a `Dictionary` — row order is never consulted.** No top-level sort on `Timestamp` exists in `ForwardWindowJoiner.vb` or `BandLadder.vb` either (`AnalysisRunner.vb` sorts only within-population value lists), and `.Index` is positional alignment within one load, not a chronological assumption. **RULED: put the NEW schema FIRST in the concat**, so the header map carries the added column names and post-rotation rows stay readable; pre-rotation short rows return empty on those columns via the verified bounds checks. **This is strictly better than the chronological ordering and costs nothing** |
| ~~`change_log` v64 reversibility wording~~ | ✅ **CONSUMED 2026-08-02 — travelled with D3/v65** |
| **`TriggerMode` CSV column** | The next natural CSV header rotation — **never force one** ⭐ **Ruled 2026-09-13: the value is what FIRED the run (`MANUAL` / `INTERVAL` / `ON_CLOSE` / `BACKSTOP`), not `cfg.AutoRun.TriggerMode` — that is false after a hot reload, because no reload handler touches auto-run, and on every backstop fire. Build spec §4.5.** |
| **Effective-source per-row stamp** (`DeriveWsHealth`) — ruled **J-E** | The same rotation. ⭐ **Ruled 2026-09-13: the column is named `WsHealth`, derived ONCE per run and shared with the bridge payload and `ws_health.log` (today each success run derives it twice). Build spec §4.5.** Until it ships, treat every REST-fallback-sensitive figure as a **bound, not an estimate** |
| **`SettingsVersion` per-row column** | The same rotation, third of the attribution set. ✅ **`RD-1` RULED (b) 2026-09-13: a separate `SettingsLoadError` column travels in the same rotation (build spec §9) — a startup parse failure would otherwise log the POCO default `1`.** **The deploy checklist §4.5 asserted this column already existed; it does not** — `AnalysisLogger.vb`'s header runs `Timestamp` → `SignalId` with `InstanceId,SignalId` as the only attribution fields. A version straddle is **not filterable from the data**, and because `settings.json` hot-reloads it can land **mid-`InstanceId`**. Until it ships: **make every settings-version change coincide with a process restart** so `InstanceId` is a usable proxy, and keep the checklist §5 ledger — now the only place that mapping exists. **Sharpest at a scoring boundary** — v65/D3's armed and unarmed ASIA rows are identical in shape, so a mixed fleet contaminates the D3 watch's own numerator |

---

## 3. Decisions taken 2026-09-13 (UTC), one line each

- **Ledger home** — queue §3 · a dedicated file. **Took the file: `trader-tick-queue.md` changes in nearly every commit range, so a gate requiring it to change on a rotation would pass by accident.** A check that is mechanically vacuous is not a check.
- **Mechanism** — a doc rule only · a harness fixture pinning the header hash · a gate check "header changed ⇒ ledger changed". **Took the gate check plus the doc rule: the ledger change becomes part of git history. A hash pin fires on the same event and records nothing new.**
- **Strictness** — WARN on `local-fast`, FAIL on `prepush` / `ci`, the display-parity precedent. ⛔ **A header the checker cannot extract FAILS on `prepush` — a check that silently switches itself off after a refactor is exactly the hole it exists to close.**
- **`RIDER-7` carried to THIS rotation** — leave it parked · carry it now. **Took carry: `D-5` already ruled it onto a rotation, and the rotation it named passed without it.** ✅ **CONFIRMED by the trader 2026-09-14 (UTC): `RIDER-7` carries with S2.**
- **`RIDER-8`** — reserve now · conditional. **Took conditional: reserving column names nobody has specified would invent design.**

---

## 4. What I did NOT verify

- ⚠ **Sweep completeness.** Searched `docs/` for "rider" with "rotation" or "header", and for "next … rotation", "rides the next", "deferred to … rotation", "attaches to the next", "same rotation" and "forces a rotation", plus the same patterns in `.vb` comments. **A deferral phrased another way would be missed.**
- ⚠ **Settings-touch riders** — riders on a different event, such as "rides the next settings touch" — **are out of this ledger's scope and were not swept exhaustively.**
