#!/usr/bin/env bash
# tools/ops/rawbook/rawbook-cloud.sh
#
# Runs ON the TEMPORARY cloud instance (Amazon Linux 2023) for the raw order-book absorption test
# (docs/raw-book-absorption-test-spec.md). Never on the collector box. The probe is read-only
# against the venue (one WebSocket, the read-only key, no orders) and writes only under $OUT.
#
# Verbs:
#   start   (root) run `loop` as a transient systemd unit, so it survives the SSM command ending.
#   loop    run the probe for $SECONDS_RUN; every UPLOAD_MIN minutes, and once at the end,
#           gzip the output folder and upload it to S3.
#   upload  gzip + upload the output folder now (idempotent; overwrites the same keys).
#   stop    touch the probe's STOP file (a clean end: final summary, final upload).
#   status  unit state, the last status line, memory, output size.
#
# Environment (defaults in brackets):
#   OUT [/data/rawbook/run1]  SECONDS_RUN [86400]  ENV_FILE [/opt/rawbook/ro.env]
#   HEAP_HARD_LIMIT [0x10000000 = 256 MB GC heap]  HEAP_STOP_MB [160]  MEMORY_MAX [400M]
#   BUCKET [deribit-engine-bucket]  REGION [eu-west-2]  PREFIX [rawbook-test/run1]
#   UPLOAD_MIN [30]  PROBE [/opt/rawbook/probe/RawBookProbe]
#
# ⛔ The read-only key lives ONLY in $ENV_FILE (mode 600, placed by the operator). It is never on a
# command line, in this script, in an SSM parameter, in the repo or in an upload: `upload` packs
# $OUT only, and the probe never writes the key there.

set -u

HOME_DIR=/opt/rawbook
OUT=${OUT:-/data/rawbook/run1}
SECONDS_RUN=${SECONDS_RUN:-86400}
ENV_FILE=${ENV_FILE:-$HOME_DIR/ro.env}
HEAP_HARD_LIMIT=${HEAP_HARD_LIMIT:-0x10000000}
HEAP_STOP_MB=${HEAP_STOP_MB:-160}
MEMORY_MAX=${MEMORY_MAX:-400M}
BUCKET=${BUCKET:-deribit-engine-bucket}
REGION=${REGION:-eu-west-2}
PREFIX=${PREFIX:-rawbook-test/run1}
UPLOAD_MIN=${UPLOAD_MIN:-30}
PROBE=${PROBE:-$HOME_DIR/probe/RawBookProbe}
UNIT=deribit-rawbook-probe
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

say() { echo "$(date -u +%Y-%m-%dT%H:%M:%SZ) $*" | tee -a "$OUT/loop.log"; }

upload() {
    mkdir -p "$OUT"
    local tgz=/tmp/rawbook-out.tgz
    # Pack the output folder only. The env file lives outside it, in $HOME_DIR.
    tar -czf "$tgz.tmp" -C "$OUT" . && mv "$tgz.tmp" "$tgz" || { say "upload: tar failed"; return 1; }
    if aws s3 cp "$tgz" "s3://$BUCKET/$PREFIX/rawbook-out.tgz" --region "$REGION" --only-show-errors; then
        sha256sum "$tgz" | cut -d' ' -f1 > "$OUT/last_upload.sha256"
        say "upload: ok $(stat -c %s "$tgz") bytes sha256=$(cat "$OUT/last_upload.sha256")"
    else
        say "upload: S3 copy FAILED (the next upload retries)"
    fi
    [ -f "$OUT/status_latest.txt" ] && aws s3 cp "$OUT/status_latest.txt" "s3://$BUCKET/$PREFIX/status_latest.txt" --region "$REGION" --only-show-errors
    return 0
}

