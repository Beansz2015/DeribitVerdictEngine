# Handover: Next Opus 4.7 Session (2026-05-01)

**Purpose:** Brief a fresh Opus 4.7 session on the current state of the DeribitVerdictEngine project, what's in flight, and what to do after the in-progress v22 calibration accumulates enough data.

**Read first** (project session-start protocol):

1. `CLAUDE.md` (auto-loaded)
2. `docs/DeribitIndicatorProject.md` (project handover)
3. `docs/architecture.md`
4. `docs/trader-profile.md`

This handover is a **state snapshot** — for full context on any specific item, read the referenced spec file in `/docs`.

---

## 1. Current State (one-paragraph version)

The engine is at **v22** of `settings.json`. All seven indicator/feature specs (`bid-ask-spread`, `ofi-momentum`, `dynamic-microcvd-accel`, `vpfr-lite-v2`, `swing-pivots`, `settings-exposure-pass`, `analysis-log-csv-expansion`) plus the `api-resilience-pass`, `v17-followup-fixes`, `v19-calibration-tuning-pass`, `v20-oi-threshold-recalibration`, `v20-rsi-roc-algorithm-fixes`, and `v22-funding-calibration-pass` are **shipped and tested**. The user is currently running calibration accumulation under v22 to populate `analysis_log.csv` for eventual auto-tweaker analysis (Section 16.1 of project handover). No active spec is awaiting implementation.

**Master HEAD:** commit ~`5204495` (push of v22 spec; v22 settings landed via subsequent Sonnet commit).

**settings.json version:** 22 (`modified_by: funding-calibration-v22`).

**Build status as of last check:** clean, 0 warnings, 0 errors.

---

## 2. Recent Activity Narrative (compact)

Late April → early May 2026 sequence, in order:

- **2026-04-27** — v15 dead-code cleanup (settings.json + 5 commit chain: dead fields, settings keys, dead const, render colour bugs, VWAP helper). Doc sync to v15 in `DeribitIndicatorProject.md` + `architecture.md`.
- **2026-04-27** — Initial spec batch drafted: bid-ask-spread, ofi-momentum, vpfr-lite-v2, swing-pivots + post-WebSocket/post-calibration backlog file.
- **2026-04-27** — Settings-exposure audit found 19 hardcoded scoring constants. New spec `settings-exposure-pass-proposal.md`. Section 16 added to `DeribitIndicatorProject.md` documenting future auto-tweaker + dual-interface (CLI/WinForms) plans.
- **2026-04-27** — Dynamic MicroCVD acceleration threshold spec added (was deferred from initial batch).
- **2026-04-29** — All 6 indicator specs implemented by Sonnet 4.6 (v17). Code review found two minor issues: VerdictContext STRUCTURALLY_WEAK precedence (fired before MOMENTUM_FADING) and Section 16.3 ✅ marker. Both fixed in `v17-followup-fixes`.
- **2026-04-29** — `analysis-log-csv-expansion-proposal.md` shipped (v0.3 schema): added VerdictContext, FundingMomentum, OiCvdOutcome columns to CSV + 3 distribution sections to CalibrationReport.
- **2026-04-29** — `api-resilience-pass-proposal.md` shipped (v18): retry-once + skip-on-failure for transient Deribit/Cloudflare errors. Funding rate, book summary became nullable. `_skipCount` surfaced in status bar.
- **2026-04-30** — First calibration audit: 618-row CSV revealed 6 columns stuck on a single value. v19 (`v19-calibration-tuning-pass`) shipped: lowered funding bands, funding momentum threshold, OI change threshold, ROC slope_sensitivity; widened liq trade fetch from 100 → 500. v20 added a manual OI threshold recalibration (0.003 → 0.002) after another audit.
- **2026-04-30** — v21 (`v20-rsi-roc-algorithm-fixes`) shipped: rewrote `CalcRSIDivergence` (was over-firing 80% non-NONE due to direction-inverted comparison + missing overbought/oversold gate); split ROC `slope_sensitivity` into `slope_delta_threshold` (0.05) + `magnitude_threshold` (0.1).
- **2026-05-01** — v22 (`v22-funding-calibration-pass`) shipped: regime-aware funding band recalibration based on Deribit's 1m/7d/8h funding rate charts. Funding low ±1 bp, high ±8 bp, momentum_threshold 1 bp. Spec acknowledges polling cadence may miss sub-minute spikes (8h chart shows single-tick spikes that 60s polls miss — long-term WebSocket question).

