# Pre-Aug-1 Opus Batch — Execution Set for the Secondary Orchestrator

**Date:** 2026-07-31 (Fable coordinator). **For:** an Opus orchestrator conversation the trader opens with: *"Execute docs/pre-aug1-opus-batch-2026-07-31.md top-down."*
**Review flow:** the orchestrator runs each item as its own lane, reviews each lane's spec-back + re-runs the gate itself, and writes ONE summary doc (`pre-aug1-batch-summary.md`: per-item outcome, deviations, gate tails, commit hashes). The trader relays that summary to the Fable seat for the final double-check. **The orchestrator's review does not replace the Fable double-check — flag anything uncertain rather than resolving it silently.**

## Standing constraints (bind every item)

Read CLAUDE.md first; `git status -sb` before EVERY lane (two-lane rule — one lane at a time, serialize). Release-only builds (`dotnet build -c Release` — NEVER Debug; the collector stomp rule). Local commits with EXPLICIT paths; NEVER push. Gate per lane: `tools/checks/verify-gate.ps1 -Mode prepush` GATE PASSED. Fixture families: confirm next-free against `verify/ordercheck/Program.vb` at each build. Spec-back per item (own file or a §-addendum where the item says so). **Engine .vb edits: item A's one parameter ONLY — nothing else touches Core//analysis//UI beyond what an item names.** No settings.json version bumps unless an item says so. No ⚠ anything.

## FENCED OUT — do NOT attempt (Fable/trader items)

The absorption mechanism-revision spec · the Aug-1 handover doc · any live scoring/geometry change · interpreting any study result (F1 read, grid conclusions, P1 promotion) · absorption anchor changes · anything touching the bridge contract or the order-app repo.

---

## A. VWAP session-anchor parameterization (§7.5) — REQUIRES the trader's one-word tick at handoff

**Spec:** `backtest-synthesizer-proposal.md` §7.5 + the validation report §8.6 row-proof. **Scope:** `Core/Indicators_Volatility.vb::GetSessionCandles` (and its two callers `CalcVWAP`/`CalcVWAPBands` if the signature threads through) gains `Optional nowUtc As DateTime? = Nothing` — `Nothing` ⇒ `DateTime.UtcNow` (live byte-identical); `tools/BacktestRunner/ReplayLoop.vb` passes the bar close. **The ONE authorized engine edit.** Fixture: default-path identity (explicit `nowUtc` = UtcNow ⇒ identical output to the default call, plus a historical-anchor case). Then **re-run the §7.1 validation window** and append the VWAP-family before→after to the validation report (expect the ~44% family to recover toward the EMA-class levels; OBVTrend may partially recover too — report, don't chase). Settings untouched.

## B. Pooled-file report runner (unblocks the F1 §9 read — the READ itself is fenced)

**Mini-spec (this section is the spec).** New console verb or tiny project (recommend: a `report` verb on an existing host-agnostic console — BacktestRunner or a 40-line `tools/ReportRunner/` — orchestrator's call, state it): takes `--csv <path>` (+ optional `--settings`), runs the SHIPPED `analysis/AnalysisRunner` pipeline over that file (the analysis layer is host-agnostic by design; forward-bar OHLC comes from the existing fetch path), writes the standard markdown report beside the input. Zero changes to the in-app report path. Fixture: report generated from a small fixture CSV contains the §2 matrix + §9 band-ladder sections. **Deliverable includes one RUN:** build the dedup-pooled snapshot per `aws-collector-deploy-checklist.md` §4.3b (local-preferred per UTC session-hour, AWS fills; the frozen 20260730 pair in the session scratchpad is acceptable input) and generate the report — attach the §9 ladder table RAW in the summary, no interpretation.

## C. Forming-bar live investigation (report-only — NO code changes anywhere)

**Question (from `backtest-synthesizer-proposal.md` §7.2):** live on-close runs carry the seconds-old forming bar as the last candle in every series (`MarketState.ApplyChartTick` forming-bar update — verified). Investigate and WRITE `docs/forming-bar-live-investigation-2026-07.md`: (1) what the v44 on-close spec (`docs/` — locate it) intended for the last-bar state; (2) which indicators consume the last bar and how (volume ratio, ROC, RSI, ATR, BBW/TTM, EMA — table: indicator → last-bar sensitivity); (3) quantify from the live CSV: VolumeRatio distribution on on-close rows vs backstop/interval rows (the 0.0002-class fingerprint), any other measurable stub artifacts; (4) enumerate the options WITHOUT recommending (close-bar-only slice at fire time · stub-aware indicator variants · status quo) and state what each would do to dataset continuity. **Any change here is a maximal ⚠ — the doc informs a later Fable/trader ruling, nothing more.**

## D. TweakSettingsForm tooltip lifetime fix

`UI/TweakSettingsForm.vb:~724` local `minTierTip` → form-level field (`_minTierTip`), per the `c508d93` pattern (see `fee-aware-min-move-spec-back.md` §2.9); sweep the dialog for any other method-local ToolTips. UI-only; parity-exempt (live status class); no fixture needed; note in commit. (A pending background-task chip exists for this — if the trader already ran it, skip.)

## E. Geometry-session grid runs (mechanical execution ONLY — attach raw reports, zero conclusions)

Run via the What-If launcher's CLI equivalent (`tools/WhatIfRunner`, overlay JSON per `docs/offline-whatif-replay-proposal.md` conventions; the v56 overlays in `tools/WhatIfRunner/overlays/` are format examples). Use the CURRENT live CSV (freeze a copy first — the freeze rule). For EACH grid, two runs: full book AND `--from 2026-07-08`:
1. **W6-1 LONDON stop grid:** sweep `scoring.structural_levels.stop_max_atr_mult` = `1.6:2.2:0.2` (4 cells), everything else live.
2. **Geometry 4-cell:** `scoring.structural_levels.target_arbitration_mode` = `0:1:1` × `scoring.structural_levels.use_best_pivot_candidate` = `0:1:1`.
Attach all four report paths + the ranking tables raw in the summary. The Friday session interprets; the DIVERGENT/overfit guard-rails speak for themselves.

## F. Ops list (trader-executed; the orchestrator only reminds in its summary)

Push cadence (the stack is large — push before opening lanes) · **AWS redeploy with v63** at next RDP (same-settings discipline §4.5 — AWS runs v61; v62/v63 are behavior-neutral so the straddle pools, but close it) · start the 6-month **candle+funding** store fetch (`BacktestRunner fetch` — trades cap at ~24h, known) · schedule the daily append-forward `fetch` on the AWS box (§7.3) · UserManual PDF regen (one revision behind since the fee build — the manual lane's job).

---

**Order:** A (after tick) → B → C and D in either order → E last (needs the freshest book). If any lane hits something outside its written scope: STOP that lane, record the finding in the summary, move on — do not improvise scope.
