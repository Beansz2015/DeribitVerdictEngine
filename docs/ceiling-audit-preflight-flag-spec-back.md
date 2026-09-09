# Spec-back — `D-1` `--preflight` flag for `CeilingAudit`

**Spec:** [`ceiling-audit-preflight-flag-spec.md`](ceiling-audit-preflight-flag-spec.md), baseline commit `d5ce4a5`.
**Status:** built, all acceptance criteria met, **not yet committed** — commit is the orchestrator's per §7.

**Format note.** [`batch-review-packet-convention.md`](batch-review-packet-convention.md) calls for a summary doc + this spec-back on a *multi-lane batch*. This build is one item against one spec, so this single document carries both — the packet's four sections below, no separate `-summary.md`.

---

## 1. Ranked verification handles

⛔ Labelled per the rule in `CLAUDE.md`: `H-n` = reader can run it now, `E-n` = build-time evidence that cannot be re-run. Everything here is `H-n` — nothing was routed through a throwaway instrument.

**If you only run one, run `H-1`.** It is the load-bearing claim the whole spec exists for (§1: *"a pre-flight number that disagrees with the full run's number is worse than no flag at all"*).

| # | Command | Confirms |
|---|---|---|
| **`H-1`** | `dotnet tools/CeilingAudit/bin/Debug/net8.0/CeilingAudit.dll AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --preflight` | `PREFLIGHT_ELIGIBLE_ROWS=8269` — matches the 2026-09-09 full run's `8269` exactly. Exit `$?` = `0`. No `[CeilingAudit] OHLC fetch span …` line — confirms no network call before the exit |
| **`H-2`** | Run the same command twice, once with `--preflight --out /tmp/x` and once with `--out /tmp/x --preflight`, diff the two stdout captures | Byte-identical output both orders — `--out`'s value is never swallowed by the no-value `--preflight` switch (Trap 3) |
| **`H-3`** | `dotnet verify/ordercheck/bin/Debug/net8.0/OrderCheck.dll` — check the tail for `ALL PASS` and count `PASS` lines | `346` `PASS` lines (was `345` at baseline), `0` `FAIL` lines |
| **`H-4`** | `git diff --stat` | Only `tools/CeilingAudit/CeilingAuditProgram.vb` and `verify/ordercheck/Program.vb` listed — `settings.json` absent (AC-6) |

**Arithmetic identity, not just a headline count.** `A70a` (`verify/ordercheck/Program.vb`) pins:

```
TotalRows − BurstCadenceRowsExcluded − NonV08Excluded − WeekendExcluded − NonDirectionalExcluded = eligible
```

on a synthetic 5-row CSV (`5 − 0 − 1 − 1 − 1 = 2`, and `LoadAndBuild` returns 2 rows). This is the property the printed `--preflight` block is a *view* of — pinning the arithmetic rather than the string format means a future rewording of the printed labels can't silently break the count itself unnoticed.

---

## 2. Decisions queued

**None outstanding from this build.** The spec's own §0.2 escalation trigger (*"if closing `P-5` in full requires a `LoadStats` field, STOP"*) did not fire — `P-5`'s duplicate-`(InstanceId, SignalId)`-pair half was never attempted, per §3's explicit scope line, so there was nothing to escalate.

One item to **record, not decide** — the spec already ruled it (§3, "recommendation, not built"):

- **Queue row:** the `P-5` duplicate-identity-pair scan stays a manual, out-of-tool check until a pooled read actually needs it inside `CeilingAudit`. I have not added this row to `docs/trader-tick-queue.md` — flagging that as a gap in this build rather than doing it silently, since the spec didn't list queue-row maintenance as part of §2's build table.

---

## 3. Spec feedback

**What the spec got right, specifically.**

- **The trap list (§0.1) was exhaustive and in the right order of likelihood.** Trap 3 (argument-swallowing on a no-value switch copied from a value-consuming neighbour) is exactly the class of bug this codebase's `Select Case` parser invites — writing `H-2` against both argument orders before touching the code would have caught it if the naive copy-paste had happened.
- **Naming the exact block to emit (§2), field-for-field against `LoadStats`,** removed any judgment call about what counts as "the full stats" — there was no ambiguity to introduce.
- **§3's table format (closed / not closed per row) is more useful than a prose caveat** — it made the one open gap (`P-5` second half) impossible to miss when writing this spec-back.

**Which assumptions broke.** None. The one place I checked before trusting the spec's own framing: §4 asserts `CeilingAuditProgram` is unreachable from `OrderCheck.vbproj`'s harness, so argument-parsing coverage is manual (`H-2`), not a fixture. I confirmed this by reading `OrderCheck.vbproj`'s references before writing `A70a` — it links `CsvFeatureBuilder.vb`, `L2Logistic.vb`, `FeatureMatrix.vb`, `AuditMetrics.vb` and nothing that pulls in `CeilingAuditProgram.Main`. The spec's claim held.

**Where the spec was narrower than its own words.** Nowhere found. §2's build table and §7's "must NOT do" list were both concrete enough that there was no end-to-end-sounding requirement that turned out to be offline-only or partial.

**Constraint pairs that nearly conflicted.** None. `LoadAndBuild` called once (§0.1 Trap 1) and the pre-flight exit sitting after `:127-128` but before the OHLC block (§0.1 Trap 2) compose without tension — the existing code already had a natural seam there (`populations` is computed, printed, and nothing after it is needed for the pre-flight block).

---

## 4. What I did not verify, and cannot

- **The frozen book's MD5 is a check I ran (`E8418846838FF97F3C90F782A95B3523`, matched), but I did not re-derive the 2026-09-09 full run's `8269`** — I took that figure from the spec (§5, §1) as the reference value pre-flight must match. If that number was itself wrong when recorded, this build would pass a check against a wrong baseline and I would not know.
- **`--out`'s directory-creation behaviour under `--preflight` is unverified** — because pre-flight exits before `Directory.CreateDirectory(outDir)` is reached (that call sits after the OHLC/model/report block), neither `H-2` run created `/tmp/pf1` or `/tmp/pf2`. This is correct behaviour (no report is written), but it means `H-2` only proves the *value* of `--out` survives parsing, not that the directory path is otherwise exercised.
- **Network-call absence (`H-1`'s "no OHLC fetch line") is inferred from stdout, not from an actual network-level check** (no packet capture, no mock verification that `DeribitOhlcFetcher` was never constructed). The `Return 0` sits textually before the `Await DeribitOhlcFetcher.FetchOhlcRange(...)` call, so this is a code-reading guarantee, not a runtime-instrumented one.
