' analysis/AnalysisConstants.vb
' Shared constants for failure-rate definitions used by both the offline analysis
' report and the auto-tweaker pipeline. Keeping them in one place ensures both
' tools compute against the same definition without diverging.
'
' Host-agnostic: no System.Windows.Forms references.

Public Module AnalysisConstants

    ' ATR-multiple thresholds evaluated per verdict tier.
    ' STRONG is held to a tighter standard (smaller move counts as failure).
    Public ReadOnly StrongAtrThresholds As Double() = {0.3, 0.5}
    Public ReadOnly MediumAtrThresholds As Double() = {0.5, 0.8}

    ' Hold windows in minutes. Forward prices are resolved by timestamp lookup
    ' (±30 s tolerance) so these are cadence-agnostic — correct at any auto-run interval.
    Public ReadOnly HoldWindowsMinutes As Integer() = {5, 10, 15}

    ' Minimum rows in a cell before its failure rate is considered stable.
    Public Const MinSamplesPerCell As Integer = 30

    ' Minimum tier-eligible (STRONG_* or MEDIUM_*) rows before the auto-tweaker
    ' considers the failure rate trustworthy enough to trigger a tweak.
    Public Const MinSamplesForAutoTweakerTrigger As Integer = 60

End Module
