# SwingFallbackRead output: --mode liqflag (liquidation flag and attribution evidence)

- Run at (UTC): 2026-09-16 09:37:31
- Pooled log: C:\Dev\DeribitVerdictEngine\AWS-copybacks\pooled-book-2026-09-09\analysis_log_pooled.csv
- Box logs: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv.v0.7.bak + C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_log.csv
- Eval cache: C:\Dev\DeribitVerdictEngine\aws_fetch\20260913-153704\analysis_eval_cache.csv
- settings.json version 68, sha256 A059DEC578D8B4C7 (copy identical)
- Session ASIA: hours 0-7 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 1.25xATR
- Session LONDON: hours 8-12 UTC inclusive, execution_resolution 3, windows 15/30/45 min, fallback target 2xATR
- Session NY: hours 13-23 UTC inclusive, execution_resolution 1, windows 5/10/15 min, fallback target 1.75xATR
- Trading week: Monday 00:00 UTC to Friday 24:00 UTC (exclusive)
- Fees: style maker_maker, round trip 3.00 bps (maker 1.5, taker 3.5). Taker-stop case: 3.00 bps on a target hit, 5.00 bps on a stop hit or a marked exit

- Read: docs/medium-tier-bug-hunt-2026-09-16.md, resumed session. Deribit field meaning: public/get_last_trades_by_instrument (`direction` = the taker's side; `liquidation` M = maker side liquidated, T = taker side, MT = both).
- Window: the last 500 trades at or before the row timestamp, copies of one trade collapsed (a flag survives if any copy carries one).

## 1. Trade store

- Files: trades_2026-07.csv, trades_2026-08.csv, trades_2026-09.csv. Rows: 3710718. Span: 2026-07-31 21:49:57 to 2026-09-13 15:36:35 UTC.

| Liquidation flag | Taker direction | Rows |
|---|---|---|
| M | buy | 1 |
| T | buy | 14 |
| T | sell | 82 |

## 2. Copies of one liquidation trade (finding L-1)

| Measure | Rows |
|---|---|
| Liquidation-flagged store rows | 97 |
| ... with an identical copy (same timestamp, price, amount, direction) whose flag is `none` | 93 |
| ... where that `none` copy was appended EARLIER in the same file (the streamed copy) | 93 |
| ... carrying a trade id on both copies | 91 |
| ... where the `none` copy has the SAME trade id | 91 |

| Flagged row (UTC) | File, line | Flag | Direction | Amount USD | Copies (flag @ line) |
|---|---|---|---|---|---|
| 2026-08-04 18:15:28.394 | trades_2026-08.csv 137699 | T | buy | 440 | none @ 115850; T @ 137699 |
| 2026-08-05 03:19:17.608 | trades_2026-08.csv 157143 | T | sell | 5000 | none @ 124961; T @ 157143 |
| 2026-08-17 03:55:49.943 | trades_2026-08.csv 488271 | T | buy | 50 | T @ 488271 |
| 2026-08-17 03:55:49.943 | trades_2026-08.csv 488272 | T | buy | 20 | T @ 488272 |
| 2026-08-17 06:46:21.124 | trades_2026-08.csv 516803 | T | buy | 2900 | T @ 516803 |
| 2026-08-17 06:46:21.124 | trades_2026-08.csv 516804 | T | buy | 2100 | T @ 516804 |
| 2026-09-09 15:01:39.534 | trades_2026-09.csv 963580 | T | sell | 39130 | none @ 867362; T @ 963580 |
| 2026-09-09 15:01:39.534 | trades_2026-09.csv 963581 | T | sell | 4040 | none @ 867363; T @ 963581 |
| 2026-09-09 15:01:39.534 | trades_2026-09.csv 963582 | T | sell | 3000 | none @ 867364; T @ 963582 |
| 2026-09-09 15:01:39.534 | trades_2026-09.csv 963583 | T | sell | 6000 | none @ 867365; T @ 963583 |
| 2026-09-09 15:01:39.534 | trades_2026-09.csv 963584 | T | sell | 37920 | none @ 867366; T @ 963584 |
| 2026-09-09 15:01:39.534 | trades_2026-09.csv 963585 | T | sell | 6000 | none @ 867367; T @ 963585 |

## 3. Collector rows whose trade window holds a liquidation (finding L-1)

| Measure | Rows |
|---|---|
| Collector log rows inside the store span | 36327 |
| ... whose 500-trade window holds at least one liquidation trade | 32 |
| ... of which the collector logged LiqSignal other than NONE | 0 |
| ... of which the collector logged a non-zero liquidation size | 0 |

| Row (UTC) | Verdict | Logged LiqSignal | Logged long / short size | Liquidation trades in window | Of which maker-side (M, MT) |
|---|---|---|---|---|---|
| 2026-08-04 18:16:02 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-04 18:17:11 | LONG | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-04 18:18:03 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-04 18:19:02 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-04 18:20:01 | LONG | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-04 18:21:03 | WEAK LONG | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-04 18:22:16 | WEAK LONG | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-04 18:23:10 | LONG | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-04 18:24:02 | WEAK LONG | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-08-05 03:21:01 | WEAK SHORT | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-09 15:02:03 | NO TRADE | NONE | 0.00 / 0.00 | 21 | 0 |
| 2026-09-09 15:04:01 | NO TRADE [WEAK SHORT] | NONE | 0.00 / 0.00 | 5 | 0 |
| 2026-09-09 15:12:01 | SHORT | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-09 15:23:01 | SHORT | NONE | 0.00 / 0.00 | 20 | 0 |
| 2026-09-09 15:24:02 | SHORT | NONE | 0.00 / 0.00 | 3 | 0 |
| 2026-09-09 19:46:01 | WEAK SHORT | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-09 19:47:08 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-09 19:48:01 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-09 19:49:02 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-09 19:50:01 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-09 19:51:01 | SHORT | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-10 07:36:01 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-10 07:39:01 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-10 10:36:05 | WEAK SHORT | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-10 10:39:03 | NO TRADE | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-11 09:39:02 | WEAK SHORT | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-11 09:42:03 | WEAK SHORT | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-11 13:50:02 | WEAK LONG | NONE | 0.00 / 0.00 | 2 | 0 |
| 2026-09-11 14:00:03 | WEAK LONG | NONE | 0.00 / 0.00 | 2 | 0 |
| 2026-09-11 14:05:04 | WEAK LONG | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-11 14:06:01 | WEAK LONG | NONE | 0.00 / 0.00 | 1 | 0 |
| 2026-09-11 14:08:01 | NO TRADE | NONE | 0.00 / 0.00 | 3 | 0 |

## 4. Maker-side liquidations (finding L-2)

- Collapsed tape liquidation trades: 96; flag T: 95; flag M: 1; flag MT: 0.
- 2026-09-11 12:30:07.253 UTC: flag M, taker direction buy, 20 USD. CalcLiquidations books it as a SHORT liquidation; Deribit's flag says the liquidated account was the maker on the sell side, a LONG.
- Collector rows whose window holds a maker-side liquidation: 0.

## 5. Logged LiqSignal

| Rows | Count | NONE | LONG LIQS | SHORT LIQS | Non-zero size |
|---|---|---|---|---|---|
| All merged rows from the v51 edge (pooled book + box live log) | 51107 | 51107 | 0 | 0 | 0 |
| Swing read population rows | 8810 | 8810 | 0 | 0 | 0 |

## 6. Units of `amount` (finding L-3)

- Store rows whose amount is a whole multiple of 10: 3710718 of 3710718 (100.0000 %). BTC-PERPETUAL trades in 10 USD contracts, so amounts are USD, not BTC.
- Median liquidation trade amount: 6000 USD. `large_liq_size` 200 is compared with these USD sums.

