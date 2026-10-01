#!/usr/bin/env bash
# tools/ops/history-backfill/history-backfill-cloud.sh
#
# Runs ON the TEMPORARY cloud backfill instance (Amazon Linux 2023), never on the collector box
# and never on the dev machine. docs/history-store-backfill-runbook.md is the procedure;
# docs/history-data-store-spec.md (ruling HDS-2) is why.
#
# Verbs:
#   start   (root) run `loop` as a transient systemd unit, so it survives the SSM command ending.
#   loop    the unattended backfill: CHUNK_DAYS days per pass, newest day first, resumed from the
#           checkpoint after any stop; after every pass, every COMPLETE month is gzipped and
#           uploaded to S3; at the end, the deep status check and a DONE marker are uploaded.
#   upload  gzip + sha256 + upload every COMPLETE month not yet uploaded (idempotent).
#   final   deep status check, upload everything, upload DONE (or INCOMPLETE).
#   status  progress: unit state, last log lines, store status, uploaded months, disk.
#
# Environment (defaults in brackets):
#   STORE [/data/history]  FROM [2025-01-01]  TO [2026-10-01, EXCLUSIVE]  CHUNK_DAYS [31]
#   BUCKET [deribit-engine-bucket]  REGION [eu-west-2]  PREFIX [history-backfill/store]
#   RUNNER [/opt/history/runner/BacktestRunner]  (a command; may be "dotnet /path/BacktestRunner.dll")
#   AWS [aws]  (set AWS="echo aws" to dry-run the uploads)
#
# The tool's exit codes (tools/BacktestRunner/HistoryCli.vb): 0 done · 1 bad args / store
# problem · 2 stopped on a host failure the retries did not absorb (resumable) · 4 some days
# GAP or FAILED.

set -u

HOME_DIR=/opt/history
STORE=${STORE:-/data/history}
FROM=${FROM:-2025-01-01}
TO=${TO:-2026-10-01}
CHUNK_DAYS=${CHUNK_DAYS:-31}
BUCKET=${BUCKET:-deribit-engine-bucket}
REGION=${REGION:-eu-west-2}
PREFIX=${PREFIX:-history-backfill/store}
RUNNER=${RUNNER:-$HOME_DIR/runner/BacktestRunner}
AWS=${AWS:-aws}
MAX_TRANSPORT_STOPS=${MAX_TRANSPORT_STOPS:-24}
MAX_INCOMPLETE_PASSES=${MAX_INCOMPLETE_PASSES:-3}
UNIT=deribit-history-backfill
UPLOAD_DIR="$STORE/upload"
LOOP_LOG="$STORE/loop.log"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

say() { echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) $*" | tee -a "$LOOP_LOG"; }

# RUNNER is deliberately unquoted: it may be a command plus an argument (dotnet + dll).
tool() { $RUNNER "$@"; }

s3put() {  # local-file key
    $AWS s3 cp "$1" "s3://$BUCKET/$PREFIX/$2" --region "$REGION" --only-show-errors
}

upload() {
    mkdir -p "$UPLOAD_DIR"
    tool history status --store "$STORE" > "$UPLOAD_DIR/status_latest.txt" 2>&1
    local m f sha gz gzsha rows marker
    for m in $(grep -E '^MONTH [0-9]{4}-[0-9]{2} .* COMPLETE$' "$UPLOAD_DIR/status_latest.txt" | awk '{print $2}'); do
        f="$STORE/trades_$m.csv"
        [ -f "$f" ] || { say "upload: $f missing although $m is COMPLETE"; continue; }
        sha=$(sha256sum "$f" | cut -d' ' -f1)
        marker="$UPLOAD_DIR/trades_$m.uploaded"
        if [ -f "$marker" ] && [ "$(cut -d, -f2 "$marker")" = "$sha" ]; then continue; fi
        gz="$UPLOAD_DIR/trades_$m.csv.gz"
        gzip -c "$f" > "$gz.tmp" && mv "$gz.tmp" "$gz" || { say "upload: gzip failed for $m"; continue; }
        gzsha=$(sha256sum "$gz" | cut -d' ' -f1)
        rows=$(( $(wc -l < "$f") - 1 ))
        if s3put "$gz" "trades_$m.csv.gz"; then
            echo "$m,$sha,$gzsha,$rows" > "$marker"
            rm -f "$gz"
            say "upload: $m rows=$rows csv_sha256=$sha"
        else
            say "upload: S3 copy FAILED for $m (kept $gz; the next pass retries)"
        fi
    done
    # The manifest lists every uploaded month; the pull script verifies each file against it.
    { echo "Month,CsvSha256,GzSha256,Rows"; cat "$UPLOAD_DIR"/trades_*.uploaded 2>/dev/null | sort; } > "$UPLOAD_DIR/manifest.csv"
    s3put "$UPLOAD_DIR/manifest.csv" manifest.csv
    [ -f "$STORE/history_checkpoint.csv" ] && s3put "$STORE/history_checkpoint.csv" history_checkpoint.csv
    [ -f "$STORE/history_backfill.log" ] && s3put "$STORE/history_backfill.log" history_backfill.log
    s3put "$UPLOAD_DIR/status_latest.txt" status_latest.txt
    return 0
}

