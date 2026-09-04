# A54a JSON↔POCO drift guard — batch summary

**Spec:** [`a54a-json-poco-drift-guard-spec.md`](a54a-json-poco-drift-guard-spec.md).
**Escalation + scope correction:** [`a54a-drift-guard-escalation-2026-09-04.md`](a54a-drift-guard-escalation-2026-09-04.md).
**Status:** ✅ Session 1 **DONE**. Session 2 (§7 scoped-(b) dead-code removal) **NOT started** — separate session per D-5.

---

## 0. What changed since the escalation, up top

The build **stopped once** (2026-09-04, documented in the escalation doc above) on a real
gap: the spec's D-table originally re-synced only 2 of 6 real calibration drifts. The
trader resolved it without a new ruling — the missing four were already ruled at
[`trader-tick-queue.md`](trader-tick-queue.md) §0a (2026-08-11, *"Seeded session
buckets"*), and the spec's own §4.2 (added 2026-09-04) now carries that ruling forward.
**Session 1 resumed and completed the same day with SEVEN re-syncs, not three.**

Two more corrections landed mid-build, both from the reviewing seat's review of the
stopped state:

- `MicroCvdSettings.AccelThresholdDynamicPct`'s doc comment was stacked (two `<summary>`
  blocks, the old one still reading "0.03") rather than edited in place — fixed to one
  corrected block.
- The escalation doc's own `Skipped=19` claim was over-stated as "matching" the spec's
  §3.4, which actually documents 23 exclusions (4 root + 2 `<JsonIgnore>` derived + 2
  `resolution_profiles` dict keys), not 19. Answered in §3 of the spec-back.

---

## 1. Outcome

| Item | Result |
|---|---|
| Reflection walk (`WalkPocoVsJson` + `DriftWalkResult`) | ✅ Built in `verify/ordercheck/Program.vb`, harness-only, zero `Core`/`tools` dependency |
| Fixtures `A62a`–`A62g` | ✅ All 7 written, all PASS at baseline |
| Re-syncs | ✅ SEVEN applied to `Core/Settings/EngineSettings.vb` — D-2's six + D-3's one |
| D-3 comment rewrite | ✅ `NetworkSettings.Transport`'s declaration comment records the P3 cutover history |
| `docs/DeribitIndicatorProject.md` §15 row | ✅ Added |
| Mutation-proofs (§6a) | ✅ All 7 fixtures' mutations RUN for real — see §2 below |
| Harness | ✅ 324/324 PASS, `ALL PASS` |
| Full build matrix | ✅ Solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck, each 0/0 Release, each run separately |
| `verify-gate.ps1 -Mode local-fast` | ✅ `GATE PASSED` |
| Settings version | Unchanged — **v68**, no `change_log` entry (no key added/changed) |
| Commit | Single commit, this batch |

---

## 2. Mutation-proof log — every mutation in the spec's §6 table, run for real

Per §6a: *"reverted, confirmed FAIL, restored, confirmed PASS, with the actual output
recorded."* All seven were applied as temporary edits to the real shipped
`WalkPocoVsJson`/`EngineSettings.vb`, run through the harness, then reverted — never
shipped as a second code path.

