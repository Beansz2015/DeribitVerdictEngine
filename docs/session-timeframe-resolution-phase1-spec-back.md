# Session-Timeframe Resolution (v36 Phase 1) — Implementer Spec-Back

**Date:** 2026-06-16
**Seat:** implementer (Opus 4.8, fresh conversation).
**Build spec:** [`session-timeframe-resolution-implementer-handoff.md`](session-timeframe-resolution-implementer-handoff.md) (authoritative). Supporting: [`-proposal.md`](session-timeframe-resolution-proposal.md), [`-spec-back.md`](session-timeframe-resolution-spec-back.md), [`-spec-writer-brief.md`](session-timeframe-resolution-spec-writer-brief.md).
**Status:** **IMPLEMENTED — local commits only.** Solution + AutoTweaker + verify harness all build clean; A1–A13 unregressed; A14a–A14i green. **Routes to the COORDINATOR seat for independent review** (per hand-off §0/§10). Trader compiles, tests live, and pushes — I did not push.

**One material deviation from the hand-off's blanket find-replace rule** (the OI `priceUp` window) — flagged in §3 below; it's the one thing to put your eyes on. Everything else implements the spec as written.

> **Coordinator review (2026-06-16, sanity-check seat) — APPROVED; cleared for trader live-test + push.** Independent verification, not a doc-check:
> - **Re-ran the builds + harness:** solution + AutoTweaker + `verify/ordercheck` all build **0/0**; the harness runs **29 checks (A1–A13 unregressed + A14a–A14i) ALL PASS**.
> - **Source-verified the 6 load-bearing claims:** 2-key ROC seed (`resolution_profiles["3"]` = 0.21/0.105 only, CVD/MicroCVD untouched); `MatchSessionBucket` inclusive `<=` + Enabled-independence (resolver null-guarded; `ApplySessionVolume` keeps its `Enabled` early-return for the multiplier while sharing the matcher; A14i guards it); EXEC-tag display-parity on `_atrSubHeader` **and** `BuildPlaintextSnapshot` in the same commit + `ResolveSessionLabel` hour-7 fix; host-agnostic resolver in `Core/`; §10a `Save(... bumpVersion:=False)` on the three operational saves; §10b tweaker `settings_path`→bin (held safe at `auto_commit:false`/`dry_run:true`).
> - **OI `priceUp` deviation (§3): CONFIRMED correct — keep on `candles1m`.** `OIChange15m` is a wall-clock 15-min `_oiHistory` ring-buffer delta (resolution-independent); proposal §4 classes "funding/OI (slow)" as leave-at-1-min. The deviation is from the hand-off's over-broad blanket rule, **not** the approved design → coordinator-resolvable, no trader escalation. **One non-blocking micro-finding (Phase-2 optional):** `priceUp`'s *"now"* endpoint reads `r.CurrentPrice` (now the 3-min last close) while *"15 min ago"* reads `candles1m(count-16)` — a ≤2-min cross-resolution endpoint lag on a coarse 4-state signal; could read `candles1m.Last().Close` for full 1m-grid coherence. Immaterial; `r.CurrentPrice = 3-min close` is correct everywhere else (ATR levels / Kelly / min-move / VPFR).
> - **Live render verified (the in-harness-unverifiable surface):** ran the built app under a forced 3-min resolution (NY bucket temporarily flipped to 3, with full backup-and-restore of the bin `settings.json` + `analysis_log.csv` + `analysis_eval_cache.csv` — the live book was byte-restored to 1641/3291 rows afterward). **`EXEC 3m` renders correctly on both surfaces** — card session label (`NY session · EXEC 3m`) and ATR sub-header (`ATR 60.11  size ×1.23 | 1.2× stop / 2.0× target | EXEC 3m`), plus the `BuildPlaintextSnapshot` block — with ATR computed on 3-min (60.11) and the 5m/15m regime + 15m MTF layer unchanged. The v36 CSV v0.6→v0.7 rotation + eval v3→v4 re-stamp fired as designed during the test (then reverted). **Note for the trader's first real launch:** those migrations are still pending on the live book — they fire on first run; the old v0.6 data is preserved in `analysis_log.csv.v0.6.bak`.

---

## 1. What was built (every §10 checklist item)

