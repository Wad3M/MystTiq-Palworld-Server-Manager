#!/usr/bin/env bash
# v0.8.24.0: processor cores (affinity) on Linux, run by Test-v0.8.24.0-LinuxIsolated.ps1 on the test VM. An isolated
# MystTiq (own config, FleetRoot, runtime, port 18425) over a synthetic server folder; never touches /etc/mysttiq or the
# installed mysttiq-palworld service. The stand-in server is a copy of python3 named PalServer-Linux-Shipping with extra
# threads, because Linux affinity, like niceness, is per thread: every thread must follow.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
APP="$ROOT/app/mysttiq-server"
PORT=18425
T="$ROOT/run"; mkdir -p "$T/runtime" "$T/backups" "$T/fleet"
SERVER="$ROOT/fixture/server"
FAKE="$SERVER/Pal/Binaries/Linux/PalServer-Linux-Shipping"
pass=0; fail=0
ok() { echo "[PASS] $1"; pass=$((pass+1)); }
bad() { echo "[FAIL] $1 -- $2"; fail=$((fail+1)); }
j() { python3 -c "import json,sys; d=json.load(sys.stdin); print($1)"; }

CPUS=$(nproc --all)
ALL="0-$((CPUS-1))"; [[ $CPUS -eq 1 ]] && ALL="0"
mkdir -p "$(dirname "$FAKE")"
cp "$(readlink -f "$(command -v python3)")" "$FAKE"; chmod +x "$FAKE"
"$FAKE" -c "import threading,time
for _ in range(3): threading.Thread(target=time.sleep, args=(900,), daemon=True).start()
time.sleep(900)" >/dev/null 2>&1 &
FAKEPID=$!
sleep 1
# The distinct allowed-core lists over all threads, and how many threads there are.
thread_cores() { cat /proc/$FAKEPID/task/*/status | awk '/^Cpus_allowed_list:/{print $2}' | sort -u | tr '\n' ' ' | sed 's/ $//'; }
thread_count() { ls /proc/$FAKEPID/task | wc -l | tr -d ' '; }

chmod +x "$APP"
"$APP" config-write-default --config "$T/mysttiq.json" --overwrite >/dev/null
"$APP" api-run --desktop-sidecar --config "$T/mysttiq.json" --fleet-root "$T/fleet" --bind-address 127.0.0.1 --api-port $PORT \
  --server-root "$SERVER" --steamcmd "$T/missing-steamcmd" --backup-root "$T/backups" --runtime-root "$T/runtime" \
  >"$T/headless.log" 2>"$T/headless.err.log" &
PID=$!
trap 'kill $PID 2>/dev/null; kill $FAKEPID 2>/dev/null; wait 2>/dev/null' EXIT
for i in $(seq 1 40); do curl -sf "http://127.0.0.1:$PORT/healthz" >/dev/null && break; sleep 0.5; done
put() { curl -s -o "$T/put.json" -w '%{http_code}' -X PUT -H 'Content-Type: application/json' -d "$1" "http://127.0.0.1:$PORT/api/v1/resources/policy"; }

V=$(curl -sf "http://127.0.0.1:$PORT/healthz" | j 'd.get("version")')
# The version the wrapper was built from (it passes it), so this check still holds as a regression in later versions.
EXPECTED="${1:-0.8.24.0}"
[[ "$V" == "$EXPECTED" ]] && ok "healthz reports this version ($EXPECTED)" || bad "healthz" "version '$V' $(tail -3 "$T/headless.err.log")"

[[ "$(thread_count)" -ge 4 && "$(thread_cores)" == "$ALL" ]] && ok "the stand-in runs $(thread_count) threads on every core ($ALL)" || bad "baseline" "threads $(thread_count), cores '$(thread_cores)'"
H=$(curl -sf --max-time 20 "http://127.0.0.1:$PORT/api/v1/host" | j 'str(d["resources"]["processorCount"])+" "+d["resources"]["processes"][0]["cores"]')
[[ "$H" == "$CPUS all $CPUS" ]] && ok "the HOST route reports $CPUS cores and the server on all of them" || bad "host route" "'$H'"

if [[ $CPUS -ge 2 ]]; then
  C=$(put '{"priority":"Default","ecoMode":"Off","ecoAfterEmptyMinutes":10,"cores":"1"}')
  S=$(j 'str(d["success"])+" "+d["snapshot"]["processes"][0]["cores"]+" "+repr(d["snapshot"]["lastError"])' < "$T/put.json")
  [[ "$C" == "200" && "$S" == "True 1 ''" && "$(thread_cores)" == "1" ]] && ok "cores 1 pins every thread to core 1 ($(thread_count) threads)" || bad "pin" "$C $S, cores '$(thread_cores)'"
  C=$(put '{"priority":"Default","ecoMode":"Off","ecoAfterEmptyMinutes":10,"cores":""}')
  [[ "$C" == "200" && "$(thread_cores)" == "$ALL" ]] && ok "clearing the list gives every thread every core back, without any privilege" || bad "unpin" "$C, cores '$(thread_cores)'"
else
  echo "[SKIP] pinning needs at least two cores (this VM has $CPUS)"
fi
C=$(put "{\"priority\":\"Default\",\"ecoMode\":\"Off\",\"ecoAfterEmptyMinutes\":10,\"cores\":\"0-$((CPUS+5))\"}")
[[ "$C" == "400" && "$(thread_cores)" == "$ALL" ]] && ok "cores this machine does not have are refused (400) and nothing changes" || bad "refusal" "$C, cores '$(thread_cores)'"

echo "linux-isolated: $pass passed, $fail failed"
[[ $fail -eq 0 ]]
