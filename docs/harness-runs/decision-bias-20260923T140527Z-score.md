# Decision-bias tripwire — score

- population rev `b5000c9`, items scored: 150 (provenance filter: all; excluded granularity: none; CLAUDE.md-named four excluded: no; revealed sources excluded: no)
- outcomes: ADOPTED 124, OVERRULED 22, PARTIAL 4

### Regex arm — pattern `adequate|good enough|buys nothing|defer` on the rationale, pre-registered

| | flagged | not flagged | n |
|---|---:|---:|---:|
| OVERRULED | 5 | 17 | 22 |
| ADOPTED | 2 | 122 | 124 |
| PARTIAL (reported apart) | 0 | 4 | 4 |

- Catches among OVERRULED: **5 of 22** (Wilson 95% 0.10–0.43)
- False flags among ADOPTED: **2 of 124** (Wilson 95% 0.00–0.06)
- ⚠ PILOT, NOT A RATE: the deciding arm (OVERRULED) has n = 22.

### Seat arm — flag = `gives_up_for_economy`; 1 'unsure' rows excluded

| | flagged | not flagged | n |
|---|---:|---:|---:|
| OVERRULED | 11 | 11 | 22 |
| ADOPTED | 13 | 110 | 123 |
| PARTIAL (reported apart) | 1 | 3 | 4 |

- Catches among OVERRULED: **11 of 22** (Wilson 95% 0.31–0.69)
- False flags among ADOPTED: **13 of 123** (Wilson 95% 0.06–0.17)
- ⚠ PILOT, NOT A RATE: the deciding arm (OVERRULED) has n = 22.

### Jev arm — flag = modal verdict `gives_up_for_economy`, all judged rows

| | flagged | not flagged | n |
|---|---:|---:|---:|
| OVERRULED | 9 | 13 | 22 |
| ADOPTED | 16 | 108 | 124 |
| PARTIAL (reported apart) | 0 | 4 | 4 |

- Catches among OVERRULED: **9 of 22** (Wilson 95% 0.23–0.61)
- False flags among ADOPTED: **16 of 124** (Wilson 95% 0.08–0.20)
- ⚠ PILOT, NOT A RATE: the deciding arm (OVERRULED) has n = 22.

### Jev arm, STABLE rows only — agreement rate 1.0

| | flagged | not flagged | n |
|---|---:|---:|---:|
| OVERRULED | 9 | 13 | 22 |
| ADOPTED | 15 | 100 | 115 |
| PARTIAL (reported apart) | 0 | 4 | 4 |

- Catches among OVERRULED: **9 of 22** (Wilson 95% 0.23–0.61)
- False flags among ADOPTED: **15 of 115** (Wilson 95% 0.08–0.20)
- ⚠ PILOT, NOT A RATE: the deciding arm (OVERRULED) has n = 22.

### Jev against seat, per item

