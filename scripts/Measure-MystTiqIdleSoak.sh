#!/usr/bin/env bash
# MystTiq v1.0.7.5: file reviewed for this release (2026-10-11).
# v1.0.7.5 (roadmap Q-10): the Linux half of the idle soak, copied to the test VM by Measure-MystTiqIdleSoak.ps1.
#   start: an isolated MystTiq (own folder, config, FleetRoot, a loopback port; no Palworld) as a per-user systemd unit,
#          a poller asking for the status every few seconds as a connected desktop does, and a sampler recording the
#          service's memory, open files and threads. Nothing is installed: no sudo, no /etc, and the installed
#          mysttiq-palworld service is never touched. The units stop on their own after the given hours.
#   status: how far it is.   stop: stops all three units now (the sampler records the shutdown first if it can).
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
ACTION="${1:-status}"; HOURS="${2:-24}"; SAMPLE_MINUTES="${3:-60}"; POLL_SECONDS="${4:-5}"; PORT="${5:-18697}"
NAME="$(basename "$ROOT")"
UNIT="$NAME"; POLL_UNIT="$NAME-poll"; SAMPLE_UNIT="$NAME-sample"
APP="$ROOT/app/mysttiq-server"; T="$ROOT/run"; OUT="$ROOT/results"

case "$ACTION" in
start)
  mkdir -p "$T/server/Pal/Saved/SaveGames/0/SoakWorld" "$T/runtime" "$T/backups" "$T/fleet" "$OUT"
  chmod +x "$APP"
  "$APP" config-write-default --config "$T/mysttiq.json" --overwrite >/dev/null || { echo "config-write-default failed"; exit 1; }
  jq --arg fleet "$T/fleet" --argjson port "$PORT" '.FleetRoot = $fleet | .Api.Port = $port | .Api.BindAddress = "127.0.0.1"' \
    "$T/mysttiq.json" > "$T/mysttiq.json.new" && mv "$T/mysttiq.json.new" "$T/mysttiq.json"
  SECONDS_TOTAL=$(python3 -c "print(int(float('$HOURS') * 3600))")
  systemd-run --user --unit "$UNIT" --description "MystTiq idle soak (isolated, $HOURS h)" --property=RuntimeMaxSec=$((SECONDS_TOTAL + 900)) \
    --working-directory "$ROOT" "$APP" api-run --config "$T/mysttiq.json" --server-root "$T/server" --runtime-root "$T/runtime" \
    --backup-root "$T/backups" --steamcmd "$T/none" >/dev/null 2>&1 || { echo "systemd-run failed for the service"; exit 1; }
  for i in $(seq 1 80); do curl -fs "http://127.0.0.1:$PORT/healthz" >/dev/null && break; sleep 0.25; done
  curl -fs "http://127.0.0.1:$PORT/healthz" >/dev/null || { echo "the soak service is not answering"; journalctl --user -u "$UNIT" -n 20 --no-pager; exit 1; }
  systemd-run --user --unit "$POLL_UNIT" --description "MystTiq soak poller" --property=RuntimeMaxSec=$SECONDS_TOTAL \
    bash -c "ok=0; bad=0; while true; do if curl -fs -o /dev/null 'http://127.0.0.1:$PORT/api/v1/status/poll?lines=500'; then ok=\$((ok+1)); else bad=\$((bad+1)); fi; echo \"\$ok \$bad\" > '$OUT/poll-count.txt'; sleep $POLL_SECONDS; done" >/dev/null 2>&1
  systemd-run --user --unit "$SAMPLE_UNIT" --description "MystTiq soak sampler" --property=RuntimeMaxSec=$((SECONDS_TOTAL + 600)) \
    bash "$ROOT/Measure-MystTiqIdleSoak.sh" sample "$HOURS" "$SAMPLE_MINUTES" "$POLL_SECONDS" "$PORT" >/dev/null 2>&1
  echo "started $UNIT on 127.0.0.1:$PORT for $HOURS h, sampling every $SAMPLE_MINUTES min"
  ;;
