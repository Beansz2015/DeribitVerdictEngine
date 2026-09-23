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


def add(doc, label, src, q, opts, rec, rat, src_path=None, status='pop', reason='', note='',
        provenance='pre_ruling_revision'):
    """provenance: 'pre_ruling_revision' (default) = every state span is read at a revision that
    predates the ruling. 'quoted_in_pre_ruling_review' = the recommendation survives only as a
    verbatim QUOTATION inside a review written for the decision, before it was ruled, but that
    review was committed in the same commit as the ruling."""
    ENTRIES.append(dict(doc=doc, label=label, src=src, src_path=src_path or doc, q=q, opts=opts,
                        rec=rec, rat=rat, status=status, reason=reason, note=note, provenance=provenance))


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

# ---- docs/signal-bridge-v1-proposal.md: D1-D10 are single-design "Yes" rows
for _q in ('D1', 'D2', 'D3', 'D4', 'D5', 'D6', 'D7', 'D8', 'D9', 'D10'):
    excl('docs/signal-bridge-v1-proposal.md', _q, 'options_not_explicit')
excl('docs/fable5-audit-2026-07-02.md', 'F3', 'mirror', 'decision item with no recommendation; decided as audit-fixes-2026-07-02-proposal.md D3')

# ---- docs/d6-eval-placed-stop-migration-proposal.md
add('docs/d6-eval-placed-stop-migration-proposal.md', 'D1', '7b62b95', C(25, 2),
    [('(1)', E(25, 'Replace')), ('(2)', E(25, 'dual-track'))], '(1)', C(25, 3))
for _q in ('D2', 'D3', 'D4', 'D5'):
    excl('docs/d6-eval-placed-stop-migration-proposal.md', _q, 'options_not_explicit')

# ---- docs/aggressor-velocity-s52-derivation-2026-07-13.md (S-table)
add('docs/aggressor-velocity-s52-derivation-2026-07-13.md', 'S1', '542cb92', C(60, 2),
    [('(1)', E(60, '**4.5**')), ('(2)', E(60, '4.0 looser')), ('(3)', E(60, '5.0 tighter'))], '(1)', [C(60, 3), ('line', 37)])
add('docs/aggressor-velocity-s52-derivation-2026-07-13.md', 'S2', '542cb92', C(61, 2),
    [('(a)', S(49, 'the wire-in modifier applies only')), ('(b)', S(50, 'simpler-but-blunt'))], '(a)', [C(61, 3), ('line', 49)])
for _q in ('S3', 'S4', 'S5'):
    excl('docs/aggressor-velocity-s52-derivation-2026-07-13.md', _q, 'options_not_explicit')

# ---- what-if W-table, matrix M-table: no named alternatives
for _q in ('W1', 'W2', 'W3', 'W4', 'W5', 'W6', 'W7'):
    excl('docs/offline-whatif-replay-proposal.md', _q, 'options_not_explicit')
for _q in ('M1', 'M2', 'M3', 'M4', 'M5'):
    excl('docs/offline-matrix-placed-target-proposal.md', _q, 'options_not_explicit')

# ---- docs/session-policy-gate-proposal.md
add('docs/session-policy-gate-proposal.md', 'P1', 'ce601f6', C(39, 2),
    [('(1)', E(39, '**Consumer-side**')), ('(2)', E(39, 'Engine-side per-session thresholds'))], '(1)', C(39, 3))
for _q in ('P2', 'P3', 'P4', 'P5'):
    excl('docs/session-policy-gate-proposal.md', _q, 'options_not_explicit')

# ---- docs/eval-no-data-outcome-proposal.md (N-table) and eval-display-semantics (E-table)
add('docs/eval-no-data-outcome-proposal.md', 'N4', 'e722ad7', C(26, 2),
    [('(1)', E(26, 'Migrate to placed geometry')), ('(2)', E(26, 'keep a fixed yardstick'))], '(1)', [C(26, 3), ('line', 17)])
for _q in ('N1', 'N2', 'N3', 'N5'):
    excl('docs/eval-no-data-outcome-proposal.md', _q, 'options_not_explicit')
for _q in ('E1', 'E2a', 'E2b', 'E3a', 'E3b', 'E4', 'E5'):
    excl('docs/eval-display-semantics-proposal.md', _q, 'options_not_explicit')

# ---- docs/geometry-arbitration-modes-proposal.md (G-table)
add('docs/geometry-arbitration-modes-proposal.md', 'G2', '8f72f7f', C(35, 2),
    [('(1)', E(35, '**% of placed distance**')), ('(2)', E(35, 'ATR-fraction'))], '(1)', C(35, 3))
