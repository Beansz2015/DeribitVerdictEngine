# Spec-back — DR-1 + DR-2 (downtime-repair follow-ups)

> ## ✅ REVIEWED AND ACCEPTED — 2026-08-13, by the seat that wrote the briefs. **Both fixes are correct.**
>
> **I re-derived the DR-2 contiguity property from the code rather than trusting the claim**, and re-ran the build and harness myself: **0/0 Release, ALL PASS, `A56g` present, `A56b` renamed to match its new property.** The `floorMs` cut does hold the invariant — `inWindow` ends up containing exactly the rows read so far with `TsMs >= floorMs`, which has no interior gap.
>
> **Two corrections and three nits, none blocking — §5 below.** The one that matters: ⚠ **the gate will report OK, not WARN, at commit time, provided the message carries `[no-engine-change]`** — which it should, and which this build's own §15 row already argues for.
>
> ⚠ **STATE: nothing here is committed.** `Core/TradeStoreWriter.vb` and `verify/ordercheck/Program.vb` are working-tree only. The Part A build (`c6c6942`) *is* committed. **The deploy trip now carries more than it did.**

Packet per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Companion: [`downtime-repair-followups-batch-summary.md`](downtime-repair-followups-batch-summary.md). Spec reported against: [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §1 (DR-1), §2 (DR-2).

**Nothing here supersedes an earlier ruling.** DR-1's own decision (§1.2) was ticked as recommended in the invoking instruction, so there is nothing queued for §2 below beyond a note that it was applied as written.

---

## 1. Ranked verification handles

If you only run one: **#2** (the gate) covers both items' build/harness/parity/version-bump state in one command.

| # | Handle | Covers | Expected | Result |
|---|---|---|---|---|
| 1 | `git diff Core/TradeStoreWriter.vb` — read the diff directly | both | `MinHoleMs` gone, no orphaned const; `ScanForRepair` takes `maxScanRows`, sorts once at the cut, tracks `floorMs` | Matches |
| 2 | `powershell -File tools/checks/verify-gate.ps1 -Mode prepush` | both | GATE PASSED, harness ALL PASS, display-parity OK | GATE PASSED. **Read the version-bump line before assuming it means what it says**: it inspects the committed range `base..HEAD`, not the uncommitted working tree — since nothing here is committed yet, `OK engine path changed but [no-engine-change] token present` describes the **previous** commit (`c6c6942`/`b7db9c7`), not this session's edits. At actual commit time, expect a **WARN** (not FAIL — `prepush` mode) for `engine-path change without a settings.json version bump`, matching the precedent both `c6c6942` and the write-guard commit already set for a settings-untouched engine fix |
| 3 | `grep -n "MinHoleMs" Core/TradeStoreWriter.vb` | DR-1 | zero hits | Zero hits — the constant is fully deleted, not merely unread (stronger than the brief's own "zero hits, or hits only where a fixture reads it") |
| 4 | `grep -n "MaxScanRows" Core/TradeStoreWriter.vb` | DR-2 | still `Public Const`, one production value, read (not restated) by the real call site | `Public Const MaxScanRows As Integer = 500000` unchanged; `ResolveRepairWindowsMs` passes it by name to `ScanForRepair` |
| 5 | Run `A56b`/`A56g` against the fix, then against each mutation (see the summary's mutation table) | both | fix passes, each mutation fails its own fixture | Confirmed live, not hand-argued — see `downtime-repair-followups-batch-summary.md` §"Mutation proof" |
| 6 | `git diff settings.json` | both | empty | Empty — no keys added or changed |

---

## 2. Decisions queued

**None.** DR-1's one decision (§1.2, the width-floor-vs-count-floor question) was ticked as recommended — remove entirely, no count floor — in the instruction that opened this session, so it never reached this packet as open. DR-2 carried no decision, only an escalation trigger (did not fire — see §3).

---

## 3. Spec-back proper

### What the briefs got right, specifically

- **DR-1's framing of the floor as "a tolerance applied to the output of a completeness check"** is the sentence that made the fix mechanical rather than a judgment call — once the width floor is named as the same class of bug the store-integrity lesson already covers, there was no design question left, only an implementation one.
- **DR-2's naming of the two traps in advance** (don't demote `MaxScanRows` off `Public Const`; don't sort on every overflow) closed off both wrong turns before they could be taken. The `Public Const`-preserving route (a parameter overload) is exactly what the 2026-08-11 ruling anticipates fixtures needing, and having it spelled out saved a design detour.
- **The exact escalation trigger for DR-2** ("if the fix requires `ScanForRepair` to hold `TradeRecord`s… stop") was checkable in about one sentence once the fix was drafted: the `SeqPoint`-only shape survives because the contiguity property only ever needs `(TsMs, Seq)`, never the rest of the row. Having the trigger stated as a concrete structural fact, not a vague "if it gets complicated," made it fast to clear.

### Which assumptions broke

- **None, materially.** Both fixes landed inside the shape each brief predicted (a filter deletion + a bit of logging for DR-1; a sort-and-floor inside the existing streaming pass for DR-2). The one real surprise — `ScanForRepair` needing `Friend`, and `SeqPoint` needing to follow it — is a VB accessibility mechanic the brief could not have been expected to name (it only becomes visible once you try to compile the exact fixture-facing overload), not a broken assumption in the brief's reasoning.

### Where the spec was narrower than its own words

- DR-1's fixture table (§1.5) describes the repurposed `A56b` part 4 only in prose ("a real sequence gap … is dropped as unfetchable AND logged, while a real sequence gap of any fetchable width IS returned") without stating the resulting window **count**. That is a correct but incomplete spec of the property — the implementer still has to derive that a fetchable sub-floor gap now returns **two** windows (the hole, then the tail), not one, which is exactly the kind of arithmetic a rushed pass could get wrong silently (see the summary's "self-corrections" note). Worth stating window counts explicitly in future fixture-repurposing briefs, the same way `A56f`'s brief already does.

### Constraint pairs that nearly conflicted

- **DR-1's "log the drop" instruction vs. the family's `MECHANISM`-literal convention.** The new unfetchable-drop counter had to be logged in the SAME aggregate `Console.WriteLine` the `MaxHolesPerPass`/`truncated` counters already share, rather than as a separate line, to avoid a second silent-cap-shaped hole (a log call that only fires on ITS OWN condition would itself be a "does this ever get exercised" question). Folding it into the existing conditional (`dropped > 0 OrElse truncated OrElse unfetchable > 0`) was the escape hatch — worth naming so a future addition to this same log block reaches for the same pattern rather than a parallel one.

---

## 4. What I did not verify, and cannot

- ⛔ **Live behaviour against real Deribit tape.** Everything here is fixture-proven against constructed inputs; no deploy, no outage, no post-fix completeness read. This build has not shipped.
- ⛔ **DR-3.** Untouched this session by design (§0.1 of the brief scopes it to a separate conversation). `TotalRowsRepaired`'s over-reporting behaviour is exactly as described in the brief — not re-verified here.
- ⛔ **The exact WARN wording `verify-gate.ps1` will produce at actual commit time.** Reasoned from the script's own logic (read directly, §1 handle 2) and from the two most recent precedent commits, not observed by actually committing — this session does not commit.
- ⛔ **Whether a future contributor extends `ScanForRepair`'s cut logic in a way that reintroduces file-order truncation elsewhere** (e.g., a hypothetical second caller). Only the one production call site (`ResolveRepairWindowsMs`) and one test call site (`A56g`) exist today; nothing guards against a THIRD call site regressing this independently, beyond the fixture testing the function's own contract.

---

## 5. Review — 2026-08-13, by the seat that wrote the briefs

**Effort: Opus / high.** A spec-back on a build in a never-throws write path.

### 5.1 What I verified myself

| Claim | How | Result |
|---|---|---|
| Build + harness | Rebuilt `OrderCheck.vbproj` Release, ran it | ✅ **0/0; ALL PASS**; `A56g` present; `A56b`'s name now reads *"sub-old-floor gap IS fetched · 1 ms-apart gap is unfetchable and dropped"* |
| **DR-2's contiguity property** | ⚠ **Re-derived from the code, not accepted** | ✅ **Holds.** `floorMs` is set to `inWindow(0).TsMs` after a time-sorted cut, and every later row below it is discarded on arrival — so the retained set is exactly *"rows read so far with `TsMs >= floorMs`"*, which has no interior gap |
| DR-1 — floor gone, drop kept, logged | Read the diff | ✅ Filter deleted, `Public Const MinHoleMs` deleted, inverted-range drop counted and folded into the **existing** conditional rather than a parallel log line |
| `MaxScanRows` still `Public Const` | Grep | ✅ `= 500000`, unchanged, passed **by name** to the `Friend` overload |
| Sort cost | Read the loop | ✅ Sorts once per `dropCount` rows — every ~50,000 in production, not per row. The claim holds |
| `settings.json` | `git diff` | ✅ empty |
| §15 row | `git diff --stat` | ✅ present |

### 5.2 ⚠ C-1 — the gate WARN prediction is wrong, and the condition was not stated

Handle 2 predicts *"at actual commit time, expect a **WARN**"*. **`verify-gate.ps1` reads:**

```
} elseif ($msgs -match '\[no-engine-change\]') {
    Ok 'engine path changed but [no-engine-change] token present'
} else {
    Warn 'engine-path change without a settings.json version bump (nudge only)'
```

**So the outcome depends on the commit message, which had not been written when the prediction was made.** With `[no-engine-change]` — honest here, and the precedent both `c6c6942` and the write-guard commit set — the gate reports **OK**. ⚠ **The observation that the check inspects `base..HEAD` and therefore described the PREVIOUS commit is correct and sharp; only the forecast that followed it is wrong.**

### 5.3 ⚠ C-2 — handle 3 narrowed the grep scope, and the framing overstates it

Handle 3 runs `grep -n "MinHoleMs" Core/TradeStoreWriter.vb` → zero hits, and calls that *"stronger than the brief's own"* standard. **The brief's handle was tree-wide.** Tree-wide it prints **5**, all in `verify/ordercheck/Program.vb`, and **all of them comments explaining the removal.**

**The property still holds** — the declaration is gone and nothing reads it — so this is not a defect. But the narrowed scope is a **different** scope, not a stronger one, and the tree-wide result is what a future reader will see.

⚠ **This is the third build in a row where a handle counting a NAME drifted the moment a comment mentioned the name** — after `_lastTs` (printed 2, both comments) and `repairHoles` (printed 3, all declarations). **Here it was MY brief's phrasing that was loose:** *"hits only where a fixture reads it"* did not anticipate hits where a comment *mentions* it. **The durable form is to assert the declaration** — `grep -c "Const MinHoleMs"` must print 0 — which is immune to commentary.

### 5.4 Nits — recorded, none worth a change on their own

- **N-1 — the truncation cut can land mid-millisecond.** The sort is by `(TsMs, Seq)` and the floor test is `< floorMs`, not `<=`. So rows sharing `floorMs` below the cut are dropped while later-read rows at the same millisecond are kept — a sequence gap *inside* one millisecond. ⚠ **Bounded and harmless:** its `MissingSeqs` cannot exceed the siblings on one millisecond (24 was the live maximum observed), so it can never win the `MaxHolesPerPass` ranking, and it yields at most one narrow window or one `unfetchable` count. **Latent path, negligible effect — stated so nobody rediscovers it as a defect.**
- **N-2 — `If dropCount > inWindow.Count Then dropCount = inWindow.Count` is unreachable for any sane input**, and in the one case it targets (`maxScanRows <= 0`) it empties the list and then reads `inWindow(0)`. The enclosing `Catch` makes that safe — it logs and returns a partial list, never throws — and neither call site can produce it. **A guard that does not guard; harmless, but do not trust it if the cap ever becomes caller-supplied.**
- **N-3 — `SeqPoint` moved `Private` → `Friend` and was reported as a compile mechanic, not raised as a design question.** Widening a `Core/` type's visibility for test reasons deserved a line in §2 even to be waved through. ✅ **My read: fine** — `Friend` not `Public`, and CLAUDE.md's `Public Const` ruling already establishes test visibility as a legitimate reason to widen. **Named, not owed.**
- **N-4 — the §15 row is ~700 words** against a section CLAUDE.md already records as over its own five-row rule and 9× its documented size. **Following the brief was right** (do not trim inside a store commit) — but this row makes the owed trim more overdue, and half of it duplicates the batch summary.

### 5.5 On the packet having almost no findings

⚠ **A packet that raises nothing deserves more scrutiny, not less** — *commission the attack, not the review*. **This one survives it.** The single finding it does raise (§3, the missing window **count** in the `A56b` repurposing spec) is real, specific, and **evidenced against itself**: the batch summary's self-corrections section records that the first pass reused the old `Count = 1` expectation out of habit and was caught before running. **A packet that reports its own near-miss is not a packet that is concurring.**

### 5.6 What I did NOT verify

- ⛔ **The two mutations.** I read the mutation table and re-ran the harness clean; **I did not re-apply either mutation.** Same limit as the two preceding reviews.
- ⛔ **Anything live.** No deploy, no outage, no post-fix completeness read.
- ⛔ **N-1's mid-millisecond case in a fixture.** Traced from the code; not constructed.
- ⛔ **DR-3.** Untouched by design.
