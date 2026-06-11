# Engine Tier D — Display & Hygiene Pass (Proposal)

**Date:** 2026-06-11
**Author:** Fable 5 (spec-author seat)
**Status:** **IMPLEMENTED 2026-06-11** (commits `482c9bb` D2, `0bd1b63` D1+v32, `d5bf209` D4, `d3a0168` D3+D5; §15 entry added). All five items shipped as specced — D2 linear, D1 option (a) fix-the-math + leverage cap with `kelly.max_leverage` 5.0, D4 as specced. `settings.json` bumped to v32 (added `kelly.max_leverage`, removed dead `auto_run.enabled`). Local acceptance harness A10/A11/A12 added (verify/ordercheck, gitignored); all 16 checks pass; `dotnet build` clean. Zero changes to Step 2–5 scoring votes, thresholds, or vetoes. Local commits only — pending user smoke test, then push.

**Status (original):** **APPROVED 2026-06-11** — user confirmed: D2 goes linear (§2), D1 takes option (a) fix-the-math + leverage cap with `kelly.max_leverage` default 5.0 (§1), D4 as specced (§4). Implementation may begin per §8 routing. Touches `settings.json` (v32) and display/logging behaviour; zero changes to Step 2–5 scoring votes, thresholds, or vetoes.
**Implementer:** Opus, fresh conversation — all items are mechanical once the decisions below are approved. Verify every file:line against the tree before commit 1 (anchors drift).
**Why now:** this is the last engine work gating the clean-data collection restart. D2 (level geometry) and D3 (context-tag distribution) change what gets logged; D5 protects row quality. Ship before serious auto-run accumulation begins.

---

## 0. Scope

| Item | Audit ID | What | Data impact |
|---|---|---|---|
| D1 | H4 | Kelly contract sizing dead (inverse-contract dimensional error) + dead verdict guard | none (Kelly fields not logged) |
| D2 | S-1 | ATR stop/target distances scale quadratically with vol; displayed "scale" is the reciprocal of the profile's sizing formula | displayed levels + `TargetCapReason` bucket frequency; eval semantics unchanged |
| D3 | S-2 | `VPFR-lite` missing from the context-tag structural list | logged `VerdictContext` distribution |
| D4 | S-4 | Every auto-run start/stop bumps settings version + litters change_log | none (stops file churn) |
| D5 | S-6 | No staleness check on candle data — stale tape scores instead of skipping | prevents garbage rows |

All five verified against the current tree on 2026-06-11 by the spec author. One new supporting finding (D2b, §3) strengthens the D2 recommendation.

---

## 1. D1 — Kelly inverse-contract sizing (H4)

### Current behaviour (verified)

[Core/ScoringEngine_Kelly.vb:94](../Core/ScoringEngine_Kelly.vb): `riskPerContractUsd = cfg.Kelly.ContractFaceUsd * stopDistanceUsd`. For a Deribit **inverse** contract, the USD loss on a $10-face contract over a $Δ move is ≈ `face × Δ / price` (cents), not `face × Δ` (tens of dollars). With the $50 risk cap (=$1,000 × 0.05), ≥1 contract needs a stop ≤ $5.00; real stops are $20–600 → **contracts always 0**, every run renders "< 1 contract (stop too wide)" — wrong message, wrong math. Also :45 — the verdict guard tests `"NEUTRAL"/"WAIT"`, strings that haven't existed for ~20 versions; dead code that misleads readers into thinking NO TRADE is suppressed (v30 intentionally renders the Lean Kelly on NO TRADE).

### Decision (recommended: fix the math + leverage cap)

**(a) — recommended.** Correct the denominator and make the output honest:

1. `CalcKellySizing` gains an `entryPriceUsd As Double` parameter. Both call sites ([UI/MainForm_Render_Header.vb:158](../UI/MainForm_Render_Header.vb), [UI/MainForm_PlaintextSnapshot.vb:143](../UI/MainForm_PlaintextSnapshot.vb)) pass `r.CurrentPrice`.
2. `riskPerContractUsd = cfg.Kelly.ContractFaceUsd * stopDistanceUsd / entryPriceUsd` (guard `entryPriceUsd <= 0` → exit).
3. Correct output is then thousands of contracts at $50 risk (e.g. stop $60 at $62,900: ≈$0.0095/contract → ≈5,260 contracts ≈ $52,600 notional ≈ 53× leverage on the $1,000 account) — so **leverage becomes the binding constraint and must be displayed and capped**: new key `kelly.max_leverage` (default **5.0**, conservative; Deribit BTC perp allows far more — the trader tunes). `maxContractsByLeverage = Floor(AccountSizeUsd × MaxLeverage / ContractFaceUsd)`; final contracts = min(risk-derived, leverage-derived); new `v.KellyLevCapped As Boolean` when the leverage cap binds.
4. Render: add a notional + implied-leverage line to the KELLY block (`≈ $N notional · n.n× lev`), `[LEV CAPPED]` tag when bound; the existing advisory label stays.
5. Delete the dead `"NEUTRAL"/"WAIT"` guard at :45 (behaviour unchanged — it never fired; keep the empty-verdict check).

