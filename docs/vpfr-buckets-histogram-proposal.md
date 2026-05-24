# VPFR Bucket Exposure + VolumeHistogramMini Wiring — Proposal

**Status:** PROPOSED
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-24
**Spec target:** `Core/Indicators_Structure.vb` (engine), `Core/IndicatorResults.vb` (DTO), `UI/MainForm_Analysis.vb` (call site), `UI/MainForm_Render_Cards.vb` (VOLUME PROFILE binding)
**Settings.json impact:** none expected (existing `cfg.Indicators.VPFR.NumBuckets` covers bucket count)
**Scoring / engine impact:** **none** — additive display-only fields, computed during the same `CalcVPFRLite` call

---

## 1. Motivation

The UI reskin proposal §4.7.2 mandates a mini horizontal histogram inside the VOLUME PROFILE card:

> Mini histogram (8 bars, POC highlighted amber, current-price line green) | derived from `r.VPFRBuckets` + `r.CurrentPrice`

P3 shipped the control (`UI/Controls/VolumeHistogramMini.vb`) but P4d shipped the VOLUME PROFILE card binding with only the five price-level rows (VAH / HVN↑ / POC / HVN↓ / VAL). The histogram is missing.

Two reasons it wasn't wired:

1. The proposal references `r.VPFRBuckets` as the data source, but **that field doesn't exist on `IndicatorResults`**. Verified at [Core/IndicatorResults.vb:91-102](Core/IndicatorResults.vb:91) — VPFR fields cover POC, VAH/VAL, nearest HVN/LVN walls only.
2. `CalcVPFRLite` at [Core/Indicators_Structure.vb:106-129](Core/Indicators_Structure.vb:106) computes the per-bucket weighted volumes locally in `bucketVol(numBuckets - 1)` and **discards the array** after deriving POC / VAH / VAL / nearest walls.

This spec closes both — expose the bucket array (and the minimal index→price metadata the UI needs to render a price-aware histogram), then wire `VolumeHistogramMini` into `BindCardVolumeProfile`.

---

## 2. Non-goals

- No change to bucket count, decay weighting, value-area expansion algorithm, or any scoring-affecting VPFR behaviour. The new fields are byproducts of the existing computation.
- No change to `cfg.Indicators.VPFR.*` settings. Bucket count stays at the existing default (50). If the histogram looks too dense at 50 bars in the card's width, UI-side downsampling is the right knob — engine output stays raw.
- No CSV column additions. The bucket array is a transient display artifact; logging it would bloat the CSV without analytic value (POC / VAH / VAL are already logged).
- No new control. `VolumeHistogramMini` already exists in `UI/Controls/`.
- No layout change to other VOLUME PROFILE rows — the histogram is appended below the existing price-level stack.

---

## 3. Engine change — expose bucket array from `CalcVPFRLite`

### 3.1 Signature change

Current `CalcVPFRLite` signature ([Core/Indicators_Structure.vb:73-94](Core/Indicators_Structure.vb:73)):

```vb
Public Shared Sub CalcVPFRLite(candles As List(Of Candle),
                                currentPrice As Double,
                                ByRef poc As Double,
                                ByRef hvnNearPoc As Boolean,
                                ByRef signal As String,
                                ByRef vah As Double,
                                ByRef val As Double,
                                ByRef valueAreaSignal As String,
                                ByRef nearestHvnAbove As Double,
                                ByRef nearestHvnBelow As Double,
                                ByRef nearestLvnAbove As Double,
                                ByRef nearestLvnBelow As Double,
                                Optional numBuckets As Integer = 50,
                                Optional hvnVolPct As Double = 0.6,
                                Optional lvnVolPct As Double = 0.2,
                                Optional hvnProximityPct As Double = 0.002,
                                Optional decayBase As Double = 0.985,
                                Optional valueAreaPct As Double = 0.70)
```

Three new `ByRef` outputs appended (after `nearestLvnBelow`, before `numBuckets`):

```vb
                                ByRef bucketVolumes As Double(),
                                ByRef bucketPriceLow As Double,
                                ByRef bucketSize As Double,
```

Semantics:

- `bucketVolumes` — the existing `bucketVol(numBuckets - 1)` local. **Bucket index 0 corresponds to `priceLow` (lowest price); the highest index corresponds to `priceHigh`.** Weighted volumes (decay-applied) in raw USD-equivalent units. The UI normalises per-render.
- `bucketPriceLow` — the existing local `priceLow`. Lets the UI map any price (e.g., `r.CurrentPrice`) to a bucket index without re-deriving from candle history.
- `bucketSize` — the existing local `bucketSize`. Width in $ of each bucket.

