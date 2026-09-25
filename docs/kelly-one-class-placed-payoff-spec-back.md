# Spec-back — Kelly as one class, p/b from the book's placed levels net of fees

**Written:** 2026-09-25 (UTC) by the implementer seat (Sonnet 5, high), reporting against `docs/kelly-one-class-placed-payoff-spec.md`. Companion: `kelly-one-class-placed-payoff-batch-summary.md` (the outcome record — read that first for what shipped).

---

## 1. Ranked verification handles

All `H-n` (the reader can run every one of these as written; none depends on a deleted scratchpad instrument).

**If you only run one: `H-1`.**

### `H-1` — harness, full run

```
dotnet build verify/ordercheck/OrderCheck.vbproj -c Release
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build
```

Expect `ALL PASS`, and the tail to show `468 -> 483` PASS lines if diffed against the pre-build tree (`git stash` the two commits and re-run to get the 468 baseline). Covers: the book filter, outcome classes, pooled-payoff formula, floor, one-class, no-edge, placed-stop sizing, side selection, bucket shape, bucket-floor fallback, tercile determinism — the whole `Core/`-side rewrite in one command.

### `H-2` — full solution + every standalone tool builds

```
dotnet build DeribitVerdictEngine.sln -c Release
dotnet build tools/CeilingAudit/CeilingAudit.vbproj -c Release
dotnet build tools/WhatIfRunner/WhatIfRunner.vbproj -c Release
```

Expect `0 Warning(s)`, `0 Error(s)` on all three. Covers: the `KellyBook`/`KellyBucket` relocation (§2 of the batch summary) didn't break any project that links `ScoringEngine_Kelly.vb`.

### `H-3` — no scoring or CSV file touched

```
git diff --stat 06e34c1^..31f57d0 -- Core/ScoringEngine_Calculate_Scoring.vb Core/ScoringEngine_Calculate_Verdict.vb AnalysisLogger.vb
```

Expect empty output. Covers: AC-8, and the reserved-class boundary the whole build stayed inside.

### `H-4` — settings v69, both retired/added keys

```
grep -n '"version"\|min_book_rows\|est_prob' settings.json
```

Expect `"version": 69`, `"min_book_rows": 400`, and **no** `est_prob_floor`/`est_prob_scale` hits. Covers: KO-3, KO-5, and that the POCO (`Core/Settings/EngineSettings.vb`) and JSON moved together (the arithmetic identity: a stale POCO field would make the harness's `A62a`-`g` drift-guard family fail, and it didn't — `H-1` covers that).

