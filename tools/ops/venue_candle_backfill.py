#!/usr/bin/env python3
# tools/ops/venue_candle_backfill.py
#
# RSI and volume rule study, ruling RVS-1 = (d) (docs/rsi-volume-rule-study-session1-2026-10-08.md, ruling box):
# backfill the venue's own BTC-PERPETUAL candles, the input the live engine reads, so every study indicator is
# computed on them. Python 3 standard library only. Read-only: public GET requests to the venue, no key.
#
# Source: https://www.deribit.com/api/v2/public/get_tradingview_chart_data. The venue returns at most ~5000 bars per
# call and keeps the LATEST bars of a longer range (analysis/DeribitOhlcFetcher.vb:19-24), so each call asks for
# exactly 5000 bars. Ticks are bar OPEN times (ms).
#
# Output: <out>/candles_<res>m_YYYY-MM.csv, the same columns as backtest_data/candles_*.csv:
#   Timestamp,Open,High,Low,Close,Volume,Cost   (Volume BTC, Cost USD)
# plus <out>/backfill_<res>m.json: per-month expected bars, bars received, missing ticks (first 20), duplicates.
#
# Usage (from the repo root):
#   python -I tools/ops/venue_candle_backfill.py --res 1 3 --start 2023-01-01 --end 2026-10-08
#
# Runtime: about 540 calls; a few minutes.

import argparse, json, os, sys, time, urllib.request
from collections import defaultdict
from datetime import datetime, timedelta

URL = "https://www.deribit.com/api/v2/public/get_tradingview_chart_data"
EPOCH = datetime(1970, 1, 1)
CHUNK = 5000


def ms(dt):
    return int((dt - EPOCH).total_seconds() * 1000)


def get(params, tries=6):
    q = "&".join("%s=%s" % kv for kv in params.items())
    for k in range(tries):
        try:
            with urllib.request.urlopen(URL + "?" + q, timeout=30) as r:
                d = json.loads(r.read().decode("utf-8"))
            if "result" in d:
                return d["result"]
            raise RuntimeError("no result: %r" % d.get("error"))
        except Exception as e:  # network, HTTP 429, venue error: back off and retry
            if k == tries - 1:
                raise SystemExit("STOP: %s failed after %d tries: %s" % (q, tries, e))
            time.sleep(2 ** k)


def backfill(res, start, end, out_dir, pause=0.15, log=print):
    step = res * 60000
    t = ms(start)
    t_end = ms(end)
    bars = {}
    dup = 0
    calls = 0
    while t < t_end:
        e = min(t + (CHUNK - 1) * step, t_end - step)
        r = get({"instrument_name": "BTC-PERPETUAL", "resolution": res, "start_timestamp": t, "end_timestamp": e})
        calls += 1
        if r.get("status") == "ok":
            for i, tk in enumerate(r["ticks"]):
                if tk < t or tk > e:
                    continue
                if tk in bars:
                    dup += 1
                bars[tk] = (r["open"][i], r["high"][i], r["low"][i], r["close"][i], r["volume"][i], r["cost"][i])
        if calls % 50 == 0:
            log("  res %d: %d calls, at %s, bars %d" % (res, calls, EPOCH + timedelta(milliseconds=t), len(bars)))
        t = e + step
        time.sleep(pause)
    # per-month files and the completeness report
    by_month = defaultdict(list)
    for tk in sorted(bars):
        by_month[(EPOCH + timedelta(milliseconds=tk)).strftime("%Y-%m")].append(tk)
    rep = {"res": res, "start": start.isoformat(), "end": end.isoformat(), "calls": calls, "bars": len(bars),
           "duplicates": dup, "months": {}}
    os.makedirs(out_dir, exist_ok=True)
    m = datetime(start.year, start.month, 1)
    while m < end:
        nm = datetime(m.year + (m.month == 12), m.month % 12 + 1, 1)
        lo, hi = max(ms(m), ms(start)), min(ms(nm), t_end)
        expected = list(range(lo, hi, step))
        key = m.strftime("%Y-%m")
        have = set(by_month.get(key, ()))
        miss = [x for x in expected if x not in have]
        rep["months"][key] = {"expected": len(expected), "received": len(have), "missing": len(miss),
                              "missing_first": [str(EPOCH + timedelta(milliseconds=x)) for x in miss[:20]],
                              "zero_volume": sum(1 for x in have if bars[x][4] == 0)}
        with open(os.path.join(out_dir, "candles_%dm_%s.csv" % (res, key)), "w", encoding="utf-8", newline="\n") as f:
            f.write("Timestamp,Open,High,Low,Close,Volume,Cost\n")
            for tk in by_month.get(key, ()):
                f.write("%d,%r,%r,%r,%r,%r,%r\n" % ((tk,) + tuple(bars[tk])))
        m = nm
    with open(os.path.join(out_dir, "backfill_%dm.json" % res), "w", encoding="utf-8", newline="\n") as f:
        json.dump(rep, f, indent=1)
    tot_miss = sum(v["missing"] for v in rep["months"].values())
    log("res %d: %d calls, %d bars, %d duplicates, %d missing ticks; months with missing: %s"
        % (res, calls, len(bars), dup, tot_miss, {k: v["missing"] for k, v in rep["months"].items() if v["missing"]}))
    return rep


def main():
    ap = argparse.ArgumentParser(description="Venue BTC-PERPETUAL candle backfill (read-only, public)")
    ap.add_argument("--res", type=int, nargs="+", default=[1, 3])
    ap.add_argument("--start", default="2023-01-01")
    ap.add_argument("--end", default="2026-10-08", help="exclusive UTC date")
    ap.add_argument("--out", default="C:/DeribitData/venue-candles")
    a = ap.parse_args()
    start, end = datetime.strptime(a.start, "%Y-%m-%d"), datetime.strptime(a.end, "%Y-%m-%d")
    for res in a.res:
        backfill(res, start, end, a.out)


if __name__ == "__main__":
    main()
