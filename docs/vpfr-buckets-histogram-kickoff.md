# VPFR Bucket Exposure + VolumeHistogramMini — Implementation Kickoff

**Phase:** Spec B — closes the §4.7.2 mini-histogram gap that P4d shipped without
**Spec source:** `docs/vpfr-buckets-histogram-proposal.md`
**Predecessor spec-back:** `docs/p3-maintenance-pass-spec-back.md` — §7 lessons folded in
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-24
**Recommended model:** **Opus 4.7 Medium.** Engine touch is shallow (three lines + signature change); UI math is the only non-trivial part. Single commit, ~80-120 LOC across four files.

---

## 0. What this phase is (and isn't)

**Is:** the wiring pass that fills in the missing mini volume-profile histogram inside the VOLUME PROFILE card. Engine exposes the per-bucket weighted volumes it already computes; UI binds them through the existing `VolumeHistogramMini` P3 control.

**Isn't:**
- ❌ Any scoring or indicator behaviour change. The new fields are byproducts of the existing `CalcVPFRLite` pipeline — POC / VAH / VAL / HVN walls remain bit-identical.
- ❌ Any settings.json change. `cfg.Indicators.VPFR.NumBuckets` (existing) drives bucket count.
- ❌ Any CSV / `analysis_log.csv` change. Histogram buckets are display-only; do not log.
- ❌ Any P3 control modification. `VolumeHistogramMini` is consumed exactly as built — no paint tweaks, no API changes. **The §4 paint carve-out is not invoked here.**
- ❌ P4f (ANALYSIS SKIPPED) or P5 (`txtOutput` deletion) work.
- ❌ Any change to `MainForm.Designer.vb`.

---

## 1. What you inherit

### 1.1 Engine surface — `CalcVPFRLite` at [Core/Indicators_Structure.vb:73](Core/Indicators_Structure.vb:73)

Existing signature (verified at draft time — see proposal §3.1 for the full block):

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

Internal locals you'll reference: `priceHigh`, `priceLow`, `priceRange`, `bucketSize`, `bucketVol(numBuckets - 1)`, `pocIdx`. All declared at [Indicators_Structure.vb:101-129](Core/Indicators_Structure.vb:101).

### 1.2 Call site — single, at [UI/MainForm_Analysis.vb:409](UI/MainForm_Analysis.vb:409)

```vb
Dim vpfrPoc       As Double  = 0
Dim vpfrHVNearPoc As Boolean = False
Dim vpfrSignal    As String  = "NEUTRAL"
IndicatorEngine.CalcVPFRLite(candles1m, r.CurrentPrice,
                             vpfrPoc, vpfrHVNearPoc, vpfrSignal,
                             r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal,
                             r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow,
                             r.VPFRNearestLvnAbove, r.VPFRNearestLvnBelow,
                             numBuckets:=cfg.Indicators.VPFR.NumBuckets,
                             ...)
r.VPFRPoc       = vpfrPoc
r.VPFRHVNearPoc = vpfrHVNearPoc
r.VPFRSignal    = vpfrSignal
```