User is currently running calibration accumulation under v22.

---

## 3. Spec Inventory — Status Table

All in `/docs`. ✅ = SHIPPED (status flag in spec header).

| Spec file | Status | Covered |
|---|---|---|
| `bid-ask-spread-proposal.md` | ✅ IMPLEMENTED 2026-04-29 | New SpreadBps + WIDE-spread entry-side penalty |
| `ofi-momentum-proposal.md` | ✅ IMPLEMENTED 2026-04-29 | OFIMomentum modifier on existing OFI level signal |
| `dynamic-microcvd-accel-proposal.md` | ✅ IMPLEMENTED 2026-04-29 | Self-scaling MicroCVD acceleration threshold |
| `vpfr-lite-v2-proposal.md` | ✅ IMPLEMENTED 2026-04-29 | VAH/VAL + nearest HVN/LVN walls |
| `swing-pivot-proposal.md` | ✅ IMPLEMENTED 2026-04-29 | 5m + 15m swing pivots, 3-tier Step 5b cap, structural-break exit |
| `settings-exposure-pass-proposal.md` | ✅ IMPLEMENTED 2026-04-29 | 19 scoring constants lifted to settings.json |
| `analysis-log-csv-expansion-proposal.md` | ✅ IMPLEMENTED 2026-04-29 | v0.3 CSV schema + 3 calibration distribution sections |
| `api-resilience-pass-proposal.md` | ✅ IMPLEMENTED 2026-04-30 | Retry + skip-on-failure for REST fetches (v18) |
| `v17-followup-fixes-proposal.md` | ✅ IMPLEMENTED 2026-04-29 | VerdictContext precedence + Section 16.3 ✅ split |
| `v19-calibration-tuning-pass-proposal.md` | ✅ IMPLEMENTED 2026-04-30 | First empirical threshold pass (funding bands, OI, ROC, liq window) |
| `v20-rsi-roc-algorithm-fixes-proposal.md` | ✅ IMPLEMENTED 2026-04-30 | RSI divergence rewrite + ROC sensitivity split (settings became v21) |
| `v22-funding-calibration-pass-proposal.md` | ✅ IMPLEMENTED 2026-05-01 | Regime-aware funding band recalibration |
| `post-websocket-post-calibration-backlog.md` | Standing reference | Items deferred until WebSocket migration or CalibrationReport READY |

**No active in-flight spec** as of handover-write time.

---

## 4. Calibration Data State

**Sample sizes accumulated:**

- v18 baseline: 618 rows (audit basis for v19)
- Post-v19/v20/v21: 499 rows (audit basis for v22)
- Post-v22: in progress at handover-write time

**Key observations from prior audits:**

| Column | Pre-tuning state | After tuning | Notes |
|---|---|---|---|
| `FundingBias` | 100% NEUTRAL (v18, v19) | TBD post-v22 | Should differentiate during regime variation |
| `FundingMomentum` | 100% FLAT (v18, v19, v21) | TBD post-v22 | Stays FLAT when rate is genuinely stable; threshold at 1 bp |
| `OISignal` | 100% NEUTRAL pre-v19 | TBD post-v20 | Threshold lowered to 0.2% in v20 |
| `OiCvdOutcome` | 100% NONE pre-v19 | Should fire post-v20 OI tuning | Cascades from OISignal |
| `LiqLongSize/ShortSize/LiqSignal` | 100% zero | TBD post-v19 (500-trade window) | Liquidations may still be sparse if BTC quiet |
| `ROCSlope` | 94% FLAT (v18) | ~70-80% FLAT (v21 split) | slope_delta_threshold 0.05 |
| `RSIDivergence` | 80% non-NONE (v18) | ~10-20% non-NONE (v21) | Algorithm rewritten with overbought/oversold gates |
| `MTFGatePass` | 97.7% True | Same | Working correctly — block rate 2.3% reflects regime alignment |

**Important caveat (from v22 spec Section 1d):** during quiet basis sub-windows, FundingMomentum staying FLAT is **correct** — quiet IS flat. The point is that thresholds shouldn't fire constantly during noise. The auto-tweaker (Section 16.1) needs accurate signal, not over-firing.