sample)
  echo "utc,elapsed_hours,rss_kb,private_kb,open_files,threads,poll_ok,poll_failed,status_ms" > "$OUT/samples.csv"
  START=$(date +%s); END=$(python3 -c "print($START + int(float('$HOURS') * 3600))"); STEP=$(python3 -c "print(int(float('$SAMPLE_MINUTES') * 60))")
  record() {
    local pid; pid=$(systemctl --user show -p MainPID --value "$UNIT")
    [[ -z "$pid" || "$pid" == "0" ]] && { echo "$(date -u +%FT%TZ),gone" >> "$OUT/samples.csv"; return 1; }
    local rss priv fds thr ms counts
    rss=$(awk '/^VmRSS:/{print $2}' "/proc/$pid/status"); thr=$(awk '/^Threads:/{print $2}' "/proc/$pid/status")
    priv=$(awk '/^(Private_Clean|Private_Dirty):/{s+=$2} END{print s}' "/proc/$pid/smaps_rollup" 2>/dev/null)
    fds=$(ls "/proc/$pid/fd" 2>/dev/null | wc -l)
    ms=$(curl -fs -o /dev/null -w '%{time_total}' "http://127.0.0.1:$PORT/api/v1/status" | python3 -c "import sys; print(int(float(sys.stdin.read() or 0) * 1000))")
    counts=$(cat "$OUT/poll-count.txt" 2>/dev/null || echo "0 0")
    echo "$(date -u +%FT%TZ),$(python3 -c "print(round(($(date +%s) - $START) / 3600, 3))"),$rss,$priv,$fds,$thr,${counts% *},${counts#* },$ms" >> "$OUT/samples.csv"
  }
  record
  while (( $(date +%s) + STEP <= END )); do sleep "$STEP"; record || break; done
  # The end of the run: the poller stops, then the service is stopped the way systemd stops it (SIGTERM); record how long
  # it took, how it exited, and whether its port is free again.
  systemctl --user stop "$POLL_UNIT" 2>/dev/null
  pid=$(systemctl --user show -p MainPID --value "$UNIT")
  t0=$(date +%s%N); systemctl --user stop "$UNIT"; t1=$(date +%s%N)
  result=$(systemctl --user show -p Result --value "$UNIT" 2>/dev/null); code=$(systemctl --user show -p ExecMainStatus --value "$UNIT" 2>/dev/null)
  alive=no; [[ -n "$pid" && "$pid" != "0" && -d "/proc/$pid" ]] && alive=yes
  port=free; ss -ltn 2>/dev/null | grep -q ":$PORT " && port=in-use
  echo "stop_ms=$(( (t1 - t0) / 1000000 )) result=${result:-unknown} exit=${code:-unknown} process_left=$alive port=$port" > "$OUT/shutdown.txt"
  journalctl --user -u "$UNIT" --no-pager -o short-iso > "$OUT/service-journal.txt" 2>/dev/null
  echo done > "$OUT/finished.txt"
  ;;
status)
  for u in "$UNIT" "$POLL_UNIT" "$SAMPLE_UNIT"; do echo "$u: $(systemctl --user is-active "$u" 2>/dev/null)"; done
  [[ -f "$OUT/samples.csv" ]] && tail -n 3 "$OUT/samples.csv"
  [[ -f "$OUT/finished.txt" ]] && echo "finished" && cat "$OUT/shutdown.txt"
  ;;
stop)
  for u in "$SAMPLE_UNIT" "$POLL_UNIT" "$UNIT"; do systemctl --user stop "$u" 2>/dev/null; done
  systemctl --user reset-failed "$UNIT" "$POLL_UNIT" "$SAMPLE_UNIT" 2>/dev/null
  echo stopped
  ;;
*) echo "usage: $0 start|status|stop [hours] [sample-minutes] [poll-seconds] [port]"; exit 2 ;;
esac
