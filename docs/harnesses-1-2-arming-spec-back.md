# Harnesses 1 and 2 arming — spec-back

**Batch:** apply the `FP-D26` firewall pattern (item `8n`) and finding 11's retry fix to
`tools/checks/rider-travel.ps1` (harness 1) and `tools/checks/commit-walker.ps1` (harness 2),
and finding 6's OK-class-misread fix to both. Per `docs/batch-review-packet-convention.md`.

**Start commit (session start):** `5556576`. **Pre-my-commit HEAD after the harness-6
phase-A agent landed its own, unrelated commits:** `2fd639a` (`git diff --stat` confirmed
only `tools/checks/commit-walker.ps1`, `tools/checks/lib/InvokeJev.ps1`,
`tools/checks/rider-travel.ps1` are mine and uncommitted; the phase-A commits touch only
`tools/checks/measure/decision-bias/` and `docs/`). Handles below are pinned to `2fd639a`.

---

## 1. Ranked verification handles

### H-1 (runnable) — harness 3/4 baseline-refusal regression, before/after

```
powershell -NoProfile -File tools/checks/doc-scanner.ps1 -Rev cbc2c91 -BaselinePath /no/such/file.json
powershell -NoProfile -File tools/checks/fixture-parser.ps1 -BaselinePath /no/such/file.json
```

Both exit 2, `BASELINE_MISSING`/`EXIT_REASON=BASELINE_MISSING`, no API call, before and
after this batch's changes. Diffed byte-for-byte: **doc-scanner — no diff.**
**fixture-parser — one line differs, `SCOPE_WALL_TIME_SEC` (0.07 vs 0.08), an inherent
wall-clock field.** No other line moved. This is acceptance item 4.

### H-2 (runnable) — offline retry/backoff/no-retry test on `Invoke-Jev` itself

A synthetic script (`test-retry.ps1`, not committed — scratch) dot-sources
`tools/checks/lib/InvokeJev.ps1` and calls `Invoke-Jev` with `-TransportOverride`
scriptblocks that never touch the network. Actual output:

```
[A: transient-then-success] Ok=True Status=200 WafBlocked=False RetryCount=2 Elapsed=0.18s
  callCountA=3 (expect 3: 2 failures + 1 success)
[B: 403-WAF-blocked] Ok=False Status=403 WafBlocked=True RetryCount=0 Elapsed=0.01s
  callCountB=1 (expect 1: never retried)
[C: 400-bad-request] Ok=False Status=400 WafBlocked=False RetryCount=0 Elapsed=0.01s
  callCountC=1 (expect 1: never retried)
[D: always-transient-exhausts-retries] Ok=False Status= WafBlocked=False RetryCount=2 Elapsed=0.01s
  callCountD=3 (expect 3: 1 initial + 2 retries, MaxRetries=2)
```

All four match the acceptance criterion: transient (5xx / no response) retries with
backoff and eventually succeeds or exhausts `-MaxRetries`; a 403 and a generic 4xx never
retry. Reproducible by any reader who writes the same four `-TransportOverride`
scriptblocks against the committed `InvokeJev.ps1`.

### H-3 (runnable, end-to-end, harness 1) — WAF-block path, item 8n

Synthetic ledger (`ledger-waf.md`, one `TRAVELLING` rider whose text contains the known
trigger phrase *"and 4 + 7 = 11"*), synthetic `-AfterFile` (real HEAD header +1 column,
so `COLUMNS_ADDED>0`), `-TestTransportOverride` blocking on that rider's id in the
outgoing JSON bytes — never a real network call, never `TYPESAFE_API_KEY`. Actual output
(trimmed):

```
RIDERS_WAF_BLOCKED=1
PER_RIDER_RESULTS:
  TEST-WAF-1 [JEV STABLE] verdict=WAF_BLOCKED ... baseline=unsure [NOT_JUDGED] ...
EXIT_CODE=1
```

Run continues past the block, item is `WAF_BLOCKED`/`NOT_JUDGED`, exit code 1. Matches
acceptance item 1 for harness 1.

### H-4 (runnable, end-to-end, harness 1) — retry + finding-6, items 11 and 6

