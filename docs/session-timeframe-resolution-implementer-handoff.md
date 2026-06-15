# Session-Timeframe Resolution — Implementer Hand-off (v36)

**Date:** 2026-06-15
**From:** spec-writer seat (finalized the threshold profile + coordination).
**To:** implementer seat (Opus, fresh conversation, this doc + the two below as kickoff).
**Status:** **APPROVED — build spec.** The *what* was trader-signed 2026-06-15 (Path B; ASIA=3 / LONDON=3 / NY=1; weekend overlay OUT). The *how* (this doc) is finalized; the **threshold profile was re-confirmed with the trader 2026-06-15** (2.1× ROC seed; CVD/MicroCVD unchanged). Scoring-affecting → spec-first, approval-gated; **local commits only, trader tests + pushes.**
**Reads with:** [`session-timeframe-resolution-proposal.md`](session-timeframe-resolution-proposal.md) (the approval artifact / the *why*), [`session-timeframe-resolution-spec-writer-brief.md`](session-timeframe-resolution-spec-writer-brief.md) (coordination brief). **Settings v35 → v36.**

This doc resolves every open design question the brief §5 handed the spec-writer, and folds in the §4 coordination decisions. It is the build spec; the proposal carries the rationale. Where this doc and the proposal disagree on *which keys scale*, **this doc wins** (§1 — the proposal's §4 was internally contradictory; the code settled it).

> **Coordinator review (2026-06-15, sanity-check seat).** Independent build-audit of this hand-off — the four load-bearing code claims (ROC override sites in `Core/ScoringEngine_*.vb`; `MatchSessionBucket` boundary + Enabled-independence in `DynamicNorms.ApplySessionVolume`; the `IsFresh` resolution param; `SessionBucketSettings` location) all verify against source. **PASSED.** Four edits applied below: §4.5 heading (CalcCVD's candle arg moves to `candlesExec`), §5 eval-cache v3→v4 legacy default, §8 A14i (resolution survives `session_volume` disabled), §10 EXEC tag promoted to MANDATORY. Design unchanged. The implementer spec-back routes to the **coordinator seat** (the spec-writer seat is retired once this is final); a genuine *design* question escalates to the trader (approval-gated).

---

## 0. Scope in one paragraph

The engine reads a per-session execution resolution (1/3/5 min). On each run it resolves the active session from the UTC hour and fetches + computes the **execution-indicator stack** at that resolution — including ATR. ASIA and LONDON move to 3-min; NY stays 1-min (byte-identical to v35). The 5-min DMI/ADX regime and the 15-min MTF gate are **unchanged** — they're the valid higher-timeframe layer above a 3-min chart. Swing pivots stay 5m/15m, so structural targets/stops are unaffected. The v35 min-tradeable-move gate, ATR levels, eval barriers, and Kelly all inherit the resolution automatically because they derive from `r.ATR`, now computed on the execution candles.

---

## 1. Finalized threshold profile (trader-confirmed 2026-06-15)

**The single most important finding — a correction to the approved proposal.** The proposal §4 listed four "magnitude-type" keys to scale ×2.1 (ROC magnitude, ROC slope-delta, MicroCVD `accel_threshold`, CVD `slope_min_usd`) but *also* said trade-stream indicators are "unaffected by candle resolution." Those contradict. The code is decisive — **only the two ROC keys scale:**

- `CalcCVD` ([Core/Indicators_OrderFlow.vb:167](Core/Indicators_OrderFlow.vb:167)) segments the **fixed 500-trade stream** (`GetRecentTradesAsync(500)`); it touches candles only for the divergence price-gate. `slope_min_usd` (12 000) gates USD flow over those 500 trades — **identical at 1m or 3m**.
- `CalcMicroCVD` ([Core/Indicators_OrderFlow.vb:269](Core/Indicators_OrderFlow.vb:269)) reads a **50-trade window, no candles at all**. `accel_threshold` (10 000) gates USD flow over 50 trades — **identical at 1m or 3m**.
- `CalcROCSeries` ([Core/Indicators_Momentum.vb:273](Core/Indicators_Momentum.vb:273)) reads the execution candles; 3-min ROC(9) measures return over 27 min vs 9 min → runs ~2.1× larger → its magnitude gate must move with it.

Scaling the two trade-stream USD thresholds would make CVD/MicroCVD *less* sensitive in Asia/London for no reason — degrading order flow in exactly the sessions we're rescuing. **They stay at 1-min values.**

| Key | 1-min | 3-min | Treatment |
|---|---|---|---|
| `indicators.ROC.magnitude_threshold` | 0.1 | **0.21** | **SEED ×2.1** (candle return) |
| `indicators.ROC.slope_delta_threshold` | 0.05 | **0.105** | **SEED ×2.1** (candle return delta) |
| `indicators.CVD.slope_min_usd` | 12 000 | 12 000 | **unchanged** — trade-stream (500), resolution-independent |
| `indicators.MicroCVD.accel_threshold` | 10 000 | 10 000 | **unchanged** — trade-stream (50), resolution-independent |
| DynamicNorms vol / VWAP-dev / ATR-ref thresholds | dynamic | dynamic | **self-scaling** — computed from the candle window; adapts once fed 3-min candles. No seed. |
| ATR 7 · RSI 9 · Vol-SMA 9 · BBW 20 · Donchian 20 · EMA 9·21·50 | counts | same counts | **keep bar count** — a 3-min RSI(9) is the standard 3-min RSI; rescaling makes 3m mimic 1m and defeats the change |
| RSI zones · VWAP-dev % · volume ratio · TFI · EMA alignment | — | unchanged | ratio/bounded — scale-invariant |
| `indicators.TTM.flat_threshold`, `CVD.divergence_price_gate`, `RSI.divergence_price_gate` | — | unchanged | secondary candle-magnitude; **left at 1-min, deferred to Phase 2** (low-impact; un-scaled biases divergence *more eager* = conservative) |
| `session_volume` multipliers / volume thresholds | — | unchanged | **out of the profile** — VolumeRatio is ~scale-invariant; the weekday-ASIA re-verify (§6.2) becomes the 3-min re-verify |

`magnitude_threshold` has three consumers — partial ROC scoring, Pass 2c ROC-active check, spread-penalty direction ([Core/Settings/EngineSettings.vb:196](Core/Settings/EngineSettings.vb:196)). Scaling ×2.1 holds their fire-rates ~constant rather than letting the naturally-bigger 3-min ROC sail past a 1-min bar.

**Honest caveat (carry into the §15 row + change_log):** 2.1× is the measured 1→3min ATR ratio used as a *proxy* for ROC scaling. ROC's true scaling is bracketed ~1.7× (pure noise) to ~3× (pure trend); only Phase-2 data settles it. **Both ROC keys are the Phase-2 re-baseline priority** — Phase-1 Asia/London verdicts are provisional by design.

**Net: 2 seeded keys, not 4.** Smaller aggressive-seed surface than the proposal implied.

---

## 2. Config taxonomy (settings.json v35 → v36)

**Decision (brief §5 Q1):** extend `session_volume.sessions[]` with `execution_resolution` — DRY, single source of truth for "what session is it + its boundaries + its resolution." A new top-level block would duplicate `start_hour`/`end_hour` → a second session definition that can drift (the `ResolveSessionLabel` off-by-one is exactly that failure mode). Rejected.

**Decision (brief §5 Q2):** a top-level `resolution_profiles` map keyed by resolution string. `"1"` is empty (everything inherits the global 1-min values). `"3"` carries only the overridden keys. Inheritance: *any key absent from the active profile falls back to the global value* — a pure override-map.

### 2.1 `session_volume.sessions[]` — add `execution_resolution`

```json
"sessions": [
  { "name": "ASIA",   "start_hour": 0,  "end_hour": 7,  "high_multiplier": 1.10, "mid_multiplier": 1.05, "execution_resolution": 3 },
  { "name": "LONDON", "start_hour": 8,  "end_hour": 12, "high_multiplier": 1.00, "mid_multiplier": 1.00, "execution_resolution": 3 },
  { "name": "NY",     "start_hour": 13, "end_hour": 23, "high_multiplier": 1.15, "mid_multiplier": 1.10, "execution_resolution": 1 }
]
```

Default **1** (absent/unspecified ⇒ current 1-min behaviour, zero change). Hot-reloadable like every key. **Off the auto-tweaker surface** — add `execution_resolution` to PromptBuilder HARD CONSTRAINT 11's exclusion list alongside `min_tradeable_move_pct` / `kelly.*` (brief §2, locked).

### 2.2 New top-level block `resolution_profiles`

```json
"resolution_profiles": {
  "1": { },
  "3": {
    "roc_magnitude_threshold": 0.21,
    "roc_slope_delta_threshold": 0.105
  }
}
```

### 2.3 POCO additions ([Core/Settings/EngineSettings.vb](Core/Settings/EngineSettings.vb))

```vb
' SessionBucketSettings (existing, line ~444) gains:
<JsonPropertyName("execution_resolution")> Public Property ExecutionResolution As Integer = 1

' New class:
Public Class ResolutionProfile
    <JsonPropertyName("roc_magnitude_threshold")>   Public Property RocMagnitudeThreshold   As Double? = Nothing
    <JsonPropertyName("roc_slope_delta_threshold")> Public Property RocSlopeDeltaThreshold  As Double? = Nothing
End Class

' EngineSettings (top level) gains:
<JsonPropertyName("resolution_profiles")>
Public Property ResolutionProfiles As Dictionary(Of String, ResolutionProfile) = New Dictionary(Of String, ResolutionProfile)
```

Nullable override fields so "absent ⇒ inherit global" is unambiguous (a `0.0` default would be a real override). Default-empty dict so an absent block = pure 1-min behaviour.

---

## 3. The resolver (host-agnostic — `Core/`, no WinForms)

**Brief §4.6, first bullet (mandatory):** the resolution boundary MUST equal the gate/eval session boundary. Guarantee it by **sharing the engine's bucket-matcher**, not re-deriving it (and *not* using the display `ResolveSessionLabel`, which has the v34 hour-7 off-by-one, `<7`).

Extract the bucket-match loop currently inline in `DynamicNorms.ApplySessionVolume` ([DynamicNorms.vb:121-127](DynamicNorms.vb:121)) into a shared pure helper, and add the resolution + ROC-override resolvers. Put them in a new host-agnostic file, e.g. `Core/ExecutionResolution.vb`:

```vb
' Shared bucket-matcher — the ONE definition of "which session is this UTC hour".
' DynamicNorms.ApplySessionVolume is refactored to call this too (DRY; identical boundary).
Public Shared Function MatchSessionBucket(cfg As EngineSettings, utcHour As Integer) As SessionBucketSettings
    Dim sv = cfg.SessionVolume
    If sv Is Nothing OrElse sv.Sessions Is Nothing Then Return Nothing
    For Each b In sv.Sessions
        If utcHour >= b.StartHour AndAlso utcHour <= b.EndHour Then Return b
    Next
    Return Nothing
End Function

' NOTE: does NOT consult sv.Enabled. That flag gates the VOLUME MULTIPLIER only.
' Resolution selection is independent — disabling session-volume scaling must not
' silently revert Asia/London to 1-min.
Public Shared Function ResolveResolution(cfg As EngineSettings, utcHour As Integer) As Integer
    Dim b = MatchSessionBucket(cfg, utcHour)
    Return If(b Is Nothing OrElse b.ExecutionResolution <= 0, 1, b.ExecutionResolution)
End Function

Public Shared Function ResolveRocMagnitude(cfg As EngineSettings, execRes As Integer) As Double
    Dim p As ResolutionProfile = Nothing
    If cfg.ResolutionProfiles IsNot Nothing Then cfg.ResolutionProfiles.TryGetValue(execRes.ToString(), p)
    Return If(p IsNot Nothing AndAlso p.RocMagnitudeThreshold.HasValue,
              p.RocMagnitudeThreshold.Value, cfg.Indicators.ROC.MagnitudeThreshold)
End Function

Public Shared Function ResolveRocSlopeDelta(cfg As EngineSettings, execRes As Integer) As Double
    Dim p As ResolutionProfile = Nothing
    If cfg.ResolutionProfiles IsNot Nothing Then cfg.ResolutionProfiles.TryGetValue(execRes.ToString(), p)
    Return If(p IsNot Nothing AndAlso p.RocSlopeDeltaThreshold.HasValue,
              p.RocSlopeDeltaThreshold.Value, cfg.Indicators.ROC.SlopeDeltaThreshold)
End Function
```

`ApplySessionVolume` keeps consuming `Enabled` for the multiplier, but its bucket lookup now routes through `MatchSessionBucket` — one matcher, two consumers (volume scaling + resolution).

---

## 4. Fetch + execution-stack rewiring ([UI/MainForm_Analysis.vb](UI/MainForm_Analysis.vb))

This is the core engine change. UI-side fetch/session-resolution is fine; the resolver helpers (§3) stay host-agnostic.

### 4.1 Resolve + fetch

Resolve once per run: `Dim execRes As Integer = ExecutionResolution.ResolveResolution(cfg, DateTime.UtcNow.Hour)`.

**Keep the 1-min fetch in all sessions** — it feeds the 1m OHLC cache, the eval barrier walk, and last-trade-price. Add the execution-stack fetch only when `execRes <> 1`:

```vb
Dim candlesExec As List(Of Candle) =
    If(execRes = 1, candles1m, Await DeribitClient.GetCandlesAsync(execRes.ToString(), 250))
```

Keep the fetch count at **250** (250 × 3-min = 12.5 h, ample for every window; the largest is the 100-bar ATR-ref / volume baseline). `candles5m` (210) and `candles15m` (cached) fetches are unchanged. When `execRes = 1`, `candlesExec` *is* `candles1m` — no extra call, NY stays byte-identical.

### 4.2 Skip + freshness gates

Add an exec-stack availability + freshness gate mirroring the 1m checks ([MainForm_Analysis.vb:89-107](UI/MainForm_Analysis.vb:89)). The **freshness gate must take the execution resolution** (brief §4.6 — a 3-min bar is fresh up to ~6 min, not 2):

```vb
ElseIf execRes <> 1 AndAlso (candlesExec Is Nothing OrElse candlesExec.Count < 50) Then
    skipReason = execRes & "m candles unavailable"
ElseIf Not IndicatorEngine.IsFresh(candlesExec, execRes, DateTime.UtcNow) Then   ' was IsFresh(candles1m, 1, ...)
    skipReason = execRes & "m candles stale"
```

Retain the existing `candles5m` freshness gate at literal `5`. `IsFresh` ([Core/Indicators_Momentum.vb:28](Core/Indicators_Momentum.vb:28)) already takes `resolutionMinutes` — just pass `execRes`. When `execRes = 1` this is the current 1m gate exactly.

### 4.3 Feed `candlesExec` to the execution stack

Replace `candles1m` with `candlesExec` at **every execution-stack site**, and route the two ROC thresholds through the resolver. Concretely, in [MainForm_Analysis.vb](UI/MainForm_Analysis.vb):

| Line(s) | Today | v36 |
|---|---|---|
| 127 | `r.CurrentPrice = candles1m.Last().Close` | `candlesExec.Last().Close` |
| 129 | `CalcATR(candles1m, …)` | `CalcATR(candlesExec, …)` |
| 131 | `DynamicNorms.Compute(candles1m, r.ATR)` | `DynamicNorms.Compute(candlesExec, r.ATR)` |
| 134 | `CalcROCSeries(candles1m, …)` | `CalcROCSeries(candlesExec, …)` |
| 140 | `cfg.Indicators.ROC.SlopeDeltaThreshold` | `ExecutionResolution.ResolveRocSlopeDelta(cfg, execRes)` |
| 146 | `CalcRSI(candles1m, …)` | `CalcRSI(candlesExec, …)` |
| 148-151 | `CalcVolumeSMA(candles1m,…)` / `candles1m.Last().Volume` | `candlesExec` |
| 182-187 | `CalcVWAP` / `CalcVWAPBands(candles1m,…)` | `candlesExec` |
| 189-192 | `CalcBBW(candles1m,…)` | `candlesExec` |
| 194-197 | `CalcTTMSqueeze(candles1m,…)` | `candlesExec` |
| 202-204 | `CalcEMA(candles1m,…)` ×3 | `candlesExec` |
| (Donchian / OBV / VPFR / CVD-candle-arg, below line 259) | `candles1m` | `candlesExec` — **walk the rest of the file; every `candles1m` consumer in the indicator-fill block moves to `candlesExec` EXCEPT the four in 4.4** |

**Unchanged (do NOT move to `candlesExec`):** `CalcDMI(candles5m, …)` (regime, line 153) · `CalcEMA(candles5m, 200)` (regime anchor) · MTF gate (`candles15m`) · swing pivots (`candles5m` / `candles15m`). These are the higher-timeframe layer (brief §2).

### 4.4 ROC magnitude override (inside ScoringEngine)

`magnitude_threshold` is consumed inside the scoring pipeline (partial ROC scoring, Pass 2c ROC-active check, spread-penalty direction), not in `MainForm_Analysis`. Plumb the resolution to those read sites via `r` — **stamp `r.ExecResolution` first (§5), then route every `cfg.Indicators.ROC.MagnitudeThreshold` read in `Core/ScoringEngine_*.vb` through `ExecutionResolution.ResolveRocMagnitude(cfg, r.ExecResolution)`.** `r` is already passed to `Calculate`; no new parameter, no cfg mutation, no singleton race. Grep `MagnitudeThreshold` across `Core/ScoringEngine_*.vb` to enumerate the sites.

### 4.5 The three candle args that stay 1-min (CalcCVD's candle arg does NOT — it moves to `candlesExec`; see the last bullet)

- `LivePerformanceTracker.UpdateAsync(verdict, r, candles1m, …)` ([MainForm_Analysis.vb:464](UI/MainForm_Analysis.vb:464)) — the eval barrier walk is a **1-min price walk** regardless of execution resolution (brief §4.6). Keep `candles1m`. Only the `FavBar` *distance* changes, and it does so automatically because it derives from `r.ATR` (now 3-min).
- The 1m OHLC cache append + gap-fill — stays 1m.
- `lastTradePrice` ([MainForm_Analysis.vb:123](UI/MainForm_Analysis.vb:123)) — from `recentTrades`, resolution-independent.
- `CalcCVD`'s `candles` argument — used only for the divergence price-gate; pass `candlesExec` so the divergence price-change is on the execution bar (this *is* an exec-stack consumer; listed in 4.3). It is *not* a 1-min hold-back — noted here only to disambiguate from `slope_min_usd`, which stays put.

---

## 5. Data layer — stamp + resolution-filtered aggregation

**Per-row stamp (mandatory, brief §2):**
- Add `r.ExecResolution As Integer` to `IndicatorResults` ([Core/IndicatorResults.vb](Core/IndicatorResults.vb)); set it in `RunAnalysisAsync` right after `execRes` is resolved.
- `analysis_log.csv` **v0.6 → v0.7**: append `ExecResolution` column. All readers are header-name-based since F9, so an appended column is transparent.
- Eval cache `analysis_eval_cache.csv` **v3 → v4**: append `ExecResolution` field; re-walk on load (same pattern v35 used for v2→v3). **Legacy default (coordinator 2026-06-15):** pre-v36 (v3) rows have no stamp and were all 1-min — the migration MUST default a missing `ExecResolution` to **1**, or the resolution-filtered aggregation below mis-buckets or silently drops them.

**Resolution-filtered aggregation (mandatory):** every consumer that aggregates by session — the live perf strip, `FailureRateMatrix`, the eval re-walk in `LivePerformanceTracker.BuildAggregate` — must key on `(session × resolution)`, never pooling 3-min Asia/London with 1-min NY. With fixed 3/3/1 each session has one resolution *today*, but the configurable-but-stable contract means a future re-baseline (e.g. ASIA 3→5) creates two sub-populations from the same session; the stamp + filter is the safety net that stops a blended rate. `BuildEntry` / `ReevaluateForFloor` already key off `r.ATR`, so the resolution-correct favourable barrier (`max(k×ATR, pct×price)`) is inherited — only the stamp + aggregation filter are new work.

**Batch-along (v34 follow-up, brief §4.3):** log `weightedSlope`. It's currently a local in `CalcCVD` ([Core/Indicators_OrderFlow.vb:198](Core/Indicators_OrderFlow.vb:198)). Add `r.CVDWeightedSlope`, surface it from `CalcCVD` via a new `ByRef` out-param, and include it in the v0.7 column set — this unblocks precise CVD threshold calibration. Optional but cheap; do it in the same CSV bump so the book doesn't churn twice. **Spec C SC/TOTAL parity** (UI-reskin item): batch into v0.7 *only if it's landing concurrently*; otherwise don't block v36 on it.

---

## 6. Coordination resolutions

### 6.1 Sequencing vs the v35 auto-tweaker first fire (brief §4.1) — HARD ORDERING
NY stays 1-min under v36, so the two are orthogonal in *content* — but there is a **hard ordering constraint, not a free choice:** run the v35 supervised first-fire dry-run (NY/1-min weekday window) **before v36 Phase 1 ships any 3-min rows.** The tweaker's fixed-window slicer walks disjoint **chronological** row slices (`allRows[LastEvaluatedRowIndex .. +WindowSize]`) and is **resolution-blind until Phase 2** (§6.3); once 3-min Asia/London rows interleave into `analysis_log.csv`, no later window can isolate a pure-1-min population, so the first-fire validation must consume the clean 1-min history first. **Order:** v35 first-fire dry-run (NY/1-min — validate snapshot / apply / revert / streak) → **then** ship v36 Phase 1 → tweaker resolution-awareness (§6.3) gates any later fire on mixed data. **Do not ship v36 ahead of the first-fire validation.**

### 6.2 v34 weekday-ASIA `session_volume` re-verify (brief §4.2)
The Phase-0 study already produced the weekday-vs-weekend ASIA split the re-verify needed (weekday Asia ≈ 2× weekend). **Resolution:** `session_volume` stays 1-min-calibrated and **out of the resolution profile** (§1) — VolumeRatio is a ratio of same-resolution quantities, so it's ~scale-invariant and the v34 ASIA multiplier transfers approximately. The existing weekday-ASIA re-verify (WATCHING, Medium) simply **becomes a 3-min re-verify** once ≥50 weekday-Asia 3-min rows exist: recompute ASIA trade-rate / RANGE_BOUND % / VolumeRatio tail on weekday-only 3-min Asia rows; dial 1.10/1.05 toward neutral if weekday Asia is materially calmer. Folding `session_volume` into the profile now would couple two calibration efforts — explicitly **not** done. Flagged, not silently inherited.

### 6.3 Auto-tweaker resolution-awareness (Phase-2 precondition, brief §4.4)
Already session-blind; v36 adds resolution-blind. Before it tunes on any post-v36 data it must filter the failure-rate matrix + CSV rows by `(session × resolution)` so it never pools 3-min Asia/London with 1-min NY. The `ExecResolution` stamp makes this possible. Spec the filter as a Phase-2 item; it compounds the existing session-blind gap — both want fixing before any un-gated fire on mixed data. `execution_resolution` itself is already on the exclusion list (§2.1), so the tweaker can never *propose* a resolution change.

### 6.4 Schema bumps — batch (brief §4.3)
CSV v0.6→v0.7 (`ExecResolution` + optional `weightedSlope`), eval cache v3→v4 (`ExecResolution`), settings v35→v36. One book churn.

---

## 7. Phasing

- **Phase 0 — DONE** (proposal §1): offline ATR/price study → 3/3/1.
- **Phase 1 (this hand-off):** `execution_resolution` config + `resolution_profiles` + the resolver (§3) + per-session `candlesExec` fetch + execution stack (incl. ATR) computed on it + ROC override + `ExecResolution` stamp (CSV v0.7 / eval v4) + resolution-filtered perf/eval + the seeded `"3"` profile. **Regime/MTF/swings untouched.** Ships a directionally-coherent 3-min Asia/London engine immediately. Asia/London verdicts provisional (ROC seed).
- **Phase 2 (calibrate, separate spec):** accumulate 3-min Asia/London data → per-resolution re-baseline (ROC keys first; then TTM / divergence gates; then the 3-min weekday-ASIA `session_volume` re-verify) → teach the auto-tweaker `(session × resolution)` before any fire on mixed data.

---

## 8. Acceptance (A14 — extend the `verify/` harness; A1–A13 must stay green)

- **A14a — Resolution selection (engine bucket):** `ResolveResolution` returns 3 at UTC hour 3 and hour 10; **3 at hour 7** (ASIA inclusive — guards the off-by-one); 1 at hour 13 and hour 23.
- **A14b — ROC override resolves:** `ResolveRocMagnitude(cfg,3)=0.21`, `(cfg,1)=0.1`; `ResolveRocSlopeDelta(cfg,3)=0.105`, `(cfg,1)=0.05`.
- **A14c — ATR on 3-min + gate flip:** with a 3-min fixture, `r.ATR = CalcATR(candlesExec,7)`; a setup that gate-killed at 1-min (ATR ~13 → target ~26 < 49.6 floor) **clears** at 3-min (ATR ~27 → target ~54 > 49.6).
- **A14d — NY byte-identical (regression guard):** at `execRes=1`, verdict + levels + breakdown + CSV row are identical to v35 for the same inputs (no profile override applied; `candlesExec` is `candles1m`).
- **A14e — Per-row stamp:** `ExecResolution` present in CSV v0.7 + eval cache v4; an ASIA row carries 3, a NY row carries 1.
- **A14f — Resolution-filtered aggregation:** a synthetic mix of two resolutions in one session yields two sub-populations, never one blended rate.
- **A14g — Freshness honours resolution:** `IsFresh(c3m, 3, now)` treats a 5-min-old 3-min bar as fresh, a 7-min-old as stale; `IsFresh(c1m, 1, now)` unchanged.
- **A14h — Regime/MTF unchanged:** 5-min regime + 15-min MTF identical to v35 for the same inputs.
- **A14i — Resolution survives `session_volume` disabled (coordinator 2026-06-15):** with `session_volume.enabled=false`, `ResolveResolution(cfg, 3)` still returns **3** (ASIA). Guards the §3 separation — disabling the volume multiplier must NOT silently revert Asia/London to 1-min.
- `dotnet build` clean. **Live sanity:** weekday Asia/London now produce tradeable (mostly non-`BELOW_MIN_MOVE`) verdicts; NY unchanged.

---

## 9. Out of scope (do not re-open)

Weekend resolution overlay (trader: out — rare weekend trading; weekends stay 3-min, partly gated, accepted). Native 5-min use (not selected in v1; the `"5"` profile is documented but unpopulated). Profile ATR-band recalibration (Low<80 for $80–100k — independent housekeeping). `session_volume` per-resolution split (§6.2 — deferred to the 3-min re-verify).

---

## 10. Commit checklist

- [ ] `settings.json`: version 35→36; `last_modified`/`modified_by`; prepend `change_log` entry (newest-first) covering the resolution config + the **2-key ROC seed (not 4 — note the CVD/MicroCVD trade-stream finding)** + the 2.1× proxy caveat + CSV v0.7 / eval v4. Add `sessions[].execution_resolution` (3/3/1) + the `resolution_profiles` block.
- [ ] **Version + settings.json reconciliation (coordinator 2026-06-15 — runtime version churn surfaced during first-fire validation):** the TRACKED root `settings.json` is still **v35**, so the bump stays **35→36** — the runtime *bin-copy* churn to v38 (UI `auto_run`-interval saves; `SettingsLoader.Save` bumps `version` + `change_log` on every save) is an **untracked** artifact, overwritten when root is next modified + rebuilt. Fold in two fixes (you're already in `settings.json` / `SettingsLoader` territory): **(a)** operational/UI-only saves (auto_run interval, perf `metric_mode`, output-dump settings) must NOT bump the feature `version` or append a `change_log` entry — only scoring/feature saves do (D4 closed start/stop churn but not interval-change version bumps); **(b)** align the auto-tweaker `tweaker_config.settings_path` (currently repo-root) with the file the running app actually reads/writes (the bin-copy), so an `auto_commit` fire's change reaches the live engine — today they are different files (root v35 vs bin v38).
- [ ] POCO (`EngineSettings.vb`): `SessionBucketSettings.ExecutionResolution`, `ResolutionProfile` class, `EngineSettings.ResolutionProfiles`. Defaults make an absent block a no-op.
- [ ] `Core/ExecutionResolution.vb` (host-agnostic) + refactor `DynamicNorms.ApplySessionVolume` to share `MatchSessionBucket`.
- [ ] `IndicatorResults.ExecResolution` (+ optional `CVDWeightedSlope`).
- [ ] Fetch + exec-stack rewiring (§4) + ROC override sites in `Core/ScoringEngine_*.vb` (§4.4).
- [ ] CSV v0.7 + eval cache v4 + resolution-filtered aggregation (§5).
- [ ] PromptBuilder HARD CONSTRAINT 11: add `execution_resolution` to the exclusion list.
- [ ] **Display the active resolution — MANDATORY (coordinator call 2026-06-15, trader-confirmed):** surface an `EXEC 3m` / `EXEC 1m` tag in the rendered output (near the ATR-levels header or the session label). An invisible resolution means you can't tell whether an Asia verdict ran on 1-min or 3-min, and a misconfiguration (`execution_resolution` accidentally 1) would be silent. **Display-parity (CLAUDE.md hard rule):** the tag MUST land in the card binding [UI/MainForm_Render_Cards.vb](UI/MainForm_Render_Cards.vb) **and** `BuildPlaintextSnapshot` in the **same commit** — there is no "no surface changed" exemption now, because a surface always changes. While here, fix the display-only `ResolveSessionLabel` hour-7 off-by-one ([UI/MainForm_Render_Cards.vb:1375](UI/MainForm_Render_Cards.vb:1375), `<7`) so the card agrees with the engine bucket.
- [ ] A14 fixtures pass; A1–A13 unregressed; `dotnet build` clean.
- [ ] `docs/DeribitIndicatorProject.md` §15 v36 row + §6 pointer →v36; §12 WATCHING: add Phase-2 ROC re-baseline + auto-tweaker resolution-awareness; retitle the weekday-ASIA re-verify as the 3-min re-verify.
- [ ] **Local commit only.** Trader compiles + tests + pushes (host-agnostic rule respected: resolver in `Core/`, no WinForms; fetch/session detection UI-side is fine).
