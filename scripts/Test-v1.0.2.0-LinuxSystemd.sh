#!/usr/bin/env bash
# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
# v1.0.2.0 (roadmap R-3): the headless service under systemd on the Linux test VM, run phase by phase by
# Test-v1.0.2.0-LinuxSystemd.ps1: install, crash, after-reboot, stop, cleanup. Everything lives in its own unit
# (mysttiq-palworld-r3test.service), folder (/opt/mysttiq-r3test), config, API port 18431 and game port 18611, with a
# stand-in server. The unit text is MystTiq's own (`service-unit`), only pointed at the test folder. The production unit
# (mysttiq-palworld.service), /opt/mysttiq and /etc/mysttiq are never touched. Needs passwordless sudo for the test user.
set -uo pipefail
PHASE="${1:-}"
ROOT="$(cd "$(dirname "$0")" && pwd)"
BASE=/opt/mysttiq-r3test
UNIT=mysttiq-palworld-r3test.service
UNITFILE=/etc/systemd/system/$UNIT
PORT=18431
GAME=18611
pass=0; fail=0
ok() { echo "[PASS] $1"; pass=$((pass+1)); }
bad() { echo "[FAIL] $1 -- $2"; fail=$((fail+1)); }
mainpid() { systemctl show -p MainPID --value "$UNIT"; }
standin() { pgrep -f "$BASE/server/Pal/Binaries/Linux/PalServer-Linux-Shipping" | head -1; }
wait_for() { for _ in $(seq 1 "$2"); do eval "$1" && return 0; sleep 1; done; return 1; }
finish() { echo "linux-systemd $PHASE: $pass passed, $fail failed"; [[ $fail -eq 0 ]]; exit $?; }

sudo -n true 2>/dev/null || { bad "sudo" "passwordless sudo is not allowed for $(id -un)"; finish; }
sha_before() { sha256sum /etc/systemd/system/mysttiq-palworld.service 2>/dev/null | cut -d' ' -f1; }

case "$PHASE" in
install)
  [[ -e "$UNITFILE" ]] && sudo -n systemctl disable --now "$UNIT" >/dev/null 2>&1
  sha_before > /tmp/mysttiq-r3test-production.sha
  sudo -n rm -rf "$BASE"
  sudo -n mkdir -p "$BASE/bin" "$BASE/server/Pal/Binaries/Linux" "$BASE/runtime" "$BASE/backups" "$BASE/fleet"
  sudo -n cp "$ROOT/app/mysttiq-server" "$BASE/bin/mysttiq-server"
  # The stand-in server: PalServer.sh starts PalServer-Linux-Shipping (a copy of python3), which holds the game port.
  sudo -n cp "$(readlink -f "$(command -v python3)")" "$BASE/server/Pal/Binaries/Linux/PalServer-Linux-Shipping"
  sudo -n tee "$BASE/server/PalServer.sh" >/dev/null <<EOF
