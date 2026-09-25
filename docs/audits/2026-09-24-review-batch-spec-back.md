# Adversarial audit follow-up: review batch spec-back (2026-09-24)

The working document for whoever reviews the 12-lane follow-up batch. **The full audit report is [`../adversarial-audit-2026-09-24.md`](../adversarial-audit-2026-09-24.md).** It holds the ranked list (§B), the decisions for the orchestrator (§C) and what's left (§D). The per-lane record and every verdict are in [`2026-09-24-review-batch-summary.md`](2026-09-24-review-batch-summary.md). The spec being reported against is §7 of the report.

Everything is pinned to engine `6e74181` and order app `8232e9e`. The trader ruled newer master commits out of scope.

---

## 1. Ranked verification handles

Every `H-n` below was run in this session, and the output quoted is what it printed. Run the commands from the repo root in bash, with the .NET 8 SDK and python3 installed. These handles replace a re-run of the whole batch.

**If you run only one command, run H-2.** It's the finding most likely to be doubted, and the one that invalidates the most past reads. **A1, the S0, needs a read, not a run:** H-1 covers its engine half, and its app half is three lines (H-9).

| # | Covers | Command | Load-bearing output |
|---|---|---|---|
| **H-1** | The audit report's 22 cases, including AUD-05 (A1's engine half), AUD-04 (B1) and AUD-07 (A6) | `dotnet run -c Release --project verify/auditproofs/AuditProofs.vbproj` | `=== 22 defect(s) confirmed, 0 not reproduced ===` |
| **H-2** | M4, 11 cases: C1, C9–C11, C14 | See `docs/audits/proofs/live-performance-eval-pipeline/README.md`. Its `.vbproj.txt` hard-codes `/home/user/DeribitVerdictEngine/`; replace that with your repo root | `cached bars after first run with High=Low (stubs): 37 of 37` · `eval cache outcome (live path): WINDOW_EXPIRED, TargetEverHit=False` · `same row against the exchange's complete bars: AMBIGUOUS, TargetEverHit=True` |
| **H-3** | M2 P1–P19: A3, A5, A6, B2, B4, F1, F3, F4, F6 | The block under "Exact run command" in `docs/audits/proofs/engine-settings/README.md` | The run matches the README's recorded output apart from one extra hot-reload `Parse error` line, a watcher-timing artefact. Last case: `attempt 2 (timeout key now 15): TypeInitializationException` |
| **H-4** | M3 harness: D1–D7 | `docs/audits/proofs/exit-guard-live-strip-alerts/README.md`, mode `all` (77 s) | Identical to the README, warnings aside. `size sigma 0.9: 902 false EXIT latches in 1200 min (45.1/hour)` |
| **H-5** | M5 probe: E1–E4 | `docs/audits/proofs/trade-store/README.md` | Matches apart from timings and temp paths. `T1a … parsed rows : 9 (10 trades were sent)`; the `/dev/full` case returns 86 |
| **H-6** | M1 probe: C7, C8 | `docs/audits/proofs/autotweaker/README.md`. Edit its two hard-coded paths first | `atomic tmp+rename: Changed events=0`. The control printed `Changed events=2` against the README's 1, which is watcher variance |
| **H-7** | M6a P1–P4: A3 (NaN), A6, B1, B3 | See the block below this table | `P2b: gateFires=False` · `P3: OverflowException …` · `P4: passLong=True passShort=True …` · `P1: HANGS …` |
| **H-8** | M7b ports: C3, C5 | `diff <(python3 docs/audits/proofs/backtest-whatif-ceilingaudit/audit_proofs.py) docs/audits/proofs/backtest-whatif-ceilingaudit/out_audit_proofs.txt` | Prints nothing |
| **H-9** | M9, A1's app half | Read `SignalBridge.vb:687` (the only level check: `StopLevel <= 0 OrElse Target <= 0`) and `frmMainPageV2.vb:3720-3723` (the slippage anchor is the first bid seen) in the order app at `8232e9e`. Then run `python3 docs/audits/order-app/proofs/signal-to-exchange-order-path/trace.py` | `side check: none` in all three cases. Case B's placed trigger is 59,001.5, 4 ticks under the engine's entry |

H-7's command:

```bash
W=$(mktemp -d); D=docs/audits/proofs/mainform-layout-tapestore-calibration
sed "s#\.\./\.\./#$PWD/#g; s#<RootNamespace>AuditProofs</RootNamespace>#<RootNamespace>M6aRun</RootNamespace>#" \
  verify/auditproofs/AuditProofs.vbproj > "$W/M6aRun.vbproj"
cp $D/AuditProofs.vb.txt "$W/AuditProofs.vb"; cp $D/RunMain.vb.txt "$W/RunMain.vb"
dotnet run -c Release --project "$W/M6aRun.vbproj"
```

**Arithmetic identities that expose a bad claim cheaply:**

