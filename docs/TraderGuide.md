# DeribitVerdictEngine — Trader's Guidebook

> **Who this is for:** Experienced BTC perpetual scalpers on Deribit.  
> **What this is not:** A programming manual. All implementation detail has been removed.  
> **How to use it:** Read top-to-bottom on each engine run. Every section answers:  
> *"What does this tell me right now, and what should I do with it?"*

---

## The Output — Top to Bottom

Every time the engine runs, it prints its output in this order:

1. **Verdict Block** — the headline call and confidence level.
2. **ATR Entry Levels** — volatility context and reference trade frame.
3. **Kelly Sizing** — directional conviction expressed as a sizing hint.
4. **Dynamic Norms** — how the engine has adapted its thresholds to current conditions.
5. **Regime** — the broader market structure context (trending vs ranging).
6. **Core Signals (1m)** — momentum, mean-reversion, and volume on the execution timeframe.
7. **VWAP** — where price sits relative to session fair value.
8. **BBW / TTM Squeeze** — whether energy is coiling or releasing.
9. **EMA Ribbon** — trend alignment across timeframes.
10. **Market Structure** — swing highs/lows, Donchian breakouts, and OBV trend.
11. **Open Interest** — whether new money is entering the market.
12. **Order Flow** — who is actually buying and selling in real time.
13. **Liquidations** — forced exits that can fuel or exhaust a move.
14. **MTF Gate** — the 15-minute timeframe alignment check.
15. **Funding** — the cost-of-carry signal for perpetuals.
16. **Signal Breakdown Table** — the full score ledger.

---

## 1. Verdict Block

### VERDICT

**What it shows:** The engine's final directional call — the one-word answer to "long, short, or stand aside?"

**What to watch for:**

- `STRONG LONG` / `STRONG SHORT` — high signal density; this is your primary trade trigger.
- `LONG` / `SHORT` (no qualifier) — solid setup, medium confidence; valid for entry per trader-profile rules.
- `WEAK LONG` / `WEAK SHORT` — partial setup only; use as context, not an entry trigger.
- `NO TRADE` — neither direction has enough confirmation; stand aside.
- `NO TRADE [WEAK LONG]` / `NO TRADE [WEAK SHORT]` — a lean exists but a hard gate killed it (regime mismatch or 15m misalignment); the direction is noted but the trade is blocked.

**Example:** `STRONG LONG` means the majority of indicators are aligned bullish with high cross-confirmation density — this is the cleanest entry signal the engine produces. `NO TRADE [WEAK SHORT]` means the engine saw a faint short lean but the 15-minute timeframe said no; respect the block and wait.

---

### CONTEXT

**What it shows:** A warning label that tells you *why* the verdict might be weaker than its tier suggests. Only appears when something is off — if this line is absent, the setup is clean.

**What to watch for:**

- `MOMENTUM_FADING` — price is still moving in the verdict direction, but the driving force is rolling over. Tighten your profit target and don't chase entries.
- `FLOW_UNCONFIRMED` — the technical structure looks right but real buying/selling pressure hasn't shown up yet. High fakeout risk; wait for flow to confirm.
- `STRUCTURALLY_WEAK` — neither price structure nor order flow has enough evidence. The verdict is a weak lean at best; skip or size tiny.
- **No CONTEXT line** — the setup is balanced and confirmed on both axes. This is the cleanest version of the verdict.

**Example:** `WEAK LONG` with `MOMENTUM_FADING` shown = borderline, lean toward skipping. `WEAK LONG` with no CONTEXT line = cleaner setup, cautious entry is more justifiable.

---

### CONFIDENCE

**What it shows:** A plain-English tier label — `HIGH`, `MEDIUM`, `LOW`, or `N/A` — that maps directly to the verdict strength.

**What to watch for:**

- `HIGH` or `MEDIUM` — the only tiers where your trader-profile rules say act.
- `LOW` — informational only; treat like a watch-and-wait signal.
- `N/A` — verdict is `NO TRADE`; no tier was cleared, or a hard gate fired.

**Example:** `CONFIDENCE: MEDIUM` on a `LONG` verdict = valid entry signal. `CONFIDENCE: LOW` on a `WEAK SHORT` = note it but don't trade it.

