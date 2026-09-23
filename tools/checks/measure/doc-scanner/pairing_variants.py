"""Harness-4 build: measure the E2 number-to-key pairing, BOTH directions, under several readings.

docs/doc-scanner-check-spec.md section 5 item 3 requires, at revision cbc2c91 on the living set:
never_shipped falls from 13 to <= 2, AND all 11 lines labelled true_positive for E2 in
docs/harness-runs/doc-scanner-20260922T193818Z-prelabels.json stay flagged (as VALUE candidates,
i.e. E2_value_was_shipped). This script measures that for:

  legacy        -- the measured instrument's pairing (enumerators.e2, unchanged)
  literal       -- the spec's section 4.2 rules 1-3 as written (what the tool runs)
  no_span       -- rules 1-3 without rule 1's second arm ("or inside the same backtick span")
  literal+nq    -- literal, plus a name rule NOT in the spec: a single-word leaf name must have its
                   parent segment on the line as a word
  no_span+nq    -- both
  ruled(f)      -- added 2026-09-23 (UTC) for the orchestrator's ruling DS-A option (f): no_span,
                   with the name rule as a LABEL (nothing dropped; top-level keys exempt)
  restricted_span+label -- the ruling's fallback: the span arm kept, but never inside a file name
                   or a link target; name rule as a label
  no_span+sep+label -- information only, never ruled
  ruled(g)      -- THE RULED READING (DS-A option (g), 2026-09-23 UTC) and the tool's behaviour: the
                   restricted span arm plus the `unqualified` and `operator_context` labels
After the readings it prints the (f)-round checks (CHECK1-CHECK4) and the (g)-round checks (G1-G4).

Only `ruled(g)` is the tool's behaviour (doc_scanner_candidates.e2_ruled). The other readings are
kept so their published numbers stay reproducible. Record: docs/doc-scanner-build-spec-back.md.

Run: PYTHONIOENCODING=utf-8 python tools/checks/measure/doc-scanner/pairing_variants.py [REV] [--all]
  --all  scan every non-archive doc (scan_head.py's population) instead of the living set, to see
         what each reading drops OUTSIDE the labelled set -- the other direction.
"""
import json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '..', '..', 'lib')))
import enumerators as E  # noqa: E402
import doc_scanner_candidates as D  # noqa: E402

ARGS = [a for a in sys.argv[1:] if not a.startswith('--')]
ALL_DOCS = '--all' in sys.argv[1:]
REV = ARGS[0] if ARGS else 'cbc2c91'
LABELS = os.path.join(E.REPO, 'docs', 'harness-runs', 'doc-scanner-20260922T193818Z-prelabels.json')

rev = D.rev_full(REV)
docs, missing = D.living_set(rev)
if ALL_DOCS:
    ARCH = re.compile(r'(archive|harness-runs/|/archive/)', re.I)  # scan_head.py's own exclusion
    docs = sorted(f for f in E.tree_files(rev) if f.endswith('.md') and (f.startswith('docs/') or f == 'CLAUDE.md')
                  and not ARCH.search(f))
labels = json.load(open(LABELS, encoding='utf-8'))['labels']
e2_labels = {}
for k, v in labels.items():
    path, line, kind = k.rsplit(':', 2)
    if kind.startswith('E2_'):
        e2_labels[(path, int(line))] = (kind, v['label'])
tp_lines = sorted(pl for pl, (kind, lab) in e2_labels.items() if kind == 'E2_value_was_shipped' and lab == 'true_positive')

