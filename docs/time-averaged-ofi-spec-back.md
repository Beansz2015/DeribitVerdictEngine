# Time-Averaged OFI — Implementer Spec-Back (P4 #4)

**Built:** 2026-06-30 against `time-averaged-ofi-proposal.md` (APPROVED 2026-06-29, §10 all-recommended + the tweaker-exposure directive).
**Settings:** v45 → **v46** (two new keys `indicators.OFI.averaging_enabled` + `avg_window_sec`).
**Scope honoured:** **BUILD ONLY.** The OFI dominance-threshold **re-baseline is NOT in this commit** — it is the later, data-gated v47-ish pass (§5 below). This commit ships the cleaner *input* behind a dated dataset boundary; `buy_dominant_ratio` / `sell_dominant_ratio` are **unchanged**.
**Status:** solution(Release) + AutoTweaker + OrderCheck build **0/0**; harness **A1–A19e unregressed + new A20a–h** all pass. Local commit only — trader tests + pushes.

---

## 1. What shipped, file by file

| File | Change |
|---|---|
| `Core/OfiAccumulator.vb` | **New host-agnostic** time-aware EMA accumulator + the `OfiAverageSnapshot` read struct. `Fold(bidVol, askVol, ratio, tsMs, tauSec)` (first fold seeds; thereafter `alpha = 1 − exp(−dt/tau)`), `Reset()`, `CoverageSeconds`, `UpdateCount`, `HasWarmup(minCoverageSec)`, `Snapshot(minCoverageSec)`. Not internally locked — lives on `MarketState`, touched only under its `SyncLock`. No WinForms. |
| `Core/Indicators_OrderFlow.vb` | Extracted **`ComputeOfiImbalance`** (pure weighted bid/ask volumes + the sanity-bounded ratio, NO classification) and **`ClassifyOfiRatio`** (the 3-way dominance compare) from `CalcOFI`. `CalcOFI` is now a thin wrapper over both — **byte-identical** to v45. The accumulator and the run path both fold/classify through these helpers, so there is one source of truth for the cap/floor/weight math. |
| `MarketState.vb` | Owns `_ofiAcc As New OfiAccumulator()`. New `FoldOfi(...)` / `ResetOfiAccumulator()` / `GetOfiAverage(minCoverageSec)`, each under the existing `_lock`. |
| `DeribitWsFeed.vb` | `ApplyBook` now calls `FoldOfiAverage(snap, nowUtc)` after `UpdateBook` — reads `SettingsLoader.Current.Indicators.OFI` (hot-reload-honest), computes the imbalance via `IndicatorEngine.ComputeOfiImbalance`, folds with `tsMs` = receive-time epoch-ms + `tau` = `avg_window_sec`. `SeedAsync` (runs on every connect/reconnect, before `SubscribeAsync`) calls `_state.ResetOfiAccumulator()`. |
| `UI/MainForm_Analysis.vb` (~line 363) | On the WS-live path (`src Is _wsSource` AND `averaging_enabled` AND warmup met) sources `r.OFIRatio/OFISignal/OFIBidVol/OFIAskVol` from the accumulator (`GetOfiAverage` → `ClassifyOfiRatio`); else snapshot `CalcOFI`. `_ofiHistory` + `CalcOFIMomentum` unchanged (the ring now holds averaged ratios on the WS path). |
| `Core/IndicatorResults.vb` | Comment on the four OFI fields noting the WS-path averaged semantics + the ⚠ dataset boundary. No new fields. |
| `Core/Settings/EngineSettings.vb` | `OfiSettings.AveragingEnabled As Boolean = True` (`averaging_enabled`) + `AvgWindowSec As Integer = 10` (`avg_window_sec`). |
| `settings.json` | The two new keys under `indicators.OFI`; bump v45→v46; `change_log` entry (dataset-boundary marker). |
| `tools/AutoTweaker/SettingsDiffApplier.vb` | **Exact-match** reject of `indicators.ofi.averaging_enabled` (mirrors the `scoring.min_tradeable_move_pct` precedent — NOT a prefix, so the sibling OFI keys stay tunable). |
| `tools/AutoTweaker/PromptBuilder.vb` | **HARD CONSTRAINT 16** — never propose `indicators.OFI.averaging_enabled`; explicitly states the siblings (`avg_window_sec`, the dominance ratios, `book_depth`) **remain** tunable. |
| `verify/ordercheck/OrderCheck.vbproj` + `Program.vb` | Links `Core/OfiAccumulator.vb`; new **A20a–h** fixtures. |
| `docs/DeribitIndicatorProject.md` | §6 version → v46; §15 row. |

