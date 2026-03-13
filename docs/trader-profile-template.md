# Trader Profile

This document captures the trader's style, preferences, and strategic context
for the Deribit Verdict Engine project. Attach this file at the start of any
new conversation (coding or strategy) to bootstrap full context instantly.

Last updated: [DATE]

---

## 1. Background

    Exchange experience:    [e.g. former exchange employee, role, years]
    Trading experience:     [years actively trading crypto]
    Current setup:          [Deribit perpetuals, spot, options, etc.]
    Primary instrument:     [e.g. BTC-PERPETUAL]
    Session style:          [e.g. part-time, monitor a few times per day]
    Other context:          [anything relevant -- timezone, other commitments]

---

## 2. Trading Style

    Primary style:          [e.g. scalping, swing, position]
    Preferred timeframe:    [e.g. 1m entry, 5m regime]
    Hold duration:          [e.g. minutes to hours, rarely overnight]
    Risk tolerance:         [low / medium / high]
    Preferred market state: [e.g. trending only, avoids ranging markets]
    Trade frequency:        [e.g. 2-5 trades per session when conditions met]

---

## 3. Indicator Preferences

For each indicator, note: PREFERRED / NEUTRAL / SCEPTICAL + one-line reason.

    ROC:            [preference | reason]
    RSI:            [preference | reason]
    MACD:           [preference | reason]
    DMI/ADX:        [preference | reason]
    VWAP:           [preference | reason]
    Bollinger Bands:[preference | reason]
    EMA Ribbon:     [preference | reason]
    OBV:            [preference | reason]
    Volume:         [preference | reason]
    Funding Rate:   [preference | reason]
    Open Interest:  [preference | reason]
    Order Flow/OFI: [preference | reason]
    Liquidations:   [preference | reason]
    Donchian:       [preference | reason]
    [add others]:   [preference | reason]

---

## 4. Explicitly Rejected Indicators/Approaches

Indicators or strategies that have been considered and rejected, with reasons.
This prevents future conversations from re-proposing them.

    [Indicator/approach] -- [reason rejected] -- [date decided]
    [Indicator/approach] -- [reason rejected] -- [date decided]

---

## 5. Risk Management Rules

    Max position size:      [e.g. X% of account or X BTC]
    Stop-loss approach:     [e.g. ATR-based, fixed %, mental stop]
    Take-profit approach:   [e.g. scaling out, fixed target, trailing]
    Max daily loss limit:   [hard stop for the day]
    Leverage preference:    [typical range, max acceptable]
    Position sizing method: [e.g. ATR multiplier as in current engine]

---

## 6. Verdict Engine Design Preferences

    Preferred verdict style:    [e.g. conservative -- rather miss than overtrade]
    Acceptable false positive rate: [e.g. willing to act on WEAK signals]
    Minimum confidence to trade:    [e.g. only MEDIUM or HIGH, never LOW]
    Regime preference:          [e.g. only trade TRENDING regimes]
    Score threshold intuition:  [any personal view on 6/9/12 thresholds]

---

## 7. Key Design Decisions Made (Scorecard)

A running log of major architectural decisions so future Claudes don't
re-open settled questions without good reason.

    v0.13  Partial signal upgrade system added -- cross-category confirmation
           required to prevent single-indicator amplification
    v0.15  TRANSITIONAL regime penalty added -- ADX proximity-based (-1 or -2)
    v0.16  TierFloor() guard added -- penalty cannot drop score below tier floor
    v0.17  Funding OK removed from Step 2 scoring -- double-counting with Step 3
           No Adverse Liq converted to penalty-only, scaled by liq size
           Score denominator updated from /15 to /13
    v0.18  BBW Option B -- ACTIVE=-1 both, RELEASING=+1 ROC-directional, NONE=0
    [add future decisions as made]

---

## 8. Open Questions / Known Limitations

Things that are unresolved, under observation, or flagged for future review.

    [Issue description] -- [status: monitoring / needs spec / deferred]
    Liq penalty thresholds (50/200 BTC) -- monitoring, review after 2-4 weeks
    [add others]

---

## 9. What This Trader Values in AI Collaboration

    Communication style:    [e.g. technical and concise, no hand-holding]
    Decision process:       [e.g. spec first, then implement -- no surprises]
    Review preference:      [e.g. always show what changed and why]
    When to push back:      [e.g. flag issues proactively, don't just implement]
    What to avoid:          [e.g. don't re-open settled decisions without data]
