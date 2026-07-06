# Cross-Venue Lead-Lag + External Context — SPEC CANDIDATE

**Status:** SPEC CANDIDATE, authored 2026-07-07 (Fable window, trader-directed — written ahead of need because the current queue may outlast the window). **NOT scheduled.** Roadmap home: **W6-7**.
**Sequencing (binding):** builds only after the current queue clears (#5 correlation gate → funding time-anchored window → #6 absorption) **and** the W6-4 offline ceiling audit has run — the audit tells us how hard to chase combination alpha before adding a new signal class. Build follows the #5 template exactly: display/CSV-only sub-version → collection → evidence gates → scoring sub-version at its own ⚠ boundary.
**Class when it builds:** ⚠ (eventually scoring-relevant); one-⚠-per-window rule applies at every stage.

---

## 1. Why this class is non-marginal (and why nothing else is)

Every signal the engine runs today reads **one venue** (Deribit) — aggressor flow, book state, positioning, price. What no Deribit-only signal can see is **where price discovery is happening right now**. At the 2–15 min hold horizon, BTC price discovery migrates between spot venues (Coinbase/Binance) and derivatives (Deribit/Binance perps, CME during US hours); when spot leads and the perp hasn't followed, that residual is directional information of a *class* the current stack cannot express — not another angle on flow the audit already measured at ≤69% pairwise agreement.

This is the last non-marginal indicator class identified in the 2026-07-06 ceiling assessment (roadmap W6). Anything else — more oscillators, more top-book angles — is marginal by the engine's own redundancy evidence.

## 2. Candidate signals (all directional-only, per the profile's non-directional-reward ban)

| # | Signal | Mechanism | Window |
|---|---|---|---|
| S1 | **Spot→perp return lead** | Short-horizon return on the composite spot mid vs Deribit perp mark, measured at a pre-registered lag grid (1s / 5s / 15s / 60s). Fires when spot has moved and the perp residual hasn't closed — direction = spot's lead direction | seconds–1 min |
| S2 | **Cross-venue aggressor divergence** | TFI-analogue on spot venue trade streams vs the perp's TFI. Spot aggressing one way while perp aggresses the other = spot-led accumulation/distribution | ~30–120 s |
| S3 | **Basis momentum** | Rate-of-change of the perp–spot basis (funding is the 8h-smoothed shadow of this; the raw basis derivative is the sharper positioning read) | 1–5 min |

v1 recommendation: **S1 + S3** (S2 is the most redundant with the existing flow stack — build it only if S1 collection shows the trade streams carry information the return series doesn't).

**Double-counting alarm (profile §4, hard):** S3 correlates with funding by construction. If S3 ever scores, the funding Step-3 modifier and S3 must be reconciled (one scores, the other becomes display/context, or S3 is expressed strictly as the *fast deviation from* what funding already prices). The correlation gate (§5) is the instrument; this sentence is the pre-registered concern.

## 3. Data tiers — including the CME / ETF question (trader ask, 2026-07-07)

**Tier A — free, real-time, WebSocket (the build): crypto spot venues.**
Coinbase (Advanced Trade WS) + Binance (spot WS) public trade + best-bid/ask streams. Sub-second, keyless for public market data, the same engineering shape as `DeribitWsFeed`. This tier carries S1/S2/S3 entirely. Composite spot = size-weighted mid across the two venues (survives one venue's outage).

**Tier B — free, slow-cadence: the CME/ETF *context* that is actually reachable without paying.**
The trader asked whether CME BTC ETF-adjacent feeds can join the picture. Split the instruments first: **CME lists BTC futures** (institutional positioning, basis, the weekend gap); **the spot ETFs (IBIT/FBTC/…) trade on NYSE Arca/NASDAQ** (equities feeds). What's free:
- **CME weekend-gap levels — computable INTERNALLY, no CME feed at all.** CME BTC futures halt Fri 21:00 UTC and reopen Sun 22:00 UTC (summer schedule; winter +1h). Snapshotting the engine's own Deribit index price at those instants approximates the gap bounds to within basis noise. Zero new dependencies; a display/level row (pairs naturally with the §11 #8 level-approach alerts).
- **US spot-ETF net flows — public but DAILY (T+1)** (issuer pages / aggregators; scrape-fragile). A multi-day bias/regime input, cadence-mismatched to a 2–15 min engine by three orders of magnitude.
- **CME COT positioning — weekly.** Same verdict, slower.

**Tier-B ruling (recommendation):** slow-cadence context may NEVER enter Step-2 scoring (a daily datum voting on every 1-min run is non-directional padding by cadence — it would fire identically all session). Ceiling: funding-style **Step-3-class adjunct at most, and only with conditional-outcome evidence**; default disposition is a **display-only context row** (US-hours character noted — ETF/CME information exists 13:30–20:00 UTC, i.e. the NY session only). v1 scope: **CME gap levels only** (internal computation, zero deps); ETF daily flows as a later display row if wanted.

**Tier C — paid, real-time: CME futures + ETF intraday. DEFERRED.**
Real-time CME market data (MDP or broker entitlements) and real-time US equities quotes (for intraday ETF premium/discount vs NAV — a genuine arb-pressure signal) are subscription products with key/entitlement custody on an unattended box — the same operational class the authenticated-Deribit ruling rejected (W4, 2026-07-02). **Revisit trigger:** Tier A ships, survives its gates, AND a Tier-C-specific hypothesis remains — i.e. evidence that US-institutional-hours discovery carries information *beyond* what arb already glues into Coinbase/Binance spot (arbitrageurs propagate CME/ETF flow into spot within seconds; Tier A sees the echo almost as fast as Tier C sees the source). Without that evidence, Tier C buys latency on a channel Tier A already covers.

**Bottom line on the trader's question:** yes for a *more complete picture*, but at the cadences the free data allows — CME gap levels immediately (internal), ETF flows as daily context, and real-time CME/ETF only behind a deferred, evidence-gated paid tier. The minute-horizon lead-lag alpha lives in Tier A.

## 4. Architecture sketch (build-phase detail deferred to the implementation kickoff)

- One host-agnostic feed class per venue (`CoinbaseSpotFeed` / `BinanceSpotFeed`), each the `DeribitWsFeed` pattern: public WS, seed, heartbeat/ping, staleness gate, exponential backoff + storm cooldown. Own thread-safe state store (`SpotMarketState`) — **`MarketState` is not touched**.
- A host-agnostic accumulator for the lag-grid return residuals (the `OfiAccumulator`/`AggressorVelocityAccumulator` precedent: fold on message, read per run, warmup-gated, reset-on-connect). Clock discipline: exchange-stamped times per venue, aligned on local receive-time; lags below ~5s are treated as noise-bound until measured (pre-registered grid, no post-hoc lag shopping).
- Pure `IndicatorEngine` functions; new `IndicatorResults` fields; CSV columns reserved at the **next natural rotation** (the #6-D4 precedent — reserve early, land rotation-free).
- **Failure isolation (hard):** spot feeds down/stale ⇒ fields empty/NORMAL; the Deribit run NEVER skips or degrades because of Tier A. Symbol/unit normalization noted (Binance BTCUSDT quote-asset sizes vs Deribit USD contracts).
- Settings: new `indicators.cross_venue` block, three-tier tweaker surface (flat thresholds ON; feeds/switches/venue lists OFF — HC11 class).

## 5. Evidence gates (the #5/#6 discipline, binding)

1. **Build sub-version:** display (context strip/card row) + CSV only. `scoring_enabled:false`.
2. **Collection:** ≥2–3 weekday session-days including a full NY.
3. **Correlation gate** (the #5 §5.1 rule): Spearman < 0.7 AND fire-overlap < 80% vs the existing stack — S1/S2 vs OFI/TFI/CVD/MicroCVD/aggr-vel; **S3 vs funding + OI** (the pre-registered §2 concern). Fail ⇒ the signal closes honestly as display-only.
4. **Outcome gradient** (the #6 discipline): ≥10pp conditional-outcome spread on n≥30 flagged evaluated rows before any scoring wire-in.
5. Scoring sub-version at its own ⚠ boundary; per-session thresholds via the v40 nullable-override pattern.

## 6. Risks / costs (recorded now so the implementation kickoff inherits them)

Two more always-on sockets to babysit (reconnect storms, API churn, rate limits); cross-venue clock skew bounding the usable lag floor; weekend/holiday character shifts in spot volume; overfit on lag choice (hence the pre-registered grid); maintenance surface growth on a codebase headed for a headless port (feeds must be host-agnostic day one — CLAUDE.md rule). None are blockers; all are why this waits for the queue + W6-4 rather than jumping it.

## 7. Sign-off decisions (tick when this gets scheduled — not now)

| # | Decision | Recommendation |
|---|---|---|
| D1 | Venues: Coinbase + Binance composite vs single venue | **Both** (outage resilience; composite mid) |
| D2 | v1 signals: S1+S3, S2 deferred | **Yes** |
| D3 | Tier B in v1: CME gap levels only (internal); ETF daily flows later, display-only | **Yes** |
| D4 | Tier C: defer with the §3 revisit trigger | **Yes** |
| D5 | Slot: after #6 verdict + W6-4 ceiling audit | **Yes** |
| D6 | CSV columns reserved at the next natural rotation before build | **Yes** |
