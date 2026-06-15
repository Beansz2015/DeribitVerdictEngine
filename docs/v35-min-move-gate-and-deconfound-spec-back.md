# v35 — Min-Tradeable-Move Gate + Eval-Metric De-Confound (Spec-Back)

**Date:** 2026-06-14
**Implementer:** Opus 4.8 (fresh conversation)
**Status:** **ACCEPTED — trader tested 2026-06-15; gate + de-confound confirmed working** (local-unpushed: `3705d92` gate + `db050a5` eval/this doc; awaiting trader push). `dotnet build` clean (solution + AutoTweaker, 0/0); `verify/ordercheck` A1–A13 **ALL PASS**. The live test surfaced the 1-min Asia/London tradeability finding now addressed by the **v36 session-timeframe feature** — see §7 + [`session-timeframe-resolution-proposal.md`](session-timeframe-resolution-proposal.md).
**Specs:** [`min-tradeable-move-gate-proposal.md`](min-tradeable-move-gate-proposal.md) (scoring gate + shared key + UI), [`eval-metric-deconfound-proposal.md`](eval-metric-deconfound-proposal.md) (eval-layer fix + history re-evaluation, incl. the two 2026-06-14 notes).
**Settings:** v34 → **v35**, one bump, one new key `scoring.min_tradeable_move_pct = 0.0008`.

---

## 0. What shipped

The two proposals are implemented as one paired v35, sharing a single editable floor key. The gate stops the engine *emitting* sub-tradeable-target trades; the de-confound re-bases *history* so the metric the trader and the auto-tweaker read stops rewarding sub-tradeable chop. Both read the **same** `cfg.Scoring.MinTradeableMovePct`, so measurement and behaviour can never drift.

Honoured the two 2026-06-14 notes on the de-confound doc:
- **(a) Amendment** — the floor is the shared key `cfg.Scoring.MinTradeableMovePct` (0.0008), not an `AnalysisConstants` const. The const `FavBarAbsFloorPct` is retained **only** as the POCO-default mirror for host-agnostic callers without a `cfg`; every real call site passes the live key.
- **(b) Design refinement (governs the eval mechanism)** — gate-killed historical trades are **EXCLUDED** (new `EXCLUDED_BELOW_MIN_MOVE` outcome, out of the success/fail counts), **not** re-scored as failures. Survivors are still scored with the barrier floor. The re-eval applies the gate condition retroactively (`AtrTargetMultiplier × ATR < MinTradeableMovePct × Price`).

---

## 1. The shared key (settings v35)

`scoring.min_tradeable_move_pct: 0.0008` — 0.08%, price-relative (≈ $50 at $62k, sized to clear slippage; tracks BTC with no recalibration).

- POCO: `ScoringSettings.MinTradeableMovePct As Double = 0.0008` ([Core/Settings/EngineSettings.vb](../Core/Settings/EngineSettings.vb)), full XML doc.
- Hot-reloadable via the existing `FileSystemWatcher` — editing `settings.json` is live immediately. The trader can edit the file directly (the minimal editable path the proposal required: editable, validated by the `0 < x ≤ 0.01` semantics, persisted, %-labelled). A dedicated settings-dialog numeric control was **not** added in this pass — the file edit is hot-reloaded and the harness proves the key drives the gate (A13d). If the trader wants the dialog control, it's a thin follow-on on the `OutputDumpSettingsForm` pattern (off the card grid, per the proposal).
- **Off the auto-tweaker surface:** new HARD CONSTRAINT 11 in [PromptBuilder.vb](../tools/AutoTweaker/PromptBuilder.vb) tells the optimiser never to propose changing `scoring.min_tradeable_move_pct` or any `kelly.*` key (same mechanism as constraints 5/6 that protect the MTF / Pass-2c gates). Necessary because the prompt inlines the entire `settings.json`, so a new `scoring.*` key is otherwise reachable.
- `version` 34 → 35, `change_log` entry (newest-first), §15 row + §6 pointer in [DeribitIndicatorProject.md](DeribitIndicatorProject.md).

---

## 2. The scoring gate (behaviour change)

Implemented as **Step 5c** at the end of `ScoringEngine.Calculate()`, after Step 5b (so the capped targets exist) — [Core/ScoringEngine_Calculate_Verdict.vb](../Core/ScoringEngine_Calculate_Verdict.vb):

