' UI/MainForm_LiveStrip.vb
' LIVE Microstructure Strip (P4 #3, docs/live-microstructure-strip-proposal.md) — thin WinForms host.
'
' A System.Windows.Forms.Timer (UI-thread) ticks every live_strip.refresh_sec. Started ONCE at form load
' (the constructor) and disposed on form close — independent of the auto-run timer, the exit-guard timer,
' and the on-close watcher. Each tick self-gates (§4.1): it renders the live readout only when live_strip
' is enabled AND the WS feed is healthy + fresh; otherwise it shows "WS only" (no usable feed) or hides
' the data (disabled). It then calls the host-agnostic LiveMicrostructureEvaluator against the live
' MarketState and renders the full-width TAPE strip (built in MainForm_Layout.BuildCardGridLayout,
' directly under the verdict-header hero row). DISPLAY/AWARENESS ONLY — deliberately NOT a verdict:
' no scoring, no CSV, no direction call.
'
' Lifecycle note (UI thread): the timer runs on the UI thread, so MarketState reads (lock-guarded), the
' feed's plain health fields, and the label update are all safe without Control.Invoke (pure display,
' light windowed compute, no async trigger — simpler than the on-close Threading.Timer).
'
' Toggle: a visible "TAPE" CheckBox on the strip row (mirrors the SINGLE/REPEAT + INTERVAL/ON-CLOSE
' radio toggles) switches live_strip.enabled. The checkbox is always visible; the data label hides when
' the strip is off — so "disabled → hidden" holds for the readout (spec §7) while the toggle stays
' reachable (spec §10). The checkbox is kept in sync with the setting each tick (covers hot-reload).

Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class MainForm

    ' The "TAPE" toggle checkbox + the data label — created/parented in
    ' MainForm_Layout.BuildCardGridLayout (their own grid row directly under the verdict-header hero
    ' row). NOT an RTF/snapshot/card surface (spec §6).
    Friend chkLiveStrip As CheckBox
    Friend lblLiveStrip As Label

    Private _liveStripTimer    As System.Windows.Forms.Timer
    Private _liveStripLastText As String = Nothing
    Private _liveStripSyncing  As Boolean = False   ' guards the checkbox→setting handler during sync

    ' -----------------------------------------------------------------------
    ' Lifecycle — StartLiveStrip from the constructor (form load); StopLiveStrip on form close.
    ' Idempotent: a re-start disposes the prior timer first. The timer keeps running even when
    ' live_strip is disabled, so a toggle/hot-reload re-enables instantly (mirrors the exit guard).
    ' -----------------------------------------------------------------------
    Private Sub StartLiveStrip()
        StopLiveStrip()

        Dim cfg As EngineSettings = SettingsLoader.Current
        _liveStripTimer = New System.Windows.Forms.Timer() With {
            .Interval = LiveStripIntervalMs(cfg)
        }
        AddHandler _liveStripTimer.Tick, AddressOf OnLiveStripTick
        _liveStripTimer.Start()

        ' Paint once immediately so the strip + checkbox reflect the saved state from form load.
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

        ' Keep the checkbox in sync with the setting (covers a settings.json hot-reload). Guarded so the
        ' programmatic Checked change doesn't loop back through OnLiveStripCheckChanged + re-save.
        If chkLiveStrip IsNot Nothing AndAlso chkLiveStrip.Checked <> cfg.LiveStrip.Enabled Then
            _liveStripSyncing = True
            chkLiveStrip.Checked = cfg.LiveStrip.Enabled
            _liveStripSyncing = False
        End If

        ' refresh_sec hot-reload.
        Dim desiredMs As Integer = LiveStripIntervalMs(cfg)
        If _liveStripTimer IsNot Nothing AndAlso _liveStripTimer.Interval <> desiredMs Then
            _liveStripTimer.Interval = desiredMs
        End If

        ' Disabled → hide the data (the checkbox stays visible as the toggle). No recompute, no read.
        If Not cfg.LiveStrip.Enabled Then
            SetLiveStrip(Nothing, Theme.FG_QUATERNARY)
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
    ' Render — set text only when changed (no flicker, §4.4). body = Nothing hides the data label.
    ' -----------------------------------------------------------------------
    Private Sub SetLiveStrip(body As String, colour As Color)
        If lblLiveStrip Is Nothing Then Return
        If body Is Nothing Then
            lblLiveStrip.Visible = False
            _liveStripLastText = Nothing
            Return
        End If
        If body <> _liveStripLastText Then
            lblLiveStrip.Text = body
            _liveStripLastText = body
        End If
        If Not lblLiveStrip.ForeColor.Equals(colour) Then lblLiveStrip.ForeColor = colour
        If Not lblLiveStrip.Visible Then lblLiveStrip.Visible = True
    End Sub

    ' Visually distinct from the verdict (the "TAPE" checkbox labels it, neutral/dim — never the verdict
    ' colour ramp), so it reads as a readout, not a call (§4.4). "--" for any field with no data yet.
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

    ' Bracket the price between its floor and ceiling: "SL 59860 (+56) | SH 60103 (+299)".
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

    ' [P4 #5] Tape-speed field enriched with the directional burst ratio + BURST state
    ' (aggressor-velocity proposal §7). Strip-only surface — no card/snapshot obligation
    ' (the #3 precedent). Pre-warmup / disabled / REST → the plain v45 tape field.
    Private Shared Function ComposeTape(s As MicrostructureSnapshot) As String
        Dim tape As String = s.TradesPerSec.ToString("0.#") & " tr/s (" & FormatUsdPerSec(s.UsdPerSec) & ")"
        If Not s.HasBurst Then Return tape
        Dim burst As String = s.BurstRatio.ToString("0.0") & "×"
        If s.BurstSignal = "BURST_BUY" Then
            burst &= " BURST↑"
        ElseIf s.BurstSignal = "BURST_SELL" Then
            burst &= " BURST↓"
        End If
        Return tape & " " & burst
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
    ' Toggle — the visible "TAPE" checkbox writes live_strip.enabled.
    ' -----------------------------------------------------------------------
    Private Sub OnLiveStripCheckChanged(sender As Object, e As EventArgs)
        If _liveStripSyncing Then Return   ' programmatic sync from the tick — not a user action
        Dim cfg As EngineSettings = SettingsLoader.Current
        cfg.LiveStrip.Enabled = chkLiveStrip.Checked
        ' Operational/UI toggle — bumpVersion:=False (no version/change_log churn; v36 §10a precedent).
        SettingsLoader.Save(cfg, "live_strip enabled toggled via UI", bumpVersion:=False)
        OnLiveStripTick(Nothing, EventArgs.Empty)   ' repaint immediately
    End Sub

End Class
