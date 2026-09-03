# Absorption instrumentation — batch summary (outcome record)

**Built:** 2026-09-01. **Seat:** Opus / high, per the handoff's own recommendation.
**Spec:** [`absorption-instrumentation-spec.md`](absorption-instrumentation-spec.md)
**Handoff + build plan:** [`absorption-instrumentation-escalation-2026-09-01.md`](absorption-instrumentation-escalation-2026-09-01.md)
**Review packet (read this alongside):** [`absorption-instrumentation-spec-back.md`](absorption-instrumentation-spec-back.md)

**Outcome: the WHOLE spec is built, and every acceptance check in
[`absorption-instrumentation-spec.md`](absorption-instrumentation-spec.md) §7 passes.**
`A43e` cleared, R1–R9 built, fixtures A60a–e written and each mutation-tested, gate
green, live run done with **both** halves of acceptance item 4 verified on real data.

---

## 0. Findings that change how the rest of this document reads

Placed at the top per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).

1. ⛔ **The spec's stated failure mode for fixture `A60c` cannot occur as the build is
   written, and the fixture had to be widened to catch the thing the spec wanted caught.**
   The spec says a build that "forgets the reset returns the FIRST episode's elapsed
   time." It does not: episode-open assigns `EpisodeStartMs` **unconditionally**, so a
   re-open overwrites a stale value and the re-open leg alone is blind to a missing
   reset. **Proven by mutation, not argued** — deleting the reset leaves
   `reopenSec=0.5` correct and only the added reflection assertion fires. Detail in the
   packet §3.
2. ⚠ **`EnsureLogFile`'s rotation `.bak` name is the hardcoded string
   `analysis_log.csv.v0.7.bak`, which was already wrong for the v0.8 book.** The live run
   rotated a 12,312-row v0.8 book into `analysis_log.csv.v0.7.20260901_145200.bak`.
   **Nothing was lost** — an existing `.bak` gets a timestamped sibling rather than being
   overwritten — but the rotated book is misnamed. **Left unchanged deliberately**; it is
   a behaviour change no ruling covers. Queued as a decision in the packet §2.
3. ✅ **`A43e`'s trailing-position indexing was mechanical to fix and masked nothing.**
   The independent confirmation the handoff §3 asked for is in the packet §3, with the
   scope check that it is the only such reader in the tree.
4. ⭐ **A live episode row proves the five new columns REPRODUCE the two derived columns
   that already existed** — `AbsorptionPullFrac` and `AbsorptionRatio`, exactly, from
   their own newly-visible denominators. **No fixture can prove this** (a fixture builds
   both sides from one input); a live row can. §4.1 below.

---

## 1. Per-item outcome

| Item | Spec ref | Outcome |
|---|---|---|
| Clear `A43e` | handoff §1–§3 | ✅ Header-name resolution + an explicit row-width assertion |
| R8 D-6a comment | spec R8 | ✅ Verbatim, at the episode-open block; no behaviour touched |
| R2 `EpisodeStartMs` + `ReadSide(nowMs)` | spec R2 | ✅ One new field, one signature change, one call site |
| §3 type chain | spec §3 | ✅ All 8 hops; 3 `New … With {}` initialisers in the tree, all updated |
| R5 three-file rotation | spec R5 | ✅ `AnalysisLogger` · `BacktestRowWriter` · `OverlapValidator` · `ReplayLoop` |
| R1 append after `SignalId` | spec R1 | ✅ Positions 112–116, verified live and by fixture A60d |
| R4 empty when idle | spec R4 | ✅ `Double?` + `InvOpt`; verified live and by fixture A60b |
| R3 no live-strip change | spec R3 | ✅ `ComposeAbsorption` untouched; stated in the commit message |
| R6 §15 entry, no settings bump | spec R6 | ✅ One row in `docs/DeribitIndicatorProject.md` §15; settings stays **v68** |
| R7 fixture family A60 | spec R7 | ✅ A60a–e; no `HC` invented, HC29 still free |
| R9 reader audit | spec R9 / §4 | ✅ 17 lines, re-verified this session — packet §0 |

## 2. Files changed

| File | Change |
|---|---|
| `Core/LevelAbsorptionTracker.vb` | `SideState.EpisodeStartMs`; set at open, cleared in `CloseEpisode()`; `ReadSide` takes `nowMs`; 5 props each on `AbsorptionSideRead` and `AbsorptionRead`; the D-6a comment (R8) |
| `Core/Indicators_OrderFlow.vb` | `ClassifyAbsorption` carries the 5 through to `AbsorptionRead` |
| `Core/IndicatorResults.vb` | 5 × `Double?` properties |
| `UI/MainForm_Analysis.vb` | 5 assignments inside the existing `If absRead.HasEpisode` block |
| `AnalysisLogger.vb` | Header + 5 `InvOpt` values; a version-history comment carrying R6's rotation-not-a-boundary statement |
| `tools/BacktestRunner/BacktestRowWriter.vb` | The twin — same header, same 5 values, same order |
| `tools/BacktestRunner/OverlapValidator.vb` | 5 `ColKind.Muted` `ColSpec` entries + a ⛔ comment naming the list as the third schema copy |
| `tools/BacktestRunner/ReplayLoop.vb` | 5 × `Nothing` on the replay path |
| `verify/ordercheck/Program.vb` | `A43e` fix + the A60 family + dispatch registration |
| `docs/DeribitIndicatorProject.md` | §15 entry |

