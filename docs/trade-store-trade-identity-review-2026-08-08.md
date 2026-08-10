# Orchestrator review — trade identity build (`64d41e7`)

**Reviewer:** the orchestrator seat. **Build:** a separate implementer session. **Verdict: ACCEPTED. Cleared for the AWS deploy once Q1 below is read — and Q1 is ratified here.**

**Independently re-run, not taken on report:** `verify-gate.ps1 -Mode prepush` → **GATE PASSED**, harness **ALL PASS at 265 checks**, six projects 0/0 Release. One `WARN` on version-bump, which is **correct**: the change touches `Core/` and adds no settings keys, exactly as `trade-store-trade-identity-proposal.md` states.

---

## 1. The three traps in the spec's §0 — all three avoided, one better than asked

| Trap | Outcome | Evidence |
|---|---|---|
| **1 — empty-identity collapse** | **Avoided** | `DedupTrades` pass 1 opens `If Not r.HasIdentity Then Continue For`, so an absent identity can never enter `seenIds`. `HasIdentity` is `Not String.IsNullOrEmpty(TradeId)`, so an empty string counts as absent — the spec's "a missing identity is not a value" |
| **2 — the silent no-op** | **Avoided** | `TryParseRow` reads `parts(5)` and `parts(6)` and populates both fields; the `< 5` guard is retained for legacy rows. The reader consumes what the writer emits |
| **3 — feeds disagreeing on format** | ⭐ **Avoided by construction, better than the spec asked** | The spec asked for the two paths to be checked against each other. The build instead **routes both through one seam** — `TradeRecord.ReadTradeId` / `ReadTradeSeq` in `DeribitClient.vb`, called from both `DeribitWsFeed` and `HistoricalStore`. They now *cannot* diverge. Discipline replaced by structure is the stronger fix |

**Sentinel check, unprompted:** `AbsentSeq = -1L` with `HasSeq = TradeSeq >= 0`. Deribit's `trade_seq` is a large positive number (~296 M observed). **No collision.** This is the classic place a sentinel breaks and it is right.

**Fixtures:** all eight A53 present, and `A53c_EmptyIdentityDoesNotCollapseLegacyRows` is named for the trap the spec told them to write from the spec text. Worth noting the spec's own §1 endpoint table was **wrong** — it named the count-based endpoint; the build verified `get_last_trades_by_instrument_and_time`, which is the one actually used, and said so.

---

## 2. Rulings

### Q1 — ⚠ **RATIFIED: identity-first, option (a). Keep the build as written.**

**The finding is correct and it is the most important thing in the packet.** The spec's §3.4 relation is **not transitive**: for a legacy row L and two identified rows I1, I2 sharing all five legacy fields, L≡I1 and L≡I2 by the fallback arm while I1≢I2 by the identity arm. No grouping satisfies all three sentences. **The spec defined a relation and called it an equivalence. That is my error, not the implementer's.**

**Ratifying (a), for their reason plus one they did not use:**

- **(b) is disqualified, not merely worse.** They are right. A store whose row count depends on the order rows sit in a file — an artefact of when the AWS redeploy happened to land — is not a completeness instrument.
- **Between (a) and (c), identity beats a five-field coincidence.** Straightforward.
- **The reason they did not use, and it is what makes this cheap: the affected population is currently ZERO.** The mixed-shape case needs identified rows and legacy rows in one file. AWS has not deployed, so **no identified row exists on disk anywhere.** Ruling now costs nothing and re-interprets nothing. This is the cheapest moment this decision will ever have, and it stops being cheap the hour AWS deploys.

**On their honest gap** — that (a) loses a real trade when L is genuinely one of I1/I2 and the other is a trade the legacy binary never saw: **accepted, and it is bounded.** Legacy rows are only produced by pre-deploy binaries. The population is fixed at deploy time and shrinks as a share of the store forever after. A bounded, non-growing loss on historical rows, in exchange for exactness on everything future, is the right trade.

### Q2 — **RULED: keep the sequence-gap block in `BuildConsoleSummary`.**

Their reasoning holds: a pure function only a fixture calls is dead code, and §3.3 was the entire argument for taking `trade_seq`. It renders as a supplement and never as a replacement, which is what D6 required. **Going slightly beyond §4's letter was right, and flagging it was righter.**

### Q3 — **RULED: re-spec the S0 job AFTER the deploy has produced several days of identified rows.**

Their point decides it: **until AWS writes identified rows, S0's identity arm has nothing to match on and every match falls back to the ambiguous arm.** Re-speccing now would be speculation about an instrument nobody has seen working. The cadence question genuinely changed — local gap detection retires the ~24 h urgency that made *daily* necessary — so the new cadence should be derived from what the identity arm actually reports, not inherited.

### Q4 — **RULED: the C1 rows get consolidated, by someone else, as a doc tidy.**

Declining to tidy another item's record was correct — a silent edit to someone else's history is the wrong initiative. But `DeribitIndicatorProject.md` §15 does cap at one row per item, and C1 occupying three is exactly the drift that cap exists to stop. Queued as a small tidy; it blocks nothing.

---

## 3. What I checked myself, and what I did not

**Checked by reading code or running commands:**

- `DedupTrades` in full, against the §3.4 contract — including finding the transitivity gap independently before reading Q1.
- `HasIdentity`, `HasSeq`, `AbsentSeq`, and `TryParseRow`'s column handling.
- That both ingest paths call the same two readers.
- That the venue diff keeps `IdentityMatched` and `FallbackMatched` separate and never sums them.
- All eight A53 fixture names.
- The gate, re-run independently.

**Not checked, and stated rather than implied:**

- **I did not read the 359-line `verify/ordercheck/Program.vb` diff line by line.** I confirmed the fixtures exist, are named for the right properties, and that the suite passes at 265 checks. **A fixture that asserts the wrong thing would pass this review.** The implementer's own Handle 1 offers a mutation test for exactly that; I did not run it.
- I did not re-derive the §1 gate against the live API. I am taking the observed field types on report.
- I did not review the `CoverageReport.vb` changes beyond the dedup and venue-diff sites.

---

## 4. Cleared for deploy

**Q1 is ratified, which was the only item blocking.** Deploy per `aws-collector-deploy-checklist.md` §1.2 and the §1b pre-flight.

⚠ **Deploy promptly.** `settings.json` does not move, so AWS keeps writing five-field rows until it gets the binary, and every hour of that is permanently unmergeable tape. The change is additive and reversible — reverting the code leaves every written file readable, because the `< 5` guard reads old and new rows alike.
