# Absorption instrumentation — spec-back (review packet)

**Companion outcome record:** [`absorption-instrumentation-batch-summary.md`](absorption-instrumentation-batch-summary.md) — read its §0 first.
**Spec reported against:** [`absorption-instrumentation-spec.md`](absorption-instrumentation-spec.md)
**Handoff reported against:** [`absorption-instrumentation-escalation-2026-09-01.md`](absorption-instrumentation-escalation-2026-09-01.md)

Format per [`batch-review-packet-convention.md`](batch-review-packet-convention.md): ranked
verification handles · decisions queued with my read · spec-back proper · what I did not verify.

---

## The four things to read first

| | | Where |
|---|---|---|
| **1** | ⛔ **Acceptance §7 item 4 states the wrong condition.** Population is gated on `HasEpisode`, **not** on `AbsorptionSignal ≠ NONE`. The only live episode row I got is `NONE` **and** populated — a D8 veto. Following item 4 literally files a defect against correct code | this packet §3 |
| **2** | ⛔ **The spec's described failure mode for fixture `A60c` cannot occur**, so `A60c` as specced would have passed a build with no reset at all. Proven by mutation. I widened it | this packet §3 |
| **3** | ⭐ **A live episode row proves the five new columns reproduce `AbsorptionPullFrac` and `AbsorptionRatio` exactly.** No fixture can prove this. It is handle **H9** | this packet §1 |
| **4** | ⚠ **Four decisions queued**, none blocking. The one that needs your call is **D-1**, the misnamed rotation `.bak` — and it turns on a consumer I did not open | this packet §2 |

**Nothing is blocked. The build is complete and every acceptance check passes.**

---

## 0. Reader audit — all 17 files, RE-verified this session

Spec R9 / §4 requires the audit as part of the build and says the property "must be
re-verified, not assumed." **This is a fresh pass, not a copy of the handoff §2** — I ran
the greps again against the post-build tree. It agrees with the handoff on every line
except `verify/ordercheck/Program.vb`, which this build fixed.

**The property each file must satisfy:** it either (a) indexes by header NAME, or
(b) indexes positionally only at columns ≤ 111, or (c) does not parse rows at all.

| # | File | Access pattern | Safe under R1? |
|---|---|---|---|
| 1 | `analysis/ForwardWindowJoiner.vb` | header-name `colIdx` dict, `:112-114` | ✅ (a) |
| 2 | `Core/Settings/SettingsLoader.vb` | does not read `analysis_log.csv` — its `parts.Length - 1` hits are settings-KEY path splitting (`:653-681`), not CSV rows | ✅ (c) |
| 3 | `AnalysisLogger.vb` | **the live writer** — in scope for the build, not an audit target | — |
| 4 | `LivePerformanceTracker.vb` | header-name `colIdx` dict, `:1408-1414` | ✅ (a) |
| 5 | `tools/AutoTweaker/ConditionsExtractor.vb` | header-name via `ResolveColumns(lines(0))`, `:84`/`:272`; `TryD` guards with `colIdx >= parts.Length`, `:293` | ✅ (a) |
| 6 | `tools/AutoTweaker/TweakerConfig.vb` | holds a `CsvPath` string only, `:77`; no row parsing | ✅ (c) |
| 7 | `tools/BacktestRunner/BacktestProgram.vb` | **zero** `Split(","c)` / `ReadAllLines`; delegates to `BacktestRowWriter` / `OverlapValidator` | ✅ (c) |
| 8 | `tools/BacktestRunner/BacktestRowWriter.vb` | **the schema twin** — in scope for the build, not an audit target | — |
| 9 | `tools/BacktestRunner/CoverageReport.vb` | header-name `Array.IndexOf(cols, …)`, `:292-293` | ✅ (a) |
| 10 | `tools/CeilingAudit/CeilingAuditProgram.vb` | no direct parsing; delegates to `CsvFeatureBuilder.LoadAndBuild`, `:118` | ✅ (c) |
| 11 | `tools/CeilingAudit/CsvFeatureBuilder.vb` | header-name `colIdx` dict, `:119-121` | ✅ (a) |
| 12 | `tools/WhatIfRunner/WhatIfProgram.vb` | holds `csvPath` only, `:54-64`; hands off to `ForwardWindowJoiner.Load`, `:85` (row 1) | ✅ (c) |
| 13 | `UI/MainForm_Calibration.vb` | header-name `colIdx` dict, `:37-39` | ✅ (a) |
| 14 | `UI/MainForm_Layout.vb` | schema-MARKER check only — `firstLine.Contains("TrendStructure5m")`, `:2023`; no column indexing | ✅ (c) |
| 15 | `UI/TweakSettingsForm.vb` | header-name, three separate resolutions (`:287-292`, `:379`, `:450`), each guarded `parts.Length <= idx` | ✅ (a) |
| 16 | `UI/WhatIfLauncherForm.vb` | holds `_csvPath` only, `:68`; hands off to `WhatIfRunner.exe` via `Process.Start`, `:137`. Its `parts.Length - 1` at `:243` is a settings-KEY path split | ✅ (c) |
| 17 | `verify/ordercheck/Program.vb` | ⛔ **was mixed** — `A31f` (`:4007`) resolves by name, correctly; `A43e` indexed by TRAILING position. **FIXED in this build** — both now resolve via `Array.IndexOf` and row width is asserted | ✅ (a), after the fix |

