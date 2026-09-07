# `S2-2` — `CalcSpread` split: batch summary (outcome record)

**Built:** 2026-09-06 (UTC). **Base commit:** `f6a2c08`. **Build commit:** `57b55f9`. **Committed locally, NOT pushed** — the trader pushes after testing.
**Spec:** [`s2-2-calcspread-split-proposal.md`](s2-2-calcspread-split-proposal.md), built from its **§4b** (not its §4).
**Review packet (the working document):** [`s2-2-calcspread-split-spec-back.md`](s2-2-calcspread-split-spec-back.md).

⚠ **All dates in this document are UTC.** The workstation is GMT+8; `date -u` was run and read **2026-09-06 16:43 UTC** while the workstation clock showed 2026-09-07.

---

## 0. Findings that change how the rest of this document reads

⭐ **The spec's §3 arithmetic was CONFIRMED BY MEASUREMENT before any code moved, not accepted on the page.** The parity instrument was built and run at base commit `f6a2c08` against the *shipped* `CalcSpread`, and the two zeros are visibly different in that output:

| Book | Shipped `SpreadBps` | Shipped `SpreadStatus` | Card colour token |
|---|---:|---|---|
| **LOCKED** — `bestBid = bestAsk = 100000` | `0.00` | **`TIGHT`** | `Theme.ACC_STRONG_LONG` |
| **DEGENERATE** — empty ask ladder | `0.00` | **`NORMAL`** | `Theme.FG_TERTIARY` |
| **DEGENERATE** — `bestBid = 0.0` | `0.00` | **`NORMAL`** | `Theme.FG_TERTIARY` |

**That table is the whole build's justification and it is now evidence rather than argument.** A naive split collapses rows 1–3 onto `TIGHT`.

⭐ **Nothing in the spec was found wrong.** Its §4b line-range self-correction (`:571`, not `:576`) was checked by re-reading `:568-610` and is right; the range starts at the `''' <summary>` line.

⚠ **One thing in the spec was narrower than the build needed** — §4b.3 says "the stale-prose comment goes with it" and shows the comment at `:132-134`. **There were two MORE**, at `LiveMicrostructureEvaluator.vb:12` and `:93`, both naming `CalcSpread` as a reused function. Both were corrected. Detail in the spec-back §3.

---

## 1. What was built — the six §4b items

| # | Item | Status | Where |
|---|---|---|---|
| **4b.1** | Delete `CalcSpread`; add `CalcSpreadBps` + `ClassifySpread` | ✅ | `Core/Indicators_OrderFlow.vb:571-626` |
| **4b.2** | Move production call site 1 | ✅ | `UI/MainForm_Analysis.vb:422-429` |
| **4b.3** | Move production call site 2, drop the throwaway vars | ✅ | `LiveMicrostructureEvaluator.vb:132-141` |
| **4b.4** | Fixtures `A65a`–`A65d` | ✅ | `verify/ordercheck/Program.vb` (registered `:600-605`) |
| **4b.5** | `DeribitIndicatorProject.md` §15 — ONE row | ✅ | one row, cap checked first (§4 below) |
| **4b.6** | `trader-tick-queue.md` — close in BOTH places | ✅ | §0a owed table **and** state banner **and** the §2 row |

**§4b.7's out-of-scope list was respected in full:** `settings.json` untouched · `Core/IndicatorResults.vb` untouched · `tools/BacktestRunner/ReplayLoop.vb:466` untouched · `HasTopOfBook` untouched · the 5.0 / 1.5 values untouched.

---

## 2. Acceptance — §7, item by item

| § | Requirement | Result |
|---|---|---|
| 7.1 | Six Release builds, each run separately | ✅ **0 Warning / 0 Error, all six** — solution · AutoTweaker · WhatIfRunner · CeilingAudit · BacktestRunner · OrderCheck |
| 7.2 | Harness `ALL PASS`, **328 → 332**, `A1`–`A64b` unregressed | ✅ **332 PASS / 0 FAIL / `ALL PASS`** |
| 7.3 | `verify-gate.ps1` GATE PASSED | ✅ **`GATE PASSED`**, `-Mode local-fast` |
| 7.4 | ⛔ Display-string parity **proved, not asserted** | ✅ **byte-identical, same MD5**, over eight book shapes — §3 below |
| 7.5 | `settings.json` unchanged, still v68 | ✅ **`git diff -- settings.json` = 0 lines**; line 2 reads `"version": 68` |
| 7.6 | `DeribitIndicatorProject.md` §15 one row, cap checked first | ✅ §4 below |
| 7.7 | Commit message states the parity position explicitly | ✅ verbatim sentence in `57b55f9` |

