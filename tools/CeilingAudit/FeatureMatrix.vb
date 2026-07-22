' tools/CeilingAudit/FeatureMatrix.vb
' Builds the standardized design matrix X (train + test) from a list of FeatureBundles,
' one column per one-hot categorical level or standardized numeric. Encapsulates the
' train-only-fitting discipline:
'   - categorical levels are enumerated on the train slice; unseen test levels drop into
'     an "OTHER" column already present in the schema
'   - numerics are z-scored using train mean/std; missing values (NaN) are imputed with
'     the train median and paired with a "<name>_MISSING" binary indicator column
'   - the informational bundle is NEVER fed here — the caller passes only scored fields,
'     and A39e asserts absence
'
' Deterministic column ordering (schema.Columns) is the load-bearing contract: fixture
' A39a relies on it to check that the coefficient sort is stable, and the report emits
' per-column coefficients in this order.
'
' Host-agnostic; no System.Windows.Forms references; no numerical libraries.

Imports System.Collections.Generic
Imports System.Linq

Namespace CeilingAudit

    Public Class FeatureSchema
        Public Property Columns As List(Of String) = New List(Of String)()
        ' Per-numeric train mean/std/median (kept for standardize + median-impute at test time).
        Public Property NumericMean As Dictionary(Of String, Double) = New Dictionary(Of String, Double)()
        Public Property NumericStd As Dictionary(Of String, Double) = New Dictionary(Of String, Double)()
        Public Property NumericMedian As Dictionary(Of String, Double) = New Dictionary(Of String, Double)()
        ' Per-categorical set of accepted levels (unseen levels at test time → OTHER column).
        Public Property CategoricalLevels As Dictionary(Of String, List(Of String)) = New Dictionary(Of String, List(Of String))()
        ' The scored-feature name list, in the order rows are extracted from FeatureBundle
        ' — used by A39e to prove the informational names are absent.
        Public Property ScoredCategoricalNames As New List(Of String)()
        Public Property ScoredNumericNames As New List(Of String)()
        Public Property IncludeAggrVel As Boolean
    End Class

    Public Class FeatureMatrix

        ''' <summary>Fit a schema against the TRAIN bundles only. Categorical levels are the
        ''' distinct values that appear on train; numeric mean/std/median are computed on the
        ''' non-NaN train values (missing → median-imputed later with a paired missing flag).
        ''' includeAggrVel controls whether the AggrVel* fields join the scored side (armed
        ''' populations) or are omitted from the design matrix entirely (un-armed populations).
        ''' </summary>
        Public Shared Function FitSchema(train As List(Of FeatureBundle),
                                         includeAggrVel As Boolean) As FeatureSchema
            Dim s As New FeatureSchema()
            s.IncludeAggrVel = includeAggrVel

            ' Categorical levels — sample the SCORED categoricals off the first train bundle
            ' so the column order tracks the bundle's dictionary insertion order.
            If train.Count > 0 Then
                For Each key In train(0).ScoredCategoricals.Keys
                    s.ScoredCategoricalNames.Add(key)
                    Dim levels As New HashSet(Of String)(StringComparer.Ordinal)
                    For Each fb In train
                        Dim v As String = ""
                        fb.ScoredCategoricals.TryGetValue(key, v)
                        levels.Add(If(String.IsNullOrEmpty(v), "EMPTY", v))
                    Next
                    Dim ordered = levels.OrderBy(Function(l) l, StringComparer.Ordinal).ToList()
                    ' Always append an OTHER bucket so unseen test levels have a home.
                    If Not ordered.Contains("OTHER") Then ordered.Add("OTHER")
                    s.CategoricalLevels(key) = ordered
                Next
                For Each key In train(0).ScoredNumerics.Keys
                    s.ScoredNumericNames.Add(key)
                Next
            End If

            ' Add AggrVelSignal as a scored categorical when armed. Its levels are enumerated
            ' the same way.
            If includeAggrVel Then
                s.ScoredCategoricalNames.Add("AggrVelSignal")
                Dim levels As New HashSet(Of String)(StringComparer.Ordinal)
                For Each fb In train
                    levels.Add(If(String.IsNullOrEmpty(fb.AggrVelSignal), "EMPTY", fb.AggrVelSignal))
                Next
                Dim ordered = levels.OrderBy(Function(l) l, StringComparer.Ordinal).ToList()
                If Not ordered.Contains("OTHER") Then ordered.Add("OTHER")
                s.CategoricalLevels("AggrVelSignal") = ordered
                s.ScoredNumericNames.Add("AggrVelBurstRatio")
                s.ScoredNumericNames.Add("AggrVelNet")
            End If

            ' Regime + session-hour are declared here so the schema is complete for both
            ' fit and transform.
            s.ScoredCategoricalNames.Add("Regime")
            Dim regLevels As New HashSet(Of String)(StringComparer.Ordinal)
            For Each fb In train
                regLevels.Add(If(String.IsNullOrEmpty(fb.Regime), "EMPTY", fb.Regime))
            Next
            Dim regOrdered = regLevels.OrderBy(Function(l) l, StringComparer.Ordinal).ToList()
            If Not regOrdered.Contains("OTHER") Then regOrdered.Add("OTHER")
            s.CategoricalLevels("Regime") = regOrdered

            s.ScoredCategoricalNames.Add("SessionHour")
            Dim hourLevels As New HashSet(Of String)(StringComparer.Ordinal)
            For Each fb In train
                hourLevels.Add(fb.SessionHour.ToString())
            Next
            Dim hourOrdered = hourLevels.OrderBy(Function(l) l, StringComparer.Ordinal).ToList()
            If Not hourOrdered.Contains("OTHER") Then hourOrdered.Add("OTHER")
            s.CategoricalLevels("SessionHour") = hourOrdered

            ' Numeric stats on TRAIN only.
            For Each nm In s.ScoredNumericNames
                Dim vals As New List(Of Double)()
                For Each fb In train
                    Dim x As Double
                    If ExtractNumeric(fb, nm, x) AndAlso Not Double.IsNaN(x) AndAlso Not Double.IsInfinity(x) Then
                        vals.Add(x)
                    End If
                Next
                If vals.Count = 0 Then
                    s.NumericMean(nm) = 0
                    s.NumericStd(nm) = 1
                    s.NumericMedian(nm) = 0
                Else
                    Dim mean As Double = vals.Sum() / vals.Count
                    Dim var As Double = 0
                    For Each v In vals
                        var += (v - mean) * (v - mean)
                    Next
                    Dim std As Double = System.Math.Sqrt(var / System.Math.Max(1, vals.Count - 1))
                    If std < 1.0E-12 Then std = 1.0    ' guard degenerate columns
                    vals.Sort()
                    Dim median As Double = vals(vals.Count \ 2)
                    s.NumericMean(nm) = mean
                    s.NumericStd(nm) = std
                    s.NumericMedian(nm) = median
                End If
            Next

            ' Materialise the column list in a stable order:
            '   categoricals (in ScoredCategoricalNames order, minus one baseline level per
            '     categorical to avoid the dummy-variable trap for the intercept-free logistic)
            '   numerics (in ScoredNumericNames order) + paired _MISSING flags
            For Each cat In s.ScoredCategoricalNames
                Dim levels = s.CategoricalLevels(cat)
                ' Drop the first level as baseline; the L2 penalty is unaffected by which one
                ' we drop (the coefficients absorb the shift), but skipping avoids perfect
                ' multicollinearity with the intercept the fitter appends.
                For k = 1 To levels.Count - 1
                    s.Columns.Add(cat & "=" & levels(k))
                Next
            Next
            For Each nm In s.ScoredNumericNames
                s.Columns.Add(nm)
                s.Columns.Add(nm & "_MISSING")
            Next

            Return s
        End Function

        ''' <summary>Transform a bundle list under a fitted schema. Rows dropped upstream
        ''' (label = -1) are NOT included here — the caller filters first.</summary>
        Public Shared Function Transform(schema As FeatureSchema,
                                          bundles As List(Of FeatureBundle)) As Double(,)
            Dim nRows As Integer = bundles.Count
            Dim nCols As Integer = schema.Columns.Count
            Dim X(nRows - 1, nCols - 1) As Double
            Dim colIdx As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            For j = 0 To schema.Columns.Count - 1
                colIdx(schema.Columns(j)) = j
            Next

            For i = 0 To nRows - 1
                Dim fb = bundles(i)
                For Each cat In schema.ScoredCategoricalNames
                    Dim v As String = ExtractCategorical(fb, cat)
                    Dim vNorm As String = If(String.IsNullOrEmpty(v), "EMPTY", v)
                    Dim levels = schema.CategoricalLevels(cat)
                    Dim active As String = If(levels.Contains(vNorm), vNorm, "OTHER")
                    ' Skip if active is the dropped baseline level.
                    If levels.Count > 0 AndAlso active = levels(0) Then Continue For
                    Dim colName As String = cat & "=" & active
                    Dim cIdx As Integer
                    If colIdx.TryGetValue(colName, cIdx) Then X(i, cIdx) = 1.0
                Next
                For Each nm In schema.ScoredNumericNames
                    Dim xv As Double
                    Dim have As Boolean = ExtractNumeric(fb, nm, xv)
                    Dim missing As Boolean = (Not have) OrElse Double.IsNaN(xv) OrElse Double.IsInfinity(xv)
                    Dim value As Double = If(missing, schema.NumericMedian(nm), xv)
                    Dim z As Double = (value - schema.NumericMean(nm)) / schema.NumericStd(nm)
                    Dim cIdx1 As Integer
                    If colIdx.TryGetValue(nm, cIdx1) Then X(i, cIdx1) = z
                    Dim cIdx2 As Integer
                    If missing AndAlso colIdx.TryGetValue(nm & "_MISSING", cIdx2) Then X(i, cIdx2) = 1.0
                Next
            Next
            Return X
        End Function

        Private Shared Function ExtractCategorical(fb As FeatureBundle, name As String) As String
            If name = "Regime" Then Return fb.Regime
            If name = "SessionHour" Then Return fb.SessionHour.ToString()
            If name = "AggrVelSignal" Then Return fb.AggrVelSignal
            Dim s As String = ""
            fb.ScoredCategoricals.TryGetValue(name, s)
            Return s
        End Function

        Private Shared Function ExtractNumeric(fb As FeatureBundle, name As String, ByRef out As Double) As Boolean
            If name = "AggrVelBurstRatio" Then out = fb.AggrVelBurstRatio : Return True
            If name = "AggrVelNet" Then out = fb.AggrVelNet : Return True
            Dim v As Double
            If fb.ScoredNumerics.TryGetValue(name, v) Then
                out = v
                Return True
            End If
            out = Double.NaN
            Return False
        End Function

    End Class

End Namespace