```
If directional verdict on the dominant side:
    effTarget = adjustedTarget(side) > 0 ? adjustedTarget(side) : CurrentPrice ± ATR×AtrTargetMultiplier
    floor     = cfg.Scoring.MinTradeableMovePct × CurrentPrice
    If |effTarget − CurrentPrice| < floor → Verdict = NO TRADE, VerdictContext = BELOW_MIN_MOVE
```

- Uses the **effective (post-cap) target** — catches both low-ATR (small ATR target) and near-swing (the Step 5b cap pulled TP below the floor) causes (chose form **A** from the proposal, not the ATR-only form B).
- Scores, breakdown, and the computed ATR/structural levels are **preserved for display** — only the verdict flips. Mirrors the MTF veto pattern (compute everything, then override, then return).
- Surfaced via `VerdictContext = "BELOW_MIN_MOVE"` — **no new CSV column** (`VerdictContext` is already logged; the new value rides the existing column).

### Surfacing & parity (CLAUDE.md hard rule)

`BELOW_MIN_MOVE` renders consistently on **both** display surfaces in the same commit:
- **Card:** new `ContextBadge.ContextKind.BELOW_MIN_MOVE` (⚠ glyph — proven-rendering in Geist Mono; `ACC_AMBER_DEEP` to distinguish it from the three amber warn-tags) + the `ParseContextKind` case in [MainForm_Render_Cards.vb](../UI/MainForm_Render_Cards.vb).
- **Plaintext snapshot:** `BuildPlaintextSnapshot` already emits `CONTEXT: <VerdictContext>` verbatim, so the tag appears with no snapshot edit — both surfaces read `v.VerdictContext`.
- **Calibration distribution:** added `BELOW_MIN_MOVE` to the `contextCounts` dictionary in [MainForm_Calibration.vb](../UI/MainForm_Calibration.vb) — same `ContainsKey` drop-trap the v30 `ALIGNED` fix patched; without it, gate-suppressed rows would silently vanish from the CONTEXT DISTRIBUTION.

### Behaviour-change honesty

At ~$62k the floor is **$49.6**; the gate's effective-target test fires when `ATR×2.0 < 49.6`, i.e. **ATR < ~24.8**. Asia (ATR ~13) goes mostly NO TRADE; chunks of low-ATR NY too. That is the intent ("trade like you do"). Consequences are real and accepted: slower low-vol data accumulation, a cleaner calibration book, verdict distribution shifting toward NO TRADE.

### A13 acceptance fixtures (all PASS)

Added to [verify/ordercheck/Program.vb](../verify/ordercheck/Program.vb), driving the **real** `Calculate()` via the A8 RANGE_BOUND cascade (SHORT-dominant) re-anchored to a $62k entry. Floor at the default key = `0.0008 × 62000 = 49.6`.

| Fixture | Setup | Expected | Result |
|---|---|---|---|
| **A13a** low-ATR veto | ATR 13, price 62000, no cap → raw short target 26 | NO TRADE / `BELOW_MIN_MOVE` (26 < 49.6) | **PASS** |
| **A13b** tradeable ATR | ATR 30 → raw short target 60 | directional SHORT stands (60 > 49.6) | **PASS** |
| **A13c** near-swing cap | ATR 100 (raw target 200 *would* pass) but a swing cap pulls TP to 30 pts from entry | NO TRADE / `BELOW_MIN_MOVE` + `AdjustedShortTarget = 61970` (validates the **effective**-target choice A) | **PASS** |
| **A13d** editability | A13a inputs, key lowered to `0.0004` → floor 24.8 | directional SHORT stands (24.8 < 26) — the shared key drives the gate; hot-reload path proven | **PASS** |

Side-fix: A8's `BuildA8Indicators` never set `r.ATR` (defaulted 0 → effective target distance 0 → tripped the new gate). Set `r.ATR = 50` (raw target 100 > the $80 floor at price 100000) so A8 stays isolated to the cascade it tests — exactly as it already disables MTF/Pass-2b/2c. A12 already set ATR 50, so it was unaffected.

---

