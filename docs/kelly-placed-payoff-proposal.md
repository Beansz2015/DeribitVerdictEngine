# Proposal — Kelly payoff ratio from the placed levels, net of fees

**Written:** 2026-09-17 (UTC) by the MEDIUM-tier diagnosis session 2 seat, trader-directed. **Status:** PROPOSAL, for orchestrator review and queueing. Nothing is built. **Context:** [`docs/medium-tier-diagnosis-read-2026-09-17.md`](medium-tier-diagnosis-read-2026-09-17.md) section 3, fix class `F-4a`; [`docs/kelly-est-honesty-decision-2026-08-02.md`](kelly-est-honesty-decision-2026-08-02.md); [`docs/kelly-w6-4-batch-summary.md`](kelly-w6-4-batch-summary.md) (the per-session payoff correction of 2026-09-09).

**Legend. IDs used in this doc:**

| ID | Source and kind | Meaning |
|---|---|---|
| `K-1` … `K-4` | This doc, decisions (section 4) | Decisions for the trader, each with my read |
| `F-4a` | Fix class, `docs/medium-tier-diagnosis-read-2026-09-17.md` section 3 | Flatten tiers for Kelly sizing only; payload `confidence` untouched |
| `Q-1` | Decision, `docs/medium-tier-diagnosis-spec-back.md` section 3 | What to do with the tier ladder |
| b | Kelly's payoff ratio | Reward distance ÷ risk distance |

---

## 0. Implementer recommendation (read first)

| Item | Detail |
|---|---|
| **Model / effort** | **Sonnet, high.** One pure function changes (`Core/ScoringEngine_Kelly.vb`) and its single call site (`UI/MainForm_PlaintextSnapshot.vb:149`). The levels already exist in one shared function (`SignalEmitter.ComputeSideLevels`), and the fee-aware ratio already has a template (`BuildNetRRLine`, `UI/MainForm_Render_Cards.vb:39-60`) |
| **Where it will slip** | (1) **Display-string parity:** the advisory line "R:R uses ATR multiples, not structural targets" renders on BOTH the snapshot and the Kelly card; both must change in one commit (`CLAUDE.md` parity rule). (2) **Side selection on NO TRADE leans:** Kelly renders a lean Kelly on "NO TRADE [WEAK LONG]"; "[TIE]" has no side. (3) **Fixture literals:** a fixture that hardcodes 1.75 / 1.6 must say whether it asserts shipped behaviour (derive from cfg) or mechanism |
| **Escalate to Opus, high** | If the placed levels for the Kelly side can be empty or wrong-sided on a row the display shows, or if the call-site ordering (snapshot before card bind) must move |
| **Reserved class** | **Moves a rendered value** (Kelly card, snapshot, payload advisory `kelly` block). No scoring change, no settings key if `K-2` is (a), no CSV change. **The payload `confidence` field is not touched** |

---

## 1. The problem

- **Kelly's b is a fixed global ratio:** `atr_target_multiplier` 1.75 ÷ `atr_stop_multiplier` 1.6 = 1.094 (`Core/ScoringEngine_Kelly.vb:77-78`).
- **Its stop distance is ATR × 1.6** (`UI/MainForm_PlaintextSnapshot.vb:45-48`).
- **The trade the engine actually places does not use that geometry.** The placed target comes from the swing, HVN, POC or fallback ladder. The fallback multiplier is per session (LONDON 2.0, ASIA 1.25). The placed stop comes from the structural stop ladder (`SignalEmitter.ComputeSideLevels`, the function the CSV `Placed*` columns and the bridge payload levels read).
- **Kelly ignores fees.** At a 3.00 bps maker/maker round trip and stops of 11–14 bps, fees are a large share of the risk.
- **Already found once:** the 2026-09-09 review corrected ASIA's breakeven from the global 47.76 % to 56.14 % (b 0.781). The live Kelly still uses the global b.

## 2. Evidence (population rows, placed levels)

Source: `backtest_data/swing-fallback-read/diagnosis-rows.csv` (`SwingFallbackRead --mode diagexport`), 8,810 trading-week directional rows. Net b = (target − 3 bps) ÷ (stop + 3 bps).

| Session | Rows | Gross placed b p10 / p50 / p90 | Net placed b p10 / p50 / p90 | Breakeven win rate at median net b | Kelly today (global gross b) |
|---|---|---|---|---|---|
| NY | 5,417 | 0.94 / 1.09 / 1.85 | 0.50 / 0.71 / 1.12 | 58.5 % | b 1.094, breakeven 47.8 % |
| LONDON | 1,665 | 0.85 / 1.25 / 1.84 | 0.52 / 0.81 / 1.20 | 55.3 % | same |
| ASIA | 1,728 | 0.78 / 0.78 / 1.82 | 0.40 / 0.55 / 1.27 | 64.5 % | same |

- **The ratio varies widely per row** (p10 to p90 roughly doubles), because 46–58 % of rows carry a swing or HVN target (NY 47 %, LONDON 46 %, ASIA 58 %).
- **Fees move the breakeven by about 10 to 17 points.** This is the largest single error in b.
- The median gross ratio matches the session fallback (NY 1.09, LONDON 1.25, ASIA 0.78), so a per-session static b would fix the median but not the spread or the fees.

