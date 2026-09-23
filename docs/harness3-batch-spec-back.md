# Harness 3 batch — spec-back, 2026-09-23 (UTC)

**Batch:** close-list item 4 of the Jev programme — fixes to harness 3, the fixture parser (`tools/checks/fixture-parser.ps1`). **Implementer:** Opus 5.5 (`claude-opus-5-5`), effort medium. **Start commit:** `358c5eb`. **Format:** [`batch-review-packet-convention.md`](batch-review-packet-convention.md). No separate summary document was asked for; this packet carries the outcome too.

| Commit | What |
|---|---|
| `ca566c5` | Harness revision 4: decisions `FP-D27`–`FP-D31` (defined in §0 below) |
| `51ae1aa` | Offline self-test, its synthetic input, and the `FP-2` mutation driver |
| *(this commit)* | Mutation run record, driver prints sample verdicts, spec and review notes, this packet |

---

## 0. Outcome

| Brief item | Result | New decision ID (harness header) |
|---|---|---|
| 1 — review finding 4, bare-name signature lookup | ✅ Done | `FP-D27`: resolve a same-named callee by receiver type, else `AMBIGUOUS_CALLEE` |
| 2 — review finding 8, block-level marker | ✅ Done | `FP-D28`: `SITE_NAMED` / `BLOCK_ONLY` label |
| 3 — review finding 9, arrays skipped | ✅ Done | `FP-D29`: arrays walked by `name` field, `ARRAY_UNKEYED` counted |
| 4 — spec item `8o`, `FP-1v2` beside version 1 | ✅ Armed, not measured | `FP-D30` |
| 5 — `FP-2` mutation test (spec decision `FP-D3`) | ✅ **6 of 6 flagged** | seams: `FP-D31` (`-TestTransportOverride`, `-SiteDumpPath`, `-Fp2Only`) |

- **Escalation triggers:** neither fired. No code-only output changed except as items 1–3 explain. The mutation test flagged 6 of 6, above the "fewer than half" line.
- **Jev spend:** 30 real calls, all in the mutation run. 35,826 input + 2,509 output tokens; about $0.0015. Every other run used no key or the synthetic transport.
- **No unjudged real site was sent to Jev.** The 30 calls were `FP-2` only, on six fixture bodies that past runs had already judged (§1, `E-2`). `FP-Q1` (the scope detector) made 0 calls: no scope baseline was supplied.

---

## 1. Ranked verification handles

`H-n` runs from committed code. `E-n` is build-time evidence a reader cannot rerun as-is. Handles are pinned to `358c5eb` where a diff is involved.

⭐ **If you only run one, run `H-1`.** It needs no key and no network, and it covers items 1, 2 and 4 plus the no-baseline refusal.

### `H-1` — the offline self-test (items 1, 2, 4, refusal)

```
powershell -NoProfile -File tools/checks/selftest/fixture-parser-selftest.ps1
```

Actual output at `ca566c5` (selected lines follow the PASS list in the real output):

```
PASS  A exits 2
PASS  A says BASELINE_MISSING
PASS  A made zero transport calls
PASS  B exits 0 (v2 disagreement does not reach the exit code)
PASS  B made 15 transport calls (2 FP-1 items + 1 FP-2 item, 5 samples each)
PASS  B MAPPING_AMBIGUOUS_CALLEE_SITES=2
PASS  B Snapshot and Compute listed as ambiguous
PASS  B IN_SCOPE_MARKED_SITE_NAMED=1 and IN_SCOPE_MARKED_BLOCK_ONLY=1
PASS  B tfiWindowSize is SITE_NAMED, threshold is BLOCK_ONLY
PASS  B both FP-1 items were judged (BLOCK_ONLY is a label, not a filter)
PASS  B v2 reported beside v1
PASS  B FP1_BAD_VERDICTS=0
PASS  B request body: sentence in exactly declared_but_contradicted + mechanism_declared_ok, v1 untouched
PASS  C exits 1 (version 1 alone drives the exit code)
PASS  C FP1_BAD_VERDICTS=1 while v2 said mechanism_declared_ok
...
  CALLEE_MULTI_DECLARED Snapshot declared=4 sites=1 typed=0 ambiguous=1 receiver_types=[(untyped)] mapping=[AMBIGUOUS_CALLEE]
  CALLEE_MULTI_DECLARED Compute declared=5 sites=1 typed=0 ambiguous=1 receiver_types=[syntheticfixtureparserchecks] mapping=[AMBIGUOUS_CALLEE]
  A902_SyntheticTfiNamed#18#tfiWindowSize [STABLE] verdict=mechanism_declared_ok ... [AGREE] ... marker=SITE_NAMED
      v2: verdict=declared_but_contradicted agreement_rate=1 mean_top_prob=0.7 ... samples_v1_eq_v2=0/5
  A902_SyntheticTfiNamed#18#threshold [STABLE] verdict=mechanism_declared_ok ... [AGREE] ... marker=BLOCK_ONLY
FP1_BAD_VERDICTS=0
FP1V2_ITEMS_SAME_PLURALITY_AS_V1=1 of 2
EXIT_B=0
  A902_SyntheticTfiNamed#18#threshold [STABLE] verdict=undeclared ... [DISAGREE] ... marker=BLOCK_ONLY
      v2: verdict=mechanism_declared_ok ...
FP1_BAD_VERDICTS=1
EXIT_C=1

SELFTEST PASSED
```