**Polling cadence ceiling:** the 8h funding rate chart shows some spikes are single-tick width (sub-minute). At 60s REST polling, the engine misses these. v22 calibration is correct; the residual gap is upstream and needs WebSocket migration to fix.

---

## 5. Immediate Next Actions (this week)

### 5a. Continue calibration accumulation under v22

Target prerequisites for `CalibrationReport READY` (from `DeribitIndicatorProject.md` Section 12):

- ≥300 total rows logged
- ≥3 distinct sessions (Asia / London / NY UTC buckets)
- ≥3 regimes covered (TRENDING_UP, TRENDING_DOWN, RANGE_BOUND each ≥50 rows)
- ≥2 liquidation events captured

Auto-run interval recommendation: 60s (per v18 API resilience spec — 10s causes excess timeouts under transient API failures).

### 5b. Periodic audit (every ~500 rows)

When user reports back with CalibrationReport output, audit the new distributions:

1. **Stuck columns:** any column showing 1-2 distinct values across 500+ rows is a calibration mismatch. Compare to actual data range in CSV; propose threshold tweak via a `vNN-calibration-tuning` follow-up spec.
2. **Over-firing columns:** any column firing >70% on a single non-default value is too sensitive. Loosen.
3. **Right distribution:** broadly varied output across CSV — auto-tweaker prerequisite met for that column.

If CalibrationReport flags `READY` in the in-app calibration link, that's the signal to begin auto-tweaker work (Section 5c).

### 5c. Liquidation count promotion to settings (optional, low priority)

If 500-trade window proves insufficient (post-v22 audit shows still 0 liq events across regime mix), spec a small follow-up:

- Add `cfg.Indicators.Liquidations.TradeCount` (re-introducing the v15-removed key)
- Wire through call site
- Test 1000-count window

Defer until empirical data justifies it.

---

## 6. Mid-Term Work (post-calibration, when CalibrationReport READY)

### 6a. Auto-tweaker pipeline (Section 16.1 of project handover)

The big one. Once calibration data is informative, build the frontier-LLM auto-tuning loop. **Spec needed first** — covering:

- **Failure definition** (Section 16.1 open question): what counts as "failure" in a verdict? Candidates: STRONG verdict followed by adverse 5-15 min price action; NO TRADE that would have been profitable; WEAK verdict that whipped within hold window. Each definition implies different CSV columns and trigger thresholds.
- **Window size:** 50 / 100 / 200 analyses for the failure-rate calculation.
- **Failure-rate threshold:** 25% / 40% / etc. — calibration-bound.
- **API contract:** what payload to send to the frontier LLM (settings.json + recent CSV slice + verdict accuracy metrics), what to expect back, how to merge.
- **Cooldown logic:** prevent thrash. Min interval between auto-tweaks tied to row accumulation.
- **Audit gate:** confirm settings exposure is complete (Section 16.3 prerequisite item 2 — already ✅).

This is **Opus 4.7 medium territory** — design-sensitive, not mechanical. Don't punt to Sonnet for the spec; do punt for the implementation.

### 6b. CSV-to-future-price correlation analysis

Required to define "failure" for 6a. Either:

- Add per-row T+5min and T+15min price columns to CSV (schema bump, requires another expansion pass), OR
- Build an offline analysis script that joins `analysis_log.csv` with subsequent price action (cleaner — no schema change to the live engine).

Decision tree:

- If CalibrationReport shows context tags well-distributed → favour offline script (tag-vs-price correlation) since the engine columns are now informative.
- If context tags still skewed → another threshold pass first.

### 6c. Trader-profile re-read before any auto-tweaker work

The trader-profile (Section 6 — Signal & Decision Philosophy) emphasises:

- Conservative bias, prefer "no trade" over weak signals
- LOW false-positive tolerance
- MEDIUM or HIGH conviction minimum to act
- Won't act on weak signals

Auto-tweaker tuning recommendations should respect these — don't let it optimise toward higher action rate at the cost of false-positive growth. Bias toward verdict thresholds that maintain or *raise* the false-positive bar.

---

## 7. Long-Term Roadmap

### 7a. WebSocket migration (Section 16.4 of project handover)

