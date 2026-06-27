param(
  [string]$SiteConfig = ".\config\site.local.json",
  [string]$PublicEndpoint = "",
  [string]$LanEndpoint = "",
  [string]$TunnelAddress = "",
  [switch]$DetectPublicIp,
  [switch]$DetectLanIp
)

$ErrorActionPreference = "Stop"

function Get-PrimaryLanIp {
  $candidates = Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object {
      $_.IPAddress -notlike "169.254.*" -and
      $_.IPAddress -ne "127.0.0.1" -and
      $_.InterfaceAlias -notlike "Loopback*" -and
      $_.InterfaceAlias -notlike "vEthernet*" -and
      $_.InterfaceAlias -notlike "dualnet-*"
    } |
    Sort-Object InterfaceMetric, InterfaceAlias

  $ip = $candidates | Select-Object -First 1 -ExpandProperty IPAddress
  if (!$ip) { throw "Unable to detect LAN IP. Pass -LanEndpoint manually." }
  return $ip
}

if (!(Test-Path $SiteConfig)) {
  throw "Site config not found: $SiteConfig"
}

$cfg = Get-Content $SiteConfig -Raw | ConvertFrom-Json

if ($DetectPublicIp) {
  $PublicEndpoint = (Invoke-RestMethod -UseBasicParsing -Uri "https://api.ipify.org" -TimeoutSec 15).Trim()
}

if ($DetectLanIp) {
  $LanEndpoint = Get-PrimaryLanIp
}

if ($PublicEndpoint) {
  $cfg.server.publicEndpoint = $PublicEndpoint
}

if ($LanEndpoint) {
  $cfg.server | Add-Member -NotePropertyName lanEndpoint -NotePropertyValue $LanEndpoint -Force
}

if ($TunnelAddress) {
  $cfg.server.tunnelAddress = $TunnelAddress
}

$cfg | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $SiteConfig

Write-Host "Updated $SiteConfig"
Write-Host "  publicEndpoint: $($cfg.server.publicEndpoint)"
Write-Host "  lanEndpoint:    $($cfg.server.lanEndpoint)"
Write-Host "  tunnelAddress:  $($cfg.server.tunnelAddress)"
Write-Host ""
Write-Host "Next:"
Write-Host "  .\scripts\windows-server\01-generate-wireguard-config.ps1 -SiteConfig $SiteConfig"
Write-Host "  .\scripts\common\Package-GeneratedClients.ps1"
