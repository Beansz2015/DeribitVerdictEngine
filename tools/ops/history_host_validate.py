"""History-host validation read (docs/history-host-and-raw-channel-read-2026-09-28.md, HH-1 = (a)).

Read-only. Python 3 stdlib. Pages BTC-PERPETUAL trades from https://history.deribit.com by
trade_seq and checks:
  A  completeness  - every seq in [lo, hi] present (no gaps), per window
  B  agreement     - same trade_seq -> same trade_id / timestamp / price / amount / direction as
                     the local trade store (aws_fetch/<folder>/backtest_data/trades_YYYY-MM.csv)
  C  liquidation   - flags on the history host vs. the store's flags, and vs. a list of seqs a
                     main-host scan found flagged (optional file)
  D  fields by era - property names of one trade per sample date
  E  rate          - requests, elapsed, errors

Paging rule, measured 2026-09-28: the history host WIDENS a seq range to whole milliseconds
(every trade sharing a ms with either bound comes back, unordered inside the ms). So pages
overlap and are de-duplicated on trade_seq; a page that hits `count` (has_more) is re-requested
in halves so no ms group is truncated.

Usage:
  python tools/ops/history_host_validate.py --store aws_fetch/20260928-121255/backtest_data \
      --from 2026-09-27T00:00:00Z --to 2026-09-28T12:00:00Z [--old-day 2025-06-02] \
      [--flagged-seqs file]
"""
import argparse, csv, datetime as dt, glob, json, os, sys, time, urllib.request

BASE = "https://history.deribit.com/api/v2/public/"
INSTR = "BTC-PERPETUAL"
STATS = {"requests": 0, "errors": 0, "retries": 0, "has_more_splits": 0}


def get(method, params, tries=5):
    q = "&".join(f"{k}={v}" for k, v in params.items())
    url = f"{BASE}{method}?{q}"
    for i in range(tries):
        STATS["requests"] += 1
        try:
            with urllib.request.urlopen(url, timeout=30) as r:
                return json.load(r)["result"]
        except Exception as e:  # noqa: BLE001 - count and retry, never hide
            STATS["errors"] += 1
            STATS["retries"] += 1
            print(f"  request error ({type(e).__name__}: {e}); retry {i + 1}", file=sys.stderr)
            time.sleep(2 * (i + 1))
    raise RuntimeError(f"giving up on {url}")


def ms(iso):
    return int(dt.datetime.fromisoformat(iso.replace("Z", "+00:00")).timestamp() * 1000)


def iso(t):
    return dt.datetime.fromtimestamp(t / 1000, dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]


def seq_at(ts_ms, first=True):
    r = get("get_last_trades_by_instrument_and_time",
            {"instrument_name": INSTR, "start_timestamp": ts_ms if first else ts_ms - 600000,
             "end_timestamp": ts_ms + 600000 if first else ts_ms, "count": 1,
             "sorting": "asc" if first else "desc"})
    return r["trades"][0]["trade_seq"]


def page_range(lo, hi, out, step=800):
    """Fetch [lo, hi] into out{seq: trade}; split on has_more."""
    s = lo
    while s <= hi:
        e = min(s + step - 1, hi)
        stack = [(s, e)]
        while stack:
            a, b = stack.pop()
            r = get("get_last_trades_by_instrument",
                    {"instrument_name": INSTR, "start_seq": a, "end_seq": b, "count": 1000, "sorting": "asc"})
            if r.get("has_more") and b > a:
                STATS["has_more_splits"] += 1
                m = (a + b) // 2
                stack += [(a, m), (m + 1, b)]
                continue
            for t in r["trades"]:
                out[t["trade_seq"]] = t
            time.sleep(0.15)
        s = e + 1