- **Check A is the acceptance "no baseline → exit 2, no API call".** It runs on the REAL `Program.vb` with a dummy key present, so the only thing stopping a call is the refusal itself.
- **The synthetic `AMBIGUOUS_CALLEE` cases:** `s.Snapshot(...)`, where `s` is `Dim s = GetThing()` and cannot be typed. And an unqualified `Compute(...)` from a module that declares no `Compute`.

### `H-2` — code-only coverage AFTER (items 1–3)

```
env -u TYPESAFE_API_KEY powershell -NoProfile -File tools/checks/fixture-parser.ps1 -BaselinePath /nonexistent.json -OutPath "$TEMP/r.md"
```

Actual output at `ca566c5`, revision 4 block plus every line that changed:

```
PROD_METHOD_NAMES_MULTI_DECLARED=70
SITES_CALLEE_MULTI_DECLARED=11
SITES_CALLEE_TYPED_BY_RECEIVER=11
MAPPING_AMBIGUOUS_CALLEE_SITES=0
  CALLEE_MULTI_DECLARED Compute declared=5 sites=1 typed=1 ambiguous=0 receiver_types=[dynamicnorms] mapping=[SOURCE_NOT_TRACEABLE]
  CALLEE_MULTI_DECLARED Evaluate declared=3 sites=2 typed=2 ambiguous=0 receiver_types=[livemicrostructureevaluator] mapping=[PARAM_NOT_PASSED_AT_CALL_SITE]
  CALLEE_MULTI_DECLARED Fold declared=2 sites=4 typed=4 ambiguous=0 receiver_types=[aggressorvelocityaccumulator] mapping=[WRAPPER_FORWARDED]
  CALLEE_MULTI_DECLARED Snapshot declared=4 sites=2 typed=2 ambiguous=0 receiver_types=[aggressorvelocityaccumulator] mapping=[WRAPPER_FORWARDED]
  CALLEE_MULTI_DECLARED Build declared=3 sites=2 typed=2 ambiguous=0 receiver_types=[promptbuilder] mapping=[SOURCE_NOT_TRACEABLE]
MARKED_SITES_SITE_NAMED=53
MARKED_SITES_BLOCK_ONLY=26
IN_SCOPE_MARKED_SITE_NAMED=50
IN_SCOPE_MARKED_BLOCK_ONLY=8
  A1_CvdSlopeRising#863#lateSegmentWeight
  A20b_CalcOfiEdgeCasesUnchanged#2652#bookDepth / #buyDominantRatio / #sellDominantRatio
  A23b_AggrVelBurstDetection#3216#tauFastSec / #tauNormSec
  A65c_WideArmIsTestedFirst#12820#tightThresholdBps / #wideThresholdBps
SETTINGS_HEAD_LEAVES=271
SETTINGS_HEAD_LEAVES_WITHOUT_ARRAYS=254
SETTINGS_HEAD_ARRAY_KEYED_LEAVES=17
SETTINGS_HEAD_ARRAY_KEYED_ELEMENTS=3
ARRAY_UNKEYED=0
SETTINGS_HEAD_SCALAR_ARRAYS_SKIPPED=1
SETTINGS_ARRAY_KEYS_EVER_WALKED=17
MAPPING_SOURCE_NOT_TRACEABLE_SITES=14
MAPPING_OTHER_UNRESOLVED_SITES=16
ARRAY_PROOF session_volume.sessions[name=ASIA].high_multiplier ever-shipped = {0.8, 1, 1.1} (revisions carrying the key: 75 of 87)
  nowUtcMs    x2  why=PARAM_NOT_PASSED_AT_CALL_SITE callees=[Evaluate]
  currentATR  x1  why=SOURCE_NOT_TRACEABLE callees=[Compute]
EXIT_REASON=BASELINE_MISSING
```

