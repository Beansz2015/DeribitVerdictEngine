# Spec-back — engine-fix Session C (`D-8` report lean column, `D-9` Step 3b effect on both surfaces)

**Written:** 2026-09-24 (UTC) by Seat 2, Opus, high. **Spec:** [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §5. **Outcome record:** [`docs/engine-fix-session-c-batch-summary.md`](engine-fix-session-c-batch-summary.md). **Build commit:** `ea32818`. **Base commit:** `3c26437` (Session A's reports).

**Legend — IDs in this packet:**

| ID | Source and kind | Meaning |
|---|---|---|
| `H-n` | This packet, handle | A check the reader can run; output pasted from this seat's run |
| `E-n` | This packet, evidence | Evidence the reader cannot re-run as committed |
| `D-8`, `D-9` | `docs/medium-tier-bug-hunt-spec-back.md` §R.6.3, trader rulings | `D-8` (b) = lean rows in a separate labelled column · `D-9` (b) = render Step 3b's actual effect |
| `EF-2`, `EF-4` | `docs/engine-fix-build-spec-2026-09-21.md` §6, trader rulings | `EF-2` (a) = ties counted, not walked · `EF-4` (a) = the snapshot changes too |
| `EFT-7`, `EFT-8` | `docs/engine-fix-build-spec-2026-09-21.md` §7, traps | Parsing the Step 3b note in the card · fixing only some card sites |
| `EVAL-1`, `DISP-1` | `docs/medium-tier-bug-hunt-spec-back.md` §R.6, findings | Report counts lean rows as trades · card colours funding momentum by direction alone |
| `A86a`–`A86c`, `A87a`–`A87c` | `verify/ordercheck/Program.vb`, new fixtures | See `docs/engine-fix-session-c-batch-summary.md` §3 |

---

## 1. Ranked verification handles

**If you only run one, run `H-1`.** It pins both surfaces' text, the card tone, the new fields on every arm, and the report split.

### H-1 — the harness, default mode (pinned to `ea32818`)

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

```
453 lines start with PASS
SKIP  A81b known-defect repro (CalcLiquidations books a maker-side liquidation on the taker's side) — set ORDERCHECK_KNOWN_DEFECTS=1 to run; ...
ALL PASS
PASS  A34f §6 splits into (a) DIRECTIONAL + (b) NO-TRADE LEAN sub-tables with the D7 not-comparable caption
PASS  A86a longs crowded + RISING (crowding penalty): FundingStep3b points = the cfg-derived effect = the breakdown delta (enabled minus disabled)
PASS  A86c longs crowded + RISING (crowding penalty): snapshot line, card Step 3b row and card tone all carry the effect -1[L]
PASS  A86a longs crowded + FALLING (de-crowding soften): ... 
PASS  A86c longs crowded + FALLING (de-crowding soften): ... carry the effect +1[L]
PASS  A86a shorts crowded + FALLING (crowding penalty): ...
PASS  A86c shorts crowded + FALLING (crowding penalty): ... carry the effect -1[S]
PASS  A86a shorts crowded + RISING (de-crowding soften): ...
PASS  A86c shorts crowded + RISING (de-crowding soften): ... carry the effect +1[S]
PASS  A86a neutral into crowding, RISING with positive funding: ...
PASS  A86c neutral into crowding, RISING with positive funding: ... carry the effect -1[L]
PASS  A86a neutral into crowding, FALLING with negative funding: ...
PASS  A86c neutral into crowding, FALLING with negative funding: ... carry the effect -1[S]
PASS  A86a disabled: longs crowded + RISING with Step 3b off → effect 0 on both sides
PASS  A86c disabled: snapshot line reads Enabled: NO | Effect: none, card reads Enabled=N | Effect=none, tone neutral
PASS  A86b the penalty arm fires on a long score already at 0 → the ACTUAL effect is 0 (the note still names the arm), tone neutral
PASS  A87a ClassifyContextRow: directional, lean long, lean short, tie and excluded verdicts each classify as their own kind (ties on the TEXT)
PASS  A87b CONFIRMED: trades n=2 (1 fail), lean n=4 (1 fail: the short lean), ties 2 counted and not walked; FLOW_UNCONFIRMED: lean only
PASS  A87c §6 (a) renders DIRECTIONAL, LEAN NO TRADE and TIE as three labelled columns, with the not-traded caption
```

- Lines shortened with `...` are cut for width only; the full text is in the harness output.
- **Arithmetic identity:** 435 (after Session A) + 7 (`A86a`) + 1 (`A86b`) + 7 (`A86c`) + 3 (`A87a`–`A87c`) = 453.
- **Load-bearing in `A86a`:** each arm is checked two ways — against the effect derived from cfg, and against the `Funding (info)` breakdown row's points with Step 3b on minus off. The second never reads the note.

### H-2 — the snapshot line was re-formatted, not added

```
git show ea32818 -- UI/MainForm_PlaintextSnapshot.vb | grep '^[-+]' | grep -v '^+++\|^---'
```

```
-        AppendFunding(sb, r, cfg)
+        AppendFunding(sb, r, v, cfg)
-    Private Sub AppendFunding(sb As StringBuilder, r As IndicatorResults, cfg As EngineSettings)
+    Private Sub AppendFunding(sb As StringBuilder, r As IndicatorResults, v As VerdictResult, cfg As EngineSettings)
-        sb.AppendLine(String.Format("  Momentum: {0}  |  Enabled: {1}  |  Soften: +{2}  |  Amplify: -{3}",
-                                     r.FundingMomentum,
-                                     If(cfg.Indicators.Funding.MomentumEnabled, "YES", "NO"),
-                                     cfg.Indicators.Funding.MomentumSoften,
-                                     cfg.Indicators.Funding.MomentumAmplify))
+        ' [D-9 (b) + EF-4 (a)] Re-formatted, not added: this line printed the configured
+        ' Soften / Amplify values (what Step 3b COULD do). It now prints Step 3b's actual
+        ' effect. Card twin: BuildGroupFunding's "Step 3b:" row (same FundingStep3bDisplay).
+        sb.AppendLine(FundingStep3bDisplay.MomentumLine(r, v, cfg))
```

- **Load-bearing:** one `sb.AppendLine` removed, one added, in the same place. **The Session C escalation trigger did not fire.**

### H-3 — every card colour site reads the effect; the direction rule is gone (`EFT-8`)

```
git grep -n "ResolveFundStep3bColour(\|ResolveFundMomColour" ea32818 -- UI
```

```
ea32818:UI/MainForm_Render_Cards.vb:2075:                                          ResolveFundStep3bColour(v)))
ea32818:UI/MainForm_Render_Cards.vb:2119:    Private Shared Function ResolveFundStep3bColour(v As VerdictResult) As Color
ea32818:UI/MainForm_Render_Cards.vb:2520:        AddKv(g.body, "Step 3b:",  FundingStep3bDisplay.CardValue(v, cfg), valueColour:=ResolveFundStep3bColour(v))
ea32818:UI/MainForm_Render_Cards.vb:3237:        Dim colour As Color = ResolveFundStep3bColour(v)
ea32818:UI/MainForm_Render_Cards.vb:3584:        Dim fmColour As Color = ResolveFundStep3bColour(v)
```

- **Load-bearing:** four call sites (meter 2075, FUNDING row 2520, signal row 3237, footer 3584), one definition, and zero hits for the old `ResolveFundMomColour`.

### H-4 — Step 3b's score arithmetic did not change

```
git show ea32818 -- Core/ScoringEngine_Calculate_Scoring.vb | grep '^[-+]' | grep -v '^+++\|^---'
```

```
+        ' [D-9 (b)] Scores entering Step 3b, so its ACTUAL effect (after the clamps) can be
+        ' carried on res.FundingStep3b*Points. Pure read: no score changes here.
+        Dim pre3bL As Integer = ls
+        Dim pre3bS As Integer = ss
+        res.FundingStep3bLongPoints = ls - pre3bL
+        res.FundingStep3bShortPoints = ss - pre3bS
```

- **Load-bearing:** only `+` lines; no line that assigns `ls` or `ss` changed.

### H-5 — the three mutations (recipe; edit, run `H-1`, reverse with the inverse edit)

| Mutation | FAIL lines this seat saw |
|---|---|
| In `ScoringEngine_Calculate_Scoring.vb`: `res.FundingStep3bLongPoints = If(fundingStep3bNote.Contains("-") AndAlso fundingStep3bNote.Contains("[L]"), -1, ls - pre3bL)` — the note-parse shape of trap `EFT-7` | `A86a` longs-FALLING (`want L1/S0; got L-1/S0`), `A86c` longs-FALLING, `A86b` — `3 FAILURE(S)` |
| In `AnalysisRunner.vb`: `Case ContextRowKind.LeanTie : leanRows.Add(row)` — ties walked | `A87b` (`lean n=6 f=3; ties=0`), `A87c` — `2 FAILURE(S)` |
| Swap the two assignments (long effect ↔ short effect) | all six firing arms of `A86a` and `A86c` — `12 FAILURE(S)` |

- ⭐ **Row 1 is the trap happening in miniature.** The note `STEP3b: +1[L] de-crowding` contains a `-` inside "de-crowding". A text parse read the soften as a penalty on the first try.

### H-6 — known-defects mode and the pre-push gate (pinned to `ea32818`)

```
ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/verify-gate.ps1 -Mode prepush
```

```
453 lines start with PASS
FAIL  A81b maker-side liquidation (flag M, taker buy): ...
FAIL  A81b maker-side liquidation (flag M, taker sell): ...
2 FAILURE(S)
---
OK    harness ALL PASS
OK    no snapshot/card drift detected
OK    AnalysisLogger.vb not in the changed set - no header rotation possible
GATE PASSED
```

### E-1 — the Tone → colour map (read by eye; the harness cannot link WinForms)

- `UI/MainForm_Render_Cards.vb` `ResolveFundStep3bColour`: `Caution` → `Theme.ACC_WARN`, `Relief` → `Theme.ACC_STRONG_LONG`, else `Theme.FG_TERTIARY`. The two accents are the ones the old direction rule used for "caution" and "de-crowding".
- `A86c` pins `Tone` per arm. **Nothing pins this three-line map.** A swap of the two accents would pass the harness.

---

## 2. Decisions taken (auto-proceeded, one line each)

- **The effect is the ACTUAL delta, not the configured size:** options were (a) post-step score minus pre-step score, after the 0 floor and the `regimeMax` cap, or (b) the configured `soften` / `min(amplify, high_penalty)` of the arm that fired → **(a)**. It is what the ruling asks for ("what the step actually did") and it equals the breakdown's own points. It loses the arm name when a clamp absorbs the effect, but the `Momentum:` state, the bias and the note still show it. `A86b` pins it.
- **A fourth card site, `BuildGroupFunding`, changed:** the spec lists three. This one is the card twin of the snapshot's `Momentum:` line and printed the same `Soften` / `Amplify` config. Under `EF-4` (a) and the display-string parity rule, the matching card binding must move with the snapshot line. Its row `Config:` was relabelled `Step 3b:`; no row was added.
- **Card words unchanged, colours changed:** options were (a) colour only, as the spec's `D-9` text says, or (b) also rewrite `RISE` / `FALL` and the arrows → **(a)**. The words describe the momentum state, which is still true.
- **Shared helper `Core/FundingStep3bDisplay.vb`:** options were (a) format inline in each UI file, or (b) one host-agnostic helper both surfaces call → **(b)**. It is the only way the harness can pin the text of both surfaces; the P5 text-parity harness cannot see the card.
- **Harness now links `analysis/AnalysisRunner.vb`** (+ `FundingMomentumDiagnostic.vb`, `OutlierAudit.vb`): options were (a) link it and test the shipped `ComputeContextOutcomes`, or (b) move the split into an already-linked file → **(a)**, less code motion. `Run()` needs a live OHLC fetch and is never called. The project comment that said "AnalysisRunner stays OUT" was corrected.
- **Lean walk scope:** lean rows are walked on the same five contexts, the same `ATR > 0` filter and the same hold window as the trades. A plain `NO TRADE` (no lean tag) is excluded. `WEAK LONG` / `WEAK SHORT` tier verdicts stay excluded, as before.
- **Directional test tightened to `IsDirectionalVerdict`:** the old filter admitted any verdict that was not exactly `NO TRADE` and did not start with `WEAK`. Now only `STRONG LONG`, `LONG`, `STRONG SHORT`, `SHORT` count. Any other verdict string (none known) is excluded.
- **§6 (a) rendered as a table:** options were (a) a table with three labelled columns, or (b) extend each bullet → **(a)**; the ruling says "column". `A34f`'s asserted strings are kept.
- **Manual updated** (`docs/UserManual.md` Funding row 2, its colour list, the sign convention, and the report §4 line): not named in the spec; documentation of a rendered line that changed.
- **Fixture ids `A86`, `A87`:** checked free in `verify/`, every `docs/*.md`, and on every branch.
- **One commit for `D-8` and `D-9`:** they share `verify/ordercheck/Program.vb`; the parity requirement is met because the snapshot and every card binding are in the same commit.

## 3. Decisions queued, with my read

### `Q-C1` — should the card also show the effect as TEXT on the three colour sites?

✅ **RULED 2026-09-24 (UTC), trader: NO.** The card keeps its words; only the colours follow the effect.

- **Fact:** the meter, the signal row and the footer show the momentum word plus a colour. Only the FUNDING group row shows `Effect=-1[L]` as text.
- **Options:** (a) leave as built · (b) add the effect to the signal row's note (`step 3b` → `3b -1[L]`) and the footer tag.
- **My read (hypothesis): (a).** The ruling is colour; the text is one card group away. (b) moves more rendered text for no new information. I have no strong view; it is a display preference.

### `Q-C2` — unpinned card colour map (`E-1`)

✅ **RULED 2026-09-24 (UTC), trader: ACCEPTED.** The three-line Tone→colour map stays covered by review only.

- **Options:** (a) accept review-only cover · (b) move the Tone → colour choice behind an enum-to-name map in Core so a fixture can pin it.
- **My read: (a).** Three lines, one owner file, and the accents are the same ones the old rule used. Flagged so it is a choice, not an accident.

### Queue rows that should change — for the orchestrator (I did not edit `docs/trader-tick-queue.md`)

| `docs/trader-tick-queue.md` §2 row | Action |
|---|---|
| "Bug-hunt side findings: `EVAL-1`, `DISP-1`, `TOOL-1`, and a VPFR CSV rider" | Mark `D-8` (`EVAL-1`) and `D-9` (`DISP-1`) **BUILT at `ea32818`**. `D-8` never deploys; `D-9` rides the `EF-1` (a) deploy. **`TOOL-1` / `D-10` and `RIDER-9` stay open** — not in this build |

## 4. Feedback on the spec

### What worked

- **Naming the three card sites with line numbers** made `EFT-8` checkable. It also made the fourth site easy to notice: it was the one that printed config, like the snapshot.
- **`EF-4`'s "re-format ONE line, do not add one"** gave a crisp stop test. `H-2` shows it held.
- **"Do not parse the note" (`EFT-7`)** was right for a reason the spec did not state: the note's text contains a `-` in "de-crowding" (`H-5` row 1).

### What broke or was incomplete

- ⚠ **The spec's card-site list was one short.** `BuildGroupFunding` printed `Soften` / `Amplify` config — the exact `D-9` defect on the card, and the card twin of the snapshot line. A seat that fixed only the three listed sites would have left the card and the snapshot describing Step 3b differently.
- ⚠ **"Fixture: pin the rendered `Momentum:` text and the matching card state" is not reachable as written.** The harness does not link `UI/` (WinForms). The build met it by moving the text and tone into a Core helper both surfaces call; the colour map itself stays unpinned (`E-1`).
- ⚠ **The spec did not say which "effect" — actual or configured.** They differ when a clamp absorbs the step. I took actual (§2, first bullet).
- ⚠ **The spec's `D-8` fixture needed `AnalysisRunner` in the harness,** which the harness project said was deliberately out. Not a problem — `Run()` is never called — but the spec could have said so.

## 5. What I did not verify

- **The card on screen.** No app run, no screenshot. The card claims rest on `H-3`, on `A86c` (text and tone), and on reading `E-1`.
- **The new report on the real book.** `AnalysisRunner.Run` needs a live Deribit OHLC fetch; I did not run it. The 7,195 lean / 59 tie / 3,305 directional figures are carried from bug-hunt handle `H-13` (`docs/medium-tier-bug-hunt-spec-back.md` §R.6.1), not re-measured.
- **That no other consumer reads the removed `Soften` / `Amplify` text.** Checked by `grep` over `*.vb`, `*.ps1`, `*.py` (no hits outside the snapshot); not checked in external tools.
- **The combined deploy.** Nothing here is live until it.
- **Carried without re-checking:** the `D-8`, `D-9`, `EF-2`, `EF-4` ruling text.
