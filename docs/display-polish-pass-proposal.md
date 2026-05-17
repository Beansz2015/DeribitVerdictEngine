# Display Polish Pass + Output Dump Audit Trail — Proposal

**Status:** ✅ IMPLEMENTED — 2026-05-17
**Settings.json:** unchanged (no new keys; hardcoded thresholds keyed off ATR)
**Spec dependency:** None. Independent display-side changes.

---

## Motivation

Output-dump audit on 2026-05-17 (2096 runs spanning 50 hours) surfaced 13 findings. 1 ruled out, 3 confirmed by-design (no code change), 1 by-design but under-documented (Finding 5, MTF Reason format), and 8 actionable display issues. This spec bundles the 8 actionable items into a single commit since they're all small, all display-side, and have no behaviour-side risk.

Findings folded in:
- **F1** Live perf-strip absent from output dump (audit-trail gap)
- **F2** `R:R 1:0.0` rendering when reward rounds to zero at 1dp
- **F4** Negative-zero funding rate (`-0.0000%`)
- **F6** `CAPPED @` label fires for sub-tick adjustments (visual noise)
- **F8** `Lean: 1 contracts` pluralisation
- **F9** OI delta precision mismatch between OPEN INTEREST and SIGNAL BREAKDOWN sections
- **F11** `NO TRADE [WEAK X] + CONTEXT: CONFIRMED` is contradictory
- **F12** Same explanatory note used for missing target vs missing stop in structural rows

---

## Design changes

### 1. Output dump captures perf-strip (F1)

**Problem:** `AnalysisOutputDump.Append(timestamp, renderedText, ...)` receives `txtOutput.Text` (the RTF content). The six perf-strip labels (`lblPerfWeek` etc.) plus the `[B]/[T]` mode indicator are separate WinForms `Label` controls outside the RTF, so they aren't captured. Audit trail is incomplete — you can't reconstruct what the user saw on the strip at the time of any historical run.

**Fix:** Prepend a one-line "PERF STRIP" header to the dump block before the existing RTF content.

Format:
```
## Run 2026-05-17 02:15:33 UTC+8
PERF STRIP [B] Cur.Wk: 13% | 3d: 13% | Cur.Day: 19% | Asia: --% | London: --% | NY: 19%

=========================================================
  VERDICT:    ...
  ...
```

The mode indicator (`[B]` or `[T]`) reflects the current `_metricMode` value. The six labels mirror `lblPerfWeek`/`lblPerf3d`/`lblPerfDay`/`lblPerfAsia`/`lblPerfLondon`/`lblPerfNy` text at the moment of append.

**Implementation:**

- Augment `AnalysisOutputDump.Append` signature to take an optional `perfStripLine As String = Nothing`. If non-empty, write it as the first line after `## Run ...`.
- In `MainForm_Analysis.RunAnalysisAsync` (the call site), build the perf-strip line from the six label `.Text` values + `_metricMode`. Pass it through.

Backward-compat: existing dump files don't have the PERF STRIP line. Audit tools should treat its absence as "pre-v30" and not error.

### 2. R:R precision — 2dp or `< 0.1` literal (F2)

**Problem:** `R:R 1:0.0` is rendered when reward/risk rounds to zero at 1dp. Looks like divide-by-zero or missing target, but a target IS displayed.

**Fix:** Two-tier precision. If ratio ≥ 0.1, render to 1dp as today. If ratio < 0.1 (but > 0), render as `< 0.1`. If ratio == 0 (target missing), render `0.0` as today (this is the genuine zero case and looks correct alongside an empty Target field).

```vb
Dim ratio As Double = reward / risk
Dim rrText As String
If ratio = 0.0 Then
    rrText = "0.0"
ElseIf ratio < 0.1 Then
    rrText = "< 0.1"
Else
    rrText = ratio.ToString("F1", CultureInfo.InvariantCulture)
End If
```

Touchpoints: structural row R:R rendering in `MainForm_Render_Header.RenderOutputHeader`.

### 3. Negative-zero funding clamp (F4)

