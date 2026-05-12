' tools/AutoTweaker/ConditionsExtractor.vb
' Walks analysis_log.csv slices that correspond to round windows and aggregates
' the conditions vector used by the snapshot manifest (settings-snapshot-history
' -proposal.md §3e).
'
' Inputs:
'   - csvPath: path to analysis_log.csv
'   - rounds: list of RoundSummary entries to include (typically all successful
'             rounds in the current streak)
'   - cfg.Indicators.Spread.TightThresholdBps / WideThresholdBps for spread
'     regime bucketing
'
' Outputs (Conditions struct, all fields populated for direct manifest write):
'   - RegimeMix / VerdictTierMix / SpreadRegimeMix / OFIImbalanceMix (pipe-format strings)
'   - AtrScaleAvg / AtrScaleMin / AtrScaleMax
'   - FundingMin / FundingMax
'   - NetPriceMovePct
'   - VolumeRatioAvg
'   - VWAPDevAvg / VWAPDevMin / VWAPDevMax
'   - AvgFailureRatePct (averaged across round AggregateFailureRatePct values)
'   - ConditionBucket (Regime _ VolatilityTier)
'
' Re-computes from full row range on each call — spec §3e: cleaner than
' incremental accumulation; cost is trivial for typical streaks.
'
' Host-agnostic: no System.Windows.Forms references. SpreadSettings reference
' is via the host-agnostic EngineSettings POCO already present in AutoTweaker.vbproj.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Public Class ConditionsVector

    Public Property RegimeMix          As String = ""
    Public Property AtrScaleAvg        As Double = 0.0
    Public Property AtrScaleMin        As Double = 0.0
    Public Property AtrScaleMax        As Double = 0.0
    Public Property FundingMin         As Double = 0.0
    Public Property FundingMax         As Double = 0.0
    Public Property NetPriceMovePct    As Double = 0.0
    Public Property VolumeRatioAvg     As Double = 0.0
    Public Property VerdictTierMix     As String = ""
    Public Property VWAPDevAvg         As Double = 0.0
    Public Property VWAPDevMin         As Double = 0.0
    Public Property VWAPDevMax         As Double = 0.0
    Public Property SpreadRegimeMix    As String = ""
    Public Property OFIImbalanceMix    As String = ""
    Public Property AvgFailureRatePct  As Double = 0.0
    Public Property ConditionBucket    As String = ""

End Class

