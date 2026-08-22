# Spec-back — deploy acceptance gate, CADENCE not one row

**Reports against:** [`deploy-acceptance-gate-cadence-spec.md`](deploy-acceptance-gate-cadence-spec.md)
**Built:** 2026-08-22. **Scope executed:** `tools/ops/collector.ps1` only, plus the doc amendments in §2.4 below. **No engine code.**
**Tree state at hand-off:** `master`, level with `origin/master`, **UNCOMMITTED**. `git diff --stat` = 4 files. Two of them (`UI/MainForm_Layout.vb`, `docs/DeribitIndicatorProject.md`) are the **pre-existing** `rbRepeat` engine fix and its Section 15 v68 row — **not mine, and not part of this build.**

**Format:** [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Single lane, so there is no companion `*-summary.md` — this packet carries the outcome record inline in §1.

---

## 0. Model + effort for the reviewing seat

**Model: Opus. Effort: high.**

**Why that tier.** Not for the diff — it is ~60 lines of PowerShell and the judgment was done in the spec. It is because **the artefact under review is a checker, and the last review of this checker passed a version that certified a dead collector as healthy.** A reviewer at a low tier will read the pass condition, agree it is stricter than the old one, and stop. The two findings in §3.2 below both survive that reading, and one of them (**F-2**, the `spanSec` expectation) would cause a *reviewer* to mis-read a healthy live run.

**Where the reviewer will specifically slip:** accepting §1's H1–H3 as evidence the gate works **live**. They are not. They are evidence the *mechanism* is correct offline. §4 is the section that matters.

---

## 1. Ranked verification handles

⭐ **If you only run one: H1.** It executes the shipped payload against the exact 2026-08-22 failure and reports whether it now fails.

| # | Claim | The check | Passes when |
|---|---|---|---|
| **H1** | The one-row failure now fails the gate | `& "<scratchpad>\gate-exec-check.ps1"` — it **extracts** `$gateCmds` and `$rowOk` from `collector.ps1` by regex and executes them; it does not retype them | `ALL GATE PAYLOAD CHECKS PASSED`, and the `T2` line reads `rows=1 span=0 gate=False` |
| **H2** | The pass condition is cadence, not existence | `grep -n 'rowOk = ' tools/ops/collector.ps1` | prints exactly `$rowOk = ($rowsAfter -ge 2 -and $spanSec -ge 45)` — **one hit, no `GATE_LAST_ROW_NEWER` anywhere** |
| **H3** | The retired marker has no surviving consumer | `grep -rn 'GATE_LAST_ROW_NEWER' tools/ \| wc -l` | prints **0**. ⚠ **Scoped to `tools/` on purpose — do not widen it to `docs/`.** The name is *discussed* in the spec and twice in this packet, so a `docs/` sweep counts prose and fails a correct build. See §3.5 |
| **H4** | The deadline is derived, not nudged | `grep -n 'AddMinutes' tools/ops/collector.ps1` | `AddMinutes(12)` at [collector.ps1:819](../tools/ops/collector.ps1:819), and **no other `AddMinutes(5)` in `Wait-DeployGate`** |
| **H5** | Trap 2 avoided — no full read of a 22 MB file | `grep -n 'Get-Content .\$csv' tools/ops/collector.ps1` | the only hit carries `-Tail 50`. **An unqualified `Get-Content $csv` is a fail** |
| **H6** | Trap 1's premise is real, not inherited | `grep -n execution_resolution settings.json` | `3` / `3` / `1` at lines 369 / 378 / 387 — ASIA / LONDON / NY. **This is why 5 minutes was unsafe** |
| **H7** | P4 — the operator sees evidence, not a verdict | `grep -n 'poll: PID=' tools/ops/collector.ps1` | the line carries `rowsAfterRestart=$rowsAfter spanSec=$spanSec`, not a boolean |
| **H8** | P5 — session and settings-version checks untouched | `git diff -U0 tools/ops/collector.ps1 \| grep '^[+-]' \| grep -c 'sessionOk\|versionOk'` | prints **0**. ⚠ **The `-U0` and the `^[+-]` filter are both load-bearing** — without them the diff's *context* lines are counted and it prints 3 on a correct build. See §3.5. Pair with `grep -n 'sessionOk = \|versionOk = '`, which must still show both declarations |
| **H9** | The file still runs | `[Parser]::ParseFile(...)` → 0 errors | already run: **PARSE OK**. Cheap, and a broken deploy script is discovered at 3am otherwise |

**Arithmetic identity worth having:** on any passing poll, `spanSec ≈ (rowsAfterRestart − 1) × cadenceSeconds`. It is **not** ≈ one cadence unless exactly two rows landed. See **F-2** — the spec's own V1/V3 get this wrong.

**Outcome record — 10 of 10 offline cases pass.** Extracted-payload execution, not a trace:

| Case | rows | span | gate |
|---|---|---|---|
| Two rows 180 s apart (ASIA/LONDON 3-min) | 2 | 180 | pass |
| **One row after restart — the 2026-08-22 failure** | 1 | 0 | **fail** |
| Header only · no CSV · all rows pre-restart | 0 | 0 | fail |
| Two rows 30 s apart (double-write — D-1's veto) | 2 | 30 | fail |
| Two rows exactly 45 s apart (boundary) | 2 | 45 | pass |
| Malformed row between two good ones | 2 | 60 | pass |
| NY 1-min cadence | 2 | 60 | pass |
| 5,060-row book, `-Tail 50` window | 50 | 2940 | pass, 21 ms |

**One additional risk closed by execution, not reasoning.** `AnalysisLogger.vb:179` opens the CSV with `New StreamWriter(path, append:=True)`, which takes a `FileShare.Read` lock. I held a file open exactly that way and ran `Get-Content -Tail 50` against it concurrently: **not blocked, 50 lines returned.** `-Tail` uses a different read path from a full `Get-Content`, so this was not inherited from the old code and was worth proving rather than assuming.

---

## 2. Decisions queued

### Q-1 — Five operator strings outside `Wait-DeployGate`. Keep the fix, or revert to the literal §3 patch?

The spec's scope line reads **"`tools/ops/collector.ps1`, function `Wait-DeployGate` only."** But the gate's contract is *printed* by five strings in two other functions, all of which said "5 minutes" and "a new CSV row":

| Location | Was | Now |
|---|---|---|
| [collector.ps1:609–611](../tools/ops/collector.ps1:609) | step-9 comment + `Section` | names 2 rows / 45 s / 12 min |
| [collector.ps1:615](../tools/ops/collector.ps1:615) | `Ok 'ACCEPTED -- new row landed…'` | adds the "does NOT prove it survives an hour" caveat |
| [collector.ps1:619](../tools/ops/collector.ps1:619) | `Fail 'gate did not pass within 5 minutes…'` | 12 minutes, "not at cadence", "one row alone does not pass" |
| [collector.ps1:675](../tools/ops/collector.ps1:675) | rollback `Section` | same |
| [collector.ps1:685](../tools/ops/collector.ps1:685) | rollback `Fail` (FIX 11's message) | "fewer than 2 new CSV rows within 12 minutes"; **FIX 11's tape-vs-book diagnosis left intact** |

**My read — keep it, and it is not really a scope widening.** The spec's own **P4** requires the operator to see the evidence. A gate that waits 12 minutes while printing "5 minutes" fails P4 at the exact moment P4 matters — an operator watching a healthy ASIA deploy at minute 6 would read "did not pass within 5 minutes" as a hang and intervene. **Labelled a hypothesis:** I am inferring that the spec's scope line meant *no engine code and no new functions*, not *leave the printed contract lying*.

**Narrowest version, if you disagree:** revert all five, keep only §3.1–§3.4. The gate behaves identically either way — this is entirely about what the operator reads.

### Q-2 — The proposal-doc amendments. Right call, right shape?

Trader-directed after the build. [`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) is a **ratified** document with a trader D-table, so I amended rather than rewrote:

- **§2.6** — the original gate sentence is kept **struck through**, followed by the measured gap, then the new gate. Both are kept because the reasoning was sound and only the *sufficiency* was wrong.
- **D-5** — split explicitly: *"the 'not a hash match' half stands; the 'a new CSV row within 5 min' half is SUPERSEDED."*
- **§0 TRAP 3** and **§5.4 step 7** (box replacement) — both amended in place with the date.

**My read — the strike-through-and-amend shape is right and should be the house pattern for ratified docs.** [`batch-review-packet-convention.md`](batch-review-packet-convention.md) already rules that a superseded item is corrected *in place, at the top of the affected section*; this extends it to a D-table cell. **I have no read on** whether you want the struck-through original kept long-term or removed at the next trim — that is a doc-budget call, and `CLAUDE.md`'s session-start note says sprawl, not age, is what has actually bitten this project twice.

### Q-3 — Track the offline harness, or leave it in the scratchpad?

Spec **D-4** ruled **no fixture**, on the grounds that `collector.ps1` sits outside every `.vbproj` and inventing coverage is worse than admitting there is none. I honoured that: `gate-exec-check.ps1` lives in the session scratchpad and is **not** in the repo.

**My read — D-4 is right about fixtures and this is not one, but I would still leave it untracked.** It is not coverage: it proves the payload's *arithmetic*, and it would go green against a gate that could never reach a real box. Tracking it creates exactly the false comfort D-4 was protecting against — a green script in `tools/` that a future seat cites as "the gate is tested." **If you want it kept**, the honest home is `docs/` beside this packet with a header saying it is offline mechanism evidence and not V1–V4.

### Q-4 — V2 gates the ship. Who runs it, and when?

**Shared root with Q-3**, so rule them together: both are "what counts as evidence for this gate." The spec is unambiguous — *"V2 is not optional. A gate that has only ever been observed passing is in exactly the state this gate was in on 2026-08-22."* **I ran none of V1–V4** (§4).

**My read — do not ship without V2, and V2 is cheap right now.** The negative case needs a build *without* the `rbRepeat` fix, and that fix is **still uncommitted in the working tree** — so the negative binary is one `git stash` away today and a revert-commit away after you push. **The cheapest ordering is V2 before the commit, not after.**

---

## 3. Spec-back proper

### 3.1 What the spec got right, specifically

- ⭐ **"§0 trap 1 — reading `on_close` cadence as the NUD interval."** This was the load-bearing sentence in the document. Without it the obvious edit is to keep 5 minutes, and the result is a gate that **fails healthy ASIA and LONDON boxes and rolls them back** — strictly worse than the bug being fixed. I verified the premise rather than inheriting it (**H6**): `settings.json` really does carry 3 / 3 / 1.
- **"Escalation trigger: if the gate fails on a box you can independently show is collecting, STOP. Do not tune the tolerance until it passes."** This is the right shape for a checker, because the failure mode of *fixing a checker* is loosening it until it agrees with you.
- **"Do not 'improve' this into a regex match on the timestamp shape; the parse IS the validation."** Correct, and I would have been tempted. It is now a comment at the call site.
- **D-3's reasoning against folding the tape in** — *"folding it in would let a healthy tape mask a dead book"* — is right, and there is a **second, stronger reason the spec does not give**: the rollback gate exists to escalate a v66/v67 restore that comes back running-but-stopped. The tape on such a box is **fine**. A tape assertion would make `Invoke-Rollback` report OK on precisely the case FIX 9 was built to catch. D-3 is not a preference; the alternative is a defect.

### 3.2 Two findings against the spec

**F-1 — D-2's arithmetic is wrong, though its conclusion is right.** The spec says deriving the deadline per session *"buys one saved minute on the failure path."* It does not. A derived deadline would be ~5 min in NY against a fixed 12, so **NY failures wait ~7 extra minutes**, not one. **I still agree with fixed 12** — the extra wait costs only book rows (the tape is unaffected, per FIX 11), it lands on a deploy that is already failing, and the escalation says STOP anyway. But the D-table's justification should not be re-quoted in a later doc, because the number in it is not right.

**F-2 — §5's V1 and V3 state `spanSec` as a point estimate, and it is a lower bound.** V1 expects *"a plausible `spanSec` (≈60 in NY, ≈180 in ASIA/LONDON)"*; V3 says *"if `spanSec` ≈ 60 every time, you have only tested the 1-minute path."*

`spanSec` is `last − first` **across every row newer than the restart**, so it grows with each additional row: it is `≈ (rows − 1) × cadence`, not one cadence. The T10 case makes this unmissable — 50 rows returned **span 2940**, not 60. Two consequences for a reviewer:

- A healthy **NY** run that happens to be caught on a later poll with 4 rows shows `spanSec≈180` — **the value V3 tells you to read as ASIA/LONDON.** V3's diagnostic is therefore not sound as written.
- The correct reading is: **`rowsAfterRestart` tells you how many fires; `spanSec ÷ (rowsAfterRestart − 1)` tells you the cadence.** That quotient is what distinguishes the 1-minute path from the 3-minute path, and it is what V3 should have asked for.

This does **not** affect the pass condition — `≥2 rows AND ≥45 s` is correct under either reading, and a growing span only makes the span test easier to satisfy. It affects **V3's ability to do its job**, which was to stop us claiming coverage of the 3-minute path we have not tested.

### 3.3 Where the spec was narrower than its own words

The scope line says **"function `Wait-DeployGate` only"** while **P4** demands the operator see the evidence. The function does not print to the operator on the success and failure paths — its **callers** do, in `Invoke-Deploy` and `Invoke-Rollback`. Applying the scope line literally satisfies P4 inside the function and breaks it outside. See **Q-1**. **The next spec that changes a function's contract should say "and every string that states that contract", or name them.**

Related, and to the spec's credit: **§4 caught the `Invoke-Rollback` caller explicitly** — *"one caller you must not forget."* It caught the *call site* and missed the *messages*. That is a narrow miss, not a blind spot.

### 3.4 One constraint pair that nearly conflicted

**D-4 (no fixture) versus §5 (run it, do not parse it)** read as a deadlock for anyone without AWS access: forbidden to write coverage, required to produce execution evidence, unable to reach the box. **The hatch is that "fixture" and "execution evidence" are different things**, and the resolution is to execute the *extracted* payload rather than a retyped copy — which is why H1 extracts `$gateCmds` and `$rowOk` from the file by regex. A hand-copied harness would have tested my transcription. **Write that hatch into the next spec that bans a fixture but demands execution**, because the tempting alternative is to satisfy §5 by retyping the payload and calling it a run.

### 3.5 ⚠ I published two broken handles, then caught them by running them

Recorded because `CLAUDE.md`'s standing rule — *"verification handles must test the property, not a string that mentions it"* — was written after the `_lastTs` incident, **and I reproduced that incident anyway, twice, in this packet, in the section whose whole job is verification.**

| Handle | As first written | What it actually printed | Why |
|---|---|---|---|
| **H3** | `grep -rn 'GATE_LAST_ROW_NEWER' tools/ docs/` → expect hits only in the spec | **3 hits**, two of them **this packet's own §1 table** | I swept `docs/` for a name that this document is obliged to discuss. The handle counted its own prose |
| **H8** | `git diff … \| grep -c 'sessionOk\|versionOk'` → expect **0** | **3** | Unified diff context lines carry the surrounding code. The count measured *proximity to the change*, not *the change* |

Both would have made a reviewer reject a build that is correct — the same direction of error as `_lastTs`. **Neither was reachable by reading them; both fell out in seconds by executing them.** That is §5's *"run it, do not parse it"* applied to the packet rather than to the code, and it is the third instance in this project of the same lesson.

⭐ **The transferable rule is narrower than "run your handles":** a grep-based handle over a repo that also *documents* the thing being greped is checking a name, not a property, **and the packet you are writing is part of that repo.** Scope such a handle to the code tree, or assert the declaration.

---

## 4. What I did not verify, and cannot

⛔ **The whole of §5 V1–V4 is unrun. Nothing in §1 substitutes for it.**

| Not verified | Why |
|---|---|
| **V1** — a real `deploy` against the test box | needs AWS + SSM. I have no access from this seat |
| ⚠ **V2 — the negative case, deploying without the `rbRepeat` fix** | same. **This is the one the spec calls not optional**, and it is the check that would show the change is worth anything. **Treat this build as unproven until V2 runs** |
| **V3** — confirming the run was not in NY | same, and see **F-2**: as written V3 cannot distinguish the two paths anyway. **The 3-minute ASIA/LONDON path is completely untested, offline and on**, and it is the path trap 1 is about |
| **V4** — `status` ~30 min after a passing deploy | same. This is the check for the property the gate deliberately does not prove |
| SSM stdout ordering, truncation, or partial returns | `ConvertFrom-KeyValueLines` tolerates any order and both new keys match its regex — **checked**. Behaviour on a *truncated* SSM stdout is not checked; a lost `GATE_SPAN_SEC` line yields `spanSec=0` and a **fail**, which is the safe direction, but I have not seen it happen |
| `Get-Content -Tail` on the **real** 22 MB book, on the box, under EBS and whatever AV is installed | I proved only that the writer's `FileShare.Read` lock does not block it (§1). File size, disk and AV are not reproduced locally. **If V1 fails, look here first** |
| `[datetime]::Parse` under a non-invariant culture on the box | the CSV column is `yyyy-MM-dd HH:mm:ss` (`AnalysisLogger.vb:180`), which is culture-robust, and the **old code made the identical call** — so no regression either way. Not observed on the box |
| That any of this survives a commit | **nothing is committed.** The working tree also holds the unrelated `rbRepeat` engine fix |

⚠ **One thing I want to state plainly rather than leave implied.** Every green result in this packet comes from a harness I wrote, run against CSVs I wrote, on a machine that is not the collector. That is the same *category* of evidence as the reasoning that produced the one-row gate — better, because it executes, but not different in kind. **The gate has still only ever been observed passing.**