## 3. The eval-metric de-confound (analysis layer — zero scoring votes/thresholds/vetoes change)

### 3.1 The floor + EXCLUDE (matrix — the auto-tweaker's metric)

[analysis/FailureRateMatrix.vb](../analysis/FailureRateMatrix.vb) `Compute` gains `belowMinMoveExcluded` (ByRef) + optional `floorPct` / `engineTargetMult` (default to the `AnalysisConstants` mirrors; call sites pass the live `cfg` values):

- **EXCLUDE (gate mirror):** a row whose engine target `AtrTargetMultiplier × ATR < floorPct × entry` is removed from the denominator (`belowMinMoveExcluded += 1`, `Continue For`) — a trade the live gate would NO-TRADE, **not** a failure. The dominant low-ATR case; near-swing-cap exclusions are approximated (the CSV lacks the adjusted target value), exactly as the proposal sanctions.
- **Floor (§1):** for surviving rows, each per-cell favourable barrier is `max(thr × ATR, floorPct × entry)`, so a "success" always means a tradeable move. Sub-floor `k×ATR` cells collapse onto the floored barrier → the matrix differentiates on the window dimension (correct: sub-floor thresholds were never tradeable).

Call sites pass the live floor:
- [analysis/AnalysisRunner.vb](../analysis/AnalysisRunner.vb) — `cfg.Scoring.MinTradeableMovePct` / `.AtrTargetMultiplier`; the count is surfaced as `report.BelowMinMoveExcluded` and rendered in the analysis report's Summary ([MarkdownReportWriter.vb](../analysis/MarkdownReportWriter.vb)) — no silent cap.
- [tools/AutoTweaker/AutoTweakerCore.vb](../tools/AutoTweaker/AutoTweakerCore.vb) — reads the live floor from the `EngineSettings` it already deserialises (falls back to the const mirror if absent); logs the excluded count per run. The tweaker now optimises only the book the engine will actually trade.

### 3.2 EXCLUDE + re-eval (eval cache / perf strip — schema v2 → v3)

