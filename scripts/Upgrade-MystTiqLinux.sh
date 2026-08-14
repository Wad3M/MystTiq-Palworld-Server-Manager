#!/usr/bin/env bash
set -euo pipefail
VERSION="0.3.0.7"
APP="${MYSTTIQ_APP:-$(pwd)/mysttiq-server}"
CONFIG="/etc/mysttiq/mysttiq.json"
SERVICE_USER="${USER}"
while (($#)); do
  case "$1" in --service-user) SERVICE_USER="$2"; shift 2;; *) echo "Unknown argument: $1" >&2; exit 2;; esac
done
[[ -x "$APP" ]] || { echo "[FAIL] mysttiq-server not found/executable: $APP"; exit 1; }
sudo -v
stamp="$(date -u +%Y%m%d-%H%M%S)"
backup="/etc/mysttiq/mysttiq.json.pre-upgrade-${VERSION}-${stamp}.bak"
[[ -f "$CONFIG" ]] && sudo cp -a "$CONFIG" "$backup"
echo "[PASS] Configuration backup: $backup"
sudo "$APP" config-migrate --config "$CONFIG" || true
sudo "$APP" config-validate --config "$CONFIG"
sudo "$APP" service-install --service-user "$SERVICE_USER" --config "$CONFIG" --start-now
sleep 5
systemctl is-active mysttiq-palworld >/dev/null
echo "[PASS] v${VERSION} installed and systemd is active."
echo "Preserved: configuration, secrets/TLS, PalServer, saves, backups, runtime data."
echo "Next: bash ./scripts/Test-v${VERSION}-ProductionReadiness.sh"
