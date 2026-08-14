#!/usr/bin/env bash
set -uo pipefail

VERSION="0.3.0.7"
if [[ -x "$(pwd)/mysttiq-server" ]]; then DEFAULT_APP="$(pwd)/mysttiq-server"; else DEFAULT_APP="/opt/mysttiq/bin/mysttiq-server"; fi
APP="${MYSTTIQ_APP:-$DEFAULT_APP}"
CONFIG="${MYSTTIQ_CONFIG:-/etc/mysttiq/mysttiq.json}"
API_PORT="${MYSTTIQ_API_PORT:-8213}"
SERVICE="mysttiq-palworld"
EXTENDED=0
INSTALL_CURRENT=0

usage() {
  cat <<EOF
MystTiq v${VERSION} Linux acceptance runner

Usage:
  bash scripts/Test-v${VERSION}-LinuxAcceptance.sh [--install-current] [--extended] [--app PATH] [--config PATH]

Default mode is non-destructive and tests the extracted version's mysttiq-server when present.
--install-current performs service-install --start-now and may prompt once for sudo.
--extended exercises API lifecycle mutation on the disposable test VM.
EOF
}

while (($#)); do
  case "$1" in
    --extended) EXTENDED=1; shift ;;
    --install-current) INSTALL_CURRENT=1; shift ;;
    --app) APP="$2"; shift 2 ;;
    --config) CONFIG="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage; exit 2 ;;
  esac
done

STAMP="$(date -u +%Y%m%d-%H%M%S)"
REPORT_ROOT="${HOME}/mysttiq-test-results/v${VERSION}/${STAMP}"
mkdir -p "$REPORT_ROOT"
LOG="$REPORT_ROOT/acceptance.log"
SUMMARY="$REPORT_ROOT/summary.txt"
JSON_SUMMARY="$REPORT_ROOT/summary.json"
exec > >(tee -a "$LOG") 2>&1

PASS=0
FAIL=0
WARN=0
RESULT_ROWS=()

record() {
  local state="$1" name="$2" detail="${3:-}"
  printf '[%s] %s%s\n' "$state" "$name" "${detail:+ :: $detail}"
  RESULT_ROWS+=("$state|$name|$detail")
  case "$state" in PASS) ((PASS++));; FAIL) ((FAIL++));; WARN) ((WARN++));; esac
}

run_capture() {
  local file="$1"; shift
  "$@" >"$REPORT_ROOT/$file" 2>&1
}

json_field() { jq -r "$2 // empty" "$1" 2>/dev/null; }

cleanup_pid=""
cleanup_dir=""
cleanup() {
  if [[ -n "$cleanup_pid" ]] && kill -0 "$cleanup_pid" 2>/dev/null; then kill "$cleanup_pid" 2>/dev/null || true; wait "$cleanup_pid" 2>/dev/null || true; fi
  [[ -n "$cleanup_dir" && -d "$cleanup_dir" ]] && rm -rf "$cleanup_dir"
}
trap cleanup EXIT

echo "============================================================"
echo "MystTiq v${VERSION} Linux Acceptance"
echo "UTC: $(date -u --iso-8601=seconds)"
echo "Host: $(hostname)"
echo "Report: $REPORT_ROOT"
echo "============================================================"

# Environment
if grep -q 'Ubuntu 24.04.4 LTS' /etc/os-release 2>/dev/null; then record PASS "Ubuntu reference environment" "24.04.4 LTS"; else record WARN "Ubuntu reference environment" "$(. /etc/os-release; echo "${PRETTY_NAME:-unknown}")"; fi
kernel="$(uname -r)"
[[ "$kernel" == "6.8.0-137-generic" ]] && record PASS "Reference kernel" "$kernel" || record WARN "Reference kernel" "$kernel"
[[ -x "$APP" ]] && record PASS "MystTiq executable" "$APP" || record FAIL "MystTiq executable" "$APP missing/not executable"
command -v jq >/dev/null && record PASS "jq available" || record FAIL "jq available"
command -v curl >/dev/null && record PASS "curl available" || record FAIL "curl available"
command -v systemctl >/dev/null && record PASS "systemctl available" || record FAIL "systemctl available"

if ((INSTALL_CURRENT)); then
  echo
  echo "==> Installing this v${VERSION} build into systemd"
  if sudo -v && sudo "$APP" service-install --service-user "${USER}" --config "$CONFIG" --start-now >"$REPORT_ROOT/service-install.txt" 2>&1; then
    record PASS "Current build installed into systemd"
    sleep 8
  else
    record FAIL "Current build installed into systemd" "see service-install.txt"
  fi
fi

