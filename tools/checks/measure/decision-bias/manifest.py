"""Harness 6 (decision-bias tripwire), Phase A: the HAND-CURATED decision manifest.

This file is the enumeration record. `build_population.py` reads it, pulls every text field
VERBATIM out of git (`git show <rev>:<path>`), and refuses on any span that is not found.

Conventions (the rules are argued in docs/decision-bias-measurement-phase-a-spec-back.md):
- One entry = one decision, recorded under the doc that holds it at REV (`doc`). Mirrors of the
  same decision in queues, handovers, section-15 entries and archives are not separate entries.
- `src` is the revision the STATE text is read from: the last revision of `src_path` before the
  trader's ruling was written into it. Never `git checkout`; always `git show`.
- `opts` is ('parse', span) to split an options span on its (a)/(b)/... markers, or an explicit
  list of (label, span). A label in parentheses with a digit, like '(1)', is assigned HERE because
  the source names the options without letters; the option TEXT is still verbatim.
- ⛔ OUTCOMES DO NOT LIVE HERE. They are in manifest_outcomes.py, so that this file (the state
  definition) can be read without seeing any ruling. The seat labelling blind must not open
  manifest_outcomes.py or the outcomes JSON before its baseline is committed.
- `status`: 'pop' (scored) | 'unruled' | 'unrecoverable'. Candidates that fail the population
  definition for any other reason go in EXCLUDED with a reason code.
"""

REV = 'b5000c9'

ENTRIES = []
EXCLUDED = []   # (doc, label, reason_code, note)
UNRECOVERABLE = []   # (doc, label, note): meets all three population criteria, but no revision
                     # holds the recommendation text WITHOUT the ruling (co-committed). Count only.


def add(doc, label, src, q, opts, rec, rat, src_path=None, status='pop', reason='', note=''):
    ENTRIES.append(dict(doc=doc, label=label, src=src, src_path=src_path or doc, q=q, opts=opts,
                        rec=rec, rat=rat, status=status, reason=reason, note=note))


def excl(doc, label, reason, note=''):
    EXCLUDED.append((doc, label, reason, note))


def unrec(doc, label, note):
    UNRECOVERABLE.append((doc, label, note))


def S(line, start, end=None):
    return ('sub', line, start, end)


def E(line, text):
    """An exact verbatim token on `line` (start == end)."""
    return ('sub', line, text, text)


def C(line, idx):
    return ('cell', line, idx)


def CS(line, idx, start, end=None):
    return ('cellsub', line, idx, start, end)


def AT(rev, path, span):
    return ('at', rev, path, span)


# ------------------------------------------------------------------------------------------
# Option-explicitness rule: options are explicit when the QUESTION or the RECOMMENDATION text
# names at least two courses of action AS OPTIONS - "vs", "or" between alternatives, "Alt:",
# "Alternative", lettered or numbered options, "instead of", "rather than", an alternative
# named as considered-and-rejected, or a value change "a -> b" (keep a / move to b). A bare
# "Yes" to a single stated design is options_not_explicit, and so is a design stated with a
# negated feature ("NO X", "never X", "not at Y"): the text rules the feature out, it does not
# offer it as an option. An option text that would have to be invented is never allowed.
# ------------------------------------------------------------------------------------------

# ---- docs/bid-ask-spread-proposal.md
add('docs/bid-ask-spread-proposal.md', 'Q2', '67508fe', ('cell', 308, 2), [('(1)', ('sub', 308, 'both sides', 'both sides')), ('(2)', ('sub', 308, 'neither', 'neither'))], '(1)', ('cell', 308, 3))

# ---- docs/ofi-momentum-proposal.md
add('docs/ofi-momentum-proposal.md', 'Q2', '67508fe', ('cell', 333, 2), [('(1)', ('sub', 333, 'always remove', '`FullLongCategories` set')), ('(2)', ('sub', 333, 'only when OFI was sole contributor', 'sole contributor'))], '(2)', ('cell', 333, 3))

# ---- docs/swing-pivot-proposal.md
add('docs/swing-pivot-proposal.md', 'Q1', '67508fe', ('cell', 548, 2), [('(1)', ('sub', 548, 'score (e.g.', 'bonus)')), ('(2)', ('sub', 548, 'only inform display', 'arbitration'))], '(2)', ('cell', 548, 3))
add('docs/swing-pivot-proposal.md', 'Q2', '67508fe', ('cell', 549, 2), [('(1)', ('sub', 549, '2, 3', '2')), ('(2)', ('sub', 549, '3, or', '3')), ('(3)', ('sub', 549, '4?', '4'))], '(2)', ('cell', 549, 3))

