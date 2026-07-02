' DynamicNorms.vb  v0.25
' Computes live normalization thresholds from candle data fetched each run.
' No log dependency -- fully self-contained per Analyze Now click.
' v0.24:  Static fallback constants now driven by SettingsLoader.Current.
' v0.24a: Expanded single-line Get...End Get to multi-line to fix BC30205.
' v0.25:  Session-aware volume threshold scaling by UTC trading bucket.
'         After dynamic high/mid thresholds are computed, applies per-session
'         HighMultiplier / MidMultiplier from EngineSettings.SessionVolume.
'         Bypassed when SessionVolume.Enabled = False or no session matches.

Public Class DynamicNorms

    Public Property VolHighThreshold As Double
    Public Property VolMidThreshold As Double
    Public Property VolMean As Double
    Public Property VolStdDev As Double
    Public Property VWAPDevThreshold As Double
    Public Property ATRScaleFactor As Double
    Public Property ATRRef As Double
    Public Property IsLive As Boolean

    ' [v47 N1] utcHour: the run's captured hour, threaded through to ApplySessionVolume so
    ' the volume bucket can never resolve from a different hour than execRes at an hour
    ' rollover. -1 (the default) falls back to DateTime.UtcNow.Hour — existing callers
    ' and harness fixtures stay byte-identical.
    Public Shared Function Compute(candles As List(Of Candle), currentATR As Double,
                                   Optional utcHour As Integer = -1) As DynamicNorms
        Dim cfg = SettingsLoader.Current.Indicators
        Dim n As New DynamicNorms()

        If candles Is Nothing OrElse candles.Count < 30 Then
            Return StaticFallback(currentATR)
        End If

        ' -- Method 1a: Volume normalization ----------------------------------
        ' Window = the most recent 100 COMPLETED bars (the final candle is in
        ' progress and excluded), mirroring ComputeATRRef. The baseline must
        ' describe current conditions — session multipliers stack on top of it.
        Dim volLen   As Integer = Math.Min(100, candles.Count - 1)
        Dim volStart As Integer = candles.Count - 1 - volLen
        Dim volWindow = candles.Skip(volStart).Take(volLen) _
                               .Select(Function(c) c.Volume).ToList()
        If volWindow.Count < 10 Then
            Return StaticFallback(currentATR)
        End If

        Dim volMean As Double = volWindow.Average()
        Dim volVariance As Double = volWindow.Average(Function(v) (v - volMean) ^ 2)
        Dim volSD As Double = Math.Sqrt(volVariance)

        n.VolMean = volMean
        n.VolStdDev = volSD

        If volSD < volMean * 0.05 Then
            n.VolHighThreshold = cfg.Volume.StaticHigh
            n.VolMidThreshold  = cfg.Volume.StaticMid
        Else
            Dim highRaw As Double = (volMean + 2.0 * volSD) / volMean
            Dim midRaw As Double  = (volMean + 1.0 * volSD) / volMean
            n.VolHighThreshold = Math.Clamp(highRaw, cfg.Volume.DynamicHighClampMin, cfg.Volume.DynamicHighClampMax)
            n.VolMidThreshold  = Math.Clamp(midRaw,  cfg.Volume.DynamicMidClampMin,  cfg.Volume.DynamicMidClampMax)
        End If

        ' -- Session-aware volume scaling -------------------------------------
        ApplySessionVolume(n, utcHour)

        ' -- Method 1b: VWAP deviation normalization --------------------------
        ' Same recent-completed-bars window shape as the volume baseline above.
        ' The cumulative VWAP built across this window is a rolling last-~50-min
        ' deviation stat — the intended adaptive behaviour.
        Dim vwapDevSamples As New List(Of Double)()
        Dim vwapLen   As Integer = Math.Min(50, candles.Count - 1)
        Dim vwapStart As Integer = candles.Count - 1 - vwapLen
        Dim vwapWindow = candles.Skip(vwapStart).Take(vwapLen).ToList()
        If vwapWindow.Count >= 10 Then
            Dim cumTPV As Double = 0
            Dim cumVol As Double = 0
            For Each c In vwapWindow
                Dim tp As Double = (c.High + c.Low + c.Close) / 3.0
                cumTPV += tp * c.Volume
                cumVol += c.Volume
                If cumVol > 0 Then
                    Dim vwap As Double = cumTPV / cumVol
                    If vwap > 0 Then
                        vwapDevSamples.Add(Math.Abs((c.Close - vwap) / vwap * 100))
                    End If
                End If
            Next
        End If

        Dim vd = cfg.VWAPDynamic
        If vwapDevSamples.Count >= 10 Then
            Dim devMean As Double = vwapDevSamples.Average()
            Dim devVar As Double  = vwapDevSamples.Average(Function(d) (d - devMean) ^ 2)
            Dim devSD As Double   = Math.Sqrt(devVar)
            n.VWAPDevThreshold = Math.Clamp(devMean + devSD, vd.DevClampMin, vd.DevClampMax)
        Else
            n.VWAPDevThreshold = vd.StaticFallback
        End If

        ' -- Method 3: ATR scaling --------------------------------------------
        n.ATRRef = ComputeATRRef(candles)
        Dim atrCfg = cfg.ATR
        If n.ATRRef > 0 AndAlso currentATR > 0 Then
            n.ATRScaleFactor = Math.Clamp(currentATR / n.ATRRef, atrCfg.ScaleMin, atrCfg.ScaleMax)
        Else
            n.ATRScaleFactor = 1.0
            n.ATRRef = atrCfg.StaticRef
        End If

        n.IsLive = True
        Return n
    End Function

    ''' <summary>
    ''' Applies per-session high/mid multipliers from EngineSettings.SessionVolume.
    ''' Matches the given UTC hour (-1 ⇒ DateTime.UtcNow.Hour) to the first session
    ''' bucket whose StartHour..EndHour range contains it.  No-op when Enabled=False
    ''' or no match found.
    ''' </summary>
    Private Shared Sub ApplySessionVolume(n As DynamicNorms, utcHour As Integer)
        Dim cfg = SettingsLoader.Current
        Dim svCfg = cfg.SessionVolume
        If svCfg Is Nothing OrElse Not svCfg.Enabled Then Return

        ' Route the bucket lookup through the shared matcher (ExecutionResolution) so the
        ' volume-scaling boundary is identical to the resolution / display-label boundary
        ' — one definition of "which session is this UTC hour" (DRY; fixes the class of
        ' off-by-one drift). The Enabled gate above is preserved: it controls ONLY the
        ' volume multiplier, NOT resolution selection (which never consults Enabled).
        ' [v47 N1] The hour comes from the run's captured utcHour (temporal cousin of that
        ' same drift class: a run straddling an hour rollover must not resolve execRes and
        ' the volume bucket from different hours).
        Dim hour As Integer = If(utcHour >= 0, utcHour, DateTime.UtcNow.Hour)
        Dim bucket = ExecutionResolution.MatchSessionBucket(cfg, hour)
        If bucket Is Nothing Then Return
        n.VolHighThreshold *= bucket.HighMultiplier
        n.VolMidThreshold  *= bucket.MidMultiplier
    End Sub

    Private Shared Function ComputeATRRef(candles As List(Of Candle)) As Double
        Dim period As Integer = SettingsLoader.Current.Indicators.ATR.Period
        If candles.Count < period + 10 Then Return SettingsLoader.Current.Indicators.ATR.StaticRef

        Dim atrValues As New List(Of Double)()
        Dim sampleCount As Integer = Math.Min(100, candles.Count - period)
        Dim startIdx As Integer = Math.Max(1, candles.Count - sampleCount - period)

        For i As Integer = startIdx + period To candles.Count - 1
            Dim trValues As New List(Of Double)()
            For j As Integer = i - period + 1 To i
                Dim c = candles(j) : Dim p = candles(j - 1)
                trValues.Add(Math.Max(c.High - c.Low,
                             Math.Max(Math.Abs(c.High - p.Close),
                                      Math.Abs(c.Low - p.Close))))
            Next
            atrValues.Add(trValues.Average())
        Next

        Return If(atrValues.Count > 0, atrValues.Average(), SettingsLoader.Current.Indicators.ATR.StaticRef)
    End Function

    Private Shared Function StaticFallback(currentATR As Double) As DynamicNorms
        Dim cfg = SettingsLoader.Current.Indicators
        Dim n As New DynamicNorms()
        n.VolHighThreshold = cfg.Volume.StaticHigh
        n.VolMidThreshold  = cfg.Volume.StaticMid
        n.VWAPDevThreshold = cfg.VWAPDynamic.StaticFallback
        n.ATRRef           = cfg.ATR.StaticRef
        n.ATRScaleFactor   = If(currentATR > 0,
            Math.Clamp(currentATR / cfg.ATR.StaticRef, cfg.ATR.ScaleMin, cfg.ATR.ScaleMax), 1.0)
        n.IsLive = False
        Return n
    End Function

End Class
