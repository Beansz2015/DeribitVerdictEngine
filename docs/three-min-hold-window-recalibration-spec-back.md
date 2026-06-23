# 3-min Hold-Window Recalibration — implementer spec-back

**Status: IMPLEMENTED (offline matrix + live tracker) + local-committed, NOT pushed. 2026-06-23.**
Authoritative spec: `three-min-hold-window-recalibration-proposal.md`. Routed to the
coordinator seat for review. The §5 live-tracker scaling was trader-approved 2026-06-23 and
is now implemented (was flagged as the one open decision; see §5). One optional future item
flagged for the coordinator: early-resolution-on-confirmed-hit (§5, end).

---

## 1. What shipped

Resolution-scaled offline eval windows so every execution resolution gets the same
**bar-count** budget — `{5,10,15}` bars — instead of a fixed `{5,10,15}` *minutes*
that gave 3-min trades a third of the bar-budget and inflated their failure rate with
spurious window-expiry "failures."

New lookup (`analysis/AnalysisConstants.vb`):

```
HoldWindowsForResolution(execRes) = { 5×execRes, 10×execRes, 15×execRes }
  res=1 (NY)        → { 5, 10, 15}   (= HoldWindowsMinutes; value-identical)
  res=3 (ASIA/LON)  → {15, 30, 45}   (= 5/10/15 three-minute bars)
```

`HoldWindowsMinutes = {5,10,15}` is **kept** as the res=1 base case (the auto-tweaker's
`PromptBuilder` reads it directly and is always res=1, so it stays byte-identical).

## 2. Implementation map (spec §4 — the 5 offline consumers)

| Site | Change |
|---|---|
| `AnalysisConstants.vb` | Added `HoldWindowsForResolution(execRes)`. `execRes <= 0` guards to 1. |
| `ForwardWindowJoiner.PopulateForwardBars` | Window loop now `HoldWindowsForResolution(row.ExecResolution)` — **per row**. A 3-min row keys `ForwardBars` `{15,30,45}`, a 1-min row `{5,10,15}`. The `For closeMin = 3 To w` T+3 floor is unchanged (see §3). |
| `FailureRateMatrix.Compute` | New trailing `Optional resolution As Integer = 1`. One `Dim windows = HoldWindowsForResolution(resolution)` drives the 3 internal window loops (counts-init / per-row walk / result-build). The AutoTweaker call omits the arg → default 1 → byte-identical. |
| `AnalysisRunner.Run` | (a) OHLC fetch end now `max(ts) + (largest window present + 1)` min — a 3-min book needs forward OHLC to T+45, not T+16. (b) `ExcludedRows` uses the population's `HoldWindowsForResolution(popRes)`. (c) `Compute` call passes `popRes(popKey)`. |
| `MarkdownReportWriter` | `AppendMatrixGrid` gains a `resolution` param; its Window-column loop + the `AppendDecomposition` loop use `HoldWindowsForResolution(pop.Resolution)`. NY tables render 5/10/15m rows; 3-min tables render 15/30/45m. Added a one-line reader note under the Section 2 header. |

