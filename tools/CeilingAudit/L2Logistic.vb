' tools/CeilingAudit/L2Logistic.vb
' Hand-rolled L2-regularised logistic regression: batch gradient descent, deterministic
' zero-init, InvariantCulture math throughout, no external ML dependency (§5 dep-free).
'
' Loss (per row):    L_i = -[y log σ(z) + (1-y) log (1-σ(z))]   with z = w·x + b
' Objective:         mean(L_i) + (lambda/2n) · ||w||^2   (bias not regularised — standard)
' Gradient:          ∂L/∂w_j = (1/n) Σ (σ(z_i) - y_i) x_ij   + (lambda/n) w_j
'                    ∂L/∂b   = (1/n) Σ (σ(z_i) - y_i)
'
' λ selection is a separate stage (LambdaTuner in AuditMetrics.vb) that fits this fitter
' repeatedly against internal train-block splits and picks the AUC-maximising λ. This file
' knows nothing about walk-forward or bootstrap — those live one layer up so the fixture
' set can pin them separately.

Imports System.Collections.Generic
Imports System.Linq

Namespace CeilingAudit

    Public Class LogisticModel
        Public Property Weights As Double()
        Public Property Bias As Double
        Public Property Lambda As Double
        Public Property Iterations As Integer
        Public Property FinalLoss As Double
        Public Property LossTrace As New List(Of Double)()    ' one entry per epoch — A39a monotonicity check reads it

        Public Function Predict(x As Double()) As Double
            Dim z As Double = Bias
            For j = 0 To x.Length - 1
                z += Weights(j) * x(j)
            Next
            Return Sigmoid(z)
        End Function

        Public Function PredictAll(X As Double(,)) As Double()
            Dim n As Integer = X.GetLength(0)
            Dim d As Integer = X.GetLength(1)
            Dim p(n - 1) As Double
            For i = 0 To n - 1
                Dim z As Double = Bias
                For j = 0 To d - 1
                    z += Weights(j) * X(i, j)
                Next
                p(i) = Sigmoid(z)
            Next
            Return p
        End Function

        Public Shared Function Sigmoid(z As Double) As Double
            ' Numerically stable — the naive form overflows for |z| ~ 700.
            If z >= 0 Then
                Dim ez As Double = System.Math.Exp(-z)
                Return 1.0 / (1.0 + ez)
            Else
                Dim ez As Double = System.Math.Exp(z)
                Return ez / (1.0 + ez)
            End If
        End Function

    End Class

    Public Class L2Logistic

        ''' <summary>Fit an L2-regularised logistic regression via batch gradient descent.
        ''' Deterministic (all-zero init, fixed epoch count, no random shuffling — batch GD
        ''' has no stochastic component). Learning rate uses a simple diminishing schedule
        ''' (lr / (1 + decay·epoch)) so the last epochs step conservatively toward the
        ''' train-loss floor.</summary>
        Public Shared Function Fit(X As Double(,), y As Integer(),
                                    lambda As Double,
                                    Optional lr As Double = 0.5,
                                    Optional epochs As Integer = 500,
                                    Optional decay As Double = 0.01) As LogisticModel
            Dim n As Integer = X.GetLength(0)
            Dim d As Integer = X.GetLength(1)
            Dim w(d - 1) As Double
            Dim b As Double = 0.0
            Dim m As New LogisticModel() With {.Weights = w, .Bias = b, .Lambda = lambda, .Iterations = epochs}
            If n = 0 OrElse d = 0 Then Return m

            Dim probs(n - 1) As Double
            For ep = 0 To epochs - 1
                ' Forward pass + loss + gradient accumulator.
                Dim gW(d - 1) As Double
                Dim gB As Double = 0.0
                Dim loss As Double = 0.0
                For i = 0 To n - 1
                    Dim z As Double = b
                    For j = 0 To d - 1
                        z += w(j) * X(i, j)
                    Next
                    Dim p As Double = LogisticModel.Sigmoid(z)
                    probs(i) = p
                    Dim yi As Integer = y(i)
                    ' Clamp for numerical safety inside the log — mirrors what standard
                    ' logistic-fitter implementations do.
                    Dim pc As Double = System.Math.Min(1.0 - 1.0E-12, System.Math.Max(1.0E-12, p))
                    loss += -(yi * System.Math.Log(pc) + (1 - yi) * System.Math.Log(1.0 - pc))
                    Dim err As Double = p - yi
                    For j = 0 To d - 1
                        gW(j) += err * X(i, j)
                    Next
                    gB += err
                Next
                loss /= n
                ' Add L2 penalty term to the reported loss (matches the objective).
                Dim wSq As Double = 0.0
                For j = 0 To d - 1
                    wSq += w(j) * w(j)
                Next
                loss += (lambda / (2.0 * n)) * wSq
                m.LossTrace.Add(loss)

                ' Descend.
                Dim eta As Double = lr / (1.0 + decay * ep)
                For j = 0 To d - 1
                    Dim grad As Double = gW(j) / n + (lambda / n) * w(j)
                    w(j) -= eta * grad
                Next
                b -= eta * (gB / n)
            Next

            m.Weights = w
            m.Bias = b
            m.FinalLoss = If(m.LossTrace.Count > 0, m.LossTrace.Last(), 0.0)
            Return m
        End Function

    End Class

End Namespace