for _q in ('G1', 'G3', 'G4', 'G5', 'G6'):
    excl('docs/geometry-arbitration-modes-proposal.md', _q, 'options_not_explicit')

# ---- docs/w6-4-ceiling-audit-method-proposal.md (K-table)
for _q in ('K1', 'K2', 'K3', 'K4', 'K5', 'K6'):
    excl('docs/w6-4-ceiling-audit-method-proposal.md', _q, 'options_not_explicit')


# ==========================================================================================
# LEAK_NOTES: every leak-check hit on a population text field, explained. Key = (id, field,
# marker). A hit with no note is UNEXPLAINED and blocks --write. Each note says why the hit is
# pre-ruling text and carries no ruling. Kept HERE (state side): no note names an outcome.
# ==========================================================================================
LEAK_NOTES = {}


def leak(doc, label, field, marker, why):
    LEAK_NOTES[('%s|%s@%s' % (doc, label, REV), field, marker)] = why


_PRE = 'pre-ruling text addressed to the trader as the future decider; conditional, carries no ruling'
leak('docs/session-policy-gate-proposal.md', 'P1', 'rationale', 'ticked', _PRE + " ('if ticked')")
leak('docs/eval-no-data-outcome-proposal.md', 'N4', 'rationale', 'trader', _PRE + " ('trader may overrule', 'if the trader prefers')")
leak('docs/eval-no-data-outcome-proposal.md', 'N4', 'rationale', 'overrul', _PRE + " ('trader may overrule')")
leak('docs/geometry-arbitration-modes-proposal.md', 'G2', 'rationale', 'ticked', _PRE + " ('record whichever is ticked')")
leak('docs/geometry-arbitration-modes-proposal.md', 'G2', 'rationale', 'trader', "names the trader's earlier framing of the buffer, an input to the recommendation, not a ruling on it")

# ---- docs/liq-cascade-level-alerts-proposal.md (H-table)
add('docs/liq-cascade-level-alerts-proposal.md', 'H1', '74eff4b', C(22, 2),
    [('(1)', E(22, '**TAPE-strip tag + status-bar flash**')), ('(2)', E(22, 'modal/banner'))], '(1)', C(22, 3))
for _q in ('H2', 'H3', 'H4', 'H5'):
    excl('docs/liq-cascade-level-alerts-proposal.md', _q, 'options_not_explicit')

# ---- docs/absorption-geometry-rescale-proposal.md (V-table; single commit, table pre-tick)
add('docs/absorption-geometry-rescale-proposal.md', 'V2', '309b0fc', C(18, 2),
    [('(1)', E(18, 'Retire tick keys (unresolvable)')), ('(2)', E(18, 'keep-with-null'))], '(1)', C(18, 3))
for _q in ('V1', 'V3', 'V4'):
    excl('docs/absorption-geometry-rescale-proposal.md', _q, 'options_not_explicit')

# ---- docs/aggr-vel-s52-london-derivation-2026-07-23.md (S-table; ruling recorded in a commit message only)
add('docs/aggr-vel-s52-london-derivation-2026-07-23.md', 'S1', '8b7cc38', C(82, 2),
    [('(1)', E(82, '**5.5**')), ('(2)', E(82, '5.0 looser')), ('(3)', E(82, '6.0 tighter'))], '(1)', C(82, 3))
for _q in ('S2', 'S3', 'S4', 'S5'):
    excl('docs/aggr-vel-s52-london-derivation-2026-07-23.md', _q, 'options_not_explicit')

# ---- single-design D-tables with no named alternative
for _q in ('D1', 'D2', 'D3', 'D4', 'D5', 'D6'):
    excl('docs/fee-aware-min-move-proposal.md', _q, 'options_not_explicit')
for _q in ('D1', 'D2', 'D3', 'D4', 'D5', 'D6'):
    excl('docs/d2v2-whatif-candidate-mode-proposal.md', _q, 'options_not_explicit')
for _q in ('D1', 'D2', 'D3', 'D4', 'D5', 'D6', 'D7', 'D8'):
    excl('docs/backtest-synthesizer-proposal.md', _q, 'options_not_explicit')
for _q in ('J1a', 'J1b', 'J1c', 'J1d', 'J2', 'J3'):
    excl('docs/absorption-engagement-derivation-2026-07-23.md', _q, 'options_not_explicit')
