#!/usr/bin/env bash
set -Eeuo pipefail

VERSION="0.3.0.7"

if [[ -x "$(pwd)/mysttiq-server" ]]; then
  APP="$(pwd)/mysttiq-server"
else
  APP="/opt/mysttiq/bin/mysttiq-server"
fi

CONFIG="/etc/mysttiq/mysttiq.json"
SERVICE="mysttiq-palworld"
SERVICE_USER="${USER}"
BIND=""
PORT=8213
DNS_NAME="$(hostname)"
TOKEN_FILE="/etc/mysttiq/secrets/api-token"
CERT_FILE="/etc/mysttiq/certs/mysttiq.pfx"
CERT_PASSWORD_FILE="/etc/mysttiq/secrets/certificate-password"
BACKUP_FILE=""
ROTATE=0
COMMITTED=0

usage() {
  cat <<EOF
MystTiq ${VERSION} secure remote API enrollment

Usage:
  bash ./scripts/Configure-MystTiqRemoteApi.sh --bind <LAN-IP> [options]

Options:
  --bind <ip>             Required non-loopback IPv4/IPv6 address.
  --port <n>              API port (default: 8213).
  --dns-name <name>       Optional certificate DNS SAN (default: hostname).
  --service-user <user>   systemd service account (default: current user).
  --config <path>         Config path (default: /etc/mysttiq/mysttiq.json).
  --app <path>            mysttiq-server executable.
  --rotate                Replace token/certificate/password secrets.
  -h, --help              Show help.

Remote exposure is explicit. Authentication and TLS are mandatory.
Firewall rules are never changed automatically.
EOF
}

stage() { echo; echo "==> $1"; }
pass()  { echo "[PASS] $1"; }
fail()  { echo "[FAIL] $1" >&2; exit 1; }

cleanup_tmp() {
  rm -f /tmp/mysttiq-healthz.json /tmp/mysttiq-status.json 2>/dev/null || true
}

rollback() {
  local code=$?
  cleanup_tmp

  if (( code != 0 && COMMITTED == 0 )); then
    echo
    echo "[WARN] Enrollment failed before commit."

    if [[ -n "${BACKUP_FILE}" && -f "${BACKUP_FILE}" ]]; then
      echo "       Restoring previous MystTiq configuration."
      sudo cp "${BACKUP_FILE}" "${CONFIG}" || true
      sudo chmod 644 "${CONFIG}" || true
      sudo systemctl restart "${SERVICE}" || true
    fi
  fi

  exit "$code"
}
trap rollback EXIT

while (($#)); do
  case "$1" in
    --bind) BIND="$2"; shift 2 ;;
    --port) PORT="$2"; shift 2 ;;
    --dns-name) DNS_NAME="$2"; shift 2 ;;
    --service-user) SERVICE_USER="$2"; shift 2 ;;
    --config) CONFIG="$2"; shift 2 ;;
    --app) APP="$2"; shift 2 ;;
    --rotate) ROTATE=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage; exit 2 ;;
  esac
done

[[ -n "$BIND" ]] || fail "--bind <LAN-IP> is required."
[[ -x "$APP" ]] || fail "MystTiq executable is not executable: $APP"
[[ "$PORT" =~ ^[0-9]+$ ]] || fail "--port must be numeric."

echo "============================================================"
echo "MystTiq ${VERSION} Secure Remote API Enrollment"
echo "Bind:         ${BIND}:${PORT}"
echo "DNS SAN:      ${DNS_NAME}"
echo "Service user: ${SERVICE_USER}"
echo "Config:       ${CONFIG}"
echo "============================================================"
echo
echo "This intentionally enables authenticated HTTPS access on the selected LAN address."
read -r -p "Continue? (y/N): " answer
[[ "$answer" =~ ^[Yy]$ ]] || { echo "Cancelled."; exit 0; }

stage "Authorizing administrative changes"
sudo -v
pass "sudo authorization accepted"

stage "Verifying current MystTiq binary and configuration support"
"$APP" --help >/tmp/mysttiq-help.txt 2>&1 || true
grep -q "api-tls-create" /tmp/mysttiq-help.txt ||
  fail "This installed/extracted mysttiq-server does not contain the v0.3.0.7 remote API commands."
grep -q "api-remote-enable" /tmp/mysttiq-help.txt ||
  fail "api-remote-enable command is missing from this mysttiq-server build."
pass "Remote enrollment commands are present"

stage "Preparing protected directories"
sudo install -d -m 0755 /etc/mysttiq /etc/mysttiq/certs
sudo install -d -m 0700 -o "$SERVICE_USER" -g "$SERVICE_USER" /etc/mysttiq/secrets

