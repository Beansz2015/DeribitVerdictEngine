# Pre-Aug-1 Opus batch — execution summary

**Executed:** 2026-07-30 (Opus orchestrator). **Spec:** `docs/pre-aug1-opus-batch-2026-07-31.md`, run top-down.
**For:** relay to the Fable seat for the final double-check. **This orchestrator's review does not replace that double-check** — §7 lists everything flagged rather than resolved.

**All five lanes ran. Nothing was skipped, nothing was blocked.** Item A's tick was given at handoff.

| Item | Outcome | Commits | Gate |
|---|---|---|---|
| **A** VWAP session anchor (§7.5) | ✅ built, fixture-pinned, re-validated | `ff1d34c` `87bc5b7` `3aa5a8f` | GATE PASSED |
| **B** Pooled-file report runner + RUN | ✅ built, fixture-pinned, one RUN done | `23af962` `a5ba2e1` `4fbc782` | GATE PASSED |
| **C** Forming-bar investigation | ✅ report written, zero code touched | `14e2821` | GATE PASSED |
| **D** TweakSettingsForm tooltip lifetime | ✅ fixed (the chip had NOT been run) | `7dbb58f` | GATE PASSED |
| **E** Geometry-session grids | ✅ 4 runs, raw attached, zero conclusions | `9273416` (overlays only) | n/a — runs, not code |
| **F** Ops list | reminders only — §6 | — | — |

**Nine local commits, none pushed.** Settings untouched throughout — **still v63**, no config keys added by any lane. Every build Release. Fixture families consumed: **A45** (lane A), **A46** (lane B) — **next free is A47**. Harness A1–A46a all green on every gate run.

---

## A. VWAP session-anchor parameterization (§7.5)

