#!/usr/bin/env python3
# tools/ops/rsi_volume_rule_counts.py
#
# RSI and volume rule study, SESSION 1: bars, ported indicators, frozen thresholds, outcome-blind counts, power,
# and the parity check E-1. Spec of record: docs/rsi-volume-rule-study-spec.md. Python 3 standard library only.
#
# What it does:
#   * reads the two dev history stores (C:/DeribitData/history-2023-2024 then C:/DeribitData/history) as one span,
#     2023-01-01 00:00 -> --cut (default 2026-10-06 00:00 UTC, exclusive), checks the TradeSeq seam between them,
#     and builds 1-minute bars on the UTC grid from trades (open = first trade, close = last, volume = sum).
#     3-minute bars are aggregated from the 1-minute bars on the :00/:03/... grid.
#   * ports the engine's RSI(9), ROC(9), volume ratio vs SMA(9), DynamicNorms volume thresholds and dual-anchor VWAP,
#     evaluates them on CLOSED bars only (the bar that closes at T is the signal bar), per session at its own
#     execution resolution (NY 1 min, ASIA and LONDON 3 min), weekdays by the signal bar's close time;
#   * freezes the V1/V2 share-matched thresholds, the R0 3-minute levels and the R1 terciles on H1 days and writes
#     them to docs/rsi-volume-rule-study-frozen.json;
#   * prints counts per rule x session x half (signals, episodes, drops), the V0 ratio-only fire share against
#     candle-store-derivations-2026-07-31.md section 3.1, gap drops per session-year, and a power table;
#   * --parity: E-1, the port against the live engine's analysis_log.csv rows in a fetch folder.
#
# OUTCOME-BLIND BY CONSTRUCTION: no entry price, level, walk or return is computed. Prices are read to build the
# indicator bars; nothing after a signal bar's close is read except the trade COUNT per minute (the gap check on the
# outcome window). ATR is computed only at and before the signal bar, for the power scale.
#
# Volume unit (session-1 decision RVR-10, recorded in docs/rsi-volume-rule-study-session1-2026-10-08.md):
#   the engine's Candle.Volume is BTC (DeribitClient.vb:253, "BTC -- used for scoring"); spec section 4.2 wrote
#   "volume = sum Amount" (USD). Every volume rule and the VWAP use BTC volume = sum(Amount / Price) per trade, the
#   engine's own unit. The USD sum is kept in the bar cache and printed only as a V0 fire-share sensitivity.
#
# Usage (from the repo root):
#   python -I tools/ops/rsi_volume_rule_counts.py --stores C:\DeribitData\history-2023-2024 C:\DeribitData\history --cut 2026-10-06
#   python -I tools/ops/rsi_volume_rule_counts.py --parity aws_fetch/20261007-180541
#   python -I tools/ops/rsi_volume_rule_counts.py --selftest
#
# The bar build caches to C:/DeribitData/rsi-volume-study/ (outside the repo; keyed on the store files' names, sizes
# and mtimes). The cache holds bars only. Delete it to force a rebuild.

import argparse, json, math, os, statistics, sys, time
from array import array
from bisect import bisect_left, bisect_right
from collections import Counter, defaultdict, deque
from datetime import datetime, timedelta
from multiprocessing import Pool

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import liq_tradeflow_counts as tc   # noqa: E402  (seam_check / check_store_seams: read-only reuse)

EPOCH = datetime(1970, 1, 1)
START = datetime(2023, 1, 1)
SEAM = datetime(2025, 1, 1)
DEFAULT_STORES = ["C:/DeribitData/history-2023-2024", "C:/DeribitData/history"]
CACHE_DIR = "C:/DeribitData/rsi-volume-study"
FROZEN_PATH = "docs/rsi-volume-rule-study-frozen.json"


def ms(dt):
    return int((dt - EPOCH).total_seconds() * 1000)


def utc_min(m):
    return EPOCH + timedelta(minutes=m)


START_MIN = ms(START) // 60000
SEAM_MIN = ms(SEAM) // 60000

# ---------------------------------------------------------------- pins (spec sections 3, 4.2 - 4.7)
LOOKBACK = 250                                   # bars per signal (the engine fetches 250 candles)
GAP_RUN = 5                                      # a gap = more than 5 consecutive empty 1-minute bars
V1_DAYS = 20                                     # V1 baseline: same slot, previous 20 weekdays
V2_WIN = 100                                     # V2: percentile rank within the previous 100 bars
DYN_WIN = 100                                    # DynamicNorms: 100 completed bars before the signal bar
ATR_BARS = 50                                    # ATR(7) over the last 50 completed bars (spec section 4.7)
HOLD_MIN = {"NY": 15, "ASIA": 45, "LONDON": 45}  # outcome window W, also the episode window
SESSIONS = ("ASIA", "LONDON", "NY")
RES_OF = {"ASIA": 3, "LONDON": 3, "NY": 1}
REF_FIRE = {"NY": 2.16, "ASIA": 3.17, "LONDON": 3.27}   # candle-store-derivations-2026-07-31.md section 3.1, %
MIN_N = 100
SIGMA_CONS, SIGMA_OPT = 34.3, 14.7               # published sigma proxies (a4_liq_ofi_counts.py power block)
EFFECTS = (5.0, 10.0, 20.0)
FAMILY_M = 9                                     # Holm family size per family (spec section 4.8)
HYPS = [("VOL-H1", "V0", "V-ctl"), ("VOL-H2", "V1", "V0"), ("VOL-H3", "V2", "V0"),
        ("RSI-H1", "R0", "R-ctl"), ("RSI-H2", "R1", "R0"), ("RSI-H3", "R2", "R0")]
RULES = ("V0", "V1", "V2", "V-ctl", "R0", "R1", "R2", "R-ctl")


def session_of(hour):
    if 0 <= hour <= 7:
        return "ASIA"
    if 8 <= hour <= 12:
        return "LONDON"
    return "NY"


def load_pins(path="settings.json"):
    """SHIPPED BEHAVIOUR: every engine value is derived from tracked settings.json, never restated."""
    with open(path, encoding="utf-8") as f:
        s = json.load(f)
    i, sc = s["indicators"], s["scoring"]
    sv = {b["name"]: b for b in s["session_volume"]["sessions"]}
    p = {"version": s["version"],
         "rsi_period": i["RSI"]["period"], "rsi_ob": i["RSI"]["overbought"], "rsi_os": i["RSI"]["oversold"],
         "rsi_pob": i["RSI"]["partial_overbought"], "rsi_pos": i["RSI"]["partial_oversold"],
         "roc_period": i["ROC"]["period"], "sma_period": i["Volume"]["sma_period"],
         "static_high": i["Volume"]["static_high"], "static_mid": i["Volume"]["static_mid"],
         "hi_min": i["Volume"]["dynamic_high_clamp_min"], "hi_max": i["Volume"]["dynamic_high_clamp_max"],
         "mid_min": i["Volume"]["dynamic_mid_clamp_min"], "mid_max": i["Volume"]["dynamic_mid_clamp_max"],
         "vwap_h": i["VWAP"]["session2_start_hour"], "vwap_m": i["VWAP"]["session2_start_minute"],
         "atr_period": i["ATR"]["period"],
         "sv_enabled": s["session_volume"]["enabled"],
         "hmult": {k: sv[k]["high_multiplier"] for k in SESSIONS}, "mmult": {k: sv[k]["mid_multiplier"] for k in SESSIONS},
         "res": {k: sv[k]["execution_resolution"] for k in SESSIONS},
         "hours": {k: (sv[k]["start_hour"], sv[k]["end_hour"]) for k in SESSIONS},
         "fee_rt_bps": 2 * sc["trade_costs"]["maker_fee_bps"], "fee_style": sc["trade_costs"]["round_trip_style"],
         "tgt": {"NY": sc["atr_target_multiplier"],
                 "LONDON": sc["structural_levels"]["sessions"]["LONDON"]["fallback_target_atr_mult"],
                 "ASIA": sc["structural_levels"]["sessions"]["ASIA"]["fallback_target_atr_mult"]},
         "stop": sc["atr_stop_multiplier"]}
    # The spec pins v69 values; a different settings file is a STOP, not a silent change of the design.
    want = {"rsi_period": 9, "rsi_ob": 60.0, "rsi_os": 40.0, "rsi_pob": 55.0, "rsi_pos": 45.0, "roc_period": 9,
            "sma_period": 9, "static_high": 3.0, "static_mid": 2.0, "hi_min": 2.0, "hi_max": 6.0, "mid_min": 1.5,
            "mid_max": 4.0, "vwap_h": 13, "vwap_m": 30, "atr_period": 7, "fee_rt_bps": 3.0, "stop": 1.6}
    bad = {k: (p[k], v) for k, v in want.items() if p[k] != v}
    if p["res"] != RES_OF or p["hours"] != {"ASIA": (0, 7), "LONDON": (8, 12), "NY": (13, 23)}:
        bad["sessions"] = (p["res"], p["hours"])
    if p["hmult"] != {"ASIA": 1.0, "LONDON": 1.0, "NY": 1.15} or p["tgt"] != {"NY": 1.75, "LONDON": 2.0, "ASIA": 1.25}:
        bad["mult"] = (p["hmult"], p["tgt"])
    if bad:
        raise SystemExit("STOP: settings.json differs from the spec's v69 pins: %r" % bad)
    return p


# ---------------------------------------------------------------- bar build (multiprocess, one job per month file)
def month_of(path):
    b = os.path.basename(path)[len("trades_"):-len(".csv")]
    y, m = int(b[:4]), int(b[5:7])
    m0 = ms(datetime(y, m, 1)) // 60000
    m1 = ms(datetime(y + (m == 12), m % 12 + 1, 1)) // 60000
    return m0, m1


