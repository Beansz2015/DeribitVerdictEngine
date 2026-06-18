# Spec C (SC / TOTAL Parity + Ledger Guard) — Implementation Spec-Back

**Date:** 2026-06-18 · **From:** implementer (Opus, fresh conversation). · **To:** coordinator seat (independent review).
**Status:** **IMPLEMENTED — local commit only.** Solution + AutoTweaker + verify harness all build **0/0**; **A1–A15 green** (39 checks ALL PASS, unregressed). Scoring is **bit-identical by construction** (additive attribution only; no existing scoring line altered). I did **not** push — the trader runs the live CSV-diff + ledger-quiet check and pushes (CLAUDE.md commit gate).

Authoritative inputs: `sc-column-total-parity-implementer-handoff.md` (the *how* — inventory + anchors) + `sc-column-total-parity-proposal.md` (the *why* + capture idioms + S5 amendment). Where they differed, the hand-off won.

> **Coordinator review (2026-06-18, sanity-check seat) — APPROVED; cleared for the trader's live gate + push.** Independent verification:
> - **Re-ran builds + harness:** solution + harness **0/0**; **39 checks (A1–A15g) ALL PASS**, unregressed.
> - **Scoring bit-identical — statically PROVEN (stronger than the 5-run diff).** Audited every removed line in `_Scoring.vb`/`_Verdict.vb`: the only changes are (a) additive `pL`/`pS` snapshots + per-row accumulators, (b) the 4-arg→6-arg `breakdown.Add` swaps, and (c) the Pass 2 upgrade lines — each `state.X += 1` is **preserved identically**, only `: accumLP += 1` appended (verified all 9 pairs). **No `Math.Min(…, regimeMax)` cap and no `ls`/`ss` mutation was altered** (none appear in the diff — funding is captured via read-only snapshots). So no scoring line's behaviour changes in *any* regime.
> - **The clamp removal (trader-flagged): intended, authorized, required — NOT a scoring change.** It's a 2-line **display** clamp (`If sc > 1 Then sc = 1 / If sc < -1 Then sc = -1`) in the card helper `BuildRowDmiAdx`, on the SC-cell display value only. The engine emits DMI +/-DI (+1) **and** ADX> (+1) = **+2** to the real score; the clamp forced the displayed cell to ±1, under-reporting by 1 — the exact discrepancy Spec C exists to fix. Handover §4 rows 77/151/171 explicitly deferred this clamp's removal to Spec C as "the architectural fix." Mandatory for SC=TOTAL parity; the scoring `regimeMax` caps are untouched.
> - **§5.1 caveat acknowledged (correct, not a bug):** with a single *net* SC column, the proposal §6.2 split hand-tally can't be exact for dual-side rows (BBW net 0; funding) or dominant-side penalties. The authoritative check is what `CheckLedger` enforces — `Σ LongPoints = LongScore` / `Σ ShortPoints = ShortScore` summed **separately** — or the net `Σ SC = TOTAL Long − TOTAL Short`. The proposal's §6.2 was the imprecise part, not the implementation.
> - **Confirmed:** §15 post-v37 row + the handover §4 locked decision (row 151) both landed.
> - **Remaining (trader gate, as designed):** the live 5-run CSV bit-identical diff + the ledger-quiet watch over 10+ cycles across ≥2 regimes. Given the static proof + the permanent `CheckLedger`, these are belt-and-suspenders. Then push.

---

## 0. What shipped

`SignalBreakdownItem` carries signed `LongPoints`/`ShortPoints`; every emission captures its **actual cap-aware `state` delta**; `ScForItem`/`ScForItemPrefix` return `LongPoints − ShortPoints` so the card SC column sums to TOTAL; a permanent engine-side `CheckLedger` reconciles the sum before every `Return` in `Calculate()`. No scoring math changed; no settings keys; CSV stays v0.7.

---

## 1. The one structural decision the coordinator should eyeball

**The proposal's §3.4 before/after idiom assumes the mutation is adjacent to the `breakdown.Add`. In this codebase it is not.** All `state` mutations happen up top (Step 2 / Pass 2 / 2b / 2c / 2c-struct, lines ~134–577); all 22 `breakdown.Add` calls are at the bottom (~645–770). A snapshot taken *around the Add* would capture nothing.

So I used **per-row point accumulators** (`rocLP`/`rocSP`, … `fundLP`/`fundSP`) declared at the top of `RunScoringPipeline`, captured at each **mutation** site with a reusable `pL`/`pS` snapshot pair, and consumed at the Add sites. Same end result the proposal intends (actual cap-applied delta, never an assumed magnitude) — just threaded through the function because the structure forces it.

