' RR-1 D-2 check (docs/gap-repair-rr1-spec-back.md section 2, D-2 note item 2): does .NET 8
' List.Sort throw on the ST-1 comparator cycle, or sort silently wrong? Read-only; touches no file.
' Run: dotnet run -c Release --project tools/checks/sort-consistency-probe/SortConsistencyProbe.vbproj
Imports System.Collections.Generic
Imports System.Linq

Module Program
    ' Mirrors Core/TradeStoreWriter.vb SeqPoint (line 913).
    Structure SeqPoint
        Public TsMs As Long
        Public Seq As Long
    End Structure

    Function P(ts As Long, seq As Long) As SeqPoint
        Return New SeqPoint With {.TsMs = ts, .Seq = seq}
    End Function

    ' VERBATIM from Core/TradeStoreWriter.vb ResolveRepairWindowsCore (lines 1041-1046, 2026-09-26).
    ' If that comparator changes, copy it here again.
    ReadOnly Cmp As Comparison(Of SeqPoint) =
        Function(a, b)
            If a.Seq >= 0 AndAlso b.Seq >= 0 Then Return a.Seq.CompareTo(b.Seq)
            Dim c As Integer = a.TsMs.CompareTo(b.TsMs)
            If c <> 0 Then Return c
            Return a.Seq.CompareTo(b.Seq)
        End Function

    Sub Run(label As String, make As Func(Of List(Of SeqPoint)), trials As Integer)
        Dim rng As New Random(12345)
        Dim outcomes As New Dictionary(Of String, Integer)()
        Dim pairViolations As Integer = 0
        For t As Integer = 1 To trials
            Dim l As List(Of SeqPoint) = make().OrderBy(Function(x) rng.Next()).ToList()
            Dim key As String = "none"
            Try
                l.Sort(Cmp)
                Dim ok As Boolean = True
                For i As Integer = 0 To l.Count - 1
                    For j As Integer = i + 1 To l.Count - 1
                        If Cmp(l(i), l(j)) > 0 Then ok = False
                    Next
                Next
                If Not ok Then pairViolations += 1
            Catch ex As Exception
                key = ex.GetType().Name
            End Try
            outcomes(key) = If(outcomes.ContainsKey(key), outcomes(key), 0) + 1
        Next
        Console.WriteLine($"{label}: trials={trials} outcomes=[{String.Join(", ", outcomes.Select(Function(kv) kv.Key & "=" & kv.Value))}] returnedOrderWithPairViolation={pairViolations}")
    End Sub

    Sub Main()
        Console.WriteLine(".NET " & Environment.Version.ToString())

        ' Control: a fully random comparer - what .NET 8 throws when it does detect inconsistency.
        Dim r As New Random(7), threw As Integer = 0, kind As String = ""
        For t As Integer = 1 To 300
            Dim ints As List(Of Integer) = Enumerable.Range(0, 2000).ToList()
            Try
                ints.Sort(Function(a, b) If(a = b, 0, If(r.Next(2) = 0, -1, 1)))
            Catch ex As Exception
                threw += 1 : kind = ex.GetType().Name
            End Try
        Next
        Console.WriteLine($"random comparator, 2000 ints: trials=300 threw={threw} lastType={kind}")

        ' A91c as shipped: legacy@50000, N+1@100001, N+3@100000 (invariant-respecting).
        Run("A91c shipped rows (3)", Function() New List(Of SeqPoint) From {P(50000, -1), P(100001, 9001), P(100000, 9003)}, 2000)
        ' ST-1 cycle (docs/gap-repair-rr1-seq-order-spec.md section 4.2): A(seq 100, t 500), B legacy t 200, C(seq 200, t 100).
        Run("ST-1 cycle, 3 rows", Function() New List(Of SeqPoint) From {P(500, 100), P(200, -1), P(100, 200)}, 2000)
        For Each n As Integer In {17, 40, 200, 2000}
            Dim size As Integer = n
            Run($"ST-1 cycles repeated, {size} rows",
                Function()
                    Dim l As New List(Of SeqPoint)()
                    Dim k As Integer = 0
                    While l.Count < size
                        Dim b As Long = 1000L * k : k += 1
                        l.Add(P(b + 500, 100 + 10 * k))
                        If l.Count < size Then l.Add(P(b + 200, -1))
                        If l.Count < size Then l.Add(P(b + 100, 105 + 10 * k))
                    End While
                    Return l
                End Function, 500)
        Next
    End Sub
End Module