def parse_file(job):
    """One month file -> raw (unfilled) 1-minute aggregates over that month's minutes. Reads Timestamp, Price, Amount.
    A trade outside the month's minutes is returned in `extra` and merged by the caller."""
    path, cut_ms = job
    m0, m1 = month_of(path)
    L = m1 - m0
    o = [0.0] * L; h = [0.0] * L; l = [0.0] * L; c = [0.0] * L
    vb = [0.0] * L; vu = [0.0] * L; n = [0] * L
    extra = []
    back = 0
    last_t = -1
    first_t = None
    past_cut = 0
    with open(path, "rb") as f:
        f.readline()
        for line in f:
            a = line.split(b",", 3)
            t = int(a[0])
            if t >= cut_ms:
                past_cut += 1
                continue
            p = float(a[1]); am = float(a[2])
            if first_t is None:
                first_t = t
            if t < last_t:
                back += 1
            last_t = t
            k = t // 60000 - m0
            if 0 <= k < L:
                if n[k] == 0:
                    o[k] = p; h[k] = p; l[k] = p
                elif p > h[k]:
                    h[k] = p
                elif p < l[k]:
                    l[k] = p
                c[k] = p
                vu[k] += am
                vb[k] += am / p
                n[k] += 1
            else:
                extra.append((t, p, am))
    return (path, m0, array("d", o).tobytes(), array("d", h).tobytes(), array("d", l).tobytes(),
            array("d", c).tobytes(), array("d", vb).tobytes(), array("d", vu).tobytes(), array("i", n).tobytes(),
            extra, back, first_t, last_t, past_cut)


class Bars:
    """Bars on one resolution. Arrays indexed from START. n = trade count (0 = empty bar, OHLC = previous close)."""

    def __init__(self, res, o, h, l, c, vb, vu, n):
        self.res, self.o, self.h, self.l, self.c, self.vb, self.vu, self.n = res, o, h, l, c, vb, vu, n

    def __len__(self):
        return len(self.c)

    def close_min(self, i):
        return START_MIN + (i + 1) * self.res


def store_paths(stores, cut):
    paths = []
    groups = []
    for d in stores:
        g = []
        for name in sorted(os.listdir(d)):
            if name.startswith("trades_") and name.endswith(".csv"):
                m0, _ = month_of(name)
                if START_MIN <= m0 < ms(cut) // 60000:
                    g.append(os.path.join(d, name))
        groups.append(g)
        paths += g
    return groups, paths


def build_1m(paths, cut, out=print, workers=12):
    N = ms(cut) // 60000 - START_MIN
    meta = {"cut": cut.isoformat(), "files": [[os.path.basename(os.path.dirname(p)), os.path.basename(p),
                                               os.path.getsize(p), int(os.path.getmtime(p))] for p in paths]}
    os.makedirs(CACHE_DIR, exist_ok=True)
    tag = "bars1m_cut%s" % cut.strftime("%Y%m%d")
    mpath, bpath = os.path.join(CACHE_DIR, tag + ".json"), os.path.join(CACHE_DIR, tag + ".bin")
    if os.path.exists(mpath) and os.path.exists(bpath):
        with open(mpath, encoding="utf-8") as f:
            m = json.load(f)
        if m.get("files") == meta["files"] and m.get("cut") == meta["cut"]:
            arrs = []
            with open(bpath, "rb") as f:
                for code in "ddddddi":
                    a = array(code)
                    a.fromfile(f, N)
                    arrs.append(a)
            out("bar cache HIT %s (%d minutes); build stats as recorded: %s" % (bpath, N, m.get("stats")))
            return Bars(1, *arrs), m.get("stats")
    t0 = time.time()
    o = array("d", bytes(8 * N)); h = array("d", bytes(8 * N)); l = array("d", bytes(8 * N)); c = array("d", bytes(8 * N))
    vb = array("d", bytes(8 * N)); vu = array("d", bytes(8 * N)); n = array("i", bytes(4 * N))
    stats = Counter()
    extras = []
    cut_ms = ms(cut)
    with Pool(workers) as pool:
        for res in pool.imap(parse_file, [(p, cut_ms) for p in paths]):
            path, m0, bo, bh, bl, bc, bvb, bvu, bn, extra, back, ft, lt, pc = res
            ao = array("d"); ao.frombytes(bo); ah = array("d"); ah.frombytes(bh); al = array("d"); al.frombytes(bl)
            ac = array("d"); ac.frombytes(bc); avb = array("d"); avb.frombytes(bvb); avu = array("d"); avu.frombytes(bvu)
            an = array("i"); an.frombytes(bn)
            base = m0 - START_MIN
            for k in range(len(an)):
                j = base + k
                if not (0 <= j < N) or an[k] == 0:
                    continue
                if n[j] != 0:
                    raise SystemExit("STOP: minute %s filled by two month files" % utc_min(START_MIN + j))
                o[j] = ao[k]; h[j] = ah[k]; l[j] = al[k]; c[j] = ac[k]; vb[j] = avb[k]; vu[j] = avu[k]; n[j] = an[k]
            stats["trades"] += sum(an)
            stats["out_of_order_trades"] += back
            stats["trades_past_cut"] += pc
            stats["trades_outside_file_month"] += len(extra)
            extras += extra
            out("  read %-28s trades %10d  out-of-order %d  outside-month %d  (%.0fs)"
                % (os.path.basename(path), sum(an), back, len(extra), time.time() - t0))
    for t, p, am in sorted(extras):
        j = t // 60000 - START_MIN
        if not (0 <= j < N):
            stats["trades_outside_span"] += 1
            continue
        if n[j] == 0:
            o[j] = p; h[j] = p; l[j] = p
        h[j] = max(h[j], p); l[j] = min(l[j], p); c[j] = p
        vb[j] += am / p; vu[j] += am; n[j] += 1
        stats["trades"] += 1
    stats = dict(stats)
    meta["stats"] = stats
    fill(o, h, l, c, n)
    with open(bpath, "wb") as f:
        for a in (o, h, l, c, vb, vu, n):
            a.tofile(f)
    with open(mpath, "w", encoding="utf-8") as f:
        json.dump(meta, f)
    out("bar build done in %.0fs: %s" % (time.time() - t0, stats))
    return Bars(1, o, h, l, c, vb, vu, n), stats


def fill(o, h, l, c, n):
    """Empty bar: OHLC = previous close (spec section 4.2). The first bars before any trade take the first close."""
    first = next((j for j in range(len(n)) if n[j] > 0), None)
    if first is None:
        raise SystemExit("STOP: no trade in the span")
    prev = o[first]
    for j in range(len(n)):
        if n[j] == 0:
            o[j] = h[j] = l[j] = c[j] = prev
        else:
            prev = c[j]


def aggregate(b1, res):
    """res-minute bars on the UTC grid from FILLED 1-minute bars: open = first non-empty bar's open, high/low over the
    non-empty bars, close = last non-empty close; an all-empty group takes the previous close."""
    M = len(b1) // res
    o = array("d", bytes(8 * M)); h = array("d", bytes(8 * M)); l = array("d", bytes(8 * M)); c = array("d", bytes(8 * M))
    vb = array("d", bytes(8 * M)); vu = array("d", bytes(8 * M)); n = array("i", bytes(4 * M))
    prev = b1.c[0]
    for j in range(M):
        k0 = j * res
        cnt = 0
        for k in range(k0, k0 + res):
            if b1.n[k] > 0:
                if cnt == 0:
                    o[j] = b1.o[k]; h[j] = b1.h[k]; l[j] = b1.l[k]
                else:
                    if b1.h[k] > h[j]: h[j] = b1.h[k]
                    if b1.l[k] < l[j]: l[j] = b1.l[k]
                c[j] = b1.c[k]
                cnt += b1.n[k]
                vb[j] += b1.vb[k]; vu[j] += b1.vu[k]
        n[j] = cnt
        if cnt == 0:
            o[j] = h[j] = l[j] = c[j] = prev
        prev = c[j]
    return Bars(res, o, h, l, c, vb, vu, n)


def gap_prefix(b1):
    """Prefix count of minutes that sit inside a gap (a run of more than GAP_RUN consecutive empty 1-minute bars)."""
    N = len(b1)
    g = bytearray(N)
    j = 0
    runs = []
    while j < N:
        if b1.n[j] == 0:
            k = j
            while k < N and b1.n[k] == 0:
                k += 1
            if k - j > GAP_RUN:
                for x in range(j, k):
                    g[x] = 1
                runs.append((j, k))
            j = k
        else:
            j += 1
    pre = array("i", bytes(4 * (N + 1)))
    s = 0
    for x in range(N):
        s += g[x]
        pre[x + 1] = s
    return pre, runs


# ---------------------------------------------------------------- ported indicators (Core/Indicators_*.vb, DynamicNorms.vb)
def rsi_list(closes, period):
    """IndicatorEngine.CalcRSI over one candle list (Core/Indicators_Momentum.vb:127-145), exact port."""
    if len(closes) < period + 1:
        return 50.0
    gains = [max(closes[i] - closes[i - 1], 0.0) for i in range(1, len(closes))]
    losses = [max(closes[i - 1] - closes[i], 0.0) for i in range(1, len(closes))]
    ag = sum(gains[:period]) / period
    al = sum(losses[:period]) / period
    for i in range(period, len(gains)):
        ag = (ag * (period - 1) + gains[i]) / period
        al = (al * (period - 1) + losses[i]) / period
    if al == 0:
        return 100.0
    return 100.0 - 100.0 / (1.0 + ag / al)


