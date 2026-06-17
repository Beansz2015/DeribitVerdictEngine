# Trader Profile

This document captures the trader's style, preferences, and strategic context
for the Deribit Verdict Engine project. Attach this file at the start of any
new conversation (coding or strategy) to bootstrap full context instantly.

Last updated: 2026-04-11

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

    ATR(7):          PREFERRED | Position sizing AND reference display block.
                               NOT used for stop placement in execution (swing
                               structure defines stops). Used as volatility
                               scaler: Position = Base x (AvgATR/CurrATR).

    Volume SMA(9):   PREFERRED | Volume spike detection. Breakout only counts if
                               volume > 3x SMA(9). Essential for filtering
                               fakeout breakouts.

    VWAP:            PREFERRED | Institutional fair-value reference. Provides
                               intraday directional bias. Dual-session reset
                               (configurable UTC times).

    Bollinger/BBW:   PREFERRED | Used via BBW (Bandwidth) for squeeze detection.
                               Not used for overbought/oversold bands directly.
                               Squeeze = ACTIVE (-1 both), RELEASING = directional
                               via ROC, NONE = no score.

    EMA Ribbon       PREFERRED | 9/21/50 on 1m for dynamic trend structure.
    (9/21/50):                 Provides price-based support/resistance that DMI
                               alone cannot. EMA(200) on 5m as regime anchor
                               (ABOVE/BELOW veto).

    Funding Rate:    PREFERRED | Contrarian crowd-positioning signal. Used as
                               confidence modifier in Step 3 only (NOT Step 2
                               scoring -- removed in v0.17 to prevent
                               double-counting).
                               NOTE: Funding momentum (rising vs falling rate)
                               identified as future upgrade -- not yet implemented.

    Open Interest:   PREFERRED | OI change direction + price direction = quality
    (OI Delta):                of trend signal. Rising OI + rising price =
                               genuine new longs. Essential for filtering
                               short-covering rallies from real breakouts.

    Order Flow/OFI:  PREFERRED | Real-time buy/sell pressure from L2 order book.
                               Leading indicator -- shows imbalance before price
                               moves.

    Liquidations:    PREFERRED | Cascade detection. Penalty-only signal (v0.17).
                               -1 for > 50 BTC, -2 for > 200 BTC on affected side.
                               NOTE: default dominanceRatio 1.0 = equal-or-greater;
                               consider raising to 1.2-1.5 after live calibration.

    CVD:             PREFERRED | Cumulative volume delta. 3-segment weighted slope
                               (late x2 - early x1). Divergence triggers -1 penalty.

    MicroCVD:        PREFERRED | Intra-window segmentation.
                               BULL/BEAR_ACCEL/DECEL signals. FLAT stall penalty
                               when price and CVD direction contradict.

    TFI:             PREFERRED | Trade flow aggressor pressure.
                               BUY PRESSURE / SELL PRESSURE / NEUTRAL.
                               Window (30) intentionally smaller than MicroCVD (50)
                               -- TFI measures short-burst aggressor pressure,
                               MicroCVD measures structural segmentation.

    MTF Gate (15m):  PREFERRED | Hard veto gate. Forces NO TRADE on BLOCK.
                               15m DMI/ADX + EMA confluence alignment required.
                               TTL cache 60s; 1-bar regime hysteresis.

    OBV:             NEUTRAL   | Volume trend confirmation. Useful for divergence
                               but slower signal. Tier 3 -- nice to have.
                               Adverse divergence blocks cross-category upgrade.

    Donchian(20):    NEUTRAL   | Objective breakout level. Complements VPFR with
                               pure price-based breakout detection. Tier 3.
                               Full LONG/SHORT + quartile partial signals.

    VPFR-lite:       PREFERRED | Volume Profile (Fixed Range) -- fully implemented
    (Engine):                  in engine as Tier 3. NOT visual-only.
                               POC proximity scoring. HVN wall triggers ATR
                               target cap.

    VPVR             VISUAL    | Visual use only on TradingView/Deribit chart.
    (TradingView):   ONLY      | Used to identify swing targets and stops on screen.
                               Engine uses VPFR-lite instead.

---

