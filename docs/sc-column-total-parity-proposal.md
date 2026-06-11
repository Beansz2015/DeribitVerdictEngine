# SC Column / TOTAL Row Parity — Proposal

**Status:** PROPOSED
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-24

---

## Amendment 2026-06-11 (Fable spec-author seat) — S5 ledger-guard merge + post-correctness-pass deltas

This spec now also carries **audit suggestion S5 (score-ledger reconciliation guard)** — the merge decided in `engine-correctness-pass-proposal.md` §13. Four changes to the body below:

1. **§3.6's assertion is upgraded from removable debug scaffolding to a permanent, engine-side invariant guard.** It runs in ALL builds, lives in `ScoringEngine.Calculate()` (host-agnostic — survives the CLI port and any card rewrite), and is never removed: it is the checked property that makes a future double-count regression — the trader profile's #1 banned pattern — impossible to ship silently. §3.6 as amended below supersedes the original "remove after a quiet week" plan. Cost: one integer sum over ~25 items per run.
2. **Mismatch surfacing (decided): no CSV column.** A ledger mismatch is a bug signal, not an analytics trend — a column reading False for ten thousand rows is padding. On mismatch: set a new display-only `VerdictResult.LedgerMismatch As Boolean`, `Console.WriteLine` a `[LEDGER_MISMATCH]` line with both sums and both scores, surface in the status-bar LOG line (same pattern as `SettingsLoader.LastLoadError`), and append a warning line to the output dump block. CSV schema untouched (stays v0.6).
3. **Emission-site inventory drifted** since this was drafted (the 2026-06-11 engine correctness pass restructured Steps 4/4b/5): `_Verdict.vb` now has **three** `SignalBreakdown.Add` sites, not one — the MTF Gate row is emitted on both regime-veto early-return paths and at Step 4b. All three are informational for the ledger (points 0/0 — the gate is a veto, not a scoring contributor). `AppendLean` and the dominant-side cascade also moved code around §4's line anchors. **Re-grep the full inventory before commit 1**; do not trust the counts/line numbers below.
4. The §3.2 invariant scope (raw `v.LongScore`/`v.ShortScore`, pre-Step-4) and the Steps 3/3b `ls`/`ss`-vs-`state` caveat (§3.4, R2) are unchanged and remain the delicate part. Note: Step 3's funding boost is now capped at `regimeMax` (correctness-pass F7) — the before/after delta-capture pattern in §3.4 handles this automatically; assumed-magnitude capture would not.

Sequencing unchanged: post-P5b on the reskin track, zero data coupling. Implementer per §10 (Opus); the ledger guard adds no model-difficulty — the delta-capture discipline was already the hard part.

---
**Spec target:** `Core/ScoringEngine_Types.vb` (DTO), `Core/ScoringEngine_Calculate_Scoring.vb` (23 emission sites), `Core/ScoringEngine_Calculate_Verdict.vb` (1 emission site), `UI/MainForm_Render_Cards.vb` (SC column derivation + TOTAL verification hook)
**Settings.json impact:** none
**Scoring / engine impact:** **scoring outputs bit-identical** — `v.LongScore` / `v.ShortScore` / `v.MaxScore` unchanged. New per-item attribution fields are *additive*; existing `LongHit` / `ShortHit` booleans stay live for STATE derivation and Pass 2c CONFLICT detection.

---

## 1. Motivation

The SIGNAL BREAKDOWN card's SC column does not sum to the TOTAL row. Spec B's spec-back §5 caught this on three live runs with consistent off-by-one deltas; the spec author's audit determined the cause is structural, not a localised bug.

Per-row SC currently derives from `SignalBreakdownItem.LongHit` / `ShortHit` booleans via `ScForItem` ([UI/MainForm_Render_Cards.vb:2136](UI/MainForm_Render_Cards.vb:2136)) and is always in `{−1, 0, +1}`. TOTAL uses `v.LongScore` / `v.ShortScore` — actual accumulated `ScoreState` after variable-magnitude inputs:

- BBW squeeze penalty: `cfg.Scoring.BbwSqueezePenalty` (default 2)
- Funding Step 3 / 3b: penalties + boosts of variable magnitude per cfg
- OI × CVD Pass 2b: `UpgradeBonus` / `ConflictPenalty` (default 1, configurable)
- Pass 2c alignment: `AlignmentBonus` / `ConflictPenalty` (variable per `regime_weights`)
- Pass 2c-struct trend bonus: `StructureBonus` (default 1)
- Liquidation penalty/boost
- TRANSITIONAL ADX penalty (Step 4)
- Spread WIDE penalty
- Cap via `Math.Min(state.X + bonus, regimeMax)` at multiple sites

**Per-row sum can only equal TOTAL when every emission site contributes exactly ±1 or 0** — a coincidence under default settings, broken whenever BBW squeeze, OI×CVD, Pass 2c alignment, or any cap fires.

The user explicitly chose option (a) — "make SC reflect actual weighted contribution" — over the cheaper option (c) header relabel, on the grounds that scoring-related displays must be accurate.

---

## 2. Non-goals

- No change to scoring math. `v.LongScore` / `v.ShortScore` / `v.MaxScore` / `v.EffectiveLongScore` / `v.EffectiveShortScore` / `v.Verdict` / `v.Confidence` and the CSV columns derived from them are **bit-identical** before and after.
- No removal of `LongHit` / `ShortHit`. The booleans stay live because (a) STATE derivation for RSI / DMI / BBW / TTM relies on them (handover §4 locked decision: "STATE derivation … uses `SignalBreakdownItem.LongHit/ShortHit`"), and (b) Pass 2c CONFLICT detection in `BuildBreakdownFooter` reads them.
- No change to the auto-tweaker pipeline. It consumes `analysis_log.csv` which doesn't carry per-row attribution.
- No new settings.json keys. Cap thresholds and bonus magnitudes already live in `cfg.Scoring.*` / `cfg.Indicators.*.*` / `cfg.RegimeWeights.*`.
- No change to `cfg.RegimeMaxScore` or the verdict-threshold math.

---

## 3. Design

### 3.1 DTO change — `SignalBreakdownItem` gains attribution fields

[Core/ScoringEngine_Types.vb:6-14](Core/ScoringEngine_Types.vb:6) currently:

```vb
Public Class SignalBreakdownItem
    Public Property Label As String
    Public Property LongHit As Boolean
    Public Property ShortHit As Boolean
    Public Property Note As String
    Public Sub New(lbl As String, lng As Boolean, sht As Boolean, nt As String)
        Label = lbl : LongHit = lng : ShortHit = sht : Note = nt
    End Sub
End Class
```

Append two integer attribution fields. **Both can be negative** to faithfully represent penalties (e.g., funding `ls -= penalty` is a negative Long contribution; BBW squeeze applied to Long side is a negative Long contribution).

```vb
Public Class SignalBreakdownItem
    Public Property Label As String
    Public Property LongHit As Boolean
    Public Property ShortHit As Boolean
    Public Property Note As String
    ''' <summary>Actual contribution to v.LongScore from this emission.
    ''' Signed: positive when this row added to Long, negative when it
    ''' subtracted (penalties). Sum across all items must equal v.LongScore
    ''' after Step 4 verdict-time adjustments.</summary>
    Public Property LongPoints As Integer
    ''' <summary>Actual contribution to v.ShortScore from this emission.
    ''' Signed: positive when this row added to Short, negative when it
    ''' subtracted (penalties). Sum across all items must equal v.ShortScore
    ''' after Step 4 verdict-time adjustments.</summary>
    Public Property ShortPoints As Integer

    ' Existing constructor preserved for backward compat — new fields default to 0.
    Public Sub New(lbl As String, lng As Boolean, sht As Boolean, nt As String)
        Label = lbl : LongHit = lng : ShortHit = sht : Note = nt
    End Sub
    ' New constructor for emission sites that know their points.
    Public Sub New(lbl As String, lng As Boolean, sht As Boolean, nt As String,
                   lngPts As Integer, shtPts As Integer)
        Label = lbl : LongHit = lng : ShortHit = sht : Note = nt
        LongPoints = lngPts : ShortPoints = shtPts
    End Sub
End Class
```

