' analysis/AnalysisReport.vb
' POCO container for all offline analysis sections.
' No I/O. Pure data class. Host-agnostic.

Imports System.Collections.Generic

Public Class AnalysisReport

    ' -------------------------------------------------------------------
    ' Summary (GLOBAL — counts span all populations)
    ' -------------------------------------------------------------------
    Public Property TotalRows           As Integer
    Public Property VerdictCounts       As New Dictionary(Of String, Integer)()

    ' -------------------------------------------------------------------
    ' Per-(session × resolution) populations (offline-analysis-report-audit-proposal.md).
    ' The failure-rate matrix, barrier diagnostics, and VerdictContext cross-tab are
    ' computed once PER population (NY×1, LONDON×3, ASIA×3, …) — never pooled across
    ' execution regimes. Display order is highest-data-first (see AnalysisRunner).
    ' -------------------------------------------------------------------
    Public Property Populations As New List(Of PopulationReport)()

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
' PopulationReport — all per-(session × resolution) sections for one population.
' One per NY×1 / LONDON×3 / ASIA×3 / (phantom UNKNOWN). The failure-rate matrix
' and the barrier-based diagnostics below are computed over THIS population's rows
' only — pooling two execution regimes (1-min NY vs 3-min Asia/London) into one
' failure cell was the bug this proposal fixes. See offline-analysis-report-audit-proposal.md.
' -----------------------------------------------------------------------
Public Class PopulationReport

    Public Property PopulationKey       As String        ' "NY|1"
    Public Property SessionName         As String        ' "NY"
    Public Property Resolution          As Integer       ' 1 | 3
    Public Property RowCount            As Integer       ' all rows in this population
    ' [D6] "PLACED" (rows carry v0.8 Placed* → adverse = placed stop) or "LEGACY_YARDSTICK"
    ' (pre-v0.8 rows, no Placed* → legacy swing-else-ATR adverse). Populations are split on
    ' this dimension so the two barrier bases are never silently mixed in one cell.
    Public Property BarrierLabel        As String = "PLACED"
    Public Property FailureCells        As New List(Of FailureCellResult)()
    ' [D6] D4 before/after: the SAME rows re-walked under the legacy raw-swing adverse
    ' barrier (FailureCells above use the migrated placed adverse). The delta between the
    ' two is the continuity bridge — the first honest read of executed stop-out risk.
    Public Property LegacyFailureCells  As New List(Of FailureCellResult)()
    Public Property ContextOutcomes     As New Dictionary(Of String, FailureCellResult)()
    Public Property ExcludedRows         As Integer       ' rows with no valid OHLC bars for any window
    Public Property AtrInvalidExcluded   As Integer       ' rows excluded because ATR <= 0
    Public Property BelowMinMoveExcluded As Integer       ' v35 gate-killed: engine target < min-tradeable-move floor
    Public Property StructuralStopRows   As Integer       ' rows where swing stop was available
    Public Property AtrFallbackRows      As Integer       ' rows where ATR-multiple fallback was used
    ' Caption stats for the per-session sub-table headers (proposal §2.4 req 3): ATR
    ' distribution of this population's DIRECTIONAL rows (the rows that feed the tier
    ' matrices) + the $ move-floor. Lets "0.5× ATR" translate to dollars at a glance
    ' and makes the floor-collapse legible per session.
    Public Property DirAtrN      As Integer  ' directional-row count in this population
    Public Property DirAtrP25    As Double
    Public Property DirAtrP50    As Double
    Public Property DirAtrP75    As Double
    Public Property MoveFloorUsd As Double   ' cfg.Scoring.MinTradeableMovePct × representative price

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
    Public Property IsRecommended    As Boolean  ' lowest CI width with n >= MinSamplesPerCell
    Public Property IsMostProfitable As Boolean  ' lowest failure rate with n >= MinSamplesPerCell (trader view)
    ' v2 barrier-hit decomposition (how failures occurred)
    Public Property Successes         As Integer  ' favourable barrier hit first
    Public Property AdverseHitFails   As Integer  ' adverse barrier hit first
    Public Property WindowExpiryFails As Integer  ' window expired without any hit
    Public Property AmbiguousFails    As Integer  ' both barriers in same 1m bar (conservative = fail)

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
