# Batch summary — Kelly as one class, p/b from the book's placed levels net of fees

**Written:** 2026-09-25 (UTC) by the implementer seat (Sonnet 5, high), per `docs/kelly-one-class-placed-payoff-spec.md` §4 "RULINGS — 2026-09-25". **Status:** ✅ BUILT, both sessions, local commits only — the trader pushes and schedules the deploy.

**Commits (local, unpushed):**

| Commit | Session | Scope |
|---|---|---|
| `06e34c1` | 1 | `LivePerformanceTracker.vb`, `Core/ScoringEngine_Kelly.vb`, `Core/ScoringEngine_Types.vb`, `Core/Settings/EngineSettings.vb`, `settings.json` (v68→v69), `tools/ops/q1d_tier_geometry.py`, `verify/ordercheck/Program.vb` (A10 rewritten + A90a-k) |
| (unplanned, folded into Session 2's diff) | 2 | `KellyBook`/`KellyBucket`/`SelectKellyBucket` relocated from `LivePerformanceTracker.vb` to `Core/ScoringEngine_Types.vb` / `Core/ScoringEngine_Kelly.vb` — see §2 below |
| `31f57d0` | 2 | `UI/MainForm_PlaintextSnapshot.vb`, `UI/MainForm_Render_Cards.vb`, `Core/SignalEmitter.vb` (comment), `UI/MainForm_Layout.vb` (`KELLY_CARD_H`), docs (`DeribitIndicatorProject.md`, `architecture.md`, `UserManual.md`, `trader-tick-queue.md`) |

---

## 1. What was built

Kelly's p and b no longer come from a confidence-tier map (`kelly.est_prob_floor`/`est_prob_scale`, retired). They come from the live eval cache, pooled per the current run's session (`LivePerformanceTracker.ComputeKellyBook`), split into 3 terciles of the book's own rows' placed net payoff (K-1 = (g)). A row's p and b are its bucket's measured values, falling back to the session-pooled pair when its own bucket holds fewer than `kelly.min_book_rows` (400) rows; the session pool itself being under 400 renders a distinct "book below the floor" state.

The render gate moved from `v.KellyPWin > 0` to `v.KellyHasSide` (finding F-1: the old gate rendered the block on every non-empty verdict, including a negative f*). Three states now exist, mirrored 1:1 on both surfaces:

1. **Book below the floor** — basis + a placeholder `p(win)` row naming the shortfall. No sizing rows.
2. **`[NO EDGE]`** (f* ≤ 0) — basis + measured p/b/breakeven/f*. No sizing rows.
3. **Full sizing** (f* > 0) — as (2) plus Applied fraction / Risk $ / Contracts|Lean / Notional.

Hidden entirely on plain `NO TRADE` and `NO TRADE [TIE]` (no Kelly side).

## 2. Unplanned build-driven fix: KellyBook/KellyBucket relocated

`CalcKellySizing`'s new `book As KellyBook` parameter made `Core/ScoringEngine_Kelly.vb` depend on `LivePerformanceTracker.vb`. `tools/BacktestRunner/BacktestRunner.vbproj` links `ScoringEngine_Kelly.vb` (the whole `ScoringEngine` partial class must compile as a unit) but not `LivePerformanceTracker.vb`, and adding it pulled in `OhlcCache` and that dependency chain for a feature BacktestRunner never calls.

Fix: `KellyBook`/`KellyBucket` (plain data types) moved to `Core/ScoringEngine_Types.vb`; `SelectKellyBucket` (depends only on those types) moved to `Core/ScoringEngine_Kelly.vb`. `LivePerformanceTracker.vb` keeps `ComputeKellyBook` (the fold over its own `EvalCacheEntry` list) and gained a 2-arg production wrapper (`ComputeKellyBook(sessionName, cfg)`, reads `_evalCache`) beside the pure 3-arg fixture-testable one. Zero behaviour change — confirmed: the harness stayed ALL PASS across the move with no fixture touched, and this is a pure type-location refactor with no logic change.

## 3. Build verification

| Check | Result |
|---|---|
| `dotnet build DeribitVerdictEngine.sln -c Release` | 0 errors, 0 warnings |
| `tools/AutoTweaker/AutoTweaker.vbproj` (via verify-gate) | OK |
| `tools/BacktestRunner/BacktestRunner.vbproj` (via verify-gate) | OK |
| `tools/CeilingAudit/CeilingAudit.vbproj` | OK |
| `tools/WhatIfRunner/WhatIfRunner.vbproj` | OK |
| `verify/ordercheck/OrderCheck.vbproj -c Release` | 0 errors, 0 warnings |
| Harness | 468 → 483 PASS, 0 FAIL (15 new `A90a`-`A90k` checks + `A10` rewritten, same count) |
| Every §6 mutation (A10 + A90a-k, 12 total) | run, confirmed red, reverted — see the spec-back §1 for each result |
| `verify-gate.ps1 -Mode local-fast` (post-commit) | **GATE PASSED**, 0 warnings |
| AC-4: `ComputeKellyBook` on the real 2026-09-25 08:53 UTC cache (101,732 rows) | f* < 0 in **every** session and **every** bucket (9 of 9) — no escalation |
| AC-5/AC-6: live app, real Deribit data, 2 analysis runs | Both WEAK LONG / LONDON / bucket 1 fallback / `[NO EDGE]`. Card did not clip at 280px. Snapshot and card text compared side by side: **byte-identical**. Screenshots deleted after the check |

## 4. AC-4 measurement (paste)

Session, N, p, b, f* (pooled) and per-bucket, on `aws_fetch/20260925-085341/analysis_eval_cache.csv` (101,732 rows, span 2026-07-22 → present):

```
NY     N=  9043 Successes=  3252 p=0.3596 b=0.7702 f*(pooled)=-0.4718 sufficient=True span=2026-07-22
  bucket1 N=  3014 b∈[0.112,0.617] p=0.3988 b=0.4817 f*=-0.8492 sufficient=True
  bucket2 N=  3014 b∈[0.618,0.801] p=0.3839 b=0.7130 f*=-0.4803 sufficient=True
  bucket3 N=  3015 b∈[0.801,2.377] p=0.2962 b=1.0315 f*=-0.3861 sufficient=True
LONDON N=  2620 Successes=  1138 p=0.4344 b=0.8944 f*(pooled)=-0.1981 sufficient=True span=2026-07-23
  bucket1 N=   873 b∈[0.107,0.715] p=0.4868 b=0.5243 f*=-0.4919 sufficient=True
  bucket2 N=   873 b∈[0.715,0.912] p=0.4651 b=0.8190 f*=-0.1881 sufficient=True
  bucket3 N=   874 b∈[0.912,6.465] p=0.3513 b=1.1660 f*=-0.2051 sufficient=True
ASIA   N=  3315 Successes=  1507 p=0.4546 b=0.7093 f*(pooled)=-0.3144 sufficient=True span=2026-07-23
  bucket1 N=  1105 b∈[0.091,0.486] p=0.5357 b=0.4064 f*=-0.6066 sufficient=True
  bucket2 N=  1105 b∈[0.486,0.733] p=0.4914 b=0.5715 f*=-0.3985 sufficient=True
  bucket3 N=  1105 b∈[0.733,6.623] p=0.3367 b=1.1604 f*=-0.2350 sufficient=True
```

`E-4` (build-time evidence, not re-runnable as committed — the diagnostic Sub was written into `verify/ordercheck/Program.vb`, run, and deleted before the commit; a reader would need to re-add it against a current cache export to reproduce). All 9 session×bucket cells and all 3 pooled values are negative, matching the spec's own prediction (§4 "Not measured before this ruling" note).

## 5. Session's own effort, model and escalation record

- **Model/effort used:** Sonnet 5, high — as the spec's §0 recommended.
- **Escalation triggers checked, none fired:** (a) f* > 0 anywhere on the real cache — did not fire (§4 above). (b) an edit needed in `Core/ScoringEngine_Calculate_*.vb` or `AnalysisLogger.vb` — did not fire (`git diff --stat` against both commits lists neither file). (c) the snapshot→card→payload call order needing to move across `MainForm_Analysis.vb` — did not fire; only a local two-statement reorder inside `AppendHeaderBlock` was needed (lvLong/lvShort computed before `CalcKellySizing` instead of after).
- **Auto-proceed decisions taken and recorded** (CLAUDE.md "log every auto-proceeded decision in ONE line"): see the spec-back §2.

## 6. Docs updated

`docs/DeribitIndicatorProject.md` §7 (CalcKellySizing bullet), §8 (Kelly Sizing block bullet), new §15 row; `docs/architecture.md:446-449`; `docs/UserManual.md` §3 (rewritten: example, rendering gate, three states, header format, historic note); `docs/trader-tick-queue.md` (`Q-1` row marked BUILT); `Core/SignalEmitter.vb:172-176` (payload `kelly` block comment); `tools/ops/q1d_tier_geometry.py` §11 (F-6: carries the retired `est_prob_floor`/`est_prob_scale` v68 values as a documented historical literal, since the keys no longer exist in `settings.json`).

**Housekeeping owed, not done in this build** (out of this session's briefed scope — see the spec-back §2 for why): the new §15 row pushes v64 (2026-07-31) out of the file's kept-5-settings-versions window; per `DeribitIndicatorProject.md`'s own retention rule that row should move to `docs/history-archive.md` §E.

## 7. What is NOT done

- **Not pushed.** Both commits are local; the trader pushes per the brief.
- **Not deployed.** The spec's own deploy note (§8) reserves this to the trader — executable + `settings.json` together at one restart, bundled with the next scheduled deploy.
- **`CLAUDE.md`'s "suppressed when KellyF ≤ 0" invariant line** — the spec named this as the orchestrator's or trader's edit, not the implementer's; not touched.
