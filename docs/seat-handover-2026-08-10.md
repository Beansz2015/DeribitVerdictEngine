# Seat handover — 2026-08-10 (orchestrator seat)

**From:** the Opus orchestrator seat that opened on [`seat-handover-2026-08-05.md`](seat-handover-2026-08-05.md) and ran the three-doc reconciliation, the first AWS store copy-back, the withdrawn completeness claim, the trade-identity spec and review, and the AWS deploy.

**Read in this order:** CLAUDE.md session-start protocol (**step 6 is the state rule**) → [`trader-tick-queue.md`](trader-tick-queue.md) **§0a first — what is OWED** → this doc → **§0 below, which is your first task.**

> **The one thing to carry, and it is the same one the last seat wrote down.** *Check before REPORTING, not before concluding.* This session broke it once, expensively: I reported that AWS was missing 21 % of trades, with a table and a worked example. **It was wrong.** The trader challenged it on a one-line intuition — *a box in Deribit's datacentre should hold more trades than a laptop, not fewer* — and the re-check took ten minutes. I had an explanation ready for why the number was low, which is precisely the moment to check harder rather than explain. **A ready explanation for a surprising result is a warning sign, not a resolution.**

---

## 0. YOUR FIRST TASK — read the D3 ASIA watch

**Model: Opus. Effort: high.** It gates a ⚠ scoring change (D2), and a wrong read is expensive and hard to notice.

**Why it is first.** The watch has accrued since **2026-08-01 19:02:31 UTC** and **has never been read**. It is the single thing blocking **D2** (OBV `trend_gate` 18→~23), which is derived, ready, and has been waiting since 2026-08-01.

**Do a fresh AWS copy-back first.** The book in hand ends **2026-08-07 14:06** — three days stale. Procedure: [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) **§4b**, which I wrote after executing it. It also brings home the **first identified tape** (see §1 below).

**What is already in hand, verified 2026-08-10:** 960 ASIA rows since arming, across **6 session-days (08-02…08-07), five of them weekdays**, with `AggrVelBurstRatio` populated on **952 of 960**. So the read is runnable today and better after the copy-back.

**The trigger values:** fire rate **≈9.7 %**, same-side **≥85 %**, on ASIA rows. Source: [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) §5.

⚠ **Four traps, all of which will silently produce a wrong answer:**

1. **Read over a MULTI-DAY band, never one session-day.** ASIA's per-day fire rate spans **4.5–13.8 %** at T=5.5, because row density is only ~106 AggrVel rows/day. A single day proves nothing.
2. **NY's ±2pp band does NOT transfer to res-3.** Do not re-fit any res-3 threshold off one session-day.
3. ⚠ **v65 spans FIVE InstanceIds, and the obvious filter drops most of the data.** AWS `09c747f8…` → `ec487909…` → `d8678d2b…`; local `3916540f…` → `ad7cadf4…`. **All five are v65 and all five have ASIA armed.** There is no settings-version column, so the ledger in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a is the only mapping. Filtering on the original pair silently drops everything after 2026-08-07.
4. **Weekday-only evaluation.** Capture is 24/7; evaluation is not.

**The honest caveat that must travel into whatever you conclude:** the W6-4 ceiling audit gave the only outcome-linked read on this knob — `AggrVelBurstRatio` AUC **0.5179** (n=217). Essentially no demonstrated edge. It does not refute arming, but a watch PASS on distributional grounds must not be reported as if outcomes agreed.

---

## 1. State — verified in the tree 2026-08-10, with how to re-check