def rsi_running(c, period):
    """The same Wilder recursion carried along the whole bar series, seeded at the series start. Differs from the
    250-bar list form by the seed's weight ((period-1)/period)^~240, i.e. < 1e-11 (selftest and E-1 measure it)."""
    N = len(c)
    out = array("d", [float("nan")] * N)
    if N < period + 1:
        return out
    ag = sum(max(c[i] - c[i - 1], 0.0) for i in range(1, period + 1)) / period
    al = sum(max(c[i - 1] - c[i], 0.0) for i in range(1, period + 1)) / period
    pm1 = period - 1
    for i in range(period, N):
        if i > period:
            d = c[i] - c[i - 1]
            ag = (ag * pm1 + (d if d > 0 else 0.0)) / period
            al = (al * pm1 + (-d if d < 0 else 0.0)) / period
        out[i] = 100.0 if al == 0 else 100.0 - 100.0 / (1.0 + ag / al)
    return out


def atr_list(o_h, o_l, o_c, period):
    """IndicatorEngine.CalcATR over one candle list (Core/Indicators_Momentum.vb:98-111), exact port."""
    n = len(o_c)
    if n < period + 1:
        return 0.0
    tr = [max(o_h[i] - o_l[i], abs(o_h[i] - o_c[i - 1]), abs(o_l[i] - o_c[i - 1])) for i in range(1, n)]
    a = sum(tr[:period]) / period
    for i in range(period, len(tr)):
        a = (a * (period - 1) + tr[i]) / period
    return a


def atr_at(b, i, period):
    """ATR(7) over the 50 completed bars ending at bar i (spec section 4.7)."""
    s = i - ATR_BARS + 1
    return atr_list(b.h[s:i + 1], b.l[s:i + 1], b.c[s:i + 1], period)


def dyn_thresholds(vols, P, sess):
    """DynamicNorms.Compute volume block (DynamicNorms.vb:36-60) on the window passed (the 100 completed bars before
    the signal bar), then ApplySessionVolume (x session multiplier). Population variance, as .Average() is."""
    m = sum(vols) / len(vols)
    sd = math.sqrt(sum((v - m) ** 2 for v in vols) / len(vols))
    if sd < m * 0.05:
        hi, mid = P["static_high"], P["static_mid"]
    else:
        hi = min(max((m + 2.0 * sd) / m, P["hi_min"]), P["hi_max"])
        mid = min(max((m + 1.0 * sd) / m, P["mid_min"]), P["mid_max"])
    if P["sv_enabled"]:
        hi *= P["hmult"][sess]
        mid *= P["mmult"][sess]
    return hi, mid


