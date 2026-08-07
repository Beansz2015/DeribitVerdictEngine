# Trade-store trade identity — `trade_id` and `trade_seq`

**Status:** ✅ **BUILT 2026-08-08.** §1 verification gate PASSED (both feeds carry both fields; the escalation fork did not fire). Solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck build 0/0 Release; harness ALL PASS, 265 checks, A1–A52a unregressed + A53a–h. Spec-back: [`trade-store-trade-identity-spec-back.md`](trade-store-trade-identity-spec-back.md). ⚠ **NOT YET DEPLOYED TO AWS — until it is, AWS keeps writing five-field rows and that tape is permanently unmergeable.** Previously: APPROVED 2026-08-08 — D1–D7 ticked, all as recommended.

> ⚠ **Sequencing note the D-table does not cover, and it costs tape if missed.** Building this does **not** start capturing identity. `settings.json` is unchanged, so **AWS keeps running the old binary and keeps writing five-field rows until it is redeployed.** Every hour between the build landing and the AWS redeploy produces tape that is permanently unmergeable, for the same reason as the quarantined books. **Treat the AWS redeploy as part of this change, not as a follow-up** — deploy steps are in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §1.2 and §1b.
**Class:** store schema + ingest + dedup + venue diff. **No settings keys. No scoring impact. NOT a dataset boundary** — no indicator, card, snapshot line, CSV column or bridge field reads the trade store.
**Origin:** the withdrawn 78.8 % completeness claim, [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §4a.

---

## 0. Model and effort — read before assigning

**Model: Opus**
**Effort: high**

**Why that tier.** The mechanical work is moderate — two parse sites, one row format, one dedup, one comparison. The tier is not for volume. It is because **every failure mode in this change looks correct from the outside.** The defect being fixed is itself a case of wrong-looking-right that survived a build, a review and a copy-back before anyone noticed. Two of the traps below produce a green harness and a plausible report while silently destroying data.

**Where this will specifically slip — three places, all silent:**

1. ⚠ **The empty-identity collapse.** If dedup keys on `trade_id` and a legacy row has none, every legacy row keys on the same empty string and **collapses to one row**. That is the original defect, reproduced at greater scale, in the code written to fix it. The dedup must branch on presence, never key on a possibly-empty field.
2. ⚠ **The silent no-op.** `TryParseRow` accepts `parts.Length < 5`, so it already tolerates extra columns and **ignores them**. A writer that emits `trade_id` against a reader that never reads it produces a full store, a clean harness and no behaviour change at all. A fixture must prove the reader *consumes* the field, not merely that the writer emits it.
3. ⚠ **Assuming the two feeds agree on format.** The WS and REST paths are parsed separately (`DeribitWsFeed.vb:476-481`, `HistoricalStore.vb:307-317`). If their `trade_id` differs in type or spelling, cross-feed matching silently fails and every venue diff reports total loss.

**The fixtures cannot be relied on to catch these**, because the implementer writes them too. A misunderstanding of the dedup contract propagates straight into its own test. **Trap 1 in particular must be a fixture written from the spec text, not from the implementation.**

**Escalation trigger — stop and come back.** If the §1 verification gate shows the WS channel does **not** carry `trade_id`, stop. The design forks: WS-captured rows would have no identity at all, and the whole approach needs re-deciding rather than adapting.

**Session split.** One session, but the §1 gate runs **first and alone**. Do not begin the build until both feeds are confirmed to carry the fields.

---

## 1. Verification gate — run this before any other work

**Confirm, against the live API, that both feeds supply identity:**

| Feed | Where | Confirm |
|---|---|---|
| REST | `get_last_trades_by_instrument` | `trade_id` present; `trade_seq` present; note their JSON types |
| WS | the `trades.{instrument}.{interval}` channel payload | the same two fields, with the same types and the same values for a trade seen on both |

**Record the finding in the spec-back either way.** If WS lacks them, stop — see §0.

**Why this is a gate and not an assumption.** Our code never reads either field, so the codebase is no evidence at all about what Deribit sends. I did not verify it, and the spec must not proceed on my guess.

---

## 2. The problem, with the evidence that produced it

The store row is five fields: `Timestamp,Price,Amount,Direction,Liquidation` (`TradeStoreWriter.HeaderLine`). Deribit supplies a `trade_id`; `HistoricalStore.vb:307-317` and `DeribitWsFeed.vb:476-481` both read the five and never it.

`TradeStoreWriter.FormatRow` — that same five-field row — is what the store dedups on (`CoverageReport.vb:310`) **and** what the S0 venue diff matches on (`CoverageReport.vb:486, 492`).

**Three consequences, all live and all measured on 2026-08-08:**

| Consequence | Evidence |
|---|---|
| **Two tape books cannot be correctly merged** | Merging the AWS and local books nearly **doubled volume** over the tested window, 78.6 M → 152.2 M, while **zero timestamps** were absent from either side. Whether that is two partial feeds correctly unioned or the same trades counted twice **cannot be determined** |
| **Genuinely distinct trades are silently dropped at write time** | 22,376 of AWS's 228,163 August rows are exact five-field duplicates. Some are backfill overlapping the stream. Some may be real trades. **Nothing in the code can tell them apart** |
| **Any venue diff is uninterpretable** | S0 matches on the same row, so it reports "different representation" as "missing trade" |

**Retrofit is impossible.** Rows already on disk have no identity to recover. Every day of tape captured before this ships is permanently unmergeable, which is why the local second sampler was turned off on 2026-08-08 rather than left collecting.

---

## 3. Proposed change

### 3.1 The favourable migration property

`TryParseRow` guards with `If parts.Length < 5 Then Return False` — a **`<`**, not an `=`. **Appending columns at the end is therefore already backward-compatible.** Old five-column files parse unchanged. New files parse unchanged. **No file rotation and no rewrite of existing months is required.**

This is the single biggest simplification available, and it is why §7 row D5 recommends appending rather than versioning the file.

### 3.2 The row

```
Timestamp,Price,Amount,Direction,Liquidation,TradeId,TradeSeq
```

- Both new fields append at the end.
- A legacy row simply ends after `Liquidation`. Its identity fields are **absent**, which is distinct from empty.
- `TryParseRow` sets them only when present, and exposes presence to callers.

### 3.3 `trade_seq` is the more valuable of the two — and it is why this is worth doing now

`trade_id` gives **identity**: it makes dedup exact and makes a cross-book merge correct.

`trade_seq` is a per-instrument **monotonic sequence**. It gives something identity cannot: **gap detection from the store alone.** If the store holds sequence 100, 101 and 103, then 102 is missing — provable without a venue call, without network, and **without Deribit's ~24 h retention window.**

That property reaches past this spec:

- The S0 venue diff exists because completeness was otherwise unmeasurable. With `trade_seq`, completeness becomes a local computation over a file.
- The ~24 h clock that made a daily S0 job urgent **stops applying**. A month-old month-file can be checked for gaps at any time.
- The S3 longest-gap metric — which I showed cannot detect scattered loss, on arithmetic that still stands — gains a companion that can.

**So `trade_seq` does not merely support the fix. It retires the constraint that made the surrounding work urgent.** D1 in §7 asks whether to take both fields; the recommendation is yes, and this is the reason.

### 3.4 Dedup

> ⚠ **The contract, stated so a fixture can be written from it without reading the implementation:**
> **Two rows are the same trade if and only if both carry an identity and the identities are equal.**
> **If either row lacks an identity, fall back to whole-row equality on the five legacy fields.**
> **Never key on an absent or empty identity. A missing identity is not a value and must not join a group.**

### 3.5 Venue diff (S0)

Match on `trade_id` when both sides carry one. Fall back to whole-row otherwise, and **report the two populations separately** — an identity-matched count and a fallback-matched count. A single blended number would hide exactly the ambiguity this spec exists to remove.

---

## 4. Surfaces touched

| Surface | Change |
|---|---|
| `Core/TradeStoreWriter.vb` | `HeaderLine`, `FormatRow`, `TryParseRow`, `TradeRecord` |
| `tools/BacktestRunner/HistoricalStore.vb` | REST parse, ~line 313 |
| `DeribitWsFeed.vb` | WS parse, ~line 476 |
| `tools/BacktestRunner/CoverageReport.vb` | dedup at `:310`, venue diff at `:486, 492`, plus the S0 summary line |

**Not touched:** any indicator, scoring path, card, snapshot line, CSV column or bridge field. The trade store feeds none of them today.

**Tweaker:** no settings keys are added, so **no new hard constraint. HC28 stays free.**

---

## 5. Legacy rows

- Existing months keep five columns and stay readable.
- New rows in the **same file** carry seven. A file will legitimately hold both shapes.
- **Dedup must handle mixed shapes within one file** — this is the normal case, not an edge case.
- No backfill of identity is possible or attempted. §7 row D2 asks whether to mark the boundary.

---

## 6. Fixtures — family **A53** (verified free; A52a is the high-water mark)

| Fixture | Pins |
|---|---|
| **A53a** | Round trip: a seven-field row writes and parses back with both identity fields intact |
| **A53b** | A five-field legacy row still parses, and reports identity **absent** — not empty |
| **A53c** | ⚠ **The empty-identity collapse.** Ten legacy rows that differ only in amount survive dedup as ten rows. **Write this from §3.4, before reading the implementation** |
| **A53d** | Two rows with equal `trade_id` and differing other fields dedup to one |
| **A53e** | ⚠ **The silent no-op.** Two rows identical in all five legacy fields but with different `trade_id` survive as **two** rows. This fails if the reader ignores the new column |
| **A53f** | Mixed-shape file: legacy and identified rows in one file dedup correctly under both branches |
| **A53g** | Venue diff reports identity-matched and fallback-matched counts **separately** |
| **A53h** | `trade_seq` gap detection: a store holding 100, 101, 103 reports 102 missing |

---

## 7. D-table — ✅ **ALL TICKED 2026-08-08 (trader), every row as recommended**

> ⚠ **Read D7 carefully before acting on it.** Its recommendation was *"trader's call, and it becomes a real option again"* — so ticking it means **the question re-opens once identity ships**, not that local capture is pre-approved to resume. **A future seat must not read "D7 ticked" as authorisation.** Local capture stays OFF until an explicit decision is made at that point.

| # | Question | Ruling — as recommended |
|---|---|---|
| **D1** | Capture `trade_id`, `trade_seq`, or both? | **Both.** `trade_id` fixes dedup and merging. `trade_seq` adds local gap detection and **retires the ~24 h completeness clock** (§3.3). The marginal cost over taking one is two characters of parse |
| **D2** | Mark the point where identity begins? | **Yes, in the spec-back and the deploy checklist — not in the data.** A marker row would be a sixth thing to parse. The first identified row in each file is self-marking |
| **D3** | Dedup when identity is absent | **Fall back to whole-row on the five legacy fields.** Never key on an absent value (§3.4) |
| **D4** | Venue diff matching | **Identity when both sides have it, whole-row otherwise, and report the two counts separately** |
| **D5** | Append at end, or version the file? | **Append.** `TryParseRow`'s `< 5` guard already makes it backward-compatible, so no rotation and no rewrite (§3.1) |
| **D6** | Does `trade_seq` gap detection replace S0, or supplement it? | **Supplement, and re-scope S0.** Local gap detection is cheaper, unbounded in time and needs no network. S0 keeps one job the sequence cannot do: proving the store agrees with the venue on *content*, not just on continuity. **The suspended daily S0 job should be re-specified after this ships, not before** |
| **D7** | Re-open D1-a and restore the local second sampler once identity ships? | **Trader's call, and it becomes a real option again.** Two books can be merged correctly once both carry identity. Note the interim tape captured 2026-08-07/08 stays unmergeable regardless |

---

## 8. Out of scope

- Any change to `analysis_log.csv`. Its three pending riders — `TriggerMode`, the J-E effective-source stamp and `SettingsVersion` — ride a rotation of **that** file and must not be pulled into this one.
- Backfilling identity onto existing rows. Impossible.
- Re-running the S0 daily job design. Blocked on D6.

## 9. Acceptance

- The §1 verification gate is recorded, with the observed field types from both feeds.
- Solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck build 0/0 Release.
- A1–A52a unregressed, A53a–h pass.
- `verify-gate.ps1 -Mode prepush` GATE PASSED, **run after committing** — the v64 F5 lesson.
- A spec-back recording every deviation, and explicitly stating whether the WS and REST `trade_id` values were observed to match for the same trade.
