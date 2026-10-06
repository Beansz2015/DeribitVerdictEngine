#!/usr/bin/env python3
# tools/ops/liq_tradeflow_read.py
#
# Liquidation x TRADE-FLOW flip study, SESSION 2: the outcome read.
# Spec of record: docs/liq-tradeflow-flip-study-spec.md (section 14, the 2026-10-06 re-registration, supersedes earlier
# sections where they differ). Python 3 standard library only (no numpy).
#
# PHASES (one run, in this order; each phase is a STOP point):
#   A  outcome-blind. Imports run_pass/analyse/report from tools/ops/liq_tradeflow_counts.py (NOT re-implemented) and
#      re-runs the re-registered counts on both stores. H-3: the printed sections 1-6 must equal the H-4 block pasted in
#      docs/liq-tradeflow-flip-study-spec.md section 14.4, line for line. A mismatch is a STOP before any Price is read.
#      Phase A also records, per flow window, the stream ordinal of the trade that closed it (an observer wrapped around
#      Machine.close; it changes no count -- H-3 proves that).
#   B  first Price read. One fenced pass over both stores: 1m / 3m / 5m candles built from trade prices (every trade,
#      empty buckets = flat at the previous close with 0 volume -- the ReplayLoop forming-stub zero-trade convention),
#      and the entry trade of every row: time variants and control rows = the first trade with Timestamp >= D; TFI30 =
#      the trade after the 30th (by stream ordinal).
#   L  levels. tools/ops/TradeflowLevels (VB, links the SHIPPED Core) replays the engine's placed levels at D from the
#      completed tape candles (TFS-8 (d), the feasibility addendum in the spec) and the ATR-fallback geometry of
#      spec section 4.8 (TFS-8 (c), descriptive here).
#   C  outcome walk. One more fenced pass: each row walks the trades after its entry trade, before entry + window
#      (NY 15 min, ASIA / LONDON 45 min by the session at T0). LONG: first price >= target = success, first <= stop =
#      stop; SHORT mirrored. Timeout = the last trade price before the window end. No trade in the window = a named drop.
#   S  statistics, as registered: house net EV (DeribitIndicatorProject.md section 5a), Sigma breakevens, day bootstrap
#      (seed 20261005, 10,000), TFS-H1 / TFS-H2 / TFS-H3 with Holm across the three (A4L-9 (b)), the census label
#      (TFS-10), the trailing-tercile THRESHOLD-SENSITIVE check (TFS-7), descriptive splits (TFS-H4).
#
# DATE FENCE (hard): every pass reads through tc.fenced_rows, which stops at the first line >= 2026-07-03 00:00 UTC.
# Candles, entries and walks never see a trade at or after the fence. A walk whose window would end at or after the
# fence is dropped and counted (spec section 4.1, the outcome fence).
#
# COPIED CODE: boot, diff_of, halves, label, readable, sig, pctl, rw_run, arm_rw, boot_p, holm, finalize_label are copied
# from tools/ops/burst_outcome_read.py (which copied the first group from tools/ops/medium_tier_diagnosis.py). Changes:
# finalize_label's suffix "EDGE-SENSITIVE" reads "THRESHOLD-SENSITIVE" (spec section 4.4) and its seam arm is removed;
# nothing else. They read the module globals args.seed, R, DRAWS, SEEDC, ALL_LABELS, FLOOR as in the source.
#
# Usage (from the repo root):
#   python tools/ops/liq_tradeflow_read.py --selftest
#   python tools/ops/liq_tradeflow_read.py --h3 --workdir <dir>             # phase A only: the H-3 gate
#   python tools/ops/liq_tradeflow_read.py --feasibility --workdir <dir>    # tape vs venue candles and levels, 2026-02..06
#   python tools/ops/liq_tradeflow_read.py --run --workdir <dir>            # A, B, L, C, S: the registered run
# The level tool must be built first: dotnet build tools/ops/TradeflowLevels/TradeflowLevels.vbproj -c Release

import argparse, csv, heapq, json, math, os, random, statistics, subprocess, sys, tempfile, time
from bisect import bisect_left, bisect_right
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta
from types import SimpleNamespace

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import liq_tradeflow_counts as tc   # noqa: E402  (session 1 + re-registration counts tool; imported, not re-implemented)
import a4_liq_ofi_counts as a4      # noqa: E402  (the sister's event code, via tc)

SPEC = "docs/liq-tradeflow-flip-study-spec.md"
SEED, RESAMPLES = 20261005, 10000
FLOOR = 100
LEVEL_DLL = "tools/ops/TradeflowLevels/bin/Release/net8.0/TradeflowLevels.dll"
PINS = {"version": 69, "maker_fee_bps": 1.5, "round_trip_style": "maker_maker", "atr_period": 7,
        "atr_target_multiplier": 1.75, "LONDON": 2.0, "ASIA": 1.25, "atr_stop_multiplier": 1.6,
        "tfi_window": 30, "tfi_threshold": 0.15}

# ---------------------------------------------------------------- phase A: the counts, re-run, and the ordinal observer
CNT_REF = [None]
CLOSE_ORD = {}
_orig_close = tc.Machine.close


def _observed_close(self, D, fires_at_D):
    """Observer only: records the stream ordinal (1-based, = cnt["trades"]) of the trade being processed when a flow
    window closes. For a time window that trade is the first trade with Timestamp >= D (the entry trade); for TFI30 it
    is the 30th unflagged trade (the entry is the next one)."""
    _orig_close(self, D, fires_at_D)
    CLOSE_ORD[id(self)] = CNT_REF[0]["trades"]


tc.Machine.close = _observed_close


def store_paths(store, store23):
    stores = [(store23, tc.MONTHS_2023), (store, tc.MONTHS)]
    groups = [[os.path.join(d, "trades_%s.csv" % m) for m in mm] for d, mm in stores]
    return stores, groups, [p for g in groups for p in g]


def h4_block():
    """The H-4 block pasted in the spec, section 14.4 (between the first pair of ``` fences after its heading)."""
    with open(SPEC, encoding="utf-8") as f:
        lines = f.read().split("\n")
    i = next(k for k, s in enumerate(lines) if s.startswith("### 14.4 Output of `H-4`"))
    j = next(k for k in range(i, len(lines)) if lines[k].startswith("```"))
    k2 = next(k for k in range(j + 1, len(lines)) if lines[k].startswith("```"))
    return [s.rstrip("\r") for s in lines[j + 1:k2]]


def section(lines, a, b):
    i = next(k for k, s in enumerate(lines) if s.startswith("=== %d." % a))
    j = next((k for k, s in enumerate(lines) if s.startswith("=== %d." % b)), len(lines))
    return lines[i:j]


def phase_a(opt, out=print):
    tc.configure("extended")
    stores, groups, paths = store_paths(opt.store, opt.store_2023)
    miss = [p for p in paths if not os.path.exists(p)]
    if miss:
        raise SystemExit("STOP: missing store files %s" % miss)
    got = ["Liquidation x TRADE-FLOW flip - counts (OUTCOME-BLIND). docs/liq-tradeflow-flip-study-spec.md",
           "store files: %d in %d stores, read in this order as one pass, bytes %d"
           % (len(paths), len(groups), sum(os.path.getsize(p) for p in paths))]
    for (d, mm), g in zip(stores, groups):
        got.append("  %s  trades_%s.csv .. trades_%s.csv  files %d  bytes %d" % (d, mm[0], mm[-1], len(g), sum(os.path.getsize(p) for p in g)))
    tc.check_store_seams(groups, out=got.append)
    mbad = []
    for g in groups:
        for x, y in zip(g, g[1:]):
            ok, v = tc.seam_check(x, y)
            if not ok:
                mbad.append("%s->%s seq %d->%d" % (os.path.basename(x), os.path.basename(y), v[1], v[3]))
    got.append("month-file seams inside each store: %d checked, %d FAIL%s"
               % (sum(len(g) - 1 for g in groups), len(mbad), (": " + "; ".join(mbad)) if mbad else ""))
    tfi_info = tc.engine_tfi_from_settings(opt.settings)
    cnt = Counter()
    CNT_REF[0] = cnt
    CLOSE_ORD.clear()
    events, spans, by_ev, refs, refdrop, st = tc.run_pass(paths, cnt)
    assert st["max_ts"] < tc.FENCE_MS and st["min_ts"] >= tc.ms(tc.START), "FENCE BREACH or data before 2023-01-01"
    an = tc.analyse(events, by_ev, refs, tfi_info[2])
    tc.report(events, spans, by_ev, refs, refdrop, st, an, cnt, tfi_info, out=got.append)
    want = h4_block()
    s16_got, s16_want = section(got, 1, 7), section(want, 1, 7)
    ok16 = s16_got == s16_want
    okall = got == want
    out("H-3: sections 1-6 of the re-run vs the H-4 block (%s section 14.4): %d vs %d lines: %s"
        % (SPEC, len(s16_got), len(s16_want), "PASS (identical)" if ok16 else "FAIL"))
    out("H-3 (information): the whole block incl. header, section 0 and section 7: %d vs %d lines: %s"
        % (len(got), len(want), "identical" if okall else "DIFFERS"))
    if not ok16:
        for k in range(max(len(s16_got), len(s16_want))):
            a_ = s16_got[k] if k < len(s16_got) else "<none>"
            b_ = s16_want[k] if k < len(s16_want) else "<none>"
            if a_ != b_:
                out("  first difference at section line %d:\n    got  %s\n    want %s" % (k, a_, b_))
                break
        raise SystemExit("STOP: H-3 failed; no Price is read")
    return {"paths": paths, "events": events, "an": an, "refs": refs, "cnt": cnt, "tfi": tfi_info}


