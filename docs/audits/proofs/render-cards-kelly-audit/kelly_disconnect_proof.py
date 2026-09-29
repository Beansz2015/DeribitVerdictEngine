#!/usr/bin/env python3
"""
Proof: Kelly sizing (Core/ScoringEngine_Kelly.vb::CalcKellySizing) computes its
payoff ratio b from the FIXED cfg.Scoring.AtrTargetMultiplier / AtrStopMultiplier
ratio, never from the actual placed structural geometry
(Core/SignalEmitter.vb::ComputeSideLevels / ComputeStructuralSideLevels).

This script re-implements both formulas verbatim from the VB source and runs
them against the LOGIC TRACE scenario (ATR=120, STRONG LONG, real placed R:R
0.39) using the live settings.json values, to show the two numbers the trader
sees on screen (and that ship in verdict_signal.json) are computed from two
different, disconnected geometries.

Source of the formulas reproduced here:
  - Core/ScoringEngine_Kelly.vb:64-122   (CalcKellySizing)
  - settings.json:425-484                (scoring.atr_*_multiplier, kelly.*)
"""

# ---- settings.json values (repo root, lines 425-484), read at audit time ----
ATR_STOP_MULTIPLIER = 1.6
ATR_TARGET_MULTIPLIER = 1.75
ACCOUNT_SIZE_USD = 1000.0
USE_HALF_KELLY = True
MAX_RISK_FRACTION = 0.05
CONTRACT_FACE_USD = 10.0
EST_PROB_FLOOR = 0.45
EST_PROB_SCALE = 0.20
MAX_LEVERAGE = 5.0

# ---- LOGIC TRACE scenario ----
ATR = 120.0
ENTRY = 80_000.0          # illustrative BTC-PERPETUAL price during the flush
CONFIDENCE = "HIGH"        # typical for a STRONG verdict
REAL_PLACED_RR = 0.39      # the R:R actually rendered in the ATR ENTRY LEVELS
                            # row this run, from ComputeSideLevels' structural
                            # arbitration (nearest HVN/POC overhead, clamped stop)


def calc_kelly_sizing(stop_distance_usd, entry_price_usd, b_override=None):
    """Line-for-line port of Core/ScoringEngine_Kelly.vb::CalcKellySizing.
    b_override lets us ask "what would Kelly say if it used the REAL placed
    R:R instead of the fixed ATR-multiple ratio" — the engine has no such
    parameter; this is purely to quantify the disconnect."""
    if stop_distance_usd <= 0 or entry_price_usd <= 0:
        return None

    p = EST_PROB_FLOOR
    if CONFIDENCE == "HIGH":
        p += EST_PROB_SCALE
    elif CONFIDENCE == "MEDIUM":
        p += EST_PROB_SCALE / 2.0
    q = 1.0 - p

    b = b_override if b_override is not None else (ATR_TARGET_MULTIPLIER / ATR_STOP_MULTIPLIER)
    if b <= 0:
        return {"KellyF": (0 * p - q) / 1e-9 if False else None, "b": b, "edge": False}

    f_star = (b * p - q) / b
    if f_star <= 0:
        return {"b": b, "p": p, "KellyF": f_star, "edge": False}

    f_half = f_star / 2.0 if USE_HALF_KELLY else f_star
    f_applied = min(f_half, MAX_RISK_FRACTION)
    kelly_capped = f_half > MAX_RISK_FRACTION
    risk_usd = ACCOUNT_SIZE_USD * f_applied

    risk_per_contract_usd = CONTRACT_FACE_USD * stop_distance_usd / entry_price_usd
    if risk_per_contract_usd <= 0:
        return {"b": b, "p": p, "KellyF": f_star, "edge": True, "contracts": 0}

    contracts_by_risk = int(risk_usd // risk_per_contract_usd)
    max_contracts_by_leverage = int((ACCOUNT_SIZE_USD * MAX_LEVERAGE) // CONTRACT_FACE_USD) \
        if CONTRACT_FACE_USD > 0 else 0

    if max_contracts_by_leverage > 0 and max_contracts_by_leverage < contracts_by_risk:
        contracts = max_contracts_by_leverage
        lev_capped = True
    else:
        contracts = contracts_by_risk
        lev_capped = False

    notional = contracts * CONTRACT_FACE_USD
    leverage = notional / ACCOUNT_SIZE_USD if ACCOUNT_SIZE_USD > 0 else 0.0

    return {
        "b": b, "p": p, "q": q,
        "KellyF": f_star, "KellyFHalf": f_half, "KellyFApplied": f_applied,
        "KellyCapped": kelly_capped, "KellyRiskUsd": risk_usd,
        "KellyContracts": contracts, "KellyLevCapped": lev_capped,
        "Notional": notional, "Leverage": leverage,
    }


if __name__ == "__main__":
    atr_stop_distance = ATR * ATR_STOP_MULTIPLIER  # what the code actually calls
                                                     # CalcKellySizing with

    print("=" * 78)
    print("WHAT THE ENGINE ACTUALLY RENDERS (both the KELLY card and")
    print("verdict_signal.json's `kelly` block):")
    print("=" * 78)
    shipped = calc_kelly_sizing(atr_stop_distance, ENTRY)
    for k, v in shipped.items():
        print(f"  {k:16s} = {v}")

    print()
    print("=" * 78)
    print("WHAT THE SAME FORMULA SAYS IF FED THE *REAL PLACED* R:R (0.39)")
    print("shown two inches below it in the ATR ENTRY LEVELS row this run:")
    print("=" * 78)
    honest = calc_kelly_sizing(atr_stop_distance, ENTRY, b_override=REAL_PLACED_RR)
    for k, v in honest.items():
        print(f"  {k:16s} = {v}")

    print()
    print("=" * 78)
    print("DELTA")
    print("=" * 78)
    print(f"  Shipped KellyContracts : {shipped['KellyContracts']}  "
          f"(${shipped['Notional']:.0f} notional, {shipped['Leverage']:.2f}x leverage)")
    print(f"  Honest  KellyF*        : {honest['KellyF']:.4f}  "
          f"({'POSITIVE EDGE -> size a trade' if honest['KellyF'] > 0 else 'NO EDGE -> size should be ZERO'})")
