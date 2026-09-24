# Jev harnesses — second reader, 2026-09-24 (UTC)

**Close-list step 6** in [`trader-tick-queue.md`](trader-tick-queue.md) §2 (the Jev-programme CLOSE LIST row). Format: [`batch-review-packet-convention.md`](batch-review-packet-convention.md), in one document as the brief asked.

- **Reader:** Opus 5.5 (`claude-opus-5-5`), effort high. Not the author of any reviewed change.
- **Start commit:** `853b476`. **Commits made:** `ad73b7e` (tooling fixes and self-tests), `bb035f2` (the `FP-2` title-rename run record), plus the commit that adds this document. Not pushed.
- **Population:** `git log --since=2026-09-22T00:00Z -- tools/checks`, 41 commits: `rider-travel.ps1`, `commit-walker.ps1`, `fixture-parser.ps1`, `doc-scanner.ps1`, `doc-reranker.ps1`, `lib/InvokeJev.ps1`, `lib/doc_*.py`, `measure/*`, `selftest/*`.
- **Jev spend:** 30 calls, all in the `FP-2` re-run (§4 of this document). 35,367 input and 2,500 output tokens. No other keyed call. No unjudged population was touched.
- ✅ **No recorded result changes.** One recorded *explanation* is wrong (finding `SR-2` below). One recorded run *cannot be checked* for a silent defect (finding `SR-3`). Neither met the brief's stop trigger, because neither changes a recorded number. Neither is reinterpreted here.

---

## 1. Findings, ranked

IDs `SR-n` are this document's own (second-reader findings). They do not collide with `F-n`, `FP-Dn` or the 2026-09-22 review's numbered findings.

