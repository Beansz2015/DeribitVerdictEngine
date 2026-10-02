"""Re-derive indicators.Liquidations.large_liq_size (and inspect dominance_ratio) from the dev
history store. READ-ONLY. Research only: no settings.json edit (liquidation-park-spec.md, "NOT in
scope"; ruling HSR-4). Record: docs/large-liq-size-rederivation-2026-10-02.md

Mirrors IndicatorEngine.CalcLiquidations (Core/Indicators_OrderFlow.vb:259-282):
  window   = the last 500 trades (analysis_log rtc=500), taken at each UTC minute close
             (a superset of the engine's 1-min / 3-min ON_CLOSE runs);
  sizes    = sums of Amount (USD) of liquidation-flagged trades, per side;
  signal   = LONG LIQS if L > 0 and L >= S * ratio; SHORT LIQS if S > 0 and S > L * ratio;
  size used by the penalty (ScoringEngine_Calculate_Scoring.vb:400-405) = the dominant side's sum.
Two side bookings:
  CURRENT = the code today: any flag, direction buy -> short liq, sell -> long liq;
  D5      = the ruled maker-side fix (liquidation-park-spec.md, D-5 rider): T -> the taker's side
            (as CURRENT), M -> the maker's side (buy -> long liq, sell -> short liq), MT -> both.
Usage: python derive.py [STORE] (default C:/DeribitData/history)
"""
import collections, datetime as dt, glob, os, sys

STORE = sys.argv[1] if len(sys.argv) > 1 else 'C:/DeribitData/history'
WIN, RATIO, SHIPPED = 500, 2.0, 200.0
SESS = [('ASIA', 0, 7), ('LONDON', 8, 12), ('NY', 13, 23)]   # settings.json session_volume, end inclusive (assumed)


def sess(h):
    for n, a, b in SESS:
        if a <= h <= b: return n
    return 'GAP'


def pct(xs, p):
    if not xs: return float('nan')
    xs = sorted(xs); k = (len(xs) - 1) * p / 100.0; f = int(k); c = min(f + 1, len(xs) - 1)
    return xs[f] + (xs[c] - xs[f]) * (k - f)


flags = collections.Counter()
liq_trade_usd = {'T': [], 'M': [], 'MT': []}
q = collections.deque()
sums = {'CUR': [0.0, 0.0], 'D5': [0.0, 0.0]}    # [long, short]
samples = {'CUR': [], 'D5': []}                  # (year_half, session, L, S) for windows with any liq
minutes = 0
cur_min = None


def contrib(direction, flag, amt):
    if flag == 'none': return (0.0, 0.0), (0.0, 0.0)
    cur = (amt, 0.0) if direction == 'sell' else (0.0, amt)
    if flag == 'T': d5 = cur
    elif flag == 'M': d5 = (amt, 0.0) if direction == 'buy' else (0.0, amt)
    else: d5 = (amt, amt)   # MT: both sides
    return cur, d5


def sample(m):
    global minutes
    minutes += 1
    t = dt.datetime.fromtimestamp(m * 60, dt.timezone.utc)
    key = (f'{t.year}H{1 if t.month <= 6 else 2}', sess(t.hour))
    for b in ('CUR', 'D5'):
        L, S = sums[b]
        if L > 0.5 or S > 0.5: samples[b].append((key[0], key[1], L, S))


for path in sorted(glob.glob(os.path.join(STORE, 'trades_*.csv'))):
    with open(path, encoding='utf-8') as f:
        f.readline()
        for line in f:
            p = line.split(',', 6)
            ts = int(p[0]); m = ts // 60000
            if cur_min is None: cur_min = m
            while cur_min < m:          # every minute close passed: the window as of that close
                sample(cur_min); cur_min += 1
            fl = p[4]; amt = float(p[2])
            flags[fl] += 1
            if fl != 'none': liq_trade_usd[fl].append(amt)
            c, d = contrib(p[3], fl, amt)
            q.append((c, d))
            sums['CUR'][0] += c[0]; sums['CUR'][1] += c[1]; sums['D5'][0] += d[0]; sums['D5'][1] += d[1]
            if len(q) > WIN:
                oc, od = q.popleft()
                sums['CUR'][0] -= oc[0]; sums['CUR'][1] -= oc[1]; sums['D5'][0] -= od[0]; sums['D5'][1] -= od[1]

print(f'STORE {STORE} | minute samples {minutes} | flags {dict(flags)}')
for k, v in liq_trade_usd.items():
    if v: print(f'  per-trade USD, flag {k}: n={len(v)} p50={pct(v,50):,.0f} p90={pct(v,90):,.0f} p99={pct(v,99):,.0f} max={max(v):,.0f}')

for b in ('CUR', 'D5'):
    s = samples[b]
    fired = []
    for yh, se, L, S in s:
        if L > 0 and L >= S * RATIO: fired.append((yh, se, L))
        elif S > 0 and S > L * RATIO: fired.append((yh, se, S))
    allside = [x for _, _, L, S in s for x in (L, S) if x > 0.5]
    print(f'\n== booking {b} | minutes with any liq in window: {len(s)} ({100*len(s)/minutes:.2f} %) | signal fired: {len(fired)} ({100*len(fired)/minutes:.2f} %)')
    sz = [x[2] for x in fired]
    print('  dominant size when fired (USD): ' + ' '.join(f'p{p}={pct(sz,p):,.0f}' for p in (10, 25, 50, 75, 90, 95, 99)) + f' max={max(sz):,.0f}')
    print('  any-side size, minutes with liq (USD): ' + ' '.join(f'p{p}={pct(allside,p):,.0f}' for p in (50, 90, 95)))
    print(f'  share of fired minutes ABOVE the shipped {SHIPPED:.0f} (=> large penalty today): {100*sum(1 for x in sz if x > SHIPPED)/len(sz):.2f} %')
    print('  p90 of dominant size by segment:')
    seg = collections.defaultdict(list)
    for yh, se, v in fired: seg[(yh, se)].append(v); seg[(yh, '*')].append(v); seg[('*', se)].append(v)
    for k in sorted(seg):
        v = seg[k]
        print(f'    {k[0]:7} {k[1]:7} n={len(v):>7} p50={pct(v,50):>11,.0f} p90={pct(v,90):>12,.0f}')
    both = [max(L, S) / min(L, S) for _, _, L, S in s if L > 0.5 and S > 0.5]
    one = sum(1 for _, _, L, S in s if (L > 0.5) != (S > 0.5))
    print(f'  dominance: one-sided minutes {one} ({100*one/len(s):.1f} % of liq minutes); two-sided {len(both)}; ratio max/min ' +
          ' '.join(f'p{p}={pct(both,p):.2f}' for p in (25, 50, 75, 90)) +
          f'; two-sided minutes with ratio < {RATIO}: {sum(1 for r in both if r < RATIO)}')
