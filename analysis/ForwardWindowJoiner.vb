' analysis/ForwardWindowJoiner.vb
' Loads a v0.4 analysis_log.csv and parses each row into CsvRow objects.
' Forward bar data (OHLC, used by the barrier-hit evaluator) is populated
' separately after a Deribit OHLC bulk fetch — see DeribitOhlcFetcher and
' AnalysisRunner / AutoTweakerCore.
'
' Replaces ForwardReturnJoiner.vb (v1, fixed-horizon CSV-close lookup).
' v2 semantic: barrier-hit with adverse stop (failure-definition-v2-proposal.md).
'   SUCCESS = favourable barrier wick hit before adverse barrier within window.
'   FAILURE = adverse barrier hit first, OR window expired, OR both barriers in
'             same 1m bar (conservative-bias ambiguous-bar rule).
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Globalization

' -----------------------------------------------------------------------
' OhlcBar — one 1m bar returned by DeribitOhlcFetcher.
' -----------------------------------------------------------------------
Public Class OhlcBar
    Public Property CloseTime As DateTime   ' UTC, minute-aligned (openTime + 1 min)
    Public Property Open      As Double
    Public Property High      As Double
    Public Property Low       As Double
    Public Property Close     As Double
End Class

' -----------------------------------------------------------------------
' CsvRow — one parsed row from analysis_log.csv (v0.4 schema).
' -----------------------------------------------------------------------
Public Class CsvRow
    Public Property Index           As Integer
    Public Property Timestamp       As DateTime
    Public Property Price           As Double
    Public Property ATR             As Double
    Public Property Verdict         As String
    Public Property Regime          As String
    Public Property FundingBias     As String
    Public Property VerdictContext  As String
    Public Property OiCvdOutcome    As String
    Public Property OfiRatio        As Double
    Public Property OfiBidVol       As Double
    Public Property OfiAskVol       As Double
    Public Property FundingDelta    As Double
    Public Property SwingStopLong   As Double   ' 0 when no swing data logged
    Public Property SwingStopShort  As Double   ' 0 when no swing data logged

    ' [D6] v0.8 placed levels (cols 106-109) — the barriers the engine actually emitted
    ' for this row (SignalEmitter.ComputeSideLevels at log time). The adverse eval barrier
    ' migrates onto PlacedStop{Long,Short} when HasPlaced; pre-v0.8 rows (HasPlaced=False)
    ' keep the legacy swing-else-ATR adverse formula and are labelled LEGACY_YARDSTICK.
    ' d6-eval-placed-stop-migration-proposal.md.
    Public Property PlacedTargetLong  As Double
    Public Property PlacedStopLong    As Double
    Public Property PlacedTargetShort As Double
    Public Property PlacedStopShort   As Double
    Public Property HasPlaced         As Boolean

    ' v36 Phase-2a (auto-tweaker population filter): the execution resolution the
    ' engine actually ran for this row (CSV v0.7 ExecResolution column). Defaults
    ' to 1 for legacy v0.6 rows that lack the column — authoritative for the
    ' (session × resolution) population filter; never re-derived from the timestamp.
    Public Property ExecResolution  As Integer = 1

    ' [offline-whatif-replay] Additional logged fields the What-If replay adapter feeds
    ' into SignalEmitter.ComputeSideLevels and the verdict re-derivation (all header-name
    ' parsed, guarded — absent columns leave the default, so existing consumers are
    ' untouched). Scores drive the Math.Ceiling(MaxScore × pct) tier walk + regime veto;
    ' the swing/HVN levels + cap-reason feed the placed-level arbitration (POC tier is
    ' unlogged, so poc-bucket rows are excluded by the runner). See
    ' docs/offline-whatif-replay-proposal.md §2/§3.
    Public Property LongScore           As Integer   ' col 5 — raw long score (regime-veto input)
    Public Property ShortScore          As Integer   ' col 6 — raw short score (regime-veto input)
    Public Property EffectiveLongScore  As Integer   ' col 7 — post-Step-4 effective long (tier walk)
    Public Property EffectiveShortScore As Integer   ' col 8 — post-Step-4 effective short (tier walk)
    Public Property MaxScore            As Integer   ' col 9 — regimeMax (threshold denominator)
    Public Property Confidence          As String    ' logged confidence (display only)
    Public Property MtfGatePassLong     As Boolean = True   ' per-side MTF flag (re-applies the veto)
    Public Property MtfGatePassShort    As Boolean = True
    Public Property SwingTargetLong     As Double    ' 0 when no swing data logged
    Public Property SwingTargetShort    As Double
    Public Property VpfrNearestHvnAbove As Double    ' 0 = none
    Public Property VpfrNearestHvnBelow As Double    ' 0 = none
    Public Property TargetCapReason     As String    ' bucket: swing/hvn/poc/none (poc → replay-excluded)

    ' v2: per-window OHLC bar list populated by PopulateForwardBars after OHLC fetch.
    ' Key = window minutes (5, 10, 15). Empty list → row excluded from that window.
    Public Property ForwardBars As New Dictionary(Of Integer, List(Of OhlcBar))()
