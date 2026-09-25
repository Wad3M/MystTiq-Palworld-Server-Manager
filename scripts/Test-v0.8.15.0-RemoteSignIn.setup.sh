#!/usr/bin/env bash
# v0.8.15.0: starts an isolated, remote-enabled MystTiq on the Linux test VM for Test-v0.8.15.0-RemoteSignIn.ps1.
# Everything lives in this script's own folder: its own config, FleetRoot, runtime, bearer token, self-signed TLS
# certificate and port. /etc/mysttiq and the installed mysttiq-palworld service are never touched.
#   setup.sh <bind address> <port>      (accounts.txt beside it: "<role number> <username> <password>" per line)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
BIND="$1"; PORT="$2"
APP="$ROOT/app/mysttiq-server"; T="$ROOT/run"
mkdir -p "$T/runtime" "$T/backups" "$T/fleet" "$ROOT/server"
chmod +x "$APP"
"$APP" config-write-default --config "$T/mysttiq.json" --overwrite >/dev/null
python3 - "$T/mysttiq.json" "$T/fleet" <<'PY'
import json, sys
p, fleet = sys.argv[1], sys.argv[2]
c = json.load(open(p)); c["FleetRoot"] = fleet; json.dump(c, open(p, "w"), indent=2)
PY
"$APP" api-token-create --token-file "$T/token" --overwrite >/dev/null
"$APP" api-tls-create --certificate-file "$T/cert.pfx" --certificate-password-file "$T/cert.pw" --bind-address "$BIND" --overwrite >/dev/null
"$APP" api-remote-enable --config "$T/mysttiq.json" --bind-address "$BIND" --api-port "$PORT" --token-file "$T/token" \
  --certificate-file "$T/cert.pfx" --certificate-password-file "$T/cert.pw" >/dev/null
nohup "$APP" api-run --config "$T/mysttiq.json" --server-root "$ROOT/server" --steamcmd "$T/missing-steamcmd" \
  --backup-root "$T/backups" --runtime-root "$T/runtime" >"$T/api.log" 2>&1 &
echo $! > "$T/pid"
for i in $(seq 1 60); do curl -sfk "https://$BIND:$PORT/healthz" >/dev/null && break; sleep 0.5; done
curl -sfk "https://$BIND:$PORT/healthz" >/dev/null || { echo "NOT-HEALTHY"; tail -20 "$T/api.log"; exit 1; }
TOKEN="$(cat "$T/token")"
while read -r ROLE USER PASS; do
  [[ -z "$ROLE" ]] && continue
  BODY=$(python3 -c 'import json,sys; print(json.dumps({"username":sys.argv[2],"displayName":sys.argv[2],"role":int(sys.argv[1]),"password":sys.argv[3],"scopedServerProfileId":None}))' "$ROLE" "$USER" "$PASS")
  curl -sfk -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "$BODY" "https://$BIND:$PORT/api/v1/security/users" >/dev/null \
    && echo "USER-CREATED $USER" || echo "USER-FAILED $USER"
done < "$ROOT/accounts.txt"
PIN=$(openssl pkcs12 -in "$T/cert.pfx" -nokeys -passin "file:$T/cert.pw" 2>/dev/null | openssl x509 -outform der | sha256sum | cut -d' ' -f1)
echo "PIN=$PIN"
echo "READY"