| Fact | Value | Re-check |
|---|---|---|
| Settings version | **v65**, tracked, unchanged since 2026-08-02 | `Get-Content settings.json -TotalCount 2` |
| Push state | **ahead 1** at handover | `git status -sb` — never inherit |
| Next free fixture family | **A54** (A53h is high-water) | `Select-String verify/ordercheck/Program.vb -Pattern '\bA[0-9]{2}[a-z]_'` |
| Next free hard constraint | **HC28** (HC27 high-water) | `Select-String tools/AutoTweaker/*.vb -Pattern 'HARD CONSTRAINT (\d+)'` |
| AWS collector | **live, InstanceId `d8678d2b…` since 2026-08-10 14:08:39.770Z** — the trade-identity deploy | `ws_health.log` at next copy-back |
| **AWS is the SOLE capturer** | Local capture is OFF in both `bin\Debug` and `bin\Release`; overlays verified present in each | `Test-Path bin\*\net8.0-windows\settings.local.json` |
| AWS book in hand | ends **2026-08-07 14:06** — three days stale | tail `analysis_log_aws.csv` |
| Store provenance | **one source per file:** July 118,775 rows = pure REST fetch · August 228,163 rows = pure AWS capture | `backtest_data\` |
| Kelly dated trigger | **237 of 406** pooled weekday STRONG. Measured rate **9.5/weekday**, not the 12.4 the watch assumed, so **ETA ~2026-09-01**, not 08-30 | recompute at the next copy-back |

⚠ **The store now holds MIXED-SHAPE files.** Rows before InstanceId `d8678d2b…` are five-field and identity-less; rows after carry `trade_id` + `trade_seq`. `trades_2026-08.csv` keeps its five-field header until the September rollover — `LegacyHeaderLine` exists so the reader accepts both. **This is expected, not a defect.**

---

## 2. What happened this session

**Shipped and deployed:** **trade identity** (`trade_id` + `trade_seq`) — specced, built by a separate implementer session (`64d41e7`), reviewed and ACCEPTED by this seat ([`trade-store-trade-identity-review-2026-08-08.md`](trade-store-trade-identity-review-2026-08-08.md)), deployed to AWS 2026-08-10. **The first AWS store copy-back ever performed**, with the procedure written up as [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §4b, which had none.

**Reconciled:** [`roadmap.md`](roadmap.md), [`trader-tick-queue.md`](trader-tick-queue.md) and [`backlog-dependency-map.md`](backlog-dependency-map.md), each verified against the tree rather than against its own prose. **The dependency map now carries NO state at all** — only edges and a pointer to where state lives — because its State column was a copy and seven cells were stale.

**Ruled:** **D1-a** — local capture ON as a second sampler (2026-08-07), then **REVERSED** (2026-08-08) when its premise was withdrawn · **A4 scoping** — a missed weekend cascade is not a defect, but **do not weekday-filter the gate itself** · **Q1 identity-first dedup**, ratified while the affected population was still zero.

**Withdrawn:** the **78.8 % completeness claim** — see §6.

---

## 3. What is open

**Read [`trader-tick-queue.md`](trader-tick-queue.md) §0a.** At handover, **owed by the trader:** D2 (blocked on your first task), E5 absorption Path B, the F3 watch decision, and C1-coverage F2's split-hour rule.

⚠ **The live strategic decision, and it is unresolved: the AWS cost path.** The free tier expired. Three options were analysed and none chosen:

| Option | Finding |
|---|---|
| **Part-time running** | Viable. **Tape gaps under 20 h self-heal** (`gap_repair_lookback_hours`). ⚠ **Prerequisite: intentional-downtime scoping**, or the coverage report flags a defect every day and becomes noise |
| **Downgrade the instance** | ⚠ **Measured: the app is 86 MB; Windows is ~1474 MB.** You are paying for Windows to host an 86 MB process. t3.micro is plausible but unconfirmed; t3.nano very unlikely |
| **CLI port to Linux** | The real answer, and already objective O3. Four Opus stages. **Its Stage 3 was amended this session** to make call-order preservation provable |

**If part-time is chosen, the right window is 00:00–12:00 UTC** — ASIA + LONDON, which is what AWS exists for per [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §3. The tempting 08:00–20:00 window keeps 79 % of STRONG but **kills ASIA entirely and ends the D3 watch**.

---

## 4. Flagged, not ruled

- **F2 — `ResetBufferState` drops trades in a narrow race** (`DeribitWsFeed.vb:298`, ~4 lines, one `SyncLock`). **Its value changed this session.** It is a known trade-dropper in the streaming path, and we have just built the instrument to detect that class of loss. Well-sequenced to fix now.
- **The S0 venue-diff job stays SUSPENDED.** Q3 ruled it re-specs only after the deploy produces **several days** of identified rows. At handover there are hours.
- **`trade_seq` gap detection is shipped but barely used.** It gives gap detection **from the store alone** — no venue call, no ~24 h retention limit. That retires the urgency behind a *daily* S0 cadence, and the new cadence should be derived, not inherited.
- **Two quarantined tape books**, kept under the retention rule, in `AWS-copybacks\`: the mixed merge I created and reversed, and the local D1-a tape. **Neither may be merged** — both are identity-less.

---

## 5. Conventions established this session

1. **A doc must not carry state that lives somewhere else.** The dependency map's State column was a copy; seven cells were stale, one of them a row the queue's own sweep had already corrected. It now carries edges and pointers only.
2. **Spent sections get ARCHIVED, not deleted** — `history-archive.md` §H holds the roadmap's superseded planning sections verbatim. The roadmap is the history future orchestrators reference.
3. **For an irreversible operation, name what goes rather than what stays.** The AWS deploy list became a **positive allowlist of six items** after a denylist was wrong twice in two days — once omitting `settings.local.json`, which would have **stopped AWS capturing**.
4. **Count rows before and after any merge, and require every number to rise.** Two destructive mistakes were caught this way and neither reached disk.
5. **A declared schedule is a positive record of intent; a statistical baseline is not.** This is what makes intentional-downtime scoping compatible with the J-B ruling that rejected uptime baselines.

---

## 6. Things I got wrong, recorded plainly

1. ⚠⚠ **I reported that AWS was 78.8 % complete and had lost 16,459 trades. It was wrong, and I published it into four documents before it was caught.** Zero timestamps were absent from AWS; the two books disagreed on *amounts at shared timestamps*. **The root cause was real and larger than my error** — the store had no trade identity, so no whole-row comparison between two books could mean anything. That produced the trade-identity build. **But the finding was luck, not method:** the trader's challenge is what forced the re-check.
2. **My first merge would have destroyed ~44,000 trades.** `sort -u` **with a key** dedups on the key, not the line, and 10,199 timestamps carry more than one distinct trade. Caught only because the row count went *down*.
3. **I reported a "zero rows in common" result that was an artefact of my own `awk` stripping `\r`.** It contradicted an earlier run; I debugged rather than picking one.
4. **I wrote "delete the overlay from both `bin\Debug` and `bin\Release`"** — which would have made the deploy source repopulate `backtest_data\` on every run, and an overwriting xcopy would then have pushed ~150,000 fewer rows over AWS's tape. Corrected the same day, before it shipped.
5. **My spec's §3.4 defined a dedup relation that is not transitive.** The implementer found it, chose a resolution and asked for ratification. **That was my error in the spec, not theirs in the build.**
6. **I said "I am not that review"** when I am in fact the only orchestrator. The trader corrected me.

---

## 7. What I did not verify

- **Anything on the AWS box.** Every AWS fact in this document is the trader's report, recorded as fact.
- **The trade-identity fixture diff, line by line.** I confirmed the eight A53 fixtures exist, are named for the right properties, and that the suite passes at 265 checks. **A fixture asserting the wrong thing would have passed my review.** The implementer's own mutation test is the instrument for that and I did not run it.
- **The §1 gate against the live API.** The observed field types are taken on report.
- **Which feed is closer to the truth**, and whether AWS misses trades at all. That is now *answerable* — the identity arm can settle it — but nobody has run it.
- **Any AWS cost or memory figure** beyond the two commands the trader pasted.