`AnalysisRunner.ComputeContextOutcomes` needed **no change** — it reads the window from
`recCell.WindowMin` (the population's recommended cell), which is already resolution-correct
once `Compute` runs per population. (Its legacy `w=10` fallback only fires when there is no
recommended cell at all — i.e. insufficient data — and degrades to "insufficient sample"
for a 3-min population, which is honest.)

## 3. Decisions worth recording

- **T+3 execution-latency floor is NOT scaled.** `PopulateForwardBars` still excludes bars
  closing at T+1/T+2. 3 minutes of "can't execute yet after the verdict fires" is 3 minutes
  regardless of chart resolution; the spec §3 only scales the window *endpoints*. So a 3-min
  row's eligible bars are T+3..T+45 (1-min bars), which still gives ~5/10/15 three-minute
  bars of *window length*. Barrier detection deliberately keeps 1m OHLC granularity inside
  the window — finer granularity = more accurate wick-hit detection; only the window length
  scales.
- **No `settings.json` change.** The windows live in `AnalysisConstants` (a code constant),
  per spec §6. No version bump, no `change_log` entry. A §15 "post-v41" entry was added to
  `DeribitIndicatorProject.md` because the offline report output changes.
- **No card-binding obligation.** The engine display-string parity rule covers the RTF
  renderers / `BuildPlaintextSnapshot` / cards. This change only touches the offline
  **markdown report** surface (`MarkdownReportWriter`), a different surface. No card change.

## 4. Safety — auto-tweaker byte-identical (spec §6)

- `tools/AutoTweaker/` is **untouched** — `git diff --stat` shows only the 5 `analysis/*.vb`
  files changed.
- The tweaker is NY×1-population-filtered (Phase-2a): `windowRows` is a slice of the
  population-`filtered` set, all `ExecResolution=1`. `PopulateForwardBars` therefore keys
  `{5,10,15}`, and `FailureRateMatrix.Compute(...)` is called without the resolution arg →
  defaults to 1 → `HoldWindowsForResolution(1) = {5,10,15}` (value-identical to the old
  `HoldWindowsMinutes`). The 3-min window change is invisible to the tweaker.
- Confirmed live by the OrderCheck harness: A15a–A15h exercise the real `MatchesPopulation`
  / `RunAsync` re-seed / `SettingsDiffApplier.Validate` chain — all green.

## 5. Live eval horizon (`LivePerformanceTracker`) — FINDING: there IS a hidden cap

The spec §5 said "likely barrier-based, no change — confirm don't assume." Confirmed against
the code: **it is NOT pure barrier-hit.** There is a hard 15-min horizon cap at **three** sites:

- `InitialiseAsync` Step 1.5 backfill: `row.Timestamp.AddMinutes(15) <= nowUtc` gate.
- `ResolvePendingRows`: `If e.Timestamp.AddMinutes(15) > nowUtc Then Continue For` — a PENDING
  row only matures after 15 wall-clock minutes.
- `GetEligibleBars(ts, nowUtc)`: walks bars `T+3..T+15` (`t15 = ts.AddMinutes(15)`).

So the **live perf-strip Asia/London 3-min rates carry the same window-too-short artifact**
the offline report just fixed: a 3-min trade gets only ~5 three-minute bars to hit target
before it's force-classified (mostly as `WINDOW_EXPIRED` → FAILURE).

**TRADER-APPROVED 2026-06-23 + IMPLEMENTED** (per spec §5 "if there is, scale it by
resolution too"). All three sites now scale by the row's `ExecResolution` (carried by the
eval cache since v36) via one shared helper:

```
EvalHorizonMinutes(execResolution) = AnalysisConstants.HoldWindowsForResolution(res).Max()
  res=1 → 15   res=3 → 45
```

Routing it through `AnalysisConstants.HoldWindowsForResolution(...).Max()` (rather than a
local `15 * res`) ties the live horizon to the **same source of truth** as the offline
matrix's largest window — the two surfaces can never silently diverge.

| Site | Change |
|---|---|
| `GetEligibleBars` | New `execResolution` param; walks T+3..`T+EvalHorizonMinutes(res)` instead of T+3..T+15. |
| `EvaluateEntry` (line ~660) | Passes `e.ExecResolution` to `GetEligibleBars`. |
| `MigrateV1ToV2` (line ~691) | Passes `e.ExecResolution` to `GetEligibleBars`. |
| `ResolvePendingRows` maturity gate (line ~640) | `e.Timestamp.AddMinutes(EvalHorizonMinutes(e.ExecResolution)) > nowUtc` — a PENDING row matures only after its scaled horizon. |
| `InitialiseAsync` Step-1.5 backfill gate (line ~340) | `row.Timestamp.AddMinutes(EvalHorizonMinutes(entry.ExecResolution)) <= nowUtc`. |

Net effect: a 3-min PENDING row matures at **T+45** and walks T+3..T+45 (= 15 three-minute
bars); NY/1-min unchanged at T+15. **No eval-cache schema change** (the `ExecResolution`
column already exists); the T+3 floor stays absolute, mirroring the offline fix. Two stale
docstrings ("15-min windows" / "T+3..T+15") corrected for accuracy.

Behavioural note for the trader: the live perf strip's 3-min Asia/London outcomes now lag
~45 min (vs 15) because a 3-min trade's outcome genuinely can't be known until its full
window fills — slower but correct, replacing fresher-but-wrong. NY/1-min cadence is identical.

### Future consideration (NOT in this pass) — early-resolution-on-confirmed-hit

The tracker waits for the **full** window before recording *any* outcome — it does not
early-resolve the instant a barrier is hit. So a 3-min trade that wins at T+9 still won't
show as a win on the strip until T+45. This was already true at 1-min (a win at T+6 waited
until T+15); the resolution scaling just makes the latency more visible at 3-min.

A refinement would let a PENDING row resolve early once a favourable/adverse barrier is
*confirmed* hit in the bars available so far (peek each tick rather than wait for window
completion), cutting the display lag for fast 3-min trades. It's more involved — it changes
the resolve loop from a single window-complete check to an incremental walk, and needs care
that an early favourable hit isn't later "un-won" by an adverse bar (the barrier-hit
semantic already resolves on the *first* hit chronologically, so a confirmed first hit is
final — but the implementation must walk only the bars that exist *and* stop at the first
hit, not re-evaluate). **Flagged for the coordinator** as a separate, optional spec; left
out here to keep the live tracker's window math identical to the offline matrix.