# Probe/config
if run_capture probe.txt "$APP" probe; then record PASS "Platform probe"; else record FAIL "Platform probe" "see probe.txt"; fi
if run_capture config-validate.txt "$APP" config-validate --config "$CONFIG"; then record PASS "Configuration validates" "$CONFIG"; else record FAIL "Configuration validates" "see config-validate.txt"; fi
if "$APP" config-show --config "$CONFIG" >"$REPORT_ROOT/config-effective.json" 2>"$REPORT_ROOT/config-show.err"; then
  schema="$(jq -r '.SchemaVersion // .schemaVersion // 0' "$REPORT_ROOT/config-effective.json")"
  [[ "$schema" == "2" ]] && record PASS "Configuration schema" "2" || record FAIL "Configuration schema" "expected 2, got $schema"
else record FAIL "Configuration show" "see config-show.err"; fi

# Service/systemd
if run_capture service-status.txt "$APP" service-status --config "$CONFIG"; then record PASS "Service status command"; else record FAIL "Service status command"; fi
if systemctl is-enabled "$SERVICE" >"$REPORT_ROOT/systemd-enabled.txt" 2>&1; then record PASS "systemd enabled"; else record FAIL "systemd enabled"; fi
if systemctl is-active "$SERVICE" >"$REPORT_ROOT/systemd-active.txt" 2>&1; then record PASS "systemd active"; else record FAIL "systemd active"; fi
if sudo -n systemd-analyze verify "/etc/systemd/system/${SERVICE}.service" >"$REPORT_ROOT/systemd-verify.txt" 2>&1; then
  record PASS "systemd unit verifies"
else
  if systemd-analyze verify "/etc/systemd/system/${SERVICE}.service" >"$REPORT_ROOT/systemd-verify.txt" 2>&1; then record PASS "systemd unit verifies"; else record FAIL "systemd unit verifies" "see systemd-verify.txt"; fi
fi
systemctl show "$SERVICE" -p StartLimitIntervalUSec -p StartLimitBurst >"$REPORT_ROOT/systemd-start-limit.txt" 2>&1
if grep -q 'StartLimitIntervalUSec=5min' "$REPORT_ROOT/systemd-start-limit.txt" && grep -q 'StartLimitBurst=5' "$REPORT_ROOT/systemd-start-limit.txt"; then record PASS "systemd restart throttle" "5min / burst 5"; else record FAIL "systemd restart throttle" "see systemd-start-limit.txt"; fi

# API
scheme="http"
bind="127.0.0.1"
if [[ -s "$REPORT_ROOT/config-effective.json" ]]; then
  bind="$(jq -r '.Api.BindAddress // .api.bindAddress // "127.0.0.1"' "$REPORT_ROOT/config-effective.json")"
  API_PORT="$(jq -r '.Api.Port // .api.port // 8213' "$REPORT_ROOT/config-effective.json")"
  tls="$(jq -r '.Api.Tls.Enabled // .api.tls.enabled // false' "$REPORT_ROOT/config-effective.json")"
  [[ "$tls" == "true" ]] && scheme="https"
fi

curl_opts=(-sS --max-time 5)
[[ "$scheme" == "https" ]] && curl_opts+=(-k)
if curl "${curl_opts[@]}" "${scheme}://${bind}:${API_PORT}/healthz" >"$REPORT_ROOT/healthz.json" 2>"$REPORT_ROOT/healthz.err"; then
  [[ "$(jq -r '.status // empty' "$REPORT_ROOT/healthz.json")" == "ok" ]] && record PASS "API health" "${scheme}://${bind}:${API_PORT}" || record FAIL "API health" "unexpected payload"
else record FAIL "API health" "see healthz.err"; fi

ss -lntp >"$REPORT_ROOT/listeners.txt" 2>&1 || true
if grep -Eq "127\.0\.0\.1:${API_PORT}|\[::1\]:${API_PORT}" "$REPORT_ROOT/listeners.txt"; then record PASS "Default API listener is loopback"; else
  if [[ "$bind" != "127.0.0.1" && "$bind" != "::1" ]]; then record PASS "Configured secured API listener" "$bind:$API_PORT"; else record FAIL "API listener" "expected loopback $API_PORT"; fi
fi

# Current PalServer state
if "$APP" status --config "$CONFIG" --json >"$REPORT_ROOT/server-status.json" 2>"$REPORT_ROOT/server-status.err"; then
  ready="$(jq -r '.Ready // .ready // false' "$REPORT_ROOT/server-status.json")"
  phase="$(jq -r '.Phase // .phase // -1' "$REPORT_ROOT/server-status.json")"
  if [[ "$ready" == "true" ]]; then record PASS "PalServer Running / Ready"; else record WARN "PalServer readiness" "phase=$phase ready=$ready"; fi