**Result: 15 of 17 are safe by construction (header-name resolution or no row parsing),
2 are the writers this build edits, and the single unsafe reader is fixed.**

⚠ **Two false-positive shapes worth recording, because both look like the trap and are
not.** `parts(parts.Length - 1)` in `SettingsLoader.vb` and `WhatIfLauncherForm.vb` is
**settings-key path splitting** (`"indicators.absorption.enabled"` → the leaf key), and
`lines.Length - 1` in `AnalysisLogger.GetRowCount` / `TweakSettingsForm.CountCsvRows` is
a **row count minus the header**. A grep for trailing-index syntax finds all four.
**Neither indexes a COLUMN from the end of a row, which is the only shape R1 breaks.**

---

## 1. Ranked verification handles

### ⭐ If you run only one

```bash
dotnet run --project verify/ordercheck > /tmp/h.out 2>/dev/null; echo "exit=$?"; grep -cE "^FAIL" /tmp/h.out; grep -c "ALL PASS" /tmp/h.out
```

Expect **`exit=0`, then `0`, then `1`**. It covers the three-file schema rotation (A60e),
the value round-trip through the real writer (A60a), the null discipline (A60b), the new
state and its reset (A60c), R1's no-column-moved guard (A60d), and the fixed `A43e`.

⚠ **The handle asserts the harness RAN, not merely that nothing printed** — `ALL PASS` is
emitted only at the end of a completed run. A run that dies early prints neither
`ALL PASS` nor `FAIL`. **A zero `ALL PASS` count is a FAILURE, not a pass**, which is why
the third number is asserted as `1` rather than the `FAIL` count being asserted alone.

⛔ **Do NOT write this handle as `2>&1 | grep …`, and this is not hypothetical — I wrote
it that way first and it reported a false failure on a green run.** The A48 fixtures
write `[TradeStoreWriter] …` diagnostics to stderr *on purpose* (they are the
"unwritable path never throws" cases). Merging that stream into stdout interleaves
mid-line and can split or swallow the terminal `ALL PASS`. The run was green — 311 PASS,
0 FAIL, exit 0 — and the merged pipeline showed 308 PASS and no `ALL PASS`.
**Keep the two streams separate and read the exit code.**

### Ranked, by how much of the build each covers

