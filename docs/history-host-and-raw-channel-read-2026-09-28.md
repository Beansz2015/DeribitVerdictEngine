# Deribit history host and the raw trades channel — read (2026-09-28 UTC)

**Asked by the trader 2026-09-28:** test the authenticated raw trades channel for the liquidation flag, and check what other data it carries that cannot be pulled historically, to cut data-collection time. **Author:** orchestrator seat. Read-only; no code, settings or box change.

---

## 0. Headline — the premise of forward-only trade capture is false

⭐⭐ **`history.deribit.com` serves the full BTC-PERPETUAL trade history, with every trade field including `liquidation`, back to at least 2020-01-01.** The main host (`www.deribit.com`) serves about 24 h and returns empty beyond it. **Nothing in this repo references the history host** (grep of `*.vb` and `*.md`, 2026-09-28).

- `architecture.md` Design Decisions, row "Trade capture in the app, as two redundant mechanisms (v64)", rests on *"Deribit's public trades endpoint serves ≈24 h and refuses older windows … append-forward is the only path"*. That is true of the main host only.
- **So the trade half of the data problem is a backfill problem, not a wait.** Every trade-derived input (CVD, MicroCVD, TFI, aggressor velocity, liquidations) can be re-derived for any past period.
- **The raw channel's remaining value is latency, not history.** Its trade object carries the same fields the history host already serves. What the raw channel might add is the `liquidation` flag *at trade time*, which the public stream and the main host's recent trades do not carry (the flag was observed arriving ~60 min late, `liquidation-probe-run-2026-09-21.md` §0000).

---

## 1. What was measured (2026-09-28, public endpoints, no key)

| # | Check | Command shape | Result |
|---|---|---|---|
| 1 | Old window, history host vs main host | `public/get_last_trades_by_instrument_and_time`, BTC-PERPETUAL, 2026-06-01 00:00–00:10 UTC | History host: trades returned (first `trade_seq` 290022664). Main host: `"trades":[]` |
| 2 | Fields on an old trade | same | `trade_seq`, `trade_id`, `timestamp`, `tick_direction`, `price`, `mark_price`, `index_price`, `instrument_name`, `direction`, `contracts`, `amount` |
| 3 | Liquidation flag, recent | history host, `get_last_trades_by_instrument`, seq `301706650..660` | `"liquidation":"T"` ×2 (the 09:45 UTC pair from the 24 h scan) |
| 4 | Liquidation flag, older | history host, around store seq `298837413` (a 2026-09-09 liquidation in the trade store) | `"liquidation":"T"` on seqs `298837418`, `298837428` |
| 5 | Depth | history host, 10-min windows at 2025-01-01, 2024-01-01, 2020-01-01 | Trades returned at all three (first seqs 230579801 · 191134363 · 32848558) |
| 6 | Completeness spot check | history host, 2026-06-01 00:00–01:00, one 1,000-row page | 1,000 trades over a seq span of exactly 1,000 — contiguous |

⚠ **Row 4 caveat:** the query asked for `start_seq=end_seq=298837413` and returned neighbouring seqs, so the single-seq form may not be honoured on this host. The flag's presence on old trades stands; the exact-seq call shape does not.

---

## 2. What this opens — and what it does not

| Area | What the history host changes | Status |
|---|---|---|
| **Trade store** | Backfill any past period instead of waiting. At ~140k trades/day, a year is about 50M rows, a few GB. The collector box has ~7 GB free, so a deep backfill belongs on the dev machine or a separate store | Needs a spec (§4) |
| **Gap repair** | Holes older than the main host's ~24 h could be repaired from the history host. The 2026-09-18 → 09-21 outage hole in the tape could be filled | Needs a spec |
| **Liquidation research** | Historical liquidations (flagged) for studies: size distribution for `large_liq_size` (`L-3`), the cascade alarm's thresholds, `A4` liq × OFI | Available now, offline |
| **Outcome reads** | A replay (`tools/BacktestRunner` replay) over backfilled trades regenerates trade-derived signals for months of history. ⚠ Not the whole engine: see the next rows | Needs a feasibility read |
| **Live liquidation vote and cascade alarm** | **Nothing.** The history host is historical; the flag still arrives ~60 min late for live scoring | Raw channel test (§3) |
| **Order book** (OFI, absorption, spread) | **Nothing.** No historical book API is known. Book data stays forward-only | Unchanged |
| **Ticker stream** (sub-minute OI, best bid/ask sizes, mark/index at ms) | **Nothing** known. OI history is not in the public API as far as this seat knows | Unchanged; not verified |