# ---------------------------------------------------------------- rows (built from phase A only; no price)
def build_rows(A):
    """Event rows: one per (event, variant) whose window is measured, with D and the entry ordinal. Control rows: the
    primary reference windows in an outer tercile, outcome window before the fence (as tc.report section 6)."""
    an = A["an"]
    ev_rows, ctl_rows = [], []
    pk = tc.PRIMARY
    for k, rec in enumerate(an["rows"]):
        e = rec["e"]
        for name, mode, Q, L in tc.VARIANTS:
            v = rec["v"][name]
            m = v["m"]
            if m.status != "measured" or v["cls"] is None or v["ofence"]:
                continue
            o = CLOSE_ORD[id(m)]
            ev_rows.append({
                "key": "E%04d.%s" % (k, name), "ev": k, "var": name, "kind": "event",
                "t0": e.t0, "D": m.D, "entry_ord": o if mode == "time" else o + 1, "entry_rule": mode,
                "sess": e.sess, "side": e.side, "hour": tc.utc(e.t0).hour, "cls": v["cls"], "eng": v["eng"],
                "I": m.I, "resets": m.resets, "fires_at_D": bool(m.fires_at_D), "two_sided": e.two_sided,
                "pop": bool(rec["pop"]) if name == pk else None, "trail": rec["trail"] if name == pk else None,
                "per": rec["per"], "day": tc.utc(e.t0).date()})
    for (g, s, side, of) in an["control"]:
        if side is None or of:
            continue
        ctl_rows.append({"key": "C%d" % g, "kind": "control", "g": g, "D": g + 60 * 1000, "sess": s, "side": side,
                         "hour": tc.utc(g).hour, "cls": "CONTROL", "per": tc.period_of(tc.utc(g)), "day": tc.utc(g).date()})
    return ev_rows, ctl_rows



# ---------------------------------------------------------------- pins (spec section 14.5; tracked settings.json, read-only)
MIN_MOVE = [None]


def check_pins(path):
    with open(path, encoding="utf-8") as f:
        s = json.load(f)
    sc = s["scoring"]
    tcs, sl = sc["trade_costs"], sc["structural_levels"]
    got = {"version": s["version"], "maker_fee_bps": tcs["maker_fee_bps"], "round_trip_style": tcs["round_trip_style"],
           "atr_period": s["indicators"]["ATR"]["period"], "atr_target_multiplier": sc["atr_target_multiplier"],
           "LONDON": sl["sessions"]["LONDON"].get("fallback_target_atr_mult"),
           "ASIA": sl["sessions"]["ASIA"].get("fallback_target_atr_mult"), "atr_stop_multiplier": sc["atr_stop_multiplier"],
           "tfi_window": s["indicators"]["TFI"]["window_size"], "tfi_threshold": s["indicators"]["TFI"]["threshold"]}
    bad = {k: (got[k], PINS[k]) for k in PINS if got[k] != PINS[k]}
    if bad:
        raise SystemExit("STOP: tracked settings.json differs from the spec section 14.5 pins: %r" % bad)
    # the engine's Step 5c min-move floor (fee + min_net_move_pct), for a DESCRIPTIVE count only: it acts on a verdict,
    # these rows are not verdicts, so it is not applied (feasibility addendum)
    MIN_MOVE[0] = 2.0 * tcs["maker_fee_bps"] + tcs["min_net_move_pct"] * 1e4
    return 2.0 * tcs["maker_fee_bps"], sl


# ---------------------------------------------------------------- phase B: the first Price read (candles + entries)
CANDLE_HDR = "Timestamp,Open,High,Low,Close,Volume,Cost\n"


class Candles:
    """1m candles from every trade. 3m and 5m are folded from the REAL 1m candles (a minute that held trades), which is
    identical to aggregating the trades. A bucket with no trade = flat at the previous close, volume 0, counted (the
    ReplayLoop.BuildFormingStub zero-trade convention). Volume = sum(Amount / Price) (BTC), Cost = sum(Amount) (USD), as
    ReplayLoop.BuildFormingStub."""

    def __init__(self, outdir):
        self.f = {r: open(os.path.join(outdir, "candles_%dm.csv" % r), "w", encoding="utf-8", newline="\n") for r in (1, 3, 5)}
        for f in self.f.values():
            f.write(CANDLE_HDR)
        self.acc = {3: None, 5: None}
        self.last_close = {1: None, 3: None, 5: None}
        self.last_bucket = {1: None, 3: None, 5: None}
        self.fills, self.n = Counter(), Counter()

    def _emit(self, res, b, o, h, l, c, v, u):
        lb, step = self.last_bucket[res], res * 60000
        if lb is not None:
            t, pc = lb + step, repr(self.last_close[res])
            while t < b:
                self.f[res].write("%d,%s,%s,%s,%s,0,0\n" % (t, pc, pc, pc, pc))
                self.fills[res] += 1
                self.n[res] += 1
                t += step
        self.f[res].write("%d,%r,%r,%r,%r,%r,%r\n" % (b, o, h, l, c, v, u))
        self.last_bucket[res], self.last_close[res] = b, c
        self.n[res] += 1

    def minute(self, b, o, h, l, c, v, u):
        self._emit(1, b, o, h, l, c, v, u)
        for res in (3, 5):
            rb = b - b % (res * 60000)
            a = self.acc[res]
            if a is not None and a[0] != rb:
                self._emit(res, *a)
                a = None
            if a is None:
                self.acc[res] = [rb, o, h, l, c, v, u]
            else:
                if h > a[2]:
                    a[2] = h
                if l < a[3]:
                    a[3] = l
                a[4] = c
                a[5] += v
                a[6] += u

    def close(self):
        for res in (3, 5):
            if self.acc[res] is not None:
                self._emit(res, *self.acc[res])
                self.acc[res] = None
        for f in self.f.values():
            f.close()


def phase_b(paths, outdir, ev_rows, ctl_rows, out=print, rows_iter=None):
    """One fenced pass. Candles (every trade) and the entry trade of every row:
    time-rule rows (time variants, control) = the first trade with Timestamp >= D; trade-rule rows (TFI30) = the trade at
    the ordinal phase A recorded (the trade after the 30th). Event time-rule rows carry BOTH: the phase-A ordinal decides,
    the Timestamp rule is a cross-check that must agree on every row."""
    cd = Candles(outdir)
    by_ord = defaultdict(list)
    for r in ev_rows:
        by_ord[r["entry_ord"]].append(r)
    timed = sorted([r for r in ev_rows if r["entry_rule"] == "time"] + list(ctl_rows), key=lambda r: r["D"])
    ti, nt = 0, len(timed)
    nextD = timed[0]["D"] if nt else 1 << 62
    entries, time_ord = {}, {}
    cnt = Counter()
    n = 0
    cur = None
    o = h = l = c = v = u = 0.0
    prev_ts = None
    for ts, p in (rows_iter if rows_iter is not None else tc.fenced_rows(paths, cnt)):
        n += 1
        px = float(p[1])
        amt = float(p[2])
        b = ts - ts % 60000
        if b != cur:
            if cur is not None:
                cd.minute(cur, o, h, l, c, v, u)
            cur, o, h, l, c, v, u = b, px, px, px, px, amt / px, amt
        else:
            if px > h:
                h = px
            elif px < l:
                l = px
            c = px
            v += amt / px
            u += amt
        while ts >= nextD:
            r = timed[ti]
            time_ord[r["key"]] = n
            if r["kind"] == "control":
                entries[r["key"]] = (n, ts, px)
            ti += 1
            nextD = timed[ti]["D"] if ti < nt else 1 << 62
        if n in by_ord:
            for r in by_ord[n]:
                entries[r["key"]] = (n, ts, px, prev_ts)
        prev_ts = ts
    if cur is not None:
        cd.minute(cur, o, h, l, c, v, u)
    cd.close()
    chk = Counter()
    for r in ev_rows:
        e = entries.get(r["key"])
        if e is None:
            chk["event row without an entry trade"] += 1
            continue
        if r["entry_rule"] == "time":
            chk["time rule: phase-A ordinal == first trade >= D" if time_ord.get(r["key"]) == e[0] else "time rule: ordinal DISAGREES"] += 1
            chk["time rule: entry ts >= D, previous trade ts < D" if (e[1] >= r["D"] and (e[3] is None or e[3] < r["D"])) else "time rule: entry NOT the first trade >= D"] += 1
        else:
            chk["TFI30: entry = the trade after the 30th (ts >= its ts)" if e[1] >= r["D"] else "TFI30: entry BEFORE the 30th trade"] += 1
        entries[r["key"]] = e[:3]
    for r in ctl_rows:
        chk["control: entry = first trade >= g + 60 s" if r["key"] in entries else "control row without an entry trade"] += 1
    out("phase B: trades read %d (fence stop: %s); candles 1m %d 3m %d 5m %d; empty buckets filled flat 1m %d 3m %d 5m %d"
        % (n, "yes" if cnt["fence_stop"] else "no", cd.n[1], cd.n[3], cd.n[5], cd.fills[1], cd.fills[3], cd.fills[5]))
    for k in sorted(chk):
        out("phase B entry check: %-56s %d" % (k, chk[k]))
    bad = sum(v_ for k, v_ in chk.items() if "DISAGREES" in k or "NOT the first" in k or "BEFORE" in k)
    if bad:
        raise SystemExit("STOP: entry identification failed on %d rows" % bad)
    return entries, n


