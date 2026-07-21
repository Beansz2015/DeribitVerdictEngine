## Eval Display Semantics — Success + WEAK Exclusion + Band Vocabulary (F2/F3/F12) · Spec-Back

**Built:** 2026-07-21 · **Spec:** `eval-display-semantics-proposal.md` (E1–E4 ALL TICKED 2026-07-21; E3a revised same day to DISPLAY-RENDERING only — the superseded rename inventory in §3 is decision-of-record, NOT implemented).
**Type:** display/report semantics — zero scoring impact, no ⚠ dataset boundary. Settings v54 → **v55** (one value change: `performance_display.min_sample_for_render` 4 → 10; no keys added or removed).
**State:** local commit; solution + AutoTweaker + WhatIfRunner + OrderCheck build 0/0 Release; harness **170 checks, ALL PASS** (+5 vs pre-F2/F3/F12; two pre-existing fixtures re-pinned for the delta sign flip); verify-gate `prepush` GATE PASSED.

---

## 1. What was built

Three surfaces changed how they READ; nothing changed how the engine SCORES.

**E1 — Success orientation at render** (`MarkdownReportWriter` + `PromptBuilder`). Every rate the offline report and the LLM prompt render is now a SUCCESS rate. The internal storage (`FailureCellResult.Failures` / `FailureRate` / `CiLow` / `CiHigh`) stays failure-oriented — the auto-tweaker's trigger comparison (`aggregateRatePct < FailureRateThresholdPct`, built from `cell.Failures / cell.SampleSize`) is the same comparison it was pre-flip, reading the same numbers. Conversion happens only at the render boundary, via three tiny private helpers on `MarkdownReportWriter` (`SuccessPct` / `SuccessCiLow` / `SuccessCiHigh` — Wilson CI complement: success CI low = 1 − failure CI high; ★/◆ picks unchanged, captions re-worded).

**E2a — WEAK exclusion at display time** (`LivePerformanceTracker.AggregateRange`). Storage is unchanged (`IsEligibleVerdict` still admits WEAK); the exclusion is applied when the strip renders, keyed off the verdict band via new `IsWeakVerdict`. New `WindowAggregate.WeakSuccessCount` / `WeakFailureCount` counters route WEAK rows separately so the EXISTING `_perfTip` tooltip can carry a `WEAK excl.: N% (n=M)` line (barrier-metric only — the tooltip line does not toggle with [B]/[T]). Composes with the F4 NO_DATA exclusion — a NO_DATA WEAK row is counted in neither. Cache keeps WEAK rows (reversible; no rotation).

**E2b — `min_sample_for_render` 4 → 10** (settings.json + `PerformanceDisplaySettings.MinSampleForRender` POCO default). This is the value change the settings-version bump carries. E2a drops the strip's denominator ~2.6×; a rate that moves in ~3pp steps needs a real floor.

**E3a — Band display rendering** (`Core/ScoringEngine_Types.vb` + `UI/MainForm_PlaintextSnapshot.vb` + `UI/MainForm_Render_Cards.vb`). New static helper `VerdictResult.FormatVerdictForDisplay` is the one seam both render surfaces route through: bare `LONG` → `MEDIUM LONG`, bare `SHORT` → `MEDIUM SHORT`, everything else unchanged (STRONG_x, WEAK_x, `NO TRADE`, `NO TRADE [WEAK LONG]`, empty). The two render sites change in the SAME commit (parity rule). **STORED/WIRE STRINGS UNCHANGED EVERYWHERE ELSE** — CSV `Verdict`, bridge payload `verdict`, eval cache, `AnalysisLogger`, `SignalEmitter.DeriveDirection`, `LivePerformanceTracker.IsEligibleVerdict/IsLongVerdict/IsWeakVerdict`, `FailureRateMatrix.CanonicalTier`, `AutoTweakerCore` filter, every fixture stored-string pin — all carry bare `LONG` / `SHORT` for the middle band. The four-surface parity rule is deliberately diverged on the two render surfaces (precedented by the cap-reason rich string vs CSV bucket). The frozen bridge contract routes actionability through `direction` + `confidence` — both untouched.

