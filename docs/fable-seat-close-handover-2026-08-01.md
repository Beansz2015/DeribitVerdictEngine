# Fable Seat Close — Handover to the New Orchestrator (2026-08-01)

**From:** the Fable coordinator seat (closes ~Aug 1; Pro downgrade Aug 2 — Fable becomes rationed moments only, reached via trader-relayed batch summaries per `docs/batch-review-packet-convention.md`).
**Read in order:** CLAUDE.md protocol → `seat-handover-2026-07-18.md` (standing rules, all still binding) → `backlog-dependency-map.md` (THE board) → `fable-handover-2026-07-31.md` (the first batch orchestrator's handover) → `trade-store-arc-spec-back-2026-07-31.md` (JOB 1) → `candle-store-derivation-batch-spec-back.md` (JOB 2 — deferred to you, unread by the Fable seat except via JOB 1's pointers).

## 1. State at close (per the 2026-07-31/08-01 orchestrator report — verify with `git status -sb`, never assume)

Settings **v64** (trade-store capture shipped + reviewed). **26 commits local, unpushed** — the trader tests + pushes; three D-tables await the trader (coverage report D1–D7 · overlay D1–D7 · the v64 test-and-push gate) — **not yours to rule**. Next free fixture family **A52** (A49/A50 reserved-unbuilt, A48+A51 consumed); **HC28 free**. Store verified 0-missing at all four candle resolutions + funding; every JOB 2 figure reproduced twice on two store snapshots. Collector healthy and logging Friday NY after a **~16 h unnoticed death** on 07-31 (last health-log line: `OK` — the observability cluster below exists because of exactly this). AWS box alive; fresh copy-back landed 08-01 (`bin\Debug\net8.0-windows\analysis_log_aws.csv` + `ws_health_aws.log`; **no liq_events.log ⇒ A4 stays market-gated**).

## 2. RULING — the observability cluster (J-B · J-C · J-E · JOB 2's D-F, ruled together as J-F asked)

**One principle governs all four: silence is never evidence of health. Every layer either positively attests data presence or classifies absence as a defect.** Four silent-divergence events in two days, each found only by counting rows against a deterministic expectation, are the evidence base; the 16 h `OK`-terminated outage is the type specimen.

- **J-B — RATIFIED toward DEFECT.** The ambiguous trailing interval (transition-only log, byte-identical "quiet" vs "dead") resolves as **defect** in the coverage report. A false defect costs one human dismissal; the other error costs tape, unmarked. The reviewer's reasoning is adopted verbatim.
- **J-C — RATIFIED; D4's contradicting baseline plan is overruled.** The natural-silence threshold derives from **REST-backfilled windows** (the venue's own record of what printed = ground truth), extended to include a weekend. Streamed-minus-REST is not noise to absorb into the baseline — **that difference IS the capture-gap signal.**
- **J-E — RATIFIED.** The effective-source per-row stamp (`DeriveWsHealth`'s value) rides the **next natural CSV rotation, alongside `TriggerMode`** (ruled into the same slot at the pre-Aug-1 batch double-check, D3) — two columns now queued for one rotation; **never force one.** Until it ships, treat every REST-fallback-sensitive figure (incl. JOB 2's matched-replay residual) as a bound, not an estimate — the packet already does.
- **D-F — RATIFIED as standing convention.** Derivation briefs mandate a **store-completeness pre-flight** (the freeze-the-CSV rule's sibling). The July store-repair history is the argument; no derivation runs on a store that hasn't attested completeness for its window.
- **Sequencing consequence:** the J-B/J-C coverage-report build is the *precondition instrument* for every data-gated item on the board — it decides whether future collection gaps are seen at all. Schedule it ahead of discretionary builds.

## 3. Direction-only (NOT a build authorization)

**D-A/D-B: the re-anchor is sound — build it at a boundary of your choosing.** Not hours after an outage; not stacked with another boundary; own D-table at open per the standing one-⚠ rule.

## 4. Your first tasks

1. ~~The Friday/weekend data-coverage read~~ — **READ DONE by the Fable seat, 2026-08-01 (frozen `frozen_{local,aws}_20260801.csv` in the session scratchpad).** Verdicts: **(a) ⚠ BOTH COLLECTORS ARE DOWN** — local since ~07-31 14:00 UTC (the 971-min 07-30/31 gap = defect interval per J-B, AWS covered 451 rows of it), and **AWS since 07-31 13:59 UTC** (new InstanceId `0efcda74…` after a brief 07-30 16:33→16:41 restart; zero Saturday rows where the prior weekend logged ~920/day — a second silent death in three days, the observability ruling's second type specimen). **First action: trader restarts the AWS box at next RDP + local when back at the desk; record the new InstanceId set at next copy-back.** (b) **F1 count gate: GO** — pooled weekday STRONG = **201** (NY 121 / LONDON 65 / ASIA 15) ≥ 150; run the §9 pooled read via the report runner. (c) **W6-1 depth: GO** — 540 pooled weekday LONDON directional rows ≥ 07-08 (the prior evidence base was ~227). (d) Bursts healthy (LONDON 11.0%/6.0% non-consecutive, same-side 15/15; NY 13.4% same-side 89.5%; the 07-31 NY zero is the outage, not a signal). (e) Absorption: 0 flags / 778 episode rows — Path B state unchanged. (f) No liq_events.log either box ⇒ A4 market-gated.
2. **JOB 2 read** (deferred to you in full) + close D-D/D-E ("close these").
3. **J-A / A48f disagreement:** the implementer's spec-back and the first orchestrator's review read A48f oppositely; code is fixed either way. Take a fresh read of the fixture itself and ratify one reading in a dated line — don't inherit either side's framing.
4. The **geometry session** (was Friday's plan): interpret the lane-E grids per the batch double-check's D5 ruling — post-07-08 tables are the decision surface; "no separable change yet" is a legitimate outcome. Any live geometry change = its own ⚠ D-table to the trader.
5. Standing periodic checks continue (liq_events CASCADE ⇒ A4 · §9 STRONG accrual · burst watch spot-checks · funding calm-week · absorption episode accrual under Path B).

## 5. The month's named big items (all with evidence bases on file — spec-first, trader ticks)

Absorption **mechanism-revision spec** (Path B ticked; `absorption-anchor-rederivation-2026-07-30.md` is the evidence) · the **forming-bar ⚠ candidate** (headline slot; backtester A/B FIRST — `forming-bar-live-investigation-2026-07.md` + batch double-check D3) · **backtester**: cleared geometry-class, VWAP-values partial (D1/D2 rulings in `pre-aug1-batch-spec-back.md` §6 — the D2 tolerance-reclass micro-task widens it) · **F1 → Kelly CAL → P5 tier values** once the pooled §9 read passes · **W6-4 CeilingAudit run** at the data gate · the **fee knob** (Aug-1 mechanics in `fee-aware-min-move-spec-back.md` §4 — the trader's decision, the net-EV sweep is built for it) · bridge-v2 **engine consumption build** (contract closed; queued behind nothing now).

**Fable double-check flow:** batch your judgment items; the trader relays ONE summary + review packet per batch (the `pre-aug1-batch-spec-back.md` §1-handles format — it worked). Spend the rationed Fable moments on ⚠ rulings and adversarial review, never on mechanics.