#!/usr/bin/env bash
port=$GAME
for a in "\$@"; do case "\$a" in -port=*) port="\${a#-port=}";; esac; done
exec "$BASE/server/Pal/Binaries/Linux/PalServer-Linux-Shipping" -c "import socket,time
s=socket.socket(socket.AF_INET,socket.SOCK_DGRAM); s.bind(('0.0.0.0',\$port))
while True: time.sleep(60)"
EOF
  sudo -n chmod +x "$BASE/bin/mysttiq-server" "$BASE/server/PalServer.sh" "$BASE/server/Pal/Binaries/Linux/PalServer-Linux-Shipping"
  sudo -n chown -R "$(id -un):$(id -gn)" "$BASE"
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
  "$BASE/bin/mysttiq-server" service-unit --config "$BASE/mysttiq.json" --service-user "$(id -un)" \
    | sed -e "s#/opt/mysttiq/bin/mysttiq-server#$BASE/bin/mysttiq-server#" -e "s#^WorkingDirectory=/opt/mysttiq\$#WorkingDirectory=$BASE#" > /tmp/$UNIT
  grep -qx "ExecStart=$BASE/bin/mysttiq-server service-run --config \"$BASE/mysttiq.json\"" /tmp/$UNIT && grep -qx 'Restart=on-failure' /tmp/$UNIT && grep -qx 'WantedBy=multi-user.target' /tmp/$UNIT \
    && ok "MystTiq's own unit, pointed at the test folder: restart on failure, started at boot" || bad "unit text" "$(grep -E 'ExecStart|Restart=|WantedBy' /tmp/$UNIT | tr '\n' ' ')"
  sudo -n cp /tmp/$UNIT "$UNITFILE"
  sudo -n systemctl daemon-reload
  sudo -n systemctl enable --now "$UNIT" >/dev/null 2>&1
  wait_for '[[ "$(systemctl is-active $UNIT)" == active ]] && [[ -n "$(standin)" ]] && curl -sf http://127.0.0.1:$PORT/healthz >/dev/null' 60 \
    && ok "enabled and running without a desktop: the service answers on $PORT and started the server (PID $(standin))" \
    || bad "start" "$(systemctl is-active $UNIT); $(journalctl -u $UNIT -n 5 --no-pager 2>/dev/null | tail -3 | tr '\n' ' ')"
  [[ "$(systemctl is-enabled $UNIT)" == enabled ]] && ok "enabled for boot" || bad "enabled" "$(systemctl is-enabled $UNIT)"
  ;;
crash)
  before=$(mainpid)
  sudo -n kill -9 "$before"
  wait_for '[[ "$(systemctl is-active $UNIT)" == active && "$(mainpid)" != "$before" && "$(mainpid)" != 0 ]] && curl -sf http://127.0.0.1:$PORT/healthz >/dev/null' 45 \
    && ok "killed (SIGKILL, PID $before): systemd started it again (PID $(mainpid))" || bad "crash" "$(systemctl is-active $UNIT) pid $(mainpid)"
  wait_for '[[ -n "$(standin)" ]]' 60 && ok "the server is running under the restarted service" || bad "server after crash" "no stand-in process"
  ;;
after-reboot)
  wait_for '[[ "$(systemctl is-active $UNIT)" == active ]] && curl -sf http://127.0.0.1:$PORT/healthz >/dev/null' 90 \
    && ok "after a reboot the service is running again (PID $(mainpid))" || bad "boot" "$(systemctl is-active $UNIT)"
  wait_for '[[ -n "$(standin)" ]]' 90 && ok "and it started the server at boot (PID $(standin))" || bad "server at boot" "no stand-in process"
  ;;
stop)
  sudo -n systemctl stop "$UNIT"
  result=$(systemctl show -p Result --value "$UNIT")
  [[ "$(systemctl is-active $UNIT)" != active && "$result" == success ]] && ok "stopped cleanly (Result=$result)" || bad "stop" "$(systemctl is-active $UNIT) result=$result"
  wait_for '[[ -z "$(standin)" ]]' 60 && ok "the server was stopped with it" || bad "server after stop" "PID $(standin) still running"
  ;;
cleanup)
  sudo -n systemctl disable --now "$UNIT" >/dev/null 2>&1
  sudo -n rm -f "$UNITFILE" "/tmp/$UNIT"
  sudo -n systemctl daemon-reload
  sudo -n rm -rf "$BASE"
  [[ ! -e "$UNITFILE" && ! -e "$BASE" ]] && ok "the test unit and folder are removed" || bad "cleanup" "left over"
  [[ "$(sha_before)" == "$(cat /tmp/mysttiq-r3test-production.sha 2>/dev/null)" ]] && ok "the production unit is unchanged" || bad "production unit" "changed"
  rm -f /tmp/mysttiq-r3test-production.sha
  ;;
*) bad "phase" "unknown phase '$PHASE'" ;;
esac
finish