# ---------------------------------------------------------------- phase L: the placed levels (VB, shipped Core)
def is_long(r):
    """The traded side. Event: side = the LIQUIDATED side; LONG liquidations = cascade down = fade LONG. Control: side =
    the flow's side (I > hi -> LONG, traded with the flow). In both cases the trade is LONG exactly when side == LONG."""
    return r["side"] == "LONG"


def phase_l(workdir, rows, entries, cfg_copy, out=print):
    rp, lp = os.path.join(workdir, "level_rows.csv"), os.path.join(workdir, "levels.csv")
    with open(rp, "w", encoding="utf-8", newline="\n") as f:
        f.write("key,d_ms,entry_px,hour,is_long\n")
        for r in rows:
            e = entries.get(r["key"])
            if e is None:
                continue
            f.write("%s,%d,%r,%d,%d\n" % (r["key"], r["D"], e[2], r["hour"], 1 if is_long(r) else 0))
    cp = subprocess.run(["dotnet", LEVEL_DLL, "levels", "--settings", cfg_copy, "--candles", workdir, "--rows", rp, "--out", lp],
                        capture_output=True, text=True)
    for line in cp.stdout.splitlines():
        out("phase L: " + line)
    if cp.returncode != 0:
        raise SystemExit("STOP: the level tool failed: %s" % cp.stderr[-2000:])
    lv = {}
    with open(lp, encoding="utf-8") as f:
        for d in csv.DictReader(f):
            lv[d["key"]] = d
    return lv


# ---------------------------------------------------------------- phase C: the outcome walk
class Walk:
    __slots__ = ("key", "arm", "ord", "ts", "px", "end", "long", "tgt", "stp", "res", "res_ts", "mark", "ntr", "drop")

    def __init__(self, key, arm, ord_, ts, px, end, long_, tgt, stp):
        self.key, self.arm, self.ord, self.ts, self.px, self.end = key, arm, ord_, ts, px, end
        self.long, self.tgt, self.stp = long_, tgt, stp
        self.res = self.res_ts = self.mark = self.ntr = self.drop = None


def walk_pass(rows_iter, walks):
    """Walks every trade after each walk's entry trade and before entry + window. rows_iter yields (ts, parts) in stream
    order; ordinals count from 1 as phase A's cnt["trades"]. A walk is resolved by the first trade that reaches its target
    (success) or its stop (stop); at the window end it records the mark = the last trade price before the window end."""
    opens = defaultdict(list)
    for w in walks:
        opens[w.ord].append(w)
    heap, active = [], []
    inf = float("inf")
    hi, lo = inf, -inf

    def retrig():
        if not active:
            return inf, -inf
        return (min((w.tgt if w.long else w.stp) for w in active), max((w.stp if w.long else w.tgt) for w in active))

    n = 0
    last_px = None
    for ts, p in rows_iter:
        n += 1
        px = float(p[1])
        if heap and heap[0][0] <= ts:
            changed = False
            while heap and heap[0][0] <= ts:
                w = heapq.heappop(heap)[2]
                w.mark, w.ntr = last_px, n - 1 - w.ord
                if w.res is None:
                    w.res = "timeout"
                    active.remove(w)
                    changed = True
            if changed:
                hi, lo = retrig()
        if px >= hi or px <= lo:
            hit = []
            for w in active:
                if w.long:
                    if px >= w.tgt:
                        w.res = "success"
                    elif px <= w.stp:
                        w.res = "stop"
                else:
                    if px <= w.tgt:
                        w.res = "success"
                    elif px >= w.stp:
                        w.res = "stop"
                if w.res is not None:
                    w.res_ts = ts
                    hit.append(w)
            for w in hit:
                active.remove(w)
            hi, lo = retrig()
        if n in opens:
            for w in opens.pop(n):
                heapq.heappush(heap, (w.end, id(w), w))
                active.append(w)
            hi, lo = retrig()
        last_px = px
    while heap:   # the stream ended (the fence): no trade remains before these window ends
        w = heapq.heappop(heap)[2]
        w.mark, w.ntr = last_px, n - w.ord
        if w.res is None:
            w.res = "timeout"
    for w in walks:
        if w.ntr == 0:
            w.drop = "no_trade_in_window"
    return n


def make_walks(rows, entries, lv, fence_ms=None):
    """Two walks per row: arm d (the engine's placed levels, TFS-8 (d), primary) and arm c (the ATR-fallback geometry,
    spec section 4.8, descriptive). Window = HOLD_MIN by the session at T0 (control: at g), from the entry trade."""
    fence_ms = tc.FENCE_MS if fence_ms is None else fence_ms
    walks = []
    for r in rows:
        e = entries.get(r["key"])
        x = lv.get(r["key"])
        r["drop"], r["walk"] = {}, {}
        for arm in ("d", "c"):
            if e is None:
                r["drop"][arm] = "no_entry_trade"
                continue
            st = (x["status"] if arm == "d" else x["c_status"]) if x is not None else "no_level_row"
            if st != "ok":
                r["drop"][arm] = st
                continue
            tgt = float(x["target"] if arm == "d" else x["c_target"])
            stp = float(x["stop"] if arm == "d" else x["c_stop"])
            lg = is_long(r)
            sg = 1 if lg else -1
            if not (sg * (tgt - e[2]) > 0 and sg * (e[2] - stp) > 0):
                r["drop"][arm] = "level_on_wrong_side"
                continue
            end = e[1] + tc.HOLD_MIN[r["sess"]] * 60000
            if end >= fence_ms:
                r["drop"][arm] = "outcome_window_crosses_fence"
                continue
            w = Walk(r["key"], arm, e[0], e[1], e[2], end, lg, tgt, stp)
            r["walk"][arm] = w
            walks.append(w)
    return walks


def outcome(w, fee):
    """House net EV per trade, bps (DeribitIndicatorProject.md section 5a): +target - fee on success, -stop - fee on a
    stop, mark - fee on a timeout (mark signed for the side). Also the fixed-horizon mark (descriptive)."""
    sg = 1.0 if w.long else -1.0
    t = abs(w.tgt - w.px) / w.px * 1e4
    s = abs(w.px - w.stp) / w.px * 1e4
    mk = sg * (w.mark - w.px) / w.px * 1e4
    if w.res == "success":
        ev = t - fee
    elif w.res == "stop":
        ev = -s - fee
    else:
        ev = mk - fee
    return {"ev": ev, "t": t, "s": s, "res": w.res, "fh": mk - fee}


# ================================================================== COPIED from tools/ops/burst_outcome_read.py
# boot, diff_of, halves, readable, sig, label, rw_run, pctl: that file's copy of tools/ops/medium_tier_diagnosis.py
# (with its one added DRAWS line in boot and its covered-strata readability in rw_run). arm_rw, boot_p, holm: verbatim.
# finalize_label: the suffix reads THRESHOLD-SENSITIVE (spec section 4.4, TFS-7) and the seam arm is removed.
args = SimpleNamespace(seed=SEED)
R = RESAMPLES
DRAWS = []
ALL_LABELS = []
SEEDC = [0]


def pctl(sorted_vals, q):
    if not sorted_vals: return float("nan")
    k = max(1, min(len(sorted_vals), math.ceil(q / 100.0 * len(sorted_vals))))
    return sorted_vals[k - 1]


def boot(rows, contribs, K, stat):
    """contribs(row) -> list of (cell, value). Day-level resampling. Returns (point, lo, hi, counts)."""
    days = {}
    for r in rows:
        cs = contribs(r)
        if not cs: continue
        i = days.get(r["day"])
        if i is None:
            i = days[r["day"]] = len(days)
        for k, v in cs:
            pass
    D = len(days)
    S = [[0.0] * D for _ in range(K)]
    C = [[0] * D for _ in range(K)]
    for r in rows:
        cs = contribs(r)
        if not cs: continue
        i = days[r["day"]]
        for k, v in cs:
            S[k][i] += v
            C[k][i] += 1
    tS = [sum(s) for s in S]; tC = [sum(c) for c in C]
    point = stat(tS, tC)
    SEEDC[0] += 1
    if D == 0:
        DRAWS.append([])  # ADDED (burst_outcome_read.py): keep the draws for Holm
        return point, float("nan"), float("nan"), tC
    rng = random.Random(args.seed * 1000 + SEEDC[0])
    rngD = range(D)
    draws = []
    for _ in range(R):
        idx = rng.choices(rngD, k=D)
        bs = [sum(map(S[k].__getitem__, idx)) for k in range(K)]
        bc = [sum(map(C[k].__getitem__, idx)) for k in range(K)]
        v = stat(bs, bc)
        if v == v:
            draws.append(v)
    draws.sort()
    DRAWS.append(draws)  # ADDED (burst_outcome_read.py): keep the draws for Holm
    return point, pctl(draws, 2.5), pctl(draws, 97.5), tC