---

### SCORE

**What it shows:** The raw point tally for both long and short sides, compared to the maximum possible score for the current market regime.

**What to watch for:**

- The format is `Long N/M | Short N/M` where M is the regime ceiling (15 in choppy markets, 19–20 in trending/ranging markets).
- When the market is in a choppy (TRANSITIONAL) regime, you may see an `(eff.E)` effective score that is lower than the raw score — a penalty has been applied.
- A large gap between raw and effective score means the ADX is borderline; factor that uncertainty into your sizing independently of the verdict tier.
- The side with the higher effective score wins the verdict.

**Example:** `Long 12/20 (eff.10) | Short 3/20 | TRANSITIONAL penalty: -2` — the long side has a solid raw score, but the choppy regime shaved 2 points off. The long lean is real but you're fighting uncertain conditions; size down.

---

### TIME

**What it shows:** The exact local time (UTC+8) when this engine run was processed.

**What to watch for:**

- Use this to confirm the output is fresh, especially if you're running on an auto-refresh schedule.
- Cross-reference with session boundaries: Asian session is 08:00–15:59 local, London is 16:00–20:59 local, New York is 21:00 local onward.
- A stale timestamp means the engine may not have polled fresh data — treat the verdict with caution.

**Example:** If your auto-run fires every 60 seconds but the timestamp shows 3 minutes ago, something stalled. Refresh manually before acting.

---

### LAST TRANSACTED PRICE

**What it shows:** The price of the most recent actual trade on the exchange, captured live — separate from the candle close price used in calculations.

**What to watch for:**

- Compare this to the entry price in the ATR block below it. If they diverge significantly, a fast move happened in the seconds since the last candle closed.
- A large gap (e.g., >$50 on BTC at current volatility) means an impulse is in progress right now — your entry levels are already stale.
- `N/A` means the live trade feed returned empty; treat the whole run with caution.

**Example:** ATR entry shows `76038.0` but Last Transacted Price shows `76120.0` — price has already moved $82 in the time since the candle closed. If you were planning a long, you're now chasing; reassess.

---

### HOLD / EXIT

**What it shows:** Real-time guidance for managing an open position. Only appears when you have a position tracked in the engine.

**What to watch for:**

- `EXIT -- microstructure deterioration` — multiple flow signals just flipped against you simultaneously; you're likely already late to exit.
- `EXIT -- momentum break` — the momentum oscillator just crossed zero against your position; structural exit trigger.
- `TAKE PROFIT -- extreme momentum, tighten stops` — you're running an outlier extension; don't get greedy, bring your stop up.
- `EVALUATE -- ...` — a single warning signal; check the 1m candle action before deciding.
- `HOLD -- momentum intact` — no adverse signals; position is behaving as expected.

**Example:** You're in a long and see `EXIT -- microstructure deterioration (OFI, CVD, TFI)`. Three flow signals flipped against you at once — this is a fast exit signal, not a "wait and see" situation. Hit the market.

---

## 2. ATR Entry Levels

> **Important:** These levels are a **reference frame only**. For actual trade execution, use structural stops (previous swing low/high) and structural targets (previous swing high/low) per your trading rules. Do not place the ATR stop or target as live working orders.

### ATR Entry Levels

**What it shows:** A volatility-adjusted trade frame showing where a stop, entry, and target would sit based on current market volatility — useful for sizing context, not for actual order placement.

**What to watch for:**

- The `ATR` value tells you how much BTC moves per minute on average right now. A higher ATR = wider, more volatile market.
- The `scale` factor shows whether current volatility is above or below its recent baseline. Above 1.0x = expanding volatility; below 1.0x = compressing.
- Watch for the `HVN_CAPPED` warning on the target line — this means a high-volume price level is sitting between your entry and the theoretical target, making the raw target likely unreachable.
- The R:R shown is theoretical at ATR multiples; your real R:R using structural levels will differ.

