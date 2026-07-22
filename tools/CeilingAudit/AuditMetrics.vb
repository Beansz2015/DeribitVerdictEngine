' tools/CeilingAudit/AuditMetrics.vb
' AUC + Brier + lift, chronological train/test split, internal-walk-forward λ tuner, and
' the session-hour block bootstrap that CIs the ΔAUC (challenger − baseline) — the number
' the §4 decision rule reads.
'
' Nothing here trains a model — the fitter is L2Logistic. This module composes: it fits
' at multiple λ's, holds out a validation slice, and resamples session-hour blocks to
' quantify sampling uncertainty on the delta.
'
' §3 validity discipline is enforced here in code:
'   - MakeChronologicalSplit sorts by timestamp so no test row precedes any train row
'     (fixture A39c pins this).
'   - Bootstrap resamples INTACT session-hour blocks — a block is (utc-date, utc-hour) —
'     so no bootstrap sample straddles a session-hour boundary (fixture A39d pins this).
'   - TuneLambda does its λ search on an internal chronological split WITHIN the train
'     block (nothing touches test).
'
' Determinism: the bootstrap uses a seeded System.Random. Same seed → same CI.

Imports System.Collections.Generic
Imports System.Linq

Namespace CeilingAudit

    Public Class MetricResult
        Public Property Auc As Double
        Public Property Brier As Double
        Public Property SuccessAtOperatingPoint As Double   ' lift @ STRONG+MEDIUM count K
        Public Property N As Integer
    End Class

    Public Class BootstrapCi
        Public Property DeltaMean As Double
        Public Property CiLow As Double
        Public Property CiHigh As Double
        Public Property N As Integer
    End Class

    Public Class ChronologicalSplit
        Public Property TrainIdx As New List(Of Integer)()
        Public Property TestIdx As New List(Of Integer)()
        Public Property TrainCutoffUtc As DateTime
        Public Property TestStartUtc As DateTime
        Public Property TestEndUtc As DateTime
        Public Property TestSpansSessions As Boolean          ' §3 discipline: ≥1 full week + all sessions
    End Class

    Public Class AuditMetrics

        ''' <summary>Sort by timestamp (ascending) and split so the test block spans at least
        ''' <paramref name="minTestDays"/> calendar days — chronologically clean, no row from
        ''' train can be later than any row from test.</summary>
        Public Shared Function MakeChronologicalSplit(bundles As List(Of FeatureBundle),
                                                       timestamps As List(Of DateTime),
                                                       Optional minTestDays As Integer = 7) As ChronologicalSplit
            Dim n As Integer = bundles.Count
            Dim order = Enumerable.Range(0, n).OrderBy(Function(i) timestamps(i)).ToList()
            Dim split As New ChronologicalSplit()
            If n = 0 Then Return split

            Dim endUtc As DateTime = timestamps(order(n - 1))
            Dim cutoff As DateTime = endUtc.AddDays(-minTestDays)
            ' Walk from the end backward until we find the first row before the cutoff.
            Dim splitAt As Integer = n
            For k = n - 1 To 0 Step -1
                If timestamps(order(k)) < cutoff Then
                    splitAt = k + 1
                    Exit For
                End If
            Next

            ' Fall back to 70/30 if the CSV is shorter than the minTestDays window.
            If splitAt >= n OrElse splitAt <= 0 Then
                splitAt = CInt(System.Math.Round(n * 0.7))
                splitAt = System.Math.Max(1, System.Math.Min(n - 1, splitAt))
            End If

            For k = 0 To splitAt - 1
                split.TrainIdx.Add(order(k))
            Next
            For k = splitAt To n - 1
                split.TestIdx.Add(order(k))
            Next
            split.TrainCutoffUtc = timestamps(order(splitAt - 1))
            split.TestStartUtc = timestamps(order(splitAt))
            split.TestEndUtc = endUtc

            ' §3: report whether the test block spans ≥ minTestDays AND touches ≥ 3 distinct
            ' UTC hours (as a shallow proxy for "all sessions" — the actual session mix is a
            ' population attribute, this is a per-split sanity flag).
            Dim testHours = split.TestIdx.Select(Function(i) timestamps(i).Hour).Distinct().Count()
            split.TestSpansSessions = ((split.TestEndUtc - split.TestStartUtc).TotalDays >= (minTestDays - 0.5)) AndAlso testHours >= 3
            Return split
        End Function

        ''' <summary>Fit at each candidate λ using an INTERNAL chronological split within the
        ''' train block, score by validation AUC, return the argmax. λ candidates:
        ''' {0.01, 0.1, 1.0, 10.0} — a coarse geometric sweep is enough because logistic AUC
        ''' curves are near-flat between neighbouring decades.</summary>
        Public Shared Function TuneLambda(Xtrain As Double(,), yTrain As Integer(),
                                          trainTimestamps As List(Of DateTime),
                                          Optional lambdaGrid As Double() = Nothing,
                                          Optional epochs As Integer = 500,
                                          Optional lr As Double = 0.5) As (BestLambda As Double, ValAucs As Dictionary(Of Double, Double))
            If lambdaGrid Is Nothing Then lambdaGrid = New Double() {0.01, 0.1, 1.0, 10.0}
            Dim n As Integer = Xtrain.GetLength(0)
            Dim d As Integer = Xtrain.GetLength(1)
            Dim results As New Dictionary(Of Double, Double)()

            If n < 20 Then
                ' Insufficient data → default λ; don't pretend to tune.
                For Each l In lambdaGrid
                    results(l) = Double.NaN
                Next
                Return (1.0, results)
            End If

            ' Chronological inner split — 80/20.
            Dim order = Enumerable.Range(0, n).OrderBy(Function(i) trainTimestamps(i)).ToList()
            Dim cutIdx As Integer = CInt(System.Math.Round(n * 0.8))
            cutIdx = System.Math.Max(1, System.Math.Min(n - 1, cutIdx))
            Dim inTrain = order.Take(cutIdx).ToList()
            Dim inVal = order.Skip(cutIdx).ToList()

            Dim XinTrain = Slice(Xtrain, inTrain, d)
            Dim yInTrain = inTrain.Select(Function(i) yTrain(i)).ToArray()
            Dim XinVal = Slice(Xtrain, inVal, d)
            Dim yInVal = inVal.Select(Function(i) yTrain(i)).ToArray()

            Dim bestLambda As Double = lambdaGrid(0)
            Dim bestAuc As Double = Double.NegativeInfinity
            For Each lam In lambdaGrid
                Dim m = L2Logistic.Fit(XinTrain, yInTrain, lam, lr, epochs)
                Dim pVal = m.PredictAll(XinVal)
                Dim aucVal As Double = Auc(pVal, yInVal)
                results(lam) = aucVal
                If aucVal > bestAuc Then
                    bestAuc = aucVal
                    bestLambda = lam
                End If
            Next
            Return (bestLambda, results)
        End Function

        Private Shared Function Slice(X As Double(,), idx As List(Of Integer), d As Integer) As Double(,)
            Dim n As Integer = idx.Count
            Dim Xs(n - 1, d - 1) As Double
            For i = 0 To n - 1
                For j = 0 To d - 1
                    Xs(i, j) = X(idx(i), j)
                Next
            Next
            Return Xs
        End Function

        ''' <summary>ROC AUC via the Mann–Whitney U formulation. O(n log n) via a sort on
        ''' scores. Ties contribute 0.5.</summary>
        Public Shared Function Auc(scores As Double(), y As Integer()) As Double
            Dim n As Integer = scores.Length
            If n = 0 Then Return 0.5
            Dim nPos As Integer = 0
            Dim nNeg As Integer = 0
            For i = 0 To n - 1
                If y(i) = 1 Then nPos += 1 Else nNeg += 1
            Next
            If nPos = 0 OrElse nNeg = 0 Then Return 0.5

            ' Rank-based sum for positives (average ranks for ties).
            Dim idx = Enumerable.Range(0, n).OrderBy(Function(i) scores(i)).ToList()
            Dim ranks(n - 1) As Double
            Dim i0 As Integer = 0
            While i0 < n
                Dim j0 As Integer = i0
                While j0 + 1 < n AndAlso scores(idx(j0 + 1)) = scores(idx(i0))
                    j0 += 1
                End While
                Dim avgRank As Double = (i0 + j0) / 2.0 + 1.0    ' ranks are 1-based
                For k = i0 To j0
                    ranks(idx(k)) = avgRank
                Next
                i0 = j0 + 1
            End While

            Dim sumPosRanks As Double = 0
            For i = 0 To n - 1
                If y(i) = 1 Then sumPosRanks += ranks(i)
            Next
            Dim u As Double = sumPosRanks - CDbl(nPos) * (nPos + 1) / 2.0
            Return u / (CDbl(nPos) * nNeg)
        End Function

        Public Shared Function Brier(scores As Double(), y As Integer()) As Double
            Dim n As Integer = scores.Length
            If n = 0 Then Return Double.NaN
            Dim s As Double = 0.0
            For i = 0 To n - 1
                Dim d As Double = scores(i) - y(i)
                s += d * d
            Next
            Return s / n
        End Function

        ''' <summary>Success rate on the top-K rows ranked by score. K = the operating count —
        ''' the number of rows the pipeline actually traded (STRONG+MEDIUM tier) in this
        ''' test slice. Both baseline and challenger use the SAME K so the lift is like-for-
        ''' like.</summary>
        Public Shared Function SuccessAtK(scores As Double(), y As Integer(), k As Integer) As Double
            If k <= 0 OrElse scores.Length = 0 Then Return Double.NaN
            k = System.Math.Min(k, scores.Length)
            Dim idx = Enumerable.Range(0, scores.Length).OrderByDescending(Function(i) scores(i)).Take(k).ToList()
            Dim hit As Integer = 0
            For Each i In idx
                If y(i) = 1 Then hit += 1
            Next
            Return CDbl(hit) / k
        End Function

        ''' <summary>Bundle metrics into one MetricResult under a given K.</summary>
        Public Shared Function Evaluate(scores As Double(), y As Integer(), k As Integer) As MetricResult
            Return New MetricResult With {
                .Auc = Auc(scores, y),
                .Brier = Brier(scores, y),
                .SuccessAtOperatingPoint = SuccessAtK(scores, y, k),
                .N = scores.Length}
        End Function

        ''' <summary>Assign each row to a session-hour block key. Blocks are (UTC date, UTC hour)
        ''' so no block straddles a session-hour boundary — fixture A39d pins this.</summary>
        Public Shared Function AssignBlocks(timestamps As List(Of DateTime)) As Integer()
            Dim map As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            Dim out(timestamps.Count - 1) As Integer
            For i = 0 To timestamps.Count - 1
                Dim ts As DateTime = timestamps(i)
                Dim key As String = ts.Date.ToString("yyyy-MM-dd") & "H" & ts.Hour.ToString()
                Dim id As Integer
                If Not map.TryGetValue(key, id) Then
                    id = map.Count
                    map(key) = id
                End If
                out(i) = id
            Next
            Return out
        End Function

        ''' <summary>Block-bootstrap the ΔAUC (challenger − baseline). Resamples the SET of
        ''' distinct block IDs with replacement (each draw = ONE INTACT block, all its rows
        ''' come along). Returns the mean ΔAUC over B resamples plus the [2.5%, 97.5%]
        ''' percentile CI.</summary>
        Public Shared Function BootstrapDeltaAucCi(challengerScores As Double(),
                                                    baselineScores As Double(),
                                                    y As Integer(),
                                                    blocks As Integer(),
                                                    Optional B As Integer = 1000,
                                                    Optional seed As Integer = 42) As BootstrapCi
            Dim n As Integer = challengerScores.Length
            Dim ci As New BootstrapCi() With {.N = n}
            If n = 0 Then Return ci

            Dim blockToRows As New Dictionary(Of Integer, List(Of Integer))()
            For i = 0 To n - 1
                Dim id As Integer = blocks(i)
                Dim lst As List(Of Integer) = Nothing
                If Not blockToRows.TryGetValue(id, lst) Then
                    lst = New List(Of Integer)()
                    blockToRows(id) = lst
                End If
                lst.Add(i)
            Next
            Dim blockIds = blockToRows.Keys.ToArray()
            Dim rng As New System.Random(seed)
            Dim deltas As New List(Of Double)()
            Dim K As Integer = blockIds.Length
            For b = 1 To B
                Dim sample As New List(Of Integer)()
                For draw = 1 To K
                    Dim pick As Integer = blockIds(rng.Next(K))
                    sample.AddRange(blockToRows(pick))
                Next
                Dim m As Integer = sample.Count
                Dim sC(m - 1) As Double
                Dim sB(m - 1) As Double
                Dim yy(m - 1) As Integer
                For i = 0 To m - 1
                    sC(i) = challengerScores(sample(i))
                    sB(i) = baselineScores(sample(i))
                    yy(i) = y(sample(i))
                Next
                deltas.Add(Auc(sC, yy) - Auc(sB, yy))
            Next
            deltas.Sort()
            ci.DeltaMean = deltas.Average()
            ci.CiLow = deltas(CInt(System.Math.Floor(0.025 * (deltas.Count - 1))))
            ci.CiHigh = deltas(CInt(System.Math.Floor(0.975 * (deltas.Count - 1))))
            Return ci
        End Function

    End Class

End Namespace