| id | seat | Jev modal | agreement | stable | match |
|---|---|---|---:|---|---|
| docs/bid-ask-spread-proposal.md|Q2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/ofi-momentum-proposal.md|Q2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/swing-pivot-proposal.md|Q1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/swing-pivot-proposal.md|Q2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/vpfr-lite-v2-proposal.md|Q1@b5000c9 | no_richer_option | ambiguous | 1 | True | DISAGREE |
| docs/vpfr-lite-v2-proposal.md|Q2@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/vpfr-lite-v2-proposal.md|Q6@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/dynamic-microcvd-accel-proposal.md|Q1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/dynamic-microcvd-accel-proposal.md|Q2@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/dynamic-microcvd-accel-proposal.md|Q3@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/settings-exposure-pass-proposal.md|Q2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/analysis-log-csv-expansion-proposal.md|Q1@b5000c9 | no_richer_option | no_richer_option | 0.8 | False | AGREE |
| docs/analysis-log-csv-expansion-proposal.md|Q2@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/analysis-log-csv-expansion-proposal.md|Q3@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/analysis-log-csv-expansion-proposal.md|Q6@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/v17-followup-fixes-proposal.md|Q6@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/v19-calibration-tuning-pass-proposal.md|Q3@b5000c9 | gives_up_for_economy | richer_option_wrong | 1 | True | DISAGREE |
| docs/v20-rsi-roc-algorithm-fixes-proposal.md|Q9@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/api-resilience-pass-proposal.md|Q1@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/api-resilience-pass-proposal.md|Q2@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/on-close-analysis-mode-proposal.md|S9.1@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/realtime-exit-guard-proposal.md|S9.1@b5000c9 | no_richer_option | gives_up_for_economy | 1 | True | DISAGREE |
| docs/realtime-exit-guard-proposal.md|S9.3@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/realtime-exit-guard-proposal.md|S9.5@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/realtime-exit-guard-proposal.md|S9.6@b5000c9 | ambiguous | ambiguous | 1 | True | AGREE |
| docs/live-microstructure-strip-proposal.md|S9.2@b5000c9 | no_richer_option | ambiguous | 1 | True | DISAGREE |
| docs/live-microstructure-strip-proposal.md|S9.4@b5000c9 | ambiguous | gives_up_for_economy | 1 | True | DISAGREE |
| docs/live-microstructure-strip-proposal.md|S9.5@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/auto-tweaker-session-resolution-filter-proposal.md|S5.2@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/offline-analysis-report-audit-proposal.md|D1@b5000c9 | no_richer_option | no_richer_option | 0.6 | False | AGREE |
| docs/offline-analysis-report-audit-proposal.md|D3@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/time-averaged-ofi-proposal.md|S10.1@b5000c9 | no_richer_option | gives_up_for_economy | 0.6 | False | DISAGREE |
| docs/time-averaged-ofi-proposal.md|S10.2@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/time-averaged-ofi-proposal.md|S10.3@b5000c9 | no_richer_option | ambiguous | 1 | True | DISAGREE |
| docs/time-averaged-ofi-proposal.md|S10.4@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/time-averaged-ofi-proposal.md|S10.5@b5000c9 | no_richer_option | ambiguous | 0.8 | False | DISAGREE |
| docs/dev-workflow-automation-proposal.md|B@b5000c9 | gives_up_for_economy | ambiguous | 1 | True | DISAGREE |
| docs/dev-workflow-automation-proposal.md|C@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/dev-workflow-automation-proposal.md|D@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/dev-workflow-automation-proposal.md|E@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/dev-workflow-automation-proposal.md|F@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/audit-fixes-2026-07-02-proposal.md|D1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/audit-fixes-2026-07-02-proposal.md|D2@b5000c9 | no_richer_option | gives_up_for_economy | 1 | True | DISAGREE |
| docs/audit-fixes-2026-07-02-proposal.md|D3@b5000c9 | ambiguous | ambiguous | 1 | True | AGREE |
| docs/signal-health-retune-proposal.md|D2@b5000c9 | unsure | no_richer_option | 1 | True | SEAT_UNSURE |
| docs/placed-geometry-structural-first-proposal.md|D3@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/placed-geometry-derivation-2026-07-06.md|DG2@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/placed-geometry-derivation-2026-07-06.md|DG3@b5000c9 | no_richer_option | ambiguous | 1 | True | DISAGREE |
| docs/funding-momentum-time-anchored-window-proposal.md|D4@b5000c9 | no_richer_option | richer_option_wrong | 0.8 | False | DISAGREE |
| docs/d6-eval-placed-stop-migration-proposal.md|D1@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/aggressor-velocity-s52-derivation-2026-07-13.md|S1@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/aggressor-velocity-s52-derivation-2026-07-13.md|S2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/session-policy-gate-proposal.md|P1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/eval-no-data-outcome-proposal.md|N4@b5000c9 | ambiguous | no_richer_option | 1 | True | DISAGREE |
| docs/geometry-arbitration-modes-proposal.md|G2@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/liq-cascade-level-alerts-proposal.md|H1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/absorption-geometry-rescale-proposal.md|V2@b5000c9 | richer_option_wrong | ambiguous | 0.8 | False | DISAGREE |
| docs/aggr-vel-s52-london-derivation-2026-07-23.md|S1@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/in-app-trade-store-capture-proposal.md|D1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/settings-local-overlay-proposal.md|D1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/settings-local-overlay-proposal.md|D2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/settings-local-overlay-proposal.md|D3@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/settings-local-overlay-proposal.md|D4@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/settings-local-overlay-proposal.md|D5@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/settings-local-overlay-proposal.md|D6@b5000c9 | no_richer_option | gives_up_for_economy | 1 | True | DISAGREE |
| docs/trade-store-coverage-report-proposal.md|D1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-coverage-report-proposal.md|D2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-coverage-report-proposal.md|D3@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-coverage-report-proposal.md|D7@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-coverage-report-proposal.md|D6@b5000c9 | ambiguous | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-coverage-report-proposal.md|D4@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/asia-burst-threshold-derivation-2026-08-01.md|D3-1@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/ttm-flat-threshold-rederivation-2026-08-02.md|D1-a@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/ttm-flat-threshold-rederivation-2026-08-02.md|D1-d@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/trade-store-trade-identity-proposal.md|D1@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/trade-store-trade-identity-proposal.md|D5@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/trade-store-trade-identity-proposal.md|D6@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-write-guard-identity-proposal.md|D-1@b5000c9 | ambiguous | ambiguous | 1 | True | AGREE |
| docs/trade-store-write-guard-identity-proposal.md|D-3@b5000c9 | gives_up_for_economy | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-write-guard-identity-proposal.md|D-4@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-write-guard-identity-proposal.md|D-5@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/trade-store-write-guard-identity-proposal.md|D-2@b5000c9 | richer_option_wrong | gives_up_for_economy | 1 | True | DISAGREE |
| docs/trade-store-write-guard-identity-proposal.md|D-6@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-write-guard-identity-proposal.md|D-7@b5000c9 | no_richer_option | richer_option_wrong | 0.6 | False | DISAGREE |
| docs/absorption-d6-spec-back.md|D-6a@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/absorption-d6-spec-back.md|D-6b@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/d6d-episode-continuity-spec.md|D-6d.1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/d6d-episode-continuity-spec.md|D-6d.2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/d6d-episode-continuity-spec.md|D-6d.3@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/thin-trade-window-skip-gate-proposal.md|D-1@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/collector-ops-tooling-proposal.md|D-7@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/collector-ops-tooling-proposal.md|D-9@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-3@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-5@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-6@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-4@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-5.1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-5.2@b5000c9 | ambiguous | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-5.3@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-5.4@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-5.5@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-6r@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-trailing-edge-f1-proposal.md|D-3r@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/trader-tick-queue-archive.md|A54a-scope@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/trader-tick-queue-archive.md|seeded-session-buckets@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/a54a-json-poco-drift-guard-spec.md|D-1@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/a54a-json-poco-drift-guard-spec.md|D-4@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/a54a-json-poco-drift-guard-spec.md|D-5@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/a54a-r2-r3-followup-spec.md|D-R3@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/a54a-session2-step1-measurement-2026-09-05.md|S2-1@b5000c9 | gives_up_for_economy | richer_option_wrong | 1 | True | DISAGREE |
| docs/s2-2-calcspread-split-proposal.md|D-1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/s2-2-calcspread-split-proposal.md|D-2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/s2-2-calcspread-split-proposal.md|D-3@b5000c9 | gives_up_for_economy | richer_option_wrong | 1 | True | DISAGREE |
| docs/s2-2-calcspread-split-proposal.md|D-4@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/s2-2-calcspread-split-proposal.md|D-5@b5000c9 | no_richer_option | gives_up_for_economy | 1 | True | DISAGREE |
| docs/trader-tick-queue-archive.md|pooled-minute-key-dedup@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trader-tick-queue-archive.md|E5-absorption-path@b5000c9 | no_richer_option | richer_option_wrong | 0.8 | False | DISAGREE |
| docs/trader-tick-queue-archive.md|WD-SEMANTICS@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/s4-eval-cache-identity-proposal.md|D-1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/s4-eval-cache-identity-proposal.md|D-2@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/s4-eval-cache-identity-proposal.md|D-3@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/item6-wstradeprobe-s1-recheck-2026-09-07.md|S-1@b5000c9 | ambiguous | richer_option_wrong | 1 | True | DISAGREE |
| docs/absorption-d2-stage1-rotation-build-spec.md|RD-1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/kelly-w6-4-spec-back.md|D-1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/kelly-w6-4-spec-back.md|D-3@b5000c9 | no_richer_option | ambiguous | 1 | True | DISAGREE |
| docs/doc-status-sweep-and-queue-archive-spec.md|D-1@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/coverage-report-cluster-spec.md|D-4@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/coverage-report-cluster-spec.md|D-6@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/venue-status-instrument-spec.md|D-1@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/gap-repair-same-ms-page-skip-spec.md|GR-2@b5000c9 | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| docs/gap-repair-same-ms-page-skip-spec.md|GR-4@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/gap-repair-same-ms-page-skip-spec.md|GR-5@b5000c9 | gives_up_for_economy | richer_option_wrong | 1 | True | DISAGREE |
| docs/gap-repair-same-ms-page-skip-spec.md|GR-1@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/trade-store-duplicate-rows-read-2026-09-15.md|DUP-1@b5000c9 | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| docs/trade-store-duplicate-rows-read-2026-09-15.md|DUP-2@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/venue-check-build-spec-back.md|R-2@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/venue-check-build-spec-back.md|R-3@b5000c9 | no_richer_option | no_richer_option | 1 | True | AGREE |
| docs/venue-check-schedule-plan.md|D-2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/medium-tier-bug-hunt-spec-back.md|D-2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/medium-tier-bug-hunt-spec-back.md|D-5@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/medium-tier-bug-hunt-spec-back.md|D-8@b5000c9 | gives_up_for_economy | richer_option_wrong | 1 | True | DISAGREE |
| docs/medium-tier-bug-hunt-spec-back.md|D-9@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/medium-tier-diagnosis-spec-back.md|Q-1@b5000c9 | no_richer_option | gives_up_for_economy | 1 | True | DISAGREE |
| docs/medium-tier-diagnosis-spec-back.md|Q-2@b5000c9 | no_richer_option | no_richer_option | 0.6 | False | AGREE |
| docs/engine-fix-build-spec-2026-09-21.md|EF-1@b5000c9 | gives_up_for_economy | richer_option_wrong | 1 | True | DISAGREE |
| docs/engine-fix-build-spec-2026-09-21.md|EF-2@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/engine-fix-build-spec-2026-09-21.md|EF-3@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |
| docs/engine-fix-build-spec-2026-09-21.md|EF-4@b5000c9 | no_richer_option | richer_option_wrong | 1 | True | DISAGREE |

- AGREE/unstable 3, AGREE/stable 54, DISAGREE/unstable 6, DISAGREE/stable 86, SEAT_UNSURE/stable 1
