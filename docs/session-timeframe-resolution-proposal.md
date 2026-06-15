# Session-Conditional Execution Timeframe (Proposal)

**Date:** 2026-06-15
**Author:** Opus 4.8 (spec-author seat)
**Status:** **APPROVED in principle — trader signed off 2026-06-15** (path B; **3/3/1** Asia/London/NY; weekend overlay OUT; configurable-but-stable; Phase-0 study done). Scoring-affecting (changes the timeframe the verdict is computed on) → spec-first + approval-gated; this doc is the approval artifact. **Settings v35 → v36** when implemented. Coordination hand-off: [`session-timeframe-resolution-spec-writer-brief.md`](session-timeframe-resolution-spec-writer-brief.md).
**Pairs with / follows:** [`min-tradeable-move-gate-proposal.md`](min-tradeable-move-gate-proposal.md) (the gate that surfaced this), [`eval-metric-deconfound-proposal.md`](eval-metric-deconfound-proposal.md) (shares the floor).

---

## 0. Why

The v35 min-tradeable-move gate (0.08% ≈ $49.6 at $62k) correctly suppresses verdicts whose realistic target can't clear slippage. On the **1-min** timeframe in low-vol sessions, that's a large fraction of Asia/London — the gate is telling the truth: 1-min moves there are often too small to trade. The trader's response is to **trade those sessions on a higher timeframe (3-min)**, and the engine should analyze the same timeframe it's advising on. NY stays 1-min.

This is a coherent-timeframe change (the **whole execution-indicator stack** moves to the session's resolution), not an ATR-only swap — a 1-min directional read paired with a 3-min target is incoherent (the 1-min signal can't reach a 3-min target) and would pollute the calibration book with tradeable-size / wrong-direction trades.

## 1. Phase-0 study (DONE 2026-06-15) — drives the resolution picks

28 days of native Deribit BTC-PERPETUAL 1/3/5-min OHLC (40,321 × 1-min bars), ATR(7) per bar, `ATR/price` bucketed by **session × resolution × weekday/weekend**. Gate clears when `2×ATR ≥ 0.08% × price`, i.e. **`ATR/price ≥ 0.04%`**. (Method: native fetch from `public/get_tradingview_chart_data`; `ATR/price` is price-regime-independent so it survives BTC moving off ~$62k. Volume not needed for ATR — the Volume=0 quirk of `ohlc_1m_cache.csv` is irrelevant here.)

**Median `2×ATR` target as % of price / % of bars clearing the 0.08% floor:**

| Session | 1-min WKDY | 3-min WKDY | 5-min WKDY | 1-min WKND | 3-min WKND | 5-min WKND |
|---|---|---|---|---|---|---|
| ASIA   | 0.089% / 56% | **0.191% / 95%** | 0.268% / 99.8% | 0.045% / 30% | 0.105% / 66% | 0.146% / 88% |
| LONDON | 0.079% / 50% | **0.174% / 91%** | 0.244% / 99% | 0.038% / 28% | 0.087% / 55% | 0.130% / 78% |
| NY     | **0.127% / 71%** | 0.262% / 96% | 0.357% / 99.6% | 0.069% / 43% | 0.154% / 85% | 0.213% / 97% |

**Findings:**
1. **Weekday Asia/London 1-min is *marginal*, not dead** — clears ~50–56%, median target sitting right on the 0.08% floor. The "0 predictions" experience the trader saw is largely a **weekend** artifact (Asia/London 1-min clear only ~28–30%) — the same weekday/weekend confound that bit the v34 ASIA `session_volume` change.
2. **3-min is the right lift for Asia + London weekday** (56%→95%, 50%→91%). **5-min is overkill** (≈99%) and costs signal frequency + execution granularity.
3. **NY 1-min already clears 71% weekday** (median target 0.127% = 1.6× the floor) — keep it 1-min (trader preference + adequate).
4. **ATR scales ~2.1× (1→3-min) and ~2.9× (1→5-min)**, consistent across sessions — the seed factor for the §4 threshold profiles.
5. **Weekends stay partly gated even at 3-min** (Asia 66%, London 55%). Accepted: thin weekend tape genuinely has fewer tradeable setups; v1 is session-only (no weekday/weekend resolution split — see §7 open item).

**Resolution decision (v1):** **ASIA = 3, LONDON = 3, NY = 1** — confirmed by trader 2026-06-15. Configurable (it's the trader's risk/strategy call); the data backs 3/3/1.

## 2. Design — session-conditional execution resolution

