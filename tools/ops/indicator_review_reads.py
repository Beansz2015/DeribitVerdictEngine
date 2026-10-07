"""Reads behind docs/indicator-calibration-review-adversarial-2026-10-07.md (analysis only).

Re-checks, on a collector fetch, the figures that docs/indicator-calibration-review-2026-10-02.md
carried without checking. Standard library only. Reads; writes nothing.

    python -I tools/ops/indicator_review_reads.py --fetch aws_fetch/<stamp> [--book <csv> ...] [--read <name> ...]

--fetch   a collector fetch folder (analysis_log.csv, its rotated .bak books, analysis_eval_cache.csv,
          backtest_data/trades_YYYY-MM.csv).
--book    analysis_log books to read. Default: the fetch's analysis_log.csv and every
          analysis_log.csv.*col-*.bak beside it, each reported separately (never pooled across a header).
--read    any of: volume clamp targets repaint burstage vwap divergence episodes ttm mtf eval.
          Default: all.

Every read is weekday-only (UTC Monday-Friday), the house scope. Sessions come from
settings.json session_volume (start_hour/end_hour, first match wins), and every threshold is read
from the tracked settings.json, never restated here.

The reads and their limits:
  volume      VolumeRatio percentiles; share of rows at or above the CLAMP-MINIMUM mid and high
              thresholds times the session multipliers. The live thresholds are dynamic and at least
              this high, so these shares are UPPER BOUNDS on the volume vote's fire rate. fullUB also
              needs the ROC/VWAP side agreement the vote requires.
  clamp       Directional rows with a trade-side swing stop: share whose placed stop sits at
              stop_max_atr_mult x ATR while the swing stop is farther (the clamp binds).
  targets     Directional rows: placed-target tier from TargetCapReason.
  repaint     Consecutive rows of one instance inside one 5-minute bucket: share where
              LastSwingHigh5m or LastSwingLow5m changed. Only the forming 5m bar can move a pivot inside
              its own bar, so this is an UPPER BOUND on the intra-bar repaint rate (a row near the
              bucket edge can see a newly closed bar).
  burstage    Seconds from the last stored trade to the row's timestamp. The timestamp is written at
              log time, 1-5 s after the bar close and after the snapshot, so these ages are UPPER BOUNDS
              on the silence the aggressor-velocity snapshot saw.
  vwap        Rows in the first four minutes after each VWAP anchor (from settings) whose
              VWAPSessionCandles reached the warmup count: the session fallback firing.
  divergence  CVDDivergence and RSIDivergence fire shares by ExecResolution.
  episodes    NY directional rows per episode under three rules: strict consecutive same-side rows;
              same side with gaps up to 5 min; same side with gaps up to 15 min (the rule in
              tools/ops/medium_tier_diagnosis.py, D-9).
  ttm         TTMDirection shares by ExecResolution (FLAT = the flat_threshold band).
  mtf         Rows whose MTF15mADX is 0 or empty (the gate's no-data path) and rows passing both sides.
  eval        analysis_eval_cache.csv directional outcomes from 2026-09-01, by session, split by
              provenance (a whole-second .0000000Z timestamp marks a backfilled row).
"""
import argparse, bisect, csv, glob, json, math, os, re, sys
from collections import Counter
from datetime import datetime, timezone

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))


def load_settings():
    with open(os.path.join(REPO, 'settings.json'), encoding='utf-8') as f:
        return json.load(f)


S = load_settings()
SESS = [(x['name'], x['start_hour'], x['end_hour'], x.get('high_multiplier', 1.0), x.get('mid_multiplier', 1.0))
        for x in S['session_volume']['sessions']]
VOL = S['indicators']['Volume']
AV = S['indicators']['aggressor_velocity']
STOP_MAX = S['scoring']['structural_levels']['stop_max_atr_mult']
VWAP_CFG = S['indicators']['VWAP']


def session(h):
    for name, a, b, _, _ in SESS:
        if a <= h <= b:
            return name
    return 'NONE'


def mults(name):
    for n, _, _, hm, mm in SESS:
        if n == name:
            return hm, mm
    return 1.0, 1.0


def burst_threshold(name):
    return AV['sessions'].get(name, {}).get('burst_ratio_threshold', AV['default']['burst_ratio_threshold'])