else record FAIL "PalServer status" "see server-status.err"; fi
ss -lunp >"$REPORT_ROOT/udp-listeners.txt" 2>&1 || true
grep -q ':8211 ' "$REPORT_ROOT/udp-listeners.txt" && record PASS "UDP 8211 listening" || record WARN "UDP 8211 listening" "server may intentionally be stopped"

# Journal current boot: old history intentionally excluded.
journalctl -u "$SERVICE" -b --no-pager >"$REPORT_ROOT/journal-current-boot.txt" 2>&1 || true
if grep -Eqi 'Unknown key name|Unhandled exception|fail(ed|ure)|fatal' "$REPORT_ROOT/journal-current-boot.txt"; then record WARN "Current-boot journal" "possible warning/error; review journal-current-boot.txt"; else record PASS "Current-boot journal clean"; fi

# Security gate: prove unsafe remote config fails closed.
cleanup_dir="$(mktemp -d)"
tmpcfg="$cleanup_dir/unsafe.json"
jq '.SchemaVersion=2 | .Api.BindAddress="0.0.0.0" | .Api.Authentication.Enabled=false | .Api.Tls.Enabled=false' "$REPORT_ROOT/config-effective.json" >"$tmpcfg"
if "$APP" config-validate --config "$tmpcfg" >"$REPORT_ROOT/unsafe-config-validation.txt" 2>&1; then record FAIL "Unsafe remote config fails closed" "validator accepted 0.0.0.0 without auth/TLS"; else record PASS "Unsafe remote config fails closed"; fi

# TLS provisioning and explicit remote-enrollment configuration acceptance.
tls_token="$cleanup_dir/tls-token"
tls_cert="$cleanup_dir/mysttiq-test.pfx"
tls_password="$cleanup_dir/tls-password"
tls_config="$cleanup_dir/tls-config.json"
cp "$REPORT_ROOT/config-effective.json" "$tls_config"

if "$APP" api-token-create --token-file "$tls_token" >"$REPORT_ROOT/tls-token-create.txt" 2>&1 &&
   "$APP" api-tls-create \
      --bind-address 127.0.0.1 \
      --dns-name localhost \
      --certificate-file "$tls_cert" \
      --certificate-password-file "$tls_password" \
      >"$REPORT_ROOT/tls-create.txt" 2>&1; then
  [[ -s "$tls_cert" && -s "$tls_password" ]] &&
    record PASS "TLS certificate provisioning" ||
    record FAIL "TLS certificate provisioning" "certificate/password output missing"

  if "$APP" api-remote-enable \
      --config "$tls_config" \
      --bind-address 192.0.2.10 \
      --api-port 18214 \
      --token-file "$tls_token" \
      --certificate-file "$tls_cert" \
      --certificate-password-file "$tls_password" \
      >"$REPORT_ROOT/remote-enable.txt" 2>&1 &&
     "$APP" config-validate --config "$tls_config" \
      >"$REPORT_ROOT/remote-enabled-validation.txt" 2>&1; then
    record PASS "Explicit secured remote configuration"
  else
    record FAIL "Explicit secured remote configuration" "see remote-enable.txt / remote-enabled-validation.txt"
  fi

  if "$APP" api-remote-disable --config "$tls_config" \
      >"$REPORT_ROOT/remote-disable.txt" 2>&1; then
    disabled_bind="$("$APP" config-show --config "$tls_config" | jq -r '.Api.BindAddress // .api.bindAddress // empty')"
    [[ "$disabled_bind" == "127.0.0.1" ]] &&
      record PASS "Remote configuration returns to loopback" ||
      record FAIL "Remote configuration returns to loopback" "bind=$disabled_bind"
  else
    record FAIL "Remote configuration returns to loopback" "see remote-disable.txt"
  fi
else
  record FAIL "TLS certificate provisioning" "see tls-token-create.txt / tls-create.txt"
fi