# ---- docs/vpfr-lite-v2-proposal.md
add('docs/vpfr-lite-v2-proposal.md', 'Q1', '67508fe', ('cell', 389, 2), [('(1)', ('sub', 389, 'bucket centres', 'bucket centres')), ('(2)', ('sub', 389, 'bucket edges', 'bucket edges'))], '(1)', ('cell', 389, 3))
add('docs/vpfr-lite-v2-proposal.md', 'Q2', '67508fe', ('cell', 390, 2), [('(1)', ('sub', 390, 'higher', 'higher')), ('(2)', ('sub', 390, 'lower bucket', 'lower bucket'))], '(2)', ('cell', 390, 3))
add('docs/vpfr-lite-v2-proposal.md', 'Q6', '67508fe', ('cell', 394, 2), [('(1)', ('sub', 394, 'immediately', 'immediately')), ('(2)', ('sub', 394, 'after observation', 'after observation'))], '(2)', ('cell', 394, 3))

# ---- docs/dynamic-microcvd-accel-proposal.md
add('docs/dynamic-microcvd-accel-proposal.md', 'Q1', 'f544095', ('cell', 365, 2), [('(1)', ('sub', 365, 'total window USD flow', 'total window USD flow')), ('(2)', ('sub', 365, 'VolumeSMA9 × price', 'VolumeSMA9 × price'))], '(1)', ('cell', 365, 3))
add('docs/dynamic-microcvd-accel-proposal.md', 'Q2', 'f544095', ('cell', 366, 2), [('(1)', ('sub', 366, 'reference `accel_threshold`', '(static anchor)')), ('(2)', ('sub', 366, 'an absolute USD value', 'an absolute USD value'))], '(1)', ('cell', 366, 3))
add('docs/dynamic-microcvd-accel-proposal.md', 'Q3', 'f544095', ('cell', 367, 2), [('(1)', ('sub', 367, '0.0 or', '0.0 or')), ('(2)', ('sub', 367, '0.03?', '0.03?'))], '(1)', ('cell', 367, 3))

# ---- docs/settings-exposure-pass-proposal.md
add('docs/settings-exposure-pass-proposal.md', 'Q2', '47a4535', ('cell', 443, 2), [('(1)', ('sub', 443, 'derived from `RegimeMaxScore`', 'derived from `RegimeMaxScore`')), ('(2)', ('sub', 443, 'independent', 'independent'))], '(2)', ('cell', 443, 3))

# ---- docs/analysis-log-csv-expansion-proposal.md
add('docs/analysis-log-csv-expansion-proposal.md', 'Q1', 'bcd2bd2', ('cell', 339, 2), [('(1)', ('sub', 339, 'inserted at a logical position in the header', 'inserted at a logical position in the header')), ('(2)', ('sub', 339, 'appended at end', 'appended at end'))], '(2)', ('cell', 339, 3))
add('docs/analysis-log-csv-expansion-proposal.md', 'Q2', 'bcd2bd2', ('cell', 340, 2), [('(1)', ('sub', 340, 'distinguish confirmed/conflict by direction (LONG/SHORT)', 'distinguish confirmed/conflict by direction (LONG/SHORT)')), ('(2)', ('sub', 340, 'just by category', 'just by category'))], '(1)', ('cell', 340, 3))
add('docs/analysis-log-csv-expansion-proposal.md', 'Q3', 'bcd2bd2', ('cell', 341, 2), [('(1)', ('sub', 341, '`"NONE"`', '`"NONE"`')), ('(2)', ('sub', 341, 'empty string', 'empty string'))], '(1)', ('cell', 341, 3))
add('docs/analysis-log-csv-expansion-proposal.md', 'Q6', 'bcd2bd2', ('cell', 344, 2), [('(1)', ('sub', 344, 'logged', 'logged')), ('(2)', ('sub', 344, 'left blank', 'in display)'))], '(1)', ('cell', 344, 3))

# ---- docs/v17-followup-fixes-proposal.md
add('docs/v17-followup-fixes-proposal.md', 'Q6', 'd368d30', ('cell', 254, 2), [('(1)', ('sub', 254, 'its own commit', 'its own commit')), ('(2)', ('sub', 254, 'ride with the code fix', 'ride with the code fix'))], '(2)', ('cell', 254, 3))

# ---- docs/v19-calibration-tuning-pass-proposal.md
add('docs/v19-calibration-tuning-pass-proposal.md', 'Q3', '8ac9a1f', ('cell', 334, 2), [('(1)', ('sub', 334, 'reset the existing log', 'reset the existing log')), ('(2)', ('sub', 334, 'keep it', 'keep it'))], '(1)', ('cell', 334, 3))