**F7 rider** — session-cell tooltip gains "block" (`most-recent London block, 0/26`) so `London: 0%` doesn't misread as "London is broken".

**Report legend + parity recipe** — one mapping line in the report header (`MEDIUM_x ↔ displayed "MEDIUM x" ↔ stored "x" ↔ payload MEDIUM · STRONG↔HIGH · WEAK excluded from the matrix`) plus a one-line pointer to `§7a of offline-matrix-placed-target-spec-back.md` (the strip-replication + eval-cache↔CSV join + `.0000000Z` provenance recipe — the standing parity instrument).

**Docs pass** — UserManual + TraderGuide verdict tables gain the displayed forms with the display↔stored mapping stated; `DeribitIndicatorProject.md` §5 gets the same treatment; UserManual §18 retired-constants block (F11 leftover) refreshed to the placed-target migration reality (the two ATR-threshold arrays are gone; the two multipliers moved to the legacy fallback; success orientation noted).

---

## 2. Files touched

| File | Change |
|---|---|
| `settings.json` | v54 → **v55**; `performance_display.min_sample_for_render` 4 → 10 (E2b); newest-first `change_log` entry. |
| `Core/Settings/EngineSettings.vb` | `PerformanceDisplaySettings.MinSampleForRender` default 4 → 10 (POCO parity with the JSON value); doc comment updated. |
| `Core/ScoringEngine_Types.vb` | New `Public Shared Function VerdictResult.FormatVerdictForDisplay(stored As String) As String` — the one seam for the middle-band display rendering. |
| `UI/MainForm_PlaintextSnapshot.vb` | `VERDICT:` line routes through `FormatVerdictForDisplay(v.Verdict)`. In-line comment explains the display↔stored divergence + points at the sibling render site. |
| `UI/MainForm_Render_Cards.vb` | `BindCardVerdict` routes `_lblVerdictText.Text` through `FormatVerdictForDisplay(v.Verdict)` (colour map still reads `v.Verdict` verbatim — bare `LONG` still resolves to `ACC_LONG`). Same commit as the snapshot change (parity rule). |
| `LivePerformanceTracker.vb` | `WindowAggregate` gains `WeakSuccessCount` / `WeakFailureCount` + `WeakBarrierRatePct` property. `AggregateRange` routes rows through new private `IsWeakVerdict` — WEAK to `Weak*Count`, else to `SuccessCount`/`FailureCount` (matrix population). NO_DATA branch untouched. |
| `UI/MainForm_Layout.vb` | `UpdatePerformanceLabels` tooltip gains the "block" word on session cells (F7 rider) — "most-recent London block, 0/26" — and appends a `WEAK excl.: N% (n=M)` line to the EXISTING `_perfTip` when WEAK rows exist for the window. No new tooltip control. |
| `analysis/MarkdownReportWriter.vb` | E1 render flip. New private `SuccessPct` / `SuccessCiLow` / `SuccessCiHigh` helpers at the top of the class. Section 2 heading `Failure-Rate` → `Success-Rate` + blurb reworded (Success = target hit first). Section 3 blurb: "Rising failure rates" → "Falling success rates" (and the sign of the Δ flips through the same conversion — `after - before` on success). Section 4 captions: ★ text + ◆ text re-worded; `RecommendedText` renders `success` + complemented CI. Section 5 caption: "How each cell resolved (counts, not %). Success = placed target hit first; the other three columns are the failure decomposition." Section 6: `{1:P1} success  n={2}  CI [...]`. `BuildSummaryCsv` — column renamed `FailureRate` → `SuccessRate`, value flipped (`1 - failureRate`), CI complemented. Header block gains the **Success orientation (v55, 2026-07-21)** note, the **Verdict-tier legend** (F12 mapping), and the **Parity instrument** reference. Interpretation block: `**Success model:**` (was `Failure model:`); "failure rates rise" → "success rates fall". New public `BuildFullMarkdownForHarness` + `BuildSummaryCsvForHarness` — thin passthroughs that let A34a assert the full render without touching disk. |
| `tools/AutoTweaker/PromptBuilder.vb` | Matrix heading `Failure-Rate` → `Success-Rate` + a preface paragraph explicitly reconciling the two views ("Rates below are SUCCESS rates … the **Trigger** line above quotes the internal failure comparison, unchanged … star (★) unchanged from the failure-oriented era"). Matrix cell renders `1 - c.FailureRate` + Wilson CI complement. Terminal instruction: "Based on the success-rate data above (equivalent to the failure comparison in the Trigger line — the internal truth is unchanged, only the render orientation flipped)". The trigger line itself (`aggregate failure rate X% > threshold Y%`) is UNCHANGED — it faithfully names the internal comparison AutoTweakerCore made. |
| `docs/UserManual.md` | Verdict-tier table (§ around line 87) gains the "Verdict (displayed) / Stored / wire string" pair of columns + a paragraph "Display ↔ stored mapping (v55, 2026-07-21)" naming every string-matching site that stays on the bare form. §18 outcome-definition block (F11 leftover) refreshed: the two retired ATR-threshold arrays deleted; the two multipliers re-scoped to legacy fallback; barrier-hit definition rewritten in placed-geometry terms; NO_DATA (F4) called out; success orientation (E1) called out; WEAK display-time exclusion (E2a) called out. |
| `docs/TraderGuide.md` | The "how to read the verdict" bullet list (~line 41) grows the display↔stored parenthetical on the middle-band bullet. The Quick Reference table gains the "Stored / wire" column + a footnote naming the frozen bridge contract's actionability keys as untouched. |
| `docs/DeribitIndicatorProject.md` | §5 verdict-levels table + display↔stored mapping paragraph. §6 pointer bumped v51 → v55. §15 topmost row added ("v55 · eval display-semantics (F2/F3/F12) · 2026-07-21"). |
| `verify/ordercheck/Program.vb` | Two existing fixtures re-pinned for the +40% → −40% Δ sign flip under success orientation: **A27c** (D4 before/after) and **A32c** (single-column before/after render) — same cells, opposite side of the axis, so the delta sign flips. New: **A34a** report + CSV render as SUCCESS (matrix heading, D4 blurb + −40%, legend, orientation note, CSV column rename + value flip); **A34b** tweaker trigger stays failure-oriented under the flipped render (25% aggregate < 40% threshold ⇒ BELOW_THRESHOLD, the load-bearing invariant); **A34c** WEAK excluded from Success/Failure counts + `WeakBarrierRatePct` tracks the WEAK block (5-row list: 1 STRONG success + 1 MEDIUM failure + 2 WEAK success + 1 WEAK failure → Success/Failure = 1/1 = 50%, Weak = 2/3 ≈ 66.7%, TotalRange = 5); **A34d** display helper renders MEDIUM prefix only on bare LONG/SHORT (STRONG/WEAK/`NO TRADE`/`NO TRADE [WEAK LONG]` all untouched); **A34e** stored-form pins — bare `LONG` stays bare on CSV / payload / eval cache; `DeriveDirection` still returns `LONG`; `AggregateRange` still routes it to `SuccessCount` (not `WeakSuccessCount`); display side renders `MEDIUM LONG`. |