Same rig, two riders: one whose first Jev call is a simulated transient 503 (retried by
`Invoke-Jev` itself, then succeeds), one that always succeeds; both answer
`not_a_header_column`, stable. Actual output (trimmed):

```
USAGE_INPUT_TOKENS=222
RETRY_COUNT=1
RIDERS_WAF_BLOCKED=0
RIDERS_NOT_A_HEADER_COLUMN=2 (of which STABLE=2 -- reported per ... section 4g; ... not folded into the exit code)
EXIT_CODE=0
```

`RETRY_COUNT=1` confirms the one simulated transient failure was retried and counted.
`RIDERS_NOT_A_HEADER_COLUMN=2` is the new finding-6 line; exit is still 0 — it does not
force a fail. Matches acceptance items 2 (harness-level) and 3 for harness 1.

### H-5 (runnable, end-to-end, harness 2) — WAF-block path, item 8n

Real (read-only) git log, `-Count 60`, which has exactly one residual commit
(`526ecf251f...`, tagged `[no-engine-change]` but touching an engine path). Baseline
`{"526ecf...": "unsure"}`. `-TestTransportOverride` always returns a simulated 403/WAF
block. Actual output (trimmed):

```
COMMITS_WAF_BLOCKED=1
ESCALATION_WAF_BLOCKED=0 (informational -- the primary's own stable verdict still stands...)
  526ecf251f [TAGGED_BUT_ENGINE_PATH] status=WAF_BLOCKED verdict=WAF_BLOCKED ... disagreement=NOT_JUDGED ...
WAF_BLOCKED_LIST (item 8n -- unjudged, never a pass, makes the exit code 1):
  526ecf251f ...
EXIT_CODE=1
```

Run continues, commit recorded `WAF_BLOCKED`, exit 1. Matches acceptance item 1 for
harness 2.

### H-6 (runnable, end-to-end, harness 2) — retry + finding-6, items 11 and 6

Same commit, baseline `{"526ecf...": "no_app_change"}`. `-TestTransportOverride`: first
call ever is a simulated transient 503 (retried, then a high-probability `no_app_change`
answer on every subsequent sample — never escalates). Actual output (trimmed):

```
RETRY_COUNT=1
COMMITS_WAF_BLOCKED=0
CONSISTENT_COUNT=1 (reported per ... section 4g; ... not folded into the exit code)
  526ecf251f [TAGGED_BUT_ENGINE_PATH] status=CONFIDENT verdict=no_app_change agreement_rate=1 ... disagreement=CONSISTENT baseline=no_app_change [AGREE] ...
DISAGREEMENTS=0
UNSTABLE=0
CONFLICT=0
EXIT_CODE=0
```

`RETRY_COUNT=1` and `CONSISTENT_COUNT=1` both print; exit is 0. Matches acceptance items
2 (harness-level) and 3 for harness 2.

### E-1 (evidence, not runnable) — confirmation no real population was spent

`echo $TYPESAFE_API_KEY` was empty throughout this session (checked before any change).
Every harness-1/2 run above passed `-TestTransportOverride`, which `Invoke-Jev` consults
**before** building any HTTP request or reading the real key — the fake key string set in
each test wrapper (`'test-fixture-fake-key-not-real'`) was never sent anywhere. Harness
1's real 8-rider population (`docs/csv-rotation-riders.md` §1) and harness 2's real
measurement window were never referenced by any test file (synthetic ledgers and a
synthetic `-AfterFile` were used for harness 1; harness 2 used real, already-public commit
history read-only, with **fully synthetic verdicts**, never a real Jev call). Total Jev
calls made this session: **0**. Total tokens/cost: **$0**.

---

## 2. Decisions queued, with my read

- **Finding 6 done differently from item `8m`** (report-only counters instead of moving
  the verdict into the bad-verdict/exit-code set). **My read: correct, not a deviation
  to escalate.** `8m`'s `shipped_declared_ok` is provably always wrong wherever it fires
  (an `FP-1` site is always a hardcoded literal); `not_a_header_column`/`CONSISTENT` are
  not provably anything, and are the *common, legitimate* verdict for several real riders
  (`RIDER-1`, `RIDER-2`) and for most of a residual. Copying `8m`'s mechanism verbatim
  would make a normal run of either harness exit 1 by construction — exactly the trap the
  brief's own item 3 named. Auto-proceeded per `CLAUDE.md`'s auto-proceed ruling (a
  reversible, no-live-surface, no-data-effect choice); logged here per its obligation.
