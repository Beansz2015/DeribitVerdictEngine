#!/usr/bin/env python3
# tools/ops/a4_liq_ofi_counts.py
#
# A4 (liquidation x OFI flip) logged-era study, SESSION 1: the outcome-blind counts tool.
# Spec of record: docs/a4-liq-ofi-logged-era-study-spec.md. Python 3 standard library only (no numpy).
#
# What it does: builds the pre-registered events (large liquidation clusters, from the dev history store) and joins
# each to the logged run grid (the analysis_log.csv books), then prints COUNTS ONLY:
#   * events per session x period (whole store, by half-year; logged era, by month and by half),
#   * join success and drops per named reason,
#   * FLIP / BALANCED / WITH at the first logged run after the event onset (the signal side, not an outcome),
#   * the control pool for the OFI-only comparison,
#   * a power estimate (MDE, and power at stated effect sizes) for the pre-registered tests.
#
# ⛔ OUTCOME-BLIND BY CONSTRUCTION:
#   * From the store it parses ONLY Timestamp, Amount, Direction and Liquidation. The Price, MarkPrice and IndexPrice
#     columns are never parsed, so no price before or after any event is read.
#   * From the logged books it reads ONLY Timestamp, OFIRatio, ATR, AggrVelBurstRatio, WsHealth, InstanceId and
#     SignalId. It never reads Price, the verdict, the placed levels or any outcome column, and never opens
#     analysis_eval_cache.csv or a candle.
#   * ATR is read for the power scale, and only from R0 (the last run BEFORE the event onset) and from all runs.
#
# Pinned design (docs/a4-liq-ofi-logged-era-study-spec.md section 3; change here = change the spec first):
#   * Booking: the D-5 ruling (T -> taker's side, M -> maker's side, MT -> both). Unrecognised flag or direction:
#     skipped and counted (EF-3).
#   * Window: the last 500 trades (the engine's rtc=500), evaluated after every trade while it holds a liquidation.
#   * Signal: CalcLiquidations' rule (Core/Indicators_OrderFlow.vb): LONG if L > 0 and L >= 2.0 S; SHORT if S > 0 and
#     S > 2.0 L. "Large" = dominant sum > the LLS-1 per-session threshold of the trade's UTC hour (strict >, the
#     penalty site Core/ScoringEngine_Calculate_Scoring.vb:400-405).
#   * Event onset T0: a trade at which the window fires large, with no firing in the 30 min before it (any side).
#     Firing inside that 30 min continues the current episode (de-clustering). Past-only: no look-ahead.
#   * Join (ruling HSR-11): R1 = the first logged run whose Timestamp second is strictly after T0's second, within W
#     (NY 90 s, ASIA and LONDON 210 s, by the event's session). R0 = the last run strictly before T0's second.
#   * Flip at R1, re-thresholded logged OFIRatio (strict, as ClassifyOfiRatio): LONG LIQS event (cascade down, fade
#     long): FLIP if OFIRatio > 1.60, WITH if < 0.625, else BALANCED. SHORT LIQS: FLIP if < 0.625, WITH if > 1.60.
#
# Usage (from the repo root):
#   python tools/ops/a4_liq_ofi_counts.py                 # the counts at the pinned inputs
#   python tools/ops/a4_liq_ofi_counts.py --selftest      # synthetic data, exact expected counts; exit 1 on a failure
#
# Runtime about 3 to 4 minutes (the whole store is scanned once).

import argparse, bisect, csv, glob, hashlib, math, os, statistics, sys, tempfile, time
from collections import Counter, defaultdict, deque
from datetime import date, datetime, timedelta, timezone

# ---------------------------------------------------------------- pinned constants (spec section 3)
LLS1 = {"ASIA": 69535, "LONDON": 83250, "NY": 49724}   # USD; docs/large-liq-size-rederivation-2026-10-02.md, D-5 row
WIN = 500                                               # trades; the engine's rtc=500
DOM = 2.0                                               # indicators.Liquidations.dominance_ratio (v69)
GAP_S = 30 * 60                                         # de-cluster gap, seconds
JOIN_W = {"NY": 90, "ASIA": 210, "LONDON": 210}         # seconds from T0 to R1 (and from R0 to T0)
BUY_DOM, SELL_DOM = 1.60, 0.625                         # indicators.OFI dominance pair since v48 (re-thresholded by value)
SESS = [("ASIA", 0, 7), ("LONDON", 8, 12), ("NY", 13, 23)]   # settings.json session_volume; end inclusive
                                                        # (Core/ExecutionResolution.vb:44, verified)
DATA_CUT = datetime(2026, 10, 1)                        # exclusive; the store ends 2026-09-30
V51_EDGE = datetime(2026, 7, 6, 13, 8, 51)              # placed-geometry edge (tools/ops/SwingFallbackRead SwingFallbackRead.vb:58)
NAMED_GAPS = [(date(2026, 8, 8), date(2026, 8, 10), "intentional stop 08-08..08-10"),
              (date(2026, 8, 15), date(2026, 8, 17), "gap 08-15..08-17"),
              (date(2026, 9, 18), date(2026, 9, 21), "venue outage 09-18..09-21")]