secrets_mode="$(stat -c '%a' /etc/mysttiq/secrets)"
secrets_owner="$(stat -c '%U:%G' /etc/mysttiq/secrets)"

[[ "$secrets_mode" == "700" ]] ||
  fail "Secrets directory mode is $secrets_mode; expected 700."
[[ "$secrets_owner" == "${SERVICE_USER}:${SERVICE_USER}" ]] ||
  fail "Secrets directory owner is $secrets_owner; expected ${SERVICE_USER}:${SERVICE_USER}."

pass "Protected directories prepared; secrets owner=${secrets_owner}, mode=${secrets_mode}"

stage "Backing up current configuration"
if [[ -f "$CONFIG" ]]; then
  BACKUP_FILE="${CONFIG}.pre-remote-$(date -u +%Y%m%d-%H%M%S).bak"
  sudo cp "$CONFIG" "$BACKUP_FILE"
  pass "Configuration backup created: $BACKUP_FILE"
else
  sudo "$APP" config-write-default --config "$CONFIG"
  BACKUP_FILE="${CONFIG}.pre-remote-$(date -u +%Y%m%d-%H%M%S).bak"
  sudo cp "$CONFIG" "$BACKUP_FILE"
  pass "Default configuration created and backed up"
fi

stage "Migrating configuration schema if required"
if sudo "$APP" config-migrate --config "$CONFIG"; then
  :
fi
sudo "$APP" config-validate --config "$CONFIG" ||
  fail "Configuration is invalid before remote enrollment."
pass "Configuration is valid"

stage "Creating or verifying bearer token"
if ((ROTATE)) || ! sudo test -s "$TOKEN_FILE"; then
  token_args=(api-token-create --token-file "$TOKEN_FILE")
  ((ROTATE)) && token_args+=(--overwrite)
  sudo "$APP" "${token_args[@]}" ||
    fail "Bearer token generation failed."
fi

sudo test -s "$TOKEN_FILE" || fail "Bearer token file was not created: $TOKEN_FILE"
sudo chown "${SERVICE_USER}:${SERVICE_USER}" "$TOKEN_FILE"
sudo chmod 600 "$TOKEN_FILE"
token_mode="$(stat -c '%a' "$TOKEN_FILE")"
token_owner="$(stat -c '%U:%G' "$TOKEN_FILE")"
[[ "$token_mode" == "600" ]] || fail "Bearer token permissions are $token_mode; expected 600."
[[ "$token_owner" == "${SERVICE_USER}:${SERVICE_USER}" ]] ||
  fail "Bearer token owner is $token_owner; expected ${SERVICE_USER}:${SERVICE_USER}."
pass "Bearer token exists with owner ${token_owner} and mode ${token_mode}"

stage "Creating or verifying TLS certificate"
need_certificate=0
sudo test -s "$CERT_FILE" || need_certificate=1
sudo test -s "$CERT_PASSWORD_FILE" || need_certificate=1
((ROTATE)) && need_certificate=1

if ((need_certificate)); then
  cert_args=(
    api-tls-create
    --bind-address "$BIND"
    --dns-name "$DNS_NAME"
    --certificate-file "$CERT_FILE"
    --certificate-password-file "$CERT_PASSWORD_FILE"
  )
  ((ROTATE)) && cert_args+=(--overwrite)

  sudo "$APP" "${cert_args[@]}" ||
    fail "TLS certificate generation failed."
fi

sudo test -s "$CERT_FILE" || fail "TLS certificate was not created: $CERT_FILE"
sudo test -s "$CERT_PASSWORD_FILE" ||
  fail "TLS certificate password file was not created: $CERT_PASSWORD_FILE"

sudo chown "${SERVICE_USER}:${SERVICE_USER}" "$CERT_FILE" "$CERT_PASSWORD_FILE"
sudo chmod 600 "$CERT_FILE" "$CERT_PASSWORD_FILE"

cert_mode="$(stat -c '%a' "$CERT_FILE")"
cert_owner="$(stat -c '%U:%G' "$CERT_FILE")"
password_mode="$(stat -c '%a' "$CERT_PASSWORD_FILE")"
password_owner="$(stat -c '%U:%G' "$CERT_PASSWORD_FILE")"

[[ "$cert_mode" == "600" ]] || fail "TLS certificate permissions are $cert_mode; expected 600."
[[ "$password_mode" == "600" ]] || fail "TLS password permissions are $password_mode; expected 600."
[[ "$cert_owner" == "${SERVICE_USER}:${SERVICE_USER}" ]] ||
  fail "TLS certificate owner is $cert_owner; expected ${SERVICE_USER}:${SERVICE_USER}."
[[ "$password_owner" == "${SERVICE_USER}:${SERVICE_USER}" ]] ||
  fail "TLS password owner is $password_owner; expected ${SERVICE_USER}:${SERVICE_USER}."

