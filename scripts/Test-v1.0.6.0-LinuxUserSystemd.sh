#!/usr/bin/env bash
# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
# v1.0.6.0 (roadmap R-3, carried): the headless service under systemd on the Linux test VM without sudo, as a per-user
# unit (mysttiq-palworld-r3user.service in ~/.config/systemd/user), its own folder (~/mysttiq-r3user), API port 18432 and
# game port 18612, with a stand-in server. The unit text is MystTiq's own (`service-unit`) with what a user unit needs
# changed (no User=, WantedBy=default.target). Runs every phase in one session: install, crash, stop, cleanup. The
# production unit (mysttiq-palworld.service), /opt/mysttiq and /etc/mysttiq are only read (a hash before and after).
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
BASE="$HOME/mysttiq-r3user"
UNIT=mysttiq-palworld-r3user.service
UNITDIR="$HOME/.config/systemd/user"
PORT=18432
GAME=18612
pass=0; fail=0
ok() { echo "[PASS] $1"; pass=$((pass+1)); }
bad() { echo "[FAIL] $1 -- $2"; fail=$((fail+1)); }
sc() { systemctl --user "$@"; }
mainpid() { sc show -p MainPID --value "$UNIT"; }
standin() { pgrep -f "$BASE/server/Pal/Binaries/Linux/PalServer-Linux-Shipping" | head -1; }
wait_for() { for _ in $(seq 1 "$2"); do eval "$1" && return 0; sleep 1; done; return 1; }
production() { sha256sum /etc/systemd/system/mysttiq-palworld.service 2>/dev/null | cut -d' ' -f1; echo "$(systemctl show -p MainPID --value mysttiq-palworld.service)"; }

before_production="$(production)"
sc is-system-running >/dev/null 2>&1; [[ $? -le 1 ]] || { bad "user manager" "systemctl --user is not available"; echo "linux-user-systemd: $pass passed, $fail failed"; exit 1; }

# install
[[ -e "$UNITDIR/$UNIT" ]] && sc disable --now "$UNIT" >/dev/null 2>&1
rm -rf "$BASE"; mkdir -p "$BASE/bin" "$BASE/server/Pal/Binaries/Linux" "$BASE/runtime" "$BASE/backups" "$BASE/fleet" "$UNITDIR"
cp "$ROOT/app/mysttiq-server" "$BASE/bin/mysttiq-server"
cp "$(readlink -f "$(command -v python3)")" "$BASE/server/Pal/Binaries/Linux/PalServer-Linux-Shipping"
cat > "$BASE/server/PalServer.sh" <<EOF
#!/usr/bin/env bash
port=$GAME
for a in "\$@"; do case "\$a" in -port=*) port="\${a#-port=}";; esac; done
exec "$BASE/server/Pal/Binaries/Linux/PalServer-Linux-Shipping" -c "import socket,time
s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); s.bind(('0.0.0.0',\$port))
while True: time.sleep(60)"
EOF
chmod +x "$BASE/bin/mysttiq-server" "$BASE/server/PalServer.sh" "$BASE/server/Pal/Binaries/Linux/PalServer-Linux-Shipping"
"$BASE/bin/mysttiq-server" config-write-default --config "$BASE/mysttiq.json" --overwrite >/dev/null
python3 - "$BASE" "$PORT" "$GAME" <<'PY'
import json, sys
base, port, game = sys.argv[1], int(sys.argv[2]), sys.argv[3]
path = base + "/mysttiq.json"
c = json.load(open(path))
c["Api"]["Port"] = port; c["Api"]["BindAddress"] = "127.0.0.1"; c["FleetRoot"] = base + "/fleet"
s = c["Servers"][0]
s.update({"ServerRoot": base + "/server", "SteamCmdPath": base + "/missing-steamcmd", "BackupRoot": base + "/backups",
          "RuntimeRoot": base + "/runtime", "LaunchArguments": ["-port=" + game], "Runtime": "LinuxNative"})
json.dump(c, open(path, "w"), indent=2)
PY
"$BASE/bin/mysttiq-server" service-unit --config "$BASE/mysttiq.json" --service-user "$(id -un)" > "$BASE/system.unit"
sed -e "s#/opt/mysttiq/bin/mysttiq-server#$BASE/bin/mysttiq-server#" -e "s#^WorkingDirectory=/opt/mysttiq\$#WorkingDirectory=$BASE#" \
    -e '/^User=/d' -e '/^Group=/d' -e 's#^WantedBy=multi-user.target$#WantedBy=default.target#' "$BASE/system.unit" > "$UNITDIR/$UNIT"
grep -qx 'Restart=on-failure' "$UNITDIR/$UNIT" && grep -qx "ExecStart=$BASE/bin/mysttiq-server service-run --config \"$BASE/mysttiq.json\"" "$UNITDIR/$UNIT" \
  && ok "MystTiq's own unit as a user unit: restart on failure, its own folder" || bad "unit text" "$(grep -E 'ExecStart|Restart=|WantedBy' "$UNITDIR/$UNIT" | tr '\n' ' ')"
sc daemon-reload
sc enable --now "$UNIT" >/dev/null 2>&1
wait_for '[[ "$(sc is-active $UNIT)" == active ]] && [[ -n "$(standin)" ]] && curl -sf http://127.0.0.1:$PORT/healthz >/dev/null' 60 \
  && ok "running without a desktop: the service answers on $PORT and started the server (PID $(standin))" \
  || bad "start" "$(sc is-active $UNIT); $(journalctl --user -u $UNIT -n 5 --no-pager 2>/dev/null | tail -3 | tr '\n' ' ')"
version=$(curl -sf http://127.0.0.1:$PORT/api/v1/status | python3 -c 'import json,sys; print(json.load(sys.stdin).get("phase",""))' 2>/dev/null)
echo "    status phase: ${version:-?}"

# crash
before=$(mainpid)
kill -9 "$before"
wait_for '[[ "$(sc is-active $UNIT)" == active && "$(mainpid)" != "$before" && "$(mainpid)" != 0 ]] && curl -sf http://127.0.0.1:$PORT/healthz >/dev/null' 45 \
  && ok "killed (SIGKILL, PID $before): systemd started it again (PID $(mainpid))" || bad "crash" "$(sc is-active $UNIT) pid $(mainpid)"
wait_for '[[ -n "$(standin)" ]]' 60 && ok "the server is running under the restarted service (PID $(standin))" || bad "server after crash" "no stand-in process"

# stop
sc stop "$UNIT"
result=$(sc show -p Result --value "$UNIT")
[[ "$(sc is-active $UNIT)" != active && "$result" == success ]] && ok "stopped cleanly (Result=$result)" || bad "stop" "$(sc is-active $UNIT) result=$result"
wait_for '[[ -z "$(standin)" ]]' 60 && ok "the server was stopped with it" || bad "server after stop" "PID $(standin) still running"

# cleanup
sc disable --now "$UNIT" >/dev/null 2>&1
rm -f "$UNITDIR/$UNIT"; sc daemon-reload
rm -rf "$BASE"
[[ ! -e "$UNITDIR/$UNIT" && ! -e "$BASE" ]] && ok "the test unit and folder are removed" || bad "cleanup" "left over"
[[ "$(production)" == "$before_production" ]] && ok "the production unit is unchanged and still running the same process" || bad "production unit" "changed"
echo "linux-user-systemd: $pass passed, $fail failed"
[[ $fail -eq 0 ]]
