# Engine Correctness Pass — Proposal

**Date:** 2026-06-10
**Author:** Fable 5 (spec-author seat)
**Status:** DRAFT — awaiting user approval. Scoring changes are approval-gated; nothing here ships without explicit sign-off.
**Implementer:** Fable 5 (the warm audit conversation that produced `docs/fable5-audit-report.md`, if still open; otherwise a fresh Fable conversation given this doc). NOT Opus — window-semantics judgement is required (see §12).
**Settings:** v30 → **v31**. CSV schema: v0.4 → **v0.5**. Includes the pre-authorised **data reset** (§7).

---

## 0. Summary

One bundled pass fixing every confirmed behaviour-changing bug from the 2026-06-10 engine audit (`docs/fable5-audit-report.md`), landing at a **single reset boundary**: all fixes ship as sequential local commits, the contaminated calibration data is archived and reset at the final commit, and clean data collection starts against the fully-fixed engine.

| Fix | Audit ID | Sev | One-line |
|-----|----------|-----|----------|
| F1 | C1/C2/C3 | CRITICAL | Trade-list chronological normalisation: CVD slope + MicroCVD accel/decel are inverted; TFI windowing only works by accident |
| F2 | H1 | HIGH | DynamicNorms volume/VWAP-dev baselines sample the *oldest* candles (2.5–4 h stale) |
| F3 | H2 | HIGH | MTF hard veto evaluated against a pre-scoring direction proposal, not the verdict direction |
| F4 | H3 | HIGH | Step 5 tier cascade walks LONG tiers first with no dominance comparison → wrong-side WEAK verdicts |
| F5 | M1 | MED | OBV normalised by first-bar volume → dead-for-the-run or effectively sign() |
| F6 | M2 | MED | Donchian window includes current bar → full breakouts essentially never fire |
| F7 | S-5 | LOW | Step 3 funding boost not capped at regimeMax |
| F8 | S-6 | LOW | TRANSITIONAL grace-bar ADX below 20 gets zero penalty (inverted severity) |
| F9 | M7 | MED | `LivePerformanceTracker` CSV backfill parses by fixed column index — converted to header-map, **required** before the v0.5 schema shift (§8; folded in after Tier C shipped without it) |

**Why one boundary:** the engine was calibrated on data containing C1/C2/H1 (`docs/fable5-audit-report.md` §0 calibration warning). The user has pre-authorised discarding that history. The reset is the expensive event — every behaviour-changing fix must land *before* it, or the new clean dataset fragments the same way the old one did. Sequencing Tier A and Tier B as separate fix events would mean either two resets or a fragmented dataset; neither is acceptable on a data-bottlenecked project.

**What this pass is not:** no new indicators, no threshold tuning (beyond the one value the OBV fix forces), no display redesign, no Kelly fix (H4 is display-only → Tier D), no Tier C dataset-protection items (separate kickoff, zero scoring impact, can land in parallel).

---

## 1. Verification status

Every fix below was re-verified against the current tree on 2026-06-10 by the spec author (file:line quoted from source, not from the audit). The audit's own dynamic harness (`verify/ordercheck/`, gitignored, links the real `.vb` sources) confirmed C1/C2/C3/H1/M1 by running them. The Opus seat independently confirmed 11/11 findings on the same date. **The harness directory has since been deleted** (Tier C session, 2026-06-10): its nested project was swept into the root `.vbproj`'s `**/*.vb` glob and broke the solution build. §11 recreates it glob-safely.

**Implementer instruction:** re-verify each file:line against the tree before commit 1 regardless (kickoff-staleness rule). If any anchor has drifted, stop and re-ground before editing.

---

## 2. F1 — Trade-stream chronological normalisation (C1/C2/C3)

### Current behaviour

