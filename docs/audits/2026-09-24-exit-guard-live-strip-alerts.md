# Audit — exit guard, live TAPE strip, alerts, level absorption (2026-09-24)

**Posture:** hostile senior-quant audit, display/alert modules the trader exits on. No engine code changed.
**Audited at:** commit `6e74181`, tracked `settings.json` v68.
**Scope (six files):** `ExitGuardEvaluator.vb` · `UI/MainForm_ExitGuard.vb` · `LiveMicrostructureEvaluator.vb` · `UI/MainForm_LiveStrip.vb` · `Core/AlertsTracker.vb` · `Core/LevelAbsorptionTracker.vb`.
**Files not covered:** none. All six were read in full. `MarketState.vb`, `DeribitWsFeed.vb`, `ScoringEngine_Helpers.vb`, `Core/Indicators_OrderFlow.vb`, `Core/Indicators_Structure.vb` and `Core/AggressorVelocityAccumulator.vb` were read where the six files depend on them.
**Proofs:** [`proofs/exit-guard-live-strip-alerts/`](proofs/exit-guard-live-strip-alerts/) — harness source, the exact run command, and its verbatim output.

---

--- LOGIC TRACE ---

**How this was run.** I installed the .NET 8 SDK in the container and built an isolated console project in the scratchpad, not in the repo. It links the real `MarketState`, `ExitGuardEvaluator`, `LiveMicrostructureEvaluator`, `AlertsTracker`, `LevelAbsorptionTracker`, `IndicatorEngine.*` and `ScoringEngine.ComputeFastExitPrimitives`, and loads the tracked v68 `settings.json`. The WinForms debounce/latch block (`MainForm_ExitGuard.vb:118-132`) can't be linked on Linux, so I copied it line for line. The tape, the book and the $60 1m ATR are synthetic. The carried 5m swing is recomputed by the real `CalcSwingPivots` at every 1m close.

**Setup.** A long is declared at 100,000. The full run at T−30 s carried a 5m swing low (SL5) of 99,700, a 5m swing high (SH5) of 100,400, HVNs at 100,150 / 99,550 and 15m swings at 100,900 / 99,100. The 99,700 pivot sits at bar Count−4, and it counts as confirmed only because the forming bar's low is still above it. The flush runs 100,000 → 98,500 in 40 s, with prints reaching 98,183. Every print carries `liquidation="none"`, because WS never sends the flag. A 75 s bounce to 99,300 follows. The trace below is the clean seed, which is the guard's best case.

```
t(s)   price            M O T C B   TFI / Micro / CVD window   guard strip
−2     99,966           . . T . .   5.3s / 8.2s / 83s          clear (single adverse → Clear, D3)
+1     99,913           . O T C .   0.11s/ 0.41s/ 69s          ⚠ EXIT? confirming 1/2
+4     99,754 (−0.25%)  . O T C .   0.20s/ 0.40s/ 18s          ⚠ EXIT — 3 adverse  → LATCH, Exclamation plays once
+7     99,616           . O T C B   0.20s/ 0.41s/ 5.1s         latched; break vs 99,700 now true
+10…28                  . O T C B   ~0.2s/ 0.4s / 5s           latched; MicroCVD never votes on the one-way leg
+30    FULL RUN: forming 5m low 98,536 invalidates the 99,700 pivot → LastSwingLow5m = 98,900
+37    98,183 (−1.82%)  bottom prints
+40    98,492           . . T C B                              latched
+43…79                  . . . . B                              flow arm off; held only by the break vs the REPAINTED 98,900
+82    98,943           . . . . .                              "⚠ EXIT — clear"  (latched, current eval clear)
+85    98,988 (−1.01%)                                         "EXIT GUARD · clear" — 700 below the original stop level
```

The TAPE strip at 2 s ticks, same run:

```
+0.5   245k/s   BURST_SELL 5.4×   [SL 99700 | HVN↑ 100150]
+6.5   2.8M/s   BURST_SELL 6.5×   [HVN↓ 99550 | SL 99700]   broken stop shown as an ordinary level ABOVE price
+8.5                               NEAR↓ 99550               caught
+10.5…28.5                         [-- | HVN↓ 99550]         99,700 is on no surface any more
+14.5  4.26M/s  NORMAL 4.4×                                  heaviest leg (~95× pre-flush tape) reads NORMAL
+18.5                              NEAR↑ 99100               caught after the cross; a 15m level, so it renders "L↑"
+30.5                              [-- | SL 98900]
+46.5  TFI BUY PRESSURE from here on
```