**Example:** `ATR ENTRY LEVELS (ATR 57.60 x 0.79 scale)` — current volatility is 21% below its recent average. The engine has compressed the stop and target distances accordingly. This is a relatively tight market; your structural stops can likely be tighter too. If the target shows `→ HVN_CAPPED @ 72480`, don't plan to hold to the raw target — scale out at 72480 or reconsider the trade if the capped R:R no longer makes sense.

---

## 3. Kelly Sizing

> **Always advisory.** The Kelly block uses a theoretical win probability, not measured results. Read it as "the engine's directional conviction" not as a position-sizing prescription.

### Kelly Sizing

**What it shows:** A suggested risk fraction and contract count, translated from the engine's confidence level into sizing language.

**What to watch for:**

- `[CAPPED]` in the header means the engine thinks you have more edge than the 5% safety cap allows. Do not try to override the cap — the underlying probabilities are pre-calibration estimates, not measured win rates.
- `[BIAS ONLY — NO TRADE]` means the engine computed a size but the verdict is `NO TRADE`. The number is directional colour only — do not trade it.
- `< 1 contract (stop too wide for min size)` means the current volatility is so high that your full risk budget doesn't cover the minimum contract at this stop distance. The trade is impractical at current sizing.
- The `p(win)` displayed (45–65%) is a rough prior based on confidence tier, not a backtested statistic. Treat it with appropriate scepticism.

**Example:** `CONFIDENCE: HIGH` → `p(win): 65%` → `Applied fraction: 5.00% [CAPPED]` → `Contracts: 3`. The engine is at its maximum conviction. The `[CAPPED]` tag means it would suggest more if it could. Scale the `Risk $` to your real account size (displayed value assumes a placeholder account).

---

## 4. Dynamic Norms

**What it shows:** How the engine has adapted its signal thresholds to current session conditions — volume, VWAP deviation, and ATR — rather than using fixed static values.

### Dynamic Norms

**What to watch for:**

- `[LIVE]` tag = the engine is using fresh, adaptive thresholds. Normal operating state.
- `[STATIC FALLBACK]` tag = the engine couldn't compute fresh thresholds — likely a data issue. Treat the verdict with suspicion until the next run returns `[LIVE]`.
- **Vol threshold H and M:** H is the "high volume" bar; a candle must hit this ratio to fire a full volume signal. If H is low (near 2x), even modest volume spikes register — don't over-weight them. If H is high (4–5x), only genuine institutional-scale moves fire.
- **ATR scale:** Below 1.0x = tight market, stops and targets are compressed. Above 1.0x = expanding volatility, stops and targets are wider. Use this to calibrate your manual position sizing: low ATR scale = larger size viable; high ATR scale = size down.

**Example:** `ATR scale: 0.79x` with `ATR=57.60, ref=72.58` — current volatility is 21% below the rolling average. The engine has tightened the ATR trade frame. You can afford slightly tighter structural stops and larger relative size, within your risk rules.

---

## 5. Regime

**What it shows:** The broader market structure state on the 5-minute chart — whether the market is trending, ranging, or in transition between the two.

### Regime

**What to watch for:**

- `TRENDING_UP` — the engine only scores long signals with full weight. A short verdict in this regime is blocked automatically.
- `TRENDING_DOWN` — the engine only scores short signals with full weight. A long verdict is blocked.
- `RANGE_BOUND` — both directions are valid; mean-reversion signals get extra weight.
- `TRANSITIONAL` — the regime is ambiguous; the engine applies a scoring penalty (1–2 points) to dampen conviction. Size down in this state.
- Watch the `+DI` vs `-DI` spread: a large gap (e.g., +DI 27 vs -DI 9) shows strong directional bias even if ADX hasn't crossed the trend threshold yet. This is the "direction is clean but the trend hasn't been confirmed yet" read.

**Example:** `REGIME: TRANSITIONAL | ADX: 24.3 | +DI: 27.5 | -DI: 9.6` — ADX is just below the trending threshold of 25, but +DI dominates strongly. Watch for ADX to push through 25 over the next few runs; if it does, the regime flips to `TRENDING_UP` and long verdicts get a scoring boost. For now, treat any long verdict with modest conviction and size conservatively.

---

## 6. Core Signals (1m)

### ROC(9) — Rate of Change

