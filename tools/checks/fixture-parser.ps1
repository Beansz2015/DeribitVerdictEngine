#requires -Version 5.1
<#
  tools/checks/fixture-parser.ps1 -- fixture-literal provenance (FP-1) and fixture-name-
  vs-assertion (FP-2) audit over verify/ordercheck/Program.vb.

  ADVISORY ONLY (docs/fixture-parser-check-spec.md top matter -- "NOT a gate. Advisory,
  same three reasons as docs/rider-travel-check-spec.md D-3"). Not wired into
  verify-gate.ps1 or the pre-push hook.

  WHY: CLAUDE.md's fixture-literal provenance rule (RULED 2026-08-11) says a fixture
  passing a settings-derived threshold as a literal must declare, in a comment, whether it
  is SHIPPED BEHAVIOUR (must derive from cfg) or MECHANISM (a literal is fine, and the
  comment must say why) -- and says plainly "no tool can tell the two apart". This harness
  splits the problem per docs/fixture-parser-check-spec.md section 1: CODE computes
  whether a literal equals any EVER-SHIPPED value of its matched settings.json key
  (deterministic); Jev judges only whether the comment declares the right class for what
  the code found (semantic). Jev is never asked to do the arithmetic or the set-membership
  check itself -- see the line marked "CODE COMPUTES SET MEMBERSHIP HERE" below.

  ============================== REVISION 1 (2026-09-21/22) ==============================
  docs/fixture-parser-check-spec.md section 7 -- three trader rulings, implemented here:

    FP-Q3 (derived mapping, section 7.2/7.3): the parameter->cfg-path mapping used to be
    guessed by camelCase-to-snake_case NAME matching against HEAD's settings.json shape
    (FP-D2, section 3). That guess is now SECONDARY/informational only (still computed,
    still feeds MATCHED_ONE_KEY/ZERO/MULTI below, and feeds the FP-Q1 Jev call as a
    name-similarity hint) -- the AUTHORITATIVE mapping is now DERIVED from the fixture's
    call's PRODUCTION call site(s) under Core/, UI/, analysis/ and the root .vb files:
    read what cfg path production actually passes for that parameter, resolving three
    mechanical shapes (NAMED direct, NAMED-via-one-hop-ALIAS, POSITIONAL-via-signature-
    read). A method with no production call site gets its own reported class, never a
    guessed mapping (section 7.3's closing warning).

    FP-Q1 (scope filter, section 7.2): CLAUDE.md's provenance rule governs a
    "settings-derived THRESHOLD", not every named-argument literal -- most of this file's
    118 literal passes are fixture INPUTS (a BTC price, a learning rate, an epoch count, a
    clock value), never candidates. A parameter with an FP-Q3-derived cfg path is in scope
    by construction (code, no Jev needed). A parameter WITHOUT one is not automatically
    out of scope either -- name-matching alone both over-matches (`atr:=20` hits the ATR
    settings block on a fuzzy name match but is a fixture input to a LOCAL fixture helper,
    BuildGateIndicators, that has no production call site at all) and under-matches. So
    Jev is asked, once per DISTINCT unmapped PARAMETER NAME (never per call site, never
    5x-sampled -- see FP-D14 below), "is this a settings-derived threshold or a fixture
    input?", and ONLY THE COUNT is ever read by this build or printed to console/report.
    No per-parameter scope answer is ever surfaced as a "verdict" for any parameter.

    FP-Q2 (comment extent, section 7.2): the ORIGINAL comment-block extraction
    (Get-CommentBlockOld below) walked backward from a statement's start line collecting
    CONTIGUOUS comment lines directly above it -- so a comment block attached to only the
    IMMEDIATELY FOLLOWING statement. Ruled (b): a comment block covers every statement down
    to the NEXT BLANK LINE. This silently dropped A6_ObvNormalisation's SECOND CalcOBV
    call (verify/ordercheck/Program.vb, two calls back-to-back, no blank line between them,
    one MECHANISM comment above the first) from the provenance-commented population --
    fixed via a forward coverage-map pass (Get-CommentBlockNew's backing $commentCoverage
    array), built once, looked up by statement-start line. Both old and new results are
    kept per call site so the before/after site counts can be printed and diffed.

  FP-D10 (this build, FP-Q2's mechanism): the coverage map is built by a single forward
  pass over raw lines: a blank line resets the "active" comment block to empty; a
  comment-only line accumulates into a pending buffer; a code line flushes any pending
  buffer into "active" (replacing whatever was active), then records "active" as that
  line's coverage. This lets a fresh comment block between two statements (no blank line
  between the comments and the SECOND statement) correctly override an earlier "active"
  block for statements from that point on, while extending an unchanged "active" block
  across any run of blank-line-free statements that has no comment of its own. KNOWN
  LIMITATION, not fixed (none of the target sites need it): a full-line comment sitting
  INSIDE a multi-line statement's unclosed-paren continuation would be misread as a
  free-standing comment line by this pass, since it does not track paren depth. This
  codebase's own comment style (comment block sits directly above the code it describes,
  never inside an argument list) means this has not been observed in the fixture file.

  FP-D11 (this build, WHERE FP-Q1's Jev calls sit in the pipeline, and why this is a
  deliberate, narrow exception to "structural, before any API key read"): the harness's
  candidate list (FP1_CANDIDATES/FP2_CANDIDATES) has always printed BEFORE the baseline
  gate (docs/harness-shadow-mode-protocol.md section 2 step 2 -- "the harness prints its
  candidate list first, with no judgments"). FP-Q1's scope filter now DECIDES that
  candidate list ("only the thresholds join the residual", section 7.4 item 2), so its Jev
  calls must also run before the baseline gate for the printed candidate list (and the
  IN_SCOPE_SITES/OUT_OF_SCOPE_SITES coverage counters) to be real numbers rather than
  placeholders. This means TYPESAFE_API_KEY is now read (and, if present, spent -- a
  bounded number of calls, ONE per distinct unmapped parameter name, unsampled) BEFORE the
  baseline-missing exit, which is a real, material change from the original design's "no
  API call when baseline missing" (docs/fixture-parser-check-spec.md section 5 acceptance
  item 4, pre-revision). If TYPESAFE_API_KEY is absent, this step degrades gracefully
  (every unmapped parameter is left SCOPE_PENDING / excluded from IN_SCOPE, a dedicated
  counter says how many, and the tool still reaches the baseline gate normally) rather than
  hard-failing -- the tool remains usable fully offline for structural checks. The
  FP-1/FP-2 JUDGED path (comment-class / name-assertion verdicts) is UNCHANGED: it is still
  gated on the baseline file, still refuses before running, and this build's own live
  acceptance run stops at that refusal without ever calling FP-1/FP-2 (docs/fixture-
  parser-check-spec.md section 7.5 item 7 / docs/harness-shadow-mode-protocol.md section
  4c: the seat writes the baseline, never the implementer).

  FP-D12 (this build, alias resolution depth): FP-Q3's ALIASED shape resolves exactly ONE
  hop -- `Dim localAlias = cfg.X.Y` found by a single backward-forward scan of the
  production call's enclosing Sub/Function body. If the alias itself is not a direct `= cfg.
  ...` assignment (e.g. it resolves to ANOTHER local, or is computed, or is not found in
  that body), this build does NOT chase further and does NOT guess -- the parameter is
  reported class NOT_CFG_SOURCED / mapping unresolved, same treatment as a genuine
  non-cfg-sourced parameter. Matches the build brief's escalation trigger: "resolving the
  positional/alias forms needs anything beyond reading the signature and one alias hop"
  is the stop-and-report line, not a build target.

  FP-D13 (this build, multiple production call sites): if a callee has more than one
  production call site, the FIRST one found (file list order, then line order) is used.
  Disagreement between multiple production call sites on the same parameter's cfg path is
  not detected or reconciled -- not observed in the population this build measured, and
  out of scope per FP-D12's same reasoning (not "reading the signature and one alias hop").

  FP-D14 (this build, self-consistency scope): FP-D4's 5-sample self-consistency mandate
  (docs/harness-shadow-mode-protocol.md section 4b) is read as scoped to the two NAMED
  harness verdict questions, FP-1's `verdict` and FP-2's `verdict` -- unchanged from the
  pre-revision build's FP-D7 note, which already scoped it away from auxiliary
  resolution/classification calls. FP-Q1's scope classification is exactly such an
  auxiliary call (it decides population MEMBERSHIP, never itself a verdict CODE or a
  report reads per-item) and runs UNSAMPLED, once per distinct parameter name, following
  the pre-revision FP-D7 key-resolution call's own precedent.
  [SUPERSEDED for FP-Q1 by FP-D23, revision 3: an unsampled detector has no stability
  evidence, and FP-Q1 was measured flipping -- docs/harness-shadow-mode-protocol.md 4f.]

  ============================ REVISION 2 (2026-09-22 UTC) ===============================
  docs/fixture-parser-check-spec.md section 8.2/8.4 items 8a, 8b, 8c. The FP-Q1 measured
  run (docs/harness-runs/fixture-parser-scope-run-2026-09-22.md) found the detector
  UNDER-scoping 6 of its 7 settings-derived thresholds, and every one of the six had a
  MECHANICALLY DERIVABLE cfg path this code failed to look for. The fix is enumeration,
  never the question: once FP-Q3 derives the path, the parameter is in scope BY
  CONSTRUCTION and FP-Q1 is never consulted for it.

  FP-D15 (item 8c, the SECOND baseline refusal): -ScopeBaselinePath. FP-Q1 is a detector
  and docs/harness-shadow-mode-protocol.md section 2 step 1 makes the operator-written
  baseline STRUCTURAL, "not a convention" -- but FP-D11 (below) deliberately puts FP-Q1's
  calls BEFORE the FP-1 baseline gate so the coverage counters are real numbers. Both hold
  at once because the refusal is scoped to the CALL, not to the run: with no scope
  baseline FP-Q1 makes ZERO Jev calls and degrades exactly as the no-API-key path already
  did (every unmapped parameter out of scope, a dedicated counter says how many), the
  coverage block still prints first with real, CODE-ONLY numbers, and the run continues to
  the FP-1 gate. Nothing is spent and no scope answer can be seen before the operator has
  written their own read. ⚠ Residual tension, named rather than hidden: without a scope
  baseline IN_SCOPE_* are code-derived only. SCOPE_BASELINE_STATE in the coverage block
  says which of the two a given run produced, so the number is never silently ambiguous.

  FP-D16 (item 8a gap A, the FIXTURE-LOCAL cfg builder): when a callee has NO production
  call site, look for its declaration in -SourceFile and scan that body for the assignment
  shape `<local>.<path> = <paramName>` (BuildA8Cfg: `cfg.Scoring.FundingHighBoost =
  fundingBoost`; BuildBurstCfg: `cfg.Indicators.AggressorVelocity.UpgradeBonus =
  upgradeBonus`). The path after the local's own identifier is normalised and must hit a
  REAL settings leaf or the shape is refused -- membership does the validating, so a
  coincidental `r.Foo = param` cannot invent a mapping. Fires ONLY on
  NO_PRODUCTION_CALL_SITE, so it can never pre-empt real production evidence.

  FP-D17 (item 8a gap B, the ONE-HOP FORWARDING WRAPPER): the fixture calls the inner
  method (AggressorVelocityAccumulator.Fold / .Snapshot); production calls a wrapper
  (MarketState.FoldAggressorVelocity / .GetAggressorVelocity) that forwards. The inner
  name's own production call sites are then all INSIDE wrappers and resolve
  NOT_CFG_SOURCED. So, only after EVERY direct site has failed, a site is treated as a
  forwarding hop iff it is a PURE POSITIONAL PASS-THROUGH: argument count equals the
  inner signature's, every argument is a bare identifier equal to the inner parameter name
  at that position, and every one of those names is a parameter of the enclosing method.
  ⛔ That triple test is not pedantry, it is the whole safety of the hop -- `Snapshot` and
  `Fold` are declared on FOUR different classes in this tree and this parser matches
  callees BY NAME, so MarketState.GetOfiAverage's `_ofiAcc.Snapshot(minCoverageSec)` is a
  call to "Snapshot" whose sole argument is a bare parameter name. Without the test it
  would hop and map the aggressor-velocity floor onto an OFI key: a confidently wrong
  answer, the failure mode section 6 names. Arity (1 vs 2) and name equality both reject
  it. ONE hop only; a renaming or reordering wrapper is reported unresolved, never guessed.

  FP-D18 (item 8a, the RESOLVER-RETURN argument shape): needed by tauNormSec and
  minCoverageSec, whose production argument is a local assigned from
  ExecutionResolution.ResolveAggrVelNormWindow(cfg, utcHour) -- a cfg path behind a
  session resolver, which neither the direct read nor the one alias hop can see. When the
  argument's base identifier is a local `Dim x = SomeMethod(...)`, that method's body is
  scanned for cfg-rooted Return expressions; the shape resolves ONLY when there is EXACTLY
  ONE distinct one. ⚠ KNOWN UNDER-STATEMENT, recorded not hidden: ResolveAggrVelNormWindow
  also returns a per-session override through a second hop, so the derived key is the
  DEFAULT arm (indicators.aggressor_velocity.default.norm_window_sec, ever-shipped {120})
  and the NY override key (norm_window_sec 60) is NOT in the derived ever-shipped set. A
  literal of 60 at such a site would therefore read as never-shipped. That is a smaller
  hole than the parameter being invisible altogether, but it IS a hole.

  FP-D19 (item 8a, POCO->JSON name aliases): per-segment snake-casing cannot map
  cfg.Indicators.AggressorVelocity.Defaults.NormWindowSec onto
  indicators.aggressor_velocity.default.norm_window_sec -- the POCO property is `Defaults`,
  the JSON key is `default`. The alias map is read from EngineSettings.vb's own
  <JsonPropertyName> attributes, never hand-kept (section 7.3's "a hand-kept table would
  be a fourth copy that drifts"); ten properties diverge that way and none carries two
  different JSON names (measured 2026-09-22). The plain snake path is always tried FIRST,
  so no mapping that resolved before revision 2 can change.

  FP-D20 (item 8b, splitting NOT_CFG_SOURCED): that class used to be emitted whenever the
  SEARCH failed, and it reads as a finding -- "this parameter is not settings-derived" --
  which is an assertion the code had not earned (that is how grossFloorUsdPerSec was
  written off). Now: NOT_CFG_SOURCED means SEARCHED AND NEGATIVE (the production argument,
  or the local it names, is a terminal literal / New / boolean -- it provably does not come
  from cfg). SOURCE_NOT_TRACEABLE means THE SEARCH DID NOT COMPLETE (the identifier is a
  parameter, a class field, a Const, a member of some other object, or a compound
  expression this parser will not reduce). Absence of evidence, never evidence of absence.

  ============================== REVISION 3 (2026-09-22 UTC) ==============================
  docs/fixture-parser-check-spec.md section 8.4 items 8f, 8i, 8j. From the seat's 8a
  re-measure, docs/harness-runs/fixture-parser-a23a-run-2026-09-22.md.

  FP-D21 (item 8f, closes 8i: the CALLEE'S OWN DEFAULT-FALLBACK BODY). Two parts, both
  fallbacks that run only after every earlier shape has failed:
    (1) A callee with no Sub/Function of its name may be a CLASS built with `New X(...)`.
        Its signature is read from the class's single non-Shared `Sub New`. An overloaded
        constructor is refused, never guessed.
    (2) When a production call site OMITS the argument, production runs on whatever the
        callee substitutes for the sentinel default. The callee body is scanned for
        `If(p <cond>, p, <cfgExpr>)`, its inverse, or `If p <cond> Then p = <cfgExpr>`,
        with <cfgExpr> rooted at `cfg.` or `SettingsLoader.Current.`. Resolves only on
        exactly one distinct expression, and only for a constructor or a method name
        declared ONCE in the production set (the FP-D17 same-name hazard).
  Worked instance: `New WsMarketDataSource(..., staleAfterSec:=10)`. Production
  (UI/MainForm_Layout.vb) omits it and WsMarketDataSource.vb:37 falls back to
  SettingsLoader.Current.Network.WsStaleAfterSec -> network.ws_stale_after_sec. MEASURED:
  that one parameter changes class and nothing else in the no-key output. MUTATION: a
  literal fallback (`..., 10)`) makes the shape refuse. Closes 8i because a site with a
  derived key is in scope BY CONSTRUCTION and never reaches FP-Q1, so a declaration
  written on it can no longer push it out of scope.

  FP-D22 (item 8j: in-scope sites with NO marker). The residual is marked AND in scope,
  so an in-scope site with no MECHANISM/SHIPPED keyword is never judged. Before this,
  nothing counted them and a run exited 0 while they sat there. Now
  IN_SCOPE_UNMARKED_SITES and IN_SCOPE_UNMARKED_EQUALS_EVER_SHIPPED print in the coverage
  block, and the second is listed by site. Code facts only -- keyword present or not,
  literal in the ever-shipped set or not -- so nothing here is a judgment and nothing is
  sent to Jev.

  FP-D23 (item 8h: SAMPLE FP-Q1; supersedes FP-D14 for FP-Q1). FP-Q1 answered ttlSeconds
  differently on runs with identical state, and ran once per parameter, so nothing could
  see it. It now draws $Samples times per parameter, each with a fresh sample_uid, and
  prints SCOPE_UNSTABLE_PARAMS (the draws split). A parameter is IN SCOPE if ANY draw says
  `threshold`. Why lean to over-scoping: the measured failure is UNDER-scoping (1 of 7
  thresholds found, docs/harness-runs/fixture-parser-scope-run-2026-09-22.md), a majority
  vote would silently drop an unstable parameter on some runs -- the ttlSeconds shape --
  and an over-scoped parameter costs only extra FP-1 candidates, which the seat baselines.
  Per-parameter answers still do not print (counts only), except the five named proofs.

  FP-D24 (item-level baseline refusal). The FP-1 gate was file-level: any baseline file
  let every residual item through. Because FP-Q1 runs earlier in the same process and is
  nondeterministic, the residual can gain a marked site between the seat's no-key listing
  and the keyed run -- and that site would be judged with no seat line, spent for ever.
  Now any residual item absent from the baseline stops the run (EXIT_REASON=
  BASELINE_INCOMPLETE, exit 2) before any FP-1/FP-2 call. -AllowUnbaselinedItems opts out
  for a routine re-run of items already measured once.

  FP-D25 (item 8m, trader-agreed 2026-09-22): shipped_declared_ok leaves the OK set for
  FP-1 and counts in FP1_BAD_VERDICTS, printed on its own as FP1_SHIPPED_ON_LITERAL. Every
  FP-1 site is a hardcoded literal, and CLAUDE.md forbids hardcoding SHIPPED BEHAVIOUR, so
  the verdict is a breach or a misread. Measured: the A23b run
  (docs/harness-runs/fixture-parser-a20-a23b-run-2026-09-22.md) exited 0 with two misreads.
  Only the SCORING changed; the Jev criteria text is untouched, so earlier measurements
  stay comparable.

  FP-D26 (2026-09-23 UTC, found by the first run on the 23 fundingBoost/upgradeBonus
  sites): the API sits behind a web firewall. A request whose TEXT matches an attack
  signature gets a 403 with an HTML block page. Measured: 1 of 37 items --
  A8_DominantSideCascade's comment phrase "and 4 + 7 = 11" reads as an SQL "AND x=y"
  tautology; swapping VB's leading apostrophe for REM did NOT clear it. Before this, the
  403 aborted the WHOLE run as API_FAILED on its first item. Now lib/InvokeJev.ps1 flags
  WafBlocked, the item is recorded as verdict WAF_BLOCKED (agreement NOT_JUDGED), the run
  continues, FP1_WAF_BLOCKED / FP2_WAF_BLOCKED print, and a blocked item exits 1 -- an
  unjudged item is never a pass. Rewording the fixture comment to dodge the firewall was
  rejected: it tunes the input, and the harness would still be blind to the next one.

  ============================== REVISION 4 (2026-09-23 UTC) ==============================
  docs/jev-harnesses-adversarial-review-2026-09-22.md findings 4, 8, 9, and
  docs/fixture-parser-check-spec.md section 8.4 item 8o. Close-list item 4 of the Jev
  programme. Batch record: docs/harness3-batch-spec-back.md.

  FP-D27 (finding 4: signatures by DECLARATION, not by bare name). Every production
  Sub/Function is indexed with its enclosing Class/Module/Structure/Interface. A name declared
  once resolves exactly as before. A name declared several times resolves only when the
  call's RECEIVER TYPE can be read from code -- a type-name qualifier, a local (`Dim x As
  [New] T`, `Dim x = New T`), a parameter, or a field of the enclosing type -- and exactly
  one declaration lives on that type. Otherwise the site is AMBIGUOUS_CALLEE: reported,
  counted (MAPPING_AMBIGUOUS_CALLEE_SITES, also inside MAPPING_OTHER_UNRESOLVED_SITES), never
  guessed. The same test filters a typed callee's PRODUCTION call sites (a site whose receiver
  types to another class, or cannot be typed, is not evidence); if none survive and some were
  untyped, the site is AMBIGUOUS_CALLEE too. An UNQUALIFIED call types to its enclosing class
  only if that class declares the name. The forwarding hop reads the wrapper's signature from
  its OWN declaration line. The resolver-return shape (FP-D18) finds its method the same way;
  Find-ProdProcRange (first body of that name) is gone. The declaration index uses a BROAD
  modifier set (Async, Overrides, Overloads...), because `Private Async Function
  RunAnalysisAsync` holds most production call sites; Get-FileEnclosingProc and the
  fixture's $ranges keep their narrower pre-revision form, because widening them could move
  EnclosingSub and so change item IDs. MEASURED on Program.vb: exactly 3 sites changed class
  -- A5#1042#currentATR PARAM_NOT_IN_SIGNATURE -> SOURCE_NOT_TRACEABLE (now read against
  DynamicNorms.Compute; production passes r.ATR), A19c#2531 and A19d#2551 nowUtcMs
  PARAM_NOT_IN_SIGNATURE -> PARAM_NOT_PASSED_AT_CALL_SITE (LiveMicrostructureEvaluator.
  Evaluate; production omits it). Fold, Snapshot and Build now resolve BY TYPE to the same
  declarations they hit by file order before. 0 AMBIGUOUS_CALLEE on the real file.

  FP-D28 (finding 8: the marker is block-level). A LABEL, never a filter. Each marked site
  is SITE_NAMED (the comment block names its parameter, whole word, case-insensitive) or
  BLOCK_ONLY (the block's keyword marks it, the block never names it). Counted over all
  marked sites and over the in-scope ones; in-scope BLOCK_ONLY sites are listed; the label
  rides FP1_CANDIDATES and FP1_RESULTS. Every marked site is judged exactly as before.
  Matched on the PARAMETER name only, not its settings key: the review's finding is about
  the parameter, and the strict rule reproduces its count (8 of 58) exactly.

  FP-D29 (finding 9: arrays). See Get-FlattenedSettings. Arrays of objects are walked by
  their `name` field (`session_volume.sessions[name=ASIA].high_multiplier`); an element with
  no usable name is ARRAY_UNKEYED, never an index. The settings cache carries a schema tag
  and a pre-revision-4 cache is discarded and re-walked. Array keys join the revision walk and
  FP-Q3's resolution index, NOT the FP-D2 name index or FP-Q1's fuzzy candidates: those feed
  FP-Q1's Jev state, and changing a measured detector's input is item 8o's comparability
  break. No real call site reaches an array key yet (a cfg expression indexes with `(`);
  ARRAY_PROOF shows the walk now holds one.

  FP-D30 (item 8o: FP-1 VERSION 2 BESIDE VERSION 1). A second choice question, verdict_v2,
  in the SAME call: the same instructions, criteria identical except for the probe's one
  sentence appended to mechanism_declared_ok and declared_but_contradicted -- verbatim, and
  in the two criteria the controlled probe put it in. Version 1 alone drives the verdict,
  the exit code and the baseline comparison. Version 2's plurality, agreement rate and
  samples print beside it, plus FP1V2_ITEMS_SAME_PLURALITY_AS_V1 / FP1V2_SAMPLES_SAME_AS_V1.
  This ARMS a re-measure on a fresh, baselined population; it measures nothing by itself.
  NOT VERIFIED: whether asking verdict_v2 in the same call moves version 1's answers.

  FP-D31 (seams). -TestTransportOverride forwards to every Invoke-Jev call (FP-Q1, FP-1,
  FP-2), the same seam harnesses 1 and 2 carry. -SiteDumpPath writes per-site CODE FACTS.
  -Fp2Only judges FP-2 names only (FP-1 sites neither judged nor gated), for the FP-D3
  mutation test. Offline proof of all of revision 4:
  tools/checks/selftest/fixture-parser-selftest.ps1 (no network, no key).
  ==========================================================================================

  ============================== REVISION 5 (2026-09-24 UTC) ==============================
  docs/jev-harnesses-second-reader-2026-09-24.md section 6 -- SR-D1 and SR-D6, both RULED
  YES by the trader 2026-09-24:

  FP-D32 (SR-D1: FP-Q1 item-level baseline gate, the FP-D24 pattern applied to the SECOND
  detector). Finding SR-4: the scope-baseline gate above (FP-D15) is FILE-level -- a file
  naming ONE needsJevParams candidate set $scopeBaselineOk=$true and let EVERY OTHER
  candidate reach Jev with no seat line. Now every needsJevParams item absent from a
  SUPPLIED, FOUND baseline file stops FP-Q1 cold: it takes the same no-Jev-call branch
  FP-D15's own missing-baseline case already used, and the run then exits 2
  (EXIT_REASON=BASELINE_INCOMPLETE, SCOPE_UNBASELINED_ITEMS) after Write-Coverage prints
  (section 4.2 step 5's "coverage prints on every exit path" invariant, unchanged).
  -AllowUnbaselinedItems opts out, shared with FP-1/FP-2's own gate (FP-D24) rather than a
  second flag -- SR-D1's own ruling. Unchanged: a $ScopeBaselinePath that is empty or not
  found still degrades to CODE-ONLY scope, non-fatally (FP-D15's original, deliberate
  design for "no baseline was ever supplied"; SR-D1 is scoped to a baseline that WAS
  supplied and found but does not cover every candidate).

  FP-D33 (SR-D6: FP-2t, a title-stripped FP-2 arm beside FP-2). The 2026-09-23/24 mutation
  runs (docs/harness-runs/fixture-parser-fp2-mutation-2026-09-23.md,
  fixture-parser-fp2-mutation-titles-2026-09-24.md) found FP-2 partly reads the fixture's
  own Check(...) title strings, not only the body: A23a flipped from a stable detection to
  a stable miss once its title stopped contradicting the wrong name. FP-2t asks the SAME
  question over the SAME Sub body, except every Check("...") title literal in that body is
  replaced with a neutral placeholder before the call, so Jev has only the code and
  comments to judge against the name. FP-2 alone still drives FP2_BAD_VERDICTS and the
  exit code, exactly as FP-1v2 (FP-D30) sits beside FP-1 without touching it. FP-2t's
  plurality, agreement and mean top probability print beside FP-2's, never combined.
  ==========================================================================================

  FP-D2 (retained, now informational only): the ORIGINAL camelCase-to-snake_case name
  matching against HEAD's settings.json shape. No longer authoritative for ResolvedKey
  (FP-Q3 is), but still computed and still feeds MATCHED_ONE_KEY/ZERO/MULTI (unchanged
  counter names/format, section 4.2) and the FP-Q1 Jev call's name-similarity context.
  ==========================================================================================

  THE FOUR TRAPS (docs/fixture-parser-check-spec.md section 0), unaffected by revision 1:
    1. Walk ALL settings.json revisions the tracked file has ever had (git log -- the
       path), never a recent slice. MEASURED: a 40-revision walk clears indicators.OBV.
       trend_gate as novel when the full 87-revision walk finds it was 0.001 and 10.0
       before today's 18.0/23.0 pair.
    2. Never compose the two Noul diagnostics with AND. The `verdict` Choice answer is the
       ONLY thing code reads -- see the lines marked TRAP 2 below.
    3. Never trust a documented value history (a doc in this repo records trend_gate
       starting at 8.0; 8.0 never shipped). Ground truth is the revision walk alone.
    4. Do not line-anchor regexes over VB. Sub/Function/End declarations ARE always
       statement-starting lines in this codebase (unlike Return/Throw/Exit), so anchoring
       those specific patterns is safe; the named-argument literal scan itself is applied
       to comment-stripped line text, never anchored to line start, so it cannot be fooled
       by an inline single-line form the way a `^\s*Return` scan would be.

  THE SPLIT THAT MAKES THIS TRACTABLE (docs/fixture-parser-check-spec.md section 1):
    - CODE: does the literal equal any ever-shipped value of its matched key?
    - JEV:  does the comment at that call site declare the right class for what code found?

  FP-D6 (pre-revision): "call site" for FP-1's population is a named-argument literal pass
  (`name:=literal`), matching section 4.1 step 1's literal wording. Section 2's whole-file
  MECHANISM/SHIPPED grep counts also cover non-`:=` literal forms, so
  SITES_WITH_PROVENANCE_COMMENT below (computed only over named-argument call sites) will
  not reproduce those counts exactly. Section 2 itself says "treat as smoke anchors".

  FP-D7 (pre-revision, SUPERSEDED by FP-Q3/FP-D11 above): used to run one unsampled
  `key_resolution` Jev call per residual site whose FP-D2 name match was zero/multi-
  candidate. Removed -- FP-Q3's production-call-site evidence is now the authoritative
  resolution and needs no per-site Jev call; FP-Q1's (differently-scoped) Jev call is its
  replacement for the population-membership question.

  FP-D8 (pre-revision): -SubFilter restricts which residual sites/subs are JUDGED (sent to
  the FP-1/FP-2 verdict calls), for splitting the acceptance window from the measured
  window (docs/harness-shadow-mode-protocol.md section 4a). It does NOT affect the
  full-file coverage counters (including revision 1's IN_SCOPE_SITES/mapping counters) --
  those exist to describe the whole file regardless of which subset a run judges.

  FP-D9 (pre-revision): acceptance item 3 (forced LITERAL_CALL_SITES < 60) is demonstrated
  by pointing -SourceFile at a truncated scratch copy of Program.vb, not a synthetic
  override switch.

  USAGE:
    set -a; . ./typesafe.local.env; set +a   # loads TYPESAFE_API_KEY (bash)
    powershell -NoProfile -File tools/checks/fixture-parser.ps1 `
      -BaselinePath <path to your own pre-written read, see
      docs/harness-shadow-mode-protocol.md section 2 step 3> `
      [-ScopeBaselinePath <the same, for the FP-Q1 scope detector -- FP-D15; without it
       FP-Q1 makes no Jev call and the in-scope counters are CODE-ONLY>] `
      [-SubFilter <regex on enclosing sub name>] [-Samples N] [-CountersOnly]
      [-SiteDumpPath <tsv>] [-Fp2Only] [-TestTransportOverride <scriptblock, tests only>]

  EXIT CODES:
    0 - every judged FP-1 call site verdict is mechanism_declared_ok, every judged FP-2 sub
        verdict is name_matches, and no row is UNSTABLE (self-consistency agreement rate
        below 1.0)
    1 - at least one judged FP-1 verdict is undeclared / declared_but_contradicted /
        ambiguous / shipped_declared_ok (FP-D25: on a literal site that last one is a breach
        or a misread, never a pass), at least one judged FP-2 verdict is name_overclaims /
        name_understates / ambiguous, or any judged row is UNSTABLE (never auto-resolved).
        FP-1 VERSION 1 only: FP-1v2 (FP-D30) never reaches the exit code
    2 - baseline missing, parser suspect (docs/fixture-parser-check-spec.md section 4.2),
        or API failure (now possibly raised EARLIER than the baseline gate, by FP-Q1's
        scope-classification step -- see FP-D11 above)
#>
[CmdletBinding()]
param(
    [string]$SourceFile = 'verify/ordercheck/Program.vb',
    [string]$SettingsPath = 'settings.json',
    [string]$BaselinePath = 'fixture-parser-baseline.json',
    # FP-D15 / docs/fixture-parser-check-spec.md section 8.4 item 8c: the operator-written
    # baseline for the SECOND detector, FP-Q1's scope filter. Empty means "none supplied",
    # and then FP-Q1 makes NO Jev call at all -- see the refusal below Step 4.
    [string]$ScopeBaselinePath = '',
    [string]$OutPath = 'fixture-parser-report.md',
    [string]$SettingsCachePath = 'fixture-parser-settings-cache.json',
    # Restricts which residual (provenance-commented, IN-SCOPE) sites/subs are JUDGED -- a
    # regex tested against the enclosing sub's name. Lets an acceptance dry-run window and
    # the eventual measured first run be disjoint (docs/harness-shadow-mode-protocol.md
    # section 4a). Never affects the coverage counters (FP-D8 above).
    [string]$SubFilter = $null,
    # FP-D4 / docs/harness-shadow-mode-protocol.md section 4b: self-consistency sample
    # count for FP-1/FP-2's verdict questions only (FP-D14). -1 is a sentinel for "use
    # $SELF_CONSISTENCY_SAMPLES below".
    [int]$Samples = -1,
    # docs/harness-shadow-mode-protocol.md section 4a: withholds per-item verdicts and
    # every aggregate from a RESERVED window's dry run, console and report alike. Mirrors
    # tools/checks/commit-walker.ps1's -CountersOnly exactly.
    [switch]$CountersOnly,
    # FP-D24 (revision 3): by default a run REFUSES when any residual item it would judge
    # has no line in the -BaselinePath file -- an unbaselined item judged once is spent for
    # ever (docs/harness-shadow-mode-protocol.md section 5). Pass this only for a routine
    # re-run whose items were all measured before.
    [switch]$AllowUnbaselinedItems,
    # Mirrors tools/checks/commit-walker.ps1's -DebugState: prints each call's state KEY
    # NAMES and sample_uid only, never content or the API key. Off by default.
    [switch]$DebugState,
    # REVISION 4: writes one tab-separated line per named-argument literal site -- CODE
    # FACTS ONLY (callee, receiver type, mapping class, key, marker, scope), never a verdict
    # and never anything a detector said. Lets a reviewer diff two revisions site by site.
    # Written before any API call, on every exit path that reaches the coverage block.
    [string]$SiteDumpPath = '',
    # REVISION 4, TEST SEAM ONLY -- OFF BY DEFAULT ($null). Forwarded to every Invoke-Jev
    # call (FP-Q1, FP-1, FP-2) in place of the real HTTP transport, exactly as
    # tools/checks/commit-walker.ps1 and tools/checks/rider-travel.ps1 do. Exists so FP-1v2
    # (item 8o) can be shown end to end on SYNTHETIC items with no real call. No caller
    # passes this in a real run.
    [scriptblock]$TestTransportOverride = $null,
    # REVISION 4: judge FP-2 names only; FP-1 sites are neither judged nor gated. Exists for
    # the FP-D3 mutation test (docs/fixture-parser-check-spec.md section 3), which corrupts
    # fixture NAMES and must not spend FP-1 calls on the provenance sites under them.
    [switch]$Fp2Only
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# FP-D4: 5 samples, agreement rate on every row (docs/harness-shadow-mode-protocol.md
# section 4b), named beside its sibling constants, not buried in the param block. Scope:
# FP-1/FP-2 verdict questions only -- FP-D14 above.
$SELF_CONSISTENCY_SAMPLES = 5
if ($Samples -lt 1) { $Samples = $SELF_CONSISTENCY_SAMPLES }

# Defensive cap on a fixture Sub's body text sent to FP-2 -- not specced, added because a
# handful of subs in this file run past 150 lines. Truncated, not refused, so FP-2 still
# gets a judgment on an oversized sub rather than silently failing it. Same reasoning
# tools/checks/commit-walker.ps1 uses for $MAX_ESCALATION_DIFF_CHARS.
$MAX_SUB_BODY_CHARS = 12000

# Invoke-Jev is shared with tools/checks/commit-walker.ps1 and tools/checks/rider-travel.ps1
# (docs/fixture-parser-check-spec.md section 4: "Reuse it. Never write a second HTTP
# call."). Do not re-implement it here.
. (Join-Path $PSScriptRoot 'lib\InvokeJev.ps1')

function Resolve-RepoPath([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $repo $p)
}

# ---------------------------------------------------------------------------------------
# Step 0: VB source parsing helpers (docs/fixture-parser-check-spec.md section 4.1 step 1
# / trap 4). Everything here that scans for a PATTERN (named-argument literals) works on
# comment-stripped text and is NOT line-anchored. The declaration/End scans ARE anchored
# to line start deliberately -- Sub/Function/End declarations are always statement-starting
# lines in this codebase, unlike the Return/Throw/Exit/single-line-If forms trap 4 warns
# about, so anchoring those specific patterns does not reintroduce the trap.
# ---------------------------------------------------------------------------------------

# Strips a trailing VB comment from a line, string-literal aware (VB strings use "" as an
# escaped double-quote; a `'` inside a string is not a comment start).
function Get-CodeOnly([string]$line) {
    $inStr = $false
    for ($i = 0; $i -lt $line.Length; $i++) {
        $ch = $line[$i]
        if ($ch -eq '"') {
            if ($inStr -and ($i + 1) -lt $line.Length -and $line[$i + 1] -eq '"') { $i++; continue }
            $inStr = -not $inStr
            continue
        }
        if ($ch -eq "'" -and -not $inStr) { return $line.Substring(0, $i) }
    }
    return $line
}

# camelCase -> snake_case (FP-D2's "name normalisation"; also used by FP-Q3's path
# normalisation below, per-segment). "currentATR" -> "current_atr", "slopeMinUsd" ->
# "slope_min_usd" -- verified against all 43 distinct parameter names found in this file
# before the pre-revision build, and against indicators.OBV/TFI/OFI's JSON casing before
# this revision (see FP-Q3 section below).
function ConvertTo-SnakeCase([string]$name) {
    $s = [regex]::Replace($name, '([a-z0-9])([A-Z])', '$1_$2')
    return $s.ToLowerInvariant()
}

$declRe = '^(Private|Public|Friend|Protected)?\s*(Shared\s+)?(Sub|Function)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\('
$endRe = '^End\s+(Sub|Function)\b'

# ---------------------------------------------------------------------------------------
# Step 1 (section 4.1 step 1): read the source, build per-line code-only text, a paren-
# depth table (VB.NET allows implicit line continuation inside unclosed parens), Sub/
# Function ranges, and every named-argument literal pass with its enclosing statement's
# comment block (FP-Q2: BOTH the old backward-walk block and the new forward-coverage-map
# block are captured per site, so before/after can be reported -- section 7.5 item 3).
# ---------------------------------------------------------------------------------------
$srcFull = Resolve-RepoPath $SourceFile
if (-not (Test-Path $srcFull)) {
    "EXIT_REASON=SOURCE_MISSING"
    "No file at -SourceFile '$SourceFile'."
    exit 2
}
$lines = [string[]](Get-Content -Encoding UTF8 -Path $srcFull)
$n = $lines.Length

$codeOnly = New-Object string[] $n
$netParen = New-Object int[] $n
for ($i = 0; $i -lt $n; $i++) {
    $codeOnly[$i] = Get-CodeOnly $lines[$i]
    $opens = ([regex]::Matches($codeOnly[$i], '\(')).Count
    $closes = ([regex]::Matches($codeOnly[$i], '\)')).Count
    $netParen[$i] = $opens - $closes
}
# depth0[i] = paren depth at the START of line i (0-based). A line is the start of a new
# VB statement iff depth0[i] == 0 -- unclosed parens are the only implicit continuation
# this codebase uses (no trailing "_" line-continuation forms observed).
$depth0 = New-Object int[] ($n + 1)
for ($i = 0; $i -lt $n; $i++) {
    $d = $depth0[$i] + $netParen[$i]
    if ($d -lt 0) { $d = 0 }
    $depth0[$i + 1] = $d
}
function Get-StatementStart([int]$Lidx) {
    for ($k = $Lidx; $k -ge 0; $k--) { if ($depth0[$k] -eq 0) { return $k } }
    return 0
}

# ---- FP-Q2: OLD comment-block extraction (kept, renamed, for the before/after proof) ----
# Contiguous run of comment lines immediately above a statement's start line -- no blank-
# line tolerance. This is the PRE-revision behaviour: it attaches a comment block ONLY to
# the statement immediately below it, silently dropping A6_ObvNormalisation's second
# CalcOBV call (docs/fixture-parser-check-spec.md section 5a item 3 / section 7.2 FP-Q2).
function Get-CommentBlockOld([int]$stmtStart) {
    $collected = New-Object System.Collections.Generic.List[string]
    $k = $stmtStart - 1
    while ($k -ge 0) {
        $t = $lines[$k].Trim()
        if ($t.StartsWith("'")) { $collected.Insert(0, $t); $k-- } else { break }
    }
    return ($collected -join "`n")
}

# ---- FP-Q2: NEW comment-block extraction, ruled (b) "until the next blank line" --------
# FP-D10 above documents the algorithm. Built once, indexed by RAW line number; looked up
# at a statement's start line exactly like the old function was.
$commentCoverage = New-Object string[] $n
$activeBlock = ''
$pendingBuffer = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $n; $i++) {
    $t = $lines[$i].Trim()
    if ($t -eq '') {
        $activeBlock = ''
        $pendingBuffer.Clear()
        $commentCoverage[$i] = ''
        continue
    }
    if ($t.StartsWith("'")) {
        $pendingBuffer.Add($t)
        $commentCoverage[$i] = ''
        continue
    }
    if ($pendingBuffer.Count -gt 0) {
        $activeBlock = ($pendingBuffer -join "`n")
        $pendingBuffer.Clear()
    }
    $commentCoverage[$i] = $activeBlock
}

$stack = New-Object System.Collections.Generic.Stack[object]
$ranges = New-Object System.Collections.Generic.List[object]
for ($i = 0; $i -lt $n; $i++) {
    $t = $codeOnly[$i].Trim()
    if ($t -match $declRe) {
        $stack.Push([PSCustomObject]@{ Kind = $Matches[3]; Name = $Matches[4]; Start = $i })
    } elseif ($t -match $endRe) {
        if ($stack.Count -gt 0) {
            $top = $stack.Pop()
            $ranges.Add([PSCustomObject]@{ Kind = $top.Kind; Name = $top.Name; Start = $top.Start; End = $i })
        }
    }
}
function Get-EnclosingProc([int]$Lidx) {
    $best = $null
    foreach ($r in $ranges) {
        if ($r.Start -le $Lidx -and $Lidx -le $r.End) {
            if ($null -eq $best -or ($r.End - $r.Start) -lt ($best.End - $best.Start)) { $best = $r }
        }
    }
    return $best
}

# ---- FP-Q3: which METHOD is this literal being passed to? ------------------------------
# Walks backward from the literal's own position through the statement, tracking paren
# depth, to find the nearest UNCLOSED '(' -- the call this literal is an argument of -- and
# the identifier immediately before it. Returns the FULL dotted identifier
# ("IndicatorEngine.CalcOFI", "acc.Fold"). The caller splits it: the LAST segment is the
# callee's bare name (compared against declarations, as before), and the segments before it
# are the RECEIVER qualifier that FP-D27 uses to pick between same-named declarations.
function Get-EnclosingCallIdent([int]$stmtStart, [int]$matchLineIdx, [int]$matchCharPos) {
    $depth = 0
    for ($li = $matchLineIdx; $li -ge $stmtStart; $li--) {
        $text = $codeOnly[$li]
        $startPos = if ($li -eq $matchLineIdx) { $matchCharPos - 1 } else { $text.Length - 1 }
        for ($ci = $startPos; $ci -ge 0; $ci--) {
            $ch = $text[$ci]
            if ($ch -eq ')') { $depth++ }
            elseif ($ch -eq '(') {
                if ($depth -eq 0) {
                    $j = $ci - 1
                    while ($j -ge 0 -and $text[$j] -eq ' ') { $j-- }
                    $endJ = $j
                    while ($j -ge 0 -and ($text[$j] -match '[A-Za-z0-9_.]')) { $j-- }
                    if ($endJ -lt 0 -or $endJ -le $j) { return $null }
                    return $text.Substring($j + 1, $endJ - $j)
                } else { $depth-- }
            }
        }
    }
    return $null
}

# Named-argument literal pass: `paramName:=numericLiteral`, matched against comment-
# stripped text ONLY (never against the raw line, so a literal-looking string inside a
# comment or a quoted message can never be mistaken for a real call-site argument).
$literalRe = '([A-Za-z_][A-Za-z0-9_]*):=(-?\d+(?:\.\d+)?[FLDR]?)'
$callSites = New-Object System.Collections.Generic.List[object]
for ($i = 0; $i -lt $n; $i++) {
    $ms = [regex]::Matches($codeOnly[$i], $literalRe)
    foreach ($m in $ms) {
        $param = $m.Groups[1].Value
        $valRaw = $m.Groups[2].Value
        $valClean = $valRaw.TrimEnd('F', 'L', 'D', 'R', 'f', 'l', 'd', 'r')
        $valD = [double]::Parse($valClean, [System.Globalization.CultureInfo]::InvariantCulture)
        $stmtStart = Get-StatementStart $i
        $commentBlockOld = Get-CommentBlockOld $stmtStart
        $commentBlockNew = $commentCoverage[$stmtStart]
        $proc = Get-EnclosingProc $i
        $calleeIdent = Get-EnclosingCallIdent $stmtStart $i $m.Index
        $callee = $null; $calleeQualifier = ''
        if ($calleeIdent) {
            $identSegs = @($calleeIdent -split '\.')
            $callee = $identSegs[$identSegs.Length - 1]
            if ($identSegs.Length -gt 1) { $calleeQualifier = ($identSegs[0..($identSegs.Length - 2)] -join '.') }
            # `GetThing().Snapshot(` walks back to `.Snapshot` only: the receiver is an
            # expression this parser does not type. Marked so it is never read as unqualified.
            if ($calleeIdent.StartsWith('.')) { $calleeQualifier = '(expr)' }
        }
        $callSites.Add([PSCustomObject]@{
            Line          = $i + 1
            StmtStart     = $stmtStart
            Param         = $param
            LiteralRaw    = $valRaw
            LiteralValue  = $valD
            CallLineText  = $lines[$i].Trim()
            CommentBlockOld = $commentBlockOld
            CommentBlock  = $commentBlockNew   # FP-Q2 ruling (b) -- this is the one used from here on
            EnclosingSub  = if ($proc) { $proc.Name } else { '(module-level)' }
            EnclosingKind = if ($proc) { $proc.Kind } else { 'Unknown' }
            Callee        = $callee
            # FP-D27: the receiver qualifier as written ('' for an unqualified call).
            CalleeQualifier = $calleeQualifier
            HasMarkerOld  = [bool]($commentBlockOld -match '\bMECHANISM\b' -or $commentBlockOld -match '\bSHIPPED\b')
            HasMarker     = [bool]($commentBlockNew -match '\bMECHANISM\b' -or $commentBlockNew -match '\bSHIPPED\b')
            # FP-D28 (revision 4, review finding 8): the marker is BLOCK-level -- one keyword
            # marks every site under its comment block. A LABEL, never a filter: SITE_NAMED
            # when the block names this site's parameter (whole word, case-insensitive, as
            # VB is), BLOCK_ONLY when it does not. Every marked site is judged either way.
            MarkerScope   = if ([bool]($commentBlockNew -match '\bMECHANISM\b' -or $commentBlockNew -match '\bSHIPPED\b')) {
                                if ($commentBlockNew -match "(?i)(?<![A-Za-z0-9_])$([regex]::Escape($param))(?![A-Za-z0-9_])") { 'SITE_NAMED' } else { 'BLOCK_ONLY' }
                            } else { '' }
        })
    }
}

$fixtureSubs = @($ranges | Where-Object { $_.Kind -eq 'Sub' -and $_.Name -match '^A\d+[a-z]?_' }).Count

# FP-Q2 acceptance item 3: before/after site counts, and the A6_ObvNormalisation proof.
$sitesWithCommentBefore = @($callSites | Where-Object { $_.CommentBlockOld -ne '' }).Count
$sitesWithCommentAfter  = @($callSites | Where-Object { $_.CommentBlock -ne '' }).Count
$sitesGainedCoverage    = @($callSites | Where-Object { $_.CommentBlockOld -eq '' -and $_.CommentBlock -ne '' }).Count
$sitesWithProvenanceBefore = @($callSites | Where-Object { $_.HasMarkerOld }).Count
$sitesWithProvenanceAfter  = @($callSites | Where-Object { $_.HasMarker }).Count

# ---------------------------------------------------------------------------------------
# Step 2 (section 4.1 step 2): walk ALL settings.json revisions (trap 1). Flatten each
# revision's JSON to dotted scalar-leaf paths (arrays skipped -- none of the distinct
# parameter names found in this file collide with a settings.json array element, verified
# before the pre-revision build). Cache per-revision flattening to a gitignored scratch
# file: history below HEAD is immutable, so a hash already in cache is never re-walked.
# ---------------------------------------------------------------------------------------
# FP-D29 (revision 4, review finding 9): ARRAYS. The walk used to skip every JSON array,
# and settings.json keeps per-session values in session_volume.sessions[] -- so an
# array-backed key could never resolve. Now an array of OBJECTS is walked element by
# element, each keyed by its own `name` field, giving a readable path:
#   session_volume.sessions[name=ASIA].high_multiplier
# The `name` field itself is the key, not a leaf, so it is not emitted. An element with no
# usable `name` (absent, empty, a duplicate within the array, or holding one of . [ ] =)
# is counted ARRAY_UNKEYED and skipped -- an INDEX is never guessed, because an index path
# would silently re-point when an element is inserted. An array of scalars (change_log) is
# skipped as before and counted apart. $stats is filled only for the HEAD walk.
function Get-FlattenedSettings($obj, [string]$prefix, [hashtable]$out, [hashtable]$stats = $null) {
    if ($null -eq $obj) { return }
    if ($obj -is [System.Management.Automation.PSCustomObject]) {
        foreach ($p in $obj.PSObject.Properties) {
            $np = if ($prefix) { "$prefix.$($p.Name)" } else { $p.Name }
            Get-FlattenedSettings $p.Value $np $out $stats
        }
    } elseif ($obj -is [System.Array]) {
        $objEls = @($obj | Where-Object { $_ -is [System.Management.Automation.PSCustomObject] })
        if ($objEls.Count -eq 0) {
            if ($null -ne $stats -and $obj.Count -gt 0) { $stats.ScalarArrays++ }
            return
        }
        $nameCounts = @{}
        foreach ($el in $objEls) {
            if ($el.PSObject.Properties.Name -contains 'name') {
                $nk = ([string]$el.name).ToLowerInvariant()
                if ($nameCounts.ContainsKey($nk)) { $nameCounts[$nk]++ } else { $nameCounts[$nk] = 1 }
            }
        }
        foreach ($el in $obj) {
            $nm = $null
            if ($el -is [System.Management.Automation.PSCustomObject] -and $el.PSObject.Properties.Name -contains 'name') { $nm = [string]$el.name }
            $usable = (-not [string]::IsNullOrWhiteSpace($nm)) -and ($nm -notmatch '[.\[\]=]') -and ($nameCounts[$nm.ToLowerInvariant()] -eq 1)
            if (-not $usable) {
                if ($null -ne $stats) { $stats.UnkeyedElements++; $stats.UnkeyedPaths[$prefix] = $true }
                continue
            }
            if ($null -ne $stats) { $stats.KeyedElements++ }
            foreach ($p in $el.PSObject.Properties) {
                if ($p.Name -eq 'name') { continue }
                Get-FlattenedSettings $p.Value "$prefix[name=$nm].$($p.Name)" $out $stats
            }
        }
    } else {
        if ($prefix) { $out[$prefix] = $obj }
    }
}

# FP-D29: the cache holds FLATTENED revisions, so a change to the flattening makes every
# cached entry stale even though the history below HEAD is immutable. The schema tag is
# checked on load; a mismatch (including a pre-revision-4 cache, which has no tag) is
# discarded and all revisions are walked again.
$SETTINGS_CACHE_SCHEMA = 'fp-d29-arrays-keyed-by-name'
$cacheFull = Resolve-RepoPath $SettingsCachePath
$revisionCache = @{}
$settingsCacheDiscarded = $false
if (Test-Path $cacheFull) {
    try {
        $raw = Get-Content -Raw -Path $cacheFull | ConvertFrom-Json -ErrorAction Stop
        if (($raw.PSObject.Properties.Name -contains '_schema') -and ([string]$raw._schema -eq $SETTINGS_CACHE_SCHEMA)) {
            foreach ($hp in $raw.PSObject.Properties) {
                if ($hp.Name -eq '_schema') { continue }
                $inner = @{}
                foreach ($kp in $hp.Value.PSObject.Properties) { $inner[$kp.Name] = $kp.Value }
                $revisionCache[$hp.Name] = $inner
            }
        } else { $settingsCacheDiscarded = $true }
    } catch { $revisionCache = @{} }
}

$settingsHashes = [string[]](& git -C $repo log --format=%H -- $SettingsPath)
$newlyWalked = 0
foreach ($h in $settingsHashes) {
    if ($revisionCache.ContainsKey($h)) { continue }
    $raw = & git -C $repo show "${h}:$SettingsPath" 2>$null
    if (-not $raw) { continue }
    try {
        $j = ($raw -join "`n") | ConvertFrom-Json -ErrorAction Stop
    } catch { continue }
    $inner = @{}
    Get-FlattenedSettings $j '' $inner
    $revisionCache[$h] = $inner
    $newlyWalked++
}
if ($newlyWalked -gt 0) {
    $toSave = @{ _schema = $SETTINGS_CACHE_SCHEMA }
    foreach ($hk in $revisionCache.Keys) { $toSave[$hk] = $revisionCache[$hk] }
    ($toSave | ConvertTo-Json -Depth 6) | Set-Content -Encoding UTF8 -Path $cacheFull
}
$settingsRevisionsWalked = @($settingsHashes | Where-Object { $revisionCache.ContainsKey($_) }).Count
# FP-D29: distinct array-element keys across every walked revision (not just HEAD).
$arrayKeysEverSet = New-Object System.Collections.Generic.HashSet[string]
foreach ($h in $settingsHashes) {
    if (-not $revisionCache.ContainsKey($h)) { continue }
    foreach ($rk in $revisionCache[$h].Keys) { if ($rk.Contains('[name=')) { [void]$arrayKeysEverSet.Add($rk) } }
}
$settingsArrayKeysEver = $arrayKeysEverSet.Count

# Ever-shipped value SET for one settings key path, across every walked revision. This is
# the only source of truth for "ever shipped" -- never a doc (trap 3), never a recent
# slice (trap 1).
function Get-EverShippedSet([string]$keyPath) {
    $set = New-Object System.Collections.Generic.List[double]
    $seen = New-Object System.Collections.Generic.HashSet[double]
    foreach ($h in $settingsHashes) {
        if (-not $revisionCache.ContainsKey($h)) { continue }
        $flat = $revisionCache[$h]
        if ($flat.ContainsKey($keyPath)) {
            $d = $null
            if ([double]::TryParse([string]$flat[$keyPath], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$d)) {
                if ($seen.Add($d)) { $set.Add($d) }
            }
        }
    }
    # PowerShell's `return` writes to the success pipeline, which UNROLLS a collection --
    # a 0- or 1-element array collapses to nothing / a bare scalar at the call site.
    # -NoEnumerate forces the whole array through as one object, array-ness preserved
    # regardless of element count. See the matching comment on Get-KeyCandidates below.
    Write-Output -NoEnumerate @($set | Sort-Object)
}

# ---------------------------------------------------------------------------------------
# Step 3 (section 4.1 step 3, FP-D2 -- INFORMATIONAL ONLY as of revision 1, see header):
# match each call site's parameter to candidate settings keys by name normalisation.
# ---------------------------------------------------------------------------------------
$headRaw = Get-Content -Raw -Path (Resolve-RepoPath $SettingsPath) | ConvertFrom-Json
$headFlat = @{}
$headArrayStats = @{ ScalarArrays = 0; UnkeyedElements = 0; KeyedElements = 0; UnkeyedPaths = @{} }
Get-FlattenedSettings $headRaw '' $headFlat $headArrayStats
# FP-D29: array-element keys (they carry `[name=`) join the revision walk and FP-Q3's
# resolution index below, but NOT the FP-D2 name index or FP-Q1's fuzzy candidates. Those
# two feed FP-Q1's Jev STATE, and FP-Q1 was measured on the old key set; changing a
# detector's input without a re-measure is the comparability break item 8o names.
$headFlatNoArray = @($headFlat.Keys | Where-Object { -not $_.Contains('[name=') })
$headArrayLeaves = $headFlat.Count - $headFlatNoArray.Count
$leafIndex = @{}
foreach ($k in $headFlatNoArray) {
    $leaf = $k.Split('.')[-1]
    if (-not $leafIndex.ContainsKey($leaf)) { $leafIndex[$leaf] = New-Object System.Collections.Generic.List[string] }
    $leafIndex[$leaf].Add($k)
}

# See the Get-EverShippedSet comment above: every branch uses Write-Output -NoEnumerate so
# a single-candidate match survives as a 1-element ARRAY, not a scalar string that a later
# `[0]` index would slice into its first character.
function Get-KeyCandidates([string]$paramName) {
    $snake = ConvertTo-SnakeCase $paramName
    if ($leafIndex.ContainsKey($snake)) { Write-Output -NoEnumerate @($leafIndex[$snake]); return }
    $parts = $snake -split '_'
    if ($parts.Length -gt 1) {
        $stripped = ($parts[1..($parts.Length - 1)] -join '_')
        if ($leafIndex.ContainsKey($stripped)) { Write-Output -NoEnumerate @($leafIndex[$stripped]); return }
    }
    Write-Output -NoEnumerate @()
}

foreach ($cs in $callSites) {
    $cands = Get-KeyCandidates $cs.Param
    $cs | Add-Member -NotePropertyName Candidates -NotePropertyValue $cands
    $cs | Add-Member -NotePropertyName MatchClass -NotePropertyValue $(
        if ($cands.Count -eq 0) { 'ZERO' } elseif ($cands.Count -eq 1) { 'ONE' } else { 'MULTI' })
}

# ---------------------------------------------------------------------------------------
# FP-Q3 (section 7.2/7.3, FP-D11/FP-D12/FP-D13 above): DERIVED mapping from production
# call sites. Code only -- no Jev. This is now the AUTHORITATIVE source for ResolvedKey.
# ---------------------------------------------------------------------------------------
# [2026-09-22] 'tools' ADDED. docs/fixture-parser-check-spec.md section 7.3 listed only
# Core/UI/analysis/root, and that was a spec defect: a fixture calling L2Logistic.Fit or
# PromptBuilder.Build has its PRODUCTION call site under tools/CeilingAudit, tools/
# AutoTweaker or tools/WhatIfRunner. Excluding tools/ made those read
# NO_PRODUCTION_CALL_SITE and dropped them from scope, which is part of why the in-scope
# count came in under the expected band.
#
# ⚠ This does NOT contradict docs/commit-walker-check-spec.md section 2, which
# deliberately treats tools/ as NOT an engine path. That spec asks "did this change the
# SHIPPED APP". This one asks "where is this method actually called in production" -- and
# an offline analysis tool is a production caller of its own helpers. Different question,
# different answer, both correct.
$prodDirNames = @('Core', 'UI', 'analysis', 'tools')
$prodFiles = New-Object System.Collections.Generic.List[string]
foreach ($d in $prodDirNames) {
    $full = Resolve-RepoPath $d
    if (Test-Path $full) {
        Get-ChildItem -Path $full -Filter '*.vb' -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($repo.Length + 1) -replace '\\', '/'
            $prodFiles.Add($rel)
        }
    }
}
Get-ChildItem -Path $repo -Filter '*.vb' -File | ForEach-Object { $prodFiles.Add($_.Name) }

