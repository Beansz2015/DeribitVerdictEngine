' LiveMicrostructureEvaluator.vb
' LIVE Microstructure Strip (P4 #3, docs/live-microstructure-strip-proposal.md) — host-agnostic evaluator.
'
' Reconstructs the fast streaming microstructure from a LIVE MarketState snapshot far more often than
' the full-analysis cadence (~2s), so a thin host layer can surface a one-line TAPE strip between
' deliberate runs. DISPLAY/AWARENESS ONLY — deliberately NOT a verdict: it never calls
' ScoringEngine.Calculate, never writes the CSV, never emits a directional call, never re-baselines.
' The §11 intent is explicit: the verdict stays a deliberate full-pipeline product; this only shows the
' raw microstructure *inputs* (price vs nearest structural levels, TFI, spread, book imbalance, tape
' speed) at tick freshness, visually distinct from the verdict so it informs without tempting an entry.
'
' It reuses the engine's pure indicator functions (CalcTFI / CalcSpread / CalcOFI) and the carried
' levels from the last full run — identical methodology to the full run, only fresher. Levels are
' CARRIED, not recomputed (5m swing / VPFR HVN are slow; they refresh each full run); price, TFI,
' spread, imbalance, and tape speed are recomputed live.
'
' Host-agnostic: no System.Windows.Forms, no Control.Invoke, no MainForm. Reused by the Linux port.