| Checklist item | Done | Where |
|---|---|---|
| `settings.json` v35→36 + `change_log` (newest-first) + `last_modified`/`modified_by` | ✅ | [settings.json](../settings.json) |
| `sessions[].execution_resolution` 3/3/1 + `resolution_profiles` block (`"1"` empty, `"3"` = ROC 0.21/0.105) | ✅ | settings.json |
| §10 version + bin/root reconciliation **(a)** operational saves don't bump version/change_log **(b)** tweaker `settings_path`→bin-copy | ✅ | [SettingsLoader.vb](../Core/Settings/SettingsLoader.vb) + 3 call sites; [tweaker_config.json](../tools/AutoTweaker/tweaker_config.json) |
| POCO: `SessionBucketSettings.ExecutionResolution`, `ResolutionProfile`, `EngineSettings.ResolutionProfiles` | ✅ | [EngineSettings.vb](../Core/Settings/EngineSettings.vb) |
| `Core/ExecutionResolution.vb` (host-agnostic) + `DynamicNorms.ApplySessionVolume` shares `MatchSessionBucket` | ✅ | [ExecutionResolution.vb](../Core/ExecutionResolution.vb), [DynamicNorms.vb](../DynamicNorms.vb) |
| `IndicatorResults.ExecResolution` (+ `CVDWeightedSlope`) | ✅ | [IndicatorResults.vb](../Core/IndicatorResults.vb) |
| Fetch + exec-stack rewiring (§4) + ROC override sites in `Core/ScoringEngine_*.vb` (§4.4) | ✅ | [MainForm_Analysis.vb](../UI/MainForm_Analysis.vb), [ScoringEngine_Calculate_Scoring.vb](../Core/ScoringEngine_Calculate_Scoring.vb) |
| CSV v0.6→v0.7 + eval cache v3→v4 + resolution-filtered aggregation (§5) | ✅ | [AnalysisLogger.vb](../AnalysisLogger.vb), [LivePerformanceTracker.vb](../LivePerformanceTracker.vb) |
| PromptBuilder HARD CONSTRAINT 11 adds `execution_resolution` | ✅ | [PromptBuilder.vb](../tools/AutoTweaker/PromptBuilder.vb) |
| **MANDATORY EXEC tag** in card binding **and** `BuildPlaintextSnapshot` (same commit) + `ResolveSessionLabel` hour-7 off-by-one fix | ✅ | [MainForm_Render_Cards.vb](../UI/MainForm_Render_Cards.vb), [MainForm_PlaintextSnapshot.vb](../UI/MainForm_PlaintextSnapshot.vb) |
| A14 fixtures pass; A1–A13 unregressed; `dotnet build` clean | ✅ | [verify/ordercheck/Program.vb](../verify/ordercheck/Program.vb) |
| `DeribitIndicatorProject.md` §15 v36 row + §6 →v36; §12 WATCHING (Phase-2 ROC + tweaker resolution-awareness; weekday-ASIA → 3-min re-verify) | ✅ | [DeribitIndicatorProject.md](DeribitIndicatorProject.md) |
| Local commit only; host-agnostic respected (resolver in `Core/`, fetch UI-side) | ✅ | — |

**Threshold profile honoured the hand-off over the proposal:** only the **2 ROC keys** scale ×2.1 (`roc_magnitude_threshold` 0.1→0.21, `roc_slope_delta_threshold` 0.05→0.105). CVD `slope_min_usd` and MicroCVD `accel_threshold` stay at 1-min values — they gate the fixed 500/50-trade stream, not candles.

**Resolution threading.** `r.ExecResolution` is stamped in `RunAnalysisAsync` right after `execRes` is resolved and before `ScoringEngine.Calculate`, so the three `MagnitudeThreshold` read sites resolve via `ExecutionResolution.ResolveRocMagnitude(cfg, r.ExecResolution)` — no new `Calculate` parameter, no cfg mutation, no singleton race. The slope-delta override is applied UI-side at the single `SlopeDeltaThreshold` read site.