Together, `bucketIndex_of(price) = floor((price - bucketPriceLow) / bucketSize)` lets the histogram place the current-price line at the correct vertical position. Same arithmetic the engine already uses internally for `curIdx`.

### 3.2 Implementation

Two small additions to the existing computation:

```vb
' Just before the existing bucketVol() Dim:
bucketPriceLow = priceLow
bucketSize     = (priceHigh - priceLow) / numBuckets

' At the end of the method (or anywhere after bucketVol is populated):
bucketVolumes = bucketVol
```

`bucketVol` is already an array; assigning to a `ByRef` `Double()` parameter passes the reference. No copy needed. The existing computation path (POC scan, VAH/VAL expansion, HVN/LVN walls) is untouched — these new outputs are read-only views of intermediate state.

Edge case: if the early-return guards fire (`candles Is Nothing OrElse candles.Count < 10` or `priceRange <= 0`), set defaults at the top:

```vb
bucketVolumes  = Array.Empty(Of Double)()
bucketPriceLow = 0
bucketSize     = 0
```

UI binding §5.3 checks for the empty array and suppresses the histogram in that case (same shape as the existing "missing data" handling for HVN/LVN rows that gate on `> 0`).

### 3.3 Portability check (Linux CLI port)

`Core/Indicators_Structure.vb` is host-agnostic. Adding `Double()` `ByRef` outputs doesn't touch WinForms, `Control.Invoke`, or `MainForm` coupling. The new fields are passive data — no event surface, no UI dependency. ✓ Safe for §16.2 port.

---

## 4. DTO change — add fields to `IndicatorResults`

Append three properties to `Core/IndicatorResults.vb` in the VPFR-v2 section (after line 102):

```vb
' VPFR-lite histogram buckets (display only, populated alongside VPFR-v2 fields above)
Public Property VPFRBucketVolumes As Double() = Array.Empty(Of Double)()
Public Property VPFRBucketPriceLow As Double
Public Property VPFRBucketSize     As Double
```

Default value matters for the empty-array case (no crashes when an early-return path skips bucket population).

Naming: `VPFRBucketVolumes` rather than the proposal's `VPFRBuckets` for two reasons — explicit unit cue, and avoids ambiguity with `VPFRBucketPriceLow` / `VPFRBucketSize` siblings.

If the spec-author wants to honour the proposal's `VPFRBuckets` name literally, that's a one-word change here; it has no consumer outside this spec.

---

## 5. Call-site change — wire in `MainForm_Analysis.vb`

The existing `CalcVPFRLite` call site (grep `CalcVPFRLite(` in `UI/MainForm_Analysis.vb`) takes ~12 `ByRef` outputs; the new ones append after `nearestLvnBelow`:

```vb
' BEFORE (illustrative — exact arg names vary)
CalcVPFRLite(candles1m, r.CurrentPrice,
             r.VPFRPoc, r.VPFRHVNearPoc, r.VPFRSignal,
             r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal,
             r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow,
             r.VPFRNearestLvnAbove, r.VPFRNearestLvnBelow,
             numBuckets:=cfg.Indicators.VPFR.NumBuckets)

' AFTER
CalcVPFRLite(candles1m, r.CurrentPrice,
             r.VPFRPoc, r.VPFRHVNearPoc, r.VPFRSignal,
             r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal,
             r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow,
             r.VPFRNearestLvnAbove, r.VPFRNearestLvnBelow,
             r.VPFRBucketVolumes, r.VPFRBucketPriceLow, r.VPFRBucketSize,
             numBuckets:=cfg.Indicators.VPFR.NumBuckets)
```

VB.NET allows direct property pass-through to `ByRef` parameters when the property has a `Set`; the auto-property declarations in §4 satisfy this.

---

## 6. UI change — wire `VolumeHistogramMini` into VOLUME PROFILE card

### 6.1 Where it goes

Inside [BindCardVolumeProfile](UI/MainForm_Render_Cards.vb:1084), appended **below** the existing `vaSig` sub-label (line 1128) and **above** the closing `_cardVolumeProfile.Controls.Add(stack)`.

### 6.2 Control surface (from [UI/Controls/VolumeHistogramMini.vb](UI/Controls/VolumeHistogramMini.vb))

```vb
Public Property Buckets               As Single()    ' normalised 0..1, max bucket = 1.0
Public Property PocIndex              As Integer
Public Property CurrentPriceFraction  As Single      ' 0 (bottom) → 1 (top)
Public Property BarColor              As Color
Public Property PocColor              As Color
Public Property PriceLineColor        As Color
```