- `DeribitClient.GetRecentTradesAsync` ([DeribitClient.vb:256](../DeribitClient.vb)) requests `sorting=desc` → list arrives **newest-first**. Nothing reverses it.
- `CalcCVD` ([Core/Indicators_OrderFlow.vb:149-201](../Core/Indicators_OrderFlow.vb)) splits the full list into positional thirds: "early" = list head = **newest** trades. `weightedSlope = lateDelta×2 − earlyDelta×1` therefore weights the *oldest* flow double and subtracts the newest — the exact opposite of the v0.47 design intent ("late segment carries 2× weight" to emphasise recent flow). RISING/FALLING is chronologically inverted whenever flow shifts inside the window.
- `CalcMicroCVD` (:260) — `trades.Take(microWindowSize)` correctly selects the newest 50 (because the list is desc), but within the window early/mid/late are again positional → ACCELERATING/DECELERATING and BULL_ACCEL/BULL_DECEL/BEAR_* are inverted, and the displayed E/M/L values are mislabelled.
- `CalcTFI` (:217) — `trades.Take(tfiWindowSize)` is correct **only because** the list is desc. This is the C3 trap: a naive "reverse the list" fix silently breaks TFI and MicroCVD window selection.
- Order-insensitive consumers (verified): `CalcCVD`'s net `cvdValue` and divergence; `CalcLiquidations` (full-list sums, [Indicators_OrderFlow.vb:118-141](../Core/Indicators_OrderFlow.vb)). Unaffected.
- One positional consumer outside indicators: `recentTrades(0).Price` = last-transacted price at [UI/MainForm_Analysis.vb:120](../UI/MainForm_Analysis.vb).

### Required behaviour

`GetRecentTradesAsync` returns **chronological ascending** (oldest first) as its documented contract. All window-consuming indicators take their window **from the end** of the list. Segment labels (early/mid/late) become chronologically truthful.

### Implementation

1. **`DeribitClient.GetRecentTradesAsync`**: keep the HTTP request as `sorting=desc` (this is what guarantees the *most recent* `count` trades from the API). After the parse loop, `list.Reverse()` before `Return`. Add an XML doc comment stating the ascending contract. Use `Reverse()`, not `OrderBy(Timestamp)` — reverse is the exact inverse of the API's documented order and preserves intra-millisecond trade ordering; a stable sort by timestamp would scramble same-ms bursts.
2. **`LastN` helper** in `Core/Indicators_OrderFlow.vb`: `Private Shared Function LastN(trades As List(Of TradeRecord), n As Integer) As List(Of TradeRecord)` → `trades.Skip(Math.Max(0, trades.Count - n)).ToList()`. Handles n ≥ Count by returning the whole list (preserves current short-list behaviour).
3. **`CalcTFI`**: `trades.Take(tfiWindowSize)` → `LastN(trades, tfiWindowSize)`. No other change (sums are order-insensitive within the window).
4. **`CalcMicroCVD`**: `trades.Take(microWindowSize)` → `LastN(trades, microWindowSize)`. Within-window positional thirds are now chronologically correct (index 0 = oldest of the newest 50 = "early"). No other logic change — the accel/decel comparisons (`microLate` vs `microEarly`) become truthful automatically.
5. **`CalcCVD`**: **no change**. Positional thirds on an ascending full list are chronologically correct; `weightedSlope` now emphasises recent flow as documented.
6. **`CalcLiquidations`**: no change.
7. **Call site** [MainForm_Analysis.vb:120](../UI/MainForm_Analysis.vb): `recentTrades(0).Price` → last element (`recentTrades(recentTrades.Count - 1).Price`).
8. **Closed-set check**: grep `recentTrades` and `trades.Take(` across the repo; the consumer set above must be exhaustive. Any additional positional consumer found → stop, add to this spec's record, fix with the same semantics.
9. **Comment hygiene**: update the v0.47 CVD design comment and the MicroCVD header note to state the ascending input contract.

### Display/CSV semantics shift (no code change, flag for the user)

