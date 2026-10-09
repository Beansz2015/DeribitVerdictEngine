# Burst-watch read, ASIA and NY — INTERIM, 2026-10-08 (UTC)

**Spec:** [`avr1-atr-conditional-burst-watch-spec.md`](avr1-atr-conditional-burst-watch-spec.md) §5 (the ATR-conditional reference, trader ruling `AVR-1` (b), 2026-09-26). **Tool:** `tools/ops/burst-watch-read.ps1`. **Fetch:** `aws_fetch/20261008-174358` (fetch verified; venue check CLEAN; history comparison PASS). Seat: Opus 5.5, low → raised to medium when every day read below the reference.

⚠ **INTERIM.** The ruled length is ≥ 10 covered weekday session-days after 2026-09-25. ASIA has 9 and NY has 7 at this fetch. ASIA's 10th is the 2026-10-09 session (closes 08:00 UTC); NY's is the 2026-10-09 session (closes 2026-10-10 00:00 UTC). **The official read runs on a fetch after 2026-10-10 00:00 UTC (re-dated 2026-10-08 with the trader; if a 2026-10-09 session is uncovered, on the 2026-10-13 final fetch).**

## 0. Result

| Session | Covered days | Rows | Fires | Observed | Expected | Diff | Same-side | Band | Verdict (tool) |
|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| ASIA | 9 | 1,429 | 23 | 1.61 % | 4.78 % | **−3.17 pp** | 86.96 % | 1.78–7.78 % | MISS: rate-in-band, length |
| NY | 7 | 4,582 | 163 | 3.56 % | 6.35 % | **−2.79 pp** | 86.50 % | 4.35–8.35 % | MISS: rate-in-band, length |
| LONDON | 7 | 688 | 5 | 0.73 % | 3.33 % | −2.60 pp | 100 % | no ruled band | report only |

- **Every covered day reads below expected** (ASIA 9 of 9, NY 7 of 7, daily sd 1.1–1.4 pp). Unless the remaining days run well above the reference, the 2026-10-13 read is a MISS on rate in both sessions. Same-side passes in both (≥ 85 %).
- NY uncovered days: 2026-09-30 (the collector restart sequence) and 2026-10-08 (partial). LONDON uncovered: 2026-09-28, 2026-10-01 (the venue halt).

## 1. Is it a code edge? No evidence of one.

Run: `burst-watch-read.ps1 -FetchFolder aws_fetch/20261008-174358 -From 2026-09-08 -To 2026-09-25` (inside the reference window):

| Session | 2026-09-08 → 09-17 (instance `3fe57c53…`) | 2026-09-21 → 09-25 (`ee159d03…`, then the 09-24 deploy) |
|---|---|---|
| ASIA daily diff | −4.40 to +1.26 pp, mixed | −1.40 to −2.23 pp, all below |
| NY daily diff | −2.77 to +0.61 pp, mixed | −1.69 to −3.23 pp, all below |

- The drift starts about 2026-09-21, before the 2026-09-24 deploy, and runs on through every later instance.
- The 2026-09-21 recovery deploy (`584c616`) changed nothing on the burst path: `git diff 49d0098 584c616` shows no change to `Core/AggressorVelocityAccumulator.vb` or `MarketState.vb`; `DeribitWsFeed.vb` only gained `ws.Options.Proxy = Nothing` and a log seam; `UI/MainForm_Analysis.vb` only the spread refactor.
- `-VerifyReference` on `aws_fetch/20261007-180541`: **PASS**, every edge and rate reproduces (0.00 pp). The reference itself is intact.
- **Reading (hypothesis, not tested):** the burst fire rate has fallen below what ATR alone predicts since about 2026-09-21 — a market shift the ATR conditioning does not capture. The reference window (2026-08-02 → 09-25) ends inside the drift, which is why its own last weeks also read low.

## 2. What follows

- **Ruled response to a MISS** (T-5 for ASIA; `AVR-3` (a) for NY): a re-derivation READ, never a threshold change. `AVR-2` (a) holds the threshold until the burst outcome read (run 1 after 2026-11-25).
- **Nothing is owed before the holiday beyond the official read at the 2026-10-13 fetch.** If it confirms the MISS, the re-derivation read goes on the post-holiday list, next to burst outcome run 1 (both read the same instrument).

## 3. Verified and not verified

- **Verified (run this session):** both tool runs above, `-VerifyReference` PASS, the deploy diff.
- **Not verified:** the cause of the drift. No trade-count, volume or regime split was run.