**Why this is safe — the invariant is robust to per-row misattribution.** `CheckLedger` checks the **sum** `Σ LongPoints = LongScore`. That sum is invariant to *which* row a delta is attributed to — it only breaks if a mutation is captured **zero times** (missed site → sum too low) or **twice** (double-count → sum too high). I walked every `state.LongScore`/`state.ShortScore` mutation and confirmed each sits inside **exactly one** capture region (each region resets `pL`/`pS` immediately before its mutation and reads the delta immediately after, with no intervening mutation; Pass 2 upgrades use inline `: accum += 1` co-located with the existing `state.X += 1`). No gaps, no overlaps. Per-row *display* accuracy (correct row label gets the points) is the hand-tally's job, not the ledger's.

---

## 2. Capture map (where each row's points come from)

| Row | Mutation sites captured | Idiom |
|---|---|---|
| ROC(9) | `AddFull` (Step 2) + Pass 2 upgrade | snapshot + inline `+= 1` |
| RSI(9) | `AddFull` + RSI-divergence penalty + Pass 2 upgrade | snapshot spans AddFull→penalty |
| DMI +/-DI, ADX>, EMA 9/21/50, 5m EMA(200) | `AddFull` | snapshot |
| Volume | `AddFull` (full) + volMid Pass 2 upgrade | snapshot + inline |
| VWAP | `AddFull` (non-warmup) + Pass 2 upgrade | snapshot inside the Else + inline |
| BBW/TTM | squeeze penalty (both sides, `−BbwSqueezePenalty`) OR BULL/BEAR building `+1` | snapshot spans the whole `Select Case` |
| OFI | `AddFull` + OFI-momentum modifier (cap/suppress) | snapshot spans AddFull→momentum block |
| CVD | `AddFull` + CVD-divergence penalty | snapshot spans both |
| TFI | `AddFull` | snapshot |
| MicroCVD | `AddFull` + decel penalty + FLAT-stall penalty | snapshot spans all three |
| Liq Penalty | LONG/SHORT liq penalty | snapshot spans the If |
| Spread | WIDE penalty (long / short / both) | snapshot spans the If |
| OI Delta | `AddFull` + OI Pass 2 upgrade + **Pass 2b OI×CVD** | snapshot (AddFull) + inline (upgrade) + snapshot (Pass 2b) |
| Donchian(20), OBV | `AddFull` + Pass 2 upgrade | snapshot + inline |
| VPFR-lite | `AddFull` + VA Pass 2 upgrade | snapshot + inline |
| Regime Align (2c) | Pass 2c bonus/penalty (cap-applied) | snapshot spans the whole `If` |
| Trend Structure | Pass 2c-struct bonus (cap-applied); EXPANSION/CONTRACTION/not-dominant → 0 | snapshot spans the whole `If` |
| Funding (info) | **Steps 3 + 3b** on `ls`/`ss` | `fundBaseL/S = ls/ss` at the snapshot, `fundLP = ls − fundBaseL` after Step 3b |
| MTF Gate (15m) ×3 (`_Verdict.vb`) | veto, not a contributor | explicit `0, 0` (6-arg) |

**The R2 trap (Steps 3/3b funding).** Handled per §3.4: `ls`/`ss` start equal to `state.LongScore`/`state.ShortScore` at the post-Pass-2c snapshot (the existing `ls = state.LongScore` line). I stamp `fundBaseL/fundBaseS` there and read `fundLP = ls − fundBaseL` after the final `ls = Math.Max(0, ls)` at the end of Step 3b. This captures the **net** funding delta including the F7 `Math.Min(…, regimeMax)` boost cap and the two floor clamps — exactly because it's a before/after over the real locals, not an assumed `−penalty/+boost`. The Funding row's `LongHit/ShortHit` stay `False` (unchanged); only the points fields are now populated.

---

## 3. Surfaces touched

