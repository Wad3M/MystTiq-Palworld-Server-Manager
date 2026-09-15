#!/usr/bin/env bash
set -uo pipefail
VERSION="0.7.17.0"
APP="${MYSTTIQ_APP:-$(pwd)/mysttiq-server}"
CONFIG="${MYSTTIQ_CONFIG:-/etc/mysttiq/mysttiq.json}"
SERVICE="mysttiq-palworld"
STAMP="$(date -u +%Y%m%d-%H%M%S)"
REPORT_ROOT="${HOME}/mysttiq-test-results/v${VERSION}/production-${STAMP}"
mkdir -p "$REPORT_ROOT"
LOG="$REPORT_ROOT/production-readiness.log"
exec > >(tee -a "$LOG") 2>&1

PASS=0; FAIL=0; WARN=0
record(){
  local state="$1" name="$2" detail="${3:-}"
  printf '[%s] %s%s\n' "$state" "$name" "${detail:+ :: $detail}"
  case "$state" in
    PASS) PASS=$((PASS + 1)) ;;
    FAIL) FAIL=$((FAIL + 1)) ;;
    WARN) WARN=$((WARN + 1)) ;;
  esac
  return 0
}
command -v jq >/dev/null || { echo "[FAIL] jq is required"; exit 1; }

echo "============================================================"
echo "MystTiq v${VERSION} Production Readiness"
echo "Report: $REPORT_ROOT"
echo "============================================================"

# v0.6.1.0's own memory note already documented this: /opt/mysttiq/runtime is root-owned by the
# live systemd unit (User=root), so any CLI invocation run as this script's own user that tries to
# persist lifecycle state under the live config's RuntimeRoot crashes with an unhandled
# UnauthorizedAccessException. Give these ephemeral invocations an isolated, self-owned runtime
# root instead of the live one.
scratch_runtime="$REPORT_ROOT/scratch-runtime"
mkdir -p "$scratch_runtime"

if [[ -x "$APP" ]]; then
  record PASS "MystTiq executable" "$APP"
else
  record FAIL "MystTiq executable" "$APP"
fi
if "$APP" production-doctor --config "$CONFIG" --runtime-root "$scratch_runtime" --json >"$REPORT_ROOT/doctor.json" 2>"$REPORT_ROOT/doctor.err"; then
  state="$(jq -r '.status' "$REPORT_ROOT/doctor.json")"
  [[ "$state" == PASS ]] && record PASS "Production Doctor" || record WARN "Production Doctor" "$state"
else
  state="$(jq -r '.status // "FAIL"' "$REPORT_ROOT/doctor.json" 2>/dev/null || echo FAIL)"
  record FAIL "Production Doctor" "$state; see doctor.json/doctor.err"
fi

systemctl is-enabled "$SERVICE" >"$REPORT_ROOT/systemd-enabled.txt" 2>&1 && record PASS "systemd enabled" || record FAIL "systemd enabled"
systemctl is-active "$SERVICE" >"$REPORT_ROOT/systemd-active.txt" 2>&1 && record PASS "systemd active" || record FAIL "systemd active"
# Recent window, not the whole current boot: on a long-uptime host a boot-scoped fatal-event scan
# never recovers from one old crash-loop, however long ago and however unrelated to the state being
# gated right now -- a production-readiness check is meant to answer "is anything wrong right now,"
# not "has anything ever gone wrong since the last reboot."
journalctl -u "$SERVICE" --since "-30 minutes" --no-pager >"$REPORT_ROOT/journal-recent.txt" 2>&1 || true
if grep -Eqi 'Unhandled exception|fatal|segmentation fault|core dumped' "$REPORT_ROOT/journal-recent.txt"; then
  record FAIL "Recent fatal journal events" "review journal-recent.txt"
else
  record PASS "Recent fatal journal events" "none in the last 30 minutes"
fi

"$APP" status --config "$CONFIG" --runtime-root "$scratch_runtime" --json >"$REPORT_ROOT/status.json" 2>"$REPORT_ROOT/status.err" || true
ready="$(jq -r '.ready // .Ready // false' "$REPORT_ROOT/status.json" 2>/dev/null || echo false)"
[[ "$ready" == true ]] && record PASS "PalServer ready" || record WARN "PalServer ready" "server is currently stopped/not ready"

df -Pk /opt/mysttiq >"$REPORT_ROOT/disk.txt" 2>&1 || true
avail="$(awk 'NR==2 {print $4}' "$REPORT_ROOT/disk.txt")"
if [[ "$avail" =~ ^[0-9]+$ ]]; then
  if (( avail >= 5242880 )); then
    record PASS "Disk reserve" ">=5 GiB"
  elif (( avail >= 2097152 )); then
    record WARN "Disk reserve" "<5 GiB"
  else
    record FAIL "Disk reserve" "<2 GiB"
  fi
else
  record WARN "Disk reserve" "unable to determine"
fi

cat >"$REPORT_ROOT/summary.json" <<EOF
{"version":"$VERSION","passed":$PASS,"warnings":$WARN,"failed":$FAIL,"report":"$REPORT_ROOT"}
EOF
echo "================ PRODUCTION READINESS SUMMARY ================"
echo "Passed : $PASS"
echo "Warned : $WARN"
echo "Failed : $FAIL"
echo "Report : $REPORT_ROOT"
(( FAIL == 0 ))