The architectural ceiling. Triggers when:

1. The current REST-polling indicator backlog is exhausted, OR
2. A specific Section A item from `post-websocket-post-calibration-backlog.md` becomes a priority (spread momentum, aggressor velocity, order book absorption, liquidation × OFI flip, VPFR profile shape)

Don't spec preemptively. WebSocket is a foundation rebuild, not a feature addition. Significant regression surface — only worth doing when latency becomes the binding constraint on accuracy.

### 7b. Dual-interface (CLI + WinForms) — Section 16.2

Long-arc plan: a Linux CLI port for the auto-tweaker pipeline, alongside the existing WinForms desktop interface. Architecture is already partly host-agnostic (`IAutoRunTimer` interface, host-agnostic Core/, settings hot-reload). Remaining work:

- Output rendering: split RTF helpers from MainForm_Render_*; create CLI plaintext renderer
- State plumbing: move `_oiHistory` / `_fundingHistory` / `_ofiHistory` / MTF cache / `_prevRegime` from `MainForm` partial fields into a host-agnostic `EngineState` class
- Auto-run scheduling: implement `LinuxCliAutoRunTimer : IAutoRunTimer` without `Control.Invoke`

KIV until the auto-tweaker (6a) ships and benefits from headless deployment.

### 7c. Section A items in post-WebSocket backlog

Spread momentum, aggressor velocity, order book absorption, liquidation × OFI flip detector, VPFR profile shape (D/P/b/bimodal). All gated by WebSocket migration. See `docs/post-websocket-post-calibration-backlog.md`.

### 7d. Section D items (spec-only deferred, no infrastructure prereq)

- HH/HL/LH/LL trend structure classification (builds on shipped swing pivots)
- Volume-weighted pivot ranking
- 5m RSI divergence (separate from the 1m fix in v21)
- Donchian × BBW state cross-reference
- Smart OBV (volume-weighted by price change)
- MFI vs RSI evaluation

Promote individually if specific calibration data flags a problem each addresses.

---

## 8. Open Questions / Parked Decisions

### 8a. Polling cadence vs WebSocket

v22 spec acknowledges 60s polls miss sub-minute funding spikes. Two responses:

- **Tighten polling** (e.g., 30s) — risks API resilience layer triggering more skips; trader-profile mentions selective trading, so very-frequent polling has diminishing returns
- **WebSocket migration** — proper fix, larger effort

Don't decide until calibration data shows whether the missed-spike loss is material. The `analysis_log_skip_log.csv` (not yet built; see B5 of post-WebSocket backlog) would help quantify this.

### 8b. v23+ threshold reviews

v22 may need follow-up tuning if:

- CalibrationReport shows FundingBias still firing on >50% of rows during typical regime (means LOW threshold still too aggressive at 1 bp)
- CalibrationReport shows ROCSlope still >85% FLAT (means slope_delta_threshold needs lowering further from 0.05 to 0.03)
- Other columns drift after their initial fix

**Don't preempt.** Let the next 500-1000 rows of data speak.

### 8c. RSI divergence v21 validation

v21 rewrote `CalcRSIDivergence` to require overbought/oversold pivot + price testing/exceeding pivot. Expected to drop from 80% to 5-15% non-NONE. **Verify in next audit.** If still over-firing, may need to tighten further (overbought 70 instead of 65, or rsiDelta 7 instead of 5).

### 8d. Out-of-spec doc commits

Sonnet 4.6 has historically taken initiative to update `DeribitIndicatorProject.md` Section 12 (calibration backlog) when shipping CSV-expansion specs, even when the handover instructed "don't update DeribitIndicatorProject.md beyond what the spec specifies." Content was correct; workflow deviation was minor. If you spec another doc-touching change, decide explicitly whether to permit/forbid Section 12 updates in the handover text.

---

## 9. Conventions and Workflow

### 9a. Spec lifecycle

1. Opus 4.7 drafts spec → commits to `/docs` as `<name>-proposal.md` with `Status: PROPOSED`
2. User reviews, possibly tweaks
3. Sonnet 4.6 medium implements → flips status to `APPROVED <date>` then `IMPLEMENTED <date>` after merge
4. User manually tests → confirms or flags
5. User pushes to remote (Opus pushes docs only; Sonnet never pushes per trader-profile Section 8)

