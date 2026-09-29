"""
Isolated proofs for the backtest/what-if audit. NO .NET runtime exists in this container,
so every engine function used here is a line-for-line PYTHON PORT of the shipped VB
(Core/Indicators_Momentum.vb CalcATR/CalcDMI, CoverageReport.ClassifySpan seq path,
WhatIfReplay.DeriveVerdict, ScoringEngine Step 4 TRANSITIONAL). Ports, not the shipped
binaries -- treat as evidence of mechanism, not as a harness run.
"""
import math, random

# ---------------------------------------------------------------- ports
def calc_atr(c, period):
    if len(c) < period + 1: return 0.0
    tr = [max(c[i]['h']-c[i]['l'], abs(c[i]['h']-c[i-1]['c']), abs(c[i]['l']-c[i-1]['c'])) for i in range(1, len(c))]
    atr = sum(tr[:period]) / period
    for i in range(period, len(tr)): atr = (atr*(period-1) + tr[i]) / period
    return atr

def calc_dmi(c, period):
    if len(c) < period + 2: return 0, 0, 0
    tr, dp, dm = [], [], []
    for i in range(1, len(c)):
        cu, p = c[i], c[i-1]
        tr.append(max(cu['h']-cu['l'], abs(cu['h']-p['c']), abs(cu['l']-p['c'])))
        up, dn = cu['h']-p['h'], p['l']-cu['l']
        dp.append(up if (up > dn and up > 0) else 0); dm.append(dn if (dn > up and dn > 0) else 0)
    sT, sP, sM = sum(tr[:period]), sum(dp[:period]), sum(dm[:period])
    adxl, pdi, mdi = [], 0, 0
    for i in range(period, len(tr)):
        sT = sT - sT/period + tr[i]; sP = sP - sP/period + dp[i]; sM = sM - sM/period + dm[i]
        pdi = 100*sP/sT if sT else 0; mdi = 100*sM/sT if sT else 0
        adxl.append(100*abs(pdi-mdi)/(pdi+mdi) if (pdi+mdi) else 0)
    if len(adxl) < period: return pdi, mdi, 0
    a = sum(adxl[:period])/period
    for i in range(period, len(adxl)): a = (a*(period-1)+adxl[i])/period
    return pdi, mdi, a

def regime(pdi, mdi, adx, trend=25.0, rng=20.0):
    if adx > trend and pdi > mdi: return "TRENDING_UP"
    if adx > trend and mdi > pdi: return "TRENDING_DOWN"
    if adx < rng: return "RANGE_BOUND"
    return "TRANSITIONAL"

# ---------------------------------------------------------------- synthetic tape (1 s resolution)
random.seed(7)
T0 = 0                      # seconds; t=0 is 00:00 UTC; flush hour = 14:00-15:00
N = 16 * 3600               # 00:00 -> 16:00
px = [100000.0]
sig = 100000 * 0.0003 / math.sqrt(60)      # ~3 bps per 1m of noise
for t in range(1, N):
    drift = 0.0
    if 14*3600 <= t < 15*3600:
        m = (t - 14*3600) / 60.0
        # -1.5 % over the hour, front-loaded: -1.0 % in 14:05-14:15, -0.5 % spread over the rest
        drift = (-0.010*100000/600) if 5 <= m < 15 else (-0.005*100000/3000)
    px.append(px[-1] + drift + random.gauss(0, sig))

def bar(t0, t1):   # OHLC over seconds [t0, t1)
    s = px[t0:t1]
    return {'o': s[0], 'h': max(s), 'l': min(s), 'c': s[-1]}

def closed_bars(res_min, close_s, n):
    res = res_min*60; last_open = close_s - res
    first = max(0, last_open - (n-1)*res)
    return [bar(o, o+res) for o in range(first - first % res, last_open+1, res)][-n:]

STUB = 2
def replay_series(res_min, close_s, n):     # ReplayLoop: (n-1) closed + 2 s stub
    return closed_bars(res_min, close_s, n-1) + [bar(close_s, close_s+STUB)]

def live_series(res_min, close_s, n):       # live chart endpoint at close+2 s: forming bar = [open, now)
    res = res_min*60; open_s = close_s - close_s % res
    if open_s == close_s:
        return closed_bars(res_min, close_s, n-1) + [bar(close_s, close_s+STUB)]
    return closed_bars(res_min, open_s, n-1) + [bar(open_s, close_s+STUB)]