- `MicroCVDEarly/Mid/Late` (ORDER FLOW card, plaintext snapshot, CSV columns): values re-map to true chronology. "Early" now genuinely means oldest segment of the window.
- `CVDSlope` RISING/FALLING and `MicroCVDSignal` ACCEL/DECEL flip to truthful readings. Hold/exit guidance (`CalcHoldStatus` Layers 1/3) and the MOMENTUM_FADING tag inherit the fix with zero changes of their own — they were consuming inverted inputs.

---

## 3. F2 — DynamicNorms recent-window baselines (H1)

### Current behaviour

[DynamicNorms.vb:31-32](../DynamicNorms.vb): `candles1m.Take(Math.Min(100, candles1m.Count - 1))` on an **ascending** candle list = the *oldest* 100 of 250 candles. The volume mean/σ baseline describes conditions 2.5–4 h stale; session multipliers then stack on a wrong-session baseline. Same pattern at line 59 for the VWAP-dev sample window (size 50; display-only consumer — `VWAPDevThreshold` feeds the UI per the audit). The `Count − 1` term shows the author intended to exclude the in-progress bar — the intent was a recent window; `ComputeATRRef` in the same file (:120-140) samples the recent window correctly.

### Required behaviour

Both windows sample the **most recent** N *completed* bars (excluding the final, in-progress candle), mirroring `ComputeATRRef`.

### Implementation

```vb
' volume baseline (line 31): last 100 completed bars
Dim volLen   As Integer = Math.Min(100, candles1m.Count - 1)
Dim volStart As Integer = candles1m.Count - 1 - volLen
Dim volWindow = candles1m.Skip(volStart).Take(volLen).Select(Function(c) c.Volume).ToList()
```

Same shape for the VWAP-dev window at :59 with size 50. Window sizes stay hardcoded (they are structural constants like the 250-candle fetch, not tunables; exposure can ride a future settings pass if the tweaker ever needs them).

Note: the VWAP-dev sample builds a cumulative VWAP across its window; on the recent window this becomes a rolling "last ~50 min" deviation stat — the intended adaptive behaviour.

---

## 4. F3 — Direction-aware MTF gate (H2)

### Current behaviour

- [UI/MainForm_Analysis.vb:315-322](../UI/MainForm_Analysis.vb) derives `mtfProposed` from the 1m regime/EMA alignment *before scoring*, and `CalcMTFGate` bakes that direction into a single `r.MTFGatePass`/`r.MTFGateReason`.
- Step 4b ([Core/ScoringEngine_Calculate_Verdict.vb:68-96](../Core/ScoringEngine_Calculate_Verdict.vb)) then derives its own (score-based) `proposedDir` and applies the *pre-scoring* boolean to it. When order flow drives the verdict against the 1m EMA/regime read, the gate blocks with-trend trades and passes counter-trend ones. When the 1m proposal is NONE, the gate passes everything.
- Verified mechanically convenient fact: in `CalcMTFGate` ([Core/Indicators_Structure.vb:408-499](../Core/Indicators_Structure.vb)), `mtfTrend` (BULL/BEAR/FLAT, 2-of-3 over DMI direction / ADX strength / EMA stack) is computed **direction-independently**; the proposed direction only enters the final `Select Case`. Per-side flags are nearly free.

### Required behaviour

The hard-veto invariant — *the 15m trend must align with the verdict direction* — is enforced against the **actual dominant side** at Step 4b. The 1m-derived proposal disappears.

### Implementation

1. **`CalcMTFGate` signature**: remove `proposedDirection`; replace `ByRef gatePass As Boolean, ByRef gateReason As String` with:
   - `ByRef gatePassLong As Boolean` — `(mtfTrend <> "BEAR")` (BULL or FLAT passes, preserving current pass semantics)
   - `ByRef gatePassShort As Boolean` — `(mtfTrend <> "BULL")`
   - `ByRef gateDetails As String` — the existing details format string (`"15m +DI:{0:F1} ..."`), direction-free.
   No-data / insufficient-candles paths: both flags `True`, details carries the existing message (preserves fail-open).