Harness impact of the implemented change: **none** — A14f drives `AggregateRange` with
pre-built outcomes and does not exercise `GetEligibleBars` / `ResolvePendingRows`, so no
existing fixture regresses. (A future fixture could assert the scaled maturity gate.)

## 6. Acceptance

Two local commits (offline matrix, then the trader-approved live-tracker scaling):

- Builds **0/0** after each: solution (Release), `AutoTweaker.vbproj` (Release),
  `verify/ordercheck/OrderCheck.vbproj` (Release).
- OrderCheck harness **A1–A15h (38 checks) ALL PASS** (Release run) after each commit.
- Offline-matrix commit `git diff --stat` = only the 5 `analysis/*.vb`
  (`AnalysisConstants`, `AnalysisRunner`, `FailureRateMatrix`, `ForwardWindowJoiner`,
  `MarkdownReportWriter`) + the doc/changelog edits. `tools/AutoTweaker/` byte-unchanged.
- Live-tracker commit changes only `LivePerformanceTracker.vb` (+ this spec-back +
  changelog). `analysis/` and `tools/AutoTweaker/` byte-unchanged; eval-cache schema
  unchanged (the `ExecResolution` column already existed since v36).
- All builds done **Release-only** (bin/Release) so the live 12h soak + B-test running in
  bin/Debug (transport=rest, shadow_parity=true) is never touched — no Debug rebuild, no
  relaunch.
- **DEFERRED (spec §7 acceptance, behind the running soak):** regenerate the
  per-`(session × resolution)` report on the current book and confirm the 3-min Asia/London
  failure rate **plateaus within `{15,30,45}`** (ASIA in particular should stop declining).
  The recal's data gate is already MET (Thu 06-18 + Fri 06-19 + Mon 06-22), so it does not
  need today's data — the regen is held only to avoid OHLC/eval cache contention with the
  live 12h soak + B-test. If ASIA still declines at 45m, widen the multiplier set and
  document.

## 7. Open items for the coordinator