**Cascade alarm.** No liq-flagged print ever reaches `AlertsTracker`, so `_liq` stays empty. There's no LIQ tag, no event, no flash and no sound (`alerts.sound_enabled` is false anyway). Every strip tick still runs `File.Exists` on the sidecar under the MarketState lock, because `_firstSeenWritten` never flips.

**Level-approach alerts.** Approach episodes existed for 6.6 s of the run; the strip showed NEAR on two ticks. The cross of 99,700 at about +6 s fell between samples. Approach never emits an event, so there is no flash or sound path for it at all.

**Absorption.** No ABS tag appeared, because episodes live for fractions of a second in a flush. U7 below shows what it outputs when it does fire.

**Monte Carlo, 500 seeds each, clean and choppy flush.** The choppy variant has 30% covering batches and 30% bid-refill frames.

- In 119 of 500 runs the guard was **already latched by pre-flush noise** when the flush started, so the one-shot alarm was already spent.
- Fresh latches came at a median of +4.0 s and −0.20% (worst −0.26%). Detection latency is not the problem.
- After the bottom, EXIT cleared at a median of **+82 s and −1.04%** with the swing arm on (via the repaint). With the swing arm off it cleared at **+46 s and −1.48%**, six seconds after the low.
- 31% (clean) and 49% (choppy) of the 2-of-4 EXIT ticks had no book input.
- MicroCVD voted adverse on 1.8% of clean-flush ticks.
- Approach state existed for about 2.9 s per run; the strip showed it 0.37 (clean) and 0.08 (choppy) times per run.

**Control, no flush.** Balanced synthetic tape, 40 seeds × 30 min, swing low 1% away. The guard produced **45.1 false EXIT latches per hour** at size σ 0.9 and **55.4 per hour** at σ 1.5, and was latched 19–24% of the time. Per-tick adverse rates were TFI 29–37%, CVD 28%, MicroCVD 17–23%, OFI 8%.

---

### 1. Flow arm fires on noise at the 3 s cadence
**SEVERITY:** CRITICAL. The tape is synthetic; the mechanism is not.
**LOCATION:** `ExitGuardEvaluator.vb:128-130` (2+ adverse → Exit); `MainForm_ExitGuard.vb:118-125` (2 consecutive ticks → latch and sound). Settings v68: TFI 30 trades / 0.15, CVD slope floor `max(12k, 10%·|CVD|)`, `exit_guard.sound_enabled = true`.
**DOWNSTREAM IMPACT:** About one Exclamation every 65–80 s while any position is declared. A quarter of real flushes arrive with the latch already set, so the flush itself plays no sound. The trader learns to ignore the only audible exit cue.
**FAILURE SCENARIO:** The control run above: 45–55 latches per hour on a tape with no edge.
**ANALYTICAL CRITIQUE:** The 2-of-4 bar came from a once-per-run hold read (every 1–3 min) and is now sampled every 3 s. That is the same per-evaluation false-positive rate at 20–60× the evaluations. The two-tick "debounce" is two correlated draws: CVD's 500-trade window barely changes in 3 s. The documented fallback, aligning OFI to the averaged ratio (`audit-fixes-2026-07-02-proposal.md` §4), targets the wrong input: OFI is adverse on 8% of noise ticks, and TFI plus CVD drive the latches. The watch meant to catch this fatigue has never been read (`trader-tick-queue.md`: "state unverified since v47"). The decisive check is to point this harness's guard loop at the collector's trade store and count latches on real tape.