---

## 2. The mechanism, settled (proposal §4.1 / §10)

- **Feed-side accumulator, true per-update time-weighting.** Every ~100ms book frame folds one sample; O(1); one accumulator object on `MarketState`.
- **Time-AWARE EMA.** `alpha = 1 − exp(−dt/tau)`, `dt` = seconds since the previous fold, `tau = avg_window_sec`. Built this way deliberately — a fixed-alpha EMA would let the effective horizon drift with the (irregular) book-update rate. `dt` is floored at 0 (non-monotonic/same-ms stamps can't produce a negative or >1 alpha); `tau ≤ 0` collapses to a full overwrite (defensive, never configured).
- **The folded scalar is the same `OFIRatio` CalcOFI computes** (via the shared `ComputeOfiImbalance`), so the OFISignal classification, the momentum ring, and the render are unchanged in mechanism. Only the *value* of `r.OFIRatio` moves (averaged vs snapshot).
- **Warmup gate.** `HasWarmup` requires ≥ `avg_window_sec` of fold *coverage* (most-recent minus first fold — fold stamps, not "now", so a stalled feed can't claim coverage it didn't deliver) **and** a small min fold count (`MinWarmupUpdates = 5`, an anti-degenerate floor; trivially met at 100ms cadence). Pre-warmup ⇒ snapshot `CalcOFI` fallback.
- **Reset on (re)connect.** Cleared in `SeedAsync` before subscribing, so no stale pre-disconnect average bleeds across a gap and the warmup fallback re-arms after every reconnect.

---

## 3. Deviations from the proposal (all faithful realisations, flagged for review)

1. **`OFIBidVol` / `OFIAskVol` are EMA'd alongside the ratio, not recomputed from a derived average.** Proposal §4.2 says "source all four from the time-averaged imbalance." The accumulator therefore folds three quantities with the **same** `alpha`: the ratio (the signal of record), and the weighted bid/ask volumes (display/CSV). Consequence to be aware of: on the averaged path `OFIRatio` is the *average of the instantaneous ratios*, while `OFIBidVol`/`OFIAskVol` are the *averages of the volumes* — so `OFIRatio ≠ OFIBidVol/OFIAskVol` on the displayed line (they're equal only in the snapshot path). This is intentional and harmless: the ratio is the primary signal the spec says to average; the bid/ask vols are averaged context. If the re-baseline spec-writer would rather the displayed ratio equal bid/ask, that's a one-line change (derive `ratio = avgBid/avgAsk` instead of folding the ratio) — but note that's **ratio-of-averages**, not the **average-of-ratios** the proposal §4.1 literally specifies, and it would shift the OFIRatio distribution the re-baseline calibrates against. Flagged, not changed.

2. **Average-of-ratios arithmetic-mean asymmetry — RESOLVED 2026-07-01, switched to geometric.** The OFI ratio is multiplicatively symmetric around 1 (a buy-heavy 2.0 and an equally sell-heavy 0.5 are mirror images), but an *arithmetic* EMA is not — this deviation flagged the "mild buy-side lean" (~1.25 on a symmetric book) as something the v47 firing-rate-match might absorb. It didn't wait for v47: a throwaway NY DIAG instrument (2026-06-30, `eee6e4b`, reverted) measured both constructions in lockstep over a 616-row / 5.1h net-flat session and found the lean was **not mild** — 12:1 buy-dominant (arithmetic) vs 1.4:1 (geometric) on the identical book — because the AM≥GM bias is distribution *shape*, not level, so firing-rate-matching (a level-only correction) can't remove it. **`OfiAccumulator` now folds `ln(ratio)` and reads back `Exp(emaLn)`** (same alpha/dt/tau, log space) — see `docs/ofi-geometric-construction-spec.md`. The v47 re-baseline now runs on the geometric distribution.

3. **Fold stamp is the WS receive time, not the book frame's exchange timestamp.** Proposal §4.1 says "seconds since the previous update." `ApplyBook` did not parse the book's own timestamp; using `DateTime.UtcNow` at receive (converted to epoch-ms) is simpler and is what the §4.1 "every update" cadence means in practice. The per-frame receive jitter is negligible vs a 10s `tau`.

