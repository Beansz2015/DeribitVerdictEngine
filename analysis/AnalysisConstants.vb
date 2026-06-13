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

    ' [v35 eval-metric de-confound] Minimum favourable-barrier distance as a
    ' fraction of entry price. POCO-DEFAULT MIRROR ONLY — the live value is
    ' cfg.Scoring.MinTradeableMovePct, passed in at the call sites (FailureRateMatrix
    ' floors the favourable barrier at max(k×ATR, this×price); LivePerformanceTracker
    ' uses it to EXCLUDE gate-killed rows). Kept here so host-agnostic callers without
    ' a cfg fall back to the same 0.0008 the engine ships. Spec: docs/eval-metric-deconfound-proposal.md.
    Public Const FavBarAbsFloorPct As Double = 0.0008

    ' Engine's take-profit ATR multiplier — mirror of cfg.Scoring.AtrTargetMultiplier
    ' default (2.0). Used by the de-confound EXCLUDE test: a historical directional
    ' trade whose engine target (this × ATR) can't clear the min-tradeable-move floor
    ' is a trade the live v35 gate would NO-TRADE, so it is EXCLUDED from the
    ' failure-rate denominator rather than scored as a failure. POCO-default mirror;
    ' the live value is passed in at the call sites.
    Public Const EngineTargetAtrMultiplier As Double = 2.0

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