$fileParseCache = @{}
function Get-FileParseContext([string]$relPath) {
    if ($fileParseCache.ContainsKey($relPath)) { return $fileParseCache[$relPath] }
    $full = Resolve-RepoPath $relPath
    if (-not (Test-Path $full)) { $fileParseCache[$relPath] = $null; return $null }
    $flines = [string[]](Get-Content -Encoding UTF8 -Path $full)
    $fn = $flines.Length
    $fcodeOnly = New-Object string[] $fn
    $fnetParen = New-Object int[] $fn
    for ($i = 0; $i -lt $fn; $i++) {
        $fcodeOnly[$i] = Get-CodeOnly $flines[$i]
        $o = ([regex]::Matches($fcodeOnly[$i], '\(')).Count
        $c = ([regex]::Matches($fcodeOnly[$i], '\)')).Count
        $fnetParen[$i] = $o - $c
    }
    $fdepth0 = New-Object int[] ($fn + 1)
    for ($i = 0; $i -lt $fn; $i++) {
        $d = $fdepth0[$i] + $fnetParen[$i]
        if ($d -lt 0) { $d = 0 }
        $fdepth0[$i + 1] = $d
    }
    $ctx = [PSCustomObject]@{ Path = $relPath; Lines = $flines; CodeOnly = $fcodeOnly; Depth0 = $fdepth0; N = $fn }
    $fileParseCache[$relPath] = $ctx
    return $ctx
}
function Get-FileStatementStart($ctx, [int]$lidx) {
    for ($k = $lidx; $k -ge 0; $k--) { if ($ctx.Depth0[$k] -eq 0) { return $k } }
    return 0
}
function Get-FileStatementEnd($ctx, [int]$startIdx) {
    for ($k = $startIdx; $k -lt $ctx.N; $k++) { if ($ctx.Depth0[$k + 1] -eq 0) { return $k } }
    return $ctx.N - 1
}
function Get-FileStatementText($ctx, [int]$startIdx, [int]$endIdx) {
    return (($ctx.CodeOnly[$startIdx..$endIdx]) -join ' ')
}
$fileProcRangesCache = @{}
function Get-FileEnclosingProc($ctx, [int]$lidx) {
    if (-not $fileProcRangesCache.ContainsKey($ctx.Path)) {
        $stack2 = New-Object System.Collections.Generic.Stack[object]
        $ranges2 = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $ctx.N; $i++) {
            $t = $ctx.CodeOnly[$i].Trim()
            if ($t -match $declRe) {
                $stack2.Push([PSCustomObject]@{ Kind = $Matches[3]; Name = $Matches[4]; Start = $i })
            } elseif ($t -match $endRe) {
                if ($stack2.Count -gt 0) {
                    $top = $stack2.Pop()
                    $ranges2.Add([PSCustomObject]@{ Kind = $top.Kind; Name = $top.Name; Start = $top.Start; End = $i })
                }
            }
        }
        $fileProcRangesCache[$ctx.Path] = $ranges2
    }
    $ranges2 = $fileProcRangesCache[$ctx.Path]
    $best = $null
    foreach ($r in $ranges2) {
        if ($r.Start -le $lidx -and $lidx -le $r.End) {
            if ($null -eq $best -or ($r.End - $r.Start) -lt ($best.End - $best.Start)) { $best = $r }
        }
    }
    return $best
}