2. **`IndicatorResults`**: replace `MTFGatePass`/`MTFGateReason` with `MTFGatePassLong`, `MTFGatePassShort`, `MTFGateDetails`. (`MTF15mTrend/ADX/EMAAlignment` unchanged.)
3. **`MainForm_Analysis.vb`**: delete the `mtfProposed` derivation (:315-322); call with the new signature.
4. **Step 4b** consumes the dominance determination from F4 (shared): when `dominant <> "NONE"` and the dominant side's effective score ≥ tWeak and `cfg.MTFGate.Enabled`, consult the matching flag. On block → NO TRADE (current early-return shape). Compose the final reason **here**, preserving the three locked display formats:
   - `MTF PASS [LONG] <details>` / `MTF BLOCK [LONG vs BEAR] <details>` (mirror for SHORT)
   - `MTF state: <TREND> | <details>` when no directional verdict is in play.
   Store it on `VerdictResult` (new `MTFGateReason` field). The MTF GATE card, plaintext snapshot, CSV, and the breakdown-row note must all read the **same final string** (implementer maps all consumers of the old `r.MTFGateReason`).
5. **Breakdown-row consistency**: the Step 4 regime-veto early returns (:36-43, :46-54) currently skip the MTF row entirely (known stylistic gap). While restructuring, append the row on those paths too, with the no-direction format and `(False, False)` hits.
6. **CSV (v0.5)**: replace the gate columns with `MTFGatePassLong`, `MTFGatePassShort`, `MTFGateReason` (final composed string). Both sides logged — recalibration analytics will want gate-disagreement rates.

### Behaviour-change callouts

- Counter-trend verdicts can no longer slip past the gate; with-trend verdicts can no longer be wrongly vetoed. MTF BLOCK frequency will shift in both directions — expected, watch per §8.

---

## 5. F4 — Dominant-side verdict cascade (H3)

### Current behaviour

Step 5 ([ScoringEngine_Calculate_Verdict.vb:109-124](../Core/ScoringEngine_Calculate_Verdict.vb)) checks all LONG tiers before any SHORT tier. With v30 thresholds (RANGE_BOUND regimeMax 19 → weak 7 / med 11; TRANSITIONAL 15 → weak 6 / med 8), `effectiveLS=7, effectiveSS=11` yields **WEAK LONG** while the short side qualifies for a full SHORT. Reachable in RANGE_BOUND and TRANSITIONAL (the Step 4 dominance veto covers TRENDING only). `CalcVerdictContext` ([ScoringEngine_Calculate_Scoring.vb:32](../Core/ScoringEngine_Calculate_Scoring.vb)) computes its tag from the raw dominant side → the CONTEXT line describes the short side under a WEAK LONG headline. Incoherent.

### Required behaviour

Pick the dominant side once, walk only that side's tiers, NO TRADE if the dominant side misses weak. Ties are NO TRADE.

### Implementation

1. After Step 4 produces `effectiveLS`/`effectiveSS`, determine once (shared with Step 4b/F3):
   ```vb
   Dim dominant As String = "NONE"
   If effectiveLS > effectiveSS Then dominant = "LONG"
   ElseIf effectiveSS > effectiveLS Then dominant = "SHORT"
   ' tie → NONE: a tie carries no directional information → NO TRADE
   ```
2. Step 5 walks only `dominant`'s tiers (strong → med → weak); `dominant = "NONE"` or below-weak → `AppendLean("NO TRADE", ...)` as today.
3. **`AppendLean`** ([ScoringEngine_Calculate_Scoring.vb:17-24](../Core/ScoringEngine_Calculate_Scoring.vb)): the tie arm currently labels `[WEAK LONG]` (long bias). Change the first condition to `ls > ss` and add an explicit tie arm → `" [TIE]"` suffix when `ls = ss AndAlso ls >= tWeak`. Display-only.

### Behaviour-change callouts

