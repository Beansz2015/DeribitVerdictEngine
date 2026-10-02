#!/usr/bin/env python3
# tools/ops/burst_outcome_read.py
#
# The burst outcome read (docs/burst-outcome-read-spec.md). Python 3 standard library only. Host-agnostic, reads files only.
# Built by session 1 of that spec BEFORE any outcome run (the pre-registration order, docs/burst-outcome-read-spec.md
# section 5.2). The rules below are docs/burst-outcome-read-spec.md section 5; do not change them after an outcome is seen.
#
# Inputs:
#   --export  SwingFallbackRead --mode diagexport CSV (the candle walk: MainNetEv, MainOutcome, placed-level distances).
#   --fetch   the fetch folder of the run date. Its books give TFISignal, AggrVelBurstRatio, AggrVelNet per row, joined by
#             Timestamp: analysis_log.csv.v0.7.bak, every analysis_log.csv.*col-*.bak by rotation stamp, analysis_log.csv,
#             first seen wins (the tools/ops/q1d_tier_geometry.py --logs pattern; the book order of
#             tools/ops/burst-outcome-power-count.ps1).
#   --pooled  (optional) the pooled book the export was built with. Used ONLY to name why a power-count row is not in the export.
#   settings.json (tracked, read-only): sessions, verdict percentages, fixed burst thresholds, upgrade_bonus, fees.
#
# Rules (docs/burst-outcome-read-spec.md section 5):
#   * Data window first: --cut-before T keeps Timestamp < T (run 1: 2026-11-25T00:00:00Z); --from T keeps Timestamp >= T
#     (run 2). Rows are filtered on Timestamp before any other step, in the books and in the export.
#   * Population: the export's rows (directional, valid placed levels, trading week), joined to the fetch books.
#     Exclusions, counted: not in the fetch books; verdict differs between export and book; AggrVelBurstRatio empty;
#     ATR not parseable; ASIA before 2026-08-01 19:02:31 UTC.
#   * Arms for verdict side X (Core/ScoringEngine_Calculate_Scoring.vb Step 2): A = TFI on X and burst on X;
#     B = TFI on X and AggrVelSignal NORMAL (no burst); C = TFI on X and burst on the other side; O = TFI not on X.
#     C and O are counted, never tested.
#   * Shadow arms (option (c)): S_add = a B row with ratio >= the session x fifth shadow threshold and AggrVelNet on X;
#     A_drop = an A row with ratio below it (A_keep otherwise). B_lean = a B row with ratio >= the fixed threshold.
#   * ATR fifth: the RULED edges (AVR-1), never re-binned. Fifth groups: low = 1-2, mid = 3, high = 4-5.
#   * Strata: ATR fifth x band x POC era (before / from 2026-09-24 18:46:06 UTC) x daylight-saving seam era (before / from
#     the session's seam, SEAM below; ruling AT-2). B is re-weighted to A's stratum mix
#     over strata where both have rows (rw_run, copied); A rows in strata without B rows are dropped and counted.
#   * Outcome: MainNetEv (net EV per trade, bps, maker/maker, the session's max window) from the export.
#   * CI: 95 % percentile bootstrap of whole UTC trading days, fixed seed, 10,000 resamples.
#   * BO-H1 (primary): per session, A - B re-weighted, all fifths. Holm over the THREE sessions, familywise 0.05
#     (a NOT READABLE session enters with p = 1).
#   * BO-H2 (secondary, the (c) test): per session, fifths 4-5, (A + S_add) - (B - S_add) re-weighted. Holm over the
#     readable sessions.
#   * BO-H3 and fifth-level cells: descriptive, CIs only, never labelled.
#   * Readable: n >= 100 in every group a statistic uses. Label: the census rule (label, copied), first match wins, with
#     CONFIRMED also requiring the full-sample Holm-adjusted CI to exclude 0.
#   * Halves: the first floor(D/2) UTC trading days of the analysis population (after the window and every exclusion).
#   * POC edge sensitivity: BO-H1 and BO-H2 re-run on pre-edge rows only; a labelled result whose sign flips gains
#     "EDGE-SENSITIVE".
#   * Seam sensitivity (AT-2): BO-H1 and BO-H2 also re-run on pre-seam rows only (each row against its own session's seam);
#     a labelled result whose sign flips gains "SEAM-SENSITIVE". Run 2 is wholly post-seam: that run prints n/a.
#
# Modes:
#   --counts-only  population, exclusions, export drops by reason, arm counts, the coverage column MainMissingBars, the
#                  information-only column MainFullWindowMissingBars and the pre-outcome gates (printed, never
#                  stopping); EXITS before any outcome column is opened. The export
#                  is read through a column whitelist that holds no outcome column (asserted).
#   --selftest     synthetic books + synthetic export in a temp dir, the full pipeline, known answers asserted.
#   (default)      the full read. Run it only as the pre-registered run 1 (--run 1) or run 2 (--run 2 --from <ts>); it
#                  refuses to start without one. --run pins the window, seed and resamples (RUN_PINS below).
#
# Pre-outcome gates (review docs/burst-outcome-read-tools-review-2026-10-02.md findings RV-1 and RV-2). Full mode STOPs
# (exit 2, the failed checks named) before any outcome column is opened unless: every population row has
# MainMissingBars = 0 (the main-window walk saw every 1-minute bar); analysis rows not in the power-count population = 0;
# rows with a different arm, shadow arm or fifth = 0; export Session and AggrVelSignal mismatches = 0; "verdict differs"
# and "Side/Tier inconsistent" drops = 0; settings.json version = the run's pinned version. Two more (trader ruling
# RVF-2 = (b), docs/burst-outcome-read-rv-fixes-spec-back.md): export rows dropped as "not directional" = 0, and
# power-count rows in the export but dropped for no recorded reason = 0.
# MainFullWindowMissingBars (ruling RVF-3 = (c)) is printed per session x arm in both modes and is NEVER a gate: it feeds
# no STOP, no label, no filter and no outcome. It is kept out of the population rows.
#
# Run (from the repo root; run 1 from a worktree at the reviewed tools commit, docs/burst-outcome-read-spec.md section 6.1):
#   dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
#   dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode diagexport \
#     --fetch aws_fetch/<fetch> --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv \
#     --cache <a FRESH, EMPTY folder> --out <that folder>/diagnosis-rows-<fetch>.csv
#   python tools/ops/burst_outcome_read.py --counts-only --run 1 --export <that csv> --fetch aws_fetch/<fetch> \
#     --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
#   python tools/ops/burst_outcome_read.py --selftest

import argparse, csv, glob, hashlib, json, math, os, random, shutil, sys, tempfile
from datetime import datetime, timedelta, timezone

SESS_ORDER = ["ASIA", "LONDON", "NY"]
TIERS = ["STRONG", "MEDIUM", "WEAK"]
FLOOR = 100
FMT = "%Y-%m-%d %H:%M:%S"

# --- Dates of record (not settings thresholds): docs/burst-outcome-read-spec.md section 6.2 and
# tools/ops/burst-outcome-power-count.ps1 $AsiaArmed / $EraEdges.
ASIA_ARMED = datetime(2026, 8, 1, 19, 2, 31)
V66_EDGE = datetime(2026, 8, 10, 18, 36, 1)
ATR_STEP = datetime(2026, 8, 20, 0, 0, 0)
POC_EDGE = datetime(2026, 9, 24, 18, 46, 6)
# --- Daylight-saving seam, per session (trader ruling AT-2): ASIA and LONDON when Europe leaves summer time, NY when the
# US does. MECHANISM-class literals under the fixture-literal provenance rule: copied from docs/burst-outcome-read-spec.md
# section 6.2 (the "Daylight-saving seam" row), not from settings.json. A row is "post" when Timestamp >= its session's seam.
SEAM = {"ASIA": datetime(2026, 10, 25, 1, 0, 0), "LONDON": datetime(2026, 10, 25, 1, 0, 0), "NY": datetime(2026, 11, 1, 6, 0, 0)}

# --- RULED ATR-fifth edges (AVR-1, 2026-09-26): copied from tools/ops/burst-watch-read.ps1 $Ref (commit b5b4a7d),
# identical to tools/ops/burst-outcome-power-count.ps1 $Edges. MECHANISM-class literals under the fixture-literal
# provenance rule: a frozen measurement of record (docs/aggr-vel-burst-rederivation-read-2026-09-26.md section 2.2).
EDGES = {"ASIA": [33.6, 51.2, 66.2, 83.9], "LONDON": [31.9, 51.5, 68.4, 90.7], "NY": [19.3, 30.9, 43.0, 62.9]}
# --- Frozen shadow thresholds for option (c): AggrVelBurstRatio p90 per session x ATR fifth. Copied from
# tools/ops/burst-outcome-power-count.ps1 $Shadow (commit b5b4a7d), which took them from tools/ops/aggr-vel-regime-read.ps1
# on aws_fetch\20260925-085341. MECHANISM-class: a frozen measurement. NOT re-measured here.
SHADOW = {"ASIA": [7.41, 4.92, 3.88, 3.31, 2.62], "LONDON": [9.21, 5.71, 3.51, 3.50, 2.31], "NY": [7.17, 4.46, 3.40, 2.90, 2.23]}

BOOK_NEED = ["Timestamp", "Price", "Verdict", "ATR", "AggrVelBurstRatio", "AggrVelNet", "AggrVelSignal", "TFISignal", "MaxScore",
             "EffectiveLongScore", "EffectiveShortScore", "PlacedTargetLong", "PlacedStopLong", "PlacedTargetShort", "PlacedStopShort"]
# The export is read through a whitelist. SIGNAL_COLS holds no outcome column; OUTCOME_COLS is opened only after the
# counts are printed, and never in --counts-only.
SIGNAL_COLS = ["Timestamp", "Session", "Side", "Tier", "Verdict", "Price", "Atr", "AggrVelSignal", "MaxScore",
               "EffectiveLongScore", "EffectiveShortScore", "InstanceId"]
OUTCOME_COLS = ["MainNetEv", "MainOutcome", "TBps", "SBps"]
# MainMissingBars is a COVERAGE column, not an outcome (review finding RV-1): the count of 1-minute bars the main-window
# walk did not find. A row with no bars scores -fee silently, so it is read with the signal columns, BEFORE any outcome
# column, in both modes. ⚠ It is not a pure coverage count: the walk stops at resolution, so a gap after an early
# resolution is not counted. The gate below needs it to be 0 on every row, so it can only ever reveal "all zero" or a
# count that stops the run.
COVERAGE_COLS = ["MainMissingBars"]
# MainFullWindowMissingBars (trader ruling RVF-3 = (c), docs/burst-outcome-read-rv-fixes-spec-back.md): missing 1-minute
# bars over the row's WHOLE main window, whatever the resolution; the export's last column. INFORMATION ONLY: printed per
# session x arm in both modes, before any outcome column. It never feeds a STOP, a label, a filter or an outcome, and it
# is never copied into a population row. Read only when the export has it (an export written before the column existed
# prints "absent"): a missing header must not become a gate either.
COVERAGE_INFO_COL = "MainFullWindowMissingBars"
FORBIDDEN_IN_COUNTS = {"MainOutcome", "MainResolveMin", "MainNetEv", "C24Outcome", "C24ResolveMin",
                       "C24NetEv", "C24MissingBars", "TBps", "SBps", "TAtr", "SAtr"}
assert not (set(SIGNAL_COLS + COVERAGE_COLS + [COVERAGE_INFO_COL]) & FORBIDDEN_IN_COUNTS), "an outcome column is in the counts whitelist"

# --- Run pins (review finding RV-2). docs/burst-outcome-read-spec.md section 6.1: run 1 reads rows before the fixed cut
# 2026-11-25 00:00:00 UTC; run 2 reads rows from the first row after the daylight-saving-aware session-hours deploy (given
# with --from, never before the run-1 cut, so the two runs stay disjoint). Seed: decision TB-D11 (20260928). Resamples:
# the house rule (10,000), docs/burst-outcome-read-spec.md section 5 "CI". Settings version: run 1 is pre-registered at
# v69 (docs/burst-outcome-read-spec.md sections 1 and 5). Run 2's version is not pinned yet: the run-2 rider (review
# finding RV-4) must pin it. MECHANISM-class literals: dates and values of record, not settings thresholds.
RUN1_CUT = datetime(2026, 11, 25, 0, 0, 0)
RUN_SEED = 20260928
RUN_RESAMPLES = 10000
RUN_SETTINGS_VERSION = {"1": 69}


