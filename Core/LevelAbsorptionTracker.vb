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
'   - aggrUsd    — [D-2, docs/absorption-d2-stage1-rotation-build-spec.md §3] the
'                  EPISODE-CUMULATIVE sum of aggressive BUY USD at prices ≥ level − band
'                  (SELL ≤ level + band for the BELOW side), over [episodeOpen, now].
'                  Before D-2 it was a rolling window_sec sum; because both close paths
'                  cleared the queue, that was already the sum over
'                  [max(episodeOpen, now − window_sec), now], so D-2 is byte-identical
'                  below window_sec and strictly larger above it. It puts the numerator
'                  on the same span as the episode-scoped depletion denominator
'                  (d6d-episode-continuity-spec.md §2.2a). window_sec is now UNUSED by
'                  the press path (build spec R-4: the key stays; removing it is a
'                  separate settings change). Trades and book sizes share units: both
'                  are USD notional on the inverse contract.
'   - [D-6d Stage 1, d6d-episode-continuity-spec.md §4] a behaviour-neutral
'                  instrument: the shadow-press accumulator (flow an IDLE side drops at
'                  the FoldTradeSide early-out, tested against the level it last
'                  watched) and close-reason attribution. Neither touches PressSum,
'                  SizeStart, SizeMin, PullLB, PostLB, absorbRatio or any read field;
'                  the run path drains it once per run via TakeInstrument.
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

    ' [D-2] The rolling press queue and its PressQueueCap (4096) are GONE. The cap was
    ' sized for a 10-second window; under episode-cumulative pressing a 253-second
    ' episode at a busy moment could exceed it, and its overflow arm dropped the OLDEST
    ' presses — a silent truncation of exactly the quantity D-2 measures. Nothing read
    ' the queue except the prune, so with no prune a running sum is the whole state:
    ' no cap, no truncation, O(1) memory (build spec §3 — "raise it or state why").

    ' Last value handed out by TakeInstrument (0 = never taken this process).
    Private _lastTakeMs As Long = 0

    ' [D-6d Stage 1] Number of AbsorptionCloseReason members (the Tally array length).
    Friend Const CloseReasonCount As Integer = 7

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

        ' [absorption instrumentation R2] Fold stamp of the snapshot that OPENED this
        ' episode — the only new state the instrumentation build adds. Set at episode
        ' open beside LevelPrice, cleared in CloseEpisode(), so the invariant is
        ' Active ⇒ EpisodeStartMs > 0. ReadSide turns it into EpisodeSec against the
        ' read's nowMs; both stamps come from the same receive-time clock (the book
        ' fold stamps DeribitWsFeed's nowUtc, Snapshot is called with UtcNow), so the
        ' difference is non-negative by construction, not by clamping.
        Public EpisodeStartMs As Long = 0

        Public SizeStart As Double = 0.0
        Public SizeMin As Double = 0.0
        Public SizeNow As Double = 0.0
        Public PullLB As Double = 0.0
        Public PostLB As Double = 0.0

        ' [D-2] Episode-cumulative pressing volume: the sum over [episodeOpen, now].
        ' Cleared only at episode open and in CloseEpisode — never pruned by time.
        Public PressSum As Double = 0.0

        ' Fills at band prices since the last book fold (the interval's Fills term).
        Public ReadOnly IntervalFills As New List(Of (Price As Double, Usd As Double))()

        ' After a break-through, the side stays idle until price leaves proximity or the
        ' level re-maps (no instant re-open at the break price). 0 = not awaiting re-arm.
        Public BrokenLevel As Double = 0.0

        ' ── [D-6d Stage 1] instrument state — NEVER read by the live path ──────────
        ' The level this side last watched. Set at episode open beside LevelPrice and
        ' ⛔ DELIBERATELY NOT cleared by CloseEpisode(), so an idle side still knows what
        ' it was watching (d6d spec §4.1; fixture A88e).
        Public LastLevelPrice As Double = 0.0
        ' Why the side last went idle (valid once LastLevelPrice > 0).
        Public LastCloseReason As AbsorptionCloseReason = AbsorptionCloseReason.Reset
        ' True from a real close until the idle interval's first Break-class print
        ' against LastLevelPrice: that print would have ended the episode too, so flow
        ' after it would not have been counted either.
        Public ShadowArmed As Boolean = False
        ' Flow dropped at the idle early-out that the live predicate would have counted
        ' against LastLevelPrice. Drained per run by TakeInstrument.
        Public ShadowPressUsd As Double = 0.0
        Public ShadowPressCount As Integer = 0
        ' Live press accrued since the last take — the counting gap's denominator term,
        ' an interval flow like ShadowPressUsd (PressSum is an episode sum). Write-only
        ' from the live path; read only by TakeInstrument.
        Public PressAccruedUsd As Double = 0.0
        Public ShadowBreaks As Integer = 0
        ' Per close reason, indexed by CInt(AbsorptionCloseReason). Drained per run.
        Public ReadOnly Tally(CloseReasonCount - 1) As AbsorptionReasonTally

        Public Sub New(isAbove As Boolean)
            Me.IsAbove = isAbove
        End Sub

        ''' <summary>End the episode. [D-6d Stage 1] reason is REQUIRED (not Optional —
        ''' an Optional default would be a third copy of a value, d6d spec §4.2). A close
        ''' of an ACTIVE side is tallied (count, the PressSum it discards, its lifetime)
        ''' and arms the shadow arm; closing an already-idle side (Reset / degenerate
        ''' ladder) records nothing. nowMs ≤ 0 ⇒ lifetime not recorded (Reset has no
        ''' clock). Nothing here feeds back into the live state below.</summary>
        Public Sub CloseEpisode(reason As AbsorptionCloseReason, nowMs As Long)
            If Active Then
                Dim i As Integer = CInt(reason)
                Tally(i).Count += 1
                Tally(i).DiscardedUsd += PressSum
                If nowMs > 0 AndAlso EpisodeStartMs > 0 Then
                    ' Trade stamps are exchange time and book stamps receive time; the
                    ' clamp keeps a few-ms skew from writing a negative lifetime.
                    Tally(i).LifeSec += Math.Max(0L, nowMs - EpisodeStartMs) / 1000.0
                End If
                LastCloseReason = reason
                ShadowArmed = True
            End If
            Active = False
            LevelPrice = 0.0
            EpisodeStartMs = 0
            SizeStart = 0.0 : SizeMin = 0.0 : SizeNow = 0.0
            PullLB = 0.0 : PostLB = 0.0
            PressSum = 0.0
            IntervalFills.Clear()
        End Sub
    End Class

    ''' <summary>Clear all state to the cold condition (episodes, carried levels, prev
    ''' book). Called on (re)connect so no pre-gap episode bleeds across; the tracker
    ''' re-arms on the next approach after levels are re-carried.</summary>
    Public Sub Reset()
        ' [D-6d Stage 1] Reset has no clock, so a Reset close records no lifetime.
        _above.CloseEpisode(AbsorptionCloseReason.Reset, 0L)
        _below.CloseEpisode(AbsorptionCloseReason.Reset, 0L)
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
        ' [v61 geometry rescale] Distances resolved once per run at SetLevels — the
        ' tracker consumes absolute dollars here (no tick math, no cfg lookup).
        Dim band As Double = _bandUsd
        Dim breakTol As Double = _breakTolUsd

        If Not side.Active Then
            ' [D-6d Stage 1] The idle early-out is where flow is DROPPED (d6d spec §2,
            ' trap T-2: a close-time record cannot see it). Run the SAME predicate the
            ' live arm runs, against the level this side last watched, and accrue ONLY
            ' to the shadow fields. ⛔ Never PressSum (trap T-3 / T-5; fixture A88d).
            If side.ShadowArmed AndAlso side.LastLevelPrice > 0 Then
                Select Case ClassifyPrint(side.LastLevelPrice, price, band, breakTol, isBuy, side.IsAbove)
                    Case AbsorptionPrintClass.Press
                        side.ShadowPressUsd += amountUsd
                        side.ShadowPressCount += 1
                        side.Tally(CInt(side.LastCloseReason)).ShadowUsd += amountUsd
                    Case AbsorptionPrintClass.Break
                        ' This print would have ended the episode too — later flow in
                        ' this idle interval would not have been counted either.
                        side.ShadowArmed = False
                        side.ShadowBreaks += 1
                End Select
            End If
            Return
        End If

        Dim lvl As Double = side.LevelPrice
        Select Case ClassifyPrint(lvl, price, band, breakTol, isBuy, side.IsAbove)
            Case AbsorptionPrintClass.Break
                ' Progress: a print beyond the level ± tolerance ⇒ the level gave way.
                side.CloseEpisode(AbsorptionCloseReason.BreakThrough, tsMs)
                side.BrokenLevel = lvl
                Return
            Case AbsorptionPrintClass.Press
                AddPress(side, amountUsd)
        End Select
        ' Fills at band prices (the conservation interval's Fills term) — aggression
        ' toward the level, inside [level, level + band] (mirrored below).
        If side.IsAbove Then
            If isBuy AndAlso price >= lvl AndAlso price <= lvl + band Then side.IntervalFills.Add((price, amountUsd))
        Else
            If Not isBuy AndAlso price <= lvl AndAlso price >= lvl - band Then side.IntervalFills.Add((price, amountUsd))
        End If
    End Sub

    ''' <summary>[D-6d Stage 1, build spec R-6] THE pressing predicate — the ONE
    ''' expression the live arm and the shadow arm both call. A second copy is the
    ''' fixture-literal provenance failure in executable form: it drifts the first time
    ''' the live predicate moves and the instrument then measures something else
    ''' (fixture A88b). ABOVE: a print above level + breakTol is a Break; else an
    ''' aggressive BUY at or above level − band is Press. BELOW mirrors (SELL, below
    ''' level − breakTol breaks, at or below level + band presses).</summary>
    Friend Shared Function ClassifyPrint(level As Double, price As Double, band As Double,
                                         breakTol As Double, isBuy As Boolean,
                                         isAbove As Boolean) As AbsorptionPrintClass
        If isAbove Then
            If price > level + breakTol Then Return AbsorptionPrintClass.Break
            If isBuy AndAlso price >= level - band Then Return AbsorptionPrintClass.Press
        Else
            If price < level - breakTol Then Return AbsorptionPrintClass.Break
            If Not isBuy AndAlso price <= level + band Then Return AbsorptionPrintClass.Press
        End If
        Return AbsorptionPrintClass.Ignore
    End Function

    ' [D-2] Episode-cumulative: no window, no prune, no cap (see the class header).
    Private Shared Sub AddPress(side As SideState, usd As Double)
        side.PressSum += usd
        side.PressAccruedUsd += usd    ' [D-6d Stage 1] instrument only — never read back
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
            _above.CloseEpisode(AbsorptionCloseReason.DegenerateLadder, tsMs)
            _below.CloseEpisode(AbsorptionCloseReason.DegenerateLadder, tsMs)
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
        ' [D-6d Stage 1] This test — not the gate test below — is where lvl = 0 lands,
        ' because 0 <> LevelPrice. ClassifyRemapClose names which of four things moved.
        If side.Active AndAlso lvl <> side.LevelPrice Then
            side.CloseEpisode(ClassifyRemapClose(side.IsAbove, side.LevelPrice, prox,
                                                 bestAsk, worstAsk, bestBid, worstBid), tsMs)
        End If

        If Not gateOpen Then
            ' Reached with the side still active only when lvl = LevelPrice: the level
            ' is still selected and visible, and the touch moved beyond proximity.
            If side.Active Then side.CloseEpisode(AbsorptionCloseReason.ProximityShut, tsMs)
            Return
        End If

        ' Progress via the opposing touch (price trading through the level).
        If side.Active Then
            Dim broken As Boolean = If(side.IsAbove,
                                       bestBid > side.LevelPrice + breakTol,
                                       bestAsk < side.LevelPrice - breakTol)
            If broken Then
                Dim lvlBroken As Double = side.LevelPrice
                side.CloseEpisode(AbsorptionCloseReason.BreakThrough, tsMs)
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
            ' [D-6a, ruled 2026-09-01] SizeStart samples the BAND (band_atr_frac) at the
            ' PROXIMITY (proximity_atr_frac) instant — arm-early / measure-tight, so the
            ' baseline is captured before price arrives. Collapsing the two shells shrinks
            ' the depletion denominator and inflates absorbRatio. See the proposal §4.3a.
            side.Active = True
            side.LevelPrice = lvl
            side.LastLevelPrice = lvl          ' [D-6d Stage 1] survives CloseEpisode
            side.ShadowArmed = False           ' shadow runs only while idle after a close
            side.EpisodeStartMs = tsMs         ' [R2] episode age baseline — see SideState
            side.SizeStart = bandSize
            side.SizeMin = bandSize
            side.SizeNow = bandSize
            side.PullLB = 0.0 : side.PostLB = 0.0
            side.PressSum = 0.0
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
        ' [D-2] No PrunePress here any more — pressing is episode-cumulative.
    End Sub

    ''' <summary>[D-6d Stage 1] Name the close that the re-map test forces, from where
    ''' the OLD level now sits against the current ladder. ⚠ The build spec's §4.2 says
    ''' ProximityShut and LadderSpanLost both reach the gate test and split on lvl = 0.
    ''' In the code lvl = 0 closes at the re-map test first, and lvl = 0 also covers the
    ''' touch trading through the level — so the split is made on geometry instead:
    ''' <list type="bullet">
    ''' <item>TouchCrossed — the touch moved THROUGH the old level (ABOVE: best ask above
    ''' it) without a break-tolerance breach. Price progressing, not the suspect.</item>
    ''' <item>LadderSpanLost — the old level sits beyond the far edge of the visible
    ''' ladder BUT still within proximity of the touch: only the ten-deep book closed
    ''' it. THE SUSPECT (d6d spec §2.3 path 3).</item>
    ''' <item>ProximityShut — beyond the far edge AND beyond proximity: price genuinely
    ''' left; the ladder was not the binding term.</item>
    ''' <item>LevelRemap — the old level is still inside the visible span: another
    ''' candidate became nearest, or the carried set changed (exact Double compare).</item>
    ''' </list></summary>
    Friend Shared Function ClassifyRemapClose(isAbove As Boolean, oldLevel As Double,
                                              proximityUsd As Double,
                                              bestAsk As Double, worstAsk As Double,
                                              bestBid As Double, worstBid As Double) As AbsorptionCloseReason
        If isAbove Then
            If oldLevel < bestAsk Then Return AbsorptionCloseReason.TouchCrossed
            If oldLevel > worstAsk Then
                Return If(oldLevel - bestAsk <= proximityUsd,
                          AbsorptionCloseReason.LadderSpanLost,
                          AbsorptionCloseReason.ProximityShut)
            End If
        Else
            If oldLevel > bestBid Then Return AbsorptionCloseReason.TouchCrossed
            If oldLevel < worstBid Then
                Return If(bestBid - oldLevel <= proximityUsd,
                          AbsorptionCloseReason.LadderSpanLost,
                          AbsorptionCloseReason.ProximityShut)
            End If
        End If
        Return AbsorptionCloseReason.LevelRemap
    End Function

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

    ''' <summary>A consistent point-in-time read of both sides. nowMs dates the episode
    ''' age (pass a fixed value for deterministic tests); ratio math uses the cfg passed
    ''' at read time (hot-reload honest). The caller classifies via
    ''' IndicatorEngine.ClassifyAbsorption. [D-2] The read no longer prunes anything, so
    ''' it no longer mutates state: the live strip and the run read the same sums.</summary>
    Public Function Snapshot(nowMs As Long, cfg As AbsorptionSettings) As AbsorptionSnapshot
        Dim floorUsd As Double = If(cfg Is Nothing, 1.0, Math.Max(cfg.DepletionFloorUsd, 0.000001))
        Return New AbsorptionSnapshot With {
            .Above = ReadSide(_above, nowMs, floorUsd),
            .Below = ReadSide(_below, nowMs, floorUsd)}
    End Function

    ''' <summary>[D-6d Stage 1] Drain the instrument: return both sides' shadow totals
    ''' and per-reason tallies accumulated since the previous take, then zero them.
    ''' ⛔ Call ONLY from the run path, once per run — the live strip reads Snapshot
    ''' every tick, and draining there would leave each run's line nearly empty.
    ''' Live state (PressSum, episodes, LastLevelPrice, ShadowArmed) is read, never
    ''' reset. IntervalSec is Nothing on the first take of the process.</summary>
    Friend Function TakeInstrument(nowMs As Long) As AbsorptionInstrumentRead
        Dim read As New AbsorptionInstrumentRead With {
            .NowMs = nowMs,
            .IntervalSec = If(_lastTakeMs > 0, CType(Math.Max(0L, nowMs - _lastTakeMs) / 1000.0, Double?), Nothing),
            .Above = TakeSide(_above, nowMs),
            .Below = TakeSide(_below, nowMs)}
        _lastTakeMs = nowMs
        Return read
    End Function

    Private Shared Function TakeSide(side As SideState, nowMs As Long) As AbsorptionSideInstrument
        Dim tally(CloseReasonCount - 1) As AbsorptionReasonTally
        Array.Copy(side.Tally, tally, CloseReasonCount)
        Dim s As New AbsorptionSideInstrument With {
            .Active = side.Active,
            .LevelPrice = side.LevelPrice,
            .LastLevelPrice = side.LastLevelPrice,
            .PressSum = side.PressSum,
            .PressAccruedUsd = side.PressAccruedUsd,
            .EpisodeSec = If(side.Active AndAlso side.EpisodeStartMs > 0L,
                             CType((nowMs - side.EpisodeStartMs) / 1000.0, Double?), Nothing),
            .ShadowPressUsd = side.ShadowPressUsd,
            .ShadowPressCount = side.ShadowPressCount,
            .ShadowBreaks = side.ShadowBreaks,
            .Tally = tally}
        side.ShadowPressUsd = 0.0
        side.ShadowPressCount = 0
        side.ShadowBreaks = 0
        side.PressAccruedUsd = 0.0
        Array.Clear(side.Tally, 0, CloseReasonCount)
        Return s
    End Function

    ' [absorption instrumentation R2] nowMs is threaded in from Snapshot (its only call
    ' site, which already receives it) rather than read from a clock here — a read must
    ' stay deterministic for the fixtures and consistent across both sides of one snapshot.
    Private Shared Function ReadSide(side As SideState, nowMs As Long, floorUsd As Double) As AbsorptionSideRead
        If Not side.Active Then Return New AbsorptionSideRead()
        Dim depletion As Double = Math.Max(side.SizeStart - side.SizeMin, floorUsd)
        ' Active ⇒ EpisodeStartMs > 0 (set at open, cleared at close). The guard makes a
        ' broken invariant read 0 rather than the whole unix epoch in seconds.
        Dim episodeSec As Double = If(side.EpisodeStartMs > 0L,
                                      (nowMs - side.EpisodeStartMs) / 1000.0, 0.0)
        Return New AbsorptionSideRead With {
            .Active = True,
            .LevelPrice = side.LevelPrice,
            .AggrUsd = side.PressSum,
            .AbsorbRatio = side.PressSum / depletion,
            .PullFrac = side.PullLB / Math.Max(side.PostLB, floorUsd),
            .EpisodeSec = episodeSec,
            .PullLB = side.PullLB,
            .PostLB = side.PostLB,
            .SizeStart = side.SizeStart,
            .SizeMin = side.SizeMin}
    End Function
End Class

''' <summary>[D-6d Stage 1] Why a side stopped being Active. The six members of
''' d6d-episode-continuity-spec.md §4.2 plus TouchCrossed, which the build added
''' because the spec's own split (lvl = 0) also caught the touch trading through the
''' level — see LevelAbsorptionTracker.ClassifyRemapClose. ⛔ ProximityShut and
''' LadderSpanLost stay SEPARATE: collapsing them destroys the answer Stage 1 exists
''' to get (fixture A88c). Values index SideState.Tally; append new members only at
''' the end and bump CloseReasonCount.</summary>
Friend Enum AbsorptionCloseReason
    DegenerateLadder = 0
    LevelRemap = 1
    ProximityShut = 2
    LadderSpanLost = 3
    BreakThrough = 4
    Reset = 5
    TouchCrossed = 6