# ---- docs/v20-rsi-roc-algorithm-fixes-proposal.md
add('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q9', '7d378ea', ('cell', 591, 2), [('(1)', ('sub', 591, 'before', 'before')), ('(2)', ('sub', 591, 'after v19', 'after v19'))], '(2)', ('cell', 591, 3))

# ---- docs/api-resilience-pass-proposal.md
add('docs/api-resilience-pass-proposal.md', 'Q1', '54f01a9', ('cell', 537, 2), [('(1)', ('sub', 537, 'Skip-on-any-failure', 'Skip-on-any-failure')), ('(2)', ('sub', 537, 'degraded-mode', 'degraded-mode'))], '(1)', ('cell', 537, 3))
add('docs/api-resilience-pass-proposal.md', 'Q2', '54f01a9', ('cell', 538, 2), [('(1)', ('sub', 538, 'Retry once', 'Retry once')), ('(2)', ('sub', 538, 'exponential backoff', 'exponential backoff'))], '(1)', ('cell', 538, 3))

# ---- docs/on-close-analysis-mode-proposal.md
add('docs/on-close-analysis-mode-proposal.md', 'S9.1', 'ae0b59d', ('sub', 137, 'Fire at exec-resolution close vs always 1-min', 'Fire at exec-resolution close vs always 1-min'), [('(1)', ('sub', 137, 'exec-resolution close', 'exec-resolution close')), ('(2)', ('sub', 137, 'always 1-min', 'always 1-min'))], '(1)', ('sub', 137, 'recommend', None))

# ---- docs/realtime-exit-guard-proposal.md
add('docs/realtime-exit-guard-proposal.md', 'S9.1', 'ce700a8', ('sub', 185, 'Default `enabled`', '`enabled`'), [('(1)', ('sub', 185, '**true**', '**true**')), ('(2)', ('sub', 185, 'Alternative: false', 'explicitly toggled.'))], '(1)', ('sub', 185, 'recommend', None))
add('docs/realtime-exit-guard-proposal.md', 'S9.3', 'ce700a8', ('sub', 187, '`debounce_evals`', '`debounce_evals`'), [('(1)', ('sub', 187, '**2**', '**2**')), ('(2)', ('sub', 187, 'Set 1 for immediate-fire', 'rather not wait'))], '(1)', ('sub', 187, 'recommend', None))
add('docs/realtime-exit-guard-proposal.md', 'S9.5', 'ce700a8', ('sub', 189, 'Surface the single-adverse Warn state?', 'state?'), [('(1)', ('sub', 189, '**yes**', '**yes**')), ('(2)', ('sub', 189, 'EXIT-only', 'EXIT-only'))], '(1)', ('sub', 189, 'recommend', None))
add('docs/realtime-exit-guard-proposal.md', 'S9.6', 'ce700a8', ('sub', 190, 'Latch behaviour', 'behaviour'), [('(1)', ('sub', 190, 'auto-clear after the condition resolves', 'position-flat')), ('(2)', ('sub', 190, 'require a manual ack', 'ack'))], '(1)', ('sub', 190, 'recommend', None))

# ---- docs/live-microstructure-strip-proposal.md
add('docs/live-microstructure-strip-proposal.md', 'S9.2', 'df9dc4c', ('sub', 138, 'Placement', 'Placement'), [('(1)', ('sub', 138, 'a thin full-width line', 'under the verdict header**')), ('(2)', ('sub', 138, 'Alt: beside the exit-guard row', 'TOOLS card.'))], '(1)', ('sub', 138, 'recommend', None))
add('docs/live-microstructure-strip-proposal.md', 'S9.4', 'df9dc4c', ('sub', 140, 'Nearest level: single nearest vs above+below', 'above+below'), [('(1)', ('sub', 140, '**single nearest**', '**single nearest**')), ('(2)', ('sub', 140, 'Alt: show both nearest-above', 'nearest-below.'))], '(1)', ('sub', 140, 'recommend', None))
add('docs/live-microstructure-strip-proposal.md', 'S9.5', 'df9dc4c', ('sub', 141, 'Tape speed metric', 'Tape speed metric'), [('(1)', ('sub', 141, '**both** `tr/s` and `$/s`', '`$/s`')), ('(2)', ('sub', 141, 'Or pick one.', 'Or pick one.'))], '(1)', ('sub', 141, 'recommend', None))

# ---- docs/auto-tweaker-session-resolution-filter-proposal.md
add('docs/auto-tweaker-session-resolution-filter-proposal.md', 'S5.2', '18155a0', ('sub', 115, 'Accept GLOBAL-apply scoping', 'off-surface (§4)?'), [('(1)', ('sub', 115, 'GLOBAL-apply scoping (§3)', 'off-surface (§4)')), ('(2)', ('sub', 115, 'per-session tunable targets', 'per-session tunable targets'))], '(1)', ('sub', 115, '*(Recommend YES.)*', None))

