# Spec-back — collector ops tooling (Part A auto-run-on-start + Part B collector.ps1)

**For:** the reviewing seat, before this ships to the trader / before `deploy` is ever run for real.
**Spec reported against:** [`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md).
**Commits:** uncommitted at the time of writing — Part A (settings + UI + fixtures + docs) and Part B (`tools/ops/collector.ps1`) are both complete in the working tree, not yet committed or pushed.

> **Recommended review tier: Sonnet, effort high, same tier the spec itself asked for on Part B.** Part A's diff is small and mechanical; Part B is a ~600-line PowerShell script whose blast radius is a live collector holding unrecoverable tape, and it has never been run against anything live — every claim about its correctness in this packet is static (parser + manual trace), not executed. A reviewer with the tree open, not just this packet, is the right bar.

**Single document, not a two-document batch** — [`batch-review-packet-convention.md`](batch-review-packet-convention.md) governs multi-lane batches handed to a different seat; this is one proposal implemented by one seat in one sitting, so there is no separate outcome record to point at. Everything material is in this packet.

**State:** Part A built, harness green, `verify-gate.ps1 -Mode local-fast` → GATE PASSED. `status` ran live against production (§6). `fetch` ran live against production (§7). **`deploy` has now run live against the test box — FAILED SAFELY, nothing lost, two more defects found (FIX 8, FIX 9) — see §8.** `deploy` has not run against production and must not until §8's fixes are independently re-checked.

**⚠ REVIEWED — see §5 for the response.** One blocking defect (Part B, a bare PowerShell `if`-expression that parses clean but throws at runtime) plus three lesser findings, all fixed. §4's prediction that a parse check could not catch this class of defect was correct — the reviewer caught it only by executing the payload on 5.1. **`deploy` remains unrun against anything, including the test box, until this document's fixes are independently re-checked** — §4 and §5's own execution gap are unchanged by fixing the one bug the reviewer happened to find by hand; they are evidence for running the smoke test §5 recommends, not a substitute for it.

---

## 1. Ranked verification handles

Ordered by how much of the build each one covers. All are one command; none needs AWS credentials.

### ⭐ If you only run one

```bash
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build
```

**Must print `ALL PASS`, including `PASS A58a`, `PASS A58b`, `PASS A58c`.** ⚠ **Corrected post-review (see §5) — this originally overclaimed A58b as "the structural §1.3 safety-property proof."** It is not, and cannot be: `OrderCheck.vbproj` does not link any `MainForm_*.vb` file, so nothing in this harness can test `chkArmAutotrade`/`_autotradeArmed`. A58b now covers only the JSON round-trip (`start_engaged:true` deserialises `True`). §1.3's safety property is verified by reading `UI/MainForm_Layout.vb` / `UI/MainForm_SignalBridge.vb`, not by a fixture. What this command *does* cover in one line: the default-false byte-identity (A58a), the JSON contract (A58b), and the overlay routing both directions plus the tweaker-fence reject (A58c). If this doesn't print `ALL PASS`, nothing else in §1–§2 needs reading yet.

### H2 — the overlay admit is a single key, not a widened block

```bash
grep -n "AdmittedBlocks\|AdmittedKeys" Core/Settings/SettingsLoader.vb
```

**The load-bearing fact is which array `auto_run.start_engaged` landed in.** It must appear in `AdmittedKeys` (the `network.*`-style per-key list) and **`auto_run` must NOT appear in `AdmittedBlocks`** (the `trade_store`-style whole-block list). If a reviewer sees `"auto_run"` added to `AdmittedBlocks` instead, the interval/trigger_mode cadence keys — which move scoring, per the existing `RejectNotes` comment right above — silently became overlay-overridable too. This is the one place a five-minute "just widen the block" edit would reopen a rejected-for-cause hole. See §3.1 below for why this array even needed touching.

### H3 — the tweaker fence needed no code change, and that is itself checkable

```bash
git diff --stat tools/AutoTweaker/SettingsDiffApplier.vb
```

**Must print nothing — zero lines changed.** `auto_run.` was already a whole-block `RejectedPathPrefixes` entry (comment-labelled HARD CONSTRAINT 14) before this build. Fixture A58c pins that `auto_run.start_engaged` inherits the reject with the shared prefix-reject message text (`"HARD CONSTRAINT 11/12"` — see the note in §3.5 on why the label and the returned string disagree, which is pre-existing and not something this build introduced). A diff here would mean the fence was touched when the spec said it shouldn't need to be.

### H4 — the five-version cap balanced (archive gained what §15 dropped)

```bash
grep -c '^| \*\*v6[3-8]' docs/DeribitIndicatorProject.md
grep -c '^| \*\*v63' docs/history-archive.md
```

**First must print `5` (v68/v67/v66/v65/v64), second must print `1`.** The arithmetic identity that matters: one version added to §15, one version pushed out to the archive, count stays at the stated cap. A silent double-add or a dropped-not-archived row would break this without breaking the harness.

### H5 — collector.ps1 is syntactically real, not just prose that looks like PowerShell

```powershell
$errors = $null; $tokens = $null
[System.Management.Automation.Language.Parser]::ParseFile("tools/ops/collector.ps1", [ref]$tokens, [ref]$errors)
$errors.Count
```

**Must print `0`.** This does not prove the script is *correct* — see §4, nothing here does — only that it is not broken PowerShell. I ran this after two rounds of fixes; see §3.1 for what the first draft actually had wrong, which this handle would have caught immediately if I'd run it first instead of last.

### H6 — the six-item allowlist is a positive record, not a folder copy, in both directions

```bash
grep -n "SixFiles = @\|SixDirs = @\|FetchFiles = @\|FetchDirs = @" tools/ops/collector.ps1
```

**Four short arrays, nothing computed from `Get-ChildItem $LocalBuildDir` or similar.** `deploy`'s backup/upload/place/hash steps all iterate `$SixFiles`/`$SixDirs` by name — never a wildcard, never "everything in the folder." TRAP 1 in the spec's own §0 is exactly the failure mode a positive-record array structurally forecloses; grepping for the arrays' *existence and shortness* is a cheaper proxy than reading every call site that uses them.

### H7 — no engine surface was touched by Part B

```bash
git diff --stat -- '*.vbproj' '*.sln'
```

**Must print nothing.** `collector.ps1` is a standalone script outside every project file, matching the spec's own repeated claim ("no engine code is touched"). This is the cheapest possible confirmation of that claim.

---

## 2. Decisions queued, with my read

### Q1 — ⚠ the overlay-routing re-design (§1.4's own escalation trigger fired). Ratify the per-key admit? ✅ RATIFIED, see §5

The spec's D-1 says *"Yes — reuses the `trade_store.enabled` overlay pattern exactly"* and assumes the overlay whitelist already admits `auto_run.`. **It did not — `auto_run` was on the reject list**, with a comment explaining why (`interval_minutes`/`interval_seconds`/`trigger_mode` move scoring cadence). The spec's own §0 names this exact scenario as its escalation trigger: *"the overlay whitelist does not admit `auto_run.`, and the routing needs re-designing rather than forcing."*

**What I did:** admitted `auto_run.start_engaged` individually into `AdmittedKeys` (the `network.*` per-key pattern) rather than adding the whole `auto_run` block to `AdmittedBlocks`. My reasoning: `start_engaged` doesn't touch cadence — it only decides whether the *already-shared, already-rejected* interval keys get consulted automatically at load versus on a manual click. It can't itself create a scoring divergence the block-level reject exists to prevent.

**My read: this is the re-design the spec called for, not a forced fit, and I'd ratify it as written.** But it *is* a design call I made unilaterally mid-build rather than one the trader ruled — the spec's own words say this should be "a D-row for the trader." I'm flagging it here rather than treating my own judgment as settled. **I have no read on whether the trader would want it recorded as a formal D-10** — that's a documentation-convention call, not a technical one.

### Q2 — I extended the rollback safety property beyond §2.5's literal text. Keep the extension? ✅ RATIFIED, see §5. ⚠⚠ SUPERSEDED IN PART, see §8 (FIX 9) — the ratification of *extending rollback to steps 6/7* stands; the ratification of *skipping the gate on rollback* was wrong and is withdrawn.

§2.5 step 9 and §2.6 specify restore-from-backup-then-restart-then-re-verify **only for a step-9 acceptance-gate failure**. The spec is silent on what happens if the **place** (step 6) or **hash-verify** (step 7) steps fail — both of which happen *after* step 4 has already stopped the app, so a failure there leaves the collector **offline** with nothing said about bringing it back up.

**What I did:** on a place or hash-verify failure, `collector.ps1` also restores the backup and restarts the app (~~without re-running the full 5-minute gate — just the restart~~ **WRONG, see §8** — this half of the original design is superseded), on the reasoning that leaving a 24/7 collector stopped indefinitely is a worse outcome than the spec's authors would have intended, even though the letter of §2.5 doesn't say to do this at steps 6/7.

**My read: keep it — a collector left offline is a live data-continuity failure, and every other line in this spec treats data continuity as the thing to protect above all else.** This is the "spec narrower than its own words" finding in §3.3, not a disagreement with the spec's intent. **The *extension itself* (restore+restart on steps 6/7, not just step 9) remains correct and ratified — what was wrong was the *shortcut inside it* (restart without the gate), found live in §8 and fixed there. Every rollback now runs the same gate a successful deploy does, regardless of which step failed.**

### Q3 — `-DryRun` switch, not in the spec. Keep it? ✅ RATIFIED, see §5

Added so `deploy`'s pre-flight (local git checks, six-item presence, remote process/version read, the printed plan) can be exercised with zero write risk, stopping right before §2.5 step 3's y/n prompt.

**My read: keep it.** It directly serves the spec's own acceptance line — *"Prove `deploy` end-to-end against the t2.micro test box first, never production"* — by giving a zero-risk way to validate the pre-flight logic before the first real y/n is ever answered, on either box. It changes nothing about `deploy`'s write path; `-DryRun` returns before anything remote is touched by write intent.

### Q4 — no read from me: the actual EC2 instance IDs and the operator's own S3 permissions. ✅ RESOLVED, see §5

`-InstanceId` has deliberately no default (D-7), which means I never needed to know the real prod/test instance IDs to write this script — but it also meant nobody had verified that the operator's own local AWS CLI identity (separate from the two EC2 instance roles D-8's policy covers) actually has S3 read/write on `deribit-engine-bucket`. The `ssm-s3-verify.json` probe referenced in the spec's §2.2 tested the *instance roles*, not the operator's own credentials, which is what `fetch`'s local download and `deploy`'s local upload both run under.

**Resolved by the reviewing seat, tested live: user/Beansz can PUT and LIST on `deribit-engine-bucket`. No IAM work needed.**

---

## 3. Spec-back proper — feedback on the spec itself

### 3.1 ⚠ THE ASSUMPTION THAT BROKE — and it was stated as settled fact in the spec

§1.4: *"The dev box turns it back off through `settings.local.json` — the same overlay that already carries `trade_store.enabled: false` for exactly the same reason. One mechanism, one precedent, no new concept."* §3's D-1 recommendation repeats it: *"reuses the `trade_store.enabled` overlay pattern exactly."*

**Checked in the tree, not assumed:** `Core/Settings/SettingsLoader.vb`'s `RejectNotes` array explicitly rejects `auto_run.*` with a stated reason (cadence moves scoring). The "one mechanism, no new concept" framing undersold what was actually needed — not reuse of the *same* admit, but a *narrower* one, because `auto_run.start_engaged` sits inside a block that is rejected for a reason that still applies to its siblings. See Q1 above for what I did about it. **The spec's own §0 anticipated this exact failure mode and named the correct response** ("re-design rather than force") — which is why this is worth less as a complaint and more as confirmation that the escalation-trigger section did its job.

### 3.2 What the spec got right, specifically

- **§0's four named traps mapped directly onto design decisions, not just review checkboxes.** TRAP 1 (backup scope) is why `$SixFiles`/`$SixDirs` are positive-record arrays threaded through backup/upload/place/hash rather than any folder-level operation (H6 above). TRAP 3 (verify-by-hash proves bytes arrived, not that the app runs) is why `deploy` has *two* separate verification steps — hash (step 7) and the live CSV-row gate (step 9) — rather than treating a hash match as sufficient. TRAP 4 (session 0) is why `Wait-DeployGate` asserts `SessionId -gt 0` as a *named, separate* condition from "the process exists," not folded into one check.
- **§2.1's PoC evidence, cited with an actual measured result rather than an assumption, removed what would otherwise have been the single biggest unknown in Part B.** Knowing the launch lands in session 2, not session 0, and that the running engine is untouched during the PoC, is why `Start-RemoteApp` could be written with confidence instead of hedged with "TODO: verify this works."
- **§2.7's out-of-scope list, read literally, kept Part B from growing a fourth verb.** The §5 replacement-cutover procedure reads as something a tool *could* automate — it isn't, because §2.7 says store manipulation is never this tool's job, "even for a good reason." I held that line; see the header comment in `collector.ps1`.

### 3.3 Where the spec was narrower than its own words

- **§2.5/§2.6's rollback language is written only against the step-9 gate failure**, but the document's own repeated framing — *"the safety property is the order,"* *"nothing has changed at this point"* — implies the same care should apply to every failure point after step 4 stops the app. A literal reading leaves steps 6/7 failures with the app stopped and no stated recovery. Covered in Q2.
- **D-7's "no default `-InstanceId`" is stated as a deploy-only concern** (§3's table heading is "await trader" under §2.5's context), but I applied it to `status` and `fetch` too. **My read: correct** — a `fetch` against the wrong box silently pulls the wrong tape into `analysis_log_aws.csv`, which is exactly the kind of quiet-wrong-target failure D-7's own reasoning warns about, just for a read path instead of a write path. Worth stating explicitly in a future revision so it isn't implementer-inferred.

### 3.4 A constraint pair that nearly conflicted, and the hatch

**D-7 (no default target, ever) and D-8 (a bucket name and region that are already fixed, known facts) look like they pull the same direction — "don't hardcode infrastructure" — until you look at what each is actually protecting against.** D-7 protects against *deploying to the wrong box*, a per-invocation, high-consequence choice with no single right answer. D-8 protects against *pointing the lifecycle-rule blast radius at `thecentralstorage`*, a fact that is true once and never varies per invocation. Treating both the same way — no defaults anywhere — would have made every single `fetch`/`status` call require re-typing the bucket name, buying no safety (the bucket is not a "which one" decision, it's a settled fact) for real friction.

**The hatch: default the settled facts (`-Region eu-west-2`, `-Bucket deribit-engine-bucket`), never default the per-invocation target (`-InstanceId`).** Worth naming in future specs that mix "protect against picking wrong" requirements with "this value never changes" facts in the same D-table — they read as the same kind of caution and aren't.

### 3.5 A pre-existing inconsistency this build inherited, not introduced

`SettingsDiffApplier`'s `RejectedPathPrefixes` array labels `auto_run.` as **"HARD CONSTRAINT 14"** in its comment, but `Validate()`'s actual returned `ErrorReason` string for *every* prefix-matched reject is the shared literal `"HARD CONSTRAINT 11/12"` — the per-prefix numbers are comment-only labels, not what the code returns. Fixture A58c asserts the string the code actually returns (`"HARD CONSTRAINT 11/12"`), not the comment label, per the verification-handles-must-test-the-property rule. **Flagging this because a reviewer skimming the comment and then checking A58c's assertion against "HARD CONSTRAINT 14" would think the fixture is wrong — it isn't; the comment and the code have disagreed since before this build touched the file.**

---

## 4. What I did not verify, and cannot from here

Stated so nothing is assumed covered.

| Item | Why not |
|---|---|
| ~~⭐ That `status` works against a real instance~~ | **Resolved, §6 — ran live against production, succeeded cleanly, both SSM round trips returned real data.** ⚠ This was run against **production, not the test box** — a deliberate deviation from this packet's own recommended sequence, made on the trader's explicit instruction after consulting the orchestrator (not a call I made or would have made unprompted); see §6 |
| ~~⭐ That `fetch` works against a real instance~~ | **Partially resolved, §7 — ran live against production. The S3 round trip (upload, download, recursive directory transfer) and the verification logic itself both proved correct** — `backtest_data\` matched exactly at 2 files / 57,901,750 bytes; `analysis_eval_cache.csv`, `ws_health.log`, `capture_marker.log` all verified clean. **The manifest-vs-live-file race (FIX 7) was found the same run and is fixed; the fix itself is unexecuted** — see §7 |
| ~~⭐ That `deploy` works against a real instance~~ | **Partially resolved, §8 — ran live against the test box. Pre-flight, plan, stop, backup-with-verification (FIX 2), upload, and the hash check (step 7) all proved correct and the hash check caught a real defect exactly as designed.** Two more defects found (FIX 8, FIX 9), both fixed, **neither re-verified live** — see §8 |
| ~~⭐ That the §2.1 session-2 launch mechanism holds for the real engine binary, not just `notepad.exe`~~ | **Resolved, §8 — `Start-RemoteApp` launched the real `DeribitVerdictEngine.exe` into session 2 on the first live attempt.** The PoC in `ssm-poc-launch.json` measured only `notepad.exe`; this was the single item flagged as most likely to fail differently for a WinForms app with a WS connection, and it didn't |
| ~~The `aws ssm send-command`/`get-command-invocation` JSON field names~~ | **Resolved, §6 — confirmed live against this account's actual `aws-cli/2.28.20`.** `Command.CommandId`, `Status`, `StandardOutputContent` all parsed correctly; both `status` sub-commands returned `Success` and readable output |
| ~~The operator's own local AWS credentials' S3 permissions on `deribit-engine-bucket`~~ | **Resolved, §5 — tested live by the reviewing seat: PUT and LIST both confirmed** |
| **Every manifest-comparison / hash-verification code path in `fetch` and `deploy`** | Traced by hand, not executed. I found and fixed three real bugs this way (a string-escaping error, a cross-function variable-scope bug, a missing restart-after-restore) before calling it done — which means my hand-tracing has a demonstrated non-zero miss rate on this exact file, and a fourth bug of the same class is not something I can rule out from a fourth read |
| **PowerShell 5.1 vs the Windows PowerShell / pwsh version actually installed on the two target boxes** | The script declares `#requires -Version 5.1` and avoids ternary/null-coalescing operators per that constraint, but I did not run it *on* Windows PowerShell 5.1 specifically — only parsed it, and parsing doesn't execute version-gated behaviour |
| **Whether a 6-second wait before the post-restart `Get-Process` check is long enough for the real engine** | Copied from the PoC's `Start-Sleep -Seconds 6` for `notepad.exe`. `Get-Process` should see the process the instant it spawns regardless of how long full initialisation takes, so this is probably fine — reasoning, not measurement |

---

## 5. ⚠ REVIEW RESPONSE — Part B had one blocking defect. Fixed in this commit; re-check owed before any live run.

**Part A: ratified as written, no changes.** Q1 (the per-key overlay admit), Q2 (the rollback extension to steps 6/7), Q3 (`-DryRun`) all confirmed sound. Q4 resolved live — the operator's own AWS identity can PUT and LIST on `deribit-engine-bucket`; no IAM work owed.

**Part B: ⛔ one blocking defect, found by execution, not by anything in this packet's §1.** `Wait-DeployGate` (then at `collector.ps1:590-591`) built its `GATE_PID=`/`GATE_SESSION=` output lines as `'text' + (if (...) {...} else {...})`. PowerShell parses a bare `(if ...)` as an attempt to invoke a command named `if`, not as an expression — it is not the `$(if ...)` subexpression form the code needed. **This parses cleanly and fails only at runtime**, which is exactly what §4 named as a class this packet's static checks could not reach ("that class of defect... only execution catches it"). The reviewer confirmed by executing the pattern on Windows PowerShell 5.1.

**Why this was worse than an ordinary bug:** `Wait-DeployGate` is the acceptance-gate check (deploy step 9) *and* the rollback re-verification (after a restore). Both calls fail identically on this bug, so a **completely successful deploy would have reported "gate failed → rollback → rollback also failed"** — a false double-failure on a healthy box, the kind of signal that gets someone to RDP in and start "fixing" something that was never broken.

### Fixes applied

| # | Where | What was wrong | Fix |
|---|---|---|---|
| **1** ⛔ | `collector.ps1`, `Wait-DeployGate` (2 lines) | `'text' + (if ...)` — invalid expression form, throws at runtime on 5.1 | Both changed to `` 'text' + `$(if ...)` ``. Grepped the whole file for the same pattern afterward — these were the only two occurrences ("zero correct usages" — confirmed) |
| **2** ⚠ | `collector.ps1`, backup step (step 5) | `'BACKUP=done'` was emitted **unconditionally** — no `-ErrorAction Stop` on the `Copy-Item` calls, no post-copy check that the six items actually landed in `_deploy_backup\`. A failed copy would report success, and the *previous* backup was already deleted by that point | `Copy-Item` calls now run inside `try/catch` with `-ErrorAction Stop` (a caught failure reports `BACKUP=error:<message>` and aborts). After the copy, every one of the six items is re-verified present in the backup — directories by **file count**, not existence alone — before the script can ever reach the line that says `BACKUP=done`. Mirrors the stop step immediately above it, which was already written correctly (polls actual process state, doesn't assume `Stop-Process` worked) — the right pattern was already in the file, one step away |
| **3** ⚠ | `verify/ordercheck/Program.vb`, fixture A58b + every doc that quoted it (`settings.json` ×2, `DeribitIndicatorProject.md`, this packet's own H1) | A58b's reflection clause (`AutoRunSettings` carries no `Arm`/`Autotrade`-named property) proves only that the *settings* side has no such field. It says nothing about `chkArmAutotrade`/`_autotradeArmed`, which is where §1.3's actual safety property lives — and `OrderCheck.vbproj` links no `MainForm_*.vb` file, so no fixture in this harness ever could test it. The fixture name, this packet's H1, the `settings.json` `change_log`, and the `DeribitIndicatorProject.md` row all called it "pinned structurally" / "the structural proof" — all four overclaimed | Reflection clause deleted. A58b renamed `A58b_StartEngagedRoundTripsTrueThroughJson`, now asserts only the JSON round-trip. The A58 group header comment in `Program.vb`, both `settings.json` fields, the `DeribitIndicatorProject.md` v68 row, and this document's H1 all corrected to say plainly: §1.3 is verified by **reading** `MainForm_Layout.vb`/`MainForm_SignalBridge.vb`, not by a fixture |
| **4** ⚠ | `UI/MainForm_AutoRun.vb` `StartAutoRun()`, called unattended from the constructor since this build | `_intervalMs < 10_000` (e.g. a hand-edited `interval_minutes: 0`) shows a blocking `MessageBox.Show`. Unreachable at shipped defaults, but a disconnected collector with a bad interval now hangs at startup with nobody to click OK — the exact §2.1 session-0 hazard the spec warned about, reopened in session 2 | `StartAutoRun(Optional silentOnInvalid As Boolean = False)`. The constructor's auto-start call passes `True`: on an invalid interval it logs to the console and returns without engaging — no dialog, no hang. The manual Start-button click path is unchanged (still shows the `MessageBox`, since a human is present to see it). The 10-second threshold itself is not duplicated at the call site — `silentOnInvalid` only changes *how* the existing check reports, not what it checks, so there is no second literal `10_000` to drift |
| **5** | `settings.json` (`modified_by` + `change_log`), `DeribitIndicatorProject.md` v68 row | Claimed `verify-gate prepush GATE PASSED`. Only `-Mode local-fast` was actually run; `prepush` mode needs a committed diff range against `origin/master`, which uncommitted work does not have — running it pre-commit would have produced a misleading "no engine-path change" reading, not a meaningful gate | Both corrected to state what was actually run: the solution + all tool projects + `OrderCheck` built **0/0** Release separately, plus `verify-gate.ps1 -Mode local-fast` → GATE PASSED (harness ALL PASS, display-parity clean, version-bump nudge satisfied) |

### The durable lesson, carried forward

**A parse check cannot catch a defect whose failure mode is "valid syntax, wrong runtime behaviour."** `(if ...)` vs `` $(if ...) `` is exactly that shape — H5 in §1 was never going to find it, and neither would a better-written version of H5, because the payload is a string until the moment SSM executes it remotely. **The fix going forward is a runtime smoke test, not a better static check:** a `status`-only round trip against the test box is read-only, cheap, and would have surfaced this specific defect in one call, since `status` already exercises `Invoke-RemotePs` end-to-end. This should run before `-DryRun` deploy, not instead of it, the first time `collector.ps1` ever touches a real box.

**⛔ Standing instruction, unchanged by §6 below: do not run `deploy` against anything, including the test box.** The five fixes above were believed correct on the same static-verification basis (parse + trace) that missed FIX 1 in the first draft — that gap is now partly closed for the plumbing `deploy` shares with `status`, not for `deploy`'s own additional logic. See §6.

---

## 6. FIX 6 and the first live confirmation — `status` ran against production

**A sixth defect surfaced the same way FIX 1 did: relayed from execution, not found by anything in this packet.** `Invoke-RemotePs` wrote the SSM command payload with `Set-Content -Path $tmp -Value $payload -Encoding utf8`. On Windows PowerShell 5.1, `-Encoding utf8` writes UTF-8 **with a BOM**. A BOM-prefixed file handed to `aws ssm send-command --parameters file://...` risks a JSON parse failure on the CLI side — the same defect *class* as FIX 1 (valid-looking code, wrong behaviour only visible at runtime against the real CLI), in the one function every verb in this script depends on.

**Fix applied:** `[System.IO.File]::WriteAllText($tmp, $payload, (New-Object System.Text.UTF8Encoding $false))`, replacing the `Set-Content` call — the only place this script writes that file. **While applying it I introduced and caught my own second bug in the same edit**: a first draft of the explanatory comment used VB-style `'` instead of PowerShell's `#`, which parses as an unterminated string literal, not a comment. Caught by re-running the AST parse check (H5) before reporting the fix done, not by review.

**Then `status` ran live, for real, against production (`i-08c740e22d507667d`, eu-west-2) — not the test box.** ⚠ **That target was a deliberate deviation from this packet's own §5 sequencing** ("`status`... against the test box only"); I flagged the mismatch before running anything, the trader confirmed the choice was intentional and made in consultation with the orchestrator, and supplied the instance ID directly. Recorded here rather than silently overridden, per the same discipline this whole packet asks of the spec's own escalation triggers.

**Result: both SSM round trips returned `Success` with real, readable output** — host stats, memory pressure, session state (`ssm-mem.json`), and app health including a healthy 0.2-minute CSV lag, 23,833 rows, and current `ws_health.log`/store-file state (`ssm-apphealth.json`). No `FAIL` line. This is the first proof that `Invoke-RemotePs`'s send → poll → parse path — the plumbing every verb in this script is built on — works end-to-end against the real AWS CLI and a real box, closing the two items marked resolved in §4.

**What this does and does not extend to:** `status` only ever calls `Invoke-RemotePsFile` against two pre-existing, already-hand-verified command files. It never touches S3, never calls `Invoke-Aws` for anything but the SSM round trip itself, and never reaches `Wait-DeployGate`, the backup/place/hash-verify chain, or any code path FIX 1–5 touched. **Their fixes remain unexecuted.** The standing instruction above is unchanged by this section — if anything, finding a sixth live-only defect in the one function `status` *does* exercise is a reason to run `fetch` next (still read-only, still cheap) before `-DryRun` deploy, not a reason to consider the harder-to-reverse paths any more proven than they were.

---

## 7. FIX 7 — `fetch` ran against production. The S3 transport is proven; the manifest was not.

**`fetch` ran live against production. Three of the four `§4` items its execution could reach are now proven, not reasoned about:** the S3 round trip itself (upload, download, recursive directory transfer) worked correctly, and so did the verification logic's *comparison* half — `backtest_data\` matched exactly at 2 files / 57,901,750 bytes, and `analysis_eval_cache.csv` / `ws_health.log` / `capture_marker.log` all verified clean.

**⚠ The fourth — the manifest itself — did not, and it is not an intermittent failure.** `Invoke-Fetch`'s original Step 1 measured `(Get-Item $f).Length`, then counted lines with `Get-Content ... Measure-Object -Line`, then ran `aws s3 cp` — three separate instants read against `analysis_log.csv`, a file the live collector is actively appending to 24/7. Measured on this run:

```
box manifest : analysis_log.csv | 21,943,640 bytes | 23,847 lines
downloaded   : analysis_log.csv | 21,944,609 bytes | 23,847 lines
```

Same line count, `+969` bytes — almost exactly one row. `Length` caught the row mid-write; `Measure-Object -Line` then counted it whole (a line is a line whether or not its trailing bytes are flushed yet); the upload, running later still, captured the row complete. **The manifest was the torn read. The download was correct throughout — strictly better than what the manifest described it as.**

`fetch` printed `FAIL ... SIZE MISMATCH`, declared the fetch `INCOMPLETE`, and exited 1 — a false failure on a healthy transfer, **the same shape as FIX 1**: a correct operation reported as a catastrophe, because a bug lived in the code doing the reporting, not the code being reported on. ⚠⚠ **And this is structural, not luck-dependent.** A 24/7 collector always appends; the three-instant read races it on every run except by coincidence. `backtest_data\` matching exactly this run means its buffered flush happened to miss the window that particular time — the same defect was present and simply didn't fire. **Do not read a clean directory match as evidence directories are safe from this; they are exposed to the identical race, just less often, because they flush less frequently than every row.**

**Fix applied, exactly as scoped:** the box now snapshots every target into `$dir\_fetch_snapshot\` *first* (`Copy-Item`, wrapped in `try/catch -ErrorAction Stop`, mirroring FIX 2's pattern), then computes the manifest from the snapshot, then uploads from the snapshot — never touching the live files for measurement or transport after the snapshot completes. This makes **manifest == uploaded == downloaded by construction**, all three reading the same static bytes, rather than tolerating a race between them. The snapshot directory is removed on every exit path, including a snapshot failure itself (`~80 MB` transient cost on a `30 GB` disk, per the reviewer's own sizing — not re-derived here). **A `local -ge box` size tolerance was considered and rejected**, per the reviewer's explicit recommendation against it: it would have papered over torn reads rather than preventing them, and would silently accept a genuinely short transfer in whichever direction happened to be growing.

**Not yet re-verified live.** This fix is believed correct on the same static basis (trace + parse) that has now missed something live-only-detectable on three separate occasions in this file (FIX 1, FIX 6, FIX 7) — see the note below. `fetch` has not run again since this change.

### The pattern across FIX 1, FIX 6, and FIX 7 — three for three, none reachable by parsing

Every defect found in this file so far was found by running it, not by anything in this packet's own verification handles. `status` found FIX 6. `fetch` found FIX 7. Neither is a coincidence of thoroughness — both are the specific class §4 named from the start: **valid syntax, correct-looking logic, wrong behaviour only visible against the real CLI, the real clock, or (for FIX 7) the real concurrent writer.** A parse check cannot see a race condition; a code trace cannot see what a 24/7 process is doing to a file while this script reads it three times in sequence. **The cheap-read-only-first sequencing — `status` before `fetch` before `-DryRun` deploy before a real `deploy`, test box before production — is not a formality here; it is the only verification method in this file's history that has actually caught anything.** ⚠ **Superseded, §8: this section originally said `deploy` should not run until `fetch`'s FIX 7 was independently re-verified live — `deploy` in fact ran against the test box before that happened. It found two further defects (FIX 8, FIX 9) of exactly this same class, so the underlying point stands regardless: nothing in this file has ever been proven correct by anything other than running it.**

---

## 8. `deploy` ran against the test box — FAILED SAFELY, and two more defects (FIX 8, FIX 9)

**Most of the machine worked, and it worked in order.** Pre-flight (all three gates: git clean+pushed, six items present, remote app confirmed) → plan printed with hashes → `y/N` answered → app stopped and confirmed → backup written with all six items verified present (**FIX 2 held exactly as designed**) → uploaded to S3 → hash check → rollback → relaunch. **`Start-RemoteApp` launched the real `DeribitVerdictEngine.exe` into session 2** — the single item flagged in §4 as most likely to behave differently from the `notepad.exe` PoC, and it didn't. §2.1's mechanism holds for the real engine.

### FIX 8 — the place step reported success it never checked, and for a reason worth naming precisely

**8(a) — `aws` does not resolve by name inside the SSM session on a box where the CLI was installed after the SSM Agent last started.** Measured on the test box: `Get-Command aws` failed, `Test-Path 'C:\Program Files\Amazon\AWSCLIV2\aws.exe'` was `True`. The installer updates the **machine** `PATH`; the long-running agent process keeps the `PATH` it started with. The binary was there; the agent's own process could not see it. **Fixed** by refreshing `$env:Path` from the machine+user registry values at the top of every remote command array that invokes `aws` — a single `$PathRefreshCmd` variable, spliced into `fetch`'s upload commands and `deploy`'s place commands, so there is one definition to keep correct rather than two to keep in sync. **This will recur on every fresh box** (a freshly-provisioned collector, the §5 replacement candidate) regardless of whether this specific agent happens to restart — the fix does not depend on the agent's restart state, deliberately.

**8(b) — `'PLACED=done'` printed unconditionally.** With `$ErrorActionPreference = 'Continue'` remotely, every `aws s3 cp` in the place step failed to stderr (as a direct consequence of 8(a)) and the script carried on regardless — the marker was never actually checking the property its name claimed. **Fixed**: each `aws s3 cp`'s `$LASTEXITCODE` is checked; `PLACED=done` is reached only when all six succeeded, `PLACED=incomplete:<list>` otherwise, matching the pattern already used for `BACKUP=incomplete`/`MANIFEST` reporting.

**The diagnosis is airtight without re-running anything, and it's worth recording why:** the three files that matched at hash time were `deps.json`/`runtimeconfig.json`/`OFL.txt` — precisely the three byte-identical between v67 and v68. The three that mismatched were `exe`/`dll`/`settings.json` — precisely the three that differ. **The box still had v67. Nothing was placed, and the hash check (step 7 — TRAP 3's own reason for existing) is what caught it.** Without step 7 the script would have restarted the box on a half-deployed state and reported success.

**⚠ This is the third instance of the exact defect FIX 2 and FIX 7 already found: a marker this script prints is not a property this script checked.** Swept the rest of the file for the same shape rather than fixing this one and waiting for a fourth — see below.

### The sweep found a fourth instance: `Restore-DeployBackup`'s `RESTORED=done`

**Same defect, in the rollback path itself** — no `-ErrorAction Stop` on the restore's `Copy-Item` calls, no check that the six items actually landed back on the box, `RESTORED=done` printed regardless. **This is arguably the most safety-critical instance of the four**, since it sits inside the function every failure branch now calls. **Fixed to match FIX 2's pattern exactly**: `try/catch -ErrorAction Stop` around the copies, then every item re-verified present (dirs by file count against the backup) before `RESTORED=done` is reached; `RESTORED=incomplete:<list>` otherwise. Also made `fetch`'s `SNAPSHOT_CLEANED=true` marker check `Test-Path` rather than assume — lower stakes (nothing downstream gates on it), fixed for the same reason regardless.

**No fifth instance found.** `STOPPED=true`/`LAUNCHED=true` were re-checked and are both already conditional on an observed `Get-Process` result, not printed blind — the correct shape this whole class of fix has been pushing every marker toward.

### ⛔⛔ FIX 9 — the serious one, and it is not about the test box

**The hash-mismatch branch (§8's own defect, above) restored v67, relaunched it, printed `OK relaunched (LAUNCH_SESSION=2)`, and exited.** But **v67 has no `auto_run.start_engaged`.** The app came back **running and stopped**: process confirmed, collection never checked. Measured: 3.4 minutes past a 3-minute cadence with no new row.

**The rollback verified the PROCESS started. It never verified COLLECTION resumed. Those are different properties on every build before v68 — which is every build a rollback could ever restore to**, by construction (a rollback restores the *previous* build, and auto-run-on-start is new in this one). ⚠⚠ **Production was still on v66 at review time. Before this fix, any rollback on production would have left the collector running, reporting success, and silently not capturing** — the exact failure mode this project treats as unrecoverable past ~24h.

**Fixed by control-flow change, not a patch:** extracted a single `Invoke-Rollback` function — restore, restart, then run the **same** `Wait-DeployGate` a successful deploy uses, on the *restored* build (`-ExpectSettingsVersion $null`, since the restored build's version is whatever it was, not the one just deployed). All four failure branches (place, hash, restart-confirm, gate) now call this one function instead of three/four hand-written restore+restart sequences — which is exactly what let the place and hash branches silently omit the gate check the step-9 branch happened to already have. `Invoke-Rollback` returns `$true` only when the restored build is confirmed both running *and* collecting; every caller now reports loudly on failure rather than falling through to `exit 2` after a bare "relaunched."

**⚠ Q2 is superseded in part, recorded in place at §2 above.** I ratified "restore + restart, without re-running the full 5-minute gate — just the restart" in the original review. **That ratification was wrong, and this run is the proof.** The reasoning behind Q2's extension — a stopped collector is worse than an unverified one — was sound and remains ratified; the reasoning behind *skipping the gate specifically* assumed "restarted" and "collecting" were the same fact, and this run showed they are not, on every build old enough to matter. A five-minute wait on a rollback is cheap. A silently idle collector reporting success is the failure this entire file exists to prevent.

**Not yet re-verified live.** FIX 8 and FIX 9 are believed correct on the same static basis (trace + parse) that has now missed something live-only-detectable four times in this file (FIX 1, FIX 6, FIX 7, FIX 8) — the sweep-found FIX 2-pattern fix to `RESTORED=done` is *additionally* unverified since it sits on the one path (rollback) this run actually exercised, under the *old*, buggy version of that same function. **Standing instruction, strengthened rather than relaxed by how much worked this run: `deploy` does not run against anything, including the test box again, until FIX 8 and FIX 9 are independently re-checked — and given FIX 9 changes the failure path specifically, the next test-box run should include a deliberate failure (e.g. a wrong hash) to exercise `Invoke-Rollback` on purpose, not wait to find it by accident a second time.**

**What the sequencing bought, stated plainly:** had this run gone against production first — as was nearly the case two turns ago — the deploy would have failed identically (FIX 8 is box-state-dependent, not target-dependent) and the rollback would have left the live collector silently not capturing, on the box whose data loss this whole project treats as unrecoverable. Test box first was the right call, ratified in §5, and it just paid for itself a second time.
