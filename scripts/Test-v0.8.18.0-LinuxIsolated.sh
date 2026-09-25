#!/usr/bin/env bash
# v0.8.18.0: an isolated MystTiq on the Linux test VM, run by Test-v0.8.18.0-LinuxIsolated.ps1. Own config, FleetRoot,
# runtime and port (18419) over a synthetic server folder; never touches /etc/mysttiq or the installed
# mysttiq-palworld service. MystTiq starts and stops the stand-in server itself (a PalServer.sh that runs a copy of
# python3 named PalServer-Linux-Shipping), so the bandwidth write happens on the real Linux start path.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
APP="$ROOT/app/mysttiq-server"
PORT=18419
T="$ROOT/run"; mkdir -p "$T/runtime" "$T/backups" "$T/fleet"
SERVER="$ROOT/fixture/server"
CONF="$SERVER/Pal/Saved/Config/LinuxServer"
FAKE="$SERVER/Pal/Binaries/Linux/PalServer-Linux-Shipping"
pass=0; fail=0
ok() { echo "[PASS] $1"; pass=$((pass+1)); }
bad() { echo "[FAIL] $1 -- $2"; fail=$((fail+1)); }
j() { python3 -c "import json,sys; d=json.load(sys.stdin); print($1)"; }

mkdir -p "$(dirname "$FAKE")" "$CONF"
cp "$(readlink -f "$(command -v python3)")" "$FAKE"; chmod +x "$FAKE"
cat > "$SERVER/PalServer.sh" <<'SH'
#!/usr/bin/env bash
exec "$(dirname "$0")/Pal/Binaries/Linux/PalServer-Linux-Shipping" -c "import time; time.sleep(900)"
SH
chmod +x "$SERVER/PalServer.sh"
printf '[/Script/Pal.PalGameWorldSettings]\nOptionSettings=(ServerName="Linux Bandwidth",ServerPlayerMaxNum=8,PublicPort=18519,RESTAPIEnabled=False,RCONEnabled=False,AdminPassword="a-long-random-admin-secret")\n' > "$CONF/PalWorldSettings.ini"
ORIGINAL=$'[Core.System]\nPaths=../../../Engine/Content\nPaths=%GAMEDIR%Content\n'
printf '%s' "$ORIGINAL" > "$CONF/Engine.ini"

chmod +x "$APP"
"$APP" config-write-default --config "$T/mysttiq.json" --overwrite >/dev/null
python3 - "$T/mysttiq.json" "$T/fleet" <<'PY'
import json, sys
p, fleet = sys.argv[1], sys.argv[2]
c = json.load(open(p)); c["FleetRoot"] = fleet; c["Lifecycle"]["StartupTimeoutSeconds"] = 4; c["Lifecycle"]["StopTimeoutSeconds"] = 5
json.dump(c, open(p, "w"), indent=2)
PY
"$APP" api-run --desktop-sidecar --config "$T/mysttiq.json" --bind-address 127.0.0.1 --api-port $PORT \
  --server-root "$SERVER" --steamcmd "$T/missing-steamcmd" --backup-root "$T/backups" --runtime-root "$T/runtime" \
  >"$T/headless.log" 2>"$T/headless.err.log" &
PID=$!
cleanup() { curl -s -X POST "http://127.0.0.1:$PORT/api/v1/server/stop" >/dev/null 2>&1; kill $PID 2>/dev/null; pkill -f "$FAKE" 2>/dev/null; wait 2>/dev/null; }
trap cleanup EXIT
for i in $(seq 1 40); do curl -sf "http://127.0.0.1:$PORT/healthz" >/dev/null && break; sleep 0.5; done
put() { curl -s -X PUT -H 'Content-Type: application/json' -d "$1" "http://127.0.0.1:$PORT/api/v1/network/policy"; }
bw() { curl -sf "http://127.0.0.1:$PORT/api/v1/network/policy"; }
running() { pgrep -f "$FAKE" >/dev/null; }

V=$(curl -sf "http://127.0.0.1:$PORT/healthz" | j 'd.get("version")')
[[ "$V" == "0.8.18.0" ]] && ok "healthz reports this version" || bad "healthz" "version '$V' $(tail -3 "$T/headless.err.log")"

D=$(bw | j 'd["platform"]+" "+str(d["gameDefaultTickRate"])+" "+str(d["gameDefaultPerPlayerMbps"])+" "+str(d["maxPlayers"])')
[[ "$D" == "Linux 20 64 8" ]] && ok "Linux shows the game's Linux defaults (20 updates per second, 64 Mbit/s per player) and 8 players" || bad "defaults" "$D"

S=$(put '{"mode":"Custom","perPlayerMbps":1.5,"tickRate":30}' | j 'd["message"]')
grep -q 'MaxClientRate=187500' "$CONF/Engine.ini" && grep -q 'NetServerMaxTickRate=30' "$CONF/Engine.ini" && grep -q '^Paths=%GAMEDIR%Content' "$CONF/Engine.ini" \
  && ok "saved while stopped: written to LinuxServer/Engine.ini at once, the engine's lines kept ($S)" || bad "write while stopped" "$S / $(cat "$CONF/Engine.ini")"
[[ -f "$CONF/Engine.ini.mysttiq-original" ]] && ok "the original Engine.ini is kept" || bad "original" "missing"

curl -s -X POST "http://127.0.0.1:$PORT/api/v1/server/start" >/dev/null
for i in $(seq 1 30); do running && break; sleep 0.5; done
running && ok "MystTiq starts the stand-in through PalServer.sh" || bad "start" "$(tail -5 "$T/headless.err.log")"

R=$(put '{"mode":"Custom","perPlayerMbps":3,"tickRate":40}' | j 'str(d["snapshot"]["restartNeeded"])+" "+d["message"]')
[[ "$R" == True*"next start"* ]] && grep -q 'MaxClientRate=187500' "$CONF/Engine.ini" && ok "saved while running: waits for the next start and leaves Engine.ini alone" || bad "save while running" "$R"

curl -s -X POST "http://127.0.0.1:$PORT/api/v1/server/stop" >/dev/null
for i in $(seq 1 30); do running || break; sleep 0.5; done
curl -s -X POST "http://127.0.0.1:$PORT/api/v1/server/start" >/dev/null
for i in $(seq 1 30); do running && break; sleep 0.5; done
LOG=$(find "$SERVER" "$T" -name 'MystTiq-PalServer-Console.log' 2>/dev/null | head -1)
grep -q 'MaxClientRate=375000' "$CONF/Engine.ini" && grep -q 'NetServerMaxTickRate=40' "$CONF/Engine.ini" && grep -q 'Bandwidth: Engine.ini set to 3 Mbit/s' "$LOG" \
  && ok "the Linux start path writes the new limits before the server starts, and logs it" || bad "write on start" "$(grep -E 'Rate' "$CONF/Engine.ini" | tr '\n' ' ') / log: $(grep -c Bandwidth "$LOG" 2>/dev/null)"
N=$(bw | j 'str(d["restartNeeded"])+" "+str(d["effectivePerPlayerMbps"])+" "+str(d["effectiveTickRate"])')
[[ "$N" == "False 3 40" ]] && ok "after the start nothing is pending and the server runs at 3 Mbit/s per player, 40 updates" || bad "after start" "$N"

echo "linux-isolated: $pass passed, $fail failed"
[[ $fail -eq 0 ]]
