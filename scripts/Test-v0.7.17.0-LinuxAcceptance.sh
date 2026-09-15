#!/usr/bin/env bash
set -uo pipefail

# Carried forward from Test-v0.6.1.0-LinuxAcceptance.sh (the last version this file existed for --
# Build-LinuxHeadless.ps1/Deploy-Test-MystTiqLinux.ps1 had been broken for every release since
# v0.6.2.0 because a version-specific copy of this script is a hard requirement to publish at all).
# Updated for v0.7.17.0: schema version 2 -> 3 (v0.6.2.0's Multi-Server Fleet migration), reference
# kernel bump, and a full pass expanding coverage to essentially the entire current profile-scoped
# and fleet-level GET surface (~30 read-only endpoints previously untested here), plus reachability/
# graceful-failure checks on every safe-to-call POST (self-tests, re-runs, preview-only analysis
# endpoints exercised with garbage identifiers to prove they validate input rather than crash), and
# fully reversible create-then-delete roundtrips for automation rules and security principals.
# Endpoints that mutate real save/world/guild/base/player/backup/mod/network/system state against
# this live production install are deliberately still NOT exercised here -- see the block comment
# above the "Deliberately NOT exercised" list near the end of this file for the exact set and why.
VERSION="0.7.17.0"
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
SKIP=0
RESULT_ROWS=()

record() {
  local state="$1" name="$2" detail="${3:-}"
  printf '[%s] %s%s\n' "$state" "$name" "${detail:+ :: $detail}"
  RESULT_ROWS+=("$state|$name|$detail")
  case "$state" in PASS) ((PASS++));; FAIL) ((FAIL++));; WARN) ((WARN++));; SKIP) ((SKIP++));; esac
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

# v0.6.1.0's own memory note already documented this: /opt/mysttiq/runtime is root-owned by the
# live systemd unit (User=root), so any CLI invocation run as this script's own user that tries to
# persist lifecycle state under the live config's RuntimeRoot crashes with an unhandled
# UnauthorizedAccessException. Every ephemeral/read-only CLI invocation below that isn't hitting
# the live service over HTTP uses an isolated, self-owned --runtime-root instead of the live one.
cleanup_dir="$(mktemp -d)"
scratch_runtime="$cleanup_dir/scratch-runtime"
mkdir -p "$scratch_runtime"

echo "============================================================"
echo "MystTiq v${VERSION} Linux Acceptance"
echo "UTC: $(date -u --iso-8601=seconds)"
echo "Host: $(hostname)"
echo "Report: $REPORT_ROOT"
echo "============================================================"

# Environment
if grep -q 'Ubuntu 24.04.4 LTS' /etc/os-release 2>/dev/null; then record PASS "Ubuntu reference environment" "24.04.4 LTS"; else record WARN "Ubuntu reference environment" "$(. /etc/os-release; echo "${PRETTY_NAME:-unknown}")"; fi
kernel="$(uname -r)"
[[ "$kernel" == "6.8.0-138-generic" ]] && record PASS "Reference kernel" "$kernel" || record WARN "Reference kernel" "$kernel"
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
  # v0.6.2.0's Multi-Server Fleet migration bumped the schema from 2 to 3 (Server -> Servers[]).
  [[ "$schema" == "3" ]] && record PASS "Configuration schema" "3" || record FAIL "Configuration schema" "expected 3, got $schema"
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
if "$APP" status --config "$CONFIG" --runtime-root "$scratch_runtime" --json >"$REPORT_ROOT/server-status.json" 2>"$REPORT_ROOT/server-status.err"; then
  ready="$(jq -r '.Ready // .ready // false' "$REPORT_ROOT/server-status.json")"
  phase="$(jq -r '.Phase // .phase // -1' "$REPORT_ROOT/server-status.json")"
  if [[ "$ready" == "true" ]]; then record PASS "PalServer Running / Ready"; else record WARN "PalServer readiness" "phase=$phase ready=$ready"; fi
else record FAIL "PalServer status" "see server-status.err"; fi
ss -lunp >"$REPORT_ROOT/udp-listeners.txt" 2>&1 || true
grep -q ':8211 ' "$REPORT_ROOT/udp-listeners.txt" && record PASS "UDP 8211 listening" || record WARN "UDP 8211 listening" "server may intentionally be stopped"

# Recent window, not the whole current boot: on a long-uptime host a boot-scoped scan keeps
# re-flagging one old, already-resolved event indefinitely, however unrelated to current health.
journalctl -u "$SERVICE" --since "-30 minutes" --no-pager >"$REPORT_ROOT/journal-recent.txt" 2>&1 || true
if grep -Eqi 'Unknown key name|Unhandled exception|fail(ed|ure)|fatal' "$REPORT_ROOT/journal-recent.txt"; then record WARN "Recent journal" "possible warning/error; review journal-recent.txt"; else record PASS "Recent journal clean"; fi