def load_store(folder, lo_ms, hi_ms):
    rows = {}
    for f in sorted(glob.glob(os.path.join(folder, "trades_*.csv"))):
        with open(f, newline="") as fh:
            rd = csv.reader(fh)
            next(rd, None)
            for p in rd:
                if len(p) < 7 or not p[6].strip().isdigit():
                    continue
                t = int(p[0])
                if lo_ms <= t < hi_ms:
                    rows.setdefault(int(p[6]), p)  # first wins; duplicates are the same trade
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--store", required=True)
    ap.add_argument("--from", dest="frm", required=True)
    ap.add_argument("--to", required=True)
    ap.add_argument("--old-day", action="append", default=[])
    ap.add_argument("--flagged-seqs")
    a = ap.parse_args()
    t0 = time.time()
    lo_ms, hi_ms = ms(a.frm), ms(a.to)

    # ---- window 1: recent, against the store -------------------------------------------
    store = load_store(a.store, lo_ms, hi_ms)
    lo, hi = min(store), max(store)
    print(f"store window {a.frm} -> {a.to}: rows={len(store)} seq {lo}..{hi} "
          f"(span {hi - lo + 1}, store gaps {hi - lo + 1 - len(store)})")
    hh = {}
    page_range(lo, hi, hh)
    inr = {k: v for k, v in hh.items() if lo <= k <= hi}
    missing_hh = [k for k in range(lo, hi + 1) if k not in inr]
    print(f"A  history host in [{lo},{hi}]: {len(inr)} trades, missing {len(missing_hh)}"
          + (f" (first {missing_hh[:10]})" if missing_hh else ""))
    only_store = [k for k in store if k not in inr]
    only_hh = [k for k in inr if k not in store]
    print(f"B  seqs only in store: {len(only_store)}  only on history host: {len(only_hh)}"
          + (f" (first {sorted(only_hh)[:10]})" if only_hh else ""))
    mism = {"trade_id": 0, "timestamp": 0, "price": 0, "amount": 0, "direction": 0}
    ex = []
    for k, p in store.items():
        t = inr.get(k)
        if not t:
            continue
        checks = {"trade_id": p[5] == str(t["trade_id"]), "timestamp": int(p[0]) == t["timestamp"],
                  "price": abs(float(p[1]) - t["price"]) < 1e-9, "amount": abs(float(p[2]) - t["amount"]) < 1e-9,
                  "direction": p[3] == t["direction"]}
        for f, ok in checks.items():
            if not ok:
                mism[f] += 1
                if len(ex) < 5:
                    ex.append((k, f, p, {x: t.get(x) for x in ("trade_id", "timestamp", "price", "amount", "direction")}))
    print(f"B  field mismatches on shared seqs: {mism}")
    for e in ex:
        print(f"   e.g. seq {e[0]} {e[1]}: store={e[2][:6]} history={e[3]}")
    hh_flag = {k for k, t in inr.items() if t.get("liquidation", "none") not in ("none", "")}
    st_flag = {k for k, p in store.items() if p[4] not in ("none", "")}
    print(f"C  liquidation-flagged: history host {len(hh_flag)} |store {len(st_flag)} |"
          f"store flagged but history not {len(st_flag - hh_flag)} |history flagged, store 'none' {len(hh_flag - st_flag)}")
    if a.flagged_seqs:
        with open(a.flagged_seqs) as fh:
            want = {int(x) for x in fh.read().split() if x.strip().isdigit()}
        inwin = {k for k in want if lo <= k <= hi}
        print(f"C  main-host scan flagged seqs in window: {len(inwin)} |flagged on history host: {len(inwin & hh_flag)} "
              f"· not flagged there: {sorted(inwin - hh_flag)[:10]}")

    # ---- older full days: completeness only -----------------------------------------------
    for day in a.old_day:
        d0 = ms(day + "T00:00:00Z")
        slo, shi = seq_at(d0, True), seq_at(d0 + 86400000 - 1, False)
        od = {}
        page_range(slo, shi, od)
        got = sum(1 for k in od if slo <= k <= shi)
        print(f"A  {day}: seq {slo}..{shi} span {shi - slo + 1}, got {got}, missing {shi - slo + 1 - got}")

    # ---- fields by era ---------------------------------------------------------------------
    for day in ("2020-01-02", "2022-01-03", "2024-01-02", "2026-06-01"):
        r = get("get_last_trades_by_instrument_and_time",
                {"instrument_name": INSTR, "start_timestamp": ms(day + "T12:00:00Z"),
                 "end_timestamp": ms(day + "T12:10:00Z"), "count": 1, "sorting": "asc"})
        print(f"D  {day}: {sorted(r['trades'][0].keys()) if r['trades'] else 'no trades'}")

    el = time.time() - t0
    print(f"E  requests={STATS['requests']} errors={STATS['errors']} retries={STATS['retries']} "
          f"has_more_splits={STATS['has_more_splits']} elapsed={el:.0f}s rate={STATS['requests'] / el:.1f}/s")


if __name__ == "__main__":
    main()
