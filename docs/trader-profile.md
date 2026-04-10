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
                               Divergence penalty: -1 long when BEARISH+RSI>65,
                               -1 short when BULLISH+RSI<35. PivotWing and
                               LookbackBars configurable via cfg.

    DMI/ADX(9):      PREFERRED | Core regime filter on 5m chart. Determines
                               long-bias vs short-bias vs bidirectional day.
                               ADX < 20 = range, > 25 = trend. Monitored on 5m
                               NOT on 1m. Threshold configurable via cfg.

    ATR(7):          PREFERRED | Position sizing AND reference display block.
                               NOT used for stop placement in execution (swing
                               structure defines stops). Used as volatility
                               scaler: Position = Base x (AvgATR/CurrATR).

    Volume SMA(9):   PREFERRED | Volume spike detection. Breakout only counts if
                               volume > 3x SMA(9). Essential for filtering
                               fakeout breakouts. Mid-tier directional partial
                               upgrade via cross-category confirm.

    VWAP:            PREFERRED | Institutional fair-value reference. Provides
                               intraday directional bias. Dual-session reset
                               (configurable UTC times). Warmup guard (default
                               15 candles) prevents early-session noise.
                               sigma-1/sigma-2 bands for partial zone scoring.

    Bollinger/BBW:   PREFERRED | Used via BBW (Bandwidth) for squeeze detection.
                               Not used for overbought/oversold bands directly.
                               Squeeze = ACTIVE (-1 both), RELEASING = directional
                               via ROC, NONE = no score. (v0.18 design)
                               Squeeze penalty magnitude configurable via cfg.
                               TTM flatThreshold configurable via cfg.

    EMA Ribbon       PREFERRED | 9/21/50 on 1m for dynamic trend structure.
    (9/21/50):                 Provides price-based support/resistance that DMI
                               alone cannot. EMA(200) on 5m as regime anchor
                               (ABOVE/BELOW veto).

    Funding Rate:    PREFERRED | Contrarian crowd-positioning signal. Used as
                               confidence modifier in Step 3 only (NOT Step 2
                               scoring -- removed in v0.17 to prevent
                               double-counting). Step deltas configurable via cfg.
                               NOTE: Funding momentum (rising vs falling rate)
                               identified as future upgrade -- not yet implemented.

    Open Interest:   PREFERRED | OI change direction + price direction = quality
    (OI Delta):                of trend signal. Rising OI + rising price =
                               genuine new longs. Essential for filtering
                               short-covering rallies from real breakouts.
                               Signals: NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL.

    Order Flow/OFI:  PREFERRED | Real-time buy/sell pressure from L2 order book.
                               Leading indicator -- shows imbalance before price
                               moves. BookDepth configurable (default 3); dynamic
                               descending weight array. Dominance thresholds
                               configurable via cfg (BuyDominantRatio=1.2,
                               SellDominantRatio=0.833).

    Liquidations:    PREFERRED | Cascade detection. Penalty-only signal (v0.17).
                               -1 for > 50 BTC, -2 for > 200 BTC on affected side.
                               DominanceRatio configurable via cfg (default 1.0).
                               NOTE: default 1.0 = equal-or-greater threshold;
                               consider raising to 1.2-1.5 after live calibration.

    CVD:             PREFERRED | Cumulative volume delta. 3-segment weighted slope
                               (late x2 - early x1). Late segment carries 2x weight
                               to reduce false RISING/FALLING flips from early
                               single large trades. Divergence triggers -1 penalty.

    MicroCVD:        PREFERRED | Intra-window segmentation (50-trade window, cfg).
                               BULL/BEAR_ACCEL/DECEL signals. FLAT stall penalty
                               when price and CVD direction contradict.
                               AccelThreshold configurable via cfg (default 5000 USD;
                               consider dynamic scaling vs VolumeSMA in future).

    TFI:             PREFERRED | Trade flow aggressor pressure (30-trade window, cfg).
                               BUY PRESSURE / SELL PRESSURE / NEUTRAL.
                               Threshold configurable via cfg (default 0.15).
                               Window (30) intentionally smaller than MicroCVD (50)
                               -- TFI measures short-burst aggressor pressure,
                               MicroCVD measures structural segmentation.

    MTF Gate (15m):  PREFERRED | Hard veto gate. Forces NO TRADE on BLOCK.
                               15m DMI/ADX + EMA confluence alignment required.
                               TTL cache 60s (avoids redundant 15m fetches).
                               Regime hysteresis: 1-bar grace period before
                               RANGE_BOUND flip from TRENDING/TRANSITIONAL.

    OBV:             NEUTRAL   | Volume trend confirmation. Useful for divergence
                               but slower signal. Tier 3 -- nice to have.
                               Adverse divergence blocks cross-category upgrade.

    Donchian(20):    NEUTRAL   | Objective breakout level. Complements VPFR with
                               pure price-based breakout detection. Tier 3.
                               Full LONG/SHORT + quartile partial signals
                               (LONG_PARTIAL/SHORT_PARTIAL). NONE = mid-channel
                               note annotated in scoring breakdown.

    VPFR-lite:       PREFERRED | Volume Profile (Fixed Range) -- fully implemented
    (Engine):                  in engine as Tier 3. NOT visual-only.
                               POC proximity scoring. HVN wall triggers ATR
                               target cap (AdjustedLongTarget/AdjustedShortTarget).
                               Exponential decay weighting (decayBase=0.985).
                               numBuckets configurable via cfg (default 50).

    VPVR             VISUAL    | Visual use only on TradingView/Deribit chart.
    (TradingView):   ONLY      | Used to identify swing targets and stops on screen.
                               Engine uses VPFR-lite instead (see above).

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
                           NOTE: ATR retained for reference display block only
                           (entry/stop/target levels shown as reference, not
                           as execution rules). -- Jan 2026

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
                            Review against CSV log if BTC price moves
                            significantly -- AvgATR/CurrATR ratio approach
                            is self-calibrating and preferred long-term.

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
                                NOT fixed at 6/9/12 -- thresholds vary by regime.
                                Default pcts produce approx 63%/47%/32% of MaxScore.
                                All pcts configurable via settings.json.

    False positive tolerance:   Low. Prefers engine to say NO TRADE rather
                                than output a weak directional verdict that
                                tempts entry on marginal setups.

    Display preference:         Verdict output should be clean, scannable,
                                and fast to interpret at a glance. Not cluttered.
                                Show score breakdown for transparency but
                                headline verdict should be prominent.

    Config philosophy:          All scoring thresholds and indicator parameters
                                externalised to settings.json (v6, Commit 5).
                                No hardcoded magic numbers remain in engine.
                                Hot-reloadable without recompile.