| # | Covers | Check | Expected |
|---|---|---|---|
| **H1** | The whole build | the command above | `ALL PASS`, no `FAIL` |
| **H2** | R1 + the 17 readers, on the SHIPPED header | `grep -o 'InstanceId,SignalId,' AnalysisLogger.vb \| wc -l` | `1` — the five appended AFTER `SignalId`, so no existing column moved |
| **H3** | T1, the schema twin (silent) | `diff <(sed -n '/Shared ReadOnly Header As String =/,/AbsorptionSizeMin"/p' AnalysisLogger.vb \| tail -n +2 \| sed 's/^ *//') <(sed -n '/Shared ReadOnly Header As String =/,/AbsorptionSizeMin"/p' tools/BacktestRunner/BacktestRowWriter.vb \| tail -n +2 \| sed 's/^ *//')` | no output. ⚠ `tail -n +2` drops the declaration line, which legitimately differs (`Private` vs `Public`) |
| **H4** | T2, the third schema copy (silent) | `grep -cE 'New ColSpec\("Absorption(EpisodeSec\|PullLB\|PostLB\|SizeStart\|SizeMin)", *ColKind\.Muted\)' tools/BacktestRunner/OverlapValidator.vb` | `5` — the five under review, present AND `Muted` |
| **H4b** | T2, whole-list regression | `grep -c 'New ColSpec(.*ColKind.Muted)' tools/BacktestRunner/OverlapValidator.vb` | `20` — 15 pre-existing + the 5 new |
| **H5** | R2 — the field is SET at open and CLEARED at close | `grep -cE '^ *(side\.)?EpisodeStartMs = (tsMs\|0)' Core/LevelAbsorptionTracker.vb` | `2` — the assignment in the episode-open block and the reset in `CloseEpisode()` |
| **H5b** | R2 — the field exists | `grep -c 'Public EpisodeStartMs As Long' Core/LevelAbsorptionTracker.vb` | `1` |
| **H6** | R3 — no rendered surface changed | `git diff --stat origin/master -- UI/MainForm_LiveStrip.vb UI/MainForm_PlaintextSnapshot.vb UI/MainForm_Render_Cards.vb` | empty |
| **H7** | R6 — no settings bump | `sed -n '2p' settings.json` | `"version": 68,` |
| **H8** | The live book's real shape | `head -1 bin/Debug/net8.0-windows/analysis_log.csv \| tr ',' '\n' \| wc -l` | `116` |

⛔ **Every number in the table above was RUN before it was written down, and three of my
first drafts were wrong.** Recording that, because it is the exact failure the CLAUDE.md
rule "verification handles must test the property, not a string that mentions it" exists
to prevent — and a reviewer following a wrong expected value rejects a sound build.

| Handle | I first wrote | Truth | Why I was wrong |
|---|---|---|---|
| **H3** | "no output" | a 3-line diff | The declaration line legitimately differs — `Private` in `AnalysisLogger.vb`, `Public` in `BacktestRowWriter.vb`. Fixed with `tail -n +2` |
| **H4** | `grep -c 'ColKind.Muted'` = `18` | `21` | I guessed the pre-existing count (15, not 13) **and** the raw grep matches a non-`ColSpec` line — `OverlapValidator.vb:539`, `If c.Kind = ColKind.Meta OrElse c.Kind = ColKind.Muted`. **A bare kind-count is not the property either.** Rewritten to match `New ColSpec("<name>", ColKind.Muted)` |
| **H5** | `grep -c 'EpisodeStartMs'` = `5` | `7` | **Three of the seven hits are COMMENTS** — including two I wrote myself explaining the invariant. This is the `_lastTs` failure verbatim: I counted a NAME, and my own prose inflated the count. Rewritten to match the two executable assignment sites |

⚠ **H4's `5` and H5's `2` are the load-bearing numbers.** If H4 reads `0`, the five
`ColSpec` entries are missing and the overlap check silently compares the wrong columns.
If H5 reads `1`, one of the two `EpisodeStartMs` sites is gone — and if it is the reset,
**fixture A60c is the only thing that catches it** (see this packet §3).

