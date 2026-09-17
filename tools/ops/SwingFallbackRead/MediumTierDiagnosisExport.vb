Option Strict On
Option Infer On

' tools/ops/SwingFallbackRead/MediumTierDiagnosisExport.vb
'
' --mode diagexport: the per-row export for the MEDIUM-tier diagnosis, session 2
' (docs/medium-tier-diagnosis-brief-2026-09-16.md section 3; read: docs/medium-tier-diagnosis-read-2026-09-17.md).
' It adds NO statistic. It writes one line per swing read population row: the swing read's own candle-walk
' outcomes (main window and carried 24 h, the same Walk and Result the census and the stability read use),
' the chronological half (the stability read's split rule) and the logged context columns by header name.
' The analysis runs in tools/ops/medium_tier_diagnosis.py, which joins this file to
' <cache>/rescore-attribution.csv (--mode rescore) by Timestamp.
'
' Run (from the repo root):
'   dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
'   dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode diagexport \
'     --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Partial Module SwingFallbackReadProgram

    ' Logged columns copied verbatim, by header name.
    Private ReadOnly DxRawCols As String() = {
        "InstanceId", "Regime", "ADX", "MaxScore", "LongScore", "ShortScore", "EffectiveLongScore", "EffectiveShortScore", "RegimePenalty",
        "VWAP", "VWAPDevPct", "ROC", "RSI", "EMA9", "EMA21", "EMA50", "LastSwingHigh5m", "LastSwingLow5m", "LastSwingHigh15m", "LastSwingLow15m",
        "TTMHistogram", "VerdictContext", "VolumeRatio", "AggrVelSignal", "OBVTrend", "MicroCVDSignal", "TrendStructure5m", "FundingBias",
        "MTFGateReason", "SignalId"}

    Private Function RunDiagExport(header As StringBuilder, sigs As List(Of Sig), fees As FeeCase, pooledPath As String, livePath As String,
                                   outCsv As String, outMd As String) As Integer
        Dim raw As New Dictionary(Of DateTime, RsRaw)()
        RsLoadRaw(pooledPath, raw)
        RsLoadRaw(livePath, raw)

        Dim days = sigs.Select(Function(x) x.Ts.Date).Distinct().OrderBy(Function(x) x).ToList()
        Dim splitDate As DateTime = days(days.Count \ 2)

        Dim sb As New StringBuilder()
        sb.Append("Timestamp,Day,Half,Session,Side,Tier,Verdict,Cap,ExecRes,SessionRes,Price,Atr,TBps,SBps,TAtr,SAtr,MainWindowMin,")
        sb.Append("MainOutcome,MainResolveMin,MainNetEv,MainMissingBars,C24Outcome,C24ResolveMin,C24NetEv,C24MissingBars")
        For Each c In DxRawCols
            sb.Append(","c).Append(c)
        Next
        sb.AppendLine()
        Dim nMissingRaw As Integer = 0
        For Each s In sigs.OrderBy(Function(x) x.Ts)
            Dim x As RsRaw = Nothing
            If Not raw.TryGetValue(s.Ts, x) Then nMissingRaw += 1 : Continue For
            Dim mk As String = ModeKey(s, "main")
            Dim wm = s.Res(mk), wc = s.Res("C24")
            Dim f As New List(Of String) From {
                s.Ts.ToString("yyyy-MM-dd HH:mm:ss", Inv), s.Ts.ToString("yyyy-MM-dd", Inv), If(s.Ts < splitDate, "H1", "H2"), s.Session,
                If(s.IsLong, "LONG", "SHORT"), s.Tier, s.Verdict, s.Cap, s.ExecRes.ToString(Inv), s.SessionRes.ToString(Inv),
                s.Price.ToString("R", Inv), s.Atr.ToString("R", Inv), s.TBps.ToString("R", Inv), s.SBps.ToString("R", Inv),
                s.TAtr.ToString("R", Inv), s.SAtr.ToString("R", Inv), s.Windows.Max().ToString(Inv),
                wm.Outcome.ToString(Inv), wm.ResolveMin.ToString("R", Inv), Result(s, wm, fees.MakerRt, fees.MakerRt).ToString("R", Inv), wm.MissingBars.ToString(Inv),
                wc.Outcome.ToString(Inv), wc.ResolveMin.ToString("R", Inv), Result(s, wc, fees.MakerRt, fees.MakerRt).ToString("R", Inv), wc.MissingBars.ToString(Inv)}
            For Each c In DxRawCols
                f.Add(RsS(x, c).Replace(","c, ";"c))
            Next
            sb.AppendLine(String.Join(",", f))
        Next
        File.WriteAllText(outCsv, sb.ToString(), New UTF8Encoding(False))

        Dim o As New StringBuilder()
        o.Append(header.ToString().Replace("# SwingFallbackRead output", "# SwingFallbackRead output: --mode diagexport (MEDIUM-tier diagnosis per-row export)"))
        o.AppendLine(String.Format(Inv, "- Population rows: {0}. Rows without a logged row (must be 0): {1}.", sigs.Count, nMissingRaw))
        o.AppendLine(String.Format(Inv, "- Split date {0:yyyy-MM-dd} 00:00 UTC: H1 = {1} trading days, H2 = {2} (the stability read's rule).", splitDate, days.Count \ 2, days.Count - days.Count \ 2))
        o.AppendLine(String.Format(Inv, "- Fees: maker/maker round trip {0:0.00} bps. Outcome codes: 0 open (marked), 1 target, 2 stop, 3 same-bar target and stop (= stop).", fees.MakerRt))
        o.AppendLine("- Wrote " & outCsv)
        File.WriteAllText(outMd, o.ToString(), New UTF8Encoding(False))
        Console.Write(o.ToString())
        Return If(nMissingRaw = 0, 0, 3)
    End Function

End Module
