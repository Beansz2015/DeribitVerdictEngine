# Trader Profile

The trader's style, preferences and strategic context for the Deribit Verdict Engine project. Read it in full at the start of every Claude session (CLAUDE.md Session Start Protocol).

- **Last updated:** 2026-09-14. Re-synced against settings.json v68, the code and the engine docs; 16 stale items corrected (trader-ruled 2026-09-14). The pre-sync text, including the drift notice that listed the 16 items, is at commit 4958d1f: `git show 4958d1f:docs/trader-profile.md`
- **Layout:** reformatted 2026-09-14 from indented plain-text blocks to compact markdown; the content is unchanged. Pre-reformat text: git tag `doc-trim-2026-09-14b-pre`, or `docs/archive/doc-trim-2026-09-14b/originals/trader-profile.md`. Ledger: [`doc-trim-log.md`](doc-trim-log.md).
- **Settings values** below are quoted with their key and the version they were read at ("2 at v68"). The key is the source of truth; re-read settings.json before relying on a quoted value.

---

## 1. Background

- **Exchange experience:** former employee of a digital assets exchange (role: operations/trading side).
- **Trading experience:** many years actively trading crypto; highly experienced.
- **Current setup:** Deribit perpetuals (BTC-PERPETUAL); also trades spot.
- **Primary instrument:** BTC-PERPETUAL on Deribit.
- **Session style:** part-time / discretionary; trades when conditions are met, not on a fixed schedule. Does not trade every session.
- **Timezone:** GMT+8 (Penang, Malaysia).
- **Other context:** software engineering background (ex-dev); understands code and can review VB.NET implementations critically. Also runs a separate business (hostel). Trading is a significant but not sole focus.

---

## 2. Trading Style

- **Primary style:** Momentum-Informed Scalper (Hybrid Style C). Uses multi-timeframe bias (5m/15m/1h) to determine direction, then executes entries and exits on 1m chart. NOT a pure scalper (fixed % targets) and NOT a pure momentum trader (ride indefinitely). Trades between structural swing levels.
- **Preferred timeframe:** 1m execution chart; 5m/15m for regime and bias.
- **Entry logic:** price breaks above/below previous swing high/low, confirmed by impulse (ROC) and volume spike. Requires structural breakout -- does not chase candles.
- **Profit targets:** previous swing high (for longs) / previous swing low (for shorts). Structural targets, NOT fixed % or ATR. This means R:R is dynamic depending on swing size.
- **Stop-loss placement:** below previous swing low (longs) / above previous swing high (shorts). Structural stops, NOT ATR-based. Stop distance defines risk per trade, not a fixed %.
- **Hold duration:** 2-15 minutes typical. Will hold through 2-3 red candles IF trend is confirmed intact (RSI > 60 check). Does NOT hold overnight -- always flat at end of session.
- **Risk tolerance:** medium. Comfortable with short retracements during holds but has clear exit rules.
- **Preferred market state:** both trending AND range-bound markets are acceptable, as long as there is a high-probability swing opportunity (clear high and low to trade between). Pure chop with no swing structure = no trade.
- **Trade frequency:** selective. Only enters when checklist conditions are met. Prefers fewer high-quality trades over frequent low-quality.

---

## 3. Indicator Preferences

