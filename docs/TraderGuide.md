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
- `BELOW_MIN_MOVE` (v35) — the verdict had a real lean but the realistic move (after target capping) is too small to be worth trading — under ~0.08% of price, roughly $50 at $62k BTC. Displays as `NO TRADE` regardless of how the raw score looked. Most common in low-ATR Asia/London conditions. Not a bug — the engine is refusing to call a trade it can't size meaningfully.
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

**What it shows:** A trade frame showing where a stop, entry, and target would sit at fixed ATR multiples — useful for sizing context, not for actual order placement.

**What to watch for:**

- The `ATR` value tells you how much BTC moves per bar on average right now. A higher ATR = wider, more volatile market.
- Stop/target distances are flat `ATR × multiplier` (1.2x stop / 2.0x target by default) — **not** stretched or compressed by current volatility. Volatility context lives separately in the `size ×N` figure (see below) and in Dynamic Norms' `ATR ratio`.
- `size ×N` is the trader's own sizing formula (`Base × AvgATR / CurrATR`) rendered inline — above 1.0x means current ATR is *below* its rolling average (size up within risk limits); below 1.0x means current ATR is elevated (size down).
- `EXEC <N>m` tags the execution resolution this run used — `EXEC 1m` for NY, `EXEC 3m` for Asia/London (since v36, NY trades faster so it stays on 1-minute bars; Asia/London's typically lower volatility runs on 3-minute bars to keep moves tradeable). ATR, stop/target distances, and the levels below are all computed at that resolution — don't compare a 3m-session ATR reading directly against a 1m-session one.
- Watch for a capped target line (`--> <price>  [<reason>]`) — this means structure (a swing level, an HVN wall, or POC) sits between entry and the raw target, making the raw target likely unreachable. The reason tag tells you which.
- The R:R shown is theoretical at ATR multiples; your real R:R using structural levels will differ.

**Example:** `ATR ENTRY LEVELS  (ATR 24.60  size ×1.89  |  1.2x stop / 2.0x target  |  EXEC 3m)` — current ATR is well below its rolling reference, so `size ×1.89` says you can run nearly double size within your risk rules. The stop/target distances themselves are flat at 1.2×/2.0× ATR regardless. If the target line shows `--> 59333.4  [CAPPED @ 59333.4 (NEAREST_HVN_ABOVE)]`, don't plan to hold to the raw target — scale out near there or reconsider the trade if the capped R:R no longer makes sense. (Reason labels: `SWING_HIGH_5M`/`SWING_LOW_5M`, `NEAREST_HVN_ABOVE`/`NEAREST_HVN_BELOW`, or `POC`.)

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
- A `Notional: ≈ $N · N.N× lev` line follows the contract count — this is the dollar size and implied leverage the suggested contracts represent on a Deribit inverse contract. `[LEV CAPPED]` means leverage, not the dollar risk cap, was the binding constraint (`kelly.max_leverage`, default 5.0×) — the engine would otherwise have sized more contracts than the leverage ceiling allows.

**Example:** `CONFIDENCE: HIGH` → `p(win): 65%` → `Applied fraction: 5.00% [CAPPED]` → `Contracts: 3` → `Notional: ≈ $30 · 3.0× lev`. The engine is at its maximum conviction. The `[CAPPED]` tag means it would suggest more if it could. Scale the `Risk $` to your real account size (displayed value assumes a placeholder account).

---

## 4. Dynamic Norms

**What it shows:** How the engine has adapted its signal thresholds to current session conditions — volume, VWAP deviation, and ATR — rather than using fixed static values.

### Dynamic Norms

**What to watch for:**

- `[LIVE]` tag = the engine is using fresh, adaptive thresholds. Normal operating state.
- `[STATIC FALLBACK]` tag = the engine couldn't compute fresh thresholds — likely a data issue. Treat the verdict with suspicion until the next run returns `[LIVE]`.
- **Vol threshold H and M:** H is the "high volume" bar; a candle must hit this ratio to fire a full volume signal. If H is low (near 2x), even modest volume spikes register — don't over-weight them. If H is high (4–5x), only genuine institutional-scale moves fire.
- **ATR ratio** (relabelled from "ATR scale"): current ATR vs its rolling reference. Below 1.0x = quiet relative to baseline; above 1.0x = expanding. This is now a **sizing-context figure only** — it no longer stretches or compresses the ATR stop/target distances in the entry block (those are a flat `ATR × multiplier` since the v32 display pass). Use it the same way as the ATR Entry Levels `size ×N` figure: low ratio = size up viable, high ratio = size down.
- ATR is resolution-dependent since v36 (1-min on NY, 3-min on Asia/London) — current reference bands: **1-min** Low<20 / Normal 20–55 / High>55; **3-min** Low<42 / Normal 42–115 / High>115. These move with BTC's price regime — treat as a current read, not a permanent constant.

**Example:** `ATR ratio: 0.79x` with `ATR=57.60, ref=72.58` — current volatility is 21% below the rolling average. Stop/target distances in the entry block are unaffected by this number; it's purely a "how hot is the market right now" read for your own sizing.

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
- On Asia/London (3-min execution since v36), the magnitude and slope thresholds that gate a full ROC signal are re-baselined per session (v40/v41): ASIA fires at 0.17, LONDON at 0.11 — Asia genuinely runs hotter on 3-min bars, so the same raw ROC reading means something different by session. NY (1-min) is unaffected.

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
- **OBV (On-Balance Volume):** OBV — the running total of volume flowing in and out — should trend in the same direction as price. OBV rising while price rises = healthy trend. OBV diverging from price (one going up, the other down) = warning signal; the engine can flag this as an exit trigger for open positions.
- Watch for `OBV divergence` in the HOLD/EXIT line if you're already in a trade.

**Example:** Price makes a new high above the Donchian channel but OBV is flat or declining. The engine flags this as a structural divergence. Don't add to longs here — the volume isn't backing the breakout.

---

### Swing Pivots (5m + 15m)

**What it shows:** The most recent confirmed swing high and swing low on both 5m and 15m candles. Drives the structural stop and target lines under the ATR Entry Levels block, and the Layer 1.5 structural-break exit in the HOLD/EXIT line.

**What to watch for:**

- The 5m swing low under your long entry is your structural stop — price breaking it cleanly means the long premise is invalidated. The engine fires `EXIT -- structural break (swing low breach)` when this happens.
- The 5m swing high above your long entry is your structural target. R:R is the ratio printed in the structural row of the entry block.
- 15m swings are context only — they don't drive stops/targets but tell you whether the broader structure agrees. A 5m setup against the 15m swing direction is lower-quality.
- A `STRUCTURALLY_WEAK` context tag fires when swing data exists but no clean target+stop pair can be placed for the verdict direction.

**Example:** `LONG  Stop: 71450  Entry: 71850  Target: 72420  R:R 1.4` reading from the structural row — the 5m swing low at 71450 is your stop, swing high at 72420 is your target. If price drops below 71450 mid-trade, the engine flags `EXIT -- structural break` regardless of what the indicators say.

---

### Trend Structure (HH/HL/LH/LL)

**What it shows:** The classification of the last six confirmed 5m swing pivots into one of five patterns. This is a Pass 2c scoring input — the structure bonus pushes the dominant side of a confirmed-direction setup.

**What to watch for:**

- `UPTREND` (HH+HL — higher high, higher low) — adds +1 to long score when long is dominant. This is the cleanest bull structure read.
- `DOWNTREND` (LH+LL) — adds +1 to short score when short is dominant.
- `EXPANSION` (HH+LL — both higher high and lower low) — range-widening, no scoring change. Treat as a caution flag — the market is in directional disagreement at higher and lower extremes simultaneously.
- `CONTRACTION` (LH+HL — narrowing range) — no scoring change. Often precedes a break; pair with BBW squeeze for breakout posture confirmation.
- `UNDEFINED` — fewer than 2 highs and 2 lows in the lookback. Insufficient structure to classify.

**Example:** `Trend Structure: UPTREND  (HH 102450.0 > 102100.0 | HL 101800.0 > 101500.0)` — both the most recent high and most recent low are higher than their priors. A `LONG` verdict in this state gets the +1 bonus. A `SHORT` verdict in this state would not — the structure disagrees with the direction.

---

### Best Volume Pivot (Display-Only)

**What it shows:** The pivot in the 5m lookback with the highest total volume across its wing window, plus the volume ratio against the average pivot. Not currently used in scoring or cap arbitration — treat as a chart-reading aid.

**What to watch for:**

- `Best Vol Pivot 5m: HIGH 102450.0  (vol×2.3 vs avg pivot)` — the 102450 swing high was made on 2.3× the average pivot volume. That's a stronger structural reference than a same-price level made on average volume.
- When the "best" pivot price differs from the most-recent swing being used as your target/stop, eyeball whether it's a stronger structural level worth referencing for partials.
- Ratios above 2.0× are meaningful; under 1.5× the volume-weighting is barely differentiating from the most-recent pivot.

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

- **OFI (Order Flow Imbalance):** Measures the imbalance between buy and sell orders sitting on the bid vs ask. Positive = buyers dominating. Since v46, on the live WebSocket path the ratio is a **time-weighted average** over the run window (not a single snapshot) — a brief sweep or spoof can no longer flip it; what you're reading is sustained imbalance, not an instant. (Falls back to a single snapshot when running on REST.) **Cosmetic note:** because it's an average-of-ratios rather than a ratio-of-averages, the displayed `Bid Vol` / `Ask Vol` won't always divide out to exactly the displayed ratio — that's expected, not a bug.
- **CVD (Cumulative Volume Delta):** Running tally of buy volume minus sell volume over the session. Positive and rising = sustained buying pressure. Pay attention to the `MicroCVD` sub-reading — if it shows `BULL_DECEL`, the recent buying is slowing down even if the cumulative total is still positive.
- **TFI (Trade Flow Imbalance):** Similar to CVD but measured over a shorter, more recent window. Useful for catching rapid flow reversals.
- All three agreeing (e.g., `OFI: BULL, CVD: BULL, TFI: BULL`) = the engine's cleanest order flow confirmation.
- Two or three of these flipping against your open position simultaneously triggers the `EXIT -- microstructure deterioration` alert in the HOLD/EXIT line — that's a fast exit signal.

**Example:** `OFI: BULL | CVD: BULL | TFI: BEAR` — two of three confirm bulls, but TFI (the most recent window) just flipped bearish. Not enough to exit if you're in a long, but worth watching. If OFI also flips on the next run, you have a microstructure deterioration developing.

---

### OFI Momentum

**What it shows:** Whether the OFI level signal is accelerating or fading over the last few runs. RISING/FALLING/FLAT, computed against a 10-sample ring buffer.

**What to watch for:**

- `OFI: BULL` with `OFI Momentum: RISING` — the bullish flow is intensifying. The engine adds a momentum modifier to the OFI level score.
- `OFI: BULL` with `OFI Momentum: FALLING` — the bullish read is technically still there but losing force. Lower-quality long signal.
- `FLAT` momentum on a strong OFI level is normal — it just means the imbalance has been stable. Not negative.
- A momentum shift (RISING → FALLING in consecutive runs) on a position you're holding is an early warning the flow is rolling over — earlier than waiting for the level signal itself to flip.

**Example:** `OFI: BULL | OFI Momentum: RISING` on the same run as a `LONG` verdict — the buy-side flow is accelerating, not just present. This is the cleanest fresh-entry posture for a long; flow is not just leaning bullish but doing so harder than a few runs ago.

---

### Bid-Ask Spread (WIDE penalty)

**What it shows:** The current bid-ask spread in basis points. When the spread is unusually wide, the engine applies an entry-side penalty — the assumption is that the order book has thinned out, often during a flush or news event, and is not a clean entry environment.

**What to watch for:**

- A `Spread WIDE` flag in the breakdown table means a penalty was applied to the verdict-side score. The verdict may degrade by one tier as a result.
- Wide spread + apparent directional signal often means the move is already half-finished by the time the next candle prints. The engine is biasing toward "no trade" rather than chasing.
- During normal market conditions on BTC-PERPETUAL, the spread sits in the 1–3 bps range. Anything above 5 bps is treated as wide.
- Don't second-guess this penalty — it specifically catches the "looks like a great signal but the book is empty" trap.

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

### Funding Momentum

**What it shows:** The direction of the funding rate change over the last 3 samples (≈3 minutes at 60s polling). RISING / FALLING / FLAT. Acts as an adjunct to the absolute funding signal in Step 3b — amplifies or softens the penalty depending on whether the crowding is intensifying or unwinding.

**What to watch for:**

- `Funding: +0.025% (HIGH)` with `Funding Momentum: RISING` — long crowd is still piling in. The engine amplifies the long-side penalty. Avoid late long entries.
- `Funding: +0.025% (HIGH)` with `Funding Momentum: FALLING` — crowd is unwinding even though the absolute rate is still elevated. Penalty is softened. Short window for a long re-entry if other signals support it.
- `FLAT` is the most common state at any given minute — funding moves slowly relative to 1m candles.
- Like the absolute rate, this is a confidence modifier — it does not block a trade, only nudges score weight.

**Example:** `Funding: -0.018% (LOW) | Funding Momentum: FALLING` — shorts are leaning the market and the short-side rate is becoming more negative (i.e., shorts paying more). Combined, this is a contrarian setup for longs: the short crowd is intensifying its position, classic squeeze-fuel posture if a long signal is otherwise present.

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

## 17. Working with the App

These features sit alongside the per-run output above. They affect how the engine adapts over time and how you investigate its accuracy.

---

### Analysis Report Viewer

**What it shows:** A markdown report joining recent verdicts to subsequent price action — failure rate per verdict tier, hold-window stability, funding momentum diagnostic, OFI outlier audit, OI×CVD asymmetry breakdown.

**Where:** Status bar at the bottom of the main window, link labelled `Analysis Report`.

**What to watch for:**

- **"Hold Window Selection Stats"** — answers the practical question: "STRONG LONG verdicts have been most reliable held for how long?" Per-tier table with the empirically chosen window. Use this to set your default scale-out timing per tier.
- **Failure-rate matrix** — per-tier × window × ATR threshold cells with sample size and 95% confidence interval. Cells with `n < 30` are flagged as insufficient sample; ignore them until more data accumulates.
- **Funding Momentum Diagnostic** — distribution of raw funding-rate deltas, percentile table. Tells you whether the FLAT-heavy FundingMomentum classification is genuine quiet vs threshold mismatch.
- **OFI Outlier audit** — flags rows where OFI Ratio exceeded 100 or 1000. If you see a recurring outlier pattern, raise it as a possible calculation issue.

**When to read it:** After ~500 new rows accumulate, or when verdict accuracy seems off. The auto-tweaker reads the same data programmatically, but the markdown report is the human-readable view.

---

### Tweak Settings — The Auto-Tweaker

**What it does:** Periodically reviews recent verdict accuracy (failure rate against tiered ATR-adjusted forward returns), and when accuracy slips below a threshold, asks Claude Opus to propose targeted settings adjustments. Settings get applied either automatically or after your manual approval, depending on the toggle.

**Where:** Status bar link labelled `Tweak Settings` — opens a non-modal dialog.

**What you control:**

- **Auto-commit on apply** — when on, accepted settings diffs are written to `settings.json` directly. When off, the diff is parked at `tools/AutoTweaker/proposed_diffs/` for you to review and apply manually.
- **Dry-run mode** — when on, the auto-tweaker generates the API call payload as a `.txt` file at `tools/AutoTweaker/dry_run_payloads/` and stops. No API call is made. Useful for testing without burning API credits.
- **Window size (verdicts)** — number of recent verdicts to evaluate failure rate over. Default 120. Must all fall within the same UTC session.
- **Failure rate threshold (%)** — if aggregate failure rate exceeds this, a tweak is proposed. Default 40%.
- **Cooldown rows** — minimum new verdicts between tweak attempts. Default 10.

**Status indicator:**

- `Ready` — all conditions met, "Run Tweaker Now" enabled.
- `Cooldown: N rows remaining` — too few new rows since last run.
- `Waiting for session-aligned window: M/120 rows` — current session hasn't accumulated enough rows yet.
- `Insufficient tier-eligible rows` — too many of the recent verdicts were `NO TRADE` or `WEAK_*`; not enough STRONG/MEDIUM verdicts to compute a reliable failure rate.

**What it will not do:**

- Touch any rejected pattern (fixed-% targets, double-counting setups, dead v15 keys). The applier hard-rejects these regardless of what Claude proposes.
- Disable the MTF gate or the regime-weights gate. Hard-coded blocks.
- Change more than 3 keys per proposal. The 3-key cap is the conservative-bias safeguard.

**Reading the "Last Run" line:**

- `[BELOW_THRESHOLD]` — engine is performing fine, no tweak needed. This is the normal happy state.
- `[INELIGIBLE]` — cooldown / session-not-aligned / insufficient tiers. No action. Not an error.
- `[DRY_RUN_WRITTEN]` — payload file generated, awaiting your manual handling.
- `[PROPOSED]` — diff parked, awaiting manual apply.
- `[APPLIED]` — settings updated, version bumped, change_log appended.
- `[ERROR]` — API call or validation failure. See the summary line for the reason.

The auto-tweaker is a long-loop optimiser. Don't expect frequent tweaks — most of the time, the verdict accuracy is fine and the run reports `BELOW_THRESHOLD`. When a tweak does fire, treat it as a calibration event worth reviewing rather than a routine update.

---

### Settings Snapshots

When the auto-tweaker has run for several consecutive rounds without proposing any settings changes, the engine considers those settings "proven" for the current market conditions and saves a snapshot. If market conditions later revert to a similar pattern, the auto-tweaker may propose reverting to one of these snapshots instead of tweaking fresh keys.

**You can see:**

- **Active snapshot** (if any) and the running streak counter in the Tweak Settings dialog.
- **Full history of saved snapshots** in `settings_snapshots/manifest.csv`. The directory is gitignored.
- **Round-level statistics** for the last 5 rounds via the **Show Round Stats** button inside the Tweak Settings dialog. Each round shows the aggregate failure rate plus per-tier accuracy (STRONG / MEDIUM / WEAK on both sides; NO_TRADE rows are informational only).
- **Open Snapshots Folder** button in the Tweak Settings dialog opens the snapshots directory in your file explorer.

**How snapshots are scored.** Snapshots are bucketed by `regime × volatility tier` (12 buckets total). Only one snapshot is kept active per bucket — when a new finalised snapshot scores higher than the existing one in its bucket, the older one is rotated and its `.json` file is deleted (the manifest row remains as a historical record). Score blends failure rate (weighted heavier — 1 point per percentage point) and streak length (1.5 per round up to 20).

**When a revert proposal fires**, it follows the same `auto-commit` / `dry-run` toggles as a regular tweak. The revert is a wholesale settings replacement, so the per-proposal key cap does not apply — but the snapshot's content is still validated against the rejected-pattern list before any apply.

**Snapshot-related knobs in Tweak Settings:**

- **Snapshot streak X** (default 3) — how many consecutive `BELOW_THRESHOLD` rounds before a snapshot is saved.
- **Max keys per tweak proposal** (default 3) — previously hard-coded; the conservative-bias cap that limits how aggressive a tweak can be. Reverts are exempt by design.
- **Streak weight** (default 1.5) — composite-score weight per streak round, capped at 20 rounds.

---

### Live Performance Strip

A compact strip of six labels updates after every analysis run showing how the engine's directional verdicts have been performing over multiple time windows. Use it to gauge whether the current settings are working across the session or if the recent batch of signals has been noise.

**What each label shows:**

| Label | Window |
|---|---|
| `Cur.Wk` | Monday 00:00 UTC+8 → now |
| `3d` | D-2 00:00 UTC+8 → now (last three calendar days) |
| `Cur.Day` | Today 00:00 UTC+8 → now |
| `Asia` | Most-recent Asia session block (08:00–15:00 UTC+8) |
| `London` | Most-recent London session block (16:00–20:00 UTC+8) |
| `NY` | Most-recent NY session block (21:00–07:00 UTC+8 next day) |

**Reading the colours:**

- **Green** — success rate > 50% in this window. The engine has been calling direction correctly more often than not.
- **Red** — rate ≤ 50%. Signals have been losing or breaking even; tighten style or reduce size.
- `--% ` — fewer than 4 evaluated predictions in the window. Not enough data to trust the rate.

**Hover tooltip** on any label shows the exact sample count and time range, e.g. `12 predictions evaluated. 2026-05-12 08:00 → 2026-05-13 10:45 UTC+8.`

**Session "most-recent block" semantics.** Each session label always shows the most-recently active or completed block for that session — not a rolling 24h average. If you look before today's Asia session has started, the label covers yesterday's Asia block (fully completed). Once Asia opens, it switches to today's running block and grows. This means session rates are directly comparable to your own session-level P&L.

**Success metric.** A verdict is counted as SUCCESS if price reached the displayed target before hitting the structural stop (or ATR-multiple fallback stop), evaluated on 1m bars T+3 through T+15. Ties in the same bar count as FAILURE (conservative-bias rule). `NO TRADE` and `NO TRADE [WEAK X]` verdicts are not counted — only directional signals enter the denominator.

**Cold start.** On first launch the strip shows `--% ` while 7 days of OHLC history loads and the eval cache backfills. This takes a few seconds; the status bar shows "Loading performance history..." while it runs. Subsequent launches load from the cached files in under a second.

**Settings knob.** `performance_display.enabled = false` hides the strip and stops writing the two cache files. Use `min_sample_for_render` to raise the threshold before a rate number appears (default 4).

---

### WebSocket Health

The engine has run live on a WebSocket feed since v42 (replacing REST polling as the primary data path). The status bar carries a health line so you can tell at a glance whether you're looking at fresh data:

- `WS OK · 1/3/5/15 fresh · trades N` — all candle series fresh, normal operating state.
- `WS DEGRADED — REST fallback (stream stale)` — the WS stream went stale for this run; the engine fell back to REST for that single run rather than skip it. Occasional flickers are normal; persistent DEGRADED means the feed is struggling.
- `WS DOWN — reconnecting (Xs backoff, R reconnects)` — disconnected, retrying with exponential backoff. The app keeps functioning on REST fallback while down.

This is a live, in-memory status line, not part of the rendered verdict — it doesn't get logged to CSV or the output dump. Check it if a run looks suspiciously stale or a verdict seems out of step with the tape.

### Exit Guard

While you have a position declared (via the position radio buttons), a separate fast-cadence check runs every few seconds against the live WebSocket feed — independent of your normal auto-run interval — watching for the same fast-exit conditions as the HOLD/EXIT line's microstructure layer (2+ adverse flow signals flipping at once, or a structural swing-level breach).

- **`EXIT GUARD · clear`** — no adverse condition present.
- **`EXIT GUARD · ⚠ EXIT? confirming n/d`** — an adverse condition is present but hasn't held for the debounce window yet (default 2 consecutive checks). Building toward a latch, not yet a signal.
- **`EXIT GUARD · ⚠ EXIT — <reason>`** — latched. An optional alarm sound fires on this transition. This is the same urgency as a HOLD/EXIT fast-exit line, just at tick freshness between full runs.

It's display/alert only — it never changes the verdict, never writes the CSV, and is paused (not silently stale) if the WS feed itself is down or cooling down. Requires `transport=ws`; shows "WS only" on REST.

### On-Close Trigger Mode

By default the engine fires on a fixed interval timer. An alternative **ON-CLOSE** mode (toggle next to SINGLE/REPEAT) fires the analysis the instant the execution-resolution bar closes instead — NY's 1-minute bar, or Asia/London's 3-minute bar — so you get the verdict right at the structural decision point (bar close) rather than waiting up to a full interval for the next poll.

- The interval control relabels to `BACKSTOP` in this mode — it still fires if the bar-close watcher hasn't fired in that long (covers a stalled feed), but it's not the primary trigger.
- `Next close: M:SS  [SINGLE/REPEAT <res>m]` counts down to the next bar boundary — the bracket echoes your SINGLE/REPEAT setting and the active resolution (1m NY, 3m Asia/London).
- Falls back to interval mode automatically if running on REST (`transport=rest`) — on-close detection needs the live WS bar stream.
- This only changes *when* a run fires, not what it computes — same verdict you'd get from a timer fire at that instant.

### Live Microstructure Strip (TAPE)

A continuously-updating one-line strip (toggle: the **TAPE** checkbox) showing fast streaming microstructure *between* full analysis runs, refreshed every couple of seconds:

```
76038 · SL 75920 (-118) | SH 76210 (+172) · TFI BUY +0.42 · 1.8 bps · book 2.3× bid · 4.1 tr/s ($312k/s)
```

- Last price, bracketed by the nearest structural level above and below (carried from the last full run's swing/VPFR levels — not recomputed live).
- TFI, spread, and top-book imbalance, refreshed live.
- Tape speed — trades/sec and $/sec over a short rolling window.

**This is deliberately not a signal.** It's the same raw microstructure inputs the verdict pipeline uses, shown faster and rawer. The full verdict is still the considered, multi-indicator product — don't treat a TAPE reading as a trade trigger on its own; it's there so you're not flying blind on flow between full runs (e.g. while watching a level for entry, or managing a hold).

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
| `BELOW_MIN_MOVE` | Stand aside — realistic move is too small to trade |

## Quick Reference — Regime and Sizing

| Regime | Sizing Guidance |
|---|---|
| `TRENDING_UP` / `TRENDING_DOWN` | Full size; only trade in regime direction |
| `RANGE_BOUND` | Standard size; both directions valid |
| `TRANSITIONAL` | Reduce size; scoring is penalised, conviction is lower |