4. **`LiveMicrostructureEvaluator` (the #3 TAPE strip) deliberately still uses the snapshot `CalcOFI`, not the average.** The live strip is a *raw, fresher-than-the-run* microstructure readout (proposal trio #3, §11 discipline) — an instantaneous imbalance is the right thing there. The averaging is a property of the *verdict-run* OFI input only. No change to the strip; flagged so it isn't mistaken for an inconsistency.

5. **Display-parity: no card edit.** The OFI breakdown row + the card binding (`MainForm_Render_Cards.vb:3090`) render the same `r.OFIRatio`/`OFIBidVol`/`OFIAskVol` fields — the value source is unchanged, so they render the averaged value automatically. No new/removed/renamed rendered line ⇒ no card-binding obligation under the engine display-string parity rule. Verified, not edited.

---

## 4. Acceptance (proposal §9)

- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck. ✅
- **`averaging_enabled=false` byte-identical to v45.** ✅ — proven by construction (the `If ofiCfg.AveragingEnabled` short-circuit ⇒ `usedAvgOfi` is always false ⇒ `CalcOFI` is the only path, with the same args as v45; the feed's `FoldOfiAverage` returns immediately; the per-connect `ResetOfiAccumulator` touches an unused object with no observable effect) **and** by harness **A20a/A20b** (CalcOFI byte-identical to the pre-refactor 150/15/10.0/BUY-DOMINANT math + the Nothing / zero-total edges).
- **Accumulator/averaging math** — **A20c** steady-state (constant ratio averages to itself, warmup arms), **A20d** time-aware geometric step (`dt=tau` → EMA 1.5500 post-geometric-switch, was 1.6321 arithmetic — proving the `1−exp(−dt/tau)` formula applied in log space), **A20e** warmup gate (5s coverage → not warm; 10s → warm), **A20f** reset re-arms, **A20i** (NEW, 2026-07-01) geometric symmetry (alternating 2.0/0.5 converges to ~1.0, not the ~1.25 arithmetic would give). ✅
- **`CalcOFI` classification unchanged** — existing OFI consumers (A19a imbalance via `LiveMicrostructureEvaluator`) unregressed. ✅
- **Tweaker surface** — **A20g** rejects `indicators.OFI.averaging_enabled` (HARD CONSTRAINT 16), **A20h** accepts `indicators.OFI.avg_window_sec`. ✅
- Host-agnostic — `OfiAccumulator` references no `System.Windows.Forms`. ✅

---

## 5. The re-baseline (NOT done — the data-gated v47-ish pass)

This is the ⚠ core of the proposal (§5) and is **deliberately a separate commit/version**, mirroring v36→v40. The build lands behind the dated dataset boundary (the v46 `change_log` + §15 marker); `OFIRatio` now logs the **averaged** value on the WS majority. The re-baseline spec-writer should:

1. **Collect** post-v46 WS data across the operating cadence + sessions (multi-day, like v40/v41).
2. **Re-derive `buy_dominant_ratio` / `sell_dominant_ratio`** so the time-averaged OFI's BUY/SELL-DOMINANT fire-rate **matches the snapshot-OFI historical rate** (firing-rate-match, the v40/v41 method). Averaging tightens the ratio distribution, so the current 2.0/0.5 (tuned for the spiky snapshot) will under-fire on the averaged ratio — expect both thresholds to move toward 1.0.
3. **Review `OFI.Momentum*`** — `_ofiHistory` now holds averaged (less jumpy) ratios, so RISING/FALLING may fire differently; re-confirm or adjust.
4. Watch the **window↔ratio coupling** (§5 caveat): the dominance ratios are re-derived *for* `avg_window_sec=10`. A manual window change shifts the distribution → re-check the ratios. The auto-tweaker (one validated change per round) re-tunes over subsequent rounds; a manual change does not.
5. Mind **Deviation #1/#2** above when reading the averaged `OFIRatio` distribution (arithmetic-mean-of-ratios; mild buy-lean the firing-rate-match should absorb).

---

## 6. ⚠ Flag for the re-baseline spec-writer — stale ATR bands in the loaded trader-profile

Per the project collaboration rules, the re-baseline spec-writer will consult `docs/trader-profile.md` (and the `crypto-trading-context` skill loads a **bundled copy** of it). **That bundled copy is stale on the ATR bands** — it still reads `Low < 80 | Normal 80-150 | High > 150` (§5), which were calibrated for BTC ~$80k-$100k (Q1 2026).

The **current (v37, 2026-06-17)** bands are resolution-dependent and much lower at BTC ~$62-67k:
- **1-min:** Low < 20 | Normal 20-55 | High > 55
- **3-min (Asia/London exec):** ~Low < 42 | Normal 42-115 | High > 115

The canonical `settings.json` `change_log` (v37) and `docs/trader-profile.md` §5 carry the v37 update; the skill's **bundled** trader-profile snapshot does not. This doesn't affect the *OFI* re-baseline math directly (firing-rate-matching doesn't use the ATR bands), but it's the kind of stale anchor that bites a spec-writer who eyeballs "is this ATR normal?" from the wrong table. **Decision left to the spec-writer:** note it inline, or get the skill's bundled trader-profile copy refreshed to the v37 bands. Surfaced here because this session loaded the stale copy and the discrepancy is easy to miss.

---

## 7. Not done / left for the trader

- **Live verification** — the feed-side fold path (`FoldOfiAverage`), the per-connect reset, and the run-path WS routing are host/feed glue validated by a live WS session (as with the A16/A17 WS work, `DeribitWsFeed` + the WinForms run path aren't harness-compiled; the harness proves the host-agnostic core — accumulator math + the CalcOFI refactor + the tweaker surface). On a live WS run the trader should see the OFI breakdown `Ratio:` value read **steadier** than the old snapshot (transient spikes damped) once ~10s of book coverage has accrued; at `transport=rest` it stays the snapshot value.
- **The re-baseline (v47-ish)** — §5 above; data-gated, its own spec-back + trader sign-off.
- **Follow-on P4 ⚠ items:** #5 aggressor velocity / tape burst (SCORING) and #6 book absorption — the remaining re-baseline upgrades, each its own spec (proposal §12).

---

## 8. Coordinator review — APPROVED (2026-06-30)

Independently re-ran all three Release builds (solution + AutoTweaker + OrderCheck = **0/0**), the OrderCheck harness (**A1–A20h ALL PASS**), and a full diff + card-binding-parity audit. **APPROVED — faithful, host-agnostic, rollback byte-identical.**

Verified:
- **`averaging_enabled=false` byte-identical to v45** — by construction (the `AveragingEnabled AndAlso src Is _wsSource AndAlso … HasWarmup` gate ⇒ `CalcOFI` is the only path with v45 args) **and** A20a (CalcOFI byte-identical to the pre-refactor math).
- **Time-aware EMA / warmup / reset built as specified** — `alpha = 1 − exp(−dt/tau)`, first-fold seed, `dt` floored, **fold-stamp** coverage (a stalled feed can't fake warmup), `Reset()` on every `SeedAsync` (re)connect; accumulator touched only under `MarketState._lock`; no WinForms. Proven by A20c–f.
- **Tweaker surface** — only `indicators.ofi.averaging_enabled` excluded (exact-match, not prefix); `avg_window_sec` + dominance ratios + `book_depth` stay tunable (A20g/A20h, HARD CONSTRAINT 16).
- **Spec §6 clarification held** — the two keys were *added* to the OFI block; `momentum_*` intact. §6/§15 doc bookkeeping correct, no stale change_log sub-lines.

The five §3 deviations are accepted as faithful. Two carry forward:
- **D2 — arithmetic-mean-of-ratios buy-lean (the v47 item to settle).** The averaged `OFIRatio` is an arithmetic EMA of a multiplicatively-symmetric ratio, so a symmetrically-oscillating book averages to ~1.25 not 1.0 (Jensen: arithmetic ≥ geometric; bias grows with per-frame ratio variance). Firing-rate-match restores the overall *rate*, but the **BD/SD symmetry** is the watch — if the split won't land symmetric, switch the accumulator to a **log-ratio / geometric-mean EMA** (implementer pre-registered this). **Recommendation: settle arithmetic-vs-log-ratio *before* serious data collection** — calibrating v47 on a construction we might then change wastes the multi-day collection. Coordinator owns this in the v47 spec.
- **D1 — cosmetic, flag to the trader before live test.** The OFI card (`MainForm_Render_Cards.vb:3090`) renders `ratio {OFIRatio:F2} · bid … ask …`; on the averaged path `OFIRatio` is average-of-ratios while bid/ask are averaged *volumes*, so the displayed ratio won't equal bid/ask. Faithful and harmless, but it looks wrong to anyone cross-checking — not a bug.

Re-baseline (v47-ish) stays data-gated; coordinator drives it. Local only — trader tests + pushes.
