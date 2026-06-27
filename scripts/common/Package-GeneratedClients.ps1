param(
  [string]$RuntimeDir = ".\runtime",
  [string]$WindowsDist = ".\dist\windows",
  [string]$AndroidClientName = "dualnet-client-android"
)

$ErrorActionPreference = "Stop"

$androidConf = Join-Path $RuntimeDir "clients\$AndroidClientName.conf"
$androidJson = Join-Path $RuntimeDir "clients\$AndroidClientName.json"
$windowsConf = Join-Path $RuntimeDir "clients\dualnet-client-windows.conf"
$windowsJson = Join-Path $RuntimeDir "clients\dualnet-client-windows.json"
$serverConf = Join-Path $RuntimeDir "server\dualnet-server.conf"

foreach ($path in @($androidConf, $androidJson, $windowsConf, $windowsJson, $serverConf)) {
  if (!(Test-Path $path)) { throw "Missing generated file: $path" }
}

Copy-Item $androidConf ".\apps\android\app\src\main\assets\dualnet-android.conf" -Force
Copy-Item $androidJson ".\apps\android\app\src\main\assets\dualnet-profile.json" -Force

New-Item -ItemType Directory -Force $WindowsDist | Out-Null
Copy-Item $windowsConf (Join-Path $WindowsDist "dualnet-client-windows.conf") -Force
New-Item -ItemType Directory -Force (Join-Path $WindowsDist "server") | Out-Null
Copy-Item $serverConf (Join-Path $WindowsDist "server\dualnet-server.conf") -Force

$profile = Get-Content $windowsJson -Raw | ConvertFrom-Json
$profile.configPath = (Resolve-Path (Join-Path $WindowsDist "dualnet-client-windows.conf")).Path
$profile | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $WindowsDist "dualnet-client-windows.json")
Copy-Item (Join-Path $WindowsDist "dualnet-client-windows.json") ".\apps\windows\DualNetClient\dualnet-client-windows.json" -Force

Write-Host "Packaged generated client/server configs into Android assets and Windows dist."
Write-Host "  Android client: $AndroidClientName"