VARIANTS = [
    ('legacy', None),
    ('literal', dict(span_arm=True, name_qualify=False)),
    ('no_span', dict(span_arm=False, name_qualify=False)),
    ('literal+nq', dict(span_arm=True, name_qualify=True)),
    ('no_span+nq', dict(span_arm=False, name_qualify=True)),
    # Sixth reading, added 2026-09-23 (UTC) for the orchestrator's ruling DS-A option (f): rule 1
    # without its span arm, and the name rule as a LABEL -- nothing dropped, top-level exempt.
    # The first five readings' output is unchanged by this addition.
    ('ruled(f)', dict(span_arm=False, name_label=True)),
    # Seventh reading: the branch the DS-A ruling names for its check 3 -- if the span arm's gains
    # include a correct pairing, keep the arm but stop it firing inside a file name or a link target.
    ('restricted_span+label', dict(span_arm='restricted', name_label=True)),
    # Eighth reading, INFORMATION ONLY, not ruled and not adopted: no span arm, and rule 1's
    # separator set extended with `"` and `{` -- the shapes of the span arm's correct pairings.
    ('no_span+sep+label', dict(span_arm=False, name_label=True, extra_sep=True)),
    # Ninth reading: the RULED one, DS-A option (g), 2026-09-23 (UTC) -- the restricted span arm
    # plus the `unqualified` and `operator_context` labels. This is what the tool runs.
    ('ruled(g)', dict(span_arm='restricted', name_label=True, op_label=True)),
]
# The span arm's three MIS-pairings, classified by hand in docs/doc-scanner-build-spec-back.md
# section 7.2 (rows 11-13). Every other span-arm row there is a correct pairing.
SPAN_MISPAIRINGS = {('docs/offline-whatif-replay-proposal.md', 42, 'scoring.atr_stop_multiplier', 1.0),
                    ('docs/trade-store-downtime-repair-proposal.md', 234, 'version', 1.0),
                    ('docs/trade-store-downtime-repair-spec-back.md', 38, 'version', 1.0)}

print(f'REV={rev[:7]} {"ALL_NON_ARCHIVE_DOCS" if ALL_DOCS else "LIVING_DOCS"}={len(docs)} living_missing={missing}')
print(f'E2 true_positive lines in the pre-registered labels: {len(tp_lines)}')
legacy_was = None
ROWS = {}  # reading -> full rows (with the facts dict where the reading has one)
for name, opts in VARIANTS:
    rows = []
    full = []
    for p in docs:
        t = E.show(rev, p)
        if opts is None:
            ver, flat = E.settings_at(rev)
            rows += E.e2(p, t, flat, E.settings_history(rev))
        else:
            hs = D.e2_strict(p, t, rev, **opts)
            full += hs
            rows += [h[:5] for h in hs]
    ROWS[name] = full if opts is not None else rows
    never = sorted({(r[0], r[1]) for r in rows if r[2] == 'E2_value_never_shipped'})
    was_rows = [r for r in rows if r[2] == 'E2_value_was_shipped']
    was_lines = sorted({(r[0], r[1]) for r in was_rows})
    tp_kept = [pl for pl in tp_lines if pl in was_lines]
    tp_lost = [pl for pl in tp_lines if pl not in was_lines]
    by_label = {}
    for pl in was_lines:
        lab = e2_labels.get(pl, ('', 'UNLABELLED'))[1]
        by_label[lab] = by_label.get(lab, 0) + 1
    print(f'--- {name}')
    print(f'  NEVER_SHIPPED_LINES={len(never)}  WAS_SHIPPED_ROWS={len(was_rows)}  WAS_SHIPPED_LINES={len(was_lines)}')
    print(f'  E2_TP_STILL_FLAGGED={len(tp_kept)}/{len(tp_lines)}  lost={[f"{p}:{l}" for p, l in tp_lost]}')
    print(f'  was_shipped lines by pre-registered label: {dict(sorted(by_label.items()))}')
    if legacy_was is None:
        legacy_was = set(was_lines)
    else:
        lost = sorted(legacy_was - set(was_lines))
        gained = sorted(set(was_lines) - legacy_was)
        print(f'  WAS_SHIPPED_LINES vs legacy: lost {len(lost)} {[f"{p}:{l}" for p, l in lost]}  '
              f'gained {len(gained)} {[f"{p}:{l}" for p, l in gained]}')
    if opts and opts.get('name_label'):
        q = sorted({(h[0], h[1]) for h in full if h[2] == 'E2_value_never_shipped' and h[5]['qualified']})
        uq = sorted({(h[0], h[1]) for h in full if h[2] == 'E2_value_never_shipped' and not h[5]['qualified']})
        wuq = sorted({(h[0], h[1]) for h in full if h[2] == 'E2_value_was_shipped' and not h[5]['qualified']})
        print(f'  QUALIFIED_NEVER_SHIPPED_LINES={len(q)}  UNQUALIFIED_NEVER_SHIPPED_LINES={len(uq)}  '
              f'UNQUALIFIED_WAS_SHIPPED_LINES={len(wuq)} {[f"{p}:{l}" for p, l in wuq]}')
    if ALL_DOCS:
        continue  # the never_shipped listing is for the labelled living set only
    for p, l in never:
        det = [r[3] for r in rows if r[0] == p and r[1] == l and r[2] == 'E2_value_never_shipped']
        lab = e2_labels.get((p, l), ('', 'UNLABELLED'))[1]
        tag = ''
        if opts and opts.get('name_label'):
            tag = ' QUALIFIED' if any(h[5]['qualified'] for h in full if h[0] == p and h[1] == l
                                      and h[2] == 'E2_value_never_shipped') else ' UNQUALIFIED'
        print(f'    never {p}:{l} [{lab}]{tag} {"; ".join(det)}')

