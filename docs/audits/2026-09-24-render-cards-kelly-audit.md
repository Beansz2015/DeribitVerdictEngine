# 2026-09-24 — Hostile audit: MainForm_Render_Cards.vb / MainForm_PlaintextSnapshot.vb

Scope requested: `UI/MainForm_Render_Cards.vb` (3,684 lines), `UI/MainForm_PlaintextSnapshot.vb`
(506 lines), `UI/Controls/*.vb`. Also read for context (not in scope, but load-bearing to the
findings below): `Core/ScoringEngine_Types.vb`, `Core/IndicatorResults.vb`, `Core/SignalEmitter.vb`
(`SideLevels`, `ComputeSideLevels`, `ComputeStructuralSideLevels`), `Core/ScoringEngine_Kelly.vb`
(`CalcKellySizing`), `settings.json` (`scoring.atr_*_multiplier`, `kelly.*`).

## Files in scope NOT individually read

`UI/MainForm_Render_Cards.vb` and `UI/MainForm_PlaintextSnapshot.vb` were read in full.
The following `UI/Controls/*.vb` files were **not** individually reviewed — only their usage
sites inside `MainForm_Render_Cards.vb` were examined (constructor calls, property sets).
`ScoreArcGauge.vb` and `VolumeHistogramMini.vb` were read in full and are covered below;
these were not:

- `UI/Controls/OiCvdBadge.vb`
- `UI/Controls/SectionGroup.vb`
- `UI/Controls/ContextBadge.vb`
- `UI/Controls/FlatButton.vb`
- `UI/Controls/Pill.vb`
- `UI/Controls/AnalysisReportButton.vb`
- `UI/Controls/RoundedCardPanel.vb`
- `UI/Controls/TapeStripLabel.vb`
- `UI/Controls/RegimeAnchorWarn.vb`
- `UI/Controls/LinkRow.vb`
- `UI/Controls/Helpers/PaintHelpers.vb`
- `UI/Controls/ChipNumeric.vb`
- `UI/Controls/MiniMeter.vb`
- `UI/Controls/MtfRow.vb`

---

--- LOGIC TRACE ---

Run conditions: −1.5% flush, `r.ATR = 120`, `r.AbsorptionSignal/Level/Ratio/AggrUsd = "NONE"/Nothing`
(no episode data), `r.VPFRBucketVolumes` populated (Length > 0) but every element `0.0`,
`v.Verdict = "STRONG LONG"`, and the ATR ENTRY LEVELS row for LONG carries a real R:R of `0.39`
— which only happens when `cfg.Scoring.StructuralLevels.Enabled = True` and `ComputeSideLevels`
placed a tight structural target (nearest HVN/POC just overhead, post-flush) against a wider
clamped stop.

Trace through `BuildPlaintextSnapshot` (`UI/MainForm_PlaintextSnapshot.vb:35-149`):

1. `atrStop = r.ATR * cfg.Scoring.AtrStopMultiplier` = `120 * 1.6 = 192`. This is computed
   **before** `ComputeSideLevels` is ever called (line 48, vs. line 160).
2. `ScoringEngine.CalcKellySizing(v, atrStop, r.CurrentPrice, cfg)` runs at line 149, still
   before the structural arbitration result exists in this function's scope. Inside
   `CalcKellySizing` (`Core/ScoringEngine_Kelly.vb:78`):
   `b = cfg.Scoring.AtrTargetMultiplier / cfg.Scoring.AtrStopMultiplier = 1.75 / 1.6 = 1.09`.
   With confidence HIGH (typical for STRONG), `p = 0.65`, `f* = (1.09×0.65 − 0.35)/1.09 ≈ 0.33`
   → **positive edge**, `f_half ≈ 0.165`, capped to `0.05` by `MaxRiskFraction`,
   `KellyRiskUsd = $50`. `riskPerContractUsd` is computed from `stopDistanceUsd = atrStop = 192`
   (the *fixed-ratio* distance) — not from `lv.StopPx`. At a representative $80k entry that's
   `contractsByRisk = 2083`, clamped by `maxContractsByLeverage = floor(1000×5/10) = 500`.
   **Final: 500 contracts, $5,000 notional, 5.0× leverage, rendered as a normal
   (uncapped-looking) KELLY SIZING block.**