The two-constructor pattern lets the implementation refactor emission sites incrementally without breaking the build mid-pass. The single-arg call sites that aren't yet updated emit with `LongPoints = ShortPoints = 0` — they'll fail the parity invariant in §6, which is exactly how the verification finds incomplete sites.

### 3.2 Invariant (post-spec)

```
Σ items.LongPoints  =  state.LongScore   (after all of Steps 2 / Pass 2 / 2b / 2c / 3 / 3b)
Σ items.ShortPoints =  state.ShortScore  (same scope)
```

Step 4 (TRANSITIONAL ADX penalty in `_Verdict.vb`) applies a penalty AFTER the breakdown is finalised. The invariant therefore covers items through Step 3b only. Step 4 produces `v.EffectiveLongScore` / `v.EffectiveShortScore` which are separate fields (already exposed). The SC column sum must match `v.LongScore` and `v.ShortScore` (raw, pre-Step-4), not effective scores.

**This is consistent with the current TOTAL row text:** `TOTAL  Long {v.LongScore}/{shownMax}  |  Short {v.ShortScore}/{shownMax}` ([MainForm_Render_Cards.vb:1935](UI/MainForm_Render_Cards.vb:1935)) — uses raw scores, not effective. Step 4 reduction is communicated through the SCORE card's `eff.N` sub-text, not the SIGNAL BREAKDOWN TOTAL.

### 3.3 Emission site rules — where points come from

For each emission site in `Core/ScoringEngine_Calculate_Scoring.vb` (22 sites) and `Core/ScoringEngine_Calculate_Verdict.vb` (1 site):

- **The actual delta applied to `state.LongScore` becomes `LongPoints`.** If the emission site reads `state.LongScore += 1`, the item carries `LongPoints = 1`. If a cap clamps the delta (`state.LongScore = Math.Min(state.LongScore + bonus, regimeMax)` where `state.LongScore` was at the cap), `LongPoints` records the **clamped** delta, i.e. `min(bonus, regimeMax - state.LongScore_before)`. Reading the actual before/after delta is the canonical pattern — never assume the bonus magnitude.
- **Same for `ShortPoints`** mirroring on `state.ShortScore`.
- **Dual-side emissions** (e.g., funding HighPositive where `ls -= penalty` AND `ss += boost`): the single item carries both `LongPoints = -penalty` and `ShortPoints = +boost`. NOTE text already reflects this; the points fields now match the math.
- **Informational-only emissions** (e.g., the Trend Structure "no score change — structure disagrees with dominant side" branch at [Calculate_Scoring.vb:553](Core/ScoringEngine_Calculate_Scoring.vb:553), or the EXPANSION/CONTRACTION cases at lines 558 / 563): both points fields stay at 0. Hits stay at False. NOTE carries the diagnostic text.
- **Pass 2 upgrade emissions:** when a partial signal upgrades to full via cross-category confirmation, the upgrade item carries the **additional** points contributed beyond what the partial already had. Equivalently: trace the actual `state.X += delta` line for the upgrade and use that delta. The cumulative effect must still satisfy the invariant.

### 3.4 Capture pattern — recommended idiom

Most emission sites can use a tight before/after snapshot pattern:

```vb
' Existing pattern:
state.LongScore += 1
breakdown.Add(New SignalBreakdownItem("ROC(9)", True, False, "+1 BULL ROC ..."))

' New pattern:
Dim lsBefore As Integer = state.LongScore
state.LongScore += 1
Dim lngPts As Integer = state.LongScore - lsBefore   ' = 1 here; respects future caps
breakdown.Add(New SignalBreakdownItem("ROC(9)", True, False, "+1 BULL ROC ...",
                                       lngPts, 0))
```

For dual-side sites (funding, OI×CVD conflict):

