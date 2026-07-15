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
$dnsTaskName = "bicarnet-tunnel-dns"
$ruleName = "bicarnet Status API TCP $Port"
$dnsRuleName = "bicarnet Tunnel DNS UDP 53"
$rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (!$rule) {
  New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -RemoteAddress "10.77.0.0/24" -Profile Any | Out-Null
} else {
  Set-NetFirewallRule -DisplayName $ruleName -Enabled True -Direction Inbound -Action Allow -Profile Any
  $addressFilter = Get-NetFirewallAddressFilter -AssociatedNetFirewallRule $rule
  Set-NetFirewallAddressFilter -InputObject $addressFilter -RemoteAddress "10.77.0.0/24"
}

$dnsRule = Get-NetFirewallRule -DisplayName $dnsRuleName -ErrorAction SilentlyContinue
if (!$dnsRule) {
  New-NetFirewallRule -DisplayName $dnsRuleName -Direction Inbound -Action Allow -Protocol UDP -LocalPort 53 -RemoteAddress "10.77.0.0/24" -Profile Any | Out-Null
} else {
  Set-NetFirewallRule -DisplayName $dnsRuleName -Enabled True -Direction Inbound -Action Allow -Profile Any
  $dnsAddressFilter = Get-NetFirewallAddressFilter -AssociatedNetFirewallRule $dnsRule
  Set-NetFirewallAddressFilter -InputObject $dnsAddressFilter -RemoteAddress "10.77.0.0/24"
}

$action = New-ScheduledTaskAction -Execute $ServerAppPath -WorkingDirectory (Split-Path -Parent $ServerAppPath)
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Description "Runs bicarnet status and activation API after Windows starts." -Force | Out-Null

$task = Get-ScheduledTask -TaskName $taskName
$wasRunning = $task.State -eq "Running"
if ($wasRunning) { Stop-ScheduledTask -TaskName $taskName }
Start-Sleep -Seconds 2
Start-ScheduledTask -TaskName $taskName
Start-Sleep -Seconds 5

$dnsSource = Join-Path $PSScriptRoot "tunnel-dns-relay.ps1"
$dnsDirectory = Join-Path $env:ProgramData "bicarnet"
$dnsScript = Join-Path $dnsDirectory "tunnel-dns-relay.ps1"
if (!(Test-Path -LiteralPath $dnsSource)) {
  throw "Tunnel DNS relay source not found: $dnsSource"
}
New-Item -ItemType Directory -Path $dnsDirectory -Force | Out-Null
Copy-Item -LiteralPath $dnsSource -Destination $dnsScript -Force
$dnsAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$dnsScript`""
Register-ScheduledTask -TaskName $dnsTaskName -Action $dnsAction -Trigger $trigger -Principal $principal -Description "Relays DNS for bicarnet tunnel clients." -Force | Out-Null
if ((Get-ScheduledTask -TaskName $dnsTaskName).State -eq "Running") {
  Stop-ScheduledTask -TaskName $dnsTaskName
  Start-Sleep -Seconds 2
}
Start-ScheduledTask -TaskName $dnsTaskName
Start-Sleep -Seconds 3

Write-Host "=== bicarnet status API autostart ==="
Get-ScheduledTask -TaskName $taskName | Select-Object TaskName, State
Get-ScheduledTask -TaskName $dnsTaskName | Select-Object TaskName, State
Get-NetFirewallRule -DisplayName $ruleName | Select-Object DisplayName, Enabled, Direction, Action
Get-NetFirewallRule -DisplayName $dnsRuleName | Select-Object DisplayName, Enabled, Direction, Action
try {
  $response = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$Port/status" -TimeoutSec 8
  Write-Host "Local status API: HTTP $($response.StatusCode)"
} catch {
  throw "Status API task started but local HTTP verification failed: $($_.Exception.Message)"
}
try {
  Resolve-DnsName -Name "example.com" -Type A -Server "10.77.0.1" -DnsOnly -QuickTimeout -ErrorAction Stop | Select-Object -First 1 Name, IPAddress
  Write-Host "Tunnel DNS: OK"
} catch {
  throw "Status API started but tunnel DNS verification failed: $($_.Exception.Message)"
}
