# Batch summary — absorption S1 (`D-2` episode-cumulative pressing + `D-6d` Stage 1)

**Written:** 2026-09-24 (UTC). **Seat:** implementer, Opus, high.
**Spec:** [`docs/absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §3 and §4.1–§4.3, with [`docs/d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §4 and §8.
**Review packet:** [`docs/absorption-d2-s1-spec-back.md`](absorption-d2-s1-spec-back.md). **Build commit:** `549b2c3`. **Base:** `ddebc96`.

## 0. Read this first

- ✅ **Built and committed locally. NOT pushed. NOT deployed.** S1 rides the single deploy after S2 (build spec trap `T-2`, two deploys make three eras).
- ✅ **No header change, no `settings.json` change.** Settings stays v68 (`git diff ddebc96 HEAD -- settings.json` is empty).
- ⚠ **Fixture ids moved.** The build spec's `A78` family (Stage 1) and `A79` family (`D-2` and the rotation) were already used in the tree by the coverage-report and gap-repair builds. This build uses **`A88`** for Stage 1 and **`A89`** for `D-2` and the rotation. `A88` and `A89` were free in the tree and in `docs/`.
- ⚠ **The close-reason enum has SEVEN members, not six.** The spec's split of `ProximityShut` from `LadderSpanLost` on `lvl = 0` does not match the code: `lvl = 0` closes at the re-map test, and it also covers the touch trading through the level. The build splits on geometry and adds `TouchCrossed`. Detail: `docs/absorption-d2-s1-spec-back.md` §3.
- ⚠ **No escalation trigger fired.** The "flagged rate up while the ratio shifts left" trigger needs live data; it is a post-deploy watch. `LogRun`'s signature did not change (S1 does not touch `LogRun`).
- ✅ **The episode-age read is not a gate on this build.** `docs/absorption-episode-age-read-2026-09-13.md` §5 says the build spec "does not wait on it beyond the date gate". The ~10 weekday-day date gate closed at the end of 2026-09-15, before the 2026-09-18→21 outage, so the three-day hole does not reopen it.

## 1. What changed — commit `549b2c3`

| Item | File | Change |
|---|---|---|
| `D-2` | `Core/LevelAbsorptionTracker.vb` | `PressSum` is episode-cumulative. `Press` queue, `PressQueueCap`, `PrunePress` removed. `Snapshot` no longer prunes, so it no longer mutates state |
| Stage 1 | `Core/LevelAbsorptionTracker.vb` | `SideState` gains `LastLevelPrice` (kept through `CloseEpisode`), `LastCloseReason`, `ShadowArmed`, shadow totals, `PressAccruedUsd`, a per-reason `Tally`. `CloseEpisode(reason, nowMs)` takes a required reason |
| Stage 1 | `Core/LevelAbsorptionTracker.vb` | New `ClassifyPrint` (the one shared predicate), `ClassifyRemapClose`, `TakeInstrument`; new types `AbsorptionCloseReason`, `AbsorptionPrintClass`, `AbsorptionReasonTally`, `AbsorptionSideInstrument`, `AbsorptionInstrumentRead` |
| Stage 1 | `MarketState.vb` | New `GetAbsorptionForRun`: `Snapshot` plus the instrument drain under one lock |
| Stage 1 | `Core/AbsorptionEpisodeLog.vb` (new) | `absorption_episodes.log`, one `v1` line per run, never throws |
| Stage 1 | `UI/MainForm_Analysis.vb` | Run read uses `GetAbsorptionForRun`; the sidecar line is written right after `LogRun` with the row's `InstanceId` and `SignalId` |
| `R-4` | `Core/Settings/EngineSettings.vb` | Comment only: `window_sec` is unused by the press path; the key stays |
| harness | `verify/ordercheck/OrderCheck.vbproj`, `Program.vb` | Links the sidecar; fixtures `A88a`–`A88f`, `A89a`–`A89b` |
| docs | `docs/DeribitIndicatorProject.md` §15 | One row for the whole item (S2 extends it) |
| docs | both specs | BUILT banners in the same commit |

## 2. Acceptance (build spec §6, S1 items)

| # | Item | Result |
|---|---|---|
| 1 | Release `-t:Rebuild`, six projects, each alone | ✅ 0 warnings, 0 errors each: solution · `AutoTweaker` · `WhatIfRunner` · `CeilingAudit` · `BacktestRunner` · `OrderCheck` |
| 2 | `verify-gate.ps1 -Mode local-fast` | ✅ `GATE PASSED`, harness ALL PASS, `no snapshot/card drift detected` |
| 3 | Harness count | ✅ 453 → **461** (8 new checks; `A81b` still SKIP by design) |
| 4 | `settings.json` untouched | ✅ empty diff, v68 |
| 5 | Display parity stated in the commit message | ✅ strip moves; snapshot and cards do not |
| 6 | Two `absorption_episodes.log` lines from a real run | ⛔ **NOT RUN.** A local app run writes the live bridge file (`signal_bridge.enabled` is true in the tracked settings). Deferred to the post-deploy check on the collector |
| 8 | §15 row + BUILT banners in the same commit | ✅ |

## 3. Fixtures

| Fixture | Asserts | Mutation run | Result under mutation |
|---|---|---|---|
| `A88a` | Idle flow is dropped live and held by the shadow, attributed to `LadderSpanLost`; a second drain reads zeros | `M1` shadow arm off | `A88a`, `A88b`, `A88d` FAIL |
| `A88b` | Live arm, shadow arm and `ClassifyPrint` agree at 132 points (33 prices × buy/sell × ABOVE/BELOW) | `M2` shadow arm uses its own copy, one tick wider | `A88b` FAILS ALONE |
| `A88c` | Five close routes each tally only their own reason | `M3` `LadderSpanLost` collapsed into `ProximityShut` | `A88a`, `A88b`, `A88c` FAIL |
| `A88d` | Snapshots and live instrument fields byte-identical with and without idle prints | `M4` shadow also adds to `PressSum` | `A88d` FAILS ALONE |
| `A88e` | `LastLevelPrice` survives `CloseEpisode` and `Reset` | `M5` clear it in `CloseEpisode` | `A88a`, `A88b`, `A88d`, `A88e` FAIL |
| `A88f` | Locked path returns False and does not throw; one keyed line | `M6` remove the `Try`/`Catch` | `A88f` FAILS ALONE |
| `A89a` | Press from before the 10 s mark is still counted | `M7` restore the rolling queue and prune | `A89a` FAILS ALONE (`aggr=20000`) |
| `A89b` | Below 10 s, `AggrUsd` is bitwise the sequential sum | `M7` (the old code) | `A89b` PASSES — the parity half holds on both |

Every mutation was restored by copying back a saved clean file; `cmp` confirmed both files byte-identical to the clean copies before the commit.

## 4. Auto-proceeded decisions (one line each)

- **Fixture ids** — spec `A78`/`A79` (taken) · `A88`/`A89` (free). Took `A88`/`A89`: re-using a taken id is the `A56b` collision class.
- **`PressQueueCap`** — keep 4096 · raise it · remove the queue. Took remove: nothing read the queue but the prune, so a running sum guarantees no truncation and loses nothing.
- **Close-reason split** — spec split on `lvl = 0` · split on geometry with a seventh member `TouchCrossed`. Took geometry: `lvl = 0` also covers the touch crossing the level, which would inflate the suspect bucket. Step 3 of the three-step test: the spec's split is mechanically wrong for this code.
- **Shadow after a Break-class print** — keep accruing · stop until the next close. Took stop, and count `shadow_breaks`: that print would have closed a live episode too, so later flow is not "lost".
- **Shadow attribution** — totals only (spec) · totals plus per-reason shadow USD. Took per-reason: it ties each dropped USD to the close that caused the idle interval, which is Stage 1's stated goal.
- **Counting-gap denominator** — `PressSum` (an episode sum) · a new per-interval `PressAccruedUsd`. Took the interval flow: episode sums double-count an episode that spans two runs.
- **Drain point** — drain in `Snapshot` · a separate `TakeInstrument` on the run path only. Took the separate drain: the live strip calls `Snapshot` every tick and would empty each run's line.
- **Sidecar extras** — spec fields only · plus `interval_sec`, `last_level`, per-reason lifetime seconds, a `v1` format token. Took the extras: each makes a line self-describing at no cost.

## 5. Not done in S1, by design

- The CSV column `AbsorptionShadowAggrUsd` and every rider — S2.
- The live-run acceptance item 6 — post-deploy.