print("=== P1  1m ATR(7): replay stub vs live forming bar (execRes=1, flush hour) ===")
worst = 0
for m in range(14*60+1, 15*60):
    cs = m*60
    a_r = calc_atr(replay_series(1, cs, 250), 7); a_l = calc_atr(live_series(1, cs, 250), 7)
    worst = max(worst, abs(a_r-a_l))
print(f"max |ATR_replay - ATR_live| over 59 closes = {worst:.6f}  (identical series => convention matches)")
cs = 14*3600 + 12*60
a_stub = calc_atr(replay_series(1, cs, 250), 7); a_closed = calc_atr(closed_bars(1, cs, 250), 7)
print(f"14:12 ATR stub={a_stub:.2f} closed-bars={a_closed:.2f} ratio={a_stub/a_closed:.3f} (6/7={6/7:.3f})")

print("\n=== P2  5m DMI(9) regime: replay stub vs live partial forming bar (execRes=1 closes 14:00-14:59) ===")
mis, rows = 0, []
for m in range(14*60, 15*60):
    cs = m*60
    rp = calc_dmi(replay_series(5, cs, 210), 9); lp = calc_dmi(live_series(5, cs, 210), 9)
    rr, lr = regime(*rp), regime(*lp)
    if rr != lr: mis += 1
    if m % 60 in (3, 7, 9, 12, 14, 18): rows.append((m, rp, lp, rr, lr))
for m, rp, lp, rr, lr in rows:
    print(f"{m//60:02d}:{m%60:02d}  replay ADX={rp[2]:5.1f} -DI={rp[1]:5.1f} {rr:14s} | live ADX={lp[2]:5.1f} -DI={lp[1]:5.1f} {lr}")
print(f"regime disagreements: {mis}/60 closes; aligned 5m closes (m%5==0) are identical by construction")

print("\n=== P3  15m terminal bar seen by the MTF gate (close price of last bar) ===")
for m in (14*60+3, 14*60+9, 14*60+14):
    cs = m*60
    r15 = replay_series(15, cs, 70)[-2]['c']; l15 = live_series(15, cs, 70)[-1]
    print(f"{m//60:02d}:{m%60:02d}  replay last REAL 15m close={r15:,.0f} | live forming 15m o={l15['o']:,.0f} l={l15['l']:,.0f} c={l15['c']:,.0f}"
          f"  -> replay blind to {100*(l15['c']-l15['o'])/l15['o']:+.2f}%")

