' tools/AutoTweaker/ClaudeApiClient.vb
' HTTPS client to Anthropic API.
' API key MUST come from ANTHROPIC_API_KEY env var — never hardcoded.
' Latest-Opus model resolved via GET /v1/models (spec section 4).
' Dry-run mode writes payload to file instead of calling the API.
' Host-agnostic: no System.Windows.Forms references.

Imports System.IO
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Threading.Tasks

Public Class ClaudeApiClient

    Private Const BaseUrl As String = "https://api.anthropic.com"
    Private Const AnthropicVersion As String = "2023-06-01"
    Private Const FallbackModel As String = "claude-opus-latest"
    Private Const MaxTokens As Integer = 4096

    Private Shared ReadOnly _http As New HttpClient() With {
        .Timeout = TimeSpan.FromSeconds(120)
    }

    ' Resolve the latest claude-opus-* model via /v1/models.
    ' Returns the model id sorted by created_at desc.
    ' Falls back to FallbackModel on any error.
    Public Shared Async Function ResolveLatestOpusModelAsync(apiKey As String) As Task(Of String)
        Try
            Dim req As New HttpRequestMessage(HttpMethod.Get, BaseUrl & "/v1/models")
            req.Headers.Add("x-api-key", apiKey)
            req.Headers.Add("anthropic-version", AnthropicVersion)

            Dim resp = Await _http.SendAsync(req)
            If Not resp.IsSuccessStatusCode Then Return FallbackModel

            Dim body = Await resp.Content.ReadAsStringAsync()
            Dim doc = JsonDocument.Parse(body)
            Dim data = doc.RootElement.GetProperty("data")

            Dim best As String = ""
            Dim bestCreated As Long = Long.MinValue

            For Each m In data.EnumerateArray()
                Dim id As String = m.GetProperty("id").GetString()
                If id Is Nothing OrElse Not id.StartsWith("claude-opus-") Then Continue For
                Dim created As Long = 0
                If m.TryGetProperty("created_at", Nothing) Then
                    Dim createdEl As JsonElement
                    If m.TryGetProperty("created_at", createdEl) Then
                        If createdEl.ValueKind = JsonValueKind.Number Then
                            created = createdEl.GetInt64()
                        End If
                    End If
                End If
                If created > bestCreated Then
                    bestCreated = created
                    best = id
                End If
            Next

            Return If(String.IsNullOrEmpty(best), FallbackModel, best)
        Catch
            Return FallbackModel
        End Try
    End Function

    ' Call the Claude messages API. Returns the text content of the first response block.
    Public Shared Async Function CallAsync(apiKey As String, model As String,
                                           systemMsg As String, userMsg As String) As Task(Of String)
        Dim body As New JsonObject From {
            {"model",      JsonNode.Parse("""" & model & """")},
            {"max_tokens", JsonValue.Create(MaxTokens)},
            {"system",     JsonNode.Parse("""" & EscapeJson(systemMsg) & """")},
            {"messages",   New JsonArray From {
                               New JsonObject From {
                                   {"role",    JsonNode.Parse("""user""")},
                                   {"content", JsonNode.Parse("""" & EscapeJson(userMsg) & """")}
                               }
                           }}
        }

        Dim json As String = body.ToJsonString()
        Dim content As New StringContent(json, Encoding.UTF8, "application/json")

        Dim req As New HttpRequestMessage(HttpMethod.Post, BaseUrl & "/v1/messages") With {
            .Content = content
        }
        req.Headers.Add("x-api-key", apiKey)
        req.Headers.Add("anthropic-version", AnthropicVersion)

        Dim resp = Await _http.SendAsync(req)
        Dim respBody = Await resp.Content.ReadAsStringAsync()

        If Not resp.IsSuccessStatusCode Then
            Throw New Exception(String.Format("Claude API error {0}: {1}",
                                              CInt(resp.StatusCode), respBody))
        End If

        ' Extract content[0].text
        Dim doc = JsonDocument.Parse(respBody)
        Dim contentArr = doc.RootElement.GetProperty("content")
        For Each block In contentArr.EnumerateArray()
            If block.TryGetProperty("text", Nothing) Then
                Dim textEl As JsonElement
                If block.TryGetProperty("text", textEl) Then
                    Return textEl.GetString()
                End If
            End If
        Next

        Throw New Exception("No text content in Claude response: " & respBody)
    End Function

    ' Write a dry-run payload file instead of calling the API.
    ' Format matches spec section 5 exactly.
    Public Shared Function WriteDryRunFile(systemMsg As String, userMsg As String,
                                           model As String, trigger As String,
                                           outputDir As String) As String
        Directory.CreateDirectory(outputDir)
        Dim ts As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
        Dim filePath As String = IO.Path.Combine(outputDir, ts & ".txt")

        ' Build JSON request body for reference
        Dim bodyObj As New JsonObject From {
            {"model",      JsonNode.Parse("""" & model & """")},
            {"max_tokens", JsonValue.Create(MaxTokens)},
            {"system",     JsonNode.Parse("""" & EscapeJson(systemMsg) & """")},
            {"messages",   New JsonArray From {
                               New JsonObject From {
                                   {"role",    JsonNode.Parse("""user""")},
                                   {"content", JsonNode.Parse("""" & EscapeJson(userMsg) & """")}
                               }
                           }}
        }
        Dim bodyJson As String = bodyObj.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True})

        Dim sb As New StringBuilder()
        sb.AppendLine("=== AUTO-TWEAKER DRY RUN === " & DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        sb.AppendLine("Trigger: " & trigger)
        sb.AppendLine()
        sb.AppendLine("=== SYSTEM MESSAGE ===")
        sb.AppendLine(systemMsg)
        sb.AppendLine()
        sb.AppendLine("=== USER MESSAGE ===")
        sb.AppendLine(userMsg)
        sb.AppendLine()
        sb.AppendLine("=== JSON REQUEST BODY ===")
        sb.AppendLine(bodyJson)
        sb.AppendLine()
        sb.AppendLine("=== INSTRUCTIONS FOR HUMAN ===")
        sb.AppendLine("Open a new Claude conversation. Paste the SYSTEM MESSAGE as the system prompt")
        sb.AppendLine("(or as a leading user message if system prompts are unavailable in your client).")
        sb.AppendLine("Paste the USER MESSAGE as your first message. Claude returns a JSON diff.")
        sb.AppendLine("Save the JSON in tools/AutoTweaker/manual_diffs/<timestamp>.json. Run")
        sb.AppendLine("AutoTweaker.exe with --apply-manual <path> to apply.")

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8)
        Return filePath
    End Function

    ' Minimal JSON string escaping (backslash and double-quote).
    ' Full escaping handled by JsonNode serialisation; this covers inline string embedding.
    Private Shared Function EscapeJson(s As String) As String
        Return s.Replace("\", "\\").Replace("""", "\""").
                 Replace(vbCr, "\r").Replace(vbLf, "\n").Replace(vbTab, "\t")
    End Function

End Class