**What it shows:** How much price has moved in percentage terms over the last 9 candles, and whether that movement is accelerating, flat, or reversing.

**What to watch for:**

- Positive ROC + `RISING` slope = clean bullish momentum; the engine scores this as a full long signal.
- Positive ROC + `FLAT` slope = still positive but losing steam; partial score only — watch for confirmation from other signals.
- `|ROC| > 0.3%` on a 1-minute candle is a strong single-bar impulse. `|ROC| > 0.6%` is extension territory.
- ROC crossing zero while you're in a position is a structural exit trigger — the engine will flag this in the HOLD/EXIT line.

**Example:** `ROC(9): 0.115 | Slope: FLAT` — price is marginally positive but momentum is stalling. The engine gives this a partial long score only. Paired with `MOMENTUM_FADING` context, this is a signal to stay out rather than buy.

---

### RSI(9)

**What it shows:** Whether price is in overbought or oversold territory on the 1-minute chart, and whether a momentum divergence (price and RSI disagreeing) has been detected.

**What to watch for:**

- RSI above 60 = full long signal. RSI below 40 = full short signal.
- The 45–55 zone is a dead band — RSI here provides no directional edge and the engine scores it as neutral.
- `Div: BEARISH` shown with RSI between 58–64 = soft warning to tighten exits, but not a scored penalty. Only triggers a score penalty when RSI is above 65.
- Use RSI for **trade management**, not entry: while in a long, RSI > 60 = hold, RSI < 40 = exit.

**Example:** `RSI(9): 52.6 | Div: BEARISH` — RSI is in the dead band (no directional score), and there's a divergence flag, but RSI is too low for the penalty to trigger. Read it as a soft caution: momentum is not confirming direction, and the divergence is a background warning. Not a trade entry signal in either direction.

---

### Volume

**What it shows:** The current 1-minute candle's volume in BTC and USD, how it compares to the recent average (SMA), and whether it's high enough to confirm a directional move.

**What to watch for:**

- Volume ratio at or above the `H` threshold (shown in Dynamic Norms) = strong volume confirmation; fires a full volume score when direction and VWAP position also agree.
- Volume between `M` and `H` thresholds = mid-tier; the engine requires additional confirmation before scoring it.
- Volume below 0.7x (shown in red) during price movement = fade warning. Price moving without volume rarely sustains; treat the move with suspicion.
- The USD notional column matters: a low BTC ratio can still represent meaningful participation if the dollar value is large (e.g., > $500K on a 1m candle).

**Example:** `Volume: 0.37 BTC ($28.1K) | vs SMA: 0.16x` — this is extremely low participation. No volume signal fires. Any price movement on this candle is low-conviction; don't chase it.

---

## 7. VWAP

> **VWAP** (Volume Weighted Average Price) — the average price for the session, weighted by how much volume traded at each level. It represents the market's "fair value" for the day.

### VWAP

**What it shows:** Where price sits relative to the session's fair value, and how far it has stretched from the average.

**What to watch for:**

- `[WARMUP]` tag = the session just reset (00:00 or 13:30 UTC); VWAP scores are suppressed for the first ~15 candles. Don't trust VWAP-based signals until the tag disappears.
- Price between VWAP and the s1 band = the engine's cleanest VWAP signal zone; full score fires here.
- Price between s1 and s2 = extended; partial score only, requires cross-confirmation.
- Price beyond s2 = too stretched for a fresh-direction signal in either direction; the engine scores nothing here by design.
- `Dev > 0.3%` = meaningful deviation from fair value. `Dev > 0.5%` = extended, often mean-reverts.

**Example:** `Value: 75995.6 | Dev: 0.056% | s1 band: [75715.9, 76275.3]` — price is sitting 0.056% above VWAP, well within the s1 band. This is the ideal "price hugging fair value" posture for a long setup. The engine can fire a full VWAP long score here with no stretching penalty.

---

## 8. BBW / TTM Squeeze

> **Bollinger Band Width (BBW)** measures how much price is compressing. A squeeze means the market is coiling energy before a move. **TTM Squeeze Momentum** shows which direction that energy is pointing.

### BBW (Bollinger Band Width)

