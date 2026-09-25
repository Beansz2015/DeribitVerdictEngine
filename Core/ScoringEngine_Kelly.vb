' Core/ScoringEngine_Kelly.vb
' ScoringEngine partial: Kelly sizing helper (display-only).
' No scoring impact. Computes recommended risk fraction / contracts for the
' currently dominant verdict side using the placed stop distance and configured
' account/contract assumptions.
'
' [v69, docs/kelly-one-class-placed-payoff-spec.md] REWRITTEN. p and b no longer
' come from a confidence-tier map (QD-1 (c): one class, tiers not re-cut) — they
' come from the live eval cache, folded into a LivePerformanceTracker.KellyBook by
' the caller (KO-1 (a): the live cache; KO-2 (a): the current run's session only).
' K-1 (g): p and b are the TERCILE bucket's measured values for rows with a similar
' placed net payoff to this one, falling back to the session-pooled pair below
' kelly.min_book_rows (K-1 (g) point 4). See docs/kelly-one-class-placed-payoff-spec.md
' §3.1-§3.4 and §4 "RULINGS — 2026-09-25" for the full definition; this file
' implements it, it does not re-derive it.

Partial Public Class ScoringEngine

    ' -----------------------------------------------------------------------
    ' Kelly sizing (display-only)
    ' -----------------------------------------------------------------------
    ' Inputs:
    '   v      = VerdictResult to populate
    '   book   = the current run's session book (LivePerformanceTracker.ComputeKellyBook)
    '   lvLong = placed levels for the long side (SignalEmitter.ComputeSideLevels, isLong:=True)
    '   lvShort= placed levels for the short side (isLong:=False)
    '   cfg    = Engine settings (kelly + scoring.trade_costs blocks)
    '
    ' Outputs written into v: see the "Kelly one-class + placed-payoff-book fields"
    ' block in Core/ScoringEngine_Types.vb, plus the original KellyF/KellyFHalf/
    ' KellyFApplied/KellyPWin/KellyPMode/KellyCapped/KellyContracts/KellyRiskUsd/
    ' KellyLevCapped fields (unchanged shape, new source).
    '
    ' Notes:
    ' - Silent / zeroed (KellyHasSide = False) on a verdict with no Kelly side: plain
    '   NO TRADE and NO TRADE [TIE] (§3.3). Every other output stays at its reset value.
    ' - Deribit BTC-PERPETUAL is an INVERSE contract: the USD loss on a $face contract
    '   over a $Δ price move is face × Δ / price, not face × Δ (D1/H4 fix — the old form
    '   was off by the entry price, ~1e4×, so contracts were always 0). At correct sizing
    '   the $ risk cap is rarely binding, so a leverage cap (kelly.max_leverage) is applied
    '   and surfaced via KellyLevCapped.
    Public Shared Sub CalcKellySizing(v As VerdictResult,
                                      book As LivePerformanceTracker.KellyBook,
                                      lvLong As SideLevels,
                                      lvShort As SideLevels,
                                      cfg As EngineSettings)

        ' Reset all outputs first so suppression is deterministic.
        v.KellyF = 0.0
        v.KellyFHalf = 0.0
        v.KellyFApplied = 0.0
        v.KellyPWin = 0.0
        v.KellyPMode = ""
        v.KellyCapped = False
        v.KellyContracts = 0
        v.KellyRiskUsd = 0.0
        v.KellyLevCapped = False

        v.KellyHasSide = False
        v.KellyB = 0.0
        v.KellyBreakevenP = 0.0
        v.KellyBookN = 0
        v.KellyBookSession = ""
        v.KellyBookSufficient = False
        v.KellyBookSpanStartUtc = DateTime.MinValue
        v.KellyBucketIndex = 0
        v.KellyBucketLo = 0.0
        v.KellyBucketHi = 0.0
        v.KellyBucketFallback = False

        If v Is Nothing OrElse cfg Is Nothing OrElse book Is Nothing Then Exit Sub

        ' ---------------------------------------------------------------
        ' Step 1: which side (if any) Kelly bids for (spec §3.3).
        ' The verdict side, or the lean side on "NO TRADE [WEAK LONG]"/"[WEAK SHORT]".
        ' Plain NO TRADE and "NO TRADE [TIE]" have no side — Kelly is not computed.
        ' Deliberately NOT SignalEmitter.DeriveDirection: that helper returns NONE on
        ' every "NO TRADE*" verdict (the payload's actionability contract); Kelly's
        ' bias-only contract (v30) intentionally keeps the lean side.
        ' ---------------------------------------------------------------
        Dim hasSide As Boolean = False
        Dim isLong As Boolean = False
        Dim verdict As String = If(v.Verdict, "").Trim()
        If verdict = "" Then
            hasSide = False
        ElseIf verdict.StartsWith("NO TRADE", StringComparison.OrdinalIgnoreCase) Then
            If verdict.Contains("WEAK LONG") Then
                hasSide = True : isLong = True
            ElseIf verdict.Contains("WEAK SHORT") Then
                hasSide = True : isLong = False
            End If
            ' plain "NO TRADE" and "NO TRADE [TIE]" fall through with hasSide = False
        ElseIf verdict.Contains("LONG") Then
            hasSide = True : isLong = True
        ElseIf verdict.Contains("SHORT") Then
            hasSide = True : isLong = False
        End If

        v.KellyHasSide = hasSide
        If Not hasSide Then Exit Sub

        Dim kellySide As SideLevels = If(isLong, lvLong, lvShort)

        v.KellyBookSession = book.Session
        v.KellyBookSufficient = book.Sufficient
        v.KellyBookSpanStartUtc = book.SpanStartUtc

        ' ---------------------------------------------------------------
        ' Step 2: "book below the floor" state (§3.4) — the session pool itself
        ' does not meet kelly.min_book_rows. Nothing else is computed.
        ' ---------------------------------------------------------------
        If Not book.Sufficient Then
            v.KellyBookN = book.N
            Exit Sub
        End If

        ' ---------------------------------------------------------------
        ' Step 3: this row's own bucket key — its placed net payoff for the Kelly
        ' side (K-1 (g) point 1), same formula as the book rows (spec §3.2).
        ' ---------------------------------------------------------------
        Dim entry As Double = kellySide.Entry
        If entry <= 0 Then Exit Sub
        Dim feeUsd As Double = cfg.Scoring.TradeCosts.RoundTripFeePct * entry
        Dim t As Double = Math.Abs(kellySide.Target - entry)
        Dim s As Double = Math.Abs(kellySide.StopPx - entry)
        Dim netStop As Double = s + feeUsd
        If netStop <= 0 Then Exit Sub
        Dim bRow As Double = (t - feeUsd) / netStop

        Dim bucketIdx As Integer = LivePerformanceTracker.SelectKellyBucket(book, bRow)

        Dim usedP As Double
        Dim usedB As Double
        Dim usedN As Integer
        Dim fallback As Boolean = False

        If bucketIdx = 0 Then
            ' No buckets on the book (defensive — book.Sufficient already guarantees
            ' N >= min_book_rows >= 1, so ComputeKellyBook always built 3 buckets here;
            ' kept as a fallback so a degenerate book cannot throw).
            usedP = book.P
            usedB = book.NetPayoff
            usedN = book.N
            fallback = True
        Else
            Dim bkt = book.Buckets(bucketIdx - 1)
            v.KellyBucketIndex = bucketIdx
            v.KellyBucketLo = bkt.LoB
            v.KellyBucketHi = bkt.HiB
            If bkt.Sufficient Then
                usedP = bkt.P
                usedB = bkt.NetPayoff
                usedN = bkt.N
            Else
                ' K-1 (g) point 4: bucket below the floor -> fall back to the
                ' session-pooled pair (option (e)), and say so on screen.
                usedP = book.P
                usedB = book.NetPayoff
                usedN = book.N
                fallback = True
            End If
        End If

        v.KellyBucketFallback = fallback
        v.KellyBookN = usedN
        v.KellyPWin = usedP
        v.KellyB = usedB
        v.KellyPMode = "BOOK"
        ' Breakeven p at ratio b: f*=0 => b*p - (1-p) = 0 => p = 1/(1+b). Guarded — a
        ' pathological usedB <= -1 cannot occur with real data (NetStop is always > 0
        ' and NetTarget cannot be pooled below -Σstop), but a defensive render beats a
        ' divide-by-zero on a live box.
        v.KellyBreakevenP = If(1.0 + usedB > 0, 1.0 / (1.0 + usedB), 1.0)

        Dim q As Double = 1.0 - usedP
        Dim fStar As Double = If(usedB > 0, ((usedB * usedP) - q) / usedB, -1.0)
        v.KellyF = fStar

        ' [NO EDGE] state (KO-4 (b)) — p/b/breakeven/f* render; sizing rows do not.
        If fStar <= 0 Then Exit Sub

        ' Half-Kelly and hard cap
        Dim fHalf As Double = If(cfg.Kelly.UseHalfKelly, fStar / 2.0, fStar)
        Dim fApplied As Double = Math.Min(fHalf, cfg.Kelly.MaxRiskFraction)

        v.KellyFHalf = fHalf
        v.KellyFApplied = fApplied
        v.KellyCapped = (fHalf > cfg.Kelly.MaxRiskFraction)
        v.KellyRiskUsd = cfg.Kelly.AccountSizeUsd * fApplied

        ' ---------------------------------------------------------------
        ' Step 4: Contract sizing (inverse contract), off the PLACED stop distance
        ' for the Kelly side (spec §3.3) — not ATR × stop multiplier.
        ' Risk per contract = face × stopDistance / entryPrice  (USD).
        ' Final contracts = min(risk-derived, leverage-derived); whole only.
        ' ---------------------------------------------------------------
        If s <= 0 Then Exit Sub
        Dim riskPerContractUsd As Double = cfg.Kelly.ContractFaceUsd * s / entry
        If riskPerContractUsd <= 0 Then Exit Sub

        Dim contractsByRisk As Integer = CInt(Math.Floor(v.KellyRiskUsd / riskPerContractUsd))
        If contractsByRisk < 0 Then contractsByRisk = 0

        ' Leverage cap: max contracts before notional exceeds account × max_leverage.
        Dim maxContractsByLeverage As Integer = 0
        If cfg.Kelly.ContractFaceUsd > 0 Then
            maxContractsByLeverage = CInt(Math.Floor(
                cfg.Kelly.AccountSizeUsd * cfg.Kelly.MaxLeverage / cfg.Kelly.ContractFaceUsd))
        End If

        If maxContractsByLeverage > 0 AndAlso maxContractsByLeverage < contractsByRisk Then
            v.KellyContracts = maxContractsByLeverage
            v.KellyLevCapped = True
        Else
            v.KellyContracts = contractsByRisk
        End If
    End Sub

End Class
