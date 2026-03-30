' DynamicNorms.vb  v0.23
' Computes live normalization thresholds from candle data fetched each run.
' No log dependency -- fully self-contained per Analyze Now click.
'
' Method 1: candle-derived mean+stddev for VolumeRatio and VWAPDevPct
' Method 3: ATR-scaling for ATRSizeMultiplier bounds
'
' Falls back to static defaults if candle data is insufficient (<30 rows).

Public Class DynamicNorms

    ' Volume thresholds (VolumeRatio)
    Public Property VolHighThreshold As Double   ' full signal  (mean + 2σ)
    Public Property VolMidThreshold As Double    ' partial      (mean + 1σ)
    Public Property VolMean As Double
    Public Property VolStdDev As Double

    ' VWAP deviation threshold (absolute %)
    Public Property VWAPDevThreshold As Double   ' 1σ of close-vs-VWAP pct over last 50 candles

    ' ATR scaling
    Public Property ATRScaleFactor As Double     ' ATR_current / ATR_ref
    Public Property ATRRef As Double             ' rolling mean of last 100 1m ATR values

    ' Whether norms are live (True) or static fallback (False)
    Public Property IsLive As Boolean

    ' Static fallback constants
    Public Const STATIC_VOL_HIGH As Double = 3.0
    Public Const STATIC_VOL_MID As Double = 2.0
    Public Const STATIC_VWAP_DEV As Double = 1.5
    Public Const ATR_REF_DEFAULT As Double = 150.0
    Public Const ATR_SCALE_MIN As Double = 0.25
    Public Const ATR_SCALE_MAX As Double = 4.0

    ''' <summary>
    ''' Compute dynamic norms from 1m candle list.
    ''' Requires at least 30 candles for live mode; otherwise returns static fallback.
    ''' </summary>
    Public Shared Function Compute(candles1m As List(Of Candle), currentATR As Double) As DynamicNorms
        Dim n As New DynamicNorms()

        If candles1m Is Nothing OrElse candles1m.Count < 30 Then
            Return StaticFallback(currentATR)
        End If

        ' -- Method 1a: Volume normalization ----------------------------------
        ' Use last 100 candles (or all available), exclude current (last) candle
        ' to avoid self-reference on the candle being scored.
        Dim volWindow = candles1m.Take(Math.Min(100, candles1m.Count - 1)).
                                  Select(Function(c) c.Volume).ToList()
        If volWindow.Count < 10 Then
            Return StaticFallback(currentATR)
        End If

        Dim volMean As Double = volWindow.Average()
        Dim volVariance As Double = volWindow.Average(Function(v) (v - volMean) ^ 2)
        Dim volSD As Double = Math.Sqrt(volVariance)

        n.VolMean = volMean
        n.VolStdDev = volSD

        ' Guard: if SD is near zero (flat volume), fall back to static
        If volSD < volMean * 0.05 Then
            n.VolHighThreshold = STATIC_VOL_HIGH
            n.VolMidThreshold = STATIC_VOL_MID
        Else
            ' Thresholds expressed as multiples of VolMean (VolumeRatio = CurrentVol / VolumeSMA9)
            ' We normalise by expressing mean+Nσ as a ratio over the SMA9 approximation.
            ' VolumeSMA9 ≈ volMean for large windows; safe approximation here.
            Dim highRaw As Double = (volMean + 2.0 * volSD) / volMean
            Dim midRaw As Double = (volMean + 1.0 * volSD) / volMean
            ' Clamp to sensible range to prevent absurd thresholds on low-vol sessions
            n.VolHighThreshold = Math.Clamp(highRaw, 1.5, 6.0)
            n.VolMidThreshold = Math.Clamp(midRaw, 1.2, 4.0)
        End If

        ' -- Method 1b: VWAP deviation normalization --------------------------
        ' Compute rolling VWAPDevPct for last 50 candles (excluding current)
        Dim vwapDevSamples As New List(Of Double)()
        Dim vwapWindow = candles1m.Take(Math.Min(50, candles1m.Count - 1)).ToList()
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

        If vwapDevSamples.Count >= 10 Then
            Dim devMean As Double = vwapDevSamples.Average()
            Dim devVar As Double = vwapDevSamples.Average(Function(d) (d - devMean) ^ 2)
            Dim devSD As Double = Math.Sqrt(devVar)
            ' Use mean + 1σ as the boundary, clamped to [0.3%, 3.0%]
            n.VWAPDevThreshold = Math.Clamp(devMean + devSD, 0.3, 3.0)
        Else
            n.VWAPDevThreshold = STATIC_VWAP_DEV
        End If

        ' -- Method 3: ATR scaling --------------------------------------------
        n.ATRRef = ComputeATRRef(candles1m)
        If n.ATRRef > 0 AndAlso currentATR > 0 Then
            n.ATRScaleFactor = Math.Clamp(currentATR / n.ATRRef, ATR_SCALE_MIN, ATR_SCALE_MAX)
        Else
            n.ATRScaleFactor = 1.0
            n.ATRRef = ATR_REF_DEFAULT
        End If

        n.IsLive = True
        Return n
    End Function

    ''' <summary>
    ''' Compute ATR reference as the mean of ATR(7) values across the last 100 candles.
    ''' </summary>
    Private Shared Function ComputeATRRef(candles As List(Of Candle)) As Double
        Const period As Integer = 7
        If candles.Count < period + 10 Then Return ATR_REF_DEFAULT

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

        Return If(atrValues.Count > 0, atrValues.Average(), ATR_REF_DEFAULT)
    End Function

    ''' <summary>
    ''' Static fallback used when candle data is insufficient.
    ''' </summary>
    Private Shared Function StaticFallback(currentATR As Double) As DynamicNorms
        Dim n As New DynamicNorms()
        n.VolHighThreshold = STATIC_VOL_HIGH
        n.VolMidThreshold = STATIC_VOL_MID
        n.VWAPDevThreshold = STATIC_VWAP_DEV
        n.ATRRef = ATR_REF_DEFAULT
        n.ATRScaleFactor = If(currentATR > 0, Math.Clamp(currentATR / ATR_REF_DEFAULT, ATR_SCALE_MIN, ATR_SCALE_MAX), 1.0)
        n.IsLive = False
        Return n
    End Function

End Class
