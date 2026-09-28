#!/usr/bin/env bash
# Runs the shipped `BacktestRunner coverage --strict` CLI (built from 6e74181) over each
# generated scenario and prints the hour table, verdict line and exit code.
set -u
WT=${WT:-/tmp/audit-6e74181}
FIX=${FIX:-/tmp/claude-0/proof/fixtures}
DLL=$WT/tools/BacktestRunner/bin/Release/net8.0/BacktestRunner.dll
python3 "$(dirname "$0")/gen_coverage_fixtures.py" "$FIX"
for d in "$FIX"/S-*; do
  n=$(basename "$d")
  echo "================ $n"
  out=$(cd "$WT" && dotnet "$DLL" coverage --from 2026-09-16 --to 2026-09-16T13:00:00Z \
          --evidence-dir "$d" --strict --out "$d/report.md" 2>&1)
  rc=$?
  echo "$out" | grep -E "captured hours|DEFECT|trailing-edge  |expected-missing|out-of-scope-venue|S1 \(uptime\)|seq gaps|VERDICT"
  echo "--- non-captured hour table (markdown):"
  sed -n '/^| Hour/,$p' "$d/report.md" | grep -v '^```' | grep -v VENUE_CHECK
  echo "EXIT CODE (--strict): $rc"
done