final() {
    mkdir -p "$UPLOAD_DIR"
    tool history status --store "$STORE" --deep > "$UPLOAD_DIR/status_final.txt" 2>&1
    local rc=$?
    say "final: deep status exit $rc (0 = no problem)"
    upload
    s3put "$UPLOAD_DIR/status_final.txt" status_final.txt
    s3put "$LOOP_LOG" loop.log
    if [ "$rc" -eq 0 ]; then
        date -u +%Y-%m-%dT%H:%M:%SZ > "$UPLOAD_DIR/DONE"
        s3put "$UPLOAD_DIR/DONE" DONE
        say "final: DONE uploaded"
    else
        date -u +%Y-%m-%dT%H:%M:%SZ > "$UPLOAD_DIR/INCOMPLETE"
        s3put "$UPLOAD_DIR/INCOMPLETE" INCOMPLETE
        say "final: INCOMPLETE uploaded -- read status_final.txt"
    fi
    return $rc
}

loop() {
    mkdir -p "$STORE"
    say "loop: start range $FROM .. $TO (exclusive) chunk=$CHUNK_DAYS store=$STORE"
    local stops=0 incomplete=0 rc out remaining
    while true; do
        out=$(tool history backfill --from "$FROM" --to "$TO" --store "$STORE" --max-days "$CHUNK_DAYS" 2>&1)
        rc=$?
        echo "$out" >> "$LOOP_LOG"
        echo "$out" | grep -E '^HISTORY_RUN' | tail -1 | sed "s/^/$(date -u +%H:%M:%SZ) /"
        remaining=$(echo "$out" | grep -E '^HISTORY_RUN' | tail -1 | sed -n 's/.* remaining=\([0-9]*\).*/\1/p')
        remaining=${remaining:-0}
        upload
        case "$rc" in
            0)
                if [ "$remaining" -eq 0 ]; then say "loop: every planned day OK"; final; return $?; fi
                ;;
            2)
                stops=$((stops + 1))
                say "loop: stopped on a host failure ($stops/$MAX_TRANSPORT_STOPS); resuming in 5 min"
                if [ "$stops" -ge "$MAX_TRANSPORT_STOPS" ]; then say "loop: too many host failures; giving up"; final; return 2; fi
                sleep 300
                ;;
            4)
                if [ "$remaining" -eq 0 ]; then
                    incomplete=$((incomplete + 1))
                    say "loop: whole range walked with GAP/FAILED days (pass $incomplete/$MAX_INCOMPLETE_PASSES)"
                    if [ "$incomplete" -ge "$MAX_INCOMPLETE_PASSES" ]; then final; return 4; fi
                    sleep 600
                fi
                ;;
            *)
                say "loop: tool exit $rc (store or argument problem) -- stopping; read $LOOP_LOG"
                final
                return "$rc"
                ;;
        esac
    done
}

start() {
    if [ "$(id -u)" -ne 0 ]; then echo "start needs root (SSM runs commands as root)"; return 1; fi
    if systemctl is-active --quiet "$UNIT"; then
        echo "already running: $(systemctl show -p ActiveEnterTimestamp "$UNIT")"
        return 0
    fi
    systemctl reset-failed "$UNIT" 2>/dev/null
    mkdir -p "$STORE"
    chmod +x "$HOME_DIR/runner/BacktestRunner" 2>/dev/null
    systemd-run --unit "$UNIT" --description "Deribit history store backfill (temporary)" \
        --setenv=STORE="$STORE" --setenv=FROM="$FROM" --setenv=TO="$TO" --setenv=CHUNK_DAYS="$CHUNK_DAYS" \
        --setenv=BUCKET="$BUCKET" --setenv=REGION="$REGION" --setenv=PREFIX="$PREFIX" \
        --setenv=RUNNER="$RUNNER" --setenv=HOME=/root \
        /bin/bash "$HOME_DIR/history-backfill-cloud.sh" loop
    sleep 5
    systemctl status "$UNIT" --no-pager | head -5
}

status() {
    echo "== unit"; systemctl status "$UNIT" --no-pager 2>&1 | head -4
    echo "== last loop lines"; tail -n 6 "$LOOP_LOG" 2>/dev/null
    echo "== last days"; grep -E ' DAY ' "$STORE/history_backfill.log" 2>/dev/null | tail -n 3
    echo "== store"; tool history status --store "$STORE" 2>&1 | grep -E '^\[status\]|^HISTORY_STATUS|^PROBLEM' | head -8
    echo "== uploaded months"; ls "$UPLOAD_DIR"/trades_*.uploaded 2>/dev/null | wc -l
    [ -f "$UPLOAD_DIR/DONE" ] && echo "== DONE $(cat "$UPLOAD_DIR/DONE")"
    [ -f "$UPLOAD_DIR/INCOMPLETE" ] && echo "== INCOMPLETE $(cat "$UPLOAD_DIR/INCOMPLETE")"
    echo "== disk"; df -h "$STORE" | tail -1
}

case "${1:-}" in
    start) start ;;
    loop) loop ;;
    upload) upload ;;
    final) final ;;
    status) status ;;
    *) echo "usage: $0 start|loop|upload|final|status"; exit 1 ;;
esac
