# Batch summary — SH-1 (split the coverage hour at a capture-state marker)

Per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Companion: [`coverage-split-hour-sh1-spec-back.md`](coverage-split-hour-sh1-spec-back.md). Spec built against: [`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md).

**Status: reviewed and ACCEPTED 2026-08-13, D-3 ruled, four owed items complete, NOT yet committed.** Waiting on the trader to say whether to commit — this session does not commit without being asked.

**D-3 [RULED 2026-08-13, `coverage-split-hour-implementer-brief.md` §5a]:** the residual combine order (no `Defect`, no `Captured` present) is **`UnknownScope` > `ExpectedMissing` > `NotCapturing`** — built as `ExpectedMissing > NotCapturing > UnknownScope`, half right (`ExpectedMissing > NotCapturing` stood); `UnknownScope` moved from the bottom to the top of the residual so an uncharacterisable span is never laundered into a confident `NotCapturing` label, and so the combine no longer silently reverses `ClassifySpan`'s own unknown-before-off precedence. Fixed and re-verified — see below.

---

## What changed

**File:** [`tools/BacktestRunner/CoverageReport.vb`](../tools/BacktestRunner/CoverageReport.vb)

| Function | Change |
|---|---|
| `ClassifyHour` | Now detects markers strictly inside the hour (`hourStartMs < UtcMs <= hourEndMs`), splits into N+1 spans, classifies each independently, and combines worst-of: `Defect` > `Captured` > `UnknownScope` > `ExpectedMissing` > `NotCapturing` (D-2, D-3 — the last three ruled 2026-08-13 after review). No split ⇒ identical single-scope path as before, just routed through the new shared helper. |
| `ClassifySpan` *(new, private)* | The five checks `ClassifyHour` ran inline pre-SH-1, extracted so both the whole-hour and split paths share one implementation. |
| `ClassifyUptimeSpan` *(new, public)* | `ClassifyUptime` generalised to an arbitrary `[spanStartMs, spanEndMsInclusive]` span. `ClassifyUptime` is now the whole-hour special case, unchanged externally. |
| `AccumulateSplitSpanStats` *(new, public)* | Route (b) — a second, full pass over the walked range, run ONLY when at least one split hour exists. Carries `prevTs` continuously across the whole walk (slip 2), never reset at a span boundary. |
| `SpanStartFor` *(new, private)* | Which span a trade's timestamp falls into, given a sorted list of span-start boundaries. |
| `BuildResult` | Detects split hours from the marker list, runs `AccumulateSplitSpanStats` conditionally, threads the result into every `ClassifyHour` call via a new optional 7th parameter. |

`AccumulateHourStats` (the hot path) is **byte-for-byte unchanged** — confirmed by `git diff`, zero `+`/`-` lines inside its body.

**File:** [`verify/ordercheck/Program.vb`](../verify/ordercheck/Program.vb) — 9 fixtures, `A49o`–`A49w`, extending the existing `A49` family (A49TempStore/A49Trade/A49Monday/A49Ms/A49MarkerLine scaffolding reused directly, per the brief's §6 guidance). `A49w` added post-review for D-3.

---

## Fixture results

| Fixture | Case | Result |
|---|---|---|
| `A49o` | Flip ON at :30, clean ON-half store | PASS — **CAPTURED**, not laundered |
| `A49p` | Flip ON at :30, silent ON-half store | PASS — **DEFECT** |
| `A49q` | Flip OFF at :30, defect in the ON (first) half | PASS — **DEFECT**, not laundered |
| `A49r` | Marker exactly at `:00` | PASS — unchanged single-scope path, no split |
| `A49s` | Marker at `:59` | PASS — splits; 50s gap in a ~60s span reads **CAPTURED** (absolute threshold, not scaled) |
| `A49t` | Two markers, three spans | PASS — off/captured/off ⇒ **CAPTURED** (D-2) |
| `A49u` | Gap straddling the marker | PASS — attributed to the ending span (slip 2); flips hour to **DEFECT** |
| `A49v` | Full weekday containing one split hour, via `BuildResult` end-to-end | PASS — **24** `HourResult` rows (D-1) |
| `A49w` | D-3 — first-ever marker landing mid-hour (span0 no marker applies, span1 that marker's own `off`) | PASS — **UnknownScope**, not laundered into `NotCapturing` |

**Mutation proof (§7 handle 1), run live both ways:** temporarily forced `ClassifyHour` back onto the pre-SH-1 single-scope path (`If True Then` in place of the split-detection check), rebuilt, re-ran the harness — `A49o` and `A49p` **FAILED** (both read `NotCapturing`, exactly the pre-fix defect described in the brief §3). Reverted, rebuilt, re-ran — **ALL PASS** again.

**D-3's own mutation proof, run live both ways:** temporarily restored the pre-ruling combine order (`ExpectedMissing > NotCapturing > UnknownScope`), rebuilt, re-ran the harness — `A49w` **FAILED** (read `NotCapturing`, reproducing the exact defect D-3's ruling describes). Reverted to the ruled order (`UnknownScope > ExpectedMissing > NotCapturing`), rebuilt, re-ran — **ALL PASS** again, all 9 split-hour fixtures included.

---

## Gate

```
powershell -File tools/checks/verify-gate.ps1 -Mode prepush
```

- Six project builds: OK (`DeribitVerdictEngine.sln`, `AutoTweaker`, `WhatIfRunner`, `CeilingAudit`, `BacktestRunner`, `OrderCheck`)
- Harness: **ALL PASS**
- Display-parity: OK — no snapshot/card drift (expected; this build touches no render surface)
- Version-bump: OK — "no engine-path change" (`tools/BacktestRunner/` and `verify/ordercheck/` are outside `verify-gate.ps1`'s `$enginePrefixes`)
- **Result: GATE PASSED**

`git diff --stat -- settings.json` — empty. No settings keys, no version bump, per the brief's §4.4.

---

## Scope carried per the brief

- No settings keys, no version bump (§4.4).
- No rendered engine surface touched — this build is Part B/TAPE-STORE-independent; nothing in `MainForm_PlaintextSnapshot.vb` or `MainForm_Render_Cards.vb` changes, so the display-string parity rule needs no action here. Stated per the brief's own instruction to say so rather than leave it silent.
- `ResolveScope`'s contract for other callers untouched — the split is added at the classification site only (`ClassifyHour`), not inside `ResolveScope` itself.
