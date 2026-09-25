# Hostile audit — ordercheck harness, order-payload/fee/MTF slice (2026-09-24)

**Scope as actually run:** the task prompt specified "`verify/ordercheck/Program.vb`,
lines `<RANGE>`" with the range left as an unfilled placeholder. No range was ever
supplied. Rather than guess, I scoped the audit myself to the fixtures nearest the
live order payload — the region the prompt's own framing ("this harness... is the
only automated check before code that drives live BTC-PERPETUAL orders ships") points
at — plus `OrderCheck.vbproj` in full, as instructed.

## Files/regions NOT covered

This is not a full-file audit. `verify/ordercheck/Program.vb` is 16,764 lines and
declares roughly 320 fixture calls (`A1` through at least `A85`, per `Sub Main`,
`Program.vb:30-800`ish). I read and analyzed in full only:

- `Program.vb:1-420` (header, imports, the full `Sub Main` fixture roster as text)
- `Program.vb:1160-1355` (A8 dominant-side cascade, A9 MTF per-side flags, A10 Kelly
  sizing, A11 candle freshness)
- `Program.vb:2756-3345` (A22a-g signal-bridge payload, A24a CSV/payload parity,
  A26a structural levels — partial, first fixture only)
- `Program.vb:6588-7044` (A40a-e fee-aware min-move floor, A41a-d net-EV fee rider)

I did **not** open or read: A1-A7, A12-A21, A23, A25, A27-A39, A42-A85 and whatever
follows A85 (the large majority of the file, roughly 300 of ~320 fixtures) — including
the backtest synthesizer family (A43-A49), the trade-store family (A48-A60), the
absorption/alerts/venue-status families (A31, A37-A38, A60, A74, A79, A85), the ceiling
audit (A39), and the what-if/geometry-arbitration families beyond A40/A41 (A30, A36,
A42, A44-A47). I also did not read the bodies of `SettingsDiffApplier.vb`,
`SignalEmitter.vb`, `WhatIfReplay.vb`, `ScoringEngine_Kelly.vb`, or any other linked
source file in `OrderCheck.vbproj` except the two excerpts named below — I read
`Core/Indicators_Structure.vb` only for the 80 lines containing `CalcMTFGate`, and
`Core/Settings/EngineSettings.vb` only for the 67 lines containing
`TradeCostSettings`. `OrderCheck.vbproj` was read in full (177 lines).

Two greps (`SettingsDiffApplier.` call-site tally; the MTF-fail-open and
geometry-invariant absence checks) ran against the whole file's text, so their
negative/positive results are whole-file claims even though my line-by-line reading
was not. Exact commands and output are in
`docs/audits/proofs/ordercheck-payload-fee-mtf/README.md`.

No code was run — no `dotnet build`/`dotnet run`. Every finding below is derived from
static reading and text search of the tracked source, not execution.

---

--- LOGIC TRACE ---

The three fixtures actually touching the order payload are **A22a**
(`Program.vb:2756`, payload field-by-field), **A24a** (`Program.vb:3192`, CSV Placed*
≡ payload levels), and **A40/A41** (`Program.vb:6588-7044`, the fee model that's
supposed to gate/price the trade). Trace what each would *not* catch if
BTC-PERPETUAL is flushing:

- **A22a/A24a** run against one frozen snapshot: `price=59012.5, ATR=41.3` (or
  `62000/40.0` in A26/A36), with structural swing values hand-set as literals.
  Nothing perturbs ATR, re-enters `ComputeSideLevels` mid-computation with a second,
  more-recent price, or simulates the WS→REST fallback that a flush actually
  triggers. A regression that only manifests when ATR itself jumps between the tick
  that set `r.ATR` and the tick that read `r.CurrentPrice` — the exact condition of a
  flush — has no fixture positioned to see it.
- **A24a** proves CSV-parity by calling `SignalEmitter.ComputeSideLevels` twice with
  the *same* `v`/`r`/`cfg` object graph (`Program.vb:3208-3210`) and then diffing the
  two outputs. That is parity by construction, not independent verification — it
  cannot catch the live-app case where the CSV row and the JSON payload are built
  from `cfg` snapshots taken on either side of a hot-reload (settings.json is
  explicitly hot-reload-able per CLAUDE.md), which is a scenario a flush is likely to
  coincide with if the trader is also tuning during it.
- **A40/A41** price the round trip at a single style
  (`RoundTripStyle="maker_maker"`, 1.5+1.5bps) and apply that same drag to every
  outcome arm — SUCCESS, ADVERSE_HIT, and WINDOW_EXPIRED alike
  (`Program.vb:6872-6992`). None of A41a/b/c exercise a stop-out priced as a
  taker/market fill, which is what actually happens when a stop is hit during a
  flush. The harness pins the wrong economic model as ground truth rather than
  testing against it.

