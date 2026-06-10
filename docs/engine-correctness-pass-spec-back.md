# Engine Correctness Pass — Implementation Spec-Back

**Date:** 2026-06-11
**Implementer:** Fable 5 (fresh conversation, per proposal §12 routing — the warm audit conversation was closed)
**Spec:** `docs/engine-correctness-pass-proposal.md` (approved 2026-06-10, commit `cf415cd`)
**Status:** All of F1–F9 implemented. Six local commits, **not pushed** — awaiting the user's live smoke test (§11 manual smoke is the open gate).

---

## 0. Result summary

| Commit | Scope | Build | Harness |
|---|---|---|---|
| `704b4aa` | F1 trade normalisation + harness recreation + A1–A4 | clean | 4/4 pass |
| `22505d0` | F2 DynamicNorms recent windows + A5 | clean | 5/5 pass |
| `ccdd652` | F3+F4 MTF per-side gate + dominant-side cascade + A8/A9 | clean | 8/8 pass |
| `e69db69` | F5+F6 OBV normalisation + Donchian prior window + A6/A7 | clean | 10/10 pass |
| `73d8088` | F7+F8+F9 + settings v31 + CSV v0.5 + data reset + docs | clean | 10/10 pass |
| `28757a0` | Proposal status header → IMPLEMENTED | — | — |

Anchor re-verification (§1 kickoff-staleness rule) was done in full before commit 1: every file:line quoted in the proposal was checked against the tree. **Zero drift** — all anchors held exactly.

The harness runs 10 checks (A8 has two sub-cases). Final state: ALL PASS.

---

## 1. Per-fix notes — what matched spec exactly

- **F1** — implemented precisely per §2 steps 1–9. `list.Reverse()` after the parse loop (request stays `sorting=desc`); XML doc states the ascending contract and the Reverse-not-sort rationale; `LastN` helper added; TFI + MicroCVD windows converted; CalcCVD and CalcLiquidations untouched; `recentTrades(0).Price` → last element; v0.47 CVD comment + MicroCVD header rewritten to state the contract. **Closed-set check (§2.8): pass.** Grep of `recentTrades` / `trades.Take(` found exactly the spec's consumer set — no additional positional consumer existed.
- **F2** — both windows (100-bar volume, 50-bar VWAP-dev) converted to the spec's exact `volLen/volStart` shape, in-progress bar excluded, sizes kept hardcoded.
- **F4** — dominance computed once after Step 4; tie → NONE → NO TRADE; Step 5 is a `Select Case dominant` walking only that side's tiers; below-weak dominant and NONE both route through `AppendLean("NO TRADE", ls, ss, tWeak)` with **raw** scores, as the old Else arm did. `AppendLean` first condition is now `ls > ss` with an explicit `[TIE]` arm.
- **F6** — guard `Count < period + 1`; window `Skip(Count − period − 1).Take(period)`; call-site logic untouched.
- **F7** — both boost sites (`:581`/`:587` pre-edit numbering) wrapped `Math.Min(side + boost, regimeMax)`; `regimeMax` was already in scope.
- **F8** — first arm is now `If r.ADX < penMid Then adxPenalty = TransitionalPenaltyLow`, covering [0, 22.5) at the full penalty; second arm unchanged. The now-unused `penLow` local was removed.
- **F9** — `ParseAnalysisLog` converted to header-name lookup mirroring `ForwardWindowJoiner`'s `colIdx` dictionary. Sweep of all `Split(","c)` consumers confirmed every other `analysis_log.csv` reader (ForwardWindowJoiner, MainForm_Calibration, TweakSettingsForm) was already header-based; the eval-cache and snapshot-manifest parsers read their own files. F9 closed the only fixed-index reader.

## 2. Implementer judgement calls (within spec latitude, flagged for review)

