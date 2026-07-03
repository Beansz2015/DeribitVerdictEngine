' Core/AggressorVelocityAccumulator.vb
' WebSocket migration P4 #5 — aggressor velocity / tape burst (docs/aggressor-velocity-proposal.md §4.1).
'
' A host-agnostic pair of TIME-DECAYED running sums of taker (aggressor) USD, split
' buy/sell, held at TWO horizons — a fast burst horizon (tau_fast = fast_window_sec)
' and a slow rolling-norm horizon (tau_norm = the per-session norm_window_sec). Fed one
' sample per streamed trade by DeribitWsFeed.ApplyTrades (the trade analogue of the
' OfiAccumulator book fold) and read once per analysis run / live-strip tick.
'
' Math (proposal §4.1): for each horizon with e-folding time tau, on a trade
' (amountUsd, direction, tsMs):
'     dt      = max(0, (tsMs - lastT) / 1000)     ' seconds; floored like OfiAccumulator
'     decay   = exp(-dt / tau)                     ' tau <= 0 → decay 0 (defensive overwrite)
'     Abuy   *= decay : Asell *= decay
'     side   += amountUsd
' A is an exponentially-weighted sum of recent USD with horizon tau; for a steady rate
' r USD/sec its fixed point is A* = r·tau, so the FLOW RATE = A / tau (USD/sec). O(1)
' per trade, time-aware, non-spiky (no amount/dt division). The trade's own exchange
' timestamp is the fold stamp (trades carry one; the book fold uses receive time only
' because book updates don't).
'
' Burst metric (proposal §4.2), computed in Snapshot():
'     grossFast  = (AbuyF + AsellF) / tauFast          ' USD/sec, burst horizon
'     grossNorm  = (AbuyN + AsellN) / tauNorm          ' USD/sec, rolling baseline
'     burstRatio = grossFast / max(grossNorm, floor)   ' how many× the norm
'     netFast    = AbuyF - AsellF                      ' signed USD, burst horizon
'     lean       = netFast / max(AbuyF + AsellF, eps)  ' -1..+1 directional lean
'
' Warmup gate (proposal §8): until the accumulator has >= norm_window_sec of fold
' coverage AND a small minimum trade count, Snapshot().HasWarmup is False and the
' caller emits NORMAL / null rather than dividing by a half-filled baseline. Reset()
' on (re)connect (DeribitWsFeed.SeedAsync) so no pre-disconnect flow bleeds across a
' gap; the suppression re-arms after every reconnect.
'
' Concurrency: NOT internally locked. Lives on MarketState and is folded/read only
' under MarketState's single SyncLock (single-writer feed thread, multi-reader
' analysis/strip threads) — same discipline as OfiAccumulator. Do not touch off-lock.
Public NotInheritable Class AggressorVelocityAccumulator

    ' Anti-degenerate floor: a couple of trades spanning >= the norm window via a gap
    ' shouldn't count as "warmed up". Matches OfiAccumulator's floor; on a live tape the
    ' norm window holds far more than 5 prints, so this only guards the sparse case.
    Private Const MinWarmupTrades As Integer = 5

    Private _hasState        As Boolean = False
    Private _buyFast         As Double  = 0.0
    Private _sellFast        As Double  = 0.0
    Private _buyNorm         As Double  = 0.0
    Private _sellNorm        As Double  = 0.0
    Private _lastFoldMs      As Long    = 0     ' epoch-ms of the previous fold (dt basis)
    Private _coverageStartMs As Long    = 0     ' epoch-ms of the first fold since reset
    Private _tradeCount      As Integer = 0
    Private _lastTauFast     As Double  = 0.0   ' taus used at the most recent fold —
    Private _lastTauNorm     As Double  = 0.0   ' Snapshot's rate divisors stay fold-consistent

    ''' <summary>Clear all state to the cold (no-data) condition. Called on (re)connect so
    ''' pre-disconnect flow can't survive a feed gap and the warmup suppression re-arms.</summary>
    Public Sub Reset()
        _hasState        = False
        _buyFast         = 0.0
        _sellFast        = 0.0
        _buyNorm         = 0.0
        _sellNorm        = 0.0
        _lastFoldMs      = 0
        _coverageStartMs = 0
        _tradeCount      = 0
        _lastTauFast     = 0.0
        _lastTauNorm     = 0.0
    End Sub

    ''' <summary>Fold one trade into both horizons. `amountUsd` is the Deribit inverse-perp
    ''' USD notional; `tsMs` is the trade's exchange timestamp (epoch-ms); the taus are
    ''' fast_window_sec and the session-resolved norm_window_sec. The first fold after a
    ''' reset seeds both horizons at the sample (no decay); thereafter the time-aware
    ''' decay applies. dt is floored at 0 so a non-monotonic stamp can't inflate sums.</summary>
    Public Sub Fold(amountUsd As Double, isBuy As Boolean, tsMs As Long,
                    tauFastSec As Double, tauNormSec As Double)
        If Not _hasState Then
            _lastFoldMs      = tsMs
            _coverageStartMs = tsMs
            _hasState        = True
        Else
            Dim dt As Double = Math.Max(0.0, (tsMs - _lastFoldMs) / 1000.0)
            Dim decayFast As Double = If(tauFastSec <= 0.0, 0.0, Math.Exp(-dt / tauFastSec))
            Dim decayNorm As Double = If(tauNormSec <= 0.0, 0.0, Math.Exp(-dt / tauNormSec))
            _buyFast  *= decayFast : _sellFast *= decayFast
            _buyNorm  *= decayNorm : _sellNorm *= decayNorm
            _lastFoldMs = tsMs
        End If

        If isBuy Then
            _buyFast += amountUsd : _buyNorm += amountUsd
        Else
            _sellFast += amountUsd : _sellNorm += amountUsd
        End If
        _lastTauFast = tauFastSec
        _lastTauNorm = tauNormSec
        _tradeCount += 1
    End Sub

    ''' <summary>Wall-clock span observed since the first fold (fold stamps, not "now" —
    ''' a stalled tape stops accruing coverage). 0 before any fold.</summary>
    Public ReadOnly Property CoverageSeconds As Double
        Get
            If Not _hasState Then Return 0.0
            Return (_lastFoldMs - _coverageStartMs) / 1000.0
        End Get
    End Property

    Public ReadOnly Property TradeCount As Integer
        Get
            Return _tradeCount
        End Get
    End Property

    ''' <summary>True once the accumulator has >= minCoverageSec of fold coverage (the
    ''' session's norm window) and the minimum trade count — i.e. the rolling norm is a
    ''' full baseline and the burst ratio is safe to classify.</summary>
    Public Function HasWarmup(minCoverageSec As Double) As Boolean
        Return _hasState AndAlso _tradeCount >= MinWarmupTrades AndAlso CoverageSeconds >= minCoverageSec
    End Function

    ''' <summary>A consistent read of the burst state (proposal §4.2) plus the warmup
    ''' verdict for the given norm window. Rates divide by the taus used at fold time so
    ''' read and fold can never disagree on the horizon. The caller uses BurstRatio /
    ''' NetUsdPerSec / Lean only when HasWarmup is True.</summary>
    Public Function Snapshot(grossFloorUsdPerSec As Double, minCoverageSec As Double) As AggressorVelocitySnapshot
        Dim grossFast As Double = 0.0
        Dim grossNorm As Double = 0.0
        Dim netPerSec As Double = 0.0
        If _lastTauFast > 0.0 Then
            grossFast = (_buyFast + _sellFast) / _lastTauFast
            netPerSec = (_buyFast - _sellFast) / _lastTauFast
        End If
        If _lastTauNorm > 0.0 Then grossNorm = (_buyNorm + _sellNorm) / _lastTauNorm

        ' gross_floor stops a single print on a dead tape reading as an infinite burst.
        Dim burstRatio As Double = grossFast / Math.Max(grossNorm, Math.Max(grossFloorUsdPerSec, 0.000001))
        Dim lean As Double = (_buyFast - _sellFast) / Math.Max(_buyFast + _sellFast, 0.000001)

        Return New AggressorVelocitySnapshot With {
            .HasWarmup    = HasWarmup(minCoverageSec),
            .GrossFastUsdPerSec = grossFast,
            .GrossNormUsdPerSec = grossNorm,
            .BurstRatio   = burstRatio,
            .NetUsdPerSec = netPerSec,
            .Lean         = lean,
            .TradeCount   = _tradeCount,
            .CoverageSec  = CoverageSeconds}
    End Function
End Class

''' <summary>A point-in-time read of the aggressor-velocity accumulator (proposal §4.2).
''' When HasWarmup is True the caller classifies (BurstRatio, Lean) via
''' IndicatorEngine.ClassifyAggressorBurst and logs BurstRatio / NetUsdPerSec; otherwise
''' it emits NORMAL / null (cold-feed suppression, §8).</summary>
Public Structure AggressorVelocitySnapshot
    Public Property HasWarmup           As Boolean
    Public Property GrossFastUsdPerSec  As Double
    Public Property GrossNormUsdPerSec  As Double
    Public Property BurstRatio          As Double
    Public Property NetUsdPerSec       As Double
    Public Property Lean                As Double
    Public Property TradeCount          As Integer
    Public Property CoverageSec         As Double
End Structure
