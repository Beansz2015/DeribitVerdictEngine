# Spec C (SC / TOTAL Parity + Ledger Guard) — Implementer Hand-off

**Date:** 2026-06-17 · **From:** coordinator / spec-author seat. · **To:** implementer (Opus, fresh conversation).
**Status:** **BUILD-READY.** The design is the approved proposal [`sc-column-total-parity-proposal.md`](sc-column-total-parity-proposal.md) (PROPOSED + the 2026-06-11 S5-ledger-guard amendment) — read it first; it is authoritative for the *why* and the capture idioms. **This doc supplies the one thing the proposal told you to refresh** (its emission-site inventory + line anchors drifted after the engine-correctness pass + Tier D) and confirms the rest against current source. Where a line number here and in the proposal differ, **this doc wins**. Scoring-adjacent but **bit-identical** by construction (additive attribution fields only); zero data coupling; local commits only, trader tests + pushes.

---

## 0. What this is

Make the SIGNAL BREAKDOWN card's **SC column sum to TOTAL** by giving each `SignalBreakdownItem` a signed `LongPoints`/`ShortPoints` attribution captured from the actual `state` delta as it fires, and have `ScForItem` return `LongPoints − ShortPoints` instead of the hit-derived ±1. Plus the **permanent S5 ledger guard** in `Calculate()`. No scoring math changes.

---

## 1. Refreshed emission-site inventory (supersedes proposal §4 — verified 2026-06-17)

```
Core/ScoringEngine_Calculate_Scoring.vb : 22 `New SignalBreakdownItem` sites   (scoring-contributing — the work)
Core/ScoringEngine_Calculate_Verdict.vb :  3 `New SignalBreakdownItem` sites   (ALL informational → points 0/0)
                                          ── 25 total (proposal said "23"; the correctness pass added 2 _Verdict rows)
```

**The 3 _Verdict sites are all the `"MTF Gate (15m)"` row** — `New SignalBreakdownItem("MTF Gate (15m)", False, False, res.MTFGateReason)`:
- `_Verdict.vb:44` and `:57` — emitted on the two **regime-veto early-return** paths.
- `_Verdict.vb:118` — emitted at **Step 4b** (the MTF gate veto).
All three are vetoes, not scoring contributors → emit with **`LongPoints = ShortPoints = 0`** (use the 6-arg constructor with `0, 0`, or leave informational). They do **not** affect the ledger sum.

**The 22 _Scoring sites** are the real work. Do **not** trust the proposal §4.1 line numbers (553/558/563/757/430-510 etc. — all drifted). **Grep `New SignalBreakdownItem` in `_Scoring.vb` and walk all 22 by label**, in the proposal's §4 recommended order. Capture points by the §3.4 before/after delta idiom at every site — never assume the magnitude. The special-attention sites (find by label, not line):
- **`"BBW/TTM"`** — BBW squeeze penalty `cfg.Scoring.BbwSqueezePenalty` (default 2) → `−2` on the penalised side.
- **`"Funding"`** (Step 3) + the Step 3b momentum row — **dual-side**, both points fields populated; **capture against the actual `ls`/`ss` delta that folds into `state`** (proposal §3.4 + R2 — the delicate one). Note correctness-pass **F7 caps the Step-3 funding boost at `regimeMax`** — the before/after capture respects this automatically; an assumed magnitude would not.
- **`"Regime Align (2c)"`** — `cfg.RegimeWeights.{Trending|RangeBound}.{AlignmentBonus,ConflictPenalty}`, cap-applied.
- **`"Trend Structure"`** — `StructureBonus` (default 1) on the agree branch; the EXPANSION / CONTRACTION / not-dominant branches are **informational → 0/0**.
- **`"OI Delta"`** (Pass 2b OI×CVD) — bonus/penalty as points.
- **Pass 2 partial→full upgrades** — carry only the *additional* delta (proposal §3.3 last bullet, R3); trace the actual `state.X +=` line.

---

## 2. Confirmed current anchors

| Site | Current location | Change |
|---|---|---|
| `SignalBreakdownItem` DTO | `Core/ScoringEngine_Types.vb:6-14` | Add `LongPoints`/`ShortPoints` (Integer, signed) + the 6-arg constructor; keep the 4-arg (defaults 0/0) — proposal §3.1. |
| `ScForItem` | `UI/MainForm_Render_Cards.vb:2766` | Return `it.LongPoints - it.ShortPoints` (proposal §3.5). |
| `ScForItemPrefix` | `UI/MainForm_Render_Cards.vb:2782` | Same. |
| TOTAL row | `UI/MainForm_Render_Cards.vb:2555` | **No change** — already `Long {v.LongScore}/{shownMax} | Short {v.ShortScore}/{shownMax}` (raw scores = the invariant target). Confirms §3.2. |
| `VerdictResult` | `Core/ScoringEngine_Types.vb:16+` | Add `Public Property LedgerMismatch As Boolean` (display-only; does not exist yet). |