(The eight `BLOCK_ONLY` IDs are folded onto four lines here; the tool prints one per line.)

### `H-3` — code-only coverage BEFORE, from the start commit

```
git worktree add --detach <tmp>/wt 358c5eb
env -u TYPESAFE_API_KEY powershell -NoProfile -File <tmp>/wt/tools/checks/fixture-parser.ps1 -BaselinePath /nonexistent.json -OutPath <tmp>/r.md -SettingsCachePath <tmp>/c.json
git worktree remove --force <tmp>/wt
```

Actual output: identical to the start-of-batch run in place, except the wall-clock line. The lines that differ from `H-2`:

```
MAPPING_SOURCE_NOT_TRACEABLE_SITES=13
MAPPING_OTHER_UNRESOLVED_SITES=16
  nowUtcMs    x2  why=PARAM_NOT_IN_SIGNATURE callees=[Evaluate]
  currentATR  x1  why=PARAM_NOT_IN_SIGNATURE callees=[Compute]
```

**Every before → after difference, explained:**

| Difference | Cause |
|---|---|
| `A5_NormsRecentWindow#1042#currentATR`: `PARAM_NOT_IN_SIGNATURE` → `SOURCE_NOT_TRACEABLE` | Item 1. Before: read against `analysis/BandLadder.vb`'s `Compute`. After: read against `DynamicNorms.Compute`, whose production caller passes `r.ATR`, a member of another object |
| `A19c#2531` and `A19d#2551` `nowUtcMs`: `PARAM_NOT_IN_SIGNATURE` → `PARAM_NOT_PASSED_AT_CALL_SITE` | Item 1. Now read against `LiveMicrostructureEvaluator.Evaluate`, whose one production caller omits the argument |
| `MAPPING_SOURCE_NOT_TRACEABLE_SITES` 13 → 14 | The `currentATR` row above |
| `MAPPING_OTHER_UNRESOLVED_SITES` 16 → 16 | Both old and new classes are in that set |
| New revision 4 block, `marker=` suffix on `FP1_CANDIDATES`, `ARRAY_PROOF` line | Items 1–3's new reporting |
| `SETTINGS_HEAD_LEAVES` 254 → 271 | Item 3: exactly the 17 array-element leaves |
| All other counters, including `LITERAL_CALL_SITES=109`, `IN_SCOPE_SITES=58`, `KEYS_WITH_VALUE_HISTORY=26`, every `FPQ3_PROOF` line | Unchanged |

### `H-4` — `Program.vb` untouched by the whole batch

```
git diff --stat 358c5eb -- verify/ordercheck/Program.vb
```

Actual output: empty.

### `H-5` — the `FP-1v2` sentence is the measured one

```
grep -c "Naming \`matched_key\` or its shipped value ONLY to explain why the literal is NOT derived from it, or to show the literal differs from it or does not depend on it, is consistent with MECHANISM and is NOT a contradiction." tools/checks/fixture-parser.ps1 docs/harness-runs/fixture-parser-funding-run-2026-09-23.md
```

Actual output: `tools/checks/fixture-parser.ps1:1` and `docs/harness-runs/fixture-parser-funding-run-2026-09-23.md:1`. Its placement in two criteria is `H-1` check B.

### `H-6` — the mutation test (KEYED, about $0.0015 a run)

```
set -a; . ./typesafe.local.env; set +a
powershell -NoProfile -File tools/checks/selftest/fixture-parser-fp2-mutation.ps1
```

- Actual output: the table in [`harness-runs/fixture-parser-fp2-mutation-2026-09-23.md`](harness-runs/fixture-parser-fp2-mutation-2026-09-23.md) §2.
- ⚠ A rerun draws new samples. Compare "flagged ≥ 3 of 6", not the exact probabilities.

### Evidence a reader cannot rerun as-is

