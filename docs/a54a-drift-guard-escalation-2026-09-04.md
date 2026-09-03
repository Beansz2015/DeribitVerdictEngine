# A54a drift guard — session 1 stopped at the spec's own escalation trigger

**Status:** ⛔ **STOPPED before the guard code was written.** `EngineSettings.vb` carries three
already-ruled edits (below); `verify/ordercheck/Program.vb` is untouched — no walk, no
`DriftWalkResult`, no `A62a`–`A62g`. Nothing is committed.

**Spec:** [`a54a-json-poco-drift-guard-spec.md`](a54a-json-poco-drift-guard-spec.md), §4's
D-table, trader-directed 2026-09-04.

> ### Model: **Opus.** Effort: **HIGH.**
>
> **Trader decision, 2026-09-04 (mid-build):** presented with the gap in §1 below, the
> trader chose *"stop and escalate to Opus/high"* over re-syncing the four extra paths
> unilaterally or weakening the guard's acceptance bar. This document is that stop.
>
> **Why Opus, not a Sonnet handback.** The spec's own §0 already reasons about this exact
> failure shape — *"the reviewing seat's prototype... a growing [allow-list] is a hand-
> maintained table that can drift — which is the exact objection that killed option (a)"*
> — for the case where the allow-list needs a third entry. §1 below is the same class of
> problem one level up: **the *ruled* re-sync set turns out to be incomplete once actually
> measured**, exactly the D-3 pattern (*"a third looked deliberate and was not"*) but in
> the opposite direction — here, real drift the D-table never assigned to a decision.
> Re-opening a trader-ruled D-2 scope needs a fresh ruling, not an implementer's guess.

---

## 1. What triggered the stop