3. Only afterward (line 160) does `ComputeSideLevels(v, r, cfg, isLong:=True)` run and produce
   the *real* placed geometry — the one that actually yields R:R `0.39` and is what
   `AppendPlacedAtrRow` prints in the "Long:" line the trader reads two inches below the
   Kelly block.
4. Absorption fields are never read anywhere in this file or in `BindCard*` — `grep` across
   `UI/MainForm_Render_Cards.vb` and `UI/MainForm_PlaintextSnapshot.vb` returns zero hits for
   `Absorption`/`AggrVel`. They simply don't appear. No crash, no signal either way.
5. `BindCardVolumeProfile` (`UI/MainForm_Render_Cards.vb:1788`): `VPFRBucketVolumes.Max() = 0`
   → `maxVol` forced to `1.0` (defensive) → every normalized bucket is `0`. The histogram
   control still draws: `VolumeHistogramMini.OnPaint` forces `If w < 1 Then w = 1`, so every
   bar renders as a visible 1px-wide stub. Meanwhile `AddLevelRow` for VAH/POC/VAL/HVN/LVN
   (all presumably `0` from the same degenerate `CalcVPFRLite` run) render `"—"` because of
   the `value > 0` guard. The card ends up showing a fully-drawn (if flat) bar strip stacked
   against a column of em-dashes for the actual price levels — internally contradictory, and
   it happens on exactly the run where the trader most wants to know whether volume-at-price
   data is trustworthy.
6. Both `BindCardKelly` and `BuildPlaintextSnapshot`'s KELLY block render
   `v.KellyContracts = 500`, `v.KellyRiskUsd = $50`, no `[CAPPED]`-style warning tying it to
   the ATR ENTRY LEVELS row's real R:R 0.39. `SignalEmitter.BuildOk`
   (`Core/SignalEmitter.vb:174-178`) serializes `kelly.contracts = 500` and
   `kelly.risk_usd = 50` verbatim into `verdict_signal.json`, which is the file the
   order-placement app reads.

No exception fires anywhere in this trace. That is itself part of the problem: the Kelly
disconnect below is silent by design, not a crash.

---

## SEVERITY: CRITICAL

**LOCATION:** `Core/ScoringEngine_Kelly.vb:78` (`b = cfg.Scoring.AtrTargetMultiplier / cfg.Scoring.AtrStopMultiplier`)
and `UI/MainForm_PlaintextSnapshot.vb:45-49,149` (`CalcKellySizing` called with `atrStop`,
before `ComputeSideLevels` runs)

**DOWNSTREAM IMPACT:** `v.KellyContracts`/`v.KellyRiskUsd`/`v.KellyLevCapped` feed both display
surfaces (`BindCardKelly`, `BuildPlaintextSnapshot`) *and* `SignalEmitter.BuildOk`'s `kelly`
block, which is consumed by DeribitOrderPlacementApp per this repo's own architecture note.
"Display-only, zero scoring impact" is true for the *verdict*; it is false for *sizing* the
moment a downstream consumer reads `kelly.contracts`.

**FAILURE SCENARIO:** Traced above with real `settings.json` values
(`atr_stop_multiplier=1.6`, `atr_target_multiplier=1.75`, `est_prob_floor=0.45`,
`est_prob_scale=0.20`, `max_risk_fraction=0.05`, `max_leverage=5.0`, `account_size_usd=1000`,
`contract_face_usd=10`). Kelly computes `b=1.09` (assumed) → positive edge →
**500 contracts / $5,000 notional / 5.0× leverage**. The trade the engine will actually place,
per `ComputeSideLevels`, has R:R `0.39`. Feeding the true `b=0.39` into the identical formula
gives `f* ≈ −0.25` — no edge, size should be zero. The card and the payload both ship the
500-contract number. Nothing in the codebase — no flag, no `[CAPPED]`-style tag, no
cross-check — ties the two together. Proof: `docs/audits/proofs/render-cards-kelly-audit/`.

