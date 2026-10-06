#!/usr/bin/env bash
# MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
# v0.8.15.0: stops the isolated instance setup.sh started and deletes its folder (token, certificate, accounts included).
ROOT="$(cd "$(dirname "$0")" && pwd)"
[[ -f "$ROOT/run/pid" ]] && kill "$(cat "$ROOT/run/pid")" 2>/dev/null
sleep 1
cd / && rm -rf "$ROOT"
echo "TORN-DOWN"
