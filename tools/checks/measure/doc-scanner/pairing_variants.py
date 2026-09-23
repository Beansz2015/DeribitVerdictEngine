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

Only `literal` is the tool's behaviour. The other readings are measured so a ruling can be made
from numbers; none of them is adopted by this build. Record: docs/doc-scanner-build-spec-back.md.

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
]

print(f'REV={rev[:7]} {"ALL_NON_ARCHIVE_DOCS" if ALL_DOCS else "LIVING_DOCS"}={len(docs)} living_missing={missing}')
print(f'E2 true_positive lines in the pre-registered labels: {len(tp_lines)}')
legacy_was = None
for name, opts in VARIANTS:
    rows = []
    for p in docs:
        t = E.show(rev, p)
        if opts is None:
            ver, flat = E.settings_at(rev)
            rows += E.e2(p, t, flat, E.settings_history(rev))
        else:
            rows += [h[:5] for h in D.e2_strict(p, t, rev, **opts)]
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
    if ALL_DOCS:
        continue  # the never_shipped listing is for the labelled living set only
    for p, l in never:
        det = [r[3] for r in rows if r[0] == p and r[1] == l and r[2] == 'E2_value_never_shipped']
        lab = e2_labels.get((p, l), ('', 'UNLABELLED'))[1]
        print(f'    never {p}:{l} [{lab}] {"; ".join(det)}')