- The engine reads a per-session `execution_resolution` (1/3/5). On each run it resolves the active session from the UTC hour (reusing the existing `session_volume` bucket bounds: ASIA 0–7, LONDON 8–12, NY 13–23) and fetches + computes the **execution-timeframe stack** at that resolution: ROC, RSI, RSI-divergence, EMA ribbon (9/21/50), Volume SMA, VWAP + bands, BBW/TTM, OBV, CVD/MicroCVD/TFI windows, Donchian, **and ATR(7)**.
- **Regime (5-min DMI/ADX) and MTF gate (15-min) are UNCHANGED** — they're the higher-timeframe bias/veto layer and remain valid above a 3-min execution chart (5m ≈ 1.7× of 3m, 15m = 5× of 3m). This bounds the change and preserves the multi-timeframe ladder.
- **Swing pivots are already 5-min (+15-min context)** — so the *structural* targets/stops are unaffected by execution resolution. Only the **ATR-fallback** target/stop and the momentum/flow stack move. (This is why the gate's effective-target check still composes correctly: structural cap if present, else the now-resolution-correct ATR target.)
- **The gate, ATR levels, eval barriers, and Kelly sizing inherit the resolution automatically** because they all derive from `r.ATR`, which is now `CalcATR(candlesExec, 7)`.
- Live fetch per run becomes: `candlesExec` at the session resolution (replaces the 1-min execution fetch in 3/5-min sessions) + `candles5m` (regime) + `candles15m` (MTF, cached). In a 1-min (NY) session this is identical to today.

### Config shape

Extend each `session_volume.sessions[]` entry with `execution_resolution` (default **1** → absent/unspecified = current 1-min behaviour, zero change):

```json
"sessions": [
  { "name": "ASIA",   "start_hour": 0,  "end_hour": 7,  "high_multiplier": 1.10, "mid_multiplier": 1.05, "execution_resolution": 3 },
  { "name": "LONDON", "start_hour": 8,  "end_hour": 12, "high_multiplier": 1.00, "mid_multiplier": 1.00, "execution_resolution": 3 },
  { "name": "NY",     "start_hour": 13, "end_hour": 23, "high_multiplier": 1.15, "mid_multiplier": 1.10, "execution_resolution": 1 }
]
```

Reuses the single session-bucket definition (DRY — no second source of truth for "what session is it"). Hot-reloadable like every other key. **Off the auto-tweaker surface** (resolution is a strategy/regime choice, not a failure-rate lever) — add to PromptBuilder HARD CONSTRAINT 11's exclusion list alongside `min_tradeable_move_pct` / `kelly.*`.

### Configurable-but-stable (the operating contract)

The key is hot-reloadable, but **execution resolution is a calibration-regime selector, not a daily toggle.** A 3-min ROC of 0.1 is a different event than a 1-min ROC of 0.1, so rows logged under different resolutions are **not poolable**. The discipline: pick a resolution per session and leave it long enough to calibrate; **changing it is a deliberate re-baseline boundary** (like v33/v34), not a preference flip. The data layer (§3) enforces that a mix never silently corrupts pooled stats.

## 3. Data / calibration / eval handling

- **Per-row resolution stamp (mandatory).** Add `ExecResolution` to `analysis_log.csv` (schema **v0.6 → v0.7**, appended column — header-name-based readers tolerate it since F9) and to the eval cache (`analysis_eval_cache.csv` schema **v3 → v4**, appended field). Every consumer that aggregates by session — the live perf strip, `FailureRateMatrix`, the eval re-walk — must **filter by resolution** so "Asia's success rate" is never an average of 3-min and 5-min trades.
- **Calibration surface.** Thresholds are timeframe-sensitive, so each resolution in use needs its own profile (§4). Fixed-per-session 3/3/1 = effectively **two profiles to keep fed** (1-min for NY, 3-min for Asia+London). Free per-day toggling would fragment thin per-session samples across up to 3 sub-tracks each — explicitly **not** the v1 design.
- **Eval barrier walk is timeframe-independent** — it walks 1-min OHLC over wall-clock T+3..T+15 regardless of execution resolution (it's a price walk). Only the `FavBar` *distance* (from the resolution's ATR) changes, which is the point. So `LivePerformanceTracker` needs the resolution stamp + filtering, not a new walk.
- **Auto-tweaker** becomes resolution-aware as a precondition to un-gating (it's already held + session-blind; this adds resolution-blind to the list of things to fix first). The per-row stamp makes the data available when that work happens.

## 4. Threshold profiles (Phase 1 seed → Phase 2 re-baseline)

A `resolution_profiles` block keyed by resolution (`"1"`, `"3"`, `"5"`). `"1"` = the current global values (no change). `"3"`/`"5"` populate **only the timeframe-sensitive keys**; everything unlisted inherits the 1-min global.

**Phase-1 seeding (principled first guess, not calibrated):**
- **Magnitude-type** (scale by the measured ATR ratio ≈ **2.1× for 3-min**, 2.9× for 5-min): `indicators.ROC.MagnitudeThreshold`, `indicators.ROC.SlopeDeltaThreshold`, MicroCVD `accel_threshold` static floor, CVD `slope_min_usd`.
- **Bar-count windows** (keep the bar count; they now span N× the wall-clock — verify, don't rescale by default): ATR period (7), RSI period (9), Volume SMA (9), BBW/TTM windows, Donchian (20), DynamicNorms 100-bar volume / 50-bar VWAP-dev baselines.
- **Likely stable** (relative/structural — leave at 1-min): RSI OB/OS zones, EMA-alignment logic, funding/OI (slow), VWAP-dev (price-relative).
- **Trade-count windows** (CVD/MicroCVD/TFI take from the *trade* stream, not candles): unaffected by candle resolution.

**Honest Phase-1 caveat:** seeded 3-min thresholds will lean **aggressive on the magnitude-gated signals** (ROC especially) until re-baselined — treat Phase-1 Asia/London verdicts as provisional. Phase 2 re-baselines per resolution from accumulated 3-min data (manual, like v33/v34 — the auto-tweaker can't, being resolution-blind).

## 5. Phasing

- **Phase 0 — DONE** (§1): offline ATR/price study → 3/3/1.
- **Phase 1 (engine, the low-hanging fruit):** `execution_resolution` config + per-session fetch at that resolution + the execution stack (incl. ATR) computed on it + `ExecResolution` logged per row + resolution-filtered perf/eval + the `"3"` profile seeded by §4. Regime/MTF untouched. Ships a directionally-coherent 3-min Asia/London engine immediately.
- **Phase 2 (calibrate):** accumulate 3-min Asia/London data; per-resolution re-baseline; teach the auto-tweaker (session × resolution) before any first fire on mixed data.

## 6. Acceptance

- **Phase-0 table reproduced** in this doc (§1) — the resolution picks are data-backed.
- **Resolution selection fixture:** at UTC hour 3 with ASIA `execution_resolution=3`, the engine computes ATR on 3-min candles; a setup that gate-killed at 1-min (ATR ~13 → target ~26) now clears (3-min ATR ~27 → target ~54 > 49.6). At UTC hour 15 (NY, res=1), behaviour is byte-identical to today.
- **Per-row stamp:** `ExecResolution` present in CSV v0.7 + eval cache v4; perf strip / matrix filter by it (a deliberate Asia 3→5 change produces two sub-populations, never one blended rate).
- **Regime/MTF unchanged:** 5-min regime + 15-min MTF identical to v35 for the same inputs.
- `dotnet build` clean; A1–A13 unregressed; new A14 resolution fixtures pass.
- **Sanity on live data:** weekday Asia/London now produce tradeable (mostly non-`BELOW_MIN_MOVE`) verdicts; NY unchanged.

## 7. Open items / out of scope

- **Weekend resolution overlay** (Asia/London 3→5 on weekends to lift 55–66% → 78–88%): **OUT (trader decision 2026-06-15 — trades are rare on weekends).** Weekends stay session-only 3-min and partly gated, which is acceptable (thin tape). Revisit only if weekend trading later proves material.
- **`ResolveSessionLabel` hour-7 off-by-one** (flagged v34): the execution-resolution session resolver must use the **engine** bucket (`ApplySessionVolume`, ASIA = hour 0–7 inclusive), not the display label — fix or reuse the engine path so the resolution boundary matches the gate/eval session boundary.
- **Auto-tweaker resolution-awareness:** required before any first fire on post-v36 mixed-resolution data (compounds the existing session-blind gap).
- **Profile ATR-band recalibration** (Low<80 set for $80–100k): independent display/profile housekeeping.

## 8. Routing

Opus, fresh conversation, this doc as kickoff. Scoring-affecting → this spec is the approval artifact; trader signs off on the 3/3/1 picks (and the weekend-overlay decision) before Phase 1 code. Local commits only; trader tests + pushes. Sequence after the v35 pair is live and the auto-tweaker first fire is resolved (or explicitly re-gated on v36 resolution-awareness).