End Enum

''' <summary>[D-6d Stage 1] What the shared pressing predicate says about one print.</summary>
Friend Enum AbsorptionPrintClass
    Ignore = 0
    Press = 1
    Break = 2
End Enum

''' <summary>[D-6d Stage 1] One close reason's tally since the last take.
''' Count = active episodes closed · DiscardedUsd = the PressSum those closes wiped ·
''' LifeSec = their summed lifetimes (Reset closes excluded — no clock) · ShadowUsd =
''' shadow press accrued while the side sat idle after a close of this reason.</summary>
Friend Structure AbsorptionReasonTally
    Public Count As Integer
    Public DiscardedUsd As Double
    Public LifeSec As Double
    Public ShadowUsd As Double
End Structure

''' <summary>[D-6d Stage 1] One side's drained instrument (TakeInstrument).</summary>
Friend NotInheritable Class AbsorptionSideInstrument
    Public Active As Boolean
    Public LevelPrice As Double
    Public LastLevelPrice As Double
    Public PressSum As Double
    Public PressAccruedUsd As Double       ' live press accrued since the previous take
    Public EpisodeSec As Double?           ' Nothing while idle
    Public ShadowPressUsd As Double
    Public ShadowPressCount As Integer
    Public ShadowBreaks As Integer
    Public Tally As AbsorptionReasonTally() ' indexed by CInt(AbsorptionCloseReason)