**ANALYTICAL CRITIQUE:** This isn't a rounding error, it's a structural mismatch between two
systems that were supposed to converge. `Core/SignalEmitter.vb`'s own header comment brags
that `ComputeSideLevels` is "the ONE definition of ... the effective stop/target the engine
would place for this run", consumed by "the payload levels block ... the CSV ... the snapshot
+ card ATR rows, and ScoringEngine Step 5b." Kelly sizing is conspicuously absent from that
list, and it's the one consumer where getting it wrong has dollar consequences. The
`atr_stop_multiplier`/`atr_target_multiplier` pair is doing double duty as (a) the legacy
fallback geometry and (b) Kelly's permanent, structure-blind payoff assumption — and nothing
in `structural_levels.enabled=true` mode disables assumption (b) even though it disables (a).
Worse, this gets *more* dangerous exactly when structural placement is *most* active and
*most* likely to diverge from the flat ATR ratio — i.e., during volatile,
structurally-interesting moves like the flush in the trace. The fix isn't cosmetic:
`CalcKellySizing` needs the placed `SideLevels` for the dominant side, not a settings ratio,
or the block needs to render a hard warning whenever `|placed R:R − assumed R:R| > threshold`.

---

## SEVERITY: CRITICAL

**LOCATION:** `UI/MainForm_Render_Cards.vb` — file-wide (one `Try/Catch` in 3,684 lines, at
`RenderSkippedDashboard` lines 368-395, which guards only `SuspendLayout`/`Visible`/
`BringToFront` calls, not data binding)

**DOWNSTREAM IMPACT:** Per the given architecture, card binds run inside `RunAnalysisAsync`
*before* the bridge payload emits. Any unhandled exception anywhere in
`BindCardScore`/`BindCardVerdict`/`BindCardAtrLevels`/`BindAtrRow`/`BindCardStructural`/
`BindCardKelly`/`BindCardVolumeProfile`/`BindCardOiCvdCross`/`BindCardIndicatorDetails`/
`BindCardSignalBreakdown` (and ~25 private `Build*`/`Add*` helpers they call) propagates out,
the modal `MessageBox` blocks the UI thread, and the signal file is never written for that
cycle — a live-trading system stalls on what is, by construction, a rendering concern.

