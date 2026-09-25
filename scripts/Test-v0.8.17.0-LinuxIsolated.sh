#!/usr/bin/env bash
# v0.8.17.0: an isolated MystTiq on the Linux test VM, run by Test-v0.8.17.0-LinuxIsolated.ps1. Own config, FleetRoot,
# runtime and port (18418) over a synthetic server folder; never touches /etc/mysttiq or the installed
# mysttiq-palworld service. The stand-in server is a copy of python3 named PalServer-Linux-Shipping inside the fake
# server folder, with a few extra threads, so priority is checked on every thread (Linux niceness is per thread).
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
APP="$ROOT/app/mysttiq-server"
PORT=18418
T="$ROOT/run"; mkdir -p "$T/runtime" "$T/backups" "$T/fleet"
SERVER="$ROOT/fixture/server"
FAKE="$SERVER/Pal/Binaries/Linux/PalServer-Linux-Shipping"
pass=0; fail=0
ok() { echo "[PASS] $1"; pass=$((pass+1)); }
bad() { echo "[FAIL] $1 -- $2"; fail=$((fail+1)); }
j() { python3 -c "import json,sys; d=json.load(sys.stdin); print($1)"; }

mkdir -p "$(dirname "$FAKE")"
cp "$(readlink -f "$(command -v python3)")" "$FAKE"; chmod +x "$FAKE"
start_fake() {
  "$FAKE" -c "import threading,time
for _ in range(3): threading.Thread(target=time.sleep, args=(900,), daemon=True).start()
time.sleep(900)" >/dev/null 2>&1 &
  FAKEPID=$!
  sleep 1
}
thread_nices() { ps -L -o nice= -p "$FAKEPID" | tr -d ' ' | sort -u | tr '\n' ' ' | sed 's/ $//'; }
thread_count() { ps -L -o lwp= -p "$FAKEPID" | wc -l | tr -d ' '; }

chmod +x "$APP"
"$APP" config-write-default --config "$T/mysttiq.json" --overwrite >/dev/null
python3 - "$T/mysttiq.json" "$T/fleet" <<'PY'
import json, sys
p, fleet = sys.argv[1], sys.argv[2]
c = json.load(open(p)); c["FleetRoot"] = fleet; json.dump(c, open(p, "w"), indent=2)
PY
start_fake
"$APP" api-run --desktop-sidecar --config "$T/mysttiq.json" --bind-address 127.0.0.1 --api-port $PORT \
  --server-root "$SERVER" --steamcmd "$T/missing-steamcmd" --backup-root "$T/backups" --runtime-root "$T/runtime" \
  >"$T/headless.log" 2>"$T/headless.err.log" &
PID=$!
trap 'kill $PID 2>/dev/null; kill $FAKEPID 2>/dev/null; wait 2>/dev/null' EXIT
for i in $(seq 1 40); do curl -sf "http://127.0.0.1:$PORT/healthz" >/dev/null && break; sleep 0.5; done
put() { curl -s -X PUT -H 'Content-Type: application/json' -d "$1" "http://127.0.0.1:$PORT/api/v1/resources/policy"; }
hostpage() { curl -sf --max-time 20 "http://127.0.0.1:$PORT/api/v1/host"; }

V=$(curl -sf "http://127.0.0.1:$PORT/healthz" | j 'd.get("version")')
[[ "$V" == "0.8.17.0" ]] && ok "healthz reports this version" || bad "healthz" "version '$V' $(tail -3 "$T/headless.err.log")"

H=$(hostpage)
CPUS=$(nproc --all); MEMKB=$(awk '/^MemTotal:/{print $2}' /proc/meminfo)
R=$(echo "$H" | j 'str(d["host"]["logicalProcessors"])+" "+str(d["host"]["memoryTotalBytes"]//1024)+" "+str(d["host"]["cpuPercent"] is not None and 0<=d["host"]["cpuPercent"]<=100)+" "+str(len(d["host"]["network"])>0)+" "+str(bool(d["host"]["processorName"]))')
[[ "$R" == "$CPUS $MEMKB True True True" ]] && ok "the HOST route reads this machine from /proc (processors, memory, load, adapters, processor name)" || bad "host readings" "got '$R', expected '$CPUS $MEMKB True True True'"
D=$(echo "$H" | j '";".join(x["name"]+":"+",".join(x["holds"]) for x in d["host"]["disks"])')
[[ "$D" == *install* && "$D" == *saves* ]] && ok "the disk holding the install and saves is marked ($D)" || bad "disk roles" "$D"

S=$(echo "$H" | j 'str(d["resources"]["running"])+" "+str([p["processId"] for p in d["resources"]["processes"]])+" "+str(d["resources"]["efficiencyModeSupported"])')
[[ "$S" == "True [$FAKEPID] False" ]] && ok "the stand-in server is found; Linux reports no efficiency mode" || bad "server processes" "$S (fake $FAKEPID)"
[[ "$(thread_count)" -ge 4 && "$(thread_nices)" == "0" ]] && ok "the stand-in runs $(thread_count) threads at niceness 0 and the default policy leaves them" || bad "baseline" "threads $(thread_count), nices $(thread_nices)"

B=$(put '{"priority":"BelowNormal","ecoMode":"Off","ecoAfterEmptyMinutes":10}' | j 'str(d["success"])+" "+d["snapshot"]["processes"][0]["priority"]')
[[ "$B" == "True BelowNormal" && "$(thread_nices)" == "10" ]] && ok "below normal sets niceness 10 on every thread ($(thread_count) threads)" || bad "below normal" "$B, nices '$(thread_nices)'"

if [[ $(id -u) -ne 0 ]]; then
  E=$(put '{"priority":"Normal","ecoMode":"Off","ecoAfterEmptyMinutes":10}' | j 'd["snapshot"]["lastError"]')
  [[ "$E" == *"needs root or CAP_SYS_NICE"* && "$(thread_nices)" == "10" ]] && ok "an unprivileged MystTiq cannot raise the priority again, and says what it needs" || bad "raise back" "'$E', nices '$(thread_nices)'"
else
  put '{"priority":"Normal","ecoMode":"Off","ecoAfterEmptyMinutes":10}' >/dev/null
  [[ "$(thread_nices)" == "0" ]] && ok "as root the priority goes back to normal" || bad "raise back as root" "nices '$(thread_nices)'"
fi

kill $FAKEPID 2>/dev/null; wait $FAKEPID 2>/dev/null; start_fake
O=$(put '{"priority":"Default","ecoMode":"On","ecoAfterEmptyMinutes":10}' | j 'str(d["snapshot"]["ecoActive"])+" "+repr(d["snapshot"]["lastError"])')
[[ "$O" == "True ''" && "$(thread_nices)" == "10" ]] && ok "eco mode on Linux lowers the priority only (niceness 10), with no efficiency-mode error" || bad "eco on" "$O, nices '$(thread_nices)'"
W=$(put '{"priority":"Default","ecoMode":"WhenEmpty","ecoAfterEmptyMinutes":1}' | j 'str(d["snapshot"]["ecoActive"])+" "+d["snapshot"]["ecoReason"]')
[[ "$W" == False*"cannot be read"* ]] && ok "eco when empty waits when who is online cannot be read ($W)" || bad "when empty" "$W"
[[ -f "$T/runtime/resource-policy.json" ]] && ok "the policy is kept in the server's runtime folder" || bad "policy file" "missing"

echo "linux-isolated: $pass passed, $fail failed"
[[ $fail -eq 0 ]]