def vwap_session_start_min(now_min_float, P):
    """GetSessionCandles (Core/Indicators_Volatility.vb:24-40): 00:00 UTC before 13:30, else 13:30, of now's date."""
    day = int(now_min_float // 1440)
    mod = now_min_float - day * 1440
    s2 = P["vwap_h"] * 60 + P["vwap_m"]
    return day * 1440 + (0 if mod < s2 else s2)


class Prefix:
    def __init__(self, b):
        N = len(b)
        self.tpv = array("d", bytes(8 * (N + 1)))
        self.v = array("d", bytes(8 * (N + 1)))
        a = 0.0; s = 0.0
        for i in range(N):
            a += (b.h[i] + b.l[i] + b.c[i]) / 3.0 * b.vb[i]
            s += b.vb[i]
            self.tpv[i + 1] = a
            self.v[i + 1] = s


def vwap_closed(b, pre, i, P):
    """CalcVWAP on the closed-bar candle list: the LOOKBACK bars ending at bar i, filtered to bars that OPEN at or
    after the session start of the signal bar's last instant (close - 1 ms); empty filter -> the whole list."""
    lo = i - LOOKBACK + 1
    close_min = START_MIN + (i + 1) * b.res
    ss = vwap_session_start_min(close_min - 1e-6, P)
    first = max(lo, math.ceil((ss - START_MIN) / b.res))
    if first > i:
        first = lo
    v = pre.v[i + 1] - pre.v[first]
    return ((pre.tpv[i + 1] - pre.tpv[first]) / v if v > 0 else 0.0), i - first + 1


def pct_rank(window, x):
    lt = 0; eq = 0
    for w in window:
        if w < x:
            lt += 1
        elif w == x:
            eq += 1
    return (lt + 0.5 * eq) / len(window)


# ---------------------------------------------------------------- pass A: per evaluated bar
def weekday_of_min(m):
    return (m // 1440 + 3) % 7          # 1970-01-01 is a Thursday; Monday = 0


def evaluate(b, sess_set, gpre, cut_min, P, out=print):
    """Every weekday bar at resolution b.res whose close hour falls in one of sess_set. Returns a list of dict rows
    in time order (all of them, eligible or not), plus 1-minute RSI rows for the R0 mapping when b.res == 1."""
    res = b.res
    rsi = rsi_running(b.c, P["rsi_period"])
    pre = Prefix(b)
    slots = defaultdict(lambda: deque(maxlen=V1_DAYS))
    rows = []
    rp, sp = P["roc_period"], P["sma_period"]
    for i in range(len(b)):
        cm = START_MIN + (i + 1) * res
        if weekday_of_min(cm) >= 5:
            continue
        hour = (cm % 1440) // 60
        sess = session_of(hour)
        if sess not in sess_set:
            continue
        slot = (cm % 1440) // res
        dq = slots[slot]
        v = b.vb[i]
        v1 = None
        if len(dq) == V1_DAYS:
            med = statistics.median(dq)
            v1 = (v / med) if med > 0 else (float("inf") if v > 0 else 0.0)
        dq.append(v)
        # the signal bar's UTC day is its close minute's day; a close at exactly 00:00 belongs to the new day (house
        # scope by close time)
        r = {"i": i, "cm": cm, "day": cm // 1440, "sess": sess, "v1": v1}
        if i < LOOKBACK:
            r["drop"] = "warmup"
            rows.append(r)
            continue
        W = HOLD_MIN[sess]
        lb0 = cm - LOOKBACK * res - START_MIN
        if cm + W > cut_min:
            r["drop"] = "window_past_cut"
        elif gpre[cm + W - START_MIN] - gpre[lb0] > 0:
            r["drop"] = "gap"
        elif v1 is None:
            r["drop"] = "warmup"
        else:
            r["drop"] = None
        r["seam"] = (lb0 + START_MIN) < SEAM_MIN <= cm
        c = b.c
        r["close"] = c[i]
        r["rsi"] = rsi[i]
        r["roc"] = (c[i] - c[i - rp]) / c[i - rp] * 100.0
        sma = sum(b.vb[i - sp + 1:i + 1]) / sp
        r["ratio"] = v / sma if sma > 0 else 1.0
        r["hi"], r["mid"] = dyn_thresholds(b.vb[i - DYN_WIN:i], P, sess)
        r["vwap"], r["vwap_n"] = vwap_closed(b, pre, i, P)
        r["v2"] = pct_rank(b.vb[i - V2_WIN:i], v)
        smau = sum(b.vu[i - sp + 1:i + 1]) / sp
        r["ratio_usd"] = b.vu[i] / smau if smau > 0 else 1.0
        r["hi_usd"], _ = dyn_thresholds(b.vu[i - DYN_WIN:i], P, sess)
        rows.append(r)
    return rows, rsi


def rsi1_rows(b1, rsi1, gpre, P):
    """1-minute RSI at weekday 1-minute bars whose close hour is in ASIA or LONDON, lookback gap-free (R0 mapping)."""
    out = []
    for i in range(LOOKBACK, len(b1)):
        cm = START_MIN + i + 1
        if weekday_of_min(cm) >= 5:
            continue
        sess = session_of((cm % 1440) // 60)
        if sess == "NY":
            continue
        if gpre[cm - START_MIN] - gpre[cm - LOOKBACK - START_MIN] > 0:
            continue
        out.append((cm // 1440, sess, rsi1[i]))
    return out


# ---------------------------------------------------------------- thresholds (frozen on H1 days)
def upper_level(vals, share):
    """x such that exactly round(share * n) values are strictly above x (ties aside): the (n - m)-th smallest."""
    a = sorted(vals)
    n = len(a)
    m = int(round(share * n))
    return a[n - m - 1] if 0 < m < n else (a[-1] if m <= 0 else a[0])


def lower_level(vals, share):
    a = sorted(vals)
    n = len(a)
    m = int(round(share * n))
    return a[m] if 0 < m < n else (a[0] if m <= 0 else a[-1])


def share_ge_level(vals, share):
    """k with share(vals >= k) closest to `share`, over the distinct candidate values (handles discrete metrics)."""
    a = sorted(vals)
    n = len(a)
    best = None
    # candidates: each distinct value; share(>= x) = (n - bisect_left(a, x)) / n
    seen = set()
    m = int(round(share * n))
    lo_i = max(0, n - m - 2000)
    for idx in range(lo_i, n):
        x = a[idx]
        if x in seen:
            continue
        seen.add(x)
        s = (n - bisect_left(a, x)) / n
        d = abs(s - share)
        if best is None or d < best[0]:
            best = (d, x, s)
    return best[1], best[2]


def halves(rows_all):
    days = sorted({r["day"] for r in rows_all if r["drop"] is None})
    D = len(days)
    h1 = set(days[:D // 2])
    return D, days, h1


def freeze(rows_by_sess, r1rows, h1, P, out=print):
    F = {"V0_share_H1": {}, "k1": {}, "k1_share": {}, "k2": {}, "k2_share": {}, "R0": {}, "R0_shares_1m": {},
         "R0_achieved": {}, "R1": {}}
    for s in SESSIONS:
        el = [r for r in rows_by_sess[s] if r["drop"] is None and r["day"] in h1]
        share = sum(1 for r in el if r["ratio"] >= r["hi"]) / len(el)
        F["V0_share_H1"][s] = share
        k1 = upper_level([r["v1"] for r in el], share)
        # V1 fires on metric >= k1: re-measure the achieved share (ties at k1 count)
        F["k1"][s] = k1
        F["k1_share"][s] = sum(1 for r in el if r["v1"] >= k1) / len(el)
        k2, s2 = share_ge_level([r["v2"] for r in el], share)
        F["k2"][s], F["k2_share"][s] = k2, s2
        rv = [r["rsi"] for r in el]
        F["R1"][s] = {"lo": lower_level(rv, 1 / 3), "hi": upper_level(rv, 1 / 3)}
        if RES_OF[s] == 1:
            F["R0"][s] = {"U": P["rsi_ob"], "L": P["rsi_os"], "PU": P["rsi_pob"], "PL": P["rsi_pos"]}
        else:
            r1 = [x[2] for x in r1rows if x[1] == s and x[0] in h1]
            sh = {"U": sum(1 for x in r1 if x > P["rsi_ob"]) / len(r1), "L": sum(1 for x in r1 if x < P["rsi_os"]) / len(r1),
                  "PU": sum(1 for x in r1 if x > P["rsi_pob"]) / len(r1), "PL": sum(1 for x in r1 if x < P["rsi_pos"]) / len(r1)}
            F["R0_shares_1m"][s] = dict(sh, n_1m=len(r1))
            F["R0"][s] = {"U": upper_level(rv, sh["U"]), "L": lower_level(rv, sh["L"]),
                          "PU": upper_level(rv, sh["PU"]), "PL": lower_level(rv, sh["PL"])}
        lv = F["R0"][s]
        F["R0_achieved"][s] = {"gtU": sum(1 for x in rv if x > lv["U"]) / len(rv), "ltL": sum(1 for x in rv if x < lv["L"]) / len(rv),
                               "gtPU": sum(1 for x in rv if x > lv["PU"]) / len(rv), "ltPL": sum(1 for x in rv if x < lv["PL"]) / len(rv),
                               "n": len(rv)}
    return F


# ---------------------------------------------------------------- pass B: fires, episodes, counts
def direction_vol(r):
    if r["roc"] > 0 and r["close"] > r["vwap"]:
        return "L"
    if r["roc"] < 0 and r["close"] < r["vwap"]:
        return "S"
    return None


def fires(r, F):
    """{rule: side} for one evaluated bar. Full votes only (partials are counted separately)."""
    s = r["sess"]
    out = {}
    d = direction_vol(r)
    v0 = r["ratio"] >= r["hi"]
    if d:
        if v0:
            out["V0"] = d
        else:
            out["V-ctl"] = d
        if r["v1"] is not None and r["v1"] >= F["k1"][s]:
            out["V1"] = d
        if r["v2"] >= F["k2"][s]:
            out["V2"] = d
    x = r["rsi"]
    lv = F["R0"][s]
    r0 = "L" if x > lv["U"] else ("S" if x < lv["L"] else None)
    if r0:
        out["R0"] = r0
    t = F["R1"][s]
    r1 = "L" if x > t["hi"] else ("S" if x < t["lo"] else None)
    if r1:
        out["R1"] = r1
    r2 = "L" if x > 50.0 else ("S" if x < 50.0 else None)
    if r2:
        out["R2"] = r2
    if r["roc"] > 0 and r0 != "L":
        out["R-ctl"] = "L"
    elif r["roc"] < 0 and r0 != "S":
        out["R-ctl"] = "S"
    return out


def partials(r, F):
    s = r["sess"]
    out = {}
    d = direction_vol(r)
    if d and r["mid"] <= r["ratio"] < r["hi"]:
        out["V0p"] = d
    lv = F["R0"][s]
    x = r["rsi"]
    if lv["PU"] < x <= lv["U"]:
        out["R0p"] = "L"
    elif lv["L"] <= x < lv["PL"]:
        out["R0p"] = "S"
    return out


def episodes(rows, F, h1):
    """Walk every evaluated bar in time order. A fire on an eligible bar is a signal; it is an episode if no same-side
    fire of the same rule (eligible or not) closed in the previous W minutes (keep when T - t_prev >= W)."""
    last = {}
    cnt = Counter()
    eps = []
    h1_last = max(h1)
    for r in rows:
        if r["drop"] == "warmup" and "rsi" not in r:
            continue
        # days before H1's first day hold only warm-up drops; they belong to neither half
        half = "H1" if r["day"] in h1 else ("H2" if r["day"] > h1_last else "pre")
        W = HOLD_MIN[r["sess"]]
        fs = fires(r, F)
        for rule, side in fs.items():
            key = (rule, side)
            prev = last.get(key)
            last[key] = r["cm"]
            base = (rule, r["sess"], half)
            if r["drop"] is not None:
                cnt[base + ("drop_" + r["drop"],)] += 1
                continue
            cnt[base + ("signals",)] += 1
            cnt[base + ("signals_" + side,)] += 1
            if prev is not None and r["cm"] - prev < W:
                continue
            cnt[base + ("episodes",)] += 1
            cnt[base + ("episodes_" + side,)] += 1
            if r["seam"]:
                cnt[base + ("episodes_seam",)] += 1
            eps.append((rule, side, r))
        if r["drop"] is None:
            for rule, side in partials(r, F).items():
                cnt[(rule, r["sess"], half, "partial_" + side)] += 1
    return cnt, eps


# ---------------------------------------------------------------- power (a4_liq_ofi_counts.py power block, copied)
Z = statistics.NormalDist()


def mde(alpha, sigma, n1, n2=None, power=0.80):
    if n1 <= 0 or (n2 is not None and n2 <= 0):
        return float("nan")
    se = sigma * (math.sqrt(1.0 / n1 + 1.0 / n2) if n2 is not None else math.sqrt(1.0 / n1))
    return (Z.inv_cdf(1 - alpha / 2) + Z.inv_cdf(power)) * se


def power_at(alpha, sigma, d, n1, n2=None):
    if n1 <= 0 or (n2 is not None and n2 <= 0):
        return float("nan")
    se = sigma * (math.sqrt(1.0 / n1 + 1.0 / n2) if n2 is not None else math.sqrt(1.0 / n1))
    return Z.cdf(d / se - Z.inv_cdf(1 - alpha / 2))


def fmt(x, nd=1):
    return "n/a" if x is None or (isinstance(x, float) and math.isnan(x)) else ("%.*f" % (nd, x))


# ---------------------------------------------------------------- the run
def run(stores, cut, P, out=print, workers=12):
    t0 = time.time()
    groups, paths = store_paths(stores, cut)
    out("store files: %d in %d stores, bytes %d" % (len(paths), len(groups), sum(os.path.getsize(p) for p in paths)))
    for d, g in zip(stores, groups):
        out("  %s  %s .. %s  files %d" % (d, os.path.basename(g[0]), os.path.basename(g[-1]), len(g)))
    tc.check_store_seams(groups, out)
    b1, bstats = build_1m(paths, cut, out, workers)
    N = len(b1)
    cut_min = ms(cut) // 60000
    out("1-minute bars: %d (%s .. %s exclusive); empty bars %d" % (N, START, cut, sum(1 for x in b1.n if x == 0)))
    gpre, runs = gap_prefix(b1)
    out("gaps (runs of > %d empty 1-minute bars): %d runs, %d minutes; longest %s"
        % (GAP_RUN, len(runs), gpre[N], max(((k - j), str(utc_min(START_MIN + j))) for j, k in runs) if runs else None))
    gbyyear = Counter()
    for j, k in runs:
        gbyyear[utc_min(START_MIN + j).year] += k - j
    out("gap minutes by year: %s" % dict(sorted(gbyyear.items())))
    # STOP 4 asks for the hours: where the gaps sit (run start, UTC)
    gh = Counter(); gw = Counter(); gt = Counter(); gy = Counter()
    for j, k in runs:
        t = utc_min(START_MIN + j)
        gh[t.hour] += 1; gw[t.strftime("%a")] += 1; gt[t.strftime("%H:%M")] += 1
        if t.weekday() < 5:
            gy[t.year] += 1
    out("gap runs by start hour (UTC): %s" % "  ".join("%02d:%d" % x for x in sorted(gh.items())))
    out("gap runs by weekday: %s;  weekday runs by year: %s" % (dict(gw.most_common()), dict(sorted(gy.items()))))
    out("most common start times: %s" % gt.most_common(6))
    wk9 = [(str(utc_min(START_MIN + j).date()), utc_min(START_MIN + j).strftime("%a"), k - j) for j, k in runs
           if utc_min(START_MIN + j).hour == 9 and utc_min(START_MIN + j).minute <= 1 and utc_min(START_MIN + j).weekday() < 5]
    out("weekday runs starting 09:00-09:01 UTC: %d (minutes p50 %s): %s" % (len(wk9), statistics.median(x[2] for x in wk9) if wk9 else "-",
                                                                        "  ".join("%s %s %d" % x for x in wk9)))
    b3 = aggregate(b1, 3)
    out("3-minute bars: %d" % len(b3))
    rowsA, rsi1 = evaluate(b1, {"NY"}, gpre, cut_min, P, out)
    rowsB, _ = evaluate(b3, {"ASIA", "LONDON"}, gpre, cut_min, P, out)
    r1rows = rsi1_rows(b1, rsi1, gpre, P)
    rows = sorted(rowsA + rowsB, key=lambda r: r["cm"])
    out("evaluated weekday bars: NY %d, ASIA+LONDON %d  (%.0fs)" % (len(rowsA), len(rowsB), time.time() - t0))
    by_sess = {s: [r for r in rows if r["sess"] == s] for s in SESSIONS}
    D, days, h1 = halves(rows)
    h1_days = sorted(h1)
    h2_days = [d for d in days if d not in h1]
    dd = lambda d: (EPOCH + timedelta(days=d)).date()
    out("")
    out("=== 2. Halves (spec section 4.8): D = %d UTC weekdays with at least one eligible signal bar" % D)
    out("H1 = %d days %s .. %s ; H2 = %d days %s .. %s"
        % (len(h1_days), dd(h1_days[0]), dd(h1_days[-1]), len(h2_days), dd(h2_days[0]), dd(h2_days[-1])))
    F = freeze(by_sess, r1rows, h1, P, out)
    F["meta"] = {"D": D, "H1_first": str(dd(h1_days[0])), "H1_last": str(dd(h1_days[-1])), "H2_first": str(dd(h2_days[0])),
                 "H2_last": str(dd(h2_days[-1])), "cut": cut.isoformat(), "settings_version": P["version"],
                 "volume_unit": "BTC (sum Amount/Price), RVR-10"}
    return b1, b3, gpre, rows, by_sess, D, h1, F, bstats


def report(b1, b3, gpre, rows, by_sess, D, h1, F, P, out=print):
    out("")
    out("=== 3. Frozen thresholds (H1 days only; spec sections 4.4, 4.5) ===")
    for s in SESSIONS:
        out("%-7s V0 ratio-only fire share on H1 = %.4f%%   k1 = %.4f (V1 share %.4f%%)   k2 = %.4f (V2 share %.4f%%)"
            % (s, 100 * F["V0_share_H1"][s], F["k1"][s], 100 * F["k1_share"][s], F["k2"][s], 100 * F["k2_share"][s]))
    for s in SESSIONS:
        lv, ac = F["R0"][s], F["R0_achieved"][s]
        extra = ""
        if s in F["R0_shares_1m"]:
            sh = F["R0_shares_1m"][s]
            extra = ("   1-min shares (n=%d): >60 %.2f%%  <40 %.2f%%  >55 %.2f%%  <45 %.2f%%"
                     % (sh["n_1m"], 100 * sh["U"], 100 * sh["L"], 100 * sh["PU"], 100 * sh["PL"]))
        out("%-7s R0 U %.3f  L %.3f  PU %.3f  PL %.3f   achieved on H1 (n=%d): >U %.2f%%  <L %.2f%%  >PU %.2f%%  <PL %.2f%%%s"
            % (s, lv["U"], lv["L"], lv["PU"], lv["PL"], ac["n"], 100 * ac["gtU"], 100 * ac["ltL"], 100 * ac["gtPU"], 100 * ac["ltPL"], extra))
    for s in SESSIONS:
        out("%-7s R1 terciles: lo %.3f  hi %.3f   R2 midline 50" % (s, F["R1"][s]["lo"], F["R1"][s]["hi"]))

    out("")
    out("=== 4. V0 ratio-only full-fire share vs candle-store-derivations-2026-07-31.md section 3.1 (STOP 2) ===")
    stop2 = []
    for s in SESSIONS:
        el = [r for r in by_sess[s] if r["drop"] is None]
        sh = 100.0 * sum(1 for r in el if r["ratio"] >= r["hi"]) / len(el)
        shu = 100.0 * sum(1 for r in el if r["ratio_usd"] >= r["hi_usd"]) / len(el)
        part = 100.0 * sum(1 for r in el if r["mid"] <= r["ratio"] < r["hi"]) / len(el)
        hi_med = statistics.median(r["hi"] for r in el)
        ratio = sh / REF_FIRE[s]
        ok = 0.5 <= ratio <= 2.0
        if not ok:
            stop2.append(s)
        clamp_max = 100.0 * sum(1 for r in el if abs(r["hi"] - P["hi_max"] * (P["hmult"][s] if P["sv_enabled"] else 1)) < 1e-12) / len(el)
        by_year = Counter(); n_year = Counter()
        for r in el:
            y = (EPOCH + timedelta(days=r["day"])).year
            n_year[y] += 1
            by_year[y] += r["ratio"] >= r["hi"]
        out("%-7s n %8d  full-fire %.2f%% (ref %.2f%%, ratio %.2f -> %s)  partial %.2f%%  high-thr p50 %.2f  at max clamp %.1f%%  [USD-volume sensitivity: %.2f%%]"
            % (s, len(el), sh, REF_FIRE[s], ratio, "PASS" if ok else "STOP", part, hi_med, clamp_max, shu))
        out("        by year: %s" % "  ".join("%d %.2f%%" % (y, 100.0 * by_year[y] / n_year[y]) for y in sorted(n_year)))

    out("")
    out("=== 5. Drops per session-year, share of evaluated weekday bars (STOP 4: gap > 2%) ===")
    stop4 = []
    dcount = defaultdict(Counter)
    for r in rows:
        y = (EPOCH + timedelta(days=r["day"])).year
        dcount[(r["sess"], y)]["n"] += 1
        dcount[(r["sess"], y)][r["drop"] or "kept"] += 1
    for s in SESSIONS:
        for y in sorted({k[1] for k in dcount if k[0] == s}):
            c = dcount[(s, y)]
            gsh = 100.0 * c["gap"] / c["n"]
            if gsh > 2.0:
                stop4.append("%s %d" % (s, y))
            out("%-7s %d  bars %7d  kept %7d  gap %6d (%.2f%%)  warm-up %6d  window-past-cut %d"
                % (s, y, c["n"], c["kept"], c["gap"], gsh, c["warmup"], c["window_past_cut"]))
    seam_bars = sum(1 for r in rows if r["drop"] is None and r.get("seam"))
    out("eligible signal bars whose lookback crosses the 2025-01-01 store seam: %d (kept: the seam is TradeSeq-contiguous)" % seam_bars)

    cnt, eps = episodes(rows, F, h1)
    out("")
    out("=== 6. Counts per rule x session x half (signals = full fires on eligible bars; episodes = de-clustered by W) ===")
    out("%-6s %-7s %-3s %9s %9s %8s %8s %8s %8s %7s %8s %8s %9s"
        % ("rule", "session", "hlf", "signals", "episodes", "ep_L", "ep_S", "drop_gap", "drop_wu", "drop_cut", "part_L", "part_S", "ep_seam"))
    for rule in RULES:
        for s in SESSIONS:
            for hf in ("H1", "H2"):
                k = (rule, s, hf)
                pr = "V0p" if rule == "V0" else ("R0p" if rule == "R0" else None)
                out("%-6s %-7s %-3s %9d %9d %8d %8d %8d %8d %7d %8s %8s %9d"
                    % (rule, s, hf, cnt[k + ("signals",)], cnt[k + ("episodes",)], cnt[k + ("episodes_L",)], cnt[k + ("episodes_S",)],
                       cnt[k + ("drop_gap",)], cnt[k + ("drop_warmup",)], cnt[k + ("drop_window_past_cut",)],
                       cnt[(pr, s, hf, "partial_L")] if pr else "-", cnt[(pr, s, hf, "partial_S")] if pr else "-",
                       cnt[k + ("episodes_seam",)]))

    pre = Counter()
    for (rule, s, hf, kind), v in cnt.items():
        if hf == "pre":
            pre[(rule, kind)] += v
    out("before H1 (warm-up days, in neither half): %s"
        % "  ".join("%s %s %d" % (ru, k, v) for (ru, k), v in sorted(pre.items())))
    out("")
    out("=== 7. Power (outcome-blind; sigma = published proxy x ATR scale; ATR in bps of close) ===")
    out("Holm family m = %d; the first Holm step tests at alpha %.5f; MDE80 shown at that worst case and at 0.05" % (FAMILY_M, 0.05 / FAMILY_M))
    atr_ref = {}
    for s in SESSIONS:
        b = b1 if RES_OF[s] == 1 else b3
        el = [r for r in by_sess[s] if r["drop"] is None]
        step = max(1, len(el) // 20000)     # the reference median from an even sample of eligible bars (outcome-blind)
        vals = [atr_at(b, r["i"], P["atr_period"]) / r["close"] * 1e4 for r in el[::step]]
        atr_ref[s] = statistics.median(vals)
    ep_atr = defaultdict(list)
    for rule, side, r in eps:
        b = b1 if RES_OF[r["sess"]] == 1 else b3
        ep_atr[(rule, r["sess"])].append(r)
    med_ep = {}
    for k, rs in ep_atr.items():
        b = b1 if RES_OF[k[1]] == 1 else b3
        step = max(1, len(rs) // 20000)
        med_ep[k] = statistics.median(atr_at(b, r["i"], P["atr_period"]) / r["close"] * 1e4 for r in rs[::step])
    for s in SESSIONS:
        out("%-7s median ATR(7) all eligible bars %.2f bps; at episodes: %s"
            % (s, atr_ref[s], "  ".join("%s %s" % (ru, fmt(med_ep.get((ru, s)), 2)) for ru in RULES)))
    out("%-7s %-7s %-6s %-6s %8s %8s %8s %8s  %-6s %10s %10s %10s  %s"
        % ("hyp", "session", "A", "B", "nA", "nB", "nA_H1/H2", "nB_H1/H2", "sigma", "MDE80@a/9", "MDE80@.05", "pow@10bps", "readable"))
    stop3 = defaultdict(list)
    for hid, A, B in HYPS:
        for s in SESSIONS:
            nA = sum(cnt[(A, s, hf, "episodes")] for hf in ("H1", "H2"))
            nB = sum(cnt[(B, s, hf, "episodes")] for hf in ("H1", "H2"))
            hA = "%d/%d" % (cnt[(A, s, "H1", "episodes")], cnt[(A, s, "H2", "episodes")])
            hB = "%d/%d" % (cnt[(B, s, "H1", "episodes")], cnt[(B, s, "H2", "episodes")])
            rd = min(cnt[(A, s, hf, "episodes")] for hf in ("H1", "H2")) >= MIN_N and min(cnt[(B, s, hf, "episodes")] for hf in ("H1", "H2")) >= MIN_N
            stop3[hid].append(rd)
            ea = med_ep.get((A, s)); eb = med_ep.get((B, s))
            pool = [x for x in (ea, eb) if x is not None]
            scale = (statistics.mean(pool) / atr_ref[s]) if pool else float("nan")
            for name, sg in (("cons", SIGMA_CONS), ("opt", SIGMA_OPT)):
                se = sg * scale
                out("%-7s %-7s %-6s %-6s %8d %8d %9s %9s  %-4s%5s %10s %10s %10s  %s"
                    % (hid, s, A, B, nA, nB, hA, hB, name, fmt(se), fmt(mde(0.05 / FAMILY_M, se, nA, nB)), fmt(mde(0.05, se, nA, nB)),
                       fmt(power_at(0.05 / FAMILY_M, se, 10.0, nA, nB), 2), "yes" if rd else "NO (n<100 in a half)"))
    out("")
    out("=== 8. STOP gates (spec section 7) ===")
    out("STOP 1 (E-1 parity): run --parity; see its own verdict")
    out("STOP 2 (V0 fire share within [0.5x, 2x] of the reference): %s" % ("PASS" if not stop2 else "STOP " + ", ".join(stop2)))
    s3 = [h for h, v in stop3.items() if not any(v)]
    out("STOP 3 (a hypothesis NOT READABLE in every session): %s" % ("none" if not s3 else ", ".join(s3)))
    out("STOP 4 (gap drops > 2%% of weekday bars in a session-year): %s" % ("none" if not stop4 else ", ".join(stop4)))
    return cnt, eps, {"stop2": stop2, "stop3": s3, "stop4": stop4}


# ---------------------------------------------------------------- E-1 parity
def parity(fetch, stores, P, out=print, cutoff_shift_ms=0):
    """Rebuild the engine's candle list as of each analysis_log.csv row's Timestamp: the LOOKBACK - 1 completed bars
    plus the forming stub from the trades before the cutoff, at the row's ExecResolution. Port RSI, ROC, VolumeRatio
    and VWAP; compare with the logged values."""
    import csv
    logp = os.path.join(fetch, "analysis_log.csv")
    with open(logp, encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    t_first = datetime.strptime(rows[0]["Timestamp"], "%Y-%m-%d %H:%M:%S")
    t_last = datetime.strptime(rows[-1]["Timestamp"], "%Y-%m-%d %H:%M:%S")
    lo_ms = ms(t_first - timedelta(minutes=3 * LOOKBACK + 10))
    hi_ms = ms(t_last + timedelta(minutes=5))
    # trades: the dev history store (TradeSeq-complete) before its end, the box store in the fetch after it
    hist_end = None
    T = array("q"); Pp = array("d"); A = array("d")

    def load(path, lo, hi):
        nonlocal hist_end
        last = None
        with open(path, "rb") as f:
            f.readline()
            for line in f:
                a = line.split(b",", 3)
                t = int(a[0])
                if t < lo or t >= hi:
                    continue
                T.append(t); Pp.append(float(a[1])); A.append(float(a[2]))
                last = t
        return last

    hist = stores[-1]
    months = sorted({(d.year, d.month) for d in (utc_ms_dt(lo_ms), utc_ms_dt(hi_ms))})
    ym = []
    y, m = months[0]
    while (y, m) <= months[-1]:
        ym.append("%04d-%02d" % (y, m))
        y, m = (y + (m == 12), m % 12 + 1)
    for s in ym:
        p = os.path.join(hist, "trades_%s.csv" % s)
        if os.path.exists(p):
            last = load(p, lo_ms, hi_ms)
            if last is not None:
                hist_end = last
    box_from = (hist_end // 60000 + 1) * 60000 if hist_end else lo_ms
    nbox = 0
    for s in ym:
        p = os.path.join(fetch, "backtest_data", "trades_%s.csv" % s)
        if os.path.exists(p):
            n0 = len(T)
            load(p, box_from, hi_ms)
            nbox += len(T) - n0
    order = sorted(range(len(T)), key=lambda k: T[k])
    T = array("q", (T[k] for k in order)); Pp = array("d", (Pp[k] for k in order)); A = array("d", (A[k] for k in order))
    out("E-1 trades: %d (history store to %s, box store from %s: %d trades)"
        % (len(T), utc_ms_dt(hist_end) if hist_end else None, utc_ms_dt(box_from), nbox))
    # completed 1-minute bars from those trades
    m_lo = lo_ms // 60000
    M = hi_ms // 60000 - m_lo + 1
    o = [0.0] * M; h = [0.0] * M; l = [0.0] * M; c = [0.0] * M; v = [0.0] * M; n = [0] * M
    for t, p, am in zip(T, Pp, A):
        k = t // 60000 - m_lo
        if n[k] == 0:
            o[k] = h[k] = l[k] = p
        else:
            h[k] = max(h[k], p); l[k] = min(l[k], p)
        c[k] = p; v[k] += am / p; n[k] += 1
    prev = next(c[k] for k in range(M) if n[k])
    for k in range(M):
        if n[k] == 0:
            o[k] = h[k] = l[k] = c[k] = prev
        prev = c[k]
    res_stats = defaultdict(lambda: Counter())
    diffs = defaultdict(list)
    for row in rows:
        ts = ms(datetime.strptime(row["Timestamp"], "%Y-%m-%d %H:%M:%S")) + cutoff_shift_ms
        res = int(row["ExecResolution"])
        now_min = ts // 60000
        open_min = (now_min - (now_min % res))       # the forming bar's open (UTC grid; START is 00:00-aligned)
        bars = []
        for j in range(LOOKBACK - 1, 0, -1):         # completed bars, oldest first
            b0 = open_min - j * res - m_lo
            if b0 < 0:
                break
            ks = range(b0, b0 + res)
            nz = [k for k in ks if n[k]]
            if nz:
                bars.append((o[nz[0]], max(h[k] for k in nz), min(l[k] for k in nz), c[nz[-1]], sum(v[k] for k in ks), open_min - j * res))
            else:
                bars.append((c[b0], c[b0], c[b0], c[b0], 0.0, open_min - j * res))
        # forming stub: trades in [open, cutoff)
        a0 = bisect_left(T, open_min * 60000)
        a1 = bisect_left(T, ts)
        if a1 > a0:
            ps = Pp[a0:a1]
            bars.append((ps[0], max(ps), min(ps), ps[-1], sum(A[k] / Pp[k] for k in range(a0, a1)), open_min))
            res_stats[res]["stub_with_trades"] += 1
        else:
            pc = bars[-1][3]
            bars.append((pc, pc, pc, pc, 0.0, open_min))
            res_stats[res]["stub_empty"] += 1
        cl = [x[3] for x in bars]
        rsi = rsi_list(cl, P["rsi_period"])
        rp = P["roc_period"]
        roc = (cl[-1] - cl[-1 - rp]) / cl[-1 - rp] * 100.0
        sma = sum(x[4] for x in bars[-P["sma_period"]:]) / P["sma_period"]
        vr = bars[-1][4] / sma if sma > 0 else 1.0
        ss = vwap_session_start_min(ts / 60000.0, P)
        sc = [x for x in bars if x[5] >= ss] or bars
        cv = sum(x[4] for x in sc)
        vw = sum((x[1] + x[2] + x[3]) / 3.0 * x[4] for x in sc) / cv if cv > 0 else 0.0
        lr, lroc, lvr, lvw = float(row["RSI"]), float(row["ROC"]), float(row["VolumeRatio"]), float(row["VWAP"])
        d = {"rsi": abs(rsi - lr), "roc": abs(roc - lroc), "vr_rel": abs(vr - lvr) / lvr if lvr > 0 else (0.0 if vr == 0 else float("inf")),
             "vw": abs(vw - lvw), "vwn": len(sc) - int(row["VWAPSessionCandles"])}
        for k, x in d.items():
            diffs[(res, k)].append(x)
        res_stats[res]["rows"] += 1
        # Decomposition (not the spec's E-1 test): anchor the stub CLOSE on the engine's own logged Price
        # (r.CurrentPrice = candlesExec.Last().Close, UI/MainForm_Analysis.vb:239). RSI and ROC then depend only on the
        # completed bars, so this isolates the closed-bar port from the stub's timing.
        lp = float(row["Price"])
        cla = cl[:-1] + [lp]
        rsi_a = rsi_list(cla, P["rsi_period"])
        roc_a = (cla[-1] - cla[-1 - rp]) / cla[-1 - rp] * 100.0
        wk = datetime.strptime(row["Timestamp"], "%Y-%m-%d %H:%M:%S").weekday() < 5
        for tag, use in (("all", True), ("wkday", wk)):
            if not use:
                continue
            res_stats[res]["a_rows_" + tag] += 1
            res_stats[res]["a_rsi_ok_" + tag] += abs(rsi_a - lr) <= 0.5
            res_stats[res]["a_roc_ok_" + tag] += abs(roc_a - lroc) <= 0.01
        if abs(rsi_a - lr) > 0.5:
            res_stats[res]["a_miss_day_" + row["Timestamp"][:10]] += 1
        # Volume: the engine's stub volume is not logged. Implied stub volume from the logged ratio and OUR 8 completed
        # bars: VR = vs / ((S8 + vs) / 9)  =>  vs = VR * S8 / (9 - VR). The cutoff at which the trade tape's stub reaches
        # vs is the implied fetch instant; its lag behind the logged second is printed. A wrong completed-bar volume
        # shows as an implausible lag (negative, or far beyond the run's few seconds).
        S8 = sum(x[4] for x in bars[-P["sma_period"]:-1])
        if lvr < 9.0 and S8 > 0:
            vs_e = lvr * S8 / (9.0 - lvr)
            cum = 0.0
            t_star = None
            for k in range(a0, bisect_left(T, (open_min + res) * 60000 + 60000)):
                if cum >= vs_e - 1e-9:
                    t_star = T[k - 1] if k > a0 else open_min * 60000
                    break
                cum += A[k] / Pp[k]
            if t_star is not None:
                diffs[(res, "vol_lag_s")].append((ts - cutoff_shift_ms - t_star) / 1000.0)
            else:
                res_stats[res]["vol_lag_unreached"] += 1
        ok = d["rsi"] <= 0.5 and d["roc"] <= 0.01 and d["vr_rel"] <= 0.02 and d["vw"] <= 0.5
        res_stats[res]["all4"] += ok
        res_stats[res]["rsi_ok"] += d["rsi"] <= 0.5
        res_stats[res]["roc_ok"] += d["roc"] <= 0.01
        res_stats[res]["vr_ok"] += d["vr_rel"] <= 0.02
        res_stats[res]["vw_ok"] += d["vw"] <= 0.5
        res_stats[res]["vwn_eq"] += d["vwn"] == 0
        diffs[(res, "vr_signed")].append((vr - lvr) / lvr if lvr > 0 else 0.0)
    out("E-1 rows: %d (%s .. %s), cutoff = logged second %+d ms" % (len(rows), t_first, t_last, cutoff_shift_ms))
    verdict = True
    for res in sorted(res_stats):
        c = res_stats[res]
        nn = c["rows"]
        q = lambda k: 100.0 * c[k] / nn
        p99 = lambda k: sorted(diffs[(res, k)])[int(0.99 * (nn - 1))]
        p50 = lambda k: sorted(diffs[(res, k)])[int(0.50 * (nn - 1))]
        out("  res %d min: rows %d  stub with trades %d / empty %d" % (res, nn, c["stub_with_trades"], c["stub_empty"]))
        out("    within tolerance: RSI<=0.5 %.2f%%  ROC<=0.01 %.2f%%  VolumeRatio<=2%% %.2f%%  VWAP<=0.5 USD %.2f%%  all four %.2f%%  (VWAP candle count equal %.2f%%)"
            % (q("rsi_ok"), q("roc_ok"), q("vr_ok"), q("vw_ok"), q("all4"), q("vwn_eq")))
        out("    p50 / p99 |diff|: RSI %.4f / %.4f  ROC %.5f / %.5f  VolumeRatio rel %.4f / %.4f  VWAP %.3f / %.3f USD;  VolumeRatio signed rel median %+.4f"
            % (p50("rsi"), p99("rsi"), p50("roc"), p99("roc"), p50("vr_rel"), p99("vr_rel"), p50("vw"), p99("vw"),
               statistics.median(diffs[(res, "vr_signed")])))
        for tag in ("all", "wkday"):
            na = c["a_rows_" + tag]
            if na:
                out("    decomposition, stub close = logged Price (%s rows %d): RSI<=0.5 %.2f%%  ROC<=0.01 %.2f%%"
                    % (tag, na, 100.0 * c["a_rsi_ok_" + tag] / na, 100.0 * c["a_roc_ok_" + tag] / na))
        miss = sorted((k[len("a_miss_day_"):], v) for k, v in c.items() if k.startswith("a_miss_day_"))
        out("    decomposition RSI misses by UTC day: %s" % ("  ".join("%s %d" % x for x in miss) or "none"))
        lag = sorted(diffs[(res, "vol_lag_s")])
        if lag:
            q = lambda p: lag[int(p * (len(lag) - 1))]
            out("    implied stub-volume fetch lag behind the logged second (s), n %d: p1 %.1f  p10 %.1f  p50 %.1f  p90 %.1f  p99 %.1f;  "
                "share in [-1, +10] s %.2f%%;  not reached by bar end + 60 s: %d"
                % (len(lag), q(0.01), q(0.10), q(0.50), q(0.90), q(0.99),
                   100.0 * sum(1 for x in lag if -1.0 <= x <= 10.0) / len(lag), c["vol_lag_unreached"]))
        for k in ("rsi_ok", "roc_ok", "vr_ok", "vw_ok"):
            if c[k] < 0.99 * nn:
                verdict = False
    out("E-1 verdict (>= 99%% of rows within each tolerance, per resolution): %s" % ("PASS" if verdict else "FAIL"))
    return verdict


def utc_ms_dt(t):
    return EPOCH + timedelta(milliseconds=t)


# ---------------------------------------------------------------- E-1b: closed bars vs the venue's REST candles
def load_venue(vdir, res):
    """backtest_data/candles_<res>m_YYYY-MM.csv (Timestamp = bar open ms, Open, High, Low, Close, Volume BTC, Cost USD)."""
    out = {}
    for name in sorted(os.listdir(vdir)):
        if name.startswith("candles_%dm_" % res) and name.endswith(".csv"):
            with open(os.path.join(vdir, name), encoding="utf-8") as f:
                f.readline()
                for line in f:
                    a = line.rstrip("\r\n").split(",")
                    out[int(a[0]) // 60000] = (float(a[1]), float(a[2]), float(a[3]), float(a[4]), float(a[5]), float(a[6]))
    return out


def venue_parity(vdir, stores, cut, P, out=print):
    """Evidence the reader can re-run while the venue candle store exists (it is gitignored). Not the spec's E-1: it
    compares CLOSED bars only, the study's own input, so the forming stub's timing cannot enter."""
    groups, paths = store_paths(stores, cut)
    b1, _ = build_1m(paths, cut, out)
    b3 = aggregate(b1, 3)
    verdict = {}
    for res, bt in ((1, b1), (3, b3)):
        ven = load_venue(vdir, res)
        keys = sorted(k for k in ven if START_MIN <= k and (k - START_MIN) % res == 0 and (k - START_MIN) // res < len(bt))
        out("E-1b res %d min: venue candles %d (%s .. %s)" % (res, len(keys), utc_min(keys[0]), utc_min(keys[-1])))
        # bar level
        c = Counter()
        vrel = []
        for k in keys:
            i = (k - START_MIN) // res
            vo, vh, vl, vc, vv, vcost = ven[k]
            if bt.n[i] == 0:
                c["tape_empty"] += 1
                continue
            c["n"] += 1
            c["close_eq"] += abs(bt.c[i] - vc) < 0.01
            c["ohlc_eq"] += abs(bt.o[i] - vo) < 0.01 and abs(bt.h[i] - vh) < 0.01 and abs(bt.l[i] - vl) < 0.01 and abs(bt.c[i] - vc) < 0.01
            if vv > 0:
                vrel.append((bt.vb[i] - vv) / vv)
            c["cost_eq"] += abs(bt.vu[i] - vcost) < 0.5
        vrel.sort()
        q = lambda p: vrel[int(p * (len(vrel) - 1))]
        out("  bars: compared %d (tape empty %d); close equal %.3f%%  OHLC equal %.3f%%  USD volume equal %.3f%%;  "
            "BTC volume (tape - venue)/venue p1 %+.4f p50 %+.5f p99 %+.4f; share within 1%% %.3f%%"
            % (c["n"], c["tape_empty"], 100.0 * c["close_eq"] / c["n"], 100.0 * c["ohlc_eq"] / c["n"], 100.0 * c["cost_eq"] / c["n"],
               q(0.01), q(0.5), q(0.99), 100.0 * sum(1 for x in vrel if abs(x) <= 0.01) / len(vrel)))
        # indicator level: the same evaluation on a copy of the tape bars with the venue candles written over them
        bv = Bars(res, array("d", bt.o), array("d", bt.h), array("d", bt.l), array("d", bt.c), array("d", bt.vb), array("d", bt.vu), array("i", bt.n))
        for k in keys:
            i = (k - START_MIN) // res
            vo, vh, vl, vc, vv, vcost = ven[k]
            bv.o[i], bv.h[i], bv.l[i], bv.c[i], bv.vb[i], bv.vu[i] = vo, vh, vl, vc, vv, vcost
        sess = {"NY"} if res == 1 else {"ASIA", "LONDON"}
        gz = array("i", bytes(4 * (len(b1) + 1)))
        cut_min = ms(cut) // 60000
        ra, _ = evaluate(bt, sess, gz, cut_min, P)
        rb, _ = evaluate(bv, sess, gz, cut_min, P)
        kset = set(keys)
        cc = Counter()
        for x, y in zip(ra, rb):
            if "rsi" not in x:
                continue
            i = x["i"]
            # the whole 250-bar lookback must be venue-covered
            if not all((START_MIN + j * res) in kset for j in (i - LOOKBACK + 1, i)):
                continue
            first = START_MIN + (i - LOOKBACK + 1) * res
            if first < keys[0] or START_MIN + i * res > keys[-1]:
                continue
            cc["n"] += 1
            cc["rsi"] += abs(x["rsi"] - y["rsi"]) <= 0.5
            cc["roc"] += abs(x["roc"] - y["roc"]) <= 0.01
            cc["ratio"] += abs(x["ratio"] - y["ratio"]) <= 0.02 * max(y["ratio"], 1e-12)
            cc["vwap"] += abs(x["vwap"] - y["vwap"]) <= 0.5
            f0x, f0y = x["ratio"] >= x["hi"], y["ratio"] >= y["hi"]
            cc["v0_agree"] += f0x == f0y
            cc["v0_tape"] += f0x
            cc["v0_venue"] += f0y
            cc["dir_agree"] += direction_vol(x) == direction_vol(y)
            r0x = "L" if x["rsi"] > 60 else ("S" if x["rsi"] < 40 else None)
            r0y = "L" if y["rsi"] > 60 else ("S" if y["rsi"] < 40 else None)
            cc["r6040_agree"] += r0x == r0y
        n = cc["n"]
        out("  closed-bar indicators on %d weekday session bars with a fully venue-covered lookback:" % n)
        out("    within tolerance: RSI<=0.5 %.3f%%  ROC<=0.01 %.3f%%  VolumeRatio<=2%% %.3f%%  VWAP<=0.5 USD %.3f%%"
            % tuple(100.0 * cc[k] / n for k in ("rsi", "roc", "ratio", "vwap")))
        out("    vote agreement: V0 full fire %.3f%% (tape fires %d, venue fires %d)  volume direction %.3f%%  RSI 60/40 side %.3f%%"
            % (100.0 * cc["v0_agree"] / n, cc["v0_tape"], cc["v0_venue"], 100.0 * cc["dir_agree"] / n, 100.0 * cc["r6040_agree"] / n))
        verdict[res] = all(cc[k] >= 0.99 * n for k in ("rsi", "roc", "ratio", "vwap"))
    out("E-1b verdict (>= 99%% of closed bars within each E-1 tolerance, per resolution): %s"
        % ("PASS" if all(verdict.values()) else "FAIL %s" % verdict))
    return all(verdict.values())


# ---------------------------------------------------------------- selftest
def selftest(out=print):
    fails = []

    def check(name, got, want, tol=0.0):
        ok = (abs(got - want) <= tol) if isinstance(want, float) else got == want
        out("  %-70s %s  got=%r want=%r" % (name, "PASS" if ok else "FAIL", got, want))
        if not ok:
            fails.append(name)

    P = load_pins()
    out("SELFTEST: ports")
    cl = [100, 101, 102, 101, 103, 104, 103, 105, 106, 104, 107]
    # hand value: diffs 1,1,-1,2,1,-1,2,1,-2,3; seed over the first 9: gain 9/9 = 1.0... computed explicitly below
    g = [max(cl[i] - cl[i - 1], 0) for i in range(1, len(cl))]
    lo = [max(cl[i - 1] - cl[i], 0) for i in range(1, len(cl))]
    ag, al = sum(g[:9]) / 9, sum(lo[:9]) / 9
    ag, al = (ag * 8 + g[9]) / 9, (al * 8 + lo[9]) / 9
    check("CalcRSI port, 11 closes, Wilder seed + 1 step", rsi_list(cl, 9), 100 - 100 / (1 + ag / al), 1e-12)
    check("CalcRSI port, fewer than period+1 closes -> 50", rsi_list(cl[:9], 9), 50.0)
    check("CalcRSI port, no losses -> 100", rsi_list(list(range(20)), 9), 100.0)
    import random
    rnd = random.Random(7)
    series = [50000.0]
    for _ in range(600):
        series.append(series[-1] + rnd.gauss(0, 20))
    run_ = rsi_running(series, 9)
    check("running RSI == 250-bar list RSI at the series end (seed weight)", run_[-1], rsi_list(series[-250:], 9), 1e-9)
    check("running RSI == list RSI on the full series (identical seed)", run_[-1], rsi_list(series, 9), 1e-12)
    hh = [x + 5 for x in series]; ll = [x - 5 for x in series]
    check("CalcATR port: constant 10 range, flat closes -> 10", atr_list([10.0] * 60, [0.0] * 60, [5.0] * 60, 7), 10.0, 1e-12)
    out("SELFTEST: DynamicNorms")
    flat = [10.0] * 100
    check("sd < 0.05 mean -> static high 3.0 x NY 1.15", dyn_thresholds(flat, P, "NY")[0], 3.0 * 1.15, 1e-12)
    w = [1.0] * 50 + [3.0] * 50   # mean 2, sd 1 -> high (2+2)/2 = 2.0, mid 1.5
    check("(mean+2sd)/mean = 2.0 at ASIA x1.00", dyn_thresholds(w, P, "ASIA")[0], 2.0, 1e-12)
    check("(mean+1sd)/mean = 1.5 at ASIA x1.00", dyn_thresholds(w, P, "ASIA")[1], 1.5, 1e-12)
    w2 = [0.0] * 99 + [100.0]     # mean 1, sd ~9.95 -> raw 20.9 -> clamp 6.0
    check("high clamps at 6.0 (LONDON x1.00)", dyn_thresholds(w2, P, "LONDON")[0], 6.0, 1e-12)
    out("SELFTEST: VWAP session start and percentile rank")
    d0 = 20000 * 1440
    check("13:29:59.999 -> session 00:00", vwap_session_start_min(d0 + 13 * 60 + 30 - 1e-6, P), d0)
    check("13:30:00 -> session 13:30", vwap_session_start_min(d0 + 13 * 60 + 30, P), d0 + 810)
    check("percentile rank: 3 below, 1 tie of 5", pct_rank([1, 2, 3, 5, 9], 5), (3 + 0.5) / 5)
    check("percentile rank: above all", pct_rank([1, 2, 3], 4), 1.0)
    out("SELFTEST: levels")
    vals = [float(x) for x in range(1, 101)]
    check("upper_level share 0.10 -> exactly 10 values above", sum(1 for x in vals if x > upper_level(vals, 0.10)), 10)
    check("lower_level share 0.10 -> exactly 10 values below", sum(1 for x in vals if x < lower_level(vals, 0.10)), 10)
    k, s = share_ge_level([0.5] * 90 + [1.0] * 10, 0.08)
    check("discrete share match picks 1.0 (share 0.10, closest to 0.08)", (k, round(s, 6)), (1.0, 0.1))
    out("SELFTEST: aggregation")
    b = Bars(1, array("d", [1, 2, 2, 4, 5, 6]), array("d", [1.5, 2.5, 2, 4.5, 5.5, 6.5]), array("d", [0.5, 1.5, 2, 3.5, 4.5, 5.5]),
             array("d", [1.2, 2.2, 2.2, 4.2, 5.2, 6.2]), array("d", [1, 1, 0, 1, 1, 1]), array("d", [1, 1, 0, 1, 1, 1]), array("i", [1, 1, 0, 1, 1, 1]))
    b3 = aggregate(b, 3)
    check("3-min bar 0: open first, high max, low min, close last non-empty", (b3.o[0], b3.h[0], b3.l[0], b3.c[0], b3.n[0]), (1.0, 2.5, 0.5, 2.2, 2))
    check("3-min bar 1", (b3.o[1], b3.h[1], b3.l[1], b3.c[1], b3.vb[1]), (4.0, 6.5, 3.5, 6.2, 3.0))
    out("SELFTEST: %s" % ("ALL PASS" if not fails else "FAILURES: %s" % fails))
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser(description="RSI and volume rule study, session 1 (outcome-blind)")
    ap.add_argument("--stores", nargs="+", default=DEFAULT_STORES)
    ap.add_argument("--cut", default="2026-10-06", help="exclusive UTC date")
    ap.add_argument("--settings", default="settings.json")
    ap.add_argument("--parity", metavar="FETCH", help="run E-1 against FETCH/analysis_log.csv instead of the counts")
    ap.add_argument("--venue-candles", metavar="DIR", help="E-1b: closed tape bars vs the venue REST candles in DIR")
    ap.add_argument("--workers", type=int, default=12)
    ap.add_argument("--no-freeze-write", action="store_true", help="do not write docs/rsi-volume-rule-study-frozen.json")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        sys.exit(selftest())
    P = load_pins(a.settings)
    stores = [s.replace("\\", "/") for s in a.stores]
    if a.parity:
        print("RSI/volume rule study - E-1 parity (evidence). docs/rsi-volume-rule-study-spec.md section 6")
        print("pins: settings v%d" % P["version"])
        v0 = parity(a.parity, stores, P)
        print("")
        print("sensitivity: cutoff at the end of the logged second")
        parity(a.parity, stores, P, cutoff_shift_ms=1000)
        sys.exit(0 if v0 else 2)
    cut = datetime.strptime(a.cut, "%Y-%m-%d")
    if a.venue_candles:
        print("RSI/volume rule study - E-1b closed-bar parity vs venue REST candles (evidence)")
        sys.exit(0 if venue_parity(a.venue_candles, stores, cut, P) else 2)
    print("RSI/volume rule study - session 1 counts (OUTCOME-BLIND). docs/rsi-volume-rule-study-spec.md")
    print("pins: settings v%d; RSI(%d) 60/40 (55/45); ROC(%d); volume SMA(%d); clamps high [%g, %g] mid [%g, %g]; static %g/%g; "
          "multipliers %s; VWAP anchor 00:00 and %02d:%02d; ATR(%d) over %d bars; W %s; span %s .. %s exclusive"
          % (P["version"], P["rsi_period"], P["roc_period"], P["sma_period"], P["hi_min"], P["hi_max"], P["mid_min"], P["mid_max"],
             P["static_high"], P["static_mid"], P["hmult"], P["vwap_h"], P["vwap_m"], P["atr_period"], ATR_BARS, HOLD_MIN, START.date(), cut.date()))
    b1, b3, gpre, rows, by_sess, D, h1, F, bstats = run(stores, cut, P, workers=a.workers)
    report(b1, b3, gpre, rows, by_sess, D, h1, F, P)
    if not a.no_freeze_write:
        with open(FROZEN_PATH, "w", encoding="utf-8", newline="\n") as f:
            json.dump(F, f, indent=2, sort_keys=True)
            f.write("\n")
        print("frozen thresholds written to %s" % FROZEN_PATH)


if __name__ == "__main__":
    main()
