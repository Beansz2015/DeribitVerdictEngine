# Aggressor Velocity Build + Retune Bundle — Implementer Spec-Back (v50)

**Date:** 2026-07-03 (B4 implementer seat). **Settings:** v49 → **v50**. **CSV:** v0.7 → **v0.8**.
**Status:** IMPLEMENTED, gate-green, **local 4-commit stack, NOT pushed** — trader tests + pushes; coordinator review follows.
**Specs implemented:** `aggressor-velocity-proposal.md` (build sub-version), `signal-health-retune-proposal.md` R1/R2/C1/C2, `book-absorption-proposal.md` §7/D4/D8 (columns only), `placed-geometry-structural-first-proposal.md` §8/D5 (columns only). Checklist authority: `roadmap.md` §5 item 3.

---

## 0. The commit stack (one boundary event, v31 precedent)

| # | Commit | Phase |
|---|---|---|
| 1 | `feat(#5): aggressor velocity build …` | Build proper — accumulator, classifier, folds, read path, strip, settings block, HC19, A23a–g. Proven against **unchanged scoring**. |
| 2 | `feat(csv): v0.7->v0.8 rotation …` | 16 columns + C2 F8 + shared `ComputeSideLevels` + A24a. Behavior-neutral logging. |
| 3 | `feat(retune): R1 … R2 …` | The scoring-affecting settings changes — deliberately **last**, + HC20 + A25a/b. |
| 4 | `chore(v50): version bump + docs + spec-back` | v50 `change_log`, §15 row, this doc, roadmap tick. |

Sequencing satisfied: Phase 1 landed complete with the `enabled=false`-byte-identical argument **before** any retune value moved; the rotation (Phase 2) was validated against pre-retune scoring.

## 1. What was built (deltas vs the specs — all faithful, deviations noted)

### #5 build (aggressor-velocity-proposal.md §4/§6/§7/§8)

