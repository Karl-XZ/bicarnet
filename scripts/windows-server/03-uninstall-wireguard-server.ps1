param(
  [string]$TunnelName = "dualnet-server",
  [int]$Port = 51820
)

$ErrorActionPreference = "Stop"
$wireguard = "$env:ProgramFiles\WireGuard\wireguard.exe"
if (!(Test-Path $wireguard)) {
  $cmd = Get-Command wireguard.exe -ErrorAction SilentlyContinue
  if (!$cmd) { throw "wireguard.exe not found." }
  $wireguard = $cmd.Source
}

& $wireguard /uninstalltunnelservice $TunnelName
Get-NetFirewallRule -DisplayName "DualNet WireGuard UDP $Port" -ErrorAction SilentlyContinue |
  Remove-NetFirewallRule

Write-Host "Removed tunnel $TunnelName and firewall rule for UDP $Port."