The pattern is: three locals for the non-numeric/boolean outputs (because `ByRef` can't bind to auto-property setters directly in all VB.NET overload-resolution paths), copied into `r` after the call. **Numeric properties (`r.VPFRVah`, etc.) bind to `ByRef` parameters directly.** Follow the same shape for your three new outputs.

### 1.3 DTO surface — VPFR section in [Core/IndicatorResults.vb:91-102](Core/IndicatorResults.vb:91)

Existing exposed fields:

```vb
Public Property VPFRPoc             As Double
Public Property VPFRHVNearPoc       As Boolean
Public Property VPFRSignal          As String
Public Property VPFRVah             As Double
Public Property VPFRVal             As Double
Public Property VPFRValueAreaSignal As String
Public Property VPFRNearestHvnAbove As Double
Public Property VPFRNearestHvnBelow As Double
Public Property VPFRNearestLvnAbove As Double
Public Property VPFRNearestLvnBelow As Double
```

Your three new fields land after `VPFRNearestLvnBelow`. See §2.2 for exact names and defaults.

### 1.4 VOLUME PROFILE card

- Container: `_cardVolumeProfile As RoundedCardPanel`, declared in [MainForm_Layout.vb:102](UI/MainForm_Layout.vb:102), instantiated [Layout.vb:444](UI/MainForm_Layout.vb:444).
- Position: row 7, column 1 of `row7` (TableLayoutPanel, 50/50 split with `_cardOiCvdCross` in column 0). At form width 1100 px, the card client width is roughly **520 px** after card padding.
- **Current row height: 210 px** (`AddRow(row7, 210)` at [Layout.vb:454](UI/MainForm_Layout.vb:454)). Comment there documents the P4d 140 → 210 bump. **Adding the 90 px histogram + 6 px margin requires a ~100 px bump → ~310 px.** Verify per §3.4.
- Binding method: `BindCardVolumeProfile(r As IndicatorResults)` at [MainForm_Render_Cards.vb:1084](UI/MainForm_Render_Cards.vb:1084). Builds a top-down `FlowLayoutPanel` (`stack`) with `BuildPlainSectionHeader("VOLUME PROFILE")` → seven `AddLevelRow(stack, …)` calls (VAH / HVN↑ / LVN↑ / POC / LVN↓ / HVN↓ / VAL) → two `BuildSubLabel(...)` rows (VPFR signal, value-area signal). Append the histogram **below** the value-area sub-label, **above** the closing `_cardVolumeProfile.Controls.Add(stack)`.

### 1.5 P3 control — `VolumeHistogramMini`

File: [UI/Controls/VolumeHistogramMini.vb](UI/Controls/VolumeHistogramMini.vb). Public surface (verified at draft):

```vb
Public Property Buckets               As Single()    ' normalised 0..1, max bucket = 1.0
Public Property PocIndex              As Integer
Public Property CurrentPriceFraction  As Single      ' 0 (bottom) → 1 (top)
Public Property BarColor              As Color
Public Property PocColor              As Color
Public Property PriceLineColor        As Color
```

**Render order: index 0 at top, last index at bottom.** The engine emits bucket 0 = lowest price (bottom of histogram). UI must reverse both the array and the POC index. See §2.4.

### 1.6 Existing helpers in `MainForm_Render_Cards.vb` (relevant to this work)

Per `grep "Private (Function|Sub|Shared)"`:

- `BuildPlainSectionHeader(text)` — used by VOLUME PROFILE card section header. Don't add another header.
- `AddLevelRow(parent, label, value, colour, bold, suffix)` — the existing seven price-level rows. Histogram appends as a sibling, not via this helper.
- `BuildSubLabel(text, colour)` — used for the two sub-labels. Don't reuse for the histogram (histogram is a `VolumeHistogramMini` control, not a `Label`).

You will **not** need to add any new helpers in `MainForm_Render_Cards.vb`. The histogram is a direct control instantiation inline in `BindCardVolumeProfile`.

---

## 2. Implementation surface

### 2.1 Engine change — `CalcVPFRLite` signature

Append three `ByRef` outputs after `nearestLvnBelow`, before the `Optional` parameters:

```vb
ByRef bucketVolumes As Double(),
ByRef bucketPriceLow As Double,
ByRef bucketSize As Double,
```

Inside the method body, two additions:

1. **At the top, in the early-return guards** (lines ~95-104 — `If candles Is Nothing OrElse candles.Count < 10 Then Return` and `If priceRange <= 0 Then Return`): seed defaults so callers don't crash on `Buckets.Max()`:

```vb
bucketVolumes  = Array.Empty(Of Double)()
bucketPriceLow = 0
bucketSize     = 0
```

Place these alongside the existing `poc = 0`, `hvnNearPoc = False` etc. seeding at lines 95-98 — same pattern.

2. **Inside the body, where `bucketSize` and `bucketVol` are already declared** (lines ~106-107):

```vb
Dim bucketSize As Double = priceRange / numBuckets    ' existing line
bucketPriceLow = priceLow                              ' NEW — assign output
bucketSize     = priceRange / numBuckets               ' DUPLICATE? see note below
Dim bucketVol(numBuckets - 1) As Double                ' existing line
```

Wait — the local `Dim bucketSize` shadows the `ByRef bucketSize` parameter. **You'll need to remove the `Dim` and rename, OR keep the local and copy to the output.** Cleanest path: rename the parameter to `bucketSizeOut` (and `bucketPriceLowOut`, `bucketVolumesOut`) to avoid shadowing:

```vb
' Signature
ByRef bucketVolumesOut As Double(),
ByRef bucketPriceLowOut As Double,
ByRef bucketSizeOut As Double,

' Body — early-return seeds
bucketVolumesOut  = Array.Empty(Of Double)()
bucketPriceLowOut = 0
bucketSizeOut     = 0

' After existing local Dim bucketVol(...), at the end of the function:
bucketVolumesOut  = bucketVol
bucketPriceLowOut = priceLow
bucketSizeOut     = bucketSize    ' the existing local
```

Or whatever naming convention you prefer — the point is **don't shadow the existing local `bucketSize`** with a `ByRef` parameter of the same name. Compiler will warn but the assignment will silently target the wrong scope.

### 2.2 DTO change — three new properties on `IndicatorResults`

Append after line 102 of `Core/IndicatorResults.vb`:

```vb
' VPFR-lite histogram buckets (display only, populated alongside VPFR-v2 fields above)
Public Property VPFRBucketVolumes As Double() = Array.Empty(Of Double)()
Public Property VPFRBucketPriceLow As Double
Public Property VPFRBucketSize     As Double
```

**Default `Array.Empty(Of Double)()` matters** — early-return guards in `CalcVPFRLite` may leave the field at its default if the candle history is too short. UI gates on `Length > 0` in §2.4 to suppress the histogram in that case.

### 2.3 Call-site change — single edit in `MainForm_Analysis.vb`

At [line 409-419](UI/MainForm_Analysis.vb:409), extend the `CalcVPFRLite` argument list and add the three field-copy lines after the call. Numeric/array outputs can pass-through to the `Double` / `Double()` auto-properties directly via `ByRef`, but follow the existing pattern of using locals for clarity if the call site looks cleaner that way:

```vb
Dim vpfrPoc          As Double  = 0
Dim vpfrHVNearPoc    As Boolean = False
Dim vpfrSignal       As String  = "NEUTRAL"
Dim vpfrBucketVols() As Double  = Array.Empty(Of Double)()
Dim vpfrBucketLow    As Double  = 0
Dim vpfrBucketSize   As Double  = 0
IndicatorEngine.CalcVPFRLite(candles1m, r.CurrentPrice,
                             vpfrPoc, vpfrHVNearPoc, vpfrSignal,
                             r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal,
                             r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow,
                             r.VPFRNearestLvnAbove, r.VPFRNearestLvnBelow,
                             vpfrBucketVols, vpfrBucketLow, vpfrBucketSize,
                             numBuckets:=cfg.Indicators.VPFR.NumBuckets,
                             hvnVolPct:=cfg.Indicators.VPFR.HvnVolPct,
                             lvnVolPct:=cfg.Indicators.VPFR.LvnVolPct,
                             hvnProximityPct:=cfg.Indicators.VPFR.HvnProximityPct,
                             decayBase:=cfg.Indicators.VPFR.DecayBase,
                             valueAreaPct:=cfg.Indicators.VPFR.ValueAreaPct)
r.VPFRPoc              = vpfrPoc
r.VPFRHVNearPoc        = vpfrHVNearPoc
r.VPFRSignal           = vpfrSignal
r.VPFRBucketVolumes    = vpfrBucketVols
r.VPFRBucketPriceLow   = vpfrBucketLow
r.VPFRBucketSize       = vpfrBucketSize
```

Or — equivalently — bind directly to the properties: `r.VPFRBucketVolumes, r.VPFRBucketPriceLow, r.VPFRBucketSize` as the args, no locals. Both work; pick whichever matches the surrounding style. The existing call uses three locals (`vpfrPoc/HVNearPoc/Signal`) so consistency argues for locals.

### 2.4 UI binding — inside `BindCardVolumeProfile`

Append below the existing value-area sub-label block (after [line 1129](UI/MainForm_Render_Cards.vb:1129) `End If`), above [line 1131](UI/MainForm_Render_Cards.vb:1131) `_cardVolumeProfile.Controls.Add(stack)`:

```vb
' --- VOLUME PROFILE mini histogram (P3 VolumeHistogramMini) ---
' Engine emits bucket 0 = priceLow (bottom). Control renders index 0
' at top. Reverse both the array and the POC index for display.
If r.VPFRBucketVolumes IsNot Nothing AndAlso r.VPFRBucketVolumes.Length > 0 _
   AndAlso r.VPFRBucketSize > 0 Then

    Dim n As Integer = r.VPFRBucketVolumes.Length

    ' Normalise to 0..1 and reverse so index 0 = highest price (top).
    Dim maxVol As Double = r.VPFRBucketVolumes.Max()
    If maxVol <= 0 Then maxVol = 1.0    ' defensive against all-zero
    Dim normalised(n - 1) As Single
    For i As Integer = 0 To n - 1
        normalised(i) = CSng(r.VPFRBucketVolumes(n - 1 - i) / maxVol)
    Next

    ' POC bucket index (engine-side, bottom-up), then reversed.
    Dim enginePocIdx As Integer = CInt(Math.Floor(
        (r.VPFRPoc - r.VPFRBucketPriceLow) / r.VPFRBucketSize))
    If enginePocIdx < 0 Then enginePocIdx = 0
    If enginePocIdx >= n Then enginePocIdx = n - 1
    Dim pocReversed As Integer = (n - 1) - enginePocIdx

    ' Current-price fraction (0 = bucketPriceLow at bottom, 1 = top).
    Dim totalRange As Double = r.VPFRBucketSize * n
    Dim cpFrac As Single = 0.5F
    If totalRange > 0 Then
        cpFrac = CSng(Math.Max(0.0, Math.Min(1.0,
                     (r.CurrentPrice - r.VPFRBucketPriceLow) / totalRange)))
    End If

    Dim histo As New VolumeHistogramMini() With {
        .Size = New Size(500, 90),     ' tentative — see §3.3 for width
        .Margin = New Padding(4, 8, 4, 0),
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

---

## 3. Critical implementation notes

### 3.1 Engine emission semantics — verify before binding

Apply lesson 4 from P4d spec-back §8. Before consuming the new bucket fields:

- Confirm the **bucket ordering** by reading `CalcVPFRLite` line 116-118 and verifying that `idx = floor((tp - priceLow) / bucketSize)` puts the lowest-priced trade in bucket 0. The UI reversal in §2.4 depends on this. Spec author confirmed at draft time; verify in your own pass.
- Confirm `bucketVol` is not cleared / reused / mutated after the function returns. `bucketVol` is locally allocated inside the function and the `ByRef` output assignment passes the reference out — no aliasing across calls. `IndicatorResults` is per-analysis-run, so the array's lifetime is bounded by the next `CalcVPFRLite` call (which allocates a fresh `bucketVol`).
- Confirm the early-return paths (`candles.Count < 10` and `priceRange <= 0`) set the new outputs to the seeded defaults from §2.1. The UI gate `Length > 0` in §2.4 protects against missed seeding.

### 3.2 Build-screenshot-measure gate

Apply lesson 3 from P4d spec-back §8. After implementation:

1. `dotnet build /c/Dev/DeribitVerdictEngine/DeribitVerdictEngine.sln` — must be clean.
2. Run app. Trigger one manual `Analyze Now`.
3. Screenshot the VOLUME PROFILE card.
4. **Verify visually:**
   - POC bar in amber (`ACC_WARN`).
   - Other bars in dim grey (`FG_DIM`).
   - Horizontal green line (`ACC_STRONG_LONG`) at current price.
   - **If `r.CurrentPrice > r.VPFRPoc`, the green line sits above the amber bar.** If below, below. This validates the bucket-reversal math in §2.4.
   - Histogram fills width sensibly (see §3.3).
5. Measure clipping. The card's row 7 height must accommodate the existing 7 price-level rows + 2 sub-labels + the histogram. If the histogram clips at the bottom, bump row 7 height (`AddRow(row7, …)` at [Layout.vb:454](UI/MainForm_Layout.vb:454)) by 40 px and re-verify.

### 3.3 Histogram width — watch for FlowLayoutPanel quirks

The pseudocode in §2.4 sets `.Size = New Size(500, 90)`. The card client width at form width 1100 px is roughly 520 px after padding. `FlowLayoutPanel` doesn't honour `DockStyle.Fill` on child controls — it uses each child's `Size`. Three options:

- **Fixed 500 px:** simple, works at the current form width. Will undersize if the form ever scales (it shouldn't per locked decisions).
- **Anchor + width measurement:** `.Anchor = AnchorStyles.Left Or AnchorStyles.Right`, set `Size` to `(stack.ClientSize.Width - 8, 90)`. Reads card width at bind time.
- **Wrap in a `Panel` with `Dock = DockStyle.Top` + `Height = 90`:** the inner `VolumeHistogramMini` then docks to the panel and fills it. Adds one control layer but plays nicest with FlowLayoutPanel.

Recommendation: option 1 first. Cheapest. If the histogram looks comically narrow on the verification screenshot, switch to option 2.

### 3.4 Row 7 height bump

Existing 210 px houses ~208 px of content (header + 7 level rows + 2 sub-labels). Adding 90 px histogram + 8 px top margin → needs ~308 px. **Bump row 7 to 320 px** for a 12 px safety margin. Update both the call and the comment at [Layout.vb:451-454](UI/MainForm_Layout.vb:451):

```vb
' BEFORE
' Height grown from 140 → 210 in P4d commit 3 to fit VOLUME PROFILE's
' 7-row level stack (VAH/HVN↑/LVN↑/POC/LVN↓/HVN↓/VAL) + two sub-
' labels, and OI × CVD's badge row + two MiniMeters.
AddRow(row7, 210)

' AFTER
' Height grown from 140 → 210 (P4d commit 3) → 320 (this spec) to fit
' VOLUME PROFILE's 7-row level stack + 2 sub-labels + the 90 px
' VolumeHistogramMini below them. OI × CVD column had headroom.
AddRow(row7, 320)
```

OI × CVD CROSS card on the same row has headroom and will tolerate the bump without redesign.

### 3.5 Vertical budget check

Form vertical ceiling per `ui-reskin-proposal.md` §3.6 is **1400 px target** (no hard ceiling). Row 7 + 110 px doesn't push past — current form sits comfortably under target. No further action needed unless the kickoff implementation surfaces an overflow.

### 3.6 No worst-case-string-length concern

The new lesson in handover §6 ("worst-case-string-length budget") applies to text-headline cards. This card's headline (`VOLUME PROFILE`) is fixed-string — no run-time content length variance. Lesson is documented for completeness; not actionable here.

### 3.7 No paint carve-out invocation

This work does not modify any `UI/Controls/*.vb` file. The four-check gate in handover §4.1 does not apply. If during implementation you find yourself wanting to tweak `VolumeHistogramMini.OnPaint`, **stop** — surface the need back to the spec author. Histogram visual tweaks are a separate decision.

---

## 4. Commit plan

**One commit.** Engine signature + DTO + call site + UI binding land together because the signature change requires the call site update, and the new fields require the UI consumer. Split phasing leaves the codebase non-building in between.

Suggested subject:

```
feat(ui-reskin): VPFR bucket exposure + VolumeHistogramMini wiring

Closes the docs/ui-reskin-proposal.md §4.7.2 mini-histogram gap that
P4d shipped without because r.VPFRBuckets didn't exist on
IndicatorResults at the time.

Engine (Core/Indicators_Structure.vb):
- CalcVPFRLite signature gains three ByRef outputs: bucketVolumesOut,
  bucketPriceLowOut, bucketSizeOut. All three are reference-assigned
  from existing locals (bucketVol, priceLow, bucketSize) — zero new
  allocation, zero scoring change. Early-return paths seed
  Array.Empty(Of Double)() / 0 / 0 so the UI gate Length > 0 is safe.

DTO (Core/IndicatorResults.vb):
- VPFRBucketVolumes / VPFRBucketPriceLow / VPFRBucketSize properties
  added after VPFRNearestLvnBelow. Default for the array property is
  Array.Empty(Of Double)().

Call site (UI/MainForm_Analysis.vb):
- Three new locals appended after vpfrSignal, copied into the new
  r.VPFR* fields after the call. Mirrors the existing local-and-copy
  pattern.

UI (UI/MainForm_Render_Cards.vb + UI/MainForm_Layout.vb):
- BindCardVolumeProfile instantiates VolumeHistogramMini below the
  existing value-area sub-label. Engine's low-to-high bucket order
  reversed for the control's top-to-bottom display; POC index
  recomputed in reversed space; current-price fraction maps via
  VPFRBucketPriceLow + VPFRBucketSize × n.
- row7 height bumped 210 → 320 px to accommodate the 90 px histogram.

No scoring impact. No CSV change. No settings.json change. No
UI/Controls/ modifications (paint carve-out not invoked).
```

Ship list for this commit:
- ✅ Engine bucket-exposure + early-return seed
- ✅ DTO three new fields with safe defaults
- ✅ Call-site extension
- ✅ UI binding inside `BindCardVolumeProfile`
- ✅ row7 height bump 210 → 320 px

Skip list (none defer):
- ⏸ No UI-side downsampling. Ship at full bucket count (default 50). If 50 bars at ~1.5 px each reads as a noisy block on the verification screenshot, **surface back** to the spec author as a follow-up rather than tweaking inline. The follow-up is ~10 LOC (aggregate groups of `Ceiling(n/8)` buckets, recompute PocIndex).

---

## 5. Verification gate

After the single commit:

1. **Build clean.** Engine signature change ripples to the single call site only.
2. **One manual analysis cycle.** Card renders with histogram below the price-level stack.
3. **Bucket-reversal sanity check** (§3.2 step 4). Spend 30 seconds confirming the green line / amber bar / POC value relationships.
4. **5+ auto-run cycles.** Confirm no perf regression. Per-bucket reference-pass is allocation-neutral; visual update at 50 bars should be sub-millisecond.
5. **Cross-check POC display vs histogram.** The existing `POC` row in the price-level stack shows a price (`POC: $94,210`). The amber bar's vertical position in the histogram should correspond to that price. If they disagree, the bucket-reversal math is wrong.

Live-data verification: 5 auto-run cycles is the floor. The user typically observes a full session before declaring a feature stable; that's their call, not yours.

---

## 6. Out of scope — explicit skip list

If any of these tempt you, **stop** and surface back to the spec author:

- ❌ Modify `VolumeHistogramMini.OnPaint`, constructor, or any property setter.
- ❌ Add new properties to `IndicatorResults` beyond the three in §2.2.
- ❌ Change `CalcVPFRLite`'s scoring-relevant outputs (POC / VAH / VAL / HVN / LVN). Read-only access only.
- ❌ Add a CSV column for any bucket-related data.
- ❌ Settings.json change. Bucket count stays at `cfg.Indicators.VPFR.NumBuckets` (existing key).
- ❌ Engine-side downsampling to 8 buckets. The control accepts any array length; UI handles density visually.
- ❌ Value-area band colouring on the histogram body. Out of scope per proposal §11.
- ❌ Click-to-anchor or any event surface on the histogram. Out of scope per proposal §11.
- ❌ P4f / P5 work.
- ❌ Pushing to remote.

---

## 7. If you get stuck

Three likely failure modes:

1. **Compiler complains about `ByRef bucketSize As Double` shadowing the local `Dim bucketSize As Double`.** Fix per §2.1 — rename the parameter to `bucketSizeOut` (and friends). VB.NET overload resolution will not warn loudly on this; the compiler may silently bind the local to the inner scope and leave the output uninitialised, which the UI will see as `bucketSize = 0` and silently skip the histogram. The bug presents as "histogram never appears" — not as a build error. Watch for it.

2. **POC bar position doesn't match the POC row in the price-level stack.** The bucket-reversal math is wrong. Trace:
   - `enginePocIdx = floor((POC - bucketPriceLow) / bucketSize)` — verify with `Debug.Print`.
   - `pocReversed = (n - 1) - enginePocIdx` — sanity check: if POC is at the top of the price range, `enginePocIdx` is near `n - 1`, so `pocReversed` is near 0 (top of histogram). ✓
   - If still wrong: check whether the array was reversed correctly. Print `normalised(0)` and `normalised(n-1)` and compare against `r.VPFRBucketVolumes(n-1)` and `r.VPFRBucketVolumes(0)` respectively (the reversal should make these equal up to normalisation).

3. **Histogram renders as a tiny strip at the top of the card.** FlowLayoutPanel sizing quirk — the control's `Size` got auto-sized down because the host control's width changed mid-layout. Apply §3.3 option 2 or option 3.

---

## 8. Reporting back

Spec-back doc: `docs/vpfr-buckets-histogram-spec-back.md`. Follow the structure from the P3 maintenance pass spec-back. Five things specifically worth reporting if they happen:

1. The width / size approach you ended up using (option 1 / 2 / 3 from §3.3) and why.
2. Whether 50 bars at the chosen height read as a histogram or a noisy block. If noisy, recommend the downsample follow-up.
3. Final row 7 height (320 px estimate vs. reality).
4. Any deviation from the proposal §3 / §6 pseudocode that proved necessary.
5. Whether the bucket-reversal math worked first try, or required debugging (calibrates the spec's clarity on directional details).

---

## 9. Workflow reminders

- **Local commits only.** Single commit per the plan above. **Do not push.** User decides when to push after live-data verification.
- No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits.
- No engine code other than the additions in §2.1. Read-only access for verification.
- Settings.json untouched.
- The §4 paint carve-out is **not** invoked — this work doesn't enter `UI/Controls/*.vb` at all.

---

**End of kickoff.** Drop this verbatim into a fresh Opus 4.7 Medium conversation as the opening message; the conversation has everything it needs to ship Spec B in one commit + spec-back.