| Indicator | Status | Role |
|---|---|---|
| ROC(9) | PREFERRED | Fast impulse confirmation for breakout entries. Identifies acceleration at swing breakouts before RSI reacts. Zero-line also useful as regime filter. Keep despite RSI overlap -- different timing roles. |
| RSI(9) | PREFERRED | Hold/exit decisions during trades. Divergence detection for exhaustion. Slower than ROC so used DURING trade management, not at entry. |
| DMI/ADX(9) | PREFERRED | Core regime filter on 5m chart. Determines long-bias vs short-bias vs bidirectional day. ADX < 20 = range, > 25 = trend. Monitored on 5m NOT on 1m. |
| ATR(7) | PREFERRED | Position sizing AND reference display block. NOT used for stop placement in execution (swing structure defines stops). Used as volatility scaler: Position = Base x (AvgATR/CurrATR). |
| Volume SMA(9) | PREFERRED | Volume spike detection. Breakout only counts if volume > 3x SMA(9). Essential for filtering fakeout breakouts. |
| VWAP | PREFERRED | Institutional fair-value reference. Provides intraday directional bias. Dual-session reset (configurable UTC times). |
| Bollinger/BBW | PREFERRED | Used via BBW (Bandwidth) for squeeze detection. Not used for overbought/oversold bands directly. Squeeze = ACTIVE (penalty on both sides: scoring.bbw_squeeze_penalty, 2 at v68), RELEASING = directional via ROC, NONE = no score. |
| EMA Ribbon (9/21/50) | PREFERRED | 9/21/50 on 1m for dynamic trend structure. Provides price-based support/resistance that DMI alone cannot. EMA(200) on 5m as regime anchor (ABOVE/BELOW veto). |
| Funding Rate | PREFERRED | Contrarian crowd-positioning signal. Used as confidence modifier in Step 3 only (NOT Step 2 scoring -- removed in v0.17 to prevent double-counting). Funding momentum (rising vs falling rate) is SHIPPED as the Step 3b modifier, on a time-anchored window since v53. Preferred over the absolute rate. |
| Open Interest (OI Delta) | PREFERRED | OI change direction + price direction = quality of trend signal. Rising OI + rising price = genuine new longs. Essential for filtering short-covering rallies from real breakouts. |
| Order Flow/OFI | PREFERRED | Real-time buy/sell pressure from L2 order book. Leading indicator -- shows imbalance before price moves. |
| Liquidations | PREFERRED | Cascade detection. Penalty-only signal (v0.17). On a LONG LIQS / SHORT LIQS signal the affected side loses scoring.liq_standard_penalty (1 at v68), or scoring.liq_large_penalty (2 at v68) above indicators.Liquidations.large_liq_size (200 at v68). Dominance: indicators.Liquidations.dominance_ratio (2.0 at v68). |
| CVD | PREFERRED | Cumulative volume delta. 3-segment weighted slope (late x2 - early x1). Divergence triggers -1 penalty. |
| MicroCVD | PREFERRED | Intra-window segmentation. BULL/BEAR_ACCEL/DECEL signals. FLAT stall penalty when price and CVD direction contradict. |
| TFI | PREFERRED | Trade flow aggressor pressure. BUY PRESSURE / SELL PRESSURE / NEUTRAL. Window (30) intentionally smaller than MicroCVD (50) -- TFI measures short-burst aggressor pressure, MicroCVD measures structural segmentation. |
| MTF Gate (15m) | PREFERRED | Hard veto gate. Forces NO TRADE on BLOCK. 15m DMI/ADX + EMA confluence alignment required. 15m data refreshes every run on WebSocket (the 60 s TTL applies only on the REST fallback); 1-bar regime hysteresis. |
| OBV | NEUTRAL | Volume trend confirmation. Useful for divergence but slower signal. Tier 3 -- nice to have. Adverse divergence blocks cross-category upgrade. |
| Donchian(20) | NEUTRAL | Objective breakout level. Complements VPFR with pure price-based breakout detection. Tier 3. Full LONG/SHORT + quartile partial signals. |
| VPFR-lite (Engine) | PREFERRED | Volume Profile (Fixed Range) -- fully implemented in engine as Tier 3. NOT visual-only. POC proximity scoring. HVN walls feed the v51 structural-first target ladder (swing, then HVN, then POC, then ATR fallback). |
| VPVR (TradingView) | VISUAL ONLY | Visual use only on TradingView/Deribit chart. Used to identify swing targets and stops on screen. Engine uses VPFR-lite instead. |

---

## 4. Explicitly Rejected Indicators/Approaches

