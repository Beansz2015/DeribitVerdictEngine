# UI Reskin P5-test — Implementation Kickoff

**Phase:** P5-test — Temporary render-parity test harness. Slots between **P5a** (snapshot rewire) and **P5b** (deletion sweep) inside the verification window.
**Spec source:** Feasibility study in spec-author conversation 2026-05-25; user committed to Path A after shipping P5a.
**Predecessor:** **P5a must have shipped and passed §6.2 + §6.3 gates before this kickoff is actionable.** See §0.5.
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-25 (skeleton — re-flow against P5a spec-back when ready)
**Recommended model:** **Opus 4.7 High.** The harness scaffolding is mechanical (~150-250 LoC), but the 40-60 test case factories are synthesis-heavy — each case needs realistic `IndicatorResults` field population that matches the production scenario it's representing, otherwise the renderer enters code branches that don't exist live and produces false-positive discrepancies. Lower models risk drift between intended test scenarios and actual data shapes.

---

## 0. What this phase is (and isn't)

**Is:** a temporary parity-verification harness that drives both the legacy `RenderOutput` and the new `BuildPlaintextSnapshot` over a curated set of 40-60 synthesised `IndicatorResults` + `VerdictResult` test cases covering every visual branch in the legacy display. Produces text artifacts + screenshots per case. Generates a discrepancy report that the spec author uses to draft the card-binding fix spec. After the fix spec ships and a re-run shows clean parity, the harness is removed in a final cleanup commit.

The point: compress the verification window from "wait for live market to surface every rare state" (weeks) to "exhaustively force every state in one harness run" (minutes).

**Isn't:**
- ❌ A unit test framework. No assertions, no test runner integration. The harness produces artifacts; humans + Claude review them.
- ❌ A permanent fixture. Removed once parity is confirmed. All scaffolding lives in clearly-named files (`tools/RenderParityHarness/` + `UI/MainForm_TestHarness.vb`) so cleanup is `git rm` + a few small reverts.
- ❌ A driver of the live engine via stub Deribit data. Bypasses `RunAnalysisAsync` entirely and feeds synthesised state directly to the renderers.
- ❌ Scoring / indicator / engine code change. Read-only access throughout.
- ❌ `UI/Controls/*.vb` modification.
- ❌ `MainForm.Designer.vb` edit.
- ❌ Settings.json change.
- ❌ CSV / dump schema change. Harness output writes to `verify/` (gitignored), not to the live dump or log.
- ❌ Card binding changes. The harness exercises the existing `BindCard*` methods; fixes for discrepancies ship as a SEPARATE spec, not folded in here.

---

## 0.5. Step 0 — Prerequisites (DO NOT START WITHOUT THESE)

Verify all four before writing any harness code:

1. **P5a shipped.** `git log --oneline | grep "P5a"` shows both P5a commits in the main branch (local-only is fine).
2. **`docs/ui-reskin-p5a-spec-back.md` exists** with §6.2 (inline RenderOutput-vs-snapshot parity) and §6.3 (pre/post P5a dump diff) marked clean.
3. **`BuildPlaintextSnapshot` is functional and matches the legacy text shape.** The harness relies on this — if the snapshot is broken, every test case will fail spuriously.
4. **`tools/screenshot-mainform.ps1` + companions work.** Run them once to confirm before scaling to 40-60 test cases.

If any of these is missing, **stop** and surface back to the spec author.

---

## 0.6. Addendum 2026-05-27 — drift since kickoff was drafted

Three small things the spec author confirmed against the current tree after the
screenshot-reliability work shipped (`4a9781e` + `65dd6e7` + `20f1a0b`). Fold these
into the implementation; they don't change scope.