1. **`VerdictResult.MTFGateBlocked As Boolean` added alongside `MTFGateReason`.** The spec names only the reason field, but the MTF card / legacy RTF section / plaintext snapshot all render a PASS/BLOCK header. The alternatives were string-parsing the reason or re-deriving from per-side flags at every render site. A display-only boolean set at Step 4b is cleaner and keeps all consumers reading one source. Zero scoring impact.
2. **Breakdown-row hit semantics at Step 4b:** `LongHit = directional ∧ dominant=LONG ∧ gatePassLong` (mirror short). The *computed* gate state drives both the hits and the PASS/BLOCK wording, while the *enforced* veto (`MTFGateBlocked`) additionally requires `cfg.MTFGate.Enabled` — matching the old behaviour where the reason could read BLOCK even with the gate disabled.
3. **Interim CSV mapping in commit 3.** F3 lands two commits before the schema bump, so to keep every commit building and running, commit 3 mapped the old v0.4 gate columns from the new fields (`MTFGatePass` ← `Not v.MTFGateBlocked`, reason ← `v.MTFGateReason`). Commit 5 then did the real v0.5 column swap. Rows logged between commits 3 and 5 were contaminated-era rows headed for the archive anyway.
4. **`ApplyMtfRow` (card renderer) dropped its unused `gatePass` parameter** — the row kind was already derived entirely from the reason string; the comment now documents the Step 4b composition.
5. **`MainForm_TestHarness` (P5-test, slated for deletion in the reskin cleanup) updated minimally** to compile: `WithMtfPass`/`WithMtfBlock` set the new indicator flags *and* the VerdictResult fields. Synthesised cases that never call either builder now render "—" for the MTF reason (the field defaults empty on VerdictResult). Harness display parity was not chased — it is throwaway code.
6. **F5 signature default**: `CalcOBV`'s `Optional trendGate` default changed 0.01 → 10.0. The spec names only settings.json + the POCO; the function default is a third site that would otherwise carry dead units. Call site always passes cfg, so this is belt-and-braces.
7. **F9 hardening**: a header missing any required column logs one line and returns zero rows (fail-loud-and-empty) rather than throwing per-row. Slightly more defensive than specced.
8. **CSV "schema version string" (§8)**: the v0.4 header never contained a literal version string — the **gate-column rename itself** is the v0.4/v0.5 discriminator, which satisfies the mechanical-distinguishability requirement. The file-header comment block documents v0.5 semantics. Also fixed a pre-existing inconsistency: the rotation fallback name said `v0.3` where it meant `v0.4`.

## 3. Acceptance harness (§11)

Recreated at `verify/ordercheck/` (gitignored via the existing `verify/` rule): own `OrderCheck.vbproj` (net8.0, console, no WinForms) linking the **real shipped sources** via `Compile Include` — DeribitClient, DynamicNorms, all four Indicators files, the four scoring files, IndicatorResults, EngineSettings, SettingsLoader. **Glob-safety done as mandated:** root `DeribitVerdictEngine.vbproj` now carries `Compile Remove` + `None Remove` for `verify\**\*.vb`; the solution builds clean with the harness in-tree.

| Test | Result | Notes |
|---|---|---|
| A1 CVD slope | PASS | asc list, old sells → recent buys → RISING |
| A2 MicroCVD polarity | PASS | accelerating bull burst in tail → BULL_ACCEL |
| A3 MicroCVD window | PASS | asserts `microEarly = 16000` exactly and net = window sum — old `Take(50)` would include the 10 huge sells |
| A4 TFI window | PASS | BUY PRESSURE, tfiValue = +1.0 exactly |
| A5 Norms | PASS | VolMean = 1000 (asserted before session multipliers, so the Tier C default session buckets can't perturb it) |
| A6 OBV | PASS | equal-first-pair and distinct-first-pair produce identical RISING |
| A7 Donchian | PASS | upper = prior-20 max (105), close 110 breaks out |
| A8 cascade | PASS ×2 | see fixture note below |
| A9 MTF flags | PASS | 70 falling 15m candles → BEAR, `gatePassLong=False`, `gatePassShort=True` |

**A8 fixture shape** (the judgement the spec delegated): real `Calculate()` end-to-end with a POCO-default cfg, RANGE_BOUND regimeMax 18 → tiers 13/10/7. Eleven deterministic short votes (ROC, RSI, DMI, Volume, VWAP, EMA ribbon, OI, OFI, CVD, TFI, MicroCVD) + four long votes (BBW/TTM building, Donchian LONG, OBV RISING, EMA200 anchor); Pass 2b/2c, Step 3b, and OFI-momentum suppressed via cfg switches; Step 3's negative-funding arm with `FundingHighPenalty=0` lifts the long side by exactly the boost. Boost 3 → eff 7/11 → asserts **SHORT** with exact effective scores (the proposal's worked example, which pre-fix code answers WEAK LONG); boost 7 → eff 11/11 → asserts **NO TRADE [TIE]**. Exact-score assertions mean any future Step 2 vote drift breaks the fixture loudly rather than silently weakening it.