[LivePerformanceTracker.vb](../LivePerformanceTracker.vb). The eval cache stores `EntryPrice` + `FavBar` (the favourable barrier = the engine's `2.0×ATR` target, or the capped target), so the EXCLUDE condition reduces to **`|FavBar − EntryPrice| < floor`** — no ATR join needed, and it matches the gate's effective-target condition exactly (for backfilled rows `FavBar = 2.0×ATR`, so it equals `AtrTargetMultiplier×ATR < floor`).

- **`BuildEntry`** gains a `minMovePct` param: a directional entry whose `|FavBar − EntryPrice| < floor` is born `EXCLUDED_BELOW_MIN_MOVE` (backstop; post-gate the engine already NO-TRADEs these, so the verdict arrives non-directional → `EXCLUDED_NO_PREDICTION`).
- **Schema v2 → v3:** the comment line carries `floor_pct=<value>` (the floor the cache was computed with). On load (`InitialiseAsync` Step 2.6, after the existing v1→v2 migration):
  - **pre-v3 file** → one-time re-evaluation **with the forensic count**.
  - **stored floor ≠ live floor** → self-healing re-walk (no forensic).
- **`ReevaluateForFloor`:** for every matured directional row — `|FavBar − EntryPrice| < floor` → `EXCLUDED_BELOW_MIN_MOVE` (out of the counts; survivors that were excluded at a *higher* floor are re-walked against OHLC and **recovered** when the floor is lowered). `EXCLUDED_BELOW_MIN_MOVE` is not in `BuildAggregate`'s SUCCESS/FAIL `Select Case`, so excluded rows leave the numerator **and** denominator (counted only in the tooltip `TotalRange`, like other EXCLUDED rows).

### 3.3 Re-evaluation before/after (Asia-ATR bucket)

The eval cache, OHLC cache, and `analysis_log.csv` are **runtime sidecar files in the EXE directory** (gitignored, and reset on 2026-06-11) — they are not in the source tree, so the integers below populate on the trader's **first post-v35 launch** (the migration logs the forensic line to console; the analysis report and perf strip reflect the re-based book). The *shape* is fully determined by the mechanism and the documented confound figure (`clean-data-rebaseline-v34-brief.md`: Asia <12 ATR = **86.8%** "success" on ~6–10 pt barriers). At $62k the floor is $49.6; the EXCLUDE threshold is `2.0×ATR < 49.6` ⇒ **ATR < 24.8**.

| ATR bucket (at ~$62k) | Before (old confounded barrier) | After (v35 de-confound) |
|---|---|---|
| **Asia < 12 ATR** | "success" **86.8%** — the favourable barrier (`0.5–0.8×ATR` ≈ 6–10 pt, or perf-strip `2.0×ATR` ≈ 24 pt) is trivially wicked through; the metric mostly measured sub-tradeable noise | **mostly `EXCLUDED_BELOW_MIN_MOVE`** (`2.0×ATR` = ~24 pt < $49.6 floor) → out of the counts; cell n → ~0 |
| **Low-ATR NY (ATR < ~25)** | counted, inflated for the same reason | **EXCLUDED** |
| **Tradeable NY (ATR ≥ ~25, typ. ~68)** | counted | **retained**; survivors scored with the floored barrier (`0.5/0.3×ATR` cells floored to $49.6; `0.8×ATR` clears once ATR ≥ 62) — largely unchanged where the floor doesn't bind |
| **Evaluated population** | all directional rows (ATR ~13…68+) | **tradeable-ATR only** (ATR ≳ 25 at $62k) |
| **Forward-comparable baseline** | — (confounded by ATR) | the **survivors' success rate** — the rate the gated engine will actually produce going forward, no longer inflated by sub-tradeable wicks. Within-session monotonicity should flatten/flip: success stops *rising* as ATR falls. |

Pass signal (proposal §5), to confirm on the live run: Asia <12 ATR drops sharply from 86.8% (most rows now EXCLUDED, not "successful"); high-ATR NY roughly unchanged; the evaluated population is tradeable-ATR only; re-evaluation completes over the matured entries without fabricated outcomes (missing-OHLC rows stay `WINDOW_EXPIRED`/`Nothing`, never invented).

### 3.4 One-time forensic count

On the first v2→v3 migration, `ReevaluateForFloor(logForensic:=True)` writes to console (and the value is also visible as the drop in evaluated-population size):

```
[LivePerformanceTracker] v2→v3 min-tradeable-move floor (0.080% of price): N directional
trade(s) EXCLUDED as below-min-move; K of those were SUCCESS under the old confounded
barrier (forensic — inflation now removed).
```

- **N** = how many historical directional trades the gate would have killed (the low-ATR population — expected to be most of Asia + low-ATR NY).
- **K** = how many of those were `SUCCESS` under the old barrier — the SUCCESS inflation the confound created (expected ≈ 86.8% of the Asia <12 ATR slice). Captured once; then it's gone from the live metric.

The matrix path logs its own per-run excluded count (`[AutoTweaker] … EXCLUDED below min-tradeable-move floor`), and the offline analysis report carries `Below-min-tradeable-move rows excluded` in §1 Summary.

> Provenance note: I did **not** run the migration — the data caches are runtime-only and were reset. The N/K integers and the exact Asia/NY percentages populate when the trader launches v35; the table above is the mechanism-derived shape anchored to the documented 86.8% figure.

---

## 4. Files changed

**Gate half (behaviour):**
- `Core/Settings/EngineSettings.vb` — `MinTradeableMovePct` POCO + XML doc.
- `settings.json` — key, version 34→35, change_log.
- `Core/ScoringEngine_Calculate_Verdict.vb` — Step 5c gate.
- `UI/Controls/ContextBadge.vb` — `BELOW_MIN_MOVE` enum + paint.
- `UI/MainForm_Render_Cards.vb` — `ParseContextKind` case.
- `UI/MainForm_Calibration.vb` — `contextCounts` distribution key.
- `tools/AutoTweaker/PromptBuilder.vb` — HARD CONSTRAINT 11 (off-surface).
- `verify/ordercheck/Program.vb` — A13a–d + A8 ATR fix.
- `docs/DeribitIndicatorProject.md` — §15 row + §6 pointer.

**Eval half (measurement):**
- `analysis/AnalysisConstants.vb` — `FavBarAbsFloorPct` / `EngineTargetAtrMultiplier` mirrors.
- `analysis/FailureRateMatrix.vb` — floor + EXCLUDE in `Compute`.
- `analysis/AnalysisRunner.vb`, `analysis/AnalysisReport.vb`, `analysis/MarkdownReportWriter.vb` — live floor passthrough + surfaced count.
- `tools/AutoTweaker/AutoTweakerCore.vb` — live floor passthrough + log.
- `LivePerformanceTracker.vb` — schema v3, `BuildEntry` backstop, `ReevaluateForFloor` + detection + wiring, forensic.

---

## 5. Acceptance checklist

- [x] `dotnet build DeribitVerdictEngine.sln` — clean (0/0).
- [x] `dotnet build tools/AutoTweaker/AutoTweaker.vbproj` — clean (0/0).
- [x] `dotnet run --project verify/ordercheck` — A1–A13 **ALL PASS** (A13a/b/c gate fixtures + A13d editability; A1–A12 unregressed).
- [x] Gate surfacing renders in card (`ContextBadge`) + plaintext snapshot (`CONTEXT:` line); calibration distribution counts the new value.
- [x] `min_tradeable_move_pct` off the auto-tweaker tunable surface (HARD CONSTRAINT 11); both `Compute` callers pass the live floor.
- [x] **Trader tested 2026-06-15** — gate confirmed working: Asia/London 1-min verdicts resolve NO TRADE / `BELOW_MIN_MOVE` and the eval EXCLUDES them (those perf-strip windows read `--%`), exactly as designed. The runtime forensic N/K line + the Asia-cell before/after table were **not separately captured** (post-reset sample is thin, and the gate now suppresses the low-ATR population so few eval rows accrue in those sessions) — re-confirmable from the console on any fresh launch once history accumulates. Not a blocker: the observed `--%` / "0 predictions" *is* the EXCLUDE behaviour working.

## 6. Sequencing

Both halves land **before** the supervised auto-tweaker first fire (the de-confound so the tweaker's metric is clean; the gate so the post-fire collection is already filtered). The v34-brief "must precede first fire" caution is satisfied. Local commits only — trader tests + pushes.

**Update 2026-06-15:** v35 tested + accepted (still local-unpushed). The next step is the supervised auto-tweaker **first fire on a NY weekday (1-min) window** — and per the v36 study NY stays 1-min, so the first fire is *unaffected* by v36 and can proceed independently. v36 adds the requirement that the tweaker never **pool** 3-min Asia/London rows once they exist (resolution-aware filtering) — that's a v36 precondition, not a v35 first-fire blocker.

## 7. Post-implementation outcome (trader test, 2026-06-15)

The pair works as specified — trader: *"the fixes work."* The notable observation, all **intended behaviour**:

- With the 0.08% floor, **~0 predictions evaluate in Asia/London** — 1-min `2×ATR` targets there can't clear the floor, so those verdicts become NO TRADE / `BELOW_MIN_MOVE` and the eval EXCLUDES them. The Asia/London perf-strip windows correctly read `--%`; the evaluated population is thin and NY-weighted. This is the gate telling the truth about 1-min low-vol sessions, not a regression.

- That left Asia/London without engine coverage, which **spawned the v36 session-timeframe feature**: move Asia/London *execution* to 3-min (where `2×ATR` clears the floor) and keep NY on 1-min. A 28-day ATR/price study confirmed the picks (**ASIA=3 / LONDON=3 / NY=1**) and showed the "0 predictions" was largely a **weekday/weekend artifact** — weekday Asia/London 1-min is already ~50–56% tradeable (median target sitting on the floor); weekend is the truly-dead case (~28–30%). Same weekday/weekend confound that bit the v34 ASIA `session_volume` change. Trader signed off 3/3/1 (weekend overlay out) on 2026-06-15. Docs: [`session-timeframe-resolution-proposal.md`](session-timeframe-resolution-proposal.md) + [`session-timeframe-resolution-spec-writer-brief.md`](session-timeframe-resolution-spec-writer-brief.md) (committed `b8a46eb`).

- **Net:** v35 is correct and stays as-is. v36 restores Asia/London coverage on the *appropriate timeframe* rather than weakening the floor — the floor and the timeframe are the two halves of "a tradeable move," and v35 proved the floor half by making the timeframe gap visible.