# ---- docs/offline-analysis-report-audit-proposal.md
add('docs/offline-analysis-report-audit-proposal.md', 'D1', 'fd0efdf', ('sub', 172, 'D1 — Segmentation granularity.', 'granularity.'), [('(1)', ('sub', 172, '**`(session × resolution)`**', 'LONDON×3')), ('(2)', ('sub', 172, 'Alternative: `(resolution only)`', 'thin cells)'))], '(1)', ('sub', 172, 'Recommend **`(session', None))
add('docs/offline-analysis-report-audit-proposal.md', 'D3', 'fd0efdf', ('sub', 174, 'D3 — Pooled view.', 'view.'), [('(1)', ('sub', 174, '**Drop** the old pooled matrix', 'diagnostics')), ('(2)', ('sub', 174, 'Alternative: also render', 'for continuity'))], '(1)', ('sub', 174, '**Drop**', None))

# ---- docs/time-averaged-ofi-proposal.md
add('docs/time-averaged-ofi-proposal.md', 'S10.1', '00b876c', ('sub', 124, 'Averaging mechanism', 'Averaging mechanism'), [('(a)', ('sub', 124, 'feed-side rolling accumulator', 'O(1))')), ('(b)', ('sub', 124, 'sampled ring', 'coarser)'))], '(a)', ('sub', 124, 'recommend', None))
add('docs/time-averaged-ofi-proposal.md', 'S10.2', '00b876c', ('sub', 125, 'Window / weighting', 'Window / weighting'), [('(1)', ('sub', 125, '**EMA over `avg_window_sec` = 10s**', 'recency-weighted)')), ('(2)', ('sub', 125, 'a flat mean of the window', 'a flat mean of the window'))], '(1)', ('sub', 125, 'recommend', None))
add('docs/time-averaged-ofi-proposal.md', 'S10.3', '00b876c', ('sub', 126, 'Re-baseline method', 'Re-baseline method'), [('(1)', ('sub', 126, '**firing-rate-match to snapshot-OFI history**', 'precedent)')), ('(2)', ('sub', 126, 'a distribution-percentile anchor', 'a distribution-percentile anchor'))], '(1)', ('sub', 126, 'recommend', None))
add('docs/time-averaged-ofi-proposal.md', 'S10.4', '00b876c', ('sub', 127, 'REST-fallback OFI', 'REST-fallback OFI'), [('(1)', ('sub', 127, '**keep snapshot OFI**', 'WS-averaged majority)')), ('(2)', ('sub', 127, 'Alt: skip OFI scoring', 'fallback runs.'))], '(1)', ('sub', 127, 'recommend', None))
add('docs/time-averaged-ofi-proposal.md', 'S10.5', '00b876c', ('sub', 128, 'Build/re-baseline split', 'Build/re-baseline split'), [('(1)', ('sub', 128, '**two versions**', 'v36→v40')), ('(2)', ('sub', 128, 'vs waiting', 'vs waiting'))], '(1)', ('sub', 128, 'recommend', None))

# ---- docs/dev-workflow-automation-proposal.md
add('docs/dev-workflow-automation-proposal.md', 'B', 'c3d71cc', ('cell', 197, 2), [('(1)', ('sub', 197, 'Stop+fast', 'Stop+fast')), ('(2)', ('sub', 197, 'PostToolUse', 'PostToolUse')), ('(3)', ('sub', 197, 'advisory-only', 'advisory-only'))], '(1)', [('cell', 197, 3), ('cell', 197, 4)])
add('docs/dev-workflow-automation-proposal.md', 'C', 'c3d71cc', ('cell', 198, 2), [('(1)', ('sub', 198, 'Share `.claude/settings.json`', '(un-ignore)')), ('(2)', ('sub', 198, 'local-only', 'local-only'))], '(1)', [('cell', 198, 3), ('cell', 198, 4)])
add('docs/dev-workflow-automation-proposal.md', 'D', 'c3d71cc', ('cell', 199, 2), [('(1)', ('sub', 199, 'hard-fail-without-token', 'hard-fail-without-token')), ('(2)', ('sub', 199, 'warn-only in CI', 'warn-only in CI'))], '(1)', [('cell', 199, 3), ('cell', 199, 4)])
add('docs/dev-workflow-automation-proposal.md', 'E', 'c3d71cc', ('cell', 200, 2), [('(1)', ('sub', 200, 'Build item 4 `implementer.md` too', 'Build item 4 `implementer.md` too')), ('(2)', ('sub', 200, '`coordinator-review.md` only', '`coordinator-review.md` only'))], '(2)', [('cell', 200, 3), ('cell', 200, 4)])
add('docs/dev-workflow-automation-proposal.md', 'F', 'c3d71cc', ('cell', 201, 2), [('(1)', ('sub', 201, '`windows-latest` only', '`windows-latest` only')), ('(2)', ('sub', 201, 'split (Windows solution', 'Linux harness)'))], '(1)', [('cell', 201, 3), ('cell', 201, 4)])