### Arithmetic identities

⭐ **H9 — the strongest single check in this packet, and the only one no fixture can
give.** On any row with a non-empty `AbsorptionLevel`, the two columns that ALREADY
existed must reproduce exactly from the five new ones. A fixture cannot prove this,
because it builds both sides from the same inputs; a live row can, because the tracker
computed the derived values independently of what was logged.

```bash
awk -F',' 'NR>1 && $102!="" {d=$115-$116; if(d<5000)d=5000; printf "%s pullFrac %.4f vs %s | ratio %.4f vs %s\n", $1, $113/(($114>5000)?$114:5000), $105, $104/d, $103}' bin/Debug/net8.0-windows/analysis_log.csv
```

Both computed values must match the logged ones to the logged format's precision.
`5000` is `absorption.depletion_floor_usd` — **read it from `settings.json` rather than
pasting the literal if the value has moved.** Verified on the 15:04:00 UTC row:
`33180/5000 = 6.6360` = the logged `AbsorptionPullFrac`; `7930/33180 = 0.2390` → the
logged `AbsorptionRatio` `0.24` at `F2`.

- **Header width:** `111 (pre-build) + 5 = 116`. Holds in all three copies (A60e) and in
  the live book (H8).
- **Positions:** `AbsorptionSignal` 101 · `AbsorptionPullFrac` 105 · `InstanceId` 110 ·
  `SignalId` 111 · the five at 112–116, contiguous, in spec order (A60d, 1-based).
- **Muted `ColSpec` count:** `13 + 5 = 18` (H4).

### The mutation evidence — every A60 fixture was made to fail on purpose

Each fixture passed on its first run, which is when to distrust it. Six mutations,
each reverted:

| Mutation | Fixture that fired | Fixtures that did NOT |
|---|---|---|
| Delete `EpisodeStartMs = 0` from `CloseEpisode()` | **A60c** | all others, incl. A60c's own re-open leg |
| Drop `AbsorptionSizeMin` from `BacktestRowWriter.Header` | **A60e** (`okTwin=False`) | A60a–d |
| Delete the `AbsorptionPostLB` `ColSpec` entry | **A60e** (`okCols=False`) | ⭐ **A43e PASSED** |
| Write `0` instead of empty for `AbsorptionPostLB` | **A60b** | A60a, c, d, e |
| Drop `.SizeMin` at the `ClassifyAbsorption` hop | **A60a** (`hopOk=False`) | A60b–e |
| Move `SignalId` from 111 to 116 in both headers | **A60d** (`iSid=115`) | A60c |

⭐ **Row 3 is the spec's T2 claim, proven rather than asserted: `A43e` passes while the
third schema copy is broken. A60e is the only guard against it.**

---

## 2. Decisions queued, with my read

### D-1 ⚠ The rotation `.bak` name is wrong and now demonstrably so

`AnalysisLogger.EnsureLogFile` rotates a superseded book to the hardcoded
`analysis_log.csv.v0.7.bak`. **That name was already wrong before this build** — it
named the v0.8 book "v0.7". The live run rotated a 12,312-row v0.8 book into
`analysis_log.csv.v0.7.20260901_145200.bak`.

- **(a)** Leave it. The `.bak` is never deleted and an existing one gets a timestamped
  sibling, so **nothing is lost** — only mislabelled.
- **(b)** Change the literal to name the superseded schema correctly.
- **(c)** Replace the hardcoded name with a content-derived one (e.g. a hash or width of
  the superseded header).

**My read, labelled a hypothesis: (a) for now, (b) only bundled with a schema-version
decision.** The v48 §4a fire-rate watch reads the rotated book **by name**, and the
"v0.7" string is what it reads. Changing the name without checking that consumer trades
a cosmetic defect for a real one. **I did not verify what that watch actually globs** —
see §4. **This is out of scope for this build and I did not touch it.**

### D-2 `EpisodeSec` format is `F1`; the spec does not rule it