1. **§5 live-tracker resolution-scaling — DONE** (trader-approved + implemented 2026-06-23).
   No further decision needed; noted here for the review trail.
2. **Early-resolution-on-confirmed-hit** (§5, end) — optional future refinement to cut the
   live-strip display lag for fast 3-min trades. Flagged for the coordinator; the trader
   will raise it. Not specced or built here.
3. Confirm the offline-report regen plan (run after the soak ends) — the spec §7 acceptance
   that the 3-min Asia/London failure rate plateaus within `{15,30,45}`.

---

> **Coordinator review — APPROVED (2026-06-23, sanity-check seat).**
>
> Re-ran the acceptance independently:
> - **Builds 0/0** — solution, `AutoTweaker`, `OrderCheck`, all **Release** (`-c Release` → bin/Release; the live 12h soak + B-test in bin/Debug untouched, no Debug rebuild).
> - **OrderCheck A1–A15h (38) ALL PASS** (Release) — incl. the A14/A15 resolution + NY×1-filter + Validate chain.
> - **Diff audited line-by-line vs spec §4/§5.** Offline matrix: all 5 sites scaled correctly (`AnalysisConstants` lookup; `ForwardWindowJoiner` per-row keys; `FailureRateMatrix.Compute(resolution)` driving all 3 window loops; `AnalysisRunner` fetch-span + ExcludedRows + per-pop Compute; `MarkdownReportWriter` grid + decomposition). Live tracker: all **three** functional horizon sites scaled (`InitialiseAsync` maturity gate, `ResolvePendingRows` maturity gate, `GetEligibleBars` endpoint) via `EvalHorizonMinutes = HoldWindowsForResolution(res).Max()`; the T+3 floor is correctly absolute (L881), and the `AddMinutes(1)` sites are bar-stepping, not horizons.
> - **NY×1 byte-identical CONFIRMED** — diffstat shows `tools/AutoTweaker/` untouched; the tweaker's `Compute` omits the resolution arg → default 1 → `HoldWindowsForResolution(1) = {5,10,15}`, value-identical to the old `HoldWindowsMinutes`. The `execRes<=0→1` guard cleanly handles legacy/v1 rows (`EvalHorizonMinutes(0) → 15`).
> - **Design strength:** routing the live horizon through the SAME `HoldWindowsForResolution(...).Max()` as the offline matrix (not a local `15*res`) ties both surfaces to one source of truth — they can't silently diverge. Right call.
>
> **Minor / non-blocking (none gate acceptance):**
> 1. **Stale comment** — `LivePerformanceTracker.vb:36` still reads `T+3..T+15` (the §5 docstring sweep missed it). Cosmetic.
> 2. **Historical cached 3-min rows are NOT re-walked under the new horizon.** The maturity/resolve paths only re-evaluate PENDING rows, and the v35 floor self-heal (`ApplyMinMoveFloorReeval`) fires only on a *floor* change — not a horizon change. So 3-min rows already resolved at T+15 by the running (old-code) app stay `WINDOW_EXPIRED` after rebuild; the live perf-strip 3-min rates self-heal only as those rows age out of the rolling cache. **Display-only, self-healing; the offline report (authoritative) recomputes fresh and is unaffected.** If the trader wants the strip corrected immediately, a one-time horizon-re-eval on load (mirror the v35 floor self-heal, gated on a stored horizon marker) is the clean fix — optional, not built. Set expectations on rebuild: the strip's 3-min rates won't jump instantly.
> 3. **No harness fixture** asserts the scaled live maturity gate (A14f drives `AggregateRange` with pre-built outcomes, doesn't reach `GetEligibleBars`/`ResolvePendingRows`). Acceptable for a display-only path; a future fixture could close it.
>
> **Remaining (unchanged):** the offline-report regen plateau validation stays **deferred behind the running soak** (avoids OHLC/eval-cache contention); run it once the soak ends. Local-first — trader tests + pushes.