The spec's `A62a` fixture (§6) requires **zero unexplained drift** against the tracked
`settings.json`, and D-4 rules the guard **FAILS**, not warns, on any drift not on the
D-1 allow-list. The D-1 allow-list is fixed at **exactly two** entries
(`auto_run.start_engaged`, `signal_bridge.enabled` — §5's own table). D-2 re-syncs
**two** POCO defaults (`CvdSettings.SlopePctOfValue`, `MicroCvdSettings.AccelThresholdDynamicPct`);
D-3 re-syncs **one** (`NetworkSettings.Transport`). That is three re-syncs total, and the
mutation-test note for `A62a` names exactly those three (*"Revert any of the three
re-syncs (D-2's two, or D-3's transport)"*).

**But §3.1's own divergence table classifies NINE rows, and only three of the seven
`⛔ DRIFT` rows have anywhere to go.** Rows 3–6 —

| # | Path | POCO | JSON |
|---|---|---:|---:|
| 3 | `session_volume.sessions.ASIA.high_multiplier` | 0.8 | 1.00 |
| 4 | `session_volume.sessions.ASIA.mid_multiplier` | 0.85 | 1.00 |
| 5 | `session_volume.sessions.ASIA.execution_resolution` | 1 | 3 |
| 6 | `session_volume.sessions.LONDON.execution_resolution` | 1 | 3 |

— are classified `⛔ DRIFT` in §3.1, same symbol as rows 1/2/8 which DO get a re-sync.
They are never assigned to D-2, D-3, or the D-1 allow-list. Nothing rules them.

**The spec's own accounting already implies this is wrong.** §0: *"the shipped tree
contains **two** of those [deliberate], against **seven** real ones [drift]."* 2 + 7 = 9,
matching the table. Seven real drifts, but the D-table only disposes of three
(D-2's two + D-3's one). Rows 3–6 — four of the seven — are the gap.

**This is not a restatement of the spec's own words — it is measured**, independently,
against the CURRENT tree (§2 below), and it reproduces after the three ruled re-syncs are
already applied. It is not something the three re-syncs happen to fix as a side effect.

---

## 2. Independent verification — a walk built from §5 alone, not from the spec author's numbers

Per §11.1's instruction (*"Write the walk from §5 and see what it reports... do NOT ask
for the probe"*), this session wrote its own throwaway implementation of the exact
algorithm in the spec's §5 — case-insensitive key resolution, nullable-before-absent-key
ordering, root-provenance skip, `Dictionary`/`List`-by-name recursion, the D-1 allow-list
check — in a scratchpad project linking the real `Core/Settings/EngineSettings.vb`
(never touched the author's probe; the author's probe was never read). Run 2026-09-04,
**after** the three D-2/D-3 re-syncs below were already applied to the tree:

```
Compared: 261
Drifts: 4
  DRIFT session_volume.sessions.ASIA.high_multiplier: poco=0.8 json=1
  DRIFT session_volume.sessions.ASIA.mid_multiplier: poco=0.85 json=1
  DRIFT session_volume.sessions.ASIA.execution_resolution: poco=1 json=3
  DRIFT session_volume.sessions.LONDON.execution_resolution: poco=1 json=3
Orphans: 0
JsonOnly: 0
Skipped: 19
```

**Where this agrees with §3, independently:** `Compared=261` matches §3's measured 261
exactly. `Orphans=0` and `JsonOnly=0` match. `Skipped=19` matches §3's 4 root-provenance
+ 15 nullable exactly (4+15=19), confirming the nullable-before-absent-key ordering (§0
trap 2) is implemented correctly here too — this session did **not** repeat the
seven-false-orphan mistake the spec's own author made on their first attempt (§3.3).

**Where it diverges:** after applying D-2's two re-syncs and D-3's one, §3.1 predicts
zero remaining drift (the three-re-sync framing in §6/§9). The independent walk instead
finds these same four rows §3.1 itself already lists as `⛔ DRIFT` — rows 3–6, verbatim.
**Two independent implementations agree on which rows are wrong; the disagreement is with
the D-table's scope, not with the measurement.**

**Not a floating-point artefact:** 0.8 vs 1.00 and 1 vs 3 are not near-miss values: the
`Math.Abs(...) < 1e-9` tolerance is irrelevant here, and the `Integer` comparison
(`execution_resolution`) has no tolerance at all.

---

## 3. What IS already applied — the three ruled re-syncs, verified safe, uncommitted

**`Core/Settings/EngineSettings.vb`** carries D-2's two edits and D-3's one edit plus the
D-3 comment rewrite the ruling requires. `git diff` on the file (this session) is exactly
these three hunks — nothing else touched:

| Ruling | Property | Old → New | Line (post-edit) |
|---|---|---|---|
| D-2 | `CvdSettings.SlopePctOfValue` | `0.01` → `0.10` | `:369` |
| D-2 | `MicroCvdSettings.AccelThresholdDynamicPct` | `0.03` → `0.30` | `:401` |
| D-3 | `NetworkSettings.Transport` | `"rest"` → `"ws"` | `:1167` |

The D-3 comment at the old `:1149` (now `:1157-1165`) is rewritten per the ruling — it no
longer claims *"P3 flips the default. Stays 'rest' in P1/P2"*; it records that P3 shipped
at cutover v42 (2026-06-24) and restates the §4.1 safety argument (`ResolveSource`'s two
REST fallbacks, `WsFallbackToRest` defaulting `True`) inline at the declaration site.

**Verified safe this session, in the tree, before applying:**

- Zero fixture references to `SlopePctOfValue` or `AccelThresholdDynamicPct` by property
  name in `verify/ordercheck/Program.vb` — `grep` confirms only `MicroCVD.WindowSize` is
  set by any A1–A61 fixture; A1's `CalcCVD` and A2/A3's `CalcMicroCVD` calls don't pass
  `slopePctOfValue`/`accelThresholdDynamicPct` at all, so they run on the METHOD's own
  `Optional` default (0.05 — copy 4, spec §2/§8, untouched by this edit) rather than the
  POCO. Changing the POCO default cannot move these three fixtures.
- All `.Transport` reads in the app (`UI/MainForm_Analysis.vb:79/188/748`,
  `UI/MainForm_AutoRun.vb:190`, `UI/MainForm_SignalBridge.vb:119`,
  `UI/MainForm_Layout.vb:530`) read `cfg.Network.Transport` / `net.Transport` from the
  loaded config, never the compiled default. Every `verify/ordercheck/Program.vb`
  reference to `.Transport` either sets it explicitly (`A50BaseSettings`,
  `s.Network.Transport = "ws"`) or builds its own JSON literal — none depend on the
  POCO's compile-time default.

**Not yet true:** these three edits are **not proven by any fixture** — that is what
`A62a`/`A62b` were going to be. Per D-4, they must land in the SAME commit as the guard,
or the harness ships red with an edit nothing watches. **They should not be committed
alone.**

**Not touched:** `settings.json` (no version bump — none of the three change a settings
KEY, only a POCO default, per the spec's own §4 note) and `docs/DeribitIndicatorProject.md`
§15 (owed on the eventual single commit, not before).

---

## 4. What is NOT done

- The walk itself (`WalkPocoVsJson` / `DriftWalkResult`) — not added to
  `verify/ordercheck/Program.vb`. The scratchpad implementation in §2 above is throwaway,
  per §11.1, and was never intended to ship — it exists only to get an independently
  measured number.
- Fixtures `A62a`–`A62g` — none written.
- `OrderCheck.vbproj` — unchanged (correctly; nothing needed changing yet).
- No `dotnet build`, no harness run, no mutation-proof runs (§6a's *"every mutation must
  be RUN"* requirement), no `verify-gate.ps1`.
- Session 2 (§7's scoped-(b) dead-code removal) — untouched; it depends on session 1's
  guard existing as the instrument, per the spec's own ordering.
- No `docs/DeribitIndicatorProject.md` §15 entry.
- No `*-batch-summary.md` / `*-spec-back.md` per
  [`batch-review-packet-convention.md`](batch-review-packet-convention.md) — nothing
  finished enough yet to report.

---

## 5. The decision the next seat needs, and this session's read

**Three options, evaluated:**

1. **Re-sync all four session-bucket rows too**, same treatment as D-2's two: POCO
   defaults become `high_multiplier=1.00`, `mid_multiplier=1.00` (ASIA), and
   `execution_resolution=3` (both ASIA and LONDON). Seven re-syncs total, `A62a` passes
   clean, matches the spec's own *"seven real ones"* accounting exactly.
2. **Add all four to the D-1 allow-list instead**, arguing they are as-yet-unprioritised
   rather than truly wrong. This is the path the spec's own escalation trigger explicitly
   warns against — *"a growing [list] is a hand-maintained table that can drift, which is
   the exact objection that killed option (a)"* — and none of the four carries a "ruled
   by" citation the way `auto_run.start_engaged` and `signal_bridge.enabled` do (§5's
   table). D-1's own ruling text calls (a)/blanket-exemption *"reversing a trader decision
   to satisfy a test"* when done for one key; doing it for four with no ruling behind any
   of them is weaker, not stronger.
3. **Split session_volume into its own spec/decision**, leaving `A62a` unable to assert
   zero drift in the meantime. Rejected by the trader already (§0 above) — this was
   option 3 offered and not chosen.

**This session's read, for the record, not a ruling:** option 1. §3.1 already classifies
rows 3–6 `⛔ DRIFT`, the same symbol as the rows D-2 fixes without controversy; nothing in
the spec's text argues these four are *deliberate* the way rows 7/9 are (no declaration-
site comment defending 0.8/0.85/1 the way `start_engaged`'s and `signal_bridge.enabled`'s
comments defend their False defaults, per §3.2). **But this is a live production settings
value with parse-failure-path behaviour implications** (`execution_resolution` selects
which candle resolution the session-conditional stack runs at) that no D-decision in this
spec actually priced — which is exactly why the trader chose to stop rather than have
either a Sonnet or an Opus seat decide it in-flight.

**Recommended next step:** take this back to the trader as a scoped addendum to D-2
(*"D-2 covers 2 of 7 real drifts; rule the remaining 4"*) before writing any guard code,
same as D-3 got its own fresh ruling mid-spec on 2026-09-04. Once ruled, the build is
mechanical from here — §5's algorithm is now independently verified twice (§2 above), and
nothing else in the spec is in question.

---

## 6. Once the ruling lands — build order, unchanged from the spec

Nothing about §5 (the walk), §6 (fixtures A62a–g), or §6a (acceptance) needs to change
once the D-2 scope is settled — only the exact set of re-syncs and the corresponding
`A62a`/`A62b` mutation list picks up whatever the ruling adds. Build in the spec's own
order: **A62b first** (§6's own instruction — it is the only fixture that proves the walk
has teeth, independent of the allow-list question), then the walk, then A62a, then
A62c–g. Run every mutation in §6's table for real — reverted, confirmed FAIL, restored,
confirmed PASS — and record the actual output, per §6a and the standing *"nothing here
has ever been caught by care"* lesson the spec cites from
[`seat-handover-2026-09-03.md`](seat-handover-2026-09-03.md) §5.

**Do not re-derive §2's numbers from scratch** — two independent walks already agree on
`Compared=261`, `Orphans=0`, `JsonOnly=0`, `Skipped=19`. What remains open is exactly the
D-2 scope question in §5 above, nothing else.