---

## 7. Key Design Decisions Made (Scorecard)

    v0.13      Partial signal upgrade system added -- cross-category confirmation
               required to prevent single-indicator amplification.

    v0.15      TRANSITIONAL regime penalty redesigned -- ADX proximity-based
               (-2 if ADX 20.0-22.4, -1 if ADX 22.5-24.9) plus TierFloor()
               guard (max drop = 3 points = 1 tier). Replaces flat -2.

    v0.16      TierFloor() guard formalised -- penalty cannot cause score to drop
               more than one full tier width (3 points) in a single application.

    v0.17      Non-directional padding cleanup: Funding OK removed from Step 2;
               No Adverse Liq converted to penalty-only; denominator /13.

    v0.18      BBW redesign (Option B): ACTIVE=-1 both, RELEASING=+1 ROC-aligned,
               NONE=no change.

    v0.33      MTF Gate added: 15m DMI/ADX hard veto; forces NO TRADE on BLOCK.

    v0.35      Auto-run timer UI added.

    v0.37      CalcRSIDivergence added; divergence penalty wired into scoring.

    v0.38      MicroCVD 3-segment: BULL/BEAR_ACCEL/DECEL signals.

    v0.39      Dual-session VWAP; warmup guard.

    v0.40      DynamicNorms volume thresholds; volMid directional partial scoring.

    v0.42      OBV adverse divergence gate; cross-category upgrade logic.

    v0.43      VPFR-lite added; POC proximity scoring (supersedes visual-only VPVR).

    v0.44      AdjustedLongTarget / AdjustedShortTarget; HVN wall target cap.

    v0.45      MicroCVD sign-aware penalty; CVD divergence penalty fix.

    v0.46      RenderOutput refactor; last transacted price display block.

    v0.47      CVD 3-seg weighted slope (late x2 - early x1); Donchian quartile
               partial; OBV div block; VPFR exp decay; MTF TTL cache 60s;
               RSI div penalty wired.

    v0.48      TFI window (30) separated from MicroCVD window (50); dedicated
               TfiSettings + MicroCvdSettings in EngineSettings.

    v0.49      All scoring thresholds + indicator params externalised to cfg;
               EngineSettings v0.37; settings.json v6. No hardcoded magic numbers.

    Commit 4   Regime ADX hysteresis (1-bar grace, _prevRegime field);
               MicroCVD FLAT stall penalty; OFI BookDepth injectable via cfg;
               dynamic descending weight array in CalcOFI.

    Commit 5   All remaining hardcoded params wired to cfg:
               [T2-C] Donchian NONE mid-channel breakdown note;
               [T3-A] VPFR numBuckets from cfg;
               [T3-B] RSI pivotWing + lookbackBars from cfg;
               [T3-C] TTM flatThreshold from cfg;
               [T3-D] CalcLiquidations dominanceRatio from cfg.

---

## 8. Open Questions / Known Limitations

    RESOLVED: VPFR-lite implemented (v0.43). POC proximity + HVN cap fully scored.
    RESOLVED: RSI divergence implemented (v0.37). CalcRSIDivergence with pivot detection.
    RESOLVED: Donchian + OBV confirmed scored in engine (Tier 3).
    RESOLVED: ROC thresholds now configurable via settings.json (v0.49 / Commit 5).
    RESOLVED: All indicator params externalised to cfg (Commit 5).

    Liq dominanceRatio (default 1.0) -- now configurable. Review false LONG/SHORT
    LIQS signals after 2-4 weeks live data. Consider raising to 1.2-1.5 to require
    the dominant side to be proportionally larger before signalling.

    Liq penalty thresholds (50/200 BTC) -- monitoring. Review 200 BTC threshold
    against ~90th percentile of observed LiqLongSize/LiqShortSize in CSV log.

    ATR thresholds (80/150) -- calibrated for BTC ~$80k-$100k Q1 2026. Review
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

    Session handover:       Start new sessions by reading DeribitIndicatorProject.md
                            and architecture.md only. Do NOT read entire codebase --
                            individual .vb files only when a specific edit is needed.
                            This preserves context budget for actual work.
