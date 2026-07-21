' UI/MainForm_PlaintextSnapshot.vb
'
' P5a — produces the markdown-style text body that replaces the legacy
' txtOutput.Text source of AnalysisOutputDump.Append. Shape must match
' the pre-P5 dump file modulo timestamp and per-run market values so
' existing dump-readers (auto-tweaker / future log consumers) continue
' to parse.
'
' Walks every block the legacy render path emitted (verified 55/55 by the
' P5-test parity harness before both were deleted in P5b), in this order:
'   1. Header block (was RenderOutputHeader)
'      VERDICT / CONTEXT / CONFIDENCE / REGIME ANCHOR / SCORE / TIME /
'      LAST TRANSACTED PRICE / HOLD / EXIT / ATR ENTRY LEVELS / structural
'      rows / KELLY SIZING
'   2. Section blocks (was RenderOutput)
'      DYNAMIC NORMS / REGIME / CORE SIGNALS / VWAP / BBW/TTM /
'      EMA RIBBON / MARKET STRUCTURE / OI / ORDER FLOW / LIQUIDATIONS /
'      MTF GATE / FUNDING / SIGNAL BREAKDOWN
'
' Colour is dropped (plaintext). Spacing, formatters, conditional gates,
' and sub-tick CAPPED suppression are reproduced 1:1.
'
' Called from MainForm_Analysis.RunAnalysisAsync BEFORE the card binds (the
' inline CalcKellySizing populates v.Kelly* for BindCardKelly); the returned
' string is passed as `renderedText` to AnalysisOutputDump.Append after
' UpdatePerformanceLabels so the perf-strip line reflects the current run.

Imports System.Text