---

## 3. Decisions the spec left to the implementer

**(a) `IsWeakVerdict` stays private, tested via `AggregateRange`.** The helper mirrors `IsLongVerdict` (private, static, on `LivePerformanceTracker`). The harness asserts its effect through `AggregateRange` (WEAK rows land in `Weak*Count`, non-WEAK in `Success/FailureCount`) rather than probing the classifier directly — same pattern the F4 fixtures used for the NO_DATA branch, and the correct level to pin behavior at (a future rename of the helper doesn't need to move the fixture).

**(b) `BuildFullMarkdownForHarness` / `BuildSummaryCsvForHarness` added as public passthroughs.** `BuildMarkdown` and `BuildSummaryCsv` are private (they touch disk paths and internal formatting). The E1 acceptance requires asserting on the FULL render text (matrix heading, orientation note, legend all live in that surface). Rather than expose the privates or duplicate the internals in the fixture, two one-line `Public Shared` passthroughs suffice — same shape D6 used for `BuildD4Section`. Zero production behaviour change.

**(c) `WeakBarrierRatePct` is barrier-metric only.** The tooltip's WEAK line does not toggle with the [B]/[T] mode. Rationale: the target-hit denominator on WEAK rows carries no independent information at this sample size — the trader's use for the WEAK line is "how are the refused signals doing", answered plainly enough by the barrier rate. Adding a `WeakTargetRatePct` field for a mode-toggle we may never use is over-engineering. If the mode toggle ever earns the visibility, it's one property.

**(d) The trigger line itself stays failure-oriented in the LLM prompt.** The spec calls out "PromptBuilder's matrix table AND its surrounding prose together". The trigger line comes in from AutoTweakerCore (`"aggregate failure rate {0:F1}% > threshold {1:F1}%"`) — it names the internal comparison the tweaker actually made, and rewriting it to a success rate would misrepresent that comparison. The compromise: the matrix + a fresh preface paragraph explicitly reconcile the two views for the LLM ("Rates below are SUCCESS rates … the Trigger line above quotes the internal failure comparison, unchanged … success = 1 − failure"). Same technique the report header uses (Success orientation note + failure-oriented tweaker note in the same block).

**(e) A32c / A27c re-pinned, not re-authored.** The two fixtures pin the shape of the D4 grid; the ONLY thing that changed is the sign of the +40% Δ under the flipped render (before 20% failure → after 60% failure = +40 pp on failure, equals before 80% success → after 40% success = −40 pp on success — same rows, opposite side of the axis). Re-pin the sign; keep the geometry pin exact. New A34 family carries the new-surface assertions rather than growing the existing ones.

---

## 4. Additions beyond the §5 inventory

**§18 F11 leftover was rewritten, not just quoted-fresh.** The spec lists it as "fix in the same pass". The trader-facing block that quoted `StrongAtrThresholds`/`MediumAtrThresholds` had aged out — those arrays are DELETED (placed-target migration), and the two remaining constants (`EngineTargetAtrMultiplier` / `AdverseFallbackAtrMultiplier`) now scope to legacy fallback rows only. Left as-was, the block would have re-introduced a "STRONG uses the larger set" claim that no longer describes any code path. Rewritten to describe the placed-geometry reality (barriers = the logged `Placed*` columns), name the F4 NO_DATA outcome explicitly, and disclose the E1 success orientation + E2a WEAK exclusion. Same shape the block had before, one less footgun for a next-seat reader.

---

## 5. Acceptance (spec §6)

| Requirement | Result |
|---|---|
| Builds 0/0 | Solution (Release), AutoTweaker, WhatIfRunner, OrderCheck — all **0 errors / 0 warnings**. Release-only per the standing collector-protection rule. |
| Harness unregressed | **170 checks, ALL PASS**, 0 failures (165 pre-pass + 5 new; A27c + A32c re-pinned for the Δ sign flip — same geometry, opposite orientation). |
| Success render — matrix / CSV | **A34a** — matrix heading contains `## 2. Success-Rate Matrix`; D4 blurb contains "success" + the flipped cell; legend line present; orientation note present; CSV header contains `,SuccessRate,` (no `,FailureRate,`); CSV value contains `,0.400000,` for a 0.6 failure cell. |
| Success render — D4 grid | **A27c** re-pinned: same cell now reads `80% → 40% (-40%)` (was `20% → 60% (+40%)`); heading, tier, n=40 assertions unchanged. |
| Tweaker trigger unchanged | **A34b** — 25/100 aggregate rate below 40% threshold ⇒ BELOW_THRESHOLD, the same result the tweaker got pre-flip. The load-bearing invariant of the display-only spec. |
| WEAK excluded from strip | **A34c** — 5-row list (1 STRONG success + 1 MEDIUM failure + 2 WEAK success + 1 WEAK failure): `SuccessCount=1`, `FailureCount=1`, `WeakSuccessCount=2`, `WeakFailureCount=1`, `TotalRange=5`, `BarrierRatePct=50%`, `WeakBarrierRatePct≈66.7%`. |
| Band prefix on stored LONG (both surfaces) | **A34d** — `FormatVerdictForDisplay` renders `MEDIUM LONG` / `MEDIUM SHORT` on bare `LONG` / `SHORT`; leaves `STRONG LONG`, `WEAK SHORT`, `NO TRADE`, `NO TRADE [WEAK LONG]`, empty string alone. Both render sites (snapshot + card) route through this same helper — asserted in code by construction (single seam), and the parity rule is satisfied because both call sites change in the same commit. |
| STORED-FORM PINS (the revision's load-bearing invariant) | **A34e** — a `VerdictResult` with `.Verdict = "LONG"`: bare `LONG` on `v.Verdict` (what the CSV writes and the payload emits verbatim); `SignalEmitter.DeriveDirection` returns `"LONG"` (side only, unchanged); an eval-cache entry with `Verdict = "LONG"` routes to `SuccessCount` under `AggregateRange` (not `WeakSuccessCount`); display helper renders `MEDIUM LONG` for the SAME string. All four checks in one assertion. |
| verify-gate | `prepush` mode — **GATE PASSED**, exit 0. `display-parity`: snapshot AND card both changed (parity rule satisfied by construction; both call sites edited in the same commit). `version-bump`: **OK** — engine-path change accompanied by a settings.json version bump (v54 → v55). |

---

## 6. Not verified by the implementer (runtime, trader-observed)

The **expected live effect** is a re-read of the same rows: the perf-strip cells lose their WEAK-blended baseline (denominators drop by whatever the block's WEAK share is; the sampled NY block ran ~62.5% WEAK, so the STRONG+MEDIUM headline should read a distinctly different number — 33.3% vs the previously-displayed 30.0% on that block, roughly), a `WEAK excl.: N% (n=M)` line appears in the tooltip on any window with WEAK evaluable rows, and the VERDICT line + card render `MEDIUM LONG` / `MEDIUM SHORT` on any middle-band run while the CSV `Verdict` column and any downstream soak-log join continue to see bare `LONG` / `SHORT`. Session cells will show `--%` more often for a while under the higher `min_sample_for_render` = 10 floor — that's E2b working as designed.

---

## 7. Coordinator review checklist

- [ ] Success flip lives only at render boundaries (`MarkdownReportWriter` + `PromptBuilder`); `FailureCellResult.Failures/FailureRate` unchanged; `AutoTweakerCore.aggregateRatePct < FailureRateThresholdPct` unchanged (spec §1 hard invariant).
- [ ] `AggregateRange` routes WEAK to `WeakSuccess/FailureCount`; `IsEligibleVerdict` still admits WEAK at storage; no cache rotation.
- [ ] `_perfTip` gains the `WEAK excl.:` line only when WEAK rows exist in the window; F7 "block" word added to session cells only.
- [ ] `FormatVerdictForDisplay` renders MEDIUM prefix ONLY on bare `LONG` / `SHORT`; the two render sites (snapshot + card) route through it in the same commit; no other site touches the verdict string for rendering.
- [ ] Stored/wire strings stay bare `LONG` / `SHORT` for the middle band on: CSV `Verdict`, payload `verdict`, eval cache, `AnalysisLogger`, `SignalEmitter.DeriveDirection`, `LivePerformanceTracker.IsEligibleVerdict/IsLongVerdict/IsWeakVerdict`, `FailureRateMatrix.CanonicalTier`, `AutoTweakerCore` tier filter, every harness stored-string pin (spec §3 load-bearing invariant).
- [ ] `scoring.tier_floor` untouched (E3b, HC-safety — the TRANSITIONAL penalty floor is not tier vocabulary).
- [ ] Settings v54 → v55; only `performance_display.min_sample_for_render` value changed; no keys added or removed; `change_log` entry newest-first.
- [ ] §15 row present; UserManual/TraderGuide/§5 tables carry display↔stored mapping; UserManual §18 refreshed to the placed-target migration reality.
- [ ] Deviations in §3 above accepted.