**What it shows:** Whether the market is in a volatility squeeze (energy coiling) or releasing from one.

**What to watch for:**

- `Status: ACTIVE` = market is in the bottom 20% of recent volatility; a breakout is building. Don't fade moves in this state.
- `Status: RELEASING` = the squeeze just fired; a directional move is in progress. The engine boosts signal scores in this state.
- `Status: NONE` = normal volatility conditions; no compression signal.
- Use BBW alongside TTM Direction — a `RELEASING` BBW with a clear TTM direction is the engine's preferred breakout posture.

**Example:** `BBW: 0.004 | Status: NONE` — no squeeze currently active. This is a neutral read; normal scoring applies without any squeeze boost.

---

### TTM Squeeze Momentum

**What it shows:** The directional momentum reading from the TTM Squeeze oscillator — whether bullish or bearish momentum is growing or fading.

**What to watch for:**

- `Signal: BULL_MOMENTUM` or `BEAR_MOMENTUM` = momentum is building in that direction; full score contribution.
- `Signal: BULL_FADING` or `BEAR_FADING` = momentum exists but is decelerating; partial score only, and contributes to a `MOMENTUM_FADING` context tag.
- `Dir: RISING` or `FALLING` tells you which way the histogram bars are moving — use this to confirm the signal.
- A histogram turning from positive to negative (or vice versa) is a momentum crossover; watch for it across consecutive runs.

**Example:** `Histogram=70.26 | Dir=FALLING | Signal=BULL_FADING` — bullish momentum is present (positive histogram) but the bars are shrinking. The engine treats this as a partial long contribution only, and it will add weight to any `MOMENTUM_FADING` context tag. Not the time to add to a long position.

---

## 9. EMA Ribbon

> **EMA** (Exponential Moving Average) — a moving average that weights recent prices more heavily. A "ribbon" uses multiple EMAs of different lengths to show trend alignment across timeframes.

### EMA Ribbon

**What it shows:** Whether the short-term, medium-term, and long-term trend are all pointing in the same direction.

**What to watch for:**

- `BULL` = all EMA layers are aligned upward; full long score.
- `BEAR` = all layers aligned downward; full short score.
- `BULL_WEAK` / `BEAR_WEAK` = partial alignment; one or more layers are out of sync. Partial score only.
- `NEUTRAL` = no alignment; no score contribution.
- Check the 5m EMA200 line separately — if price is above the 200-period EMA on the 5-minute chart, the macro trend is bullish; below it is bearish. This gate can block signals that go against the macro direction.

**Example:** `EMA Ribbon: BULL_WEAK | 5m EMA200: Price above` — short-term trend is mostly aligned bullish but not fully stacked. The macro backdrop (price above 5m EMA200) supports the long direction. A partial score contribution; keep watching for the ribbon to fully align.

---

## 10. Market Structure

### Market Structure

**What it shows:** The pattern of swing highs and lows, whether price is breaking out of a range, and whether volume is confirming the trend direction.

**What to watch for:**

- **Donchian breakout:** Price breaking above the recent range high = bullish breakout signal. Breaking below the range low = bearish. No break = neutral, no score.
- **Swing structure (DMI/HH-HL / LH-LL pattern):** A sequence of higher highs and higher lows confirms a bull trend; lower highs and lower lows confirm a bear trend. Mixed signals = choppy, no clear edge.
- **OBV (On-Balance Volume):** OBV — the running total of volume flowing in and out — should trend in the same direction as price. OBV rising while price rises = healthy trend. OBV diverging from price (one going up, the other down) = warning signal; the engine can flag this as an exit trigger for open positions.
- Watch for `OBV divergence` in the HOLD/EXIT line if you're already in a trade.

**Example:** Price makes a new high above the Donchian channel but OBV is flat or declining. The engine flags this as a structural divergence. Don't add to longs here — the volume isn't backing the breakout.

---

## 11. Open Interest

> **Open Interest (OI)** — the total number of active contracts currently open in the market. Rising OI means new money is entering; falling OI means positions are being closed.

### Open Interest

**What it shows:** Whether new capital is flowing into the market in the direction of the current move, or whether the move is being driven by short covering and position closing.