# Security gate: prove unsafe remote config fails closed.
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
  jq --arg token "$token" '.SchemaVersion=3 | .Api.Port=18213 | .Api.BindAddress="127.0.0.1" | .Api.Authentication.Enabled=true | .Api.Authentication.TokenFile=$token | .Api.Tls.Enabled=false' "$REPORT_ROOT/config-effective.json" >"$authcfg"
  authapi_runtime="$cleanup_dir/authapi-runtime"
  mkdir -p "$authapi_runtime"
  "$APP" api-run --config "$authcfg" --runtime-root "$authapi_runtime" >"$REPORT_ROOT/auth-api.log" 2>&1 & cleanup_pid=$!
  for _ in {1..20}; do curl -s --max-time 1 http://127.0.0.1:18213/healthz >/dev/null 2>&1 && break; sleep .5; done
  unauth_code="$(curl -s -o "$REPORT_ROOT/auth-unauthorized.json" -w '%{http_code}' --max-time 3 http://127.0.0.1:18213/api/v1/status || true)"
  bearer="$(tr -d '\r\n' < "$token")"
  auth_code="$(curl -s -o "$REPORT_ROOT/auth-authorized.json" -w '%{http_code}' --max-time 3 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/status || true)"
  [[ "$unauth_code" == "401" ]] && record PASS "API rejects missing bearer token" "HTTP 401" || record FAIL "API rejects missing bearer token" "HTTP $unauth_code"
  [[ "$auth_code" == "200" ]] && record PASS "API accepts valid bearer token" "HTTP 200" || record FAIL "API accepts valid bearer token" "HTTP $auth_code"


  distribution_code="$(curl -s -o "$REPORT_ROOT/server-distribution.json" -w '%{http_code}' --max-time 5 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/server/distribution || true)"
  distribution_plan_code="$(curl -s -o "$REPORT_ROOT/server-distribution-plan.json" -w '%{http_code}' --max-time 5 -H "Authorization: Bearer $bearer" "http://127.0.0.1:18213/api/v1/server/distribution/plan?validate=true" || true)"

  [[ "$distribution_code" == "200" ]] && record PASS "Server distribution status endpoint" "HTTP 200" || record FAIL "Server distribution status endpoint" "HTTP $distribution_code"
  [[ "$distribution_plan_code" == "200" ]] && record PASS "Server distribution plan endpoint" "HTTP 200" || record FAIL "Server distribution plan endpoint" "HTTP $distribution_plan_code"

  if [[ "$distribution_code" == "200" ]] && jq -e 'has("platform") and has("steamCmdPath") and has("steamCmdExists") and has("serverRoot") and has("serverExecutableExists")' "$REPORT_ROOT/server-distribution.json" >/dev/null 2>&1; then
    record PASS "Server distribution status payload contract"
  else
    record FAIL "Server distribution status payload contract" "see server-distribution.json"
  fi

  if [[ "$distribution_plan_code" == "200" ]] && jq -e 'has("platform") and has("steamCmdPath") and has("serverRoot") and has("validate") and has("arguments")' "$REPORT_ROOT/server-distribution-plan.json" >/dev/null 2>&1; then
    record PASS "Server distribution plan payload contract"
  else
    record FAIL "Server distribution plan payload contract" "see server-distribution-plan.json"
  fi




  mods_code="$(curl -s -o "$REPORT_ROOT/mods.json" -w '%{http_code}' --max-time 15 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/mods || true)"
  mods_verify_code="$(curl -s -o "$REPORT_ROOT/mods-verify.json" -w '%{http_code}' --max-time 15 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/mods/verify || true)"
  ue4ss_code="$(curl -s -o "$REPORT_ROOT/ue4ss.json" -w '%{http_code}' --max-time 15 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/ue4ss || true)"
  [[ "$mods_code" == "200" ]] && record PASS "MOD inventory endpoint" "HTTP 200" || record FAIL "MOD inventory endpoint" "HTTP $mods_code"
  [[ "$mods_verify_code" == "200" ]] && record PASS "MOD verification endpoint" "HTTP 200" || record FAIL "MOD verification endpoint" "HTTP $mods_verify_code"
  [[ "$ue4ss_code" == "200" ]] && record PASS "UE4SS status endpoint" "HTTP 200" || record FAIL "UE4SS status endpoint" "HTTP $ue4ss_code"
  if [[ "$mods_code" == "200" ]] && jq -e 'has("installed") and has("runtimeConfirmed") and has("activeUnverified") and has("disabled") and has("confirmedIssues") and has("overallHealth") and has("ue4ss") and has("mods")' "$REPORT_ROOT/mods.json" >/dev/null 2>&1; then record PASS "MOD inventory payload contract"; else record FAIL "MOD inventory payload contract" "see mods.json"; fi
  if [[ "$ue4ss_code" == "200" ]] && jq -e 'has("activeModsRoot") and has("detectionMethod") and has("runtimeVerified") and has("runtimeMatchesActiveRoot") and has("healthState")' "$REPORT_ROOT/ue4ss.json" >/dev/null 2>&1; then record PASS "UE4SS status payload contract"; else record FAIL "UE4SS status payload contract" "see ue4ss.json"; fi

  player_guild_code="$(curl -s -o "$REPORT_ROOT/player-guild-explorer.json" -w '%{http_code}' --max-time 15 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/world/players-guilds || true)"
  [[ "$player_guild_code" == "200" ]] && record PASS "Player & Guild Explorer endpoint" "HTTP 200" || record FAIL "Player & Guild Explorer endpoint" "HTTP $player_guild_code"

  if [[ "$player_guild_code" == "200" ]] && jq -e 'has("available") and has("semanticAvailable") and has("semanticSource") and has("players") and has("guilds") and has("warnings") and has("detail")' "$REPORT_ROOT/player-guild-explorer.json" >/dev/null 2>&1; then
    record PASS "Player & Guild Explorer payload contract"
  else
    record FAIL "Player & Guild Explorer payload contract" "see player-guild-explorer.json"
  fi

  if [[ "$player_guild_code" == "200" ]] && jq -e '.players | all(has("playerId") and has("saveExists") and has("evidence"))' "$REPORT_ROOT/player-guild-explorer.json" >/dev/null 2>&1; then
    record PASS "Player Explorer evidence contract"
  else
    record FAIL "Player Explorer evidence contract" "see player-guild-explorer.json"
  fi

  operations_code="$(curl -s -o "$REPORT_ROOT/operations.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/operations || true)"
  [[ "$operations_code" == "200" ]] && record PASS "Operation Platform list endpoint" "HTTP 200" || record FAIL "Operation Platform list endpoint" "HTTP $operations_code"

  if [[ "$operations_code" == "200" ]] && jq -e 'type == "array"' "$REPORT_ROOT/operations.json" >/dev/null 2>&1; then
    record PASS "Operation Platform list payload contract"
  else
    record FAIL "Operation Platform list payload contract" "see operations.json"
  fi

  operations_404_code="$(curl -s -o "$REPORT_ROOT/operations-404.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/operations/nonexistent-id || true)"
  [[ "$operations_404_code" == "404" ]] && record PASS "Operation Platform unknown ID returns 404" || record FAIL "Operation Platform unknown ID returns 404" "HTTP $operations_404_code"

  automation_rules_code="$(curl -s -o "$REPORT_ROOT/automation-rules.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/automation/rules || true)"
  [[ "$automation_rules_code" == "200" ]] && record PASS "Automation rules list endpoint" "HTTP 200" || record FAIL "Automation rules list endpoint" "HTTP $automation_rules_code"
  if [[ "$automation_rules_code" == "200" ]] && jq -e 'type == "array"' "$REPORT_ROOT/automation-rules.json" >/dev/null 2>&1; then
    record PASS "Automation rules payload contract"
  else
    record FAIL "Automation rules payload contract" "see automation-rules.json"
  fi

  automation_runs_code="$(curl -s -o "$REPORT_ROOT/automation-runs.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/automation/runs || true)"
  [[ "$automation_runs_code" == "200" ]] && record PASS "Automation runs endpoint" "HTTP 200" || record FAIL "Automation runs endpoint" "HTTP $automation_runs_code"

  whoami_code="$(curl -s -o "$REPORT_ROOT/whoami.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/security/whoami || true)"
  [[ "$whoami_code" == "200" ]] && jq -e '.role == "Owner"' "$REPORT_ROOT/whoami.json" >/dev/null 2>&1 && record PASS "Legacy bearer token resolves to Owner role (RBAC backward compatibility)" || record FAIL "Legacy bearer token resolves to Owner role" "HTTP $whoami_code"

  alert_rules_code="$(curl -s -o "$REPORT_ROOT/alert-rules.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/alerts/rules || true)"
  [[ "$alert_rules_code" == "200" ]] && record PASS "Alert Center rules endpoint" "HTTP 200" || record FAIL "Alert Center rules endpoint" "HTTP $alert_rules_code"

  disk_prediction_code="$(curl -s -o "$REPORT_ROOT/disk-prediction.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/alerts/predictions || true)"
  [[ "$disk_prediction_code" == "200" ]] && record PASS "Disk-space prediction endpoint" "HTTP 200" || record FAIL "Disk-space prediction endpoint" "HTTP $disk_prediction_code"

  backups_class_code="$(curl -s -o "$REPORT_ROOT/backups-for-class.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/backups || true)"
  if [[ "$backups_class_code" == "200" ]] && jq -e '.items | all(has("class"))' "$REPORT_ROOT/backups-for-class.json" >/dev/null 2>&1; then
    record PASS "Backup inventory items carry a class field"
  else
    record FAIL "Backup inventory items carry a class field" "see backups-for-class.json"
  fi

  world_explorer_code="$(curl -s -o "$REPORT_ROOT/world-explorer.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/world/explorer || true)"
  [[ "$world_explorer_code" == "200" ]] && record PASS "World Explorer endpoint" "HTTP 200" || record FAIL "World Explorer endpoint" "HTTP $world_explorer_code"

  if [[ "$world_explorer_code" == "200" ]] && jq -e 'has("available") and has("saveRoot") and has("worldCount") and has("fileCount") and has("playerSaveCount") and has("worlds") and has("files") and has("detail")' "$REPORT_ROOT/world-explorer.json" >/dev/null 2>&1; then
    record PASS "World Explorer payload contract"
  else
    record FAIL "World Explorer payload contract" "see world-explorer.json"
  fi

  backups_code="$(curl -s -o "$REPORT_ROOT/backups.json" -w '%{http_code}' --max-time 5 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/backups || true)"
  editable_config_code="$(curl -s -o "$REPORT_ROOT/editable-config.json" -w '%{http_code}' --max-time 5 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/config/editable || true)"

  [[ "$backups_code" == "200" ]] && record PASS "Backup inventory endpoint" "HTTP 200" || record FAIL "Backup inventory endpoint" "HTTP $backups_code"
  [[ "$editable_config_code" == "200" ]] && record PASS "Editable configuration endpoint" "HTTP 200" || record FAIL "Editable configuration endpoint" "HTTP $editable_config_code"

  if [[ "$backups_code" == "200" ]] && jq -e 'has("count") and has("totalSizeBytes") and has("items") and has("detail")' "$REPORT_ROOT/backups.json" >/dev/null 2>&1; then
    record PASS "Backup inventory payload contract"
  else
    record FAIL "Backup inventory payload contract" "see backups.json"
  fi

  if [[ "$editable_config_code" == "200" ]] && jq -e 'has("schemaVersion") and has("api") and has("lifecycle") and has("server") and has("authenticationEnabled") and has("tlsEnabled")' "$REPORT_ROOT/editable-config.json" >/dev/null 2>&1; then
    record PASS "Editable configuration payload contract"
  else
    record FAIL "Editable configuration payload contract" "see editable-config.json"
  fi

  if grep -qiE '"token|passwordFile|certificatePassword' "$REPORT_ROOT/editable-config.json"; then
    record FAIL "Editable configuration excludes secret-file fields"
  else
    record PASS "Editable configuration excludes secret-file fields"
  fi

  players_code="$(curl -s -o "$REPORT_ROOT/monitor-players.json" -w '%{http_code}' --max-time 5 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/players || true)"
  logs_code="$(curl -s -o "$REPORT_ROOT/monitor-logs.json" -w '%{http_code}' --max-time 5 -H "Authorization: Bearer $bearer" "http://127.0.0.1:18213/api/v1/logs/tail?lines=20" || true)"
  metrics_code="$(curl -s -o "$REPORT_ROOT/monitor-metrics.json" -w '%{http_code}' --max-time 5 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/metrics || true)"
  doctor_code="$(curl -s -o "$REPORT_ROOT/doctor.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" http://127.0.0.1:18213/api/v1/doctor || true)"
  [[ "$doctor_code" == "200" ]] && jq -e '.status and .checks and (.checks | length > 0)' "$REPORT_ROOT/doctor.json" >/dev/null 2>&1 && record PASS "Production Doctor endpoint" || record FAIL "Production Doctor endpoint" "HTTP $doctor_code or invalid payload"

  [[ "$players_code" == "200" ]] && record PASS "Monitoring players endpoint" "HTTP 200" || record FAIL "Monitoring players endpoint" "HTTP $players_code"
  [[ "$logs_code" == "200" ]] && record PASS "Monitoring log-tail endpoint" "HTTP 200" || record FAIL "Monitoring log-tail endpoint" "HTTP $logs_code"
  [[ "$metrics_code" == "200" ]] && record PASS "Monitoring metrics endpoint" "HTTP 200" || record FAIL "Monitoring metrics endpoint" "HTTP $metrics_code"

  if [[ "$players_code" == "200" ]] && jq -e 'has("available") and has("onlineCount") and has("players") and has("detail")' "$REPORT_ROOT/monitor-players.json" >/dev/null 2>&1; then
    record PASS "Monitoring players payload contract"
  else
    record FAIL "Monitoring players payload contract" "see monitor-players.json"
  fi

  if [[ "$logs_code" == "200" ]] && jq -e 'has("available") and has("lines") and has("detail")' "$REPORT_ROOT/monitor-logs.json" >/dev/null 2>&1; then
    record PASS "Monitoring log-tail payload contract"
  else
    record FAIL "Monitoring log-tail payload contract" "see monitor-logs.json"
  fi

  if [[ "$metrics_code" == "200" ]] && jq -e 'has("available") and has("workingSetBytes") and has("threadCount") and has("detail")' "$REPORT_ROOT/monitor-metrics.json" >/dev/null 2>&1; then
    record PASS "Monitoring metrics payload contract"
  else
    record FAIL "Monitoring metrics payload contract" "see monitor-metrics.json"
  fi

  if grep -qi 'AdminPassword' "$REPORT_ROOT/monitor-players.json"; then
    record FAIL "Monitoring player payload excludes AdminPassword"
  else
    record PASS "Monitoring player payload excludes AdminPassword"
  fi

  # --- v0.7.17.0: broaden coverage to the full current API surface -------------------------------
  # Read-only/analysis/self-test endpoints shipped across the ~15 releases since this script was
  # last maintained (v0.6.1.0). Each just needs to answer with a non-5xx status without crashing --
  # this is a reachability/crash smoke pass, not a deep contract audit of every field (see the
  # header disclosure at the top of this file for what's deliberately still out of scope).
  base="http://127.0.0.1:18213"
  get_ok() {
    local name="$1" path="$2" out="$3"
    local code
    code="$(curl -s -o "$REPORT_ROOT/$out" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" "${base}${path}" || true)"
    [[ "$code" == "200" ]] && record PASS "$name" "HTTP 200" || record FAIL "$name" "HTTP $code"
  }
  post_reachable() {
    local name="$1" path="$2" body="$3" out="$4"
    local code
    code="$(curl -s -o "$REPORT_ROOT/$out" -w '%{http_code}' --max-time 15 -H "Authorization: Bearer $bearer" -H 'Content-Type: application/json' -X POST -d "$body" "${base}${path}" || true)"
    if [[ "$code" =~ ^[2-4][0-9][0-9]$ ]]; then record PASS "$name" "HTTP $code"; else record FAIL "$name" "HTTP $code (unexpected server error)"; fi
  }

  get_ok "Status poll endpoint" "/api/v1/status/poll" "poll.json"
  get_ok "Activity tail endpoint" "/api/v1/activity/tail?lines=20" "activity-tail.json"
  get_ok "Notifications list endpoint" "/api/v1/notifications" "notifications.json"
  get_ok "Notification channels endpoint" "/api/v1/notifications/channels" "notification-channels.json"
  get_ok "Notification templates endpoint" "/api/v1/notifications/templates" "notification-templates.json"
  get_ok "Discord bot settings endpoint" "/api/v1/notifications/discord-bot" "discord-bot.json"
  get_ok "RCON status endpoint" "/api/v1/rcon/status" "rcon-status.json"
  # RCON is disabled in the live PalWorldSettings.ini (PalServer is currently stopped), so these
  # two correctly answer with a non-2xx "prerequisites not satisfied" status rather than 200 --
  # any non-5xx response proves the endpoint is reachable and fails closed/gracefully rather than
  # crashing (matches the graceful-failure convention already used elsewhere in this script).
  rcon_doctor_code="$(curl -s -o "$REPORT_ROOT/rcon-doctor.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" "${base}/api/v1/rcon/doctor" || true)"
  [[ "$rcon_doctor_code" =~ ^[2-4][0-9][0-9]$ ]] && record PASS "RCON doctor endpoint" "HTTP $rcon_doctor_code" || record FAIL "RCON doctor endpoint" "HTTP $rcon_doctor_code"
  ban_list_code="$(curl -s -o "$REPORT_ROOT/ban-list.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" "${base}/api/v1/players/ban-list" || true)"
  [[ "$ban_list_code" =~ ^[2-4][0-9][0-9]$ ]] && record PASS "Ban list endpoint" "HTTP $ban_list_code" || record FAIL "Ban list endpoint" "HTTP $ban_list_code"
  get_ok "Whitelist endpoint" "/api/v1/players/whitelist" "whitelist.json"
  get_ok "Temporary bans endpoint" "/api/v1/players/temp-bans" "temp-bans.json"
  get_ok "Player moderation providers endpoint" "/api/v1/players/moderation/providers" "moderation-providers.json"
  get_ok "Player registry endpoint" "/api/v1/players/registry" "player-registry.json"
  get_ok "Player registry events endpoint" "/api/v1/players/registry/events" "player-registry-events.json"
  get_ok "Diagnostics report endpoint" "/api/v1/diagnostics/report" "diagnostics-report.json"
  get_ok "Network diagnostics endpoint" "/api/v1/diagnostics/network" "diagnostics-network.json"
  get_ok "WAN diagnostics endpoint" "/api/v1/diagnostics/network/wan" "diagnostics-wan.json"
  get_ok "Crash analyzer history endpoint" "/api/v1/crash-analyzer/history" "crash-history.json"
  get_ok "Save tools diagnostics endpoint" "/api/v1/save-tools/diagnostics" "save-tools-diagnostics.json"
  get_ok "Save tools file inventory endpoint" "/api/v1/save-tools/files" "save-tools-files.json"
  get_ok "World validate endpoint" "/api/v1/world/validate" "world-validate.json"
  get_ok "World transactions endpoint" "/api/v1/world/transactions" "world-transactions.json"
  get_ok "Pals list endpoint" "/api/v1/pals" "pals.json"
  get_ok "Anticheat rules endpoint" "/api/v1/anticheat/rules" "anticheat-rules.json"
  get_ok "Anticheat findings endpoint" "/api/v1/anticheat/findings" "anticheat-findings.json"
  get_ok "MOD workshop endpoint" "/api/v1/mods/workshop" "mods-workshop.json"
  get_ok "Server environment endpoint" "/api/v1/server/environment" "server-environment.json"
  get_ok "Palworld config endpoint" "/api/v1/palworld/config" "palworld-config.json"
  get_ok "Raw config endpoint" "/api/v1/config" "raw-config.json"

  post_reachable "Network diagnostics re-run (read-only, not a service restart)" "/api/v1/diagnostics/network/restart" '{}' "diagnostics-network-restart.json"
  post_reachable "Crash analyzer analyze endpoint (read-only)" "/api/v1/crash-analyzer/analyze" '{}' "crash-analyze.json"
  post_reachable "Save tools self-test endpoint (introspection only, no save-file writes)" "/api/v1/save-tools/self-test" '{}' "save-tools-self-test.json"
  post_reachable "World save-now (graceful failure expected; PalServer is not running)" "/api/v1/world/save-now" '{}' "world-save-now.json"
  post_reachable "RCON command (graceful failure expected; PalServer is not running)" "/api/v1/rcon/command" '{"command":"info"}' "rcon-command.json"
  post_reachable "Notifications self-test endpoint" "/api/v1/notifications/self-test" '{}' "notifications-self-test.json"
  post_reachable "Notifications mark-all-read endpoint (idempotent)" "/api/v1/notifications/mark-all-read" '{}' "notifications-mark-all-read.json"
  # Deliberately rejected: the config already exists on this real install, so CreateDefault's own
  # exists-check refuses before touching any file -- confirms the endpoint is reachable and fails
  # closed rather than silently overwriting a real PalWorldSettings.ini.
  defaults_code="$(curl -s -o "$REPORT_ROOT/palworld-config-defaults.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" -H 'Content-Type: application/json' -X POST -d '{"confirmCreate":true}' "${base}/api/v1/palworld/config/defaults" || true)"
  if [[ "$defaults_code" == "400" ]] && jq -e '.success == false and (.validationErrors // [] | any(contains("already exists")))' "$REPORT_ROOT/palworld-config-defaults.json" >/dev/null 2>&1; then
    record PASS "Palworld config defaults refuses to overwrite an existing PalWorldSettings.ini"
  else
    record FAIL "Palworld config defaults exists-check" "HTTP $defaults_code; see palworld-config-defaults.json"
  fi

  # Preview/analyze-only endpoints (never *-apply) exercised with garbage identifiers -- any
  # non-5xx response proves the endpoint validates input and does not crash on a nonexistent
  # target, without needing this run's real world/save data to contain a matching entity.
  post_reachable "World import analyze (preview-only)" "/api/v1/world/import/analyze" '{}' "world-import-analyze.json"
  post_reachable "Guild ownership preview (preview-only)" "/api/v1/guilds/ownership/preview" '{"operationType":"Claim","guildId":"nonexistent","playerId":"nonexistent"}' "guild-ownership-preview.json"
  post_reachable "Pal edit preview (preview-only)" "/api/v1/pals/edit/preview" '{"instanceId":"nonexistent","changes":{}}' "pal-edit-preview.json"
  post_reachable "Base ownership preview (preview-only)" "/api/v1/bases/ownership/preview" '{"baseId":"nonexistent","targetGuildId":"nonexistent"}' "base-ownership-preview.json"
  post_reachable "Base recovery preview (preview-only)" "/api/v1/bases/recovery/preview" '{"baseId":"nonexistent"}' "base-recovery-preview.json"
  post_reachable "Character migration preview (preview-only)" "/api/v1/players/migration/preview" '{"sourcePlayerId":"nonexistent","destinationPlayerId":"nonexistent"}' "migration-preview.json"

  # Fully reversible create-then-delete roundtrips -- pure metadata, never touch real save/world
  # data, and are cleaned up within this same block.
  # Trigger fires once a year (won't fire during this run regardless) and the action is a
  # harmless SendNotification -- explicitly disabled immediately after creation too, belt and
  # suspenders, before being deleted.
  automation_body='{"name":"mysttiq-acceptance-test-rule","trigger":{"kind":"Interval","interval":"365.00:00:00"},"condition":{"requireServerRunning":false,"requireServerStopped":false},"action":{"kind":"SendNotification","notificationSeverity":"Information","notificationTitle":"Acceptance test","notificationMessage":"Acceptance test rule -- safe to ignore"}}'
  automation_create_code="$(curl -s -o "$REPORT_ROOT/automation-create.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" -H 'Content-Type: application/json' -X POST -d "$automation_body" "${base}/api/v1/automation/rules" || true)"
  automation_rule_id="$(jq -r '.id // .Id // empty' "$REPORT_ROOT/automation-create.json" 2>/dev/null)"
  if [[ "$automation_create_code" == "200" && -n "$automation_rule_id" ]]; then
    record PASS "Automation rule create" "id=$automation_rule_id"
    curl -s -o /dev/null --max-time 10 -H "Authorization: Bearer $bearer" -H 'Content-Type: application/json' -X POST -d '{"enabled":false}' "${base}/api/v1/automation/rules/${automation_rule_id}/enabled" || true
    delete_code="$(curl -s -o "$REPORT_ROOT/automation-delete.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" -X DELETE "${base}/api/v1/automation/rules/${automation_rule_id}" || true)"
    [[ "$delete_code" == "200" || "$delete_code" == "204" ]] && record PASS "Automation rule delete (cleanup)" "HTTP $delete_code" || record FAIL "Automation rule delete (cleanup)" "HTTP $delete_code -- manual cleanup of rule $automation_rule_id may be needed"
  else
    record FAIL "Automation rule create" "HTTP $automation_create_code; see automation-create.json"
  fi

  principal_create_code="$(curl -s -o "$REPORT_ROOT/principal-create.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" -H 'Content-Type: application/json' -X POST -d '{"name":"mysttiq-acceptance-test-principal","role":"Viewer"}' "${base}/api/v1/security/principals" || true)"
  principal_id="$(jq -r '.principal.id // .Principal.Id // empty' "$REPORT_ROOT/principal-create.json" 2>/dev/null)"
  if [[ "$principal_create_code" == "200" && -n "$principal_id" ]]; then
    record PASS "Security principal create" "id=$principal_id"
    principal_delete_code="$(curl -s -o "$REPORT_ROOT/principal-delete.json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" -X DELETE "${base}/api/v1/security/principals/${principal_id}" || true)"
    [[ "$principal_delete_code" == "200" || "$principal_delete_code" == "204" ]] && record PASS "Security principal delete (cleanup)" "HTTP $principal_delete_code" || record FAIL "Security principal delete (cleanup)" "HTTP $principal_delete_code -- manual cleanup of principal $principal_id may be needed"
  else
    record FAIL "Security principal create" "HTTP $principal_create_code; see principal-create.json"
  fi

  # No-op roundtrips on editable settings: read the real current value back and PUT it unchanged,
  # proving the write path works without actually altering live behavior.
  noop_roundtrip() {
    local name="$1" get_path="$2" put_path="$3"
    local body
    body="$(curl -s --max-time 10 -H "Authorization: Bearer $bearer" "${base}${get_path}")"
    if [[ -z "$body" ]]; then record FAIL "$name (no-op roundtrip)" "GET returned no body"; return; fi
    local put_code
    put_code="$(curl -s -o "$REPORT_ROOT/noop-$(basename "$put_path").json" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $bearer" -H 'Content-Type: application/json' -X PUT -d "$body" "${base}${put_path}" || true)"
    [[ "$put_code" == "200" || "$put_code" == "204" ]] && record PASS "$name (no-op roundtrip)" "HTTP $put_code" || record FAIL "$name (no-op roundtrip)" "HTTP $put_code"
  }
  noop_roundtrip "Whitelist" "/api/v1/players/whitelist" "/api/v1/players/whitelist"
  noop_roundtrip "Notification channels" "/api/v1/notifications/channels" "/api/v1/notifications/channels"
  noop_roundtrip "Notification templates" "/api/v1/notifications/templates" "/api/v1/notifications/templates"
  noop_roundtrip "Discord bot settings" "/api/v1/notifications/discord-bot" "/api/v1/notifications/discord-bot"
  noop_roundtrip "Alert rules" "/api/v1/alerts/rules" "/api/v1/alerts/rules"
  noop_roundtrip "Anticheat rules" "/api/v1/anticheat/rules" "/api/v1/anticheat/rules"

  kill "$cleanup_pid" 2>/dev/null || true; wait "$cleanup_pid" 2>/dev/null || true; cleanup_pid=""
