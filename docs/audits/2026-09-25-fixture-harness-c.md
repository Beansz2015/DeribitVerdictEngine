# Fixture harness audit — commit `6e74181`, `verify/ordercheck/Program.vb` lines 11201–16764 ("fixture-harness-c")

Hostile audit of the test harness that is the only automated check before engine code ships.
Audited tree: `/tmp` is not writable on this host, so the worktree was placed at
`C:/Dev/audit-6e74181` (`git worktree add C:/Dev/audit-6e74181 6e74181`) instead of
`/tmp/audit-6e74181` — same commit, same content, Windows path only. No code was fixed or edited.
Isolated test code was not needed to prove the findings below; the existing harness itself,
run with a documented environment variable, proves them.

## Scope not read in full

- Every line of `verify/ordercheck/Program.vb` 11201–16764 was read (in the worktree, at `6e74181`).
  This is the assigned range and the only range this report makes claims about "in range."
- **Not read in full** — read only via `grep`, targeted `sed`/`Read` excerpts, or not opened at all:
  - `Core/SignalEmitter.vb`, `Core/Indicators_OrderFlow.vb`, `Core/Indicators_Structure.vb`,
    `Core/ScoringEngine_Calculate_Verdict.vb`, `Core/ScoringEngine_Calculate_Scoring.vb` — the
    production files the flagged fixtures exercise. I read the specific line ranges the fixtures'
    own comments cite (e.g. `SignalEmitter.vb:330-332`, `Indicators_OrderFlow.vb:266-274`) as
    reported by the fixtures and by `docs/medium-tier-bug-hunt-2026-09-16.md`; I did not read
    either file end to end and did not independently re-derive the POC-tier gate logic from a
    cold read of `SignalEmitter.vb`.
  - `settings.json` full commit history: `git log -p --follow -- settings.json` at `6e74181` was
    dumped to a scratch file (5,764 lines) and used for targeted `grep` lookups against specific
    keys named in this range's fixtures. It was **not** read end to end, and I did not
    cross-check every numeric literal in every fixture against it — only the ones flagged below
    and a handful of spot checks.
  - `tools/checks/verify-gate.ps1` — read only the `Build`/harness-invocation section (lines
    ~60–100) and `grep`-checked the whole file plus `.github/workflows/*.yml` for the string
    `ORDERCHECK_KNOWN_DEFECTS`. Not read end to end.
  - `docs/medium-tier-bug-hunt-2026-09-16.md`, `docs/medium-tier-bug-hunt-spec-back.md`,
    `docs/medium-tier-bug-hunt-2026-09-16-liquidation-output.md`,
    `docs/medium-tier-bug-hunt-2026-09-16-census-output.md`,
    `docs/medium-tier-bug-hunt-2026-09-16-rescore-output.md` — not read; only
    `...-poc-gate-output.md` was read, for the production-incidence numbers quoted below.
  - Lines 736 and 742 of `Program.vb` (outside the assigned range) were `grep`-located only, to
    confirm the `ORDERCHECK_KNOWN_DEFECTS` convention already existed before this range; the two
    fixtures there were not read and are not assessed by this report.
  - `docs/DeribitIndicatorProject.md`, `docs/architecture.md`, `docs/trader-profile.md` — not
    read this session; the "Step 4c VPFR HVN cap" pipeline-stage name is taken from
    `CLAUDE.md`'s architecture summary, not verified against the primary doc.

## `--- LOGIC TRACE ---` (scratchpad, as required before any finding)

Scenario: **BTC-PERPETUAL, −1.5 % flush, ATR(7) rising 30 → 140** (a session opening quietly then
breaking hard — the shape a stop-hunt or a liquidation cascade produces). Three fixtures picked as
closest to the order payload or the settings loader, and what each would **not** catch here.