# ---------------------------------------------------------------------------------------------
# The four checks the DS-A ruling names for option (f). Each prints PASS or FAIL with its numbers.
# ---------------------------------------------------------------------------------------------
def was_set(name):
    return {(r[0], r[1]) for r in ROWS[name] if r[2] == 'E2_value_was_shipped'}


def was_rows_key(name):
    """The judged VALUE rows as (path, line, key_path, doc value) in line order -- the fields an
    item ID is built from (arm|path|line@rev, plus a #n suffix by column order)."""
    out = []
    for r in ROWS[name]:
        if r[2] != 'E2_value_was_shipped':
            continue
        kp = r[5]['key_path'] if len(r) > 5 else r[3].split(':')[0]
        val = r[5]['doc_value'] if len(r) > 5 else float(re.search(r'doc (\S+) vs', r[3]).group(1))
        out.append((r[0], r[1], kp, val))
    return sorted(out)


def was_rows_set(name):
    return {(r[0], r[1], r[5]['key_path'], r[5]['doc_value']) for r in ROWS[name]
            if r[2] == 'E2_value_was_shipped' and len(r) > 5}


for f in ('ruled(f)', 'restricted_span+label', 'no_span+sep+label'):
  print(f'=== DS-A checks for reading {f}')
  if ALL_DOCS:
    span_rows = was_rows_set('literal') - was_rows_set('no_span')
    got = sorted(span_rows & was_rows_set(f))
    print(f'SPAN_ARM_ROWS_RECOVERED {len(got)}/{len(span_rows)} (the literal reading\'s was_shipped rows that '
          f'no_span loses): {[f"{p}:{l}={v:g}" for p, l, k, v in got]}')
  if not ALL_DOCS:
    q = {(h[0], h[1]) for h in ROWS[f] if h[2] == 'E2_value_never_shipped' and h[5]['qualified']}
    tp_ok = sum(1 for pl in tp_lines if pl in was_set(f))
    ok1 = len(q) <= 2 and tp_ok == len(tp_lines)
    print(f'CHECK1 {"PASS" if ok1 else "FAIL"} living set: QUALIFIED never_shipped lines={len(q)} (need <= 2), '
          f'E2 TP flagged={tp_ok}/{len(tp_lines)} (need all)')
    same_lit = was_rows_key(f) == was_rows_key('literal')
    same_leg = was_rows_key(f) == was_rows_key('legacy')
    n_rows, n_lines = len(was_rows_key(f)), len(was_set(f))
    ok4 = same_lit and same_leg and n_rows == 22 and n_lines == 20
    print(f'CHECK4 {"PASS" if ok4 else "FAIL"} living set: was_shipped rows={n_rows} lines={n_lines} (need 22/20); '
          f'same (path,line,key,value) rows as literal={same_lit}, as legacy={same_leg}')
  else:
    lost_vs_nospan = sorted(was_set('no_span') - was_set(f))
    gained_vs_nospan = sorted(was_set(f) - was_set('no_span'))
    nq_lost = sorted(was_set('no_span') - was_set('no_span+nq'))
    ver_lines = [pl for pl in nq_lost if any(r[0] == pl[0] and r[1] == pl[1] and r[2] == 'E2_value_was_shipped'
                                              and r[5]['key_path'] == 'version' for r in ROWS['no_span'])]
    restored = [pl for pl in ver_lines if pl in was_set(f)]
    ok2 = not lost_vs_nospan and len(ver_lines) == len(nq_lost) and len(restored) == len(ver_lines)
    print(f'CHECK2 {"PASS" if ok2 else "FAIL"} all docs: was_shipped lines lost vs no_span={len(lost_vs_nospan)} '
          f'{[f"{p}:{l}" for p, l in lost_vs_nospan]} (need 0), gained vs no_span={len(gained_vs_nospan)} '
          f'{[f"{p}:{l}" for p, l in gained_vs_nospan]}; '
          f'lines no_span+nq dropped={len(nq_lost)}, of them top-level `version`={len(ver_lines)}, '
          f'present in {f}={len(restored)}')