### The 328 baseline was RE-MEASURED, not inherited

⛔ **The spec pinned its 328 to commit `2e8bc76`, and explicitly said to re-measure at the commit the build actually starts from. That was done.** At base commit `f6a2c08`, `dotnet OrderCheck.dll` printed:

```
PASS count = 328
FAIL count = 0
ALL PASS
```

⛔ **It was NOT counted from source.** The spec warns that anchored `grep -c '^\s*Check('` prints 333 and the unanchored form 342, and neither is the harness count. The harness was run.

---

## 3. The display-string parity proof

**This is the acceptance item that is not a formality, so the method is recorded in full.**

### 3.1 The instrument

A temporary console project (scratchpad only, never committed) linking the **real shipped sources** — `Core/Indicators_OrderFlow.vb`, `Core/IndicatorResults.vb`, `Core/Settings/EngineSettings.vb`, `DeribitClient.vb` for `OrderBookSnapshot`. It carries:

- `ComputeSpreadFields` — **a verbatim copy of the production call site body** at `UI/MainForm_Analysis.vb`. Pre-run it held the `CalcSpread` call; post-run, the new pair.
- `ComputeLiveStripFields` — the same for `LiveMicrostructureEvaluator.vb`.
- The four rendered surfaces, **transcribed from their source sites**: the breakdown note (`ScoringEngine_Calculate_Scoring.vb:853`), the snapshot line (`MainForm_PlaintextSnapshot.vb:450`), the card MiniMeter incl. `ResolveSpreadColour` (`MainForm_Render_Cards.vb:2069-2079`, `:2120-2126`), and the card breakdown row (`:3246-3255`).

⚠ **Colours are printed as Theme TOKEN NAMES, not `Color` structs** — the UI files need WinForms and cannot be linked into a host-agnostic project. **That transcription is why the proof has a second half.**

### 3.2 The two halves, both RUN

1. **Inputs and rendered strings are identical.** Pre-change and post-change captures of all eight shapes:
   - `diff` → **zero differing lines**
   - `3706` bytes both sides
   - MD5 **`fbcafa201a26d45e8c8d72ab5923183a`** both sides
2. **The render code is untouched.** `git diff --name-only` for `57b55f9` contains six files and **none of them is a render surface** — `MainForm_Render_Cards.vb`, `MainForm_PlaintextSnapshot.vb` and `ScoringEngine_Calculate_Scoring.vb` are all absent.

**Together those two make the claim complete:** identical inputs into unchanged renderers. Neither half alone would do — half 1 depends on a transcription, and half 2 says nothing about values.

### 3.3 The eight shapes (the spec asked for four; four degenerate sub-shapes were added)

| # | Shape | `bps` | `SpreadStatus` | Card colour |
|---|---|---:|---|---|
| 1 | WIDE — bid 99950 / ask 100050 | `10.00` | `WIDE` | `Theme.ACC_SHORT` |
| 2 | TIGHT — bid 99999.5 / ask 100000.5 | `0.10` | `TIGHT` | `Theme.ACC_STRONG_LONG` |
| 3 | **TIGHT-LOCKED** — `bestBid = bestAsk` | `0.00` | `TIGHT` | `Theme.ACC_STRONG_LONG` |
| 4 | NORMAL — bid 99985 / ask 100015 | `3.00` | `NORMAL` | `Theme.FG_TERTIARY` |
| 5 | ⛔ DEGENERATE — empty ask ladder | `0.00` | `NORMAL` | `Theme.FG_TERTIARY` |
| 6 | ⛔ DEGENERATE — `bestBid = 0.0` | `0.00` | `NORMAL` | `Theme.FG_TERTIARY` |
| 7 | ⛔ DEGENERATE — empty bid ladder | `0.00` | `NORMAL` | `Theme.FG_TERTIARY` |
| 8 | ⛔ DEGENERATE — `orderBook Is Nothing` | `0.00` | `NORMAL` | `Theme.FG_TERTIARY` |

⭐ **Rows 3 and 5–7 are the pair the spec is about**, and the capture shows them resolving differently on both sides of the change. **Rows 1, 2 and 4 would be identical under the defective implementation too** — they carry no information about this build and are included only because §7.4 names them.

⚠ **Shape 8 is included for completeness but is UNREACHABLE at `UI/MainForm_Analysis.vb:422`** — `:139` skips the whole run on a `Nothing` book. It is not what `A65a` tests, per §6's explicit instruction.

---

## 4. The §15 retention cap — checked by counting, before writing