- **Tie rule change**: `effectiveLS = effectiveSS` with both ≥ weak threshold currently produces WEAK LONG; after the fix it is NO TRADE. Conservative-bias-aligned (profile §6: prefer NO TRADE over weak directional signals). Note: TRANSITIONAL tier-floor compression can create effective ties from raw `ls > ss`; those land below tWeak in practice given the floor values (3/6/9 vs tWeak ≥ 6), so the case is nearly academic — the simple rule wins. (Raw-score tiebreak was considered and rejected: more complexity for a case the floors already push to NO TRADE.)
- Wrong-side WEAK verdicts in RANGE_BOUND/TRANSITIONAL disappear; the worked example above emits SHORT (MEDIUM).
- CONTEXT-line coherence resolves automatically: directional verdicts now always match the raw-dominant side `CalcVerdictContext` examines (raw and effective ordering can only diverge into ties, which are NO TRADE → rendered ALIGNED per v30).

---

## 6. F5 — OBV normalisation (M1)

### Current behaviour

[Core/Indicators_Structure.vb:50-57](../Core/Indicators_Structure.vb): `obvChange = (obvLast − obvFirst) / |obvValues(0)|`. `obvValues(0)` = ±(volume of bar 1), or **0 when the first two closes are equal** (common on 1m BTC) → `obvChange` forced 0 → OBV trend FLAT and divergence dead **for the entire run**, regardless of subsequent OBV behaviour. When non-zero, dividing a 250-bar cumulative change by one bar's volume makes the live gate (`indicators.OBV.trend_gate = 0.001`) meaningless — trend degenerates to `sign(obvLast − obvFirst)`. OBV gates Pass 2 upgrades (adverse-divergence block) and HoldStatus Layer 2 exits, so the dead/always-on flicker is scoring-relevant.

### Required behaviour

Normalise by a stable quantity so the gate has interpretable units and no degenerate dead state.

### Implementation

```vb
Dim meanVol As Double = candles.Skip(1).Average(Function(c) c.Volume)  ' bars 1..N-1 — the bars that contribute to OBV
Dim obvChange As Double = If(meanVol > 0, (obvLast - obvFirst) / meanVol, 0)
```

Units become "net OBV drift measured in average-bar-volumes over the window." For a 250-candle run, pure-noise drift scales ≈ √249 ≈ 16 mean-volume units.

- **New `trend_gate` value: 10.0** (settings.json v31 + POCO default in `EngineSettings.vb`). Rationale: ≈0.6σ of random-walk noise — deliberately permissive so post-fix behaviour approximates the old effective-sign() classification while killing the first-bar artifact. This is a seeded guess, not a calibrated value → WATCHING (§8).
- `divergence_gate` (0.001) unchanged — it gates **price**-change magnitude (:62); the OBV side of divergence is sign-only and survives renormalisation untouched.

---

## 7a. F6 — Donchian prior-bar channel (M2)

### Current behaviour

[Core/Indicators_Structure.vb:21-28](../Core/Indicators_Structure.vb): the 20-bar window includes the current bar, so `CurrentPrice >= DonchianUpper` ([MainForm_Analysis.vb:347](../UI/MainForm_Analysis.vb)) requires the current close to equal the 20-bar **max high** exactly. Full LONG/SHORT breakouts are knife-edge events that essentially never fire; only the quartile partials carry the indicator (a preferred-list breakout signal silently degraded to partial-only).

### Required behaviour