# ---- docs/audit-fixes-2026-07-02-proposal.md
add('docs/audit-fixes-2026-07-02-proposal.md', 'D1', 'ff23bfe', ('cell', 91, 2), [('(1)', ('sub', 91, 'remove key+POCO', 'remove key+POCO')), ('(2)', ('sub', 91, 'fence-only', 'fence-only'))], '(1)', ('cell', 91, 3))
add('docs/audit-fixes-2026-07-02-proposal.md', 'D2', 'ff23bfe', ('cell', 92, 2), [('(1)', ('sub', 92, 'this pass takes the next version', 'the one after)')), ('(2)', ('sub', 92, 'fold F1/F2 into the re-baseline commit', 'commit'))], '(1)', ('cell', 92, 3))
add('docs/audit-fixes-2026-07-02-proposal.md', 'D3', 'ff23bfe', ('cell', 93, 2), [('(1)', ('sub', 93, 'exit guard keeps snapshot OFI', 'exit guard keeps snapshot OFI')), ('(2)', ('sub', 93, 'aligns to averaged', 'aligns to averaged'))], '(1)', ('cell', 93, 3))

# ---- docs/signal-health-retune-proposal.md
add('docs/signal-health-retune-proposal.md', 'D2', '21d1b7f', ('cell', 82, 2), [('(1)', ('sub', 82, '2e-7', '2e-7')), ('(2)', ('sub', 82, '3e-7', '3e-7')), ('(3)', ('sub', 82, 'keep 5e-8', 'keep 5e-8'))], '(1)', [('cell', 82, 3), ('lines', 27, 33)])

# ---- docs/placed-geometry-structural-first-proposal.md
add('docs/placed-geometry-structural-first-proposal.md', 'D3', '69441b5', ('cell', 100, 2), ('parse', ('cellsub', 100, 2, '**(a) clamp**', None)), '(a)', ('cell', 100, 3))

# ---- docs/placed-geometry-derivation-2026-07-06.md
add('docs/placed-geometry-derivation-2026-07-06.md', 'DG2', '0ce68ad', ('cell', 53, 2), [('(1)', ('sub', 53, '**1.6**', '**1.6**')), ('(2)', ('sub', 53, 'keep-1.2', 'keep-1.2')), ('(3)', ('sub', 53, 'p90-2.2', 'p90-2.2'))], '(1)', [('cell', 53, 3), ('line', 27), ('line', 41)])
add('docs/placed-geometry-derivation-2026-07-06.md', 'DG3', '0ce68ad', ('cell', 54, 2), [('(1)', ('sub', 54, '2.0 →', '2.0')), ('(2)', ('sub', 54, '**1.75** global', 'overrides'))], '(2)', ('cell', 54, 3))

# ---- docs/funding-momentum-time-anchored-window-proposal.md
add('docs/funding-momentum-time-anchored-window-proposal.md', 'D4', '595a519', ('cell', 90, 2), [('(1)', ('sub', 90, 'bundle at B4b', 'bundle at B4b')), ('(2)', ('sub', 90, 'own boundary post-#5-gate', 'own boundary post-#5-gate'))], '(2)', ('cell', 90, 3))