```vb
' Existing pattern:
ls -= cfg.Scoring.FundingHighPenalty
ss += cfg.Scoring.FundingHighBoost
breakdown.Add(New SignalBreakdownItem("Funding", False, False, fundingBaseNote))

' New pattern:
Dim lsBefore As Integer = ls
Dim ssBefore As Integer = ss
ls -= cfg.Scoring.FundingHighPenalty
ss += cfg.Scoring.FundingHighBoost
breakdown.Add(New SignalBreakdownItem("Funding", False, False, fundingBaseNote,
                                       ls - lsBefore, ss - ssBefore))
```

Note for funding: the `ls` / `ss` locals diverge from `state.LongScore` / `state.ShortScore` after Step 3 — they're funding-modifier accumulators. The invariant compares against `state.LongScore` at end-of-pipeline. Steps 3 + 3b ultimately fold ls/ss back into state (verify in `_Verdict.vb` integration). Implementation pass must walk this carefully.

### 3.5 UI change — `ScForItem` returns points, not hit-derived ±1

[UI/MainForm_Render_Cards.vb:2136](UI/MainForm_Render_Cards.vb:2136):

```vb
' BEFORE
Private Shared Function ScForItem(items As List(Of SignalBreakdownItem), label As String) As Integer
    For Each it In items
        If it Is Nothing OrElse it.Label Is Nothing Then Continue For
        If String.Equals(it.Label, label, StringComparison.OrdinalIgnoreCase) Then
            If it.LongHit AndAlso Not it.ShortHit Then Return 1
            If it.ShortHit AndAlso Not it.LongHit Then Return -1
            Return 0
        End If
    Next
    Return 0
End Function

' AFTER
Private Shared Function ScForItem(items As List(Of SignalBreakdownItem), label As String) As Integer
    For Each it In items
        If it Is Nothing OrElse it.Label Is Nothing Then Continue For
        If String.Equals(it.Label, label, StringComparison.OrdinalIgnoreCase) Then
            ' Net signed contribution: +Long − Short. Single-sided items
            ' produce ±N as before; dual-side items (funding) produce the
            ' net effect. Magnitude reflects actual cap-applied delta.
            Return it.LongPoints - it.ShortPoints
        End If
    Next
    Return 0
End Function
```

Same change to `ScForItemPrefix` at [line 2152](UI/MainForm_Render_Cards.vb:2152).

The display column width currently fits ±1. After this change, single-row SC values can hit ±2 (BBW squeeze penalty) and up to ±2 or ±3 in extreme cases (Pass 2c with high `AlignmentBonus`). **Audit the SC column width in `MakeBreakdownCell` / the breakdown grid layout** during implementation; bump if values clip. Likely fits without change — Geist Mono "−2" at 9.5pt is ~14 px wide, current cells appear to allocate ~20 px.

### 3.6 Verification hook — permanent ledger guard (AMENDED 2026-06-11, see header)

**As amended:** the check below moves to the end of `ScoringEngine.Calculate()` (a small `Private Shared Sub CheckLedger(res As VerdictResult)` invoked before every `Return res`, covering the regime-veto and MTF-block early returns too), runs in **all builds permanently**, and surfaces per amendment point 2 (`v.LedgerMismatch` + console + status bar + output dump — no CSV column). The render-side `#If DEBUG` form below is superseded; shown for the original design intent:

```vb
#If DEBUG Then
    Dim sumLng As Integer = 0, sumSht As Integer = 0
    For Each it In v.SignalBreakdown
        sumLng += it.LongPoints
        sumSht += it.ShortPoints
    Next
    If sumLng <> v.LongScore OrElse sumSht <> v.ShortScore Then
        System.Diagnostics.Debug.WriteLine(
            $"[SC-PARITY VIOLATION] sumLng={sumLng} v.LongScore={v.LongScore} " &
            $"sumSht={sumSht} v.ShortScore={v.ShortScore}")
    End If
#End If
```

Keep the assertion in code for at least a week of live-run testing after this spec ships. If no violations log for ~50 runs across regimes, remove the assertion in a follow-up commit. This pattern catches missed emission sites that the spec didn't enumerate.

A Release-build user won't see the assertion. Behaviour unchanged in production.

---

## 4. Emission site inventory

Grep at draft time:

```
Core/ScoringEngine_Calculate_Scoring.vb  : 22 `breakdown.Add` sites
Core/ScoringEngine_Calculate_Verdict.vb  : 1  `breakdown.Add` site
```

**The implementation conversation MUST walk every site and update the constructor call**, OR explicitly mark the site as informational-only (both points = 0). Don't trust "this row is always ±1 anyway" — the parity invariant verifies this; let the assertion catch surprises.

Recommended walking order:
1. Step 2 core signals (ROC, RSI, DMI/ADX, Volume) — mostly ±1, easy
2. Step 2 Tier 1 (VWAP, BBW, EMA, OI Change) — mixes single-side and BBW squeeze ±2
3. Step 2 Tier 2 (Spread, OFI, OFI Mom, Liq, CVD, MicroCVD, TFI, EMA200) — variable penalties + spread WIDE
4. Step 2 Tier 3 (Donchian, OBV, VPFR) — Donchian quartile partial-vs-full
5. Pass 2 partial-upgrade emissions — careful with delta accounting (see §3.3 last bullet)
6. Pass 2b OI × CVD — emits one item with the bonus/penalty as points
7. Pass 2c alignment (Regime Align (2c)) — bonus/penalty magnitude from cfg
8. Pass 2c-struct Trend Structure — `cfg.Indicators.TrendStructure.StructureBonus` magnitude (default 1)
9. Step 3 funding baseline — dual-side, both points fields populated
10. Step 3b funding momentum — dual-side or single-side per cfg
11. `_Verdict.vb` site (likely the regime-veto / MTF-block path — read at implementation time)

### 4.1 Special attention sites

- **BBW Squeeze penalty** — `cfg.Scoring.BbwSqueezePenalty` (default 2). Currently emits with `LongHit`/`ShortHit` reflecting the penalised side. Points field carries `−2` (or whatever the cfg value resolves to) on the penalised side, 0 on the other.
- **Funding Step 3** — dual-side, see §3.4 pattern.
- **Pass 2c alignment** — `cfg.RegimeWeights.{Trending|RangeBound}.AlignmentBonus / ConflictPenalty`. The "Regime Align (2c)" emission at [Calculate_Scoring.vb:~430-510](Core/ScoringEngine_Calculate_Scoring.vb:430) is delicate; read the existing logic carefully.
- **Pass 2c-struct** — emission at [line 757](Core/ScoringEngine_Calculate_Scoring.vb:757). Points come from the actual `state.LongScore += sBonus` / `state.ShortScore += sBonus` delta with cap-applied. Informational branches (UPTREND-but-not-dominant, EXPANSION, CONTRACTION) emit points = 0.
- **Step 4 TRANSITIONAL ADX penalty** in `_Verdict.vb` — produces `EffectiveLongScore` / `EffectiveShortScore`, not `LongScore` / `ShortScore`. If this site emits a `SignalBreakdownItem`, the points fields should reflect the contribution to **raw** scores (likely 0 — Step 4 only adjusts effective). Verify at implementation.

---

## 5. Implementation surface summary

| File | Change |
|---|---|
| `Core/ScoringEngine_Types.vb` | Add `LongPoints` / `ShortPoints` properties + secondary 6-arg constructor on `SignalBreakdownItem`. |
| `Core/ScoringEngine_Calculate_Scoring.vb` | 22 emission sites: switch to the 6-arg constructor with before/after delta capture (or 0 for informational rows). |
| `Core/ScoringEngine_Calculate_Verdict.vb` | 1 emission site: same treatment. |
| `UI/MainForm_Render_Cards.vb` | `ScForItem` + `ScForItemPrefix` return `it.LongPoints - it.ShortPoints`. Add debug assertion in `BindCardSignalBreakdown`. Audit SC column cell width. |
| `docs/DeribitIndicatorProject.md` §15 | New entry for this spec's commit. |
| `docs/ui-reskin-handover-2026-05-22.md` §4 Locked decisions | New row: **SC column = signed actual contribution (`LongPoints − ShortPoints`).** Parity invariant `Σ items = v.{Long,Short}Score` must hold. STATE derivation continues to use `LongHit/ShortHit` (unchanged). |
| `docs/ui-reskin-handover-2026-05-22.md` §6 Outstanding | Remove the SC-column row added during the Spec B post-tidy. |

