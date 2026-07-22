' Core/LevelAbsorptionTracker.vb
' WebSocket migration P4 #6 — book absorption at structural levels (docs/book-absorption-proposal.md §4).
'
' A host-agnostic, level-scoped EPISODE tracker: while price is pressing the nearest
' CARRIED structural level (the TAPE strip's candidate set — LastSwingHigh/Low5m +
' VPFRNearestHvnAbove/Below, refreshed each full run), it measures the aggressive USD
' printed into the level band, the band's resting-size trajectory across the ~100 ms
' depth-limited book snapshots, and the D8 pull/post conservation accounting. The first
' DUAL-FED tracker: folded from BOTH the book stream (FoldBook, per snapshot) and the
' trades stream (FoldTrade, per print). MarketState owns it and folds/reads it only
' under its single SyncLock (the OfiAccumulator / AggressorVelocityAccumulator
' discipline) — NOT internally locked; do not touch off-lock.
'
' Per active side (ABOVE = resistance pressed from below; BELOW mirrors), an episode
' runs from proximity-entry until price leaves proximity, the level re-maps, or price
' breaks through (§4.2):
'   - aggrUsd    — rolling window_sec sum of aggressive BUY USD at prices ≥ level − band
'                  (SELL ≤ level + band for the BELOW side). Trades and book sizes share
'                  units: both are USD notional on the inverse contract.
'   - band size  — resting ask USD in [level, level + band] per snapshot (bid USD in
'                  [level − band, level] below): sizeStart at open, sizeMin, sizeNow.
'   - absorbRatio = aggrUsd / max(sizeStart − sizeMin, depletion_floor_usd) — USD traded
'                  into the band per USD of net band depletion. High = the band is
'                  eating flow without dying = absorption.
'   - D8 pull accounting — per snapshot interval the band obeys the conservation
'                  identity ΔSize = Posts − Pulls − Fills, with ΔSize (snapshots) and
'                  Fills (trades at band prices) both observed:
'                    pullLB += max(0, −(ΔSize + Fills))   ' provable pulls (spoof signature)
'                    postLB += max(0, ΔSize + Fills)      ' provable posts
'                  pullFrac = pullLB / max(postLB, depletion_floor_usd). Visibility
'                  mask: ΔSize (and the interval's fills) are computed only over the
'                  band portion visible in BOTH consecutive top-10 snapshots, so a
'                  shifting ladder window cannot fake size deltas.
'
' Progress test: a trade printing (or the opposing touch trading) beyond
' level ± break_tol ends the episode instantly — a broken level must never carry a
' stale ABSORB reading. After a break the side re-arms only once price leaves the
' proximity band (or the level re-maps): no instant re-open at the break price.
'
' Level selection per fold: nearest carried candidate ≥ best ask (ABOVE) / ≤ best bid
' (BELOW), additionally required to sit inside the visible ladder span (§8: a level the
' top-10 ladder can't see ⇒ IDLE — min(proximity, visible) enforced by construction).
' A re-mapped or crossed level closes the episode (no cross-level bleed, §4.1).
'
' Reset on (re)connect in DeribitWsFeed.SeedAsync (same discipline as the OFI /
' aggressor-velocity accumulators) — no pre-gap episode bleeds across a feed gap.
Public NotInheritable Class LevelAbsorptionTracker

    ' Defensive cap on the rolling pressing-volume queue (a 10s window on a violent
    ' tape holds far fewer; the cap only guards a runaway stamp sequence).
    Private Const PressQueueCap As Integer = 4096

    ' Carried candidate levels (0 = absent) — set once per full run by MarketState.
    Private _swingHigh5m As Double = 0.0
    Private _swingLow5m As Double = 0.0
    Private _hvnAbove As Double = 0.0
    Private _hvnBelow As Double = 0.0

    ' [v61 geometry rescale] Resolved dollar distances, refreshed once per full run at
    ' the SetAbsorptionLevels carry site (proximity/band/break_tol as ATR-fractions ×
    ' r.ATR — proposal §1). The tracker keeps working in absolute dollars internally;
    ' only the config→dollars conversion moved out. Zero (cold) ⇒ gate can never open,
    ' so the tracker safely stays IDLE until levels + geometry are carried in.
    Private _proximityUsd As Double = 0.0
    Private _bandUsd As Double = 0.0
    Private _breakTolUsd As Double = 0.0

    ' Previous book snapshot — the conservation interval's "before" side.
    Private _prevBook As OrderBookSnapshot = Nothing

    Private ReadOnly _above As New SideState(isAbove:=True)
    Private ReadOnly _below As New SideState(isAbove:=False)

    ' ── Per-side episode state ──────────────────────────────────────────────────────────
    Private NotInheritable Class SideState
        Public ReadOnly IsAbove As Boolean

        Public Active As Boolean = False
        Public LevelPrice As Double = 0.0
        Public SizeStart As Double = 0.0
        Public SizeMin As Double = 0.0
        Public SizeNow As Double = 0.0
        Public PullLB As Double = 0.0
        Public PostLB As Double = 0.0

        ' Rolling pressing-volume window (episode-scoped).
        Public ReadOnly Press As New Queue(Of (TsMs As Long, Usd As Double))()
        Public PressSum As Double = 0.0

        ' Fills at band prices since the last book fold (the interval's Fills term).
        Public ReadOnly IntervalFills As New List(Of (Price As Double, Usd As Double))()

        ' After a break-through, the side stays idle until price leaves proximity or the
        ' level re-maps (no instant re-open at the break price). 0 = not awaiting re-arm.
        Public BrokenLevel As Double = 0.0

        Public Sub New(isAbove As Boolean)
            Me.IsAbove = isAbove
        End Sub

        Public Sub CloseEpisode()
            Active = False
            LevelPrice = 0.0
            SizeStart = 0.0 : SizeMin = 0.0 : SizeNow = 0.0
            PullLB = 0.0 : PostLB = 0.0
            Press.Clear() : PressSum = 0.0
            IntervalFills.Clear()
        End Sub
    End Class

    ''' <summary>Clear all state to the cold condition (episodes, carried levels, prev
    ''' book). Called on (re)connect so no pre-gap episode bleeds across; the tracker
    ''' re-arms on the next approach after levels are re-carried.</summary>
    Public Sub Reset()
        _above.CloseEpisode()
        _below.CloseEpisode()
        _above.BrokenLevel = 0.0
        _below.BrokenLevel = 0.0
        _swingHigh5m = 0.0 : _swingLow5m = 0.0
        _hvnAbove = 0.0 : _hvnBelow = 0.0
        _proximityUsd = 0.0 : _bandUsd = 0.0 : _breakTolUsd = 0.0
        _prevBook = Nothing
    End Sub

    ''' <summary>Refresh the carried candidate levels + the resolved dollar geometry from
    ''' a completed full run (the strip's carry — §4.1). A mid-episode re-map is handled
    ''' at the next fold: if the side's selected nearest level changes, its episode resets
    ''' (no cross-level bleed). [v61 geometry rescale] proximity/band/break-tol arrive in
    ''' absolute dollars, resolved at the carry site from r.ATR × the ATR-fraction cfg
    ''' keys — the tracker itself stays tick/ATR-agnostic. See
    ''' docs/absorption-geometry-rescale-proposal.md §1.</summary>
    Public Sub SetLevels(swingHigh5m As Double, swingLow5m As Double,
                         hvnAbove As Double, hvnBelow As Double,
                         proximityUsd As Double, bandUsd As Double, breakTolUsd As Double)
        _swingHigh5m = swingHigh5m
        _swingLow5m = swingLow5m
        _hvnAbove = hvnAbove
        _hvnBelow = hvnBelow
        _proximityUsd = Math.Max(proximityUsd, 0.0)
        _bandUsd = Math.Max(bandUsd, 0.0)
        _breakTolUsd = Math.Max(breakTolUsd, 0.0)
    End Sub

    ' ── Trade fold (per streamed print) ─────────────────────────────────────────────────

    ''' <summary>Fold one streamed trade. Pressing volume accrues to an ACTIVE side when
    ''' the print is aggressive toward the level and inside the pressing band; fills at
    ''' band prices accrue to the current conservation interval; a print beyond
    ''' level ± break_tol ends the episode instantly (progress test).</summary>
    Public Sub FoldTrade(price As Double, amountUsd As Double, isBuy As Boolean,
                         tsMs As Long, cfg As AbsorptionSettings)
        If cfg Is Nothing OrElse price <= 0 OrElse amountUsd <= 0 Then Return
        FoldTradeSide(_above, price, amountUsd, isBuy, tsMs, cfg)
        FoldTradeSide(_below, price, amountUsd, isBuy, tsMs, cfg)
    End Sub

    Private Sub FoldTradeSide(side As SideState, price As Double, amountUsd As Double,
                              isBuy As Boolean, tsMs As Long, cfg As AbsorptionSettings)
        If Not side.Active Then Return
        ' [v61 geometry rescale] Distances resolved once per run at SetLevels — the
        ' tracker consumes absolute dollars here (no tick math, no cfg lookup).
        Dim band As Double = _bandUsd
        Dim breakTol As Double = _breakTolUsd
        Dim lvl As Double = side.LevelPrice

        If side.IsAbove Then
            ' Progress: a print beyond the level + tolerance ⇒ the level gave way.
            If price > lvl + breakTol Then
                side.CloseEpisode()
                side.BrokenLevel = lvl
                Return
            End If
            If isBuy Then
                If price >= lvl - band Then AddPress(side, tsMs, amountUsd, cfg.WindowSec)
                If price >= lvl AndAlso price <= lvl + band Then side.IntervalFills.Add((price, amountUsd))
            End If
        Else
            If price < lvl - breakTol Then
                side.CloseEpisode()
                side.BrokenLevel = lvl
                Return
            End If
            If Not isBuy Then
                If price <= lvl + band Then AddPress(side, tsMs, amountUsd, cfg.WindowSec)
                If price <= lvl AndAlso price >= lvl - band Then side.IntervalFills.Add((price, amountUsd))
            End If
        End If
    End Sub

    Private Shared Sub AddPress(side As SideState, tsMs As Long, usd As Double, windowSec As Double)
        side.Press.Enqueue((tsMs, usd))
        side.PressSum += usd
        PrunePress(side, tsMs, windowSec)
        While side.Press.Count > PressQueueCap
            side.PressSum -= side.Press.Dequeue().Usd
        End While
    End Sub

    Private Shared Sub PrunePress(side As SideState, nowMs As Long, windowSec As Double)
        Dim cutoff As Long = nowMs - CLng(Math.Max(windowSec, 0.0) * 1000.0)
        While side.Press.Count > 0 AndAlso side.Press.Peek().TsMs < cutoff
            side.PressSum -= side.Press.Dequeue().Usd
        End While
        If side.Press.Count = 0 Then side.PressSum = 0.0
    End Sub

    ' ── Book fold (per ~100 ms depth-limited snapshot) ──────────────────────────────────

    ''' <summary>Fold one book snapshot: (re)select the watched level per side, run the
    ''' proximity gate (open/close episodes), fold the D8 conservation interval for
    ''' already-active episodes, and update the band-size trajectory.</summary>
    Public Sub FoldBook(snap As OrderBookSnapshot, tsMs As Long, cfg As AbsorptionSettings)
        If cfg Is Nothing OrElse snap Is Nothing Then Return

        Dim bestAsk As Double = 0.0, worstAsk As Double = 0.0
        Dim bestBid As Double = 0.0, worstBid As Double = 0.0
        Dim hasAsks As Boolean = AskSpan(snap, bestAsk, worstAsk)
        Dim hasBids As Boolean = BidSpan(snap, bestBid, worstBid)
        If Not hasAsks OrElse Not hasBids Then
            ' Degenerate/empty ladder — no measurement is honest measurement.
            _above.CloseEpisode()
            _below.CloseEpisode()
            _prevBook = snap
            Return
        End If

        FoldBookSide(_above, snap, bestAsk, worstAsk, bestBid, worstBid, tsMs, cfg)
        FoldBookSide(_below, snap, bestAsk, worstAsk, bestBid, worstBid, tsMs, cfg)
        _prevBook = snap
    End Sub

    Private Sub FoldBookSide(side As SideState, snap As OrderBookSnapshot,
                             bestAsk As Double, worstAsk As Double,
                             bestBid As Double, worstBid As Double,
                             tsMs As Long, cfg As AbsorptionSettings)
        ' [v61 geometry rescale] Distances resolved once per run at SetLevels — the
        ' tracker consumes absolute dollars here (no tick math, no cfg lookup).
        Dim prox As Double = _proximityUsd
        Dim band As Double = _bandUsd
        Dim breakTol As Double = _breakTolUsd

        ' Nearest carried candidate on this side of the touch, required inside the
        ' visible ladder span (§8 — min(proximity, visible) by construction).
        Dim lvl As Double = If(side.IsAbove,
                               NearestAbove(bestAsk, worstAsk),
                               NearestBelow(bestBid, worstBid))

        ' Proximity gate against the touch price.
        Dim gateOpen As Boolean = False
        If lvl > 0 Then
            gateOpen = If(side.IsAbove, lvl - bestAsk <= prox, bestBid - lvl <= prox)
        End If

        ' Break-through re-arm: cleared once the broken level re-maps or price leaves
        ' its proximity band.
        If side.BrokenLevel > 0 Then
            If lvl <> side.BrokenLevel OrElse Not gateOpen Then
                side.BrokenLevel = 0.0
            Else
                Return   ' still parked at the broken level — stay idle
            End If
        End If

        ' Level re-map / cross mid-episode ⇒ episode resets (no cross-level bleed).
        If side.Active AndAlso lvl <> side.LevelPrice Then side.CloseEpisode()

        If Not gateOpen Then
            If side.Active Then side.CloseEpisode()
            Return
        End If

        ' Progress via the opposing touch (price trading through the level).
        If side.Active Then
            Dim broken As Boolean = If(side.IsAbove,
                                       bestBid > side.LevelPrice + breakTol,
                                       bestAsk < side.LevelPrice - breakTol)
            If broken Then
                Dim lvlBroken As Double = side.LevelPrice
                side.CloseEpisode()
                side.BrokenLevel = lvlBroken
                Return
            End If
        End If

        Dim bandLo As Double = If(side.IsAbove, lvl, lvl - band)
        Dim bandHi As Double = If(side.IsAbove, lvl + band, lvl)
        Dim bandSize As Double = If(side.IsAbove,
                                    SumAsks(snap, bandLo, bandHi),
                                    SumBids(snap, bandLo, bandHi))

        If Not side.Active Then
            ' Episode opens on the first in-proximity snapshot.
            side.Active = True
            side.LevelPrice = lvl
            side.SizeStart = bandSize
            side.SizeMin = bandSize
            side.SizeNow = bandSize
            side.PullLB = 0.0 : side.PostLB = 0.0
            side.Press.Clear() : side.PressSum = 0.0
            side.IntervalFills.Clear()
            Return
        End If

        ' D8 conservation interval (already-active episode, prev snapshot available):
        ' ΔSize + Fills = Posts − Pulls, masked to the band portion visible in BOTH
        ' consecutive snapshots.
        If _prevBook IsNot Nothing Then
            Dim pBestAsk As Double = 0.0, pWorstAsk As Double = 0.0
            Dim pBestBid As Double = 0.0, pWorstBid As Double = 0.0
            Dim maskLo As Double = 0.0, maskHi As Double = -1.0
            If side.IsAbove Then
                If AskSpan(_prevBook, pBestAsk, pWorstAsk) Then
                    maskLo = Math.Max(bandLo, Math.Max(pBestAsk, bestAsk))
                    maskHi = Math.Min(bandHi, Math.Min(pWorstAsk, worstAsk))
                End If
            Else
                If BidSpan(_prevBook, pBestBid, pWorstBid) Then
                    maskLo = Math.Max(bandLo, Math.Max(pWorstBid, worstBid))
                    maskHi = Math.Min(bandHi, Math.Min(pBestBid, bestBid))
                End If
            End If
            If maskHi >= maskLo Then
                Dim sizePrev As Double = If(side.IsAbove,
                                            SumAsks(_prevBook, maskLo, maskHi),
                                            SumBids(_prevBook, maskLo, maskHi))
                Dim sizeNow As Double = If(side.IsAbove,
                                           SumAsks(snap, maskLo, maskHi),
                                           SumBids(snap, maskLo, maskHi))
                Dim fills As Double = 0.0
                For Each f In side.IntervalFills
                    If f.Price >= maskLo AndAlso f.Price <= maskHi Then fills += f.Usd
                Next
                Dim net As Double = (sizeNow - sizePrev) + fills   ' = Posts − Pulls
                side.PullLB += Math.Max(0.0, -net)
                side.PostLB += Math.Max(0.0, net)
            End If
        End If
        side.IntervalFills.Clear()

        side.SizeNow = bandSize
        If bandSize < side.SizeMin Then side.SizeMin = bandSize
        PrunePress(side, tsMs, cfg.WindowSec)
    End Sub

    ' ── Candidate / ladder helpers ──────────────────────────────────────────────────────

    Private Function NearestAbove(bestAsk As Double, worstAsk As Double) As Double
        Dim best As Double = 0.0
        For Each c As Double In {_swingHigh5m, _swingLow5m, _hvnAbove, _hvnBelow}
            If c >= bestAsk AndAlso c <= worstAsk AndAlso c > 0 Then
                If best = 0.0 OrElse c < best Then best = c
            End If
        Next
        Return best
    End Function

    Private Function NearestBelow(bestBid As Double, worstBid As Double) As Double
        Dim best As Double = 0.0
        For Each c As Double In {_swingHigh5m, _swingLow5m, _hvnAbove, _hvnBelow}
            If c <= bestBid AndAlso c >= worstBid AndAlso c > 0 Then
                If best = 0.0 OrElse c > best Then best = c
            End If
        Next
        Return best
    End Function

    Private Shared Function AskSpan(snap As OrderBookSnapshot,
                                    ByRef best As Double, ByRef worst As Double) As Boolean
        If snap.Asks Is Nothing OrElse snap.Asks.Count = 0 Then Return False
        best = Double.MaxValue : worst = 0.0
        For Each lvl In snap.Asks
            If lvl.Price < best Then best = lvl.Price
            If lvl.Price > worst Then worst = lvl.Price
        Next
        Return worst > 0
    End Function

    ''' <summary>Bid span: best = highest bid, worst = lowest visible bid.</summary>
    Private Shared Function BidSpan(snap As OrderBookSnapshot,
                                    ByRef best As Double, ByRef worst As Double) As Boolean
        If snap.Bids Is Nothing OrElse snap.Bids.Count = 0 Then Return False
        best = 0.0 : worst = Double.MaxValue
        For Each lvl In snap.Bids
            If lvl.Price > best Then best = lvl.Price
            If lvl.Price < worst Then worst = lvl.Price
        Next
        Return best > 0
    End Function

    Private Shared Function SumAsks(snap As OrderBookSnapshot, lo As Double, hi As Double) As Double
        Dim total As Double = 0.0
        If snap.Asks Is Nothing Then Return 0.0
        For Each lvl In snap.Asks
            If lvl.Price >= lo AndAlso lvl.Price <= hi Then total += lvl.Size
        Next
        Return total
    End Function

    Private Shared Function SumBids(snap As OrderBookSnapshot, lo As Double, hi As Double) As Double
        Dim total As Double = 0.0
        If snap.Bids Is Nothing Then Return 0.0
        For Each lvl In snap.Bids
            If lvl.Price >= lo AndAlso lvl.Price <= hi Then total += lvl.Size
        Next
        Return total
    End Function

    ' ── Read ────────────────────────────────────────────────────────────────────────────

    ''' <summary>A consistent point-in-time read of both sides. nowMs prunes the rolling
    ''' pressing windows (pass a fixed value for deterministic tests); ratio math uses
    ''' the cfg passed at read time (hot-reload honest). The caller classifies via
    ''' IndicatorEngine.ClassifyAbsorption.</summary>
    Public Function Snapshot(nowMs As Long, cfg As AbsorptionSettings) As AbsorptionSnapshot
        Dim floorUsd As Double = If(cfg Is Nothing, 1.0, Math.Max(cfg.DepletionFloorUsd, 0.000001))
        Dim win As Double = If(cfg Is Nothing, 10.0, cfg.WindowSec)
        PrunePress(_above, nowMs, win)
        PrunePress(_below, nowMs, win)
        Return New AbsorptionSnapshot With {
            .Above = ReadSide(_above, floorUsd),
            .Below = ReadSide(_below, floorUsd)}
    End Function

    Private Shared Function ReadSide(side As SideState, floorUsd As Double) As AbsorptionSideRead
        If Not side.Active Then Return New AbsorptionSideRead()
        Dim depletion As Double = Math.Max(side.SizeStart - side.SizeMin, floorUsd)
        Return New AbsorptionSideRead With {
            .Active = True,
            .LevelPrice = side.LevelPrice,
            .AggrUsd = side.PressSum,
            .AbsorbRatio = side.PressSum / depletion,
            .PullFrac = side.PullLB / Math.Max(side.PostLB, floorUsd)}
    End Function