**What to watch for:**

- Rising OI + rising price = bulls opening new longs; strong, conviction-backed up move.
- Rising OI + falling price = bears opening new shorts; strong, conviction-backed down move.
- Falling OI + rising price = shorts closing (short covering), not new longs. This move has less staying power.
- Falling OI + falling price = longs closing (long liquidation). Weaker move.
- The engine cross-references OI delta with CVD (see Order Flow) for a combined conviction signal; both agreeing produces a scoring boost.

**Example:** `OI Delta: BULL | CVD: BULL` — both new money and the actual buy/sell flow are confirming the long side. This is the engine's strongest OI confirmation state and triggers a score boost above a single-signal read.

---

## 12. Order Flow

> **Order Flow** shows the real-time balance between buyers and sellers based on how orders are actually hitting the market. It tells you who is in control right now.

### Order Flow

**What it shows:** Three flow signals — OFI, CVD, and TFI — each measuring buyer/seller dominance from a different angle.

**What to watch for:**

- **OFI (Order Flow Imbalance):** Measures the imbalance between buy and sell orders hitting the bid vs ask right now. Positive = buyers dominating. This is the fastest, most current of the three.
- **CVD (Cumulative Volume Delta):** Running tally of buy volume minus sell volume over the session. Positive and rising = sustained buying pressure. Pay attention to the `MicroCVD` sub-reading — if it shows `BULL_DECEL`, the recent buying is slowing down even if the cumulative total is still positive.
- **TFI (Trade Flow Imbalance):** Similar to CVD but measured over a shorter, more recent window. Useful for catching rapid flow reversals.
- All three agreeing (e.g., `OFI: BULL, CVD: BULL, TFI: BULL`) = the engine's cleanest order flow confirmation.
- Two or three of these flipping against your open position simultaneously triggers the `EXIT -- microstructure deterioration` alert in the HOLD/EXIT line — that's a fast exit signal.

**Example:** `OFI: BULL | CVD: BULL | TFI: BEAR` — two of three confirm bulls, but TFI (the most recent window) just flipped bearish. Not enough to exit if you're in a long, but worth watching. If OFI also flips on the next run, you have a microstructure deterioration developing.

---

## 13. Liquidations

**What it shows:** Recent forced position closures (liquidations) and which direction they're coming from — bullish liquidations (shorts being wiped out) or bearish liquidations (longs being wiped out).

**What to watch for:**

- A cluster of bearish liquidations (long liquidations) during a down move = forced selling is adding fuel to the drop, but also potentially nearing exhaustion if the cluster is large.
- A cluster of bullish liquidations (short liquidations) during an up move = short squeeze fuel; the move can accelerate rapidly.
- Liquidations after an extended move (rather than at the start) can signal exhaustion — the weak hands have been cleared.
- The engine uses liquidation data to add context to directional signals; a move backed by liquidations on the opposing side is treated as stronger.

**Example:** Price drops sharply and you see a large cluster of long liquidations flagged. This is cascading forced selling — the move has real momentum behind it. But if this comes after a 30-minute sustained drop, the liquidation pool may be thinning and a bounce is more likely.

---

## 14. MTF Gate

> **MTF** = Multi-TimeFrame. The MTF Gate checks whether the 15-minute chart agrees with the 1-minute signal. It acts as a hard filter — even a perfect 1m setup gets blocked if the 15m disagrees.

### MTF Gate

**What it shows:** Whether the 15-minute timeframe is aligned with the 1-minute verdict. If it isn't, the engine can block the trade entirely.

**What to watch for:**

- `PASS` = 15m is aligned with the 1m direction; the gate is open, verdict stands.
- `BLOCK` = 15m disagrees; the verdict is overridden to `NO TRADE [WEAK X]` regardless of how clean the 1m setup looks. Respect this block — counter-trend scalping on 1m against 15m structure has poor expectancy.
- The 15m gate uses EMA alignment, trend direction, and momentum on the higher timeframe — it's not a single indicator.
- A `BLOCK` that clears on the next run (15m catches up) is the ideal entry sequence: wait for the gate to open, then enter on the next confirmed 1m signal.

