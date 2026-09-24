# Spec-back — absorption S1 (`D-2` + `D-6d` Stage 1 + the sidecar)

**Written:** 2026-09-24 (UTC), implementer seat, Opus, high. **Spec:** [`docs/absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §3, §4.1–§4.3, and [`docs/d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §4, §8. **Outcome record:** [`docs/absorption-d2-s1-batch-summary.md`](absorption-d2-s1-batch-summary.md). **Build commit:** `549b2c3`. **Base commit:** `ddebc96`.

**Legend — IDs in this packet:**

| ID | Source and kind | Meaning |
|---|---|---|
| `H-n` | This packet, handle | A check the reader can run; output pasted from this seat's run at `549b2c3` |
| `E-n` | This packet, evidence | Evidence the reader cannot re-run as committed (the mutated code is not in the tree) |
| `M1`–`M7` | This packet, mutation runs | See `E-1` |
| `D-2` | `docs/absorption-mechanism-revision-proposal.md` §6, trader ruling | Episode-cumulative pressing |
| `D-6d.1`, `D-6d.3` | `docs/d6d-episode-continuity-spec.md` §7, trader rulings | (c) sidecar and one CSV column · (c) `D-2` and Stage 1 ship together |
| `R-2`, `R-3`, `R-4`, `R-6` | `docs/absorption-d2-stage1-rotation-build-spec.md` §2, spec rulings | Dataset boundary · display parity fires on the strip only · no settings key · one shared predicate |
| `T-2`, `T-3`/`T-5` | `docs/d6d-episode-continuity-spec.md` §0 and the build spec §0, traps | Measure the drop, not the close · shadow must never reach `PressSum` |
| `A88a`–`A88f`, `A89a`–`A89b` | `verify/ordercheck/Program.vb`, new fixtures | Built as the spec's `A78a`–`A78f` and `A79a`–`A79b`; ids moved because both families were taken |

---

## 1. Ranked verification handles

**If you only run one, run `H-1`.** It runs all eight new checks and the 453 existing ones.

### H-1 — the harness

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

```
PASS  A88a the defect: live press 40000 is wiped by a LadderSpanLost close, the 25000 printed while idle is DR...
PASS  A88b ONE predicate: across ±4 USD of each level (0.25 steps, buys and sells, ABOVE and BELOW) the live ...
PASS  A88c close reasons distinguishable (LadderSpanLost / ProximityShut far and at the gate / TouchCrossed / ...
PASS  A88d behaviour neutrality: with idle prints (A) and without (B), every Snapshot field and every live-der...
PASS  A88e LastLevelPrice survives CloseEpisode (and Reset) while LevelPrice is zeroed — the shadow arm keep...
PASS  A88f sidecar: a locked path returns False without throwing; an open path gets exactly ONE line carrying ...
PASS  A89a D-2: two press batches (30000, then 20000 one window + 5 s later) in ONE episode are BOTH counted (...
PASS  A89b D-2 is a NO-OP below the window: at every read of an episode younger than window_sec, AggrUsd is bi...
SKIP  A81b known-defect repro (CalcLiquidations books a maker-side liquidation on the taker's side) — set ORDERCHECK_K...
ALL PASS
461 lines start with PASS
```

- Lines are cut with `...` for width only.
- **Arithmetic identity:** 453 (base `ddebc96`, measured this session) + 6 (`A88a`–`A88f`) + 2 (`A89a`–`A89b`) = 461.

### H-2 — the press queue, its cap and its prune are gone (declarations, not names)

```
grep -nE "Private (Shared )?Sub PrunePress|Const PressQueueCap|New Queue" Core/LevelAbsorptionTracker.vb; echo "exit=$?"
```

```
exit=1
```

- The names still appear in comments that explain the removal. This handle matches declarations only, per the `CLAUDE.md` rule "test the property, not a string that mentions it".

### H-3 — one predicate, two callers (`R-6`)

```
grep -n "ClassifyPrint(" Core/LevelAbsorptionTracker.vb
```

```
247:                Select Case ClassifyPrint(side.LastLevelPrice, price, band, breakTol, isBuy, side.IsAbove)
263:        Select Case ClassifyPrint(lvl, price, band, breakTol, isBuy, side.IsAbove)
288:    Friend Shared Function ClassifyPrint(level As Double, price As Double, band As Double,
```

- Line 247 is the shadow arm, line 263 the live arm, line 288 the definition.

### H-4 — every write to `PressSum` (`T-3`)

```
grep -nE "PressSum\s*(\+|-)?=" Core/LevelAbsorptionTracker.vb
```

```
182:            PressSum = 0.0
303:        side.PressSum += usd
413:            side.PressSum = 0.0
597:            .PressSum = side.PressSum,
```

- 182 is `CloseEpisode`, 303 is `AddPress` (live arm only), 413 is episode open, 597 is a READ into the drained instrument. No shadow-arm write.

### H-5 — only the run path drains the instrument

```
git grep -n "TakeInstrument(\|GetAbsorptionForRun(" -- '*.vb' ':!verify'
```

```
Core/LevelAbsorptionTracker.vb:580:    Friend Function TakeInstrument(nowMs As Long) As AbsorptionInstrumentRead
MarketState.vb:292:    Friend Function GetAbsorptionForRun(nowMs As Long, cfg As AbsorptionSettings,
MarketState.vb:296:            instrument = _absorptionTracker.TakeInstrument(nowMs)
UI/MainForm_Analysis.vb:487:            Dim absSnap = _marketState.GetAbsorptionForRun(
```

- `LiveMicrostructureEvaluator.vb` (the strip) still calls `GetAbsorption`, which never drains.

### H-6 — no settings change (`R-4`)

```
git diff ddebc96 549b2c3 -- settings.json | wc -l
```

```
0
```

### H-7 — the gate

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/verify-gate.ps1 -Mode local-fast
```

Tail, run before the commit on the same tree:

```
ALL PASS
OK    harness ALL PASS
=== display-parity ===
OK    no snapshot/card drift detected
=== version-bump ===
OK    engine path changed but [no-engine-change] token present
=== rotation-riders ===
OK    AnalysisLogger.vb not in the changed set - no header rotation possible
=== result ===
GATE PASSED
```

- ⚠ The `version-bump` line read the previous commit's message. `549b2c3` is an engine change with no settings bump (`R-4`), so a `prepush` run will print the WARN nudge for it. That is expected and is a nudge only.

### E-1 — the seven mutation runs (evidence; the mutated code is not committed)

| Run | Mutation | FAIL lines |
|---|---|---|
| `M1` | shadow arm disabled (`If False AndAlso …`) | `A88a` · `A88b` (74 mismatches) · `A88d` (`shadowExercised=False`) |
| `M2` | shadow arm calls its own copy with the band one tick (0.5) wider | **`A88b` alone** (4 mismatches, e.g. `ABOVE/100007.5/buy: live=Ignore shadow=Press`) |
| `M3` | `LadderSpanLost` returns `ProximityShut` (ABOVE) | `A88a` (`lsl=0/0/0`) · `A88b` (`lsl=False`) · `A88c` (`LSL=0,0,1,0,0,0,0`) |
| `M4` | shadow press also does `side.PressSum += amountUsd` | **`A88d` alone** (`take1: instr A=False;40000;…`) |
| `M5` | `CloseEpisode` clears `LastLevelPrice` | `A88a` · `A88b` · `A88d` · `A88e` (`idle=False/0/0`) |
| `M6` | `TryAppend` without `Try`/`Catch` | **`A88f` alone** (`threw=True`) |
| `M7` | the old rolling queue restored (10 s prune in `AddPress`, `FoldBookSide`, `Snapshot`) | **`A89a` alone** (`aggr=20000 epSec=16.5`); **`A89b` PASSES** — the parity half holds on the old code |

- Restore method: each run started from a saved clean copy and ended by copying it back. `cmp` against the clean copies printed `IDENTICAL` before the commit.
- ⚠ `M1`, `M3` and `M5` fail more than their target fixture. That is because `A88a`, `A88b` and `A88d` assert the shadow is exercised as a precondition. A mutation that silences the shadow must break them, or they would be vacuous.

---

## 2. Decisions queued, with my read

| # | Decision | Options | My read |
|---|---|---|---|
| **Q-1** | `window_sec` is now a dead tunable on the tweaker's ON surface (the `AbsorptionSettings` summary lists it, fence `HARD CONSTRAINT 23`) | (a) leave it until the key is deleted · (b) fence it in `SettingsDiffApplier` now | ⭐ **(a), hypothesis.** The tweaker's first fire is still data-gated. Fence and delete it in ONE later settings change, which `R-4` already names as separate. Reserved either way: it touches the settings surface |
| **Q-2** | `TouchCrossed` has no Stage 2 option in `docs/d6d-episode-continuity-spec.md` §5 | (a) add a row when Stage 1's read shows its share · (b) add it now | **(a).** Adding a fix option before any data is the design-invention the spec warns against. I have no read on whether crossing without a break should keep an episode open |
| **Q-3** | Acceptance item 6 (two real sidecar lines) | (a) check on the collector after the deploy · (b) run the app locally first | ⭐ **(a).** The tracked `settings.json` has `signal_bridge.enabled: true` with an output path under `C:\Dev\DeribitBridge\`. A local run writes the live bridge file. I did not run it |

✅ **RULED 2026-09-24 (UTC), trader, all as read:** `Q-1` = (a): leave `window_sec`; fence and delete it in ONE later settings change (reserved; not scheduled). `Q-2` = (a): add a `TouchCrossed` option only after the Stage 1 read. `Q-3` = (a): check the two sidecar lines on the collector after the deploy.

---

## 3. Spec-back proper

### 3.1 What the specs got right

- **`T-2`, "measure the DROP, not the CLOSE", decided the design.** The shadow arm sits at the idle early-out, not at the close. A close-time log would have measured nothing that matters.
- **`R-6`, one shared predicate, made `A88b` possible.** A sweep can only prove agreement if both arms call one function; `M2` shows a one-tick copy fails it alone.
- **`d6d-episode-continuity-spec.md` §8's warning on `A78a`'s shape** ("drive a real close, do not call the accumulator") was right and was followed.
- **§3's instruction on `PressQueueCap`** ("raise it or state why") forced the question. Removing the queue answered it with no residual truncation.

### 3.2 Assumptions that broke

- ⛔ **`d6d-episode-continuity-spec.md` §4.2 and the build spec §4.2: "`ProximityShut` and `LadderSpanLost` both reach `:278`, distinguished by whether `lvl = 0`."** Not in this code. With `lvl = 0`, the re-map test (`lvl <> side.LevelPrice`) fires first, because `0 <> LevelPrice`. The gate test is reached with an active side only when `lvl = LevelPrice`. And `lvl = 0` has two causes: the level fell beyond the far edge of the ten-deep ladder, OR the touch traded THROUGH the level (ABOVE: best ask above it) without a break-tolerance breach. The spec's split would file every crossing as `LadderSpanLost`, which is the suspect bucket that selects Stage 2's `F-1` or `F-3`. The build splits on geometry in `ClassifyRemapClose`:

  | Old level (ABOVE side) | Reason |
  |---|---|
  | below best ask | `TouchCrossed` (new seventh member) |
  | above worst ask AND within proximity of the touch | `LadderSpanLost` — only the ten-deep book closed it |
  | above worst ask AND beyond proximity | `ProximityShut` — price left; the ladder was not binding |
  | inside the visible span | `LevelRemap` |
  | still selected, gate shut | `ProximityShut` |

  ⭐ This also answers the question `d6d-episode-continuity-spec.md` §2.3 says "cannot be measured from stored data": how often the ladder, not the proximity gate, is the binding term.

- ⛔ **`A78d`'s named mutation ("let the shadow accumulator touch `PressSum` ⇒ fails alone") is invisible to a Snapshot comparison.** The shadow writes only while the side is idle; `ReadSide` returns an empty read for an idle side; and episode open zeroes `PressSum`. So the polluted value is never read by any Snapshot. `A88d` therefore also compares the drained instrument's live fields (`PressSum`, `PressAccruedUsd`, per-reason count, discarded USD, lifetime). `M4` fails there and only there. ⚠ A Snapshot-only `A88d` would have passed `M4`.
- ⚠ **"Per-reason counters … reset at each read" (`d6d-episode-continuity-spec.md` §4.2) does not say WHICH read.** Two readers call `Snapshot`: the live strip (every tick) and the run. Resetting in `Snapshot` would leave each run's line nearly empty. The build drains in a separate `TakeInstrument`, called only by the run.
- ⚠ **The spec's counting-gap ratio `ShadowPressUsd / (PressSum + ShadowPressUsd)` mixes an interval flow with an episode sum.** An episode that spans two runs puts its early flow in both runs' `PressSum`. The build adds `PressAccruedUsd` (live press accrued since the last drain), so the ratio is two interval flows and pools by summation.
- ⚠ **The build spec §4.4 names `AbsorptionRead` as the live source of the S2 column `AbsorptionShadowAggrUsd`.** `AbsorptionRead` is gated on `HasEpisode` and built from the primary active side. Shadow exists only on IDLE sides, so that source would be empty exactly when the value is non-zero. S2 sources the column from the drained instrument instead. Carried to the S2 packet.
- ⚠ **Fixture ids `A78` and `A79` were measured free at `828d868` and were taken by later builds before this one.** `A78a`–`A78f` belong to the coverage report and backtest tools; `A79a`–`A79p` to gap repair. Same class as the `A56b` collision `CLAUDE.md` records.

### 3.3 Where the spec was narrower than its words

- **Acceptance item 6 reads "a real run".** On this workstation a real run writes the live bridge file. It is really a post-deploy check on the collector, which is how `d6d-episode-continuity-spec.md` §9 item 6 words it ("against the live collector").

### 3.4 Constraint pairs that nearly conflicted

- **"Shadow must not touch any live field" vs "`A78d` must fail when it does".** They conflict for a Snapshot-only fixture (§3.2). The hatch is to compare the live-derived half of the drained instrument, which does read the idle side's `PressSum`.

---

## 4. What I did not verify

- ⚠ **No live run.** The sidecar has never been written by the real app. Its format is pinned only by `A88f`'s literal prefix and reason tokens.
- ⚠ **Close lifetimes mix clocks.** A trade-print break is stamped with the exchange time (`rec.Timestamp`); episodes open on receive time. The lifetime is clamped at zero; the skew is not measured.
- ⚠ **Whether the shadow counting gap lands in the 20–45 % band** that `d6d-episode-continuity-spec.md` §0 names as its escalation trigger. That is a read on post-deploy data.
- ⚠ **The "flagged rate up while the ratio shifts left" trigger** (both specs §0) cannot fire before live data exists. It is a post-deploy watch.
- ⚠ **I did not re-run the episode-age read's final 10-day pass** (`docs/absorption-episode-age-read-2026-09-13.md` §0 `H-1`). Its own §5 says the build does not wait on it; I carried that, and checked only that the date gate closed before the outage.
- ⚠ **Per-print cost of the shadow arm on idle sides** — one extra predicate call per print per idle side. Not measured; judged negligible against the existing fold.