**Exec-stack scope (candlesExec).** Moved to `candlesExec`: CurrentPrice, ATR, DynamicNorms.Compute, ROC series, RSI, Volume SMA + last volume, VWAP + bands, BBW, TTM, EMA ribbon (9/21/50), Donchian, OBV, RSI-divergence, VPFR, and CalcCVD's candle arg (divergence price-gate only). **Stayed fixed (HTF/other layer):** CalcDMI(candles5m), CalcEMA(candles5m,200), CalcMTFGate(candles15m), CalcSwingPivots(5m/15m), ClassifyTrendStructure(candles5m), the 1m OHLC cache + eval barrier walk (`LivePerformanceTracker.UpdateAsync(..., candles1m, ...)`), `lastTradePrice`, and the OI `priceUp` 15-bar lookback (see §3).

---

## 2. Migration / backward-compat decisions

- **CSV v0.7:** `ExecResolution` + `CVDWeightedSlope` **appended** (header-name readers tolerate them). Superseded v0.6 files rotate to `analysis_log.csv.v0.6.bak`. `ParseAnalysisLog` reads `ExecResolution` as **optional** (NOT added to the required-column set), defaulting to **1** when absent — so a legacy v0.6 CSV still backfills cleanly.
- **Eval cache v3→v4:** `ExecResolution` appended as the 8th column. `EVAL_SCHEMA_COMMENT` bumped to v4 but **retains the `min-tradeable-move` substring + `floor_pct=` tail** so `IsPreV3Schema`/`ReadSchemaFloorPct` still classify a v4 file correctly. New `IsPreV4Schema` (header lacks `ExecResolution`) drives a one-time Step-2.7 re-stamp; `ParseEvalLine` defaults a missing 8th column to **1** (coordinator §5 legacy default). `EvalCacheEntry.ExecResolution` defaults to 1.
- **Resolution-filtered aggregation:** I extracted the per-entry counting loop into a pure `Friend Shared AggregateRange(entries, rangeStartUtc, rangeEndUtc, resolutionFilter)` so it's unit-testable (A14f). The **session windows** (Asia/London/NY) filter by each session's configured `ExecutionResolution`; the **time windows** (week/3d/today) pass `0` (no filter — they're cross-session/mixed-resolution by nature). This is the safety net that stops a session rate blending pre-v36 1-min Asia rows with post-v36 3-min Asia rows.

---

## 3. The one material deviation — OI `priceUp` stays `candles1m` (NOT `candlesExec`)

**The hand-off §4.3 gives a blanket rule:** *"every `candles1m` consumer in the indicator-fill block moves to `candlesExec` EXCEPT the four in §4.4."* The OI `priceUp` check ([MainForm_Analysis.vb](../UI/MainForm_Analysis.vb), `priceUp = r.CurrentPrice > candles1m(candles1m.Count - 16).Close`) is in that block and is **not** one of the four §4.4 exceptions — so the literal blanket rule says move it. **I deliberately kept it on `candles1m`.**

**Why (and why this is faithful to the design, not a re-opening of it):**
- `OISignal` pairs `OIChange15m` — a **wall-clock 15-minute** OI delta read from the `_oiHistory` ring buffer (timestamp-keyed, resolution-independent) — with a price-direction boolean. The 15-bar lookback was specifically chosen so the price window matches *"the same 15m window that OIChange15m measures"* (the existing comment says exactly that).
- On `candlesExec` at 3-min, a 15-bar lookback is **45 minutes**, mismatching the 15-minute OI delta → `NEW LONGS` vs `NEW SHORTS` would be decided over the wrong horizon.
- **The proposal §4 explicitly lists OI as resolution-stable:** *"Likely stable (relative/structural — leave at 1-min): ... funding/OI (slow)."* So keeping OI's price helper on the 1m cache is what the approved design intends; moving it would have been a find-replace bug.

`candles1m` is always fetched and always present (it gates the skip + feeds the eval walk/cache), so `candles1m(count-16)` is always available. `r.CurrentPrice` (now the 3-min last close) ≈ the 1m last close ≈ current mark, so `priceUp = "is now higher than 15 wall-clock minutes ago"` stays coherent. Documented inline with a `[v36]` comment.

**Coordinator ask:** confirm you agree OI's `priceUp` belongs on `candles1m` (proposal §4 "OI slow / leave at 1-min"), or flag if you'd rather it move to `candlesExec` for stack-coherence. My lean: keep — the 15-min OI-window pairing is the stronger constraint.

---

## 4. Acceptance results

```
dotnet build DeribitVerdictEngine.sln          → Build succeeded, 0 warnings, 0 errors
dotnet build tools/AutoTweaker/AutoTweaker.vbproj → Build succeeded, 0/0
dotnet build verify/ordercheck/OrderCheck.vbproj  → Build succeeded, 0/0
dotnet run --project verify/ordercheck            → ALL PASS (29 checks)
settings.json                                     → parses; version 36; sessions 3/3/1; profile "3" 0.21/0.105
```

**A14 coverage:**
- **A14a** resolution selection — hr3/hr7→3 (hour-7 ASIA-inclusive guards the off-by-one), hr10→3, hr13/hr23→1.
- **A14b** ROC override — mag 3→0.21/1→0.1, slope 3→0.105/1→0.05.
- **A14c** ATR on 3-min flips the gate — `CalcATR` on 3×-range candles ≈27 (target 54 > floor 49.6 → SHORT stands) vs 1-min ≈13 (target 26 < floor → NO TRADE/BELOW_MIN_MOVE).
- **A14d** NY byte-identical guard — res=1 ⇒ ROC overrides equal the global thresholds.
- **A14e** per-row stamp — ASIA→3, NY→1; `IndicatorResults`/`EvalCacheEntry` default to 1.
- **A14f** resolution-filtered aggregation — one window, two resolutions → res3 = 2/1, res1 = 1/1, all = 3/2 (never blended) via the real `AggregateRange`.
- **A14g** freshness honours resolution — 3-min: 5-min fresh / 7-min stale; 1-min: 3-min stale (unchanged).
- **A14h** regime/MTF unchanged — 15m gate resolution-independent (BEAR/blockLong/passShort).
- **A14i** resolution survives `session_volume.enabled=false` — ASIA still 3 (guards the §3 separation).

**Harness linkage note:** A14e/A14f call the live `LivePerformanceTracker.AggregateRange`, so the harness `.vbproj` now also links `LivePerformanceTracker.vb` + its compile deps (`OhlcCache.vb`, `analysis/FailureRateMatrix.vb`, `AnalysisConstants.vb`, `AnalysisReport.vb`, `ForwardWindowJoiner.vb`) and `Core/ExecutionResolution.vb`. The root `.vbproj`'s `Compile Remove verify/**` keeps the harness out of the solution build.

**Not verified in-harness (verified by the live run instead):** the on-disk CSV v0.7 / eval v4 *column presence* (the harness asserts the stamp value + the aggregation that consumes it, not file I/O) and the EXEC-tag *rendering* (WinForms-coupled). The schema writes are mechanical and the build proves the columns are emitted; the trader's first live run will show `EXEC 3m` on Asia/London and the new CSV/eval columns.

---

## 5. Display-parity (CLAUDE.md hard rule) — discharged

The EXEC tag lands on both renderer surfaces in this commit:
- **Snapshot** (`BuildPlaintextSnapshot`): `ATR ENTRY LEVELS (... | EXEC {res}m)`.
- **Card** (`BindCardAtrLevels` → `_atrSubHeader`): `... | EXEC {res}m`.
- Plus the card session label: `{SESSION} session · EXEC {res}m` (card-only; the snapshot has no session line, so no parity obligation there).

`ResolveSessionLabel` now routes through `ExecutionResolution.MatchSessionBucket` so the displayed session label uses the exact engine bucket (fixes the v34 `<7` off-by-one — hour 7 was mislabelled LONDON; engine bucket is ASIA 0-7 inclusive).

---

## 6. Out of scope (confirmed not done)

Per hand-off §9 + the task scope: weekend resolution overlay; native 5-min use (the `"5"` profile is documented-but-unpopulated); profile ATR-band recalibration; **Phase 2** = per-resolution re-baseline (the 2.1× ROC proxy needs settling) + the auto-tweaker `(session × resolution)` filter. The `ExecResolution` stamp is the Phase-1 enabler that makes the Phase-2 tweaker filter possible. Both are recorded in §12 WATCHING.

**Sequencing reminder for the trader (hand-off §6.1, hard ordering):** run the v35 NY/1-min supervised first-fire dry-run **before** these 3-min Asia/London rows accumulate in `analysis_log.csv` — once 3-min rows interleave, the resolution-blind tweaker slicer can't isolate a clean 1-min window.