| File | Change |
|---|---|
| `Core/ScoringEngine_Types.vb` | `SignalBreakdownItem`: `LongPoints`/`ShortPoints` + 6-arg constructor (4-arg preserved → 0/0). `VerdictResult.LedgerMismatch As Boolean` (display-only). |
| `Core/ScoringEngine_Calculate_Scoring.vb` | 22 emission sites → 6-arg with per-row accumulators captured at mutation sites. No scoring line altered. |
| `Core/ScoringEngine_Calculate_Verdict.vb` | 3 MTF rows → 6-arg `0, 0`; new `Private Shared Sub CheckLedger(res)`; `CheckLedger(res)` before all **4** `Return res` (2 regime-veto + 1 MTF-block early returns + the final return). |
| `UI/MainForm_Render_Cards.vb` | `ScForItem`/`ScForItemPrefix` → `LongPoints − ShortPoints`. **Removed the `BuildRowDmiAdx` `[-1,+1]` clamp** (explicitly deferred to Spec C in handover §4 row 170) — the combined DMI+ADX row now shows the true ±2. |
| `UI/MainForm_Analysis.vb` | Sets `_ledgerWarn` from `verdict.LedgerMismatch`; appends a `[LEDGER_MISMATCH]` line to the dump snapshot on mismatch. |
| `UI/MainForm_Layout.vb` | `_ledgerWarn` field; `UpdateLogInfo` prepends it to the LOG line (same pattern as `LastLoadError`). |
| `docs/DeribitIndicatorProject.md` §15 | New `post-v37` code-commit row. |
| `docs/ui-reskin-handover-2026-05-27.md` §4 | New locked row (SC = signed contribution; parity invariant; resolves the DMI/ADX deferral). |

---

## 4. Acceptance

1. **Build 0/0** — solution + AutoTweaker + `verify/ordercheck`. ✅
2. **A1–A15 unregressed** — 39 checks ALL PASS (additive DTO; 4-arg constructor preserved). ✅
3. **CSV bit-identical** — *the trader's live gate.* Guaranteed by construction: no existing scoring mutation line was changed; every added line either reads `state` into a snapshot temp, writes a new accumulator var, or appends `: accum += 1` inside an existing single-line `If` (fires iff the existing `state.X += 1` fires). I could not run the live app (Deribit network + WinForms), so the empirical 5-run diff is the trader's step — but `LongScore`/`ShortScore`/`Verdict`/etc. cannot change.
4. **Ledger quiet** — *the trader's live gate.* `CheckLedger` is in the engine and fires on every `Calculate`; recommend the trader watch the console + LOG line over 10+ auto-run cycles across ≥2 regime transitions. If `[LEDGER_MISMATCH]` ever logs, the sums in the line identify the missed/over-counted site.

---

## 5. Two things the coordinator should know (caveats, not bugs)

**5.1 The hand-tally (proposal §6.2 / hand-off §5.3) is approximate where dual-side or footer-aggregated rows exist.** The SC column is a single **net** value (`LongPoints − ShortPoints`). The hand-tally heuristic "sum positive SC → TOTAL Long, sum |negative| → TOTAL Short" holds cleanly only for single-sided rows. It does **not** hold for:
- **Dual-side rows** — BBW squeeze (`−2` to *both* sides → net SC 0, but both scores were reduced); Funding (penalty one side + boost the other).
- **Penalties on the dominant side** — a long-side liq/spread penalty is a *negative Long* contribution but reads as negative SC (the heuristic would count it toward Short).
- **Rows with no card SC cell** — `Regime Align (2c)` has no dedicated card row (Pass 2c surfaces via the breakdown **footer** aggregate, not an SC cell); `VWAP Bands`, `OFI Mom`, `Funding Mom` card rows have no matching engine item so their SC is `—`.

The **authoritative** invariant — and what `CheckLedger` enforces — is `Σ items.LongPoints = LongScore` and `Σ items.ShortPoints = ShortScore` over the **full engine item list**, summing the two fields **separately**. That always holds. A cleaner single-column check than §6.2's split is **`Σ (SC over all rows) = TOTAL Long − TOTAL Short`** (net), which is exact. I did not change the proposal's stated verification, just flagging that a perfect visible column-sum was never achievable with one net column + footer-aggregated Pass 2c. No engine inaccuracy is implied.

**5.2 SC cell width — no change needed.** New magnitudes are ±2 (BBW, Pass 2c, combined DMI/ADX), occasionally ±3 (dual-side funding net). All are ≤ 2 characters (`+2`, `-3`), identical width to the old `±1`. `MakeBreakdownCell` is 30 px right-aligned with `AutoEllipsis` — fits with margin. Not bumped.

---

## 6. Deferred (per hand-off §6 / proposal §8 Option A + C)

- **4-arg `SignalBreakdownItem` constructor kept** — used by the 3 informational MTF rows (explicit `0, 0` would also work; left as a clean default). Its removal (forcing every site explicit) is a later cleanup once the guard has been quiet for a week of live runs, per proposal §7 R8.
- Single local commit (Option A). No push.

---

**End of spec-back. Routing to the coordinator seat for independent review.**
