# Gap repair — same-millisecond page skip — spec-back

**Written:** 2026-09-14 (UTC) by the scoped spec seat. **For:** the orchestrator seat that wrote the brief. **Record:** the spec itself, [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) (commits `3c2cdd2`, `a8b9d27`). No separate summary: this was one spec, not a multi-lane batch ([`batch-review-packet-convention.md`](batch-review-packet-convention.md)). **Handles pinned to:** `a8b9d27`.

**Review recommendation**
Model / effort: **Opus · high.** `GR-1` amends a ruled decision (`DR-1`, see below), and CLAUDE.md puts anything correcting a prior ruling at high.

**IDs, defined once:**

- `GR-1` to `GR-5` — decision rows in [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) §3.2.
- `DR-1` — a ruled follow-up in [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §1. It removed the `MinHoleMs` width floor and kept the inverted-window drop as "inherent".
- `A56a`–`A56g` — shipped harness fixtures for `ResolveRepairWindowsMs`. `A78b` — shipped harness fixture, venue pager across a same-ms page boundary. `A79a`–`A79e` — **planned** fixture IDs in the spec, not in the tree.
- `H-n` — a handle you can run. `E-n` — evidence you cannot re-run as-is.

---

## 1. Ranked verification handles

⭐ **If you only run one, run `H-1`.** It carries the whole impact claim: the defect already fired in production.

Every handle below was run on 2026-09-14 (UTC) at `a8b9d27`. The output is pasted as received.

| # | Proves | Load-bearing value | Output |
|---|---|---|---|
| **H-1** | The skip already happened, and later passes did not heal it | 16 small holes · 13 left rows at `L_pos_in_block` = 62,229 + 1,000·k · 3 at 2,000 / 3,000 / 5,000 · **identity: 14,475 − 14,405 = 70 trades lost** | See the spec's §10, `H-4`. 17 holes, `missing=14475` |
| **H-2** | The mechanism: the hole window excludes the bracket milliseconds | `:853` hole `prev.TsMs + 1L, cur.TsMs - 1L` · `:881` inverted drop · `:918` tail `+ 1L` | `853:` · `881:` · `918:` |
| **H-3** | Both venue bounds are inclusive, on fresh tape — the premise `GR-1` rests on | `start = end = ms` returns every trade in that ms | `ms=1789411386112 trades_at_ms_in_list=2` · `start=end=ms returns: 2` |
| **H-4** | The cursor line | `314` | `314:                cursorMs = newestMs + 1` |
| **H-5** | The `GR-3` headroom | 139 (August from 2026-08-12) and 107 (September) against a 1,000 cap | as stated |
| **H-6** | The `GR-1` class size | 62.34 % of September trades share a ms with a `trade_seq` neighbour | `trades=1235249 adjacent_same_ms=44.31% sharing_ms_with_seq_neighbour=62.34%` |
| **H-7** | No console capture in the tree (`GR-4`) | empty | *(empty)* |
| E-1 | The skip on the venue, step by step (probes P1–P6) | P3 returns nothing under a `+ 1` cursor · P6 refuses `count=1001` | The spec's §10, `E-1`. ⚠ The trades aged out of the venue after ~24 h; `H-3` replaces P1 and P4 |
| E-2 | 48.8 % of page boundaries skip ≥ 1 trade (simulation) | 60 of 123 boundaries, 242 trades | One-off awk over the local store, not kept |

**H-1** — needs the gitignored copy-back folder in the main checkout. Command in the spec's §10, `H-4`.

**H-2**

```bash
grep -n 'New LongRange(prev.TsMs + 1L, cur.TsMs - 1L)\|tailStart = rows(rows.Count - 1).TsMs + 1L\|If e < s Then' Core/TradeStoreWriter.vb
```

**H-3** — live venue, read-only. Picks a fresh same-ms pair, so it does not decay.

```bash
B="https://www.deribit.com/api/v2/public/get_last_trades_by_instrument_and_time?instrument_name=BTC-PERPETUAL&sorting=asc"
now=$(date -u +%s%3N); L=$(curl -s "$B&start_timestamp=$((now-600000))&end_timestamp=$now&count=1000")
T=$(printf '%s' "$L" | grep -o '"timestamp":[0-9]*' | cut -d: -f2 | uniq -d | head -1)
N=$(printf '%s' "$L" | grep -o '"timestamp":[0-9]*' | grep -c ":$T\$")
echo "ms=$T trades_at_ms_in_list=$N"
echo "start=end=ms returns: $(curl -s "$B&start_timestamp=$T&end_timestamp=$T&count=1000" | grep -o '"trade_seq":[0-9]*' | wc -l)"
```

If either bound were exclusive, the last line would print 0.

**H-4**

```bash
grep -n 'cursorMs = newestMs + 1' tools/BacktestRunner/HistoricalStore.vb
```

**H-5**

```bash
D=/c/Dev/DeribitVerdictEngine/aws_fetch/20260913-153704/backtest_data
for f in "$D/trades_2026-08.csv" "$D/trades_2026-09.csv"; do
awk -F, 'NR>1 && $7!="" && $1>=1786492800000 && !seen[$6]++ {c[$1]++} END{for(t in c) if(c[t]>max) max=c[t]; printf "%s max_trades_in_one_ms=%d\n", FILENAME, max}' "$f"
done
```

**H-6**

```bash
D=/c/Dev/DeribitVerdictEngine/aws_fetch/20260913-153704/backtest_data
awk -F, 'NR>1 && $7!="" && !s[$6]++ {print $7","$1}' "$D/trades_2026-09.csv" | sort -t, -k1,1n | awk -F, '{q[NR]=$1; t[NR]=$2} END{for(i=2;i<=NR;i++) if(q[i]-q[i-1]==1){a++; if(t[i]==t[i-1]) s++} for(i=2;i<NR;i++) if(t[i]==t[i-1]||t[i]==t[i+1]) n++; printf "trades=%d adjacent_same_ms=%.2f%% sharing_ms_with_seq_neighbour=%.2f%%\n", NR, 100*s/a, 100*n/(NR-2)}'
```

**H-7**

```bash
grep -rn 'Console.SetOut\|Console.SetError' --include=*.vb .
```

---

## 2. Decisions queued, with my read

⚠ **Each read is a hypothesis.** ⛔ **Three of my five reads (`GR-3`, `GR-4`, `GR-5`) are the cheaper option.** The CLAUDE.md auto-proceed ruling names that exact pattern as the one this seat class gets wrong, so they are reserved rather than taken.

| # | Question | Options | My read | Class |
|---|---|---|---|---|
| **GR-1** | Do repair windows include the bracket rows' own milliseconds? | **(a)** pager fix only · **(b)** hole windows `[L.ts, R.ts]` bounded by `L.seq < seq < R.seq`; tail unchanged · **(c)** (b) plus tail `[max.ts, segEnd]` bounded by `seq > max.seq` | **(c).** The more truthful option. (a) leaves 62.34 % of single-trade losses unrepairable by construction (`H-6`). `DR-1`'s "no sub-millisecond query" premise is false (`H-3`). (b) heals a tail same-ms loss only one pass later | ⛔ Reserved — amends `DR-1`; changes tape-store writes |
| **GR-2** | One shared pager, or two? | **(a)** one pager in engine-linked code · **(b)** two pagers plus contract fixture `A79e` · **(c)** a shared pure step function | **(b), taken.** A shared pager makes the venue check share the defect it audits: both lists miss the same trade, and the diff reads `CLEAN`. Mechanism argument (step 3 of the CLAUDE.md three-step test) | Taken, named. Overrule if you weigh no-drift above audit independence |
| **GR-3** | A full page inside one millisecond? | **(a)** fail that window loudly; the hole stays for the next pass · **(b)** fall back to `get_last_trades_by_instrument` with `start_seq`/`end_seq` · **(c)** larger `count` — ⛔ refused by the venue | **(a).** ⚠ **Cheaper, and (b) records more.** Headroom is about 7× (`H-5`), and (b) is the third pager contract the brief ruled out | ⛔ Reserved |
| **GR-4** | A durable record of a failed or stalled window? | **(a)** Console only; the durable record is the `trade_seq` hole · **(b)** a durable repair log for every window outcome | **(a) now, plus a queue row for (b).** ⚠ **"Defer" is the tell.** Console is not captured anywhere in the tree (`H-7`). That gap already covers every repair failure reason today | ⛔ Reserved |
| **GR-5** | Not ready before the S2 deploy? | **(a)** S2 deploys without it · **(b)** S2 waits | **(a).** Absorption trap `T-2` (two deploys make three `analysis_log.csv` eras) does not apply: this build touches no CSV. ⚠ **The trade:** permanent loss on any outage over ~15 min before the fix ships | ⛔ Reserved — deploy |

**Shared roots.**

- ⭐ **`GR-3` and `GR-4` share one root: what a window that cannot finish leaves behind.** Rule them together. If `GR-4` goes to (b), the cost argument against `GR-3` (b) weakens, because a logged stall becomes observable.
- **`GR-1` sets the build tier.** (a) → Opus, medium. (b) or (c) → Opus, high.
- **`GR-5` is probably moot.** The build is one session and S1 opens no earlier than 2026-09-15 (UTC). I did not verify S2's date.

**Scoping — the narrowest version of each option. Not recommendations.**

| Option | Touches |
|---|---|
| `GR-1` (a) | `tools/BacktestRunner/HistoricalStore.vb` only · `A79a`–`A79c`, `A79e` · no `A56` change |
| `GR-1` (b) | Plus `Core/TradeStoreWriter.vb` hole path (`LongRange` bounds, `InSeqBounds`) · `A79d` parts 1–3 · repoint `A56a`, `A56b` parts 4–5, `A56e`, `A56f` |
| `GR-1` (c) | Plus the tail path · `A79d` part 4 · also `A56b` parts 1–2, `A56c`, `A56d`. `A56b`'s "tail start equals `ResolveResumeCursorMs`" and "covered store gives an empty list" invariants are restated |
| `GR-3` (b) | A second endpoint URL, a parse, and a seq-range loop in `HistoricalStore.vb`, plus fixtures for them |
| `GR-4` (b) | A new durable file or log line. ⚠ Not scoped: whether `tools/ops/collector.ps1 fetch` must learn to copy it back |

**When you rule:** record each ruling in the spec's §3.2 cell where the question lives, then add a response section here.

---

## 3. Spec-back proper — feedback on the brief

### 3.1 What the brief got right

- **"Reuse that contract; do not invent a third one."** It kept `GR-3` from growing into a seq-endpoint design. It also forced contract reuse and code reuse apart, which is what makes `GR-2` a real choice.
- **"Read the code; state whether loss is permanent or self-heals."** The right demand. The answer was not in the pager at all.
- **"A fixture that FAILS against today's +1 cursor first."** It made the spec specify a fail-first build order instead of a test written after the fix.

### 3.2 ⛔ Assumptions that broke

- ⭐ **The brief framed self-healing as conditional: "under what conditions it does not (e.g. a hole window again longer than one page)".** Hole length is not the condition. The repair window `[L.ts + 1, R.ts − 1]` excludes the millisecond the skipped trades sit in. **Every hole of this shape fails, at any length.** The page skip and the repair miss are the same `+ 1` twice, in two files.
- **So the defect is not only in `HistoricalStore.vb`.** Its twin lives in `Core/TradeStoreWriter.vb` (`:853`, `:918`). That pulls `GR-1` and a ruled decision (`DR-1`) into scope, which the brief did not anticipate.
- **The brief classed it as "not scoring and not a rendered value".** True, but incomplete: it also amends a ruled decision. That raises the review tier.
- **"Impact to date" was answerable by measurement, not only by reading code.** The 2026-09-13 copy-back already holds the evidence: 16 holes, 70 trades (`H-1`). The queue row's "NOT verified" was one scan away.
- **Only `start_timestamp` was measured.** `GR-1`'s `[T, T]` query also needs `end_timestamp` inclusive. I measured it (`H-3`).

### 3.3 Where the brief was narrower than its words

- **"A fixture that FAILS against today's code" cannot run against today's code as-is.** The pager has no injectable page source. The spec substitutes a sequence: extract the seam with `+ 1` kept, run `A79a` and watch it fail, then fix. **Cost:** the fail-first run becomes build-time evidence (`E`). A reviewer re-runs it only by re-applying the named mutation by hand, as with `A78b`.

### 3.4 A constraint set that nearly conflicted

- **"`HistoricalStore.vb` is engine-binary" + "reuse `CoverageReport`'s contract" + "no third contract"** read together as "share the code". **The hatch: a contract is not a code path.** Two implementations of one contract, pinned by one fixture, satisfy all three, and keep the audit independent. Write that distinction into the next brief that says "reuse".
- **"Ride the S2 deploy" + absorption trap `T-2` (one deploy, one edge).** Compatible, because `T-2` is about `analysis_log.csv` eras and this build writes none. Named in `GR-5`.

### 3.5 Found in passing — outside the brief

- ⚠ **`trades_2026-09.csv` in the copy-back holds 298,934 duplicate rows** (1,534,183 rows, 1,235,249 distinct `trade_id`s, 19.5 %). Cause not investigated. I raised a task chip for it; I did not add a queue row.
- **Console output is not captured anywhere in the tree** (`H-7`). Every repair failure log line is probably invisible on AWS, not only this one. It feeds `GR-4`.

---

## 4. What I did not verify

- **That the 70 lost trades shared their left row's millisecond.** The venue history is gone. The inference rests on the exact 1,000-row spacing and on the rate (16 of about 30 boundaries, against 48.8 % simulated).
- **That later 6-hourly passes ran after 2026-08-17 16:23 UTC.** No durable repair log exists. The app was alive through 2026-08-18 (`ws_health.log` entries, carried from [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a).
- **That `trade_seq` order always agrees with timestamp order.** The shipped walk assumes it too.
- **How the AWS app's standard output is launched or captured.** Only the in-tree absence is verified.
- **Whether the coverage report grades such an hour `Defect`.** Carried from CLAUDE.md's worked example; not re-read in code.
- **The S2 deploy date.** Not read.
- **No build and no harness run.** This was a spec. The `A56` line references are from `2fa22da`; the spec commits touched no `.vb` file.
- **Scoped seat:** I did not read `docs/DeribitIndicatorProject.md`, `docs/architecture.md` or `docs/trader-profile.md`.
