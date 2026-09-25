#!/usr/bin/env bash
# v0.8.20.0: an isolated MystTiq on the Linux test VM, run by Test-v0.8.20.0-LinuxIsolated.ps1. Own config, FleetRoot,
# runtime and port (18420); never touches /etc/mysttiq or the installed mysttiq-palworld service. Checks that the host
# history records on Linux (/proc readings), is kept in the fleet folder and survives a restart.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
APP="$ROOT/app/mysttiq-server"
PORT=18420
T="$ROOT/run"; mkdir -p "$T/runtime" "$T/backups" "$T/fleet" "$ROOT/fixture/server"
pass=0; fail=0
ok() { echo "[PASS] $1"; pass=$((pass+1)); }
bad() { echo "[FAIL] $1 -- $2"; fail=$((fail+1)); }
j() { python3 -c "import json,sys; d=json.load(sys.stdin); print($1)"; }

chmod +x "$APP"
"$APP" config-write-default --config "$T/mysttiq.json" --overwrite >/dev/null
python3 - "$T/mysttiq.json" "$T/fleet" <<'PY'
import json, sys
p, fleet = sys.argv[1], sys.argv[2]
c = json.load(open(p)); c["FleetRoot"] = fleet; json.dump(c, open(p, "w"), indent=2)
PY
start() {
  "$APP" api-run --desktop-sidecar --config "$T/mysttiq.json" --bind-address 127.0.0.1 --api-port $PORT \
    --server-root "$ROOT/fixture/server" --steamcmd "$T/missing-steamcmd" --backup-root "$T/backups" --runtime-root "$T/runtime" \
    >"$T/headless.log" 2>"$T/headless.err.log" &
  PID=$!
  for i in $(seq 1 40); do curl -sf "http://127.0.0.1:$PORT/healthz" >/dev/null && break; sleep 0.5; done
}
trap 'kill $PID 2>/dev/null; wait 2>/dev/null' EXIT
readings() { curl -sf "http://127.0.0.1:$PORT/api/v1/host/history?hours=1" | j 'd["readingsInRange"]'; }
start

V=$(curl -sf "http://127.0.0.1:$PORT/healthz" | j 'd.get("version")')
[[ "$V" == "0.8.20.0" ]] && ok "healthz reports this version" || bad "healthz" "version '$V'"
for i in $(seq 1 20); do [[ "$(readings)" -ge 1 ]] && break; sleep 0.5; done
R=$(curl -sf "http://127.0.0.1:$PORT/api/v1/host/history?hours=1" | j 'str(d["readingsInRange"])+" "+str(0<=d["samples"][0]["cpuPercent"]<=100)+" "+str(0<d["samples"][0]["memoryUsedPercent"]<=100)')
[[ "$R" == "1 True True" ]] && ok "a reading is taken at start from /proc (processor and memory in range)" || bad "first reading" "$R"
[[ -f "$T/fleet/host/history.json" ]] && ok "the history is kept in the fleet folder" || bad "history file" "missing"
kill $PID 2>/dev/null; wait $PID 2>/dev/null
start
for i in $(seq 1 20); do [[ "$(readings)" -ge 2 ]] && break; sleep 0.5; done
[[ "$(readings)" -ge 2 ]] && ok "the history survives a restart and a new reading is added" || bad "after restart" "$(readings)"

echo "linux-isolated: $pass passed, $fail failed"
[[ $fail -eq 0 ]]