**Problem:** `Rate: -0.0000% | NEUTRAL` appears in 254 runs. Funding values like `-1e-7` retain a negative sign when formatted to 4dp.

**Fix:** Clamp `Math.Abs(rate) < 1e-8` to exactly 0.0 before formatting. Apply in both:
- FUNDING section header row (`MainForm_Render_Sections`)
- SIGNAL BREAKDOWN's `Funding (info)` row

Single helper function `ClampFundingForDisplay(rate As Double) As Double` to avoid drift between the two sites.

### 4. CAPPED @ suppression for sub-threshold caps (F6)

**Problem:** 1150 of 2096 runs render `CAPPED @ ... [reason]` in amber bold. Many caps are sub-tick (0.2 pt, 1.0 pt — < 0.001% on an $80K asset). Visual noise — the amber-bold treatment overweights material reductions.

**Fix:** Suppress the cap label when the adjustment is below a noise threshold. Render the adjusted target as a regular value (matching the raw target's styling) without the amber-bold `[CAPPED @ ...]` annotation.

Noise threshold formula:
```vb
Dim noiseThreshold As Double = Math.Max(0.5, r.ATR * 0.02)
Dim adjustment As Double = Math.Abs(rawLongTarget - v.AdjustedLongTarget)
If adjustment < noiseThreshold Then
    ' Render adjusted target as normal value, no CAPPED label
Else
    ' Existing amber-bold CAPPED treatment
End If
```

`0.5` is a hard floor (~$0.50 — half a tick at current Deribit BTC tick size). `ATR × 0.02` scales with volatility — 2% of ATR is below the noise floor of any real structural cap. Both apply; whichever is larger wins. At typical ATR=30, threshold = `max(0.5, 0.6) = 0.6`.

The TargetCapReason field on `VerdictResult` is still populated for CSV logging — only the visual label is suppressed.

### 5. Lean pluralisation (F8)

**Problem:** `Lean: 1 contracts (not a trade signal)` appears in the Kelly block.

**Fix:** Pluralise based on count.
```vb
Dim contractsLabel As String = If(leanContracts = 1, "contract", "contracts")
```

Apply at the single `Lean:` render site in the Kelly block.

### 6. OI delta precision unification (F9)

**Problem:** Same OI delta rendered to 3dp in OPEN INTEREST section (`d15m: -0.005%`) and 2dp in SIGNAL BREAKDOWN's `OI Delta` row (`15m:-0.00%`). After the v20 threshold drop to 0.2%, deltas in the 0.001–0.01% range matter — they should be visible in both places.

**Fix:** Standardise on **3dp** in both sites. Touchpoints in `MainForm_Render_Sections`.

### 7. CONFIRMED → ALIGNED on NO TRADE (F11)

**Problem:** `NO TRADE [WEAK SHORT] + CONTEXT: CONFIRMED` is contradictory — CONFIRMED reads as "directional call confirmed" but the verdict didn't cross threshold.

**Fix:** Rename to `ALIGNED` when verdict is NO TRADE (any sub-threshold case). Other context tags (FLOW_UNCONFIRMED, MOMENTUM_FADING, STRUCTURALLY_WEAK) remain as-is on NO TRADE — only the CONFIRMED case relabels.

In `CalcVerdictContext`:
```vb
' Existing logic returns "CONFIRMED" as fallback.
Dim baseContext As String = ...    ' existing computation
If baseContext = "CONFIRMED" AndAlso verdict.StartsWith("NO TRADE") Then
    Return "ALIGNED"
End If
Return baseContext
```

The check must happen after the existing FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK branches so those still fire on NO TRADE when appropriate.

**Semantic:**
- `CONFIRMED` = "directional call has cross-category support"
- `ALIGNED` = "sub-threshold bias has cross-category support, but score didn't qualify for a verdict"

CSV column `VerdictContext` records the literal value — downstream analytics on the CSV will need to recognise `ALIGNED` as a new value alongside the existing four. Backward-compat: existing CSV rows pre-v30 retain `CONFIRMED` on NO TRADE; only fresh rows use the new tag.

### 8. Per-side missing-target / missing-stop note wording (F12)

**Problem:** Same explanatory note `"(no swing high above entry within lookback)"` used for both:
- Long structural row's MISSING TARGET (correct — no swing high to cap target at)
- Short structural row's MISSING STOP (also caused by no swing high, but the user has to mentally invert)

**Fix:** Per-side wording.

Long row:
- Missing Target: `(target unset: no swing high above entry within lookback)`
- Missing Stop: `(stop unset: no swing low below entry within lookback)`

Short row (mirrored):
- Missing Target: `(target unset: no swing low below entry within lookback)`
- Missing Stop: `(stop unset: no swing high above entry within lookback)`

The "target unset" / "stop unset" prefix removes the mental inversion.

---

## Implementation steps

1. **`AnalysisOutputDump.Append` signature** — add optional `perfStripLine As String = Nothing`, render before RTF if non-empty.
2. **`MainForm_Analysis.RunAnalysisAsync`** — build the perf-strip line from `lblPerf*.Text` + `_metricMode`, pass to Append.
3. **`MainForm_Render_Header.RenderOutputHeader`**:
   - R:R formatter with `< 0.1` literal handling
   - Structural row notes — per-side wording
   - Kelly Lean pluralisation
   - ATR target CAPPED suppression on sub-noise adjustments (call helper to compute threshold)
4. **`MainForm_Render_Sections`**:
   - FUNDING section row — apply `ClampFundingForDisplay`
   - SIGNAL BREAKDOWN's Funding (info) row — same clamp
   - SIGNAL BREAKDOWN's OI Delta row — 3dp consistent with OPEN INTEREST section
5. **`ScoringEngine_Calculate_Scoring.CalcVerdictContext`** — add NO TRADE special-case relabel CONFIRMED → ALIGNED.
6. **Build clean:** `dotnet build` from root. 0/0.
7. **Manual smoke test:**
   - Run one analysis with `posState=None`, NO TRADE verdict expected, context shows `ALIGNED` (not CONFIRMED)
   - Run one analysis with sub-tick CAPPED — label suppressed, target rendered as normal value
   - Open `analysis_output_dump.md`, confirm first line after `## Run` is the PERF STRIP header
   - Check FUNDING section — no `-0.0000%` if rate is near zero
8. **No settings.json bump.** Hardcoded thresholds (noise = `max(0.5, ATR × 0.02)`); literal "< 0.1" and "ALIGNED" string. No new config keys.

---

## Out of scope

- **F5 (MTF Reason format inconsistency)** — three formats are by design, one per scenario (PASS/BLOCK/no-direction). Already documented in UserManual §1482. Architecture doc will get a one-line clarification but no code change.
- **F3 (STRONG + STRUCTURALLY_WEAK / MOMENTUM_FADING)** — intentional. Context tag exists to surface caveats the score didn't fold in. Will be documented in architecture.md.
- **F7 (HOLD STATUS absent from dump)** — by design. `CalcHoldStatus` guards on `posState ≠ None`. Will be documented in architecture.md.
- **F10 (POC tier 3 never fires)** — geometry + the HVN-gate. Branch is reachable but rarely a winner. Will be documented in architecture.md as a parked observation; not a code change.
- **Real-time gap detection in `UpdateAsync`** — separate concern from this spec.

---

## Risk

All changes are display-side. No scoring/verdict impact. No CSV schema change (VerdictContext column may contain new value "ALIGNED" on NO TRADE rows post-shipment; existing rows unchanged).

Single-commit, ~3 hours coding work, no schema migration.

---

## Change_log entry

If a settings.json version bump is wanted for traceability even without key changes:
> v30 (2026-05-17): display polish pass — output dump now captures perf-strip values, R:R rendering uses "< 0.1" for sub-1dp ratios, sub-noise CAPPED labels suppressed, negative-zero funding clamped, OI delta precision unified at 3dp, NO TRADE + CONFIRMED relabelled to ALIGNED, per-side missing-target/stop wording in structural rows.

If kept at v29: just append a "Display polish pass" note to the recent change_log entries.

**Recommended:** bump to v30 for traceability — even though no keys changed, the dump format change is a meaningful semantic shift worth pinning to a version.