## 4. Explicitly Rejected Indicators/Approaches

    Stochastic (8,3,3)  -- Signals overbought during valid breakout swings.
                           Harmful for breakout-focused trading. Would cause
                           premature exits on the best trades. -- Jan 2026

    MACD (6,13,5)       -- Redundant with ROC. Lags 2-3 candles during swing
                           transitions. Noisy in range periods. RSI covers
                           divergence detection more cleanly. -- Jan 2026

    CMF (20)            -- Too slow (20-bar lag). Redundant with Volume SMA
                           + VPFR for volume context. -- Jan 2026

    Fixed % profit      -- Rejected in favour of structural swing targets
    targets                (previous swing high/low). Fixed % targets
                           misalign with actual market structure. -- Jan 2026

    ATR-based stops     -- Rejected in favour of structural swing lows/highs
    (execution)            for stop placement. Swing structure defines natural
                           invalidation levels better than ATR multiples.
                           NOTE: ATR retained for reference display block only. -- Jan 2026

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

    ATR thresholds:         1-min (NY):            Low < 20 | Normal 20-55  | High > 55
                            3-min (Asia/London):   Low < 42 | Normal 42-115 | High > 115
                            (recalibrated 2026-06-17 / settings v37 from live ATR p25/p75
                            at BTC ~$62k-$67k; bands are RESOLUTION-DEPENDENT since v36 --
                            3-min ATR runs ~2.1x the 1-min. Was Low<80/Normal 80-150/High>150,
                            BTC ~$80k-$100k Q1 2026. AvgATR/CurrATR ratio self-calibrates;
                            these are display / cold-start reference bands only.)
                            Review against CSV log if BTC price moves
                            significantly.

---

## 6. Verdict Engine Design Preferences

    Preferred verdict style:    Conservative -- would rather miss a trade than
                                overtrade. Selective entry philosophy means
                                the engine should flag quality over quantity.

    Minimum confidence          MEDIUM or HIGH verdict to act on.
    to trade:                   Will not act on WEAK signals unless a specific
                                high-conviction setup is visible on chart.

    Regime preference:          Both TRENDING and RANGE_BOUND are acceptable.
                                TRANSITIONAL = reduced size, extra caution.
                                Will not override regime veto rules.

    Score thresholds:           Percentage-based against regime MaxScore (19/18/15).
                                Computed as Math.Ceiling(regimeMax x verdictStrong/Med/WeakPct).
                                Default pcts produce approx 63%/47%/32% of MaxScore.
                                All pcts configurable via settings.json.

    False positive tolerance:   Low. Prefers engine to say NO TRADE rather
                                than output a weak directional verdict that
                                tempts entry on marginal setups. However, a
                                display (that does not affect scoring) showing
                                this weak directional bias must still be
                                rendered, to help form a future opinion.

    Display preference:         Verdict output should be clean, scannable,
                                and fast to interpret at a glance. Not cluttered.
                                Show score breakdown for transparency but
                                headline verdict should be prominent.

    Config philosophy:          All scoring thresholds and indicator parameters
                                externalised to settings.json (v6, Commit 5).
                                No hardcoded magic numbers remain in engine.
                                Hot-reloadable without recompile.

---

## 7. Open Questions / Known Limitations

    Liq dominanceRatio (default 1.0) -- configurable. Review false LONG/SHORT
    LIQS signals after 2-4 weeks live data. Consider raising to 1.2-1.5 to require
    the dominant side to be proportionally larger before signalling.

    Liq penalty thresholds (50/200 BTC) -- monitoring. Review 200 BTC threshold
    against ~90th percentile of observed LiqLongSize/LiqShortSize in CSV log.

    ATR thresholds recalibrated v37 (2026-06-17): 1-min 20/55, 3-min ~42/115
    (resolution-dependent since v36; was 80/150 for BTC ~$80k-$100k Q1 2026). Review
    if BTC price moves significantly. AvgATR/CurrATR ratio approach is
    self-calibrating but the absolute Low/Normal/High bands may need updating.

    MicroCVD accelThreshold (5000 USD default) -- consider dynamic scaling vs
    VolumeSMA on quiet sessions where 5K delta is noise.

    Funding momentum -- absolute rate currently used. Rising vs falling rate
    direction identified as higher-quality signal. Not yet implemented.

    OI x CVD cross-confirm -- OI (NEW LONGS/SHORTS) and CVD direction are
    currently scored independently. A combined multiplier (e.g. NEW LONGS +
    CVD RISING = full score) identified as a meaningful Tier 1 upgrade.

    Websocket upgrade -- engine currently uses REST polling (snapshot-based).
    Moving to Deribit websocket API would provide real-time order book and
    trade stream, removing the fundamental REST latency constraint.
    Most impactful non-code upgrade available.

    AWS London (LD4) deployment -- recommended for minimal latency to
    Deribit API. Not yet confirmed as deployment target.

---

## 8. What This Trader Values in AI Collaboration

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

    Session handover:       Start new sessions by reading DeribitIndicatorProject.md
                            and architecture.md only. Do NOT read entire codebase --
                            individual .vb files only when a specific edit is needed.
                            This preserves context budget for actual work.

    Version history:        See DeribitIndicatorProject.md Section 14 for the
                            full version history and design decisions scorecard.