**Example:** `VERDICT: STRONG LONG` with `MTF Gate: BLOCK` → output reads `NO TRADE [WEAK LONG]`. The 1m setup is clean but the 15m is still pointing down. Stand aside. When the MTF Gate flips to `PASS` and the next 1m run still shows a long verdict, that's your entry window.

---

## 15. Funding

> **Funding rate** — in perpetual futures, traders on the long side pay traders on the short side (or vice versa) every 8 hours. A high positive funding rate means longs are paying shorts; high negative means shorts are paying longs.

### Funding

**What it shows:** Whether the current funding rate is aligned with, neutral to, or working against your trade direction — and whether it is significant enough to affect scoring.

**What to watch for:**

- **Positive funding (longs pay shorts):** The market is leaning long. Slightly bearish contrarian signal — longs are crowded. The engine may reduce the long score or boost the short score.
- **Negative funding (shorts pay longs):** The market is leaning short. Slightly bullish contrarian signal — shorts are crowded.
- **Near-zero funding:** Neutral; no impact on scoring.
- Extreme funding (very high positive or negative) is a stronger signal; the engine scales the modifier based on magnitude.
- Funding is most relevant for **trade bias** on holds — if you're in a long and funding is sharply positive, you're paying to hold and the crowd is with you, which means a reversal could be sharp.

**Example:** `Funding: +0.025% (HIGH)` — longs are paying a high rate to hold. The engine applies a modest penalty to long scores as a contrarian signal. This doesn't block a long trade but is a reminder that the long side is crowded. If you enter long, keep your target tighter than usual.

---

## 16. Signal Breakdown Table

**What it shows:** The full itemised score ledger — every indicator that scored, what it scored (`[L]` for long, `[S]` for short, `[L*]` / `[S*]` for partial, or a penalty), and the running total that matches the `SCORE` line in the Verdict Block.

**What to watch for:**

- The `TOTAL` row at the bottom must match the `SCORE` line in the Verdict Block. If it doesn't, the run may have had a data issue.
- Look at the balance: are most `[L]` hits from structural indicators (VWAP, EMA, Donchian) or from flow indicators (OFI, CVD, OI)? A verdict supported by both is stronger than one supported by just one category.
- Penalty lines (shown as negative scores) are as important as positive hits — they tell you what the engine is worried about.
- `[L*]` / `[S*]` = partial scores that didn't get upgraded. A verdict with many partials and few full hits is weaker than a verdict with mostly full hits, even at the same total score.
- Use this table to understand **why** the engine called what it called — not just what it called.

**Example:** `VERDICT: LONG | SCORE: Long 11/20` — looking at the breakdown, 7 of those 11 points are from flow signals (CVD, OFI, TFI, OI Delta) and only 4 from structure (VWAP, EMA). This is a flow-heavy long. It can work, but is more vulnerable to a sudden flow reversal than a structure-heavy verdict with the same score. Consider a tighter stop.

---

## Quick Reference — Verdict Action Rules

| Verdict | Confidence | Action |
|---|---|---|
| `STRONG LONG` / `STRONG SHORT` | `HIGH` | Primary entry signal |
| `LONG` / `SHORT` | `MEDIUM` | Valid entry signal |
| `WEAK LONG` / `WEAK SHORT` | `LOW` | Informational only — no trade |
| `NO TRADE` | `N/A` | Stand aside |
| `NO TRADE [WEAK X]` | `N/A` | Gate fired — stand aside, note the lean |

## Quick Reference — Context Tags

| Context Tag | What to Do |
|---|---|
| *(no tag)* | Setup is confirmed — proceed per verdict |
| `MOMENTUM_FADING` | Tighten target, don't add size |
| `FLOW_UNCONFIRMED` | Wait for flow confirmation before entering |
| `STRUCTURALLY_WEAK` | Skip the trade |

## Quick Reference — Regime and Sizing

| Regime | Sizing Guidance |
|---|---|
| `TRENDING_UP` / `TRENDING_DOWN` | Full size; only trade in regime direction |
| `RANGE_BOUND` | Standard size; both directions valid |
| `TRANSITIONAL` | Reduce size; scoring is penalised, conviction is lower |