| ID | Sev | Site (at `ad73b7e` unless noted) | Failure scenario | Recorded result affected? | Status |
|---|---|---|---|---|---|
| `SR-1` | ⛔ MED | `measure/decision-bias/run-decision-bias.ps1:199` (was `Measure-Object -Property UsageIn` over hashtables) | A firewall block on sample 2 or later of any item. PS 5.1's `Measure-Object` cannot read hashtable keys; under `$ErrorActionPreference='Stop'` it throws. **The whole run aborts after spending its calls.** The WAF handling only worked on sample 1 | **No.** The pilot's `decision-bias-20260923T140527Z-jev.json` holds 0 `WAF_BLOCKED` rows | Fixed `ad73b7e`. Proved by `H-2`, and by mutation (`E-1`) |
| `SR-2` | ⛔ MED | `doc-reranker.ps1:307` at `853b476` (`$q.kind -eq 'nowhere'`, one `$noAnswerReport` variable) | `Q29`'s kind is `nowhere_trap:...`, so **the no-answer check never ran on it** — no call was made. A second `nowhere` query would also have overwritten the first | **Numbers: no.** The explanation in [`harness-runs/doc-reranker-reserved-run-2026-09-24.md`](harness-runs/doc-reranker-reserved-run-2026-09-24.md) "Seat analysis" says *"the template prints only one no-answer query"*. The real cause is the kind test. Its conclusion — `Q29` not measured — stands | Fixed `ad73b7e`: every `nowhere*` query is checked and reported |
| `SR-3` | ⚠ MED | `doc-reranker.ps1:188` and `:275` at `853b476` | A firewall-blocked section got noul −1, ranked last, and **nothing counted it**. A blocked expected section would read as a Jev miss | **Unknown.** The build acceptance run and the seat's reserved run printed no counter. It cannot be checked without re-spending the queries | Fixed `ad73b7e`: `SECTIONS_WAF_BLOCKED` printed and reported. Not reinterpreted |
| `SR-4` | ⚠ MED | `fixture-parser.ps1:2022` | The second detector, `FP-Q1` (the scope filter), has a FILE-level gate: a scope baseline naming **one** candidate lets Jev judge **every** candidate. A new fixture's parameters get judged with no seat line. The same class as the 2026-09-22 review's finding 3, on the detector [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4e says must be gated | No. The measured scope run's baseline was written for its own candidates | **Queued** (`SR-D1` below) |
| `SR-5` | ⚠ MED | `doc-reranker.ps1:64` at `853b476` | `powershell -File ... -ExcludeIds Q11,Q12` passes ONE string `"Q11,Q12"`, which **excludes nothing**. Reproduced | No. The seat's run passed only `Q11` | Fixed `ad73b7e` (split on commas, as `doc-scanner.ps1 -Docs` already does). `H-1` check E |
| `SR-6` | LOW | `commit-walker.ps1:1225` | `-CountersOnly` withholds every verdict, but the **exit code still says whether any residual verdict was bad**. One bit leaks from a reserved window | No | Queued (`SR-D3`) |
| `SR-7` | LOW | `commit-walker.ps1:590` | `CLASSIFIER_SUSPECT` fires on a legitimately clean window. At `853b476`, `-Count 60` gives `RESIDUAL_TOTAL=0`, exit 2 (the last 60 commits are all tagged tooling). A short `-Since` window of tooling commits will refuse. **The arming packet's `H-5` (`-Count 60`, pinned `2fd639a`) no longer reproduces from `HEAD`** | No | Queued (`SR-D4`) |
| `SR-8` | LOW | `fixture-parser.ps1:1042`, `:1056` | `Get-FileTypeAt` returned a `string[]` bare. A one-line `.vb` file would unwrap it to a `[string]`, and `$typeAt[$i]` would read characters. Latent: no such file exists | No | Fixed `ad73b7e` (`,$typeAt`) |
| `SR-9` | LOW | `fixture-parser.ps1:2418` | `$fp1JudgedSites = if (…) { @() } else { … }` unwraps one site to a bare object. Latent: only `foreach` reads it | No | Fixed `ad73b7e` (`@(if …)`). `H-4` check D runs the one-site path |
| `SR-10` | LOW | `doc-reranker.ps1` report at `853b476` | Headings hard-coded "acceptance" and "8 acceptance queries"; "at revision" printed the requested ref, not the resolved one. The seat corrected both by hand in the reserved-run record | No (the seat's note already says so) | Fixed `ad73b7e`. Totals now also print over answerable queries only |
| `SR-11` | LOW | `lib/doc_reranker_matching.py:35` | The number token stops at the first dot: `§8.4 item 8e` (`Q04`) requires only a standalone `8` in the heading chain plus the word `item`. Loose matching can count a wrong section as a hit | **Not verified** either way. `Q04` was a miss in both columns | Queued as a note; the matching rule is the plan's "loose by design" |
| `SR-12` | LOW | `rider-travel.ps1:303` | Baseline values are not checked against the vocabulary (`doc-scanner.ps1` and the decision-bias runner do check). A typo reads as `DISAGREE` | No | Queued (`SR-D5`) |
| `SR-13` | LOW | `commit-walker.ps1:1131` | The report header names `-Skip`/`-Count` but not `-Since` | No | Queued (`SR-D5`) |

### 1.1 The unwrap sweep (`RT-U4`, from [`rider-travel-single-row-unwrap-spec-back.md`](rider-travel-single-row-unwrap-spec-back.md) §2)

Swept every `.ps1` under `tools/checks` (including `measure/**` and `selftest/*`): every `return $var`, every `x = if (…)`, every `x = foreach`, every unwrapped `x = <pipeline>` and every `@($var)`.

| Site | Verdict |
|---|---|
| `fixture-parser.ps1` `Get-FileTypeAt`, `$fp1JudgedSites` | Fixed (`SR-8`, `SR-9`) |
| `commit-walker.ps1` `Get-AppCompiledToolsFiles`, `Get-EngineTouchedPaths`; `doc-reranker.ps1` `Get-RerankedOrder`; `rider-travel.ps1` `Get-NamedColumns` | **Left alone on purpose.** Every caller wraps the call in `@()` |
| `rider-travel.ps1` / `rotation-riders.ps1` `Read-AtRev` | Left alone. Its only consumer is `Get-HeaderText([string[]]$lines)`, which re-types a scalar. `rotation-riders.ps1` sits on the push gate, so no idiom-only edit |
| Every other `,@(…)` / `-NoEnumerate` return in `fixture-parser.ps1` | Correct already; no caller wraps them |

⛔ **Two PS 5.1 traps found while sweeping. Both are why the fix is NOT "add `,` everywhere":**
- **`,$list` plus a caller's `@()` nests.** `function f { return ,@(1,2) }; @(f).Count` is **1**. Each function must use ONE of the two idioms, never both. (`H-6`.)
- **`@($x)` on a `New-Object System.Collections.Generic.List[object]` throws** *"Argument types do not match"* in this host — at any element count, even 0. `[List[object]]::new()`, `List[string]` and piping the list do not throw. `doc-reranker.ps1`'s own comment had met this with doubles. (`H-6`.)

### 1.2 The 2026-09-22 review's fixes — hold

Checked against the code, and by the committed self-tests where one exists: finding 1 (app-compiled `tools/` files), 2 (rider-travel sampling, item gate, `NO_ROTATION`, candidate print), 3 (full hash, item gate), 4 (`FP-D27`, receiver-typed callees — `H-4` check B), 5 (code-decided riders), 6 (reported-only counters), 8 and 9 (`FP-D28`/`FP-D29` — `H-4` check B), 10 (`-Since`), 11 (retries — `H-3`). Finding 7 stays open by ruling.

### 1.3 Gates, exit codes, handles

- ✅ **Every harness refuses before any API call.** Re-run code-only with no key at `ad73b7e`: `doc-scanner.ps1` and `fixture-parser.ps1` exit 2 `BASELINE_MISSING`; `rider-travel.ps1` on the unrotated header exits 2 `NO_ROTATION` (10 / 8 riders); `doc-reranker.ps1 -Subset reserved` exits 2 `RESERVED_QUERY_SPENT` (`H-5`). The decision-bias runner reads the key after all four baseline gates.
- ✅ **Exit codes match the packets**, with `SR-6` as the one leak.
- **Packet handles that are really evidence (`E-n` class), not runnable:**
  - [`harnesses-1-2-arming-spec-back.md`](harnesses-1-2-arming-spec-back.md) §1 `H-2` ran an uncommitted scratch script. **Now runnable:** `tools/checks/selftest/invoke-jev-selftest.ps1` (`H-3` here) commits the same four cases.
  - The same packet's `H-3`, `H-4` and `H-6` used synthetic ledgers and transport wrappers that were never committed. Its `H-5` no longer reproduces from `HEAD` (`SR-7`).
  - [`doc-reranker-build-spec-back.md`](doc-reranker-build-spec-back.md) §1 `H-4` was a keyed live `-Query` **that spent reserved query `Q11`**. The tool now warns loudly on that text (`H-1` check F).

---

## 2. Fixes made (all in `ad73b7e`, one revert undoes them)

| Brief item | What changed |
|---|---|
| B1 `RT-U4` sweep | §1.1 of this document |
| B2 harness 5 | Tracked ledger [`harness-runs/doc-reranker-spent-queries.json`](harness-runs/doc-reranker-spent-queries.json), seeded with `Q11` plus the 20 reserved ids of the seat's run. `-Subset reserved` refuses any spent id (`RESERVED_QUERY_SPENT`), refuses with no ledger (`SPENT_LEDGER_MISSING`), and writes each id to the ledger **before** that id's first call. `-Query` prints `WARNING_RESERVED_QUERY=<id>` on a verbatim reserved text (case and spaces folded) and records the id. Every no-answer query is checked (`SR-2`). Subset-true headings (`SR-10`). WAF sections counted (`SR-3`). Comma split (`SR-5`). All gates run before the key is read |
| B3 model version | `lib/InvokeJev.ps1` revision 2 tallies the requested model and the response's own `model`. Every harness prints `JEV_MODEL requested=[…] resolved=[…] run_utc=…` in its coverage block and writes it to its output file (the decision-bias JSON gains `jev_model`; the re-ranker's live log gains `jev_model`) |
| B4 `FP-2` re-run | `-RenameTitles` switch on `selftest/fixture-parser-fp2-mutation.ps1`; result in §4 of this document |
| B5 mechanical defects | `SR-1` (plus a `-TestTransportOverride` seam on the decision-bias runner, the pattern the other harnesses carry) |
| Self-tests added | `selftest/doc-reranker-selftest.ps1`, `selftest/decision-bias-runner-selftest.ps1`, `selftest/invoke-jev-selftest.ps1`; `selftest/fixture-parser-selftest.ps1` gains check D |

**Decisions auto-proceeded (one line each, per `CLAUDE.md` "The obligation that comes with it"):**
- Ledger spends an id **on attempt** (before its first call), not on success — options: on attempt / on success. On attempt records more: a crashed run has still shown partial output.
- `-Query` on a reserved text **warns and records**, does not refuse — the brief said warn; recording keeps the ledger true.
- `Read-AtRev` left unwrapped — step 3 of the three-step test: the typed consumer makes `,` output-identical, and the file is on the push gate.
- Totals print **both** all-queries and answerable-only — the richer option; the seat had to compute the second by hand.

---

## 3. Verification handles

All pinned to `bb035f2` (the tool code is `ad73b7e`; `bb035f2` adds only a run record). Every one was run, with no API key, and the output is pasted. **If you run only one, run `H-1`** (80 s).

**`H-1`** — `powershell -NoProfile -File tools/checks/selftest/doc-reranker-selftest.ps1`
```
PASS  A real ledger refuses the reserved subset
PASS  B no ledger file refuses
PASS  C run completes
PASS  C both no-answer queries checked
PASS  C ledger records all three
PASS  C report headings name the subset
PASS  C WAF-blocked section counted
PASS  C resolved model recorded
PASS  D spent ids refused on re-run
PASS  E comma-joined -ExcludeIds leaves one query
PASS  E one query, one candidate: totals never blank
PASS  F reserved text in -Query warns loudly
SELFTEST PASSED
```

**`H-2`** — `powershell -NoProfile -File tools/checks/selftest/decision-bias-runner-selftest.ps1`
```
PASS  A mid-item firewall block does not abort the run
PASS  A blocked item recorded with its earlier sample tokens
PASS  A the other three items judged
PASS  B jev_model: 17 requested, 16 resolved
SELFTEST PASSED
```

**`H-3`** — `powershell -NoProfile -File tools/checks/selftest/invoke-jev-selftest.ps1`
```
PASS  A transient then success
PASS  B WAF block never retried
PASS  C 400 never retried
PASS  D always transient stops after MaxRetries
PASS  E model tally
SELFTEST PASSED
```

**`H-4`** — `powershell -NoProfile -File tools/checks/selftest/fixture-parser-selftest.ps1` (checks A–C unchanged from the harness-3 batch; D is new)
```
PASS  A exits 2 … PASS  C FP1_BAD_VERDICTS=1 while v2 said mechanism_declared_ok   (15 lines, all PASS)
PASS  D one residual site: exit 0, 10 calls (1 FP-1 + 1 FP-2 item, 5 samples each)
PASS  D exactly one FP-1 row judged
PASS  D JEV_MODEL line: 10 requested, resolved UNAVAILABLE
SELFTEST PASSED
```

**`H-5`** — the real ledger, no key: `powershell -NoProfile -File tools/checks/doc-reranker.ps1 -AcceptanceRun -Subset reserved`
```
SUBSET=reserved  QUERIES=21  EXCLUDED=  SPENT_IN_SELECTION=21
EXIT_REASON=RESERVED_QUERY_SPENT
These reserved ids are already in 'docs/harness-runs/doc-reranker-spent-queries.json' and can never be judged again: Q01,Q02,Q04,Q05,Q07,Q08,Q09,Q11,Q12,Q13,Q15,Q16,Q18,Q19,Q21,Q22,Q23,Q25,Q26,Q27,Q29
exit=2
```

**`H-6`** — the two PS 5.1 traps (§1.1 of this document), no file needed:
```
powershell -NoProfile -Command 'function f { return ,@(1,2) }; "nested=" + @(f).Count; $l = New-Object System.Collections.Generic.List[object]; $l.Add(1); try { $x = @($l); "wrap ok" } catch { "wrap threw: " + $_ }'
```
```
nested=1
wrap threw: Argument types do not match
```
Run 2026-09-24 on PS 5.1.26100.9444.

**`E-1`** (evidence, not re-runnable from the tree) — `SR-1` mutation: with the old `Measure-Object` line restored by hand, `H-2` stopped at `Measure-Object : The property "UsageIn" cannot be found in the input for any objects` and printed no `SELFTEST PASSED`. Restored with the inverse edit.

---

## 4. The `FP-2` title-rename re-run — result

Record: [`harness-runs/fixture-parser-fp2-mutation-titles-2026-09-24.md`](harness-runs/fixture-parser-fp2-mutation-titles-2026-09-24.md).

- ⛔ **5 of 6 flagged, against 6 of 6 in the first run** ([`harness-runs/fixture-parser-fp2-mutation-2026-09-23.md`](harness-runs/fixture-parser-fp2-mutation-2026-09-23.md)). A finding, not tuned away.
- ⛔ **`A23a_FundingMergeClipsOverreachButKeepsStored`: `name_matches`, STABLE 5 of 5, mean top probability 0.602.** With the original title it was `name_overclaims` 5 of 5 at 0.856. The title carried that detection. This is the first stable-and-wrong row on `FP-2` ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4g class).
- The other five: `name_overclaims`, all STABLE 5 of 5. `A65c` moved from UNSTABLE `name_understates` to STABLE `name_overclaims` (0.542).
- Resolved model `jev-1.13.0` on all 30 calls.