excl('docs/absorption-engagement-derivation-2026-07-23.md', 'J1e', 'superseded_before_ruling',
     'Path A vs Path B: the seat later moved its own recommendation before any ruling; the ruled decision is the queue row E5 (trader-tick-queue-archive.md)')
excl('docs/forming-bar-live-investigation-2026-07.md', 'S4', 'no_explicit_recommendation', 'section 4 options are enumerated, explicitly not recommended')

excl('docs/liq-cascade-level-alerts-spec-back.md', 'S5-open-options', 'no_explicit_recommendation', 'four open decisions ruled by the trader; the pre-ruling section 4 carried no recommendation on them')

# ---- docs/in-app-trade-store-capture-proposal.md
add('docs/in-app-trade-store-capture-proposal.md', 'D1', '5ea86f7', C(133, 2),
    [('(1)', E(133, 'Both boxes capture')), ('(2)', E(133, 'AWS only'))], '(1)', C(133, 3))
for _q in ('D2', 'D3', 'D4', 'D5'):
    excl('docs/in-app-trade-store-capture-proposal.md', _q, 'options_not_explicit')
excl('docs/in-app-trade-store-capture-proposal.md', 'D1-a', 'no_explicit_recommendation', 'first committed already ruled (4ab1824); no recommendation text precedes it')

# ---- docs/settings-local-overlay-proposal.md (Decision | Options | My read)
for _q, _ln in (('D1', 187), ('D2', 188), ('D3', 189), ('D4', 190), ('D5', 191), ('D6', 192)):
    add('docs/settings-local-overlay-proposal.md', _q, '00a8478', C(_ln, 2), ('parse', C(_ln, 3)), '(a)', C(_ln, 4))
excl('docs/settings-local-overlay-proposal.md', 'D7', 'no_trader_ruling', 'answered by the orchestrator seat, not the trader')
excl('docs/settings-local-overlay-review-2026-07-31.md', 'S5', 'mirror', 'a reviewing seat read of the same D1-D7')

# ---- docs/trade-store-coverage-report-proposal.md (Decision | Options | My read), last pre-tick rev 34f074f
_d = 'docs/trade-store-coverage-report-proposal.md'
for _q, _ln in (('D1', 258), ('D2', 259), ('D3', 260), ('D7', 262), ('D6', 264)):
    add(_d, _q, '34f074f', C(_ln, 2), ('parse', C(_ln, 3)), '(a)', C(_ln, 4))
add(_d, 'D4', '34f074f', C(261, 2), [('(1)', S(261, '300,000 ms (1.85×', 'max)')), ('(2)', E(261, 'another value'))], '(1)', C(261, 4))
excl(_d, 'D5', 'recommendation_not_single', 'the read recommends "(a) or (b), not (c)"')

# ---- docs/asia-burst-threshold-derivation-2026-08-01.md: the D-table was co-committed with its
# ticks at the build (970087b); the recommendation and candidates are read from 1fc221b.
_d = 'docs/asia-burst-threshold-derivation-2026-08-01.md'
add(_d, 'D3-1', '1fc221b', ('line', 1),
    [('(1)', C(21, 1)), ('(2)', C(22, 1)), ('(3)', C(23, 1)), ('(4)', C(24, 1)), ('(5)', C(25, 1)), ('(6)', C(26, 1)), ('(7)', C(27, 1)), ('(8)', C(28, 1))],
    '(7)', [('line', 9), ('lines', 31, 35)])
excl(_d, 'D3-2', 'options_not_explicit', 'pre-ruling text argues one value; the ASIA-vs-LONDON framing exists only in the co-committed D-table')
for _q in ('D3-3', 'D3-4', 'D3-5', 'D3-6'):
    excl(_d, _q, 'options_not_explicit', 'rows exist only in the D-table co-committed with its ruling')

# ---- docs/ttm-flat-threshold-rederivation-2026-08-02.md (Question | Options | Recommendation)
_d = 'docs/ttm-flat-threshold-rederivation-2026-08-02.md'
add(_d, 'D1-a', 'bec076f', C(84, 2), ('parse', C(84, 3)), '(a)', C(84, 4))
add(_d, 'D1-d', 'bec076f', C(87, 2), ('parse', C(87, 3)), '(b)', C(87, 4))
excl(_d, 'D1-b', 'recommendation_not_single', 'recommends a range 0.25-0.30')
excl(_d, 'D1-c', 'not_a_decision', 'records a value for completeness, explicitly not recommended')

