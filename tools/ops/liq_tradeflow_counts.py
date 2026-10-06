#!/usr/bin/env python3
# tools/ops/liq_tradeflow_counts.py
#
# Liquidation x TRADE-FLOW flip study, SESSION 1: the outcome-blind counts tool.
# Spec of record: docs/liq-tradeflow-flip-study-spec.md. Python 3 standard library only (no numpy).
# Sister study (SEALED): docs/a4-liq-ofi-logged-era-study-spec.md; its event code is IMPORTED from
# tools/ops/a4_liq_ofi_counts.py (iter_store_rows, scan_rows, book, dominant, session_of, the pinned constants).
#
# What it does, in one pass over the dev history store 2025-01-01 -> 2026-07-02 (fence 2026-07-03 00:00 UTC):
#   * builds the sister study's events (large liquidation clusters, onset T0, 30-min de-clustering, D-5 booking,
#     LLS-1 per-session thresholds) with the sister's own scan code;
#   * after each onset, measures the aggressor imbalance of NON-liquidation trades in a flow window that starts
#     after the cascade's last liquidation print (the anchor) and holds no liquidation print (a print resets it);
#   * measures the same imbalance at a 15-min reference grid away from any firing (threshold source and the
#     flow-only control pool);
#   * prints COUNTS ONLY: events per session x period, flow-window coverage and drops, FLIP / BALANCED / WITH per
#     session x period, variants, the control pool, and a power estimate.
#
# OUTCOME-BLIND BY CONSTRUCTION:
#   * From the store it parses ONLY Timestamp, Amount, Direction and Liquidation. The Price, MarkPrice and
#     IndexPrice columns are never converted, so no price before or after any event is read. No candle, no
#     analysis_log.csv, no eval cache is opened.
#   * The flip threshold comes from the reference-grid imbalance distribution (signal side) only.
#
# DATE FENCE (hard): only trades_2025-01.csv .. trades_2026-07.csv are opened, by explicit name. Reading stops at
# the first line whose Timestamp is >= 2026-07-03 00:00:00.000 UTC; that line is tokenised for its timestamp only
# and nothing after it is read. Every trade passed to the scan is asserted < the fence.
#
# RE-REGISTRATION 2026-10-06 (spec section 14; rulings TFS-12 (c), TFS-6 (b); decisions TFS-13, TFS-14):
#   --span extended (the default): TWO stores, read as ONE continuous pass in order -- C:/DeribitData/history-2023-2024
#     (trades_2023-01 .. trades_2024-12) then C:/DeribitData/history (trades_2025-01 .. trades_2026-07, same fence).
#     The TradeSeq seam between the stores is checked first (last seq + 1 == first seq, time not backward); a failed
#     store seam is a STOP. LLS-1 thresholds apply PER ERA by the timestamp of the trade that evaluates the window:
#     2023-2024 its own pooled per-session p90 (D-5), 2025-01-01 onward the ruled LLS-1. Reference grid 5 min.
#   --span registered: session 1 exactly (one store, 2025-01 .. 2026-07, LLS-1 only, 15-min grid). Its output is
#     line-for-line the session-1 H-1 block.
#   The sister's scan code is still imported unchanged: for the pass, a4.LLS1 is swapped for an EraThresholds table
#   (restored afterwards). a4.scan_rows reads LLS1[sess] per trade; the fenced row iterator sets the era first.
#
# Usage (from the repo root):
#   python tools/ops/liq_tradeflow_counts.py                       # re-registered counts, 2023-01-01 -> 2026-07-02
#   python tools/ops/liq_tradeflow_counts.py --span registered     # session-1 counts (parity)
#   python tools/ops/liq_tradeflow_counts.py --selftest            # synthetic data, exact expected values; exit 1 on a failure
#
# Runtime: several minutes (43 store files for the extended span, one pass).

import argparse, json, math, os, statistics, sys, tempfile, time
from bisect import bisect_left, bisect_right
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import a4_liq_ofi_counts as a4   # noqa: E402  (the sister's event code; read-only reuse)

# ---------------------------------------------------------------- pinned constants (spec docs/liq-tradeflow-flip-study-spec.md section 4)
EPOCH = datetime(1970, 1, 1)


def ms(dt):
    return int((dt - EPOCH).total_seconds() * 1000)


START = datetime(2025, 1, 1)
FENCE = datetime(2026, 7, 3)                      # exclusive; 2026-07-03 onward belongs to the SEALED sister study
FENCE_MS = ms(FENCE)
MONTHS = ["%04d-%02d" % (y, m) for y in (2025, 2026) for m in range(1, 13) if (y, m) <= (2026, 7)]
DEFAULT_STORE = "C:/DeribitData/history"

# Flow windows. (name, mode, Q seconds, L seconds-or-trades). The FIRST is the primary (TFS-2, TFS-3).
VARIANTS = [("Q0L60", "time", 0, 60),
            ("Q0L30", "time", 0, 30),
            ("Q0L120", "time", 0, 120),
            ("Q30L60", "time", 30, 60),
            ("TFI30", "trades", 0, 30)]
PRIMARY = VARIANTS[0][0]
N_MIN = 10                                       # unflagged trades a time window needs (TFS-5)
CAP_MS = 30 * 60 * 1000                          # D - T0 must be <= 30 min (TFS-4)
GRID_MS = 15 * 60 * 1000                         # reference grid step (TFS-6)
EXCL_MS = a4.GAP_S * 1000                        # no firing in [g - 30 min, window end) for a reference window
HOLD_MIN = {"NY": 15, "ASIA": 45, "LONDON": 45}  # outcome window by event session (AnalysisConstants max window)
TRAIL_DAYS, TRAIL_MIN_REF = 90, 1000             # trailing-threshold sensitivity (TFS-7)
MIN_N = a4.MIN_N                                 # the census readability floor, 100
FW_ALPHA, M_TESTS = 0.05, 3                      # A4L-9 (b): Holm across all three tests
EFFECTS = a4.EFFECTS
SIGMA_CONS, SIGMA_OPT = a4.SIGMA_CONS, a4.SIGMA_OPT   # published proxies, carried (sister spec section 6)
ATR_SCALE_CARRIED = 2.45                         # sister H-1 pooled event/median ATR scale, carried, NOT measured here

# ---- re-registration 2026-10-06 (spec section 14)
STORE_2023 = "C:/DeribitData/history-2023-2024"
MONTHS_2023 = ["%04d-%02d" % (y, m) for y in (2023, 2024) for m in range(1, 13)]
LLS1_RULED = dict(a4.LLS1)                       # ASIA 69535 LONDON 83250 NY 49724 (2025-01 -> 2026-09 pooled, D-5)
LLS_2023_24 = {"ASIA": 61060, "LONDON": 50939, "NY": 58630}   # derive.py on the 2023-2024 store, pooled, D-5 (TFS-13 (b))
ERAS_REGISTERED = [(ms(datetime(2025, 1, 1)), LLS1_RULED, "2025-01-01 .. 2026-07-02 (LLS-1, ruled)")]
ERAS_EXTENDED = [(ms(datetime(2023, 1, 1)), LLS_2023_24, "2023-01-01 .. 2024-12-31 (2023-2024 store p90)"),
                 (ms(datetime(2025, 1, 1)), LLS1_RULED, "2025-01-01 .. 2026-07-02 (LLS-1, ruled)")]
