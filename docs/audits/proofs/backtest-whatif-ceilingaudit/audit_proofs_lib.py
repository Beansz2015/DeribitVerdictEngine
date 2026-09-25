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

