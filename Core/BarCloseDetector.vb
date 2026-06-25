' Core/BarCloseDetector.vb
' On-close analysis mode (P4 #2, docs/on-close-analysis-mode-proposal.md) — host-agnostic bar-roll detector.
'
' Decides whether an execution-resolution bar just CLOSED on a live MarketState, so a thin host
' timer can fire a full analysis run the instant the confirming bar completes (the structural-
' breakout trader's decision moment) instead of at an arbitrary interval phase. DISPLAY/TRIGGER
' ONLY: it never computes indicators, never scores, never writes the CSV — it only answers
' "did the forming bar roll since I last looked?".
'
' Host-agnostic: no System.Windows.Forms, no Control.Invoke, no MainForm. Reused by the Linux port.
'
' Design (§4.2):
'   - The forming bar is MarketState.GetCandles(execRes).Last(); its OPEN-TIME is Candle.Timestamp
'     (epoch ms — Long, NOT DateTime; the proposal's DateTime signature is adapted to the real field
'     type). A roll has occurred when that open-time ADVANCES past the last-seen value: the prior bar
'     just closed.
'   - First observation (lastSeenOpen = Long.MinValue sentinel) adopts the current forming-bar
'     open-time WITHOUT firing — same as the interval timer, which fires after one interval, not
'     immediately on start.
'   - Catch-up, not burst: if several bars elapsed during a feed gap (reconnect), the open-time jumps
'     multiple intervals — fire ONCE and adopt the new open-time. Missed bars are not replayed.
'   - Stale/empty buffer (state Nothing, no series, or out-of-order tick) → no fire, last-seen kept.

Public NotInheritable Class BarCloseDetector

    ''' <summary>Sentinel for "no forming bar seen yet" — real Deribit open-times are large
    ''' positive epoch-ms values, so Long.MinValue can never collide with one.</summary>
    Public Const Unseen As Long = Long.MinValue

    ''' <summary>
    ''' Given the last-seen forming-bar open-time, report whether the execution-resolution bar
    ''' rolled (the prior bar closed) and the open-time to carry forward.
    ''' Returns (Fired:=False, FormingOpen:=lastSeenOpen) when there is nothing to read or the
    ''' open-time is unchanged. A multi-interval jump (gap) fires exactly once.
    ''' </summary>
    Public Shared Function DetectBarRoll(state As MarketState, execRes As Integer, lastSeenOpen As Long) As (Fired As Boolean, FormingOpen As Long)
        If state Is Nothing Then Return (False, lastSeenOpen)

        Dim series As List(Of Candle) = state.GetCandles(execRes.ToString())
        If series Is Nothing OrElse series.Count = 0 Then Return (False, lastSeenOpen)

        Dim formingOpen As Long = series(series.Count - 1).Timestamp

        ' First observation: adopt without firing (no run on start; first run waits for a real roll
        ' or the host's interval backstop).
        If lastSeenOpen = Unseen Then Return (False, formingOpen)

        ' Roll: the forming-bar open-time advanced → the prior bar closed. Any forward jump (1 bar
        ' or several after a reconnect gap) fires once and adopts the newest open-time.
        If formingOpen > lastSeenOpen Then Return (True, formingOpen)

        ' Unchanged or out-of-order (older) tick → no roll, keep the last-seen open-time.
        Return (False, lastSeenOpen)
    End Function

End Class
