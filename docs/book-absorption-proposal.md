# Book Absorption at Structural Levels — Proposal (P4 #6)

**Status:** APPROVED — trader ticked §10 D1–D7, 2026-07-03, all as recommended; **D8 (pull-fraction spoof guard) added and ticked the same day**. Build is Opus-tier at its sequenced slot (after #5 calibrates); **D4 is immediately binding on the #5 build** (the **5** absorption CSV columns are reserved in #5's v0.7→v0.8 rotation). Activation remains evidence-gated per §5 regardless of this approval.
**Target:** two sub-versions, mirroring #5/#4: a display/CSV **build** (zero scoring, behavior-neutral) → a **data-gated scoring activation** (its own ⚠ dataset boundary + version bump).
**Scoring impact:** ⚠ **eventually** — activation is **evidence-gated twice** (§5): independence vs the existing flow stack AND a measured adverse outcome gradient on the collected episodes. No gradient ⇒ stays display-only, honestly.
**Item:** #6 in `websocket-migration-proposal.md` §11: *"resting-size depletion at the active swing high/low without price progress: breakout-quality vs fakeout filter, directly serving structural-breakout entries."*
**Feed:** snapshot v1 — the existing depth-limited top-10 book at ~100 ms + the trades stream. **No auth, no incremental-book plumbing** (roadmap W4: the incremental change_id book is deferred and triggers ONLY if #6-v1 proves the signal class but snapshot fidelity binds).
**Sequencing:** build lands **after #5 calibrates** (roadmap W2). CSV columns are proposed for **reservation at #5's v0.8 rotation** (§7/D4) so #6's own build is rotation-free.

---

## 1. Summary

The trader's entry is a structural breakout; the engine confirms it with impulse (ROC), volume, and — once #5 lands — tape burst. What none of those see is the **other side of the trade**: the resting liquidity AT the level being attacked. Two breakouts can print identical tape; in one the level's resting size depletes and price progresses (genuine break), in the other aggressive flow keeps hitting the level while resting size holds or replenishes and price goes nowhere — the level is **absorbing**. Absorption at the active structural level is the classic fakeout signature: a passive player is defending exactly the price the breakout needs to clear.

#6 measures this directly: while price is pressing the **nearest carried structural level** (the same swing/HVN candidates the TAPE strip already brackets), track the aggressive USD printed into the level band, the band's resting-size trajectory across ~100 ms snapshots, and whether price actually progresses. Heavy flow + no progress + size that won't die = **ABSORB** state on that side. It is the book-response companion to #5's tape-rate: velocity says *how hard the flow hits*; absorption says *whether the level soaks it up*. Together they are the breakout-quality vs fakeout pair the catalogue promised.

## 2. Motivation & profile alignment

- **Directly serves the entry style** (profile §2: breakout above/below prior swing, no chasing). The one microstructure fact that most cleanly separates a tradeable break from a fakeout is whether the defended level is absorbing. This is the engine-side version of watching the wall hold.
- **Conservative bias / low FP tolerance** (profile §6). The proposed scoring shape is **penalty-only against the side pressing into an absorbing level** — it never generates entries, it degrades doomed-looking ones. The 2026-07-03 signal-health audit validated exactly this class: the working signals in the stack are the penalties whose flags identify populations that underperform even after surviving the penalty (RSI-div 38.6% vs 55.2% baseline; Pass 2b CONFLICT 38.9%). Absorption is designed into that class from day one.
- **No bonus/fade arm in v1.** A held level is evidence *for* the fade side, but paying the fade side would tempt counter-trend entries against the breakout style. Deliberately omitted (§10 D2); revisit only with the trader's explicit ask.
- **Anti-correlation rule respected by measurement, not assertion** (profile §4/§7): activation is gated on measured independence from OFI/velocity/TFI (§5.1) — the same working rule as #5's correlation gate — plus the outcome-gradient gate (§5.2) the audit's machinery makes possible.
- **Rare by construction.** The tracker is proximity-gated: it measures only while price is within `proximity_ticks` of a carried level — the breakout moment itself. Most runs it is IDLE and emits nothing. No non-directional payout exists anywhere in the design (NONE pays nothing; states are inherently sided).

## 3. The independent dimension

| Signal | Reads | Scope | Axis |
|---|---|---|---|
| OFI (v46 time-averaged) | resting book imbalance | whole visible ladder | book **shape** |
| #5 aggressor velocity | taker USD/sec vs norm | whole tape | flow **rate** |
| TFI / MicroCVD / CVD | net aggressor windows | whole tape | flow **direction/accel** |
| **#6 absorption** *(new)* | **flow × book response** | **one structural level** | does the defended level **soak the flow** |

Absorption is the only signal that reads the *interaction* of flow with resting liquidity, and the only level-scoped one. The audit (F8) measured the existing flow stack as genuinely independent (max pairwise agreement 69.3%; OFI×CVD 50.2%) — the redundancy bar for a new entrant is therefore individual-value, not stack-decorrelation, and §5 measures both anyway. Nearest prior art in-engine is the VPFR HVN-proximity *static* wall; absorption is the *dynamic* truth of whether that wall is actually being defended right now.

## 4. Design

### 4.1 Level set and the proximity gate

Watched levels = the **nearest carried structural level above and below price**, from the same carried candidates the #3 TAPE strip brackets: `LastSwingHigh5m`, `LastSwingLow5m`, `VPFRNearestHvnAbove`, `VPFRNearestHvnBelow` (refreshed each full run; carried between runs). Per side, the tracker is **ACTIVE** only while the touch price is within `proximity_ticks` of the level (best ask vs the above-level; best bid vs the below-level); otherwise **IDLE** — no measurement, no state, no cost. If the carried level re-maps on the next full run mid-episode, the episode resets (no cross-level bleed).

### 4.2 Episode tracker (snapshot-feed mechanics)

New host-agnostic **`Core/LevelAbsorptionTracker.vb`**, owned by `MarketState`, the first **dual-fed** tracker: folded from both `UpdateBook` (each ~100 ms depth-limited snapshot) and `AppendTrade` (each print) — `MarketState` already sees both under one lock, so the fold sites mirror `FoldOfi` / #5's `ApplyTrades` fold exactly. Reset on (re)connect in `SeedAsync`, same discipline as `OfiAccumulator`.

Per active side (ABOVE case; BELOW mirrors), an **episode** runs from proximity-entry until price leaves proximity or breaks through:

- **Pressing volume** `aggrUsd`: rolling `window_sec` sum of aggressive BUY USD printed at prices ≥ `level − band_ticks` (from the trades stream; amounts are USD-notional on the inverse contract, so trades and book sizes share units).
- **Band size trajectory**: resting ask USD in `[level, level + band_ticks]` per snapshot → `sizeStart` (at episode start), `sizeMin`, `sizeNow`. Replenishment shows as `sizeNow` recovering after prints.
- **Progress test**: trade/touch price > `level + break_tol_ticks` ⇒ the level gave way — episode ends, state → NONE immediately (a broken level must never carry a stale ABSORB reading).
- **Pull accounting (D8 spoof guard)**: fills are unfakeable ground truth, so per snapshot interval the band size obeys the conservation identity `ΔSize = Posts − Pulls − Fills`, with ΔSize (snapshots) and Fills (trades at band prices) both observed. Accumulate per episode: `pullLB = Σ max(0, −(ΔSize + Fills))` — a **hard lower bound on volume pulled without being filled** (the spoof signature) — and `postLB = Σ max(0, ΔSize + Fills)`. `pullFrac = pullLB / max(postLB, depletion_floor_usd)`. A sitting defender ⇒ `pullFrac ≈ 0`; cycling paint ⇒ it grows with the churn. **Visibility mask (implementer note):** ΔSize is computed only over the band portion visible in *both* consecutive top-10 snapshots, so a shifting ladder window cannot fake size deltas; fills are assigned to intervals by timestamp (episode-aggregated bounds tolerate the 100 ms jitter).

**The metric:** `absorbRatio = aggrUsd / max(sizeStart − sizeMin, depletion_floor_usd)` — USD traded into the band per USD of net band depletion. High ratio = the band is eating flow without dying (depletion is small or replenished) = absorption. The D8 guard then asks *who* kept it alive: replenishment with provable pulls above `max_pull_frac` is treated as painted defense and vetoed.

**State classification** (a pure `ClassifyAbsorption(...)` helper, harness-testable, parallel to `ClassifyOfiRatio` / `ClassifyAggressorBurst`):

```
ACTIVE side ABOVE, no progress, aggrUsd >= min_aggr_usd, absorbRatio >= absorb_ratio  →  ABSORB_ABOVE
ACTIVE side BELOW, no progress, sellUsd >= min_aggr_usd, absorbRatio >= absorb_ratio  →  ABSORB_BELOW
either candidate with pullFrac > max_pull_frac                                        →  NONE  (D8 veto — painted defense)
otherwise                                                                              →  NONE
```

`ABSORB_ABOVE` = resistance being defended (adverse to longs pressing up); `ABSORB_BELOW` = support defended (adverse to shorts). Inherently directional; NONE is the modal state by construction.

### 4.3 Read path

`RunAnalysisAsync` (WS-live path) reads the tracker snapshot into new `IndicatorResults` fields (`AbsorptionSignal`, `AbsorptionLevel`, `AbsorptionRatio`, `AbsorptionAggrUsd`); `LiveMicrostructureEvaluator` reads the same snapshot for the strip tag (§7). REST transport / per-run fallback / cold feed / no carried levels ⇒ `NONE` + null numerics — never blocks, never guesses (same rule as #5 §8).

### 4.4 Scoring integration (activation sub-version ONLY, after §5 clears)

One appearance, penalty-only, in Step 2's entry-quality class (spread/liq shape):

- Dominant-side **LONG** pressing while `ABSORB_ABOVE` is active → `LongScore − penalty` (floored at 0).
- Dominant-side **SHORT** while `ABSORB_BELOW` → `ShortScore − penalty`.
- No bonus arm; no effect on the fade side; NONE ⇒ nothing.

Breakdown row `Absorption` renders only when the penalty fires (label + level + ratio in the note) — snapshot + card binding land in the same commit per the display-string parity rule.

## 5. Activation gate — evidence-gated twice (the ⚠ core)

Runs on the post-build collection (episodes logged to CSV, joined to the eval cache — the W1 audit's standing instrument re-run does both measurements automatically).

**5.1 Independence (the #5 working rule, for consistency):** |Spearman| of `AbsorptionRatio` (episode-active runs) vs `OFIRatio`, #5 `burstRatio`, and TFI < 0.7, and directional fire-overlap < 80%. Fails ⇒ display-only, #6 closes there.

**5.2 Outcome gradient (the audit's discipline — new for this spec class):** directional verdicts that fired INTO an active same-side absorption must show a **≥ 10 pp worse success rate** than the no-absorption baseline, on **n ≥ 30 flagged evaluated rows** (barrier outcomes from the eval cache, audit §5 method). No gradient ⇒ the penalty stays unwired regardless of 5.1. This is the first signal whose activation requires demonstrated outcome evidence, not just fire-rate plausibility — the audit made this measurable; use it.

**Calibration on activation:** there is no reference era for a new signal, so thresholds are set by **target-engagement design**: `min_aggr_usd` / `absorb_ratio` / `proximity_ticks` tuned so the penalty engages on ~**3–8% of directional runs** (the audit-validated penalty-class band: RSI-div arm 3.9–9.2% per session). `min_aggr_usd` is expected to split per-session (NY tape ≫ Asia) — the reserved `sessions{}` tier (§6) takes the v40-pattern nullable overrides, hand-tuned per HC11 class. All shipped anchors are PROVISIONAL until this pass.

## 6. Config — new `indicators.absorption` block

Three-tier surface exactly per #5 §6 (who changes each key, not whether it's exposed; no hardcoded numbers):

```json
"absorption": {
  "enabled": true,                 // feature switch — OFF tweaker surface (exact-match reject + HC line)
  "scoring_enabled": false,        // the ⚠ activation gate — OFF surface (exact-match reject)
  "proximity_ticks": 12,           // ACTIVE gate distance from the level — ON surface
  "band_ticks": 4,                 // level band width for size tracking — ON surface
  "window_sec": 10,                // rolling pressing-volume window — ON surface
  "break_tol_ticks": 2,            // progress tolerance beyond the level — ON surface
  "absorb_ratio": 3.0,             // pressing USD per USD net depletion — ON surface
  "depletion_floor_usd": 25000,    // divide-by-nothing guard — ON surface
  "max_pull_frac": 0.5,            // D8 spoof-guard veto: provable pulls / provable posts — ON surface
  "penalty": 1,                    // scoring magnitude once activated — ON surface
  "default":  { "min_aggr_usd": 150000 },          // hand-tuned re-baseline tier (HC11 class)
  "sessions": { "NY": {}, "LONDON": {}, "ASIA": {} } // nullable per-session overrides — hand-tuned, OFF surface
}
```

All values above are PROVISIONAL anchors (§5 calibrates). Version bump + change_log + §15 at the build; activation is a later bump with the calibrated values (and the §12 post-ship watch row).

## 7. Display-parity + CSV

- **Build sub-version:** the TAPE strip gains a compact `ABS↑ 60510 (3.4x)` / `ABS↓ …` tag while a state is active (D6) — live status-bar element like the rest of the strip, NOT an RTF/snapshot/card surface ⇒ no card-binding obligation (the #3 precedent, stated per the parity rule). CSV columns (**5**): `AbsorptionSignal`, `AbsorptionLevel`, `AbsorptionRatio`, `AbsorptionAggrUsd`, `AbsorptionPullFrac` (D8 — logged even on vetoed episodes; it is also the W4 fidelity-binds evidence, §12).
- **Column reservation (D4):** the five columns are **reserved in #5's v0.7→v0.8 rotation** (which already adds the burst + TFI + retune-C1 columns) and stay null until #6's build populates them — one rotation for the whole wave instead of a v0.9 split of the book. If the trader declines, #6's build rotates to v0.9 (acceptable, just costlier).
- **Scoring sub-version:** the conditional `Absorption` breakdown row ⇒ snapshot + card in the same commit (parity rule).

## 8. Edge cases & safety

- **Spoof/churn — mitigated by D8, residual stated honestly:** the pull-fraction veto catches repost cycling at interval resolution and above (hundreds of ms to seconds — the common case). A pull+repost round trip completed *inside* one 100 ms interval nets to ΔSize=0 with no fills and evades the bound — but sub-100 ms flicker is invisible to every consumer of Deribit's public feed, and where evasion succeeds the signal degrades exactly to the pre-D8 baseline, never below it. Damage direction is conservative either way: a false ABSORB suppresses an entry (a missed trade, profile-tolerated); false-negatives are unspoofable because they would require faking fills. The W4 incremental book (change_id fidelity) remains the full fix and triggers only if §5 proves the class while the logged `pullFrac` evidence shows snapshot fidelity binding.
- **Level >10 ticks away** ⇒ ladder can't even see it ⇒ IDLE by the proximity gate anyway (the gate distance must stay ≤ the visible ladder span; `proximity_ticks` 12 vs top-10 ladder ≈ enforce `min(proximity, visible)` in the tracker).
- **Reconnect / gap** ⇒ episode reset in `SeedAsync`; re-arms on the next approach.
- **Break-through** ⇒ state cleared instantly (no stale ABSORB after the level gives way).
- **REST / fallback / no levels / warmup** ⇒ `NONE`, no penalty, never blocks (§4.3).
- **Reversibility:** `scoring_enabled=false` unwires the penalty hot; `enabled=false` stops the tracker. Both hot-toggle rollbacks; `enabled=false` byte-identical to pre-build (prove in harness).

## 9. Acceptance

**Build:** 3 Release builds 0/0; `enabled=false` byte-identical regression; harness fixtures — episode lifecycle (approach→ACTIVE→absorb / break-through→NONE / leave-proximity→reset), `ClassifyAbsorption` threshold edges incl. the D8 veto (a churn sequence with fills-absent size drops → pullFrac above threshold → NONE; a sitting-defender sequence → pullFrac ≈ 0 → state fires), conservation accounting (fed ΔSize/fill sequences → expected pullLB/postLB), dual-fold under lock, reconnect reset, REST-inactive; strip tag renders only when active; CSV columns populate (or stay null pre-build if reserved at #5).
**Activation (data-gated, later):** §5.1 + §5.2 numbers reported with the go/no-go recorded either way; if proceeding — target-engagement table (per session), penalty regression through `Calculate()`, breakdown-row parity fixtures, §12 watch row added.

## 10. Sign-off decisions — ALL TICKED by the trader 2026-07-03

| # | Decision | Recommendation |
|---|---|---|
| D1 | Mechanism: proximity-gated level episode tracker; `absorbRatio` = pressing USD per USD net band depletion | **Yes** — the snapshot-feed-honest construction of the §11 promise |
| D2 | Scoring shape: penalty-only against the pressing side; NO fade-side bonus in v1 | **Yes** — audit-validated penalty class; anti-counter-trend |
| D3 | Activation gate: independence (#5 rule) AND ≥10 pp adverse gradient on n≥30 flagged evaluated rows | **Yes** — first outcome-evidence-gated activation; the audit machinery measures it |
| D4 | Reserve the 4 CSV columns in #5's v0.8 rotation (one rotation; #6 build becomes rotation-free) | **Yes** |
| D5 | Watched levels v1 = nearest carried above + below from the strip's candidate set (swing + HVN) | **Yes** — reuses the carried-level pattern; no new level machinery |
| D6 | TAPE-strip ABS tag at build | **Yes** — cheap, awareness-only, strip discipline |
| D7 | Config three-tier surface per #5 §6 (flat ON; `sessions[]` hand-tuned OFF; switches exact-match OFF + HC lines) | **Yes** |
| D8 | Pull-fraction spoof guard (added + ticked 2026-07-03): `pullLB`/`postLB` conservation accounting, `max_pull_frac` veto (0.5 provisional, ON surface), `AbsorptionPullFrac` as the 5th reserved column, per-interval visibility mask | **Yes** — snapshot-only, additive, catches interval-resolution churn; residual flicker degrades to the pre-D8 baseline, never below |

## 11. Implementation map (files)

- **New `Core/LevelAbsorptionTracker.vb`** — episode state machine + rolling sums + the D8 conservation accumulators (`pullLB`/`postLB` with the visibility mask) + `Snapshot()`; host-agnostic, no WinForms.
- **`Core/Indicators_OrderFlow.vb`** — pure `ClassifyAbsorption(snapshot, cfg)`.
- **`MarketState.vb`** — owns the tracker under its lock; fold hooks in `UpdateBook` + `AppendTrade`; reset on seed; carried-levels setter (from the run's `_lastSuccessfulIndicators`, the strip's existing carry).
- **`DeribitWsFeed.vb`** — calls the two folds (analogue of `FoldOfi` post-`UpdateBook`).
- **`UI/MainForm_Analysis.vb`** — read snapshot → new `IndicatorResults` fields; REST path → NONE.
- **`Core/IndicatorResults.vb`** — `AbsorptionSignal/Level/Ratio/AggrUsd/PullFrac`.
- **`LiveMicrostructureEvaluator.vb`** — ABS tag.
- **`AnalysisLogger.vb`** — 5 columns (reserved at #5's rotation per D4, else v0.9 here).
- **`EngineSettings.vb` + `settings.json`** — the block; version bump + change_log + §15.
- **`tools/AutoTweaker/`** — exact-match rejects `absorption.enabled`/`absorption.scoring_enabled` + HC line for `absorption.sessions[].*`/`absorption.default.*` (hand-tuned tier); flat params stay reachable.
- **`verify/ordercheck/`** — §9 fixtures.
- **(Activation)** — `ScoringEngine_Calculate_Scoring.vb` penalty wire-in + breakdown row + parity.

## 12. Sequencing / out of scope

- Spec approval this window → **columns reserved at #5's v0.8 build** (D4) → **#6 build after #5 calibrates** (roadmap W2 order; the build itself is behavior-neutral with `scoring_enabled:false` and, with D4, rotation-free) → multi-session episode collection → §5 gates → **activation as its own ⚠ boundary**.
- Out of scope: fade-side bonus (D2), multi-level simultaneous tracking beyond the nearest pair, deeper-than-visible-ladder levels, the incremental full-depth book (W4 trigger unchanged — and now *measured*: the logged episode `pullFrac` distribution is the fidelity-binds evidence), any change to VPFR/swing level *derivation* (this consumes carried levels, never recomputes them).
