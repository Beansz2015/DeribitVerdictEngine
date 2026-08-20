# Spec-back — thin-trade-window skip gate

**For:** the reviewing seat (or the trader, before push).
**Spec reported against:** [`thin-trade-window-skip-gate-proposal.md`](thin-trade-window-skip-gate-proposal.md) — D1–D5 all ticked as recommended, trader-directed.
**Commit:** `613cf1e` (the whole build — settings v66 → v67, one commit, per the spec's own "session split: none").

> **Recommended review tier: Sonnet, effort medium.** Matches the build's own tier (§0 of the spec). The diff is small and every trap the spec named is independently checkable in one command (§1 below). The one item that needs judgment rather than a grep is §4's coverage gap on `ExitGuardEvaluator` — read that section before deciding whether it blocks.

**⚠ ONE DOCUMENT, deviating from [`batch-review-packet-convention.md`](batch-review-packet-convention.md) on purpose** — that convention is for multi-lane batches handed to a different seat; this is one item, one spec, one commit, and the outcome record already lives in the reply that shipped it plus `settings.json`'s own `change_log`. Nothing from either is repeated here.

**State:** built, committed, **NOT pushed.** Per the crypto-trading-context commit workflow, the trader tests and pushes.

---

## 1. Ranked verification handles

All one command or one grep; none re-runs the build.

### ⭐ If you only run one — TRAP 1 did not survive

```bash
grep -n "50" Core/ScoringEngine_Helpers.vb UI/MainForm_Analysis.vb ExitGuardEvaluator.vb
```

**Expect zero hits inside the new gate logic.** The function body is `Math.Max(cfg.Indicators.TFI.WindowSize, cfg.Indicators.MicroCVD.WindowSize)` with no literal; the skip-gate `ElseIf` and the `ExitGuardEvaluator` guard both call `ScoringEngine.MinTradesForScoring(cfg)`, never a number. (The command *will* surface unrelated pre-existing hits — `candles1m.Count < 50`, `r.EMA50`, the `GetRecentTradesAsync(500)` window — none of which are new; check that none sit inside the new `ElseIf`/`If` lines.) **I ran this before writing the doc; it came back clean.**

### H2 — the mutation proof actually ran, not just claimed

```bash
git log -p --all --grep="MUTATION PROOF" -- Core/ScoringEngine_Helpers.vb
```

Won't find anything — the mutation was never committed, by design (§6 of the spec: prove it, then revert before shipping). What I can hand you instead is the transcript: I temporarily replaced the function body with `Return 50`, rebuilt, and re-ran the harness. **Result: A57c and A57d FAILED** (`minTrades=50 fires79=False passes80=True` for A57c). Reverted, rebuilt, re-ran: **ALL PASS** again. This is the exact obligation the spec's §6 states — *"revert the gate condition to a hardcoded `< 50` and confirm A57c fails. If it does not, the fixture is not testing what it claims."* It failed as required, so the fixture has teeth. **You cannot verify this yourself without redoing it** — it's the one claim in this packet that's a report of an action, not a grep. If you want it re-proven, the one-line edit is `Core/ScoringEngine_Helpers.vb:67-70`.

### H3 — the derivation reads the right settings, not a copy

```bash
grep -n '"window_size": 30\|"window_size": 50' settings.json
```

Two lines: `TFI` at 30, `MicroCVD` at 50 (`settings.json:284-285`) — the live shipped values `MinTradesForScoring` derives from (`Max` = 50). (A plain `grep -n "window_size"` also works but additionally surfaces the two huge prose `change_log`/`modified_by` lines that narrate this commit — noisy, not wrong.) No third copy exists — the function reads `cfg.Indicators.TFI.WindowSize` / `cfg.Indicators.MicroCVD.WindowSize` directly, the same POCO fields `CalcTFI`/`CalcMicroCVD` already consume, so there is nothing to drift.

### H4 — the tweaker fence exists on both sides and doesn't overreach

```bash
grep -n "min_trades_for_scoring_override" tools/AutoTweaker/SettingsDiffApplier.vb tools/AutoTweaker/PromptBuilder.vb
```

Expect one exact-match `If path = ...` block in the applier (citing HARD CONSTRAINT 28) and one numbered rule in the prompt builder. **Neither is a prefix** — confirm by checking `scoring.verdict_med_pct` (or any other `scoring.*` key) still validates; A57d's second half pins this (`rSibling.IsValid` in the fixture).

### H5 — the version-bump discipline actually fired

```bash
git show 613cf1e --stat -- settings.json docs/DeribitIndicatorProject.md docs/history-archive.md
```

Expect all three touched. `settings.json` version 66 → 67; `DeribitIndicatorProject.md` §15 gains a v67 row and loses its v62 row (five versioned rows is the cap — v67/v66/v65/v64/v63); `history-archive.md` §E gains v62 verbatim, with a "Fifth batch moved 2026-08-20" note. `tools/checks/verify-gate.ps1 -Mode prepush`'s own `version-bump` check confirms this mechanically — I re-ran it post-commit and it reports `OK engine-path change accompanied by a settings.json version bump` (it reported the unhelpful `no engine-path change` pre-commit, because that check diffs against `origin/master`, not the working tree — expected, not a defect).

### H6 — the six-project build set, all Release, all zero

```bash
for p in DeribitVerdictEngine.sln tools/AutoTweaker/AutoTweaker.vbproj tools/WhatIfRunner/WhatIfRunner.vbproj tools/CeilingAudit/CeilingAudit.vbproj tools/BacktestRunner/BacktestRunner.vbproj verify/ordercheck/OrderCheck.vbproj; do dotnet build "$p" -c Release 2>&1 | grep -cE "error"; done
```

**Six zeros**, and I ran this — not just the two builds the earlier turn happened to touch. `Core/ScoringEngine_Helpers.vb` and `Core/Settings/EngineSettings.vb` are linked by every one of the six per their `.vbproj` `Compile Include` lists, so a silent break in a project I didn't explicitly build would have shown here (the F10 lesson this project already keeps a rider for).

---

## 2. Decisions queued, with my read where I have one

The D-table itself has nothing open — the trader ticked all five before the build started. Two smaller things surfaced during the build that the spec didn't anticipate and that I resolved without asking, because neither changes behaviour or crosses a settled ruling. Flagging both so the resolution is visible rather than silent.

### Q1 — `MinTradesForScoring(cfg)` is called twice per skip in `MainForm_Analysis.vb`

The `ElseIf` condition and the reason-string interpolation each call the function independently, rather than caching it in a `Dim`.

- **(a)** leave it — the function is a pure `Math.Max` over two already-loaded ints, and it only runs on the branch that (per D-3's own falsifiable prediction) should fire near-zero times.
- **(b)** cache it in a local once at the top of the check.

**My read: (a), and I hold it loosely.** The cost of the extra call is not measurable, and the code reads as directly as the spec's own §3.1 pseudocode (which also calls it twice, in the `ElseIf` and in the reason string). (b) is a two-line change if you disagree — not worth re-opening the build for.

### Q2 — no fixture drives `ExitGuardEvaluator.Evaluate` through the *thin, non-zero* path specifically

Covered in full in §4 below, because it's a coverage gap rather than a decision with two live options — but naming it here too since it's the one place a reviewer might want to say "build the fixture before this ships" rather than "note it and move on." **My read: note it and move on** — see §4 for why.

---

## 3. Spec-back proper — feedback on the spec itself

### 3.1 What the spec got right, specifically

- **§6's mutation obligation, stated as an instruction rather than a suggestion, is why this build has teeth.** *"Revert the gate condition to a hardcoded `< 50` and confirm A57c fails. If it does not, the fixture is not testing what it claims and must be rewritten before the build is offered for review."* I would not have run that step unprompted — the naive belief that "A57c mutates `MicroCvdSettings.WindowSize`, so it obviously can't be satisfied by a hardcoded 50" is exactly the trap, and it reads as obviously true right up until you actually revert the code and watch the fixture pass anyway (which it does *not*, here — but the spec is right that this needs checking, not assuming).
- **TRAP 2's framing — "gate on the list the indicators actually consume, not the requested count"** — was the one place I nearly wrote the wrong thing on a first pass, reaching for `recentTrades.Count` before checking it really is the post-`Math.Min` delivered count (`WsMarketDataSource.vb`) and not the `500` requested at the call site (`MainForm_Analysis.vb:89`). The spec named this explicitly enough that the check took one read of `WsMarketDataSource.GetRecentTradesAsync`, not a debugging session.
- **The escalation triggers (§0) are genuinely falsifiable and none fired** — I did not touch `CalcTFI`/`CalcCVD`/`CalcMicroCVD`/`CalcLiquidations`, and the D-table in §5 answered every question that came up. Worth recording that a spec's escalation section did its job by *not* being needed, which is easy to overlook when writing up a build that went smoothly.

### 3.2 Where the spec was narrower than its own words

**§6's A57a/A57b language describes "a run" producing a skip and "writ[ing] no CSV row"** — prose that reads as if the fixture drives the actual `RunAnasysisAsync` pipeline end to end. **It cannot, and the spec doesn't say so.** `UI/MainForm_Analysis.vb` is WinForms and is deliberately not linked into `verify/ordercheck/OrderCheck.vbproj` — the same boundary the project already documents for fifteen-plus other features (the `.vbproj`'s own comments name it "the A16-A31 boundary notes"). What I built instead is the project's standing substitute for that boundary: A57a/b/c/d exercise `ScoringEngine.MinTradesForScoring(cfg)` directly (the host-agnostic seam both the UI gate and the exit guard call) and separately assert the reason-string format the UI code builds from it. This is consistent with how every comparable UI-boundary feature in this codebase is tested, but it means **the actual `ElseIf` chain in `MainForm_Analysis.vb` — its position in the chain, whether `cfg` is the right variable in scope, whether the `&`-concatenation compiles to the intended string — is verified by full-solution build and by reading, not by a fixture that executes that file.** §7's acceptance criteria (solution builds 0/0) catches a compile break; nothing catches a logic break in that specific file short of a live run. Named in full in §4.

### 3.3 A constraint pair that resolved cleanly, worth recording why

**D-3's "not a dataset boundary" ruling and the fixture family's own A57b ("does not over-fire") could have pulled in different directions** — a spec arguing hard that a gate should almost never fire, paired with a fixture family whose whole point is proving the gate fires correctly, risks reading like the fixtures matter less because the live rate is expected to be zero. **They don't conflict, and the reason is worth stating for the next spec that has this shape:** D-3's "near-zero" is a claim about the *live population* (how often a real connect drops the trade seed), while A57a–d prove the *mechanism* (that the derived threshold is computed and applied correctly) independent of how often it's exercised live. A near-zero live rate does not reduce the value of a correct mechanism — it is, if anything, the reason the mechanism needs to be provably right rather than empirically validated, since the live evidence will be sparse by design (§8's own post-ship watch is reading a rate expected to be ~0).

### 3.4 One thing I did that the spec didn't ask for

Section 1 of the spec doc's proposal frames `ExitGuardEvaluator`'s existing `.Count = 0` guard and the new derived-minimum guard as the same posture (D-4). I implemented it as a single combined condition — `allTrades Is Nothing OrElse allTrades.Count < ScoringEngine.MinTradesForScoring(cfg)` — rather than two separate `If` statements. The spec doesn't specify the shape, only the behaviour (below the minimum ⇒ Clear, never skip/throw), and the combined form is behaviourally identical and reads more directly. Flagging only because it's a place I made a shape choice the spec left open, not because it's contentious.

---

## 4. What I did not verify, and cannot from here

| Item | Why not |
|---|---|
| ⭐ **The live `MainForm_Analysis.vb` skip gate actually firing on a thin trade list, in the running app** | No live run — same reasoning as every WinForms-boundary feature in this project. Covered by build + read, not by a fixture that executes the file. This is the gap §3.2 names in full. |
| ⭐ **`ExitGuardEvaluator.Evaluate` returning Clear on a *thin-but-nonzero* buffer with genuinely adverse content** | `A17f` proves the **empty**-buffer case (0 trades, unaffected by this change — 0 was already `< 50`). `A57a–d` prove the **derivation** in isolation. **No fixture constructs, say, 30 heavily-adverse trades and asserts `Kind = Clear` because 30 < the derived minimum.** The code is a one-line, shared-condition change and I read it twice, but this specific behaviour is asserted by neither an existing fixture nor a new one — I did not build one because the spec's §6 fixture list names only A57a–d and doesn't ask for an `ExitGuardEvaluator`-specific case. If you want this closed rather than noted, it's a ~15-line fixture in the `A17` family's own style (seed N adverse trades where `N < MinTradesForScoring`, assert Clear despite what would otherwise be `AdverseCount ≥ 2`). |
| **The post-ship skip rate (§8's falsifiable prediction)** | Needs a live weekday-week. Nothing here can substitute for it. |
| **Whether `DeribitWsFeed.SeedAsync`'s failure mode is exactly as described in §1.1** | Carried from the spec, which itself states "NOT CHECKED: nothing was run live" in the originating audit ([`trader-tick-queue.md`](trader-tick-queue.md) §2). I did not independently re-derive the ~1.4 trades/sec figure or trace `ExecuteWithRetry`'s retry count on the seed call. |
| **Concurrency** | `recentTrades`/`allTrades` access patterns are unchanged by this build (same lists, same call sites) — no new lock surface was introduced, but I did not specifically test concurrent access. |
| **The A1–A56g fixtures beyond the ones this change could plausibly affect** | Confirmed unregressed via the harness's `ALL PASS`, not individually re-read. |

---

**Summary for whoever reads only this line:** the mechanism is provably correct (H1/H2), the fence and version-bump discipline are in place (H4/H5), and the one real gap is that neither the live UI wiring nor `ExitGuardEvaluator`'s specific thin-buffer behaviour has a fixture exercising it directly (§4) — both are one read/one small fixture away from closed, and neither blocks a push on my reasoning, but they're the two things I'd want a second pair of eyes on before calling this fully proven rather than build-correct-by-construction.

---

# ✅ REVIEW RESPONSE — follow-up commit, fixture items only, no code defect

**Reviewing seat confirmed the mechanism independently and found it correct**: derivation, override, gate placement (after the null arm, so `.Count` cannot throw), exit-guard Clear posture, HC28 exact-match with the sibling passing, v67, §15 at exactly five rows with v62 archived, six builds clean, harness `ALL PASS`. Three fixture-family findings came back, all against §4/H2's own content. **No settings key, no version bump — commit tagged `[no-engine-change]`.**

## F1 — A57b deleted (vacuous, proven by mutation)

**Confirmed and closed.** The review mutated `MinTradesForScoring` to drop the MicroCVD term (`Max(TFI, 1)` = 30, not 50): A57a/c/d all failed, **A57b passed** — `minTrades < minTrades` reduces to `Not False`, `(minTrades − 1) < minTrades` reduces to `True`, both independent of what `minTrades` actually is. It cannot fail for any implementation; it asserts that VB's `<` operator is a strict less-than. A57c already pins the same boundary at a non-default window (`fires79`/`passes80`), which is the strictly stronger version of the same claim. **A57b deleted. A57c/A57d numbering kept unchanged**, since the spec and this packet both reference them by name.

**Part of this is on the spec, and the review said so directly rather than leaving it implicit — worth restating here because it's the actionable half for next time.** §6 asked for "a run at exactly the derived minimum," which the harness cannot build at the WinForms boundary (§3.2 of this packet already names that boundary for A57a/b). I substituted a predicate-only check and it happened to be a vacuous one. **The lesson, generalized: when a spec names a fixture the harness structurally cannot build as described, the spec should say what stands in for it** — or, failing that, the implementer substituting one should sanity-check that the substitute can actually fail before shipping it, which a mutation pass (§6's own obligation) would have caught immediately had I run it against A57b specifically rather than only against A57c.

## F2 — reason string hoisted to `ScoringEngine.ThinTradesSkipReason`, Q1 resolved as (b)

**Built.** New `Public Shared Function ThinTradesSkipReason(count As Integer, minTrades As Integer) As String` beside `MinTradesForScoring` in `Core/ScoringEngine_Helpers.vb`; the skip gate in `UI/MainForm_Analysis.vb` now computes `minTradesForScoring` once (a `Dim` ahead of the resilience-check chain) and passes it to both the `ElseIf` condition and `ThinTradesSkipReason` — one call, not two, closing §2 Q1 by option (b) rather than a bare local cache, per the review's framing. A57a now asserts against the real function (`ScoringEngine.ThinTradesSkipReason(count, minTrades)`) instead of building a copy of its format inline — the exact gap §3.2 named: *"the production string is never invoked, so changing it leaves A57a green."* **Mutation-proved**: reverted `ThinTradesSkipReason` to a stub string, rebuilt, A57a failed (`reason='recent trades thin'`, missing the counts); reverted back, A57a passes again.

**Display-string parity: no card obligation, stated per the hard rule** — the reason string's *value* is byte-identical (`"recent trades thin (n<m)"`); only *where* it is constructed moved, from an inline `&`-concatenation to a named function. Nothing rendered changes.

## F3 — A57e built (the exit-guard's silent-Clear window, now pinned)

**Taken up, not just noted — the review's framing is right that this outranks the other two.** New `A57e_ExitGuardClearOnThinAdverseBuffer`: 40 trades (`< 50`, the shipped derived minimum), heavy one-sided sells, evaluated **twice** — once with `MinTradesForScoringOverride=1` (bypassing the gate) to prove the buffer really does read `Kind=Exit, AdverseCount>=2` if evaluated, once at shipped defaults to prove `Kind=Clear`. The bypass comparison is what makes the assertion mean something rather than merely being consistent with a Clear result for unrelated reasons — the same distinction §6's mutation obligation exists to enforce, applied here without needing to mutate production code at all, since the override *is* a legitimate way to get the counterfactual. **Mutation-proved anyway, on the guard itself**: reverted `ExitGuardEvaluator`'s condition to the pre-build `.Count = 0`, rebuilt, A57e failed (`default=Exit` instead of `Clear` — the exact regression D-4 exists to prevent); reverted back, A57e passes.

## Housekeeping confirmed

- `settings.json` stays **v67** — no settings key touched, no version bump.
- No `DeribitIndicatorProject.md` §15 row, no `history-archive.md` move.
- Full solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck build **0/0**; harness **ALL PASS** (A57b removed, A57e added — net fixture count unchanged); `verify-gate.ps1 -Mode prepush` **GATE PASSED** pre-commit (the `version-bump` section correctly reads `no engine-path change` before commit — it diffs `origin/master...HEAD`, not the working tree, same as `613cf1e`'s pre-commit run).
- `613cf1e` was pushed to `origin/master` before this follow-up began, per the review's instruction to keep it a separate commit.
