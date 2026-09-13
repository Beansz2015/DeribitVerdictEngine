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
>
> ⛔⛔ **`D-6d.1` (c) FORCES A HEADER ROTATION. That is deliberate and it is what makes this build big: the FIVE riders parked in [`trader-tick-queue.md`](trader-tick-queue.md) §3 travel with it.** **The 2026-09-01 rotation went past without them — see §8.**

---

## 0. Implementer brief

**Model: Opus · Effort: HIGH · TWO sessions · ⛔ ONE deploy at the end, not one per session.**

**Why that tier.** Three different kinds of change land in one edge: a **semantics** change to a live column (`D-2`), a **state-machine** instrument on a dual-fed path under one `MarketState` lock (Stage 1), and a **schema rotation** across three hand-kept copies. ⚠ **Any one of them is Sonnet/medium work. The combination is not** — the traps are all in how they interact.

⛔ **Where it will slip — five concrete traps.**

| # | Trap | Why nothing catches it |
|---|---|---|
| **T-1** | ⛔⛔ **The effective-source stamp (rider 4) is computed INSIDE the `signal_bridge.enabled` gate.** `SignalEmitter.DeriveWsHealth` has exactly ONE caller — [`UI/MainForm_SignalBridge.vb:118`](../UI/MainForm_SignalBridge.vb) — and that path is gated. **Compute it there and a box with the bridge off logs an empty stamp forever** | The dev box and the collector may differ on that flag. **A fixture built on the collector's config passes** |
| **T-2** | ⛔ **Two deploys ⇒ two dataset boundaries.** `D-2` changes the meaning of `AbsorptionAggrUsd`; the rotation changes the header. **Deploy them separately and the absorption study gets three eras instead of two** | Both commits are individually clean and the gate passes on each |
| **T-3** | ⛔ **Rider 1 (`.bak` name) MUST land in the SAME commit as the header change.** [`AnalysisLogger.vb:159`](../AnalysisLogger.vb) hardcodes `analysis_log.csv.v0.7.bak`, and **that name is already TAKEN by the real 95-column v0.7 book.** Once the rotation runs, the mislabelled file exists and cannot be un-created | The rotation succeeds. Nothing errors. The wrong file just sits there |
| **T-4** | **The header has THREE hand-kept copies, and one resolves by its own list, not by the file.** `AnalysisLogger.vb` (header + values) · the byte-verbatim twin `tools/BacktestRunner/BacktestRowWriter.vb` · `tools/BacktestRunner/OverlapValidator.vb`'s `ColSpec` list, whose `ColIndex()` reads **that list**, not the header. Plus `tools/BacktestRunner/ReplayLoop.vb` must set every new column to `Nothing` on the replay path | ⭐ **`A60e` is the existing fixture that catches a broken twin. Extend it — do not write a new one that misses the `ColSpec` copy** |
| **T-5** | **Writing the shadow accumulator into `PressSum`.** Stage 1's measurement must never reach `absorbRatio` | `absorption.scoring_enabled` is `false`, so nothing goes red. The live strip moves silently |

⛔ **Escalation triggers.**

- **If the flagged rate rises while the ratio distribution shifts LEFT — STOP.** That is the numerator/denominator span artefact, carried from [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) §2.
- **If `LogRun`'s signature has to change to carry the effective-source stamp — STOP and re-read §4.4.** It has four fixture call sites and the intended route avoids touching any of them.
- **If the rotation's `.bak` branch cannot be made to name the SUPERSEDED header rather than a literal — STOP.** Shipping the rotation without rider 1 is the one ordering this spec forbids.

**Session split — sequenced by dependency, and the deploy is deliberately at the end.**