1. **Screenshot capture is in-process, not via PowerShell.** A `Friend Sub
   SaveFullFormScreenshot(outPath As String)` already exists at
   `UI/MainForm_Layout.vb:1300`. It uses `_gridRoot.DrawToBitmap` against the
   grid's natural extent — captures the entire form regardless of display
   height, ~50ms per call. The harness should call it directly inside
   `RunOneTestCase` rather than shelling to `pwsh tools/screenshot-mainform.ps1`
   (the latter is visible-only and adds ~1-2s per case via process startup +
   hotkey marker-file roundtrip — for 50 cases that's 60-100s vs ~2.5s).
   Supersedes the `CaptureScreenshotAsync` PowerShell roundtrip implied in §3.3
   and the screenshot trigger flow in §5.1. §8.5 troubleshooting becomes moot.

2. **`Me.KeyPreview = True` is already set** at `UI/MainForm_Layout.vb:198`
   (added when the Ctrl+Shift+S hotkey shipped). §8.4 troubleshooting is
   obsolete — delete that bullet from your mental checklist.

3. **Ctrl+Shift+T wiring — add an `ElseIf` to the existing handler, do NOT add
   a second `Handles MyBase.KeyDown` Sub.** WinForms invokes every handler
   subscribed to the event, so a second handler would silently double-fire on
   any keypress and break the existing Ctrl+Shift+S hotkey. Pattern:

   ```vb
   ' UI/MainForm_Layout.vb — extend OnFormKeyDown around line 1277:
   Private Sub OnFormKeyDown(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
       If e.Control AndAlso e.Shift AndAlso e.KeyCode = Keys.S Then
           ' ... existing Ctrl+Shift+S branch ...
       ElseIf e.Control AndAlso e.Shift AndAlso e.KeyCode = Keys.T Then
           RunRenderParityHarness()
           e.Handled = True
       End If
   End Sub
   ```

   The `OnTestHarnessKeyCombo()` skeleton in §3.1 should be deleted — the
   `RunRenderParityHarness()` method itself stays in
   `UI/MainForm_TestHarness.vb`, but the keybind plumbing belongs in the
   existing handler in `MainForm_Layout.vb`. The §0 "isn't" rule about not
   touching `MainForm.Designer.vb` is unaffected; this is a programmatic
   handler in a partial-class `.vb` file.

Everything else in the kickoff (TestCase / TestCaseBuilder shape, side-effect
neutralisation pattern, commit plan, coverage matrix, discrepancy workflow) is
unchanged.

---

## 1. What you inherit

### 1.1 The renderers under test

After P5a:
- **Legacy:** `RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)` at `MainForm_Render_Sections.vb`. Writes RTF to `txtOutput`. Still alive throughout the harness's lifetime.
- **New (snapshot):** `BuildPlaintextSnapshot(v, r, norms, cfg) As String` at `UI/MainForm_PlaintextSnapshot.vb` (or wherever P5a placed it — confirm via grep). Returns plain text.
- **Card grid:** all `BindCardXxx(...)` methods in `MainForm_Render_Cards.vb`. The harness calls these directly with synthesised state.

### 1.2 Side-effect collaborators (must be neutralised in harness mode)

Calling `RenderOutput` or the binding methods normally triggers downstream side effects that pollute production state:

| Collaborator | Side effect | Neutralisation |
|---|---|---|
| `AnalysisOutputDump.Append` | Writes to `analysis_output_dump.md` | Harness sets `enabled = False` on the call OR adds `If _testHarnessMode Then Return` guard at the top of `Append`. **Prefer the in-call false:** keeps `AnalysisOutputDump.vb` untouched (host-agnostic). |
| `LivePerformanceTracker.UpdateAsync` | Writes to `analysis_eval_cache.csv` + updates perf strip | Skip the call entirely in harness mode. |
| `AnalysisLogger.LogRun` | Appends to `analysis_log.csv` | Skip the call entirely in harness mode. |
| `_oiHistory`, `_fundingHistory`, `_ofiHistory` ring buffers | Accumulate state across runs | The harness doesn't call data-fetch / append code, so these stay untouched. |
| `_skipCount` | Increments on skip path | The harness skips the skip branch — synthesises `VerdictResult` directly. |
| Auto-run timer | Triggers analyses on schedule | Harness mode disables auto-run for the duration of the test sequence. |

The general principle: the harness only invokes the **render** layer (legacy + snapshot + cards). Anything downstream is bypassed.

### 1.3 Existing helpers

- `tools/screenshot-mainform.ps1` — captures the form to PNG
- `tools/click-mainform-button.ps1` — UIA invoke (not needed for harness; we drive the renderers directly)
- `tools/inspect-mainform-tree.ps1` — UIA tree dump (useful for verifying card-grid state if screenshots are hard to compare)

---

## 2. Commit plan

Three commits within the harness lifecycle. The fourth (cleanup) ships after the discrepancy fix spec lands.

| # | Subject | Scope | Effort | LoC est. |
|---|---|---|---|---|
| 1 | `feat(test): P5-test — render parity harness scaffolding` | `UI/MainForm_TestHarness.vb` partial with `_testHarnessMode` flag, `TestCase` class, `TestRunner` loop, `TestCaseBuilder` fluent API, artifact writer. Hidden Ctrl+Shift+T keybind to enter harness mode. Includes ~5 sentinel test cases to prove the harness works. | High | ~400-500 |
| 2 | `feat(test): P5-test — full test case library (40-60 cases)` | `UI/TestHarnessCases.vb` partial: factory methods for every visual branch per the §4 coverage matrix. | High | ~1,500-2,500 |
| 3 | `chore(test): P5-test — remove harness after parity confirmed` | Ships AFTER the discrepancy fix spec has landed and a re-run shows clean diff. Deletes the two harness files, removes the keybind, removes the `_testHarnessMode` guards from the side-effect collaborators. | Low | ~-2,000 deletions |

Commit 3 is **gated on**: (a) the harness has run, (b) every discrepancy has been spec'd and fixed, (c) a re-run produces zero diffs. If new test cases need to be added during the cycle (e.g., the trader notices an unrelated edge case), they bundle into commit 2's branch.

### 2.1 Per-commit ship / skip

**Commit 1:**
- ✅ `UI/MainForm_TestHarness.vb` partial with `_testHarnessMode As Boolean` field
- ✅ `Ctrl+Shift+T` keybind handler that calls `RunRenderParityHarness()`
- ✅ `TestCase` class definition
- ✅ `TestCaseBuilder` with `NeutralCase()` factory + `.WithXxx(...)` fluent setters
- ✅ `TestRunner` that loops cases, invokes both renderers, writes artifacts
- ✅ 5 sentinel test cases proving the harness works (e.g., one for each verdict tier: STRONG_LONG, LONG, NO_TRADE, SHORT, STRONG_SHORT)
- ✅ Side-effect guards on `AnalysisOutputDump.Append` / `LivePerformanceTracker.UpdateAsync` / `AnalysisLogger.LogRun` call sites in the harness path only
- ⏸ Full test case library — commit 2
- ⏸ Cleanup — commit 3

**Commit 2:**
- ✅ Full test case library per §4 coverage matrix
- ✅ Each case has a comment citing the production scenario it represents (e.g., "modelled on CSV row 2026-05-13 14:21:09 — STRONG SHORT with MTF block")
- ⏸ Nothing else

**Commit 3 (after fix spec implemented and re-run is clean):**
- ✅ Delete `UI/MainForm_TestHarness.vb`
- ✅ Delete `UI/TestHarnessCases.vb`
- ✅ Remove `Ctrl+Shift+T` keybind
- ✅ Remove `_testHarnessMode` field and all guards
- ⏸ Don't delete `verify/` artifacts (gitignored anyway)

---

## 3. Harness architecture

### 3.1 Entry point

```vb
' UI/MainForm_TestHarness.vb (NEW partial — deleted in commit 3)
Partial Public Class MainForm

    Private _testHarnessMode As Boolean = False

    ' Hidden keybind. Captured in MainForm's KeyDown handler.
    Private Sub OnTestHarnessKeyCombo() Handles MyBase.KeyDown
        ' Trigger on Ctrl+Shift+T
    End Sub

    Friend Async Sub RunRenderParityHarness()
        _testHarnessMode = True
        Try
            Dim cases As List(Of TestCase) = TestHarnessCases.BuildAll()
            Dim outputDir As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "verify", "p5-test")
            Directory.CreateDirectory(outputDir)
            Dim report As New StringBuilder()
            For Each tc In cases
                Await RunOneTestCase(tc, outputDir, report)
            Next
            File.WriteAllText(Path.Combine(outputDir, "test-results.md"), report.ToString())
            MessageBox.Show($"Harness complete. {cases.Count} cases run. Report: {outputDir}\test-results.md",
                            "Test Harness", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Finally
            _testHarnessMode = False
        End Try
    End Sub

End Class
```

### 3.2 TestCase + builder

```vb
Public Class TestCase
    Public Property Name As String
    Public Property Description As String   ' production scenario this models
    Public Property Verdict As VerdictResult
    Public Property Indicators As IndicatorResults
    Public Property Norms As DynamicNorms
    Public Property Cfg As EngineSettings
    Public Property PosState As PositionState = PositionState.None
End Class

Public Class TestCaseBuilder
    Private _tc As New TestCase()

    Public Shared Function NeutralCase(name As String) As TestCaseBuilder
        Dim b As New TestCaseBuilder()
        b._tc.Name = name
        b._tc.Indicators = NeutralIndicators()    ' all fields zeroed / non-event
        b._tc.Verdict = NeutralVerdict()
        b._tc.Norms = NeutralNorms()
        b._tc.Cfg = SettingsLoader.Current        ' use real cfg as baseline
        Return b
    End Function

    Public Function WithVerdict(verdict As String, longScore As Integer, shortScore As Integer) As TestCaseBuilder
        _tc.Verdict.Verdict = verdict
        _tc.Verdict.LongScore = longScore
        _tc.Verdict.ShortScore = shortScore
        Return Me
    End Function

    Public Function WithContext(context As String) As TestCaseBuilder
        _tc.Verdict.VerdictContext = context
        Return Me
    End Function

    Public Function WithRegime(regime As String, adx As Double, plusDi As Double, minusDi As Double) As TestCaseBuilder
        _tc.Indicators.Regime = regime
        _tc.Indicators.ADX = adx
        _tc.Indicators.PlusDI = plusDi
        _tc.Indicators.MinusDI = minusDi
        Return Me
    End Function

    ' ... more fluent setters for each renderable state group ...

    Public Function Build() As TestCase
        Return _tc
    End Function
End Class
```

### 3.3 One test case run

```vb
Friend Async Function RunOneTestCase(tc As TestCase, outDir As String, report As StringBuilder) As Task
    ' Capture legacy via RenderOutput — writes to txtOutput (still alive in
    ' P5a interim). The harness mode flag suppresses the side-effect calls
    ' inside RenderOutput / RenderOutputHeader.
    RenderOutput(tc.Indicators, tc.Verdict, tc.Norms, vwapWarmup:=15, lastTradePrice:=tc.Indicators.CurrentPrice)
    Dim legacyText As String = txtOutput.Text
    File.WriteAllText(Path.Combine(outDir, $"{tc.Name}-legacy.txt"), legacyText)

    ' Capture snapshot.
    Dim snapshotText As String = BuildPlaintextSnapshot(tc.Verdict, tc.Indicators, tc.Norms, tc.Cfg)
    File.WriteAllText(Path.Combine(outDir, $"{tc.Name}-snapshot.txt"), snapshotText)

    ' Bind cards with the test state.
    BindAllCardsForTest(tc)

    ' Screenshot (call PowerShell helper via Process.Start).
    Await CaptureScreenshotAsync(Path.Combine(outDir, $"{tc.Name}.png"))

    ' Append diff summary to report.
    Dim diff As String = ComputeDiff(legacyText, snapshotText)
    If String.IsNullOrEmpty(diff) Then
        report.AppendLine($"## ✅ {tc.Name} — PARITY")
        report.AppendLine($"_{tc.Description}_")
        report.AppendLine()
    Else
        report.AppendLine($"## ❌ {tc.Name} — DISCREPANCY")
        report.AppendLine($"_{tc.Description}_")
        report.AppendLine()
        report.AppendLine("```diff")
        report.AppendLine(diff)
        report.AppendLine("```")
        report.AppendLine()
    End If
End Function
```

`ComputeDiff` is a small line-by-line diff function — no library dependency. ~30 LoC. Skip lines that differ only in market values (you can pin all test cases to a fixed price = `12345.6`, so any line containing that doesn't get flagged).

### 3.4 Side-effect neutralisation

The renderers call into side-effect collaborators. Wrap each call site with a `_testHarnessMode` guard:

```vb
' MainForm_Render_Sections.vb (still alive in P5a interim)
' Around line 329:
If Not _testHarnessMode Then
    AnalysisOutputDump.Append(...)
End If
```

Same pattern for `LivePerformanceTracker.UpdateAsync` and `AnalysisLogger.LogRun`. **All guards live in the host MainForm partials, not in the collaborator classes.** That keeps `AnalysisOutputDump.vb` host-agnostic per the Linux CLI portability rule (CLAUDE.md collaboration rules).

In commit 3 cleanup, these guards delete cleanly — the field reference becomes a "field not found" build error, surfacing every guard site at once.

---

## 4. Test case coverage matrix

Aim: 40-60 test cases. Each case packs as many independent states as possible. Each case has a unique `Name` (used for artifact filename) and a `Description` citing the production scenario.

### 4.1 Coverage axes

The §1 of the spec-author feasibility study enumerated the state surface. The harness must cover at minimum:

**Verdict tier (7 cases minimum):**
- `STRONG_LONG_CONFIRMED`
- `LONG_CONFIRMED` / `LONG_FLOW_UNCONFIRMED`
- `WEAK_LONG_MOMENTUM_FADING` / `WEAK_LONG_STRUCTURALLY_WEAK`
- `NO_TRADE_ALIGNED` (v30 ALIGNED context)
- `NO_TRADE_MTF_BLOCK`
- `WEAK_SHORT_MOMENTUM_FADING`
- `SHORT_CONFIRMED`
- `STRONG_SHORT_CONFIRMED`

**MTF gate formats (3 cases, can pack into above):**
- `MTF_PASS_DIR` — `MTF PASS [SHORT]`
- `MTF_BLOCK_DIR` — `MTF BLOCK [LONG vs TREND]`
- `MTF_STATE_ONLY` — `MTF state: TREND` (no direction proposed)

**Regime + ADX (4 cases × pack):**
- TRENDING_UP / TRENDING_DOWN / RANGE_BOUND / TRANSITIONAL with ADX above/below threshold

**Hold/Exit layers (~10 cases, requires `posState ≠ None`):**
- Layer 1: 2+ adverse microstructure → fast EXIT
- Layer 1.5: structural break exit (price closed below prior swing low for long)
- Layer 2: OBV divergence exit
- Layer 3: RSI divergence
- Layer 3: single adverse microstructure warning
- Layer 3: RSI/ROC structural OK
- Long + Short variants of each → ~10 cases

**ATR CAPPED reasons (4 cases × pack):**
- No cap (raw target unchanged)
- `CAPPED @ ... (SWING_HIGH_5M)` / `(SWING_LOW_5M)`
- `CAPPED @ ... (NEAREST_HVN_BELOW)` / `(NEAREST_HVN_ABOVE)`
- `CAPPED @ ... (POC)`
- Sub-tick CAPPED suppression case (delta < `max(0.5, ATR × 0.02)`)

**STRUCTURAL row states (9 cases × pack):**
- FULL + FULL (both directions have target+stop)
- FULL + STOP_ONLY / FULL + TARGET_ONLY combinations
- STOP_ONLY + STOP_ONLY
- TARGET_ONLY + TARGET_ONLY

**KELLY variants (6 cases × pack):**
- Suppressed (`KellyPWin = 0`)
- BIAS ONLY + CAPPED on NO TRADE
- BIAS ONLY on NO TRADE (not capped)
- Normal + CAPPED on directional verdict
- Normal directional (not capped)
- Lean `< 1 contract`

**VPFR + value area (5-6 cases × pack):**
- NEAR_HVN_SUPPORT / NEAR_HVN_RESIST / IN_LVN_BULL / IN_LVN_BEAR / NEUTRAL
- Combined with INSIDE_VA / ABOVE_VAH / BELOW_VAL

**OI × CVD outcomes (4 × pack):**
- CONFIRMED_LONG / CONFIRMED_SHORT / CONFLICT / NEUTRAL

**Pass 2c outcomes (3 × pack):**
- ALIGNED LONG / ALIGNED SHORT / CONFLICT / SUPPRESSED (4 actually — the SUPPRESSED case may not emit an item; see P4d spec-back §5.1 engine emission semantics)

**Trend Structure (5 × pack):**
- UPTREND with bonus / UPTREND without bonus (disagrees with side)
- DOWNTREND with bonus / DOWNTREND without bonus
- EXPANSION / CONTRACTION (no score change)
- UNDEFINED

**MicroCVD 5-state (5 × pack):**
- BULL_ACCEL / BULL_DECEL / BEAR_ACCEL / BEAR_DECEL / FLAT

**BBW squeeze states (4 × pack):**
- ACTIVE / RELEASING / NORMAL / NONE

**Funding bias × momentum (~9 cases pack):**
- 5 bias states × 3 momentum states, packed into ~9 cases

**Spread / Volume / RSI / RSI Div / EMA Ribbon / Donchian / OBV / TFI / Liquidations:**
- Each has 2-4 states, all packable into existing cases

**Edge cases (8-10):**
- REGIME ANCHOR caution firing
- ALIGNED context tag (NO TRADE special case)
- STRUCTURALLY_WEAK with swing data present
- FLOW_UNCONFIRMED with high score
- Negative-zero funding clamp (rate just below 1e-8)
- All seven verdict tiers stress (a case with everything maxed positively)
- All states minimum / NEUTRAL (a case with nothing firing)

### 4.2 Case construction discipline

Each case file (in `UI/TestHarnessCases.vb`) follows the same shape:

```vb
Public Shared Function StrongLong_Confirmed_Trending() As TestCase
    ' Description: STRONG LONG verdict in TRENDING_UP regime with full
    ' Pass 2c alignment. Modelled on CSV rows around 2026-05-04 11:30 UTC+8
    ' when BTC broke 100k with confluence. Tests:
    '   - Verdict tier hero rendering at 18pt
    '   - CONFIRMED context badge (green ✓)
    '   - REGIME ANCHOR caution NOT firing (within bounds)
    '   - MTF PASS [LONG] format
    '   - Long-side ATR levels normal (no CAPPED)
    '   - STRUCTURAL LONG full row, SHORT full row
    '   - Pass 2c ALIGNED ↑
    '   - OI × CVD CONFIRMED_LONG
    '   - Trend Structure UPTREND bonus +1
    Return TestCaseBuilder.NeutralCase("STRONG_LONG_CONFIRMED_TRENDING") _
        .WithVerdict("STRONG LONG", longScore:=17, shortScore:=3) _
        .WithContext("CONFIRMED") _
        .WithRegime("TRENDING_UP", adx:=35.0, plusDi:=28.0, minusDi:=12.0) _
        .WithMtfGate(MtfGate.PASS, direction:="LONG", trend:="BULL") _
        .WithTrendStructure(TrendStructure.UPTREND) _
        .WithOiCvdOutcome("CONFIRMED_LONG") _
        .WithPass2cOutcome("ALIGNED", side:="LONG") _
        .WithFunding(rate:=0.0001, bias:="NEUTRAL", momentum:="FLAT") _
        .WithVpfr(signal:="NEUTRAL", valueArea:="INSIDE_VA") _
        .WithKelly(pWin:=0.62, f:=0.18, halfKelly:=0.09, applied:=0.05, contracts:=3) _
        .Build()
End Function
```

The `NeutralCase` factory pre-populates everything with sane defaults; each case only overrides what's specific. Per-case LoC averages ~15-30 lines including the comment. 50 cases × 20 LoC = ~1000 LoC of case definitions, plus ~500 LoC of builder helpers = ~1500 LoC in `UI/TestHarnessCases.vb`.

### 4.3 Realism — the critical risk

The biggest risk (§7.1 in the feasibility study) is that synthesised data sets fields in combinations the engine would never produce. Mitigations:

1. **Cite the production scenario** in each case's comment. If the implementer can't think of a realistic scenario the case represents, the case is probably wrong.
2. **Cross-reference recent CSV rows.** For most cases, look up an actual `analysis_log.csv` row that exhibits the state being tested and use its values as the seed.
3. **Run sentinel cases first.** Commit 1's 5 sentinel cases should produce **zero diff** against legacy. If they don't, the builder or the snapshot has a bug — fix before scaling to 50 cases.

---

## 5. Discrepancy detection + fix-spec workflow

### 5.1 After commit 2 — first harness run

1. Build clean.
2. Launch app: `dotnet run` background.
3. Wait 10 seconds for form to render.
4. Trigger harness: send `Ctrl+Shift+T` via the existing `tools/click-mainform-button.ps1` (or a new `tools/send-keys.ps1` helper).
5. Wait for the harness to complete (~30-60 seconds for 50 cases including screenshots).
6. Open `verify/p5-test/test-results.md`.

The report shows each case as `✅ PARITY` or `❌ DISCREPANCY` with the diff inline for failures. Screenshot per case is at `verify/p5-test/{Name}.png`.

### 5.2 Discrepancy triage

Implementer kills the app and:
1. Reads `test-results.md`.
2. For each `❌ DISCREPANCY`, opens the diff + the corresponding screenshot.
3. Categorises the discrepancy:
   - **Missing in cards (true gap):** legacy text shows X, snapshot agrees, screenshot doesn't visibly show X anywhere → card binding needs to surface X
   - **Missing in snapshot:** legacy text shows X, snapshot omits → `BuildPlaintextSnapshot` shape bug
   - **Test case bug:** the test data is unrealistic / fields not coherent → fix the case factory
4. Writes a short summary in the spec-back appendix categorising each discrepancy.

### 5.3 Fix spec

Implementer **does NOT fix the discrepancies in this conversation.** Surfaces them back to the spec author (you) who:
1. Reviews the discrepancy summary
2. Drafts a fix spec — typically titled `ui-reskin-p5-test-gap-fixes-proposal.md`
3. Fix spec ships as its own implementation conversation (probably Medium effort)

This keeps blast radius small: the harness conversation produces a structured gap list; the fix conversation makes the actual card-binding / snapshot changes.

### 5.4 Re-run + cleanup

After the fix spec lands:
1. Re-run the harness (same `Ctrl+Shift+T` flow)
2. Confirm `test-results.md` shows zero `❌ DISCREPANCY` lines
3. Ship commit 3 (cleanup): delete harness files + guards
4. P5b becomes actionable

If the re-run still shows discrepancies, iterate: add more fixes OR refine test cases OR accept the discrepancy as design intent (some legacy items may not need card representation — the trader decides). Each iteration: fix spec → re-run → assess.

---

## 6. Verification gate

### 6.1 Build clean after each commit

`dotnet build` must be clean. Side-effect guards add no warnings.

### 6.2 Sentinel test run (after commit 1)

Run the 5 sentinel cases. **All five must produce zero diff** (parity). If any fails, the harness scaffolding has a bug — debug before adding the remaining 45-55 cases.

### 6.3 Full harness run (after commit 2)

Run the full 50 cases. Expectation: SOME discrepancies. That's the point of the harness — to find them.

If zero discrepancies on the first full run, suspect the test cases aren't actually exercising the branches they claim to. Cross-check at least 3 cases manually: read the legacy text, the snapshot text, and the screenshot, confirm the case is rendering the state it's supposed to.

### 6.4 Post-fix run (after the fix spec lands)

Re-run the harness. **All cases must now show `✅ PARITY`.** If any still fail, the fix was incomplete — iterate.

### 6.5 Cleanup verification (after commit 3)

```
grep -rn "_testHarnessMode\|RunRenderParityHarness\|TestHarnessCases\|TestCaseBuilder" UI/
```

Should return zero matches. All harness scaffolding gone. Build clean.

---

## 7. Out of scope

- ❌ Fix the discrepancies in this conversation. Spec author drafts a separate fix spec.
- ❌ Change card binding code to match what the harness reports as missing. Same — fix spec territory.
- ❌ Modify `AnalysisOutputDump.vb` / `LivePerformanceTracker.vb` / `AnalysisLogger.vb`. Side-effect guards live in MainForm host code only (Linux portability rule).
- ❌ Touch `UI/Controls/*.vb`.
- ❌ Touch `MainForm.Designer.vb`.
- ❌ Scoring / indicator / engine code.
- ❌ Settings.json.
- ❌ CSV / dump schema.
- ❌ Push to remote.

---

## 8. If you get stuck

1. **Sentinel test fails on the first run.** Compare the legacy text and snapshot text manually. If they differ in expected ways (e.g., snapshot has a header text the legacy doesn't), the snapshot has a bug → file a P5a fix spec, don't continue the harness. If they differ in unexpected ways, the test case data isn't being set correctly → debug the builder.

2. **Harness runs but some test cases crash the renderer.** Most likely the test data set a field combination the renderer didn't expect (e.g., `VPFRPoc = 0` but `VPFRHvnAt = True`). Add defensive sane-defaults to `NeutralIndicators()` — every numeric field non-zero, every string field non-empty, every bool field False.

3. **Side-effect guards leak.** A test case writes to `analysis_log.csv` despite `_testHarnessMode = True`. Grep for the call site and add a missed guard. List of expected guards: `AnalysisOutputDump.Append` (in `MainForm_Render_Sections.vb`), `LivePerformanceTracker.UpdateAsync` (in `MainForm_Analysis.vb`), `AnalysisLogger.LogRun` (same).

4. **`Ctrl+Shift+T` doesn't fire.** WinForms `KeyDown` only fires when `Form.KeyPreview = True`. Set it in the MainForm constructor (or in the test harness partial via a public method called from the constructor).

5. **Screenshots take too long.** Each `pwsh tools/screenshot-mainform.ps1` invocation has process-startup overhead. For 50 cases this adds ~30-60s. Acceptable. If unacceptable, batch screenshots: call `PrintWindow` directly via DllImport in the harness (no PowerShell), save PNG via `System.Drawing` — ~50 LoC. But probably not worth it.

6. **The test report is hard to scan.** Add a top-section summary: total cases / parity count / discrepancy count / list of discrepancies by name. Easier to triage than scrolling through diff blocks.

---

## 9. Reporting back

Spec-back doc: `docs/ui-reskin-p5-test-spec-back.md`. Same structure as past spec-backs.

Specifically worth reporting:

1. **First-run discrepancy count.** How many of the 50 cases failed parity? Categorised by missing-in-cards / missing-in-snapshot / test-case-bug.
2. **Discrepancy summary table.** Per failing case: case name, what the legacy showed, what the snapshot omitted (or what cards omitted), implementer's hypothesis on root cause.
3. **Test cases that crashed the renderer.** Hopefully zero. If non-zero, list which fields the renderer didn't tolerate.
4. **Harness performance.** Total runtime for 50 cases. Acceptable if under 90s.
5. **Coverage gaps.** States the harness didn't manage to cover (e.g., REGIME ANCHOR with specific ATR ratio — hard to construct).
6. **Cleanup status.** Whether commit 3 has been shipped (only after fix spec) or still pending.

The spec author uses the discrepancy summary to draft the fix spec.

---

## 10. Workflow reminders

- **Local commits only.** Three commits. Do NOT push.
- No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits.
- **Self-screenshot is the default verification path** (handover §10.9). The harness uses `tools/screenshot-mainform.ps1` extensively — every test case captures a screenshot.
- Engine code untouched. `Core/` is read-only.
- Settings.json untouched.
- The §4 paint carve-out is NOT invoked.
- `verify/` is gitignored. Test artifacts don't pollute the repo.
- The harness exists to **find** problems, not **fix** them. Fixes ship in a separate spec.

---

**End of kickoff.** Drop into a fresh Opus 4.7 High conversation when P5a is shipped and §0.5 prerequisites are met. The harness scaffolding is mechanical; the case library is the heavy synthesis work.