- `Core/AggressorVelocityAccumulator.vb` — two-horizon time-decayed buy/sell USD sums exactly per §4.1 (`dt` floored at 0; `tau<=0 → decay 0`; first fold seeds). `Snapshot(grossFloor, minCoverage)` returns grossFast/grossNorm/burstRatio/netUsdPerSec/lean + warmup. **Fold stamp = the trade's own exchange timestamp** (trades carry one; the OFI book fold uses receive time only because book updates don't) — faithful to §4.1's `(amount, direction, tsMs)`.
- **§11 "settle in the build" settled:** the per-session norm tau is resolved **at fold time** (feed reads `SettingsLoader.Current` + the receive-hour session per notification batch, mirroring `FoldOfiAverage`'s hot-reload pattern); `Snapshot` divides by the **taus used at the last fold**, so read and fold can never disagree on the horizon. At a session boundary the EMA transitions smoothly over ~tau — no reset needed.
- Warmup gate = full **norm** window of fold coverage + ≥5 trades (mirrors `OfiAccumulator.MinWarmupUpdates`). Seed trades are **not** folded (§8 cold-feed suppression covers the window; only live prints carry the burst).
- `IndicatorEngine.ClassifyAggressorBurst` — pure, §4.3 verbatim (`>=` on both threshold and lean floor).
- `ExecutionResolution.ResolveAggrVelNormWindow/BurstThreshold` — session-name (case-insensitive) override → shared default; NY seeds 60s in both settings.json and the POCO defaults.
- Read path (`RunAnalysisAsync`): WS-live (`src Is _wsSource`) + enabled + warmed → `r.AggrVelBurstRatio/Net/Signal`; otherwise `NORMAL` + `Nothing` numerics. **`AggrVelNet` is net taker USD/sec on the burst horizon** (rate, not raw sum — matches the §7 column description).
- Strip (§7): `MicrostructureSnapshot` gains `HasBurst/BurstRatio/BurstSignal`; `ComposeTape` renders `N tr/s ($X/s) R.R×` and appends ` BURST↑`/` BURST↓` on a burst. **Strip-only surface — no card/snapshot line added or changed**, per the #3 precedent under the display-string parity rule (stated here and in commit 1).
- Config: `indicators.aggressor_velocity` per §6 verbatim. POCO note: the JSON `"default"` key maps to the VB property **`Defaults`** (`Default` is a VB keyword); `[JsonPropertyName("default")]` keeps the JSON shape spec-exact.
- Tweaker three-tier surface: flat params ON (nothing to do — dotted paths resolve); `enabled`/`scoring_enabled` **exact-match** rejects + `default.`/`sessions.` **prefix** rejects in `SettingsDiffApplier`; **PromptBuilder HARD CONSTRAINT 19**. §6 note: the spec's "array-nested / applier can't resolve `sessions[].`" rationale doesn't literally apply here because `sessions{}` is an **object** keyed by name (resolvable by dotted path) — hence the explicit prefix fence, which enforces the same HC11-class policy the spec intends.
- **NOT implemented (per scope):** the TFI-modifier scoring wire-in (`upgrade_bonus`/`contra_penalty` are live keys but no scoring read site exists yet), the #6 absorption tracker, placed-geometry arbitration logic (B4b).

### CSV v0.8 (one rotation, roadmap §5 item 3 manifest — 16 columns appended)

`AggrVelBurstRatio (F4), AggrVelNet (F0), AggrVelSignal, TFIValue (F4), TFISignal, AbsorptionSignal, AbsorptionLevel, AbsorptionRatio, AbsorptionAggrUsd, AbsorptionPullFrac, PlacedTargetLong, PlacedStopLong, PlacedTargetShort, PlacedStopShort (all F2), InstanceId, SignalId`

- AggrVel numerics write **empty** (not 0) when unavailable — "no data" is distinguishable from "quiet tape" for the §5.1 correlation-gate analysis.
- Absorption ×5: **empty until the #6 build** (D4/D8 reservation honoured; #6 lands rotation-free).
- **Placed\* parity:** the per-side arbitration was **extracted** from `SignalEmitter.BuildSideLevels` into public `SignalEmitter.ComputeSideLevels` (+ `SideLevels` struct); `BuildOk`'s levels block and `AnalysisLogger.LogRun`'s Placed\* columns both call it — equal **by construction** (the parity-hint option "extract/share", not "mirror"). Payload JSON byte-identical to v49 (A22a–g unregressed prove the refactor). `LogRun` gained a `cfg` parameter (single call site).
- Attribution: `InstanceId`/`SignalId` read `ProcessIdentity` at `LogRun`; the id ticks in `RunAnalysisAsync` **before** `LogRun` (already true since v49), so CSV SignalId ≡ payload signal_id. SKIPPED runs burn an id with no CSV row — expected; the join is total CSV→payload, partial payload→CSV.
- C2: `FundingRate` `F6`→`F8` — same column, no header change (2e-7-scale deltas were rounding away at F6).
- Rotation: header mismatch → `analysis_log.csv.v0.7.bak` (timestamp-suffixed on collision). **Never delete the .bak** — the v48 §4a per-session fire-rate watch reads it.

### Retune R1 + R2 (signal-health-retune-proposal.md §2/§3)

- **R1:** `indicators.OFI.momentum_enabled` true→false. Mechanism check confirmed pre-change: the modifier gate in `RunScoringPipeline` (`ScoringEngine_Calculate_Scoring.vb` ~:308) is the **only** scoring read site; the note's `MOM:{state}` segment renders from `r.OFIMomentum` unconditionally, so display parity holds with zero render change (the suffix simply stops, exactly as on FLAT/BALANCED today).
- **R1 rider (D5):** `indicators.OFI.momentum_` prefix in `RejectedPathPrefixes` + **HARD CONSTRAINT 20**. Prefix-safe: `book_depth`, `buy/sell_dominant_ratio`, `averaging_enabled`, `avg_window_sec` don't share the prefix (A25b asserts accept). Reversal path: flip + drop fence in one commit.
- **R2:** `indicators.funding.momentum_threshold` 5e-8 → 2e-7 (D2 as ticked). Window/amplify/soften untouched. The cadence-dependence of the 3-change window remains the recorded out-of-scope defect (retune §7 — time-anchored window is its own mini-spec later).
- **POCO defaults ride this code commit** (v33/v34 precedent + the brief's explicit instruction): `OfiSettings.MomentumEnabled=False`, `FundingSettings.MomentumThreshold=2e-7`, **plus** the v48-promised dominance-pair sync (`BuyDominantRatio 2.0→1.60`, `SellDominantRatio 0.5→0.625` — the v48 change_log assigned that sync to "the next code commit"; this is it; zero behaviour change with settings.json present). Observation for the coordinator: `AtrSettings.StaticRef` POCO default (115.0) still carries the same v37 "rides next code commit" promise and has now outlived several code commits — left untouched here (not cargo), flagging for a future hygiene pass.

## 2. Acceptance evidence

- **Builds:** solution (Release) + AutoTweaker + OrderCheck **0/0** after every phase. No Debug build was ever invoked (the live collector runs from `bin\Debug` — untouched).
- **Harness:** A1–A22g unregressed at every phase; new **A23a–g** (steady-tape rate vs the analytic EMA-sum fixed point `A*=a/(1−e^(−dt/τ))`; balanced-baseline→one-sided-burst → `BURST_BUY`; cold-start suppression at 3-trade and 60s-coverage points; reset re-arm; classifier boundary `>=` edges incl. the balanced-firehose and one-sided-trickle NORMAL guards; NY-60/inherit-120 session resolution + explicit override; HC19 three-tier surface), **A24a** (Placed\* ≡ payload levels across uncapped/capped/noise-suppressed + an absolute pin), **A25a** (disabled modifier ≡ no-modifier path through the real `Calculate()` — scores, verdict, and note; the flag still gates when re-enabled: +1[S] confirm reappears), **A25b** (HC20 fence + sibling accepts). Full run: **ALL PASS**.
- **verify-gate.ps1 -Mode prepush:** green before the stack (baseline) and after (result recorded in §4 below).
- **`enabled=false` byte-identical (spec §9):** with `aggressor_velocity.enabled=false` the feed folds nothing (`ApplyTrades` short-circuits), the run path leaves `NORMAL`/`Nothing`, and the strip renders the plain v45 tape field; scoring has **no** read site at any setting in this sub-version. The CSV columns exist regardless of the flag (they are the schema, not the signal).

## 3. What opens after this boundary

1. **#5 collection** — multi-session WS data with the burst columns populated; then the **§5.1 correlation gate** (Spearman lean↔TFI + fire-overlap; working rule: >0.7 AND >80% ⇒ display-only, #5 closes honestly) and, only if it clears, the **§5.2 per-session firing-rate re-baseline** + the scoring sub-version (TFI-modifier wire-in, `scoring_enabled` flip, breakdown-note parity commit).
2. **Retune §5 post-ship watch** (first 2 weekday sessions): FundingMomentum FLAT% ≈ 60–70%, Step 3b engagement ≈ 15–25%, OFI row shows `MOM:state` with no suffix.
3. **v48 §4a fire-rate watch** continues — reads the rotated `analysis_log.csv.v0.7.bak` plus new v0.8 rows (`OFISignal` column unchanged in name/semantics).
4. **#6 absorption build** (after #5 calibrates) — rotation-free, its 5 columns are waiting.
5. **B4b placed-geometry implementation** (separate conversation) — the Placed\* columns log current-geometry values from day one; the arbitration inputs change there, the `ComputeSideLevels` sharing survives it.

## 4. Gate results (post-stack)

Recorded after the final commit: `tools/checks/verify-gate.ps1 -Mode prepush` → builds 0/0, harness ALL PASS (A1–A25b), display-parity OK (no snapshot/card drift — the strip is not a parity surface), version-bump OK (engine-path changes accompanied by the v50 bump). See the commit stack for per-phase evidence.