for _q in ('T-1', 'T-2', 'T-3', 'T-4', 'T-5'):
    excl('docs/d3-asia-burst-watch-read-2026-08-10.md', _q, 'options_not_explicit')
for _q in ('D-C', 'D-D', 'D-E'):
    excl('docs/job2-read-2026-07-31.md', _q, 'no_trader_ruling', 'ruled by the orchestrator seat on an implementer packet, not by the trader')
excl('docs/candle-store-derivation-batch-spec-back.md', 'S2', 'no_trader_ruling', 'decisions queued to the orchestrator; ruled in job2-read by the orchestrator')
excl('docs/pre-aug1-batch-spec-back.md', 'S2+S6', 'no_trader_ruling', 'queued decisions ruled by the reviewing Fable seat, not the trader')
excl('docs/trade-store-arc-spec-back-2026-07-31.md', 'S2', 'no_trader_ruling', 'decisions queued to the reviewing seat; no trader ruling recorded')
leak('docs/asia-burst-threshold-derivation-2026-08-01.md', 'D3-1', 'question', 'date>2026-07-31',
     "the doc's own title date, written in the GMT+8 local date of the same commit that carries the recommendation (committed 2026-07-31T17:09Z); not a ruling date")

# ---- docs/trade-store-trade-identity-proposal.md (Question | Recommendation), pre-tick 9fa6450
_d = 'docs/trade-store-trade-identity-proposal.md'
add(_d, 'D1', '9fa6450', C(151, 2), [('(1)', E(151, '`trade_id`')), ('(2)', E(151, '`trade_seq`')), ('(3)', E(151, 'both?'))], '(3)', C(151, 3))
add(_d, 'D5', '9fa6450', C(155, 2), [('(1)', E(155, 'Append at end')), ('(2)', E(155, 'version the file'))], '(1)', C(155, 3))
add(_d, 'D6', '9fa6450', C(156, 2), [('(1)', E(156, 'replace S0')), ('(2)', E(156, 'supplement it'))], '(2)', C(156, 3))
for _q in ('D2', 'D3', 'D4'):
    excl(_d, _q, 'options_not_explicit')
excl(_d, 'D7', 'no_explicit_recommendation', "the read says the choice is the trader's")

# ---- docs/trade-store-write-guard-identity-proposal.md (Decision | Options | My read), pre-tick b7ae9a5
_d = 'docs/trade-store-write-guard-identity-proposal.md'
for _q, _ln in (('D-1', 172), ('D-3', 174), ('D-4', 175), ('D-5', 176)):
    add(_d, _q, 'b7ae9a5', C(_ln, 2), ('parse', C(_ln, 3)), '(a)', C(_ln, 4))
add(_d, 'D-2', 'b7ae9a5', C(173, 2), [('(1)', CS(173, 3, 'ratify', 'ratify')), ('(2)', CS(173, 3, 'overrule', 'overrule'))], '(1)', C(173, 4))
add(_d, 'D-6', 'b7ae9a5', C(177, 2), [('(1)', CS(177, 3, 'bump', 'bump')), ('(2)', CS(177, 3, 'not', 'not'))], '(2)', C(177, 4))
add(_d, 'D-7', 'b7ae9a5', C(178, 2), [('(1)', CS(178, 3, 'yes', 'yes')), ('(2)', CS(178, 3, 'no', 'no'))], '(1)', C(178, 4))
leak(_d, 'D-2', 'option(2)', 'overrul', 'the option is literally named "overrule" in the pre-ruling Options column; it is an option, not a ruling')
leak(_d, 'D-2', 'rationale', 'overrul', 'conditional "If overruled, the build must first verify..." written before the ruling')

# ---- docs/trade-store-downtime-repair-proposal.md: D-table first committed already ticked (c6c6942);
# and no row names an alternative as an option
for _q in ('D-1', 'D-2', 'D-3', 'D-4', 'D-5', 'D-6'):
    excl('docs/trade-store-downtime-repair-proposal.md', _q, 'options_not_explicit', 'also first committed already ticked (c6c6942)')
excl('docs/trade-store-downtime-repair-spec-back.md', 'Q1-Q5', 'no_trader_ruling', 'ruled by the reviewing orchestrator seat, not the trader')

unrec('docs/downtime-repair-followups-implementer-briefs.md', 'S1.2', 'width-floor removal; the brief was first committed (91942d6) together with the build that followed the ruling')
for _q in ('D-1', 'D-2'):
    excl('docs/coverage-split-hour-implementer-brief.md', _q, 'no_trader_ruling', 'reads taken by the implementer as written; no trader ruling')
