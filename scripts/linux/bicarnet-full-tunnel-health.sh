#!/usr/bin/env bash
set -euo pipefail

interface="${1:-bicarnet}"
config="/etc/wireguard/${interface}.conf"
table="51820"

# Do not start an interface deliberately left down by an operator, and do not
# apply full-tunnel rules to a profile that was intentionally configured split.
ip link show dev "$interface" >/dev/null 2>&1 || exit 0
grep -q '^AllowedIPs = 0.0.0.0/0$' "$config" || exit 0

has_policy_rules() {
  ip -4 rule show | grep -q "lookup main suppress_prefixlength 0" &&
    ip -4 rule show | grep -q "lookup $table" &&
    ip -4 route show table "$table" | grep -q "^default dev $interface"
}

has_policy_rules && exit 0

logger -t bicarnet-full-tunnel-health "Full-tunnel policy rules missing; reapplying $interface."
wg-quick down "$interface" || true
wg-quick up "$interface"

# wg-quick can resolve a stale DDNS record while it is rebuilding the tunnel.
if [[ -x /usr/local/sbin/bicarnet-ddns-refresh ]]; then
  /usr/local/sbin/bicarnet-ddns-refresh "$interface" jl13dax7yxjbhd80.myfritz.net 51820
fi

has_policy_rules || {
  logger -t bicarnet-full-tunnel-health "Policy routes are still missing after repair."
  exit 1
}
