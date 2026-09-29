#!/usr/bin/env python3
"""Fixture generator for the coverage-verb proofs (audit 2026-09-25, backtest-ceiling-remainder).

Builds one evidence directory per scenario in the copy-back layout BacktestRunner's
`coverage --evidence-dir` reads: backtest_data/trades_YYYY-MM.csv (+ complete candle and
funding files so the S4 lines do not mask the verdict), analysis_log.csv, ws_health.log,
capture_marker.log and optionally venue_status.log.

Day: Wed 2026-09-16 UTC. Store and evidence cover 09:00-13:00. Hour 10 is a -1.5 % flush.
Trade cadence: one venue trade every 5 s, trade_seq +1 per venue trade; a capture outage
skips the rows but not the sequence numbers (the venue kept trading).
"""
import os, sys, shutil
from datetime import datetime, timezone, timedelta

DAY = datetime(2026, 9, 16, tzinfo=timezone.utc)
T0 = DAY + timedelta(hours=9)
T1 = DAY + timedelta(hours=13)
STEP = timedelta(seconds=5)

def ms(dt):
    return int(dt.timestamp() * 1000)

def at(h, m, s=0):
    return DAY + timedelta(hours=h, minutes=m, seconds=s)

def price_at(t):
    # flat 65,000 until 10:00, -1.5 % linear flush across hour 10, flat after
    if t < at(10, 0):
        return 65000.0
    if t < at(11, 0):
        frac = (t - at(10, 0)).total_seconds() / 3600.0
        return 65000.0 * (1 - 0.015 * frac)
    return 65000.0 * 0.985

def iso(dt):
    return dt.strftime('%Y-%m-%dT%H:%M:%S.') + f'{dt.microsecond // 1000:03d}Z'

def write_store(d, outages):
    """outages: list of (start, end) datetimes during which rows are NOT written."""
    sd = os.path.join(d, 'backtest_data')
    os.makedirs(sd, exist_ok=True)
    with open(os.path.join(sd, 'trades_2026-09.csv'), 'w', newline='\n') as f:
        f.write('Timestamp,Price,Amount,Direction,Liquidation,TradeId,TradeSeq\n')
        t, seq = T0, 1_000_000
        while t < T1:
            if not any(a <= t < b for a, b in outages):
                f.write(f'{ms(t)},{price_at(t):.2f},10.00,sell,none,{900_000_000 + seq},{seq}\n')
            t += STEP
            seq += 1
    # complete candles at 1/3/5/15 and hourly funding for the whole requested day
    for res in (1, 3, 5, 15):
        with open(os.path.join(sd, f'candles_{res}m_2026-09.csv'), 'w', newline='\n') as f:
            f.write('Timestamp,Open,High,Low,Close,Volume,VolumeUSD\n')
            t = DAY
            while t < DAY + timedelta(days=1):
                p = price_at(t)
                f.write(f'{ms(t)},{p:.2f},{p:.2f},{p:.2f},{p:.2f},1,{p:.2f}\n')
                t += timedelta(minutes=res)
    with open(os.path.join(sd, 'funding_2026-09.csv'), 'w', newline='\n') as f:
        f.write('Timestamp,Rate\n')
        for h in range(24):
            f.write(f'{ms(DAY + timedelta(hours=h))},0.00001\n')

def write_analysis_log(d, lives, header_only=False):
    """lives: list of (instance_id, start, end) — one row every 60 s at :30."""
    hdr = 'Timestamp,Price,Verdict,InstanceId\n'
    with open(os.path.join(d, 'analysis_log.csv'), 'w', newline='\n') as f:
        f.write(hdr)
        if header_only:
            return
        for iid, a, b in lives:
            t = a.replace(second=30)
            while t < b:
                f.write(f'{t.strftime("%Y-%m-%d %H:%M:%S")},{price_at(t):.2f},NO TRADE,{iid}\n')
                t += timedelta(seconds=60)

def write_ws_health(d, lines):
    with open(os.path.join(d, 'ws_health.log'), 'w', newline='\n') as f:
        for dt, state, iid in lines:
            f.write(f'{iso(dt)} | {state} | {iid}\n')