excl('docs/coverage-split-hour-implementer-brief.md', 'D-3', 'no_explicit_recommendation', 'the implementer built one order; no recommendation among options preceded the ruling')

# ---- docs/absorption-mechanism-revision-proposal.md section 6
for _q in ('D-1', 'D-2', 'D-3', 'D-4', 'D-5'):
    excl('docs/absorption-mechanism-revision-proposal.md', _q, 'options_not_explicit')
excl('docs/absorption-mechanism-revision-proposal.md', 'D-6', 'no_explicit_recommendation', 'carried no recommendation; split into D-6a..D-6d, entered under absorption-d6-spec-back.md and d6d-episode-continuity-spec.md')

# ---- docs/absorption-d6-spec-back.md section 2 (option tables + "My read"), pre-ruling 98af629
_d = 'docs/absorption-d6-spec-back.md'
add(_d, 'D-6a', '98af629', ('line', 151), [('(a)', C(155, 2)), ('(b)', C(156, 2)), ('(c)', C(157, 2))], '(a)', ('line', 159))
add(_d, 'D-6b', '98af629', ('line', 161), [('(a)', C(165, 2)), ('(b)', C(166, 2))], '(a)', ('line', 168))
add(_d, 'D-6c', '98af629', ('line', 170), [('(a)', C(174, 2)), ('(b)', C(175, 2))], '(a)', ('line', 177),
    status='unruled', reason='open_never_ruled', note='left OPEN until the section 5 instrumentation ships')
leak(_d, 'D-6a', 'rationale', 'trader', 'a link to docs/trader-profile.md (a file name)')

# ---- docs/d6d-episode-continuity-spec.md section 7
_d = 'docs/d6d-episode-continuity-spec.md'
add(_d, 'D-6d.1', 'b2ebae8', C(248, 2), [('(a)', E(248, 'Sidecar `absorption_episodes.log`')), ('(b)', E(248, 'five new `analysis_log.csv` columns'))], '(a)', C(248, 3))
add(_d, 'D-6d.2', '4ce5a10', C(247, 2), [('(a)', E(247, 'Stage 1 ship alone and read for ~2 weekday-weeks')), ('(b)', E(247, 'both stages ship together'))], '(a)', C(247, 3))
add(_d, 'D-6d.3', '4ce5a10', C(248, 2),
    [('(a)', E(248, 'stay scheduled for ~2026-09-15')), ('(b)', E(248, 'move behind `D-6d`')), ('(c)', S(248, 'SHIP `D-2` AND `D-6d` STAGE 1 TOGETHER AT THE GATE', 'AFTER THE READ'))],
    '(c)', C(248, 3), note='the recommendation was revised from (b) to (c) before the ruling; the state is the last pre-ruling revision')
add(_d, 'D-6d.4', 'b5000c9', C(251, 2), [('(a)', E(251, 'measurement only')), ('(b)', E(251, 'eventually the numerator'))], '(a)', C(251, 3),
    status='unruled', reason='open_never_ruled', note='section 7 still reads owed by the trader at REV')
_FUT = 'a PLANNED future date (a scheduled gate) written before the ruling, not the date of any ruling'
leak('docs/d6d-episode-continuity-spec.md', 'D-6d.3', 'question', 'date>2026-09-11', _FUT)
leak('docs/d6d-episode-continuity-spec.md', 'D-6d.3', 'option(a)', 'date>2026-09-11', _FUT)

# ---- docs/thin-trade-window-skip-gate-proposal.md (single commit; ruling in the spec-back)
_d = 'docs/thin-trade-window-skip-gate-proposal.md'
add(_d, 'D-1', '821b689', C(163, 2), [('(1)', S(163, '**SKIP the run**', 'SKIPPED bridge payload)')), ('(2)', E(163, 'proceed with trade-derived signals forced neutral'))], '(1)', C(163, 3))
for _q in ('D-2', 'D-3', 'D-4', 'D-5'):
    excl(_d, _q, 'options_not_explicit')

# ---- docs/collector-ops-tooling-proposal.md (Decision | Recommendation), pre-tick cf13daf
_d = 'docs/collector-ops-tooling-proposal.md'
add(_d, 'D-7', 'cf13daf', C(152, 2), [('(1)', E(152, 'the t2.micro test box too')), ('(2)', E(152, 'production only'))], '(1)', C(152, 3))
add(_d, 'D-9', 'cf13daf', C(154, 2), [('(1)', E(154, 'run on a schedule (e.g. daily)')), ('(2)', E(154, 'on demand'))], '(2)', C(154, 3))
for _q in ('D-1', 'D-2', 'D-5'):
    excl(_d, _q, 'options_not_explicit')
