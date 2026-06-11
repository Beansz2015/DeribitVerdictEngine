# Engine Tier D — Display & Hygiene Pass — Implementation Spec-Back

**Date:** 2026-06-12
**Implementer:** Opus 4.8 (fresh conversation, per proposal §8 routing)
**Spec:** `docs/engine-tier-d-hygiene-proposal.md` (approved 2026-06-11, commit `43cc949`)
**Status:** All of D1–D5 implemented. Five local commits, **not pushed** — awaiting the user's live smoke test (§7 manual smoke is the open gate).

---

## 0. Result summary

| Commit | Scope | Build | Harness |
|---|---|---|---|
| `482c9bb` | D2 linear ATR levels + sizing-display relabel + A12 | clean | 12/12 pass |
| `0bd1b63` | D1 inverse-contract Kelly fix + leverage cap + settings v32 + A10 | clean | 13/13 pass |
| `d5bf209` | D4 auto-run save churn + dead `auto_run.enabled` removal | clean | — |
| `d3a0168` | D3 VPFR-lite context tag + D5 candle freshness guard + A11 | clean | 16/16 pass |
| `6d5349a` | §15 v32 entry + proposal status → IMPLEMENTED | — | — |

Anchor re-verification (§6 kickoff-staleness rule) was done in full before commit 1: every file:line quoted in the proposal was checked against the tree. **Drift was minor and cosmetic** — see §0a.

The harness runs 16 checks (A8/A11/A12 have multiple sub-cases). Final state: ALL PASS. The harness lives under the gitignored `verify/` tree, so A10–A12 and the `OrderCheck.vbproj` edit are **local-only, not committed** (consistent with how A1–A9 are carried).

### 0a. Anchor drift found

- **D4 auto-run save lines:** proposal quoted `:41-45` / `:73-75`; actual `Save` sites were `:45` (StartAutoRun) and `:75` (StopAutoRun). Same code, off by a few lines. Held otherwise.
- **D2 "distance product" sites:** the proposal's list named `Render_Header.vb:70-72` and `PlaintextSnapshot.vb:83-84` among the `ATR × scale × mult` products. Those two lines actually *consume* the already-computed `atrStop`/`atrTarget` (they compute `price ± distance`); the `ATRScaleFactor` multiplications live at exactly **four** sites — `Render_Sections.vb:25-26`, `PlaintextSnapshot.vb:46-47`, `Render_Cards.vb:845`, `ScoringEngine_Calculate_Verdict.vb:179`. Removing the factor at those four propagates correctly to the consumer lines. No behavioural difference from the spec's intent; just a tracing clarification.
- Everything else (D1 Kelly anchors `:94`/`:45`, both call sites, D3 structural set `:43-46` and the `"VPFR-lite"` breakdown label at `Scoring.vb:751`, D5 skip-gate at `Analysis.vb:88-100`, `Candle.Timestamp As Long` ms-epoch, `AutoRun.Enabled` write-only) held exactly.

---

## 1. Per-item notes — what matched spec exactly

- **D2** — `norms.ATRScaleFactor` dropped from all four distance products; multiplier values (1.2/2.0) untouched; the Step 5b cap base now uses the same linear target. `r.ATRSizeMultiplier` (CSV `ATRMultiplier`) left at its current CurrATR/ATRRef meaning per §2.3; displays derive the reciprocal `ATRRef/ATR` at render. DYNAMIC NORMS line prefix relabelled `ATR scale` → `ATR ratio` with the value (`{0:F2}x (ATR=… ref=…)`) left as-is, both render paths. A12 brackets the raw Step 5b long target at exactly `price + ATR × mult` (scale absent).
- **D1** — `CalcKellySizing` gains `entryPriceUsd`; both call sites (`Render_Header.vb:158`, `PlaintextSnapshot.vb:144`) pass `r.CurrentPrice`. Denominator corrected to `face × stopDistance / entryPrice` with an `entryPriceUsd <= 0` guard. Leverage cap = `Floor(AccountSizeUsd × MaxLeverage / ContractFaceUsd)`; `final = min(risk-derived, leverage-derived)`; `KellyLevCapped` set when the leverage arm binds. Dead `"NEUTRAL"/"WAIT"` guard deleted; empty-verdict check retained; NO TRADE still renders the lean Kelly (v30 behaviour preserved). A10 reproduces the proposal's worked example exactly (entry 62,900 / stop 60 / HIGH → riskPerContract ≈ 0.00954, 500 contracts, `[LEV CAPPED]`, risk $50).
- **D3** — one line: `lbl = "VPFR-lite"` added to the structural set. RSI(9)/Liquidations/Spread/Funding and the TREND STRUCTURE row stay out per the §3 explicit non-changes.
- **D4** — `StopAutoRun` `Save` removed entirely; `StartAutoRun` saves only when `IntervalMinutes`/`IntervalSeconds` differ from the persisted value, changeNote `"auto_run interval changed via UI"`; dead `auto_run.enabled` key + `AutoRunSettings.Enabled` POCO removed.
- **D5** — `IsFresh(candles, resolutionMinutes, nowUtc)` host-agnostic pure function; "last bar older than 2× the resolution → stale"; wired into the skip-gate after the count/availability checks for **1m and 5m only**; threshold `2×` hardcoded as a structural constant (same class as the fetch counts). 15m cache and trade-list age intentionally left ungated.

