# Spec-back — absorption S2 (the 2026-09 `analysis_log.csv` rotation)

**Written:** 2026-09-24 (UTC), implementer seat, Opus, high. **Spec:** [`docs/absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §4.4–§6. **Outcome record and deploy plan:** [`docs/absorption-d2-s2-batch-summary.md`](absorption-d2-s2-batch-summary.md). **Build commit:** `5dfc91a`. **Base:** `14b6179` (S1's reports). **S1 packet:** [`docs/absorption-d2-s1-spec-back.md`](absorption-d2-s1-spec-back.md).

**Legend — IDs in this packet:**

| ID | Source and kind | Meaning |
|---|---|---|
| `H-n` / `E-n` | This packet | Runnable handle / evidence the reader cannot re-run as committed |
| `M8`–`M14` | This packet, mutation runs | See `E-1` |
| `RIDER-1`…`RIDER-9` | `docs/csv-rotation-riders.md` §1, ledger rows | The changes parked on this rotation; `RIDER-8` was not carried |
| `R-1`, `R-2`, `R-3` | Build spec §2, rulings | Append at the end · a dataset boundary · display parity |
| `T-1`, `T-4`, `T-6`, `T-7` | Build spec §0, traps | WS health sampled twice · three schema copies · wrong `ColKind` · deploy before the ops scripts see a second book |
| `RD-1` | Build spec §9, trader ruling | (b) a separate `SettingsLoadError` column |
| `EF-1` | `docs/engine-fix-build-spec-2026-09-21.md` §6, trader ruling | (a) one deploy for the engine fixes and absorption |
| `A89c`–`A89h`, `A60e` | `verify/ordercheck/Program.vb` | New fixtures (spec ids `A79c`–`A79f`), and the extended schema-copy fixture |

---

## 1. Ranked verification handles

**If you only run one, run `H-1`.** It covers every new column's value, the rotation name, the schema copies and the replay twin.

### H-1 — the harness

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

```
PASS  A60e writer-header parity across all THREE schema copies (AnalysisLogger == BacktestRowWriter byte-for-byte == ...
PASS  A60e (2026-09 rotation) the eight appended columns sit once each in all three copies, each with its ruled ColKind ...
PASS  A60d R1 guard — no existing column moved (AbsorptionSignal 101, AbsorptionPullFrac 105, InstanceId 110, SignalId 111 ...
PASS  A89c LogRun writes r.TriggerMode and r.WsHealth verbatim, and EMPTY — never MANUAL or OK — when either is unstamped
PASS  A89d RIDER-1: the .bak is named <N>col-<h8>.<utc>.bak from the SUPERSEDED header — same-width headers differ, ...
PASS  A89e RIDER-5 + RD-1: SettingsVersion is the PASSED cfg's version (4242, not SettingsLoader.Current's) and ...
PASS  A89f RIDER-7: LogRun writes r.RecentTradeCount verbatim (57, and a real 0 as 0) and EMPTY when it is Nothing ...
PASS  A89g R-1 guard: the eight new columns append contiguously at 117-124 (1-based) in the ruled order, ...
PASS  A89h T-4 on values + RIDER-9: the replay writer's eight appended cells equal LogRun's byte-for-byte ...
ALL PASS
468 lines start with PASS
```

- **Arithmetic identity:** 461 (after S1) + 1 (`A60e`'s second check) + 6 (`A89c`–`A89h`) = 468.

### H-2 — the header, before and after (the gate's own extractor)

```
powershell -NoProfile -Command ". tools\checks\lib\HeaderText.ps1; foreach($rev in '14b6179','5dfc91a'){ $h = Get-HeaderText ([string[]](git show ($rev + ':AnalysisLogger.vb'))); $rev + ' ' + $h.Split(',').Length + ' ' + (($h.Split(',') | Select-Object -Last 8) -join ',') }"
```

```
14b6179 116 PlacedStopShort InstanceId SignalId AbsorptionEpisodeSec AbsorptionPullLB AbsorptionPostLB AbsorptionSizeStart AbsorptionSizeMin
5dfc91a 124 AbsorptionShadowAggrUsd TriggerMode WsHealth SettingsVersion SettingsLoadError RecentTradeCount VPFRSignal VPFRPoc
```

### H-3 — the rider gate on the pushed range

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/verify-gate.ps1 -Mode prepush
```

```
=== rotation-riders ===
OK    HEADER ROTATION in range (116 -> 124 columns) and docs/csv-rotation-riders.md was updated
=== result ===
GATE PASSED
```

- ⚠ The gate proves the ledger was TOUCHED, not that its content is right (ledger §0 rule 2). The content check is: every carried rider shows `CONSUMED 2026-09-24`, and `RIDER-8` still shows `CONDITIONAL`.

### H-4 — Kelly calibration unchanged (`T-7`, acceptance 7a)

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/kelly-trigger-read.ps1 -Mode Calibrate
```

```
CALIBRATION PASSED - total=49 (expected 49), 09-02=14, 09-03=21
```

### H-5 — the new pooled read on a real fetch (this workstation only: `aws_fetch/` is untracked)

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/kelly-trigger-read.ps1 -Mode LocalDir -Dir aws_fetch/20260924-084613
```

```
  rotated books found in aws_fetch/20260924-084613: 1
    analysis_log.csv.v0.7.bak
  BOOKS                = 2
  TOTAL                = 532   against >= 406
  no overlap: analysis_log.csv.v0.7.bak ends 2026-09-01 15:48:01, analysis_log.csv (live) starts 2026-09-01 15:50:01
  every adjacent pair is clean - sum is sound
  TRIGGER MET - 532 >= 406
```

- Per book: `.v0.7.bak` 337, live 195. The counting script is unchanged, so the old two-file read gives the same 337 + 195.

### H-6 — `LogRun`'s signature did not change (escalation trigger 2)

```
grep -n "Public Shared Sub LogRun" AnalysisLogger.vb; git diff 14b6179 5dfc91a -- AnalysisLogger.vb | grep -c "^[-+].*Sub LogRun"
```

```
279:    Public Shared Sub LogRun(r As IndicatorResults, v As VerdictResult, cfg As EngineSettings)
0
```

### H-7 — `WsHealth` is derived once per completed run (`T-1`, review half)

```
grep -n "CurrentBridgeWsHealth(" UI/*.vb
```

```
UI/MainForm_Analysis.vb:671:        r.WsHealth = CurrentBridgeWsHealth(cfg)
UI/MainForm_Layout.vb:417:            WsHealthLog.LogStart(CurrentBridgeWsHealth(SettingsLoader.Current),
UI/MainForm_SignalBridge.vb:68:        Dim wsHealth As String = If(r.WsHealth, CurrentBridgeWsHealth(cfg))
UI/MainForm_SignalBridge.vb:94:        Dim wsHealth As String = CurrentBridgeWsHealth(cfg)
UI/MainForm_SignalBridge.vb:124:    Friend Function CurrentBridgeWsHealth(cfg As EngineSettings) As String
```

- 671 is the one derivation for a success run. 68 reads it (the fallback fires only if a path forgot to stamp). 94 is the skip path, now one derivation. 417 is the startup line, not a run. 124 is the definition.

### H-8 — no settings change

```
git diff ddebc96 5dfc91a -- settings.json | wc -l
```

```
0
```

### E-1 — mutation runs (evidence)

| Run | Mutation | FAIL lines |
|---|---|---|
| `M8` | `RotationCells` writes `OK` for a `Nothing` `WsHealth` | `A89c` (`unstamped=[\|OK]`) · `A89h` |
| `M9` | `EnsureLogFile` restores the `v0.7` literal | **`A89d` alone** |
| `M9b` | `RotatedBakName` drops the hash | **`A89d` alone** (`shapeOk=False`, both names `analysis_log.csv.3col.20260924_170509.bak`) |
| `M10` | `SettingsVersion` read from `SettingsLoader.Current` | `A89e` (`ver=[1\|1\|1]`) · `A89h` |
| `M11` | `RecentTradeCount` writes `0` for `Nothing` | **`A89f` alone** (`cells=[57\|0\|0]`) |
| `M12` | `TriggerMode` `ColKind` → `Categorical` | **`A60e` (rotation check) alone**; `A43e` PASS |
| `M12b` | `VPFRPoc` renamed in `ColSpec` | `A60e` (both checks); `A43e` PASS |
| `M13` | `BacktestRowWriter` gets its own formatter with `VPFRPoc` at `F1` | **`A89h` alone** (`twin=[…,61234.6]`) |
| `M14` | `TriggerMode`/`WsHealth` swapped in all three copies | `A89g` · `A89c`; `A60e` PASS — only the frozen-position guard sees a consistent reorder |

### E-2 — `collector.ps1 fetch`'s box-side commands, run against a local folder

A scratch script extracted the REAL `$remoteCmds` text from `tools/ops/collector.ps1`, stubbed `aws`, and ran it with `$dir` pointing at a folder holding the live file, `analysis_log.csv.v0.7.bak`, `analysis_log.csv.116col-abcdef12.20260925_091500.bak`, `ws_health.log` and a decoy `other.bak`:

```
DISCOVERED_BAK=analysis_log.csv.v0.7.bak
DISCOVERED_BAK=analysis_log.csv.116col-abcdef12.20260925_091500.bak
MANIFEST_FILE=analysis_log.csv|4|2
MANIFEST_FILE=analysis_log.csv.v0.7.bak|6|-1
...
MANIFEST_FILE=analysis_log.csv.116col-abcdef12.20260925_091500.bak|8|-1
SNAPSHOT_CLEANED=true
```

- The decoy was not picked up. ⚠ The LOCAL verification loop (`$verifyFiles`) was reviewed, not run: it needs a real download.

### E-3 — the Kelly pool on synthetic three-book folders

A clean folder (three books, spans in order) printed `BOOKS = 3`, two `no overlap` lines and `every adjacent pair is clean`. The same folder with the live file starting inside the second book's span printed `WARNING: SPANS OVERLAP - analysis_log.csv.116col-… ends 2026-09-25 09:00:00 but …`.

---

## 2. Decisions queued, with my read

| # | Decision | Options | My read |
|---|---|---|---|
| **Q-1** | The `tools/BacktestRunner/` edit conflict (batch summary §0) | (a) accept the three schema-file edits · (b) revert S2 and re-run it after the other seat finishes | ⭐ **(a).** `T-4` makes those files part of the rotation, and the gap-repair files were untouched. I have no read on what the brief intended beyond its wording |
| **Q-2** | `window_sec` is a dead tunable on the tweaker surface | carried from the S1 packet, `Q-1` there | Same read: fence and delete in one later settings change |
| **Q-3** | Row counts in the `fetch` manifest for rotated books (`-1` today) | (a) keep `-1` · (b) count lines on the box | **(a), hypothesis.** Size verification is the property; a line count of a 31 MB file is new load on a 1 GiB box beside the running probe |
| **Q-4** | The advisory rider-travel check's first run (`docs/rider-travel-check-spec.md` §1: "runs before that commit") | (a) run it now, before the push, against the pre-rotation ledger · (b) skip it for this rotation | ⭐ **(a), before the push.** It is still possible: `-BeforeRev 14b6179 -AfterFile AnalysisLogger.vb -LedgerPath <the ledger as of 14b6179>`. The baseline must be written first, by whoever runs it. Not run by me: its first run is a one-time measurement |
| ~~**Q-5**~~ | `absorption_episodes.log` was not in `collector.ps1`'s `$FetchFiles` | (a) add it · (b) leave it | ✅ **AUTO-PROCEEDED (a)** in the follow-up commit: one line in `tools/ops`, undone by one revert, no live surface. Without it the Stage 1 read could not be fetched. Listed so you can overrule it |

✅ **RULED 2026-09-24 (UTC), trader, all as read:** `Q-1` = (a): the three `tools/BacktestRunner/` schema edits are accepted (orchestrator checked: gap-repair files are not in the diff). `Q-2`: as the S1 packet's `Q-1`. `Q-3` = (a): keep `-1`. `Q-4` = (a): run the rider-travel check before the push; the orchestrator runs it, baseline first. `Q-5`: auto-proceed not overruled. ⛔ **The deploy is HELD by the trader** (2026-09-24).

---

## 3. Spec-back proper

### 3.1 What the spec got right

- **`T-4` named all three copies AND the replay path.** Without the `ColSpec` note the overlap check would have silently compared the wrong columns.
- **`T-6`'s `ColKind` table was exact.** `M12` shows a compiled-but-wrong kind is caught only by an explicit kind check, which `A60e` now carries.
- **Rider 1's pure-function instruction** (`RotatedBakName` without the file system) made `A89d`'s shape and hash halves cheap, and left room for the end-to-end half.
- **`T-1`'s correction** (the stamp is not bridge-gated) was right; the derivation point before `LogRun` is correct for both the enabled and the disabled bridge.

### 3.2 Assumptions that broke

- ⛔ **"Six new columns."** The ledger had eight travelling riders' worth by the time of the build; `RIDER-9` added two columns on 2026-09-16. The spec's §1 count, `R-1`'s order and §5's `A60e` wording ("the four new columns") are all stale. The brief pointed this out; the spec should carry a pointer to the ledger instead of a count.
- ⛔ **§4.4 names `AbsorptionRead` as the shadow column's source.** Mechanically wrong (see the S1 packet §3.2): shadow exists only on idle sides.
- ⚠ **§6 item 7a asks for a live `fetch` before the deploy.** That runs commands on the production box, which this seat treats as the trader's. It is step 1 of the deploy plan instead.
- ⚠ **Fixture ids again** — `A79c`–`A79f` in the spec are `A89c`–`A89f` here, plus `A89g` (position guard) and `A89h` (value twin), which the spec did not list.

### 3.3 Where the spec was narrower than its words

- **`A60e` "extend it"** — the spec asked only for names. Names in three byte-equal copies are already covered by the first check; the new value is the `ColKind` assertion (`T-6`), and a consistent reorder is invisible to `A60e` altogether (`M14`). `A89g` covers the reorder.
- **Rider 3 and rider 4 are review-only**, as the spec says. `H-7` and the `_pendingTrigger` sites are the review handles.

### 3.4 Constraint pairs that nearly conflicted

- **"Do not edit `tools/BacktestRunner/`" (brief) vs `T-4` (spec).** Resolved by scope: the prohibition named a read-only gap-repair investigation; the three schema files are not gap-repair code. Named in the summary §0 and queued as `Q-1`.
- **`A60d` "never fix the frozen literals" vs a header that grows.** The hatch: `A60d`'s claim is POSITIONS, which did not move; only its width test ("exactly 116") had to become "at least 116", and `A89g` takes over the exact width.

---

## 4. What I did not verify

- ⚠ **The UI plumbing never ran.** `OrderCheck` compiles no `MainForm_*.vb`, and I did not start the app (a local run writes the live bridge file). `TriggerMode`, `WsHealth`, `SettingsLoadError`, `RecentTradeCount` and the shadow column are verified by review and by what `LogRun` writes, not end to end.
- ⚠ **`collector.ps1`'s local verification loop** — reviewed, not run (`E-2`).
- ⚠ **`kelly-trigger-read.ps1 -Mode Box`** — not run; it reads the production box. `Get-RemoteBooks` has never executed over SSM.
- ⚠ **The rotation on the box** — the predicted name `analysis_log.csv.116col-83564b1b.<stamp>.bak` rests on the header line of the 2026-09-24 fetch being byte-identical to the box's live header today.
- ⚠ **Readers of `analysis_log.csv` beyond the three schema copies** — all known readers resolve by header name (carried from the 2026-09-01 rotation's audit, not re-audited). A positional reader would still read correctly, because nothing moved.
- ⚠ **I did not re-audit engine-fix Sessions A and C.** The deploy plan lists them from their commits and packets only.