| ID | Evidence | Why it is not an `H-n` |
|---|---|---|
| `E-1` | The `FP-1v2` sentence is byte-identical to the probe script's `$extra`, and the probe appended it to `mechanism_declared_ok` AND `declared_but_contradicted` | The probe script lives in another session's scratchpad (`142340dd-…/scratchpad/fp1_probe.ps1`), never committed. `H-5` covers the text through the committed run record; the run record does not record placement |
| `E-2` | The exact mutation table (6 of 6; `A65c` UNSTABLE at 0.6) | Stochastic; `H-6` reproduces the procedure, not the numbers |
| `E-3` | Independent Python walk of all 87 `settings.json` revisions: ASIA `high_multiplier` ∈ {0.8, 1.0, 1.1} in 75 revisions; 17 distinct session keys, NY lacking `roc_magnitude_threshold` | A scratch heredoc; it agrees with `H-2`'s `ARRAY_PROOF` |
| `E-4` | Per-site dump diff, before vs after: exactly the 3 sites in `H-3`'s table changed; the other 106 are identical in mapping, key, marker and scope | "Before" came from an instrumented scratch copy of the start-commit harness. `-SiteDumpPath` is committed now, so the "after" half is `H`-runnable |

---

## 2. Decisions — logged, then queued

### 2.1 Every decision I took myself, one line each

| # | Decision | Options | Took | Why |
|---|---|---|---|---|
| 1 | Where the item-1 declaration regex applies | broad everywhere · broad for the new index only | **Index only** | Widening `Get-FileEnclosingProc` could move `EnclosingSub` and so change item IDs. The index needs the broad form because `Private Async Function RunAnalysisAsync` holds most production call sites |
| 2 | An unqualified production call | type as the enclosing class · untyped unless that class declares the name | **Untyped unless declared** | VB resolves it to an imported module otherwise, so "enclosing class" would be a wrong type, not a missing one |
| 3 | Production sites of a typed callee whose receiver cannot be typed | keep · drop · drop and report `AMBIGUOUS_CALLEE` if nothing typed survives | **Drop; `AMBIGUOUS_CALLEE` if none survive** | Step 3 of `CLAUDE.md`'s three-step test: keeping them is the finding-4 guess itself |
| 4 | What `SITE_NAMED` matches | parameter name · parameter or its settings key | **Parameter only** | The finding is about the parameter, and this reproduces its "8 of 58" exactly. The four blocks checked by hand name `wide:=1.0`, `tight:=2.0` or a snake key, not the parameter |
| 5 | Array keys in `FP-Q1`'s Jev state (via the name index and fuzzy candidates) | include · freeze to the old key set | **Freeze** | Step 3: the richer option changes a measured detector's input without a re-measure — the break item `8o` names. 0 of today's parameters are affected either way |
| 6 | The array element's `name` field | emit as a leaf · omit | **Omit** | It is the key, not a value |
| 7 | Duplicate or unsafe `name` values (containing `.`, `[`, `]` or `=`) | index · `ARRAY_UNKEYED` | **`ARRAY_UNKEYED`** | Either would make an ambiguous path |
| 8 | Stale settings cache | trust · schema tag and re-walk | **Schema tag** | A cached flattening from before item 3 has no array keys and would read as "never shipped" |
| 9 | The `FP-1v2` sentence | item `8o`'s paraphrase · the probe's measured sentence | **The probe's, verbatim, in the probe's two criteria** | Only the measured text carries the measured effect |
| 10 | A `v2`-versus-seat agreement tag | add · omit | **Omit** | Step 3, forbidden by a prior ruling: the brief says version 1 alone drives the baseline comparison. The seat can compare by eye. **Queued as §2.2 Q2** |
| 11 | `FP-1v2` counters under `-CountersOnly` | print · withhold | **Withhold** | They aggregate verdicts, which `harness-shadow-mode-protocol.md` §4a forbids on a reserved window |
| 12 | How to run `FP-2` alone | a scratch script · a committed `-Fp2Only` switch | **Committed switch** | It keeps the mutation test repeatable, and no `FP-1` call is spent |
| 13 | Mutation method | edit `Program.vb` and restore · mutate a copy via `-SourceFile` | **Copy** | The brief's stated preference. `Program.vb` is never at risk |
| 14 | Mutated name shape | another fixture's full name · keep the ID prefix, borrow the descriptive part | **Keep the prefix** | A full borrowed name collides, and the harness finds a Sub's body by name, first match wins |
| 15 | Which six fixtures | any `name_matches` agreement · only those with recorded STABLE agreement | **Recorded STABLE only** | The funding run does not record per-row stability for its agreements. `A4` was also passed over for its 0.434 top probability |
| 16 | Mutation baseline value | `name_overclaims` · `unsure` plus a `Flagged` column | **`unsure`** | The truth is "not `name_matches`". Both `name_overclaims` and `name_understates` cover "a different property", so no single value states it |
| 17 | Self-test and mutation driver | scratch (`E-n`) · committed (`H-n`) | **Committed** | The packet rule wants `H-n` handles. The `.vb.txt` extension keeps the synthetic input out of every build and out of the harness's own `*.vb` scan |

