param(
  [Parameter(Mandatory = $true)]
  [string]$Endpoint,
  [string]$SiteConfig = ".\config\site.local.json",
  [string]$RuntimeDir = ".\runtime",
  [switch]$PackageClients,
  [switch]$BuildAndroid
)

$ErrorActionPreference = "Stop"

function Normalize-Endpoint([string]$Value, [int]$DefaultPort) {
  $endpointValue = $Value.Trim()
  if ($endpointValue.StartsWith("http://")) { $endpointValue = $endpointValue.Substring(7) }
  if ($endpointValue.StartsWith("https://")) { $endpointValue = $endpointValue.Substring(8) }
  $slash = $endpointValue.IndexOf("/")
  if ($slash -ge 0) { $endpointValue = $endpointValue.Substring(0, $slash) }
  $endpointValue = $endpointValue.Trim()
  if (!$endpointValue) { throw "Endpoint is empty." }

  $endpointHost = $endpointValue
  $port = $DefaultPort
  $lastColon = $endpointValue.LastIndexOf(":")
  if ($lastColon -gt 0 -and $lastColon -lt $endpointValue.Length - 1) {
    $candidatePort = 0
    if ([int]::TryParse($endpointValue.Substring($lastColon + 1), [ref]$candidatePort)) {
      $endpointHost = $endpointValue.Substring(0, $lastColon)
      $port = $candidatePort
    }
  }

  if ($endpointHost -match "^\d{1,3}(\.\d{1,3}){3}$") {
    Write-Warning "Endpoint is an IPv4 address. For long-term use, pass a DDNS domain instead, for example vpn.example.com:$port."
  }

  return [pscustomobject]@{
    Host = $endpointHost
    Port = $port
    Endpoint = "${endpointHost}:$port"
  }
}

function Set-ClientConfigEndpoint([string]$Path, [string]$EndpointValue) {
  if (!(Test-Path $Path)) { return }
  $text = Get-Content $Path -Raw
  if ($text -match "(?m)^Endpoint\s*=") {
    $text = $text -replace "(?m)^Endpoint\s*=\s*.*$", "Endpoint = $EndpointValue"
  } else {
    $text = $text -replace "(?m)^AllowedIPs\s*=", "Endpoint = $EndpointValue`r`nAllowedIPs ="
  }
  Set-Content -Encoding ascii -Path $Path -Value $text
}

if (!(Test-Path $SiteConfig)) {
  throw "Site config not found: $SiteConfig"
}

$site = Get-Content $SiteConfig -Raw | ConvertFrom-Json
$defaultPort = if ($site.server.listenPort) { [int]$site.server.listenPort } else { 51820 }
$normalized = Normalize-Endpoint $Endpoint $defaultPort

$site.server.publicEndpoint = $normalized.Host
$site.server.listenPort = $normalized.Port
$site | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $SiteConfig

$clientsDir = Join-Path $RuntimeDir "clients"
if (Test-Path $clientsDir) {
  Get-ChildItem $clientsDir -Filter "*.conf" -File | ForEach-Object {
    Set-ClientConfigEndpoint $_.FullName $normalized.Endpoint
  }
  Get-ChildItem $clientsDir -Filter "*.json" -File | ForEach-Object {
    $json = Get-Content $_.FullName -Raw | ConvertFrom-Json
    $json.endpoint = $normalized.Endpoint
    $json | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $_.FullName
  }
}

$manifestPath = Join-Path $RuntimeDir "manifest.json"
if (Test-Path $manifestPath) {
  $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
  $manifest.server.endpoint = $normalized.Endpoint
  foreach ($client in $manifest.clients) {
    $client.endpoint = $normalized.Endpoint
  }
  $manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $manifestPath
}

Write-Host "Public endpoint set to: $($normalized.Endpoint)"
Write-Host "Site config: $SiteConfig"
Write-Host "Runtime dir: $RuntimeDir"

if ($PackageClients) {
  & (Join-Path $PSScriptRoot "Package-GeneratedClients.ps1") -RuntimeDir $RuntimeDir
}

if ($BuildAndroid) {
  & (Join-Path $PSScriptRoot "Build-Android.ps1")
}
