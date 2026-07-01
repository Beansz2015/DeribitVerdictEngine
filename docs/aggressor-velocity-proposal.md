# Aggressor Velocity / Tape Burst — Proposal (P4 #5)

**Status:** **§10 decisions RESOLVED (trader sign-off 2026-07-01).** Not built. Sequenced **after** the v47 OFI re-baseline lands (§12). The one item still open is the **final scoring go/no-go**, which is reserved for the correlation-gate outcome (§5.1) — and that gate runs on a *separate, later* collection that can only begin once #5's build exists (aggressor velocity isn't logged until then).
**Target:** settings **v47→v48-ish**, in **two sub-versions**: a display/CSV **build behind a dated dataset boundary** (no scoring), then a **data-gated scoring** version — mirroring the #4 build→re-baseline split.
**Scoring impact:** ⚠ **YES, eventually** — but gated on an empirical correlation check against TFI (§5). The build sub-version is display/CSV-only (zero scoring); scoring is wired **only if** aggressor velocity proves it carries information TFI doesn't.
**Item:** #5 in `websocket-migration-proposal.md` §11 (⚠ re-baseline-flagged): *"USD-per-second aggression vs a rolling norm: the tick-resolution version of your entry-impulse confirmation. Must be specced against TFI for correlation (it may upgrade TFI rather than join it — profile's anti-correlation rule)."*
**Gate:** WS-only (needs the live trade stream over time); the build is safe to land at its version boundary, but scoring is data-gated on post-build accumulation + the correlation measurement.

---

## 1. Summary

The trader's entry is a **structural breakout confirmed by impulse (ROC) and a volume spike** (profile §2). The engine confirms the impulse on candles — `ROC(9)` and `VolumeRatio` vs `SMA(9)`, both bar-resolution and therefore lagging the actual break by up to a bar. The WS feed now streams every print, so we can measure the **tick-resolution** version of that confirmation: **how fast aggressive (taker) USD is hitting the tape, and in which direction, right now** — a firehose vs a trickle — normalised against the tape's own recent baseline.

This is **aggressor velocity**: USD-per-second of taker flow (buy and sell), time-weighted over a short window, expressed as a **burst ratio** against a longer rolling norm, with a direction from the net flow. A structural break printing on a 4× tape burst in the break direction is a real impulse; the same break on a dead tape is a candidate for a fakeout.

**The catch, and why §5 exists:** the engine already has three taker-flow indicators — **TFI** (30-trade aggressor imbalance), **MicroCVD** (50-trade accel/decel), **CVD** (500-trade weighted slope) — plus candle **VolumeRatio**. Aggressor velocity must be shown to occupy a **different axis** than all of them (specifically TFI, per the catalogue) before it earns a scoring vote. The profile's anti-correlation rule (§4, §7) is the binding constraint here, not an afterthought.

---

## 2. Motivation & profile alignment

- **Tick-resolution entry-impulse confirmation** (profile §2 "confirmed by impulse and volume spike"). VolumeRatio is candle-lagged and non-directional (it counts both sides of the bar). Aggressor velocity is sub-bar, directional, and leading — it sees the firehose as it starts, not at the next bar close.
- **Quality over quantity** (profile §6). The intended effect is *not* more signals — it's a **confirmation filter**: it upgrades a breakout-direction taker impulse that is genuinely bursting and withholds the upgrade when the tape is thin. Firing-rate-matched (§5) so it re-distributes conviction, not inflates it.
- **Respecting the anti-correlation / anti-double-count rules** (profile §4, §7; rejected patterns "non-directional rewards", "double-counting the same signal across scoring layers", "changes that increase indicator correlation"). Three hard commitments, all enforced by design + the §5 gate:
  1. **Never a non-directional payout.** The gross tape-speed component is non-directional and is used **only** as a gate (is the tape bursting?) that must combine with a **directional** net-flow lean before anything fires. A balanced firehose fires nothing.
  2. **One appearance, as a modifier — not a parallel vote.** The integration (§4.5, confirmed §10.1) is a **modifier on TFI's existing vote** (like OFI-momentum modifies the OFI level score, and funding-momentum amplifies/softens the funding penalty), not a new Microstructure category. It appears in scoring exactly once.
  3. **Correlation measured, not assumed.** §5 makes the scoring wire-in conditional on a measured independence check vs TFI (and vs MicroCVD / VolumeRatio). If it's redundant, it stays display-only. This is the whole point of the two-sub-version split.