# String-literal-aware matching-close-paren finder + top-level (depth-0, outside strings)
# comma splitter. Shared by both the DECLARATION parse (signature) and the CALL-SITE parse
# (arguments) below -- same syntax shape (`Name(arg, arg, ...)`), same parser.
function Get-CallArguments([string]$stmtText, [string]$calleeName) {
    # (?<![A-Za-z0-9_]) only -- NOT also excluding a preceding '.'. A preceding dot is
    # exactly the module-qualified call shape this parser must match
    # ("IndicatorEngine.CalcOFI(" etc.) -- excluding it here was a bug caught during this
    # build's own dry run (every one of the three worked-example methods came back
    # NO_PRODUCTION_CALL_SITE until this was fixed; see the report).
    $callRe = "(?<![A-Za-z0-9_])$([regex]::Escape($calleeName))\s*\("
    $m = [regex]::Match($stmtText, $callRe)
    if (-not $m.Success) { return $null }
    $openIdx = $m.Index + $m.Length - 1
    $depth = 0; $inStr = $false; $closeIdx = -1
    for ($i = $openIdx; $i -lt $stmtText.Length; $i++) {
        $ch = $stmtText[$i]
        if ($ch -eq '"') {
            if ($inStr -and ($i + 1) -lt $stmtText.Length -and $stmtText[$i + 1] -eq '"') { $i++; continue }
            $inStr = -not $inStr; continue
        }
        if ($inStr) { continue }
        if ($ch -eq '(') { $depth++ }
        elseif ($ch -eq ')') { $depth--; if ($depth -eq 0) { $closeIdx = $i; break } }
    }
    if ($closeIdx -lt 0) { return $null }
    $inner = $stmtText.Substring($openIdx + 1, $closeIdx - $openIdx - 1)
    $argsList = New-Object System.Collections.Generic.List[string]
    $depth2 = 0; $inStr2 = $false; $start = 0
    for ($i = 0; $i -lt $inner.Length; $i++) {
        $ch = $inner[$i]
        if ($ch -eq '"') {
            if ($inStr2 -and ($i + 1) -lt $inner.Length -and $inner[$i + 1] -eq '"') { $i++; continue }
            $inStr2 = -not $inStr2; continue
        }
        if ($inStr2) { continue }
        if ($ch -eq '(') { $depth2++ }
        elseif ($ch -eq ')') { $depth2-- }
        elseif ($ch -eq ',' -and $depth2 -eq 0) { $argsList.Add($inner.Substring($start, $i - $start)); $start = $i + 1 }
    }
    if ($inner.Length -gt 0 -or $argsList.Count -gt 0) { $argsList.Add($inner.Substring($start)) }
    return ,@($argsList | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
}

# ---------------------------------------------------------------------------------------
# FP-D27 (revision 4, review finding 4): SIGNATURES BY DECLARATION, NOT BY BARE NAME.
# Before this, a callee's signature was the FIRST `Sub|Function <name>(` in production file
# order. `Compute` is declared on five classes, `Snapshot` on four, `Evaluate` and `Build`
# on three, `Fold` on two -- so a POSITIONAL mapping could be read off the wrong class's
# parameter list and produce a confidently wrong key. Measured before the fix:
# `DynamicNorms.Compute(..., currentATR:=50)` was read against analysis/BandLadder.vb and
# reported PARAM_NOT_IN_SIGNATURE; `Fold`/`Snapshot` resolved correctly only because
# Core/AggressorVelocityAccumulator.vb happened to sort first.
#
# Now every production method declaration is indexed with its ENCLOSING TYPE. A name with
# one declaration behaves exactly as before. A name with several is resolved ONLY when the
# call's RECEIVER TYPE can be read from the code -- a type-name qualifier
# (`DynamicNorms.Compute`), a local (`Dim acc As New AggressorVelocityAccumulator()`), a
# parameter, or a field of the enclosing class (`Private ReadOnly _aggrVelAcc As New
# AggressorVelocityAccumulator()`) -- and exactly one declaration lives on that type.
# Otherwise the site is AMBIGUOUS_CALLEE: reported and counted, never guessed. The same
# rule filters a same-named callee's PRODUCTION call sites: a site whose receiver types to a
# different class is not evidence for this one, and a site whose receiver cannot be typed
# is not evidence either. VB is case-insensitive, so every name comparison here is too.
# ---------------------------------------------------------------------------------------
$methodDeclRe = '(?i)^(?:(?:Public|Private|Friend|Protected|Shared|Overrides|Overridable|NotOverridable|MustOverride|Overloads|Shadows|Async|Iterator|Static|Partial)\s+)*(?:Sub|Function)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\('
$typeDeclRe = '(?i)^(?:(?:Public|Private|Friend|Protected|Partial|NotInheritable|MustInherit|Shadows|Shared)\s+)*(Class|Module|Structure|Interface)\s+([A-Za-z_][A-Za-z0-9_]*)'
$typeEndRe = '(?i)^End\s+(Class|Module|Structure|Interface)\b'

# Innermost enclosing Class/Module/Structure/Interface name per line, cached per file.
$typeAtCache = @{}
# Unary comma on BOTH returns (second-reader sweep RT-U4, 2026-09-24 UTC): a one-line file
# gives a one-element string[], which a bare `return` unwraps to a [string]; `$typeAt[$i]`
# would then index its CHARACTERS. Every caller assigns directly (`$typeAt = Get-FileTypeAt`)
# and never wraps in @(), which would nest the array.
function Get-FileTypeAt($ctx) {
    if ($typeAtCache.ContainsKey($ctx.Path)) { return ,$typeAtCache[$ctx.Path] }
    $typeAt = New-Object string[] $ctx.N
    $tstack = New-Object System.Collections.Generic.Stack[string]
    for ($i = 0; $i -lt $ctx.N; $i++) {
        $t = $ctx.CodeOnly[$i].Trim()
        if ($t -match $typeEndRe) {
            $typeAt[$i] = if ($tstack.Count -gt 0) { $tstack.Peek() } else { $null }
            if ($tstack.Count -gt 0) { [void]$tstack.Pop() }
            continue
        }
        if ($t -match $typeDeclRe) { $tstack.Push($Matches[2]) }
        $typeAt[$i] = if ($tstack.Count -gt 0) { $tstack.Peek() } else { $null }
    }
    $typeAtCache[$ctx.Path] = $typeAt
    return ,$typeAt
}

# The parameter names of the declaration whose statement starts at $lineIdx.
function Get-DeclSignatureAt($ctx, [int]$lineIdx, [string]$name) {
    $stmtEnd = Get-FileStatementEnd $ctx $lineIdx
    $stmtText = Get-FileStatementText $ctx $lineIdx $stmtEnd
    $argTexts = Get-CallArguments $stmtText $name
    if ($null -eq $argTexts) { return $null }
    $names = New-Object System.Collections.Generic.List[string]
    foreach ($pt in $argTexts) {
        if ($pt -match '^(?:ByRef\s+|ByVal\s+|Optional\s+|ParamArray\s+)*([A-Za-z_][A-Za-z0-9_]*)') { $names.Add($Matches[1]) }
    }
    return ,@($names)
}

# Index: lower-cased method name -> every production declaration of it, with its type.
$methodDeclIndex = @{}
$knownTypeNames = @{}
foreach ($pf in $prodFiles) {
    $ctx = Get-FileParseContext $pf
    if ($null -eq $ctx) { continue }
    $typeAt = Get-FileTypeAt $ctx
    for ($i = 0; $i -lt $ctx.N; $i++) {
        $t = $ctx.CodeOnly[$i].Trim()
        if ($t -match $typeDeclRe) { $knownTypeNames[$Matches[2].ToLowerInvariant()] = $Matches[2]; continue }
        if ($t -match $methodDeclRe) {
            $mname = $Matches[1]
            if ($mname -ieq 'New') { continue }   # constructors: Find-ConstructorRange
            $key = $mname.ToLowerInvariant()
            if (-not $methodDeclIndex.ContainsKey($key)) { $methodDeclIndex[$key] = New-Object System.Collections.Generic.List[object] }
            $methodDeclIndex[$key].Add([PSCustomObject]@{ Ctx = $ctx; File = $pf; Line = $i; Name = $mname; TypeName = $typeAt[$i] })
        }
    }
}

# The type of a bare identifier at a line: a local of the enclosing procedure, a parameter
# of it, or a field of the enclosing type. $null unless exactly one distinct type is found
# at the first level that finds any. Unanchored where a statement can sit mid-line (trap 4).
function Resolve-IdentifierType($ctx, [int]$lineIdx, [string]$ident) {
    $e = [regex]::Escape($ident)
    $typeCap = '([A-Za-z_][A-Za-z0-9_.]*)'
    $proc = Get-FileEnclosingProcBroad $ctx $lineIdx
    if ($proc) {
        $localRes = @(
            "(?i)(?:^|[\s:])(?:Dim|Using|Static)\s+$e\s+As\s+(?:New\s+)?$typeCap",
            "(?i)(?:^|[\s:])(?:Dim|Using|Static)\s+$e\s*=\s*New\s+$typeCap",
            "(?i)\bFor\s+Each\s+$e\s+As\s+$typeCap"
        )
        $found = New-Object System.Collections.Generic.HashSet[string]
        for ($k = $proc.Start; $k -le $proc.End; $k++) {
            foreach ($re in $localRes) { if ($ctx.CodeOnly[$k] -match $re) { [void]$found.Add((($Matches[1] -split '\.')[-1]).ToLowerInvariant()) } }
        }
        if ($found.Count -eq 1) { return @($found)[0] }
        if ($found.Count -gt 1) { return $null }
        $declText = Get-FileStatementText $ctx $proc.Start (Get-FileStatementEnd $ctx $proc.Start)
        if ($declText -match "(?i)[\(,]\s*(?:(?:ByVal|ByRef|Optional|ParamArray)\s+)*$e\s+As\s+$typeCap") {
            return (($Matches[1] -split '\.')[-1]).ToLowerInvariant()
        }
    }
    $typeAt = Get-FileTypeAt $ctx
    $here = $typeAt[$lineIdx]
    if (-not $here) { return $null }
    $fieldRes = @(
        "(?i)^(?:Private|Public|Friend|Protected)\s+(?:(?:Shared|ReadOnly|WithEvents|Shadows)\s+)*$e\s+As\s+(?:New\s+)?$typeCap",
        "(?i)^(?:Private|Public|Friend|Protected)\s+(?:(?:Shared|ReadOnly|WithEvents|Shadows)\s+)*$e\s*=\s*New\s+$typeCap"
    )
    $ffound = New-Object System.Collections.Generic.HashSet[string]
    for ($k = 0; $k -lt $ctx.N; $k++) {
        if ($typeAt[$k] -ne $here) { continue }
        $t = $ctx.CodeOnly[$k].Trim()
        foreach ($re in $fieldRes) { if ($t -match $re) { [void]$ffound.Add((($Matches[1] -split '\.')[-1]).ToLowerInvariant()) } }
    }
    if ($ffound.Count -eq 1) { return @($ffound)[0] }
    return $null
}

# The receiver TYPE (lower-cased) of a call written `<qualifier>.<name>(` at $lineIdx, or
# $null when code cannot determine it. '' qualifier = unqualified = the enclosing type.
function Resolve-ReceiverType($ctx, [int]$lineIdx, [string]$qualifier) {
    $typeAt = Get-FileTypeAt $ctx
    $here = $typeAt[$lineIdx]
    $q = if ($null -eq $qualifier) { '' } else { $qualifier.Trim() }
    $segs = @($q -split '\.' | Where-Object { $_ -ne '' })
    if ($segs.Count -gt 0 -and ($segs[0] -ieq 'Me' -or $segs[0] -ieq 'MyClass')) { $segs = @($segs | Select-Object -Skip 1) }
    if ($q -eq '(expr)' -or ($segs.Count -gt 0 -and $segs[0] -ieq 'MyBase')) { return $null }
    foreach ($s in $segs) { if ($s -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { return $null } }
    if ($segs.Count -eq 0) { if ($here) { return $here.ToLowerInvariant() } else { return $null } }
    $varType = Resolve-IdentifierType $ctx $lineIdx $segs[0]
    if ($segs.Count -eq 1) {
        if ($varType) { return $varType }
        if ($knownTypeNames.ContainsKey($segs[0].ToLowerInvariant())) { return $segs[0].ToLowerInvariant() }
        return $null
    }
    # A dotted qualifier: a member chain on a variable is not typed; a namespace-qualified
    # type name (`CeilingAudit.AuditMetrics`) is, by its last segment.
    if ($varType) { return $null }
    $last = $segs[$segs.Count - 1].ToLowerInvariant()
    if ($knownTypeNames.ContainsKey($last)) { return $last }
    return $null
}

# Which declaration a call to $calleeName resolves to, given the receiver type code found.
#   NONE      -- no Sub/Function of the name (then FP-D21's constructor lookup may apply)
#   UNIQUE    -- exactly one declaration: behaviour identical to before revision 4
#   TYPED     -- several, and exactly one lives on $recvType
#   AMBIGUOUS -- several, and code cannot say which. Never guessed.
function Resolve-CalleeDeclaration([string]$calleeName, [string]$recvType) {
    if (-not $calleeName) { return [PSCustomObject]@{ State = 'NONE'; Decl = $null; Count = 0 } }
    $key = $calleeName.ToLowerInvariant()
    if (-not $methodDeclIndex.ContainsKey($key)) { return [PSCustomObject]@{ State = 'NONE'; Decl = $null; Count = 0 } }
    $decls = $methodDeclIndex[$key]
    if ($decls.Count -eq 1) { return [PSCustomObject]@{ State = 'UNIQUE'; Decl = $decls[0]; Count = 1 } }
    if ($recvType) {
        $onType = @($decls | Where-Object { $_.TypeName -and $_.TypeName -ieq $recvType })
        if ($onType.Count -eq 1) { return [PSCustomObject]@{ State = 'TYPED'; Decl = $onType[0]; Count = $decls.Count } }
    }
    return [PSCustomObject]@{ State = 'AMBIGUOUS'; Decl = $null; Count = $decls.Count }
}

# The callee's parameter names for a resolved declaration; for NONE, FP-D21's constructor.
function Get-ResolvedSignature($declRes, [string]$calleeName) {
    if ($declRes.Decl) {
        # Unary comma on the way out: a one-parameter signature must stay an ARRAY, or a
        # later `[0]` indexes the characters of a bare string (see Get-EverShippedSet).
        $sig = Get-DeclSignatureAt $declRes.Decl.Ctx $declRes.Decl.Line $declRes.Decl.Name
        if ($null -eq $sig) { return $null }
        return ,@($sig)
    }
    if ($declRes.State -eq 'NONE') {
        # FP-D21 (revision 3, item 8f): no Sub/Function of this name exists, so the callee
        # may be a CLASS constructed with `New <callee>(...)`. Its signature is its `Sub New`.
        $ctor = Find-ConstructorRange $calleeName
        if ($null -ne $ctor) { return ,@($ctor.SigNames) }
    }
    return $null
}

# FP-D27: keep only the production call sites that are evidence for THIS declaration. A
# name declared once keeps every site, exactly as before. Returns the kept sites and how
# many were dropped because their receiver could not be typed.
function Select-AttributableSites($sites, $declRes) {
    if ($declRes.State -ne 'TYPED') { return [PSCustomObject]@{ Sites = $sites; Untyped = 0 } }
    $kept = New-Object System.Collections.Generic.List[object]
    $untyped = 0
    $allDecls = $methodDeclIndex[$declRes.Decl.Name.ToLowerInvariant()]
    foreach ($s in $sites) {
        $rt = Resolve-ReceiverType $s.Ctx ($s.Line - 1) $s.Qualifier
        # An UNQUALIFIED call types to its enclosing class only if that class declares the
        # name; otherwise VB resolved it to some imported module's member, which is untyped.
        if ($rt -and (Test-UnqualifiedQualifier $s.Qualifier)) {
            if (-not @($allDecls | Where-Object { $_.TypeName -and $_.TypeName -ieq $rt }).Count) { $rt = $null }
        }
        if (-not $rt) { $untyped++; continue }
        if ($rt -ieq $declRes.Decl.TypeName) { $kept.Add($s) }
    }
    return [PSCustomObject]@{ Sites = $kept; Untyped = $untyped }
}
function Test-UnqualifiedQualifier([string]$qualifier) {
    if ($null -eq $qualifier) { return $false }
    $segs = @($qualifier.Trim() -split '\.' | Where-Object { $_ -ne '' -and $_ -ine 'Me' -and $_ -ine 'MyClass' })
    return ($qualifier.Trim() -ne '(expr)' -and $segs.Count -eq 0)
}

# FP-D21 (revision 3, item 8f): the ONE instance constructor of production class
# $className -- its file context, body range and declared parameter names. $null unless
# exactly one non-Shared `Sub New` is found across every block declaring that class
# (Partial classes included), so an overloaded constructor is reported unresolved, never
# guessed. Nested classes are skipped by depth.
$ctorRangeCache = @{}
function Find-ConstructorRange([string]$className) {
    if ($ctorRangeCache.ContainsKey($className)) { return $ctorRangeCache[$className] }
    $classRe = "(?i)^(?:(?:Public|Friend|Private|Protected|Partial|NotInheritable|MustInherit)\s+)*Class\s+$([regex]::Escape($className))\b"
    $anyClassRe = '(?i)^(?:(?:Public|Friend|Private|Protected|Partial|NotInheritable|MustInherit|Shadows)\s+)*Class\s+[A-Za-z_]'
    $ctorRe = '(?i)^(?:(?:Public|Friend|Private|Protected|Overloads)\s+)*Sub\s+New\s*\('
    $found = New-Object System.Collections.Generic.List[object]
    foreach ($pf in $prodFiles) {
        $ctx = Get-FileParseContext $pf
        if ($null -eq $ctx) { continue }
        for ($i = 0; $i -lt $ctx.N; $i++) {
            if ($ctx.CodeOnly[$i].Trim() -notmatch $classRe) { continue }
            $depth = 1
            for ($k = $i + 1; $k -lt $ctx.N -and $depth -gt 0; $k++) {
                $t = $ctx.CodeOnly[$k].Trim()
                if ($t -match '(?i)^End\s+Class\b') { $depth--; continue }
                if ($t -match $anyClassRe) { $depth++; continue }
                if ($depth -eq 1 -and $t -match $ctorRe) { $found.Add([PSCustomObject]@{ Ctx = $ctx; File = $pf; Line = $k }) }
            }
        }
    }
    $result = $null
    if ($found.Count -eq 1) {
        $f = $found[0]
        $stmtEnd = Get-FileStatementEnd $f.Ctx $f.Line
        $stmtText = Get-FileStatementText $f.Ctx $f.Line $stmtEnd
        $argTexts = Get-CallArguments $stmtText 'New'
        $proc = Get-FileEnclosingProc $f.Ctx $f.Line
        if ($null -ne $argTexts -and $null -ne $proc) {
            $names = New-Object System.Collections.Generic.List[string]
            foreach ($pt in $argTexts) {
                if ($pt -match '^(?:ByRef\s+|ByVal\s+|Optional\s+|ParamArray\s+)*([A-Za-z_][A-Za-z0-9_]*)') { $names.Add($Matches[1]) }
            }
            $result = [PSCustomObject]@{ Ctx = $f.Ctx; File = $f.File; Name = "$className.New"; Start = $proc.Start; End = $proc.End; SigNames = @($names) }
        }
    }
    $ctorRangeCache[$className] = $result
    return $result
}

# Find every production CALL (not the declaration) of $calleeName under Core/UI/analysis/
# root. Excludes the method's own multi-line declaration by checking the statement-start
# line against $declRe2 first.
function Get-ProductionCallSites([string]$calleeName) {
    $found = New-Object System.Collections.Generic.List[object]
    # See the identical comment on Get-CallArguments's $callRe above -- must NOT exclude a
    # preceding '.', or every module-qualified production call site is missed.
    $callRe = "(?<![A-Za-z0-9_])$([regex]::Escape($calleeName))\s*\("
    $declRe2 = "^(?:Private|Public|Friend|Protected)?\s*(?:Shared\s+)?(?:Sub|Function)\s+$([regex]::Escape($calleeName))\s*\("
    foreach ($pf in $prodFiles) {
        $ctx = Get-FileParseContext $pf
        if ($null -eq $ctx) { continue }
        for ($i = 0; $i -lt $ctx.N; $i++) {
            if ($ctx.CodeOnly[$i] -notmatch $callRe) { continue }
            $stmtStart = Get-FileStatementStart $ctx $i
            $startTrim = $ctx.CodeOnly[$stmtStart].TrimStart()
            if ($startTrim -match $declRe2) { continue }
            $stmtEnd = Get-FileStatementEnd $ctx $stmtStart
            $stmtText = Get-FileStatementText $ctx $stmtStart $stmtEnd
            $proc = Get-FileEnclosingProc $ctx $stmtStart
            # FP-D27: the receiver qualifier of the FIRST call to the name in the statement
            # (the same one Get-CallArguments parses). '(expr)' when the receiver is an
            # expression such as `GetThing().Snapshot(` -- never read as unqualified.
            $qualifier = '(expr)'
            $cm = [regex]::Match($stmtText, "(?i)(?<![A-Za-z0-9_])$([regex]::Escape($calleeName))\s*\(")
            if ($cm.Success) {
                $pre = $stmtText.Substring(0, $cm.Index)
                $qm = [regex]::Match($pre, '(?:^|[^A-Za-z0-9_.)\]])((?:[A-Za-z_][A-Za-z0-9_]*\.)*)$')
                if ($qm.Success) { $qualifier = $qm.Groups[1].Value.TrimEnd('.') }
            }
            $found.Add([PSCustomObject]@{
                File = $pf; Line = $stmtStart + 1; StmtText = $stmtText; Ctx = $ctx
                Qualifier = $qualifier
                EnclosingProcStart = if ($proc) { $proc.Start } else { 0 }
                EnclosingProcEnd   = if ($proc) { $proc.End } else { ($ctx.N - 1) }
                # FP-D17 (revision 2): the forwarding hop needs the enclosing method's NAME,
                # not just its line range.
                EnclosingProcName  = if ($proc) { $proc.Name } else { $null }
            })
        }
    }
    return ,$found
}

# One-hop alias resolution: `Dim ident [As T] = cfg.X.Y` inside the production call's
# enclosing Sub/Function body (FP-D12: exactly one hop, never chased further).
function Find-LocalAliasCfgAssignment($site, [string]$ident) {
    $ctx = $site.Ctx
    $aliasRe = "(?i)^\s*Dim\s+$([regex]::Escape($ident))\b(?:\s+As\s+[A-Za-z0-9_.\(\)]+)?\s*=\s*(cfg\.[A-Za-z0-9_.]+)"
    for ($k = $site.EnclosingProcStart; $k -le $site.EnclosingProcEnd; $k++) {
        if ($ctx.CodeOnly[$k] -match $aliasRe) { return $Matches[1] }
    }
    return $null
}

# ---- revision 2 helpers (FP-D16 / FP-D17 / FP-D18 / FP-D20) ----------------------------

# The initialiser TEXT of a local `Dim ident [As T] = <expr>` inside the production call's
# enclosing body, or $null when the identifier is not a local of that body (then it is a
# parameter, a field, a Const or a member of another object -- FP-D20's "did not complete"
# arm). Unanchored (trap 4), so a `Dim` reached through any single-line form still matches.
function Find-LocalDimInitialiser($site, [string]$ident) {
    $ctx = $site.Ctx
    $dimRe = "(?i)(?:^|[\s:])Dim\s+$([regex]::Escape($ident))\b(?:\s+As\s+[A-Za-z0-9_.\(\)]+)?\s*=\s*(.+)$"
    for ($k = $site.EnclosingProcStart; $k -le $site.EnclosingProcEnd; $k++) {
        if ($ctx.CodeOnly[$k] -match $dimRe) { return $Matches[1].Trim() }
    }
    return $null
}

# FP-D20: is this expression a TERMINAL non-cfg value -- a literal, a boolean, Nothing or a
# New? Then the search completed and the answer is genuinely negative. An identifier, a
# member chain or a call means the trail continues and the search did NOT complete.
function Test-TerminalNonCfgExpression([string]$expr) {
    if ([string]::IsNullOrWhiteSpace($expr)) { return $false }
    $t = $expr.Trim()
    if ($t -match '^-?\d') { return $true }
    if ($t.StartsWith('"')) { return $true }
    if ($t -match '^(?i)(True|False|Nothing)\s*$') { return $true }
    if ($t -match '^(?i)New\s') { return $true }
    return $false
}

# [revision 4] Find-ProdProcRange (the resolver body found by BARE NAME, first in file
# order) was removed by FP-D27. Find-ResolverReturnCfgPath now resolves by declaration.

# FP-D27: procedure ranges built with the BROAD declaration form ($methodDeclRe), which
# also admits `Async`, `Overrides`, `Overloads` and the like. Get-FileEnclosingProc keeps
# its narrower pre-revision form on purpose -- widening it would move EnclosingSub on some
# sites and so change item IDs. Used only by the revision-4 receiver typing and resolver
# lookup. Nested non-method blocks are not tracked; methods do not nest in VB.
$fileProcRangesBroadCache = @{}
function Get-FileEnclosingProcBroad($ctx, [int]$lidx) {
    if (-not $fileProcRangesBroadCache.ContainsKey($ctx.Path)) {
        $stack3 = New-Object System.Collections.Generic.Stack[object]
        $ranges3 = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $ctx.N; $i++) {
            $t = $ctx.CodeOnly[$i].Trim()
            if ($t -match $methodDeclRe -and $t -notmatch '(?i)\bMustOverride\b') {
                # Methods never nest, so an unclosed frame here is a bodiless declaration (an
                # Interface member): discard it rather than let it swallow the next End.
                if ($stack3.Count -gt 0) { [void]$stack3.Pop() }
                $stack3.Push([PSCustomObject]@{ Name = $Matches[1]; Start = $i })
            } elseif ($t -match $endRe) {
                if ($stack3.Count -gt 0) {
                    $top = $stack3.Pop()
                    $ranges3.Add([PSCustomObject]@{ Name = $top.Name; Start = $top.Start; End = $i })
                }
            }
        }
        $fileProcRangesBroadCache[$ctx.Path] = $ranges3
    }
    foreach ($r in $fileProcRangesBroadCache[$ctx.Path]) {
        if ($r.Start -le $lidx -and $lidx -le $r.End) { return $r }
    }
    return $null
}

# FP-D18: the argument is a local assigned from a resolver call (`Dim x = Resolve...(cfg,
# hour)`). Open that method and take its cfg-rooted Return expression -- but ONLY when
# there is exactly one distinct one, so an ambiguous resolver is reported unresolved
# rather than guessed. The Return scan is UNANCHORED (trap 4): `If o IsNot Nothing
# AndAlso ... Then Return o.NormWindowSec.Value` is the commonest form in this codebase and
# a `^\s*Return` scan would miss it -- and that arm is exactly what OtherArms counts.
function Find-ResolverReturnCfgPath($site, [string]$ident) {
    $init = Find-LocalDimInitialiser $site $ident
    if (-not $init) { return $null }
    if ($init -notmatch '^([A-Za-z_][A-Za-z0-9_.]*)\s*\(') { return $null }
    $segs = $Matches[1] -split '\.'
    $callName = $segs[$segs.Length - 1]
    # FP-D27: the resolver is found by DECLARATION, typed by its qualifier when its name is
    # declared more than once -- no longer the first body of that name in file order.
    $rqual = if ($segs.Length -gt 1) { ($segs[0..($segs.Length - 2)] -join '.') } else { '' }
    $rdr = Resolve-CalleeDeclaration $callName (Resolve-ReceiverType $site.Ctx ($site.Line - 1) $rqual)
    if (-not $rdr.Decl) { return $null }
    $rproc = Get-FileEnclosingProcBroad $rdr.Decl.Ctx $rdr.Decl.Line
    if ($null -eq $rproc -or $rproc.Start -ne $rdr.Decl.Line) { return $null }
    $rng = [PSCustomObject]@{ Ctx = $rdr.Decl.Ctx; File = $rdr.Decl.File; Name = $callName; Start = $rproc.Start; End = $rproc.End }
    $paths = New-Object System.Collections.Generic.List[string]
    $otherArms = 0
    for ($k = $rng.Start; $k -le $rng.End; $k++) {
        foreach ($m in [regex]::Matches($rng.Ctx.CodeOnly[$k], '(?i)(?<![A-Za-z0-9_])Return\s+([A-Za-z_][A-Za-z0-9_.]*)')) {
            $rexpr = $m.Groups[1].Value
            if ($rexpr -match '^(?i)cfg\.') {
                if (-not $paths.Contains($rexpr)) { [void]$paths.Add($rexpr) }
            } else {
                $otherArms++
            }
        }
    }
    if ($paths.Count -ne 1) { return $null }
    return [PSCustomObject]@{ CfgExpr = $paths[0]; Via = "$($rng.File):$callName"; OtherArms = $otherArms }
}

# FP-D17: is this production call site a PURE POSITIONAL PASS-THROUGH of its enclosing
# method's own parameters? All three conditions must hold or the hop is refused -- see the
# header for the MarketState.GetOfiAverage near-miss this rejects. Returns the wrapper --
# its name, its signature and its declaration -- never a guess.
# FP-D27 (revision 4): the wrapper's signature is read from ITS OWN declaration (the
# enclosing procedure's first line), not from the first method of that name in file order,
# and the wrapper's declaration travels with it so its production call sites can be
# filtered by receiver type when its name is declared more than once.
function Get-ForwardingWrapper($site, [string]$calleeName, $sigNames) {
    if (-not $site.EnclosingProcName) { return $null }
    if ($site.EnclosingProcName -ieq $calleeName) { return $null }
    $argTexts = Get-CallArguments $site.StmtText $calleeName
    if ($null -eq $argTexts) { return $null }
    if ($argTexts.Count -ne $sigNames.Count) { return $null }
    for ($i = 0; $i -lt $argTexts.Count; $i++) {
        $a = $argTexts[$i].Trim()
        if ($a -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { return $null }
        if ($a.ToLowerInvariant() -ne $sigNames[$i].ToLowerInvariant()) { return $null }
    }
    $wrapKey = $site.EnclosingProcName.ToLowerInvariant()
    if (-not $methodDeclIndex.ContainsKey($wrapKey)) { return $null }
    $wrapAll = $methodDeclIndex[$wrapKey]
    $wrapDecl = @($wrapAll | Where-Object { $_.File -eq $site.File -and $_.Line -eq $site.EnclosingProcStart })
    if ($wrapDecl.Count -ne 1) { return $null }
    $wrapSig = Get-DeclSignatureAt $wrapDecl[0].Ctx $wrapDecl[0].Line $wrapDecl[0].Name
    if ($null -eq $wrapSig -or $wrapSig.Count -eq 0) { return $null }
    $wrapLower = @($wrapSig | ForEach-Object { $_.ToLowerInvariant() })
    foreach ($a in $argTexts) {
        if ($wrapLower -notcontains $a.Trim().ToLowerInvariant()) { return $null }
    }
    $wrapState = if ($wrapAll.Count -eq 1) { 'UNIQUE' } elseif ($wrapDecl[0].TypeName) { 'TYPED' } else { 'AMBIGUOUS' }
    if ($wrapState -eq 'AMBIGUOUS') { return $null }
    return [PSCustomObject]@{
        Name = $site.EnclosingProcName; Sig = $wrapSig
        DeclRes = [PSCustomObject]@{ State = $wrapState; Decl = $wrapDecl[0]; Count = $wrapAll.Count }
    }
}

# FP-D16: the callee is a FIXTURE-LOCAL cfg builder -- its body assigns the parameter into
# a cfg path. Read from -SourceFile's own parse (never a production file), and the derived
# path must hit a real settings leaf or the shape is refused.
function Resolve-FixtureLocalCfgBuilder([string]$calleeName, [string]$paramName) {
    $r = $ranges | Where-Object { $_.Name -eq $calleeName } | Select-Object -First 1
    if ($null -eq $r) { return $null }
    $assignRe = "(?i)(?:^|[\s:])([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)+)\s*=\s*$([regex]::Escape($paramName))\s*$"
    for ($k = $r.Start; $k -le $r.End; $k++) {
        if ($codeOnly[$k] -match $assignRe) {
            $lhs = $Matches[1]
            $segs = $lhs -split '\.'
            if ($segs.Count -lt 2) { continue }
            $pathAfterRoot = ($segs[1..($segs.Count - 1)]) -join '.'
            $key = Resolve-SettingsLeafKey $pathAfterRoot
            if ($key) {
                return [PSCustomObject]@{
                    Class = 'FIXTURE_LOCAL_CFG_BUILDER'; ResolvedKey = $key; Shape = 'fixture_cfg_builder'
                    ProdFile = $SourceFile; ProdLine = ($k + 1); ResolvedExpr = $lhs
                }
            }
        }
    }
    return $null
}

# FP-D21 (revision 3, item 8f): the CALLEE'S OWN DEFAULT-FALLBACK BODY. Production omits
# the argument, so the value it runs on is whatever the callee substitutes for the
# sentinel default -- read from the callee's body, never guessed. Accepted forms, on the
# body's statement text (continuation lines joined), case-insensitive:
#   If(<p> <cond>, <p>, <cfgExpr>)      If(<p> <cond>, <cfgExpr>, <p>)
#   If <p> <cond> Then <p> = <cfgExpr>
# where <cfgExpr> is rooted at `cfg.` or `SettingsLoader.Current.`. Resolves ONLY when
# exactly one distinct cfg expression is found, and the body is a constructor (via
# Find-ConstructorRange) or a method whose name is declared EXACTLY ONCE in the production
# set -- `Snapshot` and `Fold` are declared on four classes, and a by-name lookup would
# read the wrong body (the FP-D17 hazard again).
function Find-UniqueProdProcRange([string]$procName) {
    $declRe3 = "^(?:Private|Public|Friend|Protected)?\s*(?:Shared\s+)?(?:Sub|Function)\s+$([regex]::Escape($procName))\s*\("
    $hits = New-Object System.Collections.Generic.List[object]
    foreach ($pf in $prodFiles) {
        $ctx = Get-FileParseContext $pf
        if ($null -eq $ctx) { continue }
        for ($i = 0; $i -lt $ctx.N; $i++) {
            if ($ctx.CodeOnly[$i].TrimStart() -match $declRe3) {
                $proc = Get-FileEnclosingProc $ctx $i
                if ($proc) { $hits.Add([PSCustomObject]@{ Ctx = $ctx; File = $pf; Name = $procName; Start = $proc.Start; End = $proc.End }) }
            }
        }
    }
    if ($hits.Count -eq 1) { return $hits[0] }
    return $null
}
function Resolve-DefaultFallbackBody([string]$calleeName, [string]$paramName) {
    $rng = Find-UniqueProdProcRange $calleeName
    if ($null -eq $rng) { $rng = Find-ConstructorRange $calleeName }
    if ($null -eq $rng) { return $null }
    $p = [regex]::Escape($paramName)
    $root = '((?:cfg|SettingsLoader\.Current)\.[A-Za-z0-9_.]+)'
    $forms = @(
        "(?i)(?<![A-Za-z0-9_])If\(\s*$p\b[^,]*,\s*$p\s*,\s*$root\s*\)",
        "(?i)(?<![A-Za-z0-9_])If\(\s*$p\b[^,]*,\s*$root\s*,\s*$p\s*\)",
        "(?i)(?<![A-Za-z0-9_])If\s+$p\b.*?\bThen\s+$p\s*=\s*$root"
    )
    $exprs = New-Object System.Collections.Generic.List[string]
    $firstLine = $null
    $ctx = $rng.Ctx
    $k = $rng.Start
    while ($k -le $rng.End) {
        $stmtEnd = Get-FileStatementEnd $ctx $k
        if ($stmtEnd -gt $rng.End) { $stmtEnd = $rng.End }
        $text = Get-FileStatementText $ctx $k $stmtEnd
        foreach ($f in $forms) {
            foreach ($m in [regex]::Matches($text, $f)) {
                $e = $m.Groups[1].Value
                if (-not $exprs.Contains($e)) { [void]$exprs.Add($e); if ($null -eq $firstLine) { $firstLine = $k + 1 } }
            }
        }
        $k = $stmtEnd + 1
    }
    if ($exprs.Count -ne 1) { return $null }
    $pathAfterRoot = $exprs[0] -replace '(?i)^(?:cfg|SettingsLoader\.Current)\.', ''
    $key = Resolve-SettingsLeafKey $pathAfterRoot
    if (-not $key) { return $null }
    return [PSCustomObject]@{
        Class = 'DEFAULT_FALLBACK_BODY'; ResolvedKey = $key; Shape = "default_fallback($($rng.Name))"
        ProdFile = $rng.File; ProdLine = $firstLine; ResolvedExpr = $exprs[0]
    }
}

# Per-segment snake-case normalisation of a dotted settings path, so a VB PascalCase
# property-access chain (cfg.Indicators.OBV.TrendGate) and the actual mixed-case JSON path
# (indicators.OBV.trend_gate) compare equal without needing a hand-kept casing table.
# Verified against indicators.OBV.trend_gate, indicators.TFI.window_size/threshold, and
# indicators.OFI.buy_dominant_ratio before this function was written (see report).
function Get-NormalizedSettingsPath([string]$dottedPath) {
    $segs = $dottedPath -split '\.'
    $norm = foreach ($s in $segs) { ConvertTo-SnakeCase $s }
    return ($norm -join '.')
}
$normalizedHeadFlat = @{}
foreach ($k in $headFlat.Keys) {
    $nk = Get-NormalizedSettingsPath $k
    if (-not $normalizedHeadFlat.ContainsKey($nk)) { $normalizedHeadFlat[$nk] = $k }
}

# FP-D19 (revision 2): POCO->JSON segment aliases, read from the POCO's own
# <JsonPropertyName> attributes. NOT a hand-kept table -- the attribute IS the mapping, so
# this stays self-correcting the way section 7.3 demands of the whole derivation. A
# missing or unreadable POCO file leaves the map empty and every lookup below falls back
# to the plain snake-case path exactly as it did before revision 2.
$pocoAliasFile = 'Core/Settings/EngineSettings.vb'
$pocoSegAlias = @{}
$pocoAliasFull = Resolve-RepoPath $pocoAliasFile
if (Test-Path $pocoAliasFull) {
    foreach ($pl in [string[]](Get-Content -Encoding UTF8 -Path $pocoAliasFull)) {
        if ($pl -match '<JsonPropertyName\("([^"]+)"\)>\s*(?:Public|Friend)\s+Property\s+([A-Za-z_][A-Za-z0-9_]*)') {
            $jsonSeg = ConvertTo-SnakeCase $Matches[1]
            $propSeg = ConvertTo-SnakeCase $Matches[2]
            if ($propSeg -ne $jsonSeg) {
                if (-not $pocoSegAlias.ContainsKey($propSeg)) {
                    $pocoSegAlias[$propSeg] = New-Object System.Collections.Generic.List[string]
                }
                if (-not $pocoSegAlias[$propSeg].Contains($jsonSeg)) { [void]$pocoSegAlias[$propSeg].Add($jsonSeg) }
            }
        }
    }
}
$pocoAliasSegments = $pocoSegAlias.Count

# Resolve a cfg-rooted dotted path (already stripped of its `cfg.` root) to a REAL
# settings.json leaf key. The plain per-segment snake-case form is tried FIRST, so nothing
# that resolved before revision 2 can change; only then are FP-D19's aliases substituted,
# segment by segment, bounded and in order, so the answer stays deterministic. $null when
# nothing matches -- the membership check is what validates every derived shape below.
$MAX_ALIAS_COMBINATIONS = 64
function Resolve-SettingsLeafKey([string]$pathAfterCfg) {
    if ([string]::IsNullOrWhiteSpace($pathAfterCfg)) { return $null }
    $plain = Get-NormalizedSettingsPath $pathAfterCfg
    if ($normalizedHeadFlat.ContainsKey($plain)) { return $normalizedHeadFlat[$plain] }
    if ($pocoSegAlias.Count -eq 0) { return $null }
    $candidates = New-Object System.Collections.Generic.List[string]
    [void]$candidates.Add('')
    foreach ($s in @($plain -split '\.')) {
        $opts = New-Object System.Collections.Generic.List[string]
        [void]$opts.Add($s)
        if ($pocoSegAlias.ContainsKey($s)) { foreach ($a in $pocoSegAlias[$s]) { [void]$opts.Add($a) } }
        $next = New-Object System.Collections.Generic.List[string]
        foreach ($c in $candidates) {
            foreach ($o in $opts) {
                if ($next.Count -ge $MAX_ALIAS_COMBINATIONS) { break }
                if ($c -eq '') { [void]$next.Add($o) } else { [void]$next.Add("$c.$o") }
            }
        }
        $candidates = $next
    }
    foreach ($c in $candidates) {
        if ($normalizedHeadFlat.ContainsKey($c)) { return $normalizedHeadFlat[$c] }
    }
    return $null
}

# The FP-Q3 resolver proper. Returns a PSCustomObject with Class/ResolvedKey/Shape/
# ProdFile/ProdLine/ResolvedExpr. Class is one of:
#   NAMED / ALIASED / POSITIONAL          -- resolved to a cfg path (section 7.3's 3 shapes)
#   NO_PRODUCTION_CALL_SITE               -- the callee is not called anywhere under
#                                             Core/UI/analysis/root (its own class, section
#                                             7.3's closing warning / 7.5 item 2)
#   NOT_CFG_SOURCED                       -- production call found, parameter resolved, but
#                                             the value isn't a cfg.-rooted expression (and
#                                             no alias hop found one either)
#   CFG_PATH_NOT_FOUND                    -- resolved to a cfg.-rooted expression that does
#                                             not match any HEAD settings.json leaf
#   NO_SIGNATURE_FOUND / CALL_PARSE_FAILED / PARAM_NOT_IN_SIGNATURE /
#   PARAM_NOT_PASSED_AT_CALL_SITE         -- mechanical parse failures, reported not guessed
# ---------------------------------------------------------------------------------------
# [2026-09-22] FP-D13 SUPERSEDED -- trader-approved. It read `$site = $sites[0]`, "first
# found wins", and that is WRONG whenever the first-found call is an INTERNAL FORWARDING
# call rather than a cfg source.
#
# MEASURED INSTANCE, not hypothetical. ClassifySpread has three production call sites.
# The first found, Core/Indicators_OrderFlow.vb:663, sits inside ApplySpread and forwards
# ApplySpread's OWN `wide`/`tight` parameters -- so it resolves NOT_CFG_SOURCED and the
# lookup stopped there. But tools/ops/SwingFallbackRead/MediumTierRescore.vb:767 passes
# `cfg.Indicators.Spread.WideThresholdBps` positionally and resolves cleanly. Under
# FP-D13 both wideThresholdBps and tightThresholdBps silently dropped out of scope,
# despite indicators.spread.wide_threshold_bps being a real settings key.
#
# NOW: try EVERY production call site and return the FIRST that resolves to a real
# settings key. A forwarding call can no longer mask a cfg-sourced one. When NO site
# resolves, the FIRST site's failure class is returned unchanged, so the diagnostic for a
# genuinely non-cfg-sourced parameter reads exactly as it did before.
#
# ⚠ Ordering is still deterministic (file-list then line), so the answer is stable; what
# changed is that a non-resolving site no longer ends the search. The build's own note
# that FP-D13 was "never exercised against a real multi-site disagreement" was wrong --
# it was exercised, and it lost.
# ---------------------------------------------------------------------------------------
function Resolve-FpQ3Mapping([string]$calleeName, [string]$paramName, [string]$recvType) {
    if (-not $calleeName) {
        return [PSCustomObject]@{ Class = 'NO_CALLEE_IDENTIFIED'; ResolvedKey = $null; Shape = $null; ProdFile = $null; ProdLine = $null; ResolvedExpr = $null }
    }
    # FP-D27 (revision 4): which declaration is this call? A name declared once resolves
    # exactly as before. A name declared on several types needs the receiver type, and
    # without it the site is AMBIGUOUS_CALLEE -- reported, never guessed.
    $declRes = Resolve-CalleeDeclaration $calleeName $recvType
    if ($declRes.State -eq 'AMBIGUOUS') {
        return [PSCustomObject]@{ Class = 'AMBIGUOUS_CALLEE'; ResolvedKey = $null; Shape = "declared_$($declRes.Count)x_receiver_untyped"; ProdFile = $null; ProdLine = $null; ResolvedExpr = $null }
    }
    $sigNames = Get-ResolvedSignature $declRes $calleeName
    $sites = Get-ProductionCallSites $calleeName
    $attr = Select-AttributableSites $sites $declRes
    if ($attr.Sites.Count -eq 0 -and $attr.Untyped -gt 0) {
        # Production calls this name, but none of them can be attributed to the declaration
        # the fixture calls. That is not "no production call site"; it is not knowable here.
        return [PSCustomObject]@{ Class = 'AMBIGUOUS_CALLEE'; ResolvedKey = $null; Shape = "production_sites_untyped_$($attr.Untyped)"; ProdFile = $null; ProdLine = $null; ResolvedExpr = $null }
    }
    $sites = $attr.Sites
    if ($sites.Count -eq 0) {
        # GAP A (FP-D16, revision 2): no production call site, so the callee may be a
        # FIXTURE-LOCAL cfg builder whose body assigns the parameter into a cfg path.
        $fx = Resolve-FixtureLocalCfgBuilder $calleeName $paramName
        if ($fx) { return $fx }
        return [PSCustomObject]@{ Class = 'NO_PRODUCTION_CALL_SITE'; ResolvedKey = $null; Shape = $null; ProdFile = $null; ProdLine = $null; ResolvedExpr = $null }
    }
    $firstFailure = $null
    $anyOmitted = $false
    foreach ($s in $sites) {
        $attempt = Resolve-FpQ3MappingAtSite $s $calleeName $paramName $sigNames
        if ($attempt.ResolvedKey) { return $attempt }
        if ($null -eq $firstFailure) { $firstFailure = $attempt }
        if ($attempt.Class -eq 'PARAM_NOT_PASSED_AT_CALL_SITE') { $anyOmitted = $true }
    }
    # GAP B (FP-D17, revision 2): every direct site failed. Some of them may be FORWARDING
    # sites -- a wrapper passing its own parameters straight through -- in which case the
    # cfg evidence lives at the WRAPPER's production call sites, one hop out. The
    # pure-pass-through test inside Get-ForwardingWrapper is what keeps this from hopping
    # across two same-named methods on different classes; FP-D27 (revision 4) adds that
    # the direct sites were already filtered to this declaration's receiver type, and the
    # wrapper's own sites are filtered the same way when its name is declared twice.
    $sigNamesForHop = $sigNames
    if ($null -ne $sigNamesForHop -and $sigNamesForHop.Count -gt 0) {
        $hopped = New-Object System.Collections.Generic.HashSet[string]
        foreach ($s in $sites) {
            $wrapperObj = Get-ForwardingWrapper $s $calleeName $sigNamesForHop
            if (-not $wrapperObj) { continue }
            $wrapper = $wrapperObj.Name
            if (-not $hopped.Add($wrapper.ToLowerInvariant())) { continue }
            $wrapSites = (Select-AttributableSites (Get-ProductionCallSites $wrapper) $wrapperObj.DeclRes).Sites
            foreach ($hs in $wrapSites) {
                # ONE hop: the per-site resolver, never the hopping wrapper, so this cannot
                # recurse.
                $att = Resolve-FpQ3MappingAtSite $hs $wrapper $paramName $wrapperObj.Sig
                if ($att.ResolvedKey) {
                    return [PSCustomObject]@{
                        Class = 'WRAPPER_FORWARDED'; ResolvedKey = $att.ResolvedKey
                        Shape = "wrapper($wrapper)->$($att.Class)"
                        ProdFile = $att.ProdFile; ProdLine = $att.ProdLine; ResolvedExpr = $att.ResolvedExpr
                    }
                }
            }
        }
    }
    # FP-D21 (revision 3, item 8f): at least one production site OMITS the argument, so that
    # path runs on the callee's own default-fallback value. Tried last, after the direct
    # sites and the wrapper hop have all failed, so nothing that resolved before can change.
    if ($anyOmitted) {
        $fb = Resolve-DefaultFallbackBody $calleeName $paramName
        if ($fb) { return $fb }
    }
    return $firstFailure
}

# [2026-09-22] The PER-SITE resolver. Was the whole of Resolve-FpQ3Mapping, which took
# $sites[0] under FP-D13 ("first found wins"). That is now superseded -- see the wrapper
# immediately above for why and for the measured instance. This function's body is
# UNCHANGED apart from receiving $site instead of choosing it -- and, since FP-D27
# (revision 4), receiving the callee's signature from its RESOLVED declaration instead of
# looking the first declaration of that name up itself.
function Resolve-FpQ3MappingAtSite($site, [string]$calleeName, [string]$paramName, $sigNames) {
    if ($null -eq $sigNames -or $sigNames.Count -eq 0) {
        return [PSCustomObject]@{ Class = 'NO_SIGNATURE_FOUND'; ResolvedKey = $null; Shape = $null; ProdFile = $site.File; ProdLine = $site.Line; ResolvedExpr = $null }
    }
    $argTexts = Get-CallArguments $site.StmtText $calleeName
    if ($null -eq $argTexts) {
        return [PSCustomObject]@{ Class = 'CALL_PARSE_FAILED'; ResolvedKey = $null; Shape = $null; ProdFile = $site.File; ProdLine = $site.Line; ResolvedExpr = $null }
    }
    $namedMap = @{}
    $positional = New-Object System.Collections.Generic.List[string]
    foreach ($a in $argTexts) {
        if ($a -match '^([A-Za-z_][A-Za-z0-9_]*)\s*:=\s*(.+)$') { $namedMap[$Matches[1].ToLowerInvariant()] = $Matches[2].Trim() }
        else { [void]$positional.Add($a) }
    }
    $targetKey = $paramName.ToLowerInvariant()
    $targetIdx = -1
    for ($i = 0; $i -lt $sigNames.Count; $i++) { if ($sigNames[$i].ToLowerInvariant() -eq $targetKey) { $targetIdx = $i; break } }
    if ($targetIdx -lt 0) {
        return [PSCustomObject]@{ Class = 'PARAM_NOT_IN_SIGNATURE'; ResolvedKey = $null; Shape = $null; ProdFile = $site.File; ProdLine = $site.Line; ResolvedExpr = $null }
    }
    $shape = $null; $expr = $null
    if ($namedMap.ContainsKey($targetKey)) { $shape = 'named'; $expr = $namedMap[$targetKey] }
    elseif ($targetIdx -lt $positional.Count) { $shape = 'positional'; $expr = $positional[$targetIdx] }
    else {
        return [PSCustomObject]@{ Class = 'PARAM_NOT_PASSED_AT_CALL_SITE'; ResolvedKey = $null; Shape = $null; ProdFile = $site.File; ProdLine = $site.Line; ResolvedExpr = $null }
    }
    $exprTrim = $expr.Trim()
    $baseIdent = $null; $rest = $null
    if ($exprTrim -match '^([A-Za-z_][A-Za-z0-9_]*)(\.(.+))?$') {
        $baseIdent = $Matches[1]
        $rest = $Matches[3]
    }
    $fullCfgExpr = $null
    $aliasHop = $false
    $resolverHop = $false
    $resolverVia = $null
    if ($baseIdent -and $baseIdent.ToLowerInvariant() -eq 'cfg') {
        $fullCfgExpr = $exprTrim
    } elseif ($baseIdent) {
        $aliasVal = Find-LocalAliasCfgAssignment $site $baseIdent
        if ($aliasVal) {
            $aliasHop = $true
            $fullCfgExpr = if ($rest) { "$aliasVal.$rest" } else { $aliasVal }
        } else {
            # FP-D18 (revision 2): the local is assigned from a resolver call, not from cfg
            # directly. Tried only after the direct read and the one alias hop have failed,
            # so nothing that resolved before revision 2 can change.
            $rr = Find-ResolverReturnCfgPath $site $baseIdent
            if ($rr) {
                $resolverHop = $true
                $resolverVia = $rr.Via
                $fullCfgExpr = if ($rest) { "$($rr.CfgExpr).$rest" } else { $rr.CfgExpr }
            }
        }
    }
    if (-not $fullCfgExpr) {
        # FP-D20 (revision 2): split the old catch-all. NOT_CFG_SOURCED is now an earned
        # negative; SOURCE_NOT_TRACEABLE says the search did not complete and is NOT a
        # finding.
        $failClass = 'SOURCE_NOT_TRACEABLE'
        if (-not $baseIdent) {
            if (Test-TerminalNonCfgExpression $exprTrim) { $failClass = 'NOT_CFG_SOURCED' }
        } else {
            if (Test-TerminalNonCfgExpression (Find-LocalDimInitialiser $site $baseIdent)) { $failClass = 'NOT_CFG_SOURCED' }
        }
        return [PSCustomObject]@{ Class = $failClass; ResolvedKey = $null; Shape = $shape; ProdFile = $site.File; ProdLine = $site.Line; ResolvedExpr = $exprTrim }
    }
    $pathAfterCfg = $fullCfgExpr -replace '^[Cc][Ff][Gg]\.', ''
    $resolvedKey = Resolve-SettingsLeafKey $pathAfterCfg
    if ($resolvedKey) {
        $class = if ($resolverHop) { 'RESOLVER_RETURN' } elseif ($shape -eq 'positional') { 'POSITIONAL' } elseif ($aliasHop) { 'ALIASED' } else { 'NAMED' }
        $shapeOut = if ($resolverHop) { "$shape via $resolverVia" } else { $shape }
        return [PSCustomObject]@{ Class = $class; ResolvedKey = $resolvedKey; Shape = $shapeOut; ProdFile = $site.File; ProdLine = $site.Line; ResolvedExpr = $fullCfgExpr }
    } else {
        return [PSCustomObject]@{ Class = 'CFG_PATH_NOT_FOUND'; ResolvedKey = $null; Shape = $shape; ProdFile = $site.File; ProdLine = $site.Line; ResolvedExpr = $fullCfgExpr }
    }
}

# FP-D27 (revision 4): the cache key carries the fixture call's RECEIVER TYPE, because two
# calls to the same bare name on different receivers are different methods. $mappingCache
# keeps its old "callee|param" key for the proof lines below, holding the FIRST site's
# result in file order -- the entry the pre-revision-4 cache held.
$fixtureCtx = Get-FileParseContext $SourceFile
$mappingCache = @{}
$mappingCacheTyped = @{}
foreach ($cs in $callSites) {
    $recvType = $null
    $calleeDeclState = 'NONE'
    if ($cs.Callee -and $null -ne $fixtureCtx) {
        $recvType = Resolve-ReceiverType $fixtureCtx ($cs.Line - 1) $cs.CalleeQualifier
        $calleeDeclState = (Resolve-CalleeDeclaration $cs.Callee $recvType).State
    }
    $cs | Add-Member -NotePropertyName ReceiverType -NotePropertyValue $recvType
    $cs | Add-Member -NotePropertyName CalleeDeclState -NotePropertyValue $calleeDeclState
    $mkey = "$($cs.Callee)|$recvType|$($cs.Param)"
    if (-not $mappingCacheTyped.ContainsKey($mkey)) { $mappingCacheTyped[$mkey] = Resolve-FpQ3Mapping $cs.Callee $cs.Param $recvType }
    $res = $mappingCacheTyped[$mkey]
    $proofKey = "$($cs.Callee)|$($cs.Param)"
    if (-not $mappingCache.ContainsKey($proofKey)) { $mappingCache[$proofKey] = $res }
    $cs | Add-Member -NotePropertyName MappingClass -NotePropertyValue $res.Class
    $cs | Add-Member -NotePropertyName DerivedKey -NotePropertyValue $res.ResolvedKey
    $cs | Add-Member -NotePropertyName MappingShape -NotePropertyValue $res.Shape
    $cs | Add-Member -NotePropertyName MappingProdFile -NotePropertyValue $res.ProdFile
    $cs | Add-Member -NotePropertyName MappingProdLine -NotePropertyValue $res.ProdLine
    $cs | Add-Member -NotePropertyName MappingResolvedExpr -NotePropertyValue $res.ResolvedExpr
}

$mappingNamed = @($callSites | Where-Object { $_.MappingClass -eq 'NAMED' }).Count
$mappingAliased = @($callSites | Where-Object { $_.MappingClass -eq 'ALIASED' }).Count
$mappingPositional = @($callSites | Where-Object { $_.MappingClass -eq 'POSITIONAL' }).Count
$mappingNoProdSites = @($callSites | Where-Object { $_.MappingClass -eq 'NO_PRODUCTION_CALL_SITE' }).Count
# revision 2 resolved shapes (FP-D16 / FP-D17 / FP-D18)
$mappingFixtureLocal = @($callSites | Where-Object { $_.MappingClass -eq 'FIXTURE_LOCAL_CFG_BUILDER' }).Count
$mappingWrapperFwd   = @($callSites | Where-Object { $_.MappingClass -eq 'WRAPPER_FORWARDED' }).Count
$mappingResolverRet  = @($callSites | Where-Object { $_.MappingClass -eq 'RESOLVER_RETURN' }).Count
# revision 3 resolved shape (FP-D21)
$mappingDefaultFallback = @($callSites | Where-Object { $_.MappingClass -eq 'DEFAULT_FALLBACK_BODY' }).Count
# FP-D20: the two halves of the old NOT_CFG_SOURCED catch-all, counted apart.
$mappingNotCfgSourced   = @($callSites | Where-Object { $_.MappingClass -eq 'NOT_CFG_SOURCED' }).Count
$mappingNotTraceable    = @($callSites | Where-Object { $_.MappingClass -eq 'SOURCE_NOT_TRACEABLE' }).Count
$mappingOtherUnresolvedSites = @($callSites | Where-Object {
    $_.MappingClass -in @('NOT_CFG_SOURCED', 'SOURCE_NOT_TRACEABLE', 'CFG_PATH_NOT_FOUND', 'NO_SIGNATURE_FOUND', 'CALL_PARSE_FAILED', 'PARAM_NOT_IN_SIGNATURE', 'PARAM_NOT_PASSED_AT_CALL_SITE', 'NO_CALLEE_IDENTIFIED', 'AMBIGUOUS_CALLEE')
}).Count
# FP-D27 (revision 4): same-named callees. AMBIGUOUS_CALLEE is also inside
# MAPPING_OTHER_UNRESOLVED_SITES above; it prints on its own line as well.
$mappingAmbiguousCallee = @($callSites | Where-Object { $_.MappingClass -eq 'AMBIGUOUS_CALLEE' }).Count
$sitesCalleeMultiDeclared = @($callSites | Where-Object { $_.CalleeDeclState -in @('TYPED', 'AMBIGUOUS') }).Count
$sitesCalleeTypedByReceiver = @($callSites | Where-Object { $_.CalleeDeclState -eq 'TYPED' }).Count
$calleesMultiDeclared = @($callSites | Where-Object { $_.CalleeDeclState -in @('TYPED', 'AMBIGUOUS') } | ForEach-Object { $_.Callee } | Select-Object -Unique)
$prodMethodNamesMultiDeclared = @($methodDeclIndex.Keys | Where-Object { $methodDeclIndex[$_].Count -gt 1 }).Count
$methodsNoProdCallSite = @($callSites | Where-Object { $_.MappingClass -eq 'NO_PRODUCTION_CALL_SITE' } | ForEach-Object { $_.Callee } | Where-Object { $_ } | Select-Object -Unique)

# ---------------------------------------------------------------------------------------
# Step 4 (section 4.1 step 4): CODE COMPUTES SET MEMBERSHIP HERE, now keyed off FP-Q3's
# DerivedKey (authoritative) rather than the old FP-D2 Candidates[0] (informational only
# as of revision 1). $everShippedByKey is built over the UNION of old-candidate paths
# (still needed for the MATCHED_ONE/ZERO/MULTI diagnostics) and FP-Q3-derived paths.
# ---------------------------------------------------------------------------------------
$allCandidatePaths = New-Object System.Collections.Generic.HashSet[string]
foreach ($cs in $callSites) {
    foreach ($c in $cs.Candidates) { [void]$allCandidatePaths.Add($c) }
    if ($cs.DerivedKey) { [void]$allCandidatePaths.Add($cs.DerivedKey) }
}
$everShippedByKey = @{}
foreach ($kp in $allCandidatePaths) { $everShippedByKey[$kp] = Get-EverShippedSet $kp }
$keysWithValueHistory = @($everShippedByKey.Keys | Where-Object { $everShippedByKey[$_].Count -gt 0 }).Count

foreach ($cs in $callSites) {
    if ($cs.DerivedKey -and $everShippedByKey.ContainsKey($cs.DerivedKey)) {
        $set = $everShippedByKey[$cs.DerivedKey]
        $equals = $false
        foreach ($v in $set) { if ([math]::Abs($v - $cs.LiteralValue) -lt 0.0000001) { $equals = $true; break } }
        $cs | Add-Member -NotePropertyName ResolvedKey -NotePropertyValue $cs.DerivedKey
        $cs | Add-Member -NotePropertyName EverShipped -NotePropertyValue $set
        $cs | Add-Member -NotePropertyName LiteralEqualsEverShipped -NotePropertyValue $equals
    } else {
        $cs | Add-Member -NotePropertyName ResolvedKey -NotePropertyValue $null
        $cs | Add-Member -NotePropertyName EverShipped -NotePropertyValue @()
        $cs | Add-Member -NotePropertyName LiteralEqualsEverShipped -NotePropertyValue $null
    }
}

$literalCallSites = $callSites.Count
$paramsDistinct = @($callSites | ForEach-Object { $_.Param } | Select-Object -Unique).Count
$matchedOne = @($callSites | Where-Object { $_.MatchClass -eq 'ONE' }).Count
$matchedZero = @($callSites | Where-Object { $_.MatchClass -eq 'ZERO' }).Count
$matchedMulti = @($callSites | Where-Object { $_.MatchClass -eq 'MULTI' }).Count
$literalEqualsShipped = @($callSites | Where-Object { $_.LiteralEqualsEverShipped -eq $true }).Count

# ---------------------------------------------------------------------------------------
# FP-Q1 (section 7.2/7.4 item 2): scope filter. A parameter with an FP-Q3 DerivedKey is in
# scope by construction (code only). A parameter with NONE goes to Jev, ONCE per distinct
# parameter name (FP-D14) -- never per call site, never 5x-sampled.
# ---------------------------------------------------------------------------------------
$paramInScope = @{}
foreach ($cs in $callSites) {
    if ($cs.DerivedKey) { $paramInScope[$cs.Param.ToLowerInvariant()] = $true }
}
$needsJevParams = New-Object System.Collections.Generic.List[string]
$seenParams = New-Object System.Collections.Generic.HashSet[string]
foreach ($cs in $callSites) {
    $pkey = $cs.Param.ToLowerInvariant()
    if (-not $paramInScope.ContainsKey($pkey) -and $seenParams.Add($pkey)) { [void]$needsJevParams.Add($cs.Param) }
}

# Fuzzy (substring-token) name-match candidates -- informational context ONLY for the
# FP-Q1 Jev call and for the atr:=20 worked-example proof below. Never authoritative.
$allLeafPaths = @($headFlatNoArray)   # FP-D29: frozen to the pre-revision-4 key set
function Get-FuzzyCandidates([string]$paramName) {
    $snake = ConvertTo-SnakeCase $paramName
    $tokens = @($snake -split '_' | Where-Object { $_.Length -ge 4 })
    if ($tokens.Count -eq 0) { Write-Output -NoEnumerate @(); return }
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($k in $allLeafPaths) {
        $leaf = $k.Split('.')[-1]
        foreach ($t in $tokens) {
            if ($leaf -like "*$t*") { [void]$found.Add($k); break }
        }
    }
    Write-Output -NoEnumerate @($found | Select-Object -Unique | Select-Object -First 8)
}

function Get-ParamContext([string]$paramName) {
    $sites = @($callSites | Where-Object { $_.Param -eq $paramName })
    $first = $sites[0]
    $callees = @($sites | ForEach-Object { $_.Callee } | Where-Object { $_ } | Select-Object -Unique)
    $mappingClasses = @($sites | ForEach-Object { $_.MappingClass } | Select-Object -Unique)
    return [PSCustomObject]@{ FirstSite = $first; Callees = $callees; MappingClasses = $mappingClasses; Count = $sites.Count }
}

function Invoke-ScopeClassification([string]$apiKeyIn, [string]$paramName, $ctxInfo, [string]$uid) {
    $nameCands = Get-KeyCandidates $paramName
    $fuzzy = Get-FuzzyCandidates $paramName
    $state = @{
        param_name = $paramName
        example_call_line = $ctxInfo.FirstSite.CallLineText
        example_comment_block = $ctxInfo.FirstSite.CommentBlock
        callees = @($ctxInfo.Callees)
        production_mapping_classes = @($ctxInfo.MappingClasses)
        name_match_candidates = @($nameCands)
        fuzzy_name_candidates = @($fuzzy)
        occurrence_count = $ctxInfo.Count
    }
    # FP-D23: a fresh uid per sample so the samples are independent draws
    # (docs/harness-shadow-mode-protocol.md section 4b, same as FP-1/FP-2).
    if ($uid) { $state.sample_uid = $uid }
    if ($DebugState) { Write-Host "STATE_DEBUG scope param=$paramName keys=[$($state.Keys -join ',')] sample_uid=$($state.sample_uid)" }
    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            scope = @{
                type = 'choice'
                instructions = "FP-Q1 (docs/fixture-parser-check-spec.md section 7.2): is ``param_name``, as used at ``example_call_line`` (production evidence found no derivable settings.json cfg path for it -- see ``production_mapping_classes``), a SETTINGS-DERIVED THRESHOLD (a tunable value that in production tracks a settings.json key, even if this harness could not mechanically prove which one) or a FIXTURE INPUT (a test-scenario value with no settings.json counterpart -- e.g. a price, an iteration count, a learning rate, a clock value, a raw indicator reading fed INTO a calculation)? ``name_match_candidates``/``fuzzy_name_candidates`` are NAME-similarity guesses only, not evidence -- a name match to a settings block (e.g. `atr` against the ATR block) does not by itself make this an in-scope threshold."
                criteria = @{
                    threshold = 'This parameter is a settings-derived threshold: CLAUDE.md''s fixture-literal provenance rule applies to it.'
                    input     = 'This parameter is a fixture/mechanism input with no settings.json counterpart: out of scope for the provenance rule.'
                }
            }
        }
    }
    $call = Invoke-Jev $apiKeyIn $body 3 1 $TestTransportOverride
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error; WafBlocked = $call.WafBlocked } }
    $usageIn = 0; $usageOut = 0
    if ($call.Response.usage) {
        if ($call.Response.usage.input_tokens) { $usageIn = [int]$call.Response.usage.input_tokens }
        if ($call.Response.usage.output_tokens) { $usageOut = [int]$call.Response.usage.output_tokens }
    }
    return @{ Ok = $true; Scope = $call.Response.answers.scope.choice; UsageIn = $usageIn; UsageOut = $usageOut }
}