### 2. The structural break erases itself (pivot repaint)
**SEVERITY:** CRITICAL
**LOCATION:** `Core/Indicators_Structure.vb:287`, where `scanEnd = Count−1−pivotWing` makes the forming bar a right-wing bar. Consumed at `ScoringEngine_Helpers.vb:150` and carried via `MainForm_ExitGuard.vb:110-111`. The same carry feeds alerts and absorption (`MainForm_Analysis.vb:707-717`) and the strip bracket.
**DOWNSTREAM IMPACT:** When the flush trades through the latest swing low (the level the long's stop sits under), the next full run finds a lower low in that pivot's right wing and discards it. The carried level jumps to an older pivot (99,700 → 98,900). Every surface drops 99,700, and the guard clears when price reclaims an unrelated level, at −1.0%.
**FAILURE SCENARIO:** Trace at +30 s. Unit U6 in isolation: the pivot is 99,350 while the forming bar's low is 99,700. The forming bar then trades to 99,300, the carried swing low becomes 99,200, and `99,310 <= SL` is False.
**ANALYTICAL CRITIQUE:** A pivot confirmed by an unfinished bar is not confirmed. The event that should confirm the break is the event that deletes the level. The shared primitive also feeds `CalcHoldStatus` Layer 1.5, so the HOLD\EXIT row has the same hole. It needs closed-bar right wings, or a stop level frozen when the position is declared.

### 3. The "latch" forgets and clears at maximum drawdown
**SEVERITY:** HIGH
**LOCATION:** `MainForm_ExitGuard.vb:126-131` (auto-clear), `:96-97` and `:103-106` (any "WS only" or paused state resets the latch), `:162-164` (renders the *current* `res.Reason`).
**DOWNSTREAM IMPACT:** EXIT lasts only as long as the condition plus 6 s. Flow flips at the bottom, so the strip reads "clear" at the worst price: −1.48% median without the swing arm, −1.04% with the repainted one. A 10 s trade lull (`ws_stale_after_sec`) or a feed blip also wipes it.
**FAILURE SCENARIO:** The trader hears one alarm at +4 s. A glance at +60 s shows EXIT; a glance at +90 s shows "clear" with the position at −0.9%. At +82 s the strip reads "⚠ EXIT — clear".
**ANALYTICAL CRITIQUE:** The guard keeps no memory of the event. For a 2–15 minute hold, the actionable fact is "the exit condition fired at 99,754 and you are now 1.2% lower", and it gets discarded. Auto-clear was chosen to avoid a persistent alarm, which is a symptom of #1, not a design reason.

### 4. The guard doesn't know the real position
**SEVERITY:** HIGH
**LOCATION:** `MainForm_ExitGuard.vb:85-92`, where the side comes from `rbLong`/`rbShort`. `Evaluate` takes no entry price. Nothing in `UI/` sets those radios in code.
**DOWNSTREAM IMPACT:** The order app enters automatically, so every entry the trader doesn't mirror by hand is unguarded. The unguarded state renders as a hidden strip (`:88-91`). Left armed after the app exits, the guard alarms on a flat account. In U2, a long opened at 99,650 under a carried swing low of 99,700 gets an immediate, permanent "structural break" EXIT.
**ANALYTICAL CRITIQUE:** It evaluates a hypothetical position. It can't tell "broke during the hold" from "entered below it". The full run filters levels above price through `SwingStopLong` (`MainForm_Analysis.vb:582`); the guard consumes raw `LastSwingLow5m`.

### 5. The absorption tag is manufactured by the approach itself
**SEVERITY:** HIGH
**LOCATION:** `LevelAbsorptionTracker.vb`:
- `:195`: pressing volume counts sells up to *level + band*, which is above the level.
- `:302-313`: `SizeStart` is taken from whatever slice of the band is visible at arming.
- `:356-357`: `SizeNow`/`SizeMin` are taken from a visible slice that grows as price approaches.
- `:445`: the depletion floor.
- `:276`: the episode closes on the touch cross with no `BrokenLevel` set.

**DOWNSTREAM IMPACT:** A dense top-10 ladder spans $4.5, so the arming distance is capped at the ladder. `SizeStart` sees 2 of the 13 band levels. `SizeMin` never drops below it, depletion floors at $5k, and the numerator is volume above the level. The strip shows "ABS↓ support defended" to a long right before the level goes. The CSV absorption columns, which are the evidence base for D-6 and for `scoring_enabled`, carry the same bias.
**FAILURE SCENARIO:** U7: $320k eats eight bid levels above 99,700 with zero refill. The tracker reports ABSORB_BELOW at 16×, rising to 64×, before one contract trades at the level. The tag then simply vanishes when the bid crosses.
**ANALYTICAL CRITIQUE:** D-6a rests on `SizeStart` capturing "the band's resting depth before price arrives" (`absorption-d6-spec-back.md:42`). A top-10 ladder can't do that for any ATR above about $15. The D8 visibility mask covers pulls and posts but not the depletion denominator. The 2026-09-01 live row with `SizeMin=0` is consistent with this, not proof of it.

### 6. "2 of 4" counts the same tape twice
**SEVERITY:** HIGH
**LOCATION:** `ExitGuardEvaluator.vb:80-90` and `:110-118`; `ScoringEngine_Helpers.vb:141-160`. The comment at `:181` claims "two independent microstructure signals".
**DOWNSTREAM IMPACT:** In the flush the windows were nested views of the same prints: TFI 0.1–0.3 s inside MicroCVD 0.4 s inside CVD 5 s. 31–49% of EXIT ticks had no independent input. "2 adverse (TFI SELL, CVD FALLING)" is one fact counted twice, and it's the pair behind #1.
**ANALYTICAL CRITIQUE:** `trader-profile.md` bans double-counting. It's tolerated here because the module is display-only, but this is the display the trader exits on. Only OFI (the book) and the break (price) are independent inputs.

### 7. MicroCVD abstains during the flush and votes at exhaustion
**SEVERITY:** HIGH
**LOCATION:** `CalcMicroCVD` (late third vs early third, ± 30% of window gross), consumed at `ScoringEngine_Helpers.vb:142`.
**FAILURE SCENARIO:** U1: a steady $2.5M one-way sell reads **FLAT, not adverse**. A fading sell-off (early −1.28M, late −0.36M) reads BEAR_DECEL, **adverse**. In the clean flush MicroCVD voted on 1.8% of ticks.
**ANALYTICAL CRITIQUE:** An acceleration detector is being counted as a pressure detector. BEAR_DECEL counting as adverse for a long means "sellers are running out" votes to sell, at the low.

### 8. The cascade alarm is dead on WS, and wrong if it's ever fed
**SEVERITY:** HIGH
**LOCATION:**
- `DeribitWsFeed.vb:517` collapses the liq flag to a bool.
- `AlertsTracker.vb:151` sets the threshold, and `:205-211` and `:244` infer the side.
- `AlertsTracker.vb:143` and `:157` append to the sidecar inside the lock taken at `MarketState.vb:309-311`.
- `AlertsTracker.vb:184` runs `File.Exists` on every strip tick.

**DOWNSTREAM IMPACT:** A missing LIQ tag reads as "no cascade" during a cascade. The A4 unlock (sidecar existence) can never be met from WS. If flags ever arrive, the first event does disk I/O inside the single MarketState lock, on the receive thread, at the worst moment.
**FAILURE SCENARIO:** U4: three $100 long-liquidation prints flagged "T" → CASCADE_BELOW, correct. The same prints flagged "M" (maker side liquidated, aggressor bought) → CASCADE_ABOVE, "shorts squeezed", **wrong side**. And $300 in total qualifies as a cascade, because the threshold counts prints and has no USD floor.
**ANALYTICAL CRITIQUE:** Deribit's `liquidation` field says *which side* was liquidated (M/T/MT). Inferring that from the aggressor is only right for "T". `CalcLiquidations` (`Indicators_OrderFlow.vb:259-273`) makes the same inference in the scoring path, so REST-seeded "M" trades mis-score too. That fix is a reserved scoring change; I'm only flagging it.

### 9. Level-approach alerts are invisible at flush speed
**SEVERITY:** HIGH
**LOCATION:**
- `AlertsTracker.vb:255-293`: a state flag, never an event.
- `AlertsTracker.vb:257`: fixed tolerance of 12 ticks = $6.
- `MainForm_LiveStrip.vb:128-138`: amber only if the state happens to be active at the 2 s sample.
- `AlertsTracker.vb:83-85` and `LevelAbsorptionTracker.vb:130-132`: carried levels are wiped on every reconnect.

**DOWNSTREAM IMPACT:** The approach state existed for about 2.9 s per run and was displayed 0.08–0.37 times. The header's "status-bar flash" (H1) applies only to CASCADE and FIRST_SEEN events. After a mid-flush reconnect the strip still shows SL 99,700 (it reads `_lastSuccessfulIndicators`), but NEAR and ABS can't fire against it until the next successful full run. 15m levels render as "L↑"/"L↓" because the strip's bracket (`LiveMicrostructureEvaluator.vb:252-256`) excludes them.
**ANALYTICAL CRITIQUE:** A proximity state sampled every 2 s on a $6 band is a coin flip for any move faster than $3/s. It needs an edge-triggered touch/cross event that persists until it's been shown. Fixed tick geometry is the defect v61 already migrated the absorption tracker off.

### 10. Mixed clock domains
**SEVERITY:** MEDIUM
**LOCATION:**
- `LiveMicrostructureEvaluator.vb:295-301`: tape speed compares local now against exchange-stamped trades.
- `AlertsTracker.vb:122` prunes on exchange time; `:182` prunes on local time.
- `LevelAbsorptionTracker.vb:185` and `:195` stamp pressing volume with exchange time, then prune it at `:358` on book receive time and at `:433` on the caller's clock.
- The health gates (`MainForm_ExitGuard.vb:143-144`, `MainForm_LiveStrip.vb:191-192`) read receive stamps.

**FAILURE SCENARIO:** U5: the true tape is 20 tr/s. With 2 s of receive lag or clock skew the strip reads 16.0; at 5 s it reads 10.0; at 11 s it reads 0.0. U5b: with the local clock 11 s ahead, the cascade tag reads NONE while its event still flashes.
**DOWNSTREAM IMPACT:** Receive backlog peaks in a flush, so tape speed and pressing volume are under-read exactly then. The gates measure "a frame was processed recently", not "the data is recent", so a backlogged loop passes as fresh.

### 11. Fail-open to "clear", and carried levels have no age
**SEVERITY:** MEDIUM
**LOCATION:** `ExitGuardEvaluator.vb:62` and `:144-147`; `MainForm_ExitGuard.vb:110-111` and `:169-171`.
**FAILURE SCENARIO:** U3: a thrown exception, and a 10-trade buffer with price *under* the swing low, both return Kind=Clear with Reason="", rendered as "EXIT GUARD · clear". That's identical to a real clear. Until the first successful full run, the break arm is silently disarmed. After hours of skipped runs, the guard tests against an hours-old pivot and still says "clear".
**ANALYTICAL CRITIQUE:** §7 chose "never a false EXIT" as the safety property. For an exit guard that's backwards: the dangerous failure is a false CLEAR. Unknown should render as unknown.

### 12. Both monitors live on the UI thread
**SEVERITY:** MEDIUM
**LOCATION:** `System.Windows.Forms.Timer` at `MainForm_ExitGuard.vb:38-42` and `MainForm_LiveStrip.vb:56-60`. `DeribitWsFeed.vb:207-208` documents a real UI-thread stall.
**DOWNSTREAM IMPACT:** WM_TIMER messages coalesce, so a stall drops ticks rather than queueing them. There's no "last evaluated" stamp, so a frozen strip keeps showing its last text, which is usually "clear".

### 13. The burst field hides the flush and freezes after it
**SEVERITY:** MEDIUM
**LOCATION:** `LiveMicrostructureEvaluator.vb:177-192`, reading `AggressorVelocityAccumulator.Snapshot`, which never decays to read time.
**FAILURE SCENARIO:** BURST_SELL at +0.5 s, 3.5 s before the guard latched. From +14.5 s it reads NORMAL while the tape runs at $4.3M/s, because the 60 s NY norm swallowed the flush. After a burst followed by silence, the stale BURST reading persists until the next print.
**ANALYTICAL CRITIQUE:** Self-normalised ratios flag onsets, and the strip presents that as a state. The one input that saw the onset first isn't wired into the guard.

### 14. A guard disabled at launch can never start
**SEVERITY:** LOW
**LOCATION:** `MainForm_ExitGuard.vb:34` returns before the timer exists. The hot-reload comment at `:73-74` is false in that case.

### 15. Alert delivery depends on the TAPE checkbox
**SEVERITY:** LOW
**LOCATION:** `MainForm_LiveStrip.vb:103-113` gates before `HandlePendingAlertEvents`.
**DOWNSTREAM IMPACT:** With TAPE unchecked or the feed stale, the sound and flash never play. Events pile up in `_pending` and replay stale when the strip comes back, and `Reset()` drops them on reconnect.

### 16. Contract and label lies
**SEVERITY:** LOW
- `MarketState.vb:6` says readers get copies, but `GetBook` (`:254-258`) returns the live snapshot, and the absorption tracker keeps it as `_prevBook`. That's safe only while nothing mutates a published book.
- `MarketState.vb:67` says the alerts tracker uses "the SAME candidate set" as the strip and absorption tracker; it adds 15m swings that neither of them uses.
- The strip says "WS only" when the WS feed is merely stale.
- A broken stop renders as an ordinary level above price, and "HVN↓" can render above price.
- `SystemSounds.Exclamation` is silent under a "No Sounds" Windows scheme and throws nothing to catch.

---

**Not verified:**
- The real-tape false-latch rate (#1). The harness can replay the collector's trade store to measure it.
- Receive-lag magnitudes on the box.
- That WS never delivers `liquidation`. I took that from your brief.
- The meaning of M/T/MT. That comes from Deribit's API docs, not from this repo.
- The trader's Windows sound scheme.

**Proof code and outputs:** [`proofs/exit-guard-live-strip-alerts/`](proofs/exit-guard-live-strip-alerts/) (`Program.vb.txt`, `FlushAudit.vbproj.txt`, and a `README.md` with the exact run command and the full verbatim output). The harness originally ran from the scratchpad with hardcoded container paths; the committed copy reads the repo root from `DVE_REPO`, and it was re-run from the committed files with identical results.