---

## 3. What it is — the independent dimension

The existing taker-flow indicators and the axis each occupies:

| Indicator | Window | Measures | Time-aware? | Magnitude-bearing? |
|---|---|---|---|---|
| **TFI** | last 30 trades | net aggressor **imbalance ratio** `(buy−sell)/total` | **No** — trade-count window | **No** — normalised ratio |
| **MicroCVD** | last 50 trades | **accel/decel** of net delta across thirds | No — trade-count | Partly (delta magnitude) |
| **CVD** | last 500 trades | weighted **slope** of net delta | No — trade-count | Yes (USD) |
| **VolumeRatio** | current bar | bar volume vs `SMA(9)` | Bar-resolution | Yes, but **non-directional** |
| **Aggressor velocity** *(new)* | last ~`fast_window_sec` | taker **USD/sec**, as a **burst ratio** vs a rolling norm, + net direction | **Yes** — wall-clock | **Yes** — rate, normalised |

The dimension nothing else covers: **magnitude per unit wall-clock time.** TFI can read +0.4 whether those 30 trades printed in 2 seconds or 90 seconds, and whether they were 30 dust trades or 30 blocks. Aggressor velocity is exactly that missing information — *how hard, how fast*. On a bursty tape (the breakout moment the trader cares about) it diverges sharply from TFI; on a steady tape it will track TFI, and §5 is the test of how often that happens in practice.

Distinct from **MicroCVD** too: MicroCVD's accel/decel is indexed by **trade count** (late third vs early third of 50 trades). Aggressor velocity's rate is indexed by **wall-clock**. Fifty trades that arrive as a 3-second burst then silence read "decelerating" to MicroCVD (late third quieter than early) while aggressor velocity correctly reports the burst already happened and is fading — same data, different question (accel of net delta vs rate of gross flow).

**Horizons vs hold duration (clarified 2026-07-01).** The `fast`/`norm` horizons govern **entry-impulse detection** — the burst of aggression as price takes the swing level, a 5–15-second event. They are *not* tied to hold duration: the trader's 2–15-minute hold (profile §2) is an **exit-management** concern owned by `CalcHoldStatus` / the realtime exit guard, not by this signal. Aggressor velocity confirms the *break*; a longer hold does not make the break slower, so it does not argue for longer horizons.

---

## 4. Design

### 4.1 Feed-side accumulator (mirrors `OfiAccumulator`)

A new host-agnostic **`AggressorVelocityAccumulator`** on `MarketState`, fed one sample per trade from `DeribitWsFeed.ApplyTrades` (the trade analogue of `ApplyBook → FoldOfiAverage`). It holds **time-decayed running sums** of taker USD, split buy/sell, at **two horizons** — a fast burst horizon and a slow rolling-norm horizon:

For each horizon with e-folding time `tau`, keep `Abuy`, `Asell`, `lastT`. On a trade `(amount, direction, tsMs)`:

```
dt      = max(0, (tsMs - lastT) / 1000)        ' seconds; floored like OfiAccumulator
decay   = exp(-dt / tau)                        ' tau <= 0 → decay 0 (defensive overwrite)
Abuy   *= decay : Asell *= decay
if direction = "buy"  then Abuy  += amount  else Asell += amount
lastT   = tsMs
```

`A` is an exponentially-weighted sum of recent USD with horizon `tau`; for a steady rate `r` USD/sec its fixed point is `A* = r·tau`, so the **flow rate = A / tau** (USD/sec). O(1) per trade, time-aware, non-spiky (no `amount/dt` division). Same `exp(-dt/tau)` / `dt`-floor / reset-on-connect discipline as `OfiAccumulator` — reset in `SeedAsync` so no pre-disconnect flow bleeds across a gap.