Partial Public Class MainForm

    Private Const SNAPSHOT_DIVIDER As String = "==========================================================="
    Private Const SNAPSHOT_ROW_DIVIDER_LEN As Integer = 70

    Friend Function BuildPlaintextSnapshot(
            v As VerdictResult,
            r As IndicatorResults,
            norms As DynamicNorms,
            cfg As EngineSettings,
            vwapWarmup As Integer,
            lastTradePrice As Double) As String

        Dim sb As New StringBuilder()

        Dim stopMult   As Double = cfg.Scoring.AtrStopMultiplier
        Dim targetMult As Double = cfg.Scoring.AtrTargetMultiplier
        ' D2 (S-1): linear ATR distances (no ATRScaleFactor) — mirrors RenderOutputHeader.
        Dim atrStop    As Double = r.ATR * stopMult
        Dim atrTarget  As Double = r.ATR * targetMult

        AppendHeaderBlock(sb, v, r, norms, cfg, lastTradePrice, atrStop, atrTarget)
        AppendDynamicNorms(sb, r, norms)
        AppendRegime(sb, r)
        AppendCoreSignals(sb, r)
        AppendVwap(sb, r, norms, cfg, vwapWarmup)
        AppendBbwTtm(sb, r)
        AppendEmaRibbon(sb, r)
        AppendMarketStructure(sb, r)
        AppendOpenInterest(sb, r)
        AppendOrderFlow(sb, r)
        AppendLiquidations(sb, r)
        AppendMtfGate(sb, r, v)
        AppendFunding(sb, r, cfg)
        AppendSignalBreakdown(sb, v)

        Return sb.ToString()
    End Function

    ' -----------------------------------------------------------------------
    ' Header block — mirrors MainForm_Render_Header.RenderOutputHeader.
    ' -----------------------------------------------------------------------
    Private Sub AppendHeaderBlock(sb As StringBuilder,
                                   v As VerdictResult,
                                   r As IndicatorResults,
                                   norms As DynamicNorms,
                                   cfg As EngineSettings,
                                   lastTradePrice As Double,
                                   atrStop As Double,
                                   atrTarget As Double)
        Dim rawTs As DateTime = If(v.Timestamp <> DateTime.MinValue, v.Timestamp, DateTime.Now)
        Dim offset As TimeSpan = TimeZoneInfo.Local.GetUtcOffset(rawTs)
        Dim tzSign As String = If(offset >= TimeSpan.Zero, "+", "-")
        Dim ts As String = rawTs.ToString("yyyy-MM-dd HH:mm:ss") & " UTC" & tzSign &
                           CInt(Math.Floor(Math.Abs(offset.TotalHours))).ToString()
        Dim stopMult   As Double = cfg.Scoring.AtrStopMultiplier
        Dim targetMult As Double = cfg.Scoring.AtrTargetMultiplier
        Dim longStop   As Double = r.CurrentPrice - atrStop
        Dim longTarget As Double = r.CurrentPrice + atrTarget
        Dim shortStop  As Double = r.CurrentPrice + atrStop
        Dim shortTarget As Double = r.CurrentPrice - atrTarget
        Dim rrRatio As String = String.Format("1:{0:F1}", targetMult / stopMult)

        sb.AppendLine(SNAPSHOT_DIVIDER)
        ' [F12 / E3a — 2026-07-21] Middle band renders as "MEDIUM LONG" / "MEDIUM SHORT"
        ' on the display surfaces; stored/wire strings stay bare LONG/SHORT (CSV, payload,
        ' eval cache, string-matching sites all untouched). Card renders through the same
        ' helper — see BindCardVerdict in MainForm_Render_Cards.vb. Parity rule
        ' deliberately diverged on the two render surfaces (cap-reason precedent).
        sb.AppendLine("  VERDICT:    " & VerdictResult.FormatVerdictForDisplay(v.Verdict))

        If v.VerdictContext <> "" Then
            sb.AppendLine("  CONTEXT:    " & v.VerdictContext)
        End If

        sb.AppendLine("  CONFIDENCE: " & v.Confidence)

        ' Regime-anchor warning — display-only on STRONG verdicts when price is
        ' displaced >3.0× ATR from the 5m EMA(200) anchor in the opposite direction.
        Const REGIME_ANCHOR_ATR_THRESHOLD As Double = 3.0
        If r.ATR > 0 AndAlso r.EMA200_5m > 0 Then
            Dim atrUnits As Double = (r.CurrentPrice - r.EMA200_5m) / r.ATR
            Dim warning As String = ""
            If v.Verdict.StartsWith("STRONG LONG") AndAlso atrUnits < -REGIME_ANCHOR_ATR_THRESHOLD Then
                warning = String.Format("price {0:F1}× ATR below 5m EMA(200) — STRONG LONG fighting intermediate bear",
                                         Math.Abs(atrUnits))
            ElseIf v.Verdict.StartsWith("STRONG SHORT") AndAlso atrUnits > REGIME_ANCHOR_ATR_THRESHOLD Then
                warning = String.Format("price {0:F1}× ATR above 5m EMA(200) — STRONG SHORT fighting intermediate bull",
                                         atrUnits)
            End If
            If warning <> "" Then
                sb.AppendLine("  REGIME ANCHOR:  " & ChrW(&H26A0) & " " & warning)
            End If
        End If

        Dim maxScore As Integer = v.MaxScore
        If v.RegimePenalty > 0 Then
            sb.AppendLine(String.Format("  SCORE:      Long {0}/{2} (eff.{1})  |  Short {3}/{2} (eff.{4})  |  TRANSITIONAL penalty: -{5}",
                                         v.LongScore, v.EffectiveLongScore, maxScore,
                                         v.ShortScore, v.EffectiveShortScore, v.RegimePenalty))
        Else
            sb.AppendLine(String.Format("  SCORE:      Long {0}/{1}  |  Short {2}/{1}",
                                         v.LongScore, maxScore, v.ShortScore))
        End If

        sb.AppendLine("  TIME:       " & ts)
        sb.AppendLine(SNAPSHOT_DIVIDER)

        sb.AppendLine("  LAST TRANSACTED PRICE:  " &
                      If(lastTradePrice > 0, lastTradePrice.ToString("F1"), "N/A"))

        If v.HoldStatus <> "N/A -- no open position" Then
            sb.AppendLine("  HOLD / EXIT: " & v.HoldStatus)
        End If

        ' P5b: the engine's ONLY CalcKellySizing call site. RunAnalysisAsync
        ' builds this snapshot BEFORE the card binds precisely so this call
        ' populates v.Kelly* for BindCardKelly — preserve that ordering when
        ' refactoring, or the KELLY card renders zeros.
        ScoringEngine.CalcKellySizing(v, atrStop, r.CurrentPrice, cfg)

        ' ATR ENTRY LEVELS header (SectionHeader emits a leading blank line).
        ' D2 (S-1): displays AvgATR/CurrATR sizing factor — mirrors RenderOutputHeader.
        Dim sizeMult As Double = If(r.ATR > 0, norms.ATRRef / r.ATR, 1.0)
        sb.AppendLine()

        ' [B4b placed-geometry] The rendered levels come from the ONE shared arbitration
        ' (SignalEmitter.ComputeSideLevels — the same function the bridge payload, the
        ' CSV Placed* columns, and the card read). structural_levels.enabled=false ⇒
        ' StopReason is Nothing ⇒ the legacy branches below render byte-identical v50.
        Dim lvLong As SideLevels = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
        Dim lvShort As SideLevels = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=False)
        Dim structuralMode As Boolean = lvLong.StopReason IsNot Nothing
        ' Session-resolved fallback-target multiplier (DG3: LONDON 2.0 / ASIA 1.25) —
        ' returns the plain cfg multiplier when structural levels are disabled.
        Dim headerTargetMult As Double = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, r.SessionUtcHour)

        ' [v36] EXEC {res}m surfaces the execution resolution (display-parity with the
        ' card's _atrSubHeader). At res=1 reads "EXEC 1m" — NY unchanged in content.
        ' [B4b] target mult renders F2 (1.75 must not round to 1.8); stop stays F1.
        sb.AppendLine(String.Format("ATR ENTRY LEVELS  (ATR {0:F2}  size ×{1:F2} | {2:F1}x stop / {3:F2}x target | EXEC {4}m)",
                                     r.ATR, sizeMult, stopMult, headerTargetMult, r.ExecResolution))

        Dim capNoiseFloor As Double = Math.Max(0.5, r.ATR * 0.02)

        ' Long row
        If structuralMode Then
            AppendPlacedAtrRow(sb, "Long:   ", lvLong)
        ElseIf v.AdjustedLongTarget > 0 Then
            Dim longCapAdjustment As Double = Math.Abs(longTarget - v.AdjustedLongTarget)
            If longCapAdjustment < capNoiseFloor Then
                sb.AppendLine(String.Format("  Long:   Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                             longStop, r.CurrentPrice, v.AdjustedLongTarget, rrRatio, atrStop, atrTarget))
            Else
                sb.AppendLine(String.Format("  Long:   Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1} --> {3:F1}  [{4}]",
                                             longStop, r.CurrentPrice, longTarget, v.AdjustedLongTarget, v.TargetCapReasonLong))
            End If
        Else
            sb.AppendLine(String.Format("  Long:   Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                         longStop, r.CurrentPrice, longTarget, rrRatio, atrStop, atrTarget))
        End If

        ' Long structural row
        If r.SwingTargetLong > 0 AndAlso r.SwingStopLong > 0 Then
            Dim swingRisk   As Double = r.CurrentPrice - r.SwingStopLong
            Dim swingReward As Double = r.SwingTargetLong - r.CurrentPrice
            Dim swingRR     As String = FormatRR(swingReward, swingRisk)
            sb.AppendLine(String.Format("  Long structural:  Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                         r.SwingStopLong, r.CurrentPrice, r.SwingTargetLong, swingRR, swingRisk, swingReward))
        ElseIf r.SwingTargetLong > 0 Then
            sb.AppendLine(String.Format("  Long structural:  Target {0,9:F1}  (stop unset: no swing low below entry within lookback)",
                                         r.SwingTargetLong))
        ElseIf r.SwingStopLong > 0 Then
            sb.AppendLine(String.Format("  Long structural:  Stop {0,9:F1}  (target unset: no swing high above entry within lookback)",
                                         r.SwingStopLong))
        End If

        ' Short row
        If structuralMode Then
            AppendPlacedAtrRow(sb, "Short:  ", lvShort)
        ElseIf v.AdjustedShortTarget > 0 Then
            Dim shortCapAdjustment As Double = Math.Abs(shortTarget - v.AdjustedShortTarget)
            If shortCapAdjustment < capNoiseFloor Then
                sb.AppendLine(String.Format("  Short:  Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                             shortStop, r.CurrentPrice, v.AdjustedShortTarget, rrRatio, atrStop, atrTarget))
            Else
                sb.AppendLine(String.Format("  Short:  Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1} --> {3:F1}  [{4}]",
                                             shortStop, r.CurrentPrice, shortTarget, v.AdjustedShortTarget, v.TargetCapReasonShort))
            End If
        Else
            sb.AppendLine(String.Format("  Short:  Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                         shortStop, r.CurrentPrice, shortTarget, rrRatio, atrStop, atrTarget))
        End If

        ' Short structural row
        If r.SwingTargetShort > 0 AndAlso r.SwingStopShort > 0 Then
            Dim swingRisk   As Double = r.SwingStopShort - r.CurrentPrice
            Dim swingReward As Double = r.CurrentPrice - r.SwingTargetShort
            Dim swingRR     As String = FormatRR(swingReward, swingRisk)
            sb.AppendLine(String.Format("  Short structural: Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                         r.SwingStopShort, r.CurrentPrice, r.SwingTargetShort, swingRR, swingRisk, swingReward))
        ElseIf r.SwingTargetShort > 0 Then
            sb.AppendLine(String.Format("  Short structural: Target {0,9:F1}  (stop unset: no swing high above entry within lookback)",
                                         r.SwingTargetShort))
        ElseIf r.SwingStopShort > 0 Then
            sb.AppendLine(String.Format("  Short structural: Stop {0,9:F1}  (target unset: no swing low below entry within lookback)",
                                         r.SwingStopShort))
        End If

        ' KELLY SIZING block — suppressed when KellyPWin = 0.
        If v.KellyPWin > 0 Then
            Dim isNoTradeBias As Boolean = v.Verdict.StartsWith("NO TRADE")
            Dim capTag As String = If(v.KellyCapped, "  [CAPPED]", "")
            ' Legacy emits an unconditional blank line before the KELLY header
            ' (AppendRtf with Environment.NewLine, not a SectionHeader call).
            sb.AppendLine()
            If isNoTradeBias Then
                sb.AppendLine(String.Format("KELLY SIZING  [BIAS ONLY — NO TRADE]{0}", capTag))
            Else
                sb.AppendLine(String.Format("KELLY SIZING{0}", capTag))
            End If
            sb.AppendLine("  Advisory (ATR-basis) — R:R uses ATR multiples, not structural targets.")
            sb.AppendLine("  Treat as directional bias indicator only.")
            sb.AppendLine(String.Format("  p(win):   {0:P1}", v.KellyPWin))
            sb.AppendLine(String.Format("  f* / Half-Kelly:  {0:P2}  /  {1:P2}", v.KellyF, v.KellyFHalf))
            sb.AppendLine(String.Format("  Applied fraction: {0:P2}", v.KellyFApplied))
            sb.AppendLine(String.Format("  Risk $:    ${0:F2}", v.KellyRiskUsd))
            Dim contractStr As String
            If isNoTradeBias Then
                contractStr = If(v.KellyContracts >= 1,
                                 String.Format("{0} {1}  (not a trade signal)", v.KellyContracts.ToString(), If(v.KellyContracts = 1, "contract", "contracts")),
                                 "< 1 contract  (bias only; not a trade signal)")
                sb.AppendLine("  Lean: " & contractStr)
            Else
                contractStr = If(v.KellyContracts >= 1,
                                 v.KellyContracts.ToString() & " " & If(v.KellyContracts = 1, "contract", "contracts"),
                                 "< 1 contract  (stop too wide for min size)")
                sb.AppendLine("  Contracts: " & contractStr)
            End If

            ' D1: notional + implied leverage sanity line (mirrors RenderOutputHeader).
            If v.KellyContracts >= 1 Then
                Dim notional As Double = v.KellyContracts * cfg.Kelly.ContractFaceUsd
                Dim lev As Double = If(cfg.Kelly.AccountSizeUsd > 0, notional / cfg.Kelly.AccountSizeUsd, 0.0)
                Dim levTag As String = If(v.KellyLevCapped, "  [LEV CAPPED]", "")
                sb.AppendLine(String.Format("  Notional:  ≈ ${0:N0} · {1:F1}× lev{2}", notional, lev, levTag))
            End If
        End If
    End Sub

    ' [B4b placed-geometry] One structural-first ATR row. Placed stop always carries its
    ' source label (SWING_STOP / STOP_CLAMPED / FALLBACK_ATR). A structural-placed target
    ' (post-noise-suppression) renders the legacy arrow form "fallback --> placed [reason]";
    ' fallback / noise-suppressed rows carry the target source label plus the TRUE placed
    ' R:R (computed from the placed stop/target distances, not the multiplier ratio —
    ' the placed stop may be structural). label is the padded "Long:   " / "Short:  ".
    Private Sub AppendPlacedAtrRow(sb As StringBuilder, label As String, lv As SideLevels)
        Dim risk As Double = Math.Abs(lv.Entry - lv.StopPx)
        If lv.Capped Then
            sb.AppendLine(String.Format("  {0}Stop {1,9:F1} [{2}]  |  Entry {3,9:F1}  |  Target {4,9:F1} --> {5:F1}  [{6}]",
                                         label, lv.StopPx, lv.StopReason, lv.Entry, lv.RawTarget, lv.Target, lv.Reason))
        Else
            Dim rwd As Double = Math.Abs(lv.Target - lv.Entry)
            sb.AppendLine(String.Format("  {0}Stop {1,9:F1} [{2}]  |  Entry {3,9:F1}  |  Target {4,9:F1} [{5}]    R:R {6}  (risk {7:F1} / rwd {8:F1})",
                                         label, lv.StopPx, lv.StopReason, lv.Entry, lv.Target, lv.TargetReason,
                                         FormatRR(rwd, risk), risk, rwd))
        End If
    End Sub

    Private Sub AppendDynamicNorms(sb As StringBuilder, r As IndicatorResults, norms As DynamicNorms)
        Dim normMode As String = If(norms.IsLive, "LIVE", "STATIC FALLBACK")
        sb.AppendLine()
        sb.AppendLine("DYNAMIC NORMS  [" & normMode & "]")
        sb.AppendLine(String.Format("  Vol threshold : H:{0:F2}x  M:{1:F2}x  (mean={2:F4} BTC  s={3:F4})",
                                     norms.VolHighThreshold, norms.VolMidThreshold,
                                     norms.VolMean, norms.VolStdDev))
        sb.AppendLine(String.Format("  VWAP dev thr  : +/-{0:F2}% (legacy ref)", norms.VWAPDevThreshold))
        sb.AppendLine(String.Format("  ATR ratio     : {0:F2}x  (ATR={1:F2}  ref={2:F2})",
                                     norms.ATRScaleFactor, r.ATR, norms.ATRRef))
    End Sub

    Private Sub AppendRegime(sb As StringBuilder, r As IndicatorResults)
        sb.AppendLine()
        sb.AppendLine("REGIME (5m): " & r.Regime)
        sb.AppendLine(String.Format("  ADX: {0:F1}  |  +DI: {1:F1}  |  -DI: {2:F1}",
                                     r.ADX, r.PlusDI, r.MinusDI))
    End Sub

    Private Sub AppendCoreSignals(sb As StringBuilder, r As IndicatorResults)
        sb.AppendLine()
        sb.AppendLine("CORE SIGNALS (1m):")
        sb.AppendLine(String.Format("  ROC(9):       {0:F3}  |  Slope: {1}", r.ROC, r.ROCSlope))

        Dim rsiDiv As String = If(String.IsNullOrEmpty(r.RSIDivergence) OrElse r.RSIDivergence = "NONE",
                                   "", "  |  Div: " & r.RSIDivergence)
        sb.AppendLine(String.Format("  RSI(9):       {0:F1}{1}", r.RSI, rsiDiv))

        Dim usdStr As String
        If r.CurrentVolumeUSD >= 1_000_000 Then
            usdStr = "$" & (r.CurrentVolumeUSD / 1_000_000).ToString("F2") & "M"
        ElseIf r.CurrentVolumeUSD >= 1_000 Then
            usdStr = "$" & (r.CurrentVolumeUSD / 1_000).ToString("F1") & "K"
        Else
            usdStr = "$" & r.CurrentVolumeUSD.ToString("F0")
        End If
        sb.AppendLine(String.Format("  Volume:       {0:F4} BTC ({1})  |  vs SMA: {2:F2}x  |  SMA: {3:F4} BTC",
                                     r.CurrentVolume, usdStr, r.VolumeRatio, r.VolumeSMA9))
    End Sub

    Private Sub AppendVwap(sb As StringBuilder, r As IndicatorResults,
                            norms As DynamicNorms, cfg As EngineSettings, vwapWarmup As Integer)
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

        sb.AppendLine()
        sb.AppendLine(String.Format("VWAP (reset {0} UTC){1}:", activeAnchor, vwapWarmupTag))
        sb.AppendLine(String.Format("  Value:  {0:F1}  |  Dev: {1:F3}%  |  Candles: {2}",
                                     r.VWAP, r.VWAPDevPct, r.VWAPSessionCandles))
        sb.AppendLine(String.Format("  s1 band: [{0:F1}, {1:F1}]  |  s2 band: [{2:F1}, {3:F1}]",
                                     r.VWAPSigma1Lower, r.VWAPSigma1Upper,
                                     r.VWAPSigma2Lower, r.VWAPSigma2Upper))
    End Sub

    Private Sub AppendBbwTtm(sb As StringBuilder, r As IndicatorResults)
        sb.AppendLine()
        sb.AppendLine("BBW / TTM SQUEEZE:")
        sb.AppendLine(String.Format("  BBW: {0:F3}  |  Status: {1}", r.BBW, r.SqueezeStatus))
        sb.AppendLine(String.Format("  TTM: Histogram={0:F2}  Dir={1}  Signal={2}",
                                     r.TTMHistogram, r.TTMDirection, r.TTMSignal))
    End Sub

    Private Sub AppendEmaRibbon(sb As StringBuilder, r As IndicatorResults)
        sb.AppendLine()
        sb.AppendLine("EMA RIBBON (1m):")
        sb.AppendLine(String.Format("  9: {0:F1}  |  21: {1:F1}  |  50: {2:F1}  |  Align: {3}",
                                     r.EMA9, r.EMA21, r.EMA50, r.EMAAlignment))
        sb.AppendLine(String.Format("  5m EMA200: {0:F1}  |  Price: {1}", r.EMA200_5m, r.PriceVsEMA200))
    End Sub

    Private Sub AppendMarketStructure(sb As StringBuilder, r As IndicatorResults)
        sb.AppendLine()
        sb.AppendLine("MARKET STRUCTURE:")
        sb.AppendLine(String.Format("  Donchian(20): Upper={0:F1}  Lower={1:F1}  |  Signal: {2}",
                                     r.DonchianUpper, r.DonchianLower, r.DonchianSignal))
        sb.AppendLine(String.Format("  OBV: Trend={0}  |  Div={1}", r.OBVTrend, r.OBVDivergence))

        sb.AppendLine(String.Format("  VPFR-lite: POC:{0:F1}  |  {1}  |  HVN@POC:{2}",
                                     r.VPFRPoc, r.VPFRSignal,
                                     If(r.VPFRHVNearPoc, "YES", "NO")))

        sb.AppendLine(String.Format("  Value Area: VAH:{0:F1}  |  VAL:{1:F1}  |  {2}",
                                     r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal))

        Dim hvnAboveStr As String = If(r.VPFRNearestHvnAbove > 0, r.VPFRNearestHvnAbove.ToString("F1"), "—")
        Dim hvnBelowStr As String = If(r.VPFRNearestHvnBelow > 0, r.VPFRNearestHvnBelow.ToString("F1"), "—")
        Dim lvnAboveStr As String = If(r.VPFRNearestLvnAbove > 0, r.VPFRNearestLvnAbove.ToString("F1"), "—")
        Dim lvnBelowStr As String = If(r.VPFRNearestLvnBelow > 0, r.VPFRNearestLvnBelow.ToString("F1"), "—")
        sb.AppendLine(String.Format("  HVN walls: Above:{0}  Below:{1}  |  LVN: ^{2} v{3}",
                                     hvnAboveStr, hvnBelowStr, lvnAboveStr, lvnBelowStr))

        ' Trend Structure (D1)
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
        sb.AppendLine("  Trend Struct: " & r.TrendStructure.ToString() & tsDetail)

        ' D2 Best Volume Pivot
        If r.BestPivotByVolume5m > 0 Then
            sb.AppendLine(String.Format("  Best Vol Pivot 5m: {0} {1:F1}  (vol×{2:F1} vs avg pivot)",
                                         If(r.BestPivotIsHigh5m, "HIGH", "LOW"),
                                         r.BestPivotByVolume5m, r.BestPivotVolumeRatio5m))
        Else
            sb.AppendLine("  Best Vol Pivot 5m: —  (insufficient pivots)")
        End If
    End Sub

    Private Sub AppendOpenInterest(sb As StringBuilder, r As IndicatorResults)
        sb.AppendLine()
        sb.AppendLine("OPEN INTEREST:")
        sb.AppendLine(String.Format("  OI: {0:F0}  |  d15m: {1:F3}%  |  d60m: {2:F3}%  |  Signal: {3}",
                                     r.OI_Current, r.OIChange15m, r.OIChange60m, r.OISignal))
    End Sub

    Private Sub AppendOrderFlow(sb As StringBuilder, r As IndicatorResults)
        sb.AppendLine()
        sb.AppendLine("ORDER FLOW:")
        sb.AppendLine(String.Format("  OFI Ratio: {0:F2}  |  Bid Vol: {1:F0}  |  Ask Vol: {2:F0}  |  {3}  |  Mom: {4}",
                                     r.OFIRatio, r.OFIBidVol, r.OFIAskVol, r.OFISignal, r.OFIMomentum))
        sb.AppendLine(String.Format("  Spread:    {0:F2} bps  |  {1}", r.SpreadBps, r.SpreadStatus))
        sb.AppendLine(String.Format("  CVD:       Net:{0:F0}  |  Slope:{1}  |  Div:{2}",
                                     r.CVDValue, r.CVDSlope, r.CVDDivergence))
        sb.AppendLine(String.Format("  TFI:       {0:F3}  |  {1}", r.TFIValue, r.TFISignal))
        sb.AppendLine(String.Format("  MicroCVD:  E:{0:F0}  M:{1:F0}  L:{2:F0}  |  {3}  |  {4}",
                                     r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                                     r.MicroCVDMomentum, r.MicroCVDSignal))
    End Sub

    Private Sub AppendLiquidations(sb As StringBuilder, r As IndicatorResults)
        sb.AppendLine()
        sb.AppendLine("LIQUIDATIONS:")
        sb.AppendLine(String.Format("  Long: {0:F0}  |  Short: {1:F0}  |  Signal: {2}",
                                     r.LiqLongSize, r.LiqShortSize, r.LiqSignal))
    End Sub

    Private Sub AppendMtfGate(sb As StringBuilder, r As IndicatorResults, v As VerdictResult)
        sb.AppendLine()
        sb.AppendLine("MTF GATE (15m): " & If(v.MTFGateBlocked, "BLOCK", "PASS"))
        sb.AppendLine(String.Format("  15m Trend: {0}  |  ADX: {1:F1}  |  EMA: {2}",
                                     r.MTF15mTrend, r.MTF15mADX, r.MTF15mEMAAlignment))
        sb.AppendLine("  Reason: " & v.MTFGateReason)
    End Sub

    Private Sub AppendFunding(sb As StringBuilder, r As IndicatorResults, cfg As EngineSettings)
        sb.AppendLine()
        sb.AppendLine("FUNDING:")
        Dim fundDisplayRate As Double = If(Math.Abs(r.FundingRate) < 0.00000001, 0.0, r.FundingRate)
        sb.AppendLine(String.Format("  Rate: {0:F4}%  |  {1}", fundDisplayRate * 100, r.FundingBias))
        sb.AppendLine(String.Format("  Momentum: {0}  |  Enabled: {1}  |  Soften: +{2}  |  Amplify: -{3}",
                                     r.FundingMomentum,
                                     If(cfg.Indicators.Funding.MomentumEnabled, "YES", "NO"),
                                     cfg.Indicators.Funding.MomentumSoften,
                                     cfg.Indicators.Funding.MomentumAmplify))
    End Sub

    Private Sub AppendSignalBreakdown(sb As StringBuilder, v As VerdictResult)
        ' Legacy emits: blank line, "===" divider, "  SIGNAL BREAKDOWN", "===" divider.
        sb.AppendLine()
        sb.AppendLine(SNAPSHOT_DIVIDER)
        sb.AppendLine("  SIGNAL BREAKDOWN")
        sb.AppendLine(SNAPSHOT_DIVIDER)
        sb.AppendLine(String.Format("  {0,-18}  {1,5}  {2,6}  {3}",
                                     "Signal", "Long", "Short", "Note"))
        sb.AppendLine("  " & New String("-"c, SNAPSHOT_ROW_DIVIDER_LEN))
        For Each item In v.SignalBreakdown
            Dim lMark As String = If(item.LongHit, "[L]", "   ")
            Dim sMark As String = If(item.ShortHit, "[S]", "   ")
            sb.AppendLine(String.Format("  {0,-18}  {1,5}  {2,6}  {3}",
                                         item.Label, lMark, sMark, item.Note))
        Next
        sb.AppendLine("  " & New String("-"c, SNAPSHOT_ROW_DIVIDER_LEN))
        sb.AppendLine(String.Format("  {0,-18}  {1,5:F0}  {2,6:F0}",
                                     "TOTAL", CDbl(v.LongScore), CDbl(v.ShortScore)))
    End Sub

End Class