$scopeJevCalls = 0
$scopeUnstableParams = 0
$scopeUsageIn = 0
$scopeUsageOut = 0
$scopeJevSkippedNoKey = 0
$apiKey = $env:TYPESAFE_API_KEY
$scopeJevAvailable = -not [string]::IsNullOrWhiteSpace($apiKey)

# FP-D15 (revision 2, item 8c): the SECOND baseline refusal. FP-Q1 is a detector, so
# docs/harness-shadow-mode-protocol.md section 2 step 1 applies to it as much as to FP-1 --
# and until revision 2 it sat outside the gate entirely. The refusal is scoped to the CALL,
# not to the run, so FP-D11's ordering rationale still holds: the coverage block below
# still prints real numbers before anything else, they are just CODE-ONLY numbers, and
# SCOPE_BASELINE_STATE says which kind this run produced.
$scopeJevSkippedNoBaseline = 0
$scopeBaselineCovers = 0
$scopeBaselineOk = $false
$scopeBaselineState = 'MISSING_NOT_SUPPLIED'
# SR-D1: item-level scope baseline gate (see below). Declared here, unconditionally, so
# an empty list is always the right answer when no -ScopeBaselinePath file was found.
$scopeUnbaselinedParams = New-Object System.Collections.Generic.List[string]
if (-not [string]::IsNullOrWhiteSpace($ScopeBaselinePath)) {
    $scopeBaselineFull = Resolve-RepoPath $ScopeBaselinePath
    if (-not (Test-Path $scopeBaselineFull)) {
        $scopeBaselineState = 'MISSING_PATH_NOT_FOUND'
    } else {
        $sbRaw = Get-Content -Raw -Path $scopeBaselineFull | ConvertFrom-Json
        $sbMap = @{}
        if ($null -ne $sbRaw) {
            # Accept either a flat { param: answer } map or the { judgments: {...} } shape
            # the 2026-09-22 seat baseline uses.
            $sbNode = $sbRaw
            if ($sbRaw.PSObject.Properties.Name -contains 'judgments') { $sbNode = $sbRaw.judgments }
            foreach ($p in $sbNode.PSObject.Properties) {
                if ($p.Name -eq '_meta') { continue }
                $sbMap[$p.Name.ToLowerInvariant()] = [string]$p.Value
            }
        }
        foreach ($p in $needsJevParams) { if ($sbMap.ContainsKey($p.ToLowerInvariant())) { $scopeBaselineCovers++ } }
        if ($needsJevParams.Count -gt 0 -and $scopeBaselineCovers -eq 0) {
            # A file that names none of this run's candidates is not a read of them.
            $scopeBaselineState = 'MISSING_COVERS_NO_CANDIDATE'
        } else {
            $scopeBaselineOk = $true
            $scopeBaselineState = 'SUPPLIED'
        }
        # SR-D1 (docs/jev-harnesses-second-reader-2026-09-24.md section 6, from finding
        # SR-4): the two counters above are FILE-level -- a baseline naming ONE candidate
        # set $scopeBaselineOk=$true and let EVERY OTHER needsJevParams item through with
        # no seat line. List every one this file does not cover, item by item, the FP-D24
        # pattern.
        foreach ($p in $needsJevParams) { if (-not $sbMap.ContainsKey($p.ToLowerInvariant())) { [void]$scopeUnbaselinedParams.Add($p) } }
    }
}
# SR-D1: refusing item-by-item means NO scope item may reach Invoke-ScopeClassification
# this run once one is missing -- never a partial call list -- so this is computed once,
# before the classify/skip branch below, and reused by both that branch and the exit gate
# printed later (after Write-Coverage, so coverage still prints on every exit path).
$scopeItemIncomplete = ($scopeUnbaselinedParams.Count -gt 0 -and -not $AllowUnbaselinedItems)
$scopeApiFailed = $false
$scopeApiFailMsg = ''
# Named single-parameter proofs required by docs/fixture-parser-check-spec.md section 7.5:
# item 5's worked over-match example (atr) and item 4's four named non-candidates (price,
# epochs, lr, nowUtcMs). These are the ONLY per-parameter FP-Q1 answers this build ever
# prints -- explicitly authorised by name in the spec's own acceptance items, same footing
# as TRAP1_PROOF/FPQ3_PROOF above. No other parameter's individual scope answer is ever
# surfaced (REPORTING DISCIPLINE: counts only for everything else).
$fpq1NamedProofItems = @('atr', 'price', 'epochs', 'lr', 'nowUtcMs')
$fpq1NamedProofs = @{}
$scopeSw = [System.Diagnostics.Stopwatch]::StartNew()