def write_markers(d, markers):
    with open(os.path.join(d, 'capture_marker.log'), 'w', newline='\n') as f:
        for dt, iid in markers:
            f.write(f'{iso(dt)} | True | backtest_data | {iid}\n')

def write_venue(d, lines):
    with open(os.path.join(d, 'venue_status.log'), 'w', newline='\n') as f:
        for dt, state, iid in lines:
            f.write(f'{iso(dt)} | {state} | {iid}\n')

def scenario(root, name):
    d = os.path.join(root, name)
    shutil.rmtree(d, ignore_errors=True)
    os.makedirs(d)
    return d

def main(root):
    A, B = 'aaaaaaaa-0000-0000-0000-000000000001', 'bbbbbbbb-0000-0000-0000-000000000002'

    # S-A: WS capture outage 10:07-10:52 inside the flush hour, process A stays alive.
    d = scenario(root, 'S-A_ws_outage_mid_hour')
    write_store(d, [(at(10, 7), at(10, 52))])
    write_analysis_log(d, [(A, T0, T1)])
    write_ws_health(d, [(at(8, 59), 'OK', A), (at(10, 7), 'DOWN', A), (at(10, 52), 'OK', A)])
    write_markers(d, [(at(8, 59), A)])

    # S-B: the same 45 min, but the process died at 10:07 and B started at 10:52.
    d = scenario(root, 'S-B_crash_restart_mid_hour')
    write_store(d, [(at(10, 7), at(10, 52, 30))])
    write_analysis_log(d, [(A, T0, at(10, 7)), (B, at(10, 53), T1)])
    write_ws_health(d, [(at(8, 59), 'OK', A), (at(10, 52), 'DOWN', B), (at(10, 52, 30), 'OK', B)])
    write_markers(d, [(at(8, 59), A), (at(10, 52), B)])

    # S-C: a 54 min outage straddling the 10:00 boundary (09:56-10:50), process alive.
    d = scenario(root, 'S-C_straddle_hour_boundary')
    write_store(d, [(at(9, 56), at(10, 50))])
    write_analysis_log(d, [(A, T0, T1)])
    write_ws_health(d, [(at(8, 59), 'OK', A), (at(9, 56), 'DOWN', A), (at(10, 50), 'OK', A)])
    write_markers(d, [(at(8, 59), A)])

    # S-D0: S-B plus a later 20 min WS outage under B (12:10-12:30). No venue log.
    # S-D1: identical, plus one VENUE_503 line from A at 10:06 — the flush load spike —
    #       and no VENUE_OK (A died before a successful call; B never saw an error).
    for name, venue in (('S-D0_restart_then_defect_no_venue_log', None),
                        ('S-D1_restart_then_defect_venue503_open', [(at(10, 6), 'VENUE_503', A)])):
        d = scenario(root, name)
        write_store(d, [(at(10, 7), at(10, 52, 30)), (at(12, 10), at(12, 30))])
        write_analysis_log(d, [(A, T0, at(10, 7)), (B, at(10, 53), T1)])
        write_ws_health(d, [(at(8, 59), 'OK', A), (at(10, 52), 'DOWN', B), (at(10, 52, 30), 'OK', B),
                            (at(12, 10), 'DOWN', B), (at(12, 30), 'OK', B)])
        write_markers(d, [(at(8, 59), A), (at(10, 52), B)])
        if venue:
            write_venue(d, venue)

    # S-E0: S-A's store, no ws_health.log, NO analysis_log.csv  -> S1 skipped.
    # S-E1: identical, but analysis_log.csv exists holding only its header.
    for name, header_only in (('S-E0_no_evidence_files', None), ('S-E1_header_only_analysis_log', True)):
        d = scenario(root, name)
        write_store(d, [(at(10, 7), at(10, 52))])
        if header_only:
            write_analysis_log(d, [], header_only=True)
        write_markers(d, [(at(8, 59), A)])

if __name__ == '__main__':
    main(sys.argv[1])