**No `settings.json` change** — no config keys added. Settings stays **v68**.

## 3. Column formats

| Column | Position | Format | Unit |
|---|---|---|---|
| `AbsorptionEpisodeSec` | 112 | `F1` | seconds |
| `AbsorptionPullLB` | 113 | `F0` | USD |
| `AbsorptionPostLB` | 114 | `F0` | USD |
| `AbsorptionSizeStart` | 115 | `F0` | USD |
| `AbsorptionSizeMin` | 116 | `F0` | USD |

The four USD columns follow `AbsorptionAggrUsd`'s existing `F0`. `F1` on `EpisodeSec` is
an implementer choice the spec does not rule — see the packet §2 D-2.

## 4. Acceptance (spec §7)

| | Check | Result |
|---|---|---|
| 1 | `dotnet build` clean | ✅ `dotnet build DeribitVerdictEngine.sln` — 0 warnings, 0 errors |
| 2 | Harness green incl. the five A60 fixtures | ✅ `ALL PASS` |
| 3 | `tools/checks/verify-gate.ps1` green | ✅ `GATE PASSED` (`-Mode prepush`) |
| 4 | One live run, new columns read back | ✅ **BOTH halves verified on live data** — see below. ⭐ **AND RE-VERIFIED IN PRODUCTION 2026-09-03 on 1,664 collector rows — see §4.2** |
| 5 | Reader audit's 17 lines recorded | ✅ packet §0 |
| 6 | `docs/DeribitIndicatorProject.md` §15 entry with R6's statement | ✅ |

### 4.1 The live run

App launched from `bin/Debug/net8.0-windows/`, settings **v68**, `transport: "ws"`,
`auto_run.start_engaged: true`, `absorption.enabled: true`,
`absorption.scoring_enabled: false`. WS connected, 7 channels subscribed.

**First live row, 2026-09-01 14:52:00 UTC**, read back by header name:

```
101:AbsorptionSignal=NONE     110:InstanceId=ec46c77d-…   112:AbsorptionEpisodeSec=
102:AbsorptionLevel=          111:SignalId=1              113:AbsorptionPullLB=
103:AbsorptionRatio=                                      114:AbsorptionPostLB=
104:AbsorptionAggrUsd=                                    115:AbsorptionSizeStart=
105:AbsorptionPullFrac=                                   116:AbsorptionSizeMin=
```

✅ **Empty when `AbsorptionSignal` is `NONE`** — confirmed on real data, and the existing
columns sit at their pre-build positions (101–105, 110, 111) on a real written row.

**Then an episode opened.** Row 13, **2026-09-01 15:04:00 UTC**, `ATR` 77.6105:

```
101:AbsorptionSignal=NONE        112:AbsorptionEpisodeSec=1.3
102:AbsorptionLevel=77814.31     113:AbsorptionPullLB=33180
103:AbsorptionRatio=0.24         114:AbsorptionPostLB=0
104:AbsorptionAggrUsd=7930       115:AbsorptionSizeStart=33180
105:AbsorptionPullFrac=6.6360    116:AbsorptionSizeMin=0
```

✅ **Populated when an episode is active.** ⚠ `AbsorptionSignal` is still `NONE` here and
that is **correct, not a defect** — the numerics populate for ANY active episode, and
`pullFrac` is logged even on D8-vetoed ones. This row IS a D8 veto.

⭐ **The two derived columns reproduce exactly from the five new ones, on live data.**
This is an independent consistency proof no fixture can give, because the fixture builds
both sides from the same inputs:

| Identity | Computed from the new columns | Logged |
|---|---|---|
| `PullLB / max(PostLB, depletion_floor_usd)` | `33180 / max(0, 5000)` = **6.6360** | `AbsorptionPullFrac` = **6.6360** |
| `AggrUsd / max(SizeStart − SizeMin, floor)` | `7930 / 33180` = **0.2390** | `AbsorptionRatio` = **0.24** (`F2`) |

**This is exactly what the instrumentation was built for.** Before it, this row said only
"NONE, ratio 0.24, pullFrac 6.64". Now it says *why*: the band held 33,180 USD at episode
open, went to **zero** with **no** provable posts, so every dollar of the depletion was a
provable pull — `pullFrac` 6.64 against `max_pull_frac` 0.75. **Painted defense, and the
denominators are finally visible.**

⚠ **`EpisodeSec` = 1.3 s on this row — far below the 60 s auto-run interval.** That is
the shape the spec's ⭐ note said to watch for. ⛔ **One row is not evidence and this build
does not chase it** (spec §6). It is recorded here for the study that reads the column.

### 4.2 ⭐ Production verification — added 2026-09-03 by the reviewing seat