## 2. Implementer judgement calls (within spec latitude, flagged for review)

1. **`auto_run.enabled` JSON-key removal landed in the D4 commit, not the D1/v32 commit** — even though its change_log entry rides the single v32 bump (§6, "v32 lands once"). Reason: until D4 removes the `Enabled` POCO property and the `cfg.AutoRun.Enabled = True` writer, `StartAutoRun`'s `Save` re-serialises the property and re-adds the key. Removing it from JSON earlier would not stick. The v32 change_log (written in commit `0bd1b63`) describes the *full* version scope (add `max_leverage` + remove `enabled`); the tree fully reflects it after `d5bf209`. Net of the two commits the file is correct; only the intermediate commit `0bd1b63` carries a change_log that mentions a removal landing one commit later.
2. **`ScoringEngine_Kelly.vb` added to `OrderCheck.vbproj`.** It wasn't previously linked (A1–A9 never touched Kelly). Required so A10 exercises the real shipped `CalcKellySizing` rather than a copy. Local-only file, gitignored. Solution build is unaffected (the harness is standalone, run via `dotnet run --project`).
3. **Notional/leverage line format:** `≈ ${N:N0} · {lev:F1}× lev` with the `[LEV CAPPED]` tag appended to *that* line (amber + bold when capped), gated on `KellyContracts >= 1`. The pre-existing KELLY header `[CAPPED]` tag (the *risk-fraction* cap, `KellyCapped`) is a separate signal and was left intact — the two caps are independent and both can show.
4. **`size ×N` display + divide-by-zero guard.** §2.2 says "derive at render from `norms`, clamp display to one decimal sanity." Interpreted as: `sizeMult = If(r.ATR > 0, norms.ATRRef / r.ATR, 1.0)`, formatted `:F2` (matching the literal `{ATRRef/ATR:F2}` in the spec text). Did **not** add a hard numeric range clamp — `ATRScaleFactor` is already clamped `[0.25, 4.0]` upstream, so its reciprocal is bounded `[0.25, 4.0]` too; an extra clamp would be dead. The `r.ATR > 0` guard is the "sanity" protection against a degenerate render.
5. **`Render_Cards.vb` sub-header relabelled to match.** The ATR card sub-header (`:853`) previously displayed the rounded `r.ATRSizeMultiplier` (CurrATR/ATRRef). It now shows `size ×{sizeMult:F2}` derived from `norms.ATRRef / r.ATR`, consistent with the §2.2 relabel of "the ATR card sub-header." `r.ATRSizeMultiplier` is untouched on the result object for CSV continuity. The stale "legacy parity uses the unrounded scale factor" comment block was rewritten.
6. **D5 helper location:** placed on `IndicatorEngine` (partial class in `Indicators_Momentum.vb`) rather than a new shared util file — the spec offered either ("implementer's call"). `IndicatorEngine` is already the host-agnostic home for the pure Calc* functions and is linked into the harness, so no new project plumbing. Edge behaviour: empty/`Nothing`/`resolutionMinutes <= 0` → `False` (not fresh — conservative skip); a future-dated last bar (clock skew) → treated fresh (negative age ≤ threshold). A11 covers fresh / stale / empty.

