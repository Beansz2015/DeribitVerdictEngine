' Core/OfiGapDiagnostic.vb
' [DIAG — THROWAWAY] OFI arithmetic-vs-geometric averaging gap test (P4 #4, deviation D2).
'
' Records, per warmed WS-averaged analysis run, the arithmetic averaged OFIRatio (the
' production value, r.OFIRatio) alongside the geometric averaged ratio computed in LOCKSTEP
' by OfiAccumulator (same alpha / dt / window — only the log-space averaging differs), so the
' AM/GM "buy-lean" gap can be measured DIRECTLY on live NY data rather than inferred.
'
' OFF the CSV, OFF scoring, no settings key — a side-log only, gated by the DVE_OFI_GAP_DIAG
' environment variable (set it to the output file path; unset/empty => completely inert).
' Never throws (a diagnostic must not disrupt a run). Host-agnostic (System.IO only).
'
' By AM-GM, geo_ratio <= arith_ratio on every row; gap = arith/geo >= 1 is the lean factor
' (~exp(sigma^2/2)). A row with geo > arith would indicate a math bug.
'
' THROWAWAY — once the arithmetic-vs-log-ratio construction is decided, DELETE this file +
' the OfiAccumulator [DIAG] geo lines + the RunAnalysisAsync WriteSample call. Do NOT push.
Imports System.IO
Imports System.Globalization

Public NotInheritable Class OfiGapDiagnostic

    Private Const EnvVar As String = "DVE_OFI_GAP_DIAG"
    Private Shared ReadOnly _lock As New Object()
    Private Shared _headerChecked As Boolean = False

    Public Shared Sub WriteSample(utcNow As DateTime, utcHour As Integer, price As Double,
                                  arithRatio As Double, geoRatio As Double,
                                  updateCount As Integer, coverageSec As Double,
                                  ofiSignal As String)
        Try
            Dim path As String = Environment.GetEnvironmentVariable(EnvVar)
            If String.IsNullOrWhiteSpace(path) Then Return   ' inert unless explicitly enabled

            Dim gap As Double = If(geoRatio <> 0.0, arithRatio / geoRatio, 0.0)
            SyncLock _lock
                If Not _headerChecked Then
                    _headerChecked = True
                    If Not File.Exists(path) Then
                        File.AppendAllText(path,
                            "utc,utc_hour,price,arith_ratio,geo_ratio,gap,update_count,coverage_sec,ofi_signal" &
                            Environment.NewLine)
                    End If
                End If
                File.AppendAllText(path,
                    String.Format(CultureInfo.InvariantCulture,
                        "{0:yyyy-MM-ddTHH:mm:ss},{1},{2:F2},{3:F6},{4:F6},{5:F6},{6},{7:F2},{8}" & Environment.NewLine,
                        utcNow, utcHour, price, arithRatio, geoRatio, gap, updateCount, coverageSec, If(ofiSignal, "")))
            End SyncLock
        Catch
            ' diagnostic must never disrupt a run
        End Try
    End Sub
End Class
