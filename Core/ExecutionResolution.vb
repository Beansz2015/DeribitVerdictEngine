' Core/ExecutionResolution.vb
' Session-conditional execution-resolution resolver (v36 Phase 1).
'
' Host-agnostic — NO System.Windows.Forms, no MainForm coupling, no singleton
' reads inside the pure resolvers (cfg is passed in). Lives in Core/ so the
' Linux CLI port can reuse it unchanged.
'
' The engine reads a per-session execution resolution (1/3/5 min) from
' settings.json session_volume.sessions[].execution_resolution. ASIA/LONDON run
' on 3-min, NY stays 1-min. The 5-min regime (DMI/ADX) and 15-min MTF gate are
' UNCHANGED — they are the valid higher-timeframe layer above a 3-min chart.
'
' Spec: docs/session-timeframe-resolution-implementer-handoff.md §3.
'
' Design notes:
'   - MatchSessionBucket is the ONE definition of "which session is this UTC hour".
'     DynamicNorms.ApplySessionVolume and UI/MainForm_Render_Cards.ResolveSessionLabel
'     both route through it (DRY; the resolution / volume-scaling / display-label
'     boundaries can never drift). This is the fix for the v34 hour-7 off-by-one,
'     where the display label used "<7" while the engine bucket is ASIA 0-7 inclusive.
'   - MatchSessionBucket does NOT consult SessionVolume.Enabled. That flag gates the
'     VOLUME MULTIPLIER only; resolution selection is independent (disabling session
'     volume scaling must NOT silently revert Asia/London to 1-min — A14i).
'   - ResolveRocMagnitude / ResolveRocSlopeDelta apply the resolution_profiles
'     override map. Only the two ROC keys scale on 3-min (×2.1 seed); CVD/MicroCVD
'     read the fixed 500/50-trade stream and are resolution-independent (§1).

Public Class ExecutionResolution

    ''' <summary>
    ''' The single bucket-matcher — returns the first session bucket whose
    ''' [StartHour..EndHour] (inclusive) contains utcHour, or Nothing if none match.
    ''' Does NOT consult SessionVolume.Enabled (resolution is independent of the
    ''' volume-multiplier switch).
    ''' </summary>
    Public Shared Function MatchSessionBucket(cfg As EngineSettings, utcHour As Integer) As SessionBucketSettings
        If cfg Is Nothing Then Return Nothing
        Dim sv = cfg.SessionVolume
        If sv Is Nothing OrElse sv.Sessions Is Nothing Then Return Nothing
        For Each b In sv.Sessions
            If b IsNot Nothing AndAlso utcHour >= b.StartHour AndAlso utcHour <= b.EndHour Then Return b
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' The active execution resolution (minutes) for the given UTC hour.
    ''' Defaults to 1 when no bucket matches or the bucket's resolution is unset/&lt;=0
    ''' (absent ⇒ current 1-min behaviour, zero change). Independent of
    ''' SessionVolume.Enabled by design.
    ''' </summary>
    Public Shared Function ResolveResolution(cfg As EngineSettings, utcHour As Integer) As Integer
        Dim b = MatchSessionBucket(cfg, utcHour)
        Return If(b Is Nothing OrElse b.ExecutionResolution <= 0, 1, b.ExecutionResolution)
    End Function

    ''' <summary>
    ''' ROC magnitude threshold for the given execution resolution. Reads the
    ''' resolution_profiles override map; falls back to the global 1-min value when
    ''' the resolution has no profile or the key is unset (nullable ⇒ inherit global).
    ''' </summary>
    Public Shared Function ResolveRocMagnitude(cfg As EngineSettings, execRes As Integer) As Double
        Dim p As ResolutionProfile = ProfileFor(cfg, execRes)
        Return If(p IsNot Nothing AndAlso p.RocMagnitudeThreshold.HasValue,
                  p.RocMagnitudeThreshold.Value, cfg.Indicators.ROC.MagnitudeThreshold)
    End Function

    ''' <summary>
    ''' ROC slope-delta threshold for the given execution resolution. Same
    ''' inheritance contract as ResolveRocMagnitude.
    ''' </summary>
    Public Shared Function ResolveRocSlopeDelta(cfg As EngineSettings, execRes As Integer) As Double
        Dim p As ResolutionProfile = ProfileFor(cfg, execRes)
        Return If(p IsNot Nothing AndAlso p.RocSlopeDeltaThreshold.HasValue,
                  p.RocSlopeDeltaThreshold.Value, cfg.Indicators.ROC.SlopeDeltaThreshold)
    End Function

    ''' <summary>Resolution_profiles lookup keyed by the resolution as a string. Nothing if absent.</summary>
    Private Shared Function ProfileFor(cfg As EngineSettings, execRes As Integer) As ResolutionProfile
        If cfg Is Nothing OrElse cfg.ResolutionProfiles Is Nothing Then Return Nothing
        Dim p As ResolutionProfile = Nothing
        cfg.ResolutionProfiles.TryGetValue(execRes.ToString(), p)
        Return p
    End Function

End Class