## 3. Acceptance harness (§7)

Extended `verify/ordercheck/Program.vb` (gitignored) with three fixtures; `OrderCheck.vbproj` gained `ScoringEngine_Kelly.vb` (judgement call 2). All 16 checks pass alongside the existing A1–A9.

| Test | Result | Notes |
|---|---|---|
| A10 Kelly leverage-cap | PASS | entry 62,900 / stop 60 / HIGH / POCO defaults → `KellyContracts = 500`, `KellyLevCapped = True`, `KellyRiskUsd ≈ $50`. Asserts the leverage arm binds (risk-derived would be 5,241). |
| A11 freshness | PASS ×3 | 30s-old 1m bar → fresh; 3min-old (3× resolution) → stale; empty list → not fresh. |
| A12 linear levels | PASS ×2 | `norms.ATRScaleFactor = 2.0`, ATR 50, price 100,000, targetMult 2.0. Swing 100,099 (< linear target 100,100) → caps → `AdjustedLongTarget = 100099`; swing 100,101 (> 100,100) → uncapped → `AdjustedLongTarget = 0`. The bracket pins the raw target at exactly 100,100 = `price + ATR × 2.0`; old quadratic geometry would put it at 100,200 and both would cap. |

**A12 fixture shape** (the judgement the spec delegated): reuses the A8 RANGE_BOUND builders with `r.ATR = 50` and a parameterised `SwingTargetLong`, `norms.ATRScaleFactor = 2.0`, MTF/Pass-2b/2c suppressed via the A8 cfg. Step 5b runs unconditionally regardless of verdict, so the long cap arbitration is exercised directly; VPFR is NEUTRAL with zero HVN walls so only the Tier-1 swing arm is live. The two-sided bracket means any reintroduction of the scale factor (or a multiplier change) breaks the fixture loudly.

## 4. Settings v32

- `settings.json`: version 31 → 32, `last_modified`/`modified_by` (`engine-tier-d-hygiene`) updated, change_log entry (newest-first) covering the full pass. **Added** `kelly.max_leverage = 5.0`; **removed** `auto_run.enabled`. Validated the file still parses post-edit.
- `EngineSettings.vb`: `KellySettings.MaxLeverage As Double = 5.0` + XML doc; `AutoRunSettings.Enabled` property removed (replaced with a comment noting the v32 removal).
- `ScoringEngine_Types.vb`: `VerdictResult.KellyLevCapped As Boolean = False` added.
- CSV stays **v0.6** (no schema change); no eval-cache change. Kelly fields are not logged, so D1 has zero data impact; D2/D3 shift logged `TargetCapReason` bucket frequencies and the `VerdictContext` distribution respectively (display-derived, no schema change) — which is why this landed pre-collection.

## 5. Docs

- `docs/DeribitIndicatorProject.md`: §15 v32 entry (one row covering all five items).
- `docs/engine-tier-d-hygiene-proposal.md`: status header → **IMPLEMENTED** with the four commit SHAs; original APPROVED line retained beneath for provenance.

## 6. Open items / handoff state

1. **User smoke test then push** — local commits only, per the compile/test gates. Nothing pushed. §7 smoke: KELLY shows non-zero contracts + the notional/lev line (no `[LEV CAPPED]` absurdities); ATR header reads `size ×N` with N = ref/ATR; toggle auto-run start/stop twice → `settings.json` version stays 32.
2. **Collection restart** — serious auto-run accumulation can begin once this lands; this was the last engine gate. Rows logged before it carry old-geometry display semantics (harmless for eval per D2b, but the geometry boundary is worth a mental mark).
3. **Expect post-D2 shifts** — at high vol displayed targets sit closer (fewer `CAPPED @`); at low vol levels stop compressing into noise. The 1.2/2.0 multiplier review stays a **WATCHING** item on clean data (§2.1).
4. **Out of scope, still yours:** S5 ledger guard (merged into Spec C, post-P5b), S-7 time-windowed momentum (own spec — changes signal cadence), any threshold tuning (recalibration phase), trend-structure context reclassification (§3 — a post-collection data question).
5. **Auto-tweaker remains held** (carried over from the correctness pass) — manual-fire only until ≥1 clean window + supervised first fire; re-baseline review triggers at ≥300 clean rows.