# Temporary authenticated loopback API acceptance on alternate port.
token="$cleanup_dir/api-token"
authcfg="$cleanup_dir/auth.json"
if "$APP" api-token-create --token-file "$token" >"$REPORT_ROOT/token-create.txt" 2>&1; then
  jq --arg token "$token" '.SchemaVersion=2 | .Api.Port=18213 | .Api.BindAddress="127.0.0.1" | .Api.Authentication.Enabled=true | .Api.Authentication.TokenFile=$token | .Api.Tls.Enabled=false' "$REPORT_ROOT/config-effective.json" >"$authcfg"
  "$APP" api-run --config "$authcfg" >"$REPORT_ROOT/auth-api.log" 2>&1 & cleanup_pid=$!
  for _ in {1..20}; do curl -s --max-time 1 http://127.0.0.1:18213/healthz >/dev/null 2>&1 && break; sleep .5; done
  unauth_code="$(curl -s -o "$REPORT_ROOT/auth-unauthorized.json" -w '%{http_code}' --max-time 3 http://127.0.0.1:18213/api/v1/status || true)"
  bearer="$(tr -d '\r\n' < "$token")"
  auth_code="$(curl -s -o "$REPORT_ROOT/auth-authorized.json" -w '%{http_code}' --max-time 3 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/status || true)"
  [[ "$unauth_code" == "401" ]] && record PASS "API rejects missing bearer token" "HTTP 401" || record FAIL "API rejects missing bearer token" "HTTP $unauth_code"
  [[ "$auth_code" == "200" ]] && record PASS "API accepts valid bearer token" "HTTP 200" || record FAIL "API accepts valid bearer token" "HTTP $auth_code"
  kill "$cleanup_pid" 2>/dev/null || true; wait "$cleanup_pid" 2>/dev/null || true; cleanup_pid=""
else record FAIL "Temporary API token generation" "see token-create.txt"; fi

# Extended lifecycle test uses the installed local API only when it is plain loopback/no-auth.
if ((EXTENDED)); then
  auth_enabled="$(jq -r '.Api.Authentication.Enabled // .api.authentication.enabled // false' "$REPORT_ROOT/config-effective.json")"
  if [[ "$scheme" != "http" || "$auth_enabled" == "true" || "$bind" != "127.0.0.1" ]]; then
    record WARN "Extended API lifecycle" "skipped: current API uses TLS/auth/non-default bind"
  else
    initial_ready="$ready"
    base="http://127.0.0.1:${API_PORT}"
    if [[ "$initial_ready" == "true" ]]; then
      oldpid="$(jq -r '.NativeProcessId // .nativeProcessId // empty' "$REPORT_ROOT/server-status.json")"
      if curl -sS -X POST "$base/api/v1/server/restart" >"$REPORT_ROOT/extended-restart.json"; then
        newpid="$(jq -r '.snapshot.nativeProcessId // empty' "$REPORT_ROOT/extended-restart.json")"
        [[ -n "$newpid" && "$newpid" != "$oldpid" ]] && record PASS "Extended API restart" "$oldpid -> $newpid" || record FAIL "Extended API restart" "PID did not change"
      else record FAIL "Extended API restart"; fi
    else
      curl -sS -X POST "$base/api/v1/server/start" >"$REPORT_ROOT/extended-start.json" && record PASS "Extended API start" || record FAIL "Extended API start"
      curl -sS -X POST "$base/api/v1/server/stop" >"$REPORT_ROOT/extended-stop.json" && record PASS "Extended API stop" || record FAIL "Extended API stop"
    fi
  fi
fi

# Production-readiness integration gate.
# Extended acceptance must prove the v0.3.0.7 integration/Doctor layer as part
# of the same automated Linux evidence run.
if ((EXTENDED)); then
  echo
  echo "==> Production-readiness integration gate"

  production_runner="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/Test-v${VERSION}-ProductionReadiness.sh"

  if [[ ! -f "$production_runner" ]]; then
    record FAIL "Production readiness integration" "runner missing: $production_runner"
  else
    chmod +x "$production_runner" 2>/dev/null || true

    if MYSTTIQ_APP="$APP" \
       MYSTTIQ_CONFIG="$CONFIG" \
       bash "$production_runner" \
       >"$REPORT_ROOT/production-readiness-integration.log" 2>&1; then
      record PASS "Production readiness integration" "see production-readiness-integration.log"
    else
      record FAIL "Production readiness integration" "see production-readiness-integration.log"
    fi
  fi
fi

# Summary files
{
  echo "MystTiq v${VERSION} Linux Acceptance"
  echo "Passed: $PASS"
  echo "Failed: $FAIL"
  echo "Warnings: $WARN"
  echo "Report: $REPORT_ROOT"
  echo
  for row in "${RESULT_ROWS[@]}"; do IFS='|' read -r st name detail <<<"$row"; printf '[%s] %s%s\n' "$st" "$name" "${detail:+ :: $detail}"; done
} >"$SUMMARY"

printf '{\n  "version": "%s",\n  "passed": %d,\n  "failed": %d,\n  "warnings": %d,\n  "report": "%s"\n}\n' "$VERSION" "$PASS" "$FAIL" "$WARN" "$REPORT_ROOT" >"$JSON_SUMMARY"

echo
echo "================ FINAL SUMMARY ================"
cat "$SUMMARY"

((FAIL == 0))
