param(
  [string]$ServerAppPath = "",
  [int]$Port = 8787
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

if ([string]::IsNullOrWhiteSpace($ServerAppPath)) {
  $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
  $ServerAppPath = Join-Path $repoRoot "dist\\windows\\bicarnet.exe"
}

if (!(Test-Path -LiteralPath $ServerAppPath)) {
  throw "bicarnet.exe not found: $ServerAppPath"
}

$taskName = "bicarnet-status-api"
$ruleName = "bicarnet Status API TCP $Port"
$rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (!$rule) {
  New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -RemoteAddress "10.77.0.0/24" -Profile Any | Out-Null
} else {
  Set-NetFirewallRule -DisplayName $ruleName -Enabled True -Direction Inbound -Action Allow -Profile Any
  $addressFilter = Get-NetFirewallAddressFilter -AssociatedNetFirewallRule $rule
  Set-NetFirewallAddressFilter -InputObject $addressFilter -RemoteAddress "10.77.0.0/24"
}

$action = New-ScheduledTaskAction -Execute $ServerAppPath -WorkingDirectory (Split-Path -Parent $ServerAppPath)
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Description "Runs bicarnet status and activation API after Windows starts." -Force | Out-Null

$task = Get-ScheduledTask -TaskName $taskName
if ($task.State -ne "Running") {
  Start-ScheduledTask -TaskName $taskName
}
Start-Sleep -Seconds 5

Write-Host "=== bicarnet status API autostart ==="
Get-ScheduledTask -TaskName $taskName | Select-Object TaskName, State
Get-NetFirewallRule -DisplayName $ruleName | Select-Object DisplayName, Enabled, Direction, Action
try {
  $response = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$Port/status" -TimeoutSec 8
  Write-Host "Local status API: HTTP $($response.StatusCode)"
} catch {
  throw "Status API task started but local HTTP verification failed: $($_.Exception.Message)"
}