Textbook Donchian: channel over the **prior** `period` bars, excluding the current bar. Full signal fires when the current close breaks the prior channel (close-confirmed breakout — conservative, matches the profile's confirmed-structural-breakout entry style).

### Implementation

In `CalcDonchian`: guard `candles.Count < period + 1 → Return`; window = `candles.Skip(candles.Count - period - 1).Take(period)`. Signal logic at the call site unchanged — full LONG (`close ≥ prior-20 max high`) becomes genuinely reachable. Quartile partials recompute against the prior-bar channel automatically (same code path).

### Behaviour-change callouts

Full Donchian votes (direct +1, vs partial's upgrade-gated +1) start appearing; partial frequency shifts slightly. `quartile_pct` → WATCHING (§8).

---

## 7b. F7 — Funding boost cap (S-5) and F8 — TRANSITIONAL penalty arm (S-6)

**F7**: [ScoringEngine_Calculate_Scoring.vb:581](../Core/ScoringEngine_Calculate_Scoring.vb) `ss += cfg.Scoring.FundingHighBoost` and :587 `ls += cfg.Scoring.FundingHighBoost` are the only bonus sites not capped at `regimeMax` (Step 3b's soften is capped at :608/:617). Wrap both: `Math.Min(side + boost, regimeMax)`. `regimeMax` is in scope.

**F8**: [ScoringEngine_Calculate_Verdict.vb:59](../Core/ScoringEngine_Calculate_Verdict.vb) — arms cover ADX ∈ [20, 22.5) → penalty 2 and [22.5, 25) → penalty 1; ADX < 20 (reachable only on the regime-hysteresis grace bar, [MainForm_Analysis.vb:164-172](../UI/MainForm_Analysis.vb)) falls through to **zero** penalty — the weakest bar gets the lightest treatment, inverting the proximity scale. Fix: first arm becomes `If r.ADX < penMid Then adxPenalty = cfg.RegimeGates.TransitionalPenaltyLow`, covering [0, 22.5) at penalty 2. No new keys.

---

## 8. Settings v31, CSV v0.5, and the recalibration plan

### settings.json v30 → v31

- **Changed value:** `indicators.OBV.trend_gate` 0.001 → 10.0 (normalisation basis changed — see F5).
- No keys added or removed. `version` → 31, `change_log` entry (newest first) referencing this proposal, `last_modified`/`modified_by` updated.
- `EngineSettings.vb` POCO default for `TrendGate` updated to 10.0 in the same commit. (Full POCO-drift re-alignment is Tier C scope — `docs/engine-tier-c-dataset-protection-kickoff.md`.)

### CSV schema v0.4 → v0.5

- Gate columns replaced per F3: `MTFGatePassLong`, `MTFGatePassShort`, `MTFGateReason`.
- Columns whose **semantics** change under the same names: `CVDSlope`, `MicroCVDEarly/Mid/Late/Momentum/Signal`, `VolumeRatio` (vs new thresholds), `OBVTrend`, `OBVDivergence`, `DonchianSignal`, and the verdict/confidence distributions. The schema version string in the header is the semantics marker — post-fix rows are mechanically distinguishable even if files are ever merged.
- `AnalysisLogger` rotates on header mismatch as usual; the manual archive (§9) happens first anyway.
- **F9 (M7) — required here:** convert `LivePerformanceTracker`'s CSV backfill parse from fixed column indices (`p(1)`/`p(63)`/`p(81)`/`p(82)`, ~:873-876) to header-name lookup, matching `ForwardWindowJoiner`'s approach. The v0.5 schema shifts column positions, which would turn the latent fixed-index trap into silent garbage in the eval cache. (Tier C shipped 2026-06-10 without M7 — it belongs here regardless, in the commit that changes the schema.)

### Starting thresholds post-reset

Keep v30 values (carried into v31) except the OBV gate. Rationale: thresholds anchored to market scale (RSI bands, ADX 25/20, spread bps, OFI ratios, funding bands) were never coupled to the inverted signals; blanket-loosening them adds churn without information. The signal-coupled thresholds are handled by explicit re-baseline review instead:

| WATCHING item | Why it moves | Review at |
|---|---|---|
| `indicators.CVD.slope_min_usd` (12000) + `slope_pct_of_value` (0.01) | Slope now measures recent-vs-old correctly; RISING/FALLING distribution shifts | ≥300 clean rows spanning ≥2 sessions |
| `indicators.MicroCVD.accel_threshold` (10000) + dynamic pct/floor | ACCEL/DECEL polarity fixed | same |
| `session_volume` multipliers (ASIA .80/.85, NY 1.15/1.10) | Baseline is now current-session; multipliers were partly compensating for a stale baseline | same, per session bucket |
| `indicators.Volume` dynamic clamps (2.0–6.0 / 1.5–4.0) | Recent-window mean/σ distribution differs from oldest-window | same |
| `indicators.OBV.trend_gate` (10.0 seed) | Fresh units, seeded not calibrated | ≥300 rows; check RISING/FALLING/FLAT mix vs ~run-level price drift |
| `indicators.Donchian.quartile_pct` (0.25) | Full signals now fire; partial/full mix changes | same |
| Verdict tier + NO TRADE distribution per regime | F4 removes wrong-side WEAKs; F3 shifts blocks | first 100 rows, sanity check |
| MTF BLOCK rate per side | F3 direction-aware veto | first 100 rows |

### Auto-tweaker

Stays **held** (it is manual-fire only — the `Run Tweaker Now` button in `TweakSettingsForm`; nothing fires it automatically) until: the pass ships, ≥1 full window (50 rows) of clean v0.5 data exists, and the user supervises the first fire. Its state is reset at the boundary (§9), so it can never evaluate archived rows.

---

## 9. Data reset at the boundary (pre-authorised 2026-06-10)

Executed as part of the **final commit** of the pass — not before (earlier reset just collects more contaminated rows).

1. Close the app; confirm AutoTweaker.exe is not running.
2. Create `data-archive/pre-orderfix-YYYYMMDD/` at repo root. **Move** into it:
   - `analysis_log.csv` (exe directory)
   - `analysis_eval_cache.csv` (exe directory)
   - `tools/AutoTweaker/state.json`
   - the picked-cell history CSV (locate by grep — written via `AppendPickedCell`)
   - `settings_snapshots/` contents + manifest (they reference pre-fix calibration)
   - the analysis output dump file (optional, for completeness)
3. **Keep in place:** `ohlc_1m_cache.csv` — raw OHLC is an input, not a derived signal; it is not contaminated.
4. Tweaker state: deleting `state.json` is the reset; on next run it re-creates with `LastEvaluatedRowIndex = −1` → seeds to the (now ~0) row count. (Tier C's M5 stall guard makes this robust in all orderings.)
5. Add `data-archive/` to `.gitignore`.
6. First post-reset run sanity: CSV recreated with the v0.5 header; perf strip shows `--%` until `min_sample_for_render`; tweaker dialog shows window-not-full.

**Archive retention:** the archive's one use is forensic — quantifying how far the bugs moved historical distributions if the recalibration spec ever wants it. Delete whenever the user pleases.

---

## 10. Commit sequence

Each commit builds clean and the app runs. Local only; the user tests after commit 5; push as one tested milestone.

1. **F1** trade normalisation + **harness recreation** (`verify/ordercheck/` was deleted during Tier C; rebuild per §11, including the root-project glob exclude) + the A1–A4 fixtures (the CRITICAL pair travels with its acceptance tests).
2. **F2** DynamicNorms recent windows (+ harness fixture).
3. **F3 + F4** MTF per-side gate + dominant-side cascade (one commit — they share the dominance restructure).
4. **F5 + F6** OBV normalisation + Donchian prior-window (+ harness fixtures).
5. **F7 + F8** riders + **F9** header-map backfill parse + settings v31 + POCO default + CSV v0.5 schema bump + data reset (§9) + `.gitignore` + doc updates:
   - `docs/DeribitIndicatorProject.md` §15 entry (one entry covering the pass) + §8 `recentTrades(0)` → last-element correction
   - `docs/architecture.md` data-flow notes (trades ascending contract; `GetRecentTradesAsync(500)` while there — the doc still says 100)
   - `CLAUDE.md` invariants if touched (MicroCVD note already compatible)

---

## 11. Acceptance

**Harness** — recreate `verify/ordercheck/` (deleted during Tier C to clear the root-glob build break): own `.vbproj` linking the real shipped sources via `<Compile Include>`, per the audit's original design. **Glob-safety is mandatory:** add `<Compile Remove="verify/**/*.vb" />` to the root `DeribitVerdictEngine.vbproj` so a nested harness can never break the solution build again (acceptable alternative: place the harness outside the repo tree). Truth labels below are chronological ground truth, not pre-fix outputs:

| Test | Fixture | Expect |
|---|---|---|
| A1 CVD | ascending list: old sells → recent buys | `cvdSlope = RISING` |
| A2 MicroCVD polarity | ascending accelerating bull burst in the tail | `BULL_ACCEL` |
| A3 MicroCVD window | 60-trade asc list, first 10 = huge sells | first 10 excluded (LastN semantics) |
| A4 TFI window | asc list: first 30 sells, last 30 buys | `BUY PRESSURE` |
| A5 Norms | oldest-100 vol=10 / newest-150 vol=1000 | `VolMean` ≈ 1000-scale |
| A6 OBV | identical 50-bar rise, first-pair-equal vs not | identical classification both ways |
| A7 Donchian | current close above prior-20 max high | `upper` = prior-window max (close ≥ upper ⇒ full LONG at call-site logic) |
| A8 Cascade | `Calculate()` with contrived RANGE_BOUND inputs: ls 7 / ss 11 | `SHORT`, not `WEAK LONG`; tie case → `NO TRADE` |
| A9 MTF | `CalcMTFGate` on 15m BEAR fixture | `gatePassLong = False`, `gatePassShort = True` |

A8's fixture shape is implementer's judgement (a minimal `IndicatorResults` + cfg into the real `Calculate`).

**Build:** `dotnet build` clean at every commit.

**Manual smoke (commit 5):** one live analysis run — MTF card reason coherent with verdict direction; OBV not FLAT-dead; CSV v0.5 header written; archive folder populated; perf strip at `--%`.

---

## 12. Routing and rules

- **Implementer: Fable.** The C3-class window-semantics traps, the harness fixtures, and the Step 4b/5 restructure need in-flight judgement about chronology and tie semantics, not rote spec-following. The warm audit conversation already holds all 21 engine files and designed the original harness — route there if open (the harness directory itself was deleted during Tier C; it rebuilds quickly from §11).
- Scoring files are approval-gated; this spec is the approval artifact once the user signs off. Implementer does not deviate from it without coming back.
- Local commits only; never push; no `--no-verify`; no `MainForm.Designer.vb` edits.
- Trader-profile check (done at spec time): pure correctness fixes to preferred-list indicators; no rejected pattern introduced; funding stays out of Step 2; tie rule and Donchian close-confirmation are conservative-bias-aligned.

## 13. Explicitly out of scope

| Item | Where it lives |
|---|---|
| H4 Kelly inverse-contract sizing, S-1 quadratic stop/target scaling + scale-display semantics, S-2 VPFR context-tag omission | Tier D engine-hygiene proposal (post-P5b) |
| M3–M6 dataset-protection fixes, S-3 session-boundary date check | Tier C — **shipped 2026-06-10**, commits `a86532a`…`57de688` (`docs/engine-tier-c-dataset-protection-spec-back.md`); M7 moved into this pass as F9 |
| S5 score-ledger reconciliation guard | merged into Spec C (`docs/sc-column-total-parity-proposal.md`), post-fix |
| S-4 settings version inflation on auto-run toggle, S-7 time-windowed momentum history, S-6 candle-freshness guard (audit §5 idea 6) | future passes; S-7 changes signal cadence → own spec |
| Any new indicator or threshold sweep | recalibration phase, on clean data |

## 14. Honest caveats for the user

- **The first clean dataset skews short** while the macro backdrop does; regime variety takes calendar weeks. The engine runs on provisional thresholds meanwhile — "correct signals, provisional calibration." Conservative bias covers the interim; expect more NO TRADE than you're used to, and trust it.
- **Verdict character will change on day one**: hold/exit cues flip to tracking the tape (if they felt inverted before, that was C1/C2), wrong-side WEAKs disappear, MTF blocks re-distribute. Different ≠ regression — judge against the tape, not against the old engine's habits.
- Attribution of performance shifts to individual fixes won't be possible — accepted cost of the single boundary (§0).
