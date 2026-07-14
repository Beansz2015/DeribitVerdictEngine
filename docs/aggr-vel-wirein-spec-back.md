# #5 Aggressor-Velocity Scoring Wire-in — Spec-back

**Date:** 2026-07-14 · **Settings:** v51 → **v52** · **Seat:** Opus (implementer, medium)
**Brief:** `aggr-vel-wirein-implementer-brief.md` · **Specs:** `aggressor-velocity-proposal.md` §4.5/§6, `aggressor-velocity-s52-derivation-2026-07-13.md`, gate verdict `aggressor-velocity-correlation-gate-verdict-2026-07-13.md`.
**Constraint compliance:** local-first, NOT pushed; Release-only builds; verify-gate `-Mode prepush` **GREEN**; serialized after the D6 lane (D6 already committed, no concurrent fixture edits). POCO defaults ride the commit.

This records what shipped and every deviation from the brief, per the collaboration rule.

---

## 1. What shipped (the five scope items)

1. **Settings (v52):** `indicators.aggressor_velocity.scoring_enabled` false→**true**; `sessions.NY.burst_ratio_threshold` added = **4.5** (S1). The shared `default` (2.5) is unchanged — res-3 display/collection untouched (S2a). Version bump + `change_log` dataset-boundary note (newest-first) + §15 row + §12 post-ship watch.
2. **Step-2 TFI modifier** (`Core/ScoringEngine_Calculate_Scoring.vb`) per §4.5, appears **once**, immediately after the TFI `AddFull`, before the `tfiLP`/`tfiSP` point-delta capture (so the TFI breakdown row's SC carries the ±1 — SC/TOTAL parity preserved).
3. **Breakdown note suffix** appended to the existing TFI note row (OFI `MOM:` precedent).
4. **S5 rider — HC22:** exact-match fence for `session_volume.enabled` in `SettingsDiffApplier.Validate` + PromptBuilder HARD CONSTRAINT 22 + fixtures.
5. **Post-ship watch** named in `change_log` + §12.

**Modifier semantics (as implemented):**
- `av.ScoringEnabled` AND TFI directional (`tfiLong Xor tfiShort`) AND `HasExplicitAggrVelBurstThreshold(cfg, r.SessionUtcHour)`:
  - TFI long + `BURST_BUY` → `LongScore = Min(LongScore + UpgradeBonus, regimeMax)`; note ` | BURST_BUY +1[L] confirmed`
  - TFI short + `BURST_SELL` → `Min(ShortScore + UpgradeBonus, regimeMax)`; note ` | BURST_SELL +1[S] confirmed`
  - TFI long + `BURST_SELL` (contra) → `Max(0, LongScore − ContraPenalty)`; note ` | BURST_SELL -1[L] contra`
  - TFI short + `BURST_BUY` (contra) → `Max(0, ShortScore − ContraPenalty)`; note ` | BURST_BUY -1[S] contra`
  - `NORMAL` / non-directional TFI → no-op, empty note.

**Files touched:** `settings.json`, `Core/Settings/EngineSettings.vb` (POCO defaults + doc), `Core/ExecutionResolution.vb` (new public helper), `Core/ScoringEngine_Calculate_Scoring.vb` (modifier + note), `tools/AutoTweaker/SettingsDiffApplier.vb` (HC22), `tools/AutoTweaker/PromptBuilder.vb` (HC22 line), `verify/ordercheck/Program.vb` (A28a–d new, A15g + A23f updated), `docs/DeribitIndicatorProject.md` (§12/§15), this spec-back.

---

## 2. Deviations & non-obvious decisions

**D1 — S2a scoping key = presence of the explicit per-session `burst_ratio_threshold` (no new flag).**
The brief says the modifier applies "only when the run's session has an explicit `sessions[s].burst_ratio_threshold` (NY today)." Implemented literally as `ExecutionResolution.HasExplicitAggrVelBurstThreshold(cfg, utcHour)` → `AggrVelSessionOverrideFor(...).BurstRatioThreshold.HasValue`. The explicit threshold value **is** the scoping signal — the same number that sets NY's firing selectivity also arms NY's scoring. Consequence (intended): when the res-3 §5.2 pass later sets `sessions.{LONDON,ASIA}.burst_ratio_threshold`, those sessions auto-arm with no further scoring-engine change. Session is resolved from `r.SessionUtcHour` (stamped once per run in `RunAnalysisAsync`; −1 default on harness/replay paths → no bucket → inert).

**D2 — POCO defaults flipped (ride the commit).**
`AggressorVelocitySettings.ScoringEnabled` default False→True and the NY code-default session override gains `.BurstRatioThreshold = 4.5`, so an absent `settings.json` seeds the shipped v52 behaviour (v15 "align defaults" principle; v50/v51 precedent). **Safety:** every pre-wire-in fixture stays byte-identical because `IndicatorResults.AggrVelSignal` defaults to `"NORMAL"` (no modifier branch fires) and `SessionUtcHour` defaults to −1 (gate closed). No existing fixture feeds a `BURST_*` signal into `Calculate()`; the A23-series test the accumulator/classifier directly, not through the pipeline.

**D3 — res-3 scoping fixture uses LONDON (hour 10), not ASIA (hour 3).**
Acceptance (c) is "res-3 session with no explicit threshold ⇒ inert." Both LONDON and ASIA are res-3 with no explicit burst threshold, but ASIA's `structural_levels` fallback-target multiplier is **1.25** → the placed fallback short target (50×1.25 = 62.5) sits **below** the min-move floor (80), which would trip the Step-5c gate to `NO TRADE / BELOW_MIN_MOVE` and confound the `EffectiveShortScore` assertion. LONDON's multiplier is **2.0** (target 100 > 80 → gate clears), so the SHORT verdict stands and `EffectiveShortScore` cleanly isolates the (inert) modifier. LONDON is an equally valid res-3 witness for S2a. (A28c.)

**D4 — HC22 fixture: A15g updated *and* a dedicated A28d added.**
A15g (`ValidatePassesNormalSessionVolumeKey`) previously asserted `session_volume.enabled` **passes** — it is literally the fixture whose 07-13 re-check exposed the unfenced switch (S5 rider). It could not stay green after HC22, so it is re-pointed to assert the HC22 rejection while keeping its original secondary proof (the array-nested `session_volume.sessions[].*` path is rejected as UNRESOLVED, **not** by the HC11 prefix guard — no over-match). Acceptance (e) additionally asks for a "new fixture," so **A28d** is the dedicated HC22 witness (reject `session_volume.enabled` with the HC22 reason **and** accept a sibling tunable `indicators.OBV.trend_gate`). The Sub name `A15g_ValidatePassesNormalSessionVolumeKey` is retained (the printed Check label is updated) to avoid churning the `Main()` registration + comment block.

**D5 — A23f re-pinned (NY threshold 2.5 → 4.5).**
`A23f_AggrVelSessionResolution` built `New EngineSettings()` and asserted NY's resolved burst threshold = the inherited default **2.5**. The POCO change (D2) gives NY an explicit **4.5**, so the assertion is updated to 4.5 (and the label notes NY now carries the wire-in value; LONDON/ASIA still inherit 2.5). This is a required re-pin, not a behaviour regression.

**D6 — contra-soften does NOT touch category membership.**
Unlike OFI-momentum's "suppressed (unwinding)" arm (which removes `Microstructure` from `FullXCategories` when OFI was the sole contributor), the contra-burst arm is a pure magnitude soften — `Max(0, score − ContraPenalty)` with no category edit. §4.5 calls for "soften / hold," not vote removal; score-reduction-without-category-change is the established penalty-site pattern (CVD divergence, liq, spread, MicroCVD decel all do exactly this). Keeping TFI's `Microstructure` membership intact also leaves `CalcVerdictContext` flow-score counting unchanged.

**D7 — TFI breakdown hit flags unchanged.**
The `SignalBreakdownItem("TFI", tfiLong, tfiShort, …)` hit flags stay `tfiLong`/`tfiShort` (the modifier changes magnitude, not which side "hit"). The card's TFI row (`BuildRowTfi`) derives its **SC** from that item's captured point-delta (so the ±1 reaches the card automatically) while rendering its own curated note (`r.TFIValue` F3) — it never renders the raw `Note`. Hence appending the burst suffix obligates **no** card change (exactly the OFI `MOM:` precedent; the snapshot FILE is untouched, so the verify-gate parity heuristic does not even trigger — no `[no-card-surface]` token needed).

---

## 3. Invariants respected (profile / CLAUDE.md)

- **No double-count:** the burst appears in scoring exactly once, as a modifier on TFI's existing vote — never a parallel Microstructure vote. The §5.1 gate cleared (NOT redundant, ρ=0.61) so it carries information TFI doesn't.
- **No non-directional payout:** the gross tape-speed component is a gate only; `BURST_*` requires both a burst **and** a direction (`|lean| ≥ floor`, enforced upstream in `ClassifyAggressorBurst`). A balanced firehose is `NORMAL` → no-op.
- **Cap discipline:** the upgrade caps at `regimeMax` at its site (`Math.Min`), like every other bonus site (OFI-momentum, Pass 2b/2c). Verified by A28b.
- **SC/TOTAL parity:** the modifier mutates `state.Long/ShortScore` **before** the `tfiLP/tfiSP` capture, so the TFI row's SC equals its true contribution incl. the ±1 and Σ(rows) still equals the final ls/ss.
- **Reversibility:** `scoring_enabled:false` ⇒ byte-identical no-modifier behaviour (hot rollback), proven by A28c.

---

## 4. Acceptance evidence

- Builds **0/0**: solution(Release) + AutoTweaker + OrderCheck.
- Harness **ALL PASS**: A1–A27d unregressed; A15g refreshed (HC22), A23f re-pinned (NY thr 4.5); new **A28a–d**:
  - **A28a** upgrade/soften/no-op through the real `Calculate()` — NORMAL ss=11, same-side +1 ss=12, contra −1 ss=10 (all SHORT).
  - **A28b** regimeMax cap holds — short 10 + upgrade 20 → 18 (not 30).
  - **A28c** S2a scoping (LONDON burst inert, ss=11) + `scoring_enabled:false` byte-identical (ss=11 == NORMAL).
  - **A28d** HC22 fence rejects `session_volume.enabled`, accepts `indicators.OBV.trend_gate`.
- `verify-gate.ps1 -Mode prepush` → **GATE PASSED** (harness ALL PASS, display-parity clean, version-bump OK).

---

## 4b. Display rider — ATR-levels card fix (2026-07-14 screenshots)

Folded in at the trader's request (unrelated to the scoring wire-in; card-only, zero engine/config/scoring impact). Card fixes to `UI/MainForm_Render_Cards.vb` (+ sub-label fields in `UI/MainForm_Layout.vb`):

- **CAPPED → PLACED caption.** v51 B4b relabeled the placed-target vocabulary `CAPPED @` → `PLACED @`, but the card's zone cell was still captioned `CAPPED`. Now the caption is `If(lv.StopReason IsNot Nothing, "PLACED", "CAPPED")` — **PLACED** on the structural-first path (the default, what the screenshots show) and **CAPPED** only on the legacy `structural_levels.enabled:false` rollback, mirroring the snapshot's `lv.Reason` wording. This is a **parity fix**: the snapshot already emitted `PLACED @` (`AppendPlacedAtrRow`), so the card was lagging — no snapshot change is needed (and the verify-gate parity heuristic only guards snapshot-changed-without-card, not the reverse).
- **STOP_CLAMPED cell wrap.** The placed-stop source label (`SWING_STOP` / `STOP_CLAMPED` / `FALLBACK_ATR`) was appended inline to the STOP price. In the clamped case the structural stop is always deeper, so the STOP price renders in the ~45%-wide sub-cell of the STRUCT|STOP split, where a 12-char label like `STOP_CLAMPED` wrapped/clipped. Fix: the STOP cell is now a **2-col × 2-row** sub-layout — row 0 holds STRUCT | STOP prices, row 1 holds the source label on its **own small-font line** (`StopReasonSub`, 7pt/6.5pt) **directly under the STOP value** (col 1, not spanned), so it can't wrap. Placing it under STOP — rather than centred across the pair — ties the label to the value it qualifies (STRUCT is the true un-clamped level; STOP is the clamped one), mirroring how TARGET shows `(FALLBACK_ATR)` under the target. Same sub-label pattern the R:R cell already uses (`RRSubValue`) to solve the identical 3-line-label clip.
  - *Vertical (2026-07-14 follow-up, per the STOP_CLAMPED screenshot):* the first pass centred the prices in the top band and the label in the bottom band, leaving a visible gap ("prices pushed up"). Fixed by bottom-aligning the price row and top-aligning the label so the STRUCT/STOP values sit just above the label as one group. With no label (legacy path) the row collapses to 100/0 and the price centres, unchanged from v50.
- **Cross-cell baseline unification (2026-07-14 follow-up 2, per the alignment screenshot).** The STOP and R:R cells were 2-row (value + sub-label) while ENTRY / PLACED / TARGET were single centred labels, so their values landed at different heights and the sub-labels sat at inconsistent gaps. Fixed structurally: **every** value cell now shares one 2-row scheme via `WrapAtrCell` — the value bottom-aligns to a single shared baseline (`ATR_VALUE_BAND`, row 0) and any sub-label top-aligns just below it (row 1). Result, by construction (not per-cell tuning): all price values sit on one horizontal line, all sub-labels on the line below at an identical gap. PLACED's `(LABEL)` and TARGET's `(reason)` moved from inline third lines onto their own top-aligned sub-lines (`CappedSubValue` / `TargetSubValue`) so the values stay on the baseline. `ATR_VALUE_BAND` (58%) is the single knob to shift the whole value line up/down.
  - *Direction-label alignment (with a documented gotcha).* The `LONG`/`SHORT` label is added **directly** to col 0, NOT via `WrapAtrCell` — a nested `TableLayoutPanel` inside the 70px **absolute** column collapses the whole row (every value column drops to 0 width, the card renders blank except `LONG`/`SHORT`). Nested panels are only safe in the percent columns, as the STOP cell already proves; col 0 must hold the label directly. To still land it on the value line, it is bottom-aligned with a small bottom `Padding` (26px ≈ the sub-label band) that lifts `LONG`/`SHORT` up beside STRUCT. (Caveat: the 26px is a fixed offset tuned to the current card height; a future card-height change would want it re-checked.)

**Verification (live).** Solution builds **0/0**; harness **ALL PASS** (engine untouched — the harness doesn't cover WinForms layout). Card rendering was confirmed by launching the app against live market data and screenshotting the ATR card: a `STOP_CLAMPED` case rendered with all four of the trader's alignment points holding — values on one horizontal line, sub-labels on the line below at an identical gap, `LONG`/`SHORT` on the value line, and the `STOP_CLAMPED` / `PLACED` source labels un-wrapped under their values. (This live check is what surfaced the col-0 nesting gotcha above.)

---

## 5. Out of scope (unchanged)

Accumulator (`AggressorVelocityAccumulator`), classifier (`ClassifyAggressorBurst`), CSV columns, the LIVE TAPE strip — all shipped at v50 and untouched. TFI/MicroCVD/CVD mechanisms untouched. Res-3 (Asia/London) scoring stays inert pending their own §5.2 samples. The funding time-anchored window build is the next ⚠ boundary (handover Q3/D4), after this.