for _q in ('D-3', 'D-4', 'D-6'):
    excl(_d, _q, 'options_not_explicit', 'also first committed already trader-chosen')
excl(_d, 'D-8', 'no_explicit_recommendation', 'trader input required; no recommendation')

for _q in ('D-1', 'D-2', 'D-3', 'D-4'):
    excl('docs/deploy-acceptance-gate-cadence-spec.md', _q, 'no_trader_ruling', 'handed to the trader, then built as recommended; no trader ruling is recorded')
excl('docs/seat-handover-2026-08-23.md', 'S7.4', 'no_explicit_recommendation', 'asks for a ruling; names no options')

# ---- docs/autotweaker-weekday-filter-proposal.md: first committed already ruled (81c54a8)
for _q in ('D-1', 'D-2', 'D-3', 'D-4'):
    unrec('docs/autotweaker-weekday-filter-proposal.md', _q, 'the D-table was first committed with its RULED column filled (81c54a8)')
excl('docs/autotweaker-weekday-filter-proposal.md', 'D-5', 'options_not_explicit')

# ---- docs/coverage-trailing-edge-f1-proposal.md
# Two ruling events. First tick on the original D-table (state 1bd268f). Then the seat re-opened
# D-3 and D-6 with a NEW option (c) and raised D-5.1..D-5.5 (state 3d0b8e1); second tick. Each
# recommendation -> ruling pair is its own record: the re-opened rows are D-3r and D-6r.
_d = 'docs/coverage-trailing-edge-f1-proposal.md'
for _q, _ln, _r in (('D-1', 87, '(a)'), ('D-2', 88, '(a)'), ('D-3', 89, '(a)'), ('D-5', 91, '(a)'), ('D-6', 92, '(a)')):
    add(_d, _q, '1bd268f', C(_ln, 2), ('parse', C(_ln, 3)), _r, C(_ln, 4))
add(_d, 'D-4', '1bd268f', C(90, 2),
    [('(a)', CS(90, 3, 'evidence boundary only', 'only')), ('(b)', CS(90, 3, 'evidence boundary **AND** store-end', 'store-end')), ('(c)', CS(90, 3, '(b) plus hour-aligning', 'final hour'))],
    '(b)', C(90, 4))
leak(_d, 'D-6r', 'rationale', 'ticked', 'cites the earlier tick of a DIFFERENT decision (D-2) as the premise of the re-opening; says nothing of how D-6r was ruled')
for _q, _ln in (('D-5.1', 132), ('D-5.2', 133), ('D-5.3', 134), ('D-5.4', 135), ('D-5.5', 136)):
    add(_d, _q, '3d0b8e1', C(_ln, 2), ('parse', C(_ln, 3)), '(a)', C(_ln, 4))
add(_d, 'D-6r', '3d0b8e1', ('line', 138),
    [('(a)', C(156, 1)), ('(b)', C(157, 1)), ('(c)', C(158, 1))], '(c)',
    [('lines', 140, 150), C(156, 2), C(157, 2), C(158, 2)])
add(_d, 'D-3r', '3d0b8e1', ('line', 162),
    [('(a)', C(170, 1)), ('(c)', C(171, 1))], '(c)',
    [('lines', 164, 166), C(170, 2), C(171, 2), ('line', 173)])

# ---- Queue-only decisions, now archived in docs/trader-tick-queue-archive.md. State read from
# docs/trader-tick-queue.md at b7ae9a5, the last revision before the ruling commit ee16d03.
_q = 'docs/trader-tick-queue.md'
_a = 'docs/trader-tick-queue-archive.md'
add(_a, 'A54a-scope', 'b7ae9a5', CS(38, 1, 'A54a scope — GUARD the third copy, or DELETE it?', 'DELETE it?'),
    [('(a)', S(38, 'Guard three copies', "the audit's Row 2.")), ('(b)', S(38, 'Delete the method defaults, make the parameters required', 'rather than watching three.'))],
    '(b)', C(38, 3), src_path=_q)
