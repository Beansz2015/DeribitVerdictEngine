' Core/OfiAccumulator.vb
' WebSocket migration P4 #4 — time-averaged OFI (docs/time-averaged-ofi-proposal.md).
'
' A host-agnostic, time-aware EMA of the top-book OFI imbalance, fed one sample per
' streaming book update (~100ms) by DeribitWsFeed and read once per analysis run by
' RunAnalysisAsync (WS path). It replaces the single-snapshot OFI with a time-weighted
' average over the run window so a transient sweep/spoof can't flip the OFIRatio — the
' actionable signal is SUSTAINED imbalance, not a one-tick spike (proposal §1/§2).
'
' Design (proposal §4.1, all five §10 decisions as-recommended):
'   - Feed-side rolling accumulator: O(1) per update, true per-update time-weighting.
'   - Time-AWARE EMA: alpha = 1 - exp(-dt / tau), dt = seconds since the previous fold,
'     tau = avg_window_sec. A fixed-alpha EMA would let the effective horizon drift with
'     the (irregular) update rate — this keeps the window meaning what it says.
'   - The folded scalar `ratio` is the SAME sanity-bounded weighted bid/ask imbalance
'     CalcOFI computes (IndicatorEngine.ComputeOfiImbalance), so the downstream vote /
'     classification / momentum ring are untouched in mechanism — only the value of
'     OFIRatio changes (averaged vs snapshot). That value shift is the ⚠ re-baseline
'     (proposal §5) — a later, data-gated pass; this class only produces the cleaner input.
'   - Warmup gate: until the accumulator has >= avg_window_sec of wall-clock coverage AND
'     a small minimum fold count, Snapshot().HasWarmup is False and the caller falls back
'     to the snapshot CalcOFI rather than emitting a half-filled average (proposal §8).
'   - Reset() on (re)connect so a stale pre-disconnect average can't bleed across a gap;
'     the warmup fallback then re-arms after every reconnect (proposal §4.1/§8).
'
' Concurrency: this class is NOT internally locked. It lives on MarketState and is folded/
' read only under MarketState's single SyncLock (single-writer feed thread, multi-reader
' analysis thread) — exactly as proposal §4.1 specifies ("State lives on MarketState under
' its existing SyncLock"). Do not touch it off-lock.
Public NotInheritable Class OfiAccumulator

    ' Anti-degenerate floor: a couple of folds spanning >= the window via a gap shouldn't
    ' count as "warmed up". At the live 100ms book cadence avg_window_sec seconds is many
    ' hundreds of folds, so this floor is trivially met on a healthy feed; it only guards
    ' the pathological sparse case.
    Private Const MinWarmupUpdates As Integer = 5

    Private _hasState       As Boolean = False
    Private _emaRatio       As Double  = 0.0   ' the OFIRatio of record (averaged)
    Private _emaBid         As Double  = 0.0   ' averaged weighted bid volume (display/CSV)
    Private _emaAsk         As Double  = 0.0   ' averaged weighted ask volume (display/CSV)
    Private _lastFoldMs     As Long    = 0      ' epoch-ms of the previous fold (dt basis)
    Private _coverageStartMs As Long   = 0      ' epoch-ms of the first fold since reset
    Private _updateCount    As Integer = 0

    ''' <summary>Clear all state to the cold (no-data) condition. Called on (re)connect so a
    ''' stale average can't survive a feed gap and the warmup fallback re-arms.</summary>
    Public Sub Reset()
        _hasState        = False
        _emaRatio        = 0.0
        _emaBid          = 0.0
        _emaAsk          = 0.0
        _lastFoldMs      = 0
        _coverageStartMs = 0
        _updateCount     = 0
    End Sub

    ''' <summary>Fold one book-update sample into the time-weighted average. `ratio`/`bidVol`/
    ''' `askVol` are the weighted top-book imbalance + volumes (IndicatorEngine.ComputeOfiImbalance);
    ''' `tsMs` is the update's epoch-ms (receive time); `tauSec` is avg_window_sec. The first fold
    ''' after a reset SEEDS the EMA at the sample (no decay); thereafter the time-aware alpha applies.</summary>
    Public Sub Fold(bidVol As Double, askVol As Double, ratio As Double, tsMs As Long, tauSec As Double)
        If Not _hasState Then
            _emaRatio        = ratio
            _emaBid          = bidVol
            _emaAsk          = askVol
            _lastFoldMs      = tsMs
            _coverageStartMs = tsMs
            _updateCount     = 1
            _hasState        = True
            Return
        End If

        ' Time-aware decay. dt floored at 0 so a non-monotonic / same-ms stamp can't produce
        ' a negative (or >1) alpha; tau <= 0 collapses to a full overwrite (no averaging).
        Dim dt As Double = Math.Max(0.0, (tsMs - _lastFoldMs) / 1000.0)
        Dim alpha As Double
        If tauSec <= 0.0 Then
            alpha = 1.0
        Else
            alpha = 1.0 - Math.Exp(-dt / tauSec)
        End If

        _emaRatio += alpha * (ratio - _emaRatio)
        _emaBid   += alpha * (bidVol - _emaBid)
        _emaAsk   += alpha * (askVol - _emaAsk)
        _lastFoldMs  = tsMs
        _updateCount += 1
    End Sub

    ''' <summary>Wall-clock span observed since the first fold (most-recent minus first fold,
    ''' seconds). 0 before any fold. Uses fold stamps, not "now", so it never claims coverage
    ''' the feed hasn't actually delivered (a stalled feed stops accruing coverage).</summary>
    Public ReadOnly Property CoverageSeconds As Double
        Get
            If Not _hasState Then Return 0.0
            Return (_lastFoldMs - _coverageStartMs) / 1000.0
        End Get
    End Property

    Public ReadOnly Property UpdateCount As Integer
        Get
            Return _updateCount
        End Get
    End Property

    ''' <summary>True once the accumulator has >= minCoverageSec of fold coverage and the
    ''' minimum fold count — i.e. the average is over a full window and safe to use.</summary>
    Public Function HasWarmup(minCoverageSec As Double) As Boolean
        Return _hasState AndAlso _updateCount >= MinWarmupUpdates AndAlso CoverageSeconds >= minCoverageSec
    End Function

    ''' <summary>A consistent read of the averaged state plus the warmup verdict for the given
    ''' window. The caller uses the averaged Ratio/Bid/Ask only when HasWarmup is True.</summary>
    Public Function Snapshot(minCoverageSec As Double) As OfiAverageSnapshot
        Return New OfiAverageSnapshot With {
            .HasWarmup   = HasWarmup(minCoverageSec),
            .Ratio       = _emaRatio,
            .BidVol      = _emaBid,
            .AskVol      = _emaAsk,
            .UpdateCount = _updateCount,
            .CoverageSec = CoverageSeconds}
    End Function
End Class

''' <summary>A point-in-time read of the OFI accumulator (proposal §4.2). When HasWarmup is
''' True the caller sources OFIRatio/OFIBidVol/OFIAskVol from Ratio/BidVol/AskVol and classifies
''' Ratio into the OFISignal; otherwise it falls back to the snapshot CalcOFI.</summary>
Public Structure OfiAverageSnapshot
    Public Property HasWarmup   As Boolean
    Public Property Ratio       As Double
    Public Property BidVol      As Double
    Public Property AskVol      As Double
    Public Property UpdateCount As Integer
    Public Property CoverageSec As Double
End Structure
