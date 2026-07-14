param(
  [int]$RestartDelaySeconds = 10
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

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
$repairSource = Join-Path $repoRoot "scripts\\windows-server\\04-repair-egress-forwarding.ps1"
if (!(Test-Path $repairSource)) {
  throw "Repair script not found: $repairSource"
}

$runtimeDirectory = Join-Path $env:ProgramData "bicarnet"
$repairCopy = Join-Path $runtimeDirectory "repair-egress-forwarding.ps1"
$bootstrapPath = Join-Path $runtimeDirectory "repair-egress-after-reboot.ps1"
$logPath = Join-Path $runtimeDirectory "hyperv-egress-repair.log"
$taskName = "bicarnet-repair-egress-after-hyperv"

New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
Copy-Item -LiteralPath $repairSource -Destination $repairCopy -Force

$bootstrap = @"
`$ErrorActionPreference = 'Stop'
Start-Sleep -Seconds 45
try {
  & '$repairCopy' *>> '$logPath'
} catch {
  "ERROR: `$(`$_.Exception.Message)" | Add-Content -LiteralPath '$logPath'
} finally {
  Unregister-ScheduledTask -TaskName '$taskName' -Confirm:`$false -ErrorAction SilentlyContinue
}
"@
Set-Content -LiteralPath $bootstrapPath -Value $bootstrap -Encoding UTF8 -Force

$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$bootstrapPath`""
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Force | Out-Null

Write-Host "Enabling Microsoft-Hyper-V-All."
Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart | Out-Host

Write-Host "Startup repair task registered: $taskName"
Write-Host "Post-reboot log: $logPath"
Write-Host "Restarting this server in $RestartDelaySeconds seconds."
Start-Sleep -Seconds $RestartDelaySeconds
Restart-Computer -Force
