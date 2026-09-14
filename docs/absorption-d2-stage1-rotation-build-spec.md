# `D-2` + `D-6d` Stage 1 + the rotation — BUILD SPEC

> ## ✅ AUTHORISED. Every decision behind this build is ruled.
>
> **Written 2026-09-11 (UTC).** ⛔ **NOT BUILT.** ⚠ **`D-2` is date-gated: do not build before ~2026-09-15** ([`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6, `D-2`'s cell — `D-1`'s post-ship read needs ~10 weekday-days; it stands at **8**, read 2026-09-13 in [`absorption-episode-age-read-2026-09-13.md`](absorption-episode-age-read-2026-09-13.md), and the final re-run is one command from 2026-09-16 UTC).
>
> | Ruling | Where |
> |---|---|
> | `D-1`–`D-5` TICKED 2026-09-01 | [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 |
> | `D-6d.3` = **(c)**, trader 2026-09-11 — `D-2` + Stage 1 together, Stage 2 after the read | [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §7 |
> | `D-6d.1` = **(c)**, trader 2026-09-11 — sidecar **and** one CSV column | [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §7 |
> | `J-E` RATIFIED — effective-source stamp rides the next rotation | [`fable-seat-close-handover-2026-08-01.md`](fable-seat-close-handover-2026-08-01.md) §2 |
> | `RD-1` = **(b)**, trader 2026-09-13 — a separate `SettingsLoadError` column | §9 of this spec |
> | `D-5` (thin-trade gate) — the trade-count column rides a rotation; carried to THIS one 2026-09-13 | [`thin-trade-window-skip-gate-proposal.md`](thin-trade-window-skip-gate-proposal.md) §5 · [`csv-rotation-riders.md`](csv-rotation-riders.md) `RIDER-7` |
>
> ⛔⛔ **`D-6d.1` (c) FORCES A HEADER ROTATION. That is deliberate and it is what makes this build big: every rider marked TRAVELLING in the ledger [`csv-rotation-riders.md`](csv-rotation-riders.md) goes with it — seven, including `RIDER-7`, which the 2026-09-01 rotation lost without anyone noticing.** **The 2026-09-01 rotation went past without them — see §8.**

---

## 0. Implementer brief

**Model: Opus · Effort: HIGH · TWO sessions · ⛔ ONE deploy at the end, not one per session.**

**Why that tier.** Three different kinds of change land in one edge: a **semantics** change to a live column (`D-2`), a **state-machine** instrument on a dual-fed path under one `MarketState` lock (Stage 1), and a **schema rotation** across three hand-kept copies. ⚠ **Any one of them is Sonnet/medium work. The combination is not** — the traps are all in how they interact.

⛔ **Where it will slip — seven concrete traps.** *(`T-1` and `T-3` corrected, `T-6` and `T-7` added, 2026-09-13 UTC.)*

| # | Trap | Why nothing catches it |
|---|---|---|
| **T-1** | ⛔⛔ **The `WsHealth` stamp (rider 4) gets sampled at a DIFFERENT INSTANT from the payload.** Every success run ALREADY derives it twice — for `ws_health.log` and again for the payload's `health.ws`, both via `CurrentBridgeWsHealth` inside `EmitBridgeSignal` ([`UI/MainForm_SignalBridge.vb`](../UI/MainForm_SignalBridge.vb)), called at `:733`, after `LogRun` at [`UI/MainForm_Analysis.vb:643`](../UI/MainForm_Analysis.vb). **A third derivation for the CSV lets the row and the payload disagree on a feed flip. Derive ONCE into `r.WsHealth` before `LogRun`; all three consumers read it** | A fixture on steady inputs passes on every version. ⚠ **CORRECTED 2026-09-13: this row first said the stamp is computed inside the `signal_bridge.enabled` gate. It is not — `LogWsHealthTransitionForRun` runs for every completed run regardless of that flag** |
| **T-2** | ⛔ **Two deploys ⇒ two dataset boundaries.** `D-2` changes the meaning of `AbsorptionAggrUsd`; the rotation changes the header. **Deploy them separately and the absorption study gets three eras instead of two** | Both commits are individually clean and the gate passes on each |
| **T-3** | ⛔ **Rider 1 (`.bak` name) MUST land in the SAME commit as the header change — and the reason first written here was WRONG for production.** ⛔ **CORRECTED 2026-09-13 from the production box's own file:** `analysis_log.csv.v0.7.bak` there holds **111 columns, 33,911 rows, 2026-07-22 16:24:54 → 2026-09-01 15:48:01** — the v0.8 book, **already mislabelled `v0.7` by the 2026-09-01 rotation.** The *"95-column v0.7 book"* was verified 2026-08-20 on the old, now-terminated box. **Without rider 1, this rotation files the 116-column book as `analysis_log.csv.v0.7.<ts>.bak` — a second wrong label, under a name nothing fetches or pools** | The rotation succeeds and nothing errors. **`tools/ops/collector.ps1` does not fetch the new file and `tools/ops/kelly-trigger-read.ps1` does not count it** |
| **T-4** | **The header has THREE hand-kept copies, and one resolves by its own list, not by the file.** `AnalysisLogger.vb` (header + values) · the byte-verbatim twin `tools/BacktestRunner/BacktestRowWriter.vb` · `tools/BacktestRunner/OverlapValidator.vb`'s `ColSpec` list, whose `ColIndex()` reads **that list**, not the header. Plus `tools/BacktestRunner/ReplayLoop.vb` must set every new column to `Nothing` on the replay path | ⭐ **`A60e` is the existing fixture that catches a broken twin. Extend it — do not write a new one that misses the `ColSpec` copy** |
| **T-5** | **Writing the shadow accumulator into `PressSum`.** Stage 1's measurement must never reach `absorbRatio` | `absorption.scoring_enabled` is `false`, so nothing goes red. The live strip moves silently |
| **T-6** | **Giving a provenance column a COMPARED `ColKind` in `OverlapValidator`.** `TriggerMode`, `WsHealth` and `SettingsVersion` differ between a live row and its replay by construction. **They are `ColKind.Meta`, like `InstanceId` / `SignalId`; `AbsorptionShadowAggrUsd` is `ColKind.Muted`, like the 2026-09-01 five** | `Categorical` compiles, and the overlap check then flags every row — which reads as a replay defect, not a schema mistake |
| **T-7** | ⛔ **Deploying before both ops scripts can see a SECOND rotated book** (rider 2b, §4.5) | The Kelly read still prints a total. It is silently short by every weekday STRONG row from 2026-09-01 to the deploy |

⛔ **Escalation triggers.**

- **If the flagged rate rises while the ratio distribution shifts LEFT — STOP.** That is the numerator/denominator span artefact, carried from [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) §2.
- **If `LogRun`'s signature has to change to carry `WsHealth` or `TriggerMode` — STOP and re-read §4.5.** It has four fixture call sites and the intended route avoids touching any of them.
- **If the rotation's `.bak` branch cannot be made to name the SUPERSEDED header rather than a literal — STOP.** Shipping the rotation without rider 1 is the one ordering this spec forbids.

**Session split — sequenced by dependency, and the deploy is deliberately at the end.**

| Session | Scope | Header touched? | Effort |
|---|---|---|---|
| **S1** | `D-2` + Stage 1 tracker instrumentation + the sidecar. §3, §4.1–§4.3 | **NO** | **Opus, high** |
| **S2** | The rotation: the new columns × 3 schema copies, **`RIDER-1` to `RIDER-7` from [`csv-rotation-riders.md`](csv-rotation-riders.md)**, **including the two ops scripts (`RIDER-2`b)**, fixtures, **and marking every carried rider `CONSUMED` in that ledger** (the gate fails the push otherwise). §4.4–§5 | **YES, once** | **Opus, high** |
| **deploy** | ⛔ **ONE stop → swap → start, AFTER S2 — and NOT before rider 2b's two scripts are merged (`T-7`).** Record the `InstanceId` in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a | — | — |

⭐ **Why the split is safe even though `D-2` is a behaviour change: the dataset boundary is created by the DEPLOY, not by the commit.** The trader's workflow is local-first ([`trader-profile.md`](trader-profile.md) §8) — **S1 commits locally and does not deploy.** One deploy ⇒ one edge ⇒ one `InstanceId` to split on.

---

## 1. What this builds, in one table

| # | Item | Kind | Authority |
|---|---|---|---|
| **1** | `D-2` — `aggrUsd` accumulates **over the episode**, not over a 10 s rolling window | ⚠ **Semantics change to a live column.** No scoring impact (`scoring_enabled:false`); **DOES move the live strip** | [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.1 |
| **2** | Stage 1 — shadow-press accumulator + six-way close-reason attribution | New instrument. **Behaviour-neutral** | [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §4 |
| **3** | Sidecar `absorption_episodes.log`, one line per run | New file | `D-6d.1` (c) |
| **4** | New column **`AbsorptionShadowAggrUsd`** | Schema | `D-6d.1` (c) |
| **5** | **Rider 1** — `.bak` suffix derived from the superseded header, never a literal | ⛔ **Must ride the rotation commit** | [`trader-tick-queue.md`](trader-tick-queue.md) §3 |
| **6** | **Rider 2** — rotated books included in every pooled read: **(2a)** the pooled-concat rule, **(2b)** `tools/ops/collector.ps1` fetch and `tools/ops/kelly-trigger-read.ps1` | ⚠ **NOT doc-only — widened 2026-09-13, see §4.5** | [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §4 step 2 *(the queue's "§3b step 2" is a stale anchor)* |
| **7** | **Rider 3** — new column **`TriggerMode`** | Schema | ruled at the pre-Aug-1 batch double-check, `D3` |
| **8** | **Rider 4** — new column **`WsHealth`** (`DeriveWsHealth`'s per-run value — `J-E`'s "effective-source stamp") | Schema. ⛔ **`T-1`** | `J-E` RATIFIED |
| **9** | **Rider 5** — new column **`SettingsVersion`** | Schema | [`trader-tick-queue.md`](trader-tick-queue.md) §3 |
| **10** | **`RD-1`** — new column **`SettingsLoadError`** (`0` / `1`) | Schema | `RD-1` = (b), trader 2026-09-13 — §9 |
| **11** | **`RIDER-7`** — new column **`RecentTradeCount`** | Schema | `D-5`, carried 2026-09-13 — §4.5 |

**Six new columns (`RD-1` ruled (b); `RIDER-7` carried under `D-5`; both 2026-09-13). One rotation. One deploy.**

---

## 2. Rulings this spec makes

| # | Ruling |
|---|---|
| **R-1** | ⛔ **APPEND AT THE END OF THE HEADER, after `AbsorptionSizeMin`, in exactly this order: `AbsorptionShadowAggrUsd` · `TriggerMode` · `WsHealth` · `SettingsVersion` · `SettingsLoadError` · `RecentTradeCount`.** Same rule the 2026-09-01 rotation used (its `R1`): **no existing column moves, so every pre-rotation row keeps both its position and its meaning.** The four are simply empty on older rows |
| **R-2** | ⛔ **`D-2` IS A DATASET BOUNDARY AND MUST BE DECLARED AS ONE.** The 2026-09-01 rotation could say *"not a comparability boundary"* because it only appended. **This one cannot** — `AbsorptionAggrUsd` and `AbsorptionRatio` change meaning at the edge. ⚠ **Do not copy that row's wording** |
| **R-3** | ⛔⛔ **THE DISPLAY-STRING PARITY RULE FIRES.** `D-2` moves `AbsorptionRatio`, which the live strip renders as `ABS↑ <level> (<ratio>×)` via `ComposeAbsorption` at [`UI/MainForm_LiveStrip.vb:270`](../UI/MainForm_LiveStrip.vb). ⚠ **That is a STRIP-only surface — the #3/#5/#6 precedent — so `BuildPlaintextSnapshot` and `MainForm_Render_Cards.vb` are NOT affected. State that explicitly in the commit message rather than leaving the gate to imply it** |
| **R-4** | **NO `settings.json` key. NO version bump. Settings stays v68.** `D-2` replaces the use of `window_sec`, it does not retune it. ⚠ **`window_sec` becomes UNUSED by the press path — do not delete the key in this build; say so in a comment and leave it** |
| **R-5** | **Fixture families: `A78` for Stage 1, `A79` for `D-2` and the rotation.** `A78` measured free at `828d868`. **Extend `A60e`, do not replace it** (`T-4`) |
| **R-6** | ⭐ **The shadow predicate is ONE expression shared by the live and shadow arms**, extracted as a `Friend Shared Function`. A second copy is the fixture-literal provenance failure in executable form |

---

## 3. `D-2` — the mechanism

**`SideState.Press` / `PressSum` stop being pruned by `window_sec`.** `PrunePress` is removed from `AddPress` and from `Snapshot`; the queue is bounded only by `PressQueueCap` and cleared only by `CloseEpisode()` / episode open.

⭐ **This has NO downside branch at any episode age, and that is worth stating because [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.1's 2026-09-03 box said the opposite before it was refuted:** `PressSum` is *already* the sum over `[max(episodeOpen, now − 10 s), now]` because both close paths clear the queue. **Episode-cumulative is the sum over `[episodeOpen, now]` — a superset. Identical below 10 s, strictly larger above it.**

**Measured effect** (weekday-scoped, `analysis_log_aws.csv`, 840 absorption-active reads): binds on **26.0 %** of reads carrying **48.6 %** of all logged `AbsorptionAggrUsd`; span multiplier p50 **2.25×**. ⚠ **8-weekday-day re-read 2026-09-13 (UTC): the read share HOLDS (26.5 %, day-block 95 % 21.5–32.2 %) but the pressing share is LUMPY, not a rate — 36.4 % pooled, day-block 95 % 25.8–55.4 %, because 10 of 1,159 reads carry 56 % of all logged pressing. Quote the interval, never 48.6 %. [`absorption-episode-age-read-2026-09-13.md`](absorption-episode-age-read-2026-09-13.md).**

⚠ **`PressQueueCap` is 4096 and was sized against a 10-second window.** A 253-second episode at a busy moment can exceed it. **Either raise it or state why 4096 still holds — do not leave it unexamined**, because the overflow arm silently drops the OLDEST presses, which under episode-cumulative is a silent truncation of exactly the thing being measured.

---

## 4. Stage 1, the sidecar, and the columns

### 4.1 Shadow press

Per [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §4.1: `SideState` gains `LastLevelPrice` (⛔ **NOT cleared by `CloseEpisode()`**), `ShadowPressUsd`, `ShadowPressCount`. `FoldTradeSide`'s early-out at [`Core/LevelAbsorptionTracker.vb:170`](../Core/LevelAbsorptionTracker.vb) runs the shared predicate against `LastLevelPrice` and accrues to the shadow pair before returning.

### 4.2 Close-reason attribution

`Friend Enum AbsorptionCloseReason`: `DegenerateLadder` · `LevelRemap` · `ProximityShut` · `LadderSpanLost` · `BreakThrough` · `Reset`. ⛔ **`ProximityShut` and `LadderSpanLost` stay SEPARATE** — both reach `:278`, distinguished by whether `lvl = 0`. **Collapsing them destroys the answer Stage 1 exists to get.** `CloseEpisode()` takes the reason as a **required** parameter.

### 4.3 The sidecar

`Core/AbsorptionEpisodeLog.vb`, modelled on [`Core/VenueStatusLog.vb`](../Core/VenueStatusLog.vb): never throws, host-agnostic, append-only at `AppDomain.BaseDirectory + "absorption_episodes.log"`. **One line per run**, carrying `InstanceId` + `SignalId`, and per side: `PressSum`, `ShadowPressUsd`, `ShadowPressCount`, `EpisodeSec`, and the six close counters with their discarded USD.

### 4.4 The new columns — every value ruled, 2026-09-13 (UTC)

⛔ **REWRITTEN 2026-09-13.** The first version called `TriggerMode` and `SettingsVersion` *"trivial — already on the cfg"*. **Both plain `cfg` reads log a value that is sometimes false.** The evidence for each ruling is in §4.5.

| # | Column | Value | Live source | Replay (`BacktestRowWriter`) | `OverlapValidator` |
|---|---|---|---|---|---|
| 1 | `AbsorptionShadowAggrUsd` | USD, `F0`, `InvOpt` | `AbsorptionRead` | empty (`ReplayLoop` sets `Nothing`) | `Muted` |
| 2 | `TriggerMode` | `MANUAL` · `INTERVAL` · `ON_CLOSE` · `BACKSTOP` — what FIRED this run | `r.TriggerMode`, consumed at run start | `REPLAY` | `Meta` |
| 3 | `WsHealth` | pinned enum `OK` · `DEGRADED` · `DOWN` · `REST` | `r.WsHealth`, derived ONCE before `LogRun` | empty — no feed | `Meta` |
| 4 | `SettingsVersion` | integer, invariant culture | the run's own `cfg.Version` | the replay cfg's `Version` | `Meta` |
| 5 | `SettingsLoadError` — **ruled `RD-1` (b), 2026-09-13** | `0` / `1`; empty when not stamped | `r.SettingsLoadError`, captured at run start beside `cfg` | `0` | `Meta` |
| 6 | `RecentTradeCount` — **`RIDER-7`, carried under `D-5`** | integer — the size of the trade window this run scored on | `recentTrades.Count`, the list the thin-trade gate tested at `:143`, assigned when `r` is created at `:207` | the replay's own window count | `NumLoose` — the kind every other trade-window-edge numeric uses (`CVDValue`, `LiqLongSize`, `MicroCVDEarly`) |

⚠ **Empty means "not stamped", never a default.** `r.TriggerMode` and `r.WsHealth` default to `Nothing`. **A code path that forgets to stamp writes an empty cell, not `MANUAL` or `OK`** — a default that reads as a real value is the silent-lie class. *(`IndicatorResults` is a `Class` — verified at [`Core/IndicatorResults.vb:5`](../Core/IndicatorResults.vb) — so new `String` fields cannot trip the `BC42109` warning the `S-4` build hit on a `Structure`.)*

### 4.5 The riders — ruled, with the evidence that forced each ruling

**Rider 1 — the `.bak` name.**

- ⛔ **A schema version label cannot name the book.** The live 116-column header still calls itself `v0.8` ([`AnalysisLogger.vb`](../AnalysisLogger.vb) line 1), because the 2026-09-01 change was ruled *"NOT a schema version bump"*. **Two different headers already share one label.**
- ✅ **RULED: derive the name from the superseded header itself** — `analysis_log.csv.<N>col-<h8>.<yyyyMMdd_HHmmss>.bak`. `N` = the superseded header's column count. `h8` = the first 8 lowercase hex digits of the MD5 of that header line (UTF-8, trimmed). The stamp = the UTC rotation instant.
  - **The count is for a human reader. The hash separates two headers of the same width** — the v0.5 rotation replaced columns without changing the count. **The stamp makes every name unique.**
  - Keep the `File.Exists` guard; on a collision append `-2`, `-3` — never overwrite.
  - **Extract the naming as a pure `Friend Shared Function RotatedBakName(supersededHeader As String, utcNow As DateTime) As String`**, so `A79d` asserts it without touching the file system.
- ⛔ **The existing production file keeps its wrong name — never renamed, never deleted.** Two tools and a dozen docs key on it. **Record in `AnalysisLogger`'s header comment what it really holds: the 111-column book, 2026-07-22 → 2026-09-01 15:48:01 UTC.** The comment at [`AnalysisLogger.vb:42`](../AnalysisLogger.vb) is stale for the same reason.
- 🔎 **Evidence:** the fetched `aws_fetch/20260913-153704` copy — 111 columns, 33,911 rows, first row `2026-07-22 16:24:54`, last `2026-09-01 15:48:01`; the live book opens `2026-09-01 15:50:01`.

**Rider 2 — rotated books in every pooled read. ⛔ WIDENED: not doc-only.**

- **(2a) The doc rule.** [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §4 step 2 still reads *"both v0.8/111-col schema — verify header equality first"*. **Replace it: pool EVERY rotated book on a box plus the live file; concat NEW schema FIRST** — the order the queue settled 2026-08-20, because readers resolve columns by name and short rows read empty; **never require header equality across a rotation.** ⚠ The queue's anchor *"§3b step 2"* does not exist — the rule is §4 step 2.
- **(2b) The two ops scripts — the half that costs rows.**
  - `tools/ops/collector.ps1:117` fetches the literal `analysis_log.csv.v0.7.bak`, and its own OPS-1 comment says a second rotation *"wants its own ruling"*. ✅ **RULED: discover `analysis_log.csv*.bak` ON THE BOX, expand to explicit names, then handle each as a name key** — keeping the manifest keying and size verification OPS-1 defended. ⚠ **Trap: the local verification loop iterates `$FetchFiles`. It must iterate the box-reported manifest, or a discovered book downloads and is never verified.**
  - `tools/ops/kelly-trigger-read.ps1:67` pools exactly two files, `.bak` + live. ✅ **RULED: discover every rotated book on the box, count each, sum all, and run the span-overlap check on every adjacent pair in time order.** Calibration unchanged. Correct its docstring's *"(closed at the v0.7 rotation)"* — that file closed 2026-09-01.
  - ⛔ **Why this blocks the deploy (`T-7`): without it, the first post-deploy Kelly read silently drops the whole 2026-09-01 → deploy book.**

**Rider 3 — `TriggerMode`. ⛔ The cfg value would lie.**

- Every run enters through `btnAnalyze_Click`, manual or auto. `RunAutoAnalysis` is handed to the interval timer ([`UI/MainForm_AutoRun.vb:108`](../UI/MainForm_AutoRun.vb) and `:110`) and called by the on-close watcher at `:263`, which alone knows a bar roll from a backstop fire.
- **No settings hot-reload handler touches auto-run** — a search of `UI/` and `Core/Settings/` for one returns nothing. **So a changed `auto_run.trigger_mode` keeps firing the OLD mode while `cfg.AutoRun.TriggerMode` reads the new one — false at exactly the change this rider exists to expose.** Backstop fires, which happen during feed stalls, would read `on_close`.
- ✅ **RULED: log what fired the run.** `RunAutoAnalysis` takes the trigger. Timer callbacks pass `INTERVAL`; the watcher passes `ON_CLOSE` when `roll.Fired`, else `BACKSTOP`. It stores `_pendingTrigger` before `btnAnalyze_Click`; `RunAnalysisAsync` consumes it beside the `cfg` capture at [`UI/MainForm_Analysis.vb:53`](../UI/MainForm_Analysis.vb) and resets it to `MANUAL`. `RunAutoAnalysis` already returns when `btnAnalyze` is disabled, so consume-and-reset is safe on the UI thread.
- ⚠ **Not harness-reachable** — `OrderCheck.vbproj` compiles no `MainForm_*.vb`. The plumbing is verified by review; `A79c` pins only what `LogRun` writes.

**Rider 4 — `WsHealth`.**

- ✅ **RULED name: `WsHealth`, not `EffectiveSource`.** The value is `DeriveWsHealth`'s pinned enum, as `J-E` ruled, and `OK` is not a source. **One vocabulary across four surfaces:** the payload's `health.ws`, `ws_health.log`, the pinned enum, this column.
- ✅ **RULED: derive ONCE** into `r.WsHealth` just before `LogRun`; `EmitBridgeSignal` passes that value to `BuildOk` and to the ws-health log instead of deriving twice more. **CSV ≡ payload by construction** — the principle `AnalysisLogger`'s header already states for the `Placed*` columns. The skip path is unchanged: no `r`, no CSV row.
- ⚠ **What the value does NOT mean — write it into the column's comment:** `OK` means the socket is up and this run did not fall back to REST. **It has no trade-flow input and must never be read as capture health** ([`trader-tick-queue.md`](trader-tick-queue.md) §2, the `ws_health.log` under-report row).

**Rider 5 — `SettingsVersion`.**

- ✅ **The source is already right:** `cfg` is captured once at [`UI/MainForm_Analysis.vb:53`](../UI/MainForm_Analysis.vb) and passed to `LogRun`, so the version is the one that scored the run, even across a hot reload.
- ⛔ **The one lie:** a startup parse failure runs the engine on POCO defaults, whose `Version` is `1` ([`Core/Settings/EngineSettings.vb:73`](../Core/Settings/EngineSettings.vb)), **so the row reads "v1".** `SettingsLoader.LastLoadError` is non-empty in that state and cleared on the next good load. After a mid-run failure the engine keeps the last good settings — the version is then true, but the file on disk is not what runs. **→ `RD-1`, §9 — RULED (b) 2026-09-13.**
- ✅ **RULED (`RD-1` (b)): a separate `SettingsLoadError` column.** Capture `SettingsLoader.LastLoadError` non-empty into **`r.SettingsLoadError As Boolean?` beside the `cfg` capture at `:53`** — ⛔ **not inside `LogRun`**, where a hot reload between `:53` and `:643` could pair one load state with a different settings snapshot. `Nothing` writes an empty cell.
- ⚠ **The column's comment must say what `1` means: the most recent disk load FAILED, so the running settings are the last good load or, at startup, the POCO defaults.** The status-bar banner reads *"running on code defaults"* in both cases; **the column must not copy that wording**, because after a mid-run failure it is false.
- *Implementation call logged (§9.1): capture at run start rather than read in `LogRun`.*

**`RIDER-7` — `RecentTradeCount`. ⛔ The rider that was lost.**

- ✅ **Already ruled:** [`thin-trade-window-skip-gate-proposal.md`](thin-trade-window-skip-gate-proposal.md) `D-5` (ticked) — *"propose it as a third rider on that rotation"*, meaning the 2026-09-01 absorption rotation. **It never shipped and was never in any rider list.** Found 2026-09-13 by sweeping specs for deferrals; recorded as `RIDER-7` in [`csv-rotation-riders.md`](csv-rotation-riders.md).
- **Why it matters:** the thin-trade gate SKIPS runs below `MinTradesForScoring`, so a skipped run writes no row — but a scored run on a window just above the minimum looks normal. That spec's own words: *"There is no trade-count column, so the row looks entirely normal in the book."*
- ✅ **Value:** `recentTrades.Count` — the same list the gate tested at [`UI/MainForm_Analysis.vb:143`](../UI/MainForm_Analysis.vb), assigned when `r` is created at `:207`. **No second count: the gate and the column read one list.**
- ⚠ **`MinTradesForScoring` is not logged beside it.** It is recoverable from `SettingsVersion` — the larger of the TFI and MicroCVD window sizes, or the override — which rides the same rotation.

---

## 5. Fixtures

| Fixture | Asserts | Required mutation |
|---|---|---|
| `A78a`–`A78f` | Stage 1, verbatim from [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §8 | as specified there |
| **`A79a`** | ⭐ **`D-2`'s only interesting case: an episode older than 10 s accumulates press from BEFORE the 10-second mark.** Two press batches 15 s apart in one episode ⇒ both counted | Restore `PrunePress` ⇒ only the second counted. **Fails alone** |
| **`A79b`** | ⛔ **`D-2` is a NO-OP below 10 s** — an episode younger than the window returns byte-identical `AggrUsd` before and after | — (this is the parity half; it must pass on both) |
| **`A79c`** | `LogRun` writes `r.WsHealth` and `r.TriggerMode` verbatim, and writes **EMPTY** — never `OK` or `MANUAL` — when either is unset | Default either field to a real enum value ⇒ fails alone. ⚠ **The one-derivation half of `T-1` and rider 3's trigger plumbing live in `MainForm_*.vb`, which `OrderCheck` cannot compile: REVIEW-ONLY, stated** |
| **`A79d`** | **Rider 1: `RotatedBakName` returns `analysis_log.csv.<N>col-<h8>.<stamp>.bak` computed from THE SUPERSEDED header** — two headers with the same column count get different names, and a pre-existing target is never overwritten | Restore the `v0.7` literal ⇒ fails alone. Drop the hash ⇒ the same-count case fails alone |
| **`A79e`** | **Rider 5 + `RD-1`: `LogRun` writes the PASSED `cfg.Version`** as an invariant-culture integer, and `SettingsLoadError` as `1` / `0` / empty for `r.SettingsLoadError` = `True` / `False` / `Nothing` | Read `SettingsLoader.Current.Version` or `SettingsLoader.LastLoadError` inside `LogRun` instead ⇒ fails alone when they differ from what the run captured |
| **`A79f`** | **`RIDER-7`: `LogRun` writes `r.RecentTradeCount` verbatim, and EMPTY when it is `Nothing`** | Write `0` for `Nothing` ⇒ fails alone — a zero-trade window is a real value and must never be fabricated |
| **`A60e` EXTENDED** | **`T-4`: all three schema copies carry the four new columns** — including `OverlapValidator`'s `ColSpec` list | Break any one copy ⇒ `A60e` fails while `A43e` still passes |

⛔ **Every mutation RUN, with the actual output pasted. A prediction is not a run.**

---

## 6. Acceptance

1. Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck` Release **`-t:Rebuild`** 0 errors 0 warnings, each run separately. ⚠ **Rebuild, not incremental** — an incremental build hid a `BC42109` in the `S-4` build.
2. `tools/checks/verify-gate.ps1 -Mode local-fast` ⇒ **`GATE PASSED`**, harness **ALL PASS**.
3. Harness **376 → ~388** (`A78a`–`A78f`, `A79a`–`A79f`; `A60e` extends in place). ⚠ An approximation — one fixture `Sub` may carry two `Check()`s, as `A69d` did.
4. `git diff --stat -- settings.json` **EMPTY**. Settings **v68**.
5. ⛔ **Display parity: the gate must report `no snapshot/card drift detected`, AND the commit message must state `R-3` — the strip moves, the snapshot and cards do not.**
6. ⭐ **A real run emits at least TWO `absorption_episodes.log` lines spanning more than one run interval.** ⛔ **Two, not one** — the v68 auto-run defect passed its own acceptance on a single row while the box had stopped collecting.
7. ⛔ **After the rotation fires on the box, verify the new `.bak` is named `analysis_log.csv.116col-<h8>.<stamp>.bak`** and that `analysis_log.csv.v0.7.bak` is untouched — `T-3`'s only real check is post-deploy.
7a. ⛔ **Before the deploy (`T-7`):** `tools/ops/kelly-trigger-read.ps1 -Mode Calibrate` still passes, and a `fetch` lists every `analysis_log.csv*.bak` on the box by name, each size-verified.
7b. ⛔ **The rotation commit updates [`csv-rotation-riders.md`](csv-rotation-riders.md)** — `RIDER-1` to `RIDER-7` marked `CONSUMED` with the commit hash. `tools/checks/verify-gate.ps1` FAILS a pushed header change that does not touch that file.
8. A version-history row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 declaring the **dataset boundary** (`R-2`), and the **BUILT banner written into this file and into [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) in the SAME commit**.

---

## 7. Out of scope — named so they are not lost

- **`D-6d` Stage 2** — the fix. Gated on Stage 1's read. [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §5.
- **`D-6c`** — still OPEN, still gated on data no stored book can supply.
- **`window_sec` deletion** — `R-4`. It goes unused by the press path; removing the key is a separate settings change.
- **`PressQueueCap` re-sizing** — §3 requires it to be *examined*, not necessarily changed.

---

## 8. ⚠ What I did NOT verify

- ~~⚠ **I did not read riders 3 and 5 beyond their [`trader-tick-queue.md`](trader-tick-queue.md) §3 one-line entries.** Both are `cfg` reads and look trivial.~~ ✅ **SUPERSEDED 2026-09-13 — both read in the code and RULED in §4.4–§4.5. Neither was trivial: the plain `cfg` read is false in both.**
- ~~⚠ **I did not verify that the 2026-09-01 rotation actually FIRED on the collector.**~~ ✅ **VERIFIED 2026-09-13 from the fetched file: it fired. `analysis_log.csv.v0.7.bak` closes at 2026-09-01 15:48:01 UTC and the live book opens at 15:50:01 — and rider 1's premise DID change as a result, see `T-3`.**
- ✅ **Verified at `828d868`, not carried:** `grep -c` on [`AnalysisLogger.vb`](../AnalysisLogger.vb) returns **0** for `TriggerMode`, **0** for `SettingsVersion`, **0** for `EffectiveSource`; the `.bak` literal is at `:159`; `DeriveWsHealth` has exactly one caller, [`UI/MainForm_SignalBridge.vb:118`](../UI/MainForm_SignalBridge.vb); `LogRun` is called once in production and **four times** in `verify/ordercheck/Program.vb`.
- ⚠ **`PressQueueCap = 4096` against a 253-second episode is arithmetic, not a measurement.** Nobody has counted prints per episode.

---

## 9. ✅ The reserved decision — RULED 2026-09-13 (UTC)

| # | Decision | Options | My read |
|---|---|---|---|
| **`RD-1`** | **How a row records that `settings.json` failed to load** — ✅ **RULED (b) by the trader, 2026-09-13 (UTC), as recommended** | **(a)** `SettingsVersion` only — a startup failure logs `1`, indistinguishable from a real v1 · **(b)** a fifth column `SettingsLoadError` (`0`/`1`) · **(c)** a suffix inside `SettingsVersion`, e.g. `68:LOAD_ERROR` | ⭐ **(b).** (a) loses the state. **(c) carries the same information in the cheaper shape — and a mixed-type column silently fails any integer reader on exactly the anomalous rows, the silent-drop class `WD-SEMANTICS` was ruled against.** ⚠ **Reserved because (b) adds a column beyond the ruled set — the schema class `CLAUDE.md` reserves.** Marginal rotation cost is zero: the rotation already happens |

### 9.1 Auto-proceeded 2026-09-13 (UTC), one line each

- **Rider 1 naming** — a version label · column count + stamp · count + header hash + stamp. **Took the last: a version label is already shared by two headers, and a count alone cannot separate a same-width column swap.**
- **Rider 2 scope** — doc only (the queue's wording) · doc + both ops scripts. **Took both: doc-only leaves the Kelly read silently short after the deploy.**
- **Rider 3 value** — `cfg.AutoRun.TriggerMode` · the trigger that fired the run. **Took the fired trigger: the cfg value is false after a hot reload and on every backstop fire.**
- **Rider 4 name and sampling** — `EffectiveSource` · `WsHealth`; derive per consumer · derive once. **Took `WsHealth`, derived once: the enum is not a source, and three derivations at two instants let the CSV and the payload disagree.**
- **Provenance `ColKind`** — `Meta` for `TriggerMode` / `WsHealth` / `SettingsVersion` / `SettingsLoadError`, `Muted` for `AbsorptionShadowAggrUsd`. **The provenance columns differ live vs replay by construction.**
- **`SettingsLoadError` capture point** — read the global inside `LogRun` · capture at run start beside `cfg`. **Took run start: `LogRun` runs after the scoring pass, so reading the global there can pair one load state with a different settings snapshot.**
- **`RIDER-7` carried to this rotation** — leave it parked · carry it now. **Took carry: `D-5` already ruled it onto a rotation, and the rotation it named passed without it.** ⚠ **The trader can overrule this before S2.**

⭐ **Every one took the option that records more, so none meets the reserved test. Each is listed so the trader can overrule it.**
