' ExitGuardEvaluator.vb
' Realtime Exit Guard (P4 #1, docs/realtime-exit-guard-proposal.md) — host-agnostic evaluator.
'
' Re-runs the fast microstructure exit checks against a LIVE MarketState snapshot far more often
' than the full-analysis cadence, and returns an exit-guard state for a thin host layer to surface
' (status strip + optional alarm). DISPLAY/ALERT ONLY: it never calls ScoringEngine.Calculate,
' never writes the CSV, never changes the verdict, never places orders.
'
' It reuses the engine's pure indicator functions (CalcMicroCVD / CalcTFI / CalcOFI / CalcCVD) and
' the SHARED ScoringEngine.ComputeFastExitPrimitives, so the guard's adverse-count is, by
' construction, the engine's adverse-count — no second, drifting copy of "what counts as adverse."
'
' Host-agnostic: no System.Windows.Forms, no Control.Invoke, no MainForm. Reused by the Linux port.

''' <summary>Exit-guard verdict. The host maps Paused/hidden itself (feed-health + posState gate).
''' Two states by design (D3 coordinator ruling): a single adverse signal is Clear, not a third
''' "Warn" tier — only a confirmed 2+-adverse / structural break is EXIT-worthy.</summary>
Public Enum ExitGuardKind
    Clear
    [Exit]
End Enum

''' <summary>Result of one exit-guard evaluation. Display/alert payload only — no scoring impact.</summary>
Public NotInheritable Class ExitGuardResult
    Public Property Kind            As ExitGuardKind = ExitGuardKind.Clear
    ''' <summary>Readable cause for the strip/tooltip (e.g. "2 adverse (MicroCVD BEAR_ACCEL, TFI SELL)").</summary>
    Public Property Reason          As String = ""
    Public Property AdverseCount    As Integer
    Public Property AdverseSignals  As String() = Array.Empty(Of String)()
    Public Property StructuralBreak As Boolean
    Public Property BreakLevel      As Double
End Class

Public NotInheritable Class ExitGuardEvaluator

    ' Trade window mirrored from RunAnalysisAsync's GetRecentTradesAsync(500): CalcCVD walks the
    ' WHOLE window it's handed, so the guard must recompute CVD on the same last-500 window the
    ' full run uses (TFI/MicroCVD self-window via LastN, so they're insensitive to this). Locks the
    ' guard's CVD methodology to the engine's; only the data is fresher.
    Private Const TradeWindow As Integer = 500

    ''' <summary>
    ''' Recompute the four streaming-driven signals (MicroCVD/TFI/OFI/CVD) from the live MarketState,
    ''' run them through the shared fast-exit primitive, and map to {Clear, Exit} (D3: no Warn tier). The host
    ''' decides Paused/hidden (feed-health + posState gating) BEFORE calling this. An empty or
    ''' degenerate buffer maps to Clear (never a false EXIT — §7). Never throws into the caller.
    ''' </summary>
    Public Shared Function Evaluate(state As MarketState,
                                    posState As PositionState,
                                    lastSwingLow5m As Double,
                                    lastSwingHigh5m As Double,
                                    cfg As EngineSettings) As ExitGuardResult
        Dim result As New ExitGuardResult()
        Try
            If posState = PositionState.None OrElse state Is Nothing Then Return result

            Dim allTrades As List(Of TradeRecord) = state.GetTrades()
            If allTrades Is Nothing OrElse allTrades.Count = 0 Then Return result   ' empty buffer → Clear

            ' Mirror GetRecentTradesAsync(500): the most-recent 500 trades (the buffer is ascending).
            Dim window As List(Of TradeRecord) =
                If(allTrades.Count > TradeWindow,
                   allTrades.GetRange(allTrades.Count - TradeWindow, TradeWindow),
                   allTrades)

            Dim book     As OrderBookSnapshot = state.GetBook()
            Dim candles1 As List(Of Candle)   = state.GetCandles("1")

            ' Lightweight IndicatorResults: only the fields the shared primitive reads.
            Dim r As New IndicatorResults()
            r.CurrentPrice    = window(window.Count - 1).Price   ' latest streaming trade (MarketState tail)
            r.LastSwingLow5m  = lastSwingLow5m
            r.LastSwingHigh5m = lastSwingHigh5m

            ' Identical call shapes to RunAnalysisAsync (same cfg params) — fresher data, same method.
            IndicatorEngine.CalcMicroCVD(window,
                r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                r.MicroCVDMomentum, r.MicroCVDSignal,
                microWindowSize:=cfg.Indicators.MicroCVD.WindowSize,
                accelThreshold:=cfg.Indicators.MicroCVD.AccelThreshold,
                dynamicPct:=cfg.Indicators.MicroCVD.AccelThresholdDynamicPct,
                floorPct:=cfg.Indicators.MicroCVD.AccelThresholdFloorPct)

            IndicatorEngine.CalcTFI(window, r.TFIValue, r.TFISignal,
                tfiWindowSize:=cfg.Indicators.TFI.WindowSize,
                threshold:=cfg.Indicators.TFI.Threshold)

            If book IsNot Nothing Then
                IndicatorEngine.CalcOFI(book, r.OFIRatio, r.OFISignal, r.OFIBidVol, r.OFIAskVol,
                    buyDominantRatio:=cfg.Indicators.OFI.BuyDominantRatio,
                    sellDominantRatio:=cfg.Indicators.OFI.SellDominantRatio,
                    bookDepth:=cfg.Indicators.OFI.BookDepth)
            Else
                r.OFISignal = "BALANCED"
            End If

            ' CVD's candle param only feeds divergence (which the guard ignores); slope/value are
            ' candle-independent. Pass the 1m series (per §4.2) or an empty list if unseeded.
            Dim cvdWeightedSlope As Double = 0
            IndicatorEngine.CalcCVD(window, If(candles1, New List(Of Candle)()),
                r.CVDValue, r.CVDSlope, r.CVDDivergence,
                slopeMinUsd:=cfg.Indicators.CVD.SlopeMinUsd,
                slopePctOfValue:=cfg.Indicators.CVD.SlopePctOfValue,
                divergencePriceGate:=cfg.Indicators.CVD.DivergencePriceGate,
                lateSegmentWeight:=cfg.Indicators.CVD.LateSegmentWeight,
                earlySegmentWeight:=cfg.Indicators.CVD.EarlySegmentWeight,
                weightedSlopeOut:=cvdWeightedSlope)

            Dim p As FastExitPrimitives = ScoringEngine.ComputeFastExitPrimitives(r, posState)
            result.AdverseCount    = p.AdverseCount
            result.AdverseSignals  = p.AdverseSignals
            result.StructuralBreak = p.StructuralBreak
            result.BreakLevel      = p.BreakLevel

            ' Mirror CalcHoldStatus's EXIT precedence: 2+-adverse fast exit, then structural break.
            ' Both map to the same Kind.
            If p.AdverseCount >= 2 Then
                result.Kind   = ExitGuardKind.[Exit]
                result.Reason = p.AdverseCount & " adverse (" & ReadableSignals(p, r, posState) & ")"
            ElseIf p.StructuralBreak Then
                result.Kind   = ExitGuardKind.[Exit]
                result.Reason = String.Format("structural break (swing {0} {1:F1})",
                                              If(posState = PositionState.InLong, "low", "high"), p.BreakLevel)
            Else
                ' D3 (coordinator ruling): a SINGLE adverse signal maps to Clear, not a "Warn" tier.
                ' Single-micro-adverse is already surfaced on the HOLD\EXIT row (CalcHoldStatus
                ' Layer 3); single OFI/TFI/CVD alone is noise; a frequently-amber strip would
                ' desensitize the trader to the real EXIT. AdverseCount is still computed (Layer 3
                ' needs it → CalcHoldStatus byte-identical) — only this mapping changed.
                result.Kind   = ExitGuardKind.Clear
                result.Reason = "clear"
            End If
        Catch
            ' Advisory overlay — never surface an exception into the host tick. Degenerate state → Clear.
            result.Kind = ExitGuardKind.Clear
        End Try
        Return result
    End Function

    ' Readable strip labels from the shared booleans (e.g. "MicroCVD BEAR_ACCEL, TFI SELL").
    ' Distinct by design from the terse CalcHoldStatus fragments in p.AdverseSignals — same source,
    ' glanceable form for the live strip.
    Private Shared Function ReadableSignals(p As FastExitPrimitives, r As IndicatorResults,
                                            posState As PositionState) As String
        Dim parts As New List(Of String)()
        If p.MicroAdverse Then parts.Add("MicroCVD " & r.MicroCVDSignal)
        If p.OfiAdverse Then parts.Add(If(posState = PositionState.InLong, "OFI SELL", "OFI BUY"))
        If p.TfiAdverse Then parts.Add(If(posState = PositionState.InLong, "TFI SELL", "TFI BUY"))
        If p.CvdAdverse Then parts.Add(If(posState = PositionState.InLong, "CVD FALLING", "CVD RISING"))
        Return String.Join(", ", parts)
    End Function

End Class