`F1` gives 0.1 s resolution. The auto-run interval is 60 s, and the spec's own ⭐ note
expects the interesting signal to be **whether values cluster below one auto-run
interval**. `F1` resolves that comfortably.

- **(a)** Keep `F1`.
- **(b)** `F2`, if sub-100 ms episode ages ever matter.

**My read: (a).** The book fold runs at ~100 ms, so 0.1 s is already the measurement's
own granularity; `F2` would log precision the instrument does not have. **Flagging it
because the study reads this column and a format is not recoverable after the fact.**

### D-3 ⚠ `A60c` asserts through reflection into a private field

`SideState` is `Private`, so the only way to assert the `CloseEpisode` reset **directly**
is reflection (`_above` → `EpisodeStartMs`). Without it that reset is untested — see §3
D-6 below for why the spec's own described route cannot reach it.

- **(a)** Keep the reflection assertion.
- **(b)** Drop it and accept that the reset is defensive-only, untested code.
- **(c)** Make the reset observable without reflection (e.g. surface `EpisodeSec` on an
  inactive read) — **a behaviour change, and it would violate R4.**

**My read: (a).** The harness already reflects into private state (`A43e` reads
`AnalysisLogger.Header` that way). (c) is the only option that removes the reflection and
it costs the null discipline, which is a much more valuable property.

### D-4 The five columns' scope is CSV-only, and nothing consumes them yet

Nothing in `analysis/`, `tools/AutoTweaker/` or `tools/CeilingAudit/` reads the five.
That is correct for this build (D-2 of the proposal is gated behind ~2 weekday-weeks of
this data), but it means **a defect in these columns is invisible to every existing
report** until the study is written.

- **(a)** Leave them unconsumed until D-2's study.
- **(b)** Add them to `CsvFeatureBuilder`/`ConditionsExtractor` now so a defect surfaces.

**My read: (a), and I did not do (b).** (b) is squarely inside the spec §6's
out-of-scope fence. **Raising it only so the gap is recorded where it will be found.**

---

## 3. Spec-back proper — feedback on the spec itself

### ✅ What the spec got right, specifically

- ⭐ **R1's "counter-intuitive" framing did the work.** Being told *in advance* that
  grouping with the absorption siblings "reads better and is wrong" removed the
  temptation entirely. **The reason it was right is stronger than the spec knew:** the
  mutation test shows that moving `SignalId` to 116 breaks A60a, A60b and A60d at once —
  three fixtures, all reading by header name, all still wrong, because the ROW's own
  fields moved under them. Appending is the only safe operation and R1 named it.
- ⭐ **"Write A60e FIRST" is the single highest-value instruction in the spec, and the
  mutation evidence proves it.** `A43e` — the fixture that already existed to catch
  header drift — **passes** while `OverlapValidator`'s third copy is broken. Without
  A60e, T2 ships silently.
- ✅ **"Follow `PullFrac` as your template"** made the eight-hop type chain mechanical.
  Every hop had a working line two lines above the one I typed.
- ✅ **The handoff's instruction to "spot-check, don't redo" the reader audit** was right
  and saved real time: the 17 files re-verified in one pass because the previous seat had
  already recorded the access pattern per file.

### ⛔ Which assumption broke — `A60c`'s described failure mode

**The spec says:** *"A build that stores `EpisodeStartMs` but forgets the reset returns
the FIRST episode's elapsed time and looks correct in every simpler test."*

**It does not, and the fixture as specced would have passed a build with no reset at all.**
Episode-open assigns `side.EpisodeStartMs = tsMs` **unconditionally** (it must — that is
what makes the second episode measure from its own open), so the re-open overwrites any
stale value. The spec's named input — open, advance, close, re-open at the same level,
read — exercises the overwrite, not the reset.

**Proven, not argued.** With `EpisodeStartMs = 0` deleted from `CloseEpisode()`:

```
FAIL A60c … — at2=2 at5=5 idleActive=False startAfterClose=1700000000000 reopenActive=True reopenSec=0.5
```

`reopenSec=0.5` is **correct**. Only `startAfterClose` is wrong. **The re-open leg the
spec named is blind to the defect the spec was worried about.**

**What I substituted:** A60c asserts the reset **directly**, by reflecting into the
private `SideState` and requiring `EpisodeStartMs = 0` while the side is idle — plus the
re-open leg the spec asked for, plus a two-read leg (2.0 s then 5.0 s) proving it
measures rather than latches. **What it costs:** the fixture now depends on a private
field NAME, so a rename breaks it. That is the price of testing the property.

**Why this matters beyond A60c:** with the assignment unconditional, the reset is
*currently* defensive-only — nothing reads `EpisodeStartMs` while `Active` is False.
It stops being defensive the moment anyone makes the open conditional. **The comment on
`SideState.EpisodeStartMs` states the invariant so that change is a visible one.**

### ✅ The `A43e` escalation — the independent confirmation the handoff §3 asked for

**Verdict: mechanical, and it masked nothing.** Reasoning, so you can disagree cheaply:

1. **The three properties `A43e` asserts are positional in no way** — the `"BACKTEST-"`
   prefix, id equality across the two rows, monotonic `SignalId`. Locating the columns
   differently cannot change any of them.
2. **The header half of `A43e` never used the position at all** — it is a reflection
   comparison of two whole strings.
3. **`A43e` did not assert row width**, so it was not carrying a hidden column-count
   claim either. The trailing index was a *locating convenience*, valid only while
   `InstanceId`/`SignalId` happened to be last.
4. **Header-name resolution is this repo's own convention** — `AnalysisLogger.vb`'s v0.7
   note says "all readers since F9" resolve by name, and `A31f` two fixtures above
   already does exactly this for `AbsorptionSignal`.
5. **I added the one property trailing indexing gave incidentally** — `r1.Length =
   hdr.Length` on both rows. That is strictly stronger than what was removed.

**Scope check, re-run this session, repo-wide and not just in `verify/`:** a grep for
trailing-index row reads across every `.vb` file returns no other CSV-column consumer.
The hits are `parts(parts.Length - 1)` in settings-path splitting (`SettingsLoader`,
`SettingsDiffApplier`, `WhatIfLauncherForm`), `rows.Count - 1` on ROW lists
(`TradeStoreWriter`, `AutoTweakerCore`), and `lines.Length - 1` row COUNTS
(`AnalysisLogger.GetRowCount`, `TweakSettingsForm`). **None indexes a column from the end
of a row. `A43e` was the only instance, exactly as the handoff §2 recorded.**

### ⚠ Where the spec was narrower than its own words

- ⛔ **Acceptance §7 item 4 states the WRONG CONDITION, and following it literally would
  have made me report a correct build as broken.** It says: *"Confirm they are empty when
  `AbsorptionSignal` is `NONE` and populated when it is not."* **Population is not gated
  on `AbsorptionSignal`. It is gated on `HasEpisode`** — `MainForm_Analysis.vb` fills the
  numerics inside `If absRead.HasEpisode`, and the spec's own R4 says so, citing
  `AbsorptionSideRead`'s `Active=False` comment. The two conditions differ on exactly the
  case this build exists to instrument: **a D8-vetoed episode, which is `NONE` AND
  populated.**

  **This is not hypothetical — it is the only live episode row I got.** The 15:04:00 UTC
  row reads `AbsorptionSignal=NONE` with all five columns populated. Read against item 4
  as written, that row is a failure. Read against the actual contract, it is the single
  most informative row in the file. **A seat that checked item 4 literally and stopped
  would have filed a defect against correct code.**

  ✅ **Correct wording for the next spec:** *"empty on rows with no episode; populated on
  every row with an active episode, INCLUDING `AbsorptionSignal=NONE` rows, which are the
  D8-vetoed ones."*

  ⚠ **Separately, the item is not schedulable, and the spec sets no waiting bound.** An
  episode needs price inside `proximity_atr_frac × ATR` of a carried level while you
  watch; the spec's own §8 measured 89.3 %/89.6 % of episodes occupying a **single CSV
  row**. I got one after ~12 minutes and 13 rows — luck, not method. **An acceptance item
  whose completion depends on the market should say what to do if it does not fire**
  (I would have reported it as a named gap with fixture A60a standing in). Silence there
  invites a seat to quietly substitute the fixture and call the item met.