def diff_of(a, b):
    return lambda s, c: (s[a] / c[a] - s[b] / c[b]) if c[a] > 0 and c[b] > 0 else float("nan")


def halves(rows):
    return {"FULL": rows, "H1": [r for r in rows if r["half"] == "H1"], "H2": [r for r in rows if r["half"] == "H2"]}


def readable(r, need):
    return all(r[3][k] >= FLOOR for k in need)


def sig(r, need):
    return readable(r, need) and r[1] == r[1] and (r[1] > 0 or r[2] < 0)


def label(res, need):
    f, h1, h2 = res["FULL"], res["H1"], res["H2"]
    if not readable(f, need): return "NOT READABLE"
    sgn = lambda x: (x > 0) - (x < 0)
    side = lambda r: "d > 0" if r[0] > 0 else "d < 0"
    if sig(h1, need) and sig(h2, need) and sgn(h1[0]) == sgn(h2[0]): return "CONFIRMED (%s)" % side(h1)
    if sig(h1, need) and readable(h2, need) and sgn(h1[0]) == sgn(h2[0]): return "H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT (%s)" % side(h1)
    if sig(h1, need): return "H1 FINDING, H2 CONTRADICTS OR NOT READABLE (%s)" % side(h1)
    if sig(h2, need): return "DISCOVERY ONLY (H2) (%s)" % side(h2)
    return "NO DIFFERENCE SHOWN"


def rw_run(sr, strat, name, ctx):
    keys = sorted(set(strat(r) for r in sr))
    K = len(keys)
    kidx = {k: i for i, k in enumerate(keys)}
    def cb(r):
        if r["tier"] == "MEDIUM": return [(kidx[strat(r)], r["ev_main"])]
        if r["tier"] == "WEAK": return [(K + kidx[strat(r)], r["ev_main"])]
        return []
    def stat(s, c):
        cov = [i for i in range(K) if c[i] > 0 and c[K + i] > 0]
        wn = sum(c[K + i] for i in cov)
        if wn == 0: return float("nan")
        med = sum(c[K + i] / wn * s[i] / c[i] for i in cov)
        wk = sum(s[K + i] for i in cov) / wn
        return med - wk
    res = {}
    for hn, hr in halves(sr).items():
        pt, lo, hi, cnt = boot(hr, cb, 2 * K, stat)
        cov = [i for i in range(K) if cnt[i] > 0 and cnt[K + i] > 0]
        res[hn] = (pt, lo, hi, [sum(cnt[i] for i in cov), sum(cnt[K + i] for i in cov)])
    lab = label(res, (0, 1))
    ALL_LABELS.append((ctx, name, lab, res["FULL"]))
    return res, lab, keys


def arm_rw(pop_rows, is_a, is_b, strat, name, ctx):
    rr = []
    for r in pop_rows:
        if is_a(r): t = "WEAK"
        elif is_b(r): t = "MEDIUM"
        else: continue
        q = dict(r); q["tier"] = t
        rr.append(q)
    n0 = len(DRAWS)
    res, _lab_unused, keys = rw_run(rr, strat, name, ctx)
    full_draws = sorted(-v for v in DRAWS[n0])   # FULL is the first boot() call inside rw_run (halves order)
    neg = {h: (-v[0], -v[2], -v[1], [v[3][1], v[3][0]]) for h, v in res.items()}   # counts become [A, B]
    lab = label(neg, (0, 1))
    ALL_LABELS[-1] = (ctx, name, lab, neg["FULL"])
    b_keys = set(strat(r) for r in rr if r["tier"] == "MEDIUM")
    a_nocov = sum(1 for r in rr if r["tier"] == "WEAK" and strat(r) not in b_keys)
    return {"res": neg, "label": lab, "draws": full_draws, "strata": len(keys), "a_dropped": a_nocov}


def boot_p(draws):
    if not draws: return 1.0
    n = float(len(draws))
    lo = sum(1 for d in draws if d <= 0) / n
    hi = sum(1 for d in draws if d >= 0) / n
    return min(1.0, 2.0 * min(lo, hi))


def holm(results, m):
    for s, x in results.items():
        x["p"] = boot_p(x["draws"]) if x["label"] != "NOT READABLE" else 1.0
    order = sorted(results, key=lambda s: results[s]["p"])
    stopped = False
    for i, s in enumerate(order):
        x = results[s]
        a_i = 0.05 / (m - i) if (m - i) > 0 else 0.05
        x["alpha"] = a_i
        x["hci"] = (pctl(x["draws"], 100.0 * a_i / 2.0), pctl(x["draws"], 100.0 * (1.0 - a_i / 2.0)))
        excl = x["hci"][0] == x["hci"][0] and (x["hci"][0] > 0 or x["hci"][1] < 0)
        ok = (not stopped) and x["label"] != "NOT READABLE" and x["p"] <= a_i and excl
        if not ok: stopped = True
        x["reject"] = ok


def finalize_label(x, sens):
    lab = x["label"]
    if lab.startswith("CONFIRMED") and not x["reject"]:
        lab = "NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (%s)" % ("d > 0" if x["res"]["FULL"][0] > 0 else "d < 0")
    if lab not in ("NOT READABLE", "NO DIFFERENCE SHOWN") and sens is not None:
        sp, fp = sens["res"]["FULL"][0], x["res"]["FULL"][0]
        if sp == sp and fp == fp and (sp > 0) != (fp > 0):
            lab += " THRESHOLD-SENSITIVE"
    x["sens_pt"] = sens["res"]["FULL"][0] if sens is not None else None
    x["final"] = lab
    return lab
# ================================================================== end of the copied block


def mean_test(rows, name, ctx):
    """TFS-H2 shape: the mean of one group vs 0, FULL / H1 / H2 with the copied boot and label (need = the one group).
    Returns the arm_rw result shape so holm() and finalize_label() take it unchanged."""
    n0 = len(DRAWS)
    res = {}
    for hn, hr in halves(rows).items():
        res[hn] = boot(hr, lambda r: [(0, r["ev_main"])], 1, lambda s, c: s[0] / c[0] if c[0] > 0 else float("nan"))
    lab = label(res, (0,))
    ALL_LABELS.append((ctx, name, lab, res["FULL"]))
    return {"res": res, "label": lab, "draws": DRAWS[n0], "strata": 1, "a_dropped": 0}


MEAN = lambda s, c: s[0] / c[0] if c[0] > 0 else float("nan")


def f1(x):
    return "n/a" if x is None or x != x else ("%+.1f" % x)


def ci3(x):
    return "%s [%s, %s]" % (f1(x[0]), f1(x[1]), f1(x[2]))


def ns(x):
    return "/".join(str(c) for c in x[3])


def desc_mean(rows):
    return boot(rows, lambda r: [(0, r["ev_main"])], 1, MEAN)


def desc_diff(a_rows, b_rows):
    rr = [{"day": q["day"], "k": 0, "v": q["ev_main"]} for q in a_rows] + [{"day": q["day"], "k": 1, "v": q["ev_main"]} for q in b_rows]
    return boot(rr, lambda r: [(r["k"], r["v"])], 2, diff_of(0, 1))


def explain(rows, fee):
    """Success rate and the Sigma breakevens (DeribitIndicatorProject.md section 5a rule 1: distance-weighted, never an
    average of per-row rates), from rows carrying o = outcome(). Returns (n, succ%, gross BE%, net BE%, net edge pp, sd)."""
    n = len(rows)
    if n == 0:
        return None
    succ = 100.0 * sum(1 for q in rows if q["o"]["res"] == "success") / n
    den = sum(q["o"]["t"] + q["o"]["s"] for q in rows)
    gbe = 100.0 * sum(q["o"]["s"] for q in rows) / den
    nbe = 100.0 * sum(q["o"]["s"] + fee for q in rows) / den
    sd = statistics.pstdev([q["ev_main"] for q in rows]) if n > 1 else float("nan")
    tout = 100.0 * sum(1 for q in rows if q["o"]["res"] == "timeout") / n
    return n, succ, gbe, nbe, succ - nbe, sd, tout


# ---------------------------------------------------------------- phase S: the registered statistics
def view(rows, arm, field="ev"):
    """Light rows for the copied functions: day, half, ev_main (+ a reference to the source row)."""
    out = []
    for r in rows:
        w = r.get("walk", {}).get(arm)
        if w is None or w.drop is not None:
            continue
        o = r["o"][arm]
        out.append({"day": r["day"], "half": r["half"], "ev_main": o[field], "o": o, "r": r})
    return out