# ---- exclusions and unrecoverables recorded so far
excl('docs/bid-ask-spread-proposal.md', 'Q4', 'no_trader_ruling', 'status reads Open - calibration')
excl('docs/bid-ask-spread-proposal.md', 'Q1', 'options_not_explicit', '')
excl('docs/bid-ask-spread-proposal.md', 'Q3', 'options_not_explicit', '')
excl('docs/bid-ask-spread-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/ofi-momentum-proposal.md', 'Q3', 'no_trader_ruling', 'status reads Open - calibration')
excl('docs/ofi-momentum-proposal.md', 'Q1', 'options_not_explicit', '')
excl('docs/ofi-momentum-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/ofi-momentum-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/ofi-momentum-proposal.md', 'Q6', 'options_not_explicit', '')
excl('docs/swing-pivot-proposal.md', 'Q3', 'options_not_explicit', '')
excl('docs/swing-pivot-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/swing-pivot-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/swing-pivot-proposal.md', 'Q6', 'options_not_explicit', '')
excl('docs/swing-pivot-proposal.md', 'Q7', 'options_not_explicit', '')
excl('docs/swing-pivot-proposal.md', 'Q8', 'options_not_explicit', '')
excl('docs/swing-pivot-proposal.md', 'Q9', 'options_not_explicit', '')
excl('docs/vpfr-lite-v2-proposal.md', 'Q3', 'options_not_explicit', '')
excl('docs/vpfr-lite-v2-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/vpfr-lite-v2-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/vpfr-lite-v2-proposal.md', 'Q7', 'options_not_explicit', '')
excl('docs/vpfr-lite-v2-proposal.md', 'Q8', 'options_not_explicit', '')
excl('docs/dynamic-microcvd-accel-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/dynamic-microcvd-accel-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/dynamic-microcvd-accel-proposal.md', 'Q6', 'options_not_explicit', '')
excl('docs/dynamic-microcvd-accel-proposal.md', 'Q7', 'options_not_explicit', '')
excl('docs/settings-exposure-pass-proposal.md', 'Q1', 'options_not_explicit', '')
excl('docs/settings-exposure-pass-proposal.md', 'Q3', 'options_not_explicit', '')
excl('docs/settings-exposure-pass-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/settings-exposure-pass-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/settings-exposure-pass-proposal.md', 'Q6', 'options_not_explicit', '')
excl('docs/settings-exposure-pass-proposal.md', 'Q7', 'options_not_explicit', '')
excl('docs/settings-exposure-pass-proposal.md', 'Q8', 'options_not_explicit', '')
excl('docs/analysis-log-csv-expansion-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/analysis-log-csv-expansion-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/analysis-log-csv-expansion-proposal.md', 'Q7', 'options_not_explicit', '')
excl('docs/v17-followup-fixes-proposal.md', 'Q1', 'options_not_explicit', '')
excl('docs/v17-followup-fixes-proposal.md', 'Q2', 'options_not_explicit', '')
excl('docs/v17-followup-fixes-proposal.md', 'Q3', 'options_not_explicit', '')
excl('docs/v17-followup-fixes-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/v17-followup-fixes-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/v19-calibration-tuning-pass-proposal.md', 'Q1', 'options_not_explicit', '')
excl('docs/v19-calibration-tuning-pass-proposal.md', 'Q2', 'options_not_explicit', '')
excl('docs/v19-calibration-tuning-pass-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/v19-calibration-tuning-pass-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/v19-calibration-tuning-pass-proposal.md', 'Q6', 'options_not_explicit', '')
excl('docs/v19-calibration-tuning-pass-proposal.md', 'Q7', 'options_not_explicit', '')
excl('docs/v19-calibration-tuning-pass-proposal.md', 'Q8', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q1', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q2', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q3', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q6', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q7', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q8', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q10', 'options_not_explicit', '')
excl('docs/v20-rsi-roc-algorithm-fixes-proposal.md', 'Q11', 'options_not_explicit', '')
excl('docs/api-resilience-pass-proposal.md', 'Q3', 'options_not_explicit', '')
excl('docs/api-resilience-pass-proposal.md', 'Q4', 'options_not_explicit', '')
excl('docs/api-resilience-pass-proposal.md', 'Q5', 'options_not_explicit', '')
excl('docs/api-resilience-pass-proposal.md', 'Q6', 'options_not_explicit', '')
excl('docs/api-resilience-pass-proposal.md', 'Q7', 'options_not_explicit', '')
excl('docs/api-resilience-pass-proposal.md', 'Q8', 'options_not_explicit', '')
excl('docs/api-resilience-pass-proposal.md', 'Q9', 'options_not_explicit', '')
excl('docs/api-resilience-pass-proposal.md', 'Q10', 'options_not_explicit', '')
excl('docs/v22-funding-calibration-pass-proposal.md', 'Q1', 'options_not_explicit', 'also first committed already APPROVED (fad236a)')
excl('docs/v22-funding-calibration-pass-proposal.md', 'Q2', 'options_not_explicit', 'also first committed already APPROVED (fad236a)')
excl('docs/v22-funding-calibration-pass-proposal.md', 'Q3', 'options_not_explicit', 'also first committed already APPROVED (fad236a)')
excl('docs/v22-funding-calibration-pass-proposal.md', 'Q4', 'options_not_explicit', 'also first committed already APPROVED (fad236a)')
excl('docs/v22-funding-calibration-pass-proposal.md', 'Q5', 'options_not_explicit', 'also first committed already APPROVED (fad236a)')
excl('docs/v22-funding-calibration-pass-proposal.md', 'Q6', 'options_not_explicit', 'also first committed already APPROVED (fad236a)')
excl('docs/v22-funding-calibration-pass-proposal.md', 'Q7', 'options_not_explicit', 'also first committed already APPROVED (fad236a)')
excl('docs/v22-funding-calibration-pass-proposal.md', 'Q8', 'options_not_explicit', 'also first committed already APPROVED (fad236a)')
excl('docs/on-close-analysis-mode-proposal.md', 'S9.2', 'options_not_explicit', '')
excl('docs/on-close-analysis-mode-proposal.md', 'S9.3', 'options_not_explicit', '')
excl('docs/on-close-analysis-mode-proposal.md', 'S9.4', 'options_not_explicit', '')
excl('docs/on-close-analysis-mode-proposal.md', 'S9.5', 'options_not_explicit', '')
excl('docs/realtime-exit-guard-proposal.md', 'S9.2', 'options_not_explicit', '')
excl('docs/realtime-exit-guard-proposal.md', 'S9.4', 'options_not_explicit', '')
excl('docs/live-microstructure-strip-proposal.md', 'S9.1', 'options_not_explicit', '')
excl('docs/live-microstructure-strip-proposal.md', 'S9.3', 'options_not_explicit', '')
excl('docs/live-microstructure-strip-proposal.md', 'S9.6', 'options_not_explicit', '')
excl('docs/display-polish-pass-proposal.md', 'version-bump', 'no_trader_ruling', 'recommendation implemented in the same commit; no ruling recorded')
excl('docs/next-session-handover-2026-05-01.md', '8a', 'no_trader_ruling', 'explicitly left undecided')
excl('docs/ui-reskin-handover-2026-05-22.md', 'P4f-overlay', 'no_trader_ruling', 'surface choice to the user; later kickoff builds option 1 with no recorded ruling')
excl('docs/ui-reskin-handover-2026-05-22.md', 'P5-calib-viewer', 'no_trader_ruling', 'implemented per recommendation; no recorded ruling')
excl('docs/auto-tweaker-session-resolution-filter-proposal.md', 'S5.1', 'options_not_explicit', '')
excl('docs/auto-tweaker-session-resolution-filter-proposal.md', 'S5.3', 'options_not_explicit', '')
excl('docs/offline-analysis-report-audit-proposal.md', 'D2', 'options_not_explicit', '')
excl('docs/dev-workflow-automation-proposal.md', 'A', 'options_not_explicit', '')
excl('docs/clean-data-rebaseline-v34-proposal.md', 'S3.3', 'options_not_explicit', 'KEEP rows, no alternative named as a choice')
excl('docs/clean-data-rebaseline-v34-proposal.md', 'S3.5', 'options_not_explicit', 'KEEP rows, no alternative named as a choice')
excl('docs/clean-data-rebaseline-v34-proposal.md', 'S3.6', 'options_not_explicit', 'KEEP rows, no alternative named as a choice')
excl('docs/clean-data-rebaseline-v34-proposal.md', 'S3.7', 'options_not_explicit', 'KEEP rows, no alternative named as a choice')
excl('docs/clean-data-rebaseline-v34-proposal.md', 'S3.10', 'options_not_explicit', 'KEEP rows, no alternative named as a choice')
excl('docs/clean-data-rebaseline-v34-proposal.md', 'S3.8', 'not_a_decision', 'sanity pass / watching, no choice made')
excl('docs/clean-data-rebaseline-v34-proposal.md', 'S3.9', 'not_a_decision', 'sanity pass / watching, no choice made')
excl('docs/min-tradeable-move-gate-proposal.md', 'S2-alt', 'no_trader_ruling', 'the approval-in-principle predates the (A)/(B) design options; no ruling on them is recorded')
excl('docs/next-session-handover-2026-06-24.md', 'P3-cutover', 'no_explicit_recommendation', '(a)/(b) offered; the text calls the cutover data-justified but records no recommendation')
excl('docs/websocket-migration-p2-spec-back.md', 'S5.2', 'no_trader_ruling', 'coordinator recommendation recorded; deferred to P3 with no trader ruling in this doc')
excl('docs/auto-tweaker-phase2b-per-population-autotuning-proposal.md', 'S3-schema-home', 'no_trader_ruling', 'data-gated proposal, never ruled')
excl('docs/next-session-handover-fable-2026-06-10.md', 'S6', 'no_trader_ruling', 'open strategic decisions handed to the next seat; rulings not recorded here')
excl('docs/audit-fixes-2026-07-02-proposal.md', 'D4', 'options_not_explicit', '')
excl('docs/v48-ofi-dominance-rebaseline-proposal.md', 'D1', 'options_not_explicit', '')
excl('docs/v48-ofi-dominance-rebaseline-proposal.md', 'D2', 'options_not_explicit', '')
excl('docs/v48-ofi-dominance-rebaseline-proposal.md', 'D3', 'options_not_explicit', '')
excl('docs/v48-ofi-dominance-rebaseline-proposal.md', 'D4', 'options_not_explicit', '')
excl('docs/signal-health-retune-proposal.md', 'D1', 'options_not_explicit', '')
excl('docs/signal-health-retune-proposal.md', 'D3', 'options_not_explicit', '')
excl('docs/signal-health-retune-proposal.md', 'D4', 'options_not_explicit', '')
excl('docs/signal-health-retune-proposal.md', 'D5', 'options_not_explicit', '')
excl('docs/signal-health-retune-proposal.md', 'D6', 'options_not_explicit', '')
excl('docs/book-absorption-proposal.md', 'D1', 'options_not_explicit', '')
excl('docs/book-absorption-proposal.md', 'D2', 'options_not_explicit', '')
excl('docs/book-absorption-proposal.md', 'D3', 'options_not_explicit', '')
excl('docs/book-absorption-proposal.md', 'D4', 'options_not_explicit', '')
excl('docs/book-absorption-proposal.md', 'D5', 'options_not_explicit', '')
excl('docs/book-absorption-proposal.md', 'D6', 'options_not_explicit', '')
excl('docs/book-absorption-proposal.md', 'D7', 'options_not_explicit', '')
excl('docs/book-absorption-proposal.md', 'D8', 'options_not_explicit', '')
excl('docs/placed-geometry-structural-first-proposal.md', 'D1', 'options_not_explicit', '')
excl('docs/placed-geometry-structural-first-proposal.md', 'D2', 'options_not_explicit', '')
excl('docs/placed-geometry-structural-first-proposal.md', 'D4', 'options_not_explicit', '')
excl('docs/placed-geometry-structural-first-proposal.md', 'D5', 'options_not_explicit', '')
excl('docs/placed-geometry-structural-first-proposal.md', 'D6', 'options_not_explicit', '')
excl('docs/placed-geometry-structural-first-proposal.md', 'D7', 'options_not_explicit', '')
excl('docs/placed-geometry-structural-first-proposal.md', 'D8', 'options_not_explicit', '')
excl('docs/placed-geometry-derivation-2026-07-06.md', 'DG1', 'options_not_explicit', '')
excl('docs/placed-geometry-derivation-2026-07-06.md', 'DG4', 'options_not_explicit', '')
excl('docs/placed-geometry-derivation-2026-07-06.md', 'DG5', 'options_not_explicit', '')
excl('docs/cli-port-run-state-extraction-proposal.md', 'D1', 'options_not_explicit', '')
excl('docs/cli-port-run-state-extraction-proposal.md', 'D2', 'options_not_explicit', '')
excl('docs/cli-port-run-state-extraction-proposal.md', 'D3', 'options_not_explicit', '')
excl('docs/cli-port-run-state-extraction-proposal.md', 'D4', 'options_not_explicit', '')
excl('docs/cli-port-run-state-extraction-proposal.md', 'D5', 'options_not_explicit', '')
excl('docs/cli-port-run-state-extraction-proposal.md', 'D6', 'options_not_explicit', '')
excl('docs/cli-port-run-state-extraction-proposal.md', 'D7', 'options_not_explicit', '')
excl('docs/funding-momentum-time-anchored-window-proposal.md', 'D1', 'options_not_explicit', '')
excl('docs/funding-momentum-time-anchored-window-proposal.md', 'D2', 'options_not_explicit', '')
excl('docs/funding-momentum-time-anchored-window-proposal.md', 'D3', 'options_not_explicit', '')
excl('docs/funding-momentum-time-anchored-window-proposal.md', 'D5', 'options_not_explicit', '')
unrec('docs/engine-tier-d-hygiene-proposal.md', 'D1', 'first commit 43cc949 already carries the user confirmation')
unrec('docs/clean-data-rebaseline-v34-proposal.md', 'S3.1', 'ASIA multipliers; the only committed text (61b4532) already records the trader choice')
unrec('docs/clean-data-rebaseline-v34-proposal.md', 'S3.2', 'funding momentum_threshold 0.00001 -> 5e-8; doc first committed APPROVED & APPLIED')
unrec('docs/clean-data-rebaseline-v34-proposal.md', 'S3.4', 'CVD slope_pct_of_value 0.05 -> 0.10; doc first committed APPROVED & APPLIED')