loop() {
    mkdir -p "$OUT"
    say "loop: start seconds=$SECONDS_RUN out=$OUT heap_hard_limit=$HEAP_HARD_LIMIT heap_stop_mb=$HEAP_STOP_MB"
    DOTNET_GCHeapHardLimit=$HEAP_HARD_LIMIT "$PROBE" --seconds "$SECONDS_RUN" --out "$OUT" --env-file "$ENV_FILE" \
        --heap-stop-mb "$HEAP_STOP_MB" >> "$OUT/console.log" 2>&1 &
    local pid=$!
    echo "$pid" > "$OUT/probe.pid"
    say "loop: probe pid $pid"
    local waited=0
    while kill -0 "$pid" 2>/dev/null; do
        sleep 60
        waited=$((waited + 1))
        if [ $((waited % UPLOAD_MIN)) -eq 0 ]; then upload; fi
    done
    wait "$pid"
    local rc=$?
    say "loop: probe exited $rc (0 ok, 1 args/key, 3 auth, 4 channel refused, 5 heap stop, 6 output cap)"
    upload
    date -u +%Y-%m-%dT%H:%M:%SZ > "$OUT/DONE"
    aws s3 cp "$OUT/DONE" "s3://$BUCKET/$PREFIX/DONE" --region "$REGION" --only-show-errors
    return $rc
}

start() {
    if [ "$(id -u)" -ne 0 ]; then echo "start needs root (SSM runs commands as root)"; return 1; fi
    if systemctl is-active --quiet "$UNIT"; then
        echo "already running: $(systemctl show -p ActiveEnterTimestamp "$UNIT")"
        return 0
    fi
    [ -f "$ENV_FILE" ] || { echo "STOP: $ENV_FILE is missing. Place it (mode 600) before start."; return 1; }
    chmod 600 "$ENV_FILE"
    test -x "$PROBE" || { echo "STOP: $PROBE is missing or not executable. Run ssm-rawbook-install.json first."; return 1; }
    systemctl reset-failed "$UNIT" 2>/dev/null
    mkdir -p "$OUT"
    systemd-run --unit "$UNIT" --description "Deribit raw book absorption test (temporary)" \
        -p MemoryMax="$MEMORY_MAX" -p Nice=5 \
        --setenv=OUT="$OUT" --setenv=SECONDS_RUN="$SECONDS_RUN" --setenv=ENV_FILE="$ENV_FILE" \
        --setenv=HEAP_HARD_LIMIT="$HEAP_HARD_LIMIT" --setenv=HEAP_STOP_MB="$HEAP_STOP_MB" \
        --setenv=BUCKET="$BUCKET" --setenv=REGION="$REGION" --setenv=PREFIX="$PREFIX" \
        --setenv=UPLOAD_MIN="$UPLOAD_MIN" --setenv=PROBE="$PROBE" --setenv=HOME=/root \
        /bin/bash "$HOME_DIR/rawbook-cloud.sh" loop
    sleep 20
    systemctl status "$UNIT" --no-pager | head -5
    echo "== console (first lines)"; head -n 20 "$OUT/console.log" 2>/dev/null
}

stop() {
    mkdir -p "$OUT"
    touch "$OUT/STOP"
    echo "STOP file written: $OUT/STOP (the probe ends within ~1 s; the loop uploads once more)"
}

status() {
    echo "== unit"; systemctl status "$UNIT" --no-pager 2>&1 | head -6
    echo "== last status line"; cat "$OUT/status_latest.txt" 2>/dev/null
    echo "== last loop lines"; tail -n 4 "$OUT/loop.log" 2>/dev/null
    echo "== errors in console"; grep -E "STOP|FATAL|dropped|error" "$OUT/console.log" 2>/dev/null | tail -n 5
    echo "== output"; du -sh "$OUT" 2>/dev/null
    echo "== memory"; free -m | head -2
    [ -f "$OUT/DONE" ] && echo "== DONE $(cat "$OUT/DONE")"
    return 0
}

case "${1:-}" in
    start) start ;;
    loop) loop ;;
    upload) upload ;;
    stop) stop ;;
    status) status ;;
    *) echo "usage: $0 start|loop|upload|stop|status"; exit 1 ;;
esac
