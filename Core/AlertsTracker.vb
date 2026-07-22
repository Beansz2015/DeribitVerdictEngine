' Core/AlertsTracker.vb
' Liquidation-cascade alarm (#7) + level-approach alerts (#8) — docs/liq-cascade-level-alerts-proposal.md.
'
' H1 alert surface: TAPE-strip tag + status-bar flash (exit-guard / #6-ABS pattern);
' sound_enabled optional, default OFF.
' H2 cascade: >= cascade_min_trades liq-flagged trades within cascade_window_sec (either side);
'             dominant side names the direction; provisional anchors (proposal §3).
' H3 level-approach: price within level_ticks of a CARRIED level (5m swing + VPFR HVN; never
'                    recomputed here — the TAPE strip's candidate set); once per approach
'                    episode, re-arm on leave (the #6 absorption-episode pattern).
' H4 (AMENDED): first-liq-seen event is PERSISTED to a tiny append-only sidecar
'   `liq_events.log` beside the CSV — one line per event:
'       utc | kind(FIRST_SEEN|CASCADE) | side | usd | instance_id
'   Written on the FIRST liq-flagged trade EVER observed AND on every cascade trigger.
'   The strip-tooltip flag reads the file's existence, so it survives restarts (the
'   durable A4 gate evidence — "does liq_events.log contain >=1 CASCADE line" is the
'   A4 unlock check).
'
' Host-agnostic (no WinForms). Owned by MarketState under its ONE SyncLock — do NOT
' touch off-lock. Reset on (re)connect in DeribitWsFeed.SeedAsync (the #4/#5/#6 discipline).
' DISPLAY/ALERT ONLY — ZERO scoring impact, no card/snapshot/CSV column (proposal §4).

Imports System.IO

Public NotInheritable Class AlertsTracker

    ' Defensive cap on the liq-flagged trade ring (a 10s window on a violent tape holds
    ' far fewer; the cap only guards a runaway stamp sequence — the #6 PressQueueCap idiom).
    Private Const LiqQueueCap As Integer = 4096

    ' Carried candidate levels (0 = absent), set once per full run.
    Private _swingHigh5m As Double = 0.0
    Private _swingLow5m As Double = 0.0
    Private _hvnAbove As Double = 0.0
    Private _hvnBelow As Double = 0.0

    ' Rolling liq-flagged trade window: (TsMs, IsBuy, Usd). Ordered by folding order (the
    ' trades stream is chronological ascending — the F1 contract), pruned on Fold + Snapshot.
    Private ReadOnly _liq As New Queue(Of (TsMs As Long, IsBuy As Boolean, Usd As Double))()

    ' Cascade edge-detector: True while the last snapshot met the cascade threshold, so a
    ' repeated read doesn't double-fire the event / sound / sidecar line.
    Private _cascadeActive As Boolean = False

    ' Per-side approach episodes (H3). Once active, re-arms only when price leaves.
    Private _approachAbove As New ApproachState()
    Private _approachBelow As New ApproachState()

    ' Last observed price (for the level-approach test; updated per trade fold).
    Private _lastPrice As Double = 0.0

    ' Per-process first-liq-seen flag — persistence is via the sidecar file's existence
    ' (H4 amended). We ALSO carry the flag in memory to avoid touching the disk from
    ' the fold hot path once we've written the sentinel.
    Private _firstSeenWritten As Boolean = False

    ' Pending events emitted since the last Snapshot read — drained + returned there so
    ' the WinForms host writes them once per strip tick (the tracker itself never touches
    ' System.Media / audio). Sidecar append IS done in-tracker (append-only, never-throws)
    ' because it is the persistence half of the H4 amendment.
    Private ReadOnly _pending As New List(Of AlertEvent)()

    Private NotInheritable Class ApproachState
        Public Active As Boolean = False
        Public LevelPrice As Double = 0.0
    End Class

    ''' <summary>Clear all state to the cold condition. Called on (re)connect
    ''' (DeribitWsFeed.SeedAsync) so no pre-gap cascade / approach bleeds across a gap.
    ''' _firstSeenWritten stays TRUE across a reconnect within the same process — the
    ''' sidecar sentinel is a per-PROCESS-lifetime guarantee, not per-connection.</summary>
    Public Sub Reset()
        _liq.Clear()
        _cascadeActive = False
        _approachAbove = New ApproachState()
        _approachBelow = New ApproachState()
        _lastPrice = 0.0
        _pending.Clear()
        _swingHigh5m = 0.0 : _swingLow5m = 0.0
        _hvnAbove = 0.0 : _hvnBelow = 0.0
    End Sub

    ''' <summary>Refresh the carried candidate levels (the same carry #6 reads). A re-map
    ''' resets that side's approach episode (no cross-level bleed — the #6 discipline).</summary>
    Public Sub SetLevels(swingHigh5m As Double, swingLow5m As Double,
                         hvnAbove As Double, hvnBelow As Double)
        _swingHigh5m = swingHigh5m
        _swingLow5m = swingLow5m
        _hvnAbove = hvnAbove
        _hvnBelow = hvnBelow
        ' Close approach episodes if their level no longer matches an available candidate.
        If _approachAbove.Active AndAlso Not IsCandidateAbove(_approachAbove.LevelPrice) Then
            _approachAbove = New ApproachState()
        End If
        If _approachBelow.Active AndAlso Not IsCandidateBelow(_approachBelow.LevelPrice) Then
            _approachBelow = New ApproachState()
        End If
    End Sub

    ''' <summary>Fold one streamed trade. Updates the liq window (if liq-flagged), the
    ''' cascade edge, the level-approach episodes, and the first-liq-seen sidecar. Never
    ''' throws (advisory instrument — the run path is the caller).</summary>
    Public Sub FoldTrade(price As Double, amountUsd As Double, isBuy As Boolean,
                         isLiq As Boolean, tsMs As Long, cfg As AlertsSettings, instanceId As String)
        If cfg Is Nothing OrElse Not cfg.Enabled Then Return
        If price > 0 Then _lastPrice = price

        Dim windowMs As Long = CLng(Math.Max(1.0, cfg.CascadeWindowSec) * 1000.0)

        ' Age-prune the liq window on every fold + on any Snapshot read.
        PruneLiq(tsMs, windowMs)

        If isLiq Then
            _liq.Enqueue((tsMs, isBuy, amountUsd))
            If _liq.Count > LiqQueueCap Then _liq.Dequeue()

            ' FIRST_SEEN — write to the sidecar the very first time we see a liq-flagged
            ' trade in this process, AND only if the sidecar doesn't already carry one
            ' from a prior process run (the file's existence is the durable per-process
            ' unlock evidence — proposal §4 H4 amended).
            If Not _firstSeenWritten Then
                _firstSeenWritten = True
                If Not SidecarFileExists() Then
                    Dim ev As New AlertEvent With {
                        .Kind = "FIRST_SEEN",
                        .Side = If(isBuy, "BUY", "SELL"),
                        .UsdAmount = amountUsd,
                        .UtcMs = tsMs,
                        .InstanceId = instanceId
                    }
                    _pending.Add(ev)
                    AlertsSidecar.TryAppend(ev)
                End If
            End If
        End If

        ' Cascade edge: rising through the threshold this fold ⇒ emit CASCADE event
        ' (once per active window; re-arms when the window count falls below threshold).
        Dim countInWindow As Integer = _liq.Count
        Dim minTrades As Integer = Math.Max(1, cfg.CascadeMinTrades)
        If countInWindow >= minTrades Then
            If Not _cascadeActive Then
                _cascadeActive = True
                Dim ev As AlertEvent = BuildCascadeEvent(tsMs, instanceId)
                _pending.Add(ev)
                AlertsSidecar.TryAppend(ev)
            End If
        Else
            ' Below threshold ⇒ re-arm. This does NOT clear the queue — a fresh fold will
            ' either push it back above threshold (still one cascade, since we re-armed
            ' by dropping under) or leave it dormant.
            _cascadeActive = False
        End If

        ' Level-approach — checked on every trade (price ticks arrive here, not on book
        ' folds, which is fine: the strip's price is the last trade price too).
        UpdateApproachEpisodes(cfg)
    End Sub

    ''' <summary>Return the current alert state (snapshot). ALSO returns the pending
    ''' event list — the caller (LiveStrip tick) is expected to consume it; on return
    ''' the internal list is cleared. Never throws.</summary>
    Public Function Snapshot(nowMs As Long, cfg As AlertsSettings) As AlertsSnapshot
        Dim snap As New AlertsSnapshot()
        If cfg Is Nothing OrElse Not cfg.Enabled Then
            snap.CascadeSignal = "NONE"
            Return snap
        End If

        Dim windowMs As Long = CLng(Math.Max(1.0, cfg.CascadeWindowSec) * 1000.0)
        PruneLiq(nowMs, windowMs)

        snap.LiqEverSeenThisProcess = _firstSeenWritten OrElse SidecarFileExists()

        ' Cascade dominance across the window — count > threshold names the direction.
        Dim countInWindow As Integer = _liq.Count
        snap.CascadeCount = countInWindow
        Dim minTrades As Integer = Math.Max(1, cfg.CascadeMinTrades)
        If countInWindow >= minTrades Then
            Dim buys As Integer = 0, sells As Integer = 0
            Dim usdBuys As Double = 0, usdSells As Double = 0
            For Each e In _liq
                If e.IsBuy Then
                    buys += 1 : usdBuys += e.Usd
                Else
                    sells += 1 : usdSells += e.Usd
                End If
            Next
            snap.CascadeBuyCount = buys
            snap.CascadeSellCount = sells
            snap.CascadeUsdTotal = usdBuys + usdSells
            ' Buy-liquidations = SHORTS getting stopped out ("cascade above" — shorts squeezed,
            ' price impulses UP). Sell-liquidations = LONGS getting stopped out ("cascade below").
            If usdBuys >= usdSells Then
                snap.CascadeSignal = "CASCADE_ABOVE"
                snap.CascadeUsdDominant = usdBuys
            Else
                snap.CascadeSignal = "CASCADE_BELOW"
                snap.CascadeUsdDominant = usdSells
            End If
        Else
            snap.CascadeSignal = "NONE"
        End If

        snap.ApproachAboveActive = _approachAbove.Active
        snap.ApproachAboveLevel = _approachAbove.LevelPrice
        snap.ApproachBelowActive = _approachBelow.Active
        snap.ApproachBelowLevel = _approachBelow.LevelPrice
        snap.LastPrice = _lastPrice

        ' Drain pending events.
        If _pending.Count > 0 Then
            snap.PendingEvents.AddRange(_pending)
            _pending.Clear()
        End If

        Return snap
    End Function

    ' -- private helpers --------------------------------------------------------

    Private Sub PruneLiq(nowMs As Long, windowMs As Long)
        While _liq.Count > 0 AndAlso _liq.Peek().TsMs < (nowMs - windowMs)
            _liq.Dequeue()
        End While
    End Sub

    Private Function BuildCascadeEvent(tsMs As Long, instanceId As String) As AlertEvent
        Dim buyUsd As Double = 0, sellUsd As Double = 0
        For Each e In _liq
            If e.IsBuy Then buyUsd += e.Usd Else sellUsd += e.Usd
        Next
        Dim side As String = If(buyUsd >= sellUsd, "BUY", "SELL")
        Dim dominant As Double = If(buyUsd >= sellUsd, buyUsd, sellUsd)
        Return New AlertEvent With {
            .Kind = "CASCADE",
            .Side = side,
            .UsdAmount = dominant,
            .UtcMs = tsMs,
            .InstanceId = instanceId
        }
    End Function

    Private Sub UpdateApproachEpisodes(cfg As AlertsSettings)
        If _lastPrice <= 0 Then Return
        Dim tolerance As Double = Math.Max(1, cfg.LevelTicks) * SignalEmitter.TickSize

        ' ABOVE — nearest carried candidate >= price.
        Dim above As Double = NearestAbove(_lastPrice)
        HandleApproachSide(_approachAbove, above, _lastPrice, tolerance)

        ' BELOW — nearest carried candidate <= price.
        Dim below As Double = NearestBelow(_lastPrice)
        HandleApproachSide(_approachBelow, below, _lastPrice, tolerance)
    End Sub

    Private Sub HandleApproachSide(side As ApproachState, level As Double,
                                    price As Double, tolerance As Double)
        If level <= 0 Then
            ' No candidate ⇒ close any live episode.
            side.Active = False
            side.LevelPrice = 0.0
            Return
        End If
        Dim dist As Double = Math.Abs(level - price)
        If side.Active Then
            ' Re-arm on leave (the #6 pattern): once outside the band, drop episode.
            If Math.Abs(side.LevelPrice - level) > 0.0001 Then
                ' The nearest level re-mapped mid-episode: close (no cross-level bleed).
                side.Active = False
                side.LevelPrice = 0.0
            ElseIf dist > tolerance Then
                side.Active = False
                side.LevelPrice = 0.0
            End If
        Else
            If dist <= tolerance Then
                side.Active = True
                side.LevelPrice = level
            End If
        End If
    End Sub

    Private Function NearestAbove(price As Double) As Double
        Dim best As Double = 0.0
        Dim cands() As Double = {_swingHigh5m, _swingLow5m, _hvnAbove, _hvnBelow}
        For Each c In cands
            If c > price Then
                If best = 0.0 OrElse c < best Then best = c
            End If
        Next
        Return best
    End Function

    Private Function NearestBelow(price As Double) As Double
        Dim best As Double = 0.0
        Dim cands() As Double = {_swingHigh5m, _swingLow5m, _hvnAbove, _hvnBelow}
        For Each c In cands
            If c > 0 AndAlso c < price Then
                If c > best Then best = c
            End If
        Next
        Return best
    End Function

    Private Function IsCandidateAbove(level As Double) As Boolean
        Dim cands() As Double = {_swingHigh5m, _swingLow5m, _hvnAbove, _hvnBelow}
        For Each c In cands
            If Math.Abs(c - level) < 0.0001 AndAlso c > 0 Then Return True
        Next
        Return False
    End Function

    Private Function IsCandidateBelow(level As Double) As Boolean
        Return IsCandidateAbove(level) ' same test — level identity, not direction, is what re-mapping checks
    End Function

    Private Shared Function SidecarFileExists() As Boolean
        Try
            Return File.Exists(AlertsSidecar.GetPath())
        Catch
            Return False
        End Try
    End Function

End Class

''' <summary>One pending event a fold has produced — the strip drains these and plays the
''' audible cue (if sound_enabled) + flashes the strip. The tracker appends the event to
''' the sidecar itself; this list is the transient in-memory notification.</summary>
Public Class AlertEvent
    Public Property Kind As String = ""            ' "FIRST_SEEN" | "CASCADE"
    Public Property Side As String = ""            ' "BUY" | "SELL"
    Public Property UsdAmount As Double
    Public Property UtcMs As Long
    Public Property InstanceId As String = ""
End Class

''' <summary>A snapshot of the current alert state. All fields safe-default (never a
''' fake reading); NONE + inactive when the tracker is disabled or the window is empty.</summary>
Public Class AlertsSnapshot
    Public Property CascadeSignal As String = "NONE"   ' CASCADE_ABOVE / CASCADE_BELOW / NONE
    Public Property CascadeCount As Integer            ' liq-flagged trades in the current window
    Public Property CascadeBuyCount As Integer
    Public Property CascadeSellCount As Integer
    Public Property CascadeUsdTotal As Double
    Public Property CascadeUsdDominant As Double

    Public Property ApproachAboveActive As Boolean
    Public Property ApproachAboveLevel As Double
    Public Property ApproachBelowActive As Boolean
    Public Property ApproachBelowLevel As Double
    Public Property LastPrice As Double

    ''' <summary>H4 amended — reads the sidecar file's existence, so the flag survives
    ''' restarts (a per-process-lifetime rebuild is not enough to unlock A4).</summary>
    Public Property LiqEverSeenThisProcess As Boolean

    ''' <summary>Events produced since the last Snapshot() read; drained on return.
    ''' The tracker has already appended them to the sidecar — this list drives the
    ''' host-side audible cue + strip flash + one-off UI notifications.</summary>
    Public Property PendingEvents As New List(Of AlertEvent)()
End Class

''' <summary>Append-only sidecar `liq_events.log` beside the CSV. Never rotated, never
''' throws into the fold path (the SignalEmitter.TryWrite discipline — the ONE hard
''' rule the coordinator called out). One line per event:
'''   utc | kind(FIRST_SEEN|CASCADE) | side | usd | instance_id
''' Its existence IS the durable A4 gate evidence (proposal §4 H4 amended).</summary>
Public NotInheritable Class AlertsSidecar

    Private Const FileName As String = "liq_events.log"

    Private Sub New()
    End Sub

    Public Shared Function GetPath() As String
        Return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    ''' <summary>Append one event. Never throws; failures are console-logged (the
    ''' SignalEmitter.TryWrite pattern the coordinator brief mandated).</summary>
    Public Shared Function TryAppend(ev As AlertEvent) As Boolean
        Try
            If ev Is Nothing Then Return False
            Dim path As String = GetPath()
            Dim dir As String = System.IO.Path.GetDirectoryName(path)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            Dim utc As String = DateTimeOffset.FromUnixTimeMilliseconds(ev.UtcMs).UtcDateTime.ToString(
                                    "yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture)
            Dim line As String = String.Format(System.Globalization.CultureInfo.InvariantCulture,
                                               "{0} | {1} | {2} | {3:F0} | {4}" & vbLf,
                                               utc, ev.Kind, ev.Side, ev.UsdAmount, ev.InstanceId)
            File.AppendAllText(path, line)
            Return True
        Catch ex As Exception
            Console.WriteLine("[AlertsSidecar] append failed: " & ex.Message)
            Return False
        End Try
    End Function

End Class
