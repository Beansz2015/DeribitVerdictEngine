# Trader Profile

This document captures the trader's style, preferences, and strategic context
for the Deribit Verdict Engine project. Attach this file at the start of any
new conversation (coding or strategy) to bootstrap full context instantly.

Last updated: 2026-03-14

---

## 1. Background

    Exchange experience:    Former employee of a digital assets exchange (role: operations/trading side)
    Trading experience:     Many years actively trading crypto; highly experienced
    Current setup:          Deribit perpetuals (BTC-PERPETUAL); also trades spot
    Primary instrument:     BTC-PERPETUAL on Deribit
    Session style:          Part-time / discretionary; trades when conditions are met,
                            not on a fixed schedule. Does not trade every session.
    Timezone:               GMT+8 (Penang, Malaysia)
    Other context:          Software engineering background (ex-dev); understands code
                            and can review VB.NET implementations critically.
                            Also runs a separate business (hostel). Trading is a
                            significant but not sole focus.

---

## 2. Trading Style

    Primary style:          Momentum-Informed Scalper (Hybrid Style C)
                            Uses multi-timeframe bias (5m/15m/1h) to determine
                            direction, then executes entries and exits on 1m chart.
                            NOT a pure scalper (fixed % targets) and NOT a pure
                            momentum trader (ride indefinitely). Trades between
                            structural swing levels.

    Preferred timeframe:    1m execution chart; 5m/15m for regime and bias

    Entry logic:            Price breaks above/below previous swing high/low,
                            confirmed by impulse (ROC) and volume spike.
                            Requires structural breakout -- does not chase candles.

    Profit targets:         Previous swing high (for longs) / previous swing low
                            (for shorts). Structural targets, NOT fixed % or ATR.
                            This means R:R is dynamic depending on swing size.

    Stop-loss placement:    Below previous swing low (longs) / above previous
                            swing high (shorts). Structural stops, NOT ATR-based.
                            Stop distance defines risk per trade, not a fixed %.

    Hold duration:          2-15 minutes typical. Will hold through 2-3 red candles
                            IF trend is confirmed intact (RSI > 60 check).
                            Does NOT hold overnight -- always flat at end of session.

    Risk tolerance:         Medium. Comfortable with short retracements during
                            holds but has clear exit rules.

    Preferred market state: Both trending AND range-bound markets are acceptable,
                            as long as there is a high-probability swing opportunity
                            (clear high and low to trade between).
                            Pure chop with no swing structure = no trade.

    Trade frequency:        Selective. Only enters when checklist conditions are met.
                            Prefers fewer high-quality trades over frequent low-quality.

---

## 3. Indicator Preferences

    ROC(9):          PREFERRED | Fast impulse confirmation for breakout entries.
                               Identifies acceleration at swing breakouts before
                               RSI reacts. Zero-line also useful as regime filter.
                               Keep despite RSI overlap -- different timing roles.

    RSI(9):          PREFERRED | Hold/exit decisions during trades. Divergence
                               detection for exhaustion. Slower than ROC so used
                               DURING trade management, not at entry.

    DMI/ADX(9):      PREFERRED | Core regime filter on 5m chart. Determines
                               long-bias vs short-bias vs bidirectional day.
                               ADX < 20 = range, > 25 = trend. Monitored on 5m
                               NOT on 1m.

    ATR(7):          PREFERRED | Position sizing ONLY. NOT used for stop placement
                               (swing structure defines stops). Used as
                               volatility scaler: Position = Base x (AvgATR/CurrATR).

    Volume SMA(9):   PREFERRED | Volume spike detection. Breakout only counts if
                               volume > 3x SMA(9). Essential for filtering
                               fakeout breakouts.

    VWAP:            PREFERRED | Institutional fair-value reference. Provides
                               intraday directional bias independent of VPVR.
                               Session-reset at 00:00 UTC.

    Bollinger Bands  PREFERRED | Used via BBW (Bandwidth) for squeeze detection.
    / BBW:                     Not used for overbought/oversold bands directly.
                               Squeeze = ACTIVE (-1 both), RELEASING = directional
                               via ROC, NONE = no score. (v0.18 design)

    EMA Ribbon       PREFERRED | 9/21/50 on 1m for dynamic trend structure.
    (9/21/50):                 Provides price-based support/resistance that DMI
                               alone cannot. EMA(200) on 5m as macro veto.

    Funding Rate:    PREFERRED | Contrarian crowd-positioning signal. Used as
                               confidence modifier in Step 3 only (NOT Step 2
                               scoring -- removed in v0.17 to prevent
                               double-counting).

    Open Interest:   PREFERRED | OI change direction + price direction = quality
    (OI Delta):                of trend signal. Rising OI + rising price =
                               genuine new longs. Essential for filtering
                               short-covering rallies from real breakouts.

    Order Flow/OFI:  PREFERRED | Real-time buy/sell pressure from L2 order book.
                               Leading indicator -- shows imbalance before price
                               moves. Via Deribit WebSocket.

    Liquidations:    PREFERRED | Cascade detection. Penalty-only signal (v0.17).
                               -1 for > 50 BTC, -2 for > 200 BTC on affected side.
                               Thresholds under review after 2-4 weeks of data.

    OBV:             NEUTRAL   | Volume trend confirmation. Useful for divergence
                               but slower signal. Tier 3 -- nice to have.

    Donchian(20):    NEUTRAL   | Objective breakout level. Complements VPVR with
                               pure price-based breakout detection. Tier 3.

    VPVR:            PREFERRED | Visual use only on TradingView/Deribit chart.
    (Visual only)              Cannot be computed from OHLCV via API.
                               Used to identify swing targets and stops on screen.
                               NOT in the automated verdict engine.