- **Escalation-call WAF blocks in harness 2** (not explicitly speced): treated the same as
  a primary-call block, except the primary's own already-sampled, stable verdict stands
  (status `AGREE_LOW_PROBABILITY_ESCALATION_WAF_BLOCKED`, counted in
  `ESCALATION_WAF_BLOCKED`, informational, never forcing the exit code on its own). My
  read: the escalation call carries the same commit text (plus a diff) as the primary, so
  it can trip the same firewall; leaving it unhandled would have reintroduced exactly the
  "aborts the whole run" defect item 8n exists to close, just one call later. Auto-
  proceeded on the same basis as above.
- **Retry constants**: `$MaxRetries = 3`, backoff `2^attempt * 1s` (1, 2, 4s). Not
  specified in the brief beyond "bounded ... with backoff". My read: matches this
  programme's existing style of small, named, re-tunable constants (`$SELF_CONSISTENCY_SAMPLES`,
  `$ESCALATION_SAMPLES`); no measurement exists yet to tune it further.
- **Test seam shape**: a `-TestTransportOverride` scriptblock parameter, threaded through
  to `Invoke-Jev`'s own `-TransportOverride`, added to all three files. Off by default
  (`$null`), never touched by a real run. Alternative considered: a global variable seam
  (no signature change) — rejected because an explicit parameter is self-documenting in
  `Get-Help`/the file itself, and this repo's fixture-literal-provenance rule already
  prefers explicit-and-checkable over implicit-and-convention-based.

---

## 3. Spec feedback

- The brief's item 8m analogy ("Make these reportable, the way item `8m` did for harness
  3") undersells the fork in the road at trap 3 — `8m` is BOTH "reportable" AND "exit-code-
  changing", and only the first half transfers safely here. Worth stating explicitly in
  a future brief that these are two separable properties of `8m`, not one.
- Escalation-level WAF blocking in harness 2 was not named anywhere in the brief or in
  `docs/jev-harnesses-adversarial-review-2026-09-22.md` finding 8n/11 — reasonable, since
  the measured 403 that motivated `FP-D26` was on harness 3's primary call only. Flagging
  it here so a reviewer can confirm the design (§2 above) rather than discovering it cold.

---

## 4. What I did not verify

- **A real firewall block on harness 1 or 2's actual population.** Never attempted
  (trap 1 forbids it); all WAF evidence is via `-TestTransportOverride`.
- **The real retry behavior against the live `api.typesafe.ai` endpoint** — the retry
  loop's HTTP-status branching was verified against fabricated `WebException`-shaped
  results, not a real 503/timeout from the live service.
- **Whether `$MaxRetries=3`/backoff `1,2,4s` are the right constants for this API's real
  failure profile.** No transient failure has been observed in this programme's own
  history; these are a starting point, same as `$SELF_CONSISTENCY_SAMPLES` was.
- A **pre-existing, unrelated defect noticed while building the harness-1 synthetic test**:
  `tools/checks/rider-travel.ps1`'s `Get-LedgerRows` returns a `System.Collections.Generic.List[object]`
  via `return $rows`; when that list holds **exactly one** row, PowerShell's pipeline
  unwraps it to a bare scalar on the way out of the function (and again across the
  `$travelling = if (...) {...} else {@(...)}` assignment), so `$ridersInLedger`/
  `$ridersTravelling` silently print `0` (via `Write-Coverage`'s `[int]` parameter
  coercion of `$null`) even though the row is real and correctly processed downstream
  (`RIDER_CANDIDATES`/`PER_RIDER_RESULTS` are unaffected — only the two coverage counters
  misprint). Reproduced in isolation; confirmed harmless to the exit-code gate only
  because `$null -eq 0` is `$false` in PowerShell. **Out of scope for this batch** (not
  one of the three assigned fixes; pre-existing; the real ledger has always had ≥2
  travelling rows so it has never fired in practice) — flagged separately rather than
  fixed here.
