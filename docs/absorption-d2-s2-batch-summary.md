# Batch summary — absorption S2 (the 2026-09 `analysis_log.csv` rotation and its riders)

**Written:** 2026-09-24 (UTC). **Seat:** implementer, Opus, high, same conversation as S1.
**Spec:** [`docs/absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §4.4, §4.5, §5, §6. **Rider ledger:** [`docs/csv-rotation-riders.md`](csv-rotation-riders.md).
**Review packet:** [`docs/absorption-d2-s2-spec-back.md`](absorption-d2-s2-spec-back.md). **Previous session:** [`docs/absorption-d2-s1-batch-summary.md`](absorption-d2-s1-batch-summary.md).

## 0. Read this first

- ✅ **Built and committed locally. NOT pushed. NOT deployed.** The deploy is reserved to the trader. Its plan is §5 below.
- ✅ **Header 116 → 124 columns, appended after `AbsorptionSizeMin`.** No existing column moved (fixtures `A60d` and `A89g`).
- ⚠ **Eight columns, not the spec's six.** `RIDER-9` (`VPFRSignal`, `VPFRPoc`) entered the ledger on 2026-09-16, after the spec. The ledger is the record of what travels, so it travelled.
- ✅ **Eight riders consumed:** `RIDER-1` to `RIDER-7` and `RIDER-9`. `RIDER-8` (cross-venue lead-lag columns) stays conditional; it was not carried.
- ✅ **No escalation trigger fired.** `LogRun`'s signature is unchanged (`H-6` in the packet). The `.bak` branch names the superseded header (`A89d`).
- ⚠ **Instruction conflict, resolved by reading, flagged for you.** The brief said not to edit `tools/BacktestRunner/` because another seat was reading gap-repair code there. The spec's trap `T-4` requires three edits in that folder: `BacktestRowWriter.vb`, `OverlapValidator.vb`, `ReplayLoop.vb`. I read the prohibition as protecting the gap-repair files. I edited only those three schema files, none of the gap-repair files (`HistoricalStore.vb`, `CoverageReport.vb`, `BacktestProgram.vb`, `TradeStoreGapRepair.vb`, `Core/TradeStoreWriter.vb`). `git status` showed no foreign change in the folder before the commit.
- ⚠ **The advisory rider-travel check was NOT run.** `docs/rider-travel-check-spec.md` §1 says it runs before the rotation commit. Its first run is a one-time measurement: the operator writes an independent baseline first. That is not mine to spend. It can still run: see `docs/absorption-d2-s2-spec-back.md` §2, decision `Q-4`.

## 1. What changed — commit `5dfc91a`

| Item | File | Change |
|---|---|---|
| header | `AnalysisLogger.vb` | Eight names appended; new `RotationCells` formats the eight cells (one copy, both writers call it) |
| `RIDER-1` | `AnalysisLogger.vb` | `EnsureLogFile` names the rotated book with `RotatedBakName` (from the superseded header) and `UniqueBakPath` (never overwrites). Header comment records what `analysis_log.csv.v0.7.bak` really holds |
| `RIDER-2` (a) | `docs/aws-collector-deploy-checklist.md` §4 step 2 | Replaced: pool every rotated book plus the live file, newest schema first, no header-equality test across a rotation, check spans |
| `RIDER-2` (b) | `tools/ops/collector.ps1` | `fetch` discovers `analysis_log.csv*.bak` on the box, prints `DISCOVERED_BAK=` lines, manifests and uploads each, and verifies every box-reported file |
| `RIDER-2` (b) | `tools/ops/kelly-trigger-read.ps1` | `-Mode Box` lists every rotated book, counts each and the live file, sums, checks every adjacent span. New `-Mode LocalDir -Dir`. Docstring's "closed at the v0.7 rotation" corrected. Calibration unchanged |
| `RIDER-3` | `UI/MainForm_AutoRun.vb`, `UI/MainForm_Analysis.vb` | `RunAutoAnalysis(trigger)`; timers pass `INTERVAL`, the watcher `ON_CLOSE` or `BACKSTOP`; `RunAnalysisAsync` consumes `_pendingTrigger` beside its cfg capture and resets it to `MANUAL` |
| `RIDER-4` | `UI/MainForm_Analysis.vb`, `UI/MainForm_SignalBridge.vb` | `r.WsHealth` derived once before `LogRun`; `EmitBridgeSignal` passes it to `ws_health.log` and the payload. The skip path also derives once |
| `RIDER-5`, `RIDER-6` | `UI/MainForm_Analysis.vb`, `AnalysisLogger.vb` | `SettingsVersion` from the passed cfg; `SettingsLoadError` captured beside the cfg |
| `RIDER-7` | `UI/MainForm_Analysis.vb` | `r.RecentTradeCount = recentTrades.Count` at `r`'s creation |
| `RIDER-9` | `AnalysisLogger.vb` | `VPFRSignal`, `VPFRPoc` written from `r` |
| shadow column | `UI/MainForm_Analysis.vb` | `r.AbsorptionShadowAggrUsd` = both sides' drained shadow USD, from the S1 instrument |
| fields | `Core/IndicatorResults.vb` | Five new properties, each `Nothing` until stamped |
| replay | `tools/BacktestRunner/BacktestRowWriter.vb`, `OverlapValidator.vb`, `ReplayLoop.vb` | Twin header; the writer calls `RotationCells`; ColSpec kinds per trap `T-6`; replay stamps `REPLAY`, `False`, its own window count |
| ledger | `docs/csv-rotation-riders.md` | Eight rows `CONSUMED`. The hash `5dfc91a` is added by the docs commit that follows, because a commit cannot name its own hash |
| harness | `verify/ordercheck/Program.vb` | `A89c`–`A89h` new; `A60e` extended in place; `A60d` width test "at least"; `A82a`'s side-free list takes the five new fields |

## 2. Acceptance (build spec §6)

| # | Item | Result |
|---|---|---|
| 1 | Release `-t:Rebuild`, six projects, each alone | ✅ 0 warnings, 0 errors each |
| 2 | Gate | ✅ `local-fast` and `prepush` both `GATE PASSED`; `prepush` prints `HEADER ROTATION in range (116 -> 124 columns) and docs/csv-rotation-riders.md was updated` |
| 3 | Harness | ✅ 461 → **468** ALL PASS (453 at the start of S1) |
| 4 | `settings.json` untouched | ✅ empty diff from `ddebc96`, v68 |
| 5 | Display parity | ✅ `no snapshot/card drift detected`; no snapshot line or card binding changed |
| 6 | Two real `absorption_episodes.log` lines | ⛔ post-deploy (§5) |
| 7 | New `.bak` named `analysis_log.csv.116col-<h8>.<stamp>.bak`, `v0.7.bak` untouched | ⛔ post-deploy. Predicted name: **`analysis_log.csv.116col-83564b1b.<yyyyMMdd_HHmmss>.bak`** (`h8` computed from the header line of the 2026-09-24 fetch) |
| 7a | Before the deploy: Kelly calibration passes; a fetch lists every book | ✅ calibration `PASSED - total=49`. ⛔ the live fetch is NOT run: it runs commands on the production box. It is step 1 of §5 |
| 7b | Ledger updated in the rotation commit | ✅ |
| 8 | §15 row, BUILT banner | ✅ one row for S1 and S2; banner in the build spec |

## 3. Fixtures and mutations

| Fixture | Asserts | Mutation | Result |
|---|---|---|---|
| `A89c` | `TriggerMode`, `WsHealth` verbatim, EMPTY when unset | `M8` `WsHealth` defaults to `OK` | `A89c` and `A89h` FAIL |
| `A89d` | `.bak` name from the superseded header; `-2`/`-3` on collision; a real rotation leaves `v0.7.bak` untouched | `M9` the `v0.7` literal restored · `M9b` hash dropped | `A89d` FAILS ALONE both times |
| `A89e` | `SettingsVersion` = the passed cfg (4242); `SettingsLoadError` 1/0/empty | `M10` read `SettingsLoader.Current.Version` | `A89e` and `A89h` FAIL |
| `A89f` | `RecentTradeCount` verbatim, a real 0 stays 0, `Nothing` is empty | `M11` write `0` for `Nothing` | `A89f` FAILS ALONE |
| `A89g` | The eight sit at 117–124 in the ruled order | `M14` swap `TriggerMode`/`WsHealth` in all three copies | `A89g` and `A89c` FAIL; `A60e` PASSES (copies agree) |
| `A89h` | The replay writer's eight cells equal `LogRun`'s byte for byte; `VPFRSignal`/`VPFRPoc` carried | `M13` the writer gets its own copy with `VPFRPoc` at `F1` | `A89h` FAILS ALONE |
| `A60e` (extended) | The eight names once each in all three copies, each with its ruled `ColKind` | `M12` `TriggerMode` made `Categorical` · `M12b` `VPFRPoc` renamed in `ColSpec` | `A60e` FAILS; `A43e` PASSES both times |

Every mutation started from a saved clean file and ended with it copied back; `cmp` confirmed each restore.

## 4. Auto-proceeded decisions (one line each)

- **`RIDER-9` travels** — six columns (spec) · eight (ledger). Took eight: the ledger is the record of what travels (its §0 rule 4), and `RIDER-7` was lost once by exactly this gap.
- **Order of the two `RIDER-9` columns** — after `RecentTradeCount`, in the ledger's order. Append-only, per `R-1`.
- **`RIDER-9` kinds** — `VPFRSignal` `Categorical` (every other signal label) · `VPFRPoc` `NumLoose` (like `VPFRVAH`, `VPFRVAL` and the HVN levels).
- **Shadow column source** — `AbsorptionRead` (spec §4.4) · the drained instrument. Took the instrument: `AbsorptionRead` exists only for an active primary side, and shadow exists only on idle sides — that source would be empty exactly when the value is non-zero. Step 3 of the three-step test: the spec's source is mechanically wrong.
- **Shadow column value** — primary side · both sides summed. Took both sides; per side is in the sidecar.
- **Value formatting** — a second hand copy in `BacktestRowWriter` · one shared `RotationCells`. Took shared: fewer copies, and `A89h` pins it.
- **Skip-path WS health** — two derivations (unchanged) · one. Took one: the log line and the `SKIPPED` payload then agree, same principle as `T-1`.
- **`collector.ps1` row counts for books** — count lines on the box · keep `-1` for non-`.csv` names (existing behaviour). Kept `-1`: counting a 31 MB file on the 1 GiB box is new load; size verification is the property that matters.
- **Kelly script parameter** — keep `-RemoteBak` · replace with `-BakFilter`. Replaced: a single-path parameter is the defect.
- **New `-Mode LocalDir`** — added so the pooling logic is testable without the box.
- **`A60d`** — its width test became "at least 116"; its frozen positions are unchanged. `A89g` pins the new width at exactly 124.
- **`absorption_episodes.log` in the fetch list** — leave out · add. Added in the follow-up docs commit: without it the Stage 1 read cannot reach this machine. One `tools/ops` line, no live surface.

## 5. ⛔ The deploy plan — reserved to the trader

**What the ONE deploy carries** (every app-code commit since the last deploy, `584c616`, 2026-09-21 15:38:41 UTC, instance `ee159d03…`):

| Commit | What | Boundary? |
|---|---|---|
| `526ecf2` | MTF TTL constant refactor | No (`[no-engine-change]`) |
| `a6b33fe` | Engine-fix Session A: `D-1` POC-tier gate fix, `D-2` manual lines | ⛔ YES — `TargetCapReason`, `Placed*`, ~1.68 % of verdicts |
| `ea32818` | Engine-fix Session C: `D-9` Step 3b display, `D-8` report column | No — rendered values only |
| `549b2c3` | Absorption S1: `D-2` episode-cumulative pressing, Stage 1, sidecar | ⛔ YES — `AbsorptionAggrUsd`, `AbsorptionRatio`; the live strip |
| `5dfc91a` | Absorption S2: header 116 → 124, riders | ⛔ YES — the schema edge |

**Not carried:** engine-fix Session B (the liquidation flag, `D-4`/`D-5`). It waits for the probe running on the box.

**Steps.**

1. **Pre-deploy, `T-7`:** run `tools/ops/collector.ps1 fetch` with this commit's script. It must print `DISCOVERED_BAK=analysis_log.csv.v0.7.bak` and verify it. Then `tools/ops/kelly-trigger-read.ps1 -Mode Calibrate` (passed locally, `H-4`). Optionally `-Mode LocalDir -Dir aws_fetch\<new stamp>` to see the pooled total.
2. **Optional, advisory:** the rider-travel check's first run, with a baseline written first (packet decision `Q-4`).
3. **Deploy:** one stop → swap → start with `tools/ops/collector.ps1 deploy`. ⛔ Do not touch `C:\probe-runs\` — the liquidation probe runs there.
4. **Post-deploy checks:**
   - A file named `analysis_log.csv.116col-83564b1b.<yyyyMMdd_HHmmss>.bak` exists; `analysis_log.csv.v0.7.bak` has its old size (31,234,053 bytes in the 2026-09-24 fetch).
   - The new `analysis_log.csv` header has 124 columns. First rows show `TriggerMode` `ON_CLOSE` (or `BACKSTOP`), `WsHealth` `OK`, `SettingsVersion` `68`, `SettingsLoadError` `0`, a non-empty `RecentTradeCount`, a `VPFRSignal` label.
   - `absorption_episodes.log` has **at least TWO lines spanning more than one run interval**, each `iid=` equal to the new `InstanceId` and each `sid=` matching a CSV row.
   - A second `fetch` prints TWO `DISCOVERED_BAK=` lines, verifies both, and lands `absorption_episodes.log`.
5. **Ledger row** in `docs/aws-collector-deploy-checklist.md` §5a: the new `InstanceId`, the deploy instant, and this sentence (per `docs/engine-fix-build-spec-2026-09-21.md` §8):

   > *One code edge at `<deploy instant UTC>`: the absorption `D-2` meaning change (`AbsorptionAggrUsd`, `AbsorptionRatio`), the `analysis_log.csv` rotation 116 → 124 columns, and the POC-tier gate fix (`TargetCapReason`, `Placed*`, about 1.68 % of verdicts); Session C moves rendered values only. Boundary count for the engine-fix build under `EF-1` (a): **TWO** — Session B (the liquidation flag, `D-4`/`D-5`) did not make this deploy and takes its own later boundary.*

6. **Watches after the deploy:** the flagged absorption rate rising while the ratio distribution shifts left means STOP (both specs' §0 trigger). After about two weekday-weeks, read the counting gap; outside roughly 20–45 % the `D-6d` diagnosis is wrong (`docs/d6d-episode-continuity-spec.md` §0).
