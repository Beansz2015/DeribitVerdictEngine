' UI/MainForm_LiveStrip.vb
' LIVE Microstructure Strip (P4 #3, docs/live-microstructure-strip-proposal.md) — thin WinForms host.
'
' A System.Windows.Forms.Timer (UI-thread) ticks every live_strip.refresh_sec. Started ONCE at form load
' (the constructor) and disposed on form close — independent of the auto-run timer, the exit-guard timer,
' and the on-close watcher. Each tick self-gates (§4.1): it renders the live readout only when live_strip
' is enabled AND the WS feed is healthy + fresh; otherwise it shows a minimal token ("WS only" /
' "off"). It then calls the host-agnostic LiveMicrostructureEvaluator against the live MarketState and
' renders the full-width TAPE strip (built in MainForm_Layout.BuildCardGridLayout, directly under the
' verdict-header hero row). DISPLAY/AWARENESS ONLY — deliberately NOT a verdict: no scoring, no CSV,
' no direction call.
'
' Lifecycle note (UI thread): the timer runs on the UI thread, so MarketState reads (lock-guarded), the
' feed's plain health fields, and the label update are all safe without Control.Invoke (pure display,
' light windowed compute, no async trigger — simpler than the on-close Threading.Timer).
'
' Toggle: the strip is always present as a thin line. When disabled it shows "TAPE · off · right-click
' to enable" carrying a context-menu toggle for live_strip.enabled, so the trader can turn it on (e.g.
' for the §9 #1 post-build visual checkpoint) without editing settings.json. The strip's live DATA is
' suppressed when off (it never recomputes / never reads MarketState while disabled); only the dim
' off-token + toggle remain. This consciously resolves the §7 ("disabled → hidden") vs §10 ("a toggle")
' tension — flagged for the visual checkpoint.

Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class MainForm

    ' Thin full-width TAPE strip — created/parented in MainForm_Layout.BuildCardGridLayout (its own grid
    ' row directly under the verdict-header hero row). NOT an RTF/snapshot/card surface (spec §6).
    Friend lblLiveStrip As Label

    Private _liveStripTimer    As System.Windows.Forms.Timer
    Private _liveStripMenu     As System.Windows.Forms.ContextMenuStrip
    Private _liveStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Private _liveStripLastText As String = Nothing

    ' -----------------------------------------------------------------------
    ' Lifecycle — StartLiveStrip from the constructor (form load); StopLiveStrip on form close.
    ' Idempotent: a re-start disposes the prior timer first. The timer keeps running even when
    ' live_strip is disabled, so a hot-reload/toggle re-enables instantly (mirrors the exit guard).
    ' -----------------------------------------------------------------------
    Private Sub StartLiveStrip()
        StopLiveStrip()

        EnsureLiveStripMenu()

        Dim cfg As EngineSettings = SettingsLoader.Current
        _liveStripTimer = New System.Windows.Forms.Timer() With {
            .Interval = LiveStripIntervalMs(cfg)
        }
        AddHandler _liveStripTimer.Tick, AddressOf OnLiveStripTick
        _liveStripTimer.Start()

        ' Paint once immediately so the strip isn't blank for the first refresh_sec.
        OnLiveStripTick(Nothing, EventArgs.Empty)
    End Sub

    Private Sub StopLiveStrip()
        If _liveStripTimer IsNot Nothing Then
            _liveStripTimer.Stop()
            RemoveHandler _liveStripTimer.Tick, AddressOf OnLiveStripTick
            _liveStripTimer.Dispose()
            _liveStripTimer = Nothing
        End If
        _liveStripLastText = Nothing
    End Sub

    Private Shared Function LiveStripIntervalMs(cfg As EngineSettings) As Integer
        ' Floor at 1s so a mis-set 0 can't busy-spin.
        Return Math.Max(1, cfg.LiveStrip.RefreshSec) * 1000
    End Function

    ' -----------------------------------------------------------------------
    ' Tick — recompute + repaint. Self-gates on enabled + feed health (§4.1).
    ' -----------------------------------------------------------------------
    Private Sub OnLiveStripTick(sender As Object, e As EventArgs)
        If IsDisposed OrElse lblLiveStrip Is Nothing Then Return
        Dim cfg As EngineSettings = SettingsLoader.Current

        ' refresh_sec hot-reload.
        Dim desiredMs As Integer = LiveStripIntervalMs(cfg)
        If _liveStripTimer IsNot Nothing AndAlso _liveStripTimer.Interval <> desiredMs Then
            _liveStripTimer.Interval = desiredMs
        End If

        ' Disabled → off-token (carries the right-click toggle). No recompute, no MarketState read.
        If Not cfg.LiveStrip.Enabled Then
            SetLiveStrip("off · right-click to enable", Theme.FG_QUATERNARY)
            Return
        End If

        ' WS-mode feature by nature (reads MarketState). No usable feed (transport=rest / stale / down) →
        ' "WS only"; never render stale numbers as live (§7).
        If Not LiveStripFeedReady(cfg) Then
            SetLiveStrip("WS only", Theme.FG_QUATERNARY)
            Return
        End If

        Dim snap As MicrostructureSnapshot =
            LiveMicrostructureEvaluator.Evaluate(_marketState, _lastSuccessfulIndicators, cfg)
        SetLiveStrip(ComposeLiveStrip(snap), Theme.FG_TERTIARY)
    End Sub

    ' Returns True only when the feed is connected, not cooling down, and book + trades are fresh.
    Private Function LiveStripFeedReady(cfg As EngineSettings) As Boolean
        If _wsFeed Is Nothing OrElse _marketState Is Nothing Then Return False
        If Not _wsFeed.IsConnected Then Return False
        If _wsFeed.IsCoolingDown Then Return False
        Dim staleAfter As Integer = cfg.Network.WsStaleAfterSec
        Dim nowUtc As DateTime = DateTime.UtcNow
        If (nowUtc - _marketState.TradesLastUpdate).TotalSeconds > staleAfter Then Return False
        If (nowUtc - _marketState.BookLastUpdate).TotalSeconds > staleAfter Then Return False
        Return True
    End Function

    ' -----------------------------------------------------------------------
    ' Render — compose the '·'-separated line; set text only when changed (no flicker, §4.4).
    ' -----------------------------------------------------------------------
    Private Sub SetLiveStrip(body As String, colour As Color)
        If lblLiveStrip Is Nothing Then Return
        Dim text As String = "TAPE · " & body
        If text <> _liveStripLastText Then
            lblLiveStrip.Text = text
            _liveStripLastText = text
        End If
        If Not lblLiveStrip.ForeColor.Equals(colour) Then lblLiveStrip.ForeColor = colour
        If Not lblLiveStrip.Visible Then lblLiveStrip.Visible = True
    End Sub

    ' Visually distinct from the verdict (label "TAPE", neutral/dim — never the verdict colour ramp), so
    ' it reads as a readout, not a call (§4.4). "--" for any field with no data yet.
    Private Shared Function ComposeLiveStrip(s As MicrostructureSnapshot) As String
        Dim parts As New List(Of String)()
        parts.Add(If(s.HasPrice, s.LastPrice.ToString("0"), "--"))
        parts.Add(ComposeLevels(s))
        parts.Add(If(s.HasTfi, "TFI " & ShortTfi(s.TfiSignal) & " " & SignedNum(s.TfiValue, 2), "TFI --"))
        parts.Add(If(s.HasSpread, s.SpreadBps.ToString("0.0") & " bps", "-- bps"))
        parts.Add(If(s.HasImbalance, "book " & ComposeImbalance(s), "book --"))
        parts.Add(ComposeTape(s))
        Return String.Join(" · ", parts)
    End Function

    ' Bracket the price between its floor and ceiling: "SL 62425 (−25) | SH 62468 (+18)".
    Private Shared Function ComposeLevels(s As MicrostructureSnapshot) As String
        Dim below As String = If(s.Below.Has, FormatLevel(s.Below), "")
        Dim above As String = If(s.Above.Has, FormatLevel(s.Above), "")
        If below = "" AndAlso above = "" Then Return "--"
        If below <> "" AndAlso above <> "" Then Return below & " | " & above
        Return If(below <> "", below, above)
    End Function

    Private Shared Function FormatLevel(lvl As MicrostructureLevel) As String
        Return lvl.Label & " " & lvl.Price.ToString("0") & " (" & SignedNum(lvl.Delta, 0) & ")"
    End Function

    Private Shared Function ComposeImbalance(s As MicrostructureSnapshot) As String
        If s.ImbalanceSide = "bid" Then
            Return s.ImbalanceRatio.ToString("0.0") & "× bid"
        ElseIf s.ImbalanceSide = "ask" Then
            Dim inv As Double = If(s.ImbalanceRatio > 0, 1.0 / s.ImbalanceRatio, 0)
            Return inv.ToString("0.0") & "× ask"
        Else
            Return "even"
        End If
    End Function

    Private Shared Function ComposeTape(s As MicrostructureSnapshot) As String
        Return s.TradesPerSec.ToString("0.#") & " tr/s (" & FormatUsdPerSec(s.UsdPerSec) & ")"
    End Function

    Private Shared Function FormatUsdPerSec(v As Double) As String
        Dim a As Double = Math.Abs(v)
        If a >= 1000000.0 Then Return "$" & (v / 1000000.0).ToString("0.0") & "M/s"
        If a >= 1000.0 Then Return "$" & (v / 1000.0).ToString("0.0") & "k/s"
        Return "$" & v.ToString("0") & "/s"
    End Function

    Private Shared Function SignedNum(v As Double, decimals As Integer) As String
        Dim a As String = Math.Abs(v).ToString("F" & decimals)
        Return If(v < 0, "−", "+") & a
    End Function

    Private Shared Function ShortTfi(sig As String) As String
        Select Case sig
            Case "BUY PRESSURE" : Return "BUY"
            Case "SELL PRESSURE" : Return "SELL"
            Case Else : Return "NEUT"
        End Select
    End Function

    ' -----------------------------------------------------------------------
    ' Toggle — right-click context menu on the strip for live_strip.enabled.
    ' -----------------------------------------------------------------------
    Private Sub EnsureLiveStripMenu()
        If _liveStripMenu IsNot Nothing Then Return
        _liveStripMenu = New System.Windows.Forms.ContextMenuStrip()
        _liveStripMenuItem = New System.Windows.Forms.ToolStripMenuItem("Live tape strip")
        AddHandler _liveStripMenuItem.Click, AddressOf OnLiveStripToggle
        _liveStripMenu.Items.Add(_liveStripMenuItem)
        ' Sync the check mark with the live setting each time the menu opens (covers hot-reload).
        AddHandler _liveStripMenu.Opening,
            Sub(s As Object, ev As System.ComponentModel.CancelEventArgs)
                _liveStripMenuItem.Checked = SettingsLoader.Current.LiveStrip.Enabled
            End Sub
        If lblLiveStrip IsNot Nothing Then lblLiveStrip.ContextMenuStrip = _liveStripMenu
    End Sub

    Private Sub OnLiveStripToggle(sender As Object, e As EventArgs)
        Dim cfg As EngineSettings = SettingsLoader.Current
        cfg.LiveStrip.Enabled = Not cfg.LiveStrip.Enabled
        ' Operational/UI toggle — bumpVersion:=False (no version/change_log churn; v36 §10a precedent).
        SettingsLoader.Save(cfg, "live_strip enabled toggled via UI", bumpVersion:=False)
        OnLiveStripTick(Nothing, EventArgs.Empty)   ' repaint immediately
    End Sub

End Class
