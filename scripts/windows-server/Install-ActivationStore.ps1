[CmdletBinding()]
param(
  [string]$SourceDirectory = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

$storeSource = Join-Path $SourceDirectory "activation-codes.json"
$plainSource = Join-Path $SourceDirectory "activation-codes-plain.txt"
if (-not (Test-Path $storeSource)) {
  throw "Missing activation-codes.json in $SourceDirectory"
}

$store = Get-Content -Raw -LiteralPath $storeSource -Encoding UTF8 | ConvertFrom-Json
if (@($store.Codes).Count -eq 0) {
  throw "activation-codes.json does not contain any activation codes."
}

$targetDirectory = Join-Path $env:ProgramData "bicarnet"
New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
Copy-Item -LiteralPath $storeSource -Destination (Join-Path $targetDirectory "activation-codes.json") -Force
if (Test-Path $plainSource) {
  Copy-Item -LiteralPath $plainSource -Destination (Join-Path $targetDirectory "activation-codes-plain.txt") -Force
}

Write-Host "Installed bicarnet activation store to $targetDirectory"
Write-Host "Server API reads this location immediately; no service restart is required."
