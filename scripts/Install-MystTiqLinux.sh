#!/usr/bin/env bash
set -euo pipefail
VERSION="0.3.0.7"
APP="${MYSTTIQ_APP:-$(pwd)/mysttiq-server}"
CONFIG="/etc/mysttiq/mysttiq.json"
SERVICE_USER="${USER}"
START_NOW=1
while (($#)); do
  case "$1" in
    --service-user) SERVICE_USER="$2"; shift 2;;
    --no-start) START_NOW=0; shift;;
    *) echo "Unknown argument: $1" >&2; exit 2;;
  esac
done
[[ -x "$APP" ]] || { echo "[FAIL] mysttiq-server not found/executable: $APP"; exit 1; }
echo "MystTiq v${VERSION} Linux first-run setup"
echo "Service user: $SERVICE_USER"
sudo -v
for cmd in jq curl systemctl tar sha256sum; do command -v "$cmd" >/dev/null || { echo "[FAIL] Required command missing: $cmd"; exit 1; }; done
sudo install -d -m 0755 /opt/mysttiq /opt/mysttiq/bin /etc/mysttiq
sudo install -d -m 0755 -o "$SERVICE_USER" -g "$SERVICE_USER" /opt/mysttiq/backups /opt/mysttiq/runtime
if [[ ! -f "$CONFIG" ]]; then
  sudo "$APP" config-write-default --config "$CONFIG"
  echo "[PASS] Default configuration created."
else
  echo "[PASS] Existing configuration preserved."
fi
sudo "$APP" config-migrate --config "$CONFIG" || true
sudo "$APP" config-validate --config "$CONFIG"
args=(service-install --service-user "$SERVICE_USER" --config "$CONFIG")
((START_NOW)) && args+=(--start-now)
sudo "$APP" "${args[@]}"
echo "[PASS] First-run setup completed."
echo "Next: bash ./scripts/Test-v${VERSION}-ProductionReadiness.sh"
