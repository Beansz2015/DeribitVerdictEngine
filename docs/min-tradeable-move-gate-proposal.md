# Minimum-Tradeable-Move Gate + Editable Floor (Proposal)

**Date:** 2026-06-14
**Author:** Opus 4.8 (spec-author seat)
**Status:** **APPROVED in principle** — trader requested both the gate and UI-editability. **Scoring change** (forces NO TRADE) → spec-first + approval-gated; this doc is the approval artifact. **Settings v34 → v35.** Pairs with `eval-metric-deconfound-proposal.md` (shared floor key).
**Implementer:** Opus, fresh conversation. Verify anchors before editing.

---

## 0. Goal — two linked trader asks, one floor

1. **Scoring gate:** emit NO TRADE when the realistic take-profit target can't clear the minimum tradeable move (0.08% ≈ $50 at $62k — sized to clear slippage). Trader: "I would not take the trade anyway; ATR 13 definitely will not work."
2. **Editability:** the 0.08% floor is editable via the interface, not hardcoded.

Both unify around **one editable settings key**, shared with the eval-metric de-confound so measurement (how past trades are judged) and behaviour (which trades are emitted) use the *same* floor.

## 1. The shared settings key (editability)

`scoring.min_tradeable_move_pct: 0.0008` (0.08%, price-relative so it tracks BTC with no recalibration).

- POCO: `ScoringSettings.MinTradeableMovePct As Double = 0.0008` + XML doc (minimum take-profit distance as a fraction of entry; trades whose realistic TP is closer are NO TRADE; sized to clear slippage; trader-owned, not auto-tuned).
- **Hot-reloadable** via the existing `FileSystemWatcher` → editing `settings.json` is live immediately (minimal editable path; the trader can edit the file directly).
- **UI control:** a labelled numeric input — display in **% (0.08)**, store the fraction (0.0008); validation `0 < x ≤ 0.01` (0–1%); persists via `SettingsLoader.Save`. Place it in a **settings dialog** (e.g. alongside `OutputDumpSettingsForm` / the SETTINGS & TOOLS surface), **not** a reskin card — keep it off the card grid to avoid the card-parity machinery and the locked-layout rules. Exact placement is the UI implementer's call; the requirement is "editable, validated, persisted, %-labelled."
- **OFF the auto-tweaker tunable surface** — trader risk preference, never auto-adjusted (same exclusion class as the `kelly` account/risk keys).
- **Consumed by:** this gate (§2) **and** the eval de-confound (amended to read this key instead of an `AnalysisConstants` const).

## 2. The gate (scoring) — gate the *effective* target

At the **end of `ScoringEngine.Calculate()`, after Step 5b** (so the capped targets exist), for a **directional** verdict on the dominant side:

```
rawTargetDist = r.ATR * cfg.Scoring.AtrTargetMultiplier              ' linear (post-D2)
effTarget     = If(AdjustedTarget(side) > 0, AdjustedTarget(side), CurrentPrice ± rawTargetDist)
effDist       = Abs(effTarget - r.CurrentPrice)
floor         = cfg.Scoring.MinTradeableMovePct * r.CurrentPrice
If effDist < floor Then  → override verdict to NO TRADE (low tradeable move)
```

- Uses the **effective (post-cap) target** — most faithful to "my TP must clear $50." It catches **both** causes: low ATR (small ATR target) *and* a near structural swing (the cap pulls TP below the floor). Both are trades the profile says skip.
- **Scores, breakdown, and the computed levels are preserved** for display — the override sets the verdict to NO TRADE but the run still shows *why* (the sub-floor target is visible). Mirror the existing veto pattern (MTF block): compute, then override, then return.
- **Design alternative (offered, not recommended):** gate the *raw ATR target only* (`AtrTargetMultiplier × ATR < floor`) — simpler, but ignores the near-swing case, so a directional call with a 30-point capped TP would still fire. Recommend the effective-target form (A); fall back to ATR-only (B) only if the trader prefers maximum simplicity.

## 3. Surfacing (engine display-string parity rule applies)

NO TRADE must carry a clear reason so it's distinguishable from a weak-signal NO TRADE. Reuse `VerdictContext` (e.g. a new value `BELOW_MIN_MOVE`) **or** an `AppendLean`-style verdict suffix — **no new CSV column** (`VerdictContext` is already logged). Whatever surface is chosen must render consistently in the card **and** `BuildPlaintextSnapshot` in the same commit (the snapshot↔card parity rule from the P5b spec-back / CLAUDE.md).

## 4. Behaviour-change honesty (state plainly to the trader)

At ~$62k in the current low-vol regime this **suppresses a large fraction of verdicts** — Asia (ATR ~13) goes mostly NO TRADE; chunks of NY too (gate bites below ATR ~25 at targetMult 2.0). That is the intent ("trade like you do"), but the consequences are real:
- **Slower data accumulation in low vol** (fewer evaluated trades). Acceptable — the removed trades are the non-tradeable ones.
- **Cleaner calibration dataset** — strips low-ATR noise trades, net-positive for the v34 re-baseline and the auto-tweaker.
- **Verdict distribution shifts toward NO TRADE** — expected; the next re-baseline reads a gate-filtered book.

## 5. Interaction with the eval de-confound (complementary, shared key)

- **Gate (this)** stops the engine *emitting* sub-floor-target trades → they become NO TRADE, never logged as trades.
- **De-confound (`eval-metric-deconfound-proposal.md`)** floors the eval favourable barrier → re-bases *history* (already-logged sub-floor trades) and acts as a backstop.
- Once the gate is live, the eval floor rarely binds going forward (the gate already removed sub-floor trades) — but both must read the **same key** for consistency.
- **Sequencing:** land **both before the supervised auto-tweaker first fire** (the de-confound so the tweaker's metric is clean; the gate so the post-fire collection is already filtered). Recommend implementing them as a **paired v35** (shared key, one settings bump). The de-confound's "must precede first fire" caution (v34 brief) extends to this gate.

## 6. Settings v35

- Add `scoring.min_tradeable_move_pct: 0.0008`. `version` 34 → 35, change_log (newest-first), §15 row.
- POCO field in `ScoringSettings`. Amend the de-confound to source the floor from `cfg.Scoring.MinTradeableMovePct` (drop the planned `AnalysisConstants.FavBarAbsFloorPct` const, or keep it only as the POCO default mirror).
- **No CSV schema change.**

## 7. Acceptance (harness A13 + checks)

- **A13a low-ATR veto:** `Calculate()` with directional scores, `r.ATR = 13`, price 62,000, no structural cap → NO TRADE with the sub-floor reason (raw target 26 < floor 49.6).
- **A13b passes when tradeable:** same with `r.ATR = 30` (target 60 > 49.6) → directional verdict stands.
- **A13c near-swing veto (validates effective-target choice A):** `r.ATR` high but a capped target 30 points from entry (< floor) → NO TRADE.
- **Editability:** set `min_tradeable_move_pct = 0.0004` → A13a now *passes* (floor 24.8 < target 26); confirms the key drives the gate and hot-reload works.
- `dotnet build` clean; surfacing renders in card + snapshot.
- Sanity on recent live data: low-ATR rows now resolve NO TRADE.

## 8. Routing

Opus, fresh conversation; **pair with `eval-metric-deconfound-proposal.md`** (shared key, one v35 bump, both before the first fire). Scoring change — this spec is the approval artifact. Local commits only; trader tests + pushes.