if (-not $scopeBaselineOk -or $scopeItemIncomplete) {
    # FP-D15: structural. No operator-written scope baseline, so FP-Q1 asks Jev NOTHING and
    # no scope answer can be seen before the operator has written their own read.
    # SR-D1: a supplied-but-incomplete baseline takes this same no-call branch -- refusing
    # item by item still means NOT ONE needsJevParams item reaches Jev this run, never a
    # partial list. The hard exit is printed later, after coverage (below).
    foreach ($p in $needsJevParams) { $paramInScope[$p.ToLowerInvariant()] = $false }
    $scopeJevSkippedNoBaseline = $needsJevParams.Count
} elseif (-not $scopeJevAvailable) {
    foreach ($p in $needsJevParams) { $paramInScope[$p.ToLowerInvariant()] = $false }
    $scopeJevSkippedNoKey = $needsJevParams.Count
} else {
    foreach ($p in $needsJevParams) {
        $ctxInfo = Get-ParamContext $p
        # FP-D23 (revision 3, item 8h): $Samples draws per parameter, each with a fresh uid.
        # In scope if ANY draw says `threshold` -- see the header for why the rule leans to
        # over-scoping. A split vote is counted as UNSTABLE; the count prints, the
        # per-parameter answer does not (same reporting discipline as before).
        $thresholdVotes = 0
        for ($si = 0; $si -lt $Samples; $si++) {
            $suid = "scope:${p}:${si}:$([guid]::NewGuid().ToString('N').Substring(0,8))"
            $sc = Invoke-ScopeClassification $apiKey $p $ctxInfo $suid
            if (-not $sc.Ok) { $scopeApiFailed = $true; $scopeApiFailMsg = "Jev scope-classification failed for parameter '$p' (sample $($si+1)/$Samples): $($sc.Error)"; break }
            $scopeJevCalls++
            $scopeUsageIn += $sc.UsageIn
            $scopeUsageOut += $sc.UsageOut
            if ($sc.Scope -eq 'threshold') { $thresholdVotes++ }
        }
        if ($scopeApiFailed) { break }
        if ($thresholdVotes -gt 0 -and $thresholdVotes -lt $Samples) { $scopeUnstableParams++ }
        $paramInScope[$p.ToLowerInvariant()] = ($thresholdVotes -gt 0)
        foreach ($namedP in $fpq1NamedProofItems) {
            if ($p -ieq $namedP) {
                $fpq1NamedProofs[$namedP] = "FPQ1_PROOF $namedP (callees=[$($ctxInfo.Callees -join ',')], production_mapping=[$($ctxInfo.MappingClasses -join ',')]) -> Jev classified 'threshold' on $thresholdVotes/$Samples samples -> $(if ($thresholdVotes -gt 0) { 'IN SCOPE' } else { 'OUT OF SCOPE' })"
            }
        }
    }
}