End Class

' -----------------------------------------------------------------------
' ForwardWindowJoiner
' -----------------------------------------------------------------------
Public Class ForwardWindowJoiner

    ' Load and parse all rows from a v0.4 analysis_log.csv.
    ' ForwardBars is NOT populated here — call PopulateForwardBars separately
    ' after DeribitOhlcFetcher.FetchOhlcRange completes.
    Public Shared Function Load(csvPath As String) As List(Of CsvRow)
        Dim lines As String() = File.ReadAllLines(csvPath)
        If lines.Length <= 1 Then Return New List(Of CsvRow)()

        Dim header = lines(0).Split(","c)
        Dim colIdx As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To header.Length - 1
            colIdx(header(i).Trim()) = i
        Next

        ' [D6] The four placed-level columns arrive together at the v0.8 rotation; a row
        ' is "placed" iff the header carries all four (else it keeps the legacy adverse).
        Dim hasPlacedSchema As Boolean =
            colIdx.ContainsKey("PlacedTargetLong") AndAlso colIdx.ContainsKey("PlacedStopLong") AndAlso
            colIdx.ContainsKey("PlacedTargetShort") AndAlso colIdx.ContainsKey("PlacedStopShort")

        Dim raw As New List(Of CsvRow)()
        For i = 1 To lines.Length - 1
            Dim parts = lines(i).Split(","c)
            If parts.Length < 2 Then Continue For
            Dim row As New CsvRow() With {.Index = i - 1}
            If colIdx.ContainsKey("Timestamp") Then
                DateTime.TryParseExact(parts(colIdx("Timestamp")).Trim(),
                                       "yyyy-MM-dd HH:mm:ss",
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.None,
                                       row.Timestamp)
            End If
            TryParseD(parts, colIdx, "Price",          row.Price)
            row.Verdict       = GetStr(parts, colIdx, "Verdict")
            TryParseD(parts, colIdx, "ATR",            row.ATR)
            row.Regime        = GetStr(parts, colIdx, "Regime")
            row.FundingBias   = GetStr(parts, colIdx, "FundingBias")
            row.VerdictContext = GetStr(parts, colIdx, "VerdictContext")
            row.OiCvdOutcome  = GetStr(parts, colIdx, "OiCvdOutcome")
            TryParseD(parts, colIdx, "OFIRatio",       row.OfiRatio)
            TryParseD(parts, colIdx, "OFIBidVol",      row.OfiBidVol)
            TryParseD(parts, colIdx, "OFIAskVol",      row.OfiAskVol)
            TryParseD(parts, colIdx, "FundingDelta",   row.FundingDelta)
            TryParseD(parts, colIdx, "SwingStopLong",  row.SwingStopLong)
            TryParseD(parts, colIdx, "SwingStopShort", row.SwingStopShort)
            ' v0.7 ExecResolution — absent in legacy v0.6 rows ⇒ default 1.
            row.ExecResolution = ParseIntOr(GetStr(parts, colIdx, "ExecResolution"), 1)
            ' [offline-whatif-replay] Scores + swing/HVN levels + cap-reason for the replay.
            row.LongScore           = ParseIntOr(GetStr(parts, colIdx, "LongScore"), 0)
            row.ShortScore          = ParseIntOr(GetStr(parts, colIdx, "ShortScore"), 0)
            row.EffectiveLongScore  = ParseIntOr(GetStr(parts, colIdx, "EffectiveLongScore"), 0)
            row.EffectiveShortScore = ParseIntOr(GetStr(parts, colIdx, "EffectiveShortScore"), 0)
            row.MaxScore            = ParseIntOr(GetStr(parts, colIdx, "MaxScore"), 0)
            row.Confidence          = GetStr(parts, colIdx, "Confidence")
            row.MtfGatePassLong     = ParseBoolOr(GetStr(parts, colIdx, "MTFGatePassLong"), True)
            row.MtfGatePassShort    = ParseBoolOr(GetStr(parts, colIdx, "MTFGatePassShort"), True)
            TryParseD(parts, colIdx, "SwingTargetLong",     row.SwingTargetLong)
            TryParseD(parts, colIdx, "SwingTargetShort",    row.SwingTargetShort)
            TryParseD(parts, colIdx, "VPFRNearestHvnAbove", row.VpfrNearestHvnAbove)
            TryParseD(parts, colIdx, "VPFRNearestHvnBelow", row.VpfrNearestHvnBelow)
            row.TargetCapReason     = GetStr(parts, colIdx, "TargetCapReason")
            ' [D6] v0.8 placed levels — parsed only when the schema carries them.
            row.HasPlaced = hasPlacedSchema
            If hasPlacedSchema Then
                TryParseD(parts, colIdx, "PlacedTargetLong",  row.PlacedTargetLong)
                TryParseD(parts, colIdx, "PlacedStopLong",    row.PlacedStopLong)
                TryParseD(parts, colIdx, "PlacedTargetShort", row.PlacedTargetShort)
                TryParseD(parts, colIdx, "PlacedStopShort",   row.PlacedStopShort)
            End If
            raw.Add(row)
        Next

        Return raw
    End Function

    ' Populate row.ForwardBars(W) by slicing the OHLC map for each window.
    ' Eligible bars: CloseTime = rowMin + 3 min through rowMin + W min,
    ' where rowMin = row.Timestamp floored to the UTC minute boundary.
    ' Missing bars (Deribit gap) are silently skipped — an empty list means
    ' the row is excluded from that window's failure-rate denominator.
    ' The window set is resolution-scaled per row (three-min-hold-window-recalibration
    ' -proposal.md): a 3-min row keys ForwardBars {15,30,45}, a 1-min row {5,10,15}.
    ' FailureRateMatrix.Compute(resolution) then looks up the matching keys. The T+3
    ' execution-latency floor is absolute (not scaled) — 3 min of latency is 3 min
    ' regardless of execution resolution; only the window length scales.
    Public Shared Sub PopulateForwardBars(rows As List(Of CsvRow),
                                          ohlcMap As Dictionary(Of DateTime, OhlcBar))
        For Each row In rows
            If row.Timestamp = DateTime.MinValue Then Continue For
            ' Floor to UTC minute boundary (analysis timestamps are UTC).
            Dim rowMin As New DateTime(row.Timestamp.Year, row.Timestamp.Month,
                                       row.Timestamp.Day,  row.Timestamp.Hour,
                                       row.Timestamp.Minute, 0, DateTimeKind.Utc)
            For Each w In AnalysisConstants.HoldWindowsForResolution(row.ExecResolution)
                Dim bars As New List(Of OhlcBar)()
                ' Bars closing at T+3, T+4, ..., T+W (bars at T+1 and T+2 excluded
                ' per spec §2b — too quick to execute after a verdict fires).
                For closeMin = 3 To w
                    Dim key As DateTime = rowMin.AddMinutes(closeMin)
                    Dim bar As OhlcBar = Nothing
                    If ohlcMap.TryGetValue(key, bar) Then bars.Add(bar)
                Next
                row.ForwardBars(w) = bars
            Next
        Next
    End Sub

    ' -- Helpers -------------------------------------------------------

    Private Shared Sub TryParseD(parts As String(),
                                  colIdx As Dictionary(Of String, Integer),
                                  key As String, ByRef result As Double)
        Dim idx As Integer
        If Not colIdx.TryGetValue(key, idx) Then Return
        If idx >= parts.Length Then Return
        Double.TryParse(parts(idx).Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, result)
    End Sub

    Private Shared Function GetStr(parts As String(),
                                   colIdx As Dictionary(Of String, Integer),
                                   key As String) As String
        Dim idx As Integer
        If Not colIdx.TryGetValue(key, idx) Then Return ""
        If idx >= parts.Length Then Return ""
        Return parts(idx).Trim()
    End Function

    ' Integer read with a fallback — returns the fallback when the string is empty
    ' or unparseable (e.g. a legacy v0.6 row missing the ExecResolution column).
    Private Shared Function ParseIntOr(s As String, fallback As Integer) As Integer
        Dim v As Integer
        If String.IsNullOrEmpty(s) Then Return fallback
        Return If(Integer.TryParse(s.Trim(), NumberStyles.Integer,
                                   CultureInfo.InvariantCulture, v), v, fallback)
    End Function

    ' [offline-whatif-replay] Boolean read with a fallback — the engine logs "True"/"False"
    ' (Boolean.ToString). Empty / unparseable ⇒ fallback (absent MTF column ⇒ pass).
    Private Shared Function ParseBoolOr(s As String, fallback As Boolean) As Boolean
        Dim v As Boolean
        If String.IsNullOrEmpty(s) Then Return fallback
        Return If(Boolean.TryParse(s.Trim(), v), v, fallback)
    End Function

End Class
