' analysis/AnalysisReport.vb
' POCO container for all offline analysis sections.
' No I/O. Pure data class. Host-agnostic.

Imports System.Collections.Generic

Public Class AnalysisReport

    ' -------------------------------------------------------------------
    ' Summary
    ' -------------------------------------------------------------------
    Public Property TotalRows      As Integer
    Public Property ExcludedRows   As Integer
    Public Property VerdictCounts  As New Dictionary(Of String, Integer)()

    ' -------------------------------------------------------------------
    ' Failure-rate matrix results (one per tier x window x threshold cell)
    ' -------------------------------------------------------------------
    Public Property FailureCells As New List(Of FailureCellResult)()

    ' -------------------------------------------------------------------
    ' Verdict context × outcome cross-tab
    ' Key: VerdictContext string. Value: FailureCellResult for recommended cell.
    ' -------------------------------------------------------------------
    Public Property ContextOutcomes As New Dictionary(Of String, FailureCellResult)()

    ' -------------------------------------------------------------------
    ' Funding momentum diagnostic
    ' -------------------------------------------------------------------
    Public Property FundingDiagnostic As FundingMomentumDiagnosticResult

    ' -------------------------------------------------------------------
    ' OFI outlier audit
    ' -------------------------------------------------------------------
    Public Property OfiAudit As OfiOutlierResult

    ' -------------------------------------------------------------------
    ' OI x CVD asymmetry audit
    ' -------------------------------------------------------------------
    Public Property OiCvdAudit As OiCvdAsymmetryResult

    ' -------------------------------------------------------------------
    ' Rendered output (written to disk by MarkdownReportWriter)
    ' -------------------------------------------------------------------
    Public Property MarkdownText     As String
    Public Property MarkdownFilePath As String
    Public Property SummaryCsvPath   As String

End Class

' -----------------------------------------------------------------------
' FailureCellResult — one cell in the tier x window x threshold matrix
' -----------------------------------------------------------------------
Public Class FailureCellResult

    Public Property VerdictTier   As String   ' "STRONG_LONG" | "STRONG_SHORT" | "MEDIUM_LONG" | "MEDIUM_SHORT"
    Public Property WindowMin     As Integer  ' 5 | 10 | 15
    Public Property AtrThreshold  As Double   ' 0.3 | 0.5 | 0.8
    Public Property SampleSize    As Integer
    Public Property Failures      As Integer
    Public Property FailureRate   As Double   ' failures / sampleSize; 0 if sampleSize=0
    Public Property CiLow         As Double   ' 95% Wilson CI lower bound
    Public Property CiHigh        As Double   ' 95% Wilson CI upper bound
    Public Property CiWidth       As Double   ' CiHigh - CiLow
    Public Property IsRecommended As Boolean  ' lowest CI width with n >= MinSamplesPerCell

End Class

' -----------------------------------------------------------------------
' FundingMomentumDiagnosticResult
' -----------------------------------------------------------------------
Public Class FundingMomentumDiagnosticResult

    Public Property TotalRows           As Integer
    Public Property NonZeroRows         As Integer
    Public Property AbsValues           As New List(Of Double)()  ' sorted for percentile use
    Public Property Pct50               As Double  ' 50th percentile of |FundingDelta|
    Public Property Pct75               As Double
    Public Property Pct90               As Double
    Public Property Pct95               As Double
    ' Implied threshold to achieve ~30% non-FLAT rate
    Public Property ImpliedThreshold30Pct As Double
    Public Property Recommendation      As String  ' human-readable

End Class

' -----------------------------------------------------------------------
' OfiOutlierResult
' -----------------------------------------------------------------------
Public Class OfiOutlierRow
    Public Property Timestamp  As String
    Public Property OfiRatio   As Double
    Public Property OfiBidVol  As Double
    Public Property OfiAskVol  As Double
End Class

Public Class OfiOutlierResult
    Public Property TotalRows       As Integer
    Public Property RowsAbove100    As Integer
    Public Property RowsAbove1000   As Integer
    Public Property Top10           As New List(Of OfiOutlierRow)()
    Public Property Recommendation  As String
End Class

' -----------------------------------------------------------------------
' OiCvdAsymmetryResult
' -----------------------------------------------------------------------
Public Class LongShortCount
    Public Property LongCount  As Integer
    Public Property ShortCount As Integer
End Class

Public Class OiCvdAsymmetryResult
    Public Property TotalConfirmedLong  As Integer
    Public Property TotalConfirmedShort As Integer
    ' Breakdown by Regime
    Public Property ByRegime            As New Dictionary(Of String, LongShortCount)()
    ' Breakdown by FundingBias
    Public Property ByFundingBias       As New Dictionary(Of String, LongShortCount)()
    Public Property Verdict             As String  ' "REGIME_PERIOD_BIAS" | "ASYMMETRIC_ALGORITHM" | "INCONCLUSIVE"
End Class
