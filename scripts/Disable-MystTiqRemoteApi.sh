#!/usr/bin/env bash
set -euo pipefail

if [[ -x "$(pwd)/mysttiq-server" ]]; then
  APP="$(pwd)/mysttiq-server"
else
  APP="/opt/mysttiq/bin/mysttiq-server"
fi
CONFIG="/etc/mysttiq/mysttiq.json"
SERVICE_USER="${USER}"

while (($#)); do
  case "$1" in
    --app) APP="$2"; shift 2 ;;
    --config) CONFIG="$2"; shift 2 ;;
    --service-user) SERVICE_USER="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: bash ./scripts/Disable-MystTiqRemoteApi.sh [--service-user user] [--config path] [--app path]"
      exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

sudo -v
sudo "$APP" api-remote-disable --config "$CONFIG"
sudo "$APP" service-install --config "$CONFIG" --service-user "$SERVICE_USER" --start-now
sleep 5

echo "MystTiq API returned to the safe loopback configuration."
"$APP" config-show --config "$CONFIG"