**Deployed 2026-09-01 15:49:02 UTC** to `i-0d6c133058876273e` via `tools/ops/collector.ps1 deploy`, commit `49d0098`. All six hashes verified against the local Release build **before** the run. Read back read-only over SSM.

| | |
|---|---|
| Rows since deploy | **1,664** · `2026-09-01 15:50:01` → `2026-09-03 12:36:00` |
| Cadence | **37.2 rows/hour** vs the 37.0 baseline in [`seat-handover-2026-08-29.md`](seat-handover-2026-08-29.md) §1.1 — **healthy** |
| Header | **116 columns**, the five appended after `SignalId` — **R1 honoured in production** |
| Rotation | `analysis_log.csv.v0.7.bak`, **31,234,053 bytes** — 9,229 larger than the pre-deploy copy-back, exactly the rows between the fetch and the restart. ⚠ **No timestamp in the name** — there was no pre-existing `.bak`, so it took the hardcoded string. That is §2's D-1 |
| Populated rows | **289** (17.4 %) |
| `PullLB`/`PostLB` cross-check | ✅ **145 rows.** Recovered 412,422 vs actual 412,440; 55,593 vs 55,590 |
| `SizeMin`/`SizeStart` cross-check | ✅ **7 of 27** usable; 20 blocked by F2 rounding on `AbsorptionRatio`. Agreement 0.24 %–4 % where `Ratio ≥ 0.04` |
| Absorption signals fired | **0** in 44.8 h — expected (289 × 0.17 % funnel survival ≈ 0.5) and **re-confirms the under-engagement finding on fresh post-deploy data** |

⭐ **Acceptance §7 item 4 is CLOSED for all five columns.** The 26 % discrepancy on the single `Ratio = 0.01` row is F2 quantisation — the error scales inversely with `Ratio`, which is rounding's signature, not a defect's.

---

## 5. Gate tail

```
=== harness ===        ALL PASS
OK    harness ALL PASS
=== display-parity === OK    no snapshot/card drift detected
=== version-bump ===   OK    no engine-path change
=== result ===         GATE PASSED
```

## 6. Not committed — the ready commit message

⛔ **This build is UNCOMMITTED.** The seat did not commit, because committing was not
asked for. Nothing is staged. **Verify with `git status -sb`; do not inherit a push state
from this document.**

The message below satisfies the CLAUDE.md display-string parity rule, which requires a
commit that changes engine output to either update the card binding or **state why no
card surface is affected** (spec R3):

```
feat(absorption): log EpisodeSec/PullLB/PostLB/SizeStart/SizeMin as CSV cols 112-116

Implements docs/absorption-instrumentation-spec.md (R1-R9), authorised by D-1 in
docs/absorption-mechanism-revision-proposal.md §6. Four of the five were already live
state on LevelAbsorptionTracker.SideState and were discarded at the read boundary;
AbsorptionEpisodeSec is the one new measurement (R2: SideState.EpisodeStartMs, set at
episode open, cleared in CloseEpisode(), nowMs threaded into ReadSide from Snapshot).

R1: appended AFTER SignalId, positions 112-116, NOT grouped with the Absorption*
siblings at 101-105. Inserting at 106 would shift Placed*/InstanceId/SignalId by five
and break every positional reader silently. Schema rotation, NOT a comparability
boundary: no existing column moves and none changes meaning (R6).

R3 — NO CARD SURFACE IS AFFECTED. All five are CSV-only diagnostics for a study, not
trader-facing signal. UI/MainForm_PlaintextSnapshot.vb and UI/MainForm_Render_Cards.vb
are untouched, and ComposeAbsorption in UI/MainForm_LiveStrip.vb is deliberately left
rendering only "ABS<arrow> <level> (<ratio>x)".

R6: no settings.json version bump — no config keys added or changed; stays v68. A
docs/DeribitIndicatorProject.md §15 entry IS written and carries the rotation-not-a-
boundary statement.

Also fixes fixture A43e, which located InstanceId/SignalId by trailing position
(Length - 2 / Length - 1) and only worked while they were the last two columns. It now
resolves both by header name and asserts row width explicitly. It was the only
trailing-position CSV column reader in the tree; the reader audit's other 16 files
resolve by header name or do not parse rows.

Fixtures A60a-e, each mutation-tested to confirm it fails on the defect it exists to
catch. Reports: docs/absorption-instrumentation-batch-summary.md +
docs/absorption-instrumentation-spec-back.md.
```

⚠ **Do NOT add `[no-engine-change]`** to this message. The gate's version-bump check will
WARN, and that WARN is correct: the logged output does change. The token would be false.

⚠ Per the harness convention, **branch before committing — `master` is the default
branch.**

---

⚠ **`version-bump` reported "no engine-path change" because the gate diffs COMMITTED
work against `origin/master` and this build was uncommitted when it ran.** On the
post-commit pre-push run it will WARN (an engine path changed, `settings.json`'s version
line did not). **That WARN is correct and expected** — R6 rules no bump. It is advisory,
not blocking. **Do not silence it with `[no-engine-change]`**; that token would be false,
because the logged output does change.
