#!/usr/bin/env bash
# v0.8.21.0: an isolated check on the Linux test VM, run by Test-v0.8.21.0-LinuxIsolated.ps1. Prints the systemd unit that
# service-install would write (as the ordinary user, into /tmp only) and has systemd itself check it. Nothing is installed:
# /etc/mysttiq, /etc/systemd and the installed mysttiq-palworld service are never touched.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
APP="$ROOT/app/mysttiq-server"
T="$ROOT/run"; mkdir -p "$T"
pass=0; fail=0
ok() { echo "[PASS] $1"; pass=$((pass+1)); }
bad() { echo "[FAIL] $1 -- $2"; fail=$((fail+1)); }

chmod +x "$APP"
"$APP" config-write-default --config "$T/mysttiq.json" --overwrite >/dev/null
UNIT="$T/mysttiq-palworld.service"
"$APP" service-unit --config "$T/mysttiq.json" --service-user "$(id -un)" > "$UNIT"; CODE=$?
[[ $CODE -eq 0 && -s "$UNIT" ]] && ok "service-unit prints the unit as an ordinary user (exit 0)" || bad "service-unit" "exit $CODE"
grep -qx 'LimitNICE=-11' "$UNIT" && grep -qx 'NoNewPrivileges=true' "$UNIT" && grep -qx "User=$(id -un)" "$UNIT" \
  && ok "the unit sets LimitNICE=-11 and keeps NoNewPrivileges" || bad "unit content" "$(grep -E 'LimitNICE|NoNewPrivileges|User=' "$UNIT" | tr '\n' ' ')"
! grep -qE 'AmbientCapabilities|CapabilityBoundingSet' "$UNIT" && ok "no capability is added" || bad "capabilities" "found"
if command -v systemd-analyze >/dev/null; then
  OUT=$(systemd-analyze verify "$UNIT" 2>&1); CODE=$?
  if echo "$OUT" | grep -qiE 'LimitNICE|Unknown (key|lvalue)|Invalid'; then bad "systemd-analyze verify" "$OUT"
  else ok "systemd-analyze accepts the unit, LimitNICE included (exit $CODE$( [[ -n "$OUT" ]] && echo "; notes: $(echo "$OUT" | head -2 | tr '\n' ' ')" ))"; fi
else
  bad "systemd-analyze" "not installed"
fi
[[ ! -e /etc/systemd/system/mysttiq-palworld.service.mysttiq-test ]] && ok "nothing was written outside /tmp" || bad "outside /tmp" "unexpected file"

echo "linux-isolated: $pass passed, $fail failed"
[[ $fail -eq 0 ]]