# Power inputs (spec section 6): sigma of net EV per trade, bps, published proxies from docs/burst-outcome-read-spec.md
# section 3 (largest session value of each kind). Not a measurement for this population.
SIGMA_CONS, SIGMA_OPT = 34.3, 14.7
EFFECTS = (5.0, 10.0, 20.0, 40.0)                      # bps, stated effect sizes for the power table
MIN_N = 100                                             # the census readability rule
ALPHA_PRIMARY, ALPHA_SECONDARY = 0.05, 0.025            # A4L-H1 alone; A4L-H2/H3 Holm worst case (m = 2)

DEFAULT_STORE = "C:/DeribitData/history"
DEFAULT_POOLED = "AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv"
DEFAULT_FETCH = "aws_fetch/20261002-121123"
HEALTH_OK = ("", "OK")


def session_of(hour):
    for n, a, b in SESS:
        if a <= hour <= b:
            return n
    return None


def utc(ms):
    return datetime(1970, 1, 1) + timedelta(milliseconds=ms)


def half_year(d):
    return "%dH%d" % (d.year, 1 if d.month <= 6 else 2)


def book(direction, flag, amt):
    """D-5 booking. Returns (long_liq_usd, short_liq_usd), or None when unrecognised (EF-3: skip and count)."""
    if flag == "none":
        return (0, 0)
    if direction not in ("buy", "sell"):
        return None
    if flag == "T":                       # the taker was liquidated: a taker sell is a forced long exit
        return (amt, 0) if direction == "sell" else (0, amt)
    if flag == "M":                       # the maker was liquidated: the maker took the other side
        return (amt, 0) if direction == "buy" else (0, amt)
    if flag == "MT":                      # both sides, the full amount each (liquidation-park-spec.md section 7.1)
        return (amt, amt)
    return None


def dominant(L, S):
    if L > 0 and L >= S * DOM:
        return "LONG", L
    if S > 0 and S > L * DOM:
        return "SHORT", S
    return None, 0


# ---------------------------------------------------------------- store scan: events and firing spans
class Event:
    __slots__ = ("t0", "side", "sess", "L", "S", "two_sided", "n_fire_trades", "last_fire")

    def __init__(self, t0, side, sess, L, S):
        self.t0, self.side, self.sess, self.L, self.S = t0, side, sess, L, S
        self.two_sided = False
        self.n_fire_trades = 1
        self.last_fire = t0


def scan_store(paths, cnt):
    """One pass over the store. Parses only Timestamp, Amount, Direction, Liquidation."""
    q = deque()
    L = S = 0
    events, spans = [], []
    last_fire = None
    in_span = False
    span_start = span_end = 0
    cur = None
    prev_ts = None
    for path in paths:
        with open(path, encoding="utf-8") as f:
            hdr = f.readline().rstrip("\r\n").split(",")
            if hdr[0] != "Timestamp" or hdr[2] != "Amount" or hdr[3] != "Direction" or hdr[4] != "Liquidation":
                raise SystemExit("STOP: unexpected store header in %s: %s" % (path, hdr[:5]))
            for line in f:
                p = line.split(",", 5)            # columns 0..4 only; column 1 (Price) is never converted
                ts = int(p[0])
                if prev_ts is not None and ts < prev_ts:
                    cnt["store_out_of_order"] += 1
                prev_ts = ts
                fl = p[4]
                cnt["trades"] += 1
                if fl == "none":
                    c = None
                else:
                    cnt["flag_" + fl] += 1
                    c = book(p[3], fl, int(round(float(p[2]))))
                    if c is None:
                        cnt["unrecognised_skipped"] += 1
                q.append(c)
                if c is not None:
                    L += c[0]; S += c[1]
                if len(q) > WIN:
                    o = q.popleft()
                    if o is not None:
                        L -= o[0]; S -= o[1]
                if L == 0 and S == 0 and not in_span:
                    continue
                side, size = dominant(L, S)
                sess = session_of(utc(ts).hour)
                fires = side is not None and size > LLS1[sess]
                if fires:
                    if last_fire is None or (ts - last_fire) > GAP_S * 1000:
                        cur = Event(ts, side, sess, L, S)
                        events.append(cur)
                    else:
                        cur.n_fire_trades += 1
                        cur.last_fire = ts
                        if side != cur.side:
                            cur.two_sided = True
                    last_fire = ts
                    if not in_span:
                        in_span, span_start = True, ts
                    span_end = ts
                elif in_span:
                    spans.append((span_start, span_end))
                    in_span = False
    if in_span:
        spans.append((span_start, span_end))
    return events, spans


# ---------------------------------------------------------------- logged run grid
NEED = ("Timestamp", "OFIRatio", "ATR", "InstanceId", "SignalId")