Estimated scope: ~150-200 LOC. Single commit if confident in walking all 23 sites; otherwise split as **commit 1 = DTO + UI + Step 2 core** (assertion-active, will violate for Tier 1-3 sites), **commit 2 = remaining sites until assertion silent**. Implementation conversation's call.

---

## 6. Verification

### 6.1 Build + assertion gate

1. `dotnet build` clean.
2. Run app. Trigger one analysis. Open Output Window in VS or attach a debugger.
3. **Confirm no `[SC-PARITY VIOLATION]` lines appear.** If they do, the offending row identifies which emission site is wrong — fix and re-run.
4. Auto-run 10+ cycles. Confirm no violations across regime transitions.

### 6.2 Hand-tally verification

Same protocol the Spec B spec-back used:

1. Take a screenshot of SIGNAL BREAKDOWN during a live run.
2. Sum the positive SC values → should equal TOTAL's `Long N/M` value.
3. Sum the absolute negative SC values → should equal TOTAL's `Short N/M` value.
4. Repeat on 3 runs with different regimes (TRENDING_UP / TRENDING_DOWN / RANGE_BOUND) and different bonus configurations active (BBW squeeze fired / Pass 2c alignment fired / funding penalty fired).

If all three runs match, the invariant holds. If not, the debug assertion should have caught it earlier; if it didn't, the assertion logic itself has a bug (likely the v.LongScore vs ls/ss confusion in Steps 3-3b).

### 6.3 CSV regression check

Before/after the implementation commit:

1. Run 5 analyses pre-commit, save `analysis_log.csv` to `analysis_log.pre-sc-fix.csv`.
2. Run 5 analyses post-commit with the same market conditions (or, more practically, run side-by-side cycles before and after).
3. `diff` the relevant columns. **`LongScore`, `ShortScore`, `MaxScore`, `Verdict`, `Confidence`, `VerdictContext`, `RegimePenalty`, `EffectiveLongScore`, `EffectiveShortScore` must be bit-identical.** If any differ, a delta-capture site contaminated state.

This is the single most important check — the spec is explicit that scoring math is unchanged. Any divergence here means the implementation accidentally modified scoring while updating attribution.

---

## 7. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Emission site missed → assertion fires → user sees no functional impact but parity violates silently in Release. | Debug assertion in §3.6; walk emission inventory in §4 by checklist; CSV regression check in §6.3. |
| R2 | Delta capture computed against wrong variable (e.g., `ls` instead of `state.LongScore`). Steps 3 / 3b use `ls`/`ss` locals that fold back into state at end. | §3.4 explicitly flags this; implementation conversation reads `_Verdict.vb` integration before touching Step 3 sites. |
| R3 | Pass 2 upgrade emissions double-count: partial item carries +1, upgrade item carries another +1, total = +2 but state only got +1 (or +1 → +2 partial-to-full). | §3.3 last bullet: trace actual `state.X +=` line for the upgrade and use that delta. Assertion catches mismatches. |
| R4 | Cap-applied delta differs from intended bonus. Example: `state.LongScore = 19, regimeMax = 20, sBonus = 2 → actual delta = 1`. | §3.4 pattern uses before/after snapshot which auto-respects the cap. The intended-vs-actual gap is exactly why the pattern is required. |
| R5 | SC column display overflows when single-row contribution > 9 (e.g., a hypothetical regime weight of 5 plus a hypothetical OI×CVD bonus of 4 stacking). | Unlikely under current cfg. Audit column width during implementation; bump if needed. Use `:+0;-0;0` format so sign is always rendered. |
| R6 | Auto-tweaker or CSV consumer reads per-item attribution somewhere this spec missed. | Grep confirmed at draft: `LongHit` / `ShortHit` are read only by UI render paths. `LongPoints`/`ShortPoints` are new — no existing reader. CSV columns reference `v.LongScore` etc., not breakdown items. |
| R7 | Hand-tally column-sum doesn't match TOTAL on the verification run because the assertion was wrong. | The CSV regression check (§6.3) is the truth source. If CSV is bit-identical and assertion is quiet but hand-tally disagrees, the bug is in the display, not the engine. |
| R8 | The single-arg constructor stays in use somewhere and silently emits with 0/0 points. | Recommended: after the implementation pass is complete and assertion has been silent for a week, **delete the single-arg constructor**. That forces every site to be explicit. Schedule as a follow-up cleanup commit. |