def phase_s(ev_rows, ctl_rows, fee, out=print):
    global R
    R = RESAMPLES
    DRAWS.clear(); SEEDC[0] = 0; ALL_LABELS.clear()
    prim = [r for r in ev_rows if r["var"] == tc.PRIMARY and r["pop"]]
    # halves: the registered split (spec section 4.9 / H-4: first floor(D/2) UTC days holding a population event)
    days = sorted(set(r["day"] for r in prim))
    h1set = set(days[:len(days) // 2])
    h2_start = days[len(days) // 2]
    for r in ev_rows:
        r["half"] = "H1" if r["day"] in h1set or r["day"] < h2_start else "H2"
    for r in ctl_rows:
        r["half"] = "H1" if r["day"] < h2_start else "H2"
    out("")
    out("=== S0. Population and halves (registered; outcome-side drops are counted below, not re-split) ===")
    out("primary population %d (FLIP %d, NO-FLIP %d); control %d; %d event days; half H1 = %s..%s (%d days), half H2 = %s..%s"
        % (len(prim), sum(1 for r in prim if r["cls"] == "FLIP"), sum(1 for r in prim if r["cls"] != "FLIP"), len(ctl_rows),
           len(days), days[0], days[len(days) // 2 - 1], len(days) // 2, h2_start, days[-1]))
    out("control rows: half by date (H1 = before %s)" % h2_start)

    # ---- drops BEFORE any outcome is printed (spec section 7 item 4; section 14.0 escalation)
    groups = [("FLIP", [r for r in prim if r["cls"] == "FLIP"]), ("NO-FLIP", [r for r in prim if r["cls"] != "FLIP"]), ("CONTROL", ctl_rows)]
    out("")
    out("=== S1. Outcome-side drops per arm (printed before any outcome) ===")
    stop = []
    for arm, lab in (("d", "placed levels (TFS-8 (d), primary)"), ("c", "ATR-fallback geometry (descriptive)")):
        for gname, gr in groups:
            reasons = Counter()
            for r in gr:
                if r["drop"].get(arm):
                    reasons[r["drop"][arm]] += 1
                elif r["walk"][arm].drop:
                    reasons[r["walk"][arm].drop] += 1
            nd = sum(reasons.values())
            share = nd / len(gr) if gr else 0.0
            out("arm %s %-38s %-8s rows %6d  dropped %5d (%.2f %%)  %s" % (arm, lab, gname, len(gr), nd, 100 * share,
                "  ".join("%s=%d" % kv for kv in sorted(reasons.items())) or "-"))
            if arm == "d" and share > 0.10:
                stop.append("%s %.1f %%" % (gname, 100 * share))
    if stop:
        raise SystemExit("STOP: an outcome-side drop exceeds 10 %% of an arm (spec section 7 item 4): %s" % "; ".join(stop))

    for r in ev_rows + ctl_rows:
        r["o"] = {}
        for arm in ("d", "c"):
            w = r["walk"].get(arm)
            if w is not None and w.drop is None:
                r["o"][arm] = outcome(w, fee)
    # ATR tercile for TFS-H3: per session, from control-row ATR (the replay's engine ATR, USD; the house unit, as the
    # burst read's ATR fifths)
    atr_edges = {}
    for s in tc.SESSIONS:
        atr_edges[s] = tc.tercile_edges([r["atr"] for r in ctl_rows if r["sess"] == s and r.get("atr")])
    for r in ev_rows + ctl_rows:
        a = r.get("atr")
        ed = atr_edges[r["sess"]]
        r["atr_t"] = None if not a or not ed else (1 if a <= ed[0] else (2 if a <= ed[1] else 3))
    out("TFS-H3 ATR tercile edges (USD, per session, from %d control rows with an engine ATR): %s"
        % (sum(1 for r in ctl_rows if r.get("atr")), "  ".join("%s %.2f / %.2f" % (s, atr_edges[s][0], atr_edges[s][1]) for s in tc.SESSIONS)))

    res_all = {}
    for arm, field, title in (("d", "ev", "PRIMARY: placed levels (TFS-8 (d)), house net EV per trade"),
                              ("c", "ev", "DESCRIPTIVE: ATR-fallback geometry (spec section 4.8 / TFS-8 (c)), house net EV per trade"),
                              ("d", "fh", "DESCRIPTIVE: fixed-horizon mark at the window end, net of fee (from the arm-d entries)")):
        primary = (arm == "d" and field == "ev")
        P = view(prim, arm, field)
        Cn = view(ctl_rows, arm, field)
        isF = lambda q: q["r"]["cls"] == "FLIP"
        isN = lambda q: q["r"]["cls"] != "FLIP"
        out("")
        out("=== S2%s. %s ===" % ("" if primary else ("c" if arm == "c" else "f"), title))
        out("fee %.1f bps round trip (maker/maker); windows NY 15 min, ASIA/LONDON 45 min; bootstrap %d resamples of whole UTC days, seed %d" % (fee, R, SEED))
        if field == "ev":
            out("")
            out("| Arm | Session | n | Success % | Timeout % | Gross breakeven % | Net breakeven % | Net edge pp | Net EV per trade [95 % CI] | SD bps |")
            out("|---|---|---|---|---|---|---|---|---|---|")
            for gname, fn in (("FLIP", lambda q: q["r"]["cls"] == "FLIP"), ("NO-FLIP", lambda q: q["r"]["cls"] != "FLIP"),
                              ("BALANCED", lambda q: q["r"]["cls"] == "BALANCED"), ("WITH", lambda q: q["r"]["cls"] == "WITH"),
                              ("CONTROL", None)):
                for s in ("ALL",) + tc.SESSIONS:
                    src = Cn if fn is None else [q for q in P if fn(q)]
                    rs = [q for q in src if s == "ALL" or q["r"]["sess"] == s]
                    e = explain(rs, fee)
                    if e is None:
                        out("| %s | %s | 0 | n/a | n/a | n/a | n/a | n/a | n/a | n/a |" % (gname, s)); continue
                    b = desc_mean(rs)
                    out("| %s | %s | %d%s | %.1f | %.1f | %.1f | %.1f | %+.1f | %s | %.1f |" % (
                        gname, s, e[0], "" if e[0] >= FLOOR else " (<100)", e[1], e[6], e[2], e[3], e[4], ci3(b), e[5]))
        # ---- the three registered tests
        s1 = lambda q: (q["r"]["sess"], q["r"]["side"])
        s3 = lambda q: (q["r"]["sess"], q["r"]["side"], q["r"]["atr_t"] or 0)
        tests = {"TFS-H1": arm_rw(P, isF, isN, s1, "TFS-H1 FLIP - NO-FLIP", "H1 " + arm + field),
                 "TFS-H2": mean_test([q for q in P if isF(q)], "TFS-H2 FLIP vs 0", "H2 " + arm + field),
                 "TFS-H3": arm_rw([q for q in P if isF(q)] + Cn, isF, lambda q: q["r"]["cls"] == "CONTROL", s3, "TFS-H3 FLIP - CONTROL", "H3 " + arm + field)}
        if not primary:
            out("")
            out("| Test | FULL d [95 %% CI] (n) | H1 half d (n) | H2 half d (n) | boot p | (descriptive: no Holm, no label) |")
            out("|---|---|---|---|---|---|")
            for t in ("TFS-H1", "TFS-H2", "TFS-H3"):
                x = tests[t]
                out("| %s | %s (%s) | %s (%s) | %s (%s) | %.4f | - |" % (t, ci3(x["res"]["FULL"]), ns(x["res"]["FULL"]), ci3(x["res"]["H1"]), ns(x["res"]["H1"]),
                                                                       ci3(x["res"]["H2"]), ns(x["res"]["H2"]), boot_p(x["draws"])))
            res_all[(arm, field)] = tests
            continue
        holm(tests, 3)
        # trailing-tercile sensitivity (TFS-7): classes from the trailing 90-day terciles; rows without enough trailing
        # references are left out of the sensitivity run only
        Pt = [q for q in P if q["r"]["trail"] in ("FLIP", "BALANCED", "WITH")]
        tF = lambda q: q["r"]["trail"] == "FLIP"
        tN = lambda q: q["r"]["trail"] in ("BALANCED", "WITH")
        sens = {"TFS-H1": arm_rw(Pt, tF, tN, s1, "sens H1", "sens"),
                "TFS-H2": mean_test([q for q in Pt if tF(q)], "sens H2", "sens"),
                "TFS-H3": arm_rw([q for q in Pt if tF(q)] + Cn, tF, lambda q: q["r"]["cls"] == "CONTROL", s3, "sens H3", "sens")}
        out("")
        out("Holm across TFS-H1, TFS-H2, TFS-H3, familywise 0.05 (ruling A4L-9 (b)). Label = the census rule (TFS-10), copied.")
        out("'Full-span Holm' = the step-down had not stopped AND the Holm-adjusted FULL CI excludes 0 (TFS-15: the best attainable result).")
        out("")
        out("| Test | Strata | A rows dropped (stratum without B) | FULL d [95 % CI] (nA/nB) | H1 half d (n) | H2 half d (n) | boot p | Holm alpha | Holm-adjusted FULL CI | Full-span Holm | Trailing-tercile FULL d (n) | Label |")
        out("|---|---|---|---|---|---|---|---|---|---|---|---|")
        for t in ("TFS-H1", "TFS-H2", "TFS-H3"):
            x, sv = tests[t], sens[t]
            lab = finalize_label(x, sv)
            out("| %s | %d | %d | %s (%s) | %s (%s) | %s (%s) | %.4f | %.4f | [%s, %s] | %s | %s (%s) | %s |" % (
                t, x["strata"], x["a_dropped"], ci3(x["res"]["FULL"]), ns(x["res"]["FULL"]), ci3(x["res"]["H1"]), ns(x["res"]["H1"]),
                ci3(x["res"]["H2"]), ns(x["res"]["H2"]), x["p"], x["alpha"], f1(x["hci"][0]), f1(x["hci"][1]),
                "PASSED" if x["reject"] else "not passed", ci3(sv["res"]["FULL"]), ns(sv["res"]["FULL"]), lab))
        res_all[(arm, field)] = tests
        res_all["sens"] = sens
        descriptive(prim, ev_rows, ctl_rows, P, Cn, fee, out)
    return res_all


def descriptive(prim, ev_rows, ctl_rows, P, Cn, fee, out):
    """TFS-H4 and the splits of spec section 4.10: CIs printed, NEVER labelled, raw (not re-weighted) differences."""
    out("")
    out("=== S3. Splits (TFS-H4): DESCRIPTIVE ONLY - CIs printed, no label, no significance claim; raw differences ===")
    isF = lambda q: q["r"]["cls"] == "FLIP"

    def row(lab, sel, ctl_sel=None):
        f = [q for q in P if isF(q) and sel(q)]
        nf = [q for q in P if not isF(q) and sel(q)]
        c = [q for q in Cn if ctl_sel(q)] if ctl_sel is not None else None
        bf, bn = desc_mean(f), desc_mean(nf)
        d = desc_diff(f, nf) if f and nf else (float("nan"),) * 3
        cc = ci3(desc_mean(c)) + " (%d)" % len(c) if c is not None else "-"
        out("| %s | %s (%d) | %s (%d) | %s | %s |" % (lab, ci3(bf), len(f), ci3(bn), len(nf), ci3(d), cc))

    out("")
    out("| Split | FLIP net EV [95 % CI] (n) | NO-FLIP net EV (n) | FLIP - NO-FLIP raw | CONTROL net EV (n) |")
    out("|---|---|---|---|---|")
    for per in tc.PERIODS:
        row("period " + per, lambda q, p_=per: q["r"]["per"] == p_, lambda q, p_=per: q["r"]["per"] == p_)
    for s in tc.SESSIONS:
        row("session " + s, lambda q, s_=s: q["r"]["sess"] == s_, lambda q, s_=s: q["r"]["sess"] == s_)
    for sd in ("LONG", "SHORT"):
        row("side " + sd + (" (fade long)" if sd == "LONG" else " (fade short)"), lambda q, d_=sd: q["r"]["side"] == d_, lambda q, d_=sd: q["r"]["side"] == d_)
    for wk in (False, True):
        row("weekend" if wk else "weekday", lambda q, w_=wk: (q["r"]["day"].weekday() >= 5) == w_, lambda q, w_=wk: (q["r"]["day"].weekday() >= 5) == w_)
    row("500-trade window still fires at D: yes", lambda q: q["r"]["fires_at_D"])
    row("500-trade window still fires at D: no", lambda q: not q["r"]["fires_at_D"])
    row("episode also fired on the other side", lambda q: q["r"]["two_sided"])
    rs_ed = tc.tercile_edges([q["r"]["resets"] for q in P])
    for k, (lab, fn) in enumerate((("anchor resets <= %d" % rs_ed[0], lambda x: x <= rs_ed[0]),
                                   ("anchor resets %d < x <= %d" % rs_ed, lambda x: rs_ed[0] < x <= rs_ed[1]),
                                   ("anchor resets > %d" % rs_ed[1], lambda x: x > rs_ed[1]))):
        row(lab, lambda q, f_=fn: f_(q["r"]["resets"]))
    out("")
    out("| Class | Net EV [95 % CI] (n) | minus FLIP, raw |")
    out("|---|---|---|")
    F = [q for q in P if isF(q)]
    for cl in ("BALANCED", "WITH"):
        g = [q for q in P if q["r"]["cls"] == cl]
        out("| %s | %s (%d) | %s |" % (cl, ci3(desc_mean(g)), len(g), ci3(desc_diff(g, F))))
    out("")
    out("| Trailing 90-day class (primary population) | Net EV [95 % CI] (n) |")
    out("|---|---|")
    for cl in ("FLIP", "BALANCED", "WITH", "too_few_trailing_ref"):
        g = [q for q in P if q["r"]["trail"] == cl]
        out("| %s | %s (%d) |" % (cl, ci3(desc_mean(g)), len(g)))
    # variants: each with its own D, entry and placed levels (arm d)
    out("")
    out("| Variant (own D, entry, levels) | FLIP net EV (n) | NO-FLIP net EV (n) | FLIP - NO-FLIP raw |")
    out("|---|---|---|---|")
    for name in [v[0] for v in tc.VARIANTS][1:]:
        V = view([r for r in ev_rows if r["var"] == name], "d")
        f = [q for q in V if q["r"]["cls"] == "FLIP"]
        nf = [q for q in V if q["r"]["cls"] != "FLIP"]
        out("| %s terciles | %s (%d) | %s (%d) | %s |" % (name, ci3(desc_mean(f)), len(f), ci3(desc_mean(nf)), len(nf), ci3(desc_diff(f, nf))))
        if name == "TFI30":
            f = [q for q in V if q["r"]["eng"] == "FLIP"]
            nf = [q for q in V if q["r"]["eng"] != "FLIP"]
            out("| TFI30 engine +-0.15 | %s (%d) | %s (%d) | %s |" % (ci3(desc_mean(f)), len(f), ci3(desc_mean(nf)), len(nf), ci3(desc_diff(f, nf))))
    # TFS-H4: OLS slope of net EV on fade-signed I, and its terciles (primary population)
    for q in P:
        q["x"] = q["r"]["I"] * (1.0 if q["r"]["side"] == "LONG" else -1.0)

    def slope(s, c):
        n = c[0]
        den = n * s[3] - s[0] * s[0]
        return (n * s[2] - s[0] * s[1]) / den if n > 2 and den != 0 else float("nan")
    b = boot(P, lambda q: [(0, q["x"]), (1, q["ev_main"]), (2, q["x"] * q["ev_main"]), (3, q["x"] * q["x"])], 4, slope)
    out("")
    out("TFS-H4: OLS slope of net EV (bps) on fade-signed I (x +1 LONG event, x -1 SHORT event), primary population n %d: %s bps per unit I"
        % (len(P), ci3(b)))
    ed = tc.tercile_edges([q["x"] for q in P])
    out("| Fade-signed I tercile (edges %.4f / %.4f) | Net EV [95 %% CI] (n) |" % ed)
    out("|---|---|")
    for lab, fn in (("low", lambda x: x <= ed[0]), ("mid", lambda x: ed[0] < x <= ed[1]), ("high", lambda x: x > ed[1])):
        g = [q for q in P if fn(q["x"])]
        out("| %s | %s (%d) |" % (lab, ci3(desc_mean(g)), len(g)))
    # placed geometry, for reading the numbers
    out("")
    out("| Arm | n | target reason shares | stop reason shares | target bps p50 | stop bps p50 | arm-c target / stop bps p50 | share target < min-move floor %.1f bps (not applied) |" % MIN_MOVE[0])
    out("|---|---|---|---|---|---|---|---|")
    for gname, g in (("FLIP", [q for q in P if isF(q)]), ("NO-FLIP", [q for q in P if not isF(q)]), ("CONTROL", Cn)):
        lvr = [q["r"]["lv"] for q in g]
        tr = Counter(x["target_reason"] for x in lvr)
        sr = Counter(x["stop_reason"] for x in lvr)
        t = sorted(q["o"]["t"] for q in g)
        s = sorted(q["o"]["s"] for q in g)
        cc = [q["r"]["o"]["c"] for q in g if "c" in q["r"]["o"]]
        out("| %s | %d | %s | %s | %.1f | %.1f | %.1f / %.1f | %.3f |" % (
            gname, len(g), " ".join("%s %.2f" % (k, v / len(g)) for k, v in tr.most_common()),
            " ".join("%s %.2f" % (k, v / len(g)) for k, v in sr.most_common()), pctl(t, 50), pctl(s, 50),
            pctl(sorted(x["t"] for x in cc), 50), pctl(sorted(x["s"] for x in cc), 50),
            sum(1 for x in t if x < MIN_MOVE[0]) / max(1, len(t))))


# ---------------------------------------------------------------- the registered run
def run(opt):
    t_start = time.time()
    lines = []

    def out(s=""):
        print(s)
        sys.stdout.flush()
        lines.append(s)
    fee, sl = check_pins(opt.settings)
    out("Liquidation x TRADE-FLOW flip - SESSION 2 outcome read. %s" % SPEC)
    out("pins (tracked settings.json v%d): fee %.1f bps maker/maker; ATR period 7; fallback target x1.75 NY x2.0 LONDON x1.25 ASIA; stop x1.6; structural_levels %s"
        % (PINS["version"], fee, json.dumps(sl, sort_keys=True)))
    os.makedirs(opt.workdir, exist_ok=True)
    cfgdir = os.path.join(opt.workdir, "cfg")
    os.makedirs(cfgdir, exist_ok=True)
    cfg_copy = os.path.join(cfgdir, "settings.json")
    with open(opt.settings, "rb") as a_, open(cfg_copy, "wb") as b_:
        b_.write(a_.read())
    # ---- A
    A = phase_a(opt, out=out)
    ev_rows, ctl_rows = build_rows(A)
    out("rows: event-variant rows %d (primary population %d), control rows %d"
        % (len(ev_rows), sum(1 for r in ev_rows if r["var"] == tc.PRIMARY and r["pop"]), len(ctl_rows)))
    # ---- B
    entries, nb = phase_b(A["paths"], opt.workdir, ev_rows, ctl_rows, out=out)
    if nb != A["cnt"]["trades"]:
        raise SystemExit("STOP: phase B read %d trades, phase A %d" % (nb, A["cnt"]["trades"]))
    # ---- L
    lv = phase_l(opt.workdir, ev_rows + ctl_rows, entries, cfg_copy, out=out)
    for r in ev_rows + ctl_rows:
        x = lv.get(r["key"])
        r["lv"] = x
        r["atr"] = float(x["atr"]) if x is not None and x["status"] == "ok" else None
    # ---- C
    walks = make_walks(ev_rows + ctl_rows, entries, lv)
    cnt = Counter()
    nc = walk_pass(tc.fenced_rows(A["paths"], cnt), walks)
    out("phase C: trades walked %d; walks %d; resolved success %d, stop %d, timeout %d; no trade in the window %d"
        % (nc, len(walks), sum(1 for w in walks if w.res == "success"), sum(1 for w in walks if w.res == "stop"),
           sum(1 for w in walks if w.res == "timeout"), sum(1 for w in walks if w.drop)))
    if nc != A["cnt"]["trades"]:
        raise SystemExit("STOP: phase C read %d trades, phase A %d" % (nc, A["cnt"]["trades"]))
    # ---- S
    phase_s(ev_rows, ctl_rows, fee, out=out)
    out("")
    out("runtime %.0f s" % (time.time() - t_start))
    with open(os.path.join(opt.workdir, "report.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


# ---------------------------------------------------------------- feasibility (TFS-8 (d)): tape vs venue, before outcomes
def feasibility(opt):
    """Tape-built candles vs the venue's chart candles (backtest_data, 2026-01 .. 2026-06, pre-fence), and the levels each
    source places at every 15-min instant (both sides, entry = the last completed tape 1m close). No forward price, no
    outcome: only prices before each instant are used."""
    fee, sl = check_pins(opt.settings)
    wd = os.path.join(opt.workdir, "feas")
    os.makedirs(wd, exist_ok=True)
    cfg_copy = os.path.join(wd, "settings.json")
    with open(opt.settings, "rb") as a_, open(cfg_copy, "wb") as b_:
        b_.write(a_.read())
    # the event decision instants D (phase A, outcome-blind; H-3 re-checked) inside the venue-candle months
    A = phase_a(opt)
    ev_rows, _ = build_rows(A)
    ip = os.path.join(wd, "instants.csv")
    with open(ip, "w", encoding="utf-8", newline="\n") as f:
        f.write("d_ms,hour,is_long\n")
        for r in ev_rows:
            if r["var"] == tc.PRIMARY and r["pop"]:
                f.write("%d,%d,%d\n" % (r["D"], r["hour"], 1 if is_long(r) else 0))
    paths = [os.path.join(opt.store, "trades_2026-%02d.csv" % m) for m in range(1, 7)]
    phase_b(paths, wd, [], [])
    cp = subprocess.run(["dotnet", LEVEL_DLL, "compare", "--settings", cfg_copy, "--candles", wd, "--venue", opt.venue,
                         "--months", ",".join("2026-%02d" % m for m in range(1, 7)), "--from", "2026-02-02", "--to", "2026-07-01",
                         "--instants", ip, "--out", os.path.join(wd, "compare.txt")], capture_output=True, text=True)
    print(cp.stdout)
    if cp.returncode != 0:
        raise SystemExit(cp.stderr[-2000:])


# ---------------------------------------------------------------- selftest (synthetic data, exact expected values)
def selftest(out=print):
    global R
    fails = []

    def check(name, got, want):
        ok = got == want
        out("  %-74s %s  got=%r want=%r" % (name, "PASS" if ok else "FAIL", got, want))
        if not ok:
            fails.append(name)

    def near(a, b, tol=1e-9):
        return a is not None and b is not None and abs(a - b) <= tol

    tmp = tempfile.mkdtemp(prefix="tfsread_")
    t0 = tc.ms(datetime(2025, 3, 3, 14, 0, 0))     # a 15-min boundary (so 1m, 3m and 5m buckets align)

    def P(px, amt=100.0, d="buy", fl="none"):
        return [None, repr(float(px)), repr(float(amt)), d, fl]

    out("SELFTEST: candles (phase B)")
    trades = [(t0 + 1000, P(10)), (t0 + 2000, P(12)), (t0 + 3000, P(9)), (t0 + 4000, P(11)),   # minute 0
              (t0 + 120500, P(20)),                                                            # minute 2 (minute 1 empty)
              (t0 + 240500, P(30)), (t0 + 250000, P(29))]                                      # minute 4 (minute 3 empty)
    os.makedirs(os.path.join(tmp, "c"))
    phase_b([], os.path.join(tmp, "c"), [], [], out=lambda s: None, rows_iter=iter(trades))

    def rd(res):
        with open(os.path.join(tmp, "c", "candles_%dm.csv" % res)) as f:
            f.readline()
            return [[float(x) for x in line.strip().split(",")] for line in f]
    c1, c3, c5 = rd(1), rd(3), rd(5)
    check("1m: minute 0 OHLC", c1[0][:5], [t0, 10.0, 12.0, 9.0, 11.0])
    check("1m: minute 0 volume = sum(amount / price) BTC", round(c1[0][5], 9), round(100 / 10 + 100 / 12 + 100 / 9 + 100 / 11, 9))
    check("1m: minute 1 (no trade) is flat at the previous close, volume 0", c1[1][1:6], [11.0, 11.0, 11.0, 11.0, 0.0])
    check("1m: candle count (minutes 0..4)", len(c1), 5)
    check("3m: bucket 0 folds minutes 0 and 2 (O 10 H 20 L 9 C 20)", c3[0][:5], [t0, 10.0, 20.0, 9.0, 20.0])
    check("3m: bucket 1 opens at the first TRADE (30), not the flat minute 3", c3[1][:5], [t0 + 180000, 30.0, 30.0, 29.0, 29.0])
    check("5m: one bucket, O 10 H 30 L 9 C 29", c5[0][:5], [t0, 10.0, 30.0, 9.0, 29.0])

    out("SELFTEST: entries (phase B)")
    tr = [(t0 + 1000 * k, P(100 + k)) for k in range(1, 11)]           # ordinals 1..10 at t0+1s .. t0+10s
    tr.insert(5, (t0 + 5000, P(555)))                                  # a second trade at t0+5s: ordinal 6
    D = t0 + 5000
    ev = [{"key": "E.time", "kind": "event", "entry_rule": "time", "D": D, "entry_ord": 5},        # first trade >= D = ordinal 5
          {"key": "E.tfi", "kind": "event", "entry_rule": "trades", "D": t0 + 3000, "entry_ord": 4}]  # 30th trade = ord 3 -> 4
    ct = [{"key": "C.x", "kind": "control", "D": t0 + 7500}]
    os.makedirs(os.path.join(tmp, "e"))
    ent, _ = phase_b([], os.path.join(tmp, "e"), ev, ct, out=lambda s: None, rows_iter=iter(tr))
    check("time rule: entry = first trade with ts >= D (ordinal 5, px 105)", ent["E.time"], (5, D, 105.0))
    check("TFI30: entry = the trade after the 30th (ordinal 4)", ent["E.tfi"][0], 4)
    check("control: entry = first trade >= g + 60 s analogue (t0+8s, ordinal 9)", ent["C.x"][:2], (9, t0 + 8000))
    bad = [dict(ev[0], entry_ord=6)]                                   # an ordinal that is NOT the first trade >= D
    try:
        phase_b([], os.path.join(tmp, "e"), bad, [], out=lambda s: None, rows_iter=iter(tr))
        check("an entry that is not the first trade >= D is a STOP", "no stop", "stop")
    except SystemExit:
        check("an entry that is not the first trade >= D is a STOP", "stop", "stop")

    out("SELFTEST: the walk (phase C)")
    seq = [100, 100.4, 101.0, 99.5, 98.9, 100.2, 100.3, 100.1, 120.0]   # ordinals 1..9, one per second
    tw = [(t0 + 1000 * (k + 1), P(px)) for k, px in enumerate(seq)]
    W = lambda key, o, lg, tgt, stp, end: Walk(key, "d", o, t0 + 1000 * o, seq[o - 1], end, lg, tgt, stp)
    ws = [W("long_success", 1, True, 101.0, 99.0, t0 + 60000),          # 101.0 at ord 3 before 98.9 at ord 5
          W("long_stop", 3, True, 102.0, 99.0, t0 + 60000),             # entry 101.0; 98.9 at ord 5
          W("short_success", 3, False, 99.0, 102.0, t0 + 60000),        # SHORT: target BELOW, 98.9 at ord 5
          W("long_timeout", 6, True, 110.0, 90.0, t0 + 9000),           # trades ord 7, 8 before t0+9s; ord 9 (120) AT the end
          W("no_trade", 9, True, 200.0, 50.0, t0 + 9500),               # nothing after ord 9 before its end
          W("short_stop_overshoot", 7, False, 50.0, 110.0, t0 + 60000)] # entry 100.3; ord 8 = 100.1; ord 9 = 120 >= stop 110
    walk_pass(iter(tw), ws)
    g = {w.key: w for w in ws}
    check("long: target 101.0 reached at ordinal 3 -> success", (g["long_success"].res, g["long_success"].res_ts), ("success", t0 + 3000))
    check("long: stop 99.0 reached first -> stop", g["long_stop"].res, "stop")
    check("short: target 99.0 BELOW entry reached -> success", g["short_success"].res, "success")
    check("timeout: a trade exactly AT the window end is outside it; mark = 100.1", (g["long_timeout"].res, g["long_timeout"].mark, g["long_timeout"].ntr), ("timeout", 100.1, 2))
    check("no trade after the entry inside the window -> drop", g["no_trade"].drop, "no_trade_in_window")
    check("short: price >= stop -> stop", g["short_stop_overshoot"].res, "stop")

    out("SELFTEST: house net EV and the Sigma breakevens")
    fee = 3.0
    o1 = outcome(g["long_success"], fee)
    check("success: +target bps - fee (target 101 from 100 = 100 bps)", round(o1["ev"], 9), round(100.0 - 3.0, 9))
    o2 = outcome(g["long_stop"], fee)
    check("stop: -stop bps - fee (99 from 101)", round(o2["ev"], 9), round(-(2.0 / 101.0 * 1e4) - 3.0, 9))
    o3 = outcome(g["long_timeout"], fee)
    check("timeout: mark - fee, signed for a LONG", round(o3["ev"], 9), round((100.1 - 100.2) / 100.2 * 1e4 - 3.0, 9))
    ws2 = Walk("st", "d", 1, t0, 100.0, t0 + 1, False, 99.0, 101.0)
    ws2.res, ws2.mark = "timeout", 99.5
    check("timeout: mark signed for a SHORT (price fell 50 bps -> +50 - fee)", round(outcome(ws2, fee)["ev"], 9), round(50.0 - 3.0, 9))
    rows = [{"o": {"res": "success", "t": 10.0, "s": 10.0}, "ev_main": 7.0}, {"o": {"res": "stop", "t": 30.0, "s": 10.0}, "ev_main": -13.0}]
    e = explain(rows, fee)
    check("gross breakeven = Sigma stop / Sigma (target + stop) = 20/60", round(e[2], 9), round(100 * 20 / 60.0, 9))
    check("net breakeven = Sigma (stop + fee) / Sigma (target + stop) = 26/60", round(e[3], 9), round(100 * 26 / 60.0, 9))

    out("SELFTEST: sides, rows and the session at T0")
    check("event LONG (long liquidations, cascade down) is traded LONG (the fade)", is_long({"side": "LONG"}), True)
    check("event SHORT is traded SHORT; control SHORT (I < lo) trades SHORT with the flow", is_long({"side": "SHORT"}), False)
    t0e = tc.ms(datetime(2025, 3, 3, 12, 59, 30))                     # LONDON at T0; D falls in the NY hour 13
    evo = a4.Event(t0e, "LONG", "LONDON", 0, 0)
    vv = {}
    for name, mode, Q, L in tc.VARIANTS:
        m = tc.Machine(evo, name, mode, Q, L, t0e)
        m.status, m.D, m.I, m.fires_at_D = "measured", t0e + 60000, 0.5, False
        CLOSE_ORD[id(m)] = 77
        vv[name] = {"m": m, "cls": "FLIP", "eng": "FLIP" if mode == "trades" else None, "ofence": False}
    fake = {"an": {"rows": [{"e": evo, "t0": tc.utc(t0e), "per": "2025H1", "v": vv, "pop": True, "trail": "FLIP"}],
                   "control": [(t0e, "LONDON", "SHORT", False), (t0e, "LONDON", None, False), (t0e, "LONDON", "LONG", True)]}}
    er, cr = build_rows(fake)
    rp = [r for r in er if r["var"] == tc.PRIMARY][0]
    check("row hour = the hour at T0 (12, LONDON), not at D (13, NY)", (rp["hour"], rp["sess"]), (12, "LONDON"))
    check("time variant: entry ordinal = the closing trade's ordinal (77)", rp["entry_ord"], 77)
    check("TFI30: entry ordinal = 30th trade's ordinal + 1 (78)", [r for r in er if r["var"] == "TFI30"][0]["entry_ord"], 78)
    check("control rows: only an outer tercile with the outcome window before the fence", [r["side"] for r in cr], ["SHORT"])
    rl = dict(rp, side="LONG")
    cw = make_walks([rl], {rp["key"]: (77, tc.FENCE_MS - 60000, 100.0)},
                    {rp["key"]: {"status": "ok", "target": "101", "stop": "99", "c_status": "ok", "c_target": "101", "c_stop": "99"}})
    check("a walk whose window ends at or after the fence is dropped (outcome fence)", (len(cw), rl["drop"]["d"]), (0, "outcome_window_crosses_fence"))

    out("SELFTEST: re-weighting, the test shapes, Holm")
    R = 300
    DRAWS.clear(); SEEDC[0] = 0; ALL_LABELS.clear()
    d0 = date(2025, 1, 1)
    rr = []
    for k in range(400):
        day = d0 + timedelta(days=k % 200)
        hf = "H1" if k % 200 < 100 else "H2"
        rr.append({"day": day, "half": hf, "ev_main": 10.0, "g": "A", "st": "X"})        # A only in stratum X
        rr.append({"day": day, "half": hf, "ev_main": 0.0, "g": "B", "st": "X"})
        rr.append({"day": day, "half": hf, "ev_main": 100.0, "g": "B", "st": "Y"})
    x = arm_rw(rr, lambda q: q["g"] == "A", lambda q: q["g"] == "B", lambda q: q["st"], "t", "t")
    check("arm_rw: A - B with B re-weighted to A's strata = 10 - 0 = +10 (raw would be -40)", round(x["res"]["FULL"][0], 9), 10.0)
    check("arm_rw: counts are [A, B] in covered strata", x["res"]["FULL"][3], [400, 400])
    check("arm_rw: CONFIRMED (d > 0) on a constant +10 in both halves", x["label"], "CONFIRMED (d > 0)")
    m_ = mean_test([q for q in rr if q["g"] == "A"], "m", "m")
    check("mean test: FULL point = +10, label CONFIRMED", (round(m_["res"]["FULL"][0], 9), m_["label"]), (10.0, "CONFIRMED (d > 0)"))
    small = mean_test([q for q in rr if q["g"] == "A"][:150], "s", "s")
    check("mean test: half H2 holds 50 rows (< 100) -> no CONFIRMED", small["label"], "H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d > 0)")
    zero = mean_test([dict(q, ev_main=(1.0 if i % 2 else -1.0)) for i, q in enumerate(rr) if q["g"] == "A"], "z", "z")
    res3 = {"a": x, "b": m_, "c": zero}
    holm(res3, 3)
    check("Holm: the two constant effects pass, the zero-mean one does not", (x["reject"], m_["reject"], zero["reject"]), (True, True, False))
    check("finalize_label: a trailing point of the other sign adds THRESHOLD-SENSITIVE",
          finalize_label(dict(x), {"res": {"FULL": (-1.0, 0, 0, [1, 1])}}), "CONFIRMED (d > 0) THRESHOLD-SENSITIVE")
    R = RESAMPLES
    out("SELFTEST %s (%d failure(s))" % ("PASS" if not fails else "FAIL", len(fails)))
    return 0 if not fails else 1


# ---------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser(description="Liquidation x trade-flow flip study, session 2: the outcome read")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--h3", action="store_true", help="phase A only: the H-3 gate (no Price is read)")
    ap.add_argument("--feasibility", action="store_true", help="tape vs venue candles and levels, 2026-02..06 (no outcome)")
    ap.add_argument("--run", action="store_true", help="the registered run: A, B, L, C, S")
    ap.add_argument("--store", default=tc.DEFAULT_STORE)
    ap.add_argument("--store-2023", default=tc.STORE_2023)
    ap.add_argument("--venue", default="backtest_data")
    ap.add_argument("--settings", default="settings.json")
    ap.add_argument("--workdir", default=None)
    opt = ap.parse_args()
    if opt.selftest:
        sys.exit(selftest())
    if opt.h3:
        t0 = time.time()
        A = phase_a(opt)
        ev, ct = build_rows(A)
        print("rows: event-variant rows %d (primary %d), control rows %d" % (len(ev), sum(1 for r in ev if r["var"] == tc.PRIMARY), len(ct)))
        print("runtime %.0f s" % (time.time() - t0))
        return
    if not opt.workdir:
        raise SystemExit("--workdir is required")
    if opt.feasibility:
        feasibility(opt)
        return
    if opt.run:
        run(opt)
        return
    ap.print_help()


if __name__ == "__main__":
    main()