None of these three catches a live 15m-data outage flipping the MTF gate to
fail-open, because that code path isn't reachable from any of them — it's reachable
only from `IndicatorEngine.CalcMTFGate`, and no fixture drives it into the branch
below.

---

**SEVERITY: CRITICAL**
**LOCATION:** `Core/Indicators_Structure.vb:452-458` (`CalcMTFGate`); uncovered
region — `verify/ordercheck/Program.vb:1253-1293` (`A9_MtfPerSideFlags`) is the only
MTF-gate fixture and never constructs this input.
**DOWNSTREAM IMPACT:** The 15m gate is CLAUDE.md's own documented "hard veto"
(`BLOCK forces NO TRADE regardless of score`). Its default when `candles15m Is
Nothing OrElse candles15m.Count < adxPeriod + 2` is `gatePassLong=True :
gatePassShort=True` — i.e. fail-open in *both* directions simultaneously, silently,
with `gateDetails="MTF: insufficient 15m candles"` buried in a string nobody asserts
on.
**FAILURE SCENARIO:** A high-volatility flush is precisely when REST/WS delivery
degrades (rate limits, disconnects, the very conditions A16's connection-health gate
exists for). If the 15m candle feed thins out below `adxPeriod+2` candles at that
moment, the hard veto that's supposed to block a counter-trend entry stops vetoing
anything — the engine can now emit STRONG LONG into a confirmed 15m downtrend it
simply couldn't see, at the exact moment stale/missing data makes that most
dangerous. `A9_MtfPerSideFlags` only ever feeds `CalcMTFGate` a fully-populated
70-candle synthetic series (`MtfBearCandles()`); the empty/short-list branch has zero
call sites in this file.
**ANALYTICAL CRITIQUE:** This isn't a missing edge case, it's *the* missing edge
case — the harness's own stated boundary (the task context I was given names this
exact gap) is correct, and it's the single highest-blast-radius gap in the file
because it silently *removes* the one veto CLAUDE.md calls load-bearing rather than
producing a wrong-but-bounded number. A one-line fixture
(`CalcMTFGate(New List(Of Candle)(), ...)` asserting both pass flags true, with a
comment flagging it as a documented hazard rather than a "should never happen")
would at least make regressions to the fail-open *direction* (e.g., someone "fixing"
it to fail-closed without updating docs, or vice versa) visible instead of unowned.

---