---

## 8. Phasing

**Option A — single commit (recommended if confidence is high):**
- All 23 emission sites + DTO + UI + assertion.
- One commit message captures the full surface.
- Build + assertion + auto-run verification.

**Option B — two-commit split (recommended if implementation conversation prefers incremental):**
- **Commit 1:** DTO + UI (`ScForItem` return change) + assertion. Step 2 core signal sites only. Build will pass; the assertion will fire on every analysis until commit 2 lands. Acceptable as long as the implementation conversation commits 2 within the same session.
- **Commit 2:** Remaining 20+ emission sites until the assertion is silent across 5+ auto-run cycles.

**Option C — defer the constructor cleanup:**
- The 4-arg constructor stays in `SignalBreakdownItem` indefinitely. Future emission additions can choose either; new code should prefer the 6-arg.
- Or: schedule a "single-arg constructor removal" follow-up commit after a week of assertion-quiet runs. Recommend this path — forces explicitness over time.

Implementation conversation picks the option that matches its confidence. **Recommend option A + option C (single commit for the points work; deferred constructor cleanup).**

Commit subject template (single commit):

```
fix(ui-reskin): SIGNAL BREAKDOWN SC column = actual scoring contribution

Adds LongPoints / ShortPoints attribution fields to SignalBreakdownItem.
Every emission site captures the actual state.LongScore / state.ShortScore
delta (cap-applied) as it fires. SC column display returns
LongPoints − ShortPoints instead of the hit-derived ±1, so per-row sum
equals TOTAL exactly.

LongHit / ShortHit booleans preserved — STATE derivation for RSI/DMI/BBW/TTM
(handover §4 locked) and Pass 2c CONFLICT detection both continue to read
them.

v.LongScore / v.ShortScore / v.Verdict / CSV columns bit-identical
before and after. Debug assertion in BindCardSignalBreakdown surfaces
any missed emission site to Output Window.

Closes the SC-vs-TOTAL discrepancy surfaced in Spec B spec-back §5
(observed off-by-one on 3 runs, root cause is structural: per-row hits
are binary while TOTAL accumulates variable bonus magnitudes + caps).
```

---

## 9. Out of scope (separately specced if pursued later)

- **Removing `LongHit` / `ShortHit` entirely.** Possible eventually if STATE derivation gets rewritten to read points directly, but not now — too many call sites.
- **Per-cell colour coding by magnitude.** SC value 0 grey / ±1 light / ±2+ deep colour. Aesthetic; out of scope.
- **Exposing per-item attribution via CSV.** Auto-tweaker doesn't need it; trader doesn't ask for it; CSV stays at 87 columns.
- **Refactoring `state.LongScore` / `state.ShortScore` / `ls` / `ss` locals into a unified score accumulator.** Tempting during the rewrite, but a separate refactor. This spec stays surgical.
- **Auto-tweaker rules that fire on per-item attribution.** If the data turns out to be useful, that's a future spec.

---

## 10. Approval gate

User reviews and either:
- Approves wholesale → kickoff drafted with the build-screenshot-CSV-diff gate prominent.
- Approves with revisions (e.g., split commit phasing, different debug-assertion location, exposed in CSV) → spec updates first.
- Defers → adds to handover §6 backlog; the spec stays available for when the trader wants display-trust during SIGNAL BREAKDOWN hand-tally.

**Recommended model for the implementation conversation: Opus 4.7 High.** Synthesis-heavy (23 emission sites + cap-aware delta capture + the dual-side funding pattern). Lower models risk missing the Steps 3 / 3b ls/ss vs state.X distinction.

---

**End of proposal.**