pass "TLS certificate and password secret exist with correct ownership/permissions"

stage "Writing secured remote configuration"
sudo "$APP" api-remote-enable \
  --config "$CONFIG" \
  --bind-address "$BIND" \
  --api-port "$PORT" \
  --token-file "$TOKEN_FILE" \
  --certificate-file "$CERT_FILE" \
  --certificate-password-file "$CERT_PASSWORD_FILE" ||
  fail "api-remote-enable failed."

sudo "$APP" config-validate --config "$CONFIG" ||
  fail "Remote API configuration failed validation after write."

effective="$(sudo "$APP" config-show --config "$CONFIG")"
printf '%s\n' "$effective" >/tmp/mysttiq-effective-config.json

config_bind="$(printf '%s\n' "$effective" | jq -r '.Api.BindAddress // .api.bindAddress // empty')"
config_auth="$(printf '%s\n' "$effective" | jq -r '.Api.Authentication.Enabled // .api.authentication.enabled // false')"
config_tls="$(printf '%s\n' "$effective" | jq -r '.Api.Tls.Enabled // .api.tls.enabled // false')"

[[ "$config_bind" == "$BIND" ]] || fail "Effective bind is $config_bind; expected $BIND."
[[ "$config_auth" == "true" ]] || fail "Effective authentication is not enabled."
[[ "$config_tls" == "true" ]] || fail "Effective TLS is not enabled."
pass "Effective config is bound to $BIND with authentication + TLS"

stage "Installing/restarting current MystTiq build under systemd"
sudo "$APP" service-install \
  --config "$CONFIG" \
  --service-user "$SERVICE_USER" \
  --start-now ||
  fail "service-install failed."

sudo systemd-analyze verify "/etc/systemd/system/${SERVICE}.service" ||
  fail "systemd unit verification failed."

sleep 5
systemctl is-active --quiet "$SERVICE" ||
  fail "MystTiq systemd service is not active after remote enrollment."
pass "MystTiq systemd service is active"

stage "Verifying HTTPS listener"
listener_ok=0
for _ in {1..30}; do
  if sudo ss -lntp | grep -F "${BIND}:${PORT}" >/tmp/mysttiq-listener.txt 2>/dev/null; then
    listener_ok=1
    break
  fi
  sleep 1
done

if ((listener_ok == 0)); then
  echo "Current listeners:"
  sudo ss -lntp | grep ":${PORT}" || true
  echo
  journalctl -u "$SERVICE" -n 80 --no-pager || true
  fail "MystTiq never opened ${BIND}:${PORT}."
fi
pass "Listener active on ${BIND}:${PORT}"

stage "Verifying HTTPS health and bearer authentication"
health_ready=0
for _ in {1..20}; do
  if curl -ksS --max-time 2 "https://${BIND}:${PORT}/healthz" \
      >/tmp/mysttiq-healthz.json 2>/dev/null; then
    health_ready=1
    break
  fi
  sleep 1
done
((health_ready)) || fail "HTTPS health endpoint did not become reachable."

health_status="$(jq -r '.status // empty' /tmp/mysttiq-healthz.json)"
[[ "$health_status" == "ok" ]] || fail "HTTPS health payload did not report status=ok."
pass "HTTPS health endpoint returned status=ok"

token="$(tr -d '\r\n' < "$TOKEN_FILE")"
[[ -n "$token" ]] || fail "Bearer token file is empty."

unauth_code="$(curl -ksS -o /tmp/mysttiq-unauth.json -w '%{http_code}' \
  "https://${BIND}:${PORT}/api/v1/status")"
[[ "$unauth_code" == "401" ]] ||
  fail "Unauthenticated management request returned HTTP ${unauth_code}; expected 401."
pass "Unauthenticated management request returned HTTP 401"

auth_code="$(curl -ksS -o /tmp/mysttiq-status.json -w '%{http_code}' \
  -H "Authorization: Bearer ${token}" \
  "https://${BIND}:${PORT}/api/v1/status")"
[[ "$auth_code" == "200" ]] ||
  fail "Authenticated status request returned HTTP ${auth_code}; expected 200."
pass "Bearer-authenticated status endpoint returned HTTP 200"

COMMITTED=1
cleanup_tmp
trap - EXIT

echo
echo "================ ENROLLMENT PASS ================"
echo "Remote URL: https://${BIND}:${PORT}"
echo "Auth:       Bearer token"
echo "TLS:        Enabled"
echo "Config:     ${CONFIG}"
echo "Backup:     ${BACKUP_FILE}"
echo
echo "MystTiq did not change firewall rules."
echo
echo "Next from Windows:"
echo "  .\\scripts\\Test-MystTiqRemoteApi.ps1"
