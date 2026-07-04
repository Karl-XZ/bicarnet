param(
  [string]$Project = ".\apps\windows\DualNetClient\DualNetClient.csproj",
  [string]$Output = ".\dist\windows-admin",
  [string]$RuntimeDir = ".\runtime"
)

$ErrorActionPreference = "Stop"

$windowsConf = Join-Path $RuntimeDir "clients\dualnet-client-windows.conf"
$windowsJson = Join-Path $RuntimeDir "clients\dualnet-client-windows.json"
foreach ($path in @($windowsConf, $windowsJson)) {
  if (!(Test-Path $path)) {
    throw "Required client runtime file not found: $path"
  }
}

New-Item -ItemType Directory -Force $Output | Out-Null
$publishArgs = @(
  "publish", $Project,
  "-c", "Release",
  "-r", "win-x64",
  "--self-contained", "true",
  "-p:PublishSingleFile=true",
  "-p:EnableCompressionInSingleFile=true",
  "-p:IncludeNativeLibrariesForSelfExtract=true",
  "-p:DefineConstants=BICARNET_CLIENT_ONLY%3BBICARNET_ADMIN_ONLY",
  "-p:AssemblyName=bicarnet-admin",
  "-o", $Output
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item $windowsConf (Join-Path $Output "dualnet-client-windows.conf") -Force
$profile = Get-Content $windowsJson -Raw | ConvertFrom-Json
$profile.configPath = (Resolve-Path (Join-Path $Output "dualnet-client-windows.conf")).Path
$profile | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $Output "dualnet-client-windows.json")

$legacyExe = Join-Path $Output "bicarnet.exe"
if (Test-Path $legacyExe) {
  Remove-Item $legacyExe -Force
}

Write-Host "Windows admin client built: $Output\bicarnet-admin.exe"