**§15 held 14 rows.** Five settings versions — **v68** (2026-08-21) · **v67** (2026-08-20) · **v66** (2026-08-11) · **v65** (2026-08-02) · **v64** (2026-07-31) — sit **exactly at the cap**. The oldest kept version is v64 at **2026-07-31**, and all nine settings-untouched rows are newer than that (2026-08-05 through 2026-09-05). **Nothing was old enough to archive.**

The new row is **settings-untouched, dated 2026-09-06**, so it satisfies the "newer than the oldest kept version" half. **It is ONE row for one queue item**, per §15's own one-item-one-row rule. Table is now 15 rows.

---

## 5. Fixtures — `A65a`–`A65d`, and the three mutations

**Zero existing fixtures broke** — no `.vb` fixture ever called `CalcSpread`; fixtures needing a spread state set `r.SpreadStatus` directly (`Program.vb:1007`, `:7316`, `:7337`). **That is also the risk the spec names: there was no coverage of this method at all, so these four are the only guard.**

| Fixture | Pins | Provenance of its literals |
|---|---|---|
| `A65a` | Degenerate ⇒ `CalcSpreadBps` `Nothing` **and** `ClassifySpread` `"NORMAL"` | thresholds **cfg-derived** |
| `A65b` | Locked book ⇒ real `0.0` and **`TIGHT`** | thresholds **cfg-derived** |
| `A65c` | The `>= wide` arm runs **first** | ⚠ **MECHANISM** — `wide:=1.0` / `tight:=2.0`, declared at the call site |
| `A65d` | Boundary inclusivity on both arms | **SHIPPED BEHAVIOUR** — every value cfg-**derived**, no literals |

⭐ **`A65a` builds its book by hand.** `MakeBook` always fills five levels a side and **cannot** express an empty ladder. `A65a` also covers **two different guards** — the empty-ladder one and `bestBid <= 0` — so a partial conversion that fixes one and misses the other is still caught.

### The mutations — RUN, not reasoned

| Mutation | Required by §6 | Observed |
|---|---|---|
| Empty-ladder guard returns `0.0` instead of `Nothing` | `A65a` FAIL, other three PASS | ✅ **331 PASS / 1 FAIL.** `A65a` failed with the literal defect signature: `emptyAsks: hasValue=True status=TIGHT`. `A65b`/`A65c`/`A65d` passed |
| `ClassifySpread` returns `"NORMAL"` for `HasValue` too | `A65b` FAIL | ✅ **329 PASS / 3 FAIL.** `A65b` failed — **and `A65a` still PASSED**, which is precisely §6's point about the pair. `A65c`/`A65d` also failed (§6 requires only `A65b`) |
| The two `If` arms swapped | `A65c` FAIL, other three PASS | ✅ **331 PASS / 1 FAIL.** `A65c` failed (`got TIGHT, expected WIDE`); `A65a`/`A65b`/`A65d` all passed |

⭐ **The second mutation is the one worth keeping.** It demonstrates, by running, that `A65a` alone is trivially satisfiable — which is exactly the failure shape `A62f` and `A63a` had earlier in this arc. The source file was restored from a byte-level backup after each mutation and the restore verified (`grep -c MUTATION` = 0) before the next run.

---

## 6. Escalation triggers — none fired

| Trigger | Status |
|---|---|
| `Core/IndicatorResults.vb` `SpreadBps`/`SpreadStatus` field types need changing | ❌ not tripped — the file is absent from the diff |
| A rendered string differs from shipped on **any** input | ❌ not tripped — proved byte-identical over eight shapes |
| More than the **two** production call sites need editing | ❌ not tripped — exactly two |

---

## 7. Verification handles (the one-line versions; ranked set is in the spec-back)

| Handle | Command | Printed |
|---|---|---|
| Harness | run `OrderCheck.dll`, `grep -c "^PASS "` | **332**, `ALL PASS`, `FAIL` = **0** |
| The method is gone | `CalcSpread(` refs, minus `CalcSpreadBps(`, minus comment lines | **0**; `Sub CalcSpread(` = **0** |
| All five guards | `Return Nothing` inside `CalcSpreadBps` | **5**; `Return 0` = **0** |
| Threshold-free evaluator | `grep -c "Indicators\.Spread" LiveMicrostructureEvaluator.vb` | **0** (base commit: **2**) |
| Render files untouched | `git diff --name-only` ∩ the three render files | **0** |
| Settings frozen | `git diff -- settings.json \| wc -l` | **0**; line 2 = `"version": 68` |
| Parity | `md5sum` pre vs post | **equal** |

---

## 8. What is now open