**(b) — alternative if the trader prefers:** retire the contracts line entirely; keep f*/p(win)/risk-$ only. Less code, less information. The spec author recommends (a): a *correct* contracts+leverage line is a genuinely useful sanity check; the block's advisory framing already guards against reading it as a prescription.

Settings: `kelly.max_leverage` → **v32** bump (shared with D4). POCO `KellySettings.MaxLeverage As Double = 5.0` + XML doc.

---

## 2. D2 — Linear ATR levels + sizing-display relabel (S-1)

### Current behaviour (verified)

Every level computation is `r.ATR × norms.ATRScaleFactor × mult` — [ScoringEngine_Calculate_Verdict.vb:179](../Core/ScoringEngine_Calculate_Verdict.vb) (Step 5b cap base), [MainForm_Render_Header.vb:70-72](../UI/MainForm_Render_Header.vb), [MainForm_Render_Sections.vb:25-26](../UI/MainForm_Render_Sections.vb), [MainForm_PlaintextSnapshot.vb:46-47, 83-84](../UI/MainForm_PlaintextSnapshot.vb), [MainForm_Render_Cards.vb:845](../UI/MainForm_Render_Cards.vb) (`atrUnit`). Since `ATRScaleFactor = clamp(ATR/ATRRef, 0.25, 4.0)` ([DynamicNorms.vb:100](../DynamicNorms.vb)), distances scale **quadratically** with current volatility: at ATR = 2× reference the stop is 2.4×ATR, at half-reference 0.6×ATR. And the displayed `× N scale` is CurrATR/AvgATR — the **reciprocal** of the trader-profile sizing formula `Base × (AvgATR/CurrATR)`; applying the displayed number to the profile's rule sizes *up* on high-vol days.

### D2b — new supporting finding (verified this pass)

The evaluation pipeline never used the scale factor: [analysis/AnalysisConstants.vb](../analysis/AnalysisConstants.vb) defines the favourable barriers as `{0.5, 0.8}` / `{0.3, 0.5}` × **raw logged ATR** and the adverse fallback as `1.2 ×` raw ATR. So the perf strip and failure-rate matrix already measure **linear-ATR geometry** while the screen shows quadratic levels — whenever scale ≠ 1, the calibration metrics and the trader's display describe different levels. Linearising the display/cap math **reconciles the engine with its own evaluation pipeline**. (Standard ATR-level constructions — Wilder trailing stops, chandelier exits, Keltner-style bands — are all linear `k × ATR`; nothing standard squares the volatility response.)

### Required behaviour

1. **Distances become linear:** drop `norms.ATRScaleFactor` from every distance product above — `distance = r.ATR × mult`. The Step 5b cap base uses the same linear target. Multiplier values (1.2/2.0) unchanged; their review stays a WATCHING item on clean data.
2. **Sizing display relabelled to the profile's formula:** the ATR header line and the ATR card sub-header replace `× {scale:F2} scale` with `size ×{ATRRef/ATR:F2}` (the profile's `AvgATR/CurrATR` multiplier — reciprocal of the old display; derive at render from `norms`, clamp display to one decimal sanity). DYNAMIC NORMS section line (`{0:F2}x (ATR=… ref=…)`) is unambiguous in context — relabel its prefix to `ATR ratio:` and leave the value as is.
3. **`r.ATRSizeMultiplier` (CSV `ATRMultiplier` column) keeps its current meaning** (CurrATR/ATRRef, rounded) — CSV continuity; displays derive the reciprocal at render.
4. Kelly is insensitive to this change in its payoff ratio (`b = targetMult/stopMult`, scale-free) but its `stopDistanceUsd` input shrinks/grows with the linear stop — correct and intended.

Behaviour shift to expect: at high vol, displayed targets sit closer than before → structural caps (`CAPPED @`) fire somewhat less often; at low vol, levels stop compressing into noise. Logged `TargetCapReason` bucket frequencies shift — which is why this lands pre-collection.

---

## 3. D3 — VPFR-lite in the context-tag structural list (S-2)

[ScoringEngine_Calculate_Scoring.vb:43-53](../Core/ScoringEngine_Calculate_Scoring.vb): the structural set is {VWAP, BBW/TTM, EMA 9/21/50, DMI +/-DI, ADX>*, Donchian(20), 5m EMA(200)}; flow is {OFI, CVD, TFI, MicroCVD, OI Delta, ROC(9), Volume}. A verdict supported by `VPFR-lite` hits (the breakdown label — verified) counts toward neither, biasing runs toward STRUCTURALLY_WEAK / FLOW_UNCONFIRMED when VPFR is doing the structural work — and VPFR is on the preferred list precisely as structural reference.

**Fix:** add `lbl = "VPFR-lite"` to the structural set. One line. Display-only mechanism, but it shifts the logged `VerdictContext` distribution → pre-collection.

**Explicit non-changes:** RSI(9), Liquidations, Spread, Funding rows stay out of both sets (penalty/modifier rows, not "support"); the TREND STRUCTURE row also stays out for now — its hits only fire on dominance-agreeing bonus awards, and reclassifying it is a data question for the post-collection review, not a correctness gap.

