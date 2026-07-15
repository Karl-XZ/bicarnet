param(
  [string]$ConfigPath = "",
  [int]$IntervalSeconds = 60
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = [Security.Principal.WindowsPrincipal]::new($identity)
  if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script as Administrator."
  }
}

function Get-ConfigValue {
  param([string[]]$Lines, [string]$Name, [int]$StartIndex = 0)
  for ($index = $StartIndex; $index -lt $Lines.Count; $index++) {
    if ($Lines[$index] -match "^$([regex]::Escape($Name))\s*=\s*(.+)$") {
      return $Matches[1].Trim()
    }
  }
  return ""
}

Assert-Admin

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
  $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
  $ConfigPath = Join-Path $repoRoot "dist\\windows\\dualnet-client-windows.conf"
}
if (!(Test-Path -LiteralPath $ConfigPath)) {
  throw "WireGuard client config not found: $ConfigPath"
}

$lines = Get-Content -LiteralPath $ConfigPath
$peerIndex = -1
for ($index = 0; $index -lt $lines.Count; $index++) {
  if ($lines[$index].Trim().Equals("[Peer]", [StringComparison]::OrdinalIgnoreCase)) {
    $peerIndex = $index
    break
  }
}
if ($peerIndex -lt 0) { throw "No [Peer] section found in $ConfigPath" }

$endpoint = Get-ConfigValue -Lines $lines -Name "Endpoint" -StartIndex $peerIndex
$peerPublicKey = Get-ConfigValue -Lines $lines -Name "PublicKey" -StartIndex $peerIndex
$tunnelName = [IO.Path]::GetFileNameWithoutExtension($ConfigPath)
if ([string]::IsNullOrWhiteSpace($endpoint) -or [string]::IsNullOrWhiteSpace($peerPublicKey)) {
  throw "Endpoint or peer public key is missing from $ConfigPath"
}

$separator = $endpoint.LastIndexOf(':')
if ($separator -le 0) { throw "Endpoint must be host:port: $endpoint" }
$hostName = $endpoint.Substring(0, $separator).Trim('[', ']')
$port = $endpoint.Substring($separator + 1)
if ($hostName -match '^\d{1,3}(\.\d{1,3}){3}$') {
  throw "Use a DDNS hostname in Endpoint, not a fixed IPv4 address: $endpoint"
}
$parsedPort = 0
if (![int]::TryParse($port, [ref]$parsedPort)) { throw "Endpoint port is invalid: $endpoint" }

$wireGuardExe = Join-Path $env:ProgramFiles "WireGuard\\wg.exe"
if (!(Test-Path -LiteralPath $wireGuardExe)) { throw "wg.exe not found: $wireGuardExe" }

$runtimeDirectory = Join-Path $env:ProgramData "bicarnet"
$workerPath = Join-Path $runtimeDirectory "ddns-refresh-worker.ps1"
$statePath = Join-Path $runtimeDirectory "ddns-refresh-endpoint.txt"
$logPath = Join-Path $runtimeDirectory "ddns-refresh.log"
$taskName = "bicarnet-ddns-refresh-$tunnelName"
New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null

$worker = @"
`$ErrorActionPreference = 'Continue'
while (`$true) {
  try {
    `$ip = Resolve-DnsName -Name '$hostName' -Type A -DnsOnly -ErrorAction Stop |
      Where-Object { `$_.IPAddress -match '^\d{1,3}(\.\d{1,3}){3}$' } |
      Select-Object -First 1 -ExpandProperty IPAddress
    if (![string]::IsNullOrWhiteSpace(`$ip)) {
      `$target = "`$(`$ip):$port"
      `$previous = if (Test-Path '$statePath') { (Get-Content '$statePath' -Raw).Trim() } else { '' }
      if (`$target -ne `$previous) {
        & '$wireGuardExe' set '$tunnelName' peer '$peerPublicKey' endpoint `$target
        if (`$LASTEXITCODE -eq 0) {
          Set-Content -LiteralPath '$statePath' -Value `$target -Encoding ASCII
          "`$(Get-Date -Format o) endpoint updated: `$previous -> `$target" | Add-Content -LiteralPath '$logPath'
        }
      }
    }
  } catch {
    "`$(Get-Date -Format o) refresh error: `$(`$_.Exception.Message)" | Add-Content -LiteralPath '$logPath'
  }
  Start-Sleep -Seconds $IntervalSeconds
}
"@
Set-Content -LiteralPath $workerPath -Value $worker -Encoding UTF8 -Force

$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$workerPath`""
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Description "Refreshes the bicarnet client WireGuard endpoint from DDNS." -Force | Out-Null
$task = Get-ScheduledTask -TaskName $taskName
if ($task.State -ne "Running") { Start-ScheduledTask -TaskName $taskName }
Start-Sleep -Seconds 3

Write-Host "=== bicarnet DDNS refresh ==="
Get-ScheduledTask -TaskName $taskName | Select-Object TaskName, State
Write-Host "Hostname: $hostName"
Write-Host "Refresh interval: $IntervalSeconds seconds"
if (Test-Path $statePath) { Write-Host "Active endpoint: $((Get-Content $statePath -Raw).Trim())" }
