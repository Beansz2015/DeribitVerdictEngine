' tools/BacktestRunner/HistoryHostHttp.vb
' The ONE network seam of the history store (docs/history-data-store-spec.md §3.2): a GET
' against https://history.deribit.com/api/v2/public/. Kept out of HistoryStore.vb so the
' fixture harness links the store logic without an HttpClient it never calls.
'
' ⛔ UseProxy:=False — the WPAD hazard recorded in DeribitClient.vb: a hang in proxy resolution
' happens before the request and is not bounded by any request timeout.
' The timeout is per request (a CancellationTokenSource), not HttpClient.Timeout — the A93e
' lesson. Every failure becomes a HistoryHostException, which HistoryStore retries with backoff.

Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks

Public NotInheritable Class HistoryHostHttp

    Public Const BaseUrl As String = "https://history.deribit.com/api/v2/public/"

    ''' <summary>Seconds one request may take, connect to last byte.</summary>
    Public Const RequestTimeoutSeconds As Integer = 30

    Private Shared ReadOnly _http As HttpClient = CreateClient()

    Private Shared Function CreateClient() As HttpClient
        Dim h As New HttpClientHandler With {
            .UseProxy = False,
            .AutomaticDecompression = DecompressionMethods.GZip Or DecompressionMethods.Deflate}
        Dim c As New HttpClient(h) With {.Timeout = Timeout.InfiniteTimeSpan}
        c.DefaultRequestHeaders.Add("User-Agent", HistoricalStore.ResolveUserAgent() & " (history-store)")
        Return c
    End Function

    Public Shared Async Function GetJsonAsync(pathAndQuery As String) As Task(Of String)
        Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(RequestTimeoutSeconds))
            Try
                Using resp As HttpResponseMessage = Await _http.GetAsync(BaseUrl & pathAndQuery, cts.Token)
                    Dim body As String = Await resp.Content.ReadAsStringAsync(cts.Token)
                    If Not resp.IsSuccessStatusCode Then
                        Throw New HistoryHostException("HTTP " & CInt(resp.StatusCode) & " " & If(body.Length > 200, body.Substring(0, 200), body))
                    End If
                    Return body
                End Using
            Catch ex As OperationCanceledException
                Throw New HistoryHostException("timeout after " & RequestTimeoutSeconds & " s")
            Catch ex As HttpRequestException
                Throw New HistoryHostException("transport: " & ex.Message)
            Catch ex As IO.IOException
                Throw New HistoryHostException("io: " & ex.Message)
            End Try
        End Using
    End Function

End Class