SPAN = "registered"                              # set by configure(); the selftest runs on the registered defaults
ERAS = ERAS_REGISTERED


def utc(t_ms):
    return EPOCH + timedelta(milliseconds=t_ms)


def period_of(dt):
    if dt >= datetime(2026, 1, 1):
        return "2026H1*"                         # 2026-01-01 -> 2026-07-02 (the two July days fold in here)
    return "%dH%d" % (dt.year, 1 if dt.month <= 6 else 2)


PERIODS = ("2025H1", "2025H2", "2026H1*")
SESSIONS = ("ASIA", "LONDON", "NY")


def configure(span, grid_min=None):
    """Sets the span-level globals. registered = session 1 exactly; extended = the 2026-10-06 re-registration."""
    global SPAN, START, PERIODS, ERAS, GRID_MS
    SPAN = span
    if span == "registered":
        START, ERAS = datetime(2025, 1, 1), ERAS_REGISTERED
        PERIODS = ("2025H1", "2025H2", "2026H1*")
        GRID_MS = (grid_min or 15) * 60 * 1000
    elif span == "extended":
        START, ERAS = datetime(2023, 1, 1), ERAS_EXTENDED
        PERIODS = ("2023H1", "2023H2", "2024H1", "2024H2", "2025H1", "2025H2", "2026H1*")
        GRID_MS = (grid_min or 5) * 60 * 1000    # TFS-6 (b), ruled 2026-10-05
    else:
        raise SystemExit("unknown span %r" % span)


class EraThresholds(dict):
    """The per-era LLS-1 table, installed as a4.LLS1 for one pass. a4.scan_rows reads LLS1[sess] once per evaluated
    trade; fenced_rows calls at(ts) before it yields that trade, so the lookup uses the trade's own era.
    Era i covers [start_i, start_{i+1}): a trade exactly at an era start belongs to the NEW era."""

    def __init__(self, eras):
        super().__init__(eras[0][1])
        self.starts = [e[0] for e in eras]
        self.tabs = [e[1] for e in eras]
        self.n = [0] * len(eras)
        self.i = -1
        self.lo = self.hi = 0

    def at(self, ts):
        if not (self.lo <= ts < self.hi):
            i = bisect_right(self.starts, ts) - 1
            if i < 0:
                raise SystemExit("STOP: a trade before the first era start reached the scan")
            self.i = i
            self.lo = self.starts[i]
            self.hi = self.starts[i + 1] if i + 1 < len(self.starts) else 1 << 62
        self.n[self.i] += 1

    def __getitem__(self, sess):
        return self.tabs[self.i][sess]


def tail_line(path):
    """The last non-empty line of a file, read from the end."""
    with open(path, "rb") as f:
        f.seek(0, os.SEEK_END)
        size = f.tell()
        back = 4096
        while True:
            f.seek(max(0, size - back))
            lines = [x for x in f.read().split(b"\n") if x.strip()]
            if len(lines) >= 2 or back >= size:
                return lines[-1].decode("utf-8").rstrip("\r")
            back *= 2


def first_data_line(path):
    with open(path, encoding="utf-8") as f:
        f.readline()
        return f.readline().rstrip("\r\n")


def seam_check(prev_path, next_path):
    """Last trade of prev_path vs first trade of next_path. Reads Timestamp (col 0) and TradeSeq (col 6) only.
    PASS needs next seq == last seq + 1 (no trade missing, none doubled) and next timestamp >= last timestamp."""
    a, b = tail_line(prev_path).split(","), first_data_line(next_path).split(",")
    lt, ls, ft, fs = int(a[0]), int(a[6]), int(b[0]), int(b[6])
    return (fs == ls + 1 and ft >= lt), (lt, ls, ft, fs)


def check_store_seams(groups, out=print):
    """TFS-14 (a): each store's last file vs the next store's first file must be contiguous by TradeSeq.
    A failure is a STOP (SystemExit) before any count is produced."""
    for k in range(1, len(groups)):
        ok, (lt, ls, ft, fs) = seam_check(groups[k - 1][-1], groups[k][0])
        out("store seam %s -> %s: last seq %d at %s, first seq %d at %s: %s"
            % (os.path.basename(groups[k - 1][-1]), os.path.basename(groups[k][0]), ls, utc(lt), fs, utc(ft), "PASS" if ok else "FAIL"))
        if not ok:
            raise SystemExit("STOP: the store seam failed (TFS-14); no counts are produced")


def engine_tfi_from_settings(path="settings.json"):
    """SHIPPED BEHAVIOUR: indicators.TFI window/threshold, derived from tracked settings.json (read-only)."""
    with open(path, encoding="utf-8") as f:
        s = json.load(f)
    t = s["indicators"]["TFI"]
    return s.get("version"), int(t["window_size"]), float(t["threshold"])