End Class

''' <summary>One side's episode read (§4.2). Active=False ⇒ IDLE — every numeric is
''' meaningless and must not be surfaced.</summary>
Public Structure AbsorptionSideRead
    Public Property Active As Boolean
    Public Property LevelPrice As Double
    Public Property AggrUsd As Double
    Public Property AbsorbRatio As Double
    Public Property PullFrac As Double
End Structure

''' <summary>A point-in-time read of the absorption tracker (both sides). Classified by
''' IndicatorEngine.ClassifyAbsorption (pure) against the session-resolved min_aggr_usd
''' + absorb_ratio + the D8 max_pull_frac veto.</summary>
Public Structure AbsorptionSnapshot
    Public Property Above As AbsorptionSideRead
    Public Property Below As AbsorptionSideRead
End Structure

''' <summary>The classified read (IndicatorEngine.ClassifyAbsorption): the §4.2 state
''' plus the primary episode's numerics for CSV/display. HasEpisode=False ⇒ NONE with
''' no episode active — the numerics stay unsurfaced (null CSV columns, §4.3).</summary>
Public Structure AbsorptionRead
    Public Property Signal As String        ' "ABSORB_ABOVE" / "ABSORB_BELOW" / "NONE"
    Public Property HasEpisode As Boolean
    Public Property LevelPrice As Double
    Public Property AbsorbRatio As Double
    Public Property AggrUsd As Double
    Public Property PullFrac As Double
End Structure