- A1: a long's trigger sits at or above its fill exactly when `fill ≤ engine_entry − stop_distance`. With a 2-USD stop, that's any fill 2.0 USD or more under the entry. The slippage cap allows 72 USD of drift.
- C2's break-even: `(stop_bps + 3) / (target_bps + stop_bps)` = (32.60 + 3) / (12.82 + 32.60) = **78.4 %**. That is the maker/maker case. M9's table shows 83.5 % once the stop exits as taker.
- C3: a walk that starts at T+3 can only drop early barrier touches, so its bias sign is fixed. That's M7b's table: +0.186, +0.108 and +0.018 ATR for stops of 0.6, 1.0 and 1.6 × ATR.

**Evidence nobody else can run (`E-n`).** Every proof set that ran in its lane is now committed. The only exceptions are two unexecuted files: M9's `AuditFixtures.vb.txt` and M8's excerpts, which were greps.

---

## 2. Decisions (moved)

The decisions a fix needs now live in the full audit report, [`../adversarial-audit-2026-09-24.md`](../adversarial-audit-2026-09-24.md) §C. They are written for the project orchestrator, who decides and sequences the fixes, and each one says whether it also needs the trader's ruling. §C replaces the D-1 to D-13 list that stood here. Two of that list's items were audit work, not fix decisions, and they moved to the report's §D: redoing M8 (now lanes L-1 to L-3) and measuring the exit guard on real tape (now X-3).

---

## 3. Spec-back: what my M1–M10 prompts got right and wrong

**What worked:**

- **Naming specific known findings.** "Already found — extend, do not repeat" worked when it named specific findings. M1 extended AUD-16 into the re-bill loop and the fence bypass instead of restating the crash. M2 turned "no validation" into a per-key range table with 19 proofs.
- **The mandatory LOGIC TRACE.** It made each lane commit to numbers a reader can recompute. M4 printed the engine's placed levels for my trace scenario, and M9 pushed the same levels through the app's own rounding and offset functions. The two agree on the placed geometry. They are not an independent check of each other, because my prompts handed both lanes the same levels.
- **M9's six severity questions.** They got six direct answers, and those answers are what turned AUD-05 into the S0 (A1). Asking the questions that decide severity beats asking for severity.
- **The `.vb` → `.vb.txt` instruction.** It kept CI green. The root project compiles every `.vb` file outside `tools/` and `verify/`.

**Assumptions that broke:**

1. **M8's `<RANGE>` was a placeholder the reader had to fill in.** It went in verbatim. A prompt meant to be pasted has to be complete; M8 should have been three prompts.
2. **No prompt said to commit the output.** All 12 reports lived only in transcripts until a second round of instructions, and the scratchpad proofs were one container reclaim from gone.
3. **Listing known gaps invites restating them.** M8 reported three of its four findings straight from the known-gap list. "Do not report these" has to be explicit.
4. **The model and effort recommendation arrived after the lanes started.** Eight of 12 lanes ran below the recommended tier. The four at medium effort (M2, M5, M6a, M7a) still produced reproducible, high-signal findings. The only refuted claim came from a Sonnet lane (M6b), which also left 14 of 16 `UI/Controls` files unread. M8's thin coverage came from my placeholder, not its model. That's 12 lanes, too few to rest a rule on.
5. **The prompts didn't require the session-start protocol, and five lanes said they skipped it.** M6b's refuted claim is the kind a read of `architecture.md`'s UI layer would probably have caught, since that doc describes the TAPE strip.
6. **The PR-review row in §7.0 had no prompt, so it never ran.** Nobody has reviewed the audit report itself.

**Where the spec was narrower than its words.** M9 was asked to trace "every gate to the exchange", but the prompt gave no list of the app's order-state functions. The four it didn't read (§4) are exactly the ones that decide A2's severity.

---

## 4. What I did not verify

- **Review findings.** None is left unchecked. On 2026-09-25 I checked the 42 the first pass had marked N; 41 hold as described. M9 F7 is confirmed only in part, and lane L-4 covers the rest.
- **Deribit behaviour.** A1, A2 and A9 all turn on three things, and none was checked against the venue:
  - what happens to a sell stop whose trigger has already been crossed when the OTOCO legs are created;
  - what `trigger_offset` does on a `stop_limit` order;
  - how a triggered post-only stop gets repriced.
- **How often A1 fires.** It needs the distribution of `SWING_STOP` distances next to fill drift. There's no CSV or tape in the clone.
- **The real-tape false-latch rate for D2.**
- **Windows-only behaviour.** The watcher's response to a rename-save (C8), `FileShare` collisions (M5 F4), and the modal loop (B2). All are reasoned or proven on Linux only.
- **Four order-app functions nobody read:** `UpdateStopLossForTriggeredStopLossOrder`, `ForceStopLossUpdate`, `SendRateLimitedUpdate`, and the `cancelled` order-state branch.
- **M2's per-key range table** was not re-derived.
- **M10's impact.** It depends on whether the pooled books already contain the rotated rows.
- **Commits after `6e74181`** were ruled out of scope.
