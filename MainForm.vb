' MainForm.vb
' UI logic — wires button click to data fetch → indicator calc → scoring → display.

Imports System.Windows.Forms
Imports System.Drawing


Public Class MainForm

    ' ── OI snapshot history (stored in-memory for delta calculation) ──────────
    ' Each entry: (timestamp_ms, open_interest)
    Private _oiHistory As New List(Of (Ts As Long, OI As Double))

    ' ── Button click: full pipeline ───────────────────────────────────────────
    Private Async Sub btnAnalyze_Click(sender As Object, e As EventArgs) Handles btnAnalyze.Click
        btnAnalyze.Enabled = False
        btnAnalyze.Text = "Fetching..."
        txtOutput.Text = "Fetching data from Deribit..."
        lblVerdict.Text = "…"
        lblVerdict.BackColor = Color.Gray

        Try
            Await RunAnalysisAsync()
        Catch ex As Exception
            txtOutput.Text = $"ERROR: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            lblVerdict.Text = "ERROR"
            lblVerdict.BackColor = Color.OrangeRed
        Finally
            btnAnalyze.Enabled = True
            btnAnalyze.Text = "▶ Analyze Now"
        End Try
    End Sub

    Private Async Function RunAnalysisAsync() As Task
        ' ── 1. Fetch all data concurrently ────────────────────────────────────
        Dim t_1m = DeribitClient.GetCandlesAsync("1", 250)   ' 250 × 1m candles
        Dim t_5m = DeribitClient.GetCandlesAsync("5", 210)   ' 210 × 5m (need 200 for EMA200)
        Dim t_funding = DeribitClient.GetFundingRateAsync()
        Dim t_book = DeribitClient.GetBookSummaryAsync()
        Dim t_ob = DeribitClient.GetOrderBookAsync(10)
        Dim t_trades = DeribitClient.GetRecentTradesAsync(100)

        Await Task.WhenAll(t_1m, t_5m, t_funding, t_book, t_ob, t_trades)

        Dim candles1m = Await t_1m
        Dim candles5m = Await t_5m
        Dim fundingRate = Await t_funding
        Dim bookSummary = Await t_book
        Dim orderBook = Await t_ob
        Dim recentTrades = Await t_trades

        If candles1m.Count < 50 Then
            txtOutput.Text = "Insufficient 1m candle data returned. Please retry."
            Return
        End If

        ' ── 2. Calculate indicators ───────────────────────────────────────────
        Dim r As New IndicatorResults()
        r.CurrentPrice = candles1m.Last().Close

        ' --- ATR ---
        r.ATR = IndicatorEngine.CalcATR(candles1m, 7)
        ' 20-day avg ATR: approximate from all fetched 1m candles (250 candles ≈ 4h; 
        ' for true 20d avg we'd need ~28800 candles — use daily ATR proxy from 5m data)
        r.ATRAvg20d = IndicatorEngine.CalcATR(candles5m, 60) * Math.Sqrt(5) ' rough daily proxy
        r.ATRSizeMultiplier = If(r.ATR > 0, r.ATRAvg20d / r.ATR, 1.0)
        r.ATRSizeMultiplier = Math.Round(Math.Clamp(r.ATRSizeMultiplier, 0.25, 4.0), 2)

        ' --- ROC ---
        Dim rocSeries = IndicatorEngine.CalcROCSeries(candles1m, 9)
        r.ROC = If(rocSeries.Count > 0, rocSeries.Last(), 0)
        If rocSeries.Count >= 2 Then
            Dim delta As Double = rocSeries.Last() - rocSeries(rocSeries.Count - 2)
            r.ROCSlope = If(delta > 0.01, "RISING", If(delta < -0.01, "FALLING", "FLAT"))
        Else
            r.ROCSlope = "FLAT"
        End If

        ' --- RSI ---
        r.RSI = IndicatorEngine.CalcRSI(candles1m, 9)

        ' --- Volume SMA ---
        r.VolumeSMA9 = IndicatorEngine.CalcVolumeSMA(candles1m, 9)
        r.CurrentVolume = candles1m.Last().Volume
        r.VolumeRatio = If(r.VolumeSMA9 > 0, r.CurrentVolume / r.VolumeSMA9, 1)

        ' --- DMI + ADX (5m) ---
        IndicatorEngine.CalcDMI(candles5m, 9, r.PlusDI, r.MinusDI, r.ADX)
        If r.ADX > 25 AndAlso r.PlusDI > r.MinusDI Then
            r.Regime = "TRENDING_UP"
        ElseIf r.ADX > 25 AndAlso r.MinusDI > r.PlusDI Then
            r.Regime = "TRENDING_DOWN"
        ElseIf r.ADX < 20 Then
            r.Regime = "RANGE_BOUND"
        Else
            r.Regime = "TRANSITIONAL"
        End If

        ' --- VWAP ---
        r.VWAP = IndicatorEngine.CalcVWAP(candles1m)
        r.VWAPDevPct = If(r.VWAP > 0, (r.CurrentPrice - r.VWAP) / r.VWAP * 100, 0)

        ' --- Bollinger Bands Width ---
        Dim minBBW As Double
        IndicatorEngine.CalcBBW(candles1m, 20, 2.0, r.BBW, minBBW, r.SqueezeStatus)

        ' --- EMA Ribbon ---
        r.EMA9 = IndicatorEngine.CalcEMA(candles1m, 9)
        r.EMA21 = IndicatorEngine.CalcEMA(candles1m, 21)
        r.EMA50 = IndicatorEngine.CalcEMA(candles1m, 50)
        If r.EMA9 > r.EMA21 AndAlso r.EMA21 > r.EMA50 Then
            r.EMAAlignment = "BULL"
        ElseIf r.EMA9 < r.EMA21 AndAlso r.EMA21 < r.EMA50 Then
            r.EMAAlignment = "BEAR"
        Else
            r.EMAAlignment = "MIXED"
        End If

        ' --- Funding Rate ---
        r.FundingRate = fundingRate
        If fundingRate > 0.001 Then
            r.FundingBias = "LONGS HEAVILY CROWDED"
        ElseIf fundingRate > 0.0005 Then
            r.FundingBias = "LONGS CROWDED"
        ElseIf fundingRate < -0.001 Then
            r.FundingBias = "SHORTS HEAVILY CROWDED"
        ElseIf fundingRate < -0.0005 Then
            r.FundingBias = "SHORTS CROWDED"
        Else
            r.FundingBias = "NEUTRAL"
        End If

        ' --- Open Interest Delta ---
        Dim nowTs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        r.OI_Current = bookSummary.OI
        _oiHistory.Add((nowTs, bookSummary.OI))
        ' Trim history older than 70 minutes
        _oiHistory = _oiHistory.Where(Function(x) nowTs - x.Ts < 70 * 60 * 1000L).ToList()

        Dim oi15m = _oiHistory.Where(Function(x) nowTs - x.Ts <= 15 * 60 * 1000L).
                               OrderBy(Function(x) x.Ts).FirstOrDefault()
        Dim oi60m = _oiHistory.Where(Function(x) nowTs - x.Ts <= 61 * 60 * 1000L).
                               OrderBy(Function(x) x.Ts).FirstOrDefault()

        r.OIChange15m = If(oi15m.OI > 0, (r.OI_Current - oi15m.OI) / oi15m.OI * 100, 0)
        r.OIChange60m = If(oi60m.OI > 0, (r.OI_Current - oi60m.OI) / oi60m.OI * 100, 0)

        Dim priceUp As Boolean = r.CurrentPrice > bookSummary.MarkPrice * 0.9999
        ' OI signal
        If r.OIChange15m > 1 AndAlso priceUp Then
            r.OISignal = "NEW LONGS"
        ElseIf r.OIChange15m > 1 AndAlso Not priceUp Then
            r.OISignal = "NEW SHORTS"
        ElseIf r.OIChange15m < -1 AndAlso priceUp Then
            r.OISignal = "COVERING"
        ElseIf r.OIChange15m < -1 AndAlso Not priceUp Then
            r.OISignal = "CAPITULATION"
        Else
            r.OISignal = "NEUTRAL"
        End If

        ' --- Order Flow Imbalance ---
        IndicatorEngine.CalcOFI(orderBook, r.OFIRatio, r.OFISignal)

        ' --- Liquidations ---
        IndicatorEngine.CalcLiquidations(recentTrades, r.LiqLongSize, r.LiqShortSize, r.LiqSignal)

        ' --- 5m EMA(200) ---
        r.EMA200_5m = IndicatorEngine.CalcEMA(candles5m, 200)
        r.PriceVsEMA200 = If(r.EMA200_5m > 0,
                              If(r.CurrentPrice > r.EMA200_5m, "ABOVE", "BELOW"),
                              "N/A")

        ' --- Donchian ---
        IndicatorEngine.CalcDonchian(candles1m, 20, r.DonchianUpper, r.DonchianLower)
        If r.CurrentPrice > r.DonchianUpper Then
            r.DonchianSignal = "LONG"
        ElseIf r.CurrentPrice < r.DonchianLower Then
            r.DonchianSignal = "SHORT"
        Else
            r.DonchianSignal = "NONE"
        End If

        ' --- OBV ---
        IndicatorEngine.CalcOBV(candles1m, r.OBVTrend, r.OBVDivergence)

        ' ── 3. Scoring engine ────────────────────────────────────────────────
        Dim posState As PositionState = PositionState.None
        If rbLong.Checked Then posState = PositionState.InLong
        If rbShort.Checked Then posState = PositionState.InShort

        Dim verdict = ScoringEngine.Calculate(r, posState)

        ' ── 4. Render output ──────────────────────────────────────────────────
        RenderOutput(r, verdict)
    End Function

    Private Sub RenderOutput(r As IndicatorResults, v As VerdictResult)
        Dim sb As New System.Text.StringBuilder()
        Dim ts As String = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") & " UTC"

        sb.AppendLine("═══════════════════════════════════════════════════════════")
        sb.AppendLine($"  VERDICT:    {v.Verdict}")
        sb.AppendLine($"  CONFIDENCE: {v.Confidence}")
        sb.AppendLine($"  SCORE:      Long {v.LongScore}/15  |  Short {v.ShortScore}/15")
        sb.AppendLine($"  TIME:       {ts}")
        sb.AppendLine("═══════════════════════════════════════════════════════════")
        sb.AppendLine()
        sb.AppendLine($"REGIME (5m): {r.Regime}")
        sb.AppendLine($"  ADX: {r.ADX:F1}  |  +DI: {r.PlusDI:F1}  |  -DI: {r.MinusDI:F1}")
        sb.AppendLine()
        sb.AppendLine("CORE SIGNALS (1m):")
        sb.AppendLine($"  ROC(9):       {r.ROC:F3}  |  Slope: {r.ROCSlope}")
        sb.AppendLine($"  RSI(9):       {r.RSI:F1}")
        sb.AppendLine($"  Volume:       {r.CurrentVolume:F2}  |  vs SMA: {r.VolumeRatio:F2}x  |  SMA: {r.VolumeSMA9:F2}")
        sb.AppendLine()
        sb.AppendLine("TIER 1 SIGNALS:")
        sb.AppendLine($"  VWAP:         {r.VWAP:F1}  |  Dev: {r.VWAPDevPct:F2}%  |  Price: {r.CurrentPrice:F1}")
        sb.AppendLine($"  BBW:          {r.BBW:F4}  |  Squeeze: {r.SqueezeStatus}")
        sb.AppendLine($"  EMA Ribbon:   9:{r.EMA9:F1}  21:{r.EMA21:F1}  50:{r.EMA50:F1}  |  {r.EMAAlignment}")
        sb.AppendLine($"  Funding:      {r.FundingRate * 100:F5}%  |  {r.FundingBias}")
        sb.AppendLine($"  OI Change:    15m: {r.OIChange15m:F2}%  |  60m: {r.OIChange60m:F2}%  |  {r.OISignal}")
        sb.AppendLine()
        sb.AppendLine("TIER 2 SIGNALS:")
        sb.AppendLine($"  Order Flow:   {r.OFISignal}  |  Bid/Ask Ratio: {r.OFIRatio:F2}")
        sb.AppendLine($"  Liquidations: {r.LiqSignal}  |  Long Liqs: {r.LiqLongSize:F0}  Short Liqs: {r.LiqShortSize:F0}")
        sb.AppendLine($"  5m EMA(200):  {r.EMA200_5m:F1}  |  {r.PriceVsEMA200}")
        sb.AppendLine()
        sb.AppendLine("TIER 3 SIGNALS:")
        sb.AppendLine($"  Donchian(20): Upper:{r.DonchianUpper:F1}  Lower:{r.DonchianLower:F1}  |  {r.DonchianSignal}")
        sb.AppendLine($"  OBV:          Trend:{r.OBVTrend}  |  Divergence:{r.OBVDivergence}")
        sb.AppendLine()
        sb.AppendLine("POSITION SIZING:")
        sb.AppendLine($"  ATR(7):       {r.ATR:F2}  |  20d Avg Proxy: {r.ATRAvg20d:F2}  |  Multiplier: {r.ATRSizeMultiplier:F2}x")
        sb.AppendLine()
        sb.AppendLine("HOLD/EXIT STATUS:")
        sb.AppendLine($"  {v.HoldStatus}")
        sb.AppendLine()
        sb.AppendLine("SIGNAL BREAKDOWN:")
        sb.AppendLine($"  {"Indicator",-20} {"Long":>6} {"Short":>6}  Note")
        sb.AppendLine($"  {New String("-"c, 65)}")
        For Each sig In v.SignalBreakdown
            Dim lMark As String = If(sig.LongHit, "✔", "·")
            Dim sMark As String = If(sig.ShortHit, "✔", "·")
            sb.AppendLine($"  {sig.Label,-20} {lMark,6} {sMark,6}  {sig.Note}")
        Next
        sb.AppendLine("═══════════════════════════════════════════════════════════")

        txtOutput.Text = sb.ToString()

        ' Color-code verdict label
        lblVerdict.Text = v.Verdict
        lblVerdict.BackColor = v.Verdict Switch {
            "STRONG LONG" => Color.LimeGreen,
            "LONG" => Color.Green,
            "WEAK LONG" => Color.DarkGreen,
            "STRONG SHORT" => Color.Red,
            "SHORT" => Color.Crimson,
            "WEAK SHORT" => Color.DarkRed,
            _ => Color.Gray
        }
        lblVerdict.ForeColor = Color.White
    End Sub

    ' ── Boilerplate ───────────────────────────────────────────────────────────
    Public Sub New()
        InitializeComponent()
        AddHandler Me.Resize, Sub(s As Object, ev As EventArgs) ResizeControls()
        ResizeControls()
    End Sub

    Private Sub ResizeControls()
        txtOutput.Size = New Size(Me.ClientSize.Width - 28, Me.ClientSize.Height - 134)
    End Sub


End Class