foreach ($cs in $callSites) {
    $pkey = $cs.Param.ToLowerInvariant()
    $inScope = if ($paramInScope.ContainsKey($pkey)) { $paramInScope[$pkey] } else { $false }
    $cs | Add-Member -NotePropertyName InScope -NotePropertyValue $inScope
}
$inScopeSites = @($callSites | Where-Object { $_.InScope }).Count
$outOfScopeSites = @($callSites | Where-Object { -not $_.InScope }).Count
$inScopeParams = @($callSites | Where-Object { $_.InScope } | ForEach-Object { $_.Param } | Select-Object -Unique).Count
# FP-D22 (revision 3, item 8j): in-scope sites carrying NO provenance marker never join
# the FP-1 residual, so FP-1 never judges them -- and before this counter nothing said
# they existed. Code-only facts, no judgment: a site either has the MECHANISM/SHIPPED
# keyword in its comment block or it does not, and its literal either equals an
# ever-shipped value or it does not.
$inScopeUnmarkedSites = @($callSites | Where-Object { $_.InScope -and -not $_.HasMarker })
$inScopeUnmarkedEqShipped = @($inScopeUnmarkedSites | Where-Object { $_.LiteralEqualsEverShipped -eq $true })
$scopeSw.Stop()
$scopeWallTimeSec = [math]::Round($scopeSw.Elapsed.TotalSeconds, 2)

function Write-Coverage([int]$judged) {
    "FIXTURE_SUBS=$fixtureSubs"
    "LITERAL_CALL_SITES=$literalCallSites"
    "PARAMS_DISTINCT=$paramsDistinct"
    "SETTINGS_REVISIONS_WALKED=$settingsRevisionsWalked"
    "KEYS_WITH_VALUE_HISTORY=$keysWithValueHistory"
    "MATCHED_ONE_KEY=$matchedOne"
    "MATCHED_ZERO_KEYS=$matchedZero"
    "MATCHED_MULTI_KEYS=$matchedMulti"
    "LITERAL_EQUALS_EVER_SHIPPED=$literalEqualsShipped"
    "SITES_WITH_PROVENANCE_COMMENT=$sitesWithProvenanceAfter"
    "SITES_JUDGED=$judged"
    "--- revision 1 additions (docs/fixture-parser-check-spec.md section 7.4) ---"
    "MAPPING_NAMED=$mappingNamed"
    "MAPPING_ALIASED=$mappingAliased"
    "MAPPING_POSITIONAL=$mappingPositional"
    "MAPPING_NO_PRODUCTION_CALL_SITE_SITES=$mappingNoProdSites"
    "MAPPING_OTHER_UNRESOLVED_SITES=$mappingOtherUnresolvedSites"
    "METHODS_NO_PRODUCTION_CALL_SITE=$($methodsNoProdCallSite.Count)"
    "--- revision 2 additions (docs/fixture-parser-check-spec.md section 8.4 items 8a-8c) ---"
    "MAPPING_FIXTURE_LOCAL_CFG_BUILDER=$mappingFixtureLocal"
    "MAPPING_WRAPPER_FORWARDED=$mappingWrapperFwd"
    "MAPPING_RESOLVER_RETURN=$mappingResolverRet"
    "POCO_JSON_ALIAS_SEGMENTS=$pocoAliasSegments"
    # FP-D20: the old NOT_CFG_SOURCED catch-all, split. The first is an earned negative;
    # the second says the search did not complete and must NOT be read as a finding.
    "MAPPING_NOT_CFG_SOURCED_SITES=$mappingNotCfgSourced"
    "MAPPING_SOURCE_NOT_TRACEABLE_SITES=$mappingNotTraceable"
    "--- end revision 2 additions ---"
    "--- revision 3 additions (docs/fixture-parser-check-spec.md section 8.4 items 8f, 8j) ---"
    "MAPPING_DEFAULT_FALLBACK_BODY=$mappingDefaultFallback"
    # FP-D22: the residual is marked AND in scope. These are in scope and NOT marked, so
    # FP-1 never sees them. A non-zero second counter is a list of literals that equal a
    # shipped value and declare nothing -- the A43b breach shape.
    "IN_SCOPE_UNMARKED_SITES=$($inScopeUnmarkedSites.Count)"
    "IN_SCOPE_UNMARKED_EQUALS_EVER_SHIPPED=$($inScopeUnmarkedEqShipped.Count)"
    # FP-D23 (item 8h): FP-Q1 is now sampled. Unstable = the draws split.
    "SCOPE_SAMPLES_PER_PARAM=$Samples"
    "SCOPE_UNSTABLE_PARAMS=$scopeUnstableParams"
    "--- end revision 3 additions ---"
    "--- revision 4 additions (docs/jev-harnesses-adversarial-review-2026-09-22.md findings 4, 8, 9) ---"
    # FP-D27 (finding 4): same-named callees resolved by receiver type, or reported.
    "PROD_METHOD_NAMES_MULTI_DECLARED=$prodMethodNamesMultiDeclared"
    "SITES_CALLEE_MULTI_DECLARED=$sitesCalleeMultiDeclared"
    "SITES_CALLEE_TYPED_BY_RECEIVER=$sitesCalleeTypedByReceiver"
    "MAPPING_AMBIGUOUS_CALLEE_SITES=$mappingAmbiguousCallee"
    foreach ($mc in $calleesMultiDeclared) {
        $g = @($callSites | Where-Object { $_.Callee -eq $mc })
        $rts = @($g | ForEach-Object { if ($_.ReceiverType) { $_.ReceiverType } else { '(untyped)' } } | Select-Object -Unique) -join ','
        "  CALLEE_MULTI_DECLARED {0} declared={1} sites={2} typed={3} ambiguous={4} receiver_types=[{5}] mapping=[{6}]" -f $mc, $methodDeclIndex[$mc.ToLowerInvariant()].Count, $g.Count, @($g | Where-Object { $_.CalleeDeclState -eq 'TYPED' }).Count, @($g | Where-Object { $_.CalleeDeclState -eq 'AMBIGUOUS' }).Count, $rts, (@($g | ForEach-Object { $_.MappingClass } | Select-Object -Unique) -join ',')
    }
    # FP-D28 (finding 8): a LABEL on every marked site. Nothing is dropped from the residual.
    "MARKED_SITES_SITE_NAMED=$(@($callSites | Where-Object { $_.MarkerScope -eq 'SITE_NAMED' }).Count)"
    "MARKED_SITES_BLOCK_ONLY=$(@($callSites | Where-Object { $_.MarkerScope -eq 'BLOCK_ONLY' }).Count)"
    $inScopeBlockOnly = @($callSites | Where-Object { $_.InScope -and $_.MarkerScope -eq 'BLOCK_ONLY' })
    "IN_SCOPE_MARKED_SITE_NAMED=$(@($callSites | Where-Object { $_.InScope -and $_.MarkerScope -eq 'SITE_NAMED' }).Count)"
    "IN_SCOPE_MARKED_BLOCK_ONLY=$($inScopeBlockOnly.Count)"
    if ($inScopeBlockOnly.Count -gt 0) {
        "IN_SCOPE_MARKED_BLOCK_ONLY_SITES (the block's keyword marks them; the block never names the parameter -- still judged):"
        foreach ($b in ($inScopeBlockOnly | Sort-Object Line, Param)) { "  {0}#{1}#{2}" -f $b.EnclosingSub, $b.Line, $b.Param }
    }
    # FP-D29 (finding 9): arrays in the settings walk. WITHOUT_ARRAYS is the key set the
    # walk saw before revision 4; the difference is exactly the array-element leaves.
    "SETTINGS_CACHE_DISCARDED_STALE_SCHEMA=$settingsCacheDiscarded"
    "SETTINGS_HEAD_LEAVES=$($headFlat.Count)"
    "SETTINGS_HEAD_LEAVES_WITHOUT_ARRAYS=$($headFlatNoArray.Count)"
    "SETTINGS_HEAD_ARRAY_KEYED_LEAVES=$headArrayLeaves"
    "SETTINGS_HEAD_ARRAY_KEYED_ELEMENTS=$($headArrayStats.KeyedElements)"
    "ARRAY_UNKEYED=$($headArrayStats.UnkeyedElements)"
    if ($headArrayStats.UnkeyedElements -gt 0) { "  ARRAY_UNKEYED_PATHS: $(@($headArrayStats.UnkeyedPaths.Keys | Sort-Object) -join ', ')" }
    "SETTINGS_HEAD_SCALAR_ARRAYS_SKIPPED=$($headArrayStats.ScalarArrays)"
    "SETTINGS_ARRAY_KEYS_EVER_WALKED=$settingsArrayKeysEver"
    "--- end revision 4 additions ---"
    "IN_SCOPE_SITES=$inScopeSites"
    "OUT_OF_SCOPE_SITES=$outOfScopeSites"
    "IN_SCOPE_PARAMS=$inScopeParams"
    "SCOPE_BASELINE_STATE=$scopeBaselineState"
    "SCOPE_BASELINE_COVERS_CANDIDATES=$scopeBaselineCovers"
    "SCOPE_JEV_CALLS=$scopeJevCalls"
    "SCOPE_JEV_SKIPPED_NO_BASELINE=$scopeJevSkippedNoBaseline"
    "SCOPE_JEV_SKIPPED_NO_API_KEY=$scopeJevSkippedNoKey"
    "SCOPE_USAGE_INPUT_TOKENS=$scopeUsageIn"
    "SCOPE_USAGE_OUTPUT_TOKENS=$scopeUsageOut"
    "SCOPE_WALL_TIME_SEC=$scopeWallTimeSec"
    "SITES_WITH_PROVENANCE_COMMENT_BEFORE=$sitesWithProvenanceBefore"
    "SITES_WITH_PROVENANCE_COMMENT_AFTER=$sitesWithProvenanceAfter"
    "SITES_GAINED_COVERAGE=$sitesGainedCoverage"
    "SITES_WITH_COMMENT_BEFORE=$sitesWithCommentBefore"
    "SITES_WITH_COMMENT_AFTER=$sitesWithCommentAfter"
    Get-JevModelLine

    # [2026-09-22] THE SILENT-HOLE FIX. FP-Q1's scope filter decides WHICH sites the seat
    # ever writes a baseline line for, so an over-eager exclusion shapes the measured
    # population and NOTHING would say so -- the seat would judge a set Jev chose and read
    # the agreement as its own. Printing every exclusion with its REASON makes the filter
    # reviewable and overrulable instead of silent. The repo's own ruling: a counter
    # reading 0 is the tripwire, not waste.
    #
    # This is a per-parameter listing, NOT a per-item verdict: it names what was dropped
    # and why, never a provenance judgment. The reporting discipline in
    # docs/harness-shadow-mode-protocol.md section 4a is about FP-1/FP-2 verdicts.
    "EXCLUDED_PARAMS (FP-Q1 dropped these from scope -- review before trusting IN_SCOPE_SITES):"
    $exParams = $callSites | Where-Object { -not $_.InScope } |
        Group-Object -Property Param |
        Sort-Object -Property @{Expression = { $_.Count }; Descending = $true }, Name
    foreach ($g in $exParams) {
        $one = $g.Group[0]
        $why = if ($one.DerivedKey) { 'HAS_KEY_BUT_SCOPED_OUT' }
               elseif ($one.MappingClass) { $one.MappingClass }
               else { 'NO_DERIVED_KEY' }
        $callees = @($g.Group | ForEach-Object { $_.Callee } | Select-Object -Unique) -join ','
        "  {0,-26} x{1,-3} why={2} callees=[{3}]" -f $g.Name, $g.Count, $why, $callees
    }

    # FP-D22: name every in-scope, unmarked site whose literal equals a shipped value. A
    # code fact about the file, not an FP-1 verdict, so it prints on every exit path.
    if ($inScopeUnmarkedEqShipped.Count -gt 0) {
        "IN_SCOPE_UNMARKED_EQUALS_EVER_SHIPPED_SITES (FP-1 never judges these -- no MECHANISM/SHIPPED marker):"
        foreach ($u in ($inScopeUnmarkedEqShipped | Sort-Object Line, Param)) {
            "  {0}#{1}#{2} literal={3} key={4} ever_shipped=[{5}]" -f $u.EnclosingSub, $u.Line, $u.Param, $u.LiteralValue, $u.ResolvedKey, (@($u.EverShipped) -join ',')
        }
    }
}

# ---------------------------------------------------------------------------------------
# Step 5 (section 4.2): coverage prints before anything else, on every exit path. A broken
# parser is loud (escalation trigger, section 0 / section 4.2): LITERAL_CALL_SITES < 60 or
# SETTINGS_REVISIONS_WALKED < 80 exits PARSER_SUSPECT. This check uses only those two
# (unchanged from the pre-revision build) and fires BEFORE any Jev call this run might make
# (FP-Q1's calls already happened above to compute IN_SCOPE_SITES for this same coverage
# block -- see FP-D11 in the header for why that ordering is a deliberate, narrow exception
# to "no API key needed before this gate", scoped to FP-Q1 only, never to FP-1/FP-2).
# ---------------------------------------------------------------------------------------
if ($SiteDumpPath) {
    $dumpLines = New-Object System.Collections.Generic.List[string]
    $dumpLines.Add((@('sub', 'line', 'param', 'callee', 'qualifier', 'receiver_type', 'callee_decl_state', 'mapping_class', 'key', 'equals_ever_shipped', 'has_marker', 'marker_scope', 'in_scope') -join "`t"))
    foreach ($cs in $callSites) {
        $dumpLines.Add((@($cs.EnclosingSub, $cs.Line, $cs.Param, $cs.Callee, $cs.CalleeQualifier, $cs.ReceiverType, $cs.CalleeDeclState, $cs.MappingClass, $cs.ResolvedKey, $cs.LiteralEqualsEverShipped, $cs.HasMarker, $cs.MarkerScope, $cs.InScope) -join "`t"))
    }
    Set-Content -Encoding UTF8 -Path (Resolve-RepoPath $SiteDumpPath) -Value $dumpLines
}

if ($scopeApiFailed) {
    Write-Coverage 0
    "EXIT_REASON=API_FAILED"
    $scopeApiFailMsg
    exit 2
}

Write-Coverage 0

if ($literalCallSites -lt 60 -or $settingsRevisionsWalked -lt 80) {
    "EXIT_REASON=PARSER_SUSPECT"
    "LITERAL_CALL_SITES=$literalCallSites (need >= 60, docs/fixture-parser-check-spec.md section 2 measured 120) or SETTINGS_REVISIONS_WALKED=$settingsRevisionsWalked (need >= 80, section 2 measured 87) tripped the escalation trigger. Treat this as the parser missing a form, never as a smaller-than-usual file."
    exit 2
}

