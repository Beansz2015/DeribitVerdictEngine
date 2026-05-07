' analysis/AnalysisConstants.vb
' Shared constants for failure-rate definitions used by both the offline analysis
' report and the auto-tweaker pipeline. Keeping them in one place ensures both
' tools compute against the same definition without diverging.
'
' v2 (failure-definition-v2-proposal.md): barrier-hit semantic.
'   - SUCCESS = favourable barrier hit by intra-bar wick before adverse barrier.
'   - FAILURE = adverse barrier hit first, OR window expired without favourable hit.
'   - STRONG/MEDIUM threshold swap vs v1: under barrier-hit semantics a SMALLER
'     multiplier is MORE LENIENT (smaller required profit move), so STRONG must use
'     the LARGER values to impose a higher bar.
'
' Host-agnostic: no System.Windows.Forms references.

Public Module AnalysisConstants

    ' ATR-multiple thresholds for the favourable barrier per verdict tier.
    ' Under v2 barrier-hit semantics: larger value = harder to succeed = STRONGER bar.
    ' STRONG uses {0.5, 0.8} — higher conviction verdicts must reach a further target.
    ' MEDIUM uses {0.3, 0.5} — moderate conviction verdicts pass on a smaller wick.
    ' (v1 had these swapped; the swap is correct for the new barrier-hit direction.)
    Public ReadOnly StrongAtrThresholds As Double() = {0.5, 0.8}
    Public ReadOnly MediumAtrThresholds As Double() = {0.3, 0.5}

    ' Adverse barrier fallback multiplier when no structural stop is logged.
    ' Matches cfg.Scoring.AtrStopMultiplier default (1.2). Keep in sync if
    ' the engine default ever changes.
    Public Const AdverseFallbackAtrMultiplier As Double = 1.2

    ' Hold windows in minutes. Eligible bars: closes at row.Timestamp + 3 min
    ' through row.Timestamp + W min (bars closing at T+1 and T+2 excluded for
    ' realistic execution latency — see spec §2b).
    Public ReadOnly HoldWindowsMinutes As Integer() = {5, 10, 15}

    ' Minimum rows in a cell before its failure rate is considered stable.
    Public Const MinSamplesPerCell As Integer = 30

    ' Minimum tier-eligible (STRONG_* or MEDIUM_*) rows before the auto-tweaker
    ' considers the failure rate trustworthy enough to trigger a tweak.
    Public Const MinSamplesForAutoTweakerTrigger As Integer = 60

End Module