⚠ **One named residual, live by ruling and not by oversight.** `D-3` was ruled **(b)**: `HasTopOfBook` (`LiveMicrostructureEvaluator.vb:303-307`, **3 conditions**) stays where it is, and now diverges visibly from `CalcSpreadBps` (**6 conditions**). **The parity capture shows the divergence concretely** — on the zero-priced-bid shape, `snap.HasSpread = True` sits beside a `Nothing` bps.

**It is a one-line change with a real if small live-strip effect, and it deserves its own line in the record rather than a quiet tidy-up inside a refactor billed as zero-change.** Recorded in `trader-tick-queue.md`'s state banner and §0a.

⭐ **RULED (a) at review, 2026-09-06 — SCHEDULE it, low priority, NOT bundled.** Queued as **`D3-RESIDUAL`** in [`trader-tick-queue.md`](trader-tick-queue.md) §2; **the trader picks the slot.** ⛔ **The justification is stronger than the one given above, and it is `UI/MainForm_LiveStrip.vb:221`:**

```vb
parts.Add(If(s.HasSpread, s.SpreadBps.ToString("0.0") & " bps", "-- bps"))
```

**`HasSpread` gates exactly whether a NUMBER or `-- bps` prints.** So on a zero-priced top of book the strip renders a **fabricated `0.0 bps`** where `-- bps` is the honest output. ⛔ **That is the same fabricated-measurement class `S2-2` existed to remove — cleared from four surfaces and left standing on a fifth.** That makes it consistency, not tidy-up. It stays **low** because a zero-priced top of book on Deribit BTC-PERP is a defensive guard, not a market state.

---

## 8a. ⛔ `R-2` — the fallback constants have no guard, and that is EXPLICITLY ACCEPTED AS UNCOVERABLE

**Raised at review 2026-09-06. Re-measured here rather than transcribed.** ⚠ **Both lines are CORRECT. This is the ABSENCE OF A GUARD, not a bug — recorded so the next reader does not read it as an oversight.**

Deleting the parity instrument (see `R-1`) removed the only thing that ever exercised the `Nothing` arm of the two call-site compositions:

| Line | The unguarded expression | Why it cannot be covered |
|---|---|---|
| `UI/MainForm_Analysis.vb:423` | `r.SpreadBps = If(spreadBps.HasValue, spreadBps.Value, 0.0)` | ⛔ **STRUCTURALLY UNCOVERABLE.** `verify/ordercheck/OrderCheck.vbproj` **cannot link `UI/`** — it needs WinForms, and the harness is host-agnostic by design. **Measured: the `.vbproj` matches `LiveMicrostructureEvaluator.vb` `1` time and `MainForm_Analysis.vb` `0` times; `UI/` files linked = `0`.** Swap the `0.0` for `-1.0` and the harness still prints **332** |
| `LiveMicrostructureEvaluator.vb:141` | `snap.SpreadBps = If(bps.HasValue, bps.Value, 0.0)` | ⚠ **Coverable in principle; the FALLBACK ARM is uncovered in fact** |

⭐ **A correction to the finding as raised, in the build's favour.** The review recorded `:141` as simply *"uncovered"*. **Measured, it is half-covered and the half matters:** `A19a` drives the evaluator with a healthy book and **asserts `snap.SpreadBps ≈ 2.0`** (`Program.vb:2245-2251`), so the **value arm** of `:141` is genuinely guarded. What is unguarded is the **`, 0.0` fallback**.

⛔ **And it cannot currently be reached: the harness contains exactly ONE `UpdateBook` call** — `Program.vb:2244`, `MakeBook(99990, 100010, 10, 1)`, a healthy book. **Every other `Evaluate` call runs against a `MarketState` with no book at all**, so `GetBook()` returns `Nothing`, the whole `If book IsNot Nothing` block is skipped, and `A19e` asserts `Not snap.HasSpread` off the field default rather than off the composition. **No fixture anywhere puts a degenerate-but-non-`Nothing` book into a `MarketState`.**

⛔⛔ **DO NOT BUILD A PARITY INSTRUMENT NOW TO CLOSE THIS.** Ruled at review: **building tooling for a single use is how the F3 watch outlived its own instrument** ([`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §12, *"the F3 watch outlived its instrument"*). The right time is when something else needs the same rig.

⭐ **`D3-RESIDUAL` closes half of this for free.** Its fix is `snap.HasSpread = bps.HasValue`, which forces a fixture driving the evaluator with a degenerate book — and that fixture puts `:141`'s fallback arm under coverage as a side effect. **`UI/MainForm_Analysis.vb:423` stays uncoverable regardless**, because the linkage boundary is architectural (the Linux-port host-agnostic constraint) and is not worth breaking for one constant.

---

Everything else the spec's §8 listed as out of scope stays out of scope and untouched.
