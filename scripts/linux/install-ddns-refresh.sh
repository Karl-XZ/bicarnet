#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run with sudo."
  exit 1
fi

install -Dm0755 "$script_dir/bicarnet-ddns-refresh.sh" /usr/local/sbin/bicarnet-ddns-refresh
install -Dm0644 "$script_dir/bicarnet-ddns-refresh.service" /etc/systemd/system/bicarnet-ddns-refresh.service
install -Dm0644 "$script_dir/bicarnet-ddns-refresh.timer" /etc/systemd/system/bicarnet-ddns-refresh.timer
systemctl daemon-reload
systemctl enable --now bicarnet-ddns-refresh.timer
systemctl start bicarnet-ddns-refresh.service
systemctl status bicarnet-ddns-refresh.timer --no-pager
