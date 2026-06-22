# 3-min Hold-Window Recalibration — resolution-scaled eval windows

**Status: SPEC / ready for a fresh implementer conversation (2026-06-23).** Data gate **MET** (3 weekday 3-min sessions). Analysis/display-layer — **zero scoring votes, auto-tweaker-safe** (see §6). Coordinator authors; trader tests + pushes. Pairs with the (B) Asia/London ROC re-baseline (`asia-london-roc-rebaseline-proposal.md`) — same data trigger, the calibration half of the v36 3-min story.

---

## 1. Problem (§12 WATCHING, trader-flagged 2026-06-18)

The offline failure-rate matrix evaluates every directional verdict over hold windows **`{5, 10, 15}` minutes** (`AnalysisConstants.HoldWindowsMinutes`). These are **1-min-calibrated** and applied **uniformly regardless of execution resolution**. For the v36 3-min Asia/London sessions, `{5,10,15}` min = only **~{1.7, 3.3, 5} three-minute bars** — far too short for a 3-min trade to reach its target.

The symptom (per-`(session×resolution)` report, 2026-06-18): **3-min tiers fail almost entirely by *window-expiry*, not adverse hits, and the failure rate keeps collapsing as the window lengthens** — i.e. the trades *do* work, just not within 15 min:

| Population / tier | @5m | @10m | @15m |
|---|---|---|---|
| LONDON×3 MEDIUM_SHORT (n=28) | 54% | 7% | 4% |
| ASIA×3 MEDIUM_LONG (n=27) | 59% | 30% | **19% (still declining)** |
| NY×1 (reference) | reaches target *fastest* (success 17→27→32 across 5/10/15m) |

LONDON resolves by ~10–15m; **ASIA is still at 19% failing at the 15m cap** — its true plateau is beyond the current window. So the 3-min failure rates the report shows are inflated by an eval artifact, not real trade quality.

## 2. Root cause

`HoldWindowsMinutes` measures wall-clock minutes, but a trade develops in **bars**. 1-min sees 5/10/15 bars at 5/10/15 min; 3-min sees the same bar-counts only at **15/30/45 min**. The single global array gives 3-min trades a third of the bar-budget to reach target → spurious window-expiry "failures."

## 3. Change — resolution-scaled hold windows

Make the eval windows **resolution-aware** so every resolution gets the same *bar-count* budget:

```
HoldWindowsForResolution(execRes) = { 5×execRes, 10×execRes, 15×execRes }
  res=1 (NY)        → { 5, 10, 15}   (unchanged — byte-identical)
  res=3 (ASIA/LON)  → {15, 30, 45}   (= 5/10/15 three-minute bars)
```

`{15,30,45}` for 3-min is the principled default (bar-count parity with 1-min). It extends ASIA's window to 45 min so its slower-resolving trades reach target inside the eval horizon; the implementer **re-runs the report and confirms the 3-min failure rate plateaus** within the new windows (and may tune the multiplier set if ASIA still hasn't plateaued at 45m).

## 4. Implementation sketch

`HoldWindowsMinutes` (`AnalysisConstants.vb:49`) is consumed at **6 sites** — replace the bare array with a per-resolution lookup, threading the population's `ExecResolution` (already known since the per-`(session×resolution)` report segmentation, `ce7ba4b`):

- `AnalysisRunner.vb:110` — the "enough forward data" row filter must use the resolution's windows (a 3-min row now needs 45 min of forward OHLC, not 15).
- `FailureRateMatrix.vb:113 / 168 / 217` — `Compute` already runs **per population** (NY×1 / ASIA×3 / LONDON×3); pass that population's resolution so each matrix uses its own windows.
- `ForwardWindowJoiner.vb:125` — join forward returns over the resolution's windows.
- `MarkdownReportWriter.vb:159 / 242` — render per-resolution window columns (NY table shows 5/10/15; the 3-min tables show 15/30/45). Caption the window set per sub-table.

Cleanest shape: add `AnalysisConstants.HoldWindowsForResolution(execRes As Integer) As Integer()` (and keep the base `{5,10,15}` as the res=1 case), then route the 6 sites through it via the population's resolution.

## 5. Live eval horizon (LivePerformanceTracker) — secondary, confirm-don't-assume

The §12 item also named the live perf-strip eval. But `LivePerformanceTracker` resolves outcomes on a **barrier-hit** model (favourable/adverse barrier ever hit — `FavBar`/`AdvBar`), not a fixed minute-window, so it likely does **not** have the window-too-short problem (it waits for the barrier regardless of resolution). **Implementer: confirm there's no hidden horizon cap** in the eval-cache resolution path that truncates 3-min trades early; if there is, scale it by resolution too. If it's pure barrier-hit, leave it — note that in the spec-back.

## 6. Safety — auto-tweaker is unaffected

`FailureRateMatrix.Compute` has two callers: `AnalysisRunner` (offline report) and `AutoTweakerCore` (the live tweaker). The tweaker is **NY×1-population-filtered** (Phase-2a), so it only ever feeds `Compute` a 1-min set → `HoldWindowsForResolution(1)` = `{5,10,15}` = **byte-unchanged**. The 3-min window change is invisible to the tweaker. Confirm with a diff: `tools/AutoTweaker/` untouched, and the NY×1 matrix bit-identical.

This is **analysis/display-layer only** — no scoring votes, no thresholds, no vetoes, no `settings.json` change (the windows live in `AnalysisConstants`, a code constant, not config — though the implementer may consider lifting them to settings as an optional bonus).

## 7. Acceptance

- Builds 0/0 (solution + AutoTweaker + OrderCheck); harness unregressed.
- **NY×1 byte-identical** — report's NY tables + the tweaker's NY×1 matrix unchanged (windows still `{5,10,15}`).
- Regenerate the per-`(session×resolution)` report on the current ~3,800-row book: the 3-min Asia/London failure rates should **plateau within `{15,30,45}`** (ASIA in particular should stop declining), confirming the windows now fit the trades. If ASIA still declines at 45m, widen the multiplier and document.
- Spec-back records the LivePerformanceTracker finding (§5: barrier-based ⇒ no change, or horizon-cap ⇒ scaled).

## 8. Data gate — MET

≥2–3 weekday 3-min sessions per bucket: have **Thu 06-18 + Fri 06-19 + Mon 06-22** (the same book that closed the B ROC re-baseline). Not blocked — this is simply the next analysis-layer item, deferred behind the B re-baseline and the WebSocket G1 gate work.
