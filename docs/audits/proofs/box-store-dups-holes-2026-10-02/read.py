"""Box-store duplicate rows and holes, attributed. READ-ONLY on both stores.

Record: docs/box-store-duplicates-and-holes-read-2026-10-02.md
Usage:  python read.py [FETCH_DIR] [DEV_STORE]
        defaults: aws_fetch/20260928-121255 and C:/DeribitData/history
Window: 2026-07-23 .. 2026-09-28 (exclusive) -- the window of comparison #1.

Duplicates: a row whose trade_seq appeared earlier in the box files. Blocks are runs of
duplicate rows no more than 2 file lines apart (the 2026-09-15 read's method). A block's
append time is the file's high-water timestamp at its first row (the store is append-only).
Holes: dev-store trades whose seq is not in the box and that no seq-less box row matches on
(timestamp, price, amount, direction) -- the same rule as HistoryCompare.vb. Holes are cut
into runs of consecutive dev trades.
"""
import datetime as dt, sys

ROOT = 'C:/Dev/DeribitVerdictEngine'
FETCH = sys.argv[1] if len(sys.argv) > 1 else ROOT + '/aws_fetch/20260928-121255'
DEV = sys.argv[2] if len(sys.argv) > 2 else 'C:/DeribitData/history'
BOX = FETCH + '/backtest_data'
MONTHS = ['2026-07', '2026-08', '2026-09']


def ms(s): return int(dt.datetime.fromisoformat(s).replace(tzinfo=dt.timezone.utc).timestamp() * 1000)
def iso(m): return dt.datetime.fromtimestamp(m / 1000, dt.timezone.utc).strftime('%m-%dT%H:%M:%S')


LO, HI = ms('2026-07-23T00:00:00'), ms('2026-09-28T00:00:00')
WRITE_GUARD_FIX = ms('2026-08-11T17:18:42')   # a5d701ad, trade-store-same-millisecond-drop-2026-08-11.md

starts = []
for line in open(FETCH + '/capture_marker.log', encoding='utf-8'):
    p = [x.strip() for x in line.split('|')]
    starts.append((ms(p[0].rstrip('Z')[:23]), p[3][:8]))
starts.sort()
def inst_at(t):
    cur = '?'
    for s, i in starts:
        if s <= t: cur = i
    return cur
passes = []
for line in open(FETCH + '/repair_status.log', encoding='utf-8'):
    p = [x.strip() for x in line.split('|')]
    if p[1].startswith('PASS_'): passes.append((ms(p[0].rstrip('Z')[:23]), p[1], p[3]))

# ---- Box store ---------------------------------------------------------------------------
seen, seqless, box_ms, dups = {}, set(), set(), []
rows_win = seqless_win = 0
first_row = None
for m in MONTHS:
    hw = 0
    with open(f'{BOX}/trades_{m}.csv', encoding='utf-8') as f:
        f.readline()
        for ln, line in enumerate(f, 2):
            p = line.rstrip('\r\n').split(',')
            if len(p) < 5: continue
            ts = int(p[0]); box_ms.add(ts)
            if first_row is None: first_row = ts
            seq = int(p[6]) if len(p) >= 7 and p[6].strip() else None
            if LO <= ts < HI:
                rows_win += 1
                if seq is None:
                    seqless_win += 1
                    seqless.add((ts, '%.2f' % float(p[1]), '%.2f' % float(p[2]), p[3]))
                else:
                    c = seen.get(seq, 0)
                    if c: dups.append((m, ln, ts, hw))
                    seen[seq] = c + 1
            if ts > hw: hw = ts
print(f'BOX rows_in_window={rows_win} distinct_seqs={len(seen)} seqless={seqless_win} dup_rows={len(dups)} first_row={iso(first_row)}')
cc = {}
for c in seen.values(): cc[c] = cc.get(c, 0) + 1
print('copies per seq:', dict(sorted(cc.items())))

blocks = []
for m, ln, ts, hw in dups:
    if blocks and blocks[-1]['m'] == m and ln - blocks[-1]['le'] <= 2:
        b = blocks[-1]; b['le'] = ln; b['n'] += 1; b['tmin'] = min(b['tmin'], ts); b['tmax'] = max(b['tmax'], ts)
    else:
        blocks.append(dict(m=m, ls=ln, le=ln, n=1, tmin=ts, tmax=ts, hw=hw))
print(f'\nDUP BLOCKS: {len(blocks)}')
print('lines               dup_rows  trades from -> to              appended(hw)    lag_min  instance  next PASS (repair_status.log)')
small = {}
for b in blocks:
    inst = inst_at(b['hw'])
    if b['n'] < 100:
        small.setdefault(inst, [0, 0]); small[inst][0] += 1; small[inst][1] += b['n']; continue
    nxt = next((p for p in passes if 0 <= p[0] - b['hw'] < 3600_000), None)
    nx = f"{iso(nxt[0])} {nxt[1]} {nxt[2]}" if nxt else '(no PASS line within 1 h; log starts 09-21)'
    print(f"{b['ls']:>8}-{b['le']:<8} {b['n']:>8}  {iso(b['tmin'])} -> {iso(b['tmax'])}  {iso(b['hw'])}  {(b['hw']-b['tmin'])/60000:7.1f}  {inst}  {nx}")
print('blocks < 100 rows, by instance [blocks, rows]:', small)
post = [b for b in blocks if b['hw'] >= ms('2026-09-25T13:29:42')]
print('duplicate rows appended under a19acc4d (from 09-25 13:29:42):', sum(b['n'] for b in post))

# ---- Holes -------------------------------------------------------------------------------
runs, cur = [], None
for m in MONTHS:
    with open(f'{DEV}/trades_{m}.csv', encoding='utf-8') as f:
        f.readline()
        for line in f:
            p = line.rstrip('\r\n').split(',')
            ts = int(p[0])
            if not (LO <= ts < HI): continue
            seq = int(p[6])
            if seq not in seen and (ts, '%.2f' % float(p[1]), '%.2f' % float(p[2]), p[3]) not in seqless:
                if cur and seq == cur['se'] + 1: cur['se'] = seq; cur['te'] = ts; cur['n'] += 1; cur['ms'].add(ts)
                else:
                    if cur: runs.append(cur)
                    cur = dict(ss=seq, se=seq, ts=ts, te=ts, n=1, ms={ts})
            elif cur: runs.append(cur); cur = None
if cur: runs.append(cur)
print(f'\nHOLES: trades={sum(r["n"] for r in runs)} runs={len(runs)}')
print('runs >= 10,000 trades:')
for r in runs:
    if r['n'] >= 10000: print(f"  seq {r['ss']}..{r['se']} n={r['n']} {iso(r['ts'])} -> {iso(r['te'])}")
def sig(rs):
    return (f"runs={len(rs)} trades={sum(r['n'] for r in rs)} "
            f"in_single_ms_runs={sum(r['n'] for r in rs if len(r['ms']) == 1)} "
            f"every_ms_has_a_kept_box_row={sum(r['n'] for r in rs if all(t in box_ms for t in r['ms']))}")
sm = [r for r in runs if r['n'] < 10000]
print('runs < 10,000, before the write-guard fix (08-11 17:18:42):', sig([r for r in sm if r['ts'] < WRITE_GUARD_FIX]))
print('runs < 10,000, after the write-guard fix:', sig([r for r in sm if r['ts'] >= WRITE_GUARD_FIX]))
for r in sm:
    if r['ts'] >= WRITE_GUARD_FIX: print(f"  seq {r['ss']}..{r['se']} n={r['n']} {iso(r['ts'])}")