## 4. Settings v31 / CSV v0.5

- `settings.json`: version 31, `last_modified`/`modified_by` updated, change_log entry (newest-first) covering the pass and marking trend_gate as a **seeded** value; `indicators.OBV.trend_gate` 0.001 → 10.0. No keys added/removed.
- `EngineSettings.vb`: `ObvSettings.TrendGate` default 10.0 + XML doc stating the new units.
- CSV header: `MTFGatePass` → `MTFGatePassLong,MTFGatePassShort` (both sides logged per §4.6 for gate-disagreement analytics); `MTFGateReason` carries the final composed string with commas → semicolons as before. Logger file comment documents v0.5 as the semantics marker for all same-name column shifts.

## 5. Data reset (§9) — executed at commit 5

Verified no `DeribitVerdictEngine`/`AutoTweaker` process running first. Moved to `data-archive/pre-orderfix-20260611/` (now gitignored; provenance README included):

| Item | Outcome |
|---|---|
| `analysis_log.csv` (exe dir, 3.2 MB) | archived |
| `analysis_eval_cache.csv` (707 KB) | archived |
| `tools/AutoTweaker/state.json` | archived (deletion = reset; re-seeds via Tier C M5 guard) |
| `analysis_output_dump.md` (optional item) | archived |
| stray `bin/Debug/analysis_log.csv` (1.4 MB, old layout — **not in the spec's list**) | archived under `bin-debug-root/` |
| picked-cell history CSV | **did not exist** — tweaker never fired; `AppendPickedCell` writes next to the configured CSV path and was never invoked |
| `settings_snapshots/` | **empty** — nothing to move |
| `ohlc_1m_cache.csv` | kept in place per spec |

First-run sanity items that need the live app (v0.5 header written, perf strip `--%`, tweaker dialog window-not-full) are folded into the user's smoke test.

**Not archived, flagged for the spec author:** ~29 small `analysis_summary_*.csv` one-shot report outputs remain in the exe dir. They are derived from contaminated data but nothing reads them back; the spec's archive list was explicit, so they were left. Sweep or ignore at your discretion.

## 6. Docs

- `docs/DeribitIndicatorProject.md`: §15 v31 entry (one entry covering the pass), §8 last-trade correction, §6 + header version refs, §12 re-baseline WATCHING item marked **ACTIVE** with the first-100-rows sanity checks inlined.
- `docs/architecture.md`: trade-stream ascending contract callout under the header, `GetRecentTradesAsync(500)` (doc said 100), MTF per-side flags + Step 4b/Step 5 pipeline notes, three-formats display-clarification row updated to name `VerdictResult.MTFGateReason` as the single source.
- `CLAUDE.md`: MTF invariant extended (direction-aware), new ascending-trade-list invariant, settings pointer v31.

## 7. Open items / handoff state

1. **User smoke test then push** — local commits only, per the compile/test gates. Nothing pushed.
2. **Auto-tweaker remains held** — manual-fire only; first fire wants ≥1 full clean 50-row v0.5 window + user supervision. Its state file is reset.
3. **Re-baseline review** (proposal §8 WATCHING table) triggers at ≥300 clean rows spanning ≥2 sessions; OBV trend_gate 10.0 is seeded, not calibrated.
4. **Untouched, still yours:** Tier D proposal (H4 Kelly, S-1, S-2), S5 ledger-guard merge into Spec C, S-4/S-6/S-7 future passes.
5. The P5-test harness deletion (reskin cleanup) will sweep `MainForm_TestHarness.vb` including its updated MTF builders — no engine coupling was added there.