**Horizon defaults (§10.3):** `fast_window_sec = 5` (a 5s burst window — long enough that a single block doesn't dominate the rate, short enough to stay sub-bar and leading; 3s was too single-print-sensitive). The **norm horizon is per-resolution** (§10.4): `norm_window_sec ≈ 60` for **NY×1** (dense 1-min tape → a 1-minute baseline holds enough prints) and `≈ 120` for the **3-min sessions** (slower tape → a 2-minute baseline avoids a jumpy ratio). A norm shorter than ~one bar reads noise as burst; too long blurs the "recent normal". All are pre-calibration anchors — §5 refines them.

### 4.2 The burst metric

Read at run time (and by the live strip) from a `Snapshot(...)`:

```
grossFast = (Abuy_fast + Asell_fast) / fast_window_sec      ' USD/sec, burst horizon
grossNorm = (Abuy_norm + Asell_norm) / norm_window_sec      ' USD/sec, rolling baseline (per-session)
burstRatio = grossFast / max(grossNorm, gross_floor)        ' how many× the norm
netFast    = (Abuy_fast - Asell_fast)                       ' signed USD, burst horizon
lean       = netFast / max(Abuy_fast + Asell_fast, eps)     ' -1..+1 directional lean
```

- **`burstRatio`** — the "vs a rolling norm" quantity from the catalogue. ~1 on a normal tape; ≫1 during a burst. `gross_floor` prevents a dead-tape norm from manufacturing a giant ratio out of noise.
- **`lean`** — direction, in `[-1, +1]`. Sign = which side is aggressing; magnitude = how one-sided the burst is.

### 4.3 Signal states

```
if burstRatio >= burst_ratio_threshold and lean >=  direction_lean_floor  →  BURST_BUY
if burstRatio >= burst_ratio_threshold and lean <= -direction_lean_floor  →  BURST_SELL
else                                                                      →  NORMAL
```

Fires **only** when the tape is bursting **and** it's directional. A balanced firehose (`|lean| < floor`) → `NORMAL`; a one-sided trickle (`burstRatio < threshold`) → `NORMAL`. This is the non-directional-reward guard from §2.1 made concrete. `burst_ratio_threshold` is **per-session** (§5.2).

### 4.4 Host-agnostic core

`AggressorVelocityAccumulator` references no WinForms — fed by `DeribitWsFeed`, read by `RunAnalysisAsync` and `LiveMicrostructureEvaluator`. The burst/lean/signal math is a pure function of the accumulator snapshot + config (a small `ClassifyAggressorBurst` helper, testable in the OrderCheck harness like `ClassifyOfiRatio`).

### 4.5 Integration — modifier on TFI (confirmed §10.1)

Wired as a **modifier on TFI's existing vote** (the "upgrade TFI rather than join it" steer), in the **scoring** sub-version only, and only after §5 clears it:

- **TFI fires a directional PRESSURE vote AND `BURST_*` confirms the same side** → **upgrade** TFI's contribution (partial→full, or a small bonus capped at regimeMax) — the tick-resolution confirmation the trader's entry rule wants.
- **TFI fires but tape is `NORMAL`** → TFI unchanged (a fired TFI is still valid, just not burst-confirmed — no penalty; we don't punish a real imbalance for arriving calmly).
- **`BURST_*` opposes the TFI direction** → **soften / hold** TFI (a contra-direction firehose against your taker lean is a genuine warning — the same shape as MicroCVD's stall penalty).

This mirrors OFI-momentum (RISING/FALLING modifies the OFI level score) and funding-momentum (amplify/soften). It appears in scoring **once**, as a modifier on an existing vote, so it cannot double-count and cannot add a correlated parallel vote. Magnitudes are cfg-driven (§6) and firing-rate-matched (§5). The standalone-vote alternative is **dropped** unless §5 surprises us (§10.1).

---

## 5. The correlation gate + re-baseline (the ⚠ core)

This is where #5 differs from #4: #4's re-baseline was a threshold re-derivation on a signal already known to belong. #5 must **first prove it belongs**, then calibrate. Two stages.

### 5.1 Correlation gate (decides whether scoring happens at all)

**Timeline (clarified 2026-07-01):** this gate does **not** run on the current v47 collection. Aggressor velocity isn't logged until #5's *build* sub-version exists, so the gate needs its **own, later** collection: v47 ships → #5 build lands (adds the burst CSV columns) → collect multi-session *with those columns* → then run the gate. The trader's §10.2 sign-off accepts the proposed thresholds as the working rule; the **final scoring go/no-go is made when those numbers are in**.

On that post-build data:

1. **Rank correlation** of the per-run `burstRatio`/`lean` against `TFI value`, `MicroCVDMomentum`, and `VolumeRatio` (Spearman — the relationships aren't linear).
2. **Fire-overlap:** `P(TFI directional | BURST_* same side)` vs the base rate `P(TFI directional)`, and the converse. High overlap ⇒ redundant.
3. **Decision rule (§10.2, working):** if `|Spearman(lean, TFI)| > 0.7` **and** directional fire-overlap `> 80%`, aggressor velocity is **too correlated to earn an upgrade** → it stays **display-only** (strip + CSV) and #5 closes there, honestly. Below those bounds it carries independent information and proceeds to the scoring wire-in. Report the actual numbers to the trader either way — the gate is a measured decision, not a foregone one.

### 5.2 Per-session re-baseline (only if the gate clears)

Wiring the modifier changes TFI's **effective** vote rate (some fires upgraded, some contras softened). Apply the firing-rate-match discipline (v40/v41 / #4 precedent) so selectivity stays stable — and do it **per session × resolution** (§10.4 — confirmed; the 1-min NY tape and the 3-min Asia/London tape have different flow densities, so a flat threshold would over-fire one and under-fire the other):

- Set `burst_ratio_threshold` (and confirm the per-resolution `norm_window_sec`) **per session**, so `BURST_*` fires at a rate consistent with genuine impulse moments on *each* tape — mirroring the per-session `roc_magnitude_threshold` machinery from v40 (nullable per-session override on a shared default).
- Confirm the **net Microstructure fire rate** after the modifier is ~stable vs before, per bucket (the modifier re-distributes conviction, it doesn't inflate the count). Before/after table in the spec-back, v40/v41 format.
- **Window↔threshold coupling** (same caveat as #4 §5): `burst_ratio_threshold` is derived *for* the chosen `fast_window_sec`/`norm_window_sec`; a manual window change requires a threshold re-check. The tweaker re-tunes over rounds.

---

## 6. Config — new `indicators.aggressor_velocity` block

New block (parallel to `indicators.OFI`), with a shared **default** plus **per-session overrides** (mirrors the v40 per-session `roc_magnitude_threshold` pattern: a nullable per-session value on a shared fallback). All numbers are pre-calibration anchors set by §5:

```json
"aggressor_velocity": {
  "enabled": true,               // feature switch — OFF the tweaker surface
  "scoring_enabled": false,      // the ⚠ scoring gate — starts false (build sub-version); OFF surface
  "fast_window_sec": 5,          // burst horizon (tau_fast) — ON surface (shapes the signal)
  "direction_lean_floor": 0.2,   // min |lean| to assign a direction — ON surface
  "gross_floor_usd_per_sec": 50, // dead-tape guard on the norm — ON surface
  "upgrade_bonus": 1,            // modifier magnitude once scoring is on — ON surface
  "contra_penalty": 1,           // contra-burst soften magnitude — ON surface
  "default":  { "norm_window_sec": 120, "burst_ratio_threshold": 2.5 },  // OFF tweaker surface — hand-tuned (HC11)
  "sessions": {                  // per-session overrides (null → inherit default) — hand-tuned, OFF tweaker surface (HC11)
    "NY":     { "norm_window_sec": 60 },   // dense 1-min tape → shorter baseline
    "LONDON": { },                          // inherits 120 / 2.5 until §5 splits it
    "ASIA":   { }
  }
}
```

- **Tweaker surface — three tiers, grounded in the applier's actual reach + HARD CONSTRAINT 11:**
  - **On the surface (tweaker-reachable):** the flat top-level params — `upgrade_bonus`, `contra_penalty` (the scoring magnitudes), `fast_window_sec`, `direction_lean_floor`, `gross_floor_usd_per_sec`. Simple dotted paths the applier resolves exactly like `indicators.ofi.avg_window_sec` (`SettingsDiffApplier` `Split(".")`s the path and overwrites the leaf). **These include the knobs that move the score** — once scoring is on, the tweaker can optimise the modifier strength on failure-rate feedback.
  - **Off the surface, hand-tuned:** the **per-session** `norm_window_sec` / `burst_ratio_threshold`. Two reasons, either sufficient: they're array-nested (`SettingsDiffApplier` can't resolve `sessions[].` paths — it rejects unresolved paths), **and** by established policy a per-session re-baseline override is trader-set, not a failure-rate lever — HARD CONSTRAINT 11 already excludes the exact precedent (`session_volume.sessions[].roc_magnitude_threshold`) as "re-baselined manually." Exposed in `settings.json` for the §5 per-session calibration; a new HC-11-style `PromptBuilder` line names the `aggressor_velocity.sessions[].*` keys. (The unbuilt Phase-2b per-population autotuning layer is what would ever tune these — not the current tweaker.)
  - **Off the surface, hand-toggle only:** the feature switches `enabled` + `scoring_enabled` — exact-match rejects in `SettingsDiffApplier` + a HARD CONSTRAINT line in `PromptBuilder`, mirroring `OFI.averaging_enabled` (HARD CONSTRAINT 16).
  No hardcoded magic numbers (profile §6) — everything is in `settings.json`; the tiers are about *who* changes each key, not whether it's exposed.
- Bump version + `change_log` + §15 at the build; the scoring wire-in is a later bump with the calibrated per-session values.

---

## 7. Display-parity

- **Build sub-version:** the **LIVE microstructure strip** (#3) already shows "tape speed" — enrich its tape-speed field to the directional `burstRatio` + `BURST_*` state (display-only, always-on, not posState-gated — consistent with the strip's #3 discipline). Any new/renamed rendered line on the strip → update the corresponding card binding + `BuildPlaintextSnapshot` in the **same commit** (hard parity rule).
- **CSV (fold at build — §10.5):** add `AggrVelBurstRatio`, `AggrVelNet` (net USD/sec), `AggrVelSignal` columns directly to `analysis_log.csv` at the #5 build. **Schema change ⇒ log rotation** (`AnalysisLogger.EnsureLogFile` rotates on header mismatch) — acceptable and clean because the aggressor columns are null pre-build anyway, and this lands **after** the v47 collection completes so it doesn't reset the in-flight geometric-OFI corpus (§12). The side-channel-CSV alternative is **dropped** (§10.5).
- **Scoring sub-version:** the TFI **SIGNAL BREAKDOWN row** gains a note (e.g. `TFI ... | BURST_BUY↑`) — new rendered content ⇒ card binding + snapshot updated in the same commit.

---

## 8. Edge cases & safety

- **transport=rest / REST-fallback run →** no live trade stream to time-weight → aggressor velocity is **unavailable**; emit `NORMAL` / null CSV and apply **no modifier** (never blocks, never guesses). The WS majority carries the signal; fallback rows are a known heterogeneity (calibrate on the WS majority, as #4).
- **Cold feed / just-connected →** until the accumulator has ≥ `norm_window_sec` of coverage the **norm is unreliable** → suppress `BURST_*` (emit `NORMAL`) rather than divide by a half-filled baseline. Resets on reconnect; the suppression re-arms.
- **Thin/burst-of-one →** `gross_floor_usd_per_sec` stops a single print on a dead tape from reading as an infinite burst.
- **Non-directional firehose →** `direction_lean_floor` holds it at `NORMAL` (the §2.1 guard).
- **Reversibility:** `scoring_enabled=false` reverts to no-modifier (hot); `enabled=false` stops the accumulator entirely. Both hot-toggle rollbacks.

---

## 9. Acceptance

**Build sub-version:**
- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck.
- `enabled=false` byte-identical to the prior version (accumulator inert, no strip/CSV change) — prove via regression.
- Harness: the accumulator decay/rate math (a fed trade sequence → the expected time-weighted USD/sec; two-horizon burst ratio; cold-start suppression; reset-on-connect), and `ClassifyAggressorBurst` (threshold/lean/floor edges). Host-agnostic (no WinForms in the accumulator).
- **Zero scoring impact** — `scoring_enabled=false`; verdict/CSV-scoring bytes unchanged vs prior version except the new display/CSV columns.

**Scoring sub-version (data-gated, later):**
- The §5 correlation numbers reported + the gate decision recorded (proceed / display-only).
- If proceeding: firing-rate-match table (before/after Microstructure fire rate, **per session×resolution**) in the spec-back; TFI-modifier regression; `scoring_enabled` toggles the modifier cleanly.

---

## 10. Decision log — RESOLVED (trader sign-off 2026-07-01)

1. **Integration shape** — ✅ **Modifier on TFI** (upgrade same-side burst, soften contra; §4.5). Standalone-vote fallback dropped unless §5 surprises us.
2. **Correlation gate** — ✅ Proposed thresholds accepted as the **working rule** (`|Spearman(lean,TFI)| > 0.7` **and** fire-overlap `> 80%` ⇒ display-only). **Final scoring go/no-go reserved for when the numbers are in** — and that is a *separate, later* collection after #5's build lands, **not** the current v47 collection (§5.1).
3. **Horizons** — ✅ `fast_window_sec = 5` (up from 3 — less single-print noise); norm **per-resolution** (~60s NY×1 / ~120s 3-min) rather than flat 90. Decoupled from hold duration (§3 clarification).
4. **Per-session thresholds** — ✅ **Yes, per-session** (+ per-resolution norm). 1-min vs 3-min tape dynamics differ; mirror the v40 per-session ROC override machinery (§5.2, §6) — **including its hand-tuned status**: per-session re-baseline overrides are trader-set, not auto-tuned (HC11). The flat scoring magnitudes (`upgrade_bonus`/`contra_penalty`) stay tweaker-reachable so the modifier strength IS auto-tunable once scoring is on (§6).
5. **CSV path** — ✅ **Fold columns into `analysis_log.csv` at the build** (rotates the log, clean post-v47). Side-channel option dropped (§7).
6. **Build/scoring split** — ✅ **Two sub-versions** (display/CSV build → collect + correlation → data-gated scoring), mirroring #4.

---

## 11. Implementation map (files)

- **New host-agnostic `AggressorVelocityAccumulator.vb`** (root/`Core/`) — two-horizon time-decayed buy/sell USD sums; `Fold(amount, direction, tsMs, tauFast, tauNorm)`, `Reset()`, `Snapshot(...)` → `{grossFast, grossNorm, burstRatio, netFast, lean}` + warmup/coverage gate. Mirrors `OfiAccumulator`.
- **`Core/Indicators_OrderFlow.vb`** — a pure `ClassifyAggressorBurst(burstRatio, lean, cfg)` → `BURST_BUY/SELL/NORMAL` (the tested classifier; parallels `ClassifyOfiRatio`).
- **`DeribitWsFeed.vb` / `MarketState.vb`** — fold each trade in `ApplyTrades` into the accumulator (analogue of `FoldOfiAverage` in `ApplyBook`); reset in `SeedAsync`; expose the snapshot via `MarketState` under its lock. The per-session `norm_window_sec` is resolved at read time (the run knows its session), so the feed can fold both horizons or the run can request the session's norm — settle in the build.
- **`UI/MainForm_Analysis.vb`** — read the burst snapshot on the WS path into new `IndicatorResults` fields; REST-fallback → `NORMAL`/null.
- **`LiveMicrostructureEvaluator.vb`** — enrich the tape-speed field to the directional burst state (#3 strip).
- **`Core/IndicatorResults.vb`** — new fields `AggrVelBurstRatio`, `AggrVelNet`, `AggrVelSignal`.
- **`AnalysisLogger.vb`** — three new CSV columns (schema bump / rotation — §7, fold at build).
- **`Core/Settings/EngineSettings.vb` + `settings.json`** — the `indicators.aggressor_velocity` block (default + per-session overrides); version bump + change_log + §15.
- **`tools/AutoTweaker/`** — exact-match reject `aggressor_velocity.enabled` + `aggressor_velocity.scoring_enabled`, **plus** an HC-11-style `PromptBuilder` line excluding the per-session `aggressor_velocity.sessions[].*` (`norm_window_sec`/`burst_ratio_threshold`) as manually-re-baselined (same class as the v40 per-session ROC key). Keep the **flat** params (`upgrade_bonus`, `contra_penalty`, `fast_window_sec`, `direction_lean_floor`, `gross_floor_usd_per_sec`) tweaker-reachable (dotted-path, like `ofi.avg_window_sec`) — the score magnitudes are here.
- **`verify/ordercheck/`** — accumulator math + `ClassifyAggressorBurst` fixtures + the `enabled=false` byte-identical regression.
- **(Scoring sub-version)** — `Core/ScoringEngine_Calculate_Scoring.vb` TFI-modifier wire-in; per-session correlation + firing-rate-match spec-back.

---

## 12. Sequencing / out of scope

- **Sequenced after the v47 OFI re-baseline.** v47 is the immediate work and is mid-collection on the live geometric-OFI corpus; #5's build (which rotates the CSV) must not reset that. Land #5's build once v47's re-baseline is measured and shipped — then #5's own multi-session collection begins (the §5.1 correlation-gate data).
- **#6 book absorption** (resting-size depletion at the active swing level without price progress) is the remaining ⚠ upgrade, its own spec — and it's the natural companion to #5: velocity says *how hard the tape is hitting*, absorption says *whether the level is soaking it up*. Together they're the breakout-quality vs fakeout pair. Specced separately.
- **Not** a change to TFI/MicroCVD/CVD mechanism — aggressor velocity is a new, orthogonal measurement that (at most) modifies TFI's vote. The existing indicators are untouched.