def tercile_edges(vals):
    """Exact rank rule: lo = the ceil(n/3)-th smallest value, hi = the ceil(2n/3)-th smallest (1-based)."""
    s = sorted(vals)
    n = len(s)
    if n == 0:
        return None
    return s[(n + 2) // 3 - 1], s[(2 * n + 2) // 3 - 1]


def classify(side, I, lo, hi):
    """Fade-signed, strict. LONG liqs (cascade down, fade long): flow against the cascade = taker BUYING (I > hi).
    SHORT liqs (cascade up, fade short): flow against = taker SELLING (I < lo)."""
    if side == "LONG":
        return "FLIP" if I > hi else ("WITH" if I < lo else "BALANCED")
    return "FLIP" if I < lo else ("WITH" if I > hi else "BALANCED")


def pctl_int(sorted_vals, q):
    if not sorted_vals:
        return float("nan")
    k = max(1, min(len(sorted_vals), math.ceil(q / 100.0 * len(sorted_vals))))
    return sorted_vals[k - 1]


# ---------------------------------------------------------------- the pass
class Machine:
    """One event's flow window for one variant."""
    __slots__ = ("ev", "var", "mode", "Q", "L", "anchor", "ws", "we", "B", "S", "n", "resets", "status", "D", "I",
                 "fires_at_D")

    def __init__(self, ev, var, mode, Q, L, t0):
        self.ev, self.var, self.mode, self.Q, self.L = ev, var, mode, Q, L
        self.resets = 0
        self.status = self.D = self.I = self.fires_at_D = None
        self.reset(t0)

    def reset(self, t):
        self.anchor = t
        self.ws = t + self.Q * 1000
        self.we = self.ws + self.L * 1000 if self.mode == "time" else None
        self.B = self.S = 0.0
        self.n = 0

    def close(self, D, fires_at_D):
        self.D, self.fires_at_D = D, fires_at_D
        if self.status is None:
            if (self.mode == "time" and self.n < N_MIN) or self.B + self.S <= 0:
                self.status = "thin_flow_window"
            else:
                self.status = "measured"
                self.I = (self.B - self.S) / (self.B + self.S)


def fenced_rows(paths, cnt, thr=None):
    """The sister's row iterator, stopped at the fence. Nothing at or after the fence is passed on.
    thr (an EraThresholds) is pointed at each trade's era before the trade is yielded."""
    for ts, p in a4.iter_store_rows(paths, cnt):
        if ts >= FENCE_MS:
            cnt["fence_stop"] += 1
            return
        if thr is not None:
            thr.at(ts)
        yield ts, p


def run_pass(paths, cnt, variants=VARIANTS, eras=None):
    """eras: [(start_ms, {session: USD}, label), ...] ascending; default = the span's ERAS. The sister's a4.LLS1 is
    swapped for an EraThresholds table for the pass and restored afterwards."""
    thr = EraThresholds(eras if eras is not None else ERAS)
    saved = a4.LLS1
    a4.LLS1 = thr
    try:
        res = _run_pass(paths, cnt, variants, thr)
    finally:
        a4.LLS1 = saved
    for k, n in enumerate(thr.n):
        cnt["era_trades_%d" % k] = n
    return res


def _run_pass(paths, cnt, variants, thr):
    time_vars = [v for v in variants if v[1] == "time"]
    trade_vars = [v for v in variants if v[1] == "trades"]
    ref_Ls = sorted(set(v[3] for v in time_vars))
    machines = []                 # active event machines
    done = []                     # closed machines
    refs = defaultdict(list)      # key -> list of (g, sess, I, D)   key = "L<sec>" or "T<n>"
    refdrop = Counter()
    st = {"last_fire": None, "prev_fires": False, "slot": None, "max_ts": None, "min_ts": None}
    # reference accumulators for the current slot, per L: [open, B, S, n, flagged]
    racc = {L: [False, 0.0, 0.0, 0, False] for L in ref_Ls}
    tacc = {v[3]: [False, 0.0, 0.0, 0, False] for v in trade_vars}   # trade-count refs: [open, B, S, n, flagged]

    def ref_close(key, g, B, S, n, flagged, need_n, D):
        lf = st["last_fire"]
        if flagged:
            refdrop[(key, "flag_in_window")] += 1
        elif lf is not None and lf >= g - EXCL_MS:
            refdrop[(key, "firing_within_30min")] += 1
        elif n < need_n or B + S <= 0:
            refdrop[(key, "thin")] += 1
        else:
            refs[key].append((g, a4.session_of(utc(g).hour), (B - S) / (B + S), D))

    def hook(ts, p, c, fires, onset):
        assert ts < FENCE_MS, "FENCE BREACH: a trade at or after 2026-07-03 00:00 UTC reached the scan"
        if st["min_ts"] is None:
            st["min_ts"] = ts
        st["max_ts"] = ts if st["max_ts"] is None or ts > st["max_ts"] else st["max_ts"]
        flagged = p[4] != "none"
        # ---- event machines (before this trade's firing is recorded: fires_at_D = the last trade before D)
        if machines:
            for m in list(machines):
                if m.mode == "time":
                    if ts >= m.we:
                        m.close(m.we, st["prev_fires"])
                        machines.remove(m); done.append(m)
                        continue
                    if flagged:
                        if ts >= m.anchor:
                            m.reset(ts); m.resets += 1
                            if m.we - m.ev.t0 > CAP_MS:
                                m.status = "not_quiet_within_cap"
                                m.close(None, None)
                                machines.remove(m); done.append(m)
                        else:
                            cnt["flow_flag_out_of_order"] += 1
                    elif ts > m.ws:
                        d = p[3]
                        if d == "buy":
                            m.B += float(p[2])
                        elif d == "sell":
                            m.S += float(p[2])
                        else:
                            cnt["flow_bad_direction"] += 1
                            continue
                        m.n += 1
                else:   # trades mode
                    if flagged:
                        if ts >= m.anchor:
                            m.reset(ts); m.resets += 1
                    elif ts > m.ws:
                        d = p[3]
                        if d == "buy":
                            m.B += float(p[2]); m.n += 1
                        elif d == "sell":
                            m.S += float(p[2]); m.n += 1
                        else:
                            cnt["flow_bad_direction"] += 1
                        if m.n >= m.L:
                            if ts - m.ev.t0 > CAP_MS:
                                m.status = "not_quiet_within_cap"
                            m.close(ts, fires)
                            machines.remove(m); done.append(m)
                            continue
                    if ts - m.ev.t0 > CAP_MS:
                        m.status = "not_quiet_within_cap"
                        m.close(None, None)
                        machines.remove(m); done.append(m)
        # ---- reference grid, time windows: close before this trade's firing is recorded
        g = ts - ts % GRID_MS
        off = ts - g
        slot = st["slot"]
        grid_ok = True
        if slot is None or g > slot:
            if slot is not None:
                for L in ref_Ls:
                    a = racc[L]
                    if a[0]:
                        ref_close("L%d" % L, slot, a[1], a[2], a[3], a[4], N_MIN, slot + L * 1000)
                for n_, a in tacc.items():
                    if a[0]:
                        refdrop[("T%d" % n_, "thin")] += 1
            st["slot"] = slot = g
            for L in ref_Ls:
                racc[L] = [True, 0.0, 0.0, 0, False]
            for n_ in tacc:
                tacc[n_] = [True, 0.0, 0.0, 0, False]
        elif g < slot:
            cnt["grid_out_of_order_skipped"] += 1
            grid_ok = False
        if grid_ok:
            for L in ref_Ls:
                a = racc[L]
                if a[0] and off >= L * 1000:
                    ref_close("L%d" % L, slot, a[1], a[2], a[3], a[4], N_MIN, slot + L * 1000)
                    a[0] = False
        # ---- record this trade's firing
        if fires:
            st["last_fire"] = ts
        st["prev_fires"] = fires
        # ---- reference accumulation
        if grid_ok:
            for L in ref_Ls:
                a = racc[L]
                if a[0] and off < L * 1000:
                    if flagged:
                        a[4] = True
                    elif p[3] == "buy":
                        a[1] += float(p[2]); a[3] += 1
                    elif p[3] == "sell":
                        a[2] += float(p[2]); a[3] += 1
            for n_, a in tacc.items():
                if a[0]:
                    if flagged:
                        a[4] = True
                    elif p[3] == "buy":
                        a[1] += float(p[2]); a[3] += 1
                    elif p[3] == "sell":
                        a[2] += float(p[2]); a[3] += 1
                    if a[3] >= n_ or a[4]:
                        ref_close("T%d" % n_, slot, a[1], a[2], a[3], a[4], n_, ts)
                        a[0] = False
        # ---- a new onset starts its machines after its own trade
        if onset is not None:
            for name, mode, Q, L in variants:
                machines.append(Machine(onset, name, mode, Q, L, ts))

    events, spans = a4.scan_rows(fenced_rows(paths, cnt, thr), cnt, hook)
    for m in machines:
        m.status = "flow_window_crosses_fence"
        done.append(m)
    by_ev = defaultdict(dict)
    for m in done:
        by_ev[id(m.ev)][m.var] = m
    return events, spans, by_ev, refs, refdrop, st


# ---------------------------------------------------------------- post-pass: thresholds, classes, population
def analyse(events, by_ev, refs, engine_thr, variants=VARIANTS):
    out = {"edges": {}, "rows": [], "control": [], "trail": Counter()}
    keymap = {}
    for name, mode, Q, L in variants:
        keymap[name] = ("L%d" % L) if mode == "time" else ("T%d" % L)
    for name in keymap:
        for s in SESSIONS:
            out["edges"][(name, s)] = tercile_edges([r[2] for r in refs[keymap[name]] if r[1] == s])
    # trailing references (primary): per session, sorted by D
    pk = keymap[PRIMARY]
    tr = {s: sorted((r[3], r[2]) for r in refs[pk] if r[1] == s) for s in SESSIONS}
    trD = {s: [x[0] for x in tr[s]] for s in SESSIONS}
    for e in events:
        rec = {"e": e, "t0": utc(e.t0), "per": period_of(utc(e.t0)), "v": {}}
        for name, mode, Q, L in variants:
            m = by_ev[id(e)][name]
            ed = out["edges"][(name, e.sess)]
            cls = classify(e.side, m.I, ed[0], ed[1]) if m.status == "measured" and ed else None
            eng = None
            if mode == "trades" and m.status == "measured":
                eng = classify(e.side, m.I, -engine_thr, engine_thr)
            ofence = None
            if m.status == "measured":
                ofence = m.D + HOLD_MIN[e.sess] * 60000 >= FENCE_MS
            rec["v"][name] = {"m": m, "cls": cls, "eng": eng, "ofence": ofence}
        mp = rec["v"][PRIMARY]
        rec["pop"] = mp["cls"] is not None and not mp["ofence"]
        # trailing-threshold sensitivity (primary)
        if mp["cls"] is not None:
            D = mp["m"].D
            i1 = bisect_left(trD[e.sess], D)
            i0 = bisect_left(trD[e.sess], D - TRAIL_DAYS * 86400000)
            vals = [x[1] for x in tr[e.sess][i0:i1]]
            if len(vals) < TRAIL_MIN_REF:
                rec["trail"] = "too_few_trailing_ref"
            else:
                lo, hi = tercile_edges(vals)
                rec["trail"] = classify(e.side, mp["m"].I, lo, hi)
        else:
            rec["trail"] = None
        out["rows"].append(rec)
    # control pool: primary reference windows in an outer tercile, traded with the flow
    for g, s, I, D in refs[pk]:
        ed = out["edges"][(PRIMARY, s)]
        side = "LONG" if I > ed[1] else ("SHORT" if I < ed[0] else None)
        ofence = D + HOLD_MIN[s] * 60000 >= FENCE_MS
        out["control"].append((g, s, side, ofence))
    return out


# ---------------------------------------------------------------- report
def fmt(x, nd=1):
    return "n/a" if x is None or (isinstance(x, float) and math.isnan(x)) else ("%.*f" % (nd, x))


def report(events, spans, by_ev, refs, refdrop, st, an, cnt, tfi_info, out=print):
    rows = an["rows"]
    out("=== 0. Inputs and pins ===")
    out("fence: trades < %s UTC (%d ms); first trade read %s; last trade read %s; fence stop hit: %s"
        % (FENCE, FENCE_MS, utc(st["min_ts"]), utc(st["max_ts"]), "yes" if cnt["fence_stop"] else "NO"))
    out("assert max trade read < fence: %s" % ("PASS" if st["max_ts"] < FENCE_MS else "FAIL"))
    if len(ERAS) == 1:
        out("LLS-1 thresholds (USD, strict >): " + " ".join("%s=%d" % (k, a4.LLS1[k]) for k in SESSIONS)
            + "  window=%d trades  dominance=%.1f  de-cluster gap=%d min" % (a4.WIN, a4.DOM, a4.GAP_S // 60))
    else:
        out("LLS-1 thresholds per era (USD, strict >; era = the evaluating trade's timestamp; TFS-13 (b))"
            + "  window=%d trades  dominance=%.1f  de-cluster gap=%d min" % (a4.WIN, a4.DOM, a4.GAP_S // 60))
        for k, (s0, tab, lab) in enumerate(ERAS):
            out("  era %d from %s  %-44s " % (k + 1, utc(s0).date(), lab) + " ".join("%s=%d" % (x, tab[x]) for x in SESSIONS)
                + "  trades scanned %d" % cnt["era_trades_%d" % k])
    out("store: trades=%d  flags T=%d M=%d MT=%d  unrecognised skipped=%d  out-of-order=%d"
        % (cnt["trades"], cnt["flag_T"], cnt["flag_M"], cnt["flag_MT"], cnt["unrecognised_skipped"], cnt["store_out_of_order"]))
    out("flow: variants %s (primary %s)  N_MIN=%d  cap D-T0<=%d min  grid=%d min  ref exclusion: firing in [g-%d min, window end)"
        % (",".join(v[0] for v in VARIANTS), PRIMARY, N_MIN, CAP_MS // 60000, GRID_MS // 60000, EXCL_MS // 60000))
    out("engine TFI (tracked settings.json v%s): window %d trades, threshold %.2f (TFI30 descriptive class only)" % tfi_info)
    out("hold window for the outcome-fence check: NY %d min, ASIA %d min, LONDON %d min"
        % (HOLD_MIN["NY"], HOLD_MIN["ASIA"], HOLD_MIN["LONDON"]))
    out("counters: flow_bad_direction=%d  flow_flag_out_of_order=%d  grid_out_of_order_skipped=%d"
        % (cnt["flow_bad_direction"], cnt["flow_flag_out_of_order"], cnt["grid_out_of_order_skipped"]))
    out("firing spans=%d  events (onsets)=%d" % (len(spans), len(events)))

    out("")
    out("=== 1. Events per session x period (LONG/SHORT = liquidated side; onset T0) ===")
    out("period    " + "".join("%-14s" % (s + " L/S") for s in SESSIONS) + "all   two-sided")
    for per in PERIODS:
        rr = [r for r in rows if r["per"] == per]
        c = Counter((r["e"].sess, r["e"].side) for r in rr)
        out("%-9s " % per + "".join("%-14s" % ("%d/%d" % (c[(s, "LONG")], c[(s, "SHORT")])) for s in SESSIONS)
            + "%-5d %d" % (len(rr), sum(1 for r in rr if r["e"].two_sided)))
    days = (FENCE.date() - START.date()).days
    out("all       %d events over %d days = %.2f per day" % (len(rows), days, len(rows) / days))

    out("")
    out("=== 2. Flow-window coverage, primary %s (counts per session x period) ===" % PRIMARY)
    keys = ("measured", "not_quiet_within_cap", "thin_flow_window", "flow_window_crosses_fence")
    out("session period    events  " + "  ".join("%-9s" % k[:9] for k in keys) + "  outcome_fence  population")
    for s in SESSIONS + ("ALL",):
        for per in PERIODS + ("all",):
            rr = [r for r in rows if (s == "ALL" or r["e"].sess == s) and (per == "all" or r["per"] == per)]
            c = Counter(r["v"][PRIMARY]["m"].status for r in rr)
            of = sum(1 for r in rr if r["v"][PRIMARY]["ofence"])
            out("%-7s %-9s %6d  " % (s, per, len(rr)) + "  ".join("%9d" % c[k] for k in keys)
                + "  %13d  %10d" % (of, sum(1 for r in rr if r["pop"])))
    meas = [r for r in rows if r["v"][PRIMARY]["m"].status == "measured"]
    for s in SESSIONS + ("ALL",):
        rr = [r for r in meas if s == "ALL" or r["e"].sess == s]
        if not rr:
            continue
        lag = sorted((r["v"][PRIMARY]["m"].D - r["e"].t0) / 1000.0 for r in rr)
        rs = sorted(r["v"][PRIMARY]["m"].resets for r in rr)
        nn = sorted(r["v"][PRIMARY]["m"].n for r in rr)
        out("%-7s measured: D-T0 s p50/p90/max %s/%s/%s  liquidation-print resets p50/p90 %d/%d  trades in window p10/p50 %d/%d  500-window still fires at D %d/%d"
            % (s, fmt(pctl_int(lag, 50), 0), fmt(pctl_int(lag, 90), 0), fmt(lag[-1], 0), pctl_int(rs, 50), pctl_int(rs, 90),
               pctl_int(nn, 10), pctl_int(nn, 50), sum(1 for r in rr if r["v"][PRIMARY]["m"].fires_at_D), len(rr)))

    out("")
    out("=== 3. Reference grid (signal side): windows kept and tercile edges of signed imbalance I ===")
    for name, mode, Q, L in VARIANTS:
        key = ("L%d" % L) if mode == "time" else ("T%d" % L)
        for s in SESSIONS:
            vals = [r[2] for r in refs[key] if r[1] == s]
            ed = an["edges"][(name, s)]
            if not vals or not ed:
                out("%-7s %-7s kept 0" % (name, s)); continue
            up = sum(1 for v in vals if v > ed[1]); dn = sum(1 for v in vals if v < ed[0])
            out("%-7s %-7s kept %6d  edges lo %+.4f hi %+.4f  shares <lo %.3f  mid %.3f  >hi %.3f  |I|=1 share %.3f"
                % (name, s, len(vals), ed[0], ed[1], dn / len(vals), 1 - (up + dn) / len(vals), up / len(vals),
                   sum(1 for v in vals if abs(v) >= 1.0) / len(vals)))
        out("%-7s drops: " % name + "  ".join("%s=%d" % (k[1], v) for k, v in sorted(refdrop.items()) if k[0] == key))
    vals = [r[2] for r in refs["T%d" % [v for v in VARIANTS if v[1] == "trades"][0][3]]]
    if vals:
        thr = tfi_info[2]
        out("TFI30 reference share beyond the engine threshold: > +%.2f %.3f   < -%.2f %.3f"
            % (thr, sum(1 for v in vals if v > thr) / len(vals), thr, sum(1 for v in vals if v < -thr) / len(vals)))

    out("")
    out("=== 4. Flip state, primary %s, population (measured, outcome window before the fence) ===" % PRIMARY)
    pop = [r for r in rows if r["pop"]]
    out("session side    period     FLIP  BALANCED  WITH  | NO-FLIP")
    for s in SESSIONS + ("ALL",):
        for sd in ("LONG", "SHORT", "both"):
            for per in PERIODS + ("all",):
                rr = [r for r in pop if (s == "ALL" or r["e"].sess == s) and (sd == "both" or r["e"].side == sd)
                      and (per == "all" or r["per"] == per)]
                c = Counter(r["v"][PRIMARY]["cls"] for r in rr)
                if per != "all" and sd != "both" and s != "ALL":
                    continue        # keep the table short: session x side totals, session x period, ALL
                out("%-7s %-7s %-9s %5d %9d %5d  | %7d" % (s, sd, per, c["FLIP"], c["BALANCED"], c["WITH"], c["BALANCED"] + c["WITH"]))
    pd_ = sorted(set(r["t0"].date() for r in pop))
    h = len(pd_) // 2
    h1 = set(pd_[:h])
    for lab, sel in (("H1", lambda r: r["t0"].date() in h1), ("H2", lambda r: r["t0"].date() not in h1)):
        rr = [r for r in pop if sel(r)]
        c = Counter(r["v"][PRIMARY]["cls"] for r in rr)
        span = (min(r["t0"].date() for r in rr), max(r["t0"].date() for r in rr)) if rr else ("-", "-")
        out("half %s (first floor(D/2) of %d event days): %s..%s  FLIP %d  NO-FLIP %d" % (lab, len(pd_), span[0], span[1], c["FLIP"], c["BALANCED"] + c["WITH"]))
    wk = Counter(("weekend" if r["t0"].weekday() >= 5 else "weekday", r["v"][PRIMARY]["cls"] == "FLIP") for r in pop)
    out("weekday FLIP/NO-FLIP %d/%d   weekend FLIP/NO-FLIP %d/%d" % (wk[("weekday", True)], wk[("weekday", False)], wk[("weekend", True)], wk[("weekend", False)]))
    out("population events whose episode also fired on the other side (kept; counted only): %d" % sum(1 for r in pop if r["e"].two_sided))

    out("")
    out("=== 5. Variants and threshold sensitivity (descriptive; signal side) ===")
    for name, mode, Q, L in VARIANTS:
        rr = [r for r in rows if r["v"][name]["cls"] is not None and not r["v"][name]["ofence"]]
        c = Counter(r["v"][name]["cls"] for r in rr)
        st_ = Counter(r["v"][name]["m"].status for r in rows)
        line = "%-7s measured %d (cap %d, thin %d, fence %d)  outcome-fence %d  FLIP %d BALANCED %d WITH %d" % (
            name, st_["measured"], st_["not_quiet_within_cap"], st_["thin_flow_window"], st_["flow_window_crosses_fence"],
            sum(1 for r in rows if r["v"][name]["ofence"]), c["FLIP"], c["BALANCED"], c["WITH"])
        if mode == "trades":
            ce = Counter(r["v"][name]["eng"] for r in rr)
            line += "  | engine threshold +-%.2f: FLIP %d BALANCED %d WITH %d" % (tfi_info[2], ce["FLIP"], ce["BALANCED"], ce["WITH"])
        out(line)
    both = [r for r in pop]
    agree = Counter((r["v"][PRIMARY]["cls"], r["trail"]) for r in both)
    out("trailing %d-day terciles (min %d refs) vs full-span, primary population:" % (TRAIL_DAYS, TRAIL_MIN_REF))
    for a in ("FLIP", "BALANCED", "WITH"):
        out("  full %-8s -> trailing FLIP %d  BALANCED %d  WITH %d  too_few_trailing_ref %d"
            % (a, agree[(a, "FLIP")], agree[(a, "BALANCED")], agree[(a, "WITH")], agree[(a, "too_few_trailing_ref")]))
    q30 = Counter((r["v"][PRIMARY]["cls"], r["v"]["Q30L60"]["cls"]) for r in pop)
    out("primary vs Q30L60 (30 s buffer after the last print), primary population: "
        + "  ".join("%s->%s=%d" % (a[:4], b[:4] if b else "none", q30[(a, b)]) for a in ("FLIP", "BALANCED", "WITH") for b in ("FLIP", "BALANCED", "WITH", None)))

    out("")
    out("=== 6. Flow-only control pool (TFS-H3): primary reference windows in an outer tercile, traded with the flow ===")
    ctl = [x for x in an["control"] if x[2] is not None and not x[3]]
    for s in SESSIONS:
        c = Counter((x[2], period_of(utc(x[0]))) for x in ctl if x[1] == s)
        out("%-7s " % s + "  ".join("%s L/S %d/%d" % (per, c[("LONG", per)], c[("SHORT", per)]) for per in PERIODS)
            + "  all %d" % sum(c.values()))
    out("control rows dropped for the outcome fence: %d" % sum(1 for x in an["control"] if x[2] is not None and x[3]))

    out("")
    out("=== 7. Power (outcome-blind; sigma is a CARRIED proxy, not measured for this population) ===")
    nf = sum(1 for r in pop if r["v"][PRIMARY]["cls"] == "FLIP")
    nn = len(pop) - nf
    strata = Counter((r["e"].sess, r["e"].side, r["v"][PRIMARY]["cls"] == "FLIP") for r in pop)
    cov_f = sum(v for (s, sd, f), v in strata.items() if f and strata[(s, sd, False)] > 0)
    cov_n = sum(v for (s, sd, f), v in strata.items() if not f and strata[(s, sd, True)] > 0)
    nc = len(ctl)
    out("population: FLIP %d  NO-FLIP %d  (covered session x side strata: FLIP %d  NO-FLIP %d)  control %d" % (nf, nn, cov_f, cov_n, nc))
    a_lo, a_hi = FW_ALPHA / M_TESTS, FW_ALPHA
    out("Holm over %d tests, familywise %.2f: first-step alpha %.4f (worst case), last-step alpha %.2f (best case)" % (M_TESTS, FW_ALPHA, a_lo, a_hi))
    out("MDE in sigma units at the worst-case alpha: H1 %.3f  H2 %.3f  H3 %.3f"
        % (a4.mde(a_lo, 1.0, cov_f, cov_n), a4.mde(a_lo, 1.0, nf), a4.mde(a_lo, 1.0, nf, nc)))
    out("test                         sigma (bps)          MDE80 worst/best   " + "  ".join("pow@%gbps" % d for d in EFFECTS) + "   readable (n>=%d)" % MIN_N)
    for lab, sg in (("conservative x2.45", SIGMA_CONS * ATR_SCALE_CARRIED), ("optimistic x2.45", SIGMA_OPT * ATR_SCALE_CARRIED),
                    ("optimistic x1", SIGMA_OPT)):
        for tname, n1, n2, rd in (("TFS-H1 FLIP - NO-FLIP", cov_f, cov_n, cov_f >= MIN_N and cov_n >= MIN_N),
                                  ("TFS-H2 FLIP vs 0", nf, None, nf >= MIN_N),
                                  ("TFS-H3 FLIP - control", nf, nc, nf >= MIN_N and nc >= MIN_N)):
            out("%-27s %-18s %5s  %6s/%-6s  " % (tname, lab, fmt(sg), fmt(a4.mde(a_lo, sg, n1, n2)), fmt(a4.mde(a_hi, sg, n1, n2)))
                + "  ".join("%9s" % fmt(a4.power_at(a_lo, sg, d, n1, n2), 2) for d in EFFECTS) + "   %s" % ("yes" if rd else "NO"))
    for per in PERIODS:
        rr = [r for r in pop if r["per"] == per]
        f = sum(1 for r in rr if r["v"][PRIMARY]["cls"] == "FLIP")
        out("period %-8s FLIP %d  NO-FLIP %d  readable %s" % (per, f, len(rr) - f, "yes" if f >= MIN_N and len(rr) - f >= MIN_N else "NO"))


# ---------------------------------------------------------------- selftest
def selftest(out=print):
    fails = []

    def check(name, got, want):
        ok = got == want
        out("  %-66s %s  got=%r want=%r" % (name, "PASS" if ok else "FAIL", got, want))
        if not ok:
            fails.append(name)

    out("SELFTEST: thresholds and classification")
    check("tercile edges, exact rank rule", tercile_edges([0.6, -0.6, 0.1, -0.1, 0.4, -0.4]), (-0.4, 0.1))
    check("LONG I == hi -> BALANCED (strict)", classify("LONG", 0.1, -0.4, 0.1), "BALANCED")
    check("LONG I > hi -> FLIP", classify("LONG", 0.1000001, -0.4, 0.1), "FLIP")
    check("LONG I == lo -> BALANCED (strict)", classify("LONG", -0.4, -0.4, 0.1), "BALANCED")
    check("SHORT I < lo -> FLIP (fade short: taker selling)", classify("SHORT", -0.4000001, -0.4, 0.1), "FLIP")
    check("SHORT I > hi -> WITH", classify("SHORT", 0.2, -0.4, 0.1), "WITH")

    tmp = tempfile.mkdtemp(prefix="tfsselftest_")
    HDR = "Timestamp,Price,Amount,Direction,Liquidation,TradeId,TradeSeq,MarkPrice,IndexPrice,TickDirection,Contracts\n"

    def write(path, trades, raw_after=()):
        trades.sort(key=lambda x: x[0])
        with open(path, "w", encoding="utf-8") as f:
            f.write(HDR)
            for t, amt, d, fl in trades:
                f.write("%d,NOT_A_PRICE,%s,%s,%s,1,1,x,x,0,1\n" % (t, amt if isinstance(amt, str) else "%.2f" % amt, d, fl))
            for line in raw_after:
                f.write(line)

    def filler(trs, t, n=520, d="buy"):
        for k in range(n):
            trs.append((t + 100 * k, 10.0, d, "none"))

    tr = []
    # Reference windows, NY, 2025-01-14 (no liquidation that day): six 60 s windows with I = -0.6 .. +0.6
    day0 = ms(datetime(2025, 1, 14, 14, 0, 0))
    for k, I in enumerate((-0.6, -0.4, -0.1, 0.1, 0.4, 0.6)):
        g = day0 + k * GRID_MS
        B, S = (1 + I) / 2 * 1000.0, (1 - I) / 2 * 1000.0
        for j in range(5):
            tr.append((g + 1000 + j * 1000, B / 5, "buy", "none"))
            tr.append((g + 1500 + j * 1000, S / 5, "sell", "none"))
    # Event A, NY 2025-01-15 14:02:00 (slot offset 120 s, outside every reference window)
    tA = ms(datetime(2025, 1, 15, 14, 2, 0))
    tr += [(tA, 30000.0, "sell", "T"), (tA + 100, 30000.0, "sell", "T")]          # onset at tA+100 (60,000 > 49,724)
    for j in range(10):                                                          # cascade-time UNFLAGGED sells (the echo)
        tr.append((tA + 1000 + j * 1000, 1000.0, "sell", "none"))
    tr.append((tA + 20000, 100000.0, "sell", "T"))                               # a later liquidation print: anchor resets here
    tr.append((tA + 20000, 5000.0, "sell", "none"))                              # an unflagged leg in the SAME ms: not in the window
    for j in range(10):                                                          # the real flow window: 600 buy / 400 sell
        tr.append((tA + 21000 + j * 1000, 100.0, "buy" if j < 6 else "sell", "none"))
    tr.append((tA + 90000, 10.0, "buy", "none"))                                 # first trade at/after D = tA+80 s
    filler(tr, tA + 200000)
    # A reference window 13 min after A's firing, strongly one-sided: must be EXCLUDED (firing within 30 min)
    g15 = ms(datetime(2025, 1, 15, 14, 15, 0))
    for j in range(10):
        tr.append((g15 + 1000 + j * 1000, 100.0, "buy", "none"))
    # Event B, NY 14:40:00, SHORT liqs; flow buy-heavy (I = +0.4) -> WITH for a SHORT event
    tB = ms(datetime(2025, 1, 15, 14, 40, 0))
    tr.append((tB, 60000.0, "buy", "T"))
    for j in range(10):
        tr.append((tB + 1000 + j * 1000, 100.0, "buy" if j < 7 else "sell", "none"))
    tr.append((tB + 70000, 10.0, "buy", "none"))
    filler(tr, tB + 200000, d="sell")
    # Event C, NY 15:20:00, thin: 5 trades in the window
    tC = ms(datetime(2025, 1, 15, 15, 20, 0))
    tr.append((tC, 60000.0, "sell", "T"))
    for j in range(5):
        tr.append((tC + 1000 + j * 1000, 100.0, "buy", "none"))
    tr.append((tC + 70000, 10.0, "buy", "none"))
    filler(tr, tC + 200000)
    # Event D, NY 16:05:00, cap: small liquidation prints every 50 s for 32 min -> never 60 s quiet within 30 min
    tD = ms(datetime(2025, 1, 15, 16, 5, 0))
    tr.append((tD, 60000.0, "sell", "T"))
    for k in range(1, 39):
        tr.append((tD + k * 50000, 100.0, "sell", "T"))
        tr.append((tD + k * 50000 + 5000, 100.0, "buy", "none"))
    tDe = tD + 38 * 50000
    for j in range(10):
        tr.append((tDe + 1000 + j * 1000, 100.0, "buy", "none"))
    tr.append((tDe + 70000, 10.0, "buy", "none"))
    filler(tr, tDe + 200000)
    # Event E, NY 17:32:00: sells in the first 30 s, buys in the next 10 s -> primary BALANCED (I = 0), Q30L60 FLIP
    tE = ms(datetime(2025, 1, 15, 17, 32, 0))
    tr.append((tE, 60000.0, "sell", "T"))
    for j in range(10):
        tr.append((tE + 1000 + j * 1000, 100.0, "sell", "none"))
        tr.append((tE + 31000 + j * 1000, 100.0, "buy", "none"))
    tr.append((tE + 100000, 10.0, "buy", "none"))
    filler(tr, tE + 200000)
    os.makedirs(os.path.join(tmp, "a"))
    pa = os.path.join(tmp, "a", "trades_2025-01.csv")
    write(pa, tr)
    # 2026-07 file, run A: event F1 NY 2026-07-02 23:44:30 -> D 23:45:30, + 15 min crosses the fence
    tr7 = []
    tF = ms(datetime(2026, 7, 2, 23, 44, 30))
    tr7.append((tF, 60000.0, "sell", "T"))
    for j in range(10):
        tr7.append((tF + 1000 + j * 1000, 100.0, "buy", "none"))
    tr7.append((tF + 70000, 10.0, "buy", "none"))
    filler(tr7, tF + 90000)
    pa7 = os.path.join(tmp, "a", "trades_2026-07.csv")
    write(pa7, tr7)

    cnt = Counter()
    try:
        events, spans, by_ev, refs, refdrop, st = run_pass([pa, pa7], cnt)
        an = analyse(events, by_ev, refs, 0.15)
    except Exception as ex:                                    # a mutant may crash; that is a failure, not a pass
        out("  run A raised %r" % ex)
        fails.append("run A raised")
        events = []
    if events:
        out("SELFTEST: run A (events, flow windows)")
        R = {r["t0"]: r for r in an["rows"]}
        check("event count (A, B, C, D, E, F1)", len(events), 6)
        check("event sides", [e.side for e in events], ["LONG", "SHORT", "LONG", "LONG", "LONG", "LONG"])
        nyref = [r for r in refs["L60"] if r[1] == "NY"]
        check("NY primary reference windows kept (the 14:15 one excluded)", len(nyref), 6)
        check("NY primary edges", an["edges"][(PRIMARY, "NY")], (-0.4, 0.1))
        a = R[utc(tA + 100)]["v"][PRIMARY]
        check("A: measured after the later print, I = +0.2", (a["m"].status, round(a["m"].I, 6)), ("measured", 0.2))
        check("A: FLIP (cascade echo and flagged prints not in the window)", a["cls"], "FLIP")
        check("A: one anchor reset, D = last print + 60 s", (a["m"].resets, a["m"].D), (1, tA + 20000 + 60000))
        check("A: TFI30 engine class (I = 1/3 > 0.15) FLIP", R[utc(tA + 100)]["v"]["TFI30"]["eng"], "FLIP")
        b = R[utc(tB)]["v"][PRIMARY]
        check("B: SHORT event, I = +0.4 -> WITH", (round(b["m"].I, 6), b["cls"]), (0.4, "WITH"))
        check("C: thin window (5 trades)", R[utc(tC)]["v"][PRIMARY]["m"].status, "thin_flow_window")
        check("D: prints every 50 s -> not quiet within 30 min", R[utc(tD)]["v"][PRIMARY]["m"].status, "not_quiet_within_cap")
        e_ = R[utc(tE)]["v"]
        check("E: primary I = 0 -> BALANCED", (round(e_[PRIMARY]["m"].I, 6), e_[PRIMARY]["cls"]), (0.0, "BALANCED"))
        check("E: Q30L60 (30 s buffer) I = +1 -> FLIP", (round(e_["Q30L60"]["m"].I, 6), e_["Q30L60"]["cls"]), (1.0, "FLIP"))
        f1 = R[utc(tF)]
        check("F1: measured but the NY 15-min outcome window crosses the fence", (f1["v"][PRIMARY]["m"].status, f1["v"][PRIMARY]["ofence"], f1["pop"]), ("measured", True, False))
        ctl = Counter(x[2] for x in an["control"] if x[1] == "NY" and x[2] is not None)
        check("control pool NY: LONG (I > hi) 2, SHORT (I < lo) 1", (ctl["LONG"], ctl["SHORT"]), (2, 1))
        check("trailing thresholds: too few refs for A", R[utc(tA + 100)]["trail"], "too_few_trailing_ref")

    # run B: the fence. A flow window that would close after the fence; poisoned lines at and after the fence.
    out("SELFTEST: run B (the date fence)")
    tr7b = []
    tG = ms(datetime(2026, 7, 2, 23, 59, 30))
    tr7b.append((tG, 60000.0, "sell", "T"))
    for j in range(10):
        tr7b.append((tG + 1000 + j * 1000, 100.0, "buy", "none"))
    poison = ["%d,NOT_A_PRICE,POISON,sell,T,1,1,x,x,0,1\n" % FENCE_MS,          # exactly at the fence
              "%d,NOT_A_PRICE,POISON,buy,T,1,1,x,x,0,1\n" % (FENCE_MS + 5000),
              "%d,NOT_A_PRICE,POISON,sell,none,1,1,x,x,0,1\n" % (FENCE_MS + 6000)]
    os.makedirs(os.path.join(tmp, "b"))
    pb7 = os.path.join(tmp, "b", "trades_2026-07.csv")
    write(pb7, tr7b, poison)
    cnt = Counter()
    try:
        ev2, sp2, be2, rf2, rd2, st2 = run_pass([pb7], cnt)
        m = be2[id(ev2[0])][PRIMARY]
        check("G: flow window crosses the fence", m.status, "flow_window_crosses_fence")
        check("trades passed to the scan (11 before the fence)", cnt["trades"], 11)
        check("max trade read < fence", st2["max_ts"] < FENCE_MS, True)
    except Exception as ex:
        out("  run B raised %r (a poisoned line at/after the fence was parsed)" % ex)
        fails.append("run B raised")

    # run C: per-era LLS-1 thresholds (TFS-13). Two stores, one continuous pass. Each liquidation size sits BETWEEN the
    # two eras' thresholds for its session, so applying the wrong era's table changes which trades fire.
    out("SELFTEST: run C (per-era thresholds, two stores, one pass)")
    eras_c = [(ms(datetime(2024, 12, 1)), {"ASIA": 100000, "LONDON": 100000, "NY": 100000}, "test era 1"),
              (ms(datetime(2025, 1, 1)), {"ASIA": 69535, "LONDON": 83250, "NY": 49724}, "test era 2")]
    t1 = ms(datetime(2024, 12, 31, 14, 0, 0))    # NY 70,000: below era 1 (100,000) -> no fire; era 2 would fire
    t2 = ms(datetime(2024, 12, 31, 15, 0, 0))    # NY 120,000: above both -> fires (onset)
    t3 = ms(datetime(2025, 1, 1, 0, 0, 0))       # ASIA 80,000 EXACTLY at the era-2 start: era 2 (69,535) -> fires
    t4 = ms(datetime(2025, 1, 1, 14, 0, 0))      # NY 60,000: era 2 (49,724) -> fires; era 1 would not
    c1, c2 = [], []
    for t, amt, dst in ((t1, 70000.0, c1), (t2, 120000.0, c1), (t3, 80000.0, c2), (t4, 60000.0, c2)):
        dst.append((t, amt, "sell", "T"))
        filler(dst, t + 1000)                    # 520 unflagged trades clear the 500-trade window
    os.makedirs(os.path.join(tmp, "c1")); os.makedirs(os.path.join(tmp, "c2"))
    pc1 = os.path.join(tmp, "c1", "trades_2024-12.csv"); pc2 = os.path.join(tmp, "c2", "trades_2025-01.csv")
    write(pc1, c1); write(pc2, c2)
    cnt = Counter()
    try:
        ev3, _, _, _, _, _ = run_pass([pc1, pc2], cnt, eras=eras_c)
        check("eras: onsets = t2, t3, t4 (t1 below its own era's bar)", [e.t0 for e in ev3], [t2, t3, t4])
        check("eras: sessions of the onsets", [e.sess for e in ev3], ["NY", "ASIA", "NY"])
        check("eras: trades scanned per era (t3 at the era start counts in era 2)", (cnt["era_trades_0"], cnt["era_trades_1"]), (521 * 2, 521 * 2))
        check("eras: a4.LLS1 restored after the pass", (type(a4.LLS1) is dict, a4.LLS1 == LLS1_RULED), (True, True))
    except Exception as ex:
        out("  run C raised %r" % ex)
        fails.append("run C raised")

    # seam checks (TFS-14): TradeSeq must step by exactly 1 and time must not go back across a store seam
    out("SELFTEST: store seam (TradeSeq)")

    def write_seq(path, rows):
        with open(path, "w", encoding="utf-8") as f:
            f.write(HDR)
            for t, sq in rows:
                f.write("%d,NOT_A_PRICE,10.00,buy,none,1,%d,x,x,0,1\n" % (t, sq))

    os.makedirs(os.path.join(tmp, "s"))
    sp = os.path.join(tmp, "s", "prev.csv")
    write_seq(sp, [(1000 + k, 100 - 299 + k) for k in range(300)])      # 300 lines (> 4 KB), last = (1299, 100)
    cases = (("contiguous (100 -> 101)", 1299, 101, True), ("gap (100 -> 102): a missing trade", 1300, 102, False),
             ("duplicate (100 -> 100)", 1300, 100, False), ("time goes back (1299 -> 1298)", 1298, 101, False))
    for k, (lab, ft, fs, want) in enumerate(cases):
        sn = os.path.join(tmp, "s", "next%d.csv" % k)
        write_seq(sn, [(ft, fs), (ft + 5, fs + 1)])
        check("seam: " + lab, seam_check(sp, sn)[0], want)
    try:
        check_store_seams([[sp], [os.path.join(tmp, "s", "next1.csv")]], out=lambda s: None)
        check("seam: a failed store seam is a STOP (SystemExit)", "no stop", "stop")
    except SystemExit:
        check("seam: a failed store seam is a STOP (SystemExit)", "stop", "stop")

    out("SELFTEST: power (the sister's functions)")
    check("MDE two-sample n=100/100 sigma=10 alpha=.05", round(a4.mde(0.05, 10.0, 100, 100), 3), 3.962)
    out("SELFTEST %s (%d failure(s))" % ("PASS" if not fails else "FAIL", len(fails)))
    return 0 if not fails else 1


def main():
    ap = argparse.ArgumentParser(description="Liquidation x trade-flow flip study: outcome-blind counts")
    ap.add_argument("--span", choices=("extended", "registered"), default="extended",
                    help="extended = 2023-01-01 -> 2026-07-02, two stores (the 2026-10-06 re-registration); "
                         "registered = session 1 exactly (2025-01-01 -> 2026-07-02)")
    ap.add_argument("--store", default=DEFAULT_STORE, help="the 2025-2026 store")
    ap.add_argument("--store-2023", default=STORE_2023, help="the 2023-2024 store (extended span only)")
    ap.add_argument("--grid-min", type=int, default=None, help="reference grid step; default 5 (extended), 15 (registered)")
    ap.add_argument("--settings", default="settings.json")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        sys.exit(selftest())
    configure(a.span, a.grid_min)
    t_start = time.time()
    print("Liquidation x TRADE-FLOW flip - counts (OUTCOME-BLIND). docs/liq-tradeflow-flip-study-spec.md")
    stores = [(a.store, MONTHS)] if SPAN == "registered" else [(a.store_2023, MONTHS_2023), (a.store, MONTHS)]
    groups = [[os.path.join(d, "trades_%s.csv" % m) for m in mm] for d, mm in stores]
    paths = [p for g in groups for p in g]
    miss = [p for p in paths if not os.path.exists(p)]
    if miss:
        raise SystemExit("STOP: missing store files %s" % miss)
    if SPAN == "registered":
        print("store files: %d (trades_%s.csv .. trades_%s.csv), bytes %d" % (len(paths), MONTHS[0], MONTHS[-1], sum(os.path.getsize(p) for p in paths)))
    else:
        print("store files: %d in %d stores, read in this order as one pass, bytes %d"
              % (len(paths), len(groups), sum(os.path.getsize(p) for p in paths)))
        for (d, mm), g in zip(stores, groups):
            print("  %s  trades_%s.csv .. trades_%s.csv  files %d  bytes %d" % (d, mm[0], mm[-1], len(g), sum(os.path.getsize(p) for p in g)))
        check_store_seams(groups)
        mbad = []
        for g in groups:
            for x, y in zip(g, g[1:]):
                ok, v = seam_check(x, y)
                if not ok:
                    mbad.append("%s->%s seq %d->%d" % (os.path.basename(x), os.path.basename(y), v[1], v[3]))
        print("month-file seams inside each store: %d checked, %d FAIL%s"
              % (sum(len(g) - 1 for g in groups), len(mbad), (": " + "; ".join(mbad)) if mbad else ""))
    tfi_info = engine_tfi_from_settings(a.settings)
    cnt = Counter()
    events, spans, by_ev, refs, refdrop, st = run_pass(paths, cnt)
    assert st["max_ts"] < FENCE_MS and st["min_ts"] >= ms(START), "FENCE BREACH or data before 2025-01-01"
    an = analyse(events, by_ev, refs, tfi_info[2])
    report(events, spans, by_ev, refs, refdrop, st, an, cnt, tfi_info)
    print("")
    print("runtime %.0f s" % (time.time() - t_start))


if __name__ == "__main__":
    main()
