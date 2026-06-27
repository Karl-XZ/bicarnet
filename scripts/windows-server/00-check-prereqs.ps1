param(
  [int]$Port = 51820
)

$ErrorActionPreference = "Stop"

function Test-Admin {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($identity)
  $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

Write-Host "DualNet prerequisite check"
Write-Host "Admin: $(Test-Admin)"

$wireguard = @(
  "$env:ProgramFiles\WireGuard\wireguard.exe",
  "$env:ProgramFiles\WireGuard\wg.exe"
) | Where-Object { Test-Path $_ }

if ($wireguard.Count -eq 0) {
  Write-Warning "WireGuard for Windows not found. Install it first: https://www.wireguard.com/install/"
} else {
  Write-Host "WireGuard files found:"
  $wireguard | ForEach-Object { Write-Host "  $_" }
}

$rules = Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object {
  $_.DisplayName -like "DualNet WireGuard UDP $Port*"
}
Write-Host "Firewall rule for UDP ${Port}: $($rules.Count -gt 0)"

Write-Host "Local IPv4 addresses:"
Get-NetIPAddress -AddressFamily IPv4 |
  Where-Object { $_.IPAddress -notlike "169.254.*" } |
  Select-Object InterfaceAlias, IPAddress, PrefixLength |
  Format-Table -AutoSize