def norm_window(name):
    return AV['sessions'].get(name, {}).get('norm_window_sec', AV['default']['norm_window_sec'])


def rows(path):
    with open(path, newline='', encoding='utf-8', errors='replace') as f:
        for x in csv.DictReader(f):
            try:
                ts = datetime.strptime(x['Timestamp'][:19], '%Y-%m-%d %H:%M:%S')
            except (ValueError, KeyError):
                continue
            if ts.weekday() >= 5:
                continue
            x['_ts'] = ts
            x['_s'] = session(ts.hour)
            yield x


def fnum(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def side(v):
    if not v or v.startswith('NO TRADE'):
        return None
    return 'L' if 'LONG' in v else ('S' if 'SHORT' in v else None)


def pct(a, q):
    a = sorted(a)
    return a[min(len(a) - 1, int(q * len(a)))] if a else float('nan')


def read_volume(book):
    by = {}
    for x in rows(book):
        vr, roc, p, vw = fnum(x['VolumeRatio']), fnum(x['ROC']), fnum(x['Price']), fnum(x['VWAP'])
        if None in (vr, roc, p, vw):
            continue
        by.setdefault((x['_s'], x.get('TriggerMode') or '-'), []).append((vr, roc, p, vw))
    print('  session  trigger      n     p50     p90     p99   >=mid  >=high  fullUB   >=3.0')
    for (s, tm), v in sorted(by.items()):
        hm, mm = mults(s)
        mid, hi = VOL['dynamic_mid_clamp_min'] * mm, VOL['dynamic_high_clamp_min'] * hm
        n = len(v)
        vrs = [a[0] for a in v]
        f_mid = sum(a[0] >= mid for a in v) / n * 100
        f_hi = sum(a[0] >= hi for a in v) / n * 100
        f_full = sum(a[0] >= hi and ((a[1] > 0 and a[2] > a[3]) or (a[1] < 0 and a[2] < a[3])) for a in v) / n * 100
        f_3 = sum(a[0] >= 3.0 for a in v) / n * 100
        print(f'  {s:8} {tm:9} {n:6d} {pct(vrs,.5):7.4f} {pct(vrs,.9):7.4f} {pct(vrs,.99):7.3f} {f_mid:6.2f}% {f_hi:6.2f}% {f_full:6.2f}% {f_3:6.2f}%')


def read_clamp(book):
    c = Counter()
    for x in rows(book):
        sd = side(x['Verdict'])
        if not sd:
            continue
        p, atr = fnum(x['Price']), fnum(x['ATR'])
        sw = fnum(x['SwingStopLong' if sd == 'L' else 'SwingStopShort']) or 0
        ps = fnum(x['PlacedStopLong' if sd == 'L' else 'PlacedStopShort']) or 0
        if not p or not atr or ps <= 0 or sw <= 0:
            continue
        swd, psd, cap = abs(p - sw), abs(p - ps), STOP_MAX * atr
        if swd > cap + 0.5 and abs(psd - cap) <= max(0.6, 0.01 * cap):
            c[(x['_s'], 'clamped')] += 1
        elif abs(psd - swd) <= 0.6:
            c[(x['_s'], 'swing')] += 1
        else:
            c[(x['_s'], 'other')] += 1
    for s in [n for n, *_ in SESS]:
        tot = c[(s, 'clamped')] + c[(s, 'swing')] + c[(s, 'other')]
        if tot:
            print(f"  {s}: structural-stop rows {tot}; clamped {c[(s,'clamped')]/tot*100:.1f}%, swing {c[(s,'swing')]/tot*100:.1f}%, other {c[(s,'other')]/tot*100:.1f}%")


def read_targets(book):
    c, tot = Counter(), Counter()
    for x in rows(book):
        if not side(x['Verdict']):
            continue
        m = re.search(r'\(([A-Za-z_0-9]+)\)', x['TargetCapReason'])
        c[(x['_s'], m.group(1) if m else (x['TargetCapReason'] or 'EMPTY')[:20])] += 1
        tot[x['_s']] += 1
    for s in [n for n, *_ in SESS]:
        if tot[s]:
            parts = ', '.join(f'{k[1]} {v/tot[s]*100:.1f}%' for k, v in sorted(c.items(), key=lambda kv: -kv[1]) if k[0] == s)
            print(f'  {s}: directional rows {tot[s]}; {parts}')


def read_repaint(book):
    c = Counter()
    prev = None
    for x in rows(book):
        ts = x['_ts']
        key = (ts.date(), ts.hour, ts.minute // 5, x.get('InstanceId', ''))
        cur = (key, x['LastSwingHigh5m'], x['LastSwingLow5m'])
        if prev and prev[0] == key:
            c[(x['_s'], 'pairs')] += 1
            if prev[1] != cur[1] or prev[2] != cur[2]:
                c[(x['_s'], 'chg')] += 1
        prev = cur
    for s in [n for n, *_ in SESS]:
        n = c[(s, 'pairs')]
        if n:
            print(f"  {s}: same-5m-bucket row pairs {n}; LastSwing*5m changed {c[(s,'chg')]/n*100:.2f}%")


def load_trade_times(fetch, months):
    ts = []
    for m in months:
        p = os.path.join(fetch, 'backtest_data', f'trades_{m}.csv')
        if not os.path.exists(p):
            continue
        with open(p, encoding='utf-8') as f:
            next(f)
            for line in f:
                ts.append(int(line[:13]))
    ts.sort()
    return ts


def read_burstage(book, fetch):
    rs = list(rows(book))
    if not rs:
        return
    months = sorted({x['_ts'].strftime('%Y-%m') for x in rs})
    tt = load_trade_times(fetch, months)
    print(f'  trades loaded {len(tt)} ({", ".join(months)})')
    c, ages = Counter(), Counter()
    for x in rs:
        ms = int(x['_ts'].replace(tzinfo=timezone.utc).timestamp() * 1000)
        i = bisect.bisect_right(tt, ms) - 1
        if i < 0:
            continue
        age = (ms - tt[i]) / 1000.0
        s = x['_s']
        c[(s, 'rows')] += 1
        for a in (2, 5, 10):
            if age >= a:
                ages[(s, a)] += 1
        if x['AggrVelSignal'].startswith('BURST'):
            c[(s, 'burst')] += 1
            br = fnum(x['AggrVelBurstRatio'])
            if age >= 2:
                c[(s, 'burst_age2')] += 1
                if br is not None:
                    dec = br * math.exp(-age / AV['fast_window_sec']) / math.exp(-age / norm_window(s))
                    if dec < burst_threshold(s):
                        c[(s, 'burst_flip')] += 1
    for s in [n for n, *_ in SESS]:
        n, b = c[(s, 'rows')], c[(s, 'burst')]
        if n:
            print(f"  {s}: rows {n}; last trade >=2 s before the row stamp {ages[(s,2)]/n*100:.1f}%, >=5 s {ages[(s,5)]/n*100:.1f}%, "
                  f">=10 s {ages[(s,10)]/n*100:.1f}%; BURST rows {b}, of which >=2 s {c[(s,'burst_age2')]}, "
                  f"and of those decayed below the threshold {c[(s,'burst_flip')]}")


def read_vwap(book):
    anchors = [(0, 0), (VWAP_CFG['session2_start_hour'], VWAP_CFG['session2_start_minute'])]
    warm = VWAP_CFG['warmup_candles']
    c = Counter()
    for x in rows(book):
        ts = x['_ts']
        for h, m in anchors:
            dm = (ts.hour * 60 + ts.minute) - (h * 60 + m)
            if 0 <= dm < 4:
                c['anchor_rows'] += 1
                n = fnum(x['VWAPSessionCandles']) or 0
                if n >= warm:
                    c['fallback'] += 1
    print(f"  anchors {anchors}, warmup {warm}: rows in the first 4 min {c['anchor_rows']}, of which session fallback {c['fallback']}")


def read_divergence(book):
    c = Counter()
    for x in rows(book):
        r = x['ExecResolution']
        c[(r, 'n')] += 1
        if x['CVDDivergence'] not in ('', 'NONE'):
            c[(r, 'cvd')] += 1
        if x['RSIDivergence'] not in ('', 'NONE'):
            c[(r, 'rsi')] += 1
    for r in sorted({k[0] for k in c}):
        n = c[(r, 'n')]
        print(f"  res {r}: rows {n}; CVD divergence {c[(r,'cvd')]/n*100:.2f}%, RSI divergence {c[(r,'rsi')]/n*100:.2f}%")


def read_episodes(book):
    rules = [('strict consecutive', None), ('gap <= 5 min', 300), ('gap <= 15 min (D-9 rule)', 900)]
    for name, gap in rules:
        eps, cur, n, last = [], None, 0, None
        for x in rows(book):
            if x['_s'] != 'NY':
                if gap is None and cur:
                    eps.append(n); cur = None
                continue
            sd = side(x['Verdict'])
            if gap is None:
                if sd and sd == cur:
                    n += 1
                else:
                    if cur:
                        eps.append(n)
                    cur, n = sd, (1 if sd else 0)
                continue
            if not sd:
                continue
            if sd == cur and last and (x['_ts'] - last).total_seconds() <= gap:
                n += 1
            else:
                if cur:
                    eps.append(n)
                cur, n = sd, 1
            last = x['_ts']
        if cur and n:
            eps.append(n)
        eps = [e for e in eps if e > 0]
        if eps:
            print(f'  NY, {name}: directional rows {sum(eps)}, episodes {len(eps)}, rows per episode {sum(eps)/len(eps):.2f}')


def read_ttm(book):
    c = Counter()
    for x in rows(book):
        c[(x['ExecResolution'], x['TTMDirection'])] += 1
    for r in sorted({k[0] for k in c}):
        n = sum(v for k, v in c.items() if k[0] == r)
        parts = ', '.join(f'{k[1]} {v/n*100:.1f}%' for k, v in sorted(c.items()) if k[0] == r)
        print(f'  res {r}: rows {n}; {parts}')


def read_mtf(book):
    c = Counter()
    for x in rows(book):
        c['rows'] += 1
        a = fnum(x['MTF15mADX'])
        if a is None or a == 0:
            c['no_data'] += 1
        if x['MTFGatePassLong'] == 'True' and x['MTFGatePassShort'] == 'True':
            c['both_pass'] += 1
    print(f"  rows {c['rows']}; MTF15mADX 0 or empty {c['no_data']}; both sides pass {c['both_pass']} ({c['both_pass']/max(1,c['rows'])*100:.1f}%)")


def read_eval(fetch):
    p = os.path.join(fetch, 'analysis_eval_cache.csv')
    c = Counter()
    with open(p, encoding='utf-8') as f:
        next(f)
        for x in csv.DictReader(f):
            if not side(x['Verdict']) or x['Timestamp'] < '2026-09-01':
                continue
            try:
                d = datetime.strptime(x['Timestamp'][:19], '%Y-%m-%dT%H:%M:%S')
            except ValueError:
                continue
            if d.weekday() >= 5:
                continue
            prov = 'backfilled' if '.0000000Z' in x['Timestamp'] else 'live'
            c[(session(d.hour), prov, x['EvalOutcome'])] += 1
    for s in [n for n, *_ in SESS]:
        for prov in ('live', 'backfilled'):
            sub = {k[2]: v for k, v in c.items() if k[0] == s and k[1] == prov}
            n = sum(sub.values())
            if n:
                print(f'  {s} {prov}: n {n}; ' + ', '.join(f'{k} {v/n*100:.1f}%' for k, v in sorted(sub.items(), key=lambda kv: -kv[1])))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--fetch', required=True)
    ap.add_argument('--book', action='append')
    ap.add_argument('--read', action='append')
    a = ap.parse_args()
    books = a.book or ([os.path.join(a.fetch, 'analysis_log.csv')] + sorted(glob.glob(os.path.join(a.fetch, 'analysis_log.csv.*col-*.bak'))))
    reads = a.read or ['volume', 'clamp', 'targets', 'repaint', 'burstage', 'vwap', 'divergence', 'episodes', 'ttm', 'mtf', 'eval']
    print(f"settings.json version {S['version']}; sessions {[(n, x, y) for n, x, y, _, _ in SESS]}")
    for r in reads:
        print(f'== {r}')
        if r == 'eval':
            read_eval(a.fetch)
            continue
        for b in books:
            print(f' {os.path.basename(b)}')
            if r == 'burstage':
                read_burstage(b, a.fetch)
            else:
                globals()['read_' + r](b)


if __name__ == '__main__':
    main()