Public Class ConditionsExtractor

    ' Column indexes are derived once from the CSV header.
    Private Class ColIdx
        Public Property Price          As Integer = -1
        Public Property ATRMultiplier  As Integer = -1
        Public Property Regime         As Integer = -1
        Public Property Verdict        As Integer = -1
        Public Property VolumeRatio    As Integer = -1
        Public Property VWAPDevPct     As Integer = -1
        Public Property FundingRate    As Integer = -1
        Public Property SpreadBps      As Integer = -1
        Public Property OFISignal      As Integer = -1
    End Class

    Public Shared Function Extract(csvPath As String,
                                    rounds As List(Of RoundSummary),
                                    tightThresholdBps As Double,
                                    wideThresholdBps As Double) As ConditionsVector

        Dim cv As New ConditionsVector()
        If rounds Is Nothing OrElse rounds.Count = 0 Then Return cv
        If Not File.Exists(csvPath) Then Return cv

        Dim lines As String() = File.ReadAllLines(csvPath)
        If lines.Length < 2 Then Return cv

        Dim idx = ResolveColumns(lines(0))

        ' Accumulators
        Dim regimeCounts     As New Dictionary(Of String, Integer)()
        Dim tierCounts       As New Dictionary(Of String, Integer)()
        Dim spreadCounts     As New Dictionary(Of String, Integer)()
        Dim ofiCounts        As New Dictionary(Of String, Integer)()
        Dim totalRows        As Integer = 0
        Dim atrSum, atrMin, atrMax As Double
        atrMin = Double.MaxValue
        atrMax = Double.MinValue
        Dim atrCount As Integer = 0
        Dim fundingMin, fundingMax As Double
        fundingMin = Double.MaxValue
        fundingMax = Double.MinValue
        Dim fundingObserved As Boolean = False
        Dim volRatioSum As Double = 0.0
        Dim volRatioCount As Integer = 0
        Dim vwapAbsSum, vwapAbsMin, vwapAbsMax As Double
        vwapAbsMin = Double.MaxValue
        vwapAbsMax = Double.MinValue
        Dim vwapCount As Integer = 0
        Dim firstPrice As Double = 0.0
        Dim lastPrice  As Double = 0.0
        Dim firstPriceCaptured As Boolean = False
        Dim failureSum As Double = 0.0
        Dim failureCount As Integer = 0

        For Each round In rounds
            failureSum += round.AggregateFailureRatePct
            failureCount += 1

            Dim startIdx As Integer = Math.Max(1, round.WindowStartRow + 1)
            Dim endIdx As Integer = Math.Min(lines.Length - 1, round.WindowEndRow + 1)
            If endIdx < startIdx Then Continue For

            For r As Integer = startIdx To endIdx
                Dim parts As String() = lines(r).Split(","c)
                If parts.Length < 2 Then Continue For
                totalRows += 1

                ' Regime mix
                If idx.Regime >= 0 AndAlso idx.Regime < parts.Length Then
                    Dim reg As String = parts(idx.Regime).Trim().ToUpper()
                    If reg.Length > 0 Then Inc(regimeCounts, reg)
                End If

                ' Verdict tier mix
                If idx.Verdict >= 0 AndAlso idx.Verdict < parts.Length Then
                    Dim tier As String = NormaliseTier(parts(idx.Verdict).Trim())
                    If tier.Length > 0 Then Inc(tierCounts, tier)
                End If

                ' ATR scale (ATRMultiplier column)
                Dim atrV As Double
                If TryD(parts, idx.ATRMultiplier, atrV) Then
                    atrSum += atrV
                    If atrV < atrMin Then atrMin = atrV
                    If atrV > atrMax Then atrMax = atrV
                    atrCount += 1
                End If

                ' Funding range
                Dim fund As Double
                If TryD(parts, idx.FundingRate, fund) Then
                    If fund < fundingMin Then fundingMin = fund
                    If fund > fundingMax Then fundingMax = fund
                    fundingObserved = True
                End If

                ' Volume ratio
                Dim vr As Double
                If TryD(parts, idx.VolumeRatio, vr) Then
                    volRatioSum += vr
                    volRatioCount += 1
                End If

                ' VWAP dev (use absolute value per spec)
                Dim vd As Double
                If TryD(parts, idx.VWAPDevPct, vd) Then
                    Dim a As Double = Math.Abs(vd)
                    vwapAbsSum += a
                    If a < vwapAbsMin Then vwapAbsMin = a
                    If a > vwapAbsMax Then vwapAbsMax = a
                    vwapCount += 1
                End If

                ' Net price move tracking — capture first and last price in chronological order
                Dim price As Double
                If TryD(parts, idx.Price, price) Then
                    If Not firstPriceCaptured Then
                        firstPrice = price
                        firstPriceCaptured = True
                    End If
                    lastPrice = price
                End If

                ' Spread regime bucket
                If idx.SpreadBps >= 0 AndAlso idx.SpreadBps < parts.Length Then
                    Dim sb As Double
                    If Double.TryParse(parts(idx.SpreadBps).Trim(), NumberStyles.Float,
                                       CultureInfo.InvariantCulture, sb) Then
                        Dim bucket As String
                        If sb <= tightThresholdBps Then
                            bucket = "T"
                        ElseIf sb >= wideThresholdBps Then
                            bucket = "W"
                        Else
                            bucket = "N"
                        End If
                        Inc(spreadCounts, bucket)
                    End If
                End If

                ' OFI imbalance bucket — read OFISignal directly
                If idx.OFISignal >= 0 AndAlso idx.OFISignal < parts.Length Then
                    Dim sig As String = parts(idx.OFISignal).Trim().ToUpper()
                    Select Case sig
                        Case "BUY_DOMINANT", "BUY DOMINANT", "BD"
                            Inc(ofiCounts, "BD")
                        Case "SELL_DOMINANT", "SELL DOMINANT", "SD"
                            Inc(ofiCounts, "SD")
                        Case "BALANCED", "BAL", "NEUTRAL"
                            Inc(ofiCounts, "BAL")
                    End Select
                End If
            Next
        Next

        ' Build outputs
        cv.AtrScaleAvg = If(atrCount > 0, atrSum / atrCount, 0.0)
        cv.AtrScaleMin = If(atrCount > 0, atrMin, 0.0)
        cv.AtrScaleMax = If(atrCount > 0, atrMax, 0.0)

        cv.FundingMin = If(fundingObserved, fundingMin, 0.0)
        cv.FundingMax = If(fundingObserved, fundingMax, 0.0)

        cv.VolumeRatioAvg = If(volRatioCount > 0, volRatioSum / volRatioCount, 0.0)

        cv.VWAPDevAvg = If(vwapCount > 0, vwapAbsSum / vwapCount, 0.0)
        cv.VWAPDevMin = If(vwapCount > 0, vwapAbsMin, 0.0)
        cv.VWAPDevMax = If(vwapCount > 0, vwapAbsMax, 0.0)

        cv.NetPriceMovePct = If(firstPriceCaptured AndAlso firstPrice > 0,
                                (lastPrice - firstPrice) / firstPrice * 100.0,
                                0.0)

        cv.AvgFailureRatePct = If(failureCount > 0, failureSum / failureCount, 0.0)

        cv.RegimeMix       = FormatRegimeMix(regimeCounts, totalRows)
        cv.VerdictTierMix  = FormatTierMix(tierCounts, totalRows)
        cv.SpreadRegimeMix = FormatNamedMix(spreadCounts, totalRows, {"T", "N", "W"})
        cv.OFIImbalanceMix = FormatNamedMix(ofiCounts, totalRows, {"BD", "SD", "BAL"})

        cv.ConditionBucket = BucketKey(cv.RegimeMix, cv.AtrScaleAvg)
        Return cv
    End Function

    ' ── Helpers ───────────────────────────────────────────────────────────────

    Private Shared Function ResolveColumns(headerLine As String) As ColIdx
        Dim idx As New ColIdx()
        Dim headers As String() = headerLine.Split(","c)
        For i As Integer = 0 To headers.Length - 1
            Select Case headers(i).Trim()
                Case "Price"          : idx.Price = i
                Case "ATRMultiplier"  : idx.ATRMultiplier = i
                Case "Regime"         : idx.Regime = i
                Case "Verdict"        : idx.Verdict = i
                Case "VolumeRatio"    : idx.VolumeRatio = i
                Case "VWAPDevPct"     : idx.VWAPDevPct = i
                Case "FundingRate"    : idx.FundingRate = i
                Case "SpreadBps"      : idx.SpreadBps = i
                Case "OFISignal"      : idx.OFISignal = i
            End Select
        Next
        Return idx
    End Function

    Private Shared Function TryD(parts As String(), colIdx As Integer, ByRef result As Double) As Boolean
        If colIdx < 0 OrElse colIdx >= parts.Length Then Return False
        Return Double.TryParse(parts(colIdx).Trim(), NumberStyles.Float,
                               CultureInfo.InvariantCulture, result)
    End Function

    Private Shared Sub Inc(d As Dictionary(Of String, Integer), key As String)
        Dim cur As Integer
        d.TryGetValue(key, cur)
        d(key) = cur + 1
    End Sub

    Private Shared Function NormaliseTier(verdict As String) As String
        Select Case verdict.Trim().ToUpper()
            Case "STRONG LONG"  : Return "SL"
            Case "LONG"         : Return "L"
            Case "WEAK LONG"    : Return "WL"
            Case "NO TRADE"     : Return "NT"
            Case "WEAK SHORT"   : Return "WS"
            Case "SHORT"        : Return "S"
            Case "STRONG SHORT" : Return "SS"
            Case Else           : Return ""
        End Select
    End Function

    Private Shared Function FormatNamedMix(counts As Dictionary(Of String, Integer),
                                            total As Integer,
                                            keysOrder As String()) As String
        If total = 0 Then Return ""
        Dim parts As New List(Of String)()
        For Each k In keysOrder
            Dim n As Integer
            counts.TryGetValue(k, n)
            Dim pct As Integer = CInt(Math.Round(CDbl(n) / total * 100.0))
            parts.Add(String.Format(CultureInfo.InvariantCulture, "{0}:{1}", k, pct))
        Next
        Return String.Join("|", parts)
    End Function

    ' Regime mix uses 2-letter codes for the four regimes.
    Private Shared Function FormatRegimeMix(counts As Dictionary(Of String, Integer),
                                             total As Integer) As String
        If total = 0 Then Return ""
        Dim mapped As New Dictionary(Of String, Integer)()
        For Each kv In counts
            Dim code As String = RegimeCode(kv.Key)
            If code.Length = 0 Then Continue For
            Dim cur As Integer
            mapped.TryGetValue(code, cur)
            mapped(code) = cur + kv.Value
        Next
        Return FormatNamedMix(mapped, total, {"UP", "DN", "RB", "TR"})
    End Function

    Private Shared Function FormatTierMix(counts As Dictionary(Of String, Integer),
                                           total As Integer) As String
        If total = 0 Then Return ""
        Return FormatNamedMix(counts, total, {"SL", "L", "WL", "NT", "WS", "S", "SS"})
    End Function

    Private Shared Function RegimeCode(regime As String) As String
        Select Case regime
            Case "TRENDING_UP"   : Return "UP"
            Case "TRENDING_DOWN" : Return "DN"
            Case "RANGE_BOUND"   : Return "RB"
            Case "TRANSITIONAL"  : Return "TR"
            Case Else            : Return ""
        End Select
    End Function

    ' Public so SnapshotManager can re-compute the bucket key from a manifest row.
    Public Shared Function BucketKey(regimeMix As String, atrScaleAvg As Double) As String
        Dim regime As String = DominantRegime(regimeMix)
        Dim vol    As String
        If atrScaleAvg < 0.85 Then
            vol = "LOW"
        ElseIf atrScaleAvg < 1.15 Then
            vol = "NORMAL"
        Else
            vol = "HIGH"
        End If
        Return regime & "_" & vol
    End Function

    ' Dominant regime — name of the regime with the highest percentage in the mix string.
    ' Mix string format: "UP:25|DN:10|RB:60|TR:5".
    Public Shared Function DominantRegime(regimeMix As String) As String
        If String.IsNullOrEmpty(regimeMix) Then Return "UNKNOWN"
        Dim bestCode As String = ""
        Dim bestPct As Integer = -1
        For Each tok In regimeMix.Split("|"c)
            Dim kv As String() = tok.Split(":"c)
            If kv.Length <> 2 Then Continue For
            Dim pct As Integer
            If Integer.TryParse(kv(1), NumberStyles.Integer, CultureInfo.InvariantCulture, pct) Then
                If pct > bestPct Then
                    bestPct = pct
                    bestCode = kv(0)
                End If
            End If
        Next
        Select Case bestCode
            Case "UP" : Return "TRENDING_UP"
            Case "DN" : Return "TRENDING_DOWN"
            Case "RB" : Return "RANGE_BOUND"
            Case "TR" : Return "TRANSITIONAL"
            Case Else : Return "UNKNOWN"
        End Select
    End Function

End Class