**FAILURE SCENARIO:** No local guard exists against NaN/Infinity propagating from upstream
indicator math into `String.Format("{0:F1}", ...)` (.NET tolerates this — prints `NaN`,
doesn't throw), but plenty of paths are one refactor away from a real throw: e.g.
`_gridRoot.RowStyles(_heroRowIndex)` (`BindCardVerdict`, line ~1023) indexes a `RowStyle`
collection with only a `>= 0` lower-bound check, no upper-bound check against
`RowStyles.Count` — a future change to the VERDICT card's row count that forgets to keep
`_heroRowIndex` in sync throws `ArgumentOutOfRangeException` from inside `BindCardVerdict`,
which every single run calls unconditionally.

**ANALYTICAL CRITIQUE:** The comment at the top of the file even says the card grid is "the
primary display surface," and the trade bridge is threaded through it by construction
(`BuildPlaintextSnapshot` runs first specifically so `BindCardKelly` gets populated
`v.Kelly*`). That ordering choice quietly made a 3,684-line WinForms rendering file a hard
dependency of the order-signal pipeline, with zero exception isolation between them. A single
`Try/Catch` wrapping the whole `BindCard*` sequence in `RunAnalysisAsync` (log-and-continue,
payload still emits with whatever was already computed) would convert "cosmetic bug halts
trading" into "cosmetic bug logs a warning." Right now they're the same failure mode.

---

## SEVERITY: HIGH

**LOCATION:** `Core/IndicatorResults.vb:81-106` (`AggrVel*`, `Absorption*` fields) vs.
`UI/MainForm_Render_Cards.vb` and `UI/MainForm_PlaintextSnapshot.vb` (no references to either
field family)

**DOWNSTREAM IMPACT:** Book absorption and aggressor-velocity/tape-burst data — the two
indicators most directly relevant to "is this flush being defended or not" — are computed by
the engine, documented in-repo as "Display/CSV-only," and then rendered nowhere on either
primary surface. The trader making a discretionary call on a STRONG LONG during a −1.5% flush
has no way to see this data without opening the CSV mid-session.

**FAILURE SCENARIO:** During the traced flush, absorption is empty (`Nothing`) — meaning no
episode was detected/available this run. That state is indistinguishable, on both display
surfaces, from "the engine checked and found strong absorption but it's just not shown" —
because neither state is shown. A trader who *assumes* the card surface is complete (a
reasonable assumption for a "primary display surface") has no way to know this category of
evidence exists, let alone that it's silent this run.

**ANALYTICAL CRITIQUE:** This is the flip side of the Kelly finding: that one is a case of
numbers moving between surfaces incorrectly; this is a case of numbers not moving to a surface
at all despite in-repo comments implying they should ("Display/CSV-only" strongly implies
"Display" is a place this happens). The display-string parity rule enforced elsewhere in this
codebase (`BuildPlaintextSnapshot` ↔ `MainForm_Render_Cards.vb`) has no teeth here because
there's no line to diff against — an entire indicator family isn't part of the parity
discipline because it was never wired into either renderer to begin with.

---

## SEVERITY: MEDIUM

**LOCATION:** `UI/MainForm_Render_Cards.vb:1873-1948` (`BindCardVolumeProfile`) +
`UI/Controls/VolumeHistogramMini.vb:117-119` (`If w < 1 Then w = 1`)

**DOWNSTREAM IMPACT:** A degenerate (all-zero) VPFR bucket array renders as a fully-populated,
visually "normal" bar histogram (every bar forced to a minimum 1px stub) at the same time the
price-level rows for VAH/POC/VAL/HVN/LVN collapse to `"—"` for the same underlying data. The
card is internally inconsistent about whether it has data.

**FAILURE SCENARIO:** Traced above — the exact scenario specified (NaN-free, zero histogram).
No exception; a misleading render.

**ANALYTICAL CRITIQUE:** `VolumeHistogramMini.Buckets`'s setter (`v.Length = 0` guard) silently
*keeps the old bucket array* on an empty push but has no equivalent defense against a
*populated-but-all-zero* array, which is exactly the shape this scenario produces. The
`maxVol <= 0 Then maxVol = 1.0` defensive line in the caller exists to prevent a
divide-by-zero, not to make the resulting all-zero-normalized chart meaningful — but its side
effect is a chart that *looks* like real, if boring, data. A `"no reliable volume data this
run"` state distinct from `"flat volume distribution"` doesn't exist anywhere in this
rendering path.

---

## SEVERITY: LOW/MEDIUM

**LOCATION:** `UI/MainForm_Render_Cards.vb:63-69` (`FormatRR`) and its callers
`AppendPlacedAtrRow` (snapshot) / `BindAtrRow` (card), both of which compute risk/reward via
`Math.Abs(...)`

**DOWNSTREAM IMPACT:** A geometry bug elsewhere that places a stop or target on the wrong side
of entry (e.g., a `swingStop` on the wrong side surviving a future regression in
`ComputeStructuralSideLevels`) would render as an ordinary, plausible-looking R:R number
instead of surfacing as the invalid state it represents.

**FAILURE SCENARIO:** If `reward` (i.e., `target − entry` for a long) were ever negative —
target behind entry — `FormatRR` computes `ratio < 0` → `ratio < 0.1` is `True` → renders
`"1:< 0.1"`, the *same string* used for a merely-very-thin-but-valid edge. Both
`AppendPlacedAtrRow`'s `risk = Math.Abs(lv.Entry - lv.StopPx)` and `BindAtrRow`'s equivalent
`Math.Abs` calls go further and erase the sign entirely before the ratio is even formed, so a
stop on the wrong side of price prints a normal-looking positive risk number.

**ANALYTICAL CRITIQUE:** `Math.Abs()` on a risk/reward distance is a defensive habit that
trades a loud, correct failure (a visibly negative or nonsensical number that would make a
trader immediately distrust the row) for a quiet, wrong one (a plausible number that invites
trust). Given how much weight `docs/DeribitIndicatorProject.md`'s own rules place on
"conservative false-positive tolerance" and "say NO TRADE rather than a weak signal," this
pattern is the opposite instinct applied to the rendering layer.
