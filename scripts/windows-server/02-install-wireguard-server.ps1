param(
  [string]$ServerConfig = ".\runtime\server\dualnet-server.conf",
  [int]$Port = 51820,
  [switch]$EnableNat,
  [string]$NatPrefix = "10.77.0.0/24"
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($identity)
  if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script as Administrator."
  }
}

function Resolve-WireGuard {
  $candidate = "$env:ProgramFiles\WireGuard\wireguard.exe"
  if (Test-Path $candidate) { return $candidate }
  $cmd = Get-Command wireguard.exe -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  throw "wireguard.exe not found. Install WireGuard for Windows first."
}

Assert-Admin
$wireguard = Resolve-WireGuard
$configPath = (Resolve-Path $ServerConfig).Path
$tunnelName = [IO.Path]::GetFileNameWithoutExtension($configPath)

$ruleName = "DualNet WireGuard UDP $Port"
$existingRule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (!$existingRule) {
  New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol UDP `
    -LocalPort $Port | Out-Null
}

$oldPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
& $wireguard /uninstalltunnelservice $tunnelName *> $null
$ErrorActionPreference = $oldPreference
& $wireguard /installtunnelservice $configPath

if ($EnableNat) {
  Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" -Name IPEnableRouter -Value 1
  $existing = Get-NetNat -Name "DualNetNat" -ErrorAction SilentlyContinue
  if ($existing) { Remove-NetNat -Name "DualNetNat" -Confirm:$false }
  New-NetNat -Name "DualNetNat" -InternalIPInterfaceAddressPrefix $NatPrefix | Out-Null
}

Write-Host "WireGuard server tunnel installed: $tunnelName"
Write-Host "UDP firewall rule opened: $Port"
if ($EnableNat) { Write-Host "NAT enabled for $NatPrefix. A reboot may be required for IP forwarding." }