## 3. Proposed change

| Input | Today | Proposed |
|---|---|---|
| Reward distance | ATR × 1.75 | **The placed target distance** for the Kelly side, from `ComputeSideLevels(v, r, cfg, isLong)` |
| Risk distance | ATR × 1.6 | **The placed stop distance**, same call |
| Fees | Ignored | **Round-trip fee in price terms** (`scoring.trade_costs.round_trip_fee_pct` × entry): b = (reward − fee) ÷ (risk + fee), the same formula `BuildNetRRLine` uses |
| Stop distance for contract sizing | ATR × 1.6 | **The placed stop distance** (the loss the order app's stop would realise) |
| Kelly side | Dominant verdict side | Unchanged: the verdict or lean side. "[TIE]" suppresses |
| Win probability | Tier map (0.65 / 0.55 / 0.45) | **Unchanged by this proposal.** See `K-3` |

- **Why the placed levels, and not a different ATR basis:** they are the levels the bridge payload sends and the order app would place. Any ATR multiple is a model of that trade; the placed levels are the trade. ATR still enters wherever the ladder falls back to ATR.
- **Suppression rule unchanged:** net b ≤ 0 (target inside the fee) or f* ≤ 0 → no Kelly block.

## 4. Decisions queued

### K-1 — the payoff basis (trader; reserved: rendered value)

| Option | b | Records |
|---|---|---|
| (a) Placed levels, net of fees | Per row, dynamic | The actual trade and its cost |
| (b) Placed levels, gross | Per row, dynamic | The actual trade, cost ignored |
| (c) Per-session fallback multipliers, net of fees | Static per session (NY 1.094, LONDON 1.25, ASIA 0.781 gross) | The median trade only |
| (d) Keep the global 1.75 / 1.6 | Static global | Neither |

- **Read (hypothesis): (a).** It is the most truthful option and the only one that describes the order the bridge sends. (b) and (c) are cheaper and record less; (d) is the known-wrong state.

### K-2 — does this need a settings key? (trader; reserved if yes)

- **Options:** (a) no key: the fee and levels already live in `settings.json`; (b) a `kelly.payoff_basis` switch for rollback.
- **Read (hypothesis): (a).** A git revert rolls back a display-only change. A switch adds a version bump and a mid-`InstanceId` edge for no data benefit.

### K-3 — sequencing with the win-probability problem (trader)

- **Fact:** with a correct b, Kelly still takes its win probability from the tier label. At the NY median net b (0.71): HIGH (0.65) still sizes (f* = +0.157, half-Kelly capped at 5 %); MEDIUM (0.55) and LOW (0.45) suppress (f* −0.084 and −0.325). Measured success rates (40–48 %) sit below that breakeven (58.5 %) in every readable tier, so with a measured win probability Kelly would suppress on most rows.
- **Options:** (a) ship this change alone now; (b) ship it together with `F-4a` (one flat, measured win probability) after `Q-1` option (d) measures payoff and success rate by tier; (c) wait.
- **Read (hypothesis): (a).** It removes an error that is measured and independent of the tier question. The display becomes more conservative, not less. ⚠ (b) records more in one step; I pick (a) because (b) is gated on a measurement that has not started, and waiting leaves a known overstatement on screen.

### K-4 — the advisory wording (trader; both surfaces)

- The line "Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets." becomes false. **No wording chosen here.** The trader set the current Kelly strings on 2026-08-02; the new line needs the same sign-off.

## 5. Acceptance (for the implementer's spec)

- A fixture on a row with a swing target: b equals (placed target distance − fee) ÷ (placed stop distance + fee), derived from cfg, not literals.
- A fixture where the placed target sits inside the fee: Kelly suppresses.
- A fixture on "NO TRADE [TIE]": Kelly suppresses.
- Parity: the snapshot and the Kelly card render the same b-derived values; the payload `kelly` block equals the rendered values.
- `confidence` in the payload: unchanged for every fixture state (assert it).

## 6. What I verified, and what I did not

| Verified | How |
|---|---|
| Kelly's b and stop inputs | Read `Core/ScoringEngine_Kelly.vb:77-78` and `UI/MainForm_PlaintextSnapshot.vb:45-48`, `:149` |
| The placed-level function is shared by the CSV, payload and snapshot | Read `UI/MainForm_PlaintextSnapshot.vb` B4b comment and `Core/SignalEmitter.vb:163-169` |
| The fee-aware formula template exists | Read `UI/MainForm_Render_Cards.vb:39-60` |
| The section 2 table | Computed from `diagnosis-rows.csv` (placed target and stop in bps, fee 3.00 bps) |

- **Not verified:** that `ComputeSideLevels` gives valid levels for the lean side on every NO TRADE row that renders Kelly (the table covers directional rows only); the order app's use of the `kelly` block (this repo's docs say advisory only); whether the stop multiplier differs by session anywhere in the Kelly path.
