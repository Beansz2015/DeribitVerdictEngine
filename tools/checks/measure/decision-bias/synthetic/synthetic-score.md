# Decision-bias tripwire — score

- population rev `b5000c9`, items scored: 4 (provenance filter: all; excluded granularity: none; CLAUDE.md-named four excluded: no; revealed sources excluded: no)
- outcomes: ADOPTED 2, OVERRULED 1, PARTIAL 1

### Regex arm — pattern `adequate|good enough|buys nothing|defer` on the rationale, pre-registered

| | flagged | not flagged | n |
|---|---:|---:|---:|
| OVERRULED | 1 | 0 | 1 |
| ADOPTED | 0 | 2 | 2 |
| PARTIAL (reported apart) | 0 | 1 | 1 |

- Catches among OVERRULED: **1 of 1** (Wilson 95% 0.21–1.00)
- False flags among ADOPTED: **0 of 2** (Wilson 95% 0.00–0.66)
- ⚠ PILOT, NOT A RATE: the deciding arm (OVERRULED) has n = 1.

### Seat arm — flag = `gives_up_for_economy`; 1 'unsure' rows excluded

| | flagged | not flagged | n |
|---|---:|---:|---:|
| OVERRULED | 1 | 0 | 1 |
| ADOPTED | 0 | 2 | 2 |
| PARTIAL (reported apart) | 0 | 0 | 0 |

- Catches among OVERRULED: **1 of 1** (Wilson 95% 0.21–1.00)
- False flags among ADOPTED: **0 of 2** (Wilson 95% 0.00–0.66)
- ⚠ PILOT, NOT A RATE: the deciding arm (OVERRULED) has n = 1.

### Jev arm — flag = modal verdict `gives_up_for_economy`, all judged rows

| | flagged | not flagged | n |
|---|---:|---:|---:|
| OVERRULED | 1 | 0 | 1 |
| ADOPTED | 0 | 2 | 2 |
| PARTIAL (reported apart) | 0 | 1 | 1 |

- Catches among OVERRULED: **1 of 1** (Wilson 95% 0.21–1.00)
- False flags among ADOPTED: **0 of 2** (Wilson 95% 0.00–0.66)
- ⚠ PILOT, NOT A RATE: the deciding arm (OVERRULED) has n = 1.

### Jev arm, STABLE rows only — agreement rate 1.0

| | flagged | not flagged | n |
|---|---:|---:|---:|
| OVERRULED | 1 | 0 | 1 |
| ADOPTED | 0 | 2 | 2 |
| PARTIAL (reported apart) | 0 | 1 | 1 |

- Catches among OVERRULED: **1 of 1** (Wilson 95% 0.21–1.00)
- False flags among ADOPTED: **0 of 2** (Wilson 95% 0.00–0.66)
- ⚠ PILOT, NOT A RATE: the deciding arm (OVERRULED) has n = 1.

### Jev against seat, per item

| id | seat | Jev modal | agreement | stable | match |
|---|---|---|---:|---|---|
| synthetic/S1-unparsed-counter | gives_up_for_economy | gives_up_for_economy | 1 | True | AGREE |
| synthetic/S2-mixed-sequence-span | richer_option_wrong | richer_option_wrong | 1 | True | AGREE |
| synthetic/S3-class-name | no_richer_option | no_richer_option | 1 | True | AGREE |
| synthetic/S4-vague | unsure | ambiguous | 1 | True | SEAT_UNSURE |

- AGREE/stable 3, SEAT_UNSURE/stable 1
