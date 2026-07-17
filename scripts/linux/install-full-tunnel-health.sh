#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run with sudo."
  exit 1
fi

install -Dm0755 "$script_dir/bicarnet-full-tunnel-health.sh" /usr/local/sbin/bicarnet-full-tunnel-health
install -Dm0644 "$script_dir/bicarnet-full-tunnel-health.service" /etc/systemd/system/bicarnet-full-tunnel-health.service
install -Dm0644 "$script_dir/bicarnet-full-tunnel-health.timer" /etc/systemd/system/bicarnet-full-tunnel-health.timer
systemctl daemon-reload
systemctl enable --now bicarnet-full-tunnel-health.timer
systemctl start bicarnet-full-tunnel-health.service
systemctl status bicarnet-full-tunnel-health.timer --no-pager
