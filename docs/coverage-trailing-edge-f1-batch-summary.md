# C1-coverage F1 — trailing-edge gap mis-attribution fix: batch summary

**Author seat:** Sonnet, effort HIGH, one session. **Date:** 2026-08-26.
**Spec:** [`coverage-trailing-edge-f1-proposal.md`](coverage-trailing-edge-f1-proposal.md) §4b (the single authoritative build list — §4 is the pre-tick record only).
**Status:** ✅ BUILT. Local commit only — trader tests + pushes.

Companion document: [`coverage-trailing-edge-f1-spec-back.md`](coverage-trailing-edge-f1-spec-back.md) (verification handles, spec-back proper, what was not verified).

---

## 0. Headline

- All eleven ruled decisions (D-1…D-6, D-5.1…D-5.5) built exactly as `coverage-trailing-edge-f1-proposal.md` §4b specifies. Nothing re-opened.
- Both silent traps named in the brief (D-5.1's precedence, D-4's superset trap) are guarded and **mutation-proofed** — see §4.
- `HourClass.Defect` count is **UNMOVED**, proved by an actual pre-fix/post-fix comparison against a real store copy, not asserted. `--strict`'s exit code is likewise unmoved (both 1, both times). See §6.
- The real AWS-copy run reports **zero** genuine trailing-edge hours in the available window — a legitimate finding, not a bug. The mechanism is independently confirmed reachable via a synthetic scenario shaped exactly like the original defect report (§6).
- `verify-gate.ps1 -Mode local-fast` → **GATE PASSED**.

---

## 1. What changed

**`tools/BacktestRunner/CoverageReport.vb`** — the fix itself:

| Piece | Change |
|---|---|
| `HourClass` enum | New `TrailingEdge` value, declared between `Defect` and `ExpectedMissing` (shifts four ordinals — verified inert, every reference is by name) |
| `HourStoreStats` | New `LastTsMs As Long?` (D-2/D-3(c) — nullable, not a sentinel) |
| `AccumulateHourStats` | Populates `LastTsMs` per hour bucket; return type is now a tuple `(ByHour, StoreEndMs)` — `StoreEndMs` is the walk's own last trade **filtered to `[fromUtc, toUtc)`**, never a `Max()` over the unfiltered whole-month-file dictionary (D-4's superset trap) |
| `AccumulateSplitSpanStats` | Mirrors the `LastTsMs` population (sibling accumulator, D-1) |
| `ClassifySpan` (private, shared by both paths) | New `boundMs As Long` parameter; flags `TrailingEdge` when a span is otherwise clean but silent from its last trade to `Min(spanEnd, boundMs)` past `gap_ms` |
| `ClassifyHour` | New `Optional observedBoundMs As Long = Long.MaxValue` parameter, threaded to every `ClassifySpan` call; split-hour combine gains `TrailingEdge` between `Defect` and `Captured` (D-5.1) |
| `BuildResult` | Resolves `observedBoundMs = Min(walkToUtcMs, StoreEndMs)` **once**, passes it to every hour in the walk; tracks `TrailingEdgeHours`/`ObservedLongestTrailingMs` beside the existing gap counters (D-6(c)) |
| `BuildConsoleSummary` | New `trailing-edge` count line, new `longest trailing` line, VERDICT line now also gates on trailing-edge count (D-5.3); **the pre-existing placeholder note is deleted** |
| `BuildMarkdown` | Unchanged — the new class renders for free (only `Captured`/`OutOfScopeWeekend` are skipped) |

**`tools/BacktestRunner/BacktestProgram.vb`** — unchanged. `--strict` stays keyed on `HourClass.Defect` alone (D-5.2), by decision, not oversight.

**`verify/ordercheck/Program.vb`** — three existing fixtures revised, six new fixtures added. See §3/§4.

**Settings:** no keys added or changed. `settings.json` stays v68, no version bump.

**Docs:** a new §15 entry added to `docs/DeribitIndicatorProject.md` per this project's standing convention (confirmed for tools-only changes by the 2026-08-25 AutoTweaker weekday-filter entry).

---

## 2. Fixture revisions — table