add(_a, 'seeded-session-buckets', 'b7ae9a5', CS(39, 1, 'Seeded session buckets — does the code-defaults path exist at all?', 'at all?'),
    [('(1)', S(39, 're-sync AND guard it', 'guard it')), ('(2)', S(39, 'an empty list is honest', 'honest'))],
    '(2)',
    [C(39, 3),
     AT('ee16d03', 'docs/seam-audit-decisions-second-opinion-2026-08-11.md', S(106, '*"wrong values are worse than absent values', 'wrong is not."*')),
     AT('ee16d03', 'docs/seam-audit-decisions-second-opinion-2026-08-11.md', CS(140, 2, 'empty the seed'))],
    src_path=_q, provenance='quoted_in_pre_ruling_review',
    note='the queue row names the options but carries no read; the requester recommendation survives as the verbatim quotation and the "Requester\'s call" cell of the second-opinion review written for this decision')
excl('docs/seam-audit-decisions-second-opinion-2026-08-11.md', 'Decisions-1-2', 'mirror', 'second-opinion review of the two queue decisions entered under trader-tick-queue-archive.md')

# ---- docs/a54a-json-poco-drift-guard-spec.md section 4, pre-tick 6e1753b
_d = 'docs/a54a-json-poco-drift-guard-spec.md'
add(_d, 'D-1', '6e1753b', CS(231, 2, '**How is a DELIBERATE divergence declared?**', 'declared?**'), ('parse', CS(231, 2, '(a) a small explicit allow-list')), '(a)', [C(231, 3), C(231, 4)])
add(_d, 'D-4', '6e1753b', C(234, 2), [('(1)', E(234, 'FAIL the harness')), ('(2)', E(234, 'WARN'))], '(1)', [C(234, 3), C(234, 4)])
add(_d, 'D-5', '6e1753b', C(235, 2), [('(1)', E(235, 'ship here')), ('(2)', E(235, 'as its own session'))], '(2)', [C(235, 3), C(235, 4)])
excl(_d, 'D-2', 'options_not_explicit')
excl(_d, 'D-3', 'no_explicit_recommendation', 'the spec abstains: "I do not have a read and will not invent one"')
excl('docs/a54a-drift-guard-escalation-2026-09-04.md', 'S5', 'no_trader_ruling', 'the ruling it asked for already existed; resolved the same day with no new ruling')
excl('docs/a54a-drift-guard-spec-back.md', 'R-2', 'no_explicit_recommendation', 'three dispositions named, no read given')

# ---- docs/a54a-r2-r3-followup-spec.md D-R3, pre-tick 778364e
_d = 'docs/a54a-r2-r3-followup-spec.md'
add(_d, 'D-R3', '778364e', C(125, 2),
    [('(i)', CS(125, 3, 'Seed `SessionBucketSettings.RocMagnitudeThreshold`', 'matches shipped exactly.')),
     ('(ii)', CS(125, 3, 'Do (b) only', 'record it.')),
     ('(iii)', CS(125, 3, 'Seed only the slope profile', 'arbitrary'))], '(i)', C(125, 4))

# ---- docs/a54a-session2-step1-measurement-2026-09-05.md section 7.3 (S2-1), reviewer read at 9d6bec0
_d = 'docs/a54a-session2-step1-measurement-2026-09-05.md'
add(_d, 'S2-1', '9d6bec0', ('line', 255), [('(A)', C(264, 1)), ('(B)', C(265, 1))], '(A)', [C(264, 2), C(265, 2), ('lines', 266, 269)])
leak(_d, 'S2-1', 'rationale', 'ruled', "cites the EARLIER ruling that framed session 2 as dead-code removal; a premise of this read, not its ruling")

for _q2 in ('D1', 'D2', 'D3', 'D4'):
    excl('docs/i17-sweep-spec-back.md', _q2, 'no_trader_ruling', 'D-table ruled by the reviewing seat (section 5), not the trader')
for _q2 in ('Q-1', 'Q-2', 'Q-3'):
    excl('docs/s2-2-calcspread-split-spec-back.md', _q2, 'no_trader_ruling', 'ruled by the reviewing seat')
for _q2 in ('Q-4', 'Q-5'):
    excl('docs/s2-2-r1-r2-remediation-spec-back.md', _q2, 'no_trader_ruling', 'ruled by the reviewing seat')

# ---- docs/s2-2-calcspread-split-proposal.md section 4, pre-tick 368c17a
_d = 'docs/s2-2-calcspread-split-proposal.md'
for _q2, _ln, _r in (('D-1', 160, '(a)'), ('D-2', 161, '(a)'), ('D-3', 162, '(b)'), ('D-4', 163, '(a)'), ('D-5', 164, '(a)')):
    add(_d, _q2, '368c17a', C(_ln, 2), ('parse', C(_ln, 3)), _r, C(_ln, 4))
