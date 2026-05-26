' UI/MainForm_Render_Sections.vb
' Partial class: RenderOutput() entry point + indicator sections.
' Contains: DYNAMIC NORMS, REGIME, CORE SIGNALS, VWAP, BBW/TTM, EMA RIBBON,
'   MARKET STRUCTURE, OPEN INTEREST, ORDER FLOW, LIQUIDATIONS, MTF GATE,
'   FUNDING, SIGNAL BREAKDOWN table, verdict label update.
' Split from MainForm_Render.vb for maintainability.
'
' Depends on MainForm_Render_Header.vb for:
'   AppendRtf, AR, SectionHeader, Divider, RenderOutputHeader()

Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class MainForm

    Private Sub RenderOutput(r As IndicatorResults, v As VerdictResult,
                              norms As DynamicNorms, vwapWarmup As Integer,
                              lastTradePrice As Double)
        Dim rtb As RichTextBox = txtOutput
        rtb.Clear()

        Dim cfg As EngineSettings = SettingsLoader.Current
        Dim stopMult   As Double = cfg.Scoring.AtrStopMultiplier
        Dim targetMult As Double = cfg.Scoring.AtrTargetMultiplier
        Dim atrStop    As Double = r.ATR * norms.ATRScaleFactor * stopMult
        Dim atrTarget  As Double = r.ATR * norms.ATRScaleFactor * targetMult

        ' -- Header block (verdict / ATR levels / Kelly) ----------------------
        RenderOutputHeader(rtb, r, v, norms, cfg, lastTradePrice, atrStop, atrTarget)

        ' -- DYNAMIC NORMS ----------------------------------------------------
        Dim normMode As String = If(norms.IsLive, "LIVE", "STATIC FALLBACK")
        SectionHeader(rtb, "DYNAMIC NORMS  [" & normMode & "]")
        AR(rtb, "Vol threshold : ",
           String.Format("H:{0:F2}x  M:{1:F2}x  (mean={2:F4} BTC  s={3:F4})",
                          norms.VolHighThreshold, norms.VolMidThreshold, norms.VolMean, norms.VolStdDev))
        AR(rtb, "VWAP dev thr  : ", String.Format("+/-{0:F2}% (legacy ref)", norms.VWAPDevThreshold))
        AR(rtb, "ATR scale     : ",
           String.Format("{0:F2}x  (ATR={1:F2}  ref={2:F2})", norms.ATRScaleFactor, r.ATR, norms.ATRRef))

        ' -- REGIME -----------------------------------------------------------
        SectionHeader(rtb, "REGIME (5m): " & r.Regime)
        Dim regColour As Color = Theme.FG_PRIMARY
        Select Case r.Regime
            Case "TRENDING_UP"   : regColour = Theme.ACC_STRONG_LONG
            Case "TRENDING_DOWN" : regColour = Theme.ACC_SHORT
            Case "RANGE_BOUND"   : regColour = Theme.ACC_WARN
            Case Else            : regColour = Theme.FG_QUATERNARY
        End Select
        AppendRtf(rtb, "  ", Theme.FG_TERTIARY)
        AppendRtf(rtb, String.Format("ADX: {0:F1}  |  +DI: {1:F1}  |  -DI: {2:F1}",
                                      r.ADX, r.PlusDI, r.MinusDI) & Environment.NewLine, regColour)

        ' -- CORE SIGNALS (1m) ------------------------------------------------
        SectionHeader(rtb, "CORE SIGNALS (1m):")
        AppendRtf(rtb, "  ROC(9):       ", Theme.FG_TERTIARY)
        Dim rocColour As Color = If(r.ROC > 0, Theme.ACC_STRONG_LONG, If(r.ROC < 0, Theme.ACC_SHORT, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("{0:F3}  |  Slope: {1}", r.ROC, r.ROCSlope) & Environment.NewLine, rocColour)

        AppendRtf(rtb, "  RSI(9):       ", Theme.FG_TERTIARY)
        Dim rsiColour As Color = If(r.RSI > 70, Theme.ACC_SHORT, If(r.RSI < 30, Theme.ACC_STRONG_LONG, Theme.FG_PRIMARY))
        Dim rsiDiv As String = If(String.IsNullOrEmpty(r.RSIDivergence) OrElse r.RSIDivergence = "NONE",
                                   "", "  |  Div: " & r.RSIDivergence)
        AppendRtf(rtb, String.Format("{0:F1}", r.RSI) & rsiDiv & Environment.NewLine, rsiColour)

        Dim usdStr As String
        If r.CurrentVolumeUSD >= 1_000_000 Then
            usdStr = "$" & (r.CurrentVolumeUSD / 1_000_000).ToString("F2") & "M"
        ElseIf r.CurrentVolumeUSD >= 1_000 Then
            usdStr = "$" & (r.CurrentVolumeUSD / 1_000).ToString("F1") & "K"
        Else
            usdStr = "$" & r.CurrentVolumeUSD.ToString("F0")
        End If
        AppendRtf(rtb, "  Volume:       ", Theme.FG_TERTIARY)
        Dim volColour As Color = If(r.VolumeRatio >= 1.5, Theme.ACC_STRONG_LONG, If(r.VolumeRatio < 0.7, Theme.ACC_SHORT, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("{0:F4} BTC ({1})  |  vs SMA: {2:F2}x  |  SMA: {3:F4} BTC",
                                      r.CurrentVolume, usdStr, r.VolumeRatio, r.VolumeSMA9) & Environment.NewLine, volColour)

        ' -- VWAP -------------------------------------------------------------
        ' Active anchor mirrors GetSessionCandles in Indicators_Volatility: pre-s2 cutoff
        ' anchors at 00:00 UTC; post-cutoff anchors at the s2 boundary (default 13:30 UTC).
        ' Without this, the label was hardcoded to s2h:s2m regardless of which anchor
        ' the math was using — display-only mismatch caught by the 2026-05-12 dump audit.
        Dim s2h As Integer = cfg.Indicators.VWAP.Session2StartHour
        Dim s2m As Integer = cfg.Indicators.VWAP.Session2StartMinute
        Dim nowUtc As DateTime = DateTime.UtcNow
        Dim activeAnchor As String
        If nowUtc.Hour < s2h OrElse (nowUtc.Hour = s2h AndAlso nowUtc.Minute < s2m) Then
            activeAnchor = "00:00"
        Else
            activeAnchor = String.Format("{0:D2}:{1:D2}", s2h, s2m)
        End If
        Dim vwapWarmupTag As String = If(r.VWAPSessionCandles < vwapWarmup, "  [WARMUP]", "")
        SectionHeader(rtb, String.Format("VWAP (reset {0} UTC){1}:", activeAnchor, vwapWarmupTag))
        AppendRtf(rtb, "  Value:  ", Theme.FG_TERTIARY)
        Dim devColour As Color = If(Math.Abs(r.VWAPDevPct) > norms.VWAPDevThreshold, Theme.ACC_WARN, Theme.FG_PRIMARY)
        AppendRtf(rtb, String.Format("{0:F1}  |  Dev: {1:F3}%  |  Candles: {2}",
                                      r.VWAP, r.VWAPDevPct, r.VWAPSessionCandles) & Environment.NewLine, devColour)
        AppendRtf(rtb, "  s1 band: ", Theme.FG_TERTIARY)
        AppendRtf(rtb, String.Format("[{0:F1}, {1:F1}]  |  s2 band: [{2:F1}, {3:F1}]",
                                      r.VWAPSigma1Lower, r.VWAPSigma1Upper,
                                      r.VWAPSigma2Lower, r.VWAPSigma2Upper) & Environment.NewLine, Theme.FG_QUATERNARY)

        ' -- BBW / TTM SQUEEZE ------------------------------------------------
        SectionHeader(rtb, "BBW / TTM SQUEEZE:")
        AppendRtf(rtb, "  BBW: ", Theme.FG_TERTIARY)
        Dim sqColour As Color = If(r.SqueezeStatus = "ACTIVE", Theme.ACC_WARN,
                                   If(r.SqueezeStatus = "RELEASING", Theme.ACC_STRONG_LONG, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("{0:F3}  |  Status: {1}", r.BBW, r.SqueezeStatus) & Environment.NewLine, sqColour)
        AppendRtf(rtb, "  TTM: ", Theme.FG_TERTIARY)
        Dim ttmColour As Color = If(r.TTMDirection = "RISING", Theme.ACC_STRONG_LONG,
                                    If(r.TTMDirection = "FALLING", Theme.ACC_SHORT, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("Histogram={0:F2}  Dir={1}  Signal={2}",
                                      r.TTMHistogram, r.TTMDirection, r.TTMSignal) & Environment.NewLine, ttmColour)

        ' -- EMA RIBBON -------------------------------------------------------
        SectionHeader(rtb, "EMA RIBBON (1m):")
        AppendRtf(rtb, "  ", Theme.FG_TERTIARY)
        Dim emaColour As Color = If(r.EMAAlignment = "BULL", Theme.ACC_STRONG_LONG, If(r.EMAAlignment = "BEAR", Theme.ACC_SHORT, Theme.ACC_WARN))
        AppendRtf(rtb, String.Format("9: {0:F1}  |  21: {1:F1}  |  50: {2:F1}  |  Align: {3}",
                                      r.EMA9, r.EMA21, r.EMA50, r.EMAAlignment) & Environment.NewLine, emaColour)
        AppendRtf(rtb, "  5m EMA200: ", Theme.FG_TERTIARY)
        Dim ema200Colour As Color = If(r.PriceVsEMA200 = "ABOVE", Theme.ACC_STRONG_LONG, Theme.ACC_SHORT)
        AppendRtf(rtb, String.Format("{0:F1}  |  Price: {1}", r.EMA200_5m, r.PriceVsEMA200) & Environment.NewLine, ema200Colour)

        ' -- MARKET STRUCTURE -------------------------------------------------
        SectionHeader(rtb, "MARKET STRUCTURE:")
        AppendRtf(rtb, "  Donchian(20): ", Theme.FG_TERTIARY)
        Dim donchColour As Color = If(r.DonchianSignal = "LONG", Theme.ACC_STRONG_LONG, If(r.DonchianSignal = "SHORT", Theme.ACC_SHORT, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("Upper={0:F1}  Lower={1:F1}  |  Signal: {2}",
                                      r.DonchianUpper, r.DonchianLower, r.DonchianSignal) & Environment.NewLine, donchColour)
        AppendRtf(rtb, "  OBV: ", Theme.FG_TERTIARY)
        AppendRtf(rtb, String.Format("Trend={0}  |  Div={1}", r.OBVTrend, r.OBVDivergence) & Environment.NewLine, Theme.FG_PRIMARY)

        Dim vpfrColour As Color = If(r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL", Theme.ACC_STRONG_LONG,
                                     If(r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR", Theme.ACC_SHORT, Theme.FG_QUATERNARY))
        AppendRtf(rtb, "  VPFR-lite: ", Theme.FG_TERTIARY)
        AppendRtf(rtb, String.Format("POC:{0:F1}  |  {1}  |  HVN@POC:{2}",
                                      r.VPFRPoc, r.VPFRSignal,
                                      If(r.VPFRHVNearPoc, "YES", "NO")) & Environment.NewLine, vpfrColour)

        AppendRtf(rtb, "  Value Area: ", Theme.FG_TERTIARY)
        Dim vaColour As Color = If(r.VPFRValueAreaSignal = "INSIDE_VA", Theme.FG_PRIMARY,
                                   If(r.VPFRValueAreaSignal = "ABOVE_VAH", Theme.ACC_STRONG_LONG, Theme.ACC_SHORT))
        AppendRtf(rtb, String.Format("VAH:{0:F1}  |  VAL:{1:F1}  |  {2}",
                                      r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal) & Environment.NewLine, vaColour)

        AppendRtf(rtb, "  HVN walls: ", Theme.FG_TERTIARY)
        Dim hvnAboveStr As String = If(r.VPFRNearestHvnAbove > 0, r.VPFRNearestHvnAbove.ToString("F1"), "—")
        Dim hvnBelowStr As String = If(r.VPFRNearestHvnBelow > 0, r.VPFRNearestHvnBelow.ToString("F1"), "—")
        Dim lvnAboveStr As String = If(r.VPFRNearestLvnAbove > 0, r.VPFRNearestLvnAbove.ToString("F1"), "—")
        Dim lvnBelowStr As String = If(r.VPFRNearestLvnBelow > 0, r.VPFRNearestLvnBelow.ToString("F1"), "—")
        AppendRtf(rtb, String.Format("Above:{0}  Below:{1}  |  LVN: ^{2} v{3}",
                                      hvnAboveStr, hvnBelowStr, lvnAboveStr, lvnBelowStr) & Environment.NewLine, Theme.FG_QUATERNARY)

        ' -- D1: Trend Structure ----------------------------------------------
        AppendRtf(rtb, "  Trend Struct: ", Theme.FG_TERTIARY)
        Dim tsColour As Color
        Select Case r.TrendStructure
            Case TrendStructure.UPTREND     : tsColour = Theme.ACC_STRONG_LONG
            Case TrendStructure.DOWNTREND   : tsColour = Theme.ACC_SHORT
            Case TrendStructure.EXPANSION   : tsColour = Theme.ACC_WARN
            Case TrendStructure.CONTRACTION : tsColour = Color.FromArgb(80, 200, 210)
            Case Else                       : tsColour = Theme.FG_QUATERNARY
        End Select
        Dim tsDetail As String
        If r.LastTwoHighs5m.Newer > 0 AndAlso r.LastTwoLows5m.Newer > 0 Then
            Select Case r.TrendStructure
                Case TrendStructure.UPTREND
                    tsDetail = String.Format("  (HH {0:F1}>{1:F1} | HL {2:F1}>{3:F1})",
                        r.LastTwoHighs5m.Newer, r.LastTwoHighs5m.Older,
                        r.LastTwoLows5m.Newer,  r.LastTwoLows5m.Older)
                Case TrendStructure.DOWNTREND
                    tsDetail = String.Format("  (LH {0:F1}<{1:F1} | LL {2:F1}<{3:F1})",
                        r.LastTwoHighs5m.Newer, r.LastTwoHighs5m.Older,
                        r.LastTwoLows5m.Newer,  r.LastTwoLows5m.Older)
                Case TrendStructure.EXPANSION
                    tsDetail = String.Format("  (HH {0:F1}>{1:F1} | LL {2:F1}<{3:F1})",
                        r.LastTwoHighs5m.Newer, r.LastTwoHighs5m.Older,
                        r.LastTwoLows5m.Newer,  r.LastTwoLows5m.Older)
                Case TrendStructure.CONTRACTION
                    tsDetail = String.Format("  (LH {0:F1}<{1:F1} | HL {2:F1}>{3:F1})",
                        r.LastTwoHighs5m.Newer, r.LastTwoHighs5m.Older,
                        r.LastTwoLows5m.Newer,  r.LastTwoLows5m.Older)
                Case Else
                    tsDetail = ""
            End Select
        Else
            tsDetail = "  (insufficient pivot data)"
        End If
        AppendRtf(rtb, r.TrendStructure.ToString() & tsDetail & Environment.NewLine, tsColour)

        ' -- D2: Best Volume Pivot --------------------------------------------
        AppendRtf(rtb, "  Best Vol Pivot 5m: ", Theme.FG_TERTIARY)
        If r.BestPivotByVolume5m > 0 Then
            AppendRtf(rtb, String.Format("{0} {1:F1}  (vol×{2:F1} vs avg pivot)",
                If(r.BestPivotIsHigh5m, "HIGH", "LOW"),
                r.BestPivotByVolume5m, r.BestPivotVolumeRatio5m) & Environment.NewLine,
                Color.FromArgb(80, 200, 210))
        Else
            AppendRtf(rtb, "—  (insufficient pivots)" & Environment.NewLine, Theme.FG_QUATERNARY)
        End If

        ' -- OPEN INTEREST ----------------------------------------------------
        SectionHeader(rtb, "OPEN INTEREST:")
        AppendRtf(rtb, "  OI: ", Theme.FG_TERTIARY)
        Dim oiColour As Color = If(r.OISignal = "NEW LONGS" OrElse r.OISignal = "COVERING", Theme.ACC_STRONG_LONG,
                                   If(r.OISignal = "NEW SHORTS" OrElse r.OISignal = "CAPITULATION", Theme.ACC_SHORT, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("{0:F0}  |  d15m: {1:F3}%  |  d60m: {2:F3}%  |  Signal: {3}",
                                      r.OI_Current, r.OIChange15m, r.OIChange60m, r.OISignal) & Environment.NewLine, oiColour)

        ' -- ORDER FLOW -------------------------------------------------------
        SectionHeader(rtb, "ORDER FLOW:")
        AppendRtf(rtb, "  OFI Ratio: ", Theme.FG_TERTIARY)
        Dim ofiColour As Color = If(r.OFIRatio > 1.2, Theme.ACC_STRONG_LONG, If(r.OFIRatio < 0.8, Theme.ACC_SHORT, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("{0:F2}  |  Bid Vol: {1:F0}  |  Ask Vol: {2:F0}  |  {3}  |  Mom: {4}",
                                      r.OFIRatio, r.OFIBidVol, r.OFIAskVol, r.OFISignal,
                                      r.OFIMomentum) & Environment.NewLine, ofiColour)

        AppendRtf(rtb, "  Spread:    ", Theme.FG_TERTIARY)
        Dim spreadColour As Color = If(r.SpreadStatus = "WIDE", Theme.ACC_SHORT,
                                       If(r.SpreadStatus = "TIGHT", Theme.ACC_STRONG_LONG, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("{0:F2} bps  |  {1}",
                                      r.SpreadBps, r.SpreadStatus) & Environment.NewLine, spreadColour)

        AppendRtf(rtb, "  CVD:       ", Theme.FG_TERTIARY)
        Dim cvdColour As Color = If(r.CVDSlope = "RISING", Theme.ACC_STRONG_LONG, If(r.CVDSlope = "FALLING", Theme.ACC_SHORT, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("Net:{0:F0}  |  Slope:{1}  |  Div:{2}",
                                      r.CVDValue, r.CVDSlope, r.CVDDivergence) & Environment.NewLine, cvdColour)

        AppendRtf(rtb, "  TFI:       ", Theme.FG_TERTIARY)
        Dim tfiColour As Color = If(r.TFISignal = "BUY PRESSURE", Theme.ACC_STRONG_LONG,
                                    If(r.TFISignal = "SELL PRESSURE", Theme.ACC_SHORT, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("{0:F3}  |  {1}",
                                      r.TFIValue, r.TFISignal) & Environment.NewLine, tfiColour)

        AppendRtf(rtb, "  MicroCVD:  ", Theme.FG_TERTIARY)
        Dim microColour As Color
        Select Case r.MicroCVDSignal
            Case "BULL_ACCEL" : microColour = Theme.ACC_STRONG_LONG
            Case "BEAR_ACCEL" : microColour = Theme.ACC_SHORT
            Case "BULL_DECEL" : microColour = Color.FromArgb(120, 200, 120)
            Case "BEAR_DECEL" : microColour = Color.FromArgb(220, 130, 130)
            Case Else         : microColour = Theme.FG_PRIMARY
        End Select
        AppendRtf(rtb, String.Format("E:{0:F0}  M:{1:F0}  L:{2:F0}  |  {3}  |  {4}",
                                      r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                                      r.MicroCVDMomentum, r.MicroCVDSignal) & Environment.NewLine, microColour)

        ' -- LIQUIDATIONS -----------------------------------------------------
        SectionHeader(rtb, "LIQUIDATIONS:")
        AppendRtf(rtb, "  ", Theme.FG_TERTIARY)
        Dim liqColour As Color = If(r.LiqSignal <> "NONE", Theme.ACC_WARN, Theme.FG_QUATERNARY)
        AppendRtf(rtb, String.Format("Long: {0:F0}  |  Short: {1:F0}  |  Signal: {2}",
                                      r.LiqLongSize, r.LiqShortSize, r.LiqSignal) & Environment.NewLine, liqColour)

        ' -- MTF GATE (15m) ---------------------------------------------------
        SectionHeader(rtb, "MTF GATE (15m): " & If(r.MTFGatePass, "PASS", "BLOCK"))
        Dim mtfColour As Color = If(r.MTFGatePass, Theme.ACC_STRONG_LONG, Theme.ACC_SHORT)
        AppendRtf(rtb, "  15m Trend: ", Theme.FG_TERTIARY)
        AppendRtf(rtb, String.Format("{0}  |  ADX: {1:F1}  |  EMA: {2}",
                                      r.MTF15mTrend, r.MTF15mADX, r.MTF15mEMAAlignment) & Environment.NewLine, mtfColour)
        AppendRtf(rtb, "  Reason: ", Theme.FG_TERTIARY)
        AppendRtf(rtb, r.MTFGateReason & Environment.NewLine, Theme.FG_QUATERNARY)

        ' -- FUNDING ----------------------------------------------------------
        SectionHeader(rtb, "FUNDING:")
        AppendRtf(rtb, "  Rate: ", Theme.FG_TERTIARY)
        Dim fundColour As Color = If(r.FundingBias.Contains("HEAVILY"), Theme.ACC_SHORT,
                                     If(r.FundingBias = "NEUTRAL", Theme.FG_PRIMARY, Theme.ACC_WARN))
        Dim fundDisplayRate As Double = If(Math.Abs(r.FundingRate) < 0.00000001, 0.0, r.FundingRate)
        AppendRtf(rtb, String.Format("{0:F4}%  |  {1}", fundDisplayRate * 100, r.FundingBias) & Environment.NewLine, fundColour)
        AppendRtf(rtb, "  Momentum: ", Theme.FG_TERTIARY)
        Dim fundMomColour As Color = If(r.FundingMomentum = "RISING", Theme.ACC_WARN,
                                        If(r.FundingMomentum = "FALLING", Theme.ACC_STRONG_LONG, Theme.FG_PRIMARY))
        AppendRtf(rtb, String.Format("{0}  |  Enabled: {1}  |  Soften: +{2}  |  Amplify: -{3}",
                                      r.FundingMomentum,
                                      If(cfg.Indicators.Funding.MomentumEnabled, "YES", "NO"),
                                      cfg.Indicators.Funding.MomentumSoften,
                                      cfg.Indicators.Funding.MomentumAmplify) & Environment.NewLine, fundMomColour)

        ' -- SIGNAL BREAKDOWN -------------------------------------------------
        AppendRtf(rtb, Environment.NewLine, Theme.BORDER_CARD)
        Divider(rtb)
        AppendRtf(rtb, "  SIGNAL BREAKDOWN" & Environment.NewLine, Theme.FG_SECONDARY, bold:=True)
        Divider(rtb)
        AppendRtf(rtb, String.Format("  {0,-18}  {1,5}  {2,6}  {3}",
                                      "Signal", "Long", "Short", "Note") & Environment.NewLine, Theme.FG_TERTIARY)
        AppendRtf(rtb, "  " & New String("-"c, 70) & Environment.NewLine, Theme.BORDER_CARD)
        For Each item In v.SignalBreakdown
            Dim lMark As String = If(item.LongHit, "[L]", "   ")
            Dim sMark As String = If(item.ShortHit, "[S]", "   ")
            Dim hitColour As Color = If(item.LongHit OrElse item.ShortHit, Theme.ACC_INFO, Theme.FG_QUATERNARY)
            AppendRtf(rtb, String.Format("  {0,-18}  {1,5}  {2,6}  {3}",
                                          item.Label, lMark, sMark, item.Note) & Environment.NewLine, hitColour)
        Next
        AppendRtf(rtb, "  " & New String("-"c, 70) & Environment.NewLine, Theme.BORDER_CARD)
        AppendRtf(rtb, String.Format("  {0,-18}  {1,5:F0}  {2,6:F0}",
                                      "TOTAL", CDbl(v.LongScore), CDbl(v.ShortScore)) & Environment.NewLine,
                  Theme.FG_PRIMARY, bold:=True)

        rtb.SelectionStart = 0
        rtb.ScrollToCaret()

        ' -- Verdict label update ---------------------------------------------
        Dim bg As Color
        Select Case v.Verdict
            Case "STRONG LONG"  : bg = Color.FromArgb(0, 180, 90)
            Case "LONG"         : bg = Color.FromArgb(0, 140, 60)
            Case "WEAK LONG"    : bg = Color.FromArgb(60, 160, 60)
            Case "STRONG SHORT" : bg = Color.FromArgb(200, 40, 40)
            Case "SHORT"        : bg = Color.FromArgb(180, 30, 30)
            Case "WEAK SHORT"   : bg = Color.FromArgb(180, 80, 80)
            Case Else           : bg = Color.DimGray
        End Select
        lblVerdict.BackColor = bg
        lblVerdict.Text = v.Verdict & "  [" & v.Confidence & "]"

        ' P5a — AnalysisOutputDump.Append moved to RunAnalysisAsync (driven by
        ' BuildPlaintextSnapshot, not txtOutput.Text). This RenderOutput method
        ' still runs in P5a so the verification dump card (txtOutput) keeps
        ' receiving the legacy RTF output for the trader-side parity window.
        ' P5b deletes this whole method plus MainForm_Render_Header.vb.
    End Sub

End Class