| Fixture | Why it moved | Revision |
|---|---|---|
| `A49e` | Calls `ClassifyHour` directly, bypassing `BuildResult` — no bound is resolved automatically for it | Passes the real `StoreEndMs` (from `AccumulateHourStats`'s new tuple output) as `observedBoundMs`, isolating the fixture back to its original inter-trade-gap-threshold intent |
| `A49o` | ON-half span's last trade sits ~27 min before the hour's own end; real `AccumulateSplitSpanStats` now populates `LastTsMs` for real, so the default unbounded case would wrongly flag `TrailingEdge` | Computes the real `StoreEndMs` and passes it explicitly |
| `A49t` | Same trap, ~10 min trailing edge on the middle ON span | Same revision |
| `A49b`, `A49c`, `A49l`, `A49r` | Listed in the spec as at-risk (trap 3) | **No edit** — confirmed: their hand-built `HourStoreStats` never set `LastTsMs`, it defaults to `Nothing`, the trailing check's `.HasValue` guard skips. Re-ran unchanged, PASS |
| `A49g` | Listed as D-4(c)'s first witness | **No edit** — goes through `BuildResult`, which resolves the bound automatically. PASS unchanged |
| `A49u` | Knife-edge witness for the `<=`/`>` measurement convention (299,999ms vs 300,000ms threshold) | **No edit** — verified the math still lands at exactly 299,999ms under the default bound, does not flag, and the hour's final classification is unaffected regardless (it's already `Defect` via the other span) |

---

## 3. New fixtures — F1-a through F1-f

| Fixture | Shape | Asserts |
|---|---|---|
| `F1-a` | Trades early in an hour, silence to hour end, a later trade well into the next hour (the canonical shape from the original defect report) | `TrailingEdge`, not `Captured` |
| `F1-b` | Same shape, but the hour IS the last hour with any data | `Captured` — store-end exempts it |
| `F1-c` | `--to` lands mid-hour (partial final hour) | `Captured` — measures to the boundary instant, not `:59:59.999` |
| `F1-d` | Split hour, one span clean, one span trailing-edge | `TrailingEdge` for the whole hour, never `Captured` |
| `F1-e` | Store's month file carries a trade PAST the walked range (`walkToUtc`), in the same file as the walk's genuinely-last hour | `Captured` — the superset trade must not un-exempt the true last walked hour |
| `F1-f` | Direct `BuildConsoleSummary` call, hand-built `CoverageResult` with one `TrailingEdge` hour and zero `Defect` hours | VERDICT line does NOT say `clean` |

All six executed and passed — confirmed by grepping the harness's own `PASS  F1-` lines, not by counting fixture definitions in source.

---

## 4. Mutation-proofing (§6's requirement)

Every mutation was applied as a temporary edit, rebuilt, run, confirmed FAIL, reverted, rebuilt, confirmed PASS again. No mutation was left in the shipped code.

| Fixture | Mutation | Result |
|---|---|---|
| `F1-a` | Disabled the D-1 trailing check in `ClassifySpan` entirely | FAILED (`h05=Captured`) — as expected. `F1-d` also failed as a bonus confirmation (it shares the dependency); `F1-b`/`F1-c`/`F1-e` stayed green, correctly, since removing the check can't falsify a test that expects "not flagged" |
| `F1-b` / `F1-c` (bound-defeat) | Forced `observedBoundMs = Long.MaxValue` unconditionally in `BuildResult`, defeating D-4(c)'s bound | `F1-b`, `F1-c`, AND `F1-e` all FAILED — confirming all three genuinely depend on the bound |
| `F1-d` (own mutation) | Swapped the combine order — `Captured` checked before `TrailingEdge` | FAILED (`h05=Captured`) — and it is the **only** split-hour fixture that failed; `A49o/A49q/A49s/A49t/A49u/A49v/A49w` all stayed green under the identical mutation, confirming the spec's claim that the naive order passes every other fixture in the table |
| `F1-e` (own mutation) | Derived `StoreEndMs` via `Max()` over the whole (superset) `AccumulateHourStats` dictionary instead of the walk's own filtered last trade | FAILED (`h05=TrailingEdge`, wrongly) — and `F1-b` stayed green under this mutation, confirming it is a genuinely distinct failure mode from the bound-defeat mutation above |

---

## 5. Build status

All six projects, each built separately, Release configuration:

| Project | Result |
|---|---|
| `DeribitVerdictEngine.sln` (DeribitVerdictEngine + AutoTweaker + BacktestRunner) | 0 Warning(s), 0 Error(s) |
| `tools/CeilingAudit/CeilingAudit.vbproj` | 0 Warning(s), 0 Error(s) |
| `tools/WhatIfRunner/WhatIfRunner.vbproj` | 0 Warning(s), 0 Error(s) |
| `verify/ordercheck/OrderCheck.vbproj` | 0 Warning(s), 0 Error(s) |

Harness: **306 PASS / 0 FAIL — ALL PASS**, run after the final clean build (no leftover mutation code, no leftover debug prints).

`verify-gate.ps1 -Mode local-fast`:
```
OK    harness ALL PASS
=== display-parity ===
OK    no snapshot/card drift detected
=== version-bump ===
OK    no engine-path change
=== result ===
GATE PASSED
```

---

## 6. Real-store acceptance run (§7)

Store used: `AWS-copybacks/aws-copyback-2026-08-14/` — the freshest AWS copy-back present locally. **~12 days stale relative to today (2026-08-26).** No live SSH/SSM pull was performed this session — see the spec-back's "what was not verified" for the consequence of that.

Evidence files (`analysis_log.csv`, `ws_health.log`, `capture_marker.log`) were temporarily copied from that same copy-back into the repo root (none existed there before), used for the run, then deleted — repo root is back to its pre-session state.

**`--strict` both ways, `--from 2026-08-01 --to 2026-08-14 --gap-ms 300000`:**

| | Pre-fix (git-stashed) | Post-fix |
|---|---|---|
| `captured hours` | 100 | 100 |
| `DEFECT` | **4** | **4** |
| `unknown-scope` | 112 | 112 |
| `out-of-scope-weekend` | 96 | 96 |
| `trailing-edge` | (class did not exist) | 0 |
| Exit code (`--strict`) | **1** | **1** |
| Exit code (no `--strict`) | 0 | 0 |

`DEFECT` and the `--strict` exit code are **byte-identical** pre-fix vs post-fix. Every other class count is also identical, confirming the new class is not silently reached through the wrong branch.

**Three-surface confirmation** — the F1-a scenario run end-to-end through the real CLI functions (`BuildResult` → `BuildConsoleSummary` + `BuildMarkdown`):

```
TRADE STORE COVERAGE  2026-07-20 → 2026-07-20
  captured hours      0
  DEFECT              1   ← capture defects
  trailing-edge        1   ← silence to the observed edge, not a gap between trades
  ...
  longest trailing    3480.0s  (1 trailing-edge hour(s))
  ...
  VERDICT: 1 defect hour(s) + 1 trailing-edge hour(s) + store gaps above
```
```
| Hour (UTC) | Class | Instance | Reason |
|---|---|---|---|
| 2026-07-20 05:00 | TrailingEdge |  | trailing-edge(3479999ms) |
| 2026-07-20 06:00 | Defect |  | gap-breach(S1 skipped,5280000ms) |
```

Console count line ✅, markdown table row ✅, VERDICT line ✅ — all three surfaces confirmed on genuinely rendered output, not asserted.

**Why the real AWS-copy run itself shows zero trailing-edge hours:** the store is a continuously-streaming WS capture; the shape the fix targets (dense trades, then a clean stop mid-hour, then a resumption well into the next hour with no internal gap on either side) is rare outside a restart/deploy boundary. This is a legitimate absence, not evidence the mechanism is unreachable — the synthetic F1-a run above proves it fires correctly when the shape occurs.

---

## 7. Display-string parity

**No card surface is affected.** This is a `tools/`-only change to the `BacktestRunner` CLI's own renderers (`BuildConsoleSummary`/`BuildMarkdown`). It does not touch `UI/MainForm_PlaintextSnapshot.vb` (the engine's only text renderer) or `UI/MainForm_Render_Cards.vb` (the card bindings) — neither file was read or edited this session, and `verify-gate.ps1`'s own display-parity check confirms no drift.

---

## 8. Commit

Local commit only, per this project's GitHub workflow (`docs/trader-profile.md` §8) and this brief's explicit instruction. The trader tests and pushes.
