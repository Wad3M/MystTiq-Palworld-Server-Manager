#!/usr/bin/env bash
# v0.8.13.0: an isolated MystTiq instance on the Linux test VM, run by Test-v0.8.13.0-LinuxIsolated.ps1. Own config,
# FleetRoot, runtime and port (18413) over a synthetic server folder; never touches /etc/mysttiq or the installed
# mysttiq-palworld service. Checks what v0.8.9.0 (crash reports), v0.8.11.0 (Pal positions), v0.8.12.0 (second-NAT
# check) and v0.8.13.0 (game names) added. The WAN report is taken twice because the real router answers its WAN
# address intermittently.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
APP="$ROOT/app/mysttiq-server"
PORT=18413
T="$ROOT/run"; mkdir -p "$T/runtime" "$T/backups" "$T/fleet"
pass=0; fail=0
ok() { echo "[PASS] $1"; pass=$((pass+1)); }
bad() { echo "[FAIL] $1 -- $2"; fail=$((fail+1)); }

# v0.8.13.0: names from the game pak. With the VM's real Linux pak (a read-only symlink) and a working ooz, the real
# names; with the real pak but no working ooz, first the honest "ooz is not installed" report, then a synthetic pak with
# the same rows. Needs python3.
PAKS="$ROOT/fixture/server/Pal/Content/Paks"; mkdir -p "$PAKS"
REAL_PAK="${MYSTTIQ_REAL_PAK:-/opt/mysttiq/palserver/Pal/Content/Paks/Pal-LinuxServer.pak}"
OOZ=0; python3 -c "import ooz; ooz.decompress" 2>/dev/null && OOZ=1
synthetic() { unlink "$PAKS/Pal-LinuxServer.pak" 2>/dev/null; python3 "$ROOT/make_test_pak.py" "$PAKS/Pal-LinuxServer.pak" '{"PalSphere":"Pal Sphere"}' '{"SheepBall":"Lamball","PinkCat":"Cattiva"}'; }
if [[ -f "$REAL_PAK" ]]; then ln -sf "$REAL_PAK" "$PAKS/Pal-LinuxServer.pak"; echo "   (real pak linked read-only; working ooz: $OOZ)"; else synthetic; echo "   (no real pak: synthetic pak)"; fi
chmod +x "$APP"
"$APP" config-write-default --config "$T/mysttiq.json" --overwrite >/dev/null
python3 - "$T/mysttiq.json" "$T/fleet" <<'PY'
import json, sys
p, fleet = sys.argv[1], sys.argv[2]
c = json.load(open(p)); c["FleetRoot"] = fleet; json.dump(c, open(p, "w"), indent=2)
PY
"$APP" api-run --desktop-sidecar --config "$T/mysttiq.json" --bind-address 127.0.0.1 --api-port $PORT \
  --server-root "$ROOT/fixture/server" --steamcmd "$T/missing-steamcmd" --backup-root "$T/backups" --runtime-root "$T/runtime" \
  >"$T/headless.log" 2>"$T/headless.err.log" &
PID=$!
trap 'kill $PID 2>/dev/null; wait $PID 2>/dev/null' EXIT
for i in $(seq 1 40); do curl -sf "http://127.0.0.1:$PORT/healthz" >/dev/null && break; sleep 0.5; done

j() { python3 -c "import json,sys; d=json.load(sys.stdin); print($1)"; }
V=$(curl -sf "http://127.0.0.1:$PORT/healthz" | j 'd.get("version")')
[[ "$V" == "0.8.13.0" ]] && ok "healthz reports $V" || bad "healthz" "version '$V' $(tail -3 "$T/headless.err.log")"

# v0.8.9.0: the crash report under Pal/Saved/Crashes is read and classified.
A=$(curl -sf -X POST "http://127.0.0.1:$PORT/api/v1/crash-analyzer/analyze")
R=$(echo "$A" | j 'str(d.get("crashReportsRead"))+" "+",".join(f["signatureId"] for f in d.get("findings",[]))')
[[ "$R" == 1\ *engine-array-size* ]] && ok "v0.8.9.0 crash report read and classified ($R)" || bad "v0.8.9.0 crash report" "$R"

# v0.8.11.0: Pal positions from the synthetic save (base worker + party alpha; Palbox only counted).
P=$(curl -sf "http://127.0.0.1:$PORT/api/v1/world/players-guilds" | j '" ".join(sorted(p["species"]+"/"+p["placement"]+"/"+(p["guildName"] or p["ownerName"]) for p in d.get("palLocations",[])))+" palbox="+str((d.get("palSummary") or {}).get("inPalbox"))')
[[ "$P" == "PinkCat/BaseWorker/Linux Guild WeaselDragon/Party/Keeper palbox=2" ]] && ok "v0.8.11.0 Pal positions ($P)" || bad "v0.8.11.0 Pal positions" "$P"

# v0.8.13.0: the Give Item catalogue and the Pal positions carry the game's names.
catalog() { curl -sf --max-time 120 "http://127.0.0.1:$PORT/api/v1/players/give/catalog"; }
if [[ -L "$PAKS/Pal-LinuxServer.pak" && $OOZ -eq 0 ]]; then
  D=$(catalog | j 'str(d["namesAvailable"])+" "+d.get("namesDetail","")')
  [[ "$D" == False*"Oodle module (ooz) is not installed"* ]] && ok "v0.8.13.0 real pak without a working ooz: ids only, and says why" || bad "v0.8.13.0 missing-ooz report" "$D"
  synthetic
fi
C=$(catalog | j 'str(d["namesAvailable"])+" "+next((e.get("name") or "-") for e in d["entries"] if e["id"]=="PalSphere")+" | "+next((e.get("name") or "-") for e in d["entries"] if e["kind"]=="Pal" and e["id"]=="Sheepball")+" | "+str(sum(1 for e in d["entries"] if e.get("inGameFiles")))+" game-only"')
[[ "$C" == "True Pal Sphere | Lamball | "* ]] && ok "v0.8.13.0 Give Item names ($C)" || bad "v0.8.13.0 Give Item names" "$C $(catalog | j 'd.get("namesDetail")')"
N=$(curl -sf "http://127.0.0.1:$PORT/api/v1/world/players-guilds" | j '" ".join(p["species"]+"="+p.get("speciesName","") for p in d.get("palLocations",[]))')
[[ "$N" == *"PinkCat=Cattiva"* ]] && ok "v0.8.13.0 Pal species names ($N)" || bad "v0.8.13.0 Pal species names" "$N"
# v0.8.12.0: the WAN report (read-only network lookups): a second-NAT verdict, the outside-in entry, and UPnP no
# longer failing on Linux with "The 'file' scheme is not supported".
for run in 1 2; do
  WJ=$(curl -sf --max-time 90 "http://127.0.0.1:$PORT/api/v1/diagnostics/network/wan")
  W=$(echo "$WJ" | j '" | ".join(c["test"]+"="+str(c["state"]) for c in d["checks"])')
  [[ "$W" == *"Second NAT (CGNAT / double NAT)="* && "$W" == *"Outside-in test=4"* && "$WJ" != *"'file' scheme"* ]] && ok "v0.8.12.0 WAN report run $run ($W)" || bad "v0.8.12.0 WAN report run $run" "$W"
  echo "$WJ" | j '"\n".join("   ["+str(c["state"])+"] "+c["test"]+": "+c["details"] for c in d["checks"][1:])' | sed -E 's/\b([0-9]{1,3}\.){3}[0-9]{1,3}\b/<ip>/g'
done
echo "passed=$pass failed=$fail"
[[ $fail -eq 0 ]]