# SR-D1 (docs/jev-harnesses-second-reader-2026-09-24.md section 6, finding SR-4): FP-Q1's
# own item-level baseline gate, the FP-D24 pattern applied to the SECOND detector. Placed
# after Write-Coverage/PARSER_SUSPECT so coverage still prints on every exit path (section
# 4.2 step 5), matching the invariant FP-1/FP-2's own gates keep. FP-Q1 already made ZERO
# Jev calls this run when $scopeItemIncomplete is true -- the classify/skip branch above
# took the no-call path -- so this is purely the refusal, never a second skip.
if ($scopeItemIncomplete) {
    "EXIT_REASON=BASELINE_INCOMPLETE"
    "SCOPE_UNBASELINED_ITEMS=$($scopeUnbaselinedParams.Count) -- no line in '$ScopeBaselinePath' for:"
    foreach ($u in $scopeUnbaselinedParams) {
        $ci = Get-ParamContext $u
        "  {0,-26} x{1,-3} callees=[{2}] production_mapping=[{3}]" -f $u, $ci.Count, (@($ci.Callees) -join ','), (@($ci.MappingClasses) -join ',')
    }
    "FP-Q1 made no Jev call this run (nothing was judged). Write your own threshold-or-input read for each item above as JSON in '$ScopeBaselinePath' (vocabulary threshold | input | unsure), or pass -AllowUnbaselinedItems for a routine re-run of items already measured once."
    exit 2
}

# Trap-1 proof line, always printed once revisions are walked and BEFORE the baseline gate,
# so it is visible even on a BASELINE_MISSING exit (docs/fixture-parser-check-spec.md
# section 5 acceptance item 1).
if ($everShippedByKey.ContainsKey('indicators.OBV.trend_gate')) {
    $obvSet = $everShippedByKey['indicators.OBV.trend_gate']
    "TRAP1_PROOF indicators.OBV.trend_gate ever-shipped = {$($obvSet -join ', ')}"
} else {
    $obvOnDemand = Get-EverShippedSet 'indicators.OBV.trend_gate'
    "TRAP1_PROOF indicators.OBV.trend_gate ever-shipped = {$($obvOnDemand -join ', ')}"
}
# FP-D29 proof line (review finding 9): an ARRAY-backed key now has a value history. Same
# footing as TRAP1_PROOF -- a structural fact about the walk, never a verdict.
$arrayProofKey = 'session_volume.sessions[name=ASIA].high_multiplier'
$arrayProofSet = Get-EverShippedSet $arrayProofKey
"ARRAY_PROOF $arrayProofKey ever-shipped = {$($arrayProofSet -join ', ')} (revisions carrying the key: $(@($settingsHashes | Where-Object { $revisionCache.ContainsKey($_) -and $revisionCache[$_].ContainsKey($arrayProofKey) }).Count) of $settingsRevisionsWalked)"

# FP-Q3 acceptance item 1 proof lines: structural facts about the derived mapping (which
# cfg path, which production file/line, which of the three shapes) -- never a "verdict" in
# the FP-1/FP-2 sense, so safe to print unconditionally, same footing as TRAP1_PROOF above.
function Format-FpQ3Proof([string]$callee, [string]$param) {
    $key = "$callee|$param"
    if ($mappingCache.ContainsKey($key)) {
        $r = $mappingCache[$key]
        if ($r.ResolvedKey) {
            # revision 2: the three section 7.3 proof lines keep their EXACT pre-revision
            # format (an acceptance item diffs them), so the shape suffix is added only for
            # the new classes -- where the Class alone hides which inner shape carried the
            # evidence (`wrapper(X)->RESOLVER_RETURN` vs `wrapper(X)->ALIASED`).
            $shapeNote = ''
            if ($r.Class -in @('WRAPPER_FORWARDED', 'RESOLVER_RETURN', 'FIXTURE_LOCAL_CFG_BUILDER', 'DEFAULT_FALLBACK_BODY')) {
                $shapeNote = " shape=$($r.Shape)"
            }
            return "FPQ3_PROOF $callee $param -> $($r.ResolvedKey) [$($r.Class)]$shapeNote ($($r.ProdFile):$($r.ProdLine), expr=$($r.ResolvedExpr))"
        } else {
            return "FPQ3_PROOF $callee $param -> UNRESOLVED [$($r.Class)]"
        }
    }
    return "FPQ3_PROOF $callee $param -> NOT IN MAPPING CACHE (no fixture call site found for this exact pair)"
}
(Format-FpQ3Proof 'CalcTFI' 'tfiWindowSize')
(Format-FpQ3Proof 'CalcOFI' 'buyDominantRatio')
(Format-FpQ3Proof 'CalcOBV' 'trendGate')

# Revision 2 (section 8.2): the SIX parameters the 2026-09-22 FP-Q1 run found the
# enumeration missing -- two by gap A (fixture-local cfg builder), four by gap B (one-hop
# forwarding wrapper). Same footing as the three above: structural facts about the derived
# mapping, never a verdict, so they print unconditionally and before the baseline gate.
(Format-FpQ3Proof 'BuildA8Cfg' 'fundingBoost')
(Format-FpQ3Proof 'BuildBurstCfg' 'upgradeBonus')
(Format-FpQ3Proof 'Fold' 'tauFastSec')
(Format-FpQ3Proof 'Fold' 'tauNormSec')
(Format-FpQ3Proof 'Snapshot' 'grossFloorUsdPerSec')
(Format-FpQ3Proof 'Snapshot' 'minCoverageSec')

# Revision 3 (item 8f): the constructor whose production call OMITS the argument, so the
# value comes from the callee's own default-fallback body. Same footing as the lines above.
(Format-FpQ3Proof 'WsMarketDataSource' 'staleAfterSec')

foreach ($namedP in $fpq1NamedProofItems) {
    if ($fpq1NamedProofs.ContainsKey($namedP)) { $fpq1NamedProofs[$namedP] }
    else { "FPQ1_PROOF $namedP -> not classified this run (SCOPE_BASELINE_STATE=$scopeBaselineState, SCOPE_JEV_SKIPPED_NO_BASELINE=$scopeJevSkippedNoBaseline, SCOPE_JEV_SKIPPED_NO_API_KEY=$scopeJevSkippedNoKey, already in scope via FP-Q3, or param not present in -SourceFile's population)" }
}

# FP-D15: the scope detector's own protocol step 2 -- print its candidate list, with zero
# judgments, so the operator can write a baseline against it. Non-fatal: the run continues
# to the FP-1 gate on a CODE-ONLY scope, which is smaller than the judged one and therefore
# cannot smuggle an unbaselined detector's opinion into the measured population.
if (-not $scopeBaselineOk -and $needsJevParams.Count -gt 0) {
    "SCOPE_BASELINE_MISSING ($scopeBaselineState) -- FP-Q1 made NO Jev call this run."
    "  docs/harness-shadow-mode-protocol.md section 2 step 1 / section 4c: the SEAT writes the"
    "  baseline, always. Write your own threshold-or-input read of each SCOPE_CANDIDATES item"
    "  below as JSON (a flat { param: answer } map, or { ""judgments"": { ... } }; vocabulary"
    "  threshold | input | unsure), then re-run with -ScopeBaselinePath. IN_SCOPE_* above are"
    "  CODE-ONLY until you do."
    "SCOPE_CANDIDATES (no judgments, for scope-baseline labelling):"
    foreach ($p in $needsJevParams) {
        $ci = Get-ParamContext $p
        "  {0,-26} x{1,-3} callees=[{2}] production_mapping=[{3}]" -f $p, $ci.Count, (@($ci.Callees) -join ','), (@($ci.MappingClasses) -join ',')
    }
}

"METHODS_WITH_NO_PRODUCTION_CALL_SITE: $($methodsNoProdCallSite -join ', ')"

# FP-Q2 acceptance item 3 proof: A6_ObvNormalisation's SECOND CalcOBV call, before/after.
$a6Sites = @($callSites | Where-Object { $_.EnclosingSub -eq 'A6_ObvNormalisation' })
if ($a6Sites.Count -gt 0) {
    $a6MaxLine = ($a6Sites | Measure-Object -Property Line -Maximum).Maximum
    $a6Second = @($a6Sites | Where-Object { $_.Line -eq $a6MaxLine })
    foreach ($cs in $a6Second) {
        "A6_SECOND_CALLSITE_PROOF line=$($cs.Line) param=$($cs.Param) HasMarkerOld=$($cs.HasMarkerOld) HasMarkerNew=$($cs.HasMarker)"
    }
} else {
    "A6_SECOND_CALLSITE_PROOF: A6_ObvNormalisation not found in -SourceFile's population."
}

# ---------------------------------------------------------------------------------------
# Shadow-mode protocol step 2 (docs/harness-shadow-mode-protocol.md section 2): print the
# candidate list first, with NO judgments, before the baseline gate. -SubFilter (FP-D8)
# narrows this to the window actually being judged this run. FP-Q1 (section 7.4 item 2):
# only sites that are BOTH provenance-commented AND in-scope join the residual.
# ---------------------------------------------------------------------------------------
$residualSites = @($callSites | Where-Object { $_.HasMarker -and $_.InScope })
if ($SubFilter) { $residualSites = @($residualSites | Where-Object { $_.EnclosingSub -match $SubFilter }) }

function Get-Fp1Id($cs) { "$($cs.EnclosingSub)#$($cs.Line)#$($cs.Param)" }

if ($residualSites.Count -gt 0) {
    "FP1_CANDIDATES (no judgments, for baseline labelling):"
    foreach ($cs in $residualSites) {
        "  $(Get-Fp1Id $cs) matchClass=$($cs.MatchClass) key=$(if ($cs.ResolvedKey) { $cs.ResolvedKey } else { '(unresolved)' }) mappingClass=$($cs.MappingClass) marker=$($cs.MarkerScope)"
    }
}

$residualSubNames = @($residualSites | ForEach-Object { $_.EnclosingSub } | Select-Object -Unique)
$fp2Items = New-Object System.Collections.Generic.List[object]
foreach ($sn in $residualSubNames) {
    $r = $ranges | Where-Object { $_.Kind -eq 'Sub' -and $_.Name -eq $sn } | Select-Object -First 1
    if ($null -eq $r) { continue }
    $bodyLines = $lines[$r.Start..$r.End]
    $body = ($bodyLines -join "`n")
    if ($body.Length -gt $MAX_SUB_BODY_CHARS) { $body = $body.Substring(0, $MAX_SUB_BODY_CHARS) + "`n... [truncated at $MAX_SUB_BODY_CHARS chars]" }
    # FP-D33 (SR-D6, revision 5): FP-2t's own body. Every Check("...") call's leading
    # title-string argument becomes a neutral placeholder -- only the title, never the
    # condition or detail arguments after it, so the body still type-checks in spirit
    # (irrelevant here, it is never compiled) and Jev is left with only code and comments
    # to judge the name against.
    $bodyTitleStripped = [regex]::Replace($body, '(Check\(\s*)"[^"]*"', '$1"check"')
    $fp2Items.Add([PSCustomObject]@{ SubName = $sn; Body = $body; BodyTitleStripped = $bodyTitleStripped })
}
if ($fp2Items.Count -gt 0) {
    "FP2_CANDIDATES (no judgments, for baseline labelling):"
    foreach ($f2 in $fp2Items) { "  $($f2.SubName)" }
}

if ($residualSites.Count -eq 0 -and $fp2Items.Count -eq 0) {
    "EXIT_REASON=NO_RESIDUAL_IN_WINDOW"
    "No provenance-commented, in-scope (MECHANISM/SHIPPED marker AND FP-Q1 in-scope) call sites matched$(if ($SubFilter) { " -SubFilter '$SubFilter'" } else { '' }). Nothing to judge this run."
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 6 (section 4.1 step 5): refuse to call the API without an operator-written
# baseline. Structural. docs/harness-shadow-mode-protocol.md section 4c: the SEAT writes
# this file, always, on a first run -- an implementer that finds none STOPS and says so; it
# must never supply one. This gate is UNCHANGED by revision 1 (FP-D11 in the header
# explains the one place upstream where revision 1 does now need the API key earlier).
# ---------------------------------------------------------------------------------------
$baselineFull = Resolve-RepoPath $BaselinePath
if (-not (Test-Path $baselineFull)) {
    "EXIT_REASON=BASELINE_MISSING"
    "No baseline file at '$BaselinePath'. Write your OWN read of each FP1_CANDIDATES / FP2_CANDIDATES item above BEFORE running this tool, as JSON, one key per item id (FP1 ids are '<Sub>#<Line>#<Param>', FP2 ids are the bare Sub name), values one of the verdict vocabulary above plus 'unsure'. Example:"
    '{ "A1_CvdSlopeRising#861#slopeMinUsd": "mechanism_declared_ok", "A1_CvdSlopeRising": "name_matches" }'
    "The whole point of the first run is comparing an independent human read against Jev's; looking first makes that comparison worthless permanently. See docs/harness-shadow-mode-protocol.md."
    exit 2
}
$baselineRaw = Get-Content -Raw -Path $baselineFull | ConvertFrom-Json
$baseline = @{}
if ($null -ne $baselineRaw) {
    foreach ($p in $baselineRaw.PSObject.Properties) { $baseline[$p.Name] = [string]$p.Value }
}

# FP-D24 (revision 3): the gate above is FILE-level -- any file lets every item through.
# But the residual can grow between the no-key listing and the judged run: FP-Q1 is
# nondeterministic (item 8h) and runs earlier in this same process, so a newly scoped-in,
# marked site would be judged with no seat line and spent. Refuse, item by item, before
# any FP-1/FP-2 call.
$unbaselinedItems = New-Object System.Collections.Generic.List[string]
# -Fp2Only (revision 4): FP-1 sites are not judged this run, so they are not gated either.
# @() around the WHOLE if-expression (sweep RT-U4): one residual site would otherwise unwrap
# to a bare PSCustomObject, whose .Count is $null in PS 5.1.
$fp1JudgedSites = @(if (-not $Fp2Only) { $residualSites })
foreach ($cs in $fp1JudgedSites) { $bid = Get-Fp1Id $cs; if (-not $baseline.ContainsKey($bid)) { [void]$unbaselinedItems.Add($bid) } }
foreach ($f2 in $fp2Items) { if (-not $baseline.ContainsKey($f2.SubName)) { [void]$unbaselinedItems.Add($f2.SubName) } }
if ($unbaselinedItems.Count -gt 0 -and -not $AllowUnbaselinedItems) {
    "EXIT_REASON=BASELINE_INCOMPLETE"
    "UNBASELINED_ITEMS=$($unbaselinedItems.Count) -- no line in '$BaselinePath' for:"
    foreach ($u in $unbaselinedItems) { "  $u" }
    "Nothing was judged. Add your own read for each item above, or narrow -SubFilter. For a routine re-run of items already measured once, pass -AllowUnbaselinedItems."
    exit 2
}

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    "EXIT_REASON=API_FAILED"
    "TYPESAFE_API_KEY is not set in the environment. Load typesafe.local.env first: set -a; . ./typesafe.local.env; set +a"
    exit 2
}

# ---------------------------------------------------------------------------------------
# FP-1 / FP-2 question sets. `verdict` is the ONLY answer code reads for either question
# (TRAP 2) -- the Noul diagnostics are printed alongside, never combined with `-and`/`-or`.
# ---------------------------------------------------------------------------------------
$fp1Criteria = @{
    mechanism_declared_ok     = 'The comment block declares this literal is MECHANISM (an arbitrary or structurally-motivated value, not meant to track settings.json), and that declaration is consistent with the evidence: `literal_equals_ever_shipped` may be true or false either way, so long as the comment''s own reasoning does not depend on tracking `matched_key`.'
    shipped_declared_ok       = 'The comment block declares this literal is SHIPPED BEHAVIOUR (deliberately derived from settings.json''s value for `matched_key`), and `literal_equals_ever_shipped` is true.'
    undeclared                = 'The comment block at this call site does not state which of the two classes (MECHANISM or SHIPPED BEHAVIOUR) this literal is.'
    declared_but_contradicted = 'The comment declares a class, but it is inconsistent with the evidence -- e.g. it claims SHIPPED BEHAVIOUR while `literal_equals_ever_shipped` is false, or it claims MECHANISM while its own reasoning ties the value to tracking `matched_key`.'
    ambiguous                 = '`comment_block`, `matched_key`, and `ever_shipped_values` together do not give enough information to place this call site in one of the classes above.'
}
# FP-D30 (revision 4, item 8o): FP-1 VERSION 2, asked BESIDE version 1 in the SAME call.
# Identical to $fp1Criteria except for ONE sentence, appended to the two criteria it
# concerns. The sentence and its placement are copied VERBATIM from the controlled probe
# that measured it (docs/harness-runs/fixture-parser-funding-run-2026-09-23.md section 5;
# the probe script appended $extra to mechanism_declared_ok AND declared_but_contradicted).
# Version 1 alone drives the verdict, the exit code and the baseline comparison -- so every
# earlier measurement stays comparable. Version 2 is REPORTED only, to arm a re-measure on
# a fresh, baselined population. It is NOT a fix until that re-measure says so.
$FP1_V2_EXTRA_SENTENCE = ' Naming `matched_key` or its shipped value ONLY to explain why the literal is NOT derived from it, or to show the literal differs from it or does not depend on it, is consistent with MECHANISM and is NOT a contradiction.'
$fp1CriteriaV2 = @{}
foreach ($ck in $fp1Criteria.Keys) { $fp1CriteriaV2[$ck] = $fp1Criteria[$ck] }
$fp1CriteriaV2.mechanism_declared_ok     = $fp1Criteria.mechanism_declared_ok + $FP1_V2_EXTRA_SENTENCE
$fp1CriteriaV2.declared_but_contradicted = $fp1Criteria.declared_but_contradicted + $FP1_V2_EXTRA_SENTENCE

$fp2Criteria = @{
    name_matches     = "The fixture Sub's body asserts exactly the property its name claims to test -- no more, no less."
    name_overclaims  = "The fixture Sub's name claims to test a broader or different property than what its body actually asserts."
    name_understates = "The fixture Sub's body asserts more, or something different, than what its name claims -- the name undersells what is actually being tested."
    ambiguous        = "The Sub's name or body do not give enough information to decide whether the name matches the assertion."
}

function Invoke-Fp1Verdict([string]$apiKeyIn, $cs, [string]$uid) {
    $state = @{
        fixture_sub   = $cs.EnclosingSub
        enclosing_kind = $cs.EnclosingKind
        call_line     = $cs.CallLineText
        comment_block = $cs.CommentBlock
        param_name    = $cs.Param
        literal_value = $cs.LiteralValue
        matched_key   = $cs.ResolvedKey
        ever_shipped_values = @($cs.EverShipped)
        # CODE COMPUTES SET MEMBERSHIP HERE, not Jev (docs/fixture-parser-check-spec.md
        # section 1's KEY DESIGN POINT) -- the boolean below is the authoritative answer to
        # "does the literal match a shipped value"; Jev is asked only whether the comment's
        # declared class is consistent with it, never to recompute it.
        literal_equals_ever_shipped = $cs.LiteralEqualsEverShipped
    }
    if ($uid) { $state.sample_uid = $uid }
    if ($DebugState) { Write-Host "STATE_DEBUG fp1 id=$(Get-Fp1Id $cs) keys=[$($state.Keys -join ',')] sample_uid=$($state.sample_uid)" }
    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            comment_declares_class = @{
                type = 'noul'
                instructions = "Does `comment_block` state whether the literal passed for `param_name` is MECHANISM (an arbitrary/structural value not meant to track settings.json) or SHIPPED BEHAVIOUR (deliberately derived from `matched_key`'s settings.json value)?"
            }
            class_matches_evidence = @{
                type = 'noul'
                instructions = "Is the class `comment_block` declares (if any) consistent with `literal_equals_ever_shipped` and `ever_shipped_values`? A SHIPPED BEHAVIOUR declaration is consistent only if `literal_equals_ever_shipped` is true. A MECHANISM declaration is consistent regardless of `literal_equals_ever_shipped`, so long as the comment's own reasoning does not depend on tracking `matched_key`."
            }
            verdict = @{
                type = 'choice'
                instructions = "Classify this call site using `comment_block`, `matched_key`, `ever_shipped_values`, and `literal_equals_ever_shipped`. Do not recompute set membership -- `literal_equals_ever_shipped` is already the authoritative answer to that question."
                criteria = $fp1Criteria
            }
            # FP-D30 (item 8o): version 2 -- the SAME instructions, criteria differing by the
            # one probe sentence. Reported beside version 1; never read by the exit code.
            verdict_v2 = @{
                type = 'choice'
                instructions = "Classify this call site using `comment_block`, `matched_key`, `ever_shipped_values`, and `literal_equals_ever_shipped`. Do not recompute set membership -- `literal_equals_ever_shipped` is already the authoritative answer to that question."
                criteria = $fp1CriteriaV2
            }
        }
    }
    $call = Invoke-Jev $apiKeyIn $body 3 1 $TestTransportOverride
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error; WafBlocked = $call.WafBlocked } }
    $ans = $call.Response.answers
    $verdict = $ans.verdict.choice
    $probs = @{}
    $topProbability = $null
    if ($ans.verdict.probabilities) {
        foreach ($p in $ans.verdict.probabilities.PSObject.Properties) { $probs[$p.Name] = [double]$p.Value }
        if ($probs.Count -gt 0) { $topProbability = ($probs.Values | Measure-Object -Maximum).Maximum }
    }
    $v2Verdict = $null
    $v2TopProbability = $null
    if ($ans.verdict_v2) {
        $v2Verdict = $ans.verdict_v2.choice
        if ($ans.verdict_v2.probabilities) {
            $v2Probs = @($ans.verdict_v2.probabilities.PSObject.Properties | ForEach-Object { [double]$_.Value })
            if ($v2Probs.Count -gt 0) { $v2TopProbability = ($v2Probs | Measure-Object -Maximum).Maximum }
        }
    }
    $usageIn = 0; $usageOut = 0
    if ($call.Response.usage) {
        if ($call.Response.usage.input_tokens) { $usageIn = [int]$call.Response.usage.input_tokens }
        if ($call.Response.usage.output_tokens) { $usageOut = [int]$call.Response.usage.output_tokens }
    }
    return @{
        Ok = $true; Verdict = $verdict; TopProbability = $topProbability
        V2Verdict = $v2Verdict; V2TopProbability = $v2TopProbability
        DeclaresClassNoul = $ans.comment_declares_class.noul
        MatchesEvidenceNoul = $ans.class_matches_evidence.noul
        UsageInputTokens = $usageIn; UsageOutputTokens = $usageOut
    }
}

function Invoke-Fp2Verdict([string]$apiKeyIn, $f2, [string]$uid, [switch]$TitleStripped) {
    # FP-D33 (SR-D6, revision 5): -TitleStripped selects $f2.BodyTitleStripped (every
    # Check("...") title replaced with a neutral placeholder) instead of $f2.Body. Same
    # question, same criteria, same fixture_sub -- only sub_body differs. A SEPARATE call
    # (unlike FP-1v2's second question in the SAME call): the STATE itself differs here,
    # and Invoke-Jev's state is one call's whole request.
    $bodyText = if ($TitleStripped) { $f2.BodyTitleStripped } else { $f2.Body }
    $state = @{ fixture_sub = $f2.SubName; sub_body = $bodyText }
    if ($uid) { $state.sample_uid = $uid }
    if ($DebugState) { Write-Host "STATE_DEBUG $(if ($TitleStripped) { 'fp2t' } else { 'fp2' }) sub=$($f2.SubName) keys=[$($state.Keys -join ',')] sample_uid=$($state.sample_uid)" }
    $body = @{
        model = 'jev-latest'
        state = $state
        questions = @{
            name_matches_assertion = @{
                type = 'noul'
                instructions = "Does `sub_body` assert the property `fixture_sub`'s name claims to test?"
            }
            verdict = @{
                type = 'choice'
                instructions = "Classify whether `fixture_sub`'s name matches what `sub_body` actually asserts."
                criteria = $fp2Criteria
            }
        }
    }
    $call = Invoke-Jev $apiKeyIn $body 3 1 $TestTransportOverride
    if (-not $call.Ok) { return @{ Ok = $false; Error = $call.Error; WafBlocked = $call.WafBlocked } }
    $ans = $call.Response.answers
    $verdict = $ans.verdict.choice
    $probs = @{}
    $topProbability = $null
    if ($ans.verdict.probabilities) {
        foreach ($p in $ans.verdict.probabilities.PSObject.Properties) { $probs[$p.Name] = [double]$p.Value }
        if ($probs.Count -gt 0) { $topProbability = ($probs.Values | Measure-Object -Maximum).Maximum }
    }
    $usageIn = 0; $usageOut = 0
    if ($call.Response.usage) {
        if ($call.Response.usage.input_tokens) { $usageIn = [int]$call.Response.usage.input_tokens }
        if ($call.Response.usage.output_tokens) { $usageOut = [int]$call.Response.usage.output_tokens }
    }
    return @{
        Ok = $true; Verdict = $verdict; TopProbability = $topProbability
        NameMatchesNoul = $ans.name_matches_assertion.noul
        UsageInputTokens = $usageIn; UsageOutputTokens = $usageOut
    }
}

