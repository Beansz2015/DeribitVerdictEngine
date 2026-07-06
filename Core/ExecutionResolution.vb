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
'   - ResolveRocSlopeDelta applies the resolution_profiles override map (a shared 3-min
'     value across Asia/London). ResolveRocMagnitudeForHour adds a per-session override on
'     top: Asia/London 3-min ROC *levels* diverge (Asia ~1.8× hotter), so magnitude is
'     per-session (session_volume.sessions[].roc_magnitude_threshold) while slope stays
'     shared. CVD/MicroCVD read the fixed 500/50-trade stream, resolution-independent (§1).
'     Re-baseline: docs/asia-london-roc-rebaseline-proposal.md.

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
    ''' [B re-baseline] Per-session ROC magnitude threshold for the given UTC hour.
    ''' Checks the session bucket's roc_magnitude_threshold override first (ASIA 0.17 /
    ''' LONDON 0.11); falls back to the resolution_profiles → global base chain via the
    ''' execRes-keyed ResolveRocMagnitude. NY (no bucket override) resolves to the base
    ''' 1-min value — byte-identical. The single resolution-aware magnitude entry point for
    ''' the live engine (stamped onto IndicatorResults.RocMagnitudeThreshold at run time).
    ''' </summary>
    Public Shared Function ResolveRocMagnitudeForHour(cfg As EngineSettings, utcHour As Integer) As Double
        Dim b = MatchSessionBucket(cfg, utcHour)
        If b IsNot Nothing AndAlso b.RocMagnitudeThreshold.HasValue Then Return b.RocMagnitudeThreshold.Value
        Return ResolveRocMagnitude(cfg, ResolveResolution(cfg, utcHour))
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

    ''' <summary>
    ''' [P4 #5 aggressor velocity] Per-session norm-horizon window (seconds) for the given
    ''' UTC hour (docs/aggressor-velocity-proposal.md §6). Session override (NY 60 — dense
    ''' 1-min tape) → shared default (120). Mirrors the v40 per-session ROC override chain:
    ''' nullable per-session value on a shared fallback, hand-tuned tier (HC11 class).
    ''' </summary>
    Public Shared Function ResolveAggrVelNormWindow(cfg As EngineSettings, utcHour As Integer) As Double
        Dim o = AggrVelSessionOverrideFor(cfg, utcHour)
        If o IsNot Nothing AndAlso o.NormWindowSec.HasValue Then Return o.NormWindowSec.Value
        Return cfg.Indicators.AggressorVelocity.Defaults.NormWindowSec
    End Function

    ''' <summary>
    ''' [P4 #5 aggressor velocity] Per-session burst-ratio threshold for the given UTC
    ''' hour. Session override → shared default (2.5). Same inheritance contract as
    ''' ResolveAggrVelNormWindow; the §5.2 per-session re-baseline sets the overrides.
    ''' </summary>
    Public Shared Function ResolveAggrVelBurstThreshold(cfg As EngineSettings, utcHour As Integer) As Double
        Dim o = AggrVelSessionOverrideFor(cfg, utcHour)
        If o IsNot Nothing AndAlso o.BurstRatioThreshold.HasValue Then Return o.BurstRatioThreshold.Value
        Return cfg.Indicators.AggressorVelocity.Defaults.BurstRatioThreshold
    End Function

    ''' <summary>
    ''' [placed-geometry B4b] The fallback-target ATR multiplier in effect for the given
    ''' UTC hour. Session override (structural_levels.sessions{}, matched by bucket name —
    ''' DG3: LONDON 2.0 / ASIA 1.25) → the global scoring.atr_target_multiplier. The
    ''' override tier is a structural_levels feature: when structural_levels.enabled=false
    ''' (legacy geometry) this returns the global multiplier unconditionally, so legacy
    ''' callers can consume it without a guard. utcHour = -1 (unstamped fixture/replay)
    ''' matches no bucket → global. Same inheritance contract as ResolveAggrVelNormWindow.
    ''' </summary>
    Public Shared Function ResolveFallbackTargetMultiplier(cfg As EngineSettings, utcHour As Integer) As Double
        If cfg Is Nothing Then Return 0
        Dim baseMult As Double = cfg.Scoring.AtrTargetMultiplier
        Dim sl = cfg.Scoring.StructuralLevels
        If sl Is Nothing OrElse Not sl.Enabled OrElse sl.Sessions Is Nothing Then Return baseMult
        Dim b = MatchSessionBucket(cfg, utcHour)
        If b Is Nothing OrElse String.IsNullOrEmpty(b.Name) Then Return baseMult
        For Each kv In sl.Sessions
            If String.Equals(kv.Key, b.Name, StringComparison.OrdinalIgnoreCase) Then
                If kv.Value IsNot Nothing AndAlso kv.Value.FallbackTargetAtrMult.HasValue Then
                    Return kv.Value.FallbackTargetAtrMult.Value
                End If
                Return baseMult
            End If
        Next
        Return baseMult
    End Function

    ''' <summary>The aggressor_velocity.sessions{} override for the UTC hour's session
    ''' bucket (matched by bucket NAME, case-insensitive), or Nothing when there is no
    ''' matching bucket / no override entry.</summary>
    Private Shared Function AggrVelSessionOverrideFor(cfg As EngineSettings, utcHour As Integer) As AggressorVelocitySessionOverride
        If cfg Is Nothing OrElse cfg.Indicators Is Nothing OrElse
           cfg.Indicators.AggressorVelocity Is Nothing Then Return Nothing
        Dim b = MatchSessionBucket(cfg, utcHour)
        If b Is Nothing OrElse String.IsNullOrEmpty(b.Name) Then Return Nothing
        Dim sessions = cfg.Indicators.AggressorVelocity.Sessions
        If sessions Is Nothing Then Return Nothing
        For Each kv In sessions
            If String.Equals(kv.Key, b.Name, StringComparison.OrdinalIgnoreCase) Then Return kv.Value
        Next
        Return Nothing
    End Function

    ''' <summary>Resolution_profiles lookup keyed by the resolution as a string. Nothing if absent.</summary>
    Private Shared Function ProfileFor(cfg As EngineSettings, execRes As Integer) As ResolutionProfile
        If cfg Is Nothing OrElse cfg.ResolutionProfiles Is Nothing Then Return Nothing
        Dim p As ResolutionProfile = Nothing
        cfg.ResolutionProfiles.TryGetValue(execRes.ToString(), p)
        Return p
    End Function

End Class
