# Fee-Awareness Relay → DeribitOrderPlacementApp — 2026-07-27

**From:** the engine coordinator seat. **To:** the order-app orchestrator lane (paste-ready, the `session-policy-gate-proposal.md` handoff pattern). **Trigger:** Deribit fee change effective **2026-08-01** — maker **1.5 bps** / taker **3.5 bps** of notional (trader-supplied 2026-07-27).

## 0. What the engine is doing (context, no action needed)

Engine ships a fee-aware min-move floor (`fee-aware-min-move-proposal.md`, APPROVED 2026-07-27): the verdict-viability floor decomposes into `round_trip_fee_pct + min_net_move_pct`, byte-identical at defaults (0.0008). **The bridge contract does NOT change:** no fee fields enter the payload; `BELOW_MIN_MOVE` and every disposition token stay frozen. The order app owns its own fee constants — the duplicate-constant risk (engine settings vs order-app settings both carrying 1.5/3.5) is accepted and documented on both sides; a schedule change is a two-repo settings touch, coordinated by relay like this one.

## 1. Rec — EV-aware chase budget (enhances the existing min-ATR-slippage stop)

Today the reposition loop cancels when the chase exceeds a fixed **0.6 ATR** from initial placement — a *structural* bound that doesn't know that every tick of chase comes directly out of the trade's net move (0.6 ATR into a 1.75-ATR target sacrifices ~a third of the move before fees).

**Add an EV stop condition alongside the ATR cap** (whichever binds first stops the chase):

```
stop repositioning when:
  remaining_net = |placed_target − reprice_price| − round_trip_fee_pct × price
  remaining_net < min_net_move_pct × price
```

- `placed_target`, `price`, `atr` are already in the v1 payload (`levels.*`) — **no contract change**.
- `round_trip_fee_pct` = 3.0 bps (maker entry + maker TP, the trader's standing flow).
- `min_net_move_pct` = an order-app-local preference knob mirroring the engine's new one (trader-editable, like the existing 0.6-ATR field. Do NOT read it from the engine — no coupling; the two knobs may legitimately differ: the engine's gates signal viability at emission, the order app's gates residual viability at fill time).
- Keep **0.6 ATR as the outer cap** — it still bounds structural drift on wide-target signals where the EV condition is slack.
- **Disposition token:** a chase stopped by the EV condition should log a distinct disposition (suggest `cancelled: ev_floor`, joining the `rejected:` / `refused: policy` family) so the counterfactual stays measurable at soak-style joins. Additive token — per the stable-identifiers rule, adding is free, document it as policy-targetable.

Serves the manual flow too: the same arithmetic is the trader's own "has this chase stopped being worth it" number.

## 2. Rec — SL-emergency threshold absorbs the maker→taker delta

The emergency path (triggered SL repositioned until it passes the threshold, then taker) now costs **+2.0 bps** vs the maker exit it replaces. Fold that delta into the threshold's break-even math so "give up repositioning and cross" prices the crossing correctly. Small, arithmetic-only.

## 3. Non-asks

No bridge schema change · no new engine coupling · no change to the trader's maker-first execution policy · effective-date alignment: land with (or before) the Aug-1 schedule, order-app-side sequencing at the orchestrator's discretion.
