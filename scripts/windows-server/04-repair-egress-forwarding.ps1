param(
  [string]$TunnelName = "dualnet-server",
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

Assert-Admin

$serviceName = "WireGuardTunnel`$$TunnelName"
$service = Get-Service -Name $serviceName -ErrorAction Stop
if ($service.Status -ne "Running") {
  Start-Service -Name $serviceName
}

Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" -Name IPEnableRouter -Value 1

$nat = Get-NetNat -Name "DualNetNat" -ErrorAction SilentlyContinue
if (!$nat) {
  $nat = New-NetNat -Name "DualNetNat" -InternalIPInterfaceAddressPrefix $NatPrefix
} elseif ($nat.InternalIPInterfaceAddressPrefix -ne $NatPrefix) {
  throw "DualNetNat uses $($nat.InternalIPInterfaceAddressPrefix), expected $NatPrefix. Do not replace it automatically."
}

$tunnelInterface = Get-NetIPInterface -InterfaceAlias $TunnelName -AddressFamily IPv4 -ErrorAction Stop
Set-NetIPInterface -InterfaceIndex $tunnelInterface.InterfaceIndex -AddressFamily IPv4 -Forwarding Enabled

Write-Host "=== bicarnet egress verification ==="
Get-Service -Name $serviceName | Select-Object Name, Status
Get-NetNat -Name "DualNetNat" | Select-Object Name, InternalIPInterfaceAddressPrefix, Active
Get-NetIPInterface -InterfaceAlias $TunnelName -AddressFamily IPv4 |
  Select-Object InterfaceAlias, InterfaceIndex, ConnectionState, Forwarding
Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" -Name IPEnableRouter |
  Select-Object IPEnableRouter
Write-Host "Done. If egress still fails, restart this Windows server once during a maintenance window so IPEnableRouter is read at boot."
