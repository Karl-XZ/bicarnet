#!/usr/bin/env bash
set -euo pipefail

interface_name="${1:-bicarnet}"
endpoint_host="${2:-jl13dax7yxjbhd80.myfritz.net}"
endpoint_port="${3:-51820}"

resolved_ip="$(getent ahostsv4 "$endpoint_host" | awk 'NR == 1 { print $1 }')"
if [[ -z "$resolved_ip" ]]; then
  logger -t bicarnet-ddns "Unable to resolve IPv4 for $endpoint_host"
  exit 1
fi

peer_key="$(wg show "$interface_name" peers | head -n 1)"
if [[ -z "$peer_key" ]]; then
  logger -t bicarnet-ddns "No peer found on $interface_name"
  exit 1
fi

target_endpoint="$resolved_ip:$endpoint_port"
current_endpoint="$(wg show "$interface_name" endpoints | awk 'NR == 1 { print $2 }')"
if [[ "$current_endpoint" == "$target_endpoint" ]]; then
  exit 0
fi

wg set "$interface_name" peer "$peer_key" endpoint "$target_endpoint"
logger -t bicarnet-ddns "Updated $interface_name endpoint: $current_endpoint -> $target_endpoint"