### `H-5` — display-string parity, mechanically

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/verify-gate.ps1 -Mode local-fast
```

Expect `GATE PASSED`, `OK    no snapshot/card drift detected`. This is the automated check for the exact property the parity hard rule names; it is the strongest evidence short of the live screen check in `H-6`.

### `H-6` — live screen check (re-runnable, but needs a live Deribit connection)

```
dotnet build DeribitVerdictEngine.vbproj -c Debug
Start-Process bin/Debug/net8.0-windows/DeribitVerdictEngine.exe -WorkingDirectory bin/Debug/net8.0-windows
# wait for load, then:
./tools/click-mainform-button.ps1 "Analyze"
./tools/screenshot-mainform-full.ps1 out.png
```

Expect the KELLY SIZING card visible, not clipped at the card's bottom edge, and — cross-checked against `bin/Debug/net8.0-windows/analysis_output_dump.md`'s latest `KELLY SIZING` block — identical text to the card's own rows. Delete `out.png` after checking (per the project's screenshot-cleanup convention).

---

## 2. Decisions queued (auto-proceed record, per the CLAUDE.md obligation)

None of these are reserved-class decisions requiring the trader — they are implementation details the spec's §3.3 left open once K-1 (g) was ruled, or build-time necessities. Logged here per CLAUDE.md's "log every auto-proceeded decision in one line" obligation.

| # | Decision | Options | Picked | Why |
|---|---|---|---|---|
| **D-1** | `VerdictResult` field list for K-1 (g)'s render state | (a) only the 5 fields spec §3.3 named (`KellyB`, `KellyBreakevenP`, `KellyBookN`, `KellyBookSession`, `KellyBookSufficient`) · (b) add `KellyHasSide`, `KellyBookSpanStartUtc`, `KellyBucketIndex`/`Lo`/`Hi`/`Fallback` | **(b)** | §3.3's list predates the K-1 (g) ruling at the bottom of the same doc; (g)'s own rendering requirement (name the bucket, the range, the fallback, the span start) cannot be satisfied by (a) without composing a number outside `CalcKellySizing` — which the "measured-number-free, reads every number from fields" instruction in (g) point 5 forbids. This is filling in a gap the ruling created, not re-opening it |
| **D-2** | Render gate for the whole block | (a) keep `KellyPWin > 0` · (b) `KellyHasSide` | **(b)** | Directly what F-1 and KO-4 require — a book-below-floor or `[NO EDGE]` row still needs `KellyPWin` at 0 or a placeholder, so the old gate can no longer distinguish "no side" from "computed, no edge" |
| **D-3** | "Net R:R (book)" line's composer | (a) reuse `BuildNetRRLine` (ATR-multiple based), fed different numbers · (b) a new line built directly from `v.KellyB` | **(b)** | §5's table entry ("Same composer, fed the book's pooled distances") predates K-1 (g): under (g) the book stores only the pooled RATIO (`NetPayoff`), not separate `ΣTarget`/`ΣStop` sums `BuildNetRRLine`'s signature needs. Reusing it would mean either storing sums I don't otherwise need (recording more, for a formatting convenience) or fudging a synthetic target/stop pair (fabricating a value). (b) formats a field that already exists — no new state, no synthetic distances |
| **D-4** | `KELLY_CARD_H` value | (a) proportional estimate only · (b) proportional estimate + a live screen check | **(b)** | AC-5 requires the screen check regardless; I raised it to 280px from a rough per-row estimate and confirmed no clipping on a real run (batch summary §3) rather than trusting the arithmetic alone |
| **D-5** | §15 row housekeeping (v64 falls out of the kept-5 window) | (a) perform the archive move to `history-archive.md` §E in this build · (b) add the row, flag the housekeeping, do not move it | **(b)** | The archive move requires reading and matching `history-archive.md`'s own §E conventions, which sits outside this session's briefed reading list (the trader's override explicitly said not to read `DeribitIndicatorProject.md` in full, and by extension its retention-mechanics sibling doc). Flagging costs one line; doing it wrong costs a malformed archive entry in an unrelated file. **I have no strong read here — this is closer to "the criterion is the trader's"** than a clean auto-proceed, since it touches a doc-maintenance convention outside the Kelly spec's scope. Recommend a short dedicated follow-up rather than folding it into a future Kelly-adjacent commit |
| **D-6** | `tools/ops/q1d_tier_geometry.py` §11 wording (F-6) | (a) leave the section silently reading stale/broken · (b) patch with a documented historical literal | **(b)**, per the spec's own F-6 instruction — not an open decision, included here only because it required picking exact wording | — |

---

## 3. Spec-back proper — feedback on the spec

### What the spec got right, specifically

- **The K-1 (g) definition (§4) was precise enough to implement without a single clarifying assumption.** Every mechanism — tercile-by-index-partition, the clamp-to-end-buckets rule for `SelectKellyBucket`, the fallback-to-session-pool condition, "no settings key, self-updating each run" — translated directly into code with no gaps. This is the strongest praise I can give a spec: I did not have to guess anywhere in the core algorithm.
- **F-1's diagnosis of the render gate was exactly right** and the fix (`KellyHasSide`) followed mechanically once named. Worth calling out because it's the kind of finding that's easy to get subtly wrong (e.g., conflating "no edge" with "no side" the way the pre-v69 code did) and the spec didn't.
- **"Take entries as a parameter so fixtures can pass their own" (§3.1) was the single most load-bearing sentence in the spec for testability.** Every one of the 12 mutation-proofs in `H-1` was possible ONLY because `ComputeKellyBook` and `SelectKellyBucket` are pure functions over explicit inputs, not because of anything I added.

### Which assumptions broke

- **§3.3's "New `VerdictResult` fields" list was written before the K-1 (g) ruling and undercounted by 6 fields** (see D-1 above). Not a defect in the spec — the doc says so itself ("At my read K-1 (e)... §3.3 predates the (g) ruling") — but worth flagging explicitly since a less careful implementer could have tried to force (g)'s render requirements into 5 fields by composing strings outside `CalcKellySizing`, which the rest of the doc explicitly forbids.
- **§5's "Net R:R line... fed the book's pooled distances" assumed option (e)'s data shape** (separate `ΣTarget`/`ΣStop`), which K-1 (g) doesn't carry (only the pooled ratio). See D-3.
- **A project-wiring assumption that isn't stated anywhere in the spec:** that `CalcKellySizing`'s signature change is free to make. It isn't — `Core/ScoringEngine_Kelly.vb` is part of a partial class linked by multiple `.vbproj` files beyond the main app and `OrderCheck.vbproj`, and the spec's own file list (§0, "the code the spec cites") does not mention `tools/BacktestRunner/BacktestRunner.vbproj` or any other standalone tool project. This is exactly the "Fixtures cannot catch" class the spec itself names for the UI surfaces, but it applies just as much to project files: `OrderCheck.vbproj` doesn't link `tools/BacktestRunner`, so a solution-wide build break there is invisible to `H-1` and was only caught by `H-2`. Worth adding "and every standalone .vbproj that links the changed Core/ files" to future specs' acceptance criteria.

### Where the spec was narrower than its own words

- **AC-5's "two states" (a directional run and a lean NO TRADE run) assumed both are independently reachable live.** On today's book (f* < 0 everywhere, `H-4`/§4 of the batch summary), a directional run and a lean run render the SAME state ([NO EDGE], both use the identical book/bucket logic) — the only real behavioural difference between them is the `[BIAS ONLY — NO TRADE]` tag, which is a one-line string switch already exercised at the fixture level (`A90h`). I captured the reachable state live (twice, both WEAK LONG) and relied on `A90h` for the tag logic rather than burning further live-market attempts chasing a `NO TRADE [WEAK ...]` verdict that may not occur for hours. Named here so it isn't read as a shortfall.

### Constraint pairs that nearly conflicted

- **"Zero scoring impact" vs. "the book needs `ExecutionResolution.MatchSessionBucket`."** That function lives in `Core/`, already used by scoring — using it for Kelly's session match risked looking like a scoring-adjacent dependency. It isn't: `MatchSessionBucket` is a pure lookup with no scoring side effect, and Kelly already depended on session-derived display values pre-v69 (the fallback target multiplier). No real conflict, but worth naming since a less careful read could have flagged it as a reserved-class boundary question.

---

## 4. What I did not verify, and cannot

| Claim | Why not verified |
|---|---|
| **The lean `NO TRADE [WEAK ...]` and plain `NO TRADE`/`[TIE]` states on a LIVE run** | Not reachable in this session's window — see §3 "narrower than its own words" above. Covered instead by `A90h` (fixture) |
| **A live `[NO EDGE]` → full-sizing transition on real data** | Not reachable — every session/bucket has f* < 0 today (AC-4, batch summary §4). Covered by `A90f`/`A90g` (fixtures, synthetic books) |
| **That the collector box's eval cache behaves identically to the dev box's** | Not compared. The AC-4 read used a fetched copy (`aws_fetch/20260925-085341/`), not a live read against the running collector |
| **Thread-safety of `LivePerformanceTracker._evalCache` read from `ComputeKellyBook(sessionName, cfg)`** | Carried from the spec (§11 "Not verified"), unchanged by this build — same precedent (`ComputeWindows`) applies to the new 2-arg wrapper, same caveat |
| **The deploy** | Reserved to the trader per the spec §8; not attempted |