**The one authorized engine edit.** `GetSessionCandles` gained `Optional nowUtc As DateTime? = Nothing`; `CalcVWAP` / `CalcVWAPBands` thread it through; `ReplayLoop` passes `curUtc` (the bar close — already that loop's "now", it drives the `IsFresh` gates). Omitting the argument is `DateTime.UtcNow`, so **live is byte-identical to every prior version**.

Fixture **A45a** pins four things, one of which matters more than the others: sub-check (iv) reproduces the §8.6 defect itself — the default call on a historical set falls through to the whole-list fallback and provably differs from the correct anchored answer. That makes A45a a regression test rather than a tautology; it fails if anyone ever re-hardwires the anchor.

Re-ran the §7.1 validation window (2026-07-29 12:00 → 07-30 08:00 UTC, 840 joined rows, same frozen live pair, same historical store). Full write-up: `docs/backtest-overlap-validation-2026-07-30.md` **§10**; spec-back `docs/backtest-synthesizer-spec-back.md` **§9**.

**The VWAP family, §9 → §10:**

| Column | §4 (pre-§7.1) | §9 (post-§7.1) | **§10 (anchor fix)** | Δ vs §9 |
|---|---:|---:|---:|---:|
| **VWAPSessionCandles** | 50.24 % | 69.29 % | **100.00 %** (mean/max \|Δ\| = 0) | **+30.71 pp** |
| VWAP | 72.62 % | 43.93 % | **56.19 %** | +12.26 pp |
| VWAPSigma1Lower | 71.31 % | 40.71 % | **55.00 %** | +14.29 pp |
| VWAPSigma2Lower | 70.71 % | 39.29 % | **53.45 %** | +14.16 pp |
| VWAPSigma1Upper | 82.14 % | 47.98 % | **55.00 %** | +7.02 pp |
| VWAPSigma2Upper | 81.19 % | 47.62 % | **54.52 %** | +6.90 pp |
| VWAPDevPct | 55.24 % | 36.07 % | **40.60 %** | +4.53 pp |
| OBVTrend / OBVDivergence | 97.38 / 95.24 % | 71.43 / 84.17 % | 71.43 / 84.17 % | **0.00 pp** |
| Verdict agreement | 71.07 % | 74.17 % | 74.05 % | −0.12 pp (one row) |
| Tier agreement | 79.64 % | 81.55 % | 81.43 % | −0.12 pp (one row) |

**The §9.5 root cause is closed.** `VWAPSessionCandles` at 100.00 % with mean and max \|Δ\| of exactly **0** across all 840 rows means the session *window* is byte-identical to live's on every row — the whole-list fallback branch no longer fires under replay anywhere.

**Two things did not go as §7.5 expected. Both are flagged, not resolved:**

1. **The VWAP *values* did not recover to EMA-class.** §7.5 expected "toward the EMA-class levels" (99 %+); actual is 53–56 %, still under §9.6's <60 % "do not use" cut. With the window provably exact the residual cannot be an anchor error — it is numeric edge sensitivity *inside* a correct window (a volume-weighted mean over up to 240 bars whose last bar is the §7.1 forming stub). The NumTight tolerance for this class is ≈ **$6.4** at BTC 64 k, which one near-zero-volume terminal bar can exceed. **Whether the next move is code or a re-think of the tolerance class for long-window accumulators is a spec-first question — not decided here.**
2. **OBV did not move at all**, unchanged to the row. Consistent with §9.5's root-cause note (`CalcOBV` normalises by `candles.Skip(1).Average(Volume)`, independent of the VWAP anchor). §7.5 said "may partially recover — report, don't chase"; it didn't, and it wasn't chased.

**Clearance status recorded, NOT ruled.** §7.5 withheld the VWAP-sensitive study class until the anchor fix re-validated. It re-validated with an **exact window and inexact values**. Funding is untouched (`FundingMomentum` 22.02 %, identical — the anchor is not on the funding path). The clearance call belongs to the Fable/trader seat.

---

## B. Pooled-file report runner

**Host decision (the spec left this to the implementer with an instruction to state it): a `report` verb on `tools/BacktestRunner`, not a new `tools/ReportRunner/`.** BacktestRunner already links six `analysis/` files including `DeribitOhlcFetcher` — the forward-bar fetch path the spec points at — so completing `AnalysisRunner`'s dependency set cost **four `<Compile Include>` lines**. A standalone project would have duplicated ~30 includes and needed its own verify-gate build-set entry, re-learning the F10 lesson for nothing. `AnalysisReportForm.vb` deliberately excluded (WinForms viewer; BacktestRunner is zero-WinForms).

```
BacktestRunner report --csv <analysisLogCsv> [--settings <settings.json>]
```

Thin shell over the **shipped** `AnalysisRunner.Run(csv, outDir, cfg)` — the same call the in-app link makes. **Zero changes to the in-app path.** Two deliberate differences: the CSV is an argument, and the output lands *beside* the input rather than in the repo root. One structural edit: `--from`/`--to` were required for every verb; `report` derives its range from the CSV, so the guard is now gated on `cmd <> "report"` and the other three verbs are byte-unchanged. A failed forward-OHLC fetch exits 1 with the banner on stderr.

Fixture **A46a** drives the real `Load → PopulateForwardBars → FailureRateMatrix.Compute → BandLadder.Compute → MarkdownReportWriter` chain from a CSV on disk and asserts §2 + §9 render with the fixture's own numbers. **What it does not cover, stated plainly:** the network hop — the OHLC map is synthetic so the fixture is deterministic. That hop is the pre-existing shared path, exercised for real by the RUN.

Spec-back: `docs/pooled-report-runner-spec-back.md`.

### B.1 The RUN — pooled snapshot

Built per `aws-collector-deploy-checklist.md` §4.3b from the frozen 2026-07-30 pair. **Header equality verified first** — both files byte-identical 111-column v0.8 headers, both ragged-free. **Dedup: local-preferred per UTC session-hour.**

| Quantity | Value |
|---|---:|
| Local rows | 8,338 |
| AWS rows (raw) | 7,079 |
| Local UTC session-hour buckets | 206 |
| AWS rows kept (hours local missed) | 5,001 |
| AWS rows dropped (same-hour as local) | 2,078 |
| **Pooled snapshot** | **13,339 rows** |
| Range | 2026-07-03 15:57:49 → 2026-07-30 08:48:02 UTC |

`[DeribitOhlcFetcher] Fetched 38498 bars across 8 chunk(s) for 2026-07-03T15:57Z → 2026-07-30T09:34Z.`
Populations: **NY|1 9,780 · LONDON|3 1,897 · ASIA|3 1,662 — zero excluded.**

### B.2 §9 band ladder — RAW, no interpretation

Reproduced verbatim from `analysis_report_20260730_142821.md`. **The F1 read is fenced; nothing below is interpreted here.**

```
## 9. Band ladder (diagnostic — includes untraded WEAK)

### NY×1 · horizon 15m

| Band   | n    | Success | CI              |
|--------|------|---------|-----------------|
| STRONG |  120 |  42.5% | [34%–51%]     |
| MEDIUM |  495 |  34.5% | [30%–39%]     |
| WEAK   | 1128 |  41.8% | [39%–45%]     |

### LONDON×3 · horizon 45m

| Band   | n    | Success | CI              |
|--------|------|---------|-----------------|
| STRONG |   53 |  39.6% | [28%–53%]     |
| MEDIUM |  121 |  52.1% | [43%–61%]     |
| WEAK   |  321 |  38.9% | [34%–44%]     |

### ASIA×3 · horizon 45m

| Band   | n    | Success | CI              |
|--------|------|---------|-----------------|
| STRONG |   12 |  75.0% | [47%–91%]     |
| MEDIUM |  112 |  43.8% | [35%–53%]     |
| WEAK   |  273 |  45.4% | [40%–51%]     |

### POOLED · per-row horizon

| Band   | n    | Success | CI              |
|--------|------|---------|-----------------|
| STRONG |  185 |  43.8% | [37%–51%]     |
| MEDIUM |  728 |  38.9% | [35%–42%]     |
| WEAK   | 1722 |  41.8% | [40%–44%]     |
```

**Artifacts** (session scratchpad — not in the repo):
`…/scratchpad/pooled_dedup_20260730.csv` · `…/scratchpad/analysis_report_20260730_142821.md` · `…/scratchpad/analysis_summary_20260730_142821.csv`

---

## C. Forming-bar live investigation

`docs/forming-bar-live-investigation-2026-07.md`. **No code changed in any file.** Options enumerated, none recommended.

The §7.2 question splits into two with different answers:

- **Is it specified? Yes, explicitly.** The v44 on-close spec named "closed bars only" a **non-goal** in so many words: *"same handling of the forming last bar … that would change the computation (a re-baseline) and is out of scope."* The carry predates v44 — on-close inherited it and deliberately declined to touch it, on grounds still correct. Mechanism verified in code: `ApplyChartTick`'s `s.Add(c)` roll branch is exactly what `DetectBarRoll` fires on, so at an on-close fire the last element is the **new** bar, 1–2 s old.
- **Was the magnitude understood?** Nothing in the spec, spec-back or §12 quantifies it, and it is large.

**The indicator table splits by *which field* of the last bar is read** — sharper than "everything is polluted". Close-based indicators (EMA / ROC / RSI / BBW / TTM) read the stub's Close = live price and are **fresher** for it. Volume-weighted (VolumeRatio / OBV / VPFR) and range-based (ATR / DMI) reads are **understated**. Donchian and the swing pivots are immune — Donchian because someone wrote `Skip(Count − period − 1)` that way, corroborated by its **100.00 %** synthetic↔live agreement in the overlap validation.

**Quantified** on the 13,339-row pooled snapshot:

- The §7.2 "VolumeRatio 0.0002" fingerprint is **not an outlier** — it sits in the 10th–25th percentile of bar-aligned NY rows. Median VolumeRatio on bar-aligned rows: **0.0088** (res-1) / **0.0010** (res-3).
- Share of rows reaching VR ≥ 0.5 climbs monotonically with bar phase in **both** resolutions (res-1 5.8 / 9.1 / 13.6 %; res-3 1.6 / 3.1 / 13.5 %). That gradient is the mechanism, visible in the data.
- **At most 2.01 % of the pooled book can carry ANY volume vote** (upper bound — computed at the lowest possible dynamic clamp). The trader's own 3× breakout rule fires on **0.18–0.89 %** of live rows by session.
- **Counterfactual from the historical store's CLOSED bars** — same instrument, same period, same formula: median VR **0.525–0.668**, and the 3× gate fires **5.50–8.47 %**.

| Population | live p50 VR | closed p50 VR | live ≥ 3.0 | closed ≥ 3.0 | suppression |
|---|---:|---:|---:|---:|---:|
| NY×1 | 0.0081 | 0.525 | 0.89 % | 8.47 % | **≈ 9.5×** |
| LONDON×3 | 0.0017 | 0.653 | 0.53 % | 5.50 % | **≈ 10×** |
| ASIA×3 | 0.0008 | 0.668 | 0.18 % | 7.25 % | **≈ 40×** |

Sampling differences and the known ~2.5 % WS volume undercount are nowhere near large enough to account for a 65–800× gap in the median.

**Honest about limits:** the CSV logs no `TriggerMode`, so roll vs backstop fires are separated only by a bar-phase proxy, and the late-phase band is enriched with backstop fires from quiet/degraded periods. The **ATR and ROC** cross-band differences therefore mix a stub effect with a selection effect and are reported as **inconclusive**, not as evidence. Only the VolumeRatio gradient is used as mechanism evidence.

**Four options, no recommendation:** close-bar-only slice (maximal continuity break; also makes every close-based indicator staler) · stub-aware indicator variants (scoped break; narrowest useful version is one function) · status quo (perfect continuity — the only zero-cost option) · add `TriggerMode` to the CSV and measure first.

---

## D. TweakSettingsForm tooltip lifetime

**The pending chip had NOT been run** — `minTierTip` was still a method-local at `UI/TweakSettingsForm.vb:724`. Now `_minTierTip`, a form-level field, per the `c508d93` pattern. Swept the whole dialog and the rest of `UI/` + `analysis/`: **this was the only remaining method-local ToolTip in the repo.**

Tooltip text, placement and every other control unchanged — a lifetime fix, not a content change. Display-parity **exempt** (a dialog tooltip is a live status element with no snapshot or card surface). No fixture — GC lifetime is not harness-observable.

---

## E. Geometry-session grid runs — RAW, zero conclusions

Frozen copy of the current live book taken first (freeze rule): `bin/Debug/net8.0-windows/analysis_log.csv` → `…/scratchpad/frozen_live_20260730_laneE.csv`, **8,340 rows, 2026-07-03 15:57:49 → 2026-07-30 09:51:02 UTC**. Settings baseline v63; every knob not swept inherits live.

**The Friday session interprets. The DIVERGENT / overfit guard-rails printed in each report speak for themselves and are reproduced below.**

### E.1 W6-1 LONDON stop grid — `stop_max_atr_mult` 1.6:2.2:0.2

**Full book** (8,340 eligible rows, 2026-07-03 15:57 → 07-30 09:51 UTC) — `…/scratchpad/laneE/w61_full/whatif_report_20260730_144148.md`

```
| rank | cell | n | EV full | σ | EV (sel) | EV (holdout) | flag |
|---|---|---:|---:|---:|---:|---:|---|
| 1 | stop_max_atr_mult=1.6 | 677 | -0.570 | 1.682 | -0.432 [-0.596,-0.269] n=404 | -0.774 [-0.973,-0.576] n=273 | ◆ winner ⚠ DIVERGENT |
| 2 | stop_max_atr_mult=1.8 | 677 | -0.624 | 1.769 | -0.455 [-0.626,-0.284] n=404 | -0.873 [-1.083,-0.664] n=273 | ⚠ DIVERGENT |
| 3 | stop_max_atr_mult=2   | 677 | -0.667 | 1.855 | -0.467 [-0.646,-0.289] n=404 | -0.964 [-1.184,-0.744] n=273 | ⚠ DIVERGENT |
| 4 | stop_max_atr_mult=2.2 | 677 | -0.697 | 1.943 | -0.488 [-0.674,-0.302] n=404 | -1.006 [-1.239,-0.773] n=273 | ⚠ DIVERGENT |
```
Population shift: NY×1 499→499 · LONDON×3 135→135 · ASIA×3 43→43 (all `=`). BELOW_MIN excluded: baseline 502 / overlay 502.

**`--from 2026-07-08`** (7,535 eligible rows, 07-08 09:42 → 07-30 09:51 UTC) — `…/scratchpad/laneE/w61_from0708/whatif_report_20260730_144157.md`

```
| rank | cell | n | EV full | σ | EV (sel) | EV (holdout) | flag |
|---|---|---:|---:|---:|---:|---:|---|
| 1 | stop_max_atr_mult=1.6 | 603 | -0.614 | 1.684 | -0.785 [-0.985,-0.586] n=270 | -0.474 [-0.656,-0.293] n=333 | ◆ winner |
| 2 | stop_max_atr_mult=1.8 | 603 | -0.675 | 1.771 | -0.885 [-1.095,-0.674] n=270 | -0.505 [-0.695,-0.316] n=333 |  |
| 3 | stop_max_atr_mult=2   | 603 | -0.732 | 1.859 | -0.975 [-1.197,-0.754] n=270 | -0.535 [-0.733,-0.337] n=333 |  |
| 4 | stop_max_atr_mult=2.2 | 603 | -0.764 | 1.949 | -1.017 [-1.251,-0.783] n=270 | -0.559 [-0.766,-0.353] n=333 |  |
```
Population shift: NY×1 428→428 · LONDON×3 132→132 · ASIA×3 43→43 (all `=`). BELOW_MIN excluded: baseline 435 / overlay 435.

### E.2 Geometry 4-cell — `target_arbitration_mode` {0,1} × `use_best_pivot_candidate` {0,1}

**Full book** (8,340 eligible rows) — `…/scratchpad/laneE/geom_full/whatif_report_20260730_144207.md`

```
| rank | cell | n | EV full | σ | EV (sel) | EV (holdout) | flag |
|---|---|---:|---:|---:|---:|---:|---|
| 1 | target_arbitration_mode=1, use_best_pivot_candidate=1 | 607 | -0.518 | 1.628 | -0.339 [-0.506,-0.171] n=363 | -0.785 [-0.985,-0.585] n=244 | ◆ winner ⚠ DIVERGENT |
| 2 | target_arbitration_mode=1, use_best_pivot_candidate=0 | 621 | -0.534 | 1.630 | -0.367 [-0.533,-0.202] n=375 | -0.789 [-0.989,-0.589] n=246 | ⚠ DIVERGENT |
| 3 | target_arbitration_mode=0, use_best_pivot_candidate=1 | 675 | -0.550 | 1.695 | -0.394 [-0.560,-0.228] n=404 | -0.782 [-0.980,-0.583] n=271 | ⚠ DIVERGENT |
| 4 | target_arbitration_mode=0, use_best_pivot_candidate=0 | 677 | -0.570 | 1.682 | -0.432 [-0.596,-0.269] n=404 | -0.774 [-0.973,-0.576] n=273 | ⚠ DIVERGENT |
```
Population shift: NY×1 499→443 ▼ · LONDON×3 135→126 ▼ · ASIA×3 43→38 ▼. BELOW_MIN excluded: baseline 502 / **overlay 572**.

**`--from 2026-07-08`** (7,535 eligible rows) — `…/scratchpad/laneE/geom_from0708/whatif_report_20260730_144216.md`

```
| rank | cell | n | EV full | σ | EV (sel) | EV (holdout) | flag |
|---|---|---:|---:|---:|---:|---:|---|
| 1 | target_arbitration_mode=0, use_best_pivot_candidate=0 | 603 | -0.614 | 1.684 | -0.785 [-0.985,-0.586] n=270 | -0.474 [-0.656,-0.293] n=333 | ◆ winner |
| 2 | target_arbitration_mode=0, use_best_pivot_candidate=1 | 604 | -0.594 | 1.694 | -0.793 [-0.992,-0.594] n=268 | -0.435 [-0.618,-0.252] n=336 |  |
| 3 | target_arbitration_mode=1, use_best_pivot_candidate=1 | 545 | -0.574 | 1.628 | -0.797 [-0.998,-0.597] n=241 | -0.397 [-0.581,-0.213] n=304 |  |
| 4 | target_arbitration_mode=1, use_best_pivot_candidate=0 | 554 | -0.589 | 1.629 | -0.801 [-1.002,-0.601] n=243 | -0.424 [-0.606,-0.242] n=311 |  |
```
Population shift: NY×1 428→428 · LONDON×3 132→132 · ASIA×3 43→43 (all `=`). BELOW_MIN excluded: baseline 435 / overlay 435.

### E.3 Two mechanical observations about the OUTPUT (not readings of it)

Recorded because they affect how the tables are *read*, not what they mean:

1. **The winner cell differs between the two spans on the geometry grid** — full-book picks `(1, 1)`, `--from 2026-07-08` picks `(0, 0)`, which is the live baseline. On the W6-1 grid both spans pick `1.6`, also the live baseline.
2. **The `--from 2026-07-08` runs report the winner's population shift as `=` in both grids**, which follows mechanically from the winner being the baseline cell there. The full-book geometry winner is the only one of the four runs with a non-trivial population shift.

**Reproducibility:** both overlays are committed (`9273416`) at `tools/WhatIfRunner/overlays/w61-london-stop-grid.json` and `geometry-4cell-mode-x-pivot.json`, per the overlays README's stated purpose. **This was a judgment call not literally in item E** — flagged in §7.

---

## F. Ops list — reminders only (trader-executed)

- **Push cadence.** The nine batch commits are local. **Note: the pre-batch stack was ALREADY pushed** — `HEAD == origin/master` at batch start, so the item-F "push before opening lanes" reminder was already satisfied.
- **AWS redeploy with v63** at the next RDP — same-settings discipline §4.5. AWS runs v61; v62/v63 are behaviour-neutral so the straddle pools, but close it. *(Confirmed relevant: the pooled snapshot in §B.1 straddles exactly this.)*
- **Start the 6-month candle+funding store fetch** (`BacktestRunner fetch`) — trades cap at ~24 h, known.
- **Schedule the daily append-forward `fetch`** on the AWS box (§7.3).
- **UserManual PDF regen** — one revision behind since the fee build; the manual lane's job.

---

## §7 Flagged for the Fable double-check — NOT resolved here

Seven items. The first three are substantive; the rest are disclosure.

1. **Lane A's headline expectation missed.** §7.5 predicted the VWAP family would "recover toward the EMA-class levels". It reached 53–56 %, not 99 %. The window is provably exact (`VWAPSessionCandles` 100.00 %, max \|Δ\| = 0), so the residual is inside a correct window. **My read — offered as a hypothesis, not a conclusion — is that the NumTight tolerance (≈ $6.4 at BTC 64 k) may be the wrong instrument for a 240-bar volume-weighted accumulator carrying a forming stub, rather than the code being wrong.** That needs a ruling, and it is not mine to make. It also means **the VWAP-sensitive study clearance is still open**, which §7.5 implied would close on re-validation.
2. **Lane C's numbers are bigger than "an investigation item" implies.** The Volume signal can vote on ≤ 2.01 % of the book against ~20 % on closed bars, and the trader's own 3× breakout rule fires ~10–40× less often than the same instrument's closed bars would produce. The behaviour is *specified* (v44 §3 is explicit), so this is not a bug report — but a specified behaviour suppressing a PREFERRED indicator by an order of magnitude is a judgment call, and the volume vote is also a Pass-2 cross-confirmation input, so its silence suppresses upgrades elsewhere. **Enumerated, not recommended, per the item.**
3. **Lane A got no §15 entry in `DeribitIndicatorProject.md`.** CLAUDE.md requires a §15 entry for commits that change engine behaviour; lane A changed a `Core/` file with **zero** behaviour change and no settings bump, so by that rule it doesn't qualify — but there is precedent for settings-untouched §15 rows (the W6-4 ceiling-audit tool has one). **I left it out to respect the batch's tight scope.** If the Fable seat wants a row, it's a one-line addition.
4. **Committing the lane-E overlays was a judgment call.** Item E didn't ask for it; the overlays README says recipes are committed precisely so a study re-read reproduces without scratchpad dependencies, and the Friday session will want to re-run these. Two 1-line JSONs + a README entry, no code.
5. **Out-of-scope finding, recorded not fixed** (per the standing constraint): `tools/BacktestRunner/BacktestProgram.vb` prints an unformatted `String.Format` placeholder on the validate verb's banner line — `[BacktestRunner] Replay {0:yyyy-MM-dd HH:mm} → {1:…} UTC into <file>`. Cosmetic; the correctly-formatted line prints immediately after. Not in any lane's scope.
6. **Lane B's fixture does not cover the network hop.** A46a supplies a synthetic OHLC map so it stays deterministic. The real hop is the pre-existing shared `DeribitOhlcFetcher` path and was exercised by the RUN, but the fixture alone would not catch a regression there.
7. **Lane C's ATR/ROC measurements are inconclusive by construction.** The CSV logs no `TriggerMode`, so roll and backstop fires are separated only by bar phase, and the late-phase band is enriched with backstop fires from quiet periods. The document says so and declines to draw from those columns. **Option D in that report (add `TriggerMode` to the CSV) exists specifically to close this**, and is listed there without recommendation.

---

## Gate tail (identical shape on all five lane gates)

```
=== harness ===
…
PASS  A45a VWAP session anchor (§7.5) — default ≡ UtcNow (non-trivial) · post/pre-cutoff historical anchor · §8.6 wall-clock fallback
PASS  A46a pooled CSV → report carries §2 matrix + §9 band ladder (real Load/matrix/ladder/writer chain)

ALL PASS
OK    harness ALL PASS

=== display-parity ===
OK    no snapshot/card drift detected

=== version-bump ===
OK    engine path changed but [no-engine-change] token present

=== result ===
GATE PASSED
```

All 6 Release builds 0/0 every run (main sln · AutoTweaker · WhatIfRunner · CeilingAudit · BacktestRunner · OrderCheck). The `[no-engine-change]` token is lane A's, justified in `ff1d34c` — engine path touched, zero config keys, settings stays v63 (the `5dc9646` precedent).
