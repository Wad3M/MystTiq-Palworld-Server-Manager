#!/usr/bin/env bash
# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
# v1.0.3.0 (roadmap P-1): the container's start. On the first start with an empty /data it writes a configuration whose
# every path is under /data, an API bearer token and a self-signed TLS certificate, and switches the API on for remote
# use (all addresses, port 8213): outside its container the API is reached through the published port, which needs
# authentication and TLS like every non-loopback MystTiq. Later starts keep what /data holds. Then it runs the service.
set -euo pipefail
APP=/opt/mysttiq/bin/mysttiq-server
DATA="${MYSTTIQ_DATA:-/data}"
CONFIG="$DATA/mysttiq.json"

if [[ ! -f "$CONFIG" ]]; then
  mkdir -p "$DATA/server" "$DATA/steamcmd" "$DATA/backups" "$DATA/runtime" "$DATA/fleet" "$DATA/secrets" "$DATA/certs"
  "$APP" config-write-default --config "$CONFIG" --overwrite >/dev/null
  python3 - "$DATA" <<'PY'
import json, sys
data = sys.argv[1]
path = data + "/mysttiq.json"
c = json.load(open(path))
c["FleetRoot"] = data + "/fleet"
c["Servers"][0].update({"ServerRoot": data + "/server", "SteamCmdPath": data + "/steamcmd/steamcmd.sh",
                        "BackupRoot": data + "/backups", "RuntimeRoot": data + "/runtime", "Runtime": "LinuxNative"})
json.dump(c, open(path, "w"), indent=2)
PY
  "$APP" api-token-create --token-file "$DATA/secrets/api-token" >/dev/null
  "$APP" api-tls-create --certificate-file "$DATA/certs/mysttiq.pfx" --certificate-password-file "$DATA/secrets/certificate-password" \
    --bind-address 0.0.0.0 ${MYSTTIQ_DNS_NAME:+--dns-name "$MYSTTIQ_DNS_NAME"} >/dev/null
  "$APP" api-remote-enable --config "$CONFIG" --bind-address 0.0.0.0 --api-port 8213 --token-file "$DATA/secrets/api-token" \
    --certificate-file "$DATA/certs/mysttiq.pfx" --certificate-password-file "$DATA/secrets/certificate-password" >/dev/null
  echo "MystTiq: first start. Created $CONFIG, an API token ($DATA/secrets/api-token) and a TLS certificate ($DATA/certs/mysttiq.pfx)."
fi

exec "$APP" api-run --config "$CONFIG"
