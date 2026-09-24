# Spec-back — engine-fix Session A (`D-1` POC-tier gate, `D-2` manual lines)

**Written:** 2026-09-24 (UTC) by Seat 2, Opus, high. **Spec:** [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §3. **Outcome record:** [`docs/engine-fix-session-a-batch-summary.md`](engine-fix-session-a-batch-summary.md). **Build commit:** `a6b33fe`. **Base commit:** `a3c9079` (engine code identical at `7711e74`, a concurrent docs-only commit).

**Legend — IDs in this packet:**

| ID | Source and kind | Meaning |
|---|---|---|
| `H-n` | This packet, handle | A check the reader can run; output pasted from this seat's run |
| `E-n` | This packet, evidence | Build-time evidence the reader cannot re-run as committed |
| bug-hunt `H-3` | `docs/medium-tier-bug-hunt-spec-back.md` §1, handle | The `--mode pocgate` counterfactual read. ⚠ **Not the same as this packet's `H-3`** |
| `D-1`, `D-2` | `docs/medium-tier-bug-hunt-spec-back.md` §3, trader rulings | `D-1` = fix the POC-tier gate; `D-2` = correct the manual's `NEAR_HVN_RESIST` line |
| `EFT-1`, `EFT-2`, `EFT-3` | `docs/engine-fix-build-spec-2026-09-21.md` §7, traps | Four-way swap · fixing one gate copy only · a hand-set label the producer never emits |
| `EF-1` | `docs/engine-fix-build-spec-2026-09-21.md` §6, trader ruling | One combined deploy |
| `A80a`–`A80c`, `A83a`, `A83b`, `A26b`, `A82b`, `A82c` | `verify/ordercheck/Program.vb`, harness fixtures | See `docs/engine-fix-session-a-batch-summary.md` §3 |

---

## 1. Ranked verification handles

**If you only run one, run `H-1`.** It covers the fix, both gate copies, both halves of each gate, and the old `SKIP` line.

### H-1 — the harness, default mode (pinned to `a6b33fe`)

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

```
435 lines start with PASS
SKIP  A81b known-defect repro (CalcLiquidations books a maker-side liquidation on the taker's side) — set ORDERCHECK_KNOWN_DEFECTS=1 to run; docs/medium-tier-bug-hunt-2026-09-16.md
ALL PASS
PASS  A80b long: the POC is the only structure on the target side (100020.0 vs entry 100005.0, label NEAR_HVN_SUPPORT) → the HVN-gated POC tier places at it, as 721882e specifies ('wall above price')
PASS  A80b short: the POC is the only structure on the target side (100020.0 vs entry 100035.0, label NEAR_HVN_RESIST) → the HVN-gated POC tier places at it, as 721882e specifies ('floor below price')
PASS  A80c long (legacy enabled:false twin): label NEAR_HVN_SUPPORT, POC 100020.0 vs entry 100005.0 → the legacy tier-3 cap places at the POC
PASS  A80c short (legacy enabled:false twin): label NEAR_HVN_RESIST, POC 100020.0 vs entry 100035.0 → the legacy tier-3 cap places at the POC
PASS  A83a long: label IN_LVN_BEAR (producer-built), POC 100020.0 vs entry 99740.0 → the POC tier opens and places at the POC
PASS  A83a short: label IN_LVN_BULL (producer-built), POC 100020.0 vs entry 100300.0 → the POC tier opens and places at the POC
PASS  A83b long (legacy enabled:false twin): label IN_LVN_BEAR, POC 100020.0 vs entry 99740.0 → the legacy tier-3 cap places at the POC
PASS  A83b short (legacy enabled:false twin): label IN_LVN_BULL, POC 100020.0 vs entry 100300.0 → the legacy tier-3 cap places at the POC
```

- **Load-bearing values:** 435 PASS; the only `SKIP` line is `A81b`; all eight `A80b`/`A80c`/`A83a`/`A83b` gate lines PASS.
- **Arithmetic identity:** 425 baseline + 2 (`A80b`, was `SKIP`) + 2 (`A80c`) + 4 (`A83a`) + 2 (`A83b`) = 435.

### H-2 — the fix equals the counterfactual (pinned to `a6b33fe`)

```
dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode pocgate --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
```

```
- Verified rows with logged placed levels: 38665. ComputeSideLevels as shipped reproduces all four logged Placed* values: 37374 (96.66 %).
- Rows where the counterfactual gate moved a STOP (must be 0; the gate reads targets only): 0.
- Verified rows: 38665. Rows where the long or the short placed target moves: 1288 (3.33 %). These values reach the CSV Placed* columns and the bridge payload levels.
```

- **Load-bearing value:** 37,374. At the base commit the same line read 38,662 (`H-3` below). **38,662 − 1,288 = 37,374**: the fixed code departs from the logged history on exactly the 1,288 rows the counterfactual predicted, and on no others.
- ⚠ After the fix, this mode's labels read backwards — see §3, queued decision `Q-1`.

### H-3 — bug-hunt `H-3` re-run at the base commit (pinned to `a3c9079`)

Same two commands as `H-2`, run before any edit. Run at `2026-09-24 13:29:46` UTC. Key lines:

```
| ALL | population rows | 8810 | 8508 | 96.6 |
| ALL | all kinds | 39594 | 38665 | 97.7 |
- Verified rows with logged placed levels: 38665. ComputeSideLevels as shipped reproduces all four logged Placed* values: 38662 (99.99 %).
- Rows where the counterfactual gate moved a STOP (must be 0; the gate reads targets only): 0.
- Logged TargetCapReason = poc in the whole swing read population (all months, verified or not): 0 of 8810.
| ALL | STRONG | 496 | 5 | 5 | 5 | 5 (1.01 %) |
| ALL | MEDIUM | 2351 | 34 | 34 | 34 | 34 (1.45 %) |
| ALL | WEAK | 5661 | 104 | 104 | 104 | 104 (1.84 %) |
| ALL | ALL | 8508 | 143 | 143 | 143 | 143 (1.68 %) |
| ALL | ALL | 8233 | 144 | 0 |
- Verified rows: 38665. Rows where the long or the short placed target moves: 1288 (3.33 %). These values reach the CSV Placed* columns and the bridge payload levels.
```

- **Matches the escalation numbers exactly:** 143 of 8,508 and 1,288 of 38,665. **The Session A escalation trigger did not fire.**
- **Arithmetic identity:** 5 + 34 + 104 = 143; 496 + 2,351 + 5,661 = 8,508.
- To re-run at the base commit without disturbing a working tree: `git worktree add ../base a3c9079`, then run the two commands with `--root` pointing at the repo that holds `aws_fetch/` and `AWS-copybacks/` (both are gitignored, so they are not in the worktree).

### H-4 — only the two `NEAR_HVN_*` literals moved, in both copies

```
git show a6b33fe -U0 -- Core | grep '^[-+].*VPFRSignal ='
```

```
-            Dim hvnAbove As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR")
-            Dim hvnBelow As Boolean = (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL")
+            Dim hvnAbove As Boolean = (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BEAR")
+            Dim hvnBelow As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BULL")
-            (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR"),
-            (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL"))
+            (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BEAR"),
+            (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BULL"))
```

- **Load-bearing:** `IN_LVN_BEAR` stays on the long side and `IN_LVN_BULL` on the short side, in both copies (trap `EFT-1`).

### H-5 — known-defects mode leaves only `A81b` red

```
ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

```
435 lines start with PASS
FAIL  A81b maker-side liquidation (flag M, taker buy): the liquidated maker held a LONG → LONG LIQS, per Deribit's `liquidation` field — KNOWN DEFECT: got signal=SHORT LIQS long=0 short=1000. Core...
FAIL  A81b maker-side liquidation (flag M, taker sell): the liquidated maker held a SHORT → SHORT LIQS, per Deribit's `liquidation` field — KNOWN DEFECT: got signal=LONG LIQS long=1000 short=0. Co...
2 FAILURE(S)
```

### H-6 — the three mutations (recipe; the reader edits two files and re-runs `H-1`)

| Mutation (edit, run `H-1`, then reverse with the inverse edit) | FAIL lines this seat saw |
|---|---|
| Four-way swap: move `IN_LVN_BEAR` ↔ `IN_LVN_BULL` in both copies as well | `A83a` long, `A83a` short, `A83b` long, `A83b` short — `4 FAILURE(S)` |
| Revert only the legacy twin to the old literals | `A80c` long, `A80c` short — `2 FAILURE(S)` |
| Revert only `Core/SignalEmitter.vb` to the old literals | `A26b` POC sub-case, `A80b` long, `A80b` short, `A82c` (`not reached: target:POC`) — `4 FAILURE(S)` |

- **Load-bearing:** row 1. Every fixture that existed before this build passes a four-way swap; only the new `A83` fixtures catch it.
- ⚠ The mutations were made with `Edit` on the working tree and reversed with the inverse `Edit`, never `git checkout`. `H-4` shows the committed state.

### H-7 — the pre-push gate (pinned to `a6b33fe`; it builds and runs but does not push)

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/verify-gate.ps1 -Mode prepush
```

```
ALL PASS
OK    harness ALL PASS
OK    no snapshot/card drift detected
OK    engine path changed but [no-engine-change] token present
OK    AnalysisLogger.vb not in the changed set - no header rotation possible
GATE PASSED
```

- ⚠ The `version-bump` line is OK only because earlier unpushed commits in the `origin/master..HEAD` range carry the token. `a6b33fe` does not carry it, correctly: it is an engine change with no settings key.

### E-1 — render-surface reading (by eye; nothing to execute)

- Text surface: `UI/MainForm_PlaintextSnapshot.vb` `AppendPlacedAtrRow` prints `lv.Reason` when `lv.Capped`, else `lv.TargetReason`. No label list.
- Card: `UI/MainForm_Render_Cards.vb` `BindAtrRow` reads `lv.Capped`, `ExtractCapLabel(lv.Reason)` (the text between the last parentheses) and `lv.TargetReason`. No label list.
- The v30 sub-tick suppression lives in `SignalEmitter.ComputeStructuralSideLevels` (`Math.Max(TickSize, r.ATR * 0.02)`, `TickSize` = 0.5) and is unchanged.
- **So `PLACED @ <price> (POC)` renders on both surfaces with no edit.** `H-7`'s display-parity line agrees, but that heuristic checks file co-change, not semantics.

---

## 2. Decisions taken (auto-proceeded, one line each)

- **Fixture ids:** options were the spec's suggested `A83a`, or another free id → took `A80c`, `A83a`, `A83b`; checked free in `verify/`, in every `docs/*.md`, and on every branch (`git log --all -S'"A83'`).
- **Legacy-twin fixtures `A80c` and `A83b`:** options were (a) live gate only, as the spec's §3.3 table lists, or (b) both copies → **(b)**. It records more and guards trap `EFT-2`, which had no fixture. Not a cheaper-and-less-truthful pick.
- **`A83a` clears the two nearest-HVN fields before the gate check:** options were (a) clear them (MECHANISM), or (b) keep the producer state → **(a)**. On any producer state with an `IN_LVN_*` label the HVN tier places first at the POC price, so (b) cannot reach the gate at all. Step-3 mechanism argument: (b) is not richer, it is unable to test the property. `A83a` also pins the masking itself.
- **`A26b` label source:** options were (a) derive through `CalcVPFRLite`, or (b) an in-fixture geometry assertion → **both**. The helper `A26bProducerLabelPocAbove` returns the producer's label, and the check also asserts the sub-case's POC sits above its entry.
- **`A82b` site "POC tier gate label":** not named by the spec. It hand-set `NEAR_HVN_RESIST` with the POC above price — the `EFT-3` shape in a second fixture. Options were (a) leave it, or (b) set `NEAR_HVN_SUPPORT` → **(b)**. Side effect: `A82c` now also fails on the old gate (`H-6` row 3).
- **`docs/UserManual.md` step 5b line (the `D-1` (c) row's line):** rewritten to the fixed gate, as the spec pre-authorised. Also names the `IN_LVN_*` masking.
- **`docs/UserManual.md` `IN_LVN_BEAR` row (`CurrentPrice < POC`; the code uses `<=`):** left alone. The difference cannot occur, because price equal to the POC always sets `HVNNearPoc`. Out of scope.
- **Commit message of `a6b33fe`:** amended once, before any push, to correct a binding name I had written wrong (`BindCardAtrRow` → `BindAtrRow`). Content unchanged.
- **§15 row:** one row for Session A, under the one-item-one-row rule; summary cell 898 characters.

## 3. Decisions queued, with my read

### `Q-1` — `tools/ops/SwingFallbackRead --mode pocgate` now reads backwards

✅ **RULED 2026-09-24 (UTC), trader: YES, add the note.** Done by the orchestrator: a header note in `tools/ops/SwingFallbackRead/PocGateDefect.vb` and a one-line note in the mode's output header. No change to the method.

- **Fact:** the mode swaps the `NEAR_HVN_*` labels and calls the shipped `ComputeSideLevels`. It calls the swapped result "the gate its spec describes". After `a6b33fe` the shipped gate IS the spec's gate, so the swap now reproduces the OLD gate. The report's headings are now wrong, and its flip tables read 0 (`H-2`).
- **Options:** (a) leave the tool; the 2026-09-16 output doc stays the record · (b) add a one-line header note in the mode's output · (c) invert the mode so it compares the fixed gate against the pre-fix gate explicitly.
- **My read (hypothesis): (b).** Tools-only, reversible, no data effect, so auto-proceed class — but it is outside Session A's four-file touch list, so I did not do it. It is also the instrument behind `H-2` and bug-hunt `H-3`, so a wrong heading could mislead a later reviewer.

### Finding (no decision needed) — the `IN_LVN_*` half of the POC tier never places on a producer state

- When the label is `IN_LVN_*`, price sits outside the POC bucket. `CalcVPFRLite` then reports the nearest HVN on the POC's side at or before the POC bucket. The HVN tier is tried first (mode 0) or ties and wins on strict `<` (mode 1). The legacy tier 3 also needs the POC strictly closer than the HVN cap.
- **Effect:** where the POC is the nearest HVN, the placed PRICE is the same; only the label reads `NEAREST_HVN_*` instead of `POC`. A reader who counts `TargetCapReason = poc` rows will under-count by design.
- **My read:** no action. Recorded in `docs/UserManual.md` and in the `A83` fixture header. ⚠ Derived by reading the code and shown on one profile by `A83a`; not measured on the book.

### Queue rows that should close — for the orchestrator (I did not edit `docs/trader-tick-queue.md`)

| `docs/trader-tick-queue.md` §2 row | Action |
|---|---|
| "⛔ POC-tier gate reads the VPFR labels inverted — the POC target tier never places" | Mark **BUILT at `a6b33fe`, not deployed**. It rides the `EF-1` (a) combined deploy. Keep the deploy half open |

## 4. Feedback on the spec

### What worked

- **The `EFT-1` warning was exactly right, and measurable.** `H-6` row 1 proves it: before `A83`, a four-way swap passed all 425 fixtures.
- **Naming the escalation numbers in advance** made `H-3` a pass/fail check, not a judgment.
- **"Anchor on the symbol, not the number"** (`docs/engine-fix-build-spec-2026-09-21.md` §10). The dispatcher had moved from lines 737-750; the `A80b` body from 15658 to 16135.

### What broke or was incomplete

- ⚠ **The spec's fixture plan left `EFT-2` without a guard.** It names the trap and lists no fixture for the legacy twin. `A80c` and `A83b` fill it.
- ⚠ **`EFT-3` had a second instance the spec did not list:** the `A82b` "POC tier gate label" site. It was also the only site that reached `target:POC` for `A82c` under the old gate.
- ⚠ **The spec's `A83a` recipe cannot work as written.** "Build `r` through `CalcVPFRLite` on an LVN profile; assert `IN_LVN_BEAR` opens the LONG POC tier" — on a real LVN state the HVN tier places first, so the POC tier is unobservable. The fixture must clear the nearest-HVN fields. That constraint is general, and it is a finding in its own right (§3).
- **The baseline was stale** (409 in the spec; 425 measured). The orchestrator's hand-off flagged it; the spec's "413 or more" gate became 425 + 10 = 435.

## 5. What I did not verify

- **The live effect.** Nothing runs on the collector until the deploy. The 1,288 / 143 figures are a counterfactual on the pooled book to 2026-09-09, not a forward measurement.
- **The card on screen.** No app run, no screenshot. The render claim rests on reading two binders (`E-1`) and on the parity heuristic in `H-7`.
- **Mode 1 (`target_arbitration_mode` = 1) with the fixed gate.** The shipped value is 0. `A82b` runs a mode-1 cfg and passes, but no fixture pins a POC placement in mode 1.
- **The `IN_LVN_*` masking on the book.** Derived from the code and shown on one synthetic profile; not counted on real rows.
- **Carried without re-checking:** the D-table text and the trader rulings in `docs/medium-tier-bug-hunt-spec-back.md` §3.
