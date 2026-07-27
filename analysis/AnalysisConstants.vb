' analysis/AnalysisConstants.vb
' Shared constants for failure-rate definitions used by both the offline analysis
' report and the auto-tweaker pipeline. Keeping them in one place ensures both
' tools compute against the same definition without diverging.
'
' v2 (failure-definition-v2-proposal.md): barrier-hit semantic.
'   - SUCCESS = favourable barrier hit by intra-bar wick before adverse barrier.
'   - FAILURE = adverse barrier hit first, OR window expired without favourable hit.
'
' [placed-target migration 2026-07-21, offline-matrix-placed-target-proposal.md]
'   The per-tier favourable ATR grid (StrongAtrThresholds {0.5,0.8} / MediumAtrThresholds
'   {0.3,0.5}) is RETIRED. It was anchored at ATR≈115 and had gone degenerate: at ATR≈44
'   every grid column sat below the $51 min-move floor, so all columns collapsed onto the
'   same floored barrier and the threshold axis carried zero information. The favourable
'   barrier is now the logged PlacedTarget* — the same placed-vs-placed geometry the live
'   tracker, D4 and the what-if runner already measure. Threshold sweeping moved to the
'   what-if runner, which does it properly (EV + split-half holdout).
'
' Host-agnostic: no System.Windows.Forms references.

Public Module AnalysisConstants

    ' Adverse barrier fallback multiplier when no structural stop is logged.
    ' Matches cfg.Scoring.AtrStopMultiplier default (1.2). Keep in sync if
    ' the engine default ever changes.
    Public Const AdverseFallbackAtrMultiplier As Double = 1.2

    ' [v35 eval-metric de-confound] Minimum favourable-barrier distance as a
    ' fraction of entry price. POCO-DEFAULT MIRROR ONLY — the live value is
    ' cfg.Scoring.TradeCosts.EffectiveMinMovePct (v62: the composed fee + min-net
    ' floor; was the flat cfg.Scoring.MinTradeableMovePct), passed in at the call
    ' sites. Post-migration it
    ' floors the LEGACY (pre-v0.8) favourable fallback only — a logged PlacedTarget is
    ' returned unfloored, because the live Step 5c gate already evaluated that exact
    ' price and flooring it would re-create the very column collapse this migration
    ' removed. LivePerformanceTracker still uses it to EXCLUDE gate-killed rows.
    ' Kept here so host-agnostic callers without a cfg fall back to the same 0.0008
    ' the engine ships. Spec: docs/eval-metric-deconfound-proposal.md.
    Public Const FavBarAbsFloorPct As Double = 0.0008

    ' Engine's take-profit ATR multiplier — mirror of cfg.Scoring.AtrTargetMultiplier
    ' default (2.0). Two roles, both LEGACY-row only since the placed-target migration:
    '   (a) the pre-v0.8 favourable-barrier fallback distance (this × ATR, floored), and
    '   (b) the de-confound EXCLUDE test for those same rows — a historical directional
    '       trade whose engine target can't clear the min-tradeable-move floor is one the
    '       live v35 gate would NO-TRADE, so it leaves the denominator rather than
    '       counting as a failure.
    ' v0.8+ rows are tested EXACTLY instead (|PlacedTarget − entry| vs the floor); the
    ' approximation existed only because the CSV lacked the placed value.
    ' POCO-default mirror; the live value is passed in at the call sites.
    Public Const EngineTargetAtrMultiplier As Double = 2.0

    ' Hold windows in minutes. Eligible bars: closes at row.Timestamp + 3 min
    ' through row.Timestamp + W min (bars closing at T+1 and T+2 excluded for
    ' realistic execution latency — see spec §2b).
    ' This is the res=1 (1-min execution) base case. Resolution-aware callers route
    ' through HoldWindowsForResolution; HoldWindowsForResolution(1) is value-identical
    ' to this array, so the NY×1 matrix and the NY×1-filtered auto-tweaker stay
    ' byte-unchanged. Kept as a field because the auto-tweaker's PromptBuilder (always
    ' res=1) reads it directly.
    Public ReadOnly HoldWindowsMinutes As Integer() = {5, 10, 15}

    ' Resolution-scaled hold windows (three-min-hold-window-recalibration-proposal.md).
    ' Each execution resolution gets the same BAR-COUNT budget — {5,10,15} bars:
    '   res=1 (NY)        → {5, 10, 15}   (= HoldWindowsMinutes; byte-identical)
    '   res=3 (ASIA/LON)  → {15, 30, 45}  (= 5/10/15 three-minute bars)
    ' Rationale: HoldWindowsMinutes measures wall-clock minutes, but a trade develops
    ' in bars. A 3-min trade reaches 5/10/15 bars only at 15/30/45 min; the unscaled
    ' array gave it a third of the bar-budget, so 3-min tiers "failed" by spurious
    ' window-expiry. Barrier detection still walks 1m OHLC WITHIN the window (finer
    ' granularity = more accurate wick detection); only the window LENGTH scales. The
    ' T+3 execution-latency floor is absolute (not scaled) — see
    ' ForwardWindowJoiner.PopulateForwardBars.
    Public Function HoldWindowsForResolution(execRes As Integer) As Integer()
        Dim res As Integer = If(execRes <= 0, 1, execRes)
        Return New Integer() {5 * res, 10 * res, 15 * res}
    End Function

    ' Minimum rows in a cell before its failure rate is considered stable.
    Public Const MinSamplesPerCell As Integer = 30

    ' Minimum tier-eligible (STRONG_* or MEDIUM_*) rows before the auto-tweaker
    ' considers the failure rate trustworthy enough to trigger a tweak.
    Public Const MinSamplesForAutoTweakerTrigger As Integer = 60

End Module
