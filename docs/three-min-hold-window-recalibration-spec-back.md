# 3-min Hold-Window Recalibration — implementer spec-back

**Status: IMPLEMENTED (offline layer) + local-committed, NOT pushed. 2026-06-23.**
Authoritative spec: `three-min-hold-window-recalibration-proposal.md`. Routed to the
coordinator seat for review. One open decision flagged below (§5 live tracker).

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

**Recommendation (per spec §5 "if there is, scale it by resolution too"):** scale all three
sites by the row's `ExecResolution` (which the eval cache already carries since v36):
- maturity gates → `e.Timestamp.AddMinutes(15 * e.ExecResolution) <= / > nowUtc`
- `GetEligibleBars` → `t15 = ts.AddMinutes(15 * res)` (thread the row's resolution in).
Net effect: a 3-min PENDING row matures at T+45 and walks T+3..T+45. No eval-cache schema
change (the column already exists); the T+3 floor stays absolute, mirroring the offline fix.

**Why deferred, not implemented in this commit:**
1. It is a **behavioural change to a live-facing surface** (the perf strip the trader watches
   in real time), whereas the offline report is a read-only batch. The coordinator's prior
   expectation was "no change" here, so the magnitude of this surprise warrants explicit
   sign-off before touching the live runtime state machine.
2. A 3-min row would now stay PENDING for 45 min instead of 15 — the most-recent 3-min
   outcomes lag more on the strip. That's a UX change the trader should be aware of.
3. Spec-first / flag-before-implement discipline: the offline fix is the approved primary
   scope; this is a clearly-specified ~3-line follow-up the coordinator can green-light.
4. Zero risk to the running soak either way (Release build only; no schema change).

If approved, it's a small, isolated follow-up commit. Harness impact: none — A14f drives
`AggregateRange` with pre-built outcomes and does not exercise `GetEligibleBars` /
`ResolvePendingRows`, so no existing fixture regresses (a new fixture could assert the
scaled maturity gate if wanted).

## 6. Acceptance

- Builds **0/0**: solution (Release), `AutoTweaker.vbproj` (Release),
  `verify/ordercheck/OrderCheck.vbproj` (Release).
- OrderCheck harness **A1–A15h (38 checks) ALL PASS** (Release run).
- `git diff --stat` = only `analysis/AnalysisConstants.vb`, `AnalysisRunner.vb`,
  `FailureRateMatrix.vb`, `ForwardWindowJoiner.vb`, `MarkdownReportWriter.vb`
  (+ the doc/changelog edits). `tools/AutoTweaker/` byte-unchanged.
- **DEFERRED (spec §7 acceptance, behind the running soak):** regenerate the
  per-`(session × resolution)` report on the current book and confirm the 3-min Asia/London
  failure rate **plateaus within `{15,30,45}`** (ASIA in particular should stop declining).
  The recal's data gate is already MET (Thu 06-18 + Fri 06-19 + Mon 06-22), so it does not
  need today's data — the regen is held only to avoid OHLC/eval cache contention with the
  live 12h soak + B-test. If ASIA still declines at 45m, widen the multiplier set and
  document.

## 7. Open items for the coordinator

1. **Approve / decline the §5 live-tracker resolution-scaling** (the one behavioural
   follow-up). Recommended: approve — otherwise the live perf strip keeps the exact
   artifact the offline report just removed, and the two surfaces disagree for 3-min sessions.
2. Confirm the offline-report regen plan (run after the soak ends).