---

## 4. D4 — Stop auto-run toggles churning settings.json (S-4)

[UI/MainForm_AutoRun.vb:41-45 and :73-75](../UI/MainForm_AutoRun.vb): `StartAutoRun`/`StopAutoRun` each call `SettingsLoader.Save(cfg, "auto_run …via UI")`, and `Save` unconditionally bumps `version` and appends to `change_log` ([SettingsLoader.vb:67-73](../Core/Settings/SettingsLoader.vb)). With collection sessions about to start, every start/stop inflates the version and buries real calibration entries. Compounding fact (verified): [MainForm_AutoRun.vb:16-17](../UI/MainForm_AutoRun.vb) deliberately ignores the saved `Enabled` at startup ("Always start in stopped state"), and **nothing anywhere reads `AutoRun.Enabled`** — persisting it buys literally nothing.

**Fix:**
1. `StopAutoRun`: remove the `Save` call entirely.
2. `StartAutoRun`: `Save` **only when the interval actually changed** vs `cfg.AutoRun.IntervalMinutes/Seconds` (preserves interval restore across app sessions — the one persisted value with a reader), changeNote `"auto_run interval changed via UI"`. No save on a plain start.
3. Remove the dead `auto_run.enabled` key from `settings.json` + the `Enabled` property from `AutoRunSettings` (no reader; v15-style dead-key cleanup). Rides the same **v32** bump as D1.

---

## 5. D5 — Candle freshness guard (S-6)

If Deribit serves a stale-but-valid chart response, the engine scores hours-old tape as if current (downstream, `GetSessionCandles` can even fall back to the full 250-candle list as the "session"). On a fresh dataset that's row pollution; conservative bias says skip.

**Fix:** host-agnostic pure helper (location: `Core/Indicators_Momentum.vb` or a small shared util — implementer's call, no WinForms coupling):

```vb
Public Shared Function IsFresh(candles As List(Of Candle), resolutionMinutes As Integer, nowUtc As DateTime) As Boolean
    ' Last bar older than 2× the resolution → stale tape. Candle.Timestamp is ms epoch (bar open).
```

Wire into the skip-gate chain at [MainForm_Analysis.vb:88-100](../UI/MainForm_Analysis.vb): after the count checks, `ElseIf Not IsFresh(candles1m, 1, …) Then skipReason = "1m candles stale"` and the 5m equivalent. **Gate 1m and 5m only** — the 15m cache deliberately tolerates staleness (documented design), and trade-list age is market-dependent (no gate). Threshold 2× resolution, hardcoded (structural constant, same class as the fetch counts).

---

## 6. Settings v32

- **Added:** `kelly.max_leverage` = 5.0 (D1).
- **Removed:** `auto_run.enabled` (D4; no reader — verified).
- `version` 31 → 32, `change_log` entry (newest first) referencing this proposal, `last_modified`/`modified_by` updated; POCO updates in `EngineSettings.vb` (add `MaxLeverage`, drop `Enabled`).
- No CSV schema change (stays v0.6). No eval-cache change.

## 7. Acceptance

- **Harness** (`verify/ordercheck/`, extend): **A10 Kelly** — entry 62,900 / stop distance 60 / HIGH confidence / defaults → assert `riskPerContractUsd ≈ 0.00954`, contracts = min(risk-derived, leverage-derived) with `max_leverage` 5.0 → 500 contracts ($5,000 notional, lev-capped) — implementer derives the exact expected values in the fixture comment; **A11 IsFresh** — fresh vs 3×-resolution-old fixtures; **A12 linear levels** — `Calculate()` fixture with `norms.ATRScaleFactor = 2.0` asserting the Step 5b raw target = `price + ATR × 2.0` (scale absent).
- `dotnet build` clean per commit.
- **Smoke via the self-screenshot loop** (`tools/README.md`): one live run — KELLY block shows non-zero contracts + notional/leverage line; ATR header reads `size ×N` with N = ref/ATR; no `[LEV CAPPED]` absurdities; toggle auto-run start/stop twice → `settings.json` version unchanged.

## 8. Sequencing, routing, out-of-scope

- **Commits:** D2 (levels+display) → D1 (Kelly + v32 with D4's key removal) → D4 → D3+D5 (small pair) — or any order the implementer prefers with one constraint: **v32 lands once**, carrying both key changes. §15 entry + this proposal marked IMPLEMENTED at the end.
- **Routing: Opus**, fresh conversation, this doc as the kickoff. No scoring votes/thresholds are touched; the one arithmetic item (Kelly) is fully specified. Local commits only; user smoke-tests, then pushes.
- **Collection restart:** serious auto-run accumulation starts once this lands. Rows logged before it carry old-geometry display semantics (harmless for eval — D2b — but mark the boundary mentally).
- **Out of scope:** S5 ledger guard (merged into Spec C, post-P5b), S-7 time-windowed momentum (own spec; changes signal cadence), any threshold tuning (recalibration phase), trend-structure context reclassification (§3).