print("\n=== P4  Funding momentum: hourly steps through a 5-min window, threshold 2e-7 ===")
# replay: one sample per hour, step-held. Live proxy (ASSUMPTION): same hourly anchors, linearly interpolated.
anchors = [1e-5 * math.sin(h/3.0) for h in range(0, 25)]
def f_replay(t): return anchors[int(t // 3600)]
def f_live(t):
    h = int(t // 3600); w = (t % 3600)/3600; return anchors[h] + w*(anchors[h+1]-anchors[h])
def mom(fn, t, W=300, thr=2e-7):
    d = fn(t) - fn(t - W); return "RISING" if d > thr else ("FALLING" if d < -thr else "FLAT")
cl = range(3600, 23*3600, 60)
nr = sum(mom(f_replay, t) != "FLAT" for t in cl); nl = sum(mom(f_live, t) != "FLAT" for t in cl)
first5 = sum(mom(f_replay, t) != "FLAT" and (t % 3600) < 300 for t in cl)
print(f"replay non-FLAT {nr}/{len(cl)} ({100*nr/len(cl):.1f}%), all {first5} of them in minutes :00-:04 | live-proxy non-FLAT {nl}/{len(cl)}")

print("\n=== P5  What-if forward-window censoring (bars T+1,T+2 skipped, entry still at T) ===")
random.seed(11)
def walk(path, t_from, t_to, entry, tgt, stp, short=True):
    for t in range(t_from, t_to, 60):
        s = path[t:t+60]; hi, lo = max(s), min(s)
        fav = lo <= tgt if short else hi >= tgt; adv = hi >= stp if short else lo <= stp
        if fav and adv: return "AMB"
        if fav: return "SUCCESS"
        if adv: return "ADVERSE"
    return "EXP"
def ev(o, tgtd, stpd, endpx, entry, atr, short=True):
    if o == "SUCCESS": return tgtd/atr
    if o in ("ADVERSE", "AMB"): return -stpd/atr
    return ((entry-endpx) if short else (endpx-entry))/atr
res = {}
for k in (0.6, 1.0, 1.6):
    true_ev, rep_ev, n = 0, 0, 4000
    for _ in range(n):
        p = [100000.0]
        for t in range(1, 17*60+60): p.append(p[-1] + random.gauss(0, sig*1.8))   # flush-hour vol
        atr = 100000*0.0006; entry = p[2]            # entry at close+2 s
        tgt, stp = entry - 1.75*atr, entry + k*atr
        o_true = walk(p, 2, 2+15*60, entry, tgt, stp)                 # path from entry
        o_rep = walk(p, 120, 15*60, entry, tgt, stp)                  # ForwardWindowJoiner: bars closing T+3..T+15
        true_ev += ev(o_true, 1.75*atr, k*atr, p[2+15*60-1], entry, atr)
        rep_ev += ev(o_rep, 1.75*atr, k*atr, p[15*60-1], entry, atr)
    res[k] = (true_ev/n, rep_ev/n)
    print(f"stop={k:.1f}xATR  EV(from entry)={true_ev/n:+.3f}  EV(what-if window)={rep_ev/n:+.3f}  bias={rep_ev/n-true_ev/n:+.3f} ATR")

print("\n=== P6  CoverageReport.ClassifySpan seq path: outage 09:40 -> 10:25, no repair ===")
GAP = 300000
def classify(stats, span_end, bound=10**18):
    if stats['nos'] == 0 and stats['seqs']:
        s = sorted(stats['seqs']); clean = all(s[i]-s[i-1] <= 1 for i in range(1, len(s)))
    else:
        clean = stats['rows'] > 0 and stats['longest'] <= GAP
    if clean:
        obs = min(span_end, bound)
        if stats['last'] is not None and obs - stats['last'] > GAP: return "TrailingEdge", clean
        return "Captured", clean
    return "Defect", clean
H = 3600000
trades = []; seq = 1000
for ms in range(9*H, 11*H, 1500):                       # a trade every 1.5 s
    if 9*H + 40*60000 <= ms < 10*H + 25*60000: seq += 1; continue   # venue printed it, store lost it
    trades.append((ms, seq)); seq += 1
by = {}; prev = None
for ms, sq in trades:
    h = ms // H * H; st = by.setdefault(h, {'rows': 0, 'seqs': [], 'nos': 0, 'longest': 0, 'last': None})
    st['rows'] += 1; st['seqs'].append(sq); st['last'] = ms
    if prev is not None: st['longest'] = max(st['longest'], ms - prev)
    prev = ms
for h in sorted(by):
    cls, _ = classify(by[h], h + H - 1)
    print(f"hour {h//H:02d}:00  rows={by[h]['rows']} longestGapMs={by[h]['longest']} -> {cls}")
print(f"trades missing from store: {sum(1 for ms in range(9*H+40*60000, 10*H+25*60000, 1500))} (~{(45*60)//1.5:.0f})")

print("\n=== P7  tier_floor knob vs what-if DeriveVerdict (TRANSITIONAL, ADX 21 => penalty 2) ===")
def tier_floor(raw, tf):
    if raw >= tf['ht']: return tf['hf']
    if raw >= tf['mt']: return tf['mf']
    if raw >= tf['lt']: return tf['lf']
    return 0
def tier(eff, mx=15, s=0.70, m=0.53, w=0.35):
    ts, tm, tw = math.ceil(mx*s), math.ceil(mx*m), math.ceil(mx*w)
    return "STRONG" if eff >= ts else "MED" if eff >= tm else "WEAK" if eff >= tw else "NONE"
live_tf = dict(ht=12, hf=9, mt=9, mf=6, lt=6, lf=3)
cell_tf = dict(live_tf, hf=11)
ls, pen = 12, 2
eff_logged = max(ls-pen, tier_floor(ls, live_tf))     # what the CSV carries
eff_cell   = max(ls-pen, tier_floor(ls, cell_tf))     # what live would compute under the cell
print(f"logged EffLS={eff_logged} -> {tier(eff_logged)} | engine under high_floor=11: EffLS={eff_cell} -> {tier(eff_cell)} | "
      f"what-if under high_floor=11 reads logged {eff_logged} -> {tier(eff_logged)}  (knob is a no-op)")