---

## 4. Explicitly Rejected Indicators/Approaches

    Stochastic (8,3,3)  -- Signals overbought during valid breakout swings.
                           Harmful for breakout-focused trading. Would cause
                           premature exits on the best trades. -- Jan 2026

    MACD (6,13,5)       -- Redundant with ROC. Lags 2-3 candles during swing
                           transitions. Noisy in range periods. RSI covers
                           divergence detection more cleanly. -- Jan 2026

    CMF (20)            -- Too slow (20-bar lag). Redundant with Volume SMA
                           + VPVR for volume context. -- Jan 2026

    Fixed % profit      -- Rejected in favour of structural swing targets
    targets                (previous swing high/low). Fixed % targets
                           misalign with actual market structure. -- Jan 2026

    ATR-based stops     -- Rejected in favour of structural swing lows/highs.
                           Swing structure defines natural invalidation levels
                           better than ATR multiples. -- Jan 2026

    Pure scalping       -- Fixed 0.1-0.5% targets. Too small for swing-to-swing
    (Style A)              volatility, ignores multi-timeframe context. -- Jan 2026

    Pure momentum       -- Riding trend indefinitely. Does not suit part-time
    (Style B)              monitoring style or intraday-only constraint. -- Jan 2026

    BBW NONE = +1       -- Non-directional padding. Rewards calm conditions
    both sides             which carry no signal. Removed in v0.18. -- Mar 2026

    Funding OK in       -- Double-counting with Step 3 funding modifier.
    Step 2 scoring         Removed in v0.17. Kept as display-only. -- Mar 2026

    No Adverse Liq      -- Non-directional padding firing ~95% of the time.
    as positive reward     Converted to penalty-only in v0.17. -- Mar 2026

    Flat TRANSITIONAL   -- Blunt -2 penalty regardless of ADX proximity.
    penalty (-2 flat)      Replaced with ADX-proximity scale (-1 or -2)
                           plus tier-floor guard. -- Mar 2026

---

## 5. Risk Management Rules

    Max position size:      Not specified in absolute terms. Scaled dynamically
                            via ATR multiplier: Base x (20d AvgATR / CurrATR).
                            Low ATR day (< 80) = larger size.
                            High ATR day (> 150) = smaller size.

    Stop-loss approach:     STRUCTURAL -- always placed below previous swing low
                            (longs) or above previous swing high (shorts).
                            NOT ATR-based, NOT fixed %. Swing structure defines
                            the natural invalidation level.

    Take-profit approach:   STRUCTURAL -- target is the previous swing high
                            (longs) or swing low (shorts). R:R is dynamic;
                            varies from ~1:1 (tight swings) to 1:3+ (wide swings).
                            Trader accepts variable R:R as a feature, not a flaw.

    Hold through drawdown:  Will hold through 2-3 red candles IF:
                            (a) RSI(9) > 60 (momentum intact)
                            (b) Trend structure has not broken
                            Will exit if RSI < 40 or ROC crosses below 0.

    Max daily loss limit:   Not formally specified. Implied by structural stops
                            and position sizing discipline.

    Overnight holding:      NEVER. Always flat at end of session.

    Leverage preference:    Not formally specified. Implied moderate given
                            selective trade frequency and structural stop usage.

    ATR thresholds:         Low < 80 | Normal 80-150 | High > 150
                            (calibrated for BTC ~$80k-$100k range, Q1 2026)
                            Recalibrate if BTC price moves significantly.

---

## 6. Verdict Engine Design Preferences

    Preferred verdict style:    Conservative -- would rather miss a trade than
                                overtrade. Selective entry philosophy means
                                the engine should flag quality over quantity.

    Minimum confidence          MEDIUM or HIGH (score >= 9) to act on verdict.
    to trade:                   Will not act on WEAK signals (score 6-8) unless
                                a specific high-conviction setup is visible on chart.

    Regime preference:          Both TRENDING and RANGE_BOUND are acceptable.
                                TRANSITIONAL = reduced size, extra caution.
                                Will not override regime veto rules.

    Score threshold             Current thresholds (6/9/12) feel correct.
    intuition:                  Strong signal at 12/13 = all meaningful signals
                                aligned (92% of max). This is appropriately
                                demanding.

    False positive tolerance:   Low. Prefers engine to say NO TRADE rather
                                than output a weak directional verdict that
                                tempts entry on marginal setups.

    Display preference:         Verdict output should be clean, scannable,
                                and fast to interpret at a glance. Not cluttered.
                                Show score breakdown for transparency but
                                headline verdict should be prominent.