| Session | Scope | Header touched? | Effort |
|---|---|---|---|
| **S1** | `D-2` + Stage 1 tracker instrumentation + the sidecar. §3, §4.1–§4.3 | **NO** | **Opus, high** |
| **S2** | The rotation: 4 new columns × 3 schema copies, riders 1–5, fixtures. §4.4–§5 | **YES, once** | **Opus, high** |
| **deploy** | ⛔ **ONE stop → swap → start, AFTER S2.** Record the `InstanceId` in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a | — | — |

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
| **6** | **Rider 2** — `.bak` inclusion added to the pooled-concat rule | Doc-only | [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §3b step 2 |
| **7** | **Rider 3** — new column **`TriggerMode`** | Schema | ruled at the pre-Aug-1 batch double-check, `D3` |
| **8** | **Rider 4** — new column **`EffectiveSource`** (`DeriveWsHealth`'s per-run value) | Schema. ⛔ **`T-1`** | `J-E` RATIFIED |
| **9** | **Rider 5** — new column **`SettingsVersion`** | Schema | [`trader-tick-queue.md`](trader-tick-queue.md) §3 |

**Four new columns. One rotation. One deploy.**

---

## 2. Rulings this spec makes

| # | Ruling |
|---|---|
| **R-1** | ⛔ **APPEND ALL FOUR AT THE END OF THE HEADER**, after `AbsorptionSizeMin`. Same rule the 2026-09-01 rotation used (its `R1`): **no existing column moves, so every pre-rotation row keeps both its position and its meaning.** The four are simply empty on older rows |
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

### 4.4 ⛔ The four columns — and rider 4 is the whole difficulty

| Column | Source | Notes |
|---|---|---|
| `AbsorptionShadowAggrUsd` | `AbsorptionRead` | `Double?` + `InvOpt`, same as the 2026-09-01 five. **Empty when no episode** — a `0` would be a fabricated measurement |
| `TriggerMode` | `cfg.AutoRun.TriggerMode` | Trivial — it is already on the cfg the logger receives |
| `SettingsVersion` | `cfg.Version` | Trivial — same |
| ⛔ **`EffectiveSource`** | `SignalEmitter.DeriveWsHealth` | **`T-1`. See below** |

⭐ **The intended route for rider 4, which avoids touching `LogRun`'s four fixture call sites:** add `EffectiveSource As String` to **`IndicatorResults`**, and set it in `RunAnalysisAsync` **unconditionally** — ⛔ **NOT inside the `signal_bridge.enabled` block.** `LogRun(r, v, cfg)` then needs no signature change. ⚠ **`UI/MainForm_SignalBridge.vb:118` should then READ `r.EffectiveSource` rather than calling `DeriveWsHealth` a second time** — two call sites is two copies, and they would drift.

---

## 5. Fixtures

| Fixture | Asserts | Required mutation |
|---|---|---|
| `A78a`–`A78f` | Stage 1, verbatim from [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §8 | as specified there |
| **`A79a`** | ⭐ **`D-2`'s only interesting case: an episode older than 10 s accumulates press from BEFORE the 10-second mark.** Two press batches 15 s apart in one episode ⇒ both counted | Restore `PrunePress` ⇒ only the second counted. **Fails alone** |
| **`A79b`** | ⛔ **`D-2` is a NO-OP below 10 s** — an episode younger than the window returns byte-identical `AggrUsd` before and after | — (this is the parity half; it must pass on both) |
| **`A79c`** | ⛔⛔ **`T-1`: with `signal_bridge.enabled = False`, `EffectiveSource` is still populated on the logged row** | Move the assignment inside the bridge gate ⇒ empty stamp. **Fails alone** |
| **`A79d`** | **`T-3`: the rotation `.bak` name is derived from the SUPERSEDED header**, not a literal — a synthetic old-header file rotates to a name carrying that header's identity | Restore the `v0.7` literal ⇒ fails alone |
| **`A60e` EXTENDED** | **`T-4`: all three schema copies carry the four new columns** — including `OverlapValidator`'s `ColSpec` list | Break any one copy ⇒ `A60e` fails while `A43e` still passes |

⛔ **Every mutation RUN, with the actual output pasted. A prediction is not a run.**

---

## 6. Acceptance

1. Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck` Release **`-t:Rebuild`** 0 errors 0 warnings, each run separately. ⚠ **Rebuild, not incremental** — an incremental build hid a `BC42109` in the `S-4` build.
2. `tools/checks/verify-gate.ps1 -Mode local-fast` ⇒ **`GATE PASSED`**, harness **ALL PASS**.
3. Harness **376 → ~386** (`A78a`–`A78f`, `A79a`–`A79d`; `A60e` extends in place).
4. `git diff --stat -- settings.json` **EMPTY**. Settings **v68**.
5. ⛔ **Display parity: the gate must report `no snapshot/card drift detected`, AND the commit message must state `R-3` — the strip moves, the snapshot and cards do not.**
6. ⭐ **A real run emits at least TWO `absorption_episodes.log` lines spanning more than one run interval.** ⛔ **Two, not one** — the v68 auto-run defect passed its own acceptance on a single row while the box had stopped collecting.
7. ⛔ **After the rotation fires on the box, verify the `.bak` file's name carries the superseded header's identity** — `T-3`'s only real check is post-deploy.
8. A version-history row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 declaring the **dataset boundary** (`R-2`), and the **BUILT banner written into this file and into [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) in the SAME commit**.

---

## 7. Out of scope — named so they are not lost

- **`D-6d` Stage 2** — the fix. Gated on Stage 1's read. [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §5.
- **`D-6c`** — still OPEN, still gated on data no stored book can supply.
- **`window_sec` deletion** — `R-4`. It goes unused by the press path; removing the key is a separate settings change.
- **`PressQueueCap` re-sizing** — §3 requires it to be *examined*, not necessarily changed.

---

## 8. ⚠ What I did NOT verify

- ⚠ **I did not read riders 3 and 5 beyond their [`trader-tick-queue.md`](trader-tick-queue.md) §3 one-line entries.** Both are `cfg` reads and look trivial; **`TriggerMode`'s and `SettingsVersion`'s exact rendering (raw string vs normalised) is unspecified and the implementer must rule it.**
- ⚠ **I did not verify that the 2026-09-01 rotation actually FIRED on the collector.** The §15 row says `EnsureLogFile` rotates on a header change and the exe was built 2026-09-01 15:48, so it should have. ⛔ **If it did not, rider 1's premise changes — check for the `.bak` before building.**
- ✅ **Verified at `828d868`, not carried:** `grep -c` on [`AnalysisLogger.vb`](../AnalysisLogger.vb) returns **0** for `TriggerMode`, **0** for `SettingsVersion`, **0** for `EffectiveSource`; the `.bak` literal is at `:159`; `DeriveWsHealth` has exactly one caller, [`UI/MainForm_SignalBridge.vb:118`](../UI/MainForm_SignalBridge.vb); `LogRun` is called once in production and **four times** in `verify/ordercheck/Program.vb`.
- ⚠ **`PressQueueCap = 4096` against a 253-second episode is arithmetic, not a measurement.** Nobody has counted prints per episode.