End Class

''' <summary>[D-6d Stage 1] Both sides' drained instrument, taken once per run.</summary>
Friend NotInheritable Class AbsorptionInstrumentRead
    Public NowMs As Long
    Public IntervalSec As Double?          ' Nothing on the first take of the process
    Public Above As AbsorptionSideInstrument
    Public Below As AbsorptionSideInstrument
End Class

''' <summary>One side's episode read (§4.2). Active=False ⇒ IDLE — every numeric is
''' meaningless and must not be surfaced.</summary>
Public Structure AbsorptionSideRead
    Public Property Active As Boolean
    Public Property LevelPrice As Double
    Public Property AggrUsd As Double
    Public Property AbsorbRatio As Double
    Public Property PullFrac As Double

    ' [absorption instrumentation, docs/absorption-instrumentation-spec.md §1] The five
    ' diagnostic quantities the read boundary used to throw away. CSV-only — R3 keeps
    ' them off the live strip. Active=False ⇒ meaningless, same as the four above.
    Public Property EpisodeSec As Double     ' episode age at the read instant (seconds)
    Public Property PullLB As Double         ' D8 provable pulls, USD (PullFrac numerator)
    Public Property PostLB As Double         ' D8 provable posts, USD (PullFrac denominator)
    Public Property SizeStart As Double      ' band size at episode open, USD
    Public Property SizeMin As Double        ' minimum band size seen this episode, USD
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

    ' [absorption instrumentation] The PRIMARY episode's five diagnostic quantities,
    ' carried for CSV alongside the four above. HasEpisode=False ⇒ meaningless.
    Public Property EpisodeSec As Double
    Public Property PullLB As Double
    Public Property PostLB As Double
    Public Property SizeStart As Double
    Public Property SizeMin As Double
End Structure
