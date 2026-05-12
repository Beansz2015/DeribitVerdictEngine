' tools/AutoTweaker/CompositeScorer.vb
' Single-purpose helper for the snapshot composite score used by the bucket
' rotation rule in settings-snapshot-history-proposal.md §3h.
'
' Formula (spec §3h, verbatim):
'   StreakLengthClamped = Math.Min(StreakLength, config.StreakLengthClamp)
'   Score = (100.0 - AvgFailureRatePct) + (StreakLengthClamped × config.StreakWeight)
'
' Failure rate dominates (1 point per %); streak adds a secondary nudge
' (StreakWeight per round, capped by StreakLengthClamp).
'
' Worked examples (StreakWeight=1.5, StreakLengthClamp=20):
'   A: 25% fail, 5  streak           → 75 + 5×1.5  = 82.5
'   B: 35% fail, 10 streak           → 65 + 10×1.5 = 80.0
'   C: 20% fail, 8  streak           → 80 + 8×1.5  = 92.0
'   D: 15% fail, 3  streak           → 85 + 3×1.5  = 89.5
'   E: 40% fail, 30 streak (→ 20)    → 60 + 20×1.5 = 90.0
'
' Identical formula must appear in PromptBuilder.vb's system message and
' UserManual.md §20e — cross-file invariant.
'
' Host-agnostic: no System.Windows.Forms references.

Public Class CompositeScorer

    Public Shared Function Score(avgFailureRatePct As Double,
                                  streakLength As Integer,
                                  streakWeight As Double,
                                  streakLengthClamp As Integer) As Double
        Dim clamped As Integer = Math.Min(streakLength, streakLengthClamp)
        Return (100.0 - avgFailureRatePct) + clamped * streakWeight
    End Function

End Class