Renders top-to-bottom, **index 0 at top, last index at bottom.** The engine emits bucket 0 = lowest price (bottom); the UI must **reverse the array** when passing to `Buckets`. Same for `PocIndex` — engine emits POC at the bucket index of `priceLow + (pocIdx + 0.5) * bucketSize`; UI passes `(bucketCount - 1) - pocIdx`.

### 6.3 Binding pseudocode

```vb
' --- VOLUME PROFILE histogram (P3 VolumeHistogramMini) ---
If r.VPFRBucketVolumes IsNot Nothing AndAlso r.VPFRBucketVolumes.Length > 0 _
   AndAlso r.VPFRBucketSize > 0 Then

    Dim n As Integer = r.VPFRBucketVolumes.Length

    ' Normalise to 0..1, reversed so index 0 = highest price (top of histogram).
    Dim maxVol As Double = r.VPFRBucketVolumes.Max()
    If maxVol <= 0 Then maxVol = 1.0     ' defensive — avoids /0 in degenerate cases
    Dim normalised(n - 1) As Single
    For i As Integer = 0 To n - 1
        normalised(i) = CSng(r.VPFRBucketVolumes(n - 1 - i) / maxVol)
    Next

    ' POC index after reversal.
    Dim engineCurIdx As Integer = CInt(Math.Floor((r.VPFRPoc - r.VPFRBucketPriceLow) / r.VPFRBucketSize))
    If engineCurIdx < 0 Then engineCurIdx = 0
    If engineCurIdx >= n Then engineCurIdx = n - 1
    Dim pocReversed As Integer = (n - 1) - engineCurIdx

    ' Current-price fraction. 0 = bucketPriceLow (bottom), 1 = bucketPriceLow + n × bucketSize (top).
    Dim totalRange As Double = r.VPFRBucketSize * n
    Dim cpFrac As Single = 0.5F
    If totalRange > 0 Then
        cpFrac = CSng(Math.Max(0.0, Math.Min(1.0,
                     (r.CurrentPrice - r.VPFRBucketPriceLow) / totalRange)))
    End If

    Dim histo = New VolumeHistogramMini() With {
        .Size = New Size(220, 90),
        .Margin = New Padding(8, 6, 8, 0),
        .Buckets = normalised,
        .PocIndex = pocReversed,
        .CurrentPriceFraction = cpFrac,
        .BarColor = Theme.FG_DIM,
        .PocColor = Theme.ACC_WARN,
        .PriceLineColor = Theme.ACC_STRONG_LONG
    }
    stack.Controls.Add(histo)
End If
```

Pseudocode only — the implementation conversation will adjust the `Size` after measuring the card width and any clipping. **The kickoff will need a build-screenshot-measure gate per P4d spec-back lesson 3.**

### 6.4 Card height impact

VOLUME PROFILE currently shares row 7 with OI × CVD CROSS (proposal §3.6 / §4.7). Adding a 90px histogram + 6px margin = ~96 px lift on the VOLUME PROFILE side. Row 7 is currently sized for the taller of the two cards. Expect to bump row 7 height by ~100 px during the binding work.

If the row already accommodates the OI × CVD card's height (which has a header + badge + 2 MiniMeters), the VOLUME PROFILE side may have headroom — measure during the kickoff before bumping.

### 6.5 Visual density at 50 bars

`VolumeHistogramMini` draws bars stacked vertically, height-fitted to the control. At 90 px control height with 50 bars, each bar is ~1.5 px tall + 1 px gap — visible but tight. Options if it reads poorly:

- **Downsample UI-side** to 8 bars (the control's design target): aggregate groups of `ceil(50/8) = 7` buckets summed; recompute PocIndex against the aggregated array.
- **Tall control:** raise to 140 px so 50 bars get ~2.5 px each (better separation).
- **Reduce engine bucket count:** change `cfg.Indicators.VPFR.NumBuckets` from 50 → 24 (or 20). Changes scoring slightly (coarser POC resolution); needs separate calibration thought. **Not recommended here.**

Recommendation for the kickoff: ship with 50 bars at 90px height first, screenshot, decide whether to downsample. UI-side downsampling is a 10-line follow-up if needed; engine change is not.

---

## 7. Verification

1. `dotnet build` clean — `CalcVPFRLite` signature change ripples to the single call site; new IndicatorResults fields don't break any consumer.
2. Run app. One analysis cycle. VOLUME PROFILE card now shows the 8/50-bar histogram below the price-level stack with:
   - POC bar in amber (`ACC_WARN`)
   - Other bars in dim grey (`FG_DIM`)
   - Horizontal green line at `currentPrice` position
3. **Sanity check the price line:** if `r.CurrentPrice` is above POC, the green line should sit above the amber bar visually. If below, below. Verifies the bucket-reversal math in §6.3.
4. Cross-check against legacy: the existing txtOutput dump (still parked in the verification dump card until P5) doesn't render a histogram, but the POC value in the dump should equal the price at the amber bar's centre. (Visual cross-check, no programmatic comparison.)
5. Auto-run through 5+ cycles. Confirm no perf regression — bucket array passing is reference, not copy, so impact should be unmeasurable.

---

## 8. Phasing

Single commit, suggested subject:

```
feat(ui-reskin): VPFR bucket exposure + VolumeHistogramMini binding

Engine side (Core/Indicators_Structure.vb):
- CalcVPFRLite now exposes the per-bucket weighted volume array,
  bucket price-low edge, and bucket size as ByRef outputs. Computed
  during the existing pipeline; no scoring change.

DTO side (Core/IndicatorResults.vb):
- Add VPFRBucketVolumes / VPFRBucketPriceLow / VPFRBucketSize fields.

UI side (UI/MainForm_Render_Cards.vb):
- BindCardVolumeProfile instantiates VolumeHistogramMini below the
  existing price-level stack. Reverses the engine's low-to-high
  bucket order for top-to-bottom display, maps current price to
  fractional vertical position, and pins POC bar to amber.

Closes the proposal §4.7.2 mini-histogram gap that P4d shipped without
because r.VPFRBuckets didn't exist on IndicatorResults at the time.

No scoring impact. No CSV change. No settings.json change.
```

Estimated scope: ~80-120 LOC across four files.

If the row 7 height needs a bump, that's a single-line follow-up commit similar to the P4d INDICATOR DETAILS / KELLY height patches.

---

## 9. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | `CalcVPFRLite` signature change breaks any caller this spec missed. | Grep at draft time shows a single call site in `UI/MainForm_Analysis.vb`. Build will catch any drift. |
| R2 | Bucket reversal math in §6.3 is wrong (off-by-one or sign error). | Verification step 3 catches this visually — if POC bar position doesn't match the engine's POC value rendered in the price-level stack above, the math is wrong. |
| R3 | 50 bars at ~1.5 px each reads as a noisy block, not a histogram. | §6.5 ships first, decides whether to downsample. If yes, follow-up commit. |
| R4 | Adding a 90 px histogram to a card already in a side-by-side row breaks vertical balance. | Row 7 height bump during the kickoff. Worst case adds ~100 px to the form's total height — well within the 1400 px target ceiling (proposal §3.6). |
| R5 | Empty-array path on early-return causes `Buckets.Max()` to throw. | §6.3 gates on `Length > 0` before invoking. Defensive `If maxVol <= 0 Then maxVol = 1.0` covers the all-zero case. |
| R6 | Future spec adds a real-time WebSocket update path; the per-render bucket exposure becomes a hot-path allocation. | `bucketVol` is already allocated per `CalcVPFRLite` call. Exposing via `ByRef` is a reference assignment — zero new allocation. Pre-existing allocation cost is unchanged. |

---

## 10. Approval gate

User reviews and either:
- Approves wholesale → kickoff drafted with the build-screenshot-measure gate baked in.
- Approves with revisions (e.g., engine-side downsample to 8 buckets; or rename `VPFRBucketVolumes` → `VPFRBuckets`; or pre-aggregate value-area into the array for a coloured value-area band) → spec updates first.
- Defers to post-P5 → no harm; the histogram has been missing since P4d ships, one more phase doesn't change the trader's primary workflow.

Recommended model for the implementation conversation: **Opus 4.7 Medium.** Engine touch is shallow (three lines + signature change); UI math is the only non-trivial part (§6.3 reversal arithmetic).

---

## 11. Out of scope (separately specced if pursued later)

- **Value-area visual band on the histogram** — colour VAH..VAL buckets a third colour to surface the value area on the histogram body. Aesthetic improvement, not in the original proposal.
- **Click-to-anchor:** clicking a bucket pins a horizontal price reference. Wasn't in the proposal; would require event surface on `VolumeHistogramMini`.
- **Engine-side bucket count autoscale** — adapt `numBuckets` to candle history range. Currently fixed at 50 from cfg; works fine.
- **Sparkline-style histogram on the perf strip.** Already out of scope per proposal §11.

---

**End of proposal.**
