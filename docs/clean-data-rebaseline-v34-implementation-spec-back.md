# Clean-Data Re-Baseline v34 — Implementation Spec-Back (session execution record)

**Date:** 2026-06-13
**Implementer:** Opus 4.8 @ High
**Scope:** Executed the v34 brief end-to-end in one session — analysis → proposal → apply → review cycle → sequencing revision.
**Companion docs:** `clean-data-rebaseline-v34-brief.md` (input) · `clean-data-rebaseline-v34-proposal.md` (plan + rationale) · `clean-data-rebaseline-v34-spec-back.md` (decision sanity-check; **§7 = review outcome**). *This file is the execution/commit record only — decisions are not re-argued here.*
**Status:** v34 **APPLIED + PUSHED**; decision spec-back **reviewed (PASSED) + revised**; the revision commit is **held local pending v35** (trader's call).

---

## 0. Commit ledger

| Commit | Remote? | Scope |
|---|---|---|
| `61b4532` | ✅ pushed | settings.json v33→v34 (3 knobs + version/change_log) + `clean-data-rebaseline-v34-proposal.md` (new) + handover §15 row / §6 pointer |
| `6d8441f` | ✅ pushed | handover §12 WATCHING — weekday-ASIA re-verify added, first-fire row enriched, post-correctness re-baseline row marked RESOLVED |
| `3c0be4c` | ⏸ **HELD local** | reviewed decision spec-back (new) + §7 outcome; two-sided CVD guard; re-fetch bundled into weekday-ASIA re-verify; first-fire re-gated on v35 (proposal §5 + handover §12) |

All on `master`, local-first. Remote was at `1e9df84` (v33) before this session; the push moved it `1e9df84..61b4532..6d8441f` (the range also carried four pre-existing unpushed commits — the v34 brief + three P5b/P5-test cleanups). `3c0be4c` is the only commit not on remote.

## 1. What was executed (chronological)

1. **Analysis** — 975-row `analysis_log.csv` (v0.6) + 1532-entry eval cache. All distributions computed **directly from the logged columns**; indicator mechanisms read from source (`CalcFundingMomentum:362`, `CalcSpread:411`, `DynamicNorms.Compute`/`ApplySessionVolume`). Verified `Timestamp` is UTC (`AnalysisLogger.vb:128`) and the engine's ASIA bucket = utcHour **0–7** (`DynamicNorms.vb:120-122`) before any session attribution. Throwaway PowerShell, not committed.
2. **Proposal authored** — `clean-data-rebaseline-v34-proposal.md`: Step 0 gate (READY confirmed), per-item rulings, diff, first-N-row checks.
3. **Applied to `settings.json`** — three values: `indicators.CVD.slope_pct_of_value` 0.05→0.10 (floor 12k kept); `indicators.funding.momentum_threshold` 1e-5→5e-8; `session_volume` ASIA `0.80/0.85→1.10/1.05` (the trader's pre-emptive value over my neutral-1.00 floor). `version`→34, `last_modified`/`modified_by`, change_log prepended. No keys added/removed; CSV stays v0.6; POCO defaults untouched.
4. **Docs** — proposal status→APPLIED with the 1.10/1.05 + weekend-confound woven in; handover §15 v34 row + §6 pointer→v34; §12 WATCHING updated.
5. **Validation** — `ConvertFrom-Json` OK; `dotnet build` 0/0; build propagated v34 into `bin\Debug\...\settings.json` (verified ASIA 1.10/1.05, CVD 0.10, funding 5e-8).
6. **Pushed** `61b4532` + `6d8441f` after the trader's first-glance smoke test.
7. **Decision spec-back** written and sent for sanity-check → spec writer **PASSED all five flagged calls, no reverts** → folded the two requested revisions (two-sided CVD guard; offline re-fetch bundled into the weekday-ASIA re-verify) and propagated the review's sequencing change (first fire now gated on v35) across proposal §5, handover §12, and spec-back §6/§7 → committed `3c0be4c`.
8. **Memory** kept current throughout (`project_engine_audit_calibration_trap`): applied→pushed→reviewed→gated-on-v35.

## 2. Settings v34 (as applied)

| Key | v33 | v34 | Reverts to |
|---|---|---|---|
| `indicators.CVD.slope_pct_of_value` | 0.05 | **0.10** | 0.05 |
| `indicators.funding.momentum_threshold` | 0.00001 | **0.00000005** | 0.00001 |
| `session_volume.sessions[ASIA].high_multiplier` | 0.80 | **1.10** | 1.00 or 0.80 |
| `session_volume.sessions[ASIA].mid_multiplier` | 0.85 | **1.05** | 1.00 or 0.85 |

`indicators.CVD.slope_min_usd` (12000), `indicators.OFI.momentum_threshold` (0.15, a *different* classifier), LONDON (1.00/1.00) and NY (1.15/1.10) — all confirmed **untouched**. Every change is a one-line settings revert, no code or data dependency.

## 3. Validation done

- `settings.json` parses; all four changed values confirmed in place, all KEEP values confirmed unchanged.
- `dotnet build` — 0 warnings / 0 errors; runtime copy in `bin` carries v34.
- No acceptance harness — settings-only, zero code, nothing to fixture. "Validation" here is the distribution analysis behind each ruling (proposal §3) + parse + build.
- First-fire safety pre-checked: `tweaker_config.json` `dry_run_enabled: true` **and** `auto_commit_enabled: false` — the eventual first fire cannot apply or commit without manual action.

## 4. Files touched this session

- `settings.json` — v34.
- `docs/clean-data-rebaseline-v34-proposal.md` — new, then revised (two-sided guard, weekend confound).
- `docs/clean-data-rebaseline-v34-spec-back.md` — new, then revised (§7 review outcome).
- `docs/DeribitIndicatorProject.md` — §15 v34 row, §6 pointer, §12 WATCHING (3 rows).
- `docs/clean-data-rebaseline-v34-implementation-spec-back.md` — this file.
- `Core/Settings/EngineSettings.vb` — **deliberately not touched** (POCO defaults ride the next code commit, per v33/Tier-C precedent).

## 5. Open / handoff state

- **`3c0be4c` held local** until v35 lands (trader's instruction); it goes up with the v35 push. This file, if committed, joins the same held batch.
- **First fire is gated on v35**, not just weekday data — the auto-tweaker optimises the failure-rate matrix, which is **ATR-confounded** until the v35 de-confound re-bases history (excludes sub-tradeable low-ATR trades as `EXCLUDED_BELOW_MIN_MOVE`). Rationale in `clean-data-rebaseline-v34-spec-back.md` §7.
- **Live sequence:** collect Monday (weekday Asia/London + NY — feeds both the weekday-ASIA re-verify and the v34 first-N checks) → v35 pair lands (separate conversation: `eval-metric-deconfound-proposal.md` + `min-tradeable-move-gate-proposal.md`) → weekday-ASIA re-verify (with the bundled volume re-fetch) → supervised dry-run first fire on a weekday window → diff reviewed by the sanity-check seat before any apply.
- **First-N-row checks** (proposal §5) ride incoming v34 rows: funding RISING/FALLING should appear (~2–4%, was 0%); CVDSlope FLAT into the **8–18%** window (two-sided guard: <8%→0.12, >20%→0.07); ASIA trade-rate easing — judged against **weekday** rows, not the Saturday baseline.

---

*Execution record; settings-only, zero code. Held-vs-pushed state per §0. Decisions and the sanity-check verdict live in the companion `-spec-back.md` §7.*