**1. `A82b_MirroredMarketGivesMirroredVerdictAndLevels` / `A82CheckOne` (line 16576 / 16695)** —
the fixture that calls `ScoringEngine.Calculate` and `SignalEmitter.ComputeSideLevels` directly,
i.e. the literal target/stop/side computation that becomes the order payload. `A82Base`
(line 16273) hardcodes `r.ATR = 100` — **one fixed value, never varied within a check, and never
moved across two points in time.** The whole fixture is a same-instant symmetry test: build one
snapshot, mirror it, and diff two `Calculate()` calls against each other. It has no way to
represent "ATR was 30 two minutes ago and is 140 now" — there is no second, later snapshot to
disagree with the first. So it cannot catch: (a) a stop or target computed from an ATR reading
that is stale relative to the price move that already happened (the exact shape of a flush,
where the realized range outruns the indicator's own lookback); (b) any defect that is
**symmetric** between long and short — a systematic overshoot or undershoot in how the ATR
multiplier scales the stop distance at ATR=140 vs ATR=30 would apply equally to both mirrored
runs and cancel out of every diff this fixture makes, because A82's entire detection mechanism is
"long run disagrees with short run," never "this run disagrees with a known-correct absolute
value" for anything but a handful of pinned corner cases. Read from code (`A82Base` line 16275).

**2. `A80b_PocGateOpensOnTheWallSideItsSpecNames` (line 15885)** — the only fixture in range that
exercises the VPFR/POC target-capping path (architecture's Step 4c) end to end through the real
`SignalEmitter.ComputeSideLevels`. Two independent reasons it catches nothing in this scenario.
First, structural: it is gated behind `Environment.GetEnvironmentVariable("ORDERCHECK_KNOWN_DEFECTS") <> "1"`
(line 15886) and prints `SKIP` otherwise; `tools/checks/verify-gate.ps1` invokes the harness with
a bare `dotnet run` and never sets that variable (confirmed by `grep`, see Finding 1), so in every
real CI run — flush or no flush — this fixture contributes nothing. Second, even run explicitly
(which I did — see "Harness run" below), its own ATR is `bs / 2.0` (line 15903), a small constant
derived from the fixture's own synthetic bucket size, not a rising 30→140 series; a violently
expanding ATR multiplies directly into `bound = sl.TargetMaxAtrMult * r.ATR` (line 15907), the
gate's own tolerance window, so the fixture's static, small ATR cannot show whether the confirmed
label-inversion defect's **blast radius grows** exactly when ATR is spiking (a wider `bound`
admits more `pocDist` values, i.e. more of exactly this scenario's price action would fall inside
the gate's geometry check) — which is the opposite of a reassuring "small, contained" defect
under stress. Read from code; the defect itself is proven by a run (Finding 1).

**3. `A50h_ScoringSurfacePinThroughRealCalculate` (line 15615)** — the fixture in the
settings-loader family that reaches the real `ScoringEngine.Calculate()`, i.e. the closest thing
in this range to "does a settings change reach the order." It uses `BuildA8Indicators()`
(line 1140), which hardcodes `r.ATR = 50` in a single static `IndicatorResults` built once per
`Check()` call. It says nothing about **when** a `settings.local.json` hot-reload (A50f,
line 15540) lands relative to a live, moving market — CLAUDE.md's own standing risk note for
`settings.json` changes is "it hot-reloads onto a live collector; the version edge lands
mid-`InstanceId` and becomes unfilterable." A50f proves the reload eventually happens (polled over
~10 s of wall clock with nothing else running); it does not run that reload concurrently with a
`RunAnalysisAsync()` call whose `IndicatorResults`/ATR are changing candle to candle, so it cannot
show whether an operator's mid-flush settings edit (e.g. disabling `mtf_gate` to stop trading) is
guaranteed to apply before the next verdict is computed, or could race a run already in flight.
Read from code.

**What this trace does not claim:** none of the three fixtures is wrong about what it asserts —
all three pass, honestly, on the property each states. The gap is that **no fixture in this range
varies ATR across time within a single check**, and the one fixture built to catch the confirmed
POC-target defect is structurally excluded from ever running in CI.

## Harness run (proof)

Built and ran inside the worktree, `.NET SDK 9.0.318`, `net8.0` target (no download needed — the
SDK was already installed on this host; `apt-get`/the Microsoft download host were not required).

**Default run** (what `tools/checks/verify-gate.ps1` actually executes):

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

Last 20 lines:

```
[SettingsLoader] settings.local.json: 'trade_store.enabledd' is admitted but the BASE HAS NO SUCH KEY — merged, but it will have NO EFFECT unless a POCO field matches. Check for a typo.
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): trade_store.enabledd: (absent) -> false [NO EFFECT] · live_strip.enabled: true -> false  [1 admitted key(s) absent from the base — see the warning above]
[SettingsLoader] settings.local.json: 'trade_store.enabledd' is admitted but the BASE HAS NO SUCH KEY — merged, but it will have NO EFFECT unless a POCO field matches. Check for a typo.
[SettingsLoader] settings.local.json present but overrode no key the base carries — base settings in force
PASS  A50k admitted-but-absent key — warned, excluded from +local, base untouched; a real sibling key still applies and still activates
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): auto_run.start_engaged: true -> false
[SettingsLoader] settings.local.json ACTIVE — 1 override(s): auto_run.start_engaged: true -> false
PASS  A58c overlay admits auto_run.start_engaged key-granular (base true -> overlaid false), and the tweaker fence still rejects it (auto_run. stays whole-block fenced)
[SettingsLoader] Re-read settings.local.json — overlay present: True · active: True

ALL PASS

[exited with code 0]
```

425 `PASS`, 0 `FAIL`, exit code 0 — this is what ships.

**Second run, `ORDERCHECK_KNOWN_DEFECTS=1`** (never set by CI or the pre-push hook — see Finding 1):

```
ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build
```

The four lines this run adds, verbatim:

```
FAIL  A80b long: the POC is the only structure on the target side (100020.0 vs entry 100005.0, label NEAR_HVN_SUPPORT) → the HVN-gated POC tier places at it, as 721882e specifies ('wall above price') — KNOWN DEFECT: got target 100040.0 (FALLBACK_ATR). Core/SignalEmitter.vb:331 opens the long POC tier only on NEAR_HVN_RESIST or IN_LVN_BEAR, which CalcVPFRLite emits when the POC sits on the OTHER side of price
FAIL  A80b short: the POC is the only structure on the target side (100020.0 vs entry 100035.0, label NEAR_HVN_RESIST) → the HVN-gated POC tier places at it, as 721882e specifies ('floor below price') — KNOWN DEFECT: got target 100000.0 (FALLBACK_ATR). Core/SignalEmitter.vb:332 opens the short POC tier only on NEAR_HVN_SUPPORT or IN_LVN_BULL, which CalcVPFRLite emits when the POC sits on the OTHER side of price
FAIL  A81b maker-side liquidation (flag M, taker buy): the liquidated maker held a LONG → LONG LIQS, per Deribit's `liquidation` field — KNOWN DEFECT: got signal=SHORT LIQS long=0 short=1000. Core/Indicators_OrderFlow.vb:268-272 books every flagged trade by the taker's direction
FAIL  A81b maker-side liquidation (flag M, taker sell): the liquidated maker held a SHORT → SHORT LIQS, per Deribit's `liquidation` field — KNOWN DEFECT: got signal=LONG LIQS long=1000 short=0. Core/Indicators_OrderFlow.vb:268-272 books every flagged trade by the taker's direction

4 FAILURE(S)
```

Both confirmed defects still reproduce at `6e74181`, exactly as their comments describe.
**Proven by a run I did**, not read from code alone.

---

## Findings

### Finding 1 — S1 — the harness has a permanent, unmonitored escape hatch for confirmed scoring defects, and it currently shelters a live one

**LOCATION:** `verify/ordercheck/Program.vb:15885-15887` (A80b gate), `:15976-15979` (A81b gate),
`tools/checks/verify-gate.ps1:71-84` (the gate invocation), all at `6e74181`. The same convention
already exists at lines 736 and 742 (outside the assigned range, not otherwise assessed here).

**DOWNSTREAM IMPACT:** `verify-gate.ps1` is, by `CLAUDE.md`'s own description, "the only
automated check before engine code ships." Its harness section (`:71-84`) runs
`dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build` with no
environment variable set, and passes the gate on `$out -match 'ALL PASS'` with exit code 0. A
fixture that starts `If Environment.GetEnvironmentVariable("ORDERCHECK_KNOWN_DEFECTS") <> "1" Then ... Return`
is therefore **permanently invisible to the one thing standing between a commit and a shipped
engine change** — not "invisible until the next review," invisible by construction, forever,
unless a human remembers the exact env var name and runs it by hand. There is no ledger (compare
`docs/csv-rotation-riders.md`, which this same repo built specifically so a different class of
deferred, acknowledged gap could not be forgotten, enforced by `tools/checks/verify-gate.ps1`
itself failing a push that doesn't touch it), no expiry, and no `grep`-able marker outside the
source file itself. A future orchestrator who runs `verify-gate.ps1`, sees `ALL PASS`, and ships
has no signal that two confirmed, named, line-numbered production defects were excluded from the
run that just told them it was safe.

**FAILURE SCENARIO:** Exactly what happened to produce this range: `docs/medium-tier-bug-hunt-2026-09-16.md`
found the POC-tier gate defect (2026-09-16), someone wrote `A80b` to pin it precisely, and shipped
it *skipped by default* rather than either fixing `Core/SignalEmitter.vb:331-332` or accepting the
`FAIL` and blocking the gate until it's fixed. Nine days later (this commit, `6e74181`, dated
around 2026-09-25 per the surrounding commit history) the defect is still present, still `SKIP`ped,
and every `verify-gate.ps1` run in between reported `ALL PASS`. The mechanism generalises: the
next scoring defect anyone finds can be silenced the same way, by copying an 3-line `If`/`Return`
block, with no review gate on that specific act (it is ordinary VB.NET, not a `settings.json`
change or a scoring change in `CLAUDE.md`'s own RESERVED sense — the *code* it's skipping is a
scoring change, but the skip mechanism itself reads as routine harness plumbing).

**ANALYTICAL CRITIQUE:** The convention is not unreasonable on its face — a known-defect repro
that would otherwise permanently red the build while a fix is scheduled is a real problem, and
`ORDERCHECK_KNOWN_DEFECTS=1` at least gives a reviewer a way to *see* the defect on demand, which
is better than deleting the fixture. But "better than deleting it" is a low bar for the *only*
automated ship gate. `CLAUDE.md`'s own `AUTO-PROCEED` ruling names exactly this failure shape —
*"the recommendation optimised for ECONOMY... the ruling optimised for NOT LOSING INFORMATION"*
— and the fixture-literal provenance rule exists because "no tool can tell the two apart, and one
of them rots." A `SKIP` line with no ledger is precisely a convenience that only a human
remembering to look will ever revisit; nothing enforces that anyone does. The two-line
`grep ORDERCHECK_KNOWN_DEFECTS` I ran to find these took thirty seconds; nothing in
`verify-gate.ps1` or the CI workflow runs it as part of the gate, so nothing forces a human to run
it either.

---

### Finding 2 — S1 — the VPFR/POC target cap (architecture's Step 4c) never actually applies in shipped code, confirmed on 8,810 live rows

**LOCATION:** `Core/SignalEmitter.vb:330-332` (not read in full; line numbers and content per the
fixture's own comment and per `docs/medium-tier-bug-hunt-2026-09-16-poc-gate-output.md`, both
consistent with the run output above). Fixture: `A80a_VpfrNearHvnLabelsPinnedToPocSide` (line 15858,
passes — it pins the *producer's* geometry correctly) and `A80b_PocGateOpensOnTheWallSideItsSpecNames`
(line 15885, fails when run — see Harness run above).

**DOWNSTREAM IMPACT:** `CalcVPFRLite` and the POC-tier gate that reads its output disagree on what
`NEAR_HVN_SUPPORT` / `NEAR_HVN_RESIST` mean (A80a proves the producer's actual geometry; A80b's
`Check` message quotes the gate's own introducing commit, `721882e`, describing the *opposite*
geometry). The gate's opening condition can structurally never be satisfied by a label the real
producer emits, so the HVN-gated POC tier can never place a target at the POC — it silently falls
through to `FALLBACK_ATR` on the shipped code path, confirmed both by the run above
(`got target 100040.0 (FALLBACK_ATR)`) and by a pooled-log measurement already in this repo:
`docs/medium-tier-bug-hunt-2026-09-16-poc-gate-output.md` §6 reports **0 of 8,810** swing-read
population rows with `TargetCapReason = poc`, across every session and every tier, and §7.1 reports
that under the geometry the gate's own spec describes, **143 of 8,508 verified population rows
(1.68 %)** would have their verdict-side target move to the POC — and in **every one** of those
143 rows (§7.1's "Step 5c floor would flip to NO TRADE" column equals the "target moves" column,
row for row), the corrected target would fail the min-tradeable-move floor and the verdict would
become `NO TRADE` instead of directional.

**FAILURE SCENARIO:** `CLAUDE.md`'s own stated design principle (§ "Collaboration Rules"):
*"Conservative false-positive tolerance... the engine should say NO TRADE rather than output a
weak directional signal."* This defect is the direct violation of that principle, measured: today
the shipped engine takes a directional trade in ~1.68 % of its qualifying population where its own
designed cap — if it worked — would say NO TRADE for insufficient edge. In a −1.5 % flush with
ATR(7) rising 30→140, price moving fast toward or through a real volume node (a POC/HVN wall) is
exactly the scenario this cap exists for; per the LOGIC TRACE above, the confirmed defect's
exposure likely *widens*, not narrows, when ATR spikes (a bigger `bound` admits more `pocDist`
values into the gate's geometry check), and nothing in the harness measures that relationship —
A80b's own fixture ATR is a small, static, hand-picked value.

**ANALYTICAL CRITIQUE:** This is not a hypothetical edge case; it is a confirmed-by-measurement,
100 %-reproduction-rate defect in a named pipeline stage (architecture's Step 4c) that has been
open since 2026-09-16 and is excluded from the ship gate by Finding 1's mechanism. A82b/A82c's own
comment (line 16035-16037) is unusually honest about this: *"a mirror test cannot see a SYMMETRIC
inversion... The POC-tier gate defect (A80b) is that shape."* The team that wrote this harness
already understands precisely why their best property test (A82) cannot substitute for fixing the
underlying label semantics — which makes the decision to ship it unfixed, silenced, and
unmeasured-under-stress a known and named gap rather than an oversight.

---

### Finding 3 — S2 — `CalcLiquidations` books every maker-side-liquidated trade onto the wrong side, corrupting the Liq Penalty scoring vote

**LOCATION:** `Core/Indicators_OrderFlow.vb:266-274` (not read in full; per the fixture's own
citation and the run output above). Fixture: `A81a_TakerSideLiquidationBooksTheTakersPosition`
(line 15957, passes — the common "T" case is correct) and
`A81b_MakerSideLiquidationBooksTheMakersPosition` (line 15976, fails when run).

**DOWNSTREAM IMPACT:** Per Deribit's `public/get_last_trades_by_instrument` contract (quoted in
the fixture's own header comment, lines 15933-15936), the `liquidation` field is `"M"` when the
*maker* side was under liquidation and `"T"` when the *taker* side was. `CalcLiquidations` books
every flagged trade by the **taker's** `direction` regardless of which flag it carries. For a
`"T"`-flagged trade this is correct (A81a passes). For an `"M"`-flagged trade it is backwards: a
maker who is being force-sold out of a long (the taker buys from them) should book onto
`LiqLongSize`, but the shipped code books it onto `LiqShortSize` instead — confirmed by the run
above (`KNOWN DEFECT: got signal=SHORT LIQS long=0 short=1000` for a maker-long liquidation).
`LiqSignal`/`LiqLongSize`/`LiqShortSize` feed a real scoring vote (`A82Sites`' "Liquidations
standard"/"Liquidations large" sites, line 16454-16461, confirmed as a scored, mirror-tested vote
site in this same range), so an `"M"`-flagged trade injects a **sign-flipped** liquidation signal
into the scoring pipeline.

**FAILURE SCENARIO:** A cascading liquidation event — the canonical case for a −1.5 % flush — is
precisely when the trade tape carries the most `liquidation`-flagged trades. If any material share
of those carry `"M"` (Deribit's liquidation engine can itself act as either side of a trade,
so `"M"` is not a hypothetical value), the Liq Penalty vote scores the *opposite* direction of the
real liquidation pressure for exactly those trades, at exactly the moment the signal matters most.

**ANALYTICAL CRITIQUE:** Same structural problem as Finding 2 — a confirmed, reproducible defect
in a real scoring input, gated behind `ORDERCHECK_KNOWN_DEFECTS=1` and therefore invisible to the
ship gate (Finding 1 covers the mechanism; this finding is the second load-bearing instance of it
in this range). Unlike the POC-tier gate, I found **no measurement doc quantifying how often "M"
actually appears** in the real trade tape (`docs/medium-tier-bug-hunt-2026-09-16-liquidation-output.md`
exists but was not read this session — see "Scope not read in full"), so I cannot state a
production incidence rate the way Finding 2 could; the defect itself, though, is proven by the run
above, not merely asserted by the fixture's comment.

---

### Finding 4 — S3 — `A79g` pins a known duplicate-write race as the passing condition, not a guard, with the fix explicitly deferred

**LOCATION:** `verify/ordercheck/Program.vb:14795-14856` (`A79g_MeasuredSeqTailBesideAnUnflushedStreamingWriter`).

**DOWNSTREAM IMPACT:** The fixture's own header (line 14795: *"⚠ MEASUREMENT PIN, not a guard"*)
and its `Check` message (line 14848: *"readers dedupe; the fix is the duplicate-rows task"*)
both say plainly that this is not asserting correct behaviour — it is asserting the **current**
(disclosed-as-imperfect) behaviour, so the fixture doesn't red the build while a genuinely separate
duplicate-rows fix is pending. Both measured races (A: streaming buffers 5-7 then repair appends
5-7 then streaming flushes; B: repair appends 5-7 then streaming receives and flushes them) produce
exactly 3 duplicate rows in `trades_*.csv`, asserted as `dupA = 3 AndAlso ... dupB = 3` — i.e. the
`Check` **fails** if the duplication is ever fixed without updating this fixture, which the
comment says is expected ("this fixture flips BY DESIGN — update it there").

**FAILURE SCENARIO:** Any production window where the streaming writer has buffered but not yet
flushed trades while a gap-repair pass runs concurrently over the same seq range writes those
trades twice to the on-disk store. The fixture's own claim that "readers dedupe" is the entire
mitigation; I did not verify that every reader of `trades_*.csv` in this codebase in fact
deduplicates (the fixture asserts the writer-side count, not that every consumer is safe).

**ANALYTICAL CRITIQUE:** This is the same shape as the already-known `A41a-c` maker/maker-fee
pin (excluded from this report per the task's instructions) — a disclosed, self-aware defect
pinned as the passing state rather than fixed. It is materially less severe than Findings 1-3: it
does not touch scoring, does not touch an order side, and the fixture itself names the mitigating
property (dedup on read) and the owning task, which the `A80b`/`A81b` `SKIP` convention does not
do (those carry no equivalent "here is why this is safe in the meantime" claim — they simply don't
run). I have not independently verified the "readers dedupe" claim against `DedupTrades` or every
call site that reads the trade store.

---

## Per-fixture answers (Q1 / Q2 / Q3), full range 11201–16764

Legend: **Q1** = asserts the named property, not a lookalike string/number · **Q2** = fixture-literal
provenance (MECHANISM vs SHIPPED BEHAVIOUR correctly declared and, where checked, correctly
classified against `settings.json` history) · **Q3** = pins a known defect as correct, A41a-c style.
"—" means the question does not apply (no literal to classify, or fixture doesn't touch a defect).
Unless a row says otherwise, Q2 literals that declare MECHANISM/SHIPPED carry an in-code
justification I read and found internally consistent; I did not re-derive every one against the
full `settings.json` history myself (see "Scope not read in full").

| Fixture(s) | Line(s) | Q1 | Q2 | Q3 |
|---|---|---|---|---|
| A46a_PooledCsvReportSections | 11204 | Yes — real `ForwardWindowJoiner`→`FailureRateMatrix`→`BandLadder`→`MarkdownReportWriter` chain, asserts real matrix/ladder cells | — no settings-derived literals | No |
| A50a..A50k, A58c (settings overlay family, 12 fixtures) | 11388-11813 | Yes, each — reject/admit/save/hot-reload/whitelist behaviour asserted against the real `SettingsLoader` singleton, not a mock | — mechanism-only literals (test JSON payloads); A50h's potency arm derives from `A50BaseSettings()`, not shipped values, correctly so | No |
| A62a_ShippedTreeIsClean | 12137 | Yes — walks `New EngineSettings()` vs the **worktree's own tracked** `settings.json` at 6e74181; confirmed PASS in both harness runs | — | No |
| A62b_MutationTeeth, A62g_AllowListIsListNotBlanket | 12108, 12158 | Yes — diff-against-baseline design so A62b doesn't depend on A62a's result; A62g flips a 3rd Boolean not on the 2-entry allow-list | Q2 N/A (mutates in memory, no literal to classify) | No |
| A62c_NullableRuleBothArms, A62d_CaseInsensitiveResolution, A62e_ResolverFailsLoudly, A62f_StructuralExclusionNotByName | 12180-12272 | Yes, each — A62f's independent `A62StructuralTestShape` class is real teeth, not a name-list in disguise | — | No |
| A63a_ParseFailurePathResolvesShipped | 12314 | Yes — every expected value is read from the tracked settings.json via `A63ReadDouble`/`A63FindSessionByName`, never restated as a literal (correct SHIPPED-arm handling) | Correctly declared, no literal drift | No |
| A63b_DictionaryCompletenessBothDirections | 12399 | Yes | — local test-only dictionary shapes | No |
| A64a_MtfAllThreeVotesCast | 12441 | Yes — reads `gateDetails` for the vote count, not just the final trend label (explicitly built to defeat a documented A9 blind spot) | "Bear:3" declared MECHANISM (vote-count property of the algorithm) — correctly reasoned | No |
| A64b_MicroCvdJointDependency | 12471 | Yes | 0.37 declared MECHANISM and explicitly checked against the shipped 0.30 (same 2×2 result) — good discipline, one of the few fixtures that shows its own cross-check | No — documents a real joint-dependency corner in MicroCVD burst classification as a coverage lesson, not a correctness claim either way |
| A65a_DegenerateBookIsNormalNotTight, A65b_LockedBookIsTight | 12508, 12542 | Yes — the pairing is the point (both books read 0.00 bps, must classify differently) | — | No |
| A65c_WideArmIsTestedFirst | 12567 | Yes | wide=1.0/tight=2.0 declared MECHANISM ("never shipped, key has run 5.0/1.5 throughout") — **not independently re-verified against `settings.json` history by me** | No |
| A65d_BoundaryInclusivity | 12583 | Yes | Correctly SHIPPED (derived from cfg, no literal) | No |
| A66a_DegenerateBookGivesNoSpreadOnTheStrip, A66b_ApplySpreadCompositionPairsBpsAndStatus | 12626, 12675 | Yes, each — both explicitly name and exercise the load-bearing case (case 2 / case 1 respectively), not just a non-regression case | — | No |
| A66c_UserAgentNamesTheRunningHost | 12715 | Yes — relative assertion (`entry assembly name`), not a literal that would coincidentally match | — | No |
| A67a_PerfStripExcludesWeekendRows, A67b_IsWeekdayRowGuardsTheMinValueTrap | 12747, 12790 | Yes, each — A67a's load-bearing assertion is `TotalRange`, not just the rate; A67b's is the counter-intuitive `DateTime.MinValue` trap | Calendar-fact dates spot-checked with `date -d`, confirmed correct (2026-07-18/2026-01-03 = Saturday) | No |
| A68a/A71a, A68b, A71b, A71c (WD-TIDY/WD-SEMANTICS family) | 12836-12995 | Yes, each — each targets exactly one fold-mutation and states it | Calendar facts spot-checked, correct | No |
| A69a..A69e, A70a (S-4 identity family) | 13010-13280 | Yes, each — A69c's shuffled CSV header is a genuine anti-position-coupling technique; A69e is explicitly the one case A69a-d cannot produce | — mechanism-only | No |
| A72a_FirstWriteToAbsentDestinationSucceeds, A72b, A72c | 13306-13371 | Yes, each — mutation actually run and pasted (lines 13297-13303), not predicted | — | No |
| A73a, A73b, A73c, A73d, A73i (C-1/C-2/coverage family) | 13384-13548 | Yes, each — A73b is the mixed-population case this project's own memory names as previously the actual defect shape | — | No |
| A74a..A74e (venue-status family) | 13558-13773 | Yes, each | RPC code 11051 declared MECHANISM, reasoned correctly | No |
| A75a, A75b | 13785-13868 | Yes, each | 11051/10009 declared MECHANISM, correctly | No |
| A73f, A73g, A73h (declared-window family) | 13876-13930 | Yes, each — A73g's boundary test and A73h's precedence-ordering test are both genuine teeth (not just happy-path) | — | No |
| A76a, A76b, A76c | 13938-14067 | Yes, each — A76b is a regression pin on a real, previously-shipped 9-day-silent bug (legitimate, not a defect-pin) | 11051/10009 reused, correctly declared | No |
| A77a..A77e | 14074-14247 | Yes, each — A77b is explicitly the "B-1 trap" (an RPC code that must NOT be treated as venue-side) | 503/10009/UNKNOWN declared MECHANISM, correctly | No |
| A78a | 14257 | Yes — self-discloses its own gap ("Not covered: the single CLI line in BacktestProgram... the harness does not link BacktestProgram.vb") | 503 declared MECHANISM | No — the disclosed gap is not a defect-pin, it's an honest scope note |
| A78b | 14344 | Yes — proves the specific same-ms boundary defect this whole family exists for, plus a stall/fail-loud arm | Page size 1000 correctly declared MECHANISM (reads `CoverageReport.VenuePageSize`) | No |
| A79a..A79p (16 fixtures, gap-repair family) | 14447-15384 | Yes, each, **except A79g** (see Finding 4) — several (A79a, A79h, A79l, A79m, A79n, A79p) explicitly paste a fail-first run's actual output before the fix, not a prediction | Family-wide MECHANISM declaration at 14389-14391, correctly reasoned | **A79g: yes — see Finding 4** |
| A78c, A78d, A78e, A78f | 15391-15587 | Yes, each — A78f's edge-vs-inside seq-span distinction is a genuine, non-trivial boundary case | Counts/sequence numbers correctly declared MECHANISM | No |
| A80a_VpfrNearHvnLabelsPinnedToPocSide | 15858 | Yes — correctly pins the producer's real geometry | SHIPPED settings correctly read from tracked file via `A80ShippedCfg` | No |
| **A80b_PocGateOpensOnTheWallSideItsSpecNames** | 15885 | Yes, when it runs — asserts the gate's own documented intent, and fails on shipped code | Correctly declared SHIPPED (six VPFR keys, structural_levels) / MECHANISM (candle profile) split | **See Finding 2 — known defect, `SKIP`ped by default, gate never sees it** |
| A81a_TakerSideLiquidationBooksTheTakersPosition | 15957 | Yes | dominance_ratio correctly SHIPPED (read from tracked cfg) | No |
| **A81b_MakerSideLiquidationBooksTheMakersPosition** | 15976 | Yes, when it runs | Correctly declared | **See Finding 3 — known defect, `SKIP`ped by default** |
| A82a_MirrorCoversEveryIndicatorResultsProperty | 16211 | Yes — a genuinely structural guard (every property classified exactly once, round-trip via double-mirror) | — | No — the label-map/kept/etc. classification is itself the assertion, no numeric literal to misclassify |
| A82b/A82c (mirror property test + coverage) | 16576 | Yes — extremely thorough (3 cfgs × 4 regimes × session hours × 150 random joint states); explicitly and correctly names its own blind spot (line 16035-16037: cannot see a symmetric inversion, "the POC-tier gate defect (A80b) is that shape") | SHIPPED (every threshold read from tracked cfg) / MECHANISM (C=100000 etc.) correctly split | A82c's OBV-partial-never-upgrades assertion (line 16691) is a **legitimate** pinned-unreachable-by-design check (v0.42 adverse-divergence gate), not a defect pin — the fixture itself cites the design rule it's pinning |
| A84_CountDataRowsMatchesReadAllLinesSemantics | 15786 | Yes — dual-arm design (equivalence to `ReadAllLines` AND an explicit expected count) explicitly closes the "both wrong the same way" hole | — pure MECHANISM (file content) | No |
| A85a..A85e | 15693-15762 | Yes, each — A85d is explicitly the arm that distinguishes this design from a weaker "consecutive-duplicate" filter | Window/cap correctly read from `Public Const` production values | No |

**Helper/infrastructure subs in range** (not fixtures, not individually scored): `A50TempDir`,
`A50Cleanup`, `A50BaseSettings`, `A50Json`, `A50Read`, `A50Init`, `A50WaitFor`, `A62RecordDrift`,
`WalkPocoVsJson`, `A62ResolveRepoRoot`, `A62RequireSettingsRoot`, `A62TryGetPropertyCI`,
`A62BuildPath`, `A63ReadDouble`, `A63FindSessionByName`, `A78VenueStub`, `A78PageJson`,
`A78IdTrade`, `A79SeqStub`, `A79AnchorStub`, `A79Fetch`, `A79Trade`, `A79Rows`, `A80ShippedCfg`,
`A80Profile`, `A80RunVpfr`, `A81Liq`, `A82Props`, `A82SwapLabel`, `A82Reflect`,
`A82ReflectBoxed`, `A82Mirror`, `A82MirrorVerdict`, `A82MirrorReason`, `A82Same`, `A82Base`,
`A82Sites`, `A82CheckOne`.

## Nits (S4)

- `A73a_SequenceContiguousAboveGapThresholdIsCapured` (line 13384) — "Capured" is a typo for
  "Captured," in the fixture name only; the assertion and message text are correct.
- `A74d_LogPathNeverThrows` (line 13680) uses an exclusive-lock `FileStream` technique that is a
  Windows sharing-violation mechanism; this is appropriate for a Windows harness testing a Windows
  app today, but is worth naming given `CLAUDE.md`'s stated Linux-CLI-port target — a future
  Linux build of this same property would need a different repro (Linux file locking advisory,
  not mandatory, so `FileShare.None` does not reproduce an `IOException` the same way).

---

Model/effort used for this audit: Sonnet 5, high effort (adversarial review of a build that
touches scoring-adjacent code, per this repo's own effort-matching rule).