| Approach | Why rejected | When |
|---|---|---|
| Stochastic (8,3,3) | Signals overbought during valid breakout swings. Harmful for breakout-focused trading. Would cause premature exits on the best trades. | Jan 2026 |
| MACD (6,13,5) | Redundant with ROC. Lags 2-3 candles during swing transitions. Noisy in range periods. RSI covers divergence detection more cleanly. | Jan 2026 |
| CMF (20) | Too slow (20-bar lag). Redundant with Volume SMA + VPFR for volume context. | Jan 2026 |
| Fixed % profit targets | Rejected in favour of structural swing targets (previous swing high/low). Fixed % targets misalign with actual market structure. | Jan 2026 |
| ATR-based stops (execution) | Rejected in favour of structural swing lows/highs for stop placement. Swing structure defines natural invalidation levels better than ATR multiples. NOTE: ATR retained for reference display block only. | Jan 2026 |
| Pure scalping (Style A) | Fixed 0.1-0.5% targets. Too small for swing-to-swing volatility, ignores multi-timeframe context. | Jan 2026 |
| Pure momentum (Style B) | Riding trend indefinitely. Does not suit part-time monitoring style or intraday-only constraint. | Jan 2026 |
| BBW NONE = +1 both sides | Non-directional padding. Rewards calm conditions which carry no signal. Removed in v0.18. | Mar 2026 |
| Funding OK in Step 2 scoring | Double-counting with Step 3 funding modifier. Removed in v0.17. Kept as display-only. | Mar 2026 |
| No Adverse Liq as positive reward | Non-directional padding firing ~95% of the time. Converted to penalty-only in v0.17. | Mar 2026 |
| Flat TRANSITIONAL penalty (-2 flat) | Blunt -2 penalty regardless of ADX proximity. Replaced with ADX-proximity scale (-1 or -2) plus tier-floor guard. | Mar 2026 |

---

## 5. Risk Management Rules

- **Max position size:** not specified in absolute terms. Scaled dynamically via ATR multiplier: Base x (20d AvgATR / CurrATR). Low ATR day = larger size; High ATR day = smaller size, as classified by the ATR thresholds below.
- **Stop-loss approach:** STRUCTURAL -- always placed below previous swing low (longs) or above previous swing high (shorts). NOT ATR-based, NOT fixed %. Swing structure defines the natural invalidation level.
- **Take-profit approach:** STRUCTURAL -- target is the previous swing high (longs) or swing low (shorts). R:R is dynamic; varies from ~1:1 (tight swings) to 1:3+ (wide swings). Trader accepts variable R:R as a feature, not a flaw.
- **Hold through drawdown:** will hold through 2-3 red candles IF (a) RSI(9) > 60 (momentum intact) and (b) trend structure has not broken. Will exit if RSI < 40 or ROC crosses below 0.
- **Max daily loss limit:** not formally specified. Implied by structural stops and position sizing discipline.
- **Overnight holding:** NEVER. Always flat at end of session.
- **Leverage preference:** not formally specified. Implied moderate given selective trade frequency and structural stop usage.

### ATR thresholds

| Resolution | Low | Normal | High |
|---|---|---|---|
| 1-min (NY) | < 20 | 20-55 | > 55 |
| 3-min (Asia/London) | < 42 | 42-115 | > 115 |

- Recalibrated 2026-06-17 / settings v37 from live ATR p25/p75 at BTC ~$62k-$67k; bands are RESOLUTION-DEPENDENT since v36 -- 3-min ATR runs ~2.1x the 1-min. Was Low<80/Normal 80-150/High>150, BTC ~$80k-$100k Q1 2026. AvgATR/CurrATR ratio self-calibrates; these are display / cold-start reference bands only.
- Review against CSV log if BTC price moves significantly.
- **THIS BLOCK IS THE SINGLE HOME OF THE ATR BANDS.** They are not in settings.json or the code (only the cold-start anchor indicators.ATR.static_ref is). Change the bands HERE; every other mention points here.

---

## 6. Verdict Engine Design Preferences