**SEVERITY: HIGH**
**LOCATION:** `tools/AutoTweaker/SettingsDiffApplier.vb` (`Apply`/`ApplyRevert`,
never invoked); every call site in the harness is `Validate` — 67 occurrences in
`Program.vb`, e.g. `3004`, `6759-6807`. `OrderCheck.vbproj:139` links the file but
the harness only exercises its validation half.
**DOWNSTREAM IMPACT:** `Validate` is a pure predicate over a diff string — it never
touches disk, never hot-reloads, never runs the code path that actually mutates the
live `settings.json` the collector reads. If `Apply`/`ApplyRevert` throw on .NET 8
(per the known gap I was given as context, not independently re-derived here), the
auto-tweaker's entire write path is dead on arrival in production, and this
harness — which is described in the task's own framing as "the only automated check
before code that drives live BTC-PERPETUAL orders ships" — would still print
all-green.
**FAILURE SCENARIO:** An operator (or the tweaker's own automated retune loop)
accepts a proposed diff mid-session, expecting a threshold to move live. `Apply`
throws, the settings file is left unchanged (or partially written, depending on
where the throw lands), the collector keeps running old thresholds, and every green
`Validate` check upstream gave zero signal that the actual apply step is broken.
Nothing in this file would have caught it before ship.
**ANALYTICAL CRITIQUE:** `Validate`-only coverage is a classic "tested the part
that's easy to test, not the part that matters" gap — `Validate` is pure and
deterministic so it's cheap to fixture; `Apply` touches I/O and process state so it's
been left alone. That's the wrong way round for a harness whose stated job is gating
what reaches a live order-driving path: the untested half is the half with side
effects.

---

**SEVERITY: HIGH**
**LOCATION:** `Core/Settings/EngineSettings.vb:940-944` (`TradeCostSettings.
RoundTripStyle` doc comment) contradicted by `verify/ordercheck/Program.vb:6872-6992`
(A41a/b/c, which pin the uniform-arm drag as correct).
**DOWNSTREAM IMPACT:** The `RoundTripStyle` doc comment states the design intent
explicitly: *"maker_maker is correct rather than optimistic — the floor gates the
TARGET side (the profit path)... Taker occurs on emergency SL repositioning and rare
manual exits, i.e. the LOSS path, which this floor does not price."* That is a
correct, deliberate design for the min-move **floor** (A40). But `RoundTripFeePct` —
the same maker_maker-derived property, whose own doc comment says the loss/SL path is
out of scope — is also the value `WhatIfReplay.ComputeEvAtr` subtracts from the
**ADVERSE_HIT** arm in the net-EV rider (A41). A41a's comment
(`Program.vb:6872-6877`) states this is deliberate: "every resolved trade pays the
round trip... the drag never depends on whether the target was hit." A41c
(`6941-6992`) then locks fees-zero-equals-gross as the regression identity, so any
future change that makes the drag *arm-dependent* (taker-priced on the stop-out arm,
matching what the settings class's own comment says should happen on that path)
would fail this fixture by design.
**FAILURE SCENARIO:** A stop-loss exit during a flush is realistically a
market/taker fill against a moving book — exactly the SL-repositioning case the
`RoundTripStyle` comment names as taker-priced and explicitly *not* covered by the
maker_maker floor. A41 nonetheless prices that same arm at maker_maker in its EV
report. Every backtested/what-if EV number for the stop-out arm is therefore
optimistic relative to the codebase's own documented model of that arm, and that
optimism is baked into a fixture (A41a-c) that will reject a fix bringing A41 in line
with the settings class's stated design.
**ANALYTICAL CRITIQUE:** This is not a hypothetical "fees should probably be
taker on stops" opinion — it's an internal inconsistency the codebase's own comments
expose: `EngineSettings.vb:970-974` even says `EffectiveMinMovePct` is "THE shared
resolver... consumed by... the what-if replay — so measurement and behaviour can
never drift," while the same section explains maker_maker deliberately excludes the
loss path. The what-if replay is exactly where the loss path (ADVERSE_HIT) gets
priced with the excluded-by-design rate. A40's own spec explicitly scoped out "the
maker→taker emergency loss-arm delta" as a deliberate, recorded deviation
(`Program.vb:6868-6869`) — so the gap is known — but the fixtures now actively defend
against closing it, which is worse than an unrecorded oversight.

---

**SEVERITY: MEDIUM**
**LOCATION:** `Program.vb:2756-3237` (A22a, A24a) and the wider A26/A36 families
(only A26a read in full) — collectively the entire structural-levels test surface I
was able to inspect.
**DOWNSTREAM IMPACT:** No fixture asserts a geometry invariant (stop below entry
below target for a long; the mirror for a short; target ≠ stop; neither collapses to
entry) generically across inputs. Every check I read is a point-pin: one
hand-computed price/ATR/swing combination per case, with the expected
`Target`/`StopPx` literals typed out by the fixture author. A whole-file grep for any
randomized/property-style check on this shape (`Random()`, a generic `StopPx <
Target` assertion, etc.) returned zero matches — see
`docs/audits/proofs/ordercheck-payload-fee-mtf/README.md` §4.
**FAILURE SCENARIO:** A future change to the ladder-tier walk or the DG1 clamp
order-of-operations (A26b/A26c territory, not fully read by me) that inverts
geometry only for some combination of swing distance and ATR *not* covered by the
existing hand-picked cases — e.g., a large ATR spike during a flush pushing the
fallback distance past a swing level in a way none of the ~15 point-fixtures happen
to construct — ships silently. The order-placement consumer downstream would receive
a stop above its target (or vice versa) with no engine-side check having ever
existed to catch that shape of bug, only ever the specific numbers these fixtures
happened to type in.
**ANALYTICAL CRITIQUE:** This is the flip side of the fee-model finding: where A41
over-tests one specific (wrong) case into permanence, the geometry suite under-tests
the *general property* that actually matters for order safety. A single
property-style fixture — loop over a spread of synthetic ATR/price/swing
combinations and assert `StopPx < Entry < Target` (long) / `Target < Entry < StopPx`
(short) plus `Target ≠ StopPx` — would cost a few lines and catch an entire class of
bug that point-pins structurally cannot, since a point-pin only fails when the
*specific* literal changes, never when a new input combination breaks the invariant
the literals never exercised. I note this recommendation is bounded by what I read:
A26/A36 have roughly a dozen more cases I did not open, so I cannot rule out that one
of them already does something invariant-like; the whole-file grep in §4 of the proof
README is the actual evidence for "no such check exists anywhere in the file," not my
partial read of A26a alone.