---

## 7. Key Design Decisions Made (Scorecard)

    v0.13  Partial signal upgrade system added -- cross-category confirmation
           required to prevent single-indicator amplification.

    v0.15  TRANSITIONAL regime penalty redesigned -- ADX proximity-based
           (-2 if ADX 20.0-22.4, -1 if ADX 22.5-24.9) plus TierFloor()
           guard (max drop = 3 points = 1 tier). Replaces flat -2.
           Ref: docs/transitional-regime-scoring-fix.md

    v0.16  TierFloor() guard formalised -- penalty cannot cause score to drop
           more than one full tier width (3 points) in a single application.

    v0.17  Non-directional padding cleanup:
           - Funding OK removed from Step 2 scoring (double-counting with
             Step 3 funding modifier). Kept as display-only row.
           - No Adverse Liq converted to penalty-only. -1 for LiqSize > 50 BTC,
             -2 for LiqSize > 200 BTC, on affected side only. No reward for calm.
           - Score denominator updated from /15 to /13.
           Ref: docs/dual-scoring-fix-proposal.md, docs/dual-scoring-fix-response.md

    v0.18  BBW redesign (Option B approved):
           - ACTIVE squeeze: -1 both sides (confidence reduction).
           - RELEASING: +1 to ROC-aligned side only (ROC > +0.10 = long,
             ROC < -0.10 = short, chop zone = no award).
           - NONE: no score change (calm = not a signal).
           Ref: docs/bbw-scoring-proposal.md, docs/bbw-scoring-response.md

---

## 8. Open Questions / Known Limitations

    Liq penalty thresholds (50/200 BTC) -- monitoring. Review after 2-4 weeks
    of live data. Adjust 200 BTC threshold to ~90th percentile of observed
    LiqLongSize/LiqShortSize values in production.

    ROC thresholds (+-0.15, +-0.40, +-0.70) -- calibrated for BTC at
    ~$80k-$100k on 1m charts. If BTC price moves significantly (e.g. $150k+),
    these percentage-of-price thresholds may need recalibration. Consider
    making them configurable or expressing as % of current price.

    ATR thresholds (80/150) -- same price-sensitivity caveat as ROC.
    The AvgATR/CurrATR ratio approach is self-calibrating and preferred.

    20-day ATR average -- currently sourced from a separate daily candle
    API call (GET /public/get_tradingview_chart_data, resolution=1D, count=30).
    Verify this endpoint returns consistent daily ATR values.

    VPVR -- not implemented in engine. Visual use only on chart. Future v2
    option: build simplified volume profile from tick data via Deribit
    GET /public/get_last_trades_by_instrument. Deferred.

    Divergence detection (RSI, OBV) -- requires basic swing detection
    algorithm (5-bar pivot high/low comparison). Complexity vs value
    not yet assessed. Status: not yet implemented.

    Donchian(20) and OBV -- Tier 3 indicators. Implemented in spec but
    lower priority. Confirm they are included and scoring in engine.

    BBW display bug (v0.17) -- [S] mark was suppressed during TRENDING_DOWN
    regime. Likely a UI-layer conditional suppression. Becomes irrelevant
    after v0.18 BBW redesign but monitor for similar suppression on other rows.

    AWS London (LD4) deployment -- recommended for minimal latency to
    Deribit API. Not yet confirmed as deployment target.

---

## 9. What This Trader Values in AI Collaboration

    Communication style:    Technical and concise. No hand-holding or excessive
                            explanation of basics. Trader has software engineering
                            background and trading exchange experience.
                            Use correct terminology without over-explaining.

    Decision process:       Spec-first workflow. Novel questions go to the
                            strategy conversation (Perplexity) for analysis.
                            Decisions are documented in .md files and committed
                            to GitHub before coding begins. Coding Claude
                            implements approved specs -- does not invent design
                            decisions unilaterally.

    GitHub workflow:        Proposal .md files are written by the coding Claude,
                            reviewed by strategy Claude (Perplexity), response
                            .md files committed to repo, then link passed back
                            to coding Claude for implementation.
                            All docs live in /docs folder of DeribitVerdictEngine repo.

    Review preference:      Always show what changed and why. Changelog entries
                            for every version. Breaking changes flagged explicitly.

    Proactive flagging:     Flag design issues, inconsistencies, or risks
                            proactively before implementing. Do not silently
                            implement something that conflicts with spec intent.

    When to push back:      When a proposed change would reintroduce a pattern
                            that was deliberately removed (e.g. non-directional
                            padding, double-counting). Cite the version it was
                            removed and why.

    What to avoid:          Do not re-open settled decisions without new data or
                            a concrete technical reason. Do not propose changes
                            that increase indicator correlation (signals should
                            remain as independent as possible).

    Conversation split:     Novel strategy questions and spec decisions go to
                            Perplexity strategy conversation.
                            Implementation, code review, debugging go to
                            Claude coding conversation.
                            This profile bridges both conversations.