else record FAIL "Temporary API token generation" "see token-create.txt"; fi

# Fleet-level endpoints (unprefixed by a server profile) -- safe read-only checks only.
fleet_token="$cleanup_dir/fleet-api-token"
fleet_cfg="$cleanup_dir/fleet-auth.json"
if "$APP" api-token-create --token-file "$fleet_token" >"$REPORT_ROOT/fleet-token-create.txt" 2>&1; then
  jq --arg token "$fleet_token" '.SchemaVersion=3 | .Api.Port=18215 | .Api.BindAddress="127.0.0.1" | .Api.Authentication.Enabled=true | .Api.Authentication.TokenFile=$token | .Api.Tls.Enabled=false' "$REPORT_ROOT/config-effective.json" >"$fleet_cfg"
  fleet_runtime="$cleanup_dir/fleet-runtime"
  mkdir -p "$fleet_runtime"
  "$APP" api-run --config "$fleet_cfg" --runtime-root "$fleet_runtime" >"$REPORT_ROOT/fleet-api.log" 2>&1 &
  fleet_pid=$!
  for _ in {1..20}; do curl -s --max-time 1 http://127.0.0.1:18215/healthz >/dev/null 2>&1 && break; sleep .5; done
  fleet_bearer="$(tr -d '\r\n' < "$fleet_token")"
  fleet_base="http://127.0.0.1:18215"

  fleet_get_ok() {
    local name="$1" path="$2" out="$3"
    local code
    code="$(curl -s -o "$REPORT_ROOT/$out" -w '%{http_code}' --max-time 10 -H "Authorization: Bearer $fleet_bearer" "${fleet_base}${path}" || true)"
    [[ "$code" == "200" ]] && record PASS "$name" "HTTP 200" || record FAIL "$name" "HTTP $code"
  }
  fleet_get_ok "Fleet servers list endpoint" "/api/v1/servers" "fleet-servers.json"
  fleet_get_ok "Fleet security principals endpoint" "/api/v1/security/principals" "fleet-principals.json"
  fleet_get_ok "Port availability check endpoint" "/api/v1/diagnostics/port-check?port=18211&protocol=UDP" "fleet-port-check.json"

  doctor_all_code="$(curl -s -o "$REPORT_ROOT/fleet-doctor-all.json" -w '%{http_code}' --max-time 30 -H "Authorization: Bearer $fleet_bearer" -X POST "${fleet_base}/api/v1/fleet/doctor-all" || true)"
  [[ "$doctor_all_code" == "200" ]] && record PASS "Fleet doctor-all endpoint (read-only diagnostic)" "HTTP 200" || record FAIL "Fleet doctor-all endpoint" "HTTP $doctor_all_code"

  kill "$fleet_pid" 2>/dev/null || true; wait "$fleet_pid" 2>/dev/null || true