def read_book(path, cnt, tag):
    rows = []
    with open(path, encoding="utf-8", newline="") as f:
        rd = csv.reader(f)
        h = next(rd)
        miss = [c for c in NEED if c not in h]
        if miss:
            raise SystemExit("STOP: %s lacks columns %s" % (path, miss))
        for r in rd:
            if len(r) == len(h):
                hh = h
            elif len(r) < len(h):
                hh = h[:len(r)]               # the pooled book carries 111-col v0.7 rows under a 116-col header;
                cnt["%s_short_row_w%d" % (tag, len(r))] += 1   # v0.7 header == pooled header[:111] (verified)
                if any(c not in hh for c in NEED):
                    cnt["%s_unmappable" % tag] += 1
                    continue
            else:
                cnt["%s_long_row" % tag] += 1
                continue
            d = dict(zip(hh, r))
            try:
                t = datetime.strptime(d["Timestamp"].strip(), "%Y-%m-%d %H:%M:%S")
            except ValueError:
                cnt["%s_bad_ts" % tag] += 1
                continue
            rows.append((t, d.get("InstanceId", ""), d.get("SignalId", ""), d, tag))
    return rows


def load_grid(pooled_path, fetch, cnt):
    pooled = read_book(pooled_path, cnt, "pooled")
    v07 = os.path.join(fetch, "analysis_log.csv.v0.7.bak")
    rotated = sorted(glob.glob(os.path.join(fetch, "analysis_log.csv.*col-*.bak")))
    live = os.path.join(fetch, "analysis_log.csv")
    box = read_book(v07, cnt, "v07")
    for rp in rotated:
        box += read_book(rp, cnt, "rotated")
    box += read_book(live, cnt, "live")
    box_ids = set(x[1] for x in box)
    collector_start = min(x[0] for x in box)
    seen_keys, by_ts = set(), {}
    # Order of record (house rule, SwingFallbackRead): pooled, then rotated, then live; first wins.
    for x in pooled + [y for y in box if y[4] != "v07"] + [y for y in box if y[4] == "v07"]:
        t, inst, sid, d, tag = x
        key = (inst, sid)
        if inst and sid:
            if key in seen_keys:
                cnt["dup_key_dropped"] += 1
                continue
            seen_keys.add(key)
        if t >= DATA_CUT:
            cnt["after_data_cut"] += 1
            continue
        if t >= collector_start and inst not in box_ids:
            cnt["concurrent_instance_dropped"] += 1
            continue
        if t in by_ts:
            cnt["same_ts_dropped"] += 1
            continue
        by_ts[t] = d
    grid = []
    for t in sorted(by_ts):
        d = by_ts[t]
        try:
            ofi = float(d["OFIRatio"])
        except ValueError:
            ofi = None
        try:
            atr = float(d["ATR"])
        except ValueError:
            atr = None
        health = (d.get("AggrVelBurstRatio", "x") != "") and d.get("WsHealth", "").strip() in HEALTH_OK
        grid.append((t, ofi, atr, health))
    return grid, collector_start


# ---------------------------------------------------------------- join and classification
def classify(side, ofi):
    if side == "LONG":       # long liquidations: forced selling, cascade down, the fade is long
        return "FLIP" if ofi > BUY_DOM else ("WITH" if ofi < SELL_DOM else "BALANCED")
    return "FLIP" if ofi < SELL_DOM else ("WITH" if ofi > BUY_DOM else "BALANCED")


def in_named_gap(d):
    for a, b, name in NAMED_GAPS:
        if a <= d <= b:
            return name
    return None


def firing_at(spans, starts, t_ms):
    i = bisect.bisect_right(starts, t_ms) - 1
    return i >= 0 and spans[i][0] <= t_ms <= spans[i][1]


def last_fire_before(spans, starts, t_ms):
    """End of the latest firing span that started at or before t_ms (clipped to t_ms), or None."""
    i = bisect.bisect_right(starts, t_ms) - 1
    if i < 0:
        return None
    return min(spans[i][1], t_ms)


def join(events, grid, collector_start, spans):
    gts = [g[0] for g in grid]
    starts = [s[0] for s in spans]
    first, last = gts[0], gts[-1]
    out = []
    for e in events:
        t0 = utc(e.t0)
        t0s = t0.replace(microsecond=0)
        rec = {"e": e, "t0": t0, "status": None, "r1": None, "r0": None, "r2": None, "cls": None}
        if t0s < first or t0s >= DATA_CUT or t0s > last:
            rec["status"] = "outside_logged_era"
            out.append(rec); continue
        i = bisect.bisect_right(gts, t0s)           # first run with Timestamp second > T0 second
        j = bisect.bisect_left(gts, t0s) - 1        # last run with Timestamp second < T0 second
        w = JOIN_W[e.sess]
        if j >= 0 and (t0s - gts[j]).total_seconds() <= w:
            rec["r0"] = grid[j]
        if i >= len(gts) or (gts[i] - t0s).total_seconds() > w:
            if in_named_gap(t0.date()):
                rec["status"] = "no_run_in_W_named_gap"
            elif t0 < collector_start:
                rec["status"] = "no_run_in_W_dev_era"
            else:
                rec["status"] = "no_run_in_W_other"
            out.append(rec); continue
        r1 = grid[i]
        rec["r1"] = r1
        if i + 1 < len(gts) and (gts[i + 1] - t0s).total_seconds() <= 2 * w:
            rec["r2"] = grid[i + 1]
        if r1[1] is None or r1[1] <= 0:
            rec["status"] = "r1_ofi_invalid"
        elif not r1[3]:
            rec["status"] = "r1_health_snapshot_proxy"
        else:
            rec["status"] = "joined"
            rec["cls"] = classify(e.side, r1[1])
            rec["r1_fires"] = firing_at(spans, starts, int((r1[0] - datetime(1970, 1, 1)).total_seconds() * 1000))
        out.append(rec)
    return out


