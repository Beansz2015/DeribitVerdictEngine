# Engine fix build spec — the six rulings from the MEDIUM-tier bug hunt

**Written:** 2026-09-21 (UTC), `date -u` = `Mon Sep 21 08:27:41 UTC 2026`. Base commit `5e42327` (`master`, clean, 0 ahead of `origin/master`).
**Commissioned by:** [`docs/seat-handover-2026-09-17.md`](seat-handover-2026-09-17.md) §1, task 1.
**Rulings implemented:** `D-1`, `D-2`, `D-4`, `D-5`, `D-6`, `D-8`, `D-9` — all trader-ruled 2026-09-16 (UTC). Decision text lives in [`docs/medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §3, §R.3 and §R.6.3, and **that text wins over this summary**.
**Evidence:** [`docs/medium-tier-bug-hunt-2026-09-16.md`](medium-tier-bug-hunt-2026-09-16.md).

---

## 0. Model and effort — read this before anything else

| Session | Model | Effort | Why that tier |
|---|---|---|---|
| **A** — the POC-tier gate (`D-1`, `D-2`) | **Opus** | **high** | Four lines of code and two doc lines, but the build is a **live scoring-outcome change and a dataset boundary**. The judgment is done; the risk is that "swap the labels" is read too widely and the `IN_LVN_*` halves get swapped too — a half that is already correct. Two fixtures must change meaning, not just values |
| **B** — the liquidation flag (`D-4`, `D-5`, `D-6`) | **Opus** | **high** | The fix cannot be designed until a live measurement returns. The instrument is new code against a live venue feed, and the fix that follows is a scoring change on a vote that has never fired once. `D-5` carries an `MT` case with no live example anywhere |
| **C** — the report column and the card colour (`D-8`, `D-9`) | **Sonnet** | **medium** | Mechanical against settled rulings, with an in-repo template for each. ⚠ Escalate to Opus if decision `EF-4` below is ruled to change the text surface as well — that engages the display-string parity hard rule |

**Sequence by dependency, not by size.** Session A and Session C are independent of each other and of Session B. **Session B cannot finish until its measurement returns**, so start Session B's measurement FIRST and let it run while Session A is built.

### Where each model will slip — the concrete traps, not a general warning

| Trap | Session | Why a fixture will not catch it |
|---|---|---|
| Swapping all four labels instead of the two `NEAR_HVN_*` labels | A | `A80b` exercises only the `NEAR_HVN_*` path. An `IN_LVN_*` inversion passes `A80b`, passes `A80a`, and passes every other fixture in the tree |
| Rewriting `A26b` with another label the producer never emits | A | The implementer writes the fixture, so the same misunderstanding propagates into its own test. `A80a` is the only producer-built pin that exists |
| Treating an unrecognised `liquidation` value as taker-side | B | No fixture covers an unknown value, and the live stream has never delivered one to look at |
| Parsing `fundingStep3bNote`'s text inside the card | C | The note is a display string. A test asserting that the card matches the note passes while both are wrong, and breaks the day the wording changes |

### Escalation triggers — stop and move up a tier when

- **Session A:** the re-run of handle `H-3` (in [`docs/medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §1) does not reproduce 143 flipped population rows and 1,288 moved placed targets at the base commit. A different number means the counterfactual and the fix disagree, and the fix is not understood.
- **Session B:** the measurement shows the stream DOES carry a readable liquidation flag on some trades. That contradicts `L-1`'s 51,107-of-51,107 measurement, and the whole fix design changes.
- **Session C:** any change to `UI/MainForm_PlaintextSnapshot.vb` becomes necessary. That is a text-surface change under the display-string parity hard rule, and it is a rendered value, so it is reserved.

---

## 1. Scope

### In scope

| Ruling | What ships | Class |
|---|---|---|
| `D-1` | Swap the two `NEAR_HVN_*` literals in both POC-tier gate copies | ⛔ Reserved — moves placed levels and flips verdicts. **The values are already ruled** |
| `D-2` | Correct `docs/UserManual.md` line 967 to the code's geometry | Documentation |
| `D-4` | Measure the live stream first, then parse the field or enrich from REST | ⛔ Reserved — scoring change and a dataset boundary |
| `D-5` | Book `M` to the maker's side, `T` to the taker's side, `MT` to both | ⛔ Reserved — scoring. **Ships with `D-4`, never before it** |
| `D-6` | Correct the manual's `large_liq_size` unit to USD | Documentation. The VALUE re-derivation waits for `D-4` |
| `D-8` | The analysis report shows lean NO TRADE rows in a separate labelled column | ⛔ Reserved — moves a rendered report value. **Ruled (b)** |
| `D-9` | The card renders what the funding-momentum step actually did | ⛔ Reserved — rendered value. **Ruled (b)** |