if ALL_DOCS:
    # CHECK3: the span arm's own gains. literal against legacy; no_span gains nothing, so every one of
    # these comes from rule 1's second arm. Printed with the text from the key to the number, for a
    # MECHANISM classification by hand -- no tense read.
    gained = sorted(was_set('literal') - was_set('legacy'))
    print(f'CHECK3 literal gains vs legacy: {len(gained)} lines (no_span gains vs legacy: '
          f'{len(was_set("no_span") - was_set("legacy"))}). Each, with the key-to-number span:')
    for p, l in gained:
        for h in ROWS['literal']:
            if h[0] == p and h[1] == l and h[2] == 'E2_value_was_shipped':
                fx = h[5]
                line = h[4]
                seg = line[fx['key_col']:fx['value_col'] + len(fx['doc_value_text'])]
                pre = line[max(0, fx['key_col'] - 40):fx['key_col']]
                print(f'  {p}:{l} key={fx["key_path"]} doc={fx["doc_value_text"]} live={fx["live_value"]}')
                print(f'      ...{pre}[[{seg}]]{line[fx["value_col"] + len(fx["doc_value_text"]):][:40]}...')


# ---------------------------------------------------------------------------------------------
# The checks the orchestrator's ruling DS-A option (g) names. STOP conditions: G2 and G4 only.
# ---------------------------------------------------------------------------------------------
g = 'ruled(g)'
print(f'=== DS-A option (g) checks')
if not ALL_DOCS:
    rows_g = [h for h in ROWS[g] if h[2] == 'E2_value_never_shipped']
    unl = sorted({(h[0], h[1], h[3]) for h in rows_g if h[5]['qualified'] and not h[5]['operator_context']})
    unq = sorted({(h[0], h[1]) for h in rows_g if not h[5]['qualified']})
    opc = sorted({(h[0], h[1]) for h in rows_g if h[5]['qualified'] and h[5]['operator_context']})
    print(f'G1 (advisory) living set: UNLABELLED never_shipped lines={len({(a, b) for a, b, c in unl})}  '
          f'unqualified bucket={len(unq)}  operator_context bucket={len(opc)} {[f"{p}:{l}" for p, l in opc]}')
    for p, l, det in unl:
        print(f'    unlabelled {p}:{l} {det}')
    n_rows, n_lines = len(was_rows_key(g)), len(was_set(g))
    same_rows = was_rows_key(g) == was_rows_key('literal') == was_rows_key('legacy')
    ids_g = [it['id'] for it in D.scan(rev, None, 'ruled')['items'] if it['arm'] == 'VALUE']
    ids_s = [it['id'] for it in D.scan(rev, None, 'strict')['items'] if it['arm'] == 'VALUE']
    ok4 = same_rows and n_rows == 22 and n_lines == 20 and ids_g == ids_s
    print(f'G4 {"PASS" if ok4 else "FAIL"} living set: was_shipped rows={n_rows} lines={n_lines} (need 22/20); '
          f'rows identical to literal and legacy={same_rows}; VALUE item ids identical to --e2 strict='
          f'{ids_g == ids_s} ({len(ids_g)} ids)')
else:
    lit, gg = was_rows_set('literal'), was_rows_set(g)
    lost = sorted(lit - gg)
    span_rows = was_rows_set('literal') - was_rows_set('no_span')
    correct_span = span_rows - SPAN_MISPAIRINGS
    ver_lines = sorted(was_set('no_span') - was_set('no_span+nq'))
    ok2 = not lost and correct_span <= gg and all(v in was_set(g) for v in ver_lines)
    print(f'G2 {"PASS" if ok2 else "FAIL"} all docs: was_shipped rows lost vs literal={len(lost)} {lost}; '
          f'correct span-arm rows present={len(correct_span & gg)}/{len(correct_span)}; '
          f'`version` lines present={sum(1 for v in ver_lines if v in was_set(g))}/{len(ver_lines)}; '
          f'rows gained vs literal={len(gg - lit)}')
    for r in sorted(SPAN_MISPAIRINGS):
        hs = [h for h in ROWS[g] if (h[0], h[1], h[5]['key_path'], h[5]['doc_value']) == r]
        state = (f'still JUDGED as {hs[0][2]}, labels qualified={hs[0][5]["qualified"]} '
                 f'operator_context={hs[0][5]["operator_context"]}') if hs else 'BLOCKED (no pair)'
        print(f'G3 (report) mis-pairing {r[0]}:{r[1]} {r[2]}={r[3]:g}: {state}')
