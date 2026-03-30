' AnalysisLogger.vb
' Appends one row per analysis run to a local CSV file.
' File location: same directory as the executable.
' Reset: truncates file back to header only.

Imports System.IO

Public Class AnalysisLogger

    Private Const FileName As String = "analysis_log.csv"

    Private Shared ReadOnly Header As String =
        "Timestamp,Price,Verdict,Confidence,LongScore,ShortScore," &
        "Regime,ROC,ROCSlope,RSI,RSIDivergence," &
        "VolumeRatio,OFIRatio,OFISignal," &
        "LiqLongSize,LiqShortSize,LiqSignal," &
        "OBVTrend,OBVDivergence," &
        "ATR,ATRMultiplier"

    Public Shared Function GetLogPath() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    Public Shared Function GetRowCount() As Integer
        Dim path As String = GetLogPath()
        If Not File.Exists(path) Then Return 0
        ' Subtract 1 for header row
        Return Math.Max(0, File.ReadAllLines(path).Length - 1)
    End Function

    Public Shared Sub LogRun(r As IndicatorResults, v As VerdictResult)
        Dim path As String = GetLogPath()
        Dim writeHeader As Boolean = Not File.Exists(path) OrElse New FileInfo(path).Length = 0
        Try
            Using sw As New StreamWriter(path, append:=True)
                If writeHeader Then sw.WriteLine(Header)
                Dim ts As String = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                sw.WriteLine(String.Join(",",
                    ts,
                    r.CurrentPrice.ToString("F2"),
                    v.Verdict,
                    v.Confidence,
                    v.LongScore.ToString(),
                    v.ShortScore.ToString(),
                    r.Regime,
                    r.ROC.ToString("F4"),
                    r.ROCSlope,
                    r.RSI.ToString("F2"),
                    r.RSIDivergence,
                    r.VolumeRatio.ToString("F4"),
                    r.OFIRatio.ToString("F4"),
                    r.OFISignal,
                    r.LiqLongSize.ToString("F2"),
                    r.LiqShortSize.ToString("F2"),
                    r.LiqSignal,
                    r.OBVTrend,
                    r.OBVDivergence,
                    r.ATR.ToString("F4"),
                    r.ATRSizeMultiplier.ToString("F4")))
            End Using
        Catch
            ' Silent fail -- logging must never crash the main pipeline
        End Try
    End Sub

    Public Shared Sub ResetLog()
        Dim path As String = GetLogPath()
        Try
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine(Header)
            End Using
        Catch
        End Try
    End Sub

End Class
