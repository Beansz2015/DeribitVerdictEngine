' UI/MainForm_TestHarness.vb
'
' P5-test — temporary render-parity harness. Drives the legacy RenderOutput
' (RTF into txtOutput) and the new BuildPlaintextSnapshot over a curated set
' of synthesised (IndicatorResults, VerdictResult, DynamicNorms) triples,
' captures text artifacts + screenshots, and writes a discrepancy report.
'
' Bypasses RunAnalysisAsync entirely — the three side-effect collaborators
' (AnalysisLogger.LogRun, LivePerformanceTracker.UpdateAsync,
' AnalysisOutputDump.Append) all live in RunAnalysisAsync post-P5a so no
' guards are needed in the harness path. RenderOutput and BindCard* are
' clean of side effects.
'
' Entire scaffold deletes in P5-test commit 3 after parity is confirmed.
' Cleanup map:
'   - this file
'   - UI/TestHarnessCases.vb (commit 2)
'   - Ctrl+Shift+T ElseIf branch in MainForm_Layout.OnFormKeyDown
'
' Spec: docs/ui-reskin-p5-test-harness-kickoff.md (with §0.6 addendum).

Imports System.IO
Imports System.Text
Imports System.Windows.Forms

Partial Public Class MainForm

    Private _testHarnessMode As Boolean = False

    ' -----------------------------------------------------------------------
    ' Entry point — invoked from OnFormKeyDown (Ctrl+Shift+T).
    ' -----------------------------------------------------------------------
    Friend Async Sub RunRenderParityHarness()
        If _testHarnessMode Then Return  ' re-entrancy guard

        _testHarnessMode = True
        Try
            Dim cases As List(Of TestCase) = BuildSentinelCases()

            Dim outDir As String = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "verify", "p5-test")
            Directory.CreateDirectory(outDir)

            Dim report As New StringBuilder()
            report.AppendLine($"# P5-test parity report")
            report.AppendLine()
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
            report.AppendLine($"Cases: {cases.Count}")
            report.AppendLine()

            Dim parityCount As Integer = 0
            Dim discrepancyCount As Integer = 0
            Dim discrepancyNames As New List(Of String)

            ' Per-case sections collected after summary.
            Dim caseBlocks As New StringBuilder()

            For Each tc In cases
                Dim hadDiff As Boolean = Await RunOneTestCase(tc, outDir, caseBlocks)
                If hadDiff Then
                    discrepancyCount += 1
                    discrepancyNames.Add(tc.Name)
                Else
                    parityCount += 1
                End If
            Next

            report.AppendLine($"## Summary")
            report.AppendLine()
            report.AppendLine($"- Parity: **{parityCount}**")
            report.AppendLine($"- Discrepancy: **{discrepancyCount}**")
            If discrepancyCount > 0 Then
                report.AppendLine()
                report.AppendLine($"Discrepant cases:")
                For Each n In discrepancyNames
                    report.AppendLine($"- {n}")
                Next
            End If
            report.AppendLine()
            report.Append(caseBlocks.ToString())

            Dim reportPath As String = Path.Combine(outDir, "test-results.md")
            File.WriteAllText(reportPath, report.ToString())

            MessageBox.Show(
                $"Harness complete." & vbCrLf &
                $"{cases.Count} cases — {parityCount} parity, {discrepancyCount} discrepancy." & vbCrLf &
                $"Report: {reportPath}",
                "P5-test harness",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(
                $"Harness crashed:" & vbCrLf & ex.ToString(),
                "P5-test harness",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error)
        Finally
            _testHarnessMode = False
        End Try
    End Sub

    ' -----------------------------------------------------------------------
    ' One case: drives both renderers, binds cards, screenshots, diffs.
    ' Returns True if the case has a discrepancy.
    ' -----------------------------------------------------------------------
    Friend Async Function RunOneTestCase(
            tc As TestCase,
            outDir As String,
            report As StringBuilder) As Task(Of Boolean)

        ' --- 1. Legacy RTF render → plain text from txtOutput.
        RenderOutput(tc.Indicators, tc.Verdict, tc.Norms,
                     vwapWarmup:=tc.VwapWarmup,
                     lastTradePrice:=tc.LastTradePrice)
        Dim legacyText As String = txtOutput.Text
        File.WriteAllText(Path.Combine(outDir, $"{tc.Name}-legacy.txt"), legacyText)

        ' --- 2. Snapshot.
        Dim snapshotText As String = BuildPlaintextSnapshot(
            tc.Verdict, tc.Indicators, tc.Norms, tc.Cfg,
            vwapWarmup:=tc.VwapWarmup,
            lastTradePrice:=tc.LastTradePrice)
        File.WriteAllText(Path.Combine(outDir, $"{tc.Name}-snapshot.txt"), snapshotText)

        ' --- 3. Card grid.
        BindAllCardsForTest(tc)

        ' --- 4. Screenshot (in-process — uses _gridRoot.DrawToBitmap).
        Dim pngPath As String = Path.Combine(outDir, $"{tc.Name}.png")
        Application.DoEvents()  ' let layout settle before draw
        Try
            SaveFullFormScreenshot(pngPath)
        Catch ex As Exception
            report.AppendLine($"<!-- screenshot failed for {tc.Name}: {ex.Message} -->")
        End Try

        ' --- 5. Diff.
        Dim diffBody As String = ComputeLineDiff(legacyText, snapshotText)
        Dim hasDiff As Boolean = Not String.IsNullOrEmpty(diffBody)

        If hasDiff Then
            report.AppendLine($"## DISCREPANCY — {tc.Name}")
            report.AppendLine($"_{tc.Description}_")
            report.AppendLine()
            report.AppendLine("```diff")
            report.Append(diffBody)
            report.AppendLine("```")
            report.AppendLine()
        Else
            report.AppendLine($"## PARITY — {tc.Name}")
            report.AppendLine($"_{tc.Description}_")
            report.AppendLine()
        End If

        ' Tiny await so the UI thread can process layout/paint between cases
        ' without freezing the form for the full sequence duration.
        Await Task.Delay(20)

        Return hasDiff
    End Function

    ' -----------------------------------------------------------------------
    ' Bind every card the production RunAnalysisAsync binds. Mirrors lines
    ' 449-459 of MainForm_Analysis.vb exactly.
    ' -----------------------------------------------------------------------
    Friend Sub BindAllCardsForTest(tc As TestCase)
        BindCardScore(tc.Verdict)
        BindCardVerdict(tc.Verdict, tc.Indicators)
        BindCardLastPrice(tc.Indicators, tc.LastTradePrice)
        BindCardAtrLevels(tc.Verdict, tc.Indicators, tc.Norms)
        BindCardStructural(tc.Indicators, isLong:=True)
        BindCardStructural(tc.Indicators, isLong:=False)
        BindCardSignalBreakdown(tc.Verdict, tc.Indicators)
        BindCardOiCvdCross(tc.Indicators, tc.Verdict)
        BindCardVolumeProfile(tc.Indicators)
        BindCardKelly(tc.Verdict)
        BindCardIndicatorDetails(tc.Verdict, tc.Indicators, tc.Norms, tc.Cfg)
    End Sub

    ' -----------------------------------------------------------------------
    ' Sentinel cases for commit 1. Five verdict tiers proving the harness
    ' wires up correctly. Per §6.2 of the kickoff, ALL FIVE must produce
    ' zero diff before the full case library is added in commit 2.
    ' Authored in UI/MainForm_TestHarness.vb (this file) for commit 1; the
    ' full library moves to UI/TestHarnessCases.vb in commit 2.
    ' -----------------------------------------------------------------------
    Friend Function BuildSentinelCases() As List(Of TestCase)
        Dim cfg = SettingsLoader.Current
        Dim out As New List(Of TestCase)

        ' 1. STRONG LONG in a trending market.
        ' Modelled on a confluence breakout (BTC ~50k mid-2026 archetype): every
        ' bullish signal firing in TRENDING_UP regime, MTF PASS, OFI/CVD/TFI
        ' aligned, EMA ribbon + 5m EMA(200) bull, OI accumulating. Score 15/19.
        out.Add(TestCaseBuilder.NeutralCase("01_strong_long_trending", cfg) _
            .WithDescription("STRONG LONG in TRENDING_UP — sentinel for hero tier + confirmed context.") _
            .WithVerdict("STRONG LONG", "HIGH", longScore:=15, shortScore:=2, maxScore:=19) _
            .WithRegime("TRENDING_UP", adx:=32.5, plusDi:=28.0, minusDi:=14.0) _
            .WithMtfPass() _
            .WithBreakdownItem("ROC(9)",         longHit:=True,  shortHit:=False, note:="+1 BULL ROC rising") _
            .WithBreakdownItem("RSI(9)",         longHit:=True,  shortHit:=False, note:="+1 RSI 64 > 50") _
            .WithBreakdownItem("DMI +/-DI",      longHit:=True,  shortHit:=False, note:="+1 +DI 28 > -DI 14") _
            .WithBreakdownItem("ADX>22",         longHit:=True,  shortHit:=False, note:="+1 trend strength") _
            .WithBreakdownItem("Volume",         longHit:=True,  shortHit:=False, note:="+1 vol 1.6x SMA") _
            .WithBreakdownItem("VWAP",           longHit:=True,  shortHit:=False, note:="+1 above VWAP") _
            .WithBreakdownItem("BBW/TTM",        longHit:=True,  shortHit:=False, note:="+1 BULL_BUILDING") _
            .WithBreakdownItem("EMA 9/21/50",    longHit:=True,  shortHit:=False, note:="+1 BULL alignment") _
            .WithBreakdownItem("Funding (info)", longHit:=False, shortHit:=False, note:="info: rate 0.010% NEUTRAL") _
            .WithBreakdownItem("OI Delta",       longHit:=True,  shortHit:=False, note:="+1 NEW LONGS") _
            .WithBreakdownItem("OFI",            longHit:=True,  shortHit:=False, note:="+1 BUY DOMINANT") _
            .WithBreakdownItem("CVD",            longHit:=True,  shortHit:=False, note:="+1 RISING") _
            .WithBreakdownItem("TFI",            longHit:=True,  shortHit:=False, note:="+1 BUY PRESSURE") _
            .WithBreakdownItem("MicroCVD",       longHit:=True,  shortHit:=False, note:="+1 BULL_ACCEL") _
            .WithBreakdownItem("Liq Penalty",    longHit:=False, shortHit:=False, note:="—") _
            .WithBreakdownItem("Spread",         longHit:=False, shortHit:=False, note:="NORMAL 1.0 bps") _
            .WithBreakdownItem("5m EMA(200)",    longHit:=True,  shortHit:=False, note:="+1 price ABOVE") _
            .WithBreakdownItem("Donchian(20)",   longHit:=True,  shortHit:=False, note:="+1 breakout up") _
            .WithBreakdownItem("OBV",            longHit:=True,  shortHit:=False, note:="+1 RISING") _
            .WithBreakdownItem("VPFR-lite",      longHit:=False, shortHit:=False, note:="NEUTRAL") _
            .WithBreakdownItem("MTF Gate (15m)", longHit:=True,  shortHit:=False, note:="PASS [LONG] vs BULL trend") _
            .Build())

        ' 2. LONG (one tier down) — confluence partial, MTF PASS, score 11/19.
        out.Add(TestCaseBuilder.NeutralCase("02_long_trending", cfg) _
            .WithDescription("LONG in TRENDING_UP — sentinel for mid-strength directional verdict.") _
            .WithVerdict("LONG", "MEDIUM", longScore:=11, shortScore:=4, maxScore:=19) _
            .WithRegime("TRENDING_UP", adx:=24.0, plusDi:=24.0, minusDi:=16.0) _
            .WithMtfPass() _
            .WithBreakdownItem("ROC(9)",         longHit:=True,  shortHit:=False, note:="+1 BULL ROC") _
            .WithBreakdownItem("RSI(9)",         longHit:=True,  shortHit:=False, note:="+1 RSI 58") _
            .WithBreakdownItem("DMI +/-DI",      longHit:=True,  shortHit:=False, note:="+1 +DI > -DI") _
            .WithBreakdownItem("ADX>22",         longHit:=True,  shortHit:=False, note:="+1 trend") _
            .WithBreakdownItem("Volume",         longHit:=False, shortHit:=False, note:="mid 1.2x") _
            .WithBreakdownItem("VWAP",           longHit:=True,  shortHit:=False, note:="+1 above VWAP") _
            .WithBreakdownItem("BBW/TTM",        longHit:=False, shortHit:=False, note:="FLAT") _
            .WithBreakdownItem("EMA 9/21/50",    longHit:=True,  shortHit:=False, note:="+1 BULL") _
            .WithBreakdownItem("Funding (info)", longHit:=False, shortHit:=False, note:="info: 0.012%") _
            .WithBreakdownItem("OI Delta",       longHit:=True,  shortHit:=False, note:="+1 NEW LONGS") _
            .WithBreakdownItem("OFI",            longHit:=False, shortHit:=True,  note:="-1 SELL DOMINANT") _
            .WithBreakdownItem("CVD",            longHit:=True,  shortHit:=False, note:="+1 RISING") _
            .WithBreakdownItem("TFI",            longHit:=False, shortHit:=False, note:="NEUTRAL") _
            .WithBreakdownItem("MicroCVD",       longHit:=False, shortHit:=True,  note:="-1 BULL_DECEL late fade") _
            .WithBreakdownItem("Liq Penalty",    longHit:=False, shortHit:=False, note:="—") _
            .WithBreakdownItem("Spread",         longHit:=False, shortHit:=False, note:="NORMAL") _
            .WithBreakdownItem("5m EMA(200)",    longHit:=True,  shortHit:=False, note:="+1 ABOVE") _
            .WithBreakdownItem("Donchian(20)",   longHit:=False, shortHit:=False, note:="NONE") _
            .WithBreakdownItem("OBV",            longHit:=False, shortHit:=True,  note:="-1 divergence") _
            .WithBreakdownItem("VPFR-lite",      longHit:=False, shortHit:=False, note:="INSIDE_VA") _
            .WithBreakdownItem("MTF Gate (15m)", longHit:=True,  shortHit:=False, note:="PASS [LONG]") _
            .Build())

        ' 3. NO TRADE — RANGE_BOUND with balanced 7/7. Per spec author's
        ' explicit guidance: "NO_TRADE sentinel may still be largely blank —
        ' that's fine (no hits is a valid production state)." A handful of
        ' mixed rows so the section isn't trivially empty.
        out.Add(TestCaseBuilder.NeutralCase("03_no_trade_neutral", cfg) _
            .WithDescription("NO TRADE in RANGE_BOUND — sentinel for indecision baseline.") _
            .WithVerdict("NO TRADE", "LOW", longScore:=7, shortScore:=7, maxScore:=18) _
            .WithRegime("RANGE_BOUND", adx:=14.0, plusDi:=18.0, minusDi:=18.0) _
            .WithMtfPass() _
            .WithBreakdownItem("ROC(9)",         longHit:=False, shortHit:=False, note:="FLAT") _
            .WithBreakdownItem("RSI(9)",         longHit:=False, shortHit:=False, note:="RSI 50") _
            .WithBreakdownItem("DMI +/-DI",      longHit:=False, shortHit:=False, note:="balanced") _
            .WithBreakdownItem("ADX>22",         longHit:=False, shortHit:=False, note:="ADX 14 below thr") _
            .WithBreakdownItem("Volume",         longHit:=False, shortHit:=False, note:="1.0x SMA") _
            .WithBreakdownItem("VWAP",           longHit:=True,  shortHit:=True,  note:="±0 at VWAP") _
            .WithBreakdownItem("BBW/TTM",        longHit:=False, shortHit:=False, note:="FLAT") _
            .WithBreakdownItem("EMA 9/21/50",    longHit:=False, shortHit:=False, note:="MIXED") _
            .WithBreakdownItem("Funding (info)", longHit:=False, shortHit:=False, note:="info: 0.010%") _
            .WithBreakdownItem("OI Delta",       longHit:=False, shortHit:=False, note:="NEUTRAL") _
            .WithBreakdownItem("OFI",            longHit:=False, shortHit:=False, note:="BALANCED") _
            .WithBreakdownItem("CVD",            longHit:=False, shortHit:=False, note:="FLAT") _
            .WithBreakdownItem("TFI",            longHit:=False, shortHit:=False, note:="NEUTRAL") _
            .WithBreakdownItem("MicroCVD",       longHit:=False, shortHit:=False, note:="FLAT") _
            .WithBreakdownItem("Liq Penalty",    longHit:=False, shortHit:=False, note:="—") _
            .WithBreakdownItem("Spread",         longHit:=False, shortHit:=False, note:="NORMAL") _
            .WithBreakdownItem("5m EMA(200)",    longHit:=True,  shortHit:=False, note:="+1 ABOVE") _
            .WithBreakdownItem("Donchian(20)",   longHit:=False, shortHit:=False, note:="NONE") _
            .WithBreakdownItem("OBV",            longHit:=False, shortHit:=False, note:="FLAT") _
            .WithBreakdownItem("VPFR-lite",      longHit:=False, shortHit:=False, note:="INSIDE_VA NEUTRAL") _
            .WithBreakdownItem("MTF Gate (15m)", longHit:=False, shortHit:=False, note:="state: FLAT") _
            .Build())

        ' 4. SHORT — mirror of case 2.
        out.Add(TestCaseBuilder.NeutralCase("04_short_trending", cfg) _
            .WithDescription("SHORT in TRENDING_DOWN — sentinel for short-side directional verdict.") _
            .WithVerdict("SHORT", "MEDIUM", longScore:=4, shortScore:=11, maxScore:=19) _
            .WithRegime("TRENDING_DOWN", adx:=24.0, plusDi:=16.0, minusDi:=24.0) _
            .WithMtfPass() _
            .WithBreakdownItem("ROC(9)",         longHit:=False, shortHit:=True,  note:="-1 BEAR ROC") _
            .WithBreakdownItem("RSI(9)",         longHit:=False, shortHit:=True,  note:="-1 RSI 42") _
            .WithBreakdownItem("DMI +/-DI",      longHit:=False, shortHit:=True,  note:="-1 -DI > +DI") _
            .WithBreakdownItem("ADX>22",         longHit:=False, shortHit:=True,  note:="-1 trend") _
            .WithBreakdownItem("Volume",         longHit:=False, shortHit:=False, note:="mid 1.2x") _
            .WithBreakdownItem("VWAP",           longHit:=False, shortHit:=True,  note:="-1 below VWAP") _
            .WithBreakdownItem("BBW/TTM",        longHit:=False, shortHit:=False, note:="FLAT") _
            .WithBreakdownItem("EMA 9/21/50",    longHit:=False, shortHit:=True,  note:="-1 BEAR") _
            .WithBreakdownItem("Funding (info)", longHit:=False, shortHit:=False, note:="info: 0.008%") _
            .WithBreakdownItem("OI Delta",       longHit:=False, shortHit:=True,  note:="-1 NEW SHORTS") _
            .WithBreakdownItem("OFI",            longHit:=True,  shortHit:=False, note:="+1 BUY DOMINANT (snap)") _
            .WithBreakdownItem("CVD",            longHit:=False, shortHit:=True,  note:="-1 FALLING") _
            .WithBreakdownItem("TFI",            longHit:=False, shortHit:=False, note:="NEUTRAL") _
            .WithBreakdownItem("MicroCVD",       longHit:=True,  shortHit:=False, note:="+1 BEAR_DECEL late bounce") _
            .WithBreakdownItem("Liq Penalty",    longHit:=False, shortHit:=False, note:="—") _
            .WithBreakdownItem("Spread",         longHit:=False, shortHit:=False, note:="NORMAL") _
            .WithBreakdownItem("5m EMA(200)",    longHit:=False, shortHit:=True,  note:="-1 BELOW") _
            .WithBreakdownItem("Donchian(20)",   longHit:=False, shortHit:=False, note:="NONE") _
            .WithBreakdownItem("OBV",            longHit:=True,  shortHit:=False, note:="+1 bull divergence") _
            .WithBreakdownItem("VPFR-lite",      longHit:=False, shortHit:=False, note:="INSIDE_VA") _
            .WithBreakdownItem("MTF Gate (15m)", longHit:=False, shortHit:=True,  note:="PASS [SHORT]") _
            .Build())

        ' 5. STRONG SHORT — mirror of case 1.
        out.Add(TestCaseBuilder.NeutralCase("05_strong_short_trending", cfg) _
            .WithDescription("STRONG SHORT in TRENDING_DOWN — sentinel for hero tier (negative).") _
            .WithVerdict("STRONG SHORT", "HIGH", longScore:=2, shortScore:=15, maxScore:=19) _
            .WithRegime("TRENDING_DOWN", adx:=32.5, plusDi:=14.0, minusDi:=28.0) _
            .WithMtfPass() _
            .WithBreakdownItem("ROC(9)",         longHit:=False, shortHit:=True,  note:="-1 BEAR ROC falling") _
            .WithBreakdownItem("RSI(9)",         longHit:=False, shortHit:=True,  note:="-1 RSI 36 < 50") _
            .WithBreakdownItem("DMI +/-DI",      longHit:=False, shortHit:=True,  note:="-1 -DI 28 > +DI 14") _
            .WithBreakdownItem("ADX>22",         longHit:=False, shortHit:=True,  note:="-1 trend strength") _
            .WithBreakdownItem("Volume",         longHit:=False, shortHit:=True,  note:="-1 vol 1.6x SMA") _
            .WithBreakdownItem("VWAP",           longHit:=False, shortHit:=True,  note:="-1 below VWAP") _
            .WithBreakdownItem("BBW/TTM",        longHit:=False, shortHit:=True,  note:="-1 BEAR_BUILDING") _
            .WithBreakdownItem("EMA 9/21/50",    longHit:=False, shortHit:=True,  note:="-1 BEAR alignment") _
            .WithBreakdownItem("Funding (info)", longHit:=False, shortHit:=False, note:="info: 0.005% NEUTRAL") _
            .WithBreakdownItem("OI Delta",       longHit:=False, shortHit:=True,  note:="-1 NEW SHORTS") _
            .WithBreakdownItem("OFI",            longHit:=False, shortHit:=True,  note:="-1 SELL DOMINANT") _
            .WithBreakdownItem("CVD",            longHit:=False, shortHit:=True,  note:="-1 FALLING") _
            .WithBreakdownItem("TFI",            longHit:=False, shortHit:=True,  note:="-1 SELL PRESSURE") _
            .WithBreakdownItem("MicroCVD",       longHit:=False, shortHit:=True,  note:="-1 BEAR_ACCEL") _
            .WithBreakdownItem("Liq Penalty",    longHit:=False, shortHit:=False, note:="—") _
            .WithBreakdownItem("Spread",         longHit:=False, shortHit:=False, note:="NORMAL 1.0 bps") _
            .WithBreakdownItem("5m EMA(200)",    longHit:=False, shortHit:=True,  note:="-1 price BELOW") _
            .WithBreakdownItem("Donchian(20)",   longHit:=False, shortHit:=True,  note:="-1 breakdown") _
            .WithBreakdownItem("OBV",            longHit:=False, shortHit:=True,  note:="-1 FALLING") _
            .WithBreakdownItem("VPFR-lite",      longHit:=False, shortHit:=False, note:="NEUTRAL") _
            .WithBreakdownItem("MTF Gate (15m)", longHit:=False, shortHit:=True,  note:="PASS [SHORT] vs BEAR trend") _
            .Build())

        Return out
    End Function

    ' -----------------------------------------------------------------------
    ' Line-by-line diff. No external dependency. Reports lines present in
    ' one but not the other, side-by-side. Returns empty string on parity.
    ' -----------------------------------------------------------------------
    Friend Shared Function ComputeLineDiff(legacy As String, snapshot As String) As String
        Dim legacyLines As String() = NormalizeLines(legacy)
        Dim snapshotLines As String() = NormalizeLines(snapshot)

        Dim n As Integer = Math.Max(legacyLines.Length, snapshotLines.Length)
        Dim sb As New StringBuilder()
        Dim anyDiff As Boolean = False

        For i As Integer = 0 To n - 1
            Dim lLine As String = If(i < legacyLines.Length, legacyLines(i), "<EOF>")
            Dim sLine As String = If(i < snapshotLines.Length, snapshotLines(i), "<EOF>")
            If lLine <> sLine Then
                anyDiff = True
                sb.AppendLine($"- L{i + 1,4}: {lLine}")
                sb.AppendLine($"+ S{i + 1,4}: {sLine}")
            End If
        Next

        If Not anyDiff Then Return ""
        Return sb.ToString()
    End Function

    Private Shared Function NormalizeLines(text As String) As String()
        If String.IsNullOrEmpty(text) Then Return Array.Empty(Of String)()
        ' Normalise CRLF / CR / LF so diffs aren't polluted by line-ending
        ' differences between txtOutput.Text and the snapshot StringBuilder.
        Dim norm As String = text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
        Return norm.Split(CChar(vbLf))
    End Function

End Class

' ===========================================================================
' TestCase + TestCaseBuilder. Top-level classes (not nested) — keep harness
' callers in MainForm partials clean. Deleted with the rest of the harness
' in commit 3.
' ===========================================================================

Public Class TestCase
    Public Property Name As String
    Public Property Description As String
    Public Property Verdict As VerdictResult
    Public Property Indicators As IndicatorResults
    Public Property Norms As DynamicNorms
    Public Property Cfg As EngineSettings
    Public Property PosState As PositionState = PositionState.None
    Public Property VwapWarmup As Integer = 60
    Public Property LastTradePrice As Double = 50000.0
End Class

Public Class TestCaseBuilder

    Private ReadOnly _tc As TestCase

    Private Sub New(tc As TestCase)
        _tc = tc
    End Sub

    Public Shared Function NeutralCase(name As String, cfg As EngineSettings) As TestCaseBuilder
        Dim tc As New TestCase With {
            .Name = name,
            .Description = "",
            .Indicators = NeutralIndicators(),
            .Verdict = NeutralVerdict(),
            .Norms = NeutralNorms(),
            .Cfg = cfg,
            .PosState = PositionState.None,
            .VwapWarmup = 60,
            .LastTradePrice = 50000.0
        }
        Return New TestCaseBuilder(tc)
    End Function

    Public Function WithDescription(description As String) As TestCaseBuilder
        _tc.Description = description
        Return Me
    End Function

    Public Function WithVerdict(verdict As String, confidence As String,
                                longScore As Integer, shortScore As Integer,
                                maxScore As Integer) As TestCaseBuilder
        _tc.Verdict.Verdict = verdict
        _tc.Verdict.Confidence = confidence
        _tc.Verdict.LongScore = longScore
        _tc.Verdict.ShortScore = shortScore
        _tc.Verdict.EffectiveLongScore = longScore
        _tc.Verdict.EffectiveShortScore = shortScore
        _tc.Verdict.MaxScore = maxScore
        Return Me
    End Function

    Public Function WithRegime(regime As String, adx As Double,
                               plusDi As Double, minusDi As Double) As TestCaseBuilder
        _tc.Indicators.Regime = regime
        _tc.Indicators.ADX = adx
        _tc.Indicators.PlusDI = plusDi
        _tc.Indicators.MinusDI = minusDi
        Return Me
    End Function

    Public Function WithMtfPass() As TestCaseBuilder
        _tc.Indicators.MTFGatePass = True
        _tc.Indicators.MTFGateReason = "MTF PASS"
        Return Me
    End Function

    Public Function WithMtfBlock(reason As String) As TestCaseBuilder
        _tc.Indicators.MTFGatePass = False
        _tc.Indicators.MTFGateReason = reason
        Return Me
    End Function

    ' Appends one row to v.SignalBreakdown. Use this to build up the
    ' per-case breakdown table that the renderer iterates. Hit booleans
    ' reflect which side(s) the signal scored for; the note column
    ' carries the engine's per-row scoring rationale ("+1 BULL ROC",
    ' "above VWAP", etc.). Label must match the canonical roster the
    ' renderer's label-match lookup expects (see
    ' Core/ScoringEngine_Calculate_Scoring.vb lines 637-757 +
    ' ScoringEngine_Calculate_Verdict.vb:82).
    Public Function WithBreakdownItem(label As String,
                                      longHit As Boolean,
                                      shortHit As Boolean,
                                      Optional note As String = "") As TestCaseBuilder
        _tc.Verdict.SignalBreakdown.Add(New SignalBreakdownItem(label, longHit, shortHit, note))
        Return Me
    End Function

    ' Replaces the entire breakdown list with the supplied items. For cases
    ' that pre-build the full row set or want to clone production CSV state.
    Public Function WithBreakdown(items As IEnumerable(Of SignalBreakdownItem)) As TestCaseBuilder
        _tc.Verdict.SignalBreakdown = items.ToList()
        Return Me
    End Function

    Public Function Build() As TestCase
        Return _tc
    End Function

    ' -----------------------------------------------------------------------
    ' Neutral / safe-default factories.
    '
    ' Strategy: populate every field the renderers (RenderOutput +
    ' BuildPlaintextSnapshot + BindCard*) read, with values that produce
    ' a coherent "nothing firing" baseline. Sentinel cases override only
    ' the fields specific to their scenario; everything else stays neutral
    ' so the two renderers see identical input and should produce parallel
    ' output.
    '
    ' All numeric fields non-zero where the renderer would divide / log;
    ' all string fields non-empty so .Trim() / .ToUpper() / Select Case
    ' branches resolve deterministically.
    ' -----------------------------------------------------------------------
    Public Shared Function NeutralIndicators() As IndicatorResults
        Dim r As New IndicatorResults()

        r.CurrentPrice = 50000.0

        r.ROC = 0.0
        r.ROCSlope = "FLAT"
        r.RSI = 50.0
        r.RSIDivergence = "NONE"
        r.ATR = 80.0
        r.ATRSizeMultiplier = 1.0
        r.VolumeSMA9 = 100.0
        r.CurrentVolume = 100.0
        r.CurrentVolumeUSD = 5_000_000.0
        r.VolumeRatio = 1.0

        r.PlusDI = 18.0
        r.MinusDI = 18.0
        r.ADX = 14.0
        r.Regime = "RANGE_BOUND"

        r.VWAP = 50000.0
        r.VWAPDevPct = 0.0
        r.VWAPSessionCandles = 60
        r.VWAPSigma1Upper = 50100.0
        r.VWAPSigma1Lower = 49900.0
        r.VWAPSigma2Upper = 50200.0
        r.VWAPSigma2Lower = 49800.0

        r.BBW = 1.0
        r.SqueezeStatus = "NONE"
        r.TTMHistogram = 0.0
        r.TTMDirection = "FLAT"
        r.TTMSignal = "FLAT"

        r.EMA9 = 50000.0
        r.EMA21 = 50000.0
        r.EMA50 = 50000.0
        r.EMAAlignment = "MIXED"

        r.FundingRate = 0.0001
        r.FundingBias = "NEUTRAL"
        r.FundingMomentum = "FLAT"
        r.FundingDelta = 0.0

        r.OI_Current = 1_000_000.0
        r.OIChange15m = 0.0
        r.OIChange60m = 0.0
        r.OISignal = "NEUTRAL"

        r.OFIRatio = 1.0
        r.OFISignal = "BALANCED"
        r.OFIMomentum = "FLAT"
        r.OFIBidVol = 100.0
        r.OFIAskVol = 100.0
        r.SpreadBps = 1.0
        r.SpreadStatus = "NORMAL"

        r.LiqLongSize = 0.0
        r.LiqShortSize = 0.0
        r.LiqSignal = "NONE"

        r.EMA200_5m = 50000.0
        r.PriceVsEMA200 = "ABOVE"
        r.CVDValue = 0.0
        r.CVDSlope = "FLAT"
        r.CVDDivergence = "NONE"

        r.TFIValue = 0.0
        r.TFISignal = "NEUTRAL"

        r.MicroCVDEarly = 0.0
        r.MicroCVDMid = 0.0
        r.MicroCVDLate = 0.0
        r.MicroCVDMomentum = "FLAT"
        r.MicroCVDSignal = "FLAT"

        r.MTF15mTrend = "FLAT"
        r.MTF15mADX = 18.0
        r.MTF15mEMAAlignment = "MIXED"
        r.MTFGatePass = True
        r.MTFGateReason = "MTF PASS"

        r.DonchianUpper = 50500.0
        r.DonchianLower = 49500.0
        r.DonchianSignal = "NONE"
        r.OBVTrend = "FLAT"
        r.OBVDivergence = "NONE"

        r.VPFRPoc = 50000.0
        r.VPFRHVNearPoc = False
        r.VPFRSignal = "NEUTRAL"
        r.VPFRVah = 50200.0
        r.VPFRVal = 49800.0
        r.VPFRValueAreaSignal = "INSIDE_VA"
        r.VPFRNearestHvnAbove = 0.0
        r.VPFRNearestHvnBelow = 0.0
        r.VPFRNearestLvnAbove = 0.0
        r.VPFRNearestLvnBelow = 0.0
        r.VPFRBucketVolumes = New Double() {1, 1, 1, 1, 1, 1, 1, 1,
                                            1, 1, 1, 1, 1, 1, 1, 1}
        r.VPFRBucketPriceLow = 49500.0
        r.VPFRBucketSize = 62.5

        r.LastSwingHigh5m = 50300.0
        r.LastSwingLow5m = 49700.0
        r.LastSwingHigh15m = 50500.0
        r.LastSwingLow15m = 49500.0
        r.SwingTargetLong = 50300.0
        r.SwingStopLong = 49700.0
        r.SwingTargetShort = 49700.0
        r.SwingStopShort = 50300.0

        r.BestPivotByVolume5m = 50300.0
        r.BestPivotVolumeRatio5m = 1.0
        r.BestPivotIsHigh5m = True

        r.TrendStructure = TrendStructure.UNDEFINED
        r.LastTwoHighs5m = (0.0, 0.0)
        r.LastTwoLows5m = (0.0, 0.0)

        Return r
    End Function

    Public Shared Function NeutralVerdict() As VerdictResult
        Dim v As New VerdictResult()
        v.LongScore = 7
        v.ShortScore = 7
        v.EffectiveLongScore = 7
        v.EffectiveShortScore = 7
        v.RegimePenalty = 0
        v.MaxScore = 18
        v.Verdict = "NO TRADE"
        v.Confidence = "LOW"
        v.HoldStatus = ""
        v.VerdictContext = "CONFIRMED"
        v.OiCvdOutcome = "NONE"
        v.AdjustedLongTarget = 0.0
        v.AdjustedShortTarget = 0.0
        v.TargetCapReasonLong = ""
        v.TargetCapReasonShort = ""
        v.KellyF = 0.0
        v.KellyFHalf = 0.0
        v.KellyFApplied = 0.0
        v.KellyPWin = 0.0
        v.KellyPMode = ""
        v.KellyCapped = False
        v.KellyContracts = 0
        v.KellyRiskUsd = 0.0
        v.Timestamp = New DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)

        ' SignalBreakdown intentionally left empty — each case populates
        ' explicitly via WithBreakdownItem so the harness exercises the
        ' For Each item loop in both renderers with realistic hit/miss
        ' patterns reflecting the case's scenario (per spec-author
        ' guidance, follow-on to commit 1). NO_TRADE-style cases may
        ' legitimately leave the list empty if no signals fired.
        Return v
    End Function

    Public Shared Function NeutralNorms() As DynamicNorms
        Dim n As New DynamicNorms()
        n.VolHighThreshold = 2.0
        n.VolMidThreshold = 1.3
        n.VolMean = 100.0
        n.VolStdDev = 20.0
        n.VWAPDevThreshold = 0.5
        n.ATRScaleFactor = 1.0
        n.ATRRef = 80.0
        n.IsLive = True
        Return n
    End Function

End Class