### Explicitly NOT in scope

- **`D-1` option (b)** — removing the label gate so the POC places on geometry alone. The trader ruled (a). Do not re-open it.
- **`D-10` / `TOOL-1`** — the `OverlapValidator` OI labels. Orchestrator-scheduled, low priority, offline tool only.
- **Renaming the `NEAR_HVN_SUPPORT` and `NEAR_HVN_RESIST` labels.** Both misreadings trace to those names ([`docs/medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §3, the last bullet of `D-2`), but a rename is CSV-free and display-visible, so it is a separate reserved decision. Not here.
- **`Q-1` option (d)**, the target and stop geometry read by tier. That is task 3 of [`docs/seat-handover-2026-09-17.md`](seat-handover-2026-09-17.md) §1, a separate analysis.
- **The re-derivation of `large_liq_size`'s value.** It needs real liquidation sizes, which only exist after `D-4` ships and runs.
- **Any `settings.json` edit.** No ruling here adds or changes a key. Settings stays v68.

---

## 2. State verified while writing this spec

| Fact | Value | How it was checked |
|---|---|---|
| UTC now | 2026-09-21 08:27 | `date -u` |
| Base commit | `5e42327`, branch `master`, tree clean | `git status -sb` |
| Push state | 0 ahead, 0 behind `origin/master` | `git rev-list --left-right --count`. ⛔ [`docs/seat-handover-2026-09-17.md`](seat-handover-2026-09-17.md)'s "63 ahead, unpushed" is STALE — everything is pushed |
| Settings version | 68 | tracked repo-root `settings.json` line 2 |
| Harness | **409 PASS, `ALL PASS`**, 2 SKIP (`A80b`, `A81b`) | `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release` |
| Instruments for the handles | present | `tools/ops/SwingFallbackRead/` (7 files), `aws_fetch/20260913-153704`, `AWS-copybacks/pooled-book-2026-09-09` |

⛔ **The collector box still runs the 2026-09-01 build.** Nothing in this spec is live until a deploy, and the deploy is reserved.

---

## 3. Session A — the POC-tier gate (`D-1`) and its manual lines (`D-2`)

### 3.1 The defect, stated once

`CalcVPFRLite` (`Core/Indicators_Structure.vb:170-185`) emits these labels. **Read from the code while writing this spec, not carried:**

| Label | Producer condition | So the POC sits | Usable as a target for |
|---|---|---|---|
| `NEAR_HVN_SUPPORT` | `hvnNearPoc` and `currentPrice < poc` | ABOVE price | a LONG |
| `NEAR_HVN_RESIST` | `hvnNearPoc` and `currentPrice >= poc` | BELOW price | a SHORT |
| `IN_LVN_BULL` | not `hvnNearPoc`, thin bucket, `currentPrice > poc` | BELOW price | a SHORT |
| `IN_LVN_BEAR` | not `hvnNearPoc`, thin bucket, `currentPrice <= poc` | ABOVE price | a LONG |

The shipped long gate opens on `NEAR_HVN_RESIST OrElse IN_LVN_BEAR`. The shipped short gate opens on `NEAR_HVN_SUPPORT OrElse IN_LVN_BULL`.

⭐⭐ **THE `IN_LVN_*` HALVES ARE ALREADY CORRECT. ONLY THE `NEAR_HVN_*` HALVES ARE INVERTED.** The shipped long gate already carries `IN_LVN_BEAR`, which puts the POC above price — right for a long. **Swap only the two `NEAR_HVN_*` literals.** This matches the trader's ruling word for word: *"swap the NEAR_HVN labels in both gate copies"*.

⚠ **This is the trap no fixture in the tree catches.** `A80b` exercises the `NEAR_HVN_*` path only. An implementer who swaps all four gets a green `A80b`, a green `A80a`, a green harness, and a newly broken `IN_LVN_*` half.

### 3.2 The two code edits

**Edit 1 — the live path, `Core/SignalEmitter.vb:330-332`.**

Shipped:

```
Dim pocGated As Boolean = If(isLong,
    (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR"),
    (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL"))
```

After:

```
Dim pocGated As Boolean = If(isLong,
    (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BEAR"),
    (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BULL"))
```

The comment above it reads *"POC tier keeps the legacy HVN-proximity gate (VPFRSignal flags the side)"*. Replace it with a comment that states the producer geometry and cites this spec, so the next reader does not have to re-derive it.

**Edit 2 — the rollback path, `Core/ScoringEngine_Calculate_Verdict.vb:223-224`.** This is the `structural_levels.enabled:false` legacy twin.

Shipped:

```
Dim hvnAbove As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR")
Dim hvnBelow As Boolean = (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL")
```

After:

```
Dim hvnAbove As Boolean = (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BEAR")
Dim hvnBelow As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BULL")
```

⭐ **The variable names become truthful for the first time.** `hvnAbove` now means "the label says the POC is above price", which is exactly what the long tier-3 test needs. Say this in the commit message — a reviewer reading only the diff will otherwise assume the names were already right.

⚠ **`enabled:false` is not the live path**, but it is the documented rollback and it is byte-identical-to-v50 by contract. Fixing one copy and leaving the other would make the rollback change behaviour, which breaks that contract.

### 3.3 Fixture work

| Fixture | Today | Required change |
|---|---|---|
| `A80a` (`verify/ordercheck/Program.vb:15633`) | PASSes on shipped code; pins the producer geometry through the real `CalcVPFRLite` | **No change.** It is the anchor the whole fix rests on |
| `A80b` (`verify/ordercheck/Program.vb:15658`) | Known-defect repro, gated on `ORDERCHECK_KNOWN_DEFECTS=1`, FAILs on shipped code | **Flip to an always-on guard.** Delete the environment-variable gate and its `SKIP` line. Rewrite the failure message — it currently explains a known defect, and after the fix it must read as a regression report |
| `A26b` (`verify/ordercheck/Program.vb:3343`) | Hand-sets `VPFRSignal = "NEAR_HVN_RESIST"` with `VPFRPoc = 62050` above an entry of 62000 — **a state the producer never emits** | **Rewrite the POC sub-case only.** The label must be `NEAR_HVN_SUPPORT` for the long POC tier |
| NEW, suggested id `A83a` | — | **Pin the `IN_LVN_*` halves against the producer.** Build `r` through `CalcVPFRLite` on an LVN profile; assert `IN_LVN_BEAR` opens the LONG POC tier and `IN_LVN_BULL` opens the SHORT one. **This is the guard for the trap in this spec §3.1** |

⭐ **`A26b`'s rewrite must follow the lesson its own defect taught** ([`docs/medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §4): *"A fixture for any gate that consumes a label should build the label from the producer."* Either derive the label through `CalcVPFRLite` the way `A80a` does, or add an in-fixture assertion that the hand-set label agrees with the POC-versus-price geometry. A bare hand-set literal reintroduces the exact hole.

⚠ **`A26b`'s other three sub-cases are unaffected.** The HVN case, and the "no tier survives" case that sets `VPFRPoc = 62050` with a neutral signal, both stay as they are. Do not touch them.

### 3.4 Render surfaces — the display-string parity check

**Claim to verify, not to assume:** the fix adds no line, removes no line and renames no line. It only makes `TargetCapReasonLong` and `TargetCapReasonShort` carry `PLACED @ <price> (POC)` on rows that previously carried a fallback.

- Both surfaces already render `TargetCapReason*` generically — `UI/MainForm_PlaintextSnapshot.vb` for the text surface, `UI/MainForm_Render_Cards.vb` for the card.
- **Read both sites and confirm it**, then state in the commit message why no card edit is needed. That is what the display-string parity hard rule in `CLAUDE.md` requires when a rendered value moves but no surface changes shape.
- ⚠ Also check the v30 sub-tick suppression: when the fallback and placed prices differ by less than `max(0.5, ATR × 0.02)`, the label is hidden and the target renders as a plain value while the CSV still records the reason. A POC placement inside that window behaves that way. **Confirm it; do not change it.**

### 3.5 The manual — `D-2`, and the consequence of taking `D-1` (a)

| Line | Today | Required |
|---|---|---|
| `docs/UserManual.md` line 967 | *"`NEAR_HVN_RESIST` reads as price is sitting just below a high-volume node acting as resistance"* | ⛔ **Wrong, and it contradicts the same manual's own classification table at line 951**, which says `NEAR_HVN_RESIST` is `CurrentPrice >= POC` — price ABOVE the node. Rewrite to the code's geometry |
| `docs/UserManual.md` line 961 | *"when `NEAR_HVN_RESIST` or `IN_LVN_BEAR` AND `POC > CurrentPrice` … long target capped at POC"* | ⚠ **This documents the DEFECT, and it is self-contradictory** — it asks for `NEAR_HVN_RESIST` and `POC > CurrentPrice` at the same time, which the producer never emits together. Rewrite to the fixed gate |

⚠ **Line 961 is not named in the `D-2` ruling**, which covers line 967. It is named in the `D-1` option (c) row, and (c) was NOT taken. **Correcting it is a consequence of taking (a), not a new decision** — leaving it would document behaviour this build has just removed. Record it as auto-proceeded, in one line.

### 3.6 Session A acceptance

1. `dotnet build` on the solution succeeds. ⚠ A failure naming a locked `.exe` (`MSB3021` or `MSB3027`) is not a compile error — the app is running. Close it, or build `verify/ordercheck/OrderCheck.vbproj`, which type-checks the same sources.
2. The harness reads **`ALL PASS` at 413 or more**, and `A80b`'s `SKIP` line is gone. That is the 409 baseline, plus `A80b`'s two sides, plus the new `IN_LVN_*` fixture.
3. `ORDERCHECK_KNOWN_DEFECTS=1` also reads `ALL PASS` except for `A81b`, which Session B owns.
4. **Re-run handle `H-3`** from [`docs/medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §1 at the base commit and paste its output. It must still read 143 flipped population rows of 8,508 and 1,288 moved placed targets of 38,665. **A different number is the escalation trigger in this spec §0.**
5. `git diff --stat` touches only `Core/SignalEmitter.vb`, `Core/ScoringEngine_Calculate_Verdict.vb`, `verify/ordercheck/Program.vb` and `docs/UserManual.md`. **`settings.json` must not appear.**

---

## 4. Session B — the liquidation flag (`D-4`), attribution (`D-5`) and the unit (`D-6`)

### 4.1 Start here — the measurement, because the fix depends on it

The trader ruled **measure first**: capture raw `trades.BTC-PERPETUAL.100ms` messages until a liquidation passes, read-only, on the dev machine.

**Facts that shape the instrument, read from the code while writing this spec:**

- The parse at `DeribitWsFeed.vb:488` already reads `liquidation`, defaulting to `"none"`. **So the parse is not the defect.**
- The subscribed channel is `"trades." & Instrument & ".100ms"` (`DeribitWsFeed.vb:27`).
- **The REST copies ARE flagged.** `L-1` measured 91 liquidation trades held twice under the same `trade_id`: the streamed copy flagged `none`, the REST copy flagged `T` or `M`. The REST parse is `DeribitClient.vb:345`.
- `tools/WsTradeProbe/` is the right home. It is standalone by construction — links nothing from the app, reads no settings, public channels only, writes one file in its working directory — and it already subscribes the same trades channel. It is safe to run beside a live collector.

**Instrument design — do not just wait for a liquidation.**

⭐ **The REST copies give a ground truth, so the probe can PAIR rather than wait blindly.** Build three arms into one run:

| Arm | What it does | What it settles |
|---|---|---|
| **1** | Dump the **raw JSON text** of every streamed trade object, not a parsed struct, to a rolling file | What field names the `100ms` channel actually delivers |
| **2** | Poll REST `public/get_last_trades_by_instrument` over the same window. For every REST trade whose `liquidation` is not `none`, find the streamed message with the same `trade_id` and print **both raw objects side by side** | Whether that exact trade arrived on the stream with the field under another name, with no field at all, or not at all |
| **3** | Subscribe `trades.BTC-PERPETUAL.raw` in parallel and record whether ITS objects carry the field | Whether the `100ms` aggregation is what drops it |

⚠ **Arm 2 is the arm that answers `D-4`.** Arms 1 and 3 alone can only report "no liquidation seen yet", which is indistinguishable from "no liquidation happened".

**Run length:** liquidations pass roughly twice a day ([`docs/seat-handover-2026-09-17.md`](seat-handover-2026-09-17.md) §2.1). Plan for at least 24 hours, and do not stop until arm 2 has paired at least one flagged REST trade. **Two pairings are better than one** — a single trade cannot separate a channel-wide omission from a one-off.

**Safety:** public channels only, no authentication, no orders, writes only into its own working directory. ⛔ Run it on the dev machine, never on the collector box.

### 4.2 The fix, once the measurement returns

| Measurement result | Ruled action |
|---|---|
| The field is present under another name | **(a)** parse it. One line at `DeribitWsFeed.vb:488`, plus a fixture |
| The field is genuinely absent from the stream | **(b)** enrich from REST when a liquidation is suspected |
| The field IS present and readable under its own name | ⛔ **Escalation trigger.** This contradicts `L-1`. Stop and report |

⛔ **Never (c).** Retiring the vote is rejected — `docs/trader-profile.md` §3 lists Liquidations as PREFERRED.

⚠ **The cascade alarm reads the same flag** at `DeribitWsFeed.vb:506`, through `FoldAlertsTrade`. Fixing `D-4` arms it as well. That is the block on row `E7` (the A4 liquidation-by-OFI flip gate) in [`docs/trader-tick-queue.md`](trader-tick-queue.md) §1 — say so in the spec-back so the queue row can be re-opened.

### 4.3 `D-5` — maker-side attribution

**Shipped**, `Core/Indicators_OrderFlow.vb:266-272`:

```
If t.Liquidation <> "none" Then
    If t.Direction = "buy" Then
        liqShortSize += t.Amount
    Else
        liqLongSize += t.Amount
    End If
End If
```

`direction` is the TAKER's direction. Deribit's `liquidation` is `"M"` when the maker side was liquidated, `"T"` when the taker side was, `"MT"` when both.

**Required behaviour, per the ruling:**

| Flag | Taker direction | The liquidated account held | Books to |
|---|---|---|---|
| `T` | buy | a SHORT, force-bought | `liqShortSize` |
| `T` | sell | a LONG, force-sold | `liqLongSize` |
| `M` | buy | a LONG, force-sold into the taker's buy | `liqLongSize` |
| `M` | sell | a SHORT, force-bought from the taker's sell | `liqShortSize` |
| `MT` | either | both sides | **both** `liqLongSize` and `liqShortSize` |

⛔ **Write it as an explicit `Select Case` on the flag, not as the one-line boolean trick.** The `E-2` mutation recipe in [`docs/medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.1 uses `If (t.Direction = "buy") <> (t.Liquidation = "M")`. That is right for `T` and `M` and **silently wrong for `MT`**. It was a mutation probe, not a design.

⚠ **`MT` has zero occurrences in the store and no prior spec.** The ruling names it, so build it — but the implementer has no live example to check against. Say so plainly in the spec-back.

**Fixtures:** `A81a` stays as it is — it pins the `T` mapping and passes today. `A81b` flips from a known-defect repro to an always-on guard, exactly as `A80b` does in Session A. Add a new sub-case for `MT`.

### 4.4 `D-6` — the unit of `large_liq_size`

Documentation only, for now.

| Line | Today | Required |
|---|---|---|
| `docs/UserManual.md` line 1452 | `LargeLiqSize (200 BTC)` | USD |
| `docs/UserManual.md` line 1463 | *"`LargeLiqSize = 200 BTC` … At BTC $76k that's ~$15M … a genuine cascade event"* | ⛔ Wrong by orders of magnitude. The code compares 200 with a **USD sum**, and the median liquidation trade in the store is 6,000 USD |
| `docs/UserManual.md` lines 1436-1437 | *"total BTC size of long liquidations"*, and its short mirror | ⚠ **Check these two as well.** They carry the same unit claim and are NOT named in the ruling. `t.Amount` on `BTC-PERPETUAL` is USD notional |

⚠ **Lines 1436 and 1437 are a finding of this spec, not part of `D-6`.** Either correct them and record it as auto-proceeded, or leave them and name them in the spec-back. **Do not fix one unit claim and leave its neighbours standing.**

⛔ **Do NOT change the value.** The ruling says re-derive it from real liquidation sizes AFTER the fix. There are no real sizes until `D-4` ships and runs.

### 4.5 Session B acceptance

1. The probe's paired raw-JSON output is committed as a read document under `docs/`, stating the run window and the number of pairings.
2. `dotnet build` succeeds. The harness reads `ALL PASS` with `A81b` always-on and the `MT` sub-case added.
3. ⚠ **The `D-4` fix cannot be fixture-verified end to end.** `DeribitWsFeed` needs a live socket and is not linked into the harness ([`docs/medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.2). **Say this plainly rather than implying coverage.** Acceptance for the parse half is a second probe run showing a flagged trade arriving through the fixed path.
4. `settings.json` unchanged.

---

## 5. Session C — the report column (`D-8`) and the card colour (`D-9`)

### 5.1 `D-8` — the analysis report's VerdictContext outcome table

**The defect**, `analysis/AnalysisRunner.vb:274-280`. The filter excludes `r.Verdict.ToUpper() <> "NO TRADE"`, but a lean row's verdict reads `NO TRADE [WEAK LONG]` — not equal to `NO TRADE`, and it does not start with `WEAK`. So lean rows pass the filter and are counted as trades.

**Measured** by handle `H-13` over the merged book: 3,019 `NO TRADE [WEAK LONG]`, 4,117 `NO TRADE [WEAK SHORT]` and 59 `NO TRADE [TIE]` — **7,195 lean rows counted alongside 3,305 real directional rows.**

**Ruled (b):** keep the lean rows, in a separate labelled column.

| Step | Where |
|---|---|
| Split the filter — directional rows through `IsDirectionalVerdict` (`analysis/AnalysisRunner.vb:245`), lean rows through a `StartsWith("NO TRADE")` test | `analysis/AnalysisRunner.vb` |
| Carry the lean cell alongside the trade cell | `analysis/AnalysisReport.vb` |
| Render the extra column, labelled so no reader can mistake it for a trade | `analysis/MarkdownReportWriter.vb` |

⭐ **A correct precedent already exists in the same file.** `ComputeLeanContextCounts` (`analysis/AnalysisRunner.vb:329`) uses `v.StartsWith("NO TRADE")` and gets it right. Reuse that test rather than writing a third one.

⚠ **`NO TRADE [TIE]` has no lean direction.** The outcome walk derives side from `row.Verdict.ToUpper().Contains("LONG")`, so a tie currently walks as a SHORT. **Decision `EF-2` below.**

⚠ **`analysis/` is host-agnostic** under the Linux CLI port rule in `CLAUDE.md`. No `System.Windows.Forms` reference, no `Control.Invoke`, no `MainForm` coupling.

### 5.2 `D-9` — the card's funding-momentum colour

**The defect:** the card colours funding momentum by direction alone, with no reference to which side is crowded. `FALLING` always renders in the de-crowding colour, even when shorts are crowded and Step 3b applied a PENALTY.

**Three card sites read `r.FundingMomentum`. Name all three, or the fix is partial:**

| Site | What it renders |
|---|---|
| `UI/MainForm_Render_Cards.vb:2071-2074` | The "Funding Mom" MiniMeter, through `ResolveFundMomColour` and `ResolveFundMomMagnitude` |
| `UI/MainForm_Render_Cards.vb:3221-3231` | `BuildRowFundingMom`, the signal row — `RISE` / `FALL` / `FLAT` |
| `UI/MainForm_Render_Cards.vb:3566-3573` | The footer aggregate — `↑ RISING` / `↓ FALLING` / `— FLAT` |

**Ruled (b):** render Step 3b's actual effect, not a heuristic.

⛔⛔ **The blocker the ruling does not mention, found by reading the code while writing this spec.** Step 3b's own effect is **not readable from any existing field**:

- `fundLP` and `fundSP` on the `Funding (info)` breakdown item are `ls - fundBaseL` and `ss - fundBaseS`, captured before Step 3. **They are Step 3 and Step 3b COMBINED.**
- `fundingStep3bNote` (`Core/ScoringEngine_Calculate_Scoring.vb:734-762`) holds the effect, but only as display text such as `STEP3b: -2[L] crowding↑`.

⛔ **Parsing that note in the card is the defect class the `_lastTs` verification-handle ruling names** — asserting on a NAME instead of on the PROPERTY, which drifts the moment the wording changes. Do not do it.

**Required:** carry Step 3b's own signed effect on a field. Two new `VerdictResult` properties, defaulting to 0, set at the Step 3b site:

```
Public Property FundingStep3bLongPoints As Integer
Public Property FundingStep3bShortPoints As Integer
```

The card reads those. The colour then follows the SIGN of the effect on the relevant side — a penalty renders as caution, a soften renders as relief, zero renders neutral.

⚠ **Adding properties that default to 0 does not by itself alter rendered output**, so the `VerdictResult` field-default clause of the parity rule is not tripped by the addition. **The CARD change is the rendered-value change**, and that is what `D-9` already rules.

⚠ **The text surface currently prints the CONFIG, not the effect** — `UI/MainForm_PlaintextSnapshot.vb:474-480` renders `Momentum: <state> | Enabled: <yes/no> | Soften: +<n> | Amplify: -<n>`. After `D-9` (b), the two surfaces would describe the same step differently. **Decision `EF-4` below.**

### 5.3 Session C acceptance

1. `dotnet build` succeeds. The harness reads `ALL PASS`, at or above the Session A count.
2. New fixtures: one pinning `FundingStep3bLongPoints` and `FundingStep3bShortPoints` against each Step 3b arm (crowded-and-rising, crowded-and-falling, neutral-into-crowding, disabled); one pinning the report's lean column against a row set holding lean, directional and tie rows.
3. **State in the commit message which card bindings changed, and why the text surface did or did not move**, per the display-string parity hard rule.
4. `settings.json` unchanged.

---

## 6. Decisions queued for the trader

⚠ **All four are reserved.** Each is a rendered value, a deploy, or a schema question. None is auto-proceedable.

### `EF-1` — does the POC-gate fix ride the absorption S2 deploy, or take its own?

| Option | What happens | The trade |
|---|---|---|
| (a) One deploy — absorption S1 and S2, the gap-repair fixes and this build together | One era edge in `analysis_log.csv` | Fewer deploys on a 24/7 collector, but the POC boundary and the rotation boundary land at the same instant and cannot be separated later |
| (b) Two deploys — gap repair and absorption first, the engine fixes after | Two clean era edges, each attributable to one change | A second deploy, and the engine fixes land later |
| (c) Engine fixes first, absorption after | The POC boundary is clean and early | ⛔ **Pushes the gap-repair deploy past the 2026-10-01 cross-month deadline. Rejected on that ground alone** |

- **My read: (a) — and I am arguing against the standing prior, so here is the mechanism rather than a cost argument.** `RIDER-9` puts `VPFRSignal` and `VPFRPoc` into the header on the S2 rotation. **Rows written after that rotation can reconstruct the POC gate's decision per row from their own columns.** Rows written between an earlier engine-fix deploy and S2 could not. So (a) does not lose information: the new behaviour and the inputs needed to audit it arrive together.
- ⚠ **What (a) genuinely costs:** the `TargetCapReason` shift from zero POC placements to non-zero happens at the same instant as the header change, so a reader cannot attribute a change in placement mix to one or the other **without** relying on the new columns. If the trader wants that attributable independently of `RIDER-9`, (b) is right.
- **This is a deploy decision, it is reserved, and it interacts with this spec §9.**

### `EF-2` — how should the report's lean column treat `NO TRADE [TIE]`?

| Option | What changes |
|---|---|
| (a) Exclude ties from the lean outcome walk, and count them in their own tally | The 59 tie rows stop being walked as shorts, and the count stays visible |
| (b) Keep walking ties as shorts, as today | The defect survives inside the new column |
| (c) Drop ties entirely | Silent |

- **My read: (a).** A tie has no lean direction, so walking it as a short fabricates one. (c) is a silent hole, which this repo already rejects. **(a) records more than the alternatives, so no information is being traded for less work here.**

### `EF-3` — what should `CalcLiquidations` do with an unrecognised `liquidation` value?

| Option | What changes |
|---|---|
| (a) Treat it as taker-side, as today | An unknown value books silently, and wrongly |
| (b) Skip it and count it, with the count surfaced | An unknown value becomes visible rather than guessed |
| (c) Skip it silently | Silent loss |

- **My read: (b).** The `WD-SEMANTICS` ruling settled the same shape: **a counter reading 0 is the tripwire, not waste.**
- ⚠ **(b) needs somewhere to put the count.** A CSV column is a schema change and would become another rotation rider on S2; a log line is free. **I recommend the log line now, and a rider only if the count is ever non-zero.**
- ⚠ Deribit documents exactly three values today, so (b) is cheap insurance rather than a response to an observed problem.

### `EF-4` — does `D-9` (b) change the text surface as well as the card?

| Option | What changes |
|---|---|
| (a) Card and snapshot both render Step 3b's actual effect | Both surfaces say the same thing |
| (b) Card only; the snapshot keeps printing the config | Two surfaces describe one step differently |

- **My read: (a).** The snapshot's `Momentum:` row prints `Soften` and `Amplify` from `cfg` — what the step COULD do, not what it DID. That is the same defect `D-9` exists to fix, on the other surface.
- ⛔ **It is reserved twice over:** it re-formats a line emitted by `BuildPlaintextSnapshot`, which engages the display-string parity hard rule, and it moves a rendered value.
- ⚠ **If (b) is ruled**, record the divergence deliberately and in writing, the way the `MEDIUM LONG` versus `LONG` divergence was recorded — otherwise the next parity review reads it as drift.

---

## 7. Traps, collected

| Id | Trap | Session |
|---|---|---|
| `EFT-1` | Swapping all four labels. **The `IN_LVN_*` halves are already correct.** No fixture in the tree catches this | A |
| `EFT-2` | Fixing only `Core/SignalEmitter.vb` and leaving the legacy twin. The rollback path would then differ from v50, breaking its own contract | A |
| `EFT-3` | Rewriting `A26b` with another hand-set label the producer never emits | A |
| `EFT-4` | Leaving `A80b` or `A81b` behind the `ORDERCHECK_KNOWN_DEFECTS` gate after the defect is fixed. The harness would keep printing a `SKIP` line naming a defect that no longer exists | A, B |
| `EFT-5` | Using the `E-2` one-line boolean for `D-5`. It is silently wrong for `MT` | B |
| `EFT-6` | Changing `large_liq_size`'s VALUE. Only the unit is ruled now | B |
| `EFT-7` | Parsing `fundingStep3bNote`'s text inside the card | C |
| `EFT-8` | Fixing one of the three card funding-momentum sites and calling it done | C |
| `EFT-9` | Touching `settings.json`. No ruling here needs a key. Settings stays v68 | all |
| `EFT-10` | Quoting a handle without running it. ⛔ Every handle in the spec-back carries pasted output, pinned to the commit the build started from, never to `HEAD` | all |
| `EFT-11` | Line-anchored greps over VB. `^\s*Return` misses `If … Then Return`, the commonest form in this codebase. Prefer unanchored patterns and filter the noise by eye | all |

---

## 8. Dataset boundary and version history

**`D-1`, and `D-4` with `D-5`, are live scoring-outcome changes and dataset boundaries.** `D-8` and `D-9` are not — they move rendered values only.

| Obligation | Detail |
|---|---|
| `docs/DeribitIndicatorProject.md` §15 | One row per shipped item, under the one-item-one-row rule. ⚠ **Keep each cell under about 1,000 B** — the build narrative belongs in the spec-back, not the version table |
| Deploy ledger | Record the deploy instant in [`docs/aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5. **Rows either side are not comparable** for `TargetCapReason`, `PlacedTarget*`, `LiqSignal`, `LiqLongSize` and `LiqShortSize`, and for the verdict on roughly 1.68 % of population rows |
| `settings.json` | **Not bumped.** No key added or changed. Say so explicitly in every commit message |
| `docs/csv-rotation-riders.md` | **Not touched by this build** — `AnalysisLogger.Header` does not change, so the rider check in `tools/checks/verify-gate.ps1` does not fire. ⚠ Confirm by reading the diff rather than assuming |
| [`docs/trader-tick-queue.md`](trader-tick-queue.md) §2 | Close the POC-gate row and the liquidation-flag row against the tree as each ships. ⛔ Re-open row `E7` — the A4 liquidation-by-OFI gate stops being defect-blocked the moment `D-4` ships |

---

## 9. ⛔ The scheduling conflict — read before fixing a session order

**Today is 2026-09-21 (UTC). The cross-month gap-repair deadline is 2026-10-01 00:00 UTC — nine days and sixteen hours away.**

[`docs/seat-handover-2026-09-17.md`](seat-handover-2026-09-17.md) §1 sets the fallback trigger at **about 2026-09-28**: if the absorption S1 and S2 build cannot deploy by then, a separate gap-repair deploy goes to the trader.

- The gap-repair fixes are **built, reviewed, merged and pushed**. Only the deploy is missing.
- ⚠ **This engine-fix build competes with that deploy for the same days.** Session A and Session C are short. **Session B is not** — it is blocked on a live measurement that needs at least 24 hours, and possibly several days, before its fix can even be designed.
- ⭐ **Recommendation: do not let Session B gate the deploy.** Start Session B's measurement immediately and let it run unattended. Build Session A and Session C beside it. Keep the absorption S1 and S2 build and its deploy on their own track.

**This is a sequencing recommendation, not a decision taken. The deploy is reserved.**

---

## 10. What this spec did NOT verify

- **Every measured figure carried from the read** — the 143 flipped rows, the 1,288 moved targets, 0 POC placements in 38,665 rows, `LiqSignal` NONE on 51,107 of 51,107 rows, the 7,195 lean rows, the 91 duplicate-flag trades. **All are quoted from [`docs/medium-tier-bug-hunt-2026-09-16.md`](medium-tier-bug-hunt-2026-09-16.md) and its spec-back. None was re-run while writing this spec.** Session A's acceptance requires re-running handle `H-3`; the others are not re-run by this spec.
- **What Deribit's `trades.BTC-PERPETUAL.100ms` notification actually carries.** That is the whole point of Session B's measurement, and nobody has the answer yet.
- **Whether `trades.BTC-PERPETUAL.raw` exists and behaves as arm 3 assumes.** Taken from Deribit's channel naming convention, not from a live subscription.
- **The intended semantics of the funding-momentum colour.** No card spec was read — the same gap the bug hunt recorded in its own "what I did not verify" list.
- **Whether the fixture id `A83a` is free.** Suggested, not checked against the harness.
- **The exact render sites for `TargetCapReason*` on both surfaces.** This spec requires the implementer to read them. The general mechanism was located; the lines were not.
- **Line numbers throughout** are from the tree at `5e42327`. They move. **Anchor on the symbol, not on the number.**

---

## 11. Reporting

Report back with **two documents**, per [`docs/batch-review-packet-convention.md`](batch-review-packet-convention.md):

- a `*-batch-summary.md` outcome record — what happened;
- a `*-spec-back.md` review packet — ranked verification handles with pasted output, decisions taken one line each, decisions queued with your read, feedback on this spec's own assumptions, and what you did not verify.

⛔ **Rank handles by whether the READER can run them.** Label an executable check `H-n`, and build-time evidence the reader cannot re-run `E-n`. **Never rank an `E-n` first.**

⭐ **Log every auto-proceeded decision in ONE line** — the decision, the options, what you picked, why. Put it in the spec-back's own decisions-taken section.
