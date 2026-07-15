#!/usr/bin/env bash
set -euo pipefail

CONFIG="${1:-/etc/wireguard/bicarnet.conf}"
BACKUP="${CONFIG}.bicarnet-before-full-tunnel.bak"
MARKER="/run/bicarnet-full-tunnel-pending"
ROLLBACK_LOG="/var/log/bicarnet-full-tunnel-rollback.log"

if [[ "${2:-}" == "--rollback" ]]; then
  sleep 90
  if [[ -f "$MARKER" && -f "$BACKUP" ]]; then
    cp "$BACKUP" "$CONFIG"
    wg-quick down bicarnet || true
    wg-quick up bicarnet || true
    rm -f "$MARKER"
  fi
  exit 0
fi

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run with sudo: sudo $0 [$CONFIG]"
  exit 1
fi
if [[ ! -f "$CONFIG" ]]; then
  echo "WireGuard config not found: $CONFIG"
  exit 1
fi

cp "$CONFIG" "$BACKUP"
touch "$MARKER"
nohup "$0" "$CONFIG" --rollback >"$ROLLBACK_LOG" 2>&1 &
rollback_pid=$!

restore() {
  cp "$BACKUP" "$CONFIG"
  wg-quick down bicarnet || true
  wg-quick up bicarnet || true
  rm -f "$MARKER"
  kill "$rollback_pid" 2>/dev/null || true
}
trap restore ERR

sed -i \
  -e 's|^DNS = .*|DNS = 10.77.0.1|' \
  -e 's|^AllowedIPs = .*|AllowedIPs = 0.0.0.0/0|' \
  "$CONFIG"

wg-quick down bicarnet
wg-quick up bicarnet

# wg-quick resolves the DDNS endpoint while the old resolver may still have a
# stale router-cache result. Refresh it synchronously before validating traffic;
# the installed timer remains responsible for future IP changes.
ddns_refresh="/usr/local/sbin/bicarnet-ddns-refresh"
if [[ ! -x "$ddns_refresh" ]]; then
  echo "DDNS refresh helper not installed: $ddns_refresh"
  exit 1
fi
"$ddns_refresh"

status_ready="false"
for _ in {1..10}; do
  if curl -4 -fsS --max-time 4 http://10.77.0.1:8787/status >/dev/null; then
    status_ready="true"
    break
  fi
  sleep 2
done
if [[ "$status_ready" != "true" ]]; then
  echo "Tunnel status API did not become reachable after DDNS refresh."
  exit 1
fi

status_code="$(curl -4 -sS --max-time 15 --connect-timeout 8 -o /dev/null -w '%{http_code}' https://www.google.com/generate_204)"
if [[ "$status_code" != "204" ]]; then
  echo "Google validation failed: HTTP $status_code"
  exit 1
fi

rm -f "$MARKER"
kill "$rollback_pid" 2>/dev/null || true

echo "Full tunnel enabled and verified."
grep -E '^(DNS|AllowedIPs) = ' "$CONFIG"
curl -4 -sS --max-time 8 https://api.ipify.org
echo