## 5. The model-version answer

- ✅ **The API returns the resolved model.** [`docs.typesafe.ai/api.md`](https://docs.typesafe.ai/api.md) "Response body": `model` is required, *"the model that performed the evaluation"*, example `jev-1.13.0`. Fetched 2026-09-24.
- ✅ **Confirmed on a real response:** the §4 run returned `jev-1.13.0` on all 30 calls.
- Every harness now records it (§2 B3 of this document). Runs before `ad73b7e` did not; their resolved model is **unknown** and cannot be recovered.

---

## 6. Decisions queued, with my read

| ID | Decision | Options | My read |
|---|---|---|---|
| `SR-D1` | `FP-Q1` item-level gate (`SR-4`) | (a) refuse the run when any scope candidate lacks a seat line, with `-AllowUnbaselinedItems` as the opt-out — the `FP-D24` pattern · (b) judge only the covered candidates, count the rest as skipped | **(a).** It guarantees more and matches the item gates in all four other harnesses. (b) silently shrinks the population, the class `harness-shadow-mode-protocol.md` §4e names |
| `SR-D2` | `Q29`'s no-answer check was never run (`SR-2`). Its section Nouls were judged, so it is in the ledger | (a) run the no-answer check alone once (a `-NoAnswerOnly` mode; about 150 calls to rebuild the re-ranked top 5 at `-Samples 5`, or 1 call over the BM25 top 5) · (b) leave it; the trial meets new no-answer queries | **(a), over the re-ranked top 5.** The seat has not seen any no-answer output for `Q29`, so it is still a clean measurement of the one trap query in the set. It needs a ledger exception, which is why it is reserved |
| `SR-D3` | `-CountersOnly` exit code (`SR-6`) | (a) always exit 0 in `-CountersOnly` · (b) leave | **(a).** The mode exists to withhold verdicts; the exit code is a verdict |
| `SR-D4` | `CLASSIFIER_SUSPECT` on a clean small window (`SR-7`) | (a) keep the refusal, add an explicit override flag that prints loudly · (b) warn only below N commits · (c) leave | **(a).** Keeps the tripwire; lets a measured `-Since` run proceed on purpose |
| `SR-D5` | `SR-12` and `SR-13` | Validate rider-travel baseline values; print `-Since` in the commit-walker report | Both yes; small; not done here to keep this batch to the brief |
| `SR-D6` | `FP-2` relies on the title (§4) | (a) add a title-stripped `FP-2` arm beside the current one, the `FP-1v2` pattern, measured at the next fixture review · (b) strip titles from `FP-2`'s state · (c) note only | **(a).** It keeps comparability and measures the dependence on real names. (b) changes a measured detector without a re-measure |

---

## 7. What I did not verify

- **Whether any recorded doc-reranker run had firewall-blocked sections** (`SR-3`). Not recoverable without re-spending queries.
- **`SR-11`'s real effect** on any recorded hit count.
- **Two packets were skimmed, not read in full** (56 KB and 29 KB): [`doc-scanner-build-spec-back.md`](doc-scanner-build-spec-back.md) and [`decision-bias-measurement-phase-a-spec-back.md`](decision-bias-measurement-phase-a-spec-back.md). I read their handle lists and their code (`doc-scanner.ps1` and the decision-bias runner in full).
  - ✅ Re-run after my runner edit, at `bb035f2`: `decision-bias-measurement-phase-a-spec-back.md` §2 `H-5` (the three refusals: `BASELINE_MISSING`, `BASELINE_INCOMPLETE`, `BASELINE_REV_MISMATCH`, each `EXIT=2`, no `STATE_DEBUG` and no `CALLS=` line) and §2 `H-2` (`population same`, `outcomes same`, `unruled same`, `excluded same`, `crossrefs same`).
  - ❌ Not re-run: the doc-scanner packet's `H-1`–`H-12`. They are Python measurement commands with no uncommitted instrument named in them, so they read as runnable; I did not check that.
- `lib/doc_scanner_candidates.py`, `lib/doc_sections.py`, `measure/doc-scanner/*` and the decision-bias Python: compiled with warnings as errors and scanned for control bytes. **Not read line by line.**
- `fixture-parser.ps1` (2,854 lines): read at the gates, the `FP-Q1` block, the `FP-2` path and every list-return site. **Not read in full.** The `FP-D21`–`FP-D26` logic is covered only through the committed self-test.
- PowerShell 7 behaviour. Every run here was 5.1.