| # | Mutation | FAIL output (verbatim, trimmed) | Restored |
|---|---|---|---|
| 1 | Revert `CvdSettings.SlopePctOfValue` 0.10→0.01 | `FAIL A62a … drifts=[indicators.CVD.slope_pct_of_value: poco=0.01 json=0.1]` | ✅ PASS |
| 2 | Revert `MicroCvdSettings.AccelThresholdDynamicPct` 0.30→0.03 | `FAIL A62a … drifts=[indicators.MicroCVD.accel_threshold_dynamic_pct: poco=0.03 json=0.3]` | ✅ PASS |
| 3 | Revert ASIA `HighMultiplier` 1.00→0.8 | `FAIL A62a … drifts=[session_volume.sessions.ASIA.high_multiplier: poco=0.8 json=1]` | ✅ PASS |
| 4 | Revert ASIA `MidMultiplier` 1.00→0.85 | `FAIL A62a … drifts=[session_volume.sessions.ASIA.mid_multiplier: poco=0.85 json=1]` | ✅ PASS |
| 5 | Revert ASIA `ExecutionResolution` 3→1 | `FAIL A62a … drifts=[session_volume.sessions.ASIA.execution_resolution: poco=1 json=3]` | ✅ PASS |
| 6 | Revert LONDON `ExecutionResolution` 3→1 | `FAIL A62a … drifts=[session_volume.sessions.LONDON.execution_resolution: poco=1 json=3]` | ✅ PASS |
| 7 | Revert `NetworkSettings.Transport` "ws"→"rest" | `FAIL A62a … drifts=[network.transport: poco=rest json=ws]` | ✅ PASS |
| 8 | Neuter the `Double` scalar comparison (A62b's own mutation) | `FAIL A62b … newDrifts=[] (want exactly [indicators.ADX.trend_threshold])`. **`A62a` still PASSED** on the same run — exactly the point A62b exists to catch: a clean-tree fixture alone cannot prove the walk has teeth. | ✅ PASS |
| 9 | Swap the nullable-skip/absent-key test order (A62c's own trap) | `FAIL A62a … orphans=[…7 entries…]` (exactly seven — reproduced verbatim, see §3 below) AND `FAIL A62c … absent(orphans=2 …)` | ✅ PASS |
| 10 | Switch `A62TryGetPropertyCI` to case-**sensitive** | `FAIL A62d … orphans=[test.period,test.trend_threshold,test.range_threshold] compared=0` | ✅ PASS |
| 11 | Resolver returns a hardcoded default instead of `Nothing` | `FAIL A62e … root=C:\Dev\DeribitVerdictEngine errMsg=''` | ✅ PASS |
| 12 | Swap the structural (`JsonIgnore`/`CanWrite`) test for a name list scoped to today's two known derived-property names | First attempt (list check ANDed with the pre-existing `CanWrite` check) — **no observable change**, both derived properties are ALSO `ReadOnly` so `CanWrite` alone still excludes them. Re-designed `A62f` to add a scoped local test type (`A62StructuralTestShape`) with an arbitrarily-named derived property a hardcoded list could not anticipate. Re-ran the SAME mutation (name list, no `CanWrite` fallback) against the corrected fixture: **`Unhandled exception. System.NullReferenceException` — the whole harness crashed** on the arbitrary property, a strictly stronger failure than a `Check(False, …)`. | ✅ PASS (rebuilt clean) |
| 13 | Widen the D-1 allow-list to "any Boolean" (mutate the `Boolean` branch to never record drift) | `FAIL A62g … drifts=[]` | ✅ PASS |
| 14 | `WalkPocoVsJson` returns immediately, visiting nothing (§11.3 handle 4 — the `Compared` floor) | `FAIL A62a … compared=0` (`6 FAILURE(S)` total — every fixture that calls the walk failed alongside it, as expected for a blanket mutation) | ✅ PASS |

**Mutation 12 is the one finding worth a reviewer's attention on its own.** The fixture as
first written could not distinguish "structural exclusion" from "a name list that happens
to be correct today" — see §2 of the spec-back for the full account.

---

## 3. The exactly-seven-false-orphans reproduction (mutation 9, full output)

```
FAIL  A62a … orphans=[indicators.aggressor_velocity.sessions.LONDON.norm_window_sec,
             indicators.aggressor_velocity.sessions.ASIA.norm_window_sec,
             indicators.absorption.sessions.NY.min_aggr_usd,
             indicators.absorption.sessions.LONDON.min_aggr_usd,
             indicators.absorption.sessions.ASIA.min_aggr_usd,
             scoring.structural_levels.sessions.NY.fallback_target_atr_mult,
             session_volume.sessions.NY.roc_magnitude_threshold]
```

Seven entries, matching §3.3's four named examples exactly (both `aggressor_velocity`
sessions, all three `absorption` sessions, `structural_levels.sessions.NY`,
`session_volume.sessions.NY`).

---

## 4. Build matrix — gate tails

- `dotnet build DeribitVerdictEngine.sln -c Release` → `0 Warning(s)`, `0 Error(s)` (builds
  the app + `AutoTweaker` + `BacktestRunner`).
- `dotnet build tools/CeilingAudit/CeilingAudit.vbproj -c Release` → `0/0`.
- `dotnet build tools/WhatIfRunner/WhatIfRunner.vbproj -c Release` → `0/0`.
- `dotnet build tools/AutoTweaker/AutoTweaker.vbproj -c Release` (standalone, redundant
  with the solution build, run anyway per "each run separately") → `0/0`.
- `dotnet build tools/BacktestRunner/BacktestRunner.vbproj -c Release` (standalone) → `0/0`.
- `dotnet build verify/ordercheck/OrderCheck.vbproj -c Release` → `0/0`.
- `dotnet run --project verify/ordercheck/OrderCheck.vbproj` → `324` `PASS`, `0` `FAIL`,
  `ALL PASS`.
- `tools/checks/verify-gate.ps1 -Mode local-fast` → `harness ALL PASS` · `display-parity: no
  snapshot/card drift detected` · `result: GATE PASSED`.

---

## 5. Not committed as of this document

This document was written before the commit landed. The commit that lands with it carries:
`Core/Settings/EngineSettings.vb` (seven re-syncs + comment rewrite), `verify/ordercheck/Program.vb`
(the guard + `A62a`–`A62g`), `docs/DeribitIndicatorProject.md` (§15 row), and this pair of
documents plus the spec-back. Per D-4, all of it lands in **one** commit — the guard
without the re-syncs (or vice versa) would ship the harness red for anyone who has to
bisect between them.