def control_pool(grid, spans, collector_start):
    """OFI-only control (A4L-H3): healthy runs with no large firing in the GAP_S before the run (and none at it).
    Counted by session x the OFI side it would trade: BUY DOMINANT -> long, SELL DOMINANT -> short."""
    starts = [s[0] for s in spans]
    c = Counter()
    for t, ofi, atr, health in grid:
        if not health or ofi is None or ofi <= 0:
            continue
        ms = int((t - datetime(1970, 1, 1)).total_seconds() * 1000)
        lf = last_fire_before(spans, starts, ms)
        if lf is not None and ms - lf <= GAP_S * 1000:
            c[("near_event",)] += 1
            continue
        s = session_of(t.hour)
        if ofi > BUY_DOM:
            c[(s, "LONG-fade analogue (OFI BUY DOMINANT)", t >= V51_EDGE)] += 1
        elif ofi < SELL_DOM:
            c[(s, "SHORT-fade analogue (OFI SELL DOMINANT)", t >= V51_EDGE)] += 1
        else:
            c[(s, "balanced")] += 1
    return c


# ---------------------------------------------------------------- power
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


# ---------------------------------------------------------------- report
def md5(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def report(events, spans, grid, collector_start, joined, ctrl, cnt, out=print):
    out("=== 0. Inputs and pins ===")
    out("LLS-1 thresholds (USD, strict >): " + " ".join("%s=%d" % (k, LLS1[k]) for k in ("ASIA", "LONDON", "NY")))
    out("window=%d trades  dominance=%.1f  de-cluster gap=%d min  join W: NY %ds ASIA %ds LONDON %ds  OFI pair %.2f/%.3f"
        % (WIN, DOM, GAP_S // 60, JOIN_W["NY"], JOIN_W["ASIA"], JOIN_W["LONDON"], BUY_DOM, SELL_DOM))
    out("store: trades=%d  flags T=%d M=%d MT=%d  unrecognised skipped=%d  out-of-order=%d"
        % (cnt["trades"], cnt["flag_T"], cnt["flag_M"], cnt["flag_MT"], cnt["unrecognised_skipped"], cnt["store_out_of_order"]))
    out("grid: runs kept=%d  first=%s  last=%s  collector start=%s" % (len(grid), grid[0][0], grid[-1][0], collector_start))
    out("grid drops: " + "  ".join("%s=%d" % (k, cnt[k]) for k in sorted(cnt) if k.endswith("dropped") or k.startswith(("pooled_", "v07_", "rotated_", "live_")) or k == "after_data_cut"))
    gaps = Counter()
    for a, b in zip(grid, grid[1:]):
        dtsec = (b[0] - a[0]).total_seconds()
        era = "dev era" if a[0] < collector_start else "collector era"
        if dtsec < 50:
            gaps["runs < 50 s apart, " + era] += 1
        elif dtsec > 600:
            gaps["holes > 10 min, " + era] += 1
    out("grid shape: " + "  ".join("%s=%d" % kv for kv in sorted(gaps.items())))
    out("firing spans=%d  events (onsets)=%d" % (len(spans), len(events)))

    out("")
    out("=== 1. Events per session x half-year, whole store (no join; LONG/SHORT = liquidated side) ===")
    t = defaultdict(Counter)
    for e in events:
        hy = half_year(utc(e.t0))
        t[hy][(e.sess, e.side)] += 1
    out("period   " + "".join("%-14s" % s for s in ("ASIA L/S", "LONDON L/S", "NY L/S")) + "all   two-sided")
    ts_ = Counter(half_year(utc(e.t0)) for e in events if e.two_sided)
    for hy in sorted(t):
        c = t[hy]
        out("%-8s " % hy + "".join("%-14s" % ("%d/%d" % (c[(s, "LONG")], c[(s, "SHORT")])) for s in ("ASIA", "LONDON", "NY"))
            + "%-5d %d" % (sum(c.values()), ts_[hy]))
    days = (utc(events[-1].t0).date() - utc(events[0].t0).date()).days + 1 if events else 0
    out("all      %d events over %d days = %.2f per day" % (len(events), days, len(events) / max(days, 1)))

    out("")
    out("=== 2. Join to the logged run grid (events with onset inside the logged span) ===")
    era = [r for r in joined if r["status"] != "outside_logged_era"]
    st = Counter(r["status"] for r in era)
    n_era = len(era)
    out("events inside the logged span: %d   (outside: %d)" % (n_era, len(joined) - n_era))
    for k in ("joined", "no_run_in_W_named_gap", "no_run_in_W_dev_era", "no_run_in_W_other", "r1_ofi_invalid",
              "r1_health_snapshot_proxy"):
        out("  %-26s %4d  (%s %%)" % (k, st[k], fmt(100.0 * st[k] / n_era if n_era else float("nan"))))
    other = [r for r in era if r["status"] == "no_run_in_W_other"]
    for r in other:
        out("    unnamed drop: T0=%s %s %s" % (r["t0"].strftime("%Y-%m-%d %H:%M:%S"), r["e"].sess, r["e"].side))
    for r in era:
        if r["status"] == "no_run_in_W_named_gap":
            out("    named-gap drop: T0=%s %s (%s)" % (r["t0"].strftime("%Y-%m-%d %H:%M:%S"), r["e"].sess, in_named_gap(r["t0"].date())))
    j = [r for r in era if r["status"] == "joined"]
    unnamed = st["no_run_in_W_other"] + st["r1_ofi_invalid"]
    out("drop rate: all reasons %s %%; reasons not named in the spec %s %%"
        % (fmt(100.0 * (n_era - len(j)) / n_era if n_era else float("nan")),
           fmt(100.0 * unnamed / n_era if n_era else float("nan"))))

    out("")
    out("=== 3. Joined events: per session x side x month (onset month) ===")
    months = sorted(set(r["t0"].strftime("%Y-%m") for r in j))
    out("session side   " + " ".join("%-8s" % m for m in months) + " all")
    for s in ("ASIA", "LONDON", "NY"):
        for sd in ("LONG", "SHORT"):
            row = [sum(1 for r in j if r["e"].sess == s and r["e"].side == sd and r["t0"].strftime("%Y-%m") == m) for m in months]
            out("%-7s %-6s " % (s, sd) + " ".join("%-8d" % x for x in row) + " %d" % sum(row))
    jd = sorted(set(r["t0"].date() for r in j))
    if jd:
        h = len(jd) // 2
        h1days = set(jd[:h])
        out("event days=%d  halves (first floor(D/2) days): H1 %d events (%s..%s)  H2 %d events"
            % (len(jd), sum(1 for r in j if r["t0"].date() in h1days), jd[0], jd[h - 1] if h else "-",
               sum(1 for r in j if r["t0"].date() not in h1days)))

    out("")
    out("=== 4. Flip state at R1 (signal side only) ===")
    out("session  side    FLIP  BALANCED  WITH   | R1 fires large  R1 lag p50/p90 s  R1 lag<10s | R0 present  R0 FLIP/BAL/WITH")
    for s in ("ASIA", "LONDON", "NY", "ALL"):
        for sd in ("LONG", "SHORT", "both"):
            rr = [r for r in j if (s == "ALL" or r["e"].sess == s) and (sd == "both" or r["e"].side == sd)]
            if not rr:
                continue
            c = Counter(r["cls"] for r in rr)
            lags = sorted((r["r1"][0] - r["t0"].replace(microsecond=0)).total_seconds() for r in rr)
            r0 = [r for r in rr if r["r0"] is not None and r["r0"][1] is not None and r["r0"][1] > 0]
            c0 = Counter(classify(r["e"].side, r["r0"][1]) for r in r0)
            out("%-8s %-6s %5d %9d %5d   | %14d  %6s/%-8s  %10d | %10d  %d/%d/%d"
                % (s, sd, c["FLIP"], c["BALANCED"], c["WITH"], sum(1 for r in rr if r.get("r1_fires")),
                   fmt(lags[len(lags) // 2], 0), fmt(lags[min(len(lags) - 1, int(0.9 * len(lags)))], 0),
                   sum(1 for x in lags if x < 10), len(r0), c0["FLIP"], c0["BALANCED"], c0["WITH"]))
    pre51 = sum(1 for r in j if r["r1"][0] < V51_EDGE)
    wk = Counter(("weekend" if r["t0"].weekday() >= 5 else "weekday", r["cls"]) for r in j)
    out("R1 before the v51 placed-geometry edge (excluded from the outcome population): %d" % pre51)
    out("weekday FLIP/NO-FLIP: %d/%d   weekend FLIP/NO-FLIP: %d/%d"
        % (wk[("weekday", "FLIP")], wk[("weekday", "BALANCED")] + wk[("weekday", "WITH")],
           wk[("weekend", "FLIP")], wk[("weekend", "BALANCED")] + wk[("weekend", "WITH")]))
    r2 = [r for r in j if r["r2"] is not None and r["r2"][1] is not None and r["r2"][1] > 0]
    c12 = Counter((r["cls"], classify(r["e"].side, r["r2"][1])) for r in r2)
    out("R1 -> R2 state (descriptive): " + "  ".join("%s->%s=%d" % (a, b, c12[(a, b)]) for a in ("FLIP", "BALANCED", "WITH") for b in ("FLIP", "BALANCED", "WITH")))
    two = sum(1 for r in j if r["e"].two_sided)
    out("joined events whose episode also fired on the other side (kept; counted only): %d" % two)

    out("")
    out("=== 5. OFI-only control pool (A4L-H3): healthy runs, no large firing in the 30 min before ===")
    out("runs within 30 min after a firing (excluded from the pool): %d" % ctrl[("near_event",)])
    for s in ("ASIA", "LONDON", "NY"):
        a = ctrl[(s, "LONG-fade analogue (OFI BUY DOMINANT)", True)]
        b = ctrl[(s, "SHORT-fade analogue (OFI SELL DOMINANT)", True)]
        a0 = ctrl[(s, "LONG-fade analogue (OFI BUY DOMINANT)", False)]
        b0 = ctrl[(s, "SHORT-fade analogue (OFI SELL DOMINANT)", False)]
        out("%-7s BUY-DOM (long) %6d   SELL-DOM (short) %6d   balanced %6d   | pre-v51 excluded %d"
            % (s, a, b, ctrl[(s, "balanced")], a0 + b0))

    out("")
    out("=== 6. Power (outcome-blind; sigma is a published proxy, scaled by ATR) ===")
    sess_atr = defaultdict(list)
    for t, ofi, atr, health in grid:
        if atr and atr > 0:
            sess_atr[session_of(t.hour)].append(atr)
    ev_atr = defaultdict(list)
    for r in j:
        if r["r0"] is not None and r["r0"][2] and r["r0"][2] > 0:
            ev_atr[r["e"].sess].append(r["r0"][2])
    scales = {}
    for s in ("ASIA", "LONDON", "NY"):
        ref = statistics.median(sess_atr[s]) if sess_atr[s] else float("nan")
        evm = statistics.median(ev_atr[s]) if ev_atr[s] else float("nan")
        scales[s] = evm / ref if ev_atr[s] and sess_atr[s] else float("nan")
        out("%-7s median ATR (USD): all runs %s  events at R0 %s (n=%d)  scale %s"
            % (s, fmt(ref, 2), fmt(evm, 2), len(ev_atr[s]), fmt(scales[s], 2)))
    allev = [x for s in ev_atr for x in ev_atr[s]]
    allref = [x for s in sess_atr for x in sess_atr[s]]
    sc_all = statistics.median(allev) / statistics.median(allref) if allev and allref else float("nan")
    out("pooled scale %s" % fmt(sc_all, 2))
    nf = sum(1 for r in j if r["cls"] == "FLIP" and r["r1"][0] >= V51_EDGE)
    nn = sum(1 for r in j if r["cls"] in ("BALANCED", "WITH") and r["r1"][0] >= V51_EDGE)
    nc = sum(ctrl[(s, k, True)] for s in ("ASIA", "LONDON", "NY")
             for k in ("LONG-fade analogue (OFI BUY DOMINANT)", "SHORT-fade analogue (OFI SELL DOMINANT)"))
    out("outcome population (R1 at/after the v51 edge): FLIP=%d  NO-FLIP=%d  control=%d" % (nf, nn, nc))
    sig_rows = [("conservative", SIGMA_CONS), ("optimistic", SIGMA_OPT)]
    sc = sc_all if not math.isnan(sc_all) else 1.0
    out("test                              alpha  sigma(bps, xATR)   MDE80 bps  " + "  ".join("pow@%gbps" % d for d in EFFECTS) + "   readable (n>=%d)" % MIN_N)
    for name, sg in sig_rows:
        s_eff = sg * sc
        for tname, alpha, n1, n2, rd in (("A4L-H1 FLIP - NO-FLIP", ALPHA_PRIMARY, nf, nn, nf >= MIN_N and nn >= MIN_N),
                                         ("A4L-H2 FLIP vs 0", ALPHA_SECONDARY, nf, None, nf >= MIN_N),
                                         ("A4L-H3 FLIP - OFI-only control", ALPHA_SECONDARY, nf, nc, nf >= MIN_N and nc >= MIN_N)):
            out("%-33s %5.3f  %-12s %6s   %9s  " % (tname, alpha, name, fmt(s_eff), fmt(mde(alpha, s_eff, n1, n2)))
                + "  ".join("%9s" % fmt(power_at(alpha, s_eff, d, n1, n2), 2) for d in EFFECTS) + "   %s" % ("yes" if rd else "NO"))
    # accrual and projection: collector era only (the dev era did not run 24 h)
    coll = [r for r in j if r["t0"] >= collector_start]
    if coll:
        span_days = (grid[-1][0] - collector_start).total_seconds() / 86400.0
        cg = [g[0] for g in grid if g[0] >= collector_start]
        cov_days = sum(d for d in ((b - a).total_seconds() for a, b in zip(cg, cg[1:])) if d <= 600) / 86400.0
        f_rate = sum(1 for r in coll if r["cls"] == "FLIP") / cov_days
        n_rate = sum(1 for r in coll if r["cls"] != "FLIP") / cov_days
        out("collector-era accrual: %.1f covered days (span %.1f days minus grid holes > 10 min); FLIP %.3f/day  NO-FLIP %.3f/day"
            % (cov_days, span_days, f_rate, n_rate))
        for lab, rate, have in (("FLIP", f_rate, nf), ("NO-FLIP", n_rate, nn)):
            if rate > 0:
                need = max(0, MIN_N - have)
                out("  %s reaches %d at this rate after %.0f more covered days (from %s: %s)"
                    % (lab, MIN_N, need / rate, DATA_CUT.date(), (DATA_CUT + timedelta(days=need / rate)).date()))
            else:
                out("  %s: rate 0, never at this rate" % lab)


# ---------------------------------------------------------------- selftest
def selftest():
    fails = []

    def check(name, got, want):
        ok = got == want
        print("  %-62s %s  got=%r want=%r" % (name, "PASS" if ok else "FAIL", got, want))
        if not ok:
            fails.append(name)

    print("SELFTEST: booking (D-5)")
    check("T sell -> long liq", book("sell", "T", 10), (10, 0))
    check("T buy -> short liq", book("buy", "T", 10), (0, 10))
    check("M buy -> long liq (maker sold)", book("buy", "M", 10), (10, 0))
    check("M sell -> short liq (maker bought)", book("sell", "M", 10), (0, 10))
    check("MT -> both, full amount", book("buy", "MT", 10), (10, 10))
    check("unknown flag -> skipped", book("buy", "X", 10), None)
    check("dominance: L=2S fires LONG (>=)", dominant(200, 100)[0], "LONG")
    check("dominance: S=2L does not fire SHORT (>)", dominant(100, 200)[0], None)
    print("SELFTEST: classification (strict, ClassifyOfiRatio)")
    check("LONG liqs, OFI 1.61 -> FLIP", classify("LONG", 1.61), "FLIP")
    check("LONG liqs, OFI 1.60 -> BALANCED", classify("LONG", 1.60), "BALANCED")
    check("LONG liqs, OFI 0.62 -> WITH", classify("LONG", 0.62), "WITH")
    check("SHORT liqs, OFI 0.62 -> FLIP", classify("SHORT", 0.62), "FLIP")
    check("SHORT liqs, OFI 1.61 -> WITH", classify("SHORT", 1.61), "WITH")

    tmp = tempfile.mkdtemp(prefix="a4selftest_")
    base = int((datetime(2026, 7, 25, 14, 0, 1) - datetime(1970, 1, 1)).total_seconds() * 1000)   # NY hour, hh:mm:01
    trades = []

    def tr(ms, amt, d, fl):
        trades.append((ms, amt, d, fl))

    # Event A (NY 14:00:01.500, the same second as a run): T sells (long liqs) 30,000 + 30,000 -> 60,000 > 49,724
    # at the 2nd trade
    tr(base + 100, 30000, "sell", "T")
    tr(base + 500, 30000, "sell", "T")
    # an M buy 10 min later = a LONG liquidation under D-5 (the maker sold): L = 100,000 still fires LONG and continues
    # the episode (no new event). Booked by taker direction it would be a SHORT liq (S = 40,000, no dominance, no fire).
    tr(base + 600_000, 40000, "buy", "M")
    # 520 plain trades push everything out of the window
    for k in range(520):
        tr(base + 601_000 + k, 10, "buy", "none")
    # Event B about 31 min after the last firing: T buys = short liqs
    tB = base + 600_000 + 31 * 60_000
    tr(tB, 50000, "buy", "T")         # 50,000 > 49,724 (NY) -> fires SHORT, gap > 30 min -> new event
    for k in range(520):
        tr(tB + 1_000 + k, 10, "sell", "none")
    # Event C: exactly at threshold (not strictly above) -> no fire
    tC = tB + 3 * 3600_000
    tr(tC, 49724, "sell", "T")
    for k in range(520):
        tr(tC + 1_000 + k, 10, "buy", "none")
    # Event D: NY, falls in a stretch with no runs -> no_run_in_W_other
    tD = tC + 3600_000
    tr(tD, 60000, "sell", "T")
    for k in range(520):
        tr(tD + 1_000 + k, 10, "buy", "none")
    # Event E: an MT trade of 30,000 books 30,000 to each side -> no dominance -> no fire
    tE = tD + 3600_000
    tr(tE, 30000, "buy", "MT")
    trades.sort()
    os.makedirs(os.path.join(tmp, "store"))
    with open(os.path.join(tmp, "store", "trades_2026-07.csv"), "w", encoding="utf-8") as f:
        f.write("Timestamp,Price,Amount,Direction,Liquidation,TradeId,TradeSeq,MarkPrice,IndexPrice,TickDirection,Contracts\n")
        for ms, amt, d, fl in trades:
            f.write("%d,NOT_A_PRICE,%.2f,%s,%s,1,1,x,x,0,1\n" % (ms, amt, d, fl))   # Price never parsed: a bad value must not matter

    cnt = Counter()
    events, spans = scan_store([os.path.join(tmp, "store", "trades_2026-07.csv")], cnt)
    print("SELFTEST: store scan")
    check("event count (A, B; not C at threshold, not E MT)", len(events), 3)
    check("event sides", [e.side for e in events], ["LONG", "SHORT", "LONG"])
    check("event A onset is the 2nd trade", events[0].t0, base + 500)
    check("event A continued by the M trade (D-5: M buy = long liq)", events[0].last_fire >= base + 600_000, True)
    check("event B onset", events[1].t0, tB)

    # Run grid: NY 1-min runs at hh:mm:01 around events A and B; nothing around D. Books by name; one duplicate key
    # across books, and one concurrent instance after the collector start.
    hdr_box = ["Timestamp", "Price", "OFIRatio", "ATR", "AggrVelBurstRatio", "InstanceId", "SignalId"]
    t_a = utc(base)

    def row(t, ofi, inst, sid, atr="20.0", burst="1.0"):
        return [t.strftime("%Y-%m-%d %H:%M:%S"), "1", "%.4f" % ofi, atr, burst, inst, str(sid)]

    fetch = os.path.join(tmp, "fetch")
    os.makedirs(fetch)
    box_rows = []
    sid = 0
    for k in range(-2, 4):        # runs 13:58:01 .. 14:03:01
        sid += 1
        box_rows.append(row(t_a.replace(second=1) + timedelta(minutes=k), 1.0 if k != 0 else 1.7, "BOX", sid))
    tb = utc(tB)
    for k in range(-1, 3):
        sid += 1
        box_rows.append(row(tb.replace(second=1) + timedelta(minutes=k), 0.5, "BOX", sid))
    box_rows.append(row(utc(tD) + timedelta(minutes=20), 1.0, "BOX", 999))
    with open(os.path.join(fetch, "analysis_log.csv.v0.7.bak"), "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f); w.writerow(hdr_box); w.writerows(box_rows[:3])
    with open(os.path.join(fetch, "analysis_log.csv"), "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f); w.writerow(hdr_box + ["WsHealth"]); w.writerows([r + ["OK"] for r in box_rows[3:]])
    pooled = os.path.join(tmp, "pooled.csv")
    with open(pooled, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f); w.writerow(hdr_box + ["Extra"])
        w.writerow(box_rows[0])                                            # duplicate key (BOX,1), short row
        w.writerow(row(t_a.replace(second=30), 0.1, "DEV", 1) + ["x"])     # concurrent instance -> dropped
    cnt2 = Counter()
    grid, cstart = load_grid(pooled, fetch, cnt2)
    print("SELFTEST: grid")
    check("dup key dropped", cnt2["dup_key_dropped"], 1)
    check("concurrent instance dropped", cnt2["concurrent_instance_dropped"], 1)
    check("grid runs kept", len(grid), len(box_rows))
    jn = join(events, grid, cstart, spans)
    print("SELFTEST: join")
    check("A joined", jn[0]["status"], "joined")
    check("A R1 is 14:01:01 (strictly after T0's second, not 14:00:01)", jn[0]["r1"][0], t_a + timedelta(minutes=1))
    check("A R1 OFI 1.0 -> BALANCED (14:00:01 holds 1.7 = FLIP)", jn[0]["cls"], "BALANCED")
    check("A R0 is 13:59:01", jn[0]["r0"][0], t_a - timedelta(minutes=1))
    check("B joined, SHORT liqs, R1 OFI 0.5 -> FLIP", (jn[1]["status"], jn[1]["e"].side, jn[1]["cls"]), ("joined", "SHORT", "FLIP"))
    check("D dropped, unnamed hole", jn[2]["status"], "no_run_in_W_other")
    print("SELFTEST: power")
    check("MDE two-sample n=100/100 sigma=10 alpha=.05", round(mde(0.05, 10.0, 100, 100), 3), 3.962)
    check("power at d=MDE is 0.80", round(power_at(0.05, 10.0, mde(0.05, 10.0, 100, 100), 100, 100), 3), 0.8)
    print("SELFTEST %s (%d failure(s))" % ("PASS" if not fails else "FAIL", len(fails)))
    return 0 if not fails else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--store", default=DEFAULT_STORE)
    ap.add_argument("--pooled", default=DEFAULT_POOLED)
    ap.add_argument("--fetch", default=DEFAULT_FETCH)
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        sys.exit(selftest())
    t_start = time.time()
    print("A4 liquidation x OFI flip - logged-era counts (OUTCOME-BLIND). docs/a4-liq-ofi-logged-era-study-spec.md")
    paths = sorted(glob.glob(os.path.join(a.store, "trades_*.csv")))
    if not paths:
        raise SystemExit("STOP: no store files under %s" % a.store)
    print("store files: %d (%s .. %s)" % (len(paths), os.path.basename(paths[0]), os.path.basename(paths[-1])))
    books = [a.pooled, os.path.join(a.fetch, "analysis_log.csv.v0.7.bak")] + \
        sorted(glob.glob(os.path.join(a.fetch, "analysis_log.csv.*col-*.bak"))) + [os.path.join(a.fetch, "analysis_log.csv")]
    for b in books:
        print("book md5 %s  %s" % (md5(b), b.replace("\\", "/")))
    cnt = Counter()
    events, spans = scan_store(paths, cnt)
    grid, cstart = load_grid(a.pooled, a.fetch, cnt)
    joined = join(events, grid, cstart, spans)
    ctrl = control_pool(grid, spans, cstart)
    report(events, spans, grid, cstart, joined, ctrl, cnt)
    print("")
    print("runtime %.0f s" % (time.time() - t_start))


if __name__ == "__main__":
    main()