_PRIOR = 'cites an EARLIER ruling of a DIFFERENT decision as precedent for this read; says nothing of how this decision was ruled'
leak('docs/a54a-json-poco-drift-guard-spec.md', 'D-1', 'rationale', 'trader', _PRIOR + ' (two earlier trader decisions the rejected option would reverse)')
leak('docs/a54a-json-poco-drift-guard-spec.md', 'D-1', 'option(a)', 'ruled', 'describes what each allow-list entry must carry ("the doc that ruled it"); a design property of the option, not a ruling')
leak('docs/s2-2-calcspread-split-proposal.md', 'D-1', 'rationale', 'ruled', _PRIOR)

# ---- more queue-only decisions (trader-tick-queue.md rows, archived at REV)
_q = 'docs/trader-tick-queue.md'
_a = 'docs/trader-tick-queue-archive.md'
add(_a, 'pooled-minute-key-dedup', 'ee16d03', CS(133, 1, 'Bound the pooled minute-key dedup — before the next pooled read', 'pooled read'),
    [('(1)', E(133, 'AWS-preferred **minute-key** dedup for pooling')), ('(2)', S(133, 'fix the key first', '+ timestamp'))],
    '(2)', C(133, 3), src_path=_q)
add(_a, 'E5-absorption-path', 'c38ea02', CS(36, 1, '**E5** absorption **Path B**'),
    [('(A)', E(36, "Path A's V-table")), ('(B)', S(36, 'Path B (hold v61 anchors', 'no ⚠)'))], '(B)', C(36, 3), src_path=_q)
excl(_a, 'F3-watch', 'no_explicit_recommendation', 'two courses named (offline tooling or retirement); the row asks only that the watch not stay live')
excl(_a, 'C1-coverage-F2', 'no_explicit_recommendation', 'the pre-ruling row names no options; the options not taken are introduced in the ruling text')
add(_a, 'WD-SEMANTICS', 'a6cfb8d',
    S(344, '`WD-SEMANTICS` — `WeekendExcluded` means THREE DIFFERENT THINGS on three surfaces', 'on three surfaces'),
    [('(a)', S(344, 'unify on "MinValue counts as weekend"', 'changes two rendered numbers)')),
     ('(b)', S(344, 'unify on "MinValue drops silently"', "changes `AnalysisRunner`'s number)")),
     ('(c)', S(344, 'add a separate `UnparsedExcluded` counter', 'parity rule fires.'))],
    '(b)', S(344, '⚠ **My read: (c) is right in principle', 'Verify it before ruling.**'), src_path=_q,
    note='the read calls (c) right in principle and (b) right in practice, then argues against paying for (c); taken as recommending (b)')
excl('docs/wd-semantics-unparsed-counter-spec.md', 'S-all', 'mirror', 'the post-ruling spec for the decision entered under trader-tick-queue-archive.md WD-SEMANTICS')

# ---- docs/s4-eval-cache-identity-proposal.md section 3, pre-tick edd77b7 (Decision | Options | My read)
_d = 'docs/s4-eval-cache-identity-proposal.md'
for _l, _ln in (('D-1', 100), ('D-2', 101), ('D-3', 102)):
    add(_d, _l, 'edd77b7', C(_ln, 2), ('parse', C(_ln, 3)), '(a)', C(_ln, 4))

# ---- docs/item6-wstradeprobe-s1-recheck-2026-09-07.md section 5 (S-1), pre-ruling 30b0c3f
_d = 'docs/item6-wstradeprobe-s1-recheck-2026-09-07.md'
add(_d, 'S-1', '30b0c3f', ('line', 119), [('(a)', C(125, 2)), ('(b)', C(126, 2)), ('(c)', C(127, 2))], '(a)',
    [C(125, 3), C(125, 4), C(126, 3), C(126, 4), C(127, 3), C(127, 4), ('lines', 129, 137)])

# ---- docs/absorption-d2-stage1-rotation-build-spec.md section 9 (RD-1), pre-tick 3fb6eda
_d = 'docs/absorption-d2-stage1-rotation-build-spec.md'
add(_d, 'RD-1', '3fb6eda', C(217, 2), ('parse', C(217, 3)), '(b)', C(217, 4))
leak('docs/absorption-d2-stage1-rotation-build-spec.md', 'RD-1', 'rationale', 'ruled', _PRIOR + ' (the WD-SEMANTICS ruling, and the already-ruled column set of this rotation)')
