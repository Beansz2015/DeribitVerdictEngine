# Spec-back — trade identity in the trade store (`trade_id` + `trade_seq`)

**Reports against:** [`trade-store-trade-identity-proposal.md`](trade-store-trade-identity-proposal.md) (APPROVED 2026-08-08, D1–D7 all ticked as recommended).
**Built:** 2026-08-08 · **Commit:** `64d41e7` · **Model/effort:** Opus, high (as the spec's §0 recommended).
**Format:** [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Single-lane build, so this is the packet only — the outcome record is the `docs/DeribitIndicatorProject.md` §15 row, not a second file.

> ⚠ **READ FIRST — the deploy question this packet exists to gate.**
> The build is local-commit only. **AWS has not been redeployed, and until it is, AWS keeps writing five-field rows.** That tape is permanently unmergeable, for the same reason as the quarantined books. The spec's own §5 sequencing note is correct and binding. Deploy steps: [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §1.2 and §1b.
>
> ⚠ **D7 ticked re-OPENS the local-sampler question now that identity ships. It is NOT authorisation to resume local capture.** Local capture stays OFF until an explicit decision. Recorded because a future seat reading "D7 ticked" will otherwise read it as a green light — the spec's §7 warns about exactly this.

---

## 0. §1 verification gate — the result, recorded either way as §1 requires

**PASSED. The escalation fork did not fire.** Both feeds carry both fields.

| Feed / endpoint | `trade_id` | `trade_seq` |
|---|---|---|
| `public/get_last_trades_by_instrument` (REST, count-based) | present — JSON **string** `"439922657"` | present — JSON **number** `295960019` |
| `public/get_last_trades_by_instrument_and_time` (REST, time-range) | present, same shape | present, same shape |
| `trades.BTC-PERPETUAL.100ms` (WS channel) | present, same shape | present, same shape |

**Cross-feed value match — observed directly, which §9 asks to be stated explicitly.** WS notification carried `trade_id` 439922712–439922717 with `trade_seq` 295960045–295960050. A REST call fired immediately after returned the same six ids, the same seqs, the same timestamps and the same price/amount/direction. **§0 trap 3 is retired on evidence, not on assumption.**

**Two observations that changed the build:**

- **`trade_id` is a STRING.** It looks numeric and is quoted on every feed. Stored and compared as text; never parsed to a number.
- **`trade_seq` needs `Long`.** Live value ~296,000,000 on 2026-08-08. `Integer` has roughly 7× headroom left, which is not a margin worth taking on a monotonic counter.

---

## 1. Ranked verification handles

Ordered by how much of the build each one covers. **If you run only one, run handle 1** — it is the whole thesis of the change in a single command, and it fails loudly if either §0 trap is live.

### Handle 1 — the mutation test. Do the fixtures have teeth?

The spec's §0 says plainly that the fixtures cannot be relied on because the implementer writes them too. So I injected the two named traps into `DedupTrades` and re-ran. **This is the check that distinguishes a real fix from a green harness.**

| Injected defect | Fixtures that FAILED |
|---|---|
| **Trap 2 — silent no-op** (dedup ignores identity, falls back to legacy key always) | A53d, A53e, A53f |
| **Trap 1 — empty-identity collapse** (key on `If(TradeId, "")` for all rows) | A53c, A53f, **and the pre-existing A48d** |

**The load-bearing detail is not "3 failed" — it is WHICH failed.** Each trap was caught by the fixtures written for it and by no others, and trap 1 was independently caught by **A48d, a fixture that predates this build and that I did not touch for this purpose.** That is the one piece of evidence here not authored against my own understanding of the contract.

To reproduce: in `Core/TradeStoreWriter.vb`, replace the two-pass body of `DedupTrades` with a single pass keying on `LegacyRowKey` (trap 2) or on `If(r.TradeId, "")` (trap 1), rebuild `verify/ordercheck`, run.

### Handle 2 — backward compatibility on real data, one command

```bash
dotnet run --project tools/BacktestRunner/BacktestRunner.vbproj -c Release -- coverage --from 2026-08-04 --to 2026-08-06
```

Read-only. Against the real 74,989-row legacy store the two load-bearing values are:

- `seq gaps (local)    NOT CHECKABLE — 74989 row(s) carry no trade_seq, 0 do`
- `longest gap         153.1s  (threshold 300.0s — 0 breach(es))`

**Both matter, and the first matters more.** `NOT CHECKABLE` is the false-clean trap avoided in production — a naive sequence walk over a legacy store finds zero gaps and reads as *perfect*. The `153.1s` is **unchanged** from the pre-build figure, which is the backward-compatibility claim: legacy rows carry no identity anywhere, so `DedupTrades` pass 1 is empty and pass 2 is exactly the old whole-row dedup. `LegacyRowKey` is byte-identical to the old `FormatRow`, so a legacy-only store cannot behave differently. **That is an identity argument, not a measurement** — see §4.

### Handle 3 — the arithmetic identity for dedup

For any input, `DedupTrades` satisfies:

```
kept = |distinct identities among identified rows|
     + |distinct legacy keys among identity-less rows, minus those already claimed by an identified row|
```

The cheap version: on a store with **no** identity anywhere, `kept` must equal the old five-field distinct count exactly. On a store with identity **everywhere**, `kept` must equal the distinct `trade_id` count exactly. A53c and A53d pin the two endpoints; A53f pins the mixed middle.

### Handle 4 — one grep proves the one-seam claim

```bash
grep -rn "ReadTradeId\|ReadTradeSeq" --include=*.vb . | grep -v "^./.claude/"
```

Must show exactly **three** call sites (`DeribitClient.vb`, `DeribitWsFeed.vb`, `tools/BacktestRunner/HistoricalStore.vb`) and **one** definition pair (`DeribitClient.vb`). More than one definition pair means the two feeds can drift again and §0 trap 3 is back.

### Handle 5 — the gate tail

`verify-gate.ps1 -Mode prepush`, run **after** committing per the v64 F5 lesson: **GATE PASSED**, 1 warning. The warning is `engine-path change without a settings.json version bump (nudge only)` — **expected and correct**, since this build adds no settings keys. Same non-blocking WARN the two C1 sessions recorded, for the same reason.

Builds: solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck, all **0/0 Release**. Harness **ALL PASS, 265 checks**.

---

## 2. Decisions queued, with my read where I have one

### Q1 — ⚠ The spec's dedup relation is not transitive. I chose a resolution; it needs ratifying.

**This is the most important item in the packet.** `trade-store-trade-identity-proposal.md` §3.4 says:

> Two rows are the same trade if and only if both carry an identity and the identities are equal.
> If either row lacks an identity, fall back to whole-row equality on the five legacy fields.

Take a legacy row **L** and two identified rows **I1**, **I2** that share all five legacy fields. Then L≡I1 and L≡I2 by the fallback arm, but I1≢I2 by the identity arm. **No grouping can satisfy all three sentences.** The spec does not define an equivalence class, so an implementation must choose one — and A53e proves the I1/I2 case is not hypothetical: Deribit returned exactly that pair in the first three trades I fetched.

| Option | Behaviour on (L, I1, I2) | Order-dependent? |
|---|---|---|
| **(a) Identity-first — what I built** | keeps I1 and I2, drops L | No |
| (b) First-wins single pass | keeps 2 or 3 rows depending on file order | **Yes** |
| (c) Fallback-first | keeps L, drops I1 and I2 | No |

**My read, labelled a hypothesis: (a) is right, and (b) is disqualified rather than merely worse.** (b) makes the store's row count depend on the order rows happen to sit in a file, which for a mixed-shape file is an artefact of when the AWS redeploy landed — a completeness instrument whose answer depends on that is not usable. Between (a) and (c), identity is strictly better information than a five-field coincidence, so the identified rows should survive and the ambiguous one should not. (a) also errs toward **not** double-counting, which is the conservative direction for a store whose presenting symptom was volume doubling on merge.

**What I cannot ground:** whether dropping L is right when L is genuinely one of I1/I2 and the other is a real trade the legacy binary never saw. In that case (a) loses one real trade. I have no way to estimate how often that happens, and the alternative loses more. **The criterion is yours.**

**Scoping, without recommending it:** switching to (c) is a ~10-line change confined to `TradeStoreWriter.DedupTrades` — swap the two passes and drop the `claimedLegacy` seeding in pass 1. A53f is the only fixture whose expected values change.

### Q2 — Should `trade_seq` gap detection be wired into the coverage report at all, or stay a library function?

I **wired it in** — one informational block in `BuildConsoleSummary`. `trade-store-trade-identity-proposal.md` §4 lists "the S0 summary line" as a CoverageReport change but does not name a home for gap detection, and §8 puts the S0 daily-job redesign out of scope pending D6.

**My read: wiring it in was right, and the alternative is worse.** A pure function that only a fixture calls is dead code, and §3.3 is the entire argument for taking `trade_seq` in the first place. It renders as a supplement, never as a replacement for S0 (D6).

**Flagging honestly:** this is the one place I went slightly beyond the letter of §4. If you want the surface frozen until the S0 job is re-specified, deleting the block in `CoverageReport.BuildConsoleSummary` is a clean revert that leaves `ComputeSequenceGaps` and A53h intact.

### Q3 — Shares a root with Q2. When does the suspended daily S0 job get re-specified?

`docs/trader-tick-queue.md` §2 suspends S0 pending this build. This build ships, so the suspension's stated cause is gone — but D6 says re-specify **after** it ships, not before, and the ~24 h urgency that made a *daily* cadence necessary is now partly retired by local gap detection.

**My read:** the cadence question genuinely changed and should be re-derived rather than resumed. S0's remaining unique job is proving the store agrees with the venue on *content*; that does not obviously need to be daily, because it is no longer the only completeness instrument. **But this is downstream of the AWS redeploy** — until AWS writes identified rows, S0's identity arm has nothing to match on and every match falls back to the ambiguous arm. **Ruling Q2 and Q3 together is cheaper.**

### Q4 — No read. The C1 rows in `DeribitIndicatorProject.md` §15 break §15's own rule.

§15's cap says *"one item gets ONE row"* and names C1 as the growth mechanism — yet C1 still occupies three rows (Session 1, Session 2, a rider). I added **one** row for this item and did not reorganise anyone else's, because tidying §15 was not in my scope and a silent edit to another item's record is the wrong kind of initiative. **Flagging only.**

---

## 3. Spec-back proper — feedback on the spec itself

### What the spec got right, specifically

- **"§1 runs first and alone"** is the sentence that did the most work. It stopped me writing a line of code against an assumption about what Deribit sends, and it turned up the string-vs-number fact that would otherwise have become a silent parse bug.
- **§3.1's observation that `TryParseRow` guards on `< 5`, not `= 5`.** This is the single finding that collapsed the whole change from "schema migration" to "append two columns". No rotation, no rewrite, no dual-read path. Worth reusing as a habit: *check the guard's operator before designing the migration.*
- **§3.4 written as a quotable contract** — three imperative sentences a fixture can be built from without reading the implementation. A53c and A53e were written straight off that text. This is the format to keep.
- **§0's "the fixtures cannot be relied on to catch these, because the implementer writes them too."** This is what made me run mutation tests rather than trust a green harness, and it is the only reason handle 1 exists. **Every spec for a defect of this class should carry that sentence.**

### Which assumptions broke

1. ⚠ **§3.4's dedup contract is not a well-defined equivalence relation.** Q1 above. The spec presents three rules as if they compose; they do not. **This is the highest-value item here** — the spec asked for a grouping that cannot exist, and any implementer would have silently picked a resolution. I picked one, documented it in the code, and surfaced it; a less suspicious build would have shipped (b) and produced order-dependent row counts that look fine until two boxes disagree.

2. **§1's REST endpoint is the wrong one.** The table names `get_last_trades_by_instrument`, but the store's actual backfill parse site — which §4 lists as a surface at `HistoricalStore.vb:307-317` — calls `get_last_trades_by_instrument_and_time`. Harmless because I verified both, but §1 as written would have gated on an endpoint the store does not use for capture.

3. **§4's surface list is missing two dedup sites.** It names the dedup at `CoverageReport.vb:310`. It does not name `HistoricalStore.LoadTradeRange` (`:357`), which dedups on `FormatRow` across the whole month union, nor the fixture site in `verify/ordercheck/Program.vb`. Both had to change. **A grep for `FormatRow` finds all four in one command** — worth doing when a spec enumerates surfaces.

### Where the spec was narrower than its own words

- **§9's "A1–A52a unregressed" cannot be met literally.** Two existing fixtures encode the *old* five-field format: **A48a** asserts the five-column header and a five-column row shape, and **A49j** calls `ComputeVenueDiff` with its old signature. Both had to be edited. That is the fixture tracking the spec, not a regression — but "unregressed" reads as "untouched", and an implementer editing the fixtures that guard their own change is precisely the §0 hazard. **Both edits are called out here so a reviewer checks them specifically rather than reading a green run as proof they were untouched.** They are the two diffs in `verify/ordercheck/Program.vb` worth reading closely.

- **§6 gives A53c and A53e no home for their data.** The instruction to write A53c "from §3.4, before reading the implementation" is right, but A53e's case is far more convincing with *real* venue data than with constructed rows — and the §1 gate produces it for free. A53e now pins Deribit's actual `439922656`/`439922657` pair. A spec asking for a gate and a fixture about the same phenomenon should say to carry the gate's output into the fixture.

### Constraint pairs that nearly conflicted

**The link-surface deadlock, and the hatch.** Three constraints collided:

1. §0 trap 3 wants **one** shared pair of JSON readers, so the feeds cannot drift.
2. `TradeStoreWriter` is the natural home — it is the store seam and links "everywhere".
3. `TradeRecord` needs an absent-sequence sentinel, and its default value must reference it.

"Links everywhere" turns out to be false. **`AutoTweaker`, `WhatIfRunner` and `CeilingAudit` all link `DeribitClient.vb` and none of them link `Core/TradeStoreWriter.vb`.** Putting the readers or the sentinel on the writer broke three builds.

**The hatch: `TradeRecord` itself is the wider seam.** It lives in `DeribitClient.vb`, which every project links, so the readers and `AbsentSeq` moved there and `TradeStoreWriter.AbsentSeq` became an alias. One seam, zero new link dependencies.

⚠ **The reason this is worth writing into the next spec: neither the app build nor the harness catches it.** Both were green while three tool projects were broken. **Only Release-building all six projects surfaced it**, which is exactly why §9's acceptance names all six — that requirement earned its keep here.

---

## 4. What I did not verify, and cannot

- ⚠ **Whether Deribit ever RESETS `trade_seq`** — at instrument expiry, venue maintenance, or otherwise. It was contiguous across a ~4-minute observation, which is not evidence about a reset. **The build treats a backwards step as an uninterpretable discontinuity and deliberately does NOT count it as loss.** If a reset does happen, gap detection across it will report a discontinuity rather than a false loss figure — but I have not seen one, and the report's wording is a guess about a case I have never observed.
- **Nothing was verified against an actual identified store**, because none exists yet. Every real-data check in handle 2 ran against 74,989 legacy rows, so the **identity arm of dedup and the identity arm of the venue diff have been exercised only by fixtures.** The first real proof arrives after the AWS redeploy. This is unavoidable and is the strongest argument for reading the first post-deploy coverage report carefully.
- **The claim that legacy-only stores behave byte-identically to pre-build** is an **identity argument** (pass 1 is empty; `LegacyRowKey` is byte-identical to the old `FormatRow`), corroborated by the unchanged `153.1s` gap figure. **I did not diff old-vs-new deduped row counts on the same window** — that would need the pre-build binary retained and run side by side.
- **The live app was not launched.** Same reason the v62 and C1 Session 2 builds gave: starting `MainForm` appends collector rows under a fresh `InstanceId` into the real dataset. The WS capture path change is two lines routed through readers the harness covers, but **it has not been observed writing a seven-field row from a live stream.** That is the trader's test gate.
- **The `--verify-venue` S0 path was not run live.** `ComputeVenueDiff` is pure and covered by A49j and A53g; `RunVenueDiffAsync`'s network wiring and the new summary block were not exercised against a real fetch.
- **Concurrency was not tested.** Streaming capture and gap repair append to the same file under `_appendLock`; I changed the row *content*, not the locking, but no test drives both paths at once. Unchanged from the v64 position.
- **Nothing has ever been calibrated off the tape store**, so there is no downstream result to re-derive — this build changes no number anyone has used.

---

## 5. What the reviewer should rule before the AWS deploy

Ranked by what each one blocks.

| # | Ruling needed | Blocks |
|---|---|---|
| **Q1** | Ratify identity-first dedup, or pick (c) | ⚠ **The deploy.** Once AWS writes identified rows into files that already hold legacy rows, the mixed-shape case is live and changing the resolution afterwards re-interprets tape already on disk |
| **Q2 + Q3** | Keep the sequence-gap report block; when to re-spec S0 | Not the deploy — both are surface/cadence questions and either can follow |
| **Q4** | Whether anyone tidies §15's C1 rows | Nothing |

**Everything else is ready.** Gate passed, 265 checks pass, six projects build 0/0 Release, and the change is additive and reversible — reverting the code leaves every written file readable, because the `< 5` guard reads old and new rows alike and identity already written is simply ignored.