### 9b. Settings.json conventions

- `version` field strictly monotonic. Bump on every settings change.
- `change_log` array — append, never edit prior entries
- `modified_by` short slug naming the spec
- Keys removed/added documented in the change_log entry
- Format: terse multi-line strings preferred for bulleted change_log entries

### 9c. Commit conventions

- Subject under 70 chars, imperative mood
- Body: descriptive paragraph(s), then optional bulleted list, then `Co-Authored-By: Claude <model> <noreply@anthropic.com>`
- Doc commits: drafted by Opus, often pushed directly per established pattern (no test gate for pure docs)
- Code commits: Sonnet drafts → user tests → user pushes

### 9d. Trader profile constraints

Critical, do not violate:

- No double-counting (`trader-profile.md` Section 4)
- No non-directional padding
- ATR for sizing, structural for execution
- Conservative bias / low false-positive tolerance
- Reject Stoch, MACD, CMF, fixed-% targets, ATR-based stops for execution

Push back on any proposed change that would re-introduce a deliberately-removed pattern.

---

## 10. Key File References

**Project state:**

- `docs/DeribitIndicatorProject.md` — master handover. Section 16 covers future-direction (auto-tweaker, dual-interface). Section 12 is the calibration backlog.
- `docs/architecture.md` — data flow, file inventory, design decisions table
- `docs/trader-profile.md` — preferences, rejected approaches, collaboration rules
- `docs/post-websocket-post-calibration-backlog.md` — items deferred until WebSocket / CalibrationReport READY

**Settings:**

- `settings.json` — v22 (latest), top-level: `indicators`, `scoring`, `kelly`, `regime_gates`, `regime_weights`, `mtf_gate`, `auto_run`, `session_volume`, `network`
- `Core/Settings/EngineSettings.vb` — POCO contract for settings.json
- `Core/Settings/SettingsLoader.vb` — singleton loader with FileSystemWatcher hot-reload

**Engine entry points:**

- `UI/MainForm_Analysis.vb` — `RunAnalysisAsync()` orchestrator
- `Core/ScoringEngine_Calculate_Verdict.vb` — `Calculate()` main scoring entry point
- `Core/ScoringEngine_Calculate_Scoring.vb` — `RunScoringPipeline()` Steps 2 / Pass 2 / Pass 2b / Pass 2c / 3 / 3b
- `AnalysisLogger.vb` — CSV writer (v0.3 schema)
- `UI/MainForm_Render_Header.vb` — top render block + `BuildCalibrationReport()`

**Latest calibration data:**

- `bin/Debug/net8.0-windows/analysis_log.csv` — current accumulation (v22 era)
- Click "Calibration Check" link in app status bar to render `CalibrationReport` against current log

---

## 11. First Action for the New Session

After reading the four protocol files (Section start of this handover):

1. Run `git log --oneline -5` and verify HEAD matches latest pushed commit
2. Check `settings.json` version field — confirm v22 (or higher if the user has shipped further passes)
3. Ask the user: "What's your CalibrationReport row count and current verdict (READY / NOT YET READY)?"
4. Based on answer:
   - **NOT YET READY:** wait for more data, optionally do a periodic audit per Section 5b
   - **Approaching READY (e.g., 250+ rows, 2-3 sessions):** prep the auto-tweaker spec draft (Section 6a)
   - **READY:** proceed with auto-tweaker spec immediately

Don't propose new indicator work pre-calibration. The bottleneck is data, not code.

---

## 12. Versioned Outstanding Items After Calibration

Once `CalibrationReport = READY`, these items unlock in priority order:

1. **Failure-definition spec** (gate for auto-tweaker)
2. **Auto-tweaker pipeline spec** (Section 16.1)
3. **CSV-to-future-price offline analysis script** (informs failure-definition)
4. **v23+ calibration tuning** (if any remaining stuck columns surface)
5. **WebSocket migration spec** (only if Section A items become priority)
6. **Dual-interface (CLI port) spec** (only if auto-tweaker benefits from headless)

Items 1-3 form a logical bundle — likely a single Opus session to spec, then Sonnet implementation per piece. Items 4-6 are independent; can be picked up at any time post-calibration.

---

**End of handover.** New session should read this top-to-bottom, then read the four protocol files, then proceed to Section 11.
