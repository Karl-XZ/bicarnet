param(
  [string]$Project = ".\apps\windows\DualNetClient\DualNetClient.csproj",
  [string]$Output = ".\dist\windows"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force $Output | Out-Null
dotnet publish $Project -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $Output

$legacyExe = Join-Path $Output "DualNetClient.exe"
if (Test-Path $legacyExe) {
  Remove-Item $legacyExe -Force
}

$activationStore = ".\runtime\activation\activation-codes.json"
if (Test-Path $activationStore) {
  Copy-Item $activationStore (Join-Path $Output "activation-codes.json") -Force
}
$activationPlain = ".\runtime\activation\activation-codes-plain.txt"
if (Test-Path $activationPlain) {
  Copy-Item $activationPlain (Join-Path $Output "activation-codes-plain.txt") -Force
}

Write-Host "Windows client built: $Output\bicarnet.exe"