''' <summary>One bracketing structural level (nearest above or nearest below the live price).</summary>
Public NotInheritable Class MicrostructureLevel
    Public Property Has   As Boolean
    ''' <summary>SH / SL / HVN↑ / HVN↓ — which carried field this level came from.</summary>
    Public Property Label As String = ""
    Public Property Price As Double
    ''' <summary>Signed distance Price − LastPrice (negative below, positive above).</summary>
    Public Property Delta As Double
End Class

''' <summary>One microstructure snapshot — the §4.2 fields. Display/awareness payload only; no scoring
''' impact. Degenerate/empty inputs map to safe blanks (Has* = False), never an exception or a fake
''' reading. The host composes the rendered '·'-separated line from these fields.</summary>
Public NotInheritable Class MicrostructureSnapshot
    Public Property HasPrice       As Boolean
    Public Property LastPrice      As Double

    ''' <summary>Nearest carried level BELOW the live price (max of the <price candidates).</summary>
    Public Property Below          As New MicrostructureLevel()
    ''' <summary>Nearest carried level ABOVE the live price (min of the >price candidates).</summary>
    Public Property Above          As New MicrostructureLevel()

    Public Property HasTfi         As Boolean
    Public Property TfiSignal      As String = "NEUTRAL"   ' BUY PRESSURE / SELL PRESSURE / NEUTRAL
    Public Property TfiValue       As Double               ' normalised [-1, +1]

    Public Property HasSpread      As Boolean
    Public Property SpreadBps      As Double

    Public Property HasImbalance   As Boolean
    ''' <summary>CalcOFI ratio (weighted bid / weighted ask). >1 bid-heavy, <1 ask-heavy.</summary>
    Public Property ImbalanceRatio As Double
    Public Property ImbalanceSide  As String = ""          ' "bid" / "ask" / "balanced"

    ''' <summary>Tape speed always renders (0 on a lull) — never blanked.</summary>
    Public Property TradesPerSec   As Double
    Public Property UsdPerSec      As Double

    ''' <summary>[P4 #5] Aggressor-velocity burst enrichment of the tape-speed field
    ''' (proposal §7 — strip-only surface, the #3 precedent). HasBurst is True only when
    ''' the feature is enabled AND the feed-side accumulator is warmed up; the fields stay
    ''' blank/NORMAL otherwise (never a fake reading).</summary>
    Public Property HasBurst       As Boolean
    Public Property BurstRatio     As Double
    Public Property BurstSignal    As String = "NORMAL"    ' BURST_BUY / BURST_SELL / NORMAL

    ''' <summary>[P4 #6] Level-absorption tag (book-absorption proposal §7 D6 — strip-only
    ''' surface, the #3/#5 precedent). HasAbsorption is True ONLY while an ABSORB state is
    ''' active (the tag renders only then); blank on NONE/IDLE — never a fake reading.</summary>
    Public Property HasAbsorption      As Boolean
    Public Property AbsorptionSignal   As String = "NONE"  ' ABSORB_ABOVE / ABSORB_BELOW / NONE
    Public Property AbsorptionLevel    As Double
    Public Property AbsorptionRatio    As Double

    ''' <summary>[#7 + #8 v59] Alerts state — cascade fire (H2) + level-approach episode
    ''' (H3) + first-liq-seen persistence sentinel (H4 amended, sidecar existence).
    ''' Strip-only surface (H1). NONE / inactive when disabled or no event fired —
    ''' never a fake reading (the tracker discipline).</summary>
    Public Property HasAlerts          As Boolean
    Public Property CascadeSignal      As String = "NONE"   ' CASCADE_ABOVE / CASCADE_BELOW / NONE
    Public Property CascadeCount       As Integer
    Public Property CascadeUsdDominant As Double
    Public Property ApproachAboveActive As Boolean
    Public Property ApproachAboveLevel As Double
    Public Property ApproachBelowActive As Boolean
    Public Property ApproachBelowLevel As Double
    Public Property LiqEverSeen        As Boolean
    Public Property PendingEvents      As New List(Of AlertEvent)()
End Class

Public NotInheritable Class LiveMicrostructureEvaluator

    ''' <summary>
    ''' Recompute the fast streaming microstructure from the live MarketState + carry the last run's
    ''' structural levels. Reuses CalcTFI / CalcSpread / CalcOFI (same cfg params the full run uses) so
    ''' the strip's numbers are, by construction, the engine's numbers — only fresher. Never throws into
    ''' the caller; a degenerate/empty buffer maps to safe blanks (advisory overlay).
    '''
    ''' lastRun may be Nothing (first full run not done yet) — the bracketing levels stay blank and the
    ''' rest still render. nowUtcMs is the wall-clock epoch-ms for the tape-speed window; pass -1 (the
    ''' default) in production to read DateTimeOffset.UtcNow, or a fixed value for deterministic tests.
    ''' </summary>
    Public Shared Function Evaluate(state As MarketState,
                                    lastRun As IndicatorResults,
                                    cfg As EngineSettings,
                                    Optional nowUtcMs As Long = -1) As MicrostructureSnapshot
        Dim snap As New MicrostructureSnapshot()
        Try
            If state Is Nothing Then Return snap

            Dim trades As List(Of TradeRecord) = state.GetTrades()
            Dim book   As OrderBookSnapshot     = state.GetBook()

            ' Last price = streaming tail (the most recent trade; the list is ascending).
            If trades IsNot Nothing AndAlso trades.Count > 0 Then
                snap.LastPrice = trades(trades.Count - 1).Price
                snap.HasPrice  = snap.LastPrice > 0
            End If

            ' Nearest bracketing levels — carried from the last full run (slow 5m swing / VPFR HVN).
            If snap.HasPrice AndAlso lastRun IsNot Nothing Then
                FillNearestLevels(snap, lastRun, snap.LastPrice)
            End If

            ' TFI — the same pure fn (and cfg params) the full run + exit guard use.
            If trades IsNot Nothing AndAlso trades.Count > 0 Then
                IndicatorEngine.CalcTFI(trades, snap.TfiValue, snap.TfiSignal,
                    tfiWindowSize:=cfg.Indicators.TFI.WindowSize,
                    threshold:=cfg.Indicators.TFI.Threshold)
                snap.HasTfi = True
            End If

            If book IsNot Nothing Then
                ' Spread — the SpreadBps formula via CalcSpread (only the bps value is read; the bps is
                ' threshold-independent, so the TIGHT/NORMAL/WIDE status args are left at their defaults).
                Dim sBps As Double = 0, sStatus As String = "NORMAL"
                IndicatorEngine.CalcSpread(book, sBps, sStatus)
                snap.SpreadBps = sBps
                snap.HasSpread = HasTopOfBook(book)

                ' Top-book imbalance — the CalcOFI basis (cfg book depth + dominance ratios).
                Dim ratio As Double = 1, sig As String = "BALANCED"
                Dim bidVol As Double = 0, askVol As Double = 0
                IndicatorEngine.CalcOFI(book, ratio, sig, bidVol, askVol,
                    buyDominantRatio:=cfg.Indicators.OFI.BuyDominantRatio,
                    sellDominantRatio:=cfg.Indicators.OFI.SellDominantRatio,
                    bookDepth:=cfg.Indicators.OFI.BookDepth)
                snap.ImbalanceRatio = ratio
                If ratio > 1.0 Then
                    snap.ImbalanceSide = "bid"
                ElseIf ratio < 1.0 Then
                    snap.ImbalanceSide = "ask"
                Else
                    snap.ImbalanceSide = "balanced"
                End If
                snap.HasImbalance = (bidVol + askVol) > 0
            End If

            ' Tape speed — count + USD notional of trades in the last tape_window_sec (lull → ~0).
            FillTapeSpeed(snap, trades, cfg.LiveStrip.TapeWindowSec, nowUtcMs)

            ' [P4 #5] Aggressor-velocity burst — the same feed-side accumulator snapshot the
            ' full run reads, classified with the same pure fn + session-resolved thresholds
            ' (identical methodology, only fresher — the strip discipline). Warmup-gated.
            Dim av = cfg.Indicators.AggressorVelocity
            If av IsNot Nothing AndAlso av.Enabled Then
                Dim hourUtc As Integer = If(nowUtcMs >= 0,
                    DateTimeOffset.FromUnixTimeMilliseconds(nowUtcMs).UtcDateTime.Hour,
                    DateTime.UtcNow.Hour)
                Dim avNormWin As Double = ExecutionResolution.ResolveAggrVelNormWindow(cfg, hourUtc)
                Dim avSnap = state.GetAggressorVelocity(av.GrossFloorUsdPerSec, avNormWin)
                If avSnap.HasWarmup Then
                    snap.HasBurst    = True
                    snap.BurstRatio  = avSnap.BurstRatio
                    snap.BurstSignal = IndicatorEngine.ClassifyAggressorBurst(
                                           avSnap.BurstRatio, avSnap.Lean,
                                           ExecutionResolution.ResolveAggrVelBurstThreshold(cfg, hourUtc),
                                           av.DirectionLeanFloor)
                End If
            End If

            ' [P4 #6] Level-absorption tag — the same feed-side tracker snapshot the full
            ' run reads, classified with the same pure fn + session-resolved min_aggr_usd
            ' (identical methodology, only fresher — the strip discipline). The tag
            ' renders ONLY while an ABSORB state is active (§7 D6).
            Dim ab = cfg.Indicators.Absorption
            If ab IsNot Nothing AndAlso ab.Enabled Then
                Dim absNowMs As Long = If(nowUtcMs >= 0, nowUtcMs,
                                          DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                Dim absHour As Integer = DateTimeOffset.FromUnixTimeMilliseconds(absNowMs).UtcDateTime.Hour
                Dim absRead = IndicatorEngine.ClassifyAbsorption(
                                  state.GetAbsorption(absNowMs, ab),
                                  ExecutionResolution.ResolveAbsorptionMinAggrUsd(cfg, absHour),
                                  ab.AbsorbRatio, ab.MaxPullFrac)
                If absRead.Signal = "ABSORB_ABOVE" OrElse absRead.Signal = "ABSORB_BELOW" Then
                    snap.HasAbsorption    = True
                    snap.AbsorptionSignal = absRead.Signal
                    snap.AbsorptionLevel  = absRead.LevelPrice
                    snap.AbsorptionRatio  = absRead.AbsorbRatio
                End If
            End If

            ' [#7 + #8 v59] Alerts snapshot — the same feed-side tracker the fold path
            ' updates. The strip renders CASCADE + NEAR tags when active (H1); the
            ' host consumes pending events (audible cue + flash + sidecar was
            ' already appended inside the tracker). LiqEverSeen reads the sidecar
            ' file's existence (H4 amended), so it survives restarts.
            Dim alCfg = cfg.Alerts
            If alCfg IsNot Nothing AndAlso alCfg.Enabled Then
                Dim alNowMs As Long = If(nowUtcMs >= 0, nowUtcMs,
                                         DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                Dim al = state.GetAlerts(alNowMs, alCfg)
                snap.HasAlerts           = al.CascadeSignal <> "NONE" OrElse
                                            al.ApproachAboveActive OrElse al.ApproachBelowActive
                snap.CascadeSignal       = al.CascadeSignal
                snap.CascadeCount        = al.CascadeCount
                snap.CascadeUsdDominant  = al.CascadeUsdDominant
                snap.ApproachAboveActive = al.ApproachAboveActive
                snap.ApproachAboveLevel  = al.ApproachAboveLevel
                snap.ApproachBelowActive = al.ApproachBelowActive
                snap.ApproachBelowLevel  = al.ApproachBelowLevel
                snap.LiqEverSeen         = al.LiqEverSeenThisProcess
                If al.PendingEvents IsNot Nothing AndAlso al.PendingEvents.Count > 0 Then
                    snap.PendingEvents.AddRange(al.PendingEvents)
                End If
            End If
        Catch
            ' Advisory overlay — never surface an exception into the host tick. Degenerate state → blanks.
        End Try
        Return snap
    End Function

    ' Bracket the live price between the nearest carried level below and the nearest above. All four
    ' carried fields are treated as generic candidates (a swing low can sit above price after a drop, so
    ' "below = max of the <price candidates / above = min of the >price candidates" is the correct rule,
    ' not "swing low is always below"). 0 = no such level. A level exactly == price is neither.
    Private Shared Sub FillNearestLevels(snap As MicrostructureSnapshot,
                                         lastRun As IndicatorResults,
                                         price As Double)
        Dim cands As New List(Of (Price As Double, Label As String))()
        If lastRun.LastSwingHigh5m > 0 Then cands.Add((lastRun.LastSwingHigh5m, "SH"))
        If lastRun.LastSwingLow5m > 0 Then cands.Add((lastRun.LastSwingLow5m, "SL"))
        If lastRun.VPFRNearestHvnAbove > 0 Then cands.Add((lastRun.VPFRNearestHvnAbove, "HVN↑"))
        If lastRun.VPFRNearestHvnBelow > 0 Then cands.Add((lastRun.VPFRNearestHvnBelow, "HVN↓"))

        Dim bestBelow As (Price As Double, Label As String)? = Nothing
        Dim bestAbove As (Price As Double, Label As String)? = Nothing
        For Each c In cands
            If c.Price < price Then
                If Not bestBelow.HasValue OrElse c.Price > bestBelow.Value.Price Then bestBelow = c
            ElseIf c.Price > price Then
                If Not bestAbove.HasValue OrElse c.Price < bestAbove.Value.Price Then bestAbove = c
            End If
        Next

        If bestBelow.HasValue Then
            snap.Below.Has = True
            snap.Below.Label = bestBelow.Value.Label
            snap.Below.Price = bestBelow.Value.Price
            snap.Below.Delta = bestBelow.Value.Price - price
        End If
        If bestAbove.HasValue Then
            snap.Above.Has = True
            snap.Above.Label = bestAbove.Value.Label
            snap.Above.Price = bestAbove.Value.Price
            snap.Above.Delta = bestAbove.Value.Price - price
        End If
    End Sub

    ' Tape speed: scan the ascending trade buffer from the tail while Timestamp ≥ now − window, summing
    ' count and USD notional (Amount is the Deribit inverse-perp USD notional). now = wall-clock, so a
    ' lull correctly reads ~0 (no recent trades → empty window → 0). The ascending order lets us stop at
    ' the first trade older than the cutoff. Always populated (0 is a valid reading, not a blank).
    Private Shared Sub FillTapeSpeed(snap As MicrostructureSnapshot,
                                     trades As List(Of TradeRecord),
                                     windowSec As Integer,
                                     nowUtcMs As Long)
        Dim w As Integer = Math.Max(1, windowSec)
        snap.TradesPerSec = 0
        snap.UsdPerSec = 0
        If trades Is Nothing OrElse trades.Count = 0 Then Return

        Dim nowMs As Long = If(nowUtcMs >= 0, nowUtcMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        Dim cutoff As Long = nowMs - CLng(w) * 1000L

        Dim count As Integer = 0
        Dim usd As Double = 0
        For i As Integer = trades.Count - 1 To 0 Step -1
            If trades(i).Timestamp < cutoff Then Exit For   ' ascending → everything earlier is older too
            count += 1
            usd += trades(i).Amount
        Next

        snap.TradesPerSec = count / CDbl(w)
        snap.UsdPerSec = usd / CDbl(w)
    End Sub

    Private Shared Function HasTopOfBook(book As OrderBookSnapshot) As Boolean
        Return book IsNot Nothing AndAlso
               book.Bids IsNot Nothing AndAlso book.Bids.Count > 0 AndAlso
               book.Asks IsNot Nothing AndAlso book.Asks.Count > 0
    End Function

End Class