# FP-D4 self-consistency aggregation -- mirrors tools/checks/commit-walker.ps1's
# Get-SelfConsistencyAggregate (defined locally here rather than shared: it is arithmetic,
# not the HTTP call, and docs/fixture-parser-check-spec.md section 4 only asks to reuse
# Invoke-Jev, not this function).
function Get-SelfConsistencyAggregate([object[]]$sampleResultsIn) {
    $verdictCounts = @{}
    foreach ($sr in $sampleResultsIn) {
        if (-not $verdictCounts.ContainsKey($sr.Verdict)) { $verdictCounts[$sr.Verdict] = 0 }
        $verdictCounts[$sr.Verdict] += 1
    }
    $pluralityVerdict = $null
    $pluralityCount = -1
    foreach ($sr in $sampleResultsIn) {
        $v = $sr.Verdict
        if ($verdictCounts[$v] -gt $pluralityCount) { $pluralityCount = $verdictCounts[$v]; $pluralityVerdict = $v }
    }
    $agreementRate = [math]::Round(($pluralityCount / $sampleResultsIn.Count), 3)
    $topProbs = @($sampleResultsIn | Where-Object { $null -ne $_.TopProbability } | ForEach-Object { $_.TopProbability })
    $meanTopProbability = if ($topProbs.Count -gt 0) { [math]::Round((($topProbs | Measure-Object -Average).Average), 3) } else { $null }
    $minTopProbability = if ($topProbs.Count -gt 0) { [math]::Round((($topProbs | Measure-Object -Minimum).Minimum), 3) } else { $null }
    $sampleVerdicts = ($sampleResultsIn | ForEach-Object { $_.Verdict }) -join ','
    $sampleTopProbs = ($sampleResultsIn | ForEach-Object { if ($null -ne $_.TopProbability) { [math]::Round([double]$_.TopProbability, 3) } else { $null } }) -join ','
    return [PSCustomObject]@{
        PluralityVerdict = $pluralityVerdict; AgreementRate = $agreementRate; Stable = ($agreementRate -ge 1.0)
        MeanTopProbability = $meanTopProbability; MinTopProbability = $minTopProbability
        SampleCount = $sampleResultsIn.Count; SampleVerdicts = $sampleVerdicts; SampleTopProbabilities = $sampleTopProbs
    }
}

$fp1Results = New-Object System.Collections.Generic.List[object]
$fp2Results = New-Object System.Collections.Generic.List[object]
$apiFailed = $false
$apiFailMsg = ''
$usageInputTokens = $scopeUsageIn
$usageOutputTokens = $scopeUsageOut
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# ---------------------------------------------------------------------------------------
# FP-1: sample the verdict question $Samples times per residual (provenance-commented,
# in-scope) call site. FP-Q3 already resolved matched_key/ever_shipped -- no per-site Jev
# key-resolution call any more (FP-D7 superseded, see header).
# ---------------------------------------------------------------------------------------
foreach ($cs in $fp1JudgedSites) {   # -Fp2Only empties this (revision 4)
    $sampleResults = New-Object System.Collections.Generic.List[object]
    $itemBlocked = $false
    for ($i = 0; $i -lt $Samples; $i++) {
        $uid = "$(Get-Fp1Id $cs):$i`:$([guid]::NewGuid().ToString('N').Substring(0,8))"
        $r = Invoke-Fp1Verdict $apiKey $cs $uid
        if (-not $r.Ok) {
            # FP-D26: a web-firewall block is about THIS item's text, not the API. Record the
            # item as unjudgeable and move on; any other failure still aborts the run.
            if ($r.WafBlocked) { $itemBlocked = $true; break }
            $apiFailed = $true; $apiFailMsg = "Jev FP-1 request failed for $(Get-Fp1Id $cs) (sample $($i+1)/$Samples): $($r.Error)"; break
        }
        $usageInputTokens += $r.UsageInputTokens
        $usageOutputTokens += $r.UsageOutputTokens
        $sampleResults.Add($r)
    }
    if ($apiFailed) { break }
    if ($itemBlocked) {
        $idB = Get-Fp1Id $cs
        $fp1Results.Add([PSCustomObject]@{
            Id = $idB; EnclosingSub = $cs.EnclosingSub; Param = $cs.Param; Line = $cs.Line
            MatchedKey = $cs.ResolvedKey; EverShipped = ($cs.EverShipped -join ',')
            LiteralValue = $cs.LiteralValue; LiteralEqualsEverShipped = $cs.LiteralEqualsEverShipped
            MarkerScope = $cs.MarkerScope
            Verdict = 'WAF_BLOCKED'; AgreementRate = $null; Stable = $true
            MeanTopProbability = $null; MinTopProbability = $null
            DeclaresClassNoul = $null; MatchesEvidenceNoul = $null
            SampleCount = 0; SampleVerdicts = ''; SampleTopProbabilities = ''
            V2Verdict = $null; V2AgreementRate = $null; V2Stable = $null; V2MeanTopProbability = $null
            V2SampleVerdicts = ''; SamplesV1EqV2 = 0
            Baseline = if ($baseline.ContainsKey($idB)) { $baseline[$idB] } else { $null }
        })
        continue
    }

    $agg = Get-SelfConsistencyAggregate $sampleResults
    $meanDeclares = [math]::Round((($sampleResults | ForEach-Object { [double]$_.DeclaresClassNoul } | Measure-Object -Average).Average), 3)
    $meanMatches = [math]::Round((($sampleResults | ForEach-Object { [double]$_.MatchesEvidenceNoul } | Measure-Object -Average).Average), 3)
    # FP-D30: version 2's own self-consistency over the SAME samples, computed by the same
    # aggregation function. Reported only -- nothing below reads it for the exit code.
    $v2Samples = @($sampleResults | ForEach-Object { [PSCustomObject]@{ Verdict = $(if ($_.V2Verdict) { $_.V2Verdict } else { 'NO_V2_ANSWER' }); TopProbability = $_.V2TopProbability } })
    $agg2 = Get-SelfConsistencyAggregate $v2Samples
    $samplesV1EqV2 = @($sampleResults | Where-Object { $_.V2Verdict -and $_.V2Verdict -eq $_.Verdict }).Count
    $id = Get-Fp1Id $cs
    $fp1Results.Add([PSCustomObject]@{
        Id = $id; EnclosingSub = $cs.EnclosingSub; Param = $cs.Param; Line = $cs.Line
        MatchedKey = $cs.ResolvedKey; EverShipped = ($cs.EverShipped -join ',')
        LiteralValue = $cs.LiteralValue; LiteralEqualsEverShipped = $cs.LiteralEqualsEverShipped
        MarkerScope = $cs.MarkerScope
        Verdict = $agg.PluralityVerdict; AgreementRate = $agg.AgreementRate; Stable = $agg.Stable
        MeanTopProbability = $agg.MeanTopProbability; MinTopProbability = $agg.MinTopProbability
        DeclaresClassNoul = $meanDeclares; MatchesEvidenceNoul = $meanMatches
        SampleCount = $agg.SampleCount; SampleVerdicts = $agg.SampleVerdicts; SampleTopProbabilities = $agg.SampleTopProbabilities
        V2Verdict = $agg2.PluralityVerdict; V2AgreementRate = $agg2.AgreementRate; V2Stable = $agg2.Stable
        V2MeanTopProbability = $agg2.MeanTopProbability; V2SampleVerdicts = $agg2.SampleVerdicts
        SamplesV1EqV2 = $samplesV1EqV2
        Baseline = if ($baseline.ContainsKey($id)) { $baseline[$id] } else { $null }
    })
}

# ---------------------------------------------------------------------------------------
# FP-2: one sampled question set per distinct residual sub (FP-D5 scope).
# ---------------------------------------------------------------------------------------
if (-not $apiFailed) {
    foreach ($f2 in $fp2Items) {
        $sampleResults = New-Object System.Collections.Generic.List[object]
        # FP-D33 (SR-D6): FP-2t's own samples, collected beside FP-2's in the same loop.
        $sampleResultsT = New-Object System.Collections.Generic.List[object]
        $fp2tWafBlocked = $false
        $itemBlocked = $false
        for ($i = 0; $i -lt $Samples; $i++) {
            $uid = "$($f2.SubName):$i`:$([guid]::NewGuid().ToString('N').Substring(0,8))"
            $r = Invoke-Fp2Verdict $apiKey $f2 $uid
            if (-not $r.Ok) {
                if ($r.WafBlocked) { $itemBlocked = $true; break }   # FP-D26
                $apiFailed = $true; $apiFailMsg = "Jev FP-2 request failed for $($f2.SubName) (sample $($i+1)/$Samples): $($r.Error)"; break
            }
            $usageInputTokens += $r.UsageInputTokens
            $usageOutputTokens += $r.UsageOutputTokens
            $sampleResults.Add($r)

            # FP-D33 (SR-D6): FP-2t, reported beside FP-2, never read by the exit code or
            # FP2_BAD_VERDICTS -- the same footing FP-1v2 (FP-D30) holds beside FP-1. A WAF
            # block on the titled call is recorded (FP2T_WAF_BLOCKED) and that one sample
            # dropped; it does not break this loop, because FP-2's own samples must not be
            # shortened by FP-2t's failure.
            $uidT = "$($f2.SubName):t:$i`:$([guid]::NewGuid().ToString('N').Substring(0,8))"
            $rt = Invoke-Fp2Verdict $apiKey $f2 $uidT -TitleStripped
            if (-not $rt.Ok) {
                if ($rt.WafBlocked) { $fp2tWafBlocked = $true }
                else { $apiFailed = $true; $apiFailMsg = "Jev FP-2t request failed for $($f2.SubName) (sample $($i+1)/$Samples): $($rt.Error)"; break }
            } else {
                $usageInputTokens += $rt.UsageInputTokens
                $usageOutputTokens += $rt.UsageOutputTokens
                $sampleResultsT.Add($rt)
            }
        }
        if ($apiFailed) { break }
        if ($itemBlocked) {
            $fp2Results.Add([PSCustomObject]@{
                SubName = $f2.SubName; Verdict = 'WAF_BLOCKED'; AgreementRate = $null; Stable = $true
                MeanTopProbability = $null; MinTopProbability = $null; NameMatchesNoul = $null
                SampleCount = 0; SampleVerdicts = ''; SampleTopProbabilities = ''
                Fp2tVerdict = $null; Fp2tAgreementRate = $null; Fp2tStable = $null
                Fp2tMeanTopProbability = $null; Fp2tSampleVerdicts = ''; Fp2tWafBlocked = $false
                Baseline = if ($baseline.ContainsKey($f2.SubName)) { $baseline[$f2.SubName] } else { $null }
            })
            continue
        }
        $agg = Get-SelfConsistencyAggregate $sampleResults
        $meanNameMatches = [math]::Round((($sampleResults | ForEach-Object { [double]$_.NameMatchesNoul } | Measure-Object -Average).Average), 3)
        # FP-D33: FP-2t's own aggregate, over whatever samples survived a WAF block. All
        # blocked (zero survivors) reports Fp2tVerdict=WAF_BLOCKED, the same vocabulary
        # FP-2's own item-level block uses.
        $aggT = if ($sampleResultsT.Count -gt 0) { Get-SelfConsistencyAggregate $sampleResultsT } else { $null }
        $fp2Results.Add([PSCustomObject]@{
            SubName = $f2.SubName
            Verdict = $agg.PluralityVerdict; AgreementRate = $agg.AgreementRate; Stable = $agg.Stable
            MeanTopProbability = $agg.MeanTopProbability; MinTopProbability = $agg.MinTopProbability
            NameMatchesNoul = $meanNameMatches
            SampleCount = $agg.SampleCount; SampleVerdicts = $agg.SampleVerdicts; SampleTopProbabilities = $agg.SampleTopProbabilities
            Fp2tVerdict = if ($aggT) { $aggT.PluralityVerdict } else { 'WAF_BLOCKED' }
            Fp2tAgreementRate = if ($aggT) { $aggT.AgreementRate } else { $null }
            Fp2tStable = if ($aggT) { $aggT.Stable } else { $true }
            Fp2tMeanTopProbability = if ($aggT) { $aggT.MeanTopProbability } else { $null }
            Fp2tSampleVerdicts = if ($aggT) { $aggT.SampleVerdicts } else { '' }
            Fp2tWafBlocked = $fp2tWafBlocked
            Baseline = if ($baseline.ContainsKey($f2.SubName)) { $baseline[$f2.SubName] } else { $null }
        })
    }
}
$sw.Stop()

if ($apiFailed) {
    Write-Coverage ($fp1Results.Count + $fp2Results.Count)
    "EXIT_REASON=API_FAILED"
    $apiFailMsg
    exit 2
}

# ---------------------------------------------------------------------------------------
# Step 7 (section 4.1 step 7): report, exit from `verdict`/stability alone.
# ---------------------------------------------------------------------------------------
Write-Coverage ($fp1Results.Count + $fp2Results.Count)
"KEY_RESOLUTION_CALLS=0 (FP-D7 superseded by FP-Q3 -- see header)"
"SCOPE_JEV_CALLS=$scopeJevCalls"
"USAGE_INPUT_TOKENS=$usageInputTokens"
"USAGE_OUTPUT_TOKENS=$usageOutputTokens"
"WALL_TIME_SEC=$([math]::Round($sw.Elapsed.TotalSeconds,2))"

$fp1Unstable = @($fp1Results | Where-Object { -not $_.Stable }).Count
$fp2Unstable = @($fp2Results | Where-Object { -not $_.Stable }).Count
# FP-D25 (revision 3, item 8m): shipped_declared_ok is NOT a pass on an FP-1 site. CLAUDE.md's
# provenance rule says SHIPPED BEHAVIOUR "must derive the value from cfg, never hardcode it",
# and every FP-1 site is a named-argument LITERAL. So the verdict means a rule breach or a
# misread -- measured: two misreads on A23b passed a run with exit 0. The question sent to
# Jev is unchanged (changing it would need a fresh measurement); only the scoring moves.
$fp1ShippedOnLiteral = @($fp1Results | Where-Object { $_.Verdict -eq 'shipped_declared_ok' }).Count
$fp1Bad = @($fp1Results | Where-Object { $_.Verdict -in @('undeclared', 'declared_but_contradicted', 'ambiguous', 'shipped_declared_ok') }).Count
$fp2Bad = @($fp2Results | Where-Object { $_.Verdict -in @('name_overclaims', 'name_understates', 'ambiguous') }).Count
# FP-D26: an unjudged item is not a pass. Counted apart, and folded into the exit code below.
$fp1WafBlocked = @($fp1Results | Where-Object { $_.Verdict -eq 'WAF_BLOCKED' }).Count
$fp2WafBlocked = @($fp2Results | Where-Object { $_.Verdict -eq 'WAF_BLOCKED' }).Count

if ($CountersOnly) {
    "COUNTERS_ONLY=true (per-item verdicts and aggregates withheld -- docs/harness-shadow-mode-protocol.md section 4a)"
} else {
    "FP1_RESULTS:"
    foreach ($res in $fp1Results) {
        $agree = if ($res.Verdict -eq 'WAF_BLOCKED') { 'NOT_JUDGED' } elseif ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' } elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' } elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' } else { 'DISAGREE' }
        $stableTxt = if ($res.Stable) { 'STABLE' } else { 'UNSTABLE' }
        "  $($res.Id) [$stableTxt] verdict=$($res.Verdict) agreement_rate=$($res.AgreementRate) mean_top_prob=$($res.MeanTopProbability) min_top_prob=$($res.MinTopProbability) key=$($res.MatchedKey) ever_shipped=[$($res.EverShipped)] literal=$($res.LiteralValue) equals_shipped=$($res.LiteralEqualsEverShipped) baseline=$($res.Baseline) [$agree] declares_class_noul=$($res.DeclaresClassNoul) matches_evidence_noul=$($res.MatchesEvidenceNoul) verdicts=[$($res.SampleVerdicts)] top_probs=[$($res.SampleTopProbabilities)] marker=$($res.MarkerScope)"
        # FP-D30: version 2 beside it. Informational; the [AGREE]/[DISAGREE] tag above is v1's.
        "      v2: verdict=$($res.V2Verdict) agreement_rate=$($res.V2AgreementRate) mean_top_prob=$($res.V2MeanTopProbability) verdicts=[$($res.V2SampleVerdicts)] samples_v1_eq_v2=$($res.SamplesV1EqV2)/$($res.SampleCount)"
    }
    "FP2_RESULTS:"
    foreach ($res in $fp2Results) {
        $agree = if ($res.Verdict -eq 'WAF_BLOCKED') { 'NOT_JUDGED' } elseif ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' } elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' } elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' } else { 'DISAGREE' }
        $stableTxt = if ($res.Stable) { 'STABLE' } else { 'UNSTABLE' }
        "  $($res.SubName) [$stableTxt] verdict=$($res.Verdict) agreement_rate=$($res.AgreementRate) mean_top_prob=$($res.MeanTopProbability) min_top_prob=$($res.MinTopProbability) name_matches_noul=$($res.NameMatchesNoul) baseline=$($res.Baseline) [$agree] verdicts=[$($res.SampleVerdicts)] top_probs=[$($res.SampleTopProbabilities)]"
        # FP-D33 (SR-D6): FP-2t beside it. Informational; the [AGREE]/[DISAGREE] tag above is FP-2's.
        "      t: verdict=$($res.Fp2tVerdict) agreement_rate=$($res.Fp2tAgreementRate) mean_top_prob=$($res.Fp2tMeanTopProbability) verdicts=[$($res.Fp2tSampleVerdicts)] waf_blocked=$($res.Fp2tWafBlocked)"
    }
    "FP1_UNSTABLE=$fp1Unstable"
    "FP2_UNSTABLE=$fp2Unstable"
    "FP1_BAD_VERDICTS=$fp1Bad"
    "FP1_SHIPPED_ON_LITERAL=$fp1ShippedOnLiteral (counted in FP1_BAD_VERDICTS -- FP-D25: a hardcoded literal cannot be valid SHIPPED BEHAVIOUR; a breach or a misread)"
    "FP2_BAD_VERDICTS=$fp2Bad"
    "FP1_WAF_BLOCKED=$fp1WafBlocked"
    "FP2_WAF_BLOCKED=$fp2WafBlocked"
    # FP-D30 (item 8o): version 1 against version 2. Informational only -- none of these
    # reaches $anyBad below.
    $fp1Judged = @($fp1Results | Where-Object { $_.Verdict -ne 'WAF_BLOCKED' })
    "FP1V2_ITEMS_SAME_PLURALITY_AS_V1=$(@($fp1Judged | Where-Object { $_.V2Verdict -eq $_.Verdict }).Count) of $($fp1Judged.Count)"
    "FP1V2_SAMPLES_SAME_AS_V1=$(($fp1Judged | Measure-Object -Property SamplesV1EqV2 -Sum).Sum) of $(($fp1Judged | Measure-Object -Property SampleCount -Sum).Sum)"
    "FP1V2_UNSTABLE=$(@($fp1Judged | Where-Object { $_.V2Stable -eq $false }).Count) (informational -- version 1 alone drives the exit code)"
    # FP-D33 (SR-D6): FP-2 against FP-2t. Informational only -- none of these reaches $anyBad.
    $fp2Judged = @($fp2Results | Where-Object { $_.Verdict -ne 'WAF_BLOCKED' })
    "FP2T_ITEMS_SAME_PLURALITY_AS_FP2=$(@($fp2Judged | Where-Object { $_.Fp2tVerdict -eq $_.Verdict }).Count) of $($fp2Judged.Count)"
    "FP2T_WAF_BLOCKED=$(@($fp2Judged | Where-Object { $_.Fp2tWafBlocked }).Count)"
}
if ($Fp2Only) { "FP2_ONLY=true (FP-1 sites listed above were NOT judged and NOT gated this run)" }

# Markdown report.
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add('# Fixture-parser check report')
$reportLines.Add('')
$reportLines.Add("Generated by ``tools/checks/fixture-parser.ps1`` against ``-SubFilter $SubFilter -Samples $Samples`` at ``HEAD``.")
$reportLines.Add('')
$reportLines.Add('## Coverage')
$reportLines.Add('')
$reportLines.Add('| Metric | Value |')
$reportLines.Add('|---|---|')
$reportLines.Add("| FIXTURE_SUBS | $fixtureSubs |")
$reportLines.Add("| LITERAL_CALL_SITES | $literalCallSites |")
$reportLines.Add("| PARAMS_DISTINCT | $paramsDistinct |")
$reportLines.Add("| SETTINGS_REVISIONS_WALKED | $settingsRevisionsWalked |")
$reportLines.Add("| KEYS_WITH_VALUE_HISTORY | $keysWithValueHistory |")
$reportLines.Add("| MATCHED_ONE_KEY | $matchedOne |")
$reportLines.Add("| MATCHED_ZERO_KEYS | $matchedZero |")
$reportLines.Add("| MATCHED_MULTI_KEYS | $matchedMulti |")
$reportLines.Add("| LITERAL_EQUALS_EVER_SHIPPED | $literalEqualsShipped |")
$reportLines.Add("| SITES_WITH_PROVENANCE_COMMENT | $sitesWithProvenanceAfter |")
$reportLines.Add("| SITES_JUDGED | $($fp1Results.Count + $fp2Results.Count) |")
$reportLines.Add("| MAPPING_NAMED | $mappingNamed |")
$reportLines.Add("| MAPPING_ALIASED | $mappingAliased |")
$reportLines.Add("| MAPPING_POSITIONAL | $mappingPositional |")
$reportLines.Add("| MAPPING_NO_PRODUCTION_CALL_SITE_SITES | $mappingNoProdSites |")
$reportLines.Add("| METHODS_NO_PRODUCTION_CALL_SITE | $($methodsNoProdCallSite.Count) |")
$reportLines.Add("| IN_SCOPE_SITES | $inScopeSites |")
$reportLines.Add("| OUT_OF_SCOPE_SITES | $outOfScopeSites |")
$reportLines.Add("| SCOPE_JEV_CALLS | $scopeJevCalls |")
$reportLines.Add("| SITES_GAINED_COVERAGE (FP-Q2) | $sitesGainedCoverage |")
if (-not $CountersOnly) {
    $reportLines.Add("| FP1_UNSTABLE | $fp1Unstable |")
    $reportLines.Add("| FP2_UNSTABLE | $fp2Unstable |")
    $reportLines.Add("| FP1_BAD_VERDICTS | $fp1Bad |")
    $reportLines.Add("| FP2_BAD_VERDICTS | $fp2Bad |")
}
$reportLines.Add("| USAGE_INPUT_TOKENS | $usageInputTokens |")
$reportLines.Add("| USAGE_OUTPUT_TOKENS | $usageOutputTokens |")
$reportLines.Add("| JEV_MODEL | $((Get-JevModelLine) -replace '^JEV_MODEL ', '') |")
$reportLines.Add("| WALL_TIME_SEC | $([math]::Round($sw.Elapsed.TotalSeconds,2)) |")
$reportLines.Add('')
if (-not $CountersOnly) {
    $reportLines.Add('## FP-1 (fixture-literal provenance)')
    $reportLines.Add('')
    $reportLines.Add('| Id | Verdict | Agreement | Mean top prob | Key | Ever shipped | Literal | Equals shipped | Baseline | Agreement | Marker | v2 verdict (informational) | v2 agreement |')
    $reportLines.Add('|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    foreach ($res in $fp1Results) {
        $agree = if ($res.Verdict -eq 'WAF_BLOCKED') { 'NOT_JUDGED' } elseif ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' } elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' } elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' } else { 'DISAGREE' }
        $reportLines.Add("| $($res.Id) | $($res.Verdict) | $($res.AgreementRate) | $($res.MeanTopProbability) | $($res.MatchedKey) | $($res.EverShipped) | $($res.LiteralValue) | $($res.LiteralEqualsEverShipped) | $($res.Baseline) | $agree | $($res.MarkerScope) | $($res.V2Verdict) | $($res.V2AgreementRate) |")
    }
    $reportLines.Add('')
    $reportLines.Add('## FP-2 (fixture name vs. assertion)')
    $reportLines.Add('')
    $reportLines.Add('| Sub | Verdict | Agreement | Mean top prob | Baseline | Agreement | t verdict (informational) | t agreement | t mean top prob |')
    $reportLines.Add('|---|---|---|---|---|---|---|---|---|')
    foreach ($res in $fp2Results) {
        $agree = if ($res.Verdict -eq 'WAF_BLOCKED') { 'NOT_JUDGED' } elseif ($null -eq $res.Baseline) { 'NO_BASELINE_VALUE' } elseif ($res.Baseline -eq 'unsure') { 'OPERATOR_UNSURE' } elseif ($res.Baseline -eq $res.Verdict) { 'AGREE' } else { 'DISAGREE' }
        $reportLines.Add("| $($res.SubName) | $($res.Verdict) | $($res.AgreementRate) | $($res.MeanTopProbability) | $($res.Baseline) | $agree | $($res.Fp2tVerdict) | $($res.Fp2tAgreementRate) | $($res.Fp2tMeanTopProbability) |")
    }
} else {
    $reportLines.Add('## Per-item detail withheld')
    $reportLines.Add('')
    $reportLines.Add('Run with `-CountersOnly` over a RESERVED window (docs/harness-shadow-mode-protocol.md section 4a). Per-item verdicts and every aggregate are withheld from both console and this report.')
}
$reportFull = Resolve-RepoPath $OutPath
Set-Content -Encoding UTF8 -Path $reportFull -Value ($reportLines -join "`r`n")
"Report written to $OutPath"

$anyBad = ($fp1Bad -gt 0) -or ($fp2Bad -gt 0) -or ($fp1Unstable -gt 0) -or ($fp2Unstable -gt 0) -or ($fp1WafBlocked -gt 0) -or ($fp2WafBlocked -gt 0)
if ($anyBad) { exit 1 } else { exit 0 }