### 2.2 Queued for the seat

| # | Decision | My read |
|---|---|---|
| Q1 | Rerun the mutation driver once to capture `A65c`'s sample split? It costs about $0.0015 and spends no fresh population | **Hypothesis: yes, cheap.** The split decides whether `A65c` is a weak detection or a partial miss. The driver now prints the split |
| Q2 | Should the re-measure of `FP-1v2` print a `v2`-versus-seat tag? | **Yes, for that run.** I left it out only because the brief ruled version 1 alone drives the baseline comparison |
| Q3 | ⚠ **Found in passing, not fixed.** `Get-FileEnclosingProc`'s narrow regex does not see `Async`/`Overrides` methods. Production sites inside `RunAnalysisAsync` therefore get NO enclosing procedure. Their alias search then scans the WHOLE file, and the forwarding hop never sees them as wrappers | **A latent wrong-alias risk.** Today's `ALIASED` proofs are right. Widening changes outputs, so it needs its own measured before/after. Only production-file ranges would change; `Program.vb` has no broad-only declarations (grep, 2026-09-23 UTC) |
| Q4 | Decision `FP-D21`'s default-fallback lookup still refuses any name declared twice. It could use item 1's typed declaration instead | **Low value.** The one case it now reaches, `nowUtcMs`, falls back to a clock, not cfg |

---

## 3. Spec-back — the brief itself

**What worked**
- Trap 2 (restore) plus "mutate a COPY if the tool accepts a path" made the mutation test zero-risk.
- "Show fewer than half as a finding, not something to tune" pre-committed the reading before the run.

**Assumptions that broke**
- **"The `-TestTransportOverride` seam `InvokeJev.ps1` now has."** `Invoke-Jev`'s parameter is `-TransportOverride`. The `-TestTransportOverride` name exists only in harnesses 1 and 2. Harness 3 had neither, so I added it (`FP-D31`).
- **Item `8o`'s quoted sentence is a paraphrase.** The measured sentence is longer. The run record does not say where the probe placed it; the probe's scratch script did (`E-1`).
- **"Show one array key that now resolves."** No real call site can reach an array key: harness 3's `FP-Q3` mapping stops at `(`. The key resolves in the value walk (`ARRAY_PROOF`), not through a mapping.

**Narrower than its words**
- `docs/fixture-parser-check-spec.md` §5 acceptance item 8 still said *"restore via `git checkout`"*. The brief's trap 2 supersedes it; spec row `8s` now says so.

**A constraint pair that nearly conflicted**
- "`FP-1v2` in the SAME call" and "version 1 stays comparable with every earlier measurement" hold together only if Jev answers each question in a call independently. That is unverified (§4).

---

## 4. What I did not verify

- ⚠ **Whether adding `verdict_v2` to the call moves version 1's answers.** If questions in one call interact, version 1 is no longer strictly comparable with earlier runs. Measuring it needs keyed calls on spent items; the brief allowed Jev only for the mutation test.
- **`A65c`'s sample split** (§2.2 Q1).
- **Whether `FP-2` reads the NAME or the body's `Check` titles.** The titles still carry the original names ([`harness-runs/fixture-parser-fp2-mutation-2026-09-23.md`](harness-runs/fixture-parser-fp2-mutation-2026-09-23.md) §3).
- **Receiver typing on forms the real file does not use:** `With` blocks, generic receivers, and `CType(...)` receivers. They are reported untyped by construction, not tested.
- **The $0.0015 cost.** Derived from the rate implied by an earlier run record, not a price list.
- **Harnesses 1, 2 and 4 are untouched,** and `tools/checks/lib/InvokeJev.ps1` is unchanged. Not re-run here.