SC column feeds from `MakeSignalRow(..., ScForItem(items, label))` at ~20 card sites (2839, 2857, …) — they need no change; they inherit the corrected `ScForItem`. **Audit the SC cell width** (`MakeBreakdownCell` / grid) for ±2 values; bump only if it clips (proposal §3.5 expects it fits).

---

## 3. The S5 ledger guard (amendment — permanent, all builds)

Add `Private Shared Sub CheckLedger(res As VerdictResult)` and call it **before every `Return res` in `Calculate()`** (`_Verdict.vb`) — including the regime-veto and MTF-block early returns (the same paths that emit the 3 MTF rows). Grep the `Return` sites in `Calculate()` to enumerate them; each early return is *after* `RunScoringPipeline` so the breakdown is fully populated and the invariant holds.

```
Σ items.LongPoints  = res.LongScore     (raw, through Step 3b — NOT effective/Step-4)
Σ items.ShortPoints = res.ShortScore
```
On mismatch (amendment point 2): set `res.LedgerMismatch = True`, `Console.WriteLine` a `[LEDGER_MISMATCH]` line with both sums + both scores, surface in the status-bar LOG line (same pattern as `SettingsLoader.LastLoadError`), and append a warning line to the output-dump block. **No CSV column** (CSV stays v0.6). Cost: one integer sum over ~25 items per run.

---

## 4. Display-parity (CLAUDE.md hard rule) — what's in scope

- The **SC column is card-only.** `BuildPlaintextSnapshot`'s SIGNAL BREAKDOWN shows `[L]/[S]` hits + Note, **not** a numeric SC column — so changing `ScForItem` has **no snapshot parity obligation**. Confirm this against the current `BuildPlaintextSnapshot` before commit (if it has gained an SC column, mirror the points there too).
- The **only new rendered surface is the ledger-mismatch warning** → it lands in the output-dump block (per §3) + the status LOG line. That's the card↔dump pair; no other renderer line is added/removed/renamed.

---

## 5. Acceptance

1. `dotnet build` solution **0/0**; **verify harness A1–A15 unregressed** (the DTO change is additive — the 4-arg constructor is preserved — so the harness should build + pass unchanged; confirm it does).
2. **Ledger guard silent**: run the app, auto-run 10+ cycles across ≥2 regime transitions → no `[LEDGER_MISMATCH]`. If one fires, the logged sums identify the missed/over-counted site (most likely a Steps 3/3b `ls`/`ss`-vs-`state` slip — R2).
3. **Hand-tally** (proposal §6.2): on 3 live runs in different regimes (ideally with BBW squeeze / Pass 2c alignment / funding penalty active), the positive SC values sum to TOTAL `Long N`, the |negative| to TOTAL `Short N`.
4. **CSV bit-identical regression (the critical check, §6.3):** 5 analyses before vs after → `LongScore`, `ShortScore`, `MaxScore`, `Verdict`, `Confidence`, `VerdictContext`, `EffectiveLongScore`, `EffectiveShortScore` **must be identical**. Any divergence = a delta-capture site contaminated `state` → fix before commit.

---

## 6. Commit checklist

- [ ] `ScoringEngine_Types.vb`: `LongPoints`/`ShortPoints` + 6-arg constructor on `SignalBreakdownItem`; `LedgerMismatch` on `VerdictResult`.
- [ ] `ScoringEngine_Calculate_Scoring.vb`: all **22** sites → 6-arg with before/after delta capture (or explicit 0/0 for informational).
- [ ] `ScoringEngine_Calculate_Verdict.vb`: the **3** MTF-gate rows → 6-arg `0, 0`; add `CheckLedger(res)` before every `Return res` in `Calculate()`.
- [ ] `MainForm_Render_Cards.vb`: `ScForItem` + `ScForItemPrefix` → `LongPoints − ShortPoints`; SC-cell width audit; ledger-mismatch → status LOG line.
- [ ] Output-dump + status surfacing of `[LEDGER_MISMATCH]`.
- [ ] Docs: `DeribitIndicatorProject.md` §15 new row; lock the decision in the current handover (`ui-reskin-handover-2026-05-27.md`, not the superseded -05-22) — "SC column = signed actual contribution (`LongPoints − ShortPoints`); parity invariant `Σ items = v.{Long,Short}Score`; STATE derivation still uses `LongHit/ShortHit`."
- [ ] Acceptance §5 all green; **CSV bit-identical**. Spec-back → coordinator.
- [ ] **Local commit only.** Single commit recommended (proposal §8 Option A + C — keep the 4-arg constructor; schedule its removal as a later cleanup once the guard is quiet).

**Model:** Opus, medium-high effort. Synthesis-heavy (25 sites + cap-aware delta + the dual-side funding `ls`/`ss`-vs-`state` distinction) — that distinction (R2) is the one place a careless pass corrupts the ledger; the CSV bit-identical check is the backstop.