class GateStop(Exception):
    """A pre-outcome gate failed in full mode. Carries the run context (for the output file) and the failed check names."""
    def __init__(self, cx, names):
        Exception.__init__(self, "pre-outcome gate(s) failed: " + ", ".join(names))
        self.cx = cx
        self.names = names

# Drop and reconciliation reasons that are also gates (trader ruling RVF-2 = (b)). One string each, used in both places.
DROP_NOT_DIRECTIONAL = "not directional (the export should hold none)"
WHY_NO_REASON = "in the export, dropped for no recorded reason"

DIRSET = {"STRONG LONG": ("LONG", "STRONG"), "LONG": ("LONG", "MEDIUM"), "WEAK LONG": ("LONG", "WEAK"),
          "STRONG SHORT": ("SHORT", "STRONG"), "SHORT": ("SHORT", "MEDIUM"), "WEAK SHORT": ("SHORT", "WEAK")}
BAND_RANK = {"BELOW": 0, "WEAK": 1, "MEDIUM": 2, "STRONG": 3}

# Globals the copied functions read (they are module-level in the source): args.seed, R.
args = None
R = 10000
DRAWS = []   # sorted bootstrap draws of every boot() call, in call order (the one line added to the copied boot)


def md5(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def tryf(s):
    """float or None. Mirrors [double]::TryParse(s, 'Float', Invariant) closely enough for these columns."""
    try:
        return float(s)
    except (TypeError, ValueError):
        return None


def tryi(s):
    try:
        return int(s)
    except (TypeError, ValueError):
        return None


def parse_when(s):
    return datetime.strptime(s.rstrip("Z").replace("T", " "), FMT)


# ================================================================== COPIED from tools/ops/medium_tier_diagnosis.py
# Source commit 3288d32 (the file's last change). boot, compare, label, rw_run and the helpers they call (pctl,
# diff_of, halves, readable, sig) are copied VERBATIM, with ONE added line in boot (marked) that records the sorted
# draws for Holm. Do not re-derive them here.
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


def compare(rows, cell_a, cell_b, value, name, context, need_cells=(0, 1), stat=None, K=2, contribs=None):
    """Two-group comparison with halves and the pre-registered label. cell_a/cell_b: predicates."""
    res = {}
    for hn, hr in halves(rows).items():
        if contribs is None:
            def cb(r, ca=cell_a, cbb=cell_b):
                out_ = []
                if ca(r): out_.append((0, value(r)))
                if cbb(r): out_.append((1, value(r)))
                return out_
            res[hn] = boot(hr, cb, K, stat or diff_of(0, 1))
        else:
            res[hn] = boot(hr, contribs, K, stat)
    lab = label(res, need_cells)
    ALL_LABELS.append((context, name, lab, res["FULL"]))
    return res, lab


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
        # ⚠ DEVIATES from the verbatim copy (orchestrator ruling 2026-09-29, before any outcome was seen):
        # readability counts only COVERED strata (both arms present), the rows `stat` actually uses —
        # docs/burst-outcome-read-spec.md §5 "n >= 100 in every group a statistic uses". The copy counted
        # every row, including A rows in strata with no B row, which the statistic drops.
        cov = [i for i in range(K) if cnt[i] > 0 and cnt[K + i] > 0]
        res[hn] = (pt, lo, hi, [sum(cnt[i] for i in cov), sum(cnt[K + i] for i in cov)])
    lab = label(res, (0, 1))
    ALL_LABELS.append((ctx, name, lab, res["FULL"]))
    return res, lab, keys
# ================================================================== end of the copied block


def f1(x):
    return "n/a" if x != x else ("%+.1f" % x)


def ci(pt, lo, hi):
    return "%s [%s, %s]" % (f1(pt), f1(lo), f1(hi))


def fifth_of(atr, edges):
    for i, e in enumerate(edges):
        if atr <= e:
            return i + 1
    return 5


def era4(ts):
    if ts < V66_EDGE: return "E1"
    if ts < ATR_STEP: return "E2"
    if ts < POC_EDGE: return "E3"
    return "E4"


class Ctx:
    """One run's settings and output."""
    def __init__(self, root):
        self.root = root
        self.cfg = json.load(open(os.path.join(root, "settings.json"), encoding="utf-8-sig"))
        c = self.cfg
        self.sessions = [(s["name"].upper(), int(s["start_hour"]), int(s["end_hour"])) for s in c["session_volume"]["sessions"]]
        av = c["indicators"]["aggressor_velocity"]
        self.fixed = {s: float(av["sessions"][s]["burst_ratio_threshold"]) for s in SESS_ORDER}
        self.bonus = int(av["upgrade_bonus"])
        self.pct = (c["scoring"]["verdict_strong_pct"], c["scoring"]["verdict_med_pct"], c["scoring"]["verdict_weak_pct"])
        tc = c["scoring"]["trade_costs"]
        if tc["round_trip_style"] != "maker_maker":
            raise SystemExit("fee style %s: the export's MainNetEv uses maker/maker; re-derive. STOP." % tc["round_trip_style"])
        self.fee = 2.0 * tc["maker_fee_bps"]
        self.out = []

    def p(self, s=""):
        self.out.append(s)
        print(s)

    def session_of(self, hour):
        for n, lo, hi in self.sessions:
            if lo <= hour <= hi:
                return n
        return None

    def band_of(self, score, mx):
        # tools/ops/burst-outcome-power-count.ps1 Get-BandOf: [int][Math]::Ceiling(maxScore * pct)
        s, m, w = (math.ceil(mx * q) for q in self.pct)
        if score >= s: return "STRONG"
        if score >= m: return "MEDIUM"
        if score >= w: return "WEAK"
        return "BELOW"


def in_window(ts, opt):
    if opt.cut_before is not None and not ts < opt.cut_before: return False
    if opt.from_ is not None and not ts >= opt.from_: return False
    return True


# ------------------------------------------------------------------ books (the power-count loader)
def book_names(fetch):
    rot = sorted(glob.glob(os.path.join(fetch, "analysis_log.csv.*col-*.bak")), key=lambda p_: os.path.basename(p_).split(".")[-2])
    return [os.path.join(fetch, "analysis_log.csv.v0.7.bak")] + rot + [os.path.join(fetch, "analysis_log.csv")]


def load_books(cx, fetch, opt):
    """First seen wins on Timestamp, weekday rows only, rows with a field count unequal to the header skipped,
    exactly as tools/ops/burst-outcome-power-count.ps1. Returns {ts_string: dict}, in book order."""
    book = {}
    for path in book_names(fetch):
        if not os.path.exists(path):
            raise SystemExit("file not found: %s" % path)
        n = 0
        with open(path, encoding="utf-8-sig", errors="replace") as f:
            cols = f.readline().rstrip("\r\n").split(",")
            ix = {}
            for c in BOOK_NEED:
                if c not in cols:
                    raise SystemExit("%s header missing %s" % (os.path.basename(path), c))
                ix[c] = cols.index(c)
            iinst = cols.index("InstanceId") if "InstanceId" in cols else -1
            for line in f:
                line = line.rstrip("\r\n")
                if line == "": continue
                p_ = line.split(",")
                if len(p_) != len(cols): continue
                try:
                    ts = datetime.strptime(p_[ix["Timestamp"]], FMT)
                except ValueError:
                    continue
                if ts.weekday() >= 5: continue
                if not in_window(ts, opt): continue
                key = ts.strftime(FMT)
                if key in book: continue
                d = {c: p_[ix[c]] for c in BOOK_NEED}
                d["InstanceId"] = p_[iinst].strip() if iinst >= 0 else ""
                d["_ts"] = ts
                d["_book"] = os.path.basename(path)
                book[key] = d
                n += 1
        cx.p("- Book `%s` (MD5 %s): weekday rows first seen in this book, inside the window = %d" % (os.path.basename(path), md5(path), n))
    return book


def arm_fields(cx, sess, side, band, atr, tfi, sig_, ratio, net_ok, net, mx_s, effl_s, effs_s):
    """The power-count arm rule for one row. Returns a dict of arm fields."""
    tfi_side = "LONG" if tfi == "BUY PRESSURE" else ("SHORT" if tfi == "SELL PRESSURE" else "")
    burst_side = "LONG" if sig_ == "BURST_BUY" else ("SHORT" if sig_ == "BURST_SELL" else "")
    sgn = 1.0 if side == "LONG" else -1.0
    fifth = fifth_of(atr, EDGES[sess])
    sh_thr = SHADOW[sess][fifth - 1]
    arm, sh, oup, crossed, mis, blean = "O", "", False, False, False, False
    if tfi_side == side:
        if burst_side == side:
            arm = "A"
            sh = "A_keep" if ratio >= sh_thr else "A_drop"
            mx, el, es = tryi(mx_s), tryi(effl_s), tryi(effs_s)
            if mx is not None and el is not None and es is not None and mx > 0:
                eff = el if side == "LONG" else es
                if cx.band_of(eff, mx) != band: mis = True
                if BAND_RANK[cx.band_of(eff - cx.bonus, mx)] < BAND_RANK[band]: crossed = True
        elif burst_side == "":
            arm = "B"
            if ratio >= cx.fixed[sess]: blean = True
            if ratio >= sh_thr and net_ok and sgn * net > 0: sh = "S_add"
        else:
            arm = "C"
    elif tfi_side != "" and burst_side == tfi_side:
        oup = True
    return {"arm": arm, "sh": sh, "oup": oup, "crossed": crossed, "mis": mis, "blean": blean, "fifth": fifth}


def reference_population(cx, book):
    """The power count's own population and arms, from the fetch books (tools/ops/burst-outcome-power-count.ps1)."""
    rows = {}
    x = {"total": 0, "pop": 0, "nonDir": 0, "lean": 0, "asiaUnarmed": 0, "badLevels": 0}
    for key, b in book.items():
        ts = b["_ts"]
        sess = cx.session_of(ts.hour)
        if sess is None: continue
        x["total"] += 1
        atr = tryf(b["ATR"])
        if b["AggrVelBurstRatio"] == "" or atr is None: continue
        x["pop"] += 1
        v = b["Verdict"].strip().upper()
        if v not in DIRSET:
            if v.startswith("NO TRADE ["): x["lean"] += 1
            else: x["nonDir"] += 1
            continue
        if sess == "ASIA" and ts < ASIA_ARMED:
            x["asiaUnarmed"] += 1; continue
        side, band = DIRSET[v]
        price = tryf(b["Price"]) or 0.0
        tgt = tryf(b["PlacedTargetLong" if side == "LONG" else "PlacedTargetShort"]) or 0.0
        stp = tryf(b["PlacedStopLong" if side == "LONG" else "PlacedStopShort"]) or 0.0
        sgn = 1.0 if side == "LONG" else -1.0
        if price <= 0 or atr <= 0 or tgt <= 0 or stp <= 0 or sgn * (tgt - price) <= 0 or sgn * (price - stp) <= 0:
            x["badLevels"] += 1; continue
        ratio = float(b["AggrVelBurstRatio"])
        net = tryf(b["AggrVelNet"])
        a = arm_fields(cx, sess, side, band, atr, b["TFISignal"], b["AggrVelSignal"], ratio, net is not None, net or 0.0,
                       b["MaxScore"], b["EffectiveLongScore"], b["EffectiveShortScore"])
        a.update({"ts": ts, "s": sess, "band": band, "side": side})
        rows[key] = a
    return rows, x


# ------------------------------------------------------------------ export
def read_export(path, cols, optional=()):
    """Reads ONLY the named columns. Anything else in the file is never placed in a row. A column in `optional` is read
    when the header has it and skipped (no error) when it does not."""
    out = []
    with open(path, encoding="utf-8", newline="") as f:
        rd = csv.reader(f)
        h = next(rd)
        ix = {}
        for c in cols:
            if c not in h:
                raise SystemExit("export header missing %s" % c)
            ix[c] = h.index(c)
        for c in optional:
            if c in h: ix[c] = h.index(c)
        for x in rd:
            if not x: continue
            out.append({c: x[i] for c, i in ix.items()})
    return out


def analysis_population(cx, export_rows, book, opt):
    pop = []
    drops = {}
    n_win = 0
    session_mismatch = 0
    sig_mismatch = 0
    def drop(reason, key):
        drops.setdefault(reason, []).append(key)
    for e in export_rows:
        assert not (set(e) & FORBIDDEN_IN_COUNTS), "outcome column reached the population step"
        ts = datetime.strptime(e["Timestamp"], FMT)
        if not in_window(ts, opt): continue
        n_win += 1
        key = e["Timestamp"]
        b = book.get(key)
        if b is None: drop("not in the fetch books (pre-collector or pooled-only row)", key); continue
        v = e["Verdict"].strip().upper()
        if b["Verdict"].strip().upper() != v: drop("verdict differs between export and fetch book", key); continue
        if b["AggrVelBurstRatio"] == "": drop("AggrVelBurstRatio empty", key); continue
        atr = tryf(e["Atr"])
        if atr is None: drop("ATR not parseable", key); continue
        sess = e["Session"].upper()
        if sess != cx.session_of(ts.hour): session_mismatch += 1
        if sess == "ASIA" and ts < ASIA_ARMED: drop("ASIA before arming 2026-08-01 19:02:31", key); continue
        if v not in DIRSET: drop(DROP_NOT_DIRECTIONAL, key); continue
        side, band = DIRSET[v]
        if e["Side"] != side or e["Tier"] != band: drop("Side/Tier inconsistent with Verdict", key); continue
        if e["AggrVelSignal"] != b["AggrVelSignal"]: sig_mismatch += 1
        ratio = float(b["AggrVelBurstRatio"])
        net = tryf(b["AggrVelNet"])
        a = arm_fields(cx, sess, side, band, atr, b["TFISignal"], b["AggrVelSignal"], ratio, net is not None, net or 0.0,
                       e["MaxScore"], e["EffectiveLongScore"], e["EffectiveShortScore"])
        a.update({"ts": ts, "s": sess, "band": band, "side": side, "key": key, "day": ts.strftime("%Y-%m-%d"),
                  "poc": "pre" if ts < POC_EDGE else "post", "seam": "pre" if ts < SEAM[sess] else "post", "era4": era4(ts),
                  "missing": tryi(e["MainMissingBars"])})   # coverage column (RV-1); None = not parseable
        pop.append(a)
    return pop, drops, n_win, session_mismatch, sig_mismatch


# ------------------------------------------------------------------ count tables
def band_split(rows):
    return "%d (%d/%d/%d)" % (len(rows), sum(1 for r in rows if r["band"] == "STRONG"), sum(1 for r in rows if r["band"] == "MEDIUM"),
                              sum(1 for r in rows if r["band"] == "WEAK"))


def counts_table(cx, rows, title):
    """Same layout and columns as tools/ops/burst-outcome-power-count.ps1 output section 1, so a diff compares them."""
    cx.p(title)
    cx.p("session fifth    A (S/M/W)        B (S/M/W)          C    O  Oup | A_keep A_drop S_add B_lean | crossed bandMismatch")
    for s in SESS_ORDER:
        sr = [r for r in rows if r["s"] == s]
        for f in range(1, 7):
            cr = [r for r in sr if r["fifth"] == f] if f <= 5 else sr
            lbl = str(f) if f <= 5 else "all"
            A = [r for r in cr if r["arm"] == "A"]; B = [r for r in cr if r["arm"] == "B"]
            cnt = lambda fn: sum(1 for r in cr if fn(r))
            cx.p("{0:<7} {1:<4} {2:<16} {3:<18} {4:>4} {5:>4} {6:>4} | {7:>6} {8:>6} {9:>5} {10:>6} | {11:>7} {12:>5}".format(
                s, lbl, band_split(A), band_split(B), cnt(lambda r: r["arm"] == "C"), cnt(lambda r: r["arm"] == "O"), cnt(lambda r: r["oup"]),
                cnt(lambda r: r["sh"] == "A_keep"), cnt(lambda r: r["sh"] == "A_drop"), cnt(lambda r: r["sh"] == "S_add"), cnt(lambda r: r["blean"]),
                cnt(lambda r: r["crossed"]), cnt(lambda r: r["mis"])))


def era_table(cx, rows):
    cx.p("=== Era split (analysis population): arm A / arm B per session x ATR fifth x era ===")
    cx.p("   E1 armed->v66 2026-08-10 18:36:01 | E2 ->2026-08-20 | E3 ->POC fix 2026-09-24 18:46:06 | E4 POC fix->")
    cx.p("session fifth  E1 A/B      E2 A/B      E3 A/B      E4 A/B      | S_add E1/E2/E3/E4")
    for s in SESS_ORDER:
        sr = [r for r in rows if r["s"] == s]
        for f in range(1, 7):
            cr = [r for r in sr if r["fifth"] == f] if f <= 5 else sr
            lbl = str(f) if f <= 5 else "all"
            cells = []
            for e in ("E1", "E2", "E3", "E4"):
                er = [r for r in cr if r["era4"] == e]
                cells.append("%d/%d" % (sum(1 for r in er if r["arm"] == "A"), sum(1 for r in er if r["arm"] == "B")))
            sa = "/".join(str(sum(1 for r in cr if r["era4"] == e and r["sh"] == "S_add")) for e in ("E1", "E2", "E3", "E4"))
            cx.p("{0:<7} {1:<4}  {2:<11} {3:<11} {4:<11} {5:<11} | {6}".format(s, lbl, cells[0], cells[1], cells[2], cells[3], sa))


def classify_missing(ref_keys, export_keys, pooled, box_ids, book):
    """Why a power-count row is not in the export. Uses the pooled book when given."""
    why = {}
    for k in ref_keys:
        if k in export_keys: continue
        pr = pooled.get(k) if pooled is not None else None
        if pr is not None and pr[0] not in box_ids:
            r_ = "export: the pooled book holds this Timestamp for a non-box instance (merge first-wins, then the concurrent-instance rule)"
        elif pr is not None and (pr[1] != book[k]["Verdict"].strip().upper()):
            r_ = "export: the pooled book holds this Timestamp with a different verdict (merge first-wins)"
        elif pooled is None:
            r_ = "export: not in the export (no --pooled given, cause not classified)"
        else:
            r_ = "export: not in the export, cause not classified"
        why.setdefault(r_, []).append(k)
    return why


def load_pooled(path):
    """{ts: (InstanceId, VERDICT)} from the pooled book, plain split (the loader convention)."""
    d = {}
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        cols = f.readline().rstrip("\r\n").split(",")
        it, iv = cols.index("Timestamp"), cols.index("Verdict")
        ii = cols.index("InstanceId") if "InstanceId" in cols else -1
        for line in f:
            p_ = line.rstrip("\r\n").split(",")
            if len(p_) != len(cols): continue
            if p_[it] in d: continue
            d[p_[it]] = (p_[ii].strip() if ii >= 0 else "", p_[iv].strip().upper())
    return d


# ------------------------------------------------------------------ outcome statistics (full mode only)
def strat_key(r):
    return (r["fifth"], r["band"], r["poc"], r["seam"])


def arm_rw(pop_rows, is_a, is_b, strat, name, ctx):
    """A - B with B re-weighted to A's stratum mix, via the copied rw_run: B takes rw_run's MEDIUM slot (re-weighted)
    and A takes its WEAK slot, so rw_run returns B_rw - A. It is negated here to A - B, and the label is re-computed on
    the negated result with the copied label (its only sign-dependent part is the "d > 0 / d < 0" text)."""
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
    # rw_run appended its B - A label and FULL result to ALL_LABELS; replace that entry with the negated A - B one, so the
    # list never carries an inverted direction (review finding RV-6).
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
    """Holm step-down over the given {session: result}. A NOT READABLE result enters with p = 1. Adds p, alpha_i,
    the Holm-adjusted percentile CI of the FULL draws, and reject (step-down not stopped and adjusted CI excludes 0)."""
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


def finalize_label(x, sens, sens_seam=None):
    lab = x["label"]
    if lab.startswith("CONFIRMED") and not x["reject"]:
        lab = "NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (%s)" % ("d > 0" if x["res"]["FULL"][0] > 0 else "d < 0")
    if lab not in ("NOT READABLE", "NO DIFFERENCE SHOWN") and sens is not None:
        sp, fp = sens["res"]["FULL"][0], x["res"]["FULL"][0]
        if sp == sp and fp == fp and (sp > 0) != (fp > 0):
            lab += " EDGE-SENSITIVE"
    if lab not in ("NOT READABLE", "NO DIFFERENCE SHOWN") and sens_seam is not None:
        sp, fp = sens_seam["res"]["FULL"][0], x["res"]["FULL"][0]
        if sp == sp and fp == fp and (sp > 0) != (fp > 0):
            lab += " SEAM-SENSITIVE"
    x["seam_pt"] = sens_seam["res"]["FULL"][0] if sens_seam is not None else None
    x["edge_pt"] = sens["res"]["FULL"][0] if sens is not None else None
    x["final"] = lab
    return lab


def row_ci(res, h):
    r = res[h]
    return "%s (%d/%d)" % (ci(*r[:3]), r[3][0], r[3][1])


def run_full(cx, pop, opt):
    """The pre-registered statistics. Reads the outcome columns now, after the counts were printed."""
    outc = {e["Timestamp"]: e for e in read_export(opt.export, ["Timestamp"] + OUTCOME_COLS)}
    for r in pop:
        e = outc[r["key"]]
        r["ev_main"] = float(e["MainNetEv"]); r["outcome"] = int(e["MainOutcome"])
        r["tbps"] = float(e["TBps"]); r["sbps"] = float(e["SBps"])
    days = sorted(set(r["day"] for r in pop))
    D = len(days)
    split = days[D // 2] if D else None
    for r in pop:
        r["half"] = "H1" if r["day"] < split else "H2"
    cx.p()
    cx.p("## 3. Outcome read (main window, net EV per trade, bps, maker/maker %.2f bps)" % cx.fee)
    cx.p()
    cx.p("- Trading days D = %d; halves: H1 = first %d days (to %s exclusive), H2 = %d days." % (D, D // 2, split, D - D // 2))
    cx.p("- Bootstrap: %d resamples of whole UTC trading days, seed %d + call index; readable n >= %d." % (R, args.seed, FLOOR))
    cx.p()
    isA = lambda r: r["arm"] == "A"; isB = lambda r: r["arm"] == "B"
    cx.p("### 3.1 Arms explained: success rate, breakevens, edge, net EV per trade (A and B, raw, not re-weighted)")
    cx.p()
    cx.p("| Session | Arm | n | Success % | Gross breakeven % | Net breakeven % | Net edge pp | Net EV per trade [95 % CI] |")
    cx.p("|---|---|---|---|---|---|---|---|")
    for s in SESS_ORDER:
        for an, fn in (("A", isA), ("B", isB)):
            rs = [r for r in pop if r["s"] == s and fn(r)]
            n = len(rs)
            if n == 0:
                cx.p("| %s | %s | 0 | n/a | n/a | n/a | n/a | n/a |" % (s, an)); continue
            succ = 100.0 * sum(1 for r in rs if r["outcome"] == 1) / n
            # DeribitIndicatorProject.md §5a rule 1: pool breakevens with the Σ formulas (distance-weighted),
            # never a simple average of per-signal rates. Corrected by the orchestrator 2026-09-29, pre-outcome.
            den = sum(r["tbps"] + r["sbps"] for r in rs)
            gbe = 100.0 * sum(r["sbps"] for r in rs) / den
            nbe = 100.0 * sum(r["sbps"] + cx.fee for r in rs) / den
            b = boot(rs, lambda r: [(0, r["ev_main"])], 1, lambda s_, c: s_[0] / c[0] if c[0] > 0 else float("nan"))
            cx.p("| %s | %s | %d%s | %.1f | %.1f | %.1f | %+.1f | %s |" % (s, an, n, "" if n >= FLOOR else " (NOT READABLE)", succ, gbe, nbe, succ - nbe, ci(*b[:3])))
    cx.p()

    results = {}
    # ---- BO-H1
    h1 = {}; h1s = {}; h1q = {}
    for s in SESS_ORDER:
        sr = [r for r in pop if r["s"] == s]
        h1[s] = arm_rw(sr, isA, isB, strat_key, "BO-H1 A - B", "BO-H1 " + s)
        pre = [r for r in sr if r["poc"] == "pre"]
        h1s[s] = arm_rw(pre, isA, isB, strat_key, "BO-H1 pre-edge", "BO-H1 pre " + s) if pre else None
        pq = [r for r in sr if r["seam"] == "pre"]
        h1q[s] = arm_rw(pq, isA, isB, strat_key, "BO-H1 pre-seam", "BO-H1 pre-seam " + s) if pq else None
    holm(h1, 3)
    # ---- BO-H2
    h2 = {}; h2s = {}; h2q = {}
    in_c = lambda r: r["arm"] == "A" or r["sh"] == "S_add"
    out_c = lambda r: r["arm"] == "B" and r["sh"] != "S_add"
    for s in SESS_ORDER:
        sr = [r for r in pop if r["s"] == s and r["fifth"] >= 4]
        h2[s] = arm_rw(sr, in_c, out_c, strat_key, "BO-H2 (A+S_add) - (B-S_add)", "BO-H2 " + s)
        pre = [r for r in sr if r["poc"] == "pre"]
        h2s[s] = arm_rw(pre, in_c, out_c, strat_key, "BO-H2 pre-edge", "BO-H2 pre " + s) if pre else None
        pq = [r for r in sr if r["seam"] == "pre"]
        h2q[s] = arm_rw(pq, in_c, out_c, strat_key, "BO-H2 pre-seam", "BO-H2 pre-seam " + s) if pq else None
    readable_h2 = {s: x for s, x in h2.items() if x["label"] != "NOT READABLE"}
    holm(readable_h2, len(readable_h2))
    for s, x in h2.items():
        if s not in readable_h2:
            x.update({"p": 1.0, "alpha": float("nan"), "hci": (float("nan"), float("nan")), "reject": False})

    for hid, title, hres, hsens, hseam in (("BO-H1", "BO-H1 (primary): A - B, B re-weighted to A's stratum mix, all fifths; Holm over 3 sessions", h1, h1s, h1q),
                                    ("BO-H2", "BO-H2 (secondary, the (c) test): fifths 4-5, (A + S_add) - (B - S_add), re-weighted; Holm over readable sessions", h2, h2s, h2q)):
        cx.p("### 3.%d %s" % (2 if hid == "BO-H1" else 3, title))
        cx.p()
        # "Holm passed" (review finding RV-5, display only): the step-down had not stopped AND the Holm-adjusted FULL CI
        # excludes 0. It shows whether a result that is not CONFIRMED was stopped by Holm. The label wording is unchanged.
        cx.p("| Session | Strata | A rows dropped (stratum without B) | FULL d [95 % CI] (nA/nB) | H1 d (nA/nB) | H2 d (nA/nB) | boot p | Holm alpha | Holm-adjusted FULL CI | Holm passed | Pre-edge FULL d (nA/nB) | Pre-seam FULL d (nA/nB) | Label |")
        cx.p("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
        for s in SESS_ORDER:
            x = hres[s]; sv = hsens[s]; sq = hseam[s]
            lab = finalize_label(x, sv, sq)
            results[(hid, s)] = x
            cx.p("| %s | %d | %d | %s | %s | %s | %.4f | %s | [%s, %s] | %s | %s | %s | %s |" % (
                s, x["strata"], x["a_dropped"], row_ci(x["res"], "FULL"), row_ci(x["res"], "H1"), row_ci(x["res"], "H2"),
                x["p"], ("%.4f" % x["alpha"]) if x["alpha"] == x["alpha"] else "n/a", f1(x["hci"][0]), f1(x["hci"][1]),
                "yes" if x["reject"] else "no",
                row_ci(sv["res"], "FULL") if sv else "n/a (no pre-edge rows)",
                row_ci(sq["res"], "FULL") if sq else "n/a (no pre-seam rows)", lab))
        cx.p()

    # ---- BO-H3 and fifth cells: descriptive, never labelled
    cx.p("### 3.4 BO-H3 and fifth-level cells: DESCRIPTIVE ONLY (CIs printed, no label, no significance claim)")
    cx.p()
    cx.p("| Session | Comparison | FULL d [95 % CI] (n1/n2) | A rows dropped (stratum without B) |")
    cx.p("|---|---|---|---|")
    groups = [("low (fifths 1-2)", (1, 2)), ("mid (fifth 3)", (3,)), ("high (fifths 4-5)", (4, 5))] + [("fifth %d" % f, (f,)) for f in range(1, 6)]
    for s in SESS_ORDER:
        sr = [r for r in pop if r["s"] == s]
        for gn, fs in groups:
            x = arm_rw([r for r in sr if r["fifth"] in fs], isA, isB, strat_key, "H3 A - B " + gn, "H3 " + s)
            cx.p("| %s | A - B re-weighted, %s | %s | %d |" % (s, gn, row_ci(x["res"], "FULL"), x["a_dropped"]))
        x = arm_rw(sr, lambda r: r["sh"] == "S_add", lambda r: r["arm"] == "B" and r["sh"] != "S_add", strat_key, "H3 S_add - (B - S_add)", "H3 " + s)
        cx.p("| %s | S_add - (B - S_add) re-weighted, all fifths | %s | %d |" % (s, row_ci(x["res"], "FULL"), x["a_dropped"]))
        rs, _ = compare([r for r in sr if r["arm"] == "A"], lambda r: r["crossed"], lambda r: not r["crossed"], lambda r: r["ev_main"],
                        "H3 crossed - uncrossed A", "H3 " + s)
        cx.p("| %s | crossed A - uncrossed A (raw) | %s | - |" % (s, row_ci(rs, "FULL")))
        for en, efn in (("E1 (before v66)", lambda r: r["era4"] == "E1"), ("E2-E4 (from v66)", lambda r: r["era4"] != "E1")):
            x = arm_rw([r for r in sr if efn(r)], isA, isB, strat_key, "era A - B " + en, "era " + s)
            cx.p("| %s | A - B re-weighted, era %s | %s | %d |" % (s, en, row_ci(x["res"], "FULL"), x["a_dropped"]))
    cx.p()
    return results


# ------------------------------------------------------------------ one run
def run(opt):
    global args, R
    args = opt
    R = opt.resamples
    DRAWS.clear(); SEEDC[0] = 0; ALL_LABELS.clear()
    run_id = getattr(opt, "run", None)
    if not opt.counts_only:
        # Full mode only as a pinned run (RV-2). main() pins the values; this re-checks them for any caller.
        if run_id == "1":
            if opt.cut_before != RUN1_CUT or opt.from_ is not None or opt.seed != RUN_SEED:
                raise SystemExit("--run 1 must read Timestamp < %s, no --from, seed %d. STOP." % (RUN1_CUT, RUN_SEED))
        elif run_id == "2":
            if opt.from_ is None or opt.from_ < RUN1_CUT or opt.cut_before is not None or opt.seed != RUN_SEED:
                raise SystemExit("--run 2 needs --from >= %s, no --cut-before, seed %d. STOP." % (RUN1_CUT, RUN_SEED))
        else:
            raise SystemExit("full mode runs only as --run 1 or --run 2 --from <ts> (docs/burst-outcome-read-spec.md section 6.1). STOP.")
    cx = Ctx(opt.root)
    cx.p("# burst_outcome_read.py output (%s)" % ("COUNTS ONLY: no outcome column opened" if opt.counts_only else "FULL READ"))
    cx.p()
    if not opt.selftest_quiet:
        cx.p("- Run at (UTC): " + datetime.now(timezone.utc).strftime(FMT))
    cx.p("- Spec: docs/burst-outcome-read-spec.md section 5. settings.json version %s; fixed thresholds ASIA %.1f LONDON %.1f NY %.1f; upgrade_bonus %d."
         % (cx.cfg["version"], cx.fixed["ASIA"], cx.fixed["LONDON"], cx.fixed["NY"], cx.bonus))
    cx.p("- Data window: %s%s" % ("Timestamp < %s " % opt.cut_before if opt.cut_before else "", "Timestamp >= %s" % opt.from_ if opt.from_ else "")
         + ("(none: every row)" if not (opt.cut_before or opt.from_) else ""))
    cx.p("- Run: %s; seed %d; resamples %d." % ("--run " + run_id if run_id else "none (counts only)", opt.seed, opt.resamples))
    cx.p("- Export `%s` (MD5 %s)." % (opt.export, md5(opt.export)))

    book = load_books(cx, opt.fetch, opt)
    ref, x = reference_population(cx, book)
    export_rows = read_export(opt.export, SIGNAL_COLS + COVERAGE_COLS, optional=[COVERAGE_INFO_COL])
    pop, drops, n_win, smis, sigmis = analysis_population(cx, export_rows, book, opt)

    cx.p()
    cx.p("## 1. Population and exclusions")
    cx.p()
    cx.p("- Export rows inside the window: %d. Excluded, by reason:" % n_win)
    for k in sorted(drops): cx.p("  - %s: %d" % (k, len(drops[k])))
    cx.p("- Analysis population (directional, valid levels, armed, ratio present, in the fetch books): %d rows, first %s, last %s."
         % (len(pop), pop[0]["ts"].strftime(FMT) if pop else "n/a", pop[-1]["ts"].strftime(FMT) if pop else "n/a"))
    cx.p("- Checks: export Session differs from the hour's session: %d. Export AggrVelSignal differs from the fetch book: %d. Band mismatch on A rows: %d."
         % (smis, sigmis, sum(1 for r in pop if r["mis"])))
    cx.p("- Power-count reference population from the same books (tools/ops/burst-outcome-power-count.ps1 rule): weekday session rows=%d population=%d; excluded NO TRADE=%d lean forms=%d ASIA before arming=%d invalid placed levels=%d; directional valid armed rows=%d."
         % (x["total"], x["pop"], x["nonDir"], x["lean"], x["asiaUnarmed"], x["badLevels"], len(ref)))

    # reconciliation: reference - analysis
    pop_keys = set(r["key"] for r in pop)
    export_keys = set(e["Timestamp"] for e in export_rows)
    pooled = load_pooled(opt.pooled) if opt.pooled else None
    box_ids = set(b["InstanceId"] for b in book.values())
    why = classify_missing([k for k in ref if k not in pop_keys], export_keys, pooled, box_ids, book)
    for k in ref:
        if k in pop_keys or k not in export_keys: continue
        for rr_, ks in drops.items():
            if k in ks: why.setdefault("analysis exclusion: " + rr_, []).append(k); break
        else:
            why.setdefault(WHY_NO_REASON, []).append(k)
    extra = [r for r in pop if r["key"] not in ref]
    arm_diff = sum(1 for r in pop if r["key"] in ref and (ref[r["key"]]["arm"], ref[r["key"]]["sh"], ref[r["key"]]["fifth"]) != (r["arm"], r["sh"], r["fifth"]))
    n_drop = sum(len(v) for v in why.values())
    cx.p()
    cx.p("## 2. Arm counts")
    cx.p()
    cx.p("- Power-count rows NOT in the analysis population: %d, by reason:" % n_drop)
    for k in sorted(why): cx.p("  - %s: %d" % (k, len(why[k])))
    cx.p("- Analysis rows NOT in the power-count population (must be 0): %d. Rows in both with a different arm, shadow arm or fifth (must be 0): %d."
         % (len(extra), arm_diff))
    cx.p()
    cx.p("```")
    counts_table(cx, list(ref.values()), "=== 2a. Power-count reference (the books, the power count's rule): session x ATR fifth x arm ===")
    cx.p("")
    counts_table(cx, pop, "=== 2b. Analysis population (export joined to the books): session x ATR fifth x arm ===")
    cx.p("")
    dropped_rows = [ref[k] for ks in why.values() for k in ks]
    counts_table(cx, dropped_rows, "=== 2c. Dropped (2a minus 2b, the rows counted above by reason) ===")
    cx.p("")
    era_table(cx, pop)
    cx.p("```")
    cx.p()
    cx.p("- POC era (analysis population): " + "  ".join("%s pre A/B %d/%d post A/B %d/%d" % (
        s, sum(1 for r in pop if r["s"] == s and r["poc"] == "pre" and r["arm"] == "A"), sum(1 for r in pop if r["s"] == s and r["poc"] == "pre" and r["arm"] == "B"),
        sum(1 for r in pop if r["s"] == s and r["poc"] == "post" and r["arm"] == "A"), sum(1 for r in pop if r["s"] == s and r["poc"] == "post" and r["arm"] == "B"))
        for s in SESS_ORDER))
    cx.p("- Daylight-saving seam era (analysis population; ASIA/LONDON seam %s, NY seam %s): " % (SEAM["ASIA"].strftime(FMT), SEAM["NY"].strftime(FMT)) + "  ".join(
        "%s pre A/B %d/%d post A/B %d/%d" % (
            s, sum(1 for r in pop if r["s"] == s and r["seam"] == "pre" and r["arm"] == "A"), sum(1 for r in pop if r["s"] == s and r["seam"] == "pre" and r["arm"] == "B"),
            sum(1 for r in pop if r["s"] == s and r["seam"] == "post" and r["arm"] == "A"), sum(1 for r in pop if r["s"] == s and r["seam"] == "post" and r["arm"] == "B"))
        for s in SESS_ORDER))
    # ---- coverage (RV-1): MainMissingBars, read with the signal columns, before any outcome column
    miss = {(s, a): sum(1 for r in pop if r["s"] == s and r["arm"] == a and (r["missing"] or 0) > 0) for s in SESS_ORDER for a in "ABCO"}
    n_miss = sum(1 for r in pop if r["missing"] is not None and r["missing"] > 0)
    n_unparsed = sum(1 for r in pop if r["missing"] is None)
    cx.p("- Coverage (MainMissingBars > 0 = the main-window walk lacks at least one 1-minute bar; a coverage column, not an outcome). Population rows, by session and arm: "
         + "  ".join("%s A %d B %d C %d O %d" % (s, miss[(s, "A")], miss[(s, "B")], miss[(s, "C")], miss[(s, "O")]) for s in SESS_ORDER)
         + ". Total %d; MainMissingBars not parseable: %d." % (n_miss, n_unparsed))
    # ---- full-window coverage (RVF-3 (c)): INFORMATION ONLY, NOT A GATE. Looked up from the export rows by Timestamp and
    # never copied into a population row; it is printed here and returned in info for the selftest, and nothing else
    # reads it: no gate below, no label, no filter, no outcome.
    has_fw = bool(export_rows) and COVERAGE_INFO_COL in export_rows[0]
    fw_miss = None
    if has_fw:
        fw = {e["Timestamp"]: tryi(e[COVERAGE_INFO_COL]) for e in export_rows}
        fw_miss = {(s, a): sum(1 for r in pop if r["s"] == s and r["arm"] == a and (fw[r["key"]] or 0) > 0) for s in SESS_ORDER for a in "ABCO"}
        fw_n = sum(1 for r in pop if fw[r["key"]] is not None and fw[r["key"]] > 0)
        fw_bars = sum(fw[r["key"]] for r in pop if fw[r["key"]] is not None and fw[r["key"]] > 0)
        fw_unparsed = sum(1 for r in pop if fw[r["key"]] is None)
        cx.p("- Full-window coverage, information only, not a gate (%s > 0 = the row's whole main window lacks at least one 1-minute bar, counted past the resolving bar). Population rows, by session and arm: " % COVERAGE_INFO_COL
             + "  ".join("%s A %d B %d C %d O %d" % (s, fw_miss[(s, "A")], fw_miss[(s, "B")], fw_miss[(s, "C")], fw_miss[(s, "O")]) for s in SESS_ORDER)
             + ". Total %d rows, %d missing bars; not parseable: %d." % (fw_n, fw_bars, fw_unparsed))
    else:
        cx.p("- Full-window coverage, information only, not a gate: column %s absent from this export (written before the column existed)." % COVERAGE_INFO_COL)

    # ---- pre-outcome gates (RV-1, RV-2). Printed in both modes; full mode STOPs on any FAIL, before run_full.
    pin_v = RUN_SETTINGS_VERSION.get(run_id or "1")
    gates = [
        ("missing_bars", n_miss == 0 and n_unparsed == 0, "population rows with MainMissingBars > 0 or not parseable = %d (must be 0)" % (n_miss + n_unparsed)),
        ("extra", len(extra) == 0, "analysis rows not in the power-count population = %d (must be 0)" % len(extra)),
        ("arm_diff", arm_diff == 0, "rows in both with a different arm, shadow arm or fifth = %d (must be 0)" % arm_diff),
        ("session_mismatch", smis == 0, "export Session differs from the hour's session = %d (must be 0)" % smis),
        ("signal_mismatch", sigmis == 0, "export AggrVelSignal differs from the fetch book = %d (must be 0)" % sigmis),
        ("verdict_differs", len(drops.get("verdict differs between export and fetch book", [])) == 0,
         "export rows dropped as 'verdict differs between export and fetch book' = %d (must be 0)" % len(drops.get("verdict differs between export and fetch book", []))),
        ("side_tier_inconsistent", len(drops.get("Side/Tier inconsistent with Verdict", [])) == 0,
         "export rows dropped as 'Side/Tier inconsistent with Verdict' = %d (must be 0)" % len(drops.get("Side/Tier inconsistent with Verdict", []))),
        # RVF-2 = (b): both should be 0; both can only stop, never relabel
        ("not_directional", len(drops.get(DROP_NOT_DIRECTIONAL, [])) == 0,
         "export rows dropped as '%s' = %d (must be 0)" % (DROP_NOT_DIRECTIONAL, len(drops.get(DROP_NOT_DIRECTIONAL, [])))),
        ("no_recorded_reason", len(why.get(WHY_NO_REASON, [])) == 0,
         "power-count rows %s = %d (must be 0)" % (WHY_NO_REASON, len(why.get(WHY_NO_REASON, [])))),
        # Run 2 has no pinned version yet, so this gate FAILS for run 2 until the run-2 rider (RV-4) pins one: run 2 cannot
        # read outcomes under a settings version nobody pre-registered.
        ("settings_version", pin_v is not None and cx.cfg["version"] == pin_v,
         "settings.json version %s, pinned %s%s" % (cx.cfg["version"], pin_v if pin_v is not None else "none",
                                                    " (run 2: not pinned yet, the run-2 rider RV-4 must pin it)" if pin_v is None else " (run 1)")),
    ]
    cx.p("- Pre-outcome gates (full mode STOPs on any FAIL before an outcome column is opened; counts-only prints them and goes on):")
    for name, ok, text in gates:
        cx.p("  - %s %s: %s" % ("PASS" if ok else "FAIL", name, text))
    failed = [name for name, ok, _ in gates if not ok]
    info = {"pop": pop, "ref": ref, "refx": x, "drops": drops, "why": why, "extra": len(extra), "arm_diff": arm_diff,
            "miss": miss, "fw_miss": fw_miss, "gates_failed": failed}
    if opt.counts_only:
        cx.p()
        cx.p("COUNTS ONLY: exiting before any outcome column is opened.")
        return cx, info, None
    if failed:
        cx.p()
        cx.p("STOP: pre-outcome gate(s) failed: %s. No outcome column was opened." % ", ".join(failed))
        raise GateStop(cx, failed)
    results = run_full(cx, pop, opt)
    return cx, info, results


# ------------------------------------------------------------------ selftest (synthetic data only)
BOOK_HDR = BOOK_NEED + ["InstanceId"]


NDAYS = 80   # weekdays from 2026-08-03 to 2026-11-20: spans the ASIA/LONDON seam (2026-10-25) and the NY seam (2026-11-01)


def st_build(tmp, null_case, seed, flip=False, const=False):
    """Synthetic fetch folder + export. NDAYS (80) weekdays from 2026-08-03, three sessions; per session-day 5 A rows, each
    with two B rows of the same stratum (fifth, band, POC era, seam era), plus C, O and excluded rows. Effect case: A EV =
    +5 + noise, B EV = noise. Null case: each B row carries its A row's EV, so the re-weighted A - B is exactly 0 in every
    resample. Flip case (AT-2): A EV = -4 + noise before the row's session seam and +40 + noise from it, B EV = noise, so
    the pooled A - B is positive and the pre-seam-only A - B is negative: "SEAM-SENSITIVE" must appear on the BO-H1 label.
    Const case (review finding RV-3), no noise: every B row (S_add included) EV = 0; A EV = +2 before the POC edge, -10
    from the POC edge to the row's session seam, +40 from the seam. So the pre-POC-only A - B is positive (same sign as
    the pooled one: no EDGE-SENSITIVE) while the pre-seam-only A - B is negative (SEAM-SENSITIVE): the seam filter and
    the POC filter give opposite signs. And S_add rows (EV 0) dilute BO-H2's upgraded set by a known amount."""
    rng = random.Random(seed)
    fetch = os.path.join(tmp, "fetch"); os.makedirs(fetch)
    mids = {"ASIA": [25.0, 40.0, 60.0, 75.0, 95.0], "LONDON": [25.0, 40.0, 60.0, 80.0, 100.0], "NY": [15.0, 25.0, 37.0, 50.0, 70.0]}
    hours = {"ASIA": 2, "LONDON": 9, "NY": 15}
    eff = {"STRONG": 15, "MEDIUM": 11, "WEAK": 8}
    verdict = {("LONG", "STRONG"): "STRONG LONG", ("LONG", "MEDIUM"): "LONG", ("LONG", "WEAK"): "WEAK LONG",
               ("SHORT", "STRONG"): "STRONG SHORT", ("SHORT", "MEDIUM"): "SHORT", ("SHORT", "WEAK"): "WEAK SHORT"}
    book_rows, export_rows, expect = [], [], {s: {"A": 0, "B": 0, "C": 0, "O": 0, "S_add": 0} for s in SESS_ORDER}
    d = datetime(2026, 8, 3)
    days = []
    while len(days) < NDAYS:
        if d.weekday() < 5: days.append(d)
        d += timedelta(days=1)
    price = 60000.0
    def mk(ts, sess, side, band, fifth, tfi, sig_, ratio, net):
        atr = mids[sess][fifth - 1]
        sg = 1 if side == "LONG" else -1
        tgt, stp = price + sg * 2 * atr, price - sg * 1.5 * atr
        el = eff[band] if side == "LONG" else 3
        es = eff[band] if side == "SHORT" else 3
        b = {"Timestamp": ts.strftime(FMT), "Price": "%.1f" % price, "Verdict": verdict[(side, band)], "ATR": "%.1f" % atr,
             "AggrVelBurstRatio": "" if ratio is None else "%.2f" % ratio, "AggrVelNet": "%.1f" % net, "AggrVelSignal": sig_,
             "TFISignal": tfi, "MaxScore": "19", "EffectiveLongScore": str(el), "EffectiveShortScore": str(es),
             "PlacedTargetLong": "%.1f" % (tgt if side == "LONG" else price + 80), "PlacedStopLong": "%.1f" % (stp if side == "LONG" else price - 60),
             "PlacedTargetShort": "%.1f" % (tgt if side == "SHORT" else price - 80), "PlacedStopShort": "%.1f" % (stp if side == "SHORT" else price + 60),
             "InstanceId": "synthetic01"}
        e = {"Timestamp": b["Timestamp"], "Session": sess, "Side": side, "Tier": band, "Verdict": b["Verdict"], "Price": b["Price"],
             "Atr": b["ATR"], "AggrVelSignal": sig_, "MaxScore": "19", "EffectiveLongScore": str(el), "EffectiveShortScore": str(es),
             "InstanceId": "synthetic01", "TBps": "%.4f" % (2 * atr / price * 1e4), "SBps": "%.4f" % (1.5 * atr / price * 1e4),
             "MainMissingBars": "0", COVERAGE_INFO_COL: "0"}
        return b, e
    for di, day in enumerate(days):
        for s in SESS_ORDER:
            t = day.replace(hour=hours[s])
            sec = [0]
            def nts():
                sec[0] += 7
                return t + timedelta(seconds=sec[0])
            for j in range(5):
                fifth = 1 + (di + j) % 5
                band = TIERS[(di + 2 * j) % 3]
                side = "LONG" if (di + j) % 2 == 0 else "SHORT"
                tfi = "BUY PRESSURE" if side == "LONG" else "SELL PRESSURE"
                burst = "BURST_BUY" if side == "LONG" else "BURST_SELL"
                sg = 1 if side == "LONG" else -1
                if const: mu_a = 2.0 if t < POC_EDGE else (-10.0 if t < SEAM[s] else 40.0)
                elif flip: mu_a = -4.0 if t < SEAM[s] else 40.0
                else: mu_a = 0.0 if null_case else 5.0
                ev_a = mu_a if const else mu_a + rng.gauss(0, 10)
                b, e = mk(nts(), s, side, band, fifth, tfi, burst, 9.5, sg * 100.0)
                e["MainNetEv"] = "%.6f" % ev_a; e["MainOutcome"] = "1" if ev_a > 0 else "2"
                book_rows.append(b); export_rows.append(e)
                expect[s]["A"] += 1
                for k in range(2):
                    ratio = 9.5 if (j == 0 and k == 0) else 1.0   # one S_add per session-day
                    b, e = mk(nts(), s, side, band, fifth, tfi, "NORMAL", ratio, sg * 50.0)
                    ev_b = 0.0 if const else (ev_a if null_case else rng.gauss(0, 10))
                    e["MainNetEv"] = "%.6f" % ev_b; e["MainOutcome"] = "1" if ev_b > 0 else "2"
                    book_rows.append(b); export_rows.append(e)
                    expect[s]["B"] += 1
                    if ratio > 9.0: expect[s]["S_add"] += 1
            # C, O, and rows the population must exclude
            b, e = mk(nts(), s, "LONG", "MEDIUM", 3, "BUY PRESSURE", "BURST_SELL", 9.5, -100.0)
            e["MainNetEv"] = "0"; e["MainOutcome"] = "2"; book_rows.append(b); export_rows.append(e); expect[s]["C"] += 1
            b, e = mk(nts(), s, "LONG", "MEDIUM", 3, "NEUTRAL", "NORMAL", 1.0, 0.0)
            e["MainNetEv"] = "0"; e["MainOutcome"] = "2"; book_rows.append(b); export_rows.append(e); expect[s]["O"] += 1
            b, e = mk(nts(), s, "LONG", "MEDIUM", 3, "BUY PRESSURE", "NORMAL", None, 0.0)   # ratio empty
            e["MainNetEv"] = "0"; e["MainOutcome"] = "2"; book_rows.append(b); export_rows.append(e)
    # 2 export rows that no book holds; 1 book row that the export lacks
    for k in range(2):
        ts = days[0].replace(hour=14) + timedelta(seconds=k)
        _, e = mk(ts, "NY", "LONG", "MEDIUM", 3, "BUY PRESSURE", "NORMAL", 1.0, 0.0)
        e["MainNetEv"] = "0"; e["MainOutcome"] = "2"; export_rows.append(e)
    b, _ = mk(days[1].replace(hour=16), "NY", "LONG", "MEDIUM", 3, "BUY PRESSURE", "NORMAL", 1.0, 0.0)
    book_rows.append(b); expect["NY"]["B_book_only"] = 1
    # 3 ASIA A-type rows before arming (Friday 2026-07-31): in the books and the export, excluded from both populations
    for k in range(3):
        b, e = mk(datetime(2026, 7, 31, 2, 0, k), "ASIA", "LONG", "MEDIUM", 3, "BUY PRESSURE", "BURST_BUY", 9.5, 100.0)
        e["MainNetEv"] = "0"; e["MainOutcome"] = "2"; book_rows.append(b); export_rows.append(e)
    expect["ASIA_unarmed_export_rows"] = 3
    book_rows.sort(key=lambda r: r["Timestamp"]); export_rows.sort(key=lambda r: r["Timestamp"])
    # books: rows split across v0.7.bak (first third), a rotated book with a different column order (middle third,
    # plus a same-Timestamp copy of the first live row with a different verdict, which must win) and the live log.
    n = len(book_rows); a_, b_ = n // 3, 2 * n // 3
    def write_book(name, rows, hdr):
        with open(os.path.join(fetch, name), "w", encoding="utf-8", newline="") as f:
            f.write(",".join(hdr) + "\r\n")
            for r in rows:
                f.write(",".join(r[c] for c in hdr) + "\r\n")
    write_book("analysis_log.csv.v0.7.bak", book_rows[:a_], BOOK_HDR)
    rot_hdr = ["Extra"] + list(reversed(BOOK_HDR))
    rot_rows = [dict(r, Extra="x") for r in book_rows[a_:b_]]
    write_book("analysis_log.csv.116col-00000000.20260920_000000.bak", rot_rows, rot_hdr)
    live = [dict(r) for r in book_rows[b_:]]
    shadow_copy = dict(live[0]); live[0] = dict(live[0], Verdict="NO TRADE")   # the rotated copy (real verdict) must win
    rot_rows.append(dict(shadow_copy, Extra="x"))
    write_book("analysis_log.csv.116col-00000000.20260920_000000.bak", rot_rows, rot_hdr)
    write_book("analysis_log.csv", live, BOOK_HDR)
    ex = os.path.join(tmp, "export.csv")
    ehdr = SIGNAL_COLS + COVERAGE_COLS + OUTCOME_COLS + [COVERAGE_INFO_COL]   # the info column last, as the real export
    with open(ex, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n"); w.writerow(ehdr)
        for e in export_rows: w.writerow([e[c] for c in ehdr])
    ex_sig = os.path.join(tmp, "export-signal-only.csv")   # no outcome column at all: --counts-only must still run
    with open(ex_sig, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n"); w.writerow(SIGNAL_COLS + COVERAGE_COLS + [COVERAGE_INFO_COL])
        for e in export_rows: w.writerow([e[c] for c in SIGNAL_COLS + COVERAGE_COLS + [COVERAGE_INFO_COL]])
    return fetch, ex, ex_sig, expect


def st_root(tmp, real_root, version):
    """A settings root for the selftest: the tracked settings.json copied, with only "version" set. The selftest tests the
    code; the real run's settings_version gate reads the real file."""
    rt = os.path.join(tmp, "root"); os.makedirs(rt, exist_ok=True)
    cfg = json.load(open(os.path.join(real_root, "settings.json"), encoding="utf-8-sig"))
    cfg["version"] = version
    with open(os.path.join(rt, "settings.json"), "w", encoding="utf-8") as f:
        json.dump(cfg, f)
    return rt


def quiet_run(o):
    with open(os.devnull, "w", encoding="utf-8") as dn:
        so = sys.stdout; sys.stdout = dn
        try:
            return run(o)
        finally:
            sys.stdout = so


def st_draws(center, n=10000, spread=10.0):
    """Evenly spaced draws, so every bootstrap p is known exactly (the review's H-7 unit check)."""
    return sorted(center - spread + 2 * spread * i / (n - 1) for i in range(n))


def selftest_holm(check):
    """Deterministic Holm and label assertions (review handle H-7, finding RV-3). Draws centred at c, spread 10, give
    p = 2 x (10 - c) / 20: c 9.9 -> 0.01, 9.7 -> 0.03, 9.6 -> 0.04."""
    print("\n==== SELFTEST case: Holm step-down and the TB-D5 label (deterministic draws) ====")
    mk = lambda c, lab: {"draws": st_draws(c), "label": lab, "res": {"FULL": (c, 0.0, 0.0, [200, 200])}}
    Rr = {"ASIA": mk(9.9, "CONFIRMED (d > 0)"), "LONDON": mk(9.7, "CONFIRMED (d > 0)"), "NY": mk(9.6, "CONFIRMED (d > 0)")}
    holm(Rr, 3)
    for s in SESS_ORDER:
        finalize_label(Rr[s], None, None)
    check(abs(Rr["ASIA"]["p"] - 0.01) < 1e-9 and abs(Rr["LONDON"]["p"] - 0.03) < 1e-9 and abs(Rr["NY"]["p"] - 0.04) < 1e-9,
          "Holm unit: boot p = 0.01 / 0.03 / 0.04 (got %.4f / %.4f / %.4f)" % (Rr["ASIA"]["p"], Rr["LONDON"]["p"], Rr["NY"]["p"]))
    check(abs(Rr["ASIA"]["alpha"] - 0.05 / 3) < 1e-12 and abs(Rr["LONDON"]["alpha"] - 0.05 / 2) < 1e-12 and abs(Rr["NY"]["alpha"] - 0.05) < 1e-12,
          "Holm unit: alpha_i = 0.05/3, 0.05/2, 0.05 by rank")
    check(Rr["ASIA"]["reject"] is True, "Holm unit: ASIA (p 0.01 <= 0.0167) passes Holm")
    check(Rr["LONDON"]["reject"] is False, "Holm unit: LONDON (p 0.03 > 0.025) is stopped")
    check(Rr["NY"]["p"] <= 0.05 and Rr["NY"]["reject"] is False,
          "Holm unit: NY is NOT passed although its own p 0.04 <= 0.05 - the step-down stopped at LONDON")
    check(Rr["ASIA"]["final"] == "CONFIRMED (d > 0)", "Holm unit: ASIA final label CONFIRMED (d > 0) (got %r)" % Rr["ASIA"]["final"])
    want = "NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (d > 0)"
    check(Rr["LONDON"]["final"] == want and Rr["NY"]["final"] == want,
          "Holm unit: LONDON and NY reach the TB-D5 label %r (got %r / %r)" % (want, Rr["LONDON"]["final"], Rr["NY"]["final"]))
    R2 = {"ASIA": mk(9.9, "CONFIRMED (d > 0)"), "LONDON": mk(9.0, "NOT READABLE"), "NY": mk(9.9, "CONFIRMED (d > 0)")}
    holm(R2, 3)
    for s in SESS_ORDER:
        finalize_label(R2[s], None, None)
    check(R2["LONDON"]["p"] == 1.0 and abs(R2["LONDON"]["alpha"] - 0.05) < 1e-12 and R2["LONDON"]["reject"] is False
          and R2["LONDON"]["final"] == "NOT READABLE", "Holm unit: a NOT READABLE session enters with p = 1, ranks last, is not passed")
    check(abs(min(R2["ASIA"]["alpha"], R2["NY"]["alpha"]) - 0.05 / 3) < 1e-12 and abs(max(R2["ASIA"]["alpha"], R2["NY"]["alpha"]) - 0.05 / 2) < 1e-12
          and R2["ASIA"]["reject"] and R2["NY"]["reject"], "Holm unit: the two readable sessions get 0.05/3 and 0.05/2 and both pass")


def selftest_run_pins(check):
    """--run pins (RV-2): a full read cannot start unpinned, and a pinned run takes no free window, seed or resamples."""
    print("\n==== SELFTEST case: run pins (argument resolution) ====")
    ap = build_parser()
    def res(argv):
        try:
            return resolve_run(ap.parse_args(argv)), None
        except ValueError as ex:
            return None, str(ex)
    for argv, why in (([], "full mode without --run"), (["--run", "1", "--cut-before", "2026-12-01T00:00:00Z"], "--run 1 with --cut-before"),
                      (["--run", "1", "--from", "2026-09-01T00:00:00Z"], "--run 1 with --from"), (["--run", "1", "--seed", "1"], "--run 1 with --seed"),
                      (["--run", "1", "--resamples", "500"], "--run 1 with --resamples"), (["--run", "2"], "--run 2 without --from"),
                      (["--run", "2", "--from", "2026-11-01T00:00:00Z"], "--run 2 --from before the run-1 cut"),
                      (["--run", "2", "--from", "2026-12-07T00:00:00Z", "--cut-before", "2027-01-01T00:00:00Z"], "--run 2 with --cut-before")):
        o, err = res(argv)
        check(o is None and err, "run pins: refused: %s (%s)" % (why, err))
    o, err = res(["--run", "1"])
    check(err is None and o.cut_before == RUN1_CUT and o.from_ is None and o.seed == 20260928 and o.resamples == 10000,
          "run pins: --run 1 gives Timestamp < 2026-11-25 00:00:00, seed 20260928, 10,000 resamples")
    o, err = res(["--run", "2", "--from", "2026-12-07T00:00:00Z"])
    check(err is None and o.from_ == datetime(2026, 12, 7) and o.cut_before is None and o.seed == 20260928 and o.resamples == 10000,
          "run pins: --run 2 --from 2026-12-07 is accepted with the same seed and resamples")
    o, err = res(["--counts-only", "--cut-before", "2026-09-01T00:00:00Z"])
    check(err is None and o.cut_before == datetime(2026, 9, 1) and o.run is None, "run pins: counts-only keeps a free window")


def st_edit_csv(path, ts, edit):
    """Rewrites every row of a synthetic CSV whose Timestamp is ts, applying edit(row_dict). Keeps the column order."""
    with open(path, encoding="utf-8", newline="") as f:
        rows = list(csv.reader(f))
    h = rows[0]; it = h.index("Timestamp"); n = 0
    for i in range(1, len(rows)):
        if rows[i] and rows[i][it] == ts:
            d = dict(zip(h, rows[i])); edit(d); rows[i] = [d[c] for c in h]; n += 1
    with open(path, "w", encoding="utf-8", newline="") as f:
        csv.writer(f, lineterminator="\r\n" if "analysis_log" in os.path.basename(path) else "\n").writerows(rows)
    return n


def selftest_gates(real_root, check, resamples):
    """Pre-outcome gates (RV-1, RV-2). Each variant breaks ONE check on one NY arm-A row (or the settings version); full
    mode must STOP naming exactly that check before an outcome column is opened, and counts-only must print it and go on."""
    print("\n==== SELFTEST case: pre-outcome gates (one broken check per variant) ====")
    tmp = tempfile.mkdtemp(prefix="burst_selftest_gates_")
    try:
        fetch0, ex0, _, _ = st_build(os.path.join(tmp, "base"), True, 20260929)
        with open(ex0, encoding="utf-8", newline="") as f:
            # ATR 15.0 = fifth 1 under both the NY and the LONDON edges, so the session_mismatch variant moves no fifth
            target = next(r for r in csv.DictReader(f) if r["Session"] == "NY" and r["Side"] == "LONG" and r["AggrVelSignal"] == "BURST_BUY"
                          and r["Atr"] == "15.0")
        ts = target["Timestamp"]
        other_tier = "STRONG" if target["Tier"] != "STRONG" else "WEAK"
        other_atr = "70.0"   # NY fifth 5
        short_v = {"STRONG": "STRONG SHORT", "MEDIUM": "SHORT", "WEAK": "WEAK SHORT"}[target["Tier"]]
        variants = [
            ("missing_bars", "export", lambda d: d.update(MainMissingBars="3"), None),
            ("session_mismatch", "export", lambda d: d.update(Session="LONDON"), None),
            ("signal_mismatch", "export", lambda d: d.update(AggrVelSignal="NORMAL"), None),
            ("verdict_differs", "export", lambda d: d.update(Verdict=short_v), None),
            ("side_tier_inconsistent", "export", lambda d: d.update(Tier=other_tier), None),
            ("arm_diff", "export", lambda d: d.update(Atr=other_atr), None),
            ("extra", "books", lambda d: d.update(PlacedTargetLong="0.0"), None),
            # RVF-2 = (b). A NO TRADE row in the export and the books (so "verdict differs" cannot fire first)
            ("not_directional", "both", lambda d: d.update(Verdict="NO TRADE"), None),
            # RVF-2 = (b). No data shape reaches this bucket: analysis_population records a reason for every row it
            # drops. The variant injects the defect the gate exists for: a population step that silently loses a row.
            ("no_recorded_reason", "patch", None, None),
            ("settings_version", None, None, RUN_SETTINGS_VERSION["1"] + 1),
        ]
        for name, where, edit, version in variants:
            vt = os.path.join(tmp, "v_" + name)
            shutil.copytree(os.path.dirname(fetch0), vt)
            fetch, ex = os.path.join(vt, "fetch"), os.path.join(vt, "export.csv")
            n_ed = 0
            if where in ("export", "both"):
                n_ed = st_edit_csv(ex, ts, edit)
            if where in ("books", "both"):
                for bp in book_names(fetch):
                    n_ed += st_edit_csv(bp, ts, edit)
            real_ap = globals()["analysis_population"]
            if where == "patch":
                def lossy_ap(cx_, export_rows_, book_, opt_, _real=real_ap, _ts=ts):
                    pop_, drops_, n_win_, smis_, sigmis_ = _real(cx_, export_rows_, book_, opt_)
                    return [r for r in pop_ if r["key"] != _ts], drops_, n_win_, smis_, sigmis_
                globals()["analysis_population"] = lossy_ap
                n_ed = 1
            try:
                rt = st_root(vt, real_root, version if version is not None else RUN_SETTINGS_VERSION["1"])
                base = dict(root=rt, export=ex, fetch=fetch, pooled=None, from_=None, resamples=resamples, seed=RUN_SEED, selftest_quiet=True)
                got = None
                try:
                    quiet_run(argparse.Namespace(**dict(base, run="1", cut_before=RUN1_CUT, counts_only=False)))
                except GateStop as g:
                    got = g.names
                check(got == [name], "gate %s: full mode STOPs naming exactly [%s] before any outcome column (edited %d rows; got %s)" % (name, name, n_ed, got))
                cx, info, res_ = quiet_run(argparse.Namespace(**dict(base, run=None, cut_before=None, counts_only=True)))
            finally:
                globals()["analysis_population"] = real_ap
            check(res_ is None and info["gates_failed"] == [name] and any(ln.startswith("  - FAIL %s:" % name) for ln in cx.out),
                  "gate %s: counts-only prints FAIL %s and does not stop (got %s)" % (name, name, info["gates_failed"]))
            if name == "missing_bars":
                check(info["miss"][("NY", "A")] == 1 and sum(info["miss"].values()) == 1,
                      "gate missing_bars: the coverage line counts the row under NY arm A only")
            if name == "not_directional":
                check(len(info["drops"].get(DROP_NOT_DIRECTIONAL, [])) == 1, "gate not_directional: exactly the edited row is dropped as not directional")
            if name == "no_recorded_reason":
                check(info["why"].get(WHY_NO_REASON) == [ts], "gate no_recorded_reason: exactly the silently lost row is counted (got %s)" % info["why"].get(WHY_NO_REASON))
        selftest_coverage_info(tmp, fetch0, ts, real_root, check)
        # full mode refuses to start unpinned, even when a caller bypasses main()
        rt = st_root(os.path.join(tmp, "base"), real_root, RUN_SETTINGS_VERSION["1"])
        for run_id, cut, why in ((None, None, "no --run"), ("1", None, "--run 1 without its cut")):
            try:
                quiet_run(argparse.Namespace(root=rt, export=ex0, fetch=fetch0, pooled=None, from_=None, resamples=resamples,
                                             seed=RUN_SEED, selftest_quiet=True, run=run_id, cut_before=cut, counts_only=False))
                refused = False
            except SystemExit:
                refused = True
            check(refused, "run(): full mode refuses to start with %s" % why)
        # run 2 has no pinned settings version yet: its full mode must STOP on settings_version
        got = None
        try:
            quiet_run(argparse.Namespace(root=rt, export=ex0, fetch=fetch0, pooled=None, from_=datetime(2026, 12, 7), resamples=resamples,
                                         seed=RUN_SEED, selftest_quiet=True, run="2", cut_before=None, counts_only=False))
        except GateStop as g:
            got = g.names
        check(got is not None and "settings_version" in got, "gate settings_version: --run 2 STOPs while run 2 has no pinned version (got %s)" % got)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def selftest_coverage_info(tmp, fetch0, ts, real_root, check):
    """RVF-3 (c): MainFullWindowMissingBars is information only. One NY arm-A row gets a full-window gap after its
    resolution (MainFullWindowMissingBars 5) with MainMissingBars 0. Failing inputs: a gate on the column STOPs full mode
    or lists a failed gate; a filter or an outcome fed by it changes a line of the full read against the unedited
    baseline; a missing print or a mis-binned count fails the coverage-line checks; a hard requirement on the header
    fails the "absent" run."""
    print("\n==== SELFTEST case: full-window coverage column is information only (RVF-3 (c)) ====")
    rs = 200   # equality against the baseline, not a statistic: a small resample count is enough and keeps it fast
    outs = {}
    for tag, edit in (("base", None), ("gap", lambda d: d.update(**{COVERAGE_INFO_COL: "5"}))):
        vt = os.path.join(tmp, "cov_" + tag)
        shutil.copytree(os.path.dirname(fetch0), vt)
        fetch, ex = os.path.join(vt, "fetch"), os.path.join(vt, "export.csv")
        if edit is not None:
            n_ed = st_edit_csv(ex, ts, edit)
            check(n_ed == 1, "coverage info: one export row edited (MainFullWindowMissingBars 5, MainMissingBars 0)")
        rt = st_root(vt, real_root, RUN_SETTINGS_VERSION["1"])
        base = dict(root=rt, export=ex, fetch=fetch, pooled=None, from_=None, resamples=rs, seed=RUN_SEED, selftest_quiet=True)
        cx, info, _ = quiet_run(argparse.Namespace(**dict(base, run=None, cut_before=None, counts_only=True)))
        if tag == "gap":
            check(info["gates_failed"] == [] and info["fw_miss"] is not None and info["fw_miss"][("NY", "A")] == 1 and sum(info["fw_miss"].values()) == 1,
                  "coverage info: counts-only passes every gate and counts the row under NY arm A only (got gates %s)" % info["gates_failed"])
            line = [ln for ln in cx.out if ln.startswith("- Full-window coverage, information only, not a gate")]
            check(len(line) == 1 and "Total 1 rows, 5 missing bars" in line[0], "coverage info: counts-only prints the labelled line (got %s)" % line)
            check(all(r["missing"] == 0 for r in info["pop"]) and not any(COVERAGE_INFO_COL in r or "fw" in r for r in info["pop"]),
                  "coverage info: MainMissingBars stays 0 and the column is not copied into a population row")
        try:
            cx, info, res_ = quiet_run(argparse.Namespace(**dict(base, run="1", cut_before=RUN1_CUT, counts_only=False)))
            outs[tag] = (cx.out, info, res_)
        except GateStop as g:
            outs[tag] = None
            check(False, "coverage info (%s): full mode STOPPED on %s" % (tag, g.names))
    if outs.get("base") and outs.get("gap"):
        (ob, ib, rb), (og, ig, rg) = outs["base"], outs["gap"]
        check(ig["gates_failed"] == [] and rg is not None, "coverage info: full mode does not STOP on a full-window gap and reads on")
        keep = lambda o: [ln for ln in o if not ln.startswith("- Export `") and not ln.startswith("- Full-window coverage")]
        diff = [(a, b) for a, b in zip(keep(ob), keep(og)) if a != b]
        check(len(ob) == len(og) and not diff and [r["key"] for r in ib["pop"]] == [r["key"] for r in ig["pop"]],
              "coverage info: the full read is line-for-line identical to the unedited baseline, outside the export MD5 and the coverage line (%d lines; first diff %s)"
              % (len(og), diff[:1]))
    # an export written before the column existed: read on, print "absent", gate nothing
    vt = os.path.join(tmp, "cov_absent")
    shutil.copytree(os.path.dirname(fetch0), vt)
    ex = os.path.join(vt, "export.csv")
    with open(ex, encoding="utf-8", newline="") as f:
        rows = list(csv.reader(f))
    ic = rows[0].index(COVERAGE_INFO_COL)
    with open(ex, "w", encoding="utf-8", newline="") as f:
        csv.writer(f, lineterminator="\n").writerows([r[:ic] + r[ic + 1:] for r in rows])
    rt = st_root(vt, real_root, RUN_SETTINGS_VERSION["1"])
    try:
        cx, info, _ = quiet_run(argparse.Namespace(root=rt, export=ex, fetch=os.path.join(vt, "fetch"), pooled=None, from_=None, resamples=rs,
                                                   seed=RUN_SEED, selftest_quiet=True, run=None, cut_before=None, counts_only=True))
        ok, got = (info["gates_failed"] == [] and info["fw_miss"] is None
                   and any(ln.startswith("- Full-window coverage, information only, not a gate: column") for ln in cx.out)), info["gates_failed"]
    except SystemExit as e_:
        ok, got = False, "refused: %s" % e_
    check(ok, "coverage info: an export without the column runs, prints 'absent' and fails no gate (got %s)" % got)


def selftest(root, resamples):
    fails = []
    def check(cond, msg):
        print(("PASS  " if cond else "FAIL  ") + msg)
        if not cond: fails.append(msg)
    # fast deterministic units first (review finding RV-3; findings RV-1 and RV-2 gates)
    selftest_holm(check)
    selftest_run_pins(check)
    selftest_gates(root, check, resamples)
    for case, null_case, flip, const in (("effect (A = +5 bps, B = 0, noise sd 10)", False, False, False),
                                         ("null (B rows carry their A row's EV)", True, False, False),
                                         ("seam flip (A = -4 bps before the session seam, +40 from it, B = 0)", False, True, False),
                                         ("const (no noise; B = 0; A = +2 before the POC edge, -10 from it to the seam, +40 from the seam)", False, False, True)):
        tmp = tempfile.mkdtemp(prefix="burst_selftest_")
        try:
            fetch, ex, ex_sig, expect = st_build(tmp, null_case, 20260929, flip, const)
            rt = st_root(tmp, root, RUN_SETTINGS_VERSION["1"])
            base = dict(root=rt, export=ex, fetch=fetch, pooled=None, cut_before=None, from_=None, resamples=resamples,
                        seed=RUN_SEED, selftest_quiet=True, run=None)
            print("\n==== SELFTEST case: %s ====" % case)
            # counts-only on an export with NO outcome column
            o = argparse.Namespace(**dict(base, export=ex_sig, counts_only=True))
            cx, info, res = quiet_run(o)
            check(res is None, "counts-only runs on an export that has no outcome column, and returns no outcome")
            check(info["gates_failed"] == [] and sum(info["miss"].values()) == 0, "counts-only: every pre-outcome gate passes on clean synthetic data, 0 missing-bar rows")
            for s in SESS_ORDER:
                got = {a: sum(1 for r in info["pop"] if r["s"] == s and r["arm"] == a) for a in "ABCO"}
                got["S_add"] = sum(1 for r in info["pop"] if r["s"] == s and r["sh"] == "S_add")
                want = {k: expect[s][k] for k in ("A", "B", "C", "O", "S_add")}
                check(got == want, "%s arm counts %s == constructed %s" % (s, got, want))
            check(len(info["drops"].get("not in the fetch books (pre-collector or pooled-only row)", [])) == 2, "2 export rows missing from the books are dropped and counted")
            check(len(info["drops"].get("AggrVelBurstRatio empty", [])) == 3 * NDAYS, "%d ratio-empty rows excluded and counted" % (3 * NDAYS))
            check(len(info["drops"].get("ASIA before arming 2026-08-01 19:02:31", [])) == expect["ASIA_unarmed_export_rows"] == info["refx"]["asiaUnarmed"],
                  "3 ASIA pre-arming rows excluded and counted, in the export and in the power-count reference")
            check(info["extra"] == 0 and info["arm_diff"] == 0, "analysis rows are a subset of the power-count rows with identical arms")
            nwhy = sum(len(v) for k, v in info["why"].items() if k.startswith("export:"))
            check(nwhy == 1, "1 book row missing from the export is counted as an export drop (got %d)" % nwhy)
            # full pipeline, as a pinned run 1 (every synthetic row is before the run-1 cut)
            o = argparse.Namespace(**dict(base, counts_only=False, run="1", cut_before=RUN1_CUT))
            cx, info, res = quiet_run(o)
            # AT-2 seam checks. Failing inputs: (a) two rows identical but for the seam must key to different strata (fails if the
            # seam term is dropped from strat_key); (b) the analysis population must hold rows on both sides of EVERY session's
            # seam, and the era printout must carry the seam line (fails if the seam field or the printout is missing).
            for s in SESS_ORDER:
                sp_ = {r["seam"] for r in info["pop"] if r["s"] == s}
                check(sp_ == {"pre", "post"}, "%s analysis population holds pre-seam and post-seam rows (got %s)" % (s, sorted(sp_)))
                check(all((r["seam"] == "pre") == (r["ts"] < SEAM[s]) for r in info["pop"] if r["s"] == s), "%s seam era is Timestamp < that session's seam" % s)
            r0 = info["pop"][0]
            check(strat_key(dict(r0, seam="pre")) != strat_key(dict(r0, seam="post")), "strat_key separates the seam eras (the fourth stratum dimension)")
            check(any(ln.startswith("- Daylight-saving seam era") for ln in cx.out), "seam era counts are in the era printout")
            for s in SESS_ORDER:
                x = res[("BO-H1", s)]
                pt, lo, hi = x["res"]["FULL"][:3]
                print("      %s BO-H1 FULL d = %s  Holm CI [%s, %s]  label: %s" % (s, ci(pt, lo, hi), f1(x["hci"][0]), f1(x["hci"][1]), x["final"]))
                # ALL_LABELS carries the A - B direction, not rw_run's B - A (RV-6)
                al = [e_ for e_ in ALL_LABELS if e_[0] == "BO-H1 " + s]
                check(len(al) == 1 and al[0][2] == x["label"] and al[0][3][0] == pt, "%s ALL_LABELS entry for BO-H1 matches the A - B result" % s)
                if const:
                    # Failing inputs: a seam sensitivity run filtered on the POC era gives +2 here (no SEAM-SENSITIVE); a
                    # BO-H2 upgraded set without S_add gives the A-only mean, not the S_add-diluted one.
                    A_ = [r for r in info["pop"] if r["s"] == s and r["arm"] == "A"]
                    check(abs(pt - sum(r["ev_main"] for r in A_) / len(A_)) < 1e-9 and x["a_dropped"] == 0,
                          "%s const BO-H1 FULL point = the mean A EV exactly (B = 0, every A stratum covered): %+.4f" % (s, pt))
                    check(x.get("edge_pt") is not None and x["edge_pt"] > 0 and pt > 0 and x.get("seam_pt") is not None and x["seam_pt"] < 0,
                          "%s const: pre-POC-only d > 0, pooled d > 0, pre-seam-only d < 0 (pre-POC %s, pooled %+.2f, pre-seam %s)" % (s, x.get("edge_pt"), pt, x.get("seam_pt")))
                    check("SEAM-SENSITIVE" in x["final"] and "EDGE-SENSITIVE" not in x["final"],
                          "%s const label carries SEAM-SENSITIVE and not EDGE-SENSITIVE (got %r)" % (s, x["final"]))
                    y = res[("BO-H2", s)]
                    A45 = [r for r in info["pop"] if r["s"] == s and r["arm"] == "A" and r["fifth"] >= 4]
                    S45 = [r for r in info["pop"] if r["s"] == s and r["sh"] == "S_add" and r["fifth"] >= 4]
                    want2 = sum(r["ev_main"] for r in A45) / (len(A45) + len(S45))
                    a_only = sum(r["ev_main"] for r in A45) / len(A45)
                    p2 = y["res"]["FULL"][0]
                    check(len(S45) > 0 and abs(want2 - a_only) > 0.1 and abs(p2 - want2) < 1e-9,
                          "%s const BO-H2 FULL point = sum(A EV) / (nA + nS_add) over fifths 4-5 = %+.4f (got %+.4f; A only would be %+.4f)" % (s, want2, p2, a_only))
                elif flip:
                    # the sensitivity run must exist, must have the opposite sign, and the label must say so
                    sqv = x.get("seam_pt")
                    check(sqv is not None and sqv < 0 < pt, "%s BO-H1 pooled d > 0 and pre-seam-only d < 0 (pooled %+.2f, pre-seam %s)" % (s, pt, sqv))
                    check(x["final"].endswith("SEAM-SENSITIVE"), "%s BO-H1 label carries SEAM-SENSITIVE (got %r)" % (s, x["final"]))
                elif not null_case:
                    check("SEAM-SENSITIVE" not in x["final"], "%s BO-H1 effect case is not labelled SEAM-SENSITIVE (got %r)" % (s, x["final"]))
                    check(lo > 0 and 3.0 <= pt <= 7.0, "%s BO-H1 A - B CI excludes 0 and the point is near +5" % s)
                    check(x["final"].startswith("CONFIRMED (d > 0)"), "%s BO-H1 label is CONFIRMED (d > 0)" % s)
                else:
                    check("SEAM-SENSITIVE" not in x["final"], "%s BO-H1 null case is not labelled SEAM-SENSITIVE" % s)
                    check(abs(pt) < 1e-9 and abs(lo) < 1e-9 and abs(hi) < 1e-9, "%s BO-H1 A - B is exactly 0 in every resample" % s)
                    check(x["final"] == "NO DIFFERENCE SHOWN", "%s BO-H1 label is NO DIFFERENCE SHOWN" % s)
            check(any(ln.startswith("### 3.3 BO-H2") for ln in cx.out), "BO-H2 table printed")
            check(sum(1 for ln in cx.out if ln.startswith("| Session | Strata |") and "| Holm passed |" in ln) == 2,
                  "BO-H1 and BO-H2 tables carry the Holm passed column (RV-5)")
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
    print("\nSELFTEST %s (%d failed)" % ("PASSED" if not fails else "FAILED", len(fails)))
    return 0 if not fails else 1


def build_parser():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--export", default=None)
    ap.add_argument("--fetch", default=None)
    ap.add_argument("--pooled", default=None)
    ap.add_argument("--run", choices=["1", "2"], default=None,
                    help="the pre-registered run. 1: pins Timestamp < 2026-11-25T00:00:00Z, seed 20260928, 10,000 resamples. "
                         "2: needs --from (the first row after the session-hours deploy, >= the run-1 cut); same seed and resamples")
    ap.add_argument("--cut-before", dest="cut_before", default=None, help="counts-only window; --run 1 sets it")
    ap.add_argument("--from", dest="from_", default=None, help="counts-only window; required with --run 2")
    ap.add_argument("--counts-only", dest="counts_only", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--resamples", type=int, default=None, help="default 10,000 = the house rule; --run pins it")
    ap.add_argument("--seed", type=int, default=None, help="default 20260928; --run pins it")
    ap.add_argument("--out", default=None)
    return ap


def resolve_run(o):
    """Applies the run pins (RV-2) to parsed arguments, in place. Raises ValueError naming the conflict. A pinned run takes
    no free window, seed or resamples argument, so a run cannot drift from its pre-registration by a typo or an omission."""
    o.cut_before = parse_when(o.cut_before) if isinstance(o.cut_before, str) else o.cut_before
    o.from_ = parse_when(o.from_) if isinstance(o.from_, str) else o.from_
    if o.run is not None:
        for flag, val in (("--seed", o.seed), ("--resamples", o.resamples)):
            if val is not None:
                raise ValueError("--run %s pins %s; do not pass it" % (o.run, flag))
        if o.run == "1":
            if o.cut_before is not None or o.from_ is not None:
                raise ValueError("--run 1 pins the window (Timestamp < %s); do not pass --cut-before or --from" % RUN1_CUT)
            o.cut_before = RUN1_CUT
        else:
            if o.from_ is None:
                raise ValueError("--run 2 needs --from <the first row after the session-hours deploy>")
            if o.from_ < RUN1_CUT:
                raise ValueError("--run 2 --from %s is before the run-1 cut %s; the two runs must be disjoint" % (o.from_, RUN1_CUT))
            if o.cut_before is not None:
                raise ValueError("--run 2 takes no --cut-before")
    elif not o.counts_only:
        raise ValueError("full mode runs only as --run 1, or --run 2 --from <ts> (docs/burst-outcome-read-spec.md section 6.1)")
    o.seed = RUN_SEED if o.seed is None else o.seed
    o.resamples = RUN_RESAMPLES if o.resamples is None else o.resamples
    return o


def main():
    ap = build_parser()
    o = ap.parse_args()
    root = os.path.abspath(o.root)
    if o.selftest:
        return selftest(root, o.resamples or RUN_RESAMPLES)
    if not o.export or not o.fetch:
        ap.error("--export and --fetch are required")
    try:
        resolve_run(o)
    except ValueError as ex:
        ap.error(str(ex))
    J = lambda p_: p_ if os.path.isabs(p_) else os.path.join(root, p_)
    o.root = root; o.export = J(o.export); o.fetch = J(o.fetch); o.pooled = J(o.pooled) if o.pooled else None
    o.selftest_quiet = False
    rc = 0
    try:
        cx, _, _ = run(o)
    except GateStop as g:
        cx, rc = g.cx, 2
    outp = o.out or os.path.join(root, "backtest_data", "burst-outcome-read",
                                 "burst-outcome-counts-output.md" if o.counts_only else "burst-outcome-read-output.md")
    os.makedirs(os.path.dirname(J(outp)), exist_ok=True)
    with open(J(outp), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(cx.out) + "\n")
    print("- Wrote " + J(outp))
    return rc


if __name__ == "__main__":
    sys.exit(main())