else
  record FAIL "Fleet-level temporary API token generation" "see fleet-token-create.txt"
fi

# Deliberately NOT exercised against this real, live production install -- each either mutates
# real save/world/guild/base/player data, real backups, real installed mods, real system network
# config, or triggers a real (possibly long-running) SteamCMD update, none of which is safe to run
# against production data from an automated acceptance pass. Covered structurally (compiles, wired
# to a route, present in the OpenAPI-equivalent surface) but not behaviorally here:
#   *-apply endpoints (world/import, guilds/ownership, bases/ownership, bases/recovery, pals/edit,
#   players/migration + disposition), players/{id}/action (kick/ban), players/{id}/teleport-*,
#   players/{id}/warnings (would leave permanent test noise in real player records),
#   players/{id}/flag and players/{id}/metadata PUT (need a real per-player id to round-trip),
#   backups/{fileName}/restore, backups/{fileName}/class, backups/retention/apply,
#   diagnostics/{id}/fix, diagnostics/network/firewall/repair, diagnostics/network/wan/upnp/repair,
#   mods/* mutation routes (install-zip/enabled/delete/rollback/repair/workshop-import),
#   server/clone, server/distribution/update, palworld/config PUT, config/editable PUT,
#   api/v1/servers POST/DELETE, api/v1/fleet/backup-all (non-destructive but a real, potentially
#   large/slow full save copy -- excluded for run-time boundedness, not safety), api/v1/fleet/
#   update-all, server/start|stop|force-stop|restart (already correctly gated by the existing
#   "Extended API lifecycle" guard above).