- **R5's table lists `ReplayLoop.vb` at `:470` as "sets absorption fields to `Nothing`"
  without saying it is a THIRD null surface that must stay in step.** It is easy to read
  as a courtesy edit. It is not: a replay row with `0` instead of empty would enter the
  overlap comparison as a value.

### ⚠ A constraint pair that nearly conflicted, and the hatch

**R1 (append after `SignalId`) vs the `Meta` convention in `OverlapValidator`.**
`ColKind.Meta` is documented as "`Timestamp`/`Price`/`InstanceId`/`SignalId` — separate
handling", and those four were the schema's bookends. R1 puts five `Muted` columns
*after* the trailing `Meta` pair, so `Meta` is no longer terminal.

**The hatch: `ColIndex()` resolves by NAME against `Cols`, and the comparison dispatches
on `Kind`, not on position.** Neither depends on `Meta` being last, so appending after it
is safe. **Name this in the next spec that appends to the schema** — the next
implementer will hit the same "wait, can I put a column after `SignalId`?" pause, and the
answer is in `ColIndex`'s body rather than anywhere near the `ColSpec` list. I added a ⛔
comment above `Cols` that says so.

### ⚠ One thing the spec did not have to say, but the next one should

**`AnalysisLogger.EnsureLogFile` rotates on ANY header change, and the spec never
mentions rotation happens.** R6 rules on the *documentation* of the rotation but not on
the *event*. In practice, deploying this build silently renames the running collector's
book on its first run. **That is correct and non-destructive**, but a spec that says
"five new columns, nothing else" should say the live book rotates on deploy.

---

## 4. What I did not verify, and cannot

- ✅ **RESOLVED — the populated half of acceptance §7 item 4 IS verified on live data.**
  This bullet previously said no episode opened in the observation window. One opened at
  **2026-09-01 15:04:00 UTC** and both derived columns reproduce exactly from the five
  new ones — see [`absorption-instrumentation-batch-summary.md`](absorption-instrumentation-batch-summary.md) §4.1.
  **Superseded in place rather than deleted, per the convention's own rule.**
- ⛔ **That the five columns are correct on the AWS collector** (`i-0d6c133058876273e`).
  I built and ran locally only. The collector's book will rotate on its first run after
  deploy; the five will be empty until an episode opens there.
- ⛔ **What the v48 §4a fire-rate watch actually globs for `.bak` files.** D-1 above turns
  on this and I did not open it. **Do not act on D-1 without checking it first.**
- ⛔ **The technical accuracy of the D-6a comment text (R8).** I transcribed it verbatim
  from the spec and did not re-derive the arm-early / measure-tight claim. The handoff
  §5 already flagged this as unverified and it remains so.
- ⛔ **Whether `EpisodeSec` will be USABLE once logged.** The spec §8's caveat stands
  unchanged: a per-row `EpisodeSec` reads the episode's age at the poll instant, which is
  a real measurement, but the distribution of episode LIFETIMES is still unrecoverable
  because the poll rarely catches an episode twice. **Say that in the study, not after.**
- ⛔ **Any claim about push state.** Run `git status -sb`; do not inherit one from this
  document.
- ⛔ **`OverlapValidator`'s actual comparison behaviour on a real synthetic-vs-live run.**
  I verified the `ColSpec` list matches the header (A60e); I did not run the `validate`
  verb over two real CSVs.