**So the forward-only list shrinks to:** the order book, sub-minute ticker fields (OI, top-of-book sizes), and the engine's own outputs (verdicts, placed levels). ⚠ The last two rows of the table rest on this seat's knowledge of the Deribit API, **not on a check this session.**

---

## 3. The raw trades channel test — plan (needs a key from the trader)

- **Question:** does `trades.BTC-PERPETUAL.raw` carry `liquidation` on the trade's first delivery? If yes, the live liquidation vote and cascade alarm have a real-time source. If no, the vote cannot be fed in time from any public source this seat knows of.
- **What it needs:** a Deribit API key with **read-only scope** (no trading, no withdrawal). ⛔ Never in the repo and never in the deploy allowlist: an environment variable on the dev machine only, for this test.
- **Instrument:** `tools/WsTradeProbe/LiqFlagProbe.vb` already subscribes the raw channel as an arm; without authentication it receives nothing (`raw 0` in run 4's status). The change is to authenticate (`public/auth`, `client_credentials`) before subscribing. Run on the dev machine for a few hours, ideally across a NY session, and pair raw-channel liquidation flags against the history host.
- **Also record, while connected:** the full raw trade object's property names, to confirm it carries nothing the history host lacks.
- **Seat:** Model: Sonnet 5 · Effort: medium. The probe exists; the change is authentication plus a tally. Escalate to Opus if the auth flow or the pairing is ambiguous.

---

## 4. Decisions for the trader — `HH-1` to `HH-3` (new IDs, checked free in `docs/`, `Core/`, `tools/`, `verify/`)

| ID | Question | Options | Orchestrator read |
|---|---|---|---|
| `HH-1` | Adopt the history host as a data source? | (a) Yes: spec a backfill tool and a gap-repair fallback · (b) Use it offline only, for one-off reads · (c) No | **(a)**, specced after a short validation read (§5). It records more and removes a waiting cost from every trade-derived study. ⚠ Scope and storage are design questions for the spec, not for this read |
| `HH-2` | Mint a read-only API key for the raw-channel test? | (a) Yes, dev machine only, env var · (b) No | **(a)**. It is the only open route to a real-time liquidation flag. The 2026-08-20 ruling already cleared credentials for the collector; this is narrower (dev machine, read scope, one test) |
| `HH-3` | Hold engine-fix B2 until §3 answers? | (a) Hold · (b) Proceed with REST enrichment anyway | **(a)**. The `D-4` ruling's premise (a flag obtainable in time to score) is in doubt; B2 was not deploying before the holiday anyway |

---

✅ **RULED 2026-09-28 (trader): `HH-1` = (a), `HH-2` = (a), `HH-3` = (a) (hold B2).**

✅ **`HH-2` SCOPE WIDENED AGAIN 2026-10-05 (trader):** if the raw order-book absorption test needs more than 6 h, it runs on the TEMPORARY AWS instance (`i-0b17cf2c2eb67496e`, or a successor temp instance) instead of the dev machine, which is shut down daily by 03:00–04:00 GMT+8. The key goes on the box only as an env file (mode 600), never on a command line or in a committed file, and leaves with the instance at termination. The collector box is still excluded.

✅ **`HH-2` SCOPE WIDENED 2026-09-29 (trader):** the read-only key (env vars `DERIBIT_RO_CLIENT_ID` / `DERIBIT_RO_CLIENT_SECRET`) may also be used for the **raw order-book absorption test** (`book.BTC-PERPETUAL.none.10.raw` against the 100 ms fold; the surviving test in `trader-tick-queue.md` §2's "REFUTED ON DIRECTION" row). Every other `HH-2` condition stands: **dev machine only**, env vars only, never on the collector box, never in the repo or a log.

## 5. Next steps, holiday-aware (trader away 2026-10-14 → 2026-11-25)

| Step | When | Model + effort |
|---|---|---|
| Raw-channel test, once the key exists (`HH-2`) | Before 2026-10-14 if the key arrives in time | Sonnet 5, medium |
| **History-host validation read:** completeness over a long span (seq gaps across days), agreement with the live store on an overlap window, rate limits, field coverage by era | Before 2026-10-14; read-only, low risk | Opus 5.5, medium |
| Backfill and gap-repair spec (`HH-1` (a)) | After the validation read; build after the holiday | Opus 5.5, high |
| Re-plan any study that waits on forward trade accrual (the burst outcome read's power problem is the first candidate: a replay could give it months more rows) | After the backfill exists | Orchestrator |

---

## 5a. ✅ Validation read — DONE 2026-09-28 (UTC)

**Instrument:** [`tools/ops/history_host_validate.py`](../tools/ops/history_host_validate.py) (read-only, Python stdlib). **Paging rule, measured:** the history host widens a `start_seq`/`end_seq` range to **whole milliseconds** — every trade sharing a ms with either bound is returned, unordered inside the ms. Nothing is lost at a page edge, but pages overlap and must be de-duplicated on `trade_seq`. (This is why the single-seq query in §1 row 4 returned neighbours.) Paging by timestamp with `+1 ms` would lose same-ms trades, the 2026-09-14 gap-repair lesson; do not use it.

Command (Python output pasted; the separator was `·` in the run and garbled by the Windows console, since fixed to `|` in the script):
```
python tools/ops/history_host_validate.py --store aws_fetch/20260928-121255/backtest_data --from 2026-09-27T00:00:00Z --to 2026-09-28T12:00:00Z --old-day 2025-06-02 --flagged-seqs <94 seqs from the 24 h main-host scan>
```
```
store window 2026-09-27T00:00:00Z -> 2026-09-28T12:00:00Z: rows=257185 seq 301465038..301722239 (span 257202, store gaps 17)
A  history host in [301465038,301722239]: 257202 trades, missing 0
B  seqs only in store: 0  only on history host: 17 (first [301702456, 301702457, ...])
B  field mismatches on shared seqs: {'trade_id': 0, 'timestamp': 0, 'price': 0, 'amount': 0, 'direction': 0}
C  liquidation-flagged: history host 94 | store 0 | store flagged but history not 0 | history flagged, store 'none' 94
C  main-host scan flagged seqs in window: 94 | flagged on history host: 94 | not flagged there: []
A  2025-06-02: seq 250755193..250852895 span 97703, got 97703, missing 0
D  2020-01-02: ['amount', 'direction', 'index_price', 'instrument_name', 'mark_price', 'price', 'tick_direction', 'timestamp', 'trade_id', 'trade_seq']
D  2022-01-03: [same as 2020]
D  2024-01-02: [2020 fields + 'contracts']
D  2026-06-01: [2020 fields + 'contracts']
E  requests=451 errors=0 retries=0 has_more_splits=0 elapsed=425s rate=1.1/s
```

| Check | Result |
|---|---|
| **A — completeness** | **0 missing** over 36 h (257,202 trades) and over the full day 2025-06-02 (97,703 trades) |
| **B — agreement with the store** | **0 mismatches** on `trade_id`, `timestamp`, `price`, `amount`, `direction` across all 257,185 shared trades; 0 trades only in the store. The history host holds the **17** trades the store still lacks from the 2026-09-28 09:20 outage (`301702456..472`) |
| **C — liquidation flags** | History host flags **94**, the same set the main-host scan flagged in its 24 h (94 of 94). ⚠ **The store flags 0 in this window**: the streamed copies all read `none`, so the live store's liquidation column is empty wherever repair did not re-fetch |
| **D — fields by era** | The same 10 fields from 2020; `contracts` added by 2024. `liquidation` appears only on liquidation trades |
| **E — rate** | 451 requests, **0 errors**, ~1.1 req/s at a 150 ms pause (~880 trades/s). At that pace a year (~50M trades) is roughly 16 h of fetching. The venue's rate limits were not probed |

**Verdict for `HH-1`: the history host is a complete, exact and flag-bearing source for BTC-PERPETUAL trades, in the windows tested.** The backfill and gap-repair spec can proceed on it.

## 6. What I did not verify

- Completeness beyond one 1,000-row page, and agreement between the history host and the live store on the same trades.
- Rate limits and terms of use for the history host.
- Whether older eras carry every field (2020 trades were counted, not inspected).
- Whether `liquidation` on the history host is complete (every liquidation flagged) — only present where checked.
- Any claim in §2 about the order book, OI history or the ticker stream (API knowledge, not checked).
- Whether the replay tool can use backfilled trades without book data, and what it would regenerate.