- **Preferred verdict style:** conservative -- would rather miss a trade than overtrade. Selective entry philosophy means the engine should flag quality over quantity.
- **Minimum confidence to trade:** MEDIUM or HIGH verdict to act on. Will not act on WEAK signals unless a specific high-conviction setup is visible on chart.
- **Regime preference:** both TRENDING and RANGE_BOUND are acceptable. TRANSITIONAL = reduced size, extra caution. Will not override regime veto rules.
- **Score thresholds:** percentage-based against regime MaxScore (20/19/15 with regime weights enabled; base 19/18/15). Computed as Math.Ceiling(regimeMax x verdictStrong/Med/WeakPct). scoring.verdict_strong_pct / verdict_med_pct / verdict_weak_pct = 0.70 / 0.53 / 0.35 at v68. All pcts configurable via settings.json.
- **False positive tolerance:** low. Prefers engine to say NO TRADE rather than output a weak directional verdict that tempts entry on marginal setups. However, a display (that does not affect scoring) showing this weak directional bias must still be rendered, to help form a future opinion.
- **Performance vocabulary (ruled 2026-09-14):** success rate · gross breakeven rate · net breakeven rate · gross edge · net edge · net EV per trade. Definitions and rules: DeribitIndicatorProject.md §5a. Net EV per trade after fees is the number that decides whether a signal is worth trading.
- **Display preference:** verdict output should be clean, scannable, and fast to interpret at a glance. Not cluttered. Show score breakdown for transparency but headline verdict should be prominent.
- **Config philosophy:** all scoring thresholds and indicator parameters externalised to settings.json (live version: its line 2). No hardcoded magic numbers remain in engine. Hot-reloadable without recompile.

---

## 7. Open Questions / Known Limitations

- **Liq large-penalty threshold** -- monitoring. Review indicators.Liquidations.large_liq_size (200 at v68) against the ~90th percentile of observed LiqLongSize/LiqShortSize in the CSV log.
- **ATR bands** -- review if BTC price moves significantly. The AvgATR/CurrATR ratio approach is self-calibrating, but the absolute Low/Normal/High bands may need updating. The bands live in the ATR thresholds block in section 5.

**Resolved in the 2026-09-14 re-sync** (kept so they are not re-raised):

- **Liq dominanceRatio** -- raised beyond the 1.2-1.5 once considered; indicators.Liquidations.dominance_ratio is 2.0 at v68.
- **MicroCVD accelThreshold** -- dynamic scaling shipped (static floor plus a share of window USD).
- **Funding momentum** -- shipped as the Step 3b modifier; time-anchored window since v53.
- **OI x CVD cross-confirm** -- shipped as Pass 2b.
- **WebSocket upgrade** -- shipped; live on WebSocket since v42, REST is the fallback.
- **AWS London deployment** -- the collector runs on AWS; the ops script tools/ops/collector.ps1 defaults to region eu-west-2 (London).

---

## 8. What This Trader Values in AI Collaboration

- **Communication style:** technical and concise. No hand-holding or excessive explanation of basics. Trader has software engineering background and trading exchange experience. Use correct terminology without over-explaining.
- **Decision process:** spec-first workflow. All strategy and design analysis is done by the current Claude orchestrator (the active seat until the next handover). Decisions are documented in .md files and committed to GitHub before coding begins. Implementer seats build approved specs -- they do not invent design decisions unilaterally.
- **GitHub workflow:** the orchestrator writes proposal .md files and commits them to the repo; implementation follows the approved spec. All docs live in /docs folder of DeribitVerdictEngine repo.
- **Commit workflow:** local-first. Commit locally as work progresses. Push to remote only after the change compiles cleanly AND the trader has tested it. Remote holds tested milestones only; failed compiles or failed tests stay local.
- **Review preference:** always show what changed and why. Changelog entries for every version. Breaking changes flagged explicitly.
- **Proactive flagging:** flag design issues, inconsistencies, or risks proactively before implementing. Do not silently implement something that conflicts with spec intent.
- **When to push back:** when a proposed change would reintroduce a pattern that was deliberately removed (e.g. non-directional padding, double-counting). Cite the version it was removed and why.
- **What to avoid:** do not re-open settled decisions without new data or a concrete technical reason. Do not propose changes that increase indicator correlation (signals should remain as independent as possible).
- **Strategy owner:** all strategy, spec decisions, implementation, code review and debugging are done in Claude. The current orchestrator (the active seat until the next handover) owns strategy. There is no external strategy conversation.
- **Session handover:** follow the CLAUDE.md Session Start Protocol (it includes this file). Do NOT read the entire codebase -- open individual .vb files only when a specific edit is needed. This preserves context budget for actual work.
- **Version history:** see DeribitIndicatorProject.md §15 (most recent five settings versions) and history-archive.md §E (older). Design rationale: architecture.md Design Decisions.