# Extended lifecycle test uses the installed local API only when it is plain loopback/no-auth.
if ((EXTENDED)); then
  auth_enabled="$(jq -r '.Api.Authentication.Enabled // .api.authentication.enabled // false' "$REPORT_ROOT/config-effective.json")"
  if [[ "$scheme" != "http" || "$auth_enabled" == "true" || "$bind" != "127.0.0.1" ]]; then
    record SKIP "Extended API lifecycle" "current API uses TLS/auth/non-default bind"
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
# Extended acceptance proves the current integration/Doctor layer as part
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
  echo "Skipped: $SKIP"
  echo "Report: $REPORT_ROOT"
  echo
  for row in "${RESULT_ROWS[@]}"; do IFS='|' read -r st name detail <<<"$row"; printf '[%s] %s%s\n' "$st" "$name" "${detail:+ :: $detail}"; done
} >"$SUMMARY"

printf '{\n  "version": "%s",\n  "passed": %d,\n  "failed": %d,\n  "warnings": %d,\n  "skipped": %d,\n  "report": "%s"\n}\n' "$VERSION" "$PASS" "$FAIL" "$WARN" "$SKIP" "$REPORT_ROOT" >"$JSON_SUMMARY"

echo
echo "================ FINAL SUMMARY ================"
cat "$SUMMARY"

((FAIL == 0))
