param(
  [string]$SiteConfig = ".\config\site.local.json",
  [string]$OutDir = ".\runtime"
)

$ErrorActionPreference = "Stop"

function Resolve-Tool([string]$Name) {
  $candidates = @(
    "$env:ProgramFiles\WireGuard\$Name",
    "$env:ProgramFiles(x86)\WireGuard\$Name"
  )
  foreach ($candidate in $candidates) {
    if (Test-Path $candidate) { return $candidate }
  }
  $cmd = Get-Command $Name -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  throw "$Name not found. Install WireGuard for Windows first."
}

function New-WgPrivateKey([string]$WgExe) {
  (& $WgExe genkey).Trim()
}

function Get-WgPublicKey([string]$WgExe, [string]$PrivateKey) {
  $psi = [Diagnostics.ProcessStartInfo]::new()
  $psi.FileName = $WgExe
  $psi.Arguments = "pubkey"
  $psi.RedirectStandardInput = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.UseShellExecute = $false
  $p = [Diagnostics.Process]::Start($psi)
  $p.StandardInput.Write($PrivateKey)
  $p.StandardInput.Close()
  $stdout = $p.StandardOutput.ReadToEnd().Trim()
  $stderr = $p.StandardError.ReadToEnd()
  $p.WaitForExit()
  if ($p.ExitCode -ne 0) { throw "wg pubkey failed: $stderr" }
  return $stdout
}

function New-PresharedKey([string]$WgExe) {
  (& $WgExe genpsk).Trim()
}

function Get-AddressIp([string]$Address) {
  return ($Address -split "/")[0]
}

if (!(Test-Path $SiteConfig)) {
  throw "Site config not found: $SiteConfig. Copy config/site.example.json to config/site.local.json first."
}

$wg = Resolve-Tool "wg.exe"
$wireguard = Resolve-Tool "wireguard.exe"
$site = Get-Content $SiteConfig -Raw | ConvertFrom-Json
$statusApiPort = if ($site.server.statusApiPort) { [int]$site.server.statusApiPort } else { 8787 }
$serverTunnelIp = Get-AddressIp $site.server.tunnelAddress
$publicEndpoint = "$($site.server.publicEndpoint):$($site.server.listenPort)"
$lanEndpoint = if ($site.server.lanEndpoint) { "$($site.server.lanEndpoint):$($site.server.listenPort)" } else { $publicEndpoint }

New-Item -ItemType Directory -Force "$OutDir\server" | Out-Null
New-Item -ItemType Directory -Force "$OutDir\clients" | Out-Null
New-Item -ItemType Directory -Force "$OutDir\keys" | Out-Null

$serverPrivate = New-WgPrivateKey $wg
$serverPublic = Get-WgPublicKey $wg $serverPrivate

$serverPeers = New-Object System.Collections.Generic.List[string]
$manifest = [ordered]@{
  generatedAt = (Get-Date).ToString("o")
  server = [ordered]@{
    name = $site.server.name
    endpoint = $publicEndpoint
    lanEndpoint = $lanEndpoint
    tunnelAddress = $site.server.tunnelAddress
    publicKey = $serverPublic
  }
  clients = @()
}

foreach ($client in $site.clients) {
  $clientPrivate = New-WgPrivateKey $wg
  $clientPublic = Get-WgPublicKey $wg $clientPrivate
  $psk = New-PresharedKey $wg
  $clientIp = Get-AddressIp $client.address

  $serverPeers.Add(@"

[Peer]
# $($client.name) / $($client.account)
PublicKey = $clientPublic
PresharedKey = $psk
AllowedIPs = $clientIp/32
"@)

  $clientConfig = @"
[Interface]
PrivateKey = $clientPrivate
Address = $($client.address)
DNS = $($site.server.dns)

[Peer]
PublicKey = $serverPublic
PresharedKey = $psk
Endpoint = $($site.server.publicEndpoint):$($site.server.listenPort)
AllowedIPs = $($client.allowedIps)
PersistentKeepalive = $($client.persistentKeepalive)
"@

  $clientConfigPath = "$OutDir\clients\$($client.name).conf"
  $clientConfig | Set-Content -Encoding ascii $clientConfigPath

  $clientManifest = [ordered]@{
    profileName = $client.name
    tunnelName = $client.name
    account = $client.account
    endpoint = $publicEndpoint
    lanEndpoint = $lanEndpoint
    statusApiUrl = "http://$serverTunnelIp`:$statusApiPort/status"
    configPath = (Resolve-Path $clientConfigPath).Path
    wireGuardExe = $wireguard
    generatedAt = (Get-Date).ToString("o")
  }
  ($clientManifest | ConvertTo-Json -Depth 8) | Set-Content -Encoding utf8 "$OutDir\clients\$($client.name).json"
  $manifest.clients += $clientManifest
}

$serverConfig = @"
[Interface]
PrivateKey = $serverPrivate
Address = $($site.server.tunnelAddress)
ListenPort = $($site.server.listenPort)
$($serverPeers -join "`r`n")
"@

$serverPath = "$OutDir\server\$($site.server.name).conf"
$serverConfig | Set-Content -Encoding ascii $serverPath
($manifest | ConvertTo-Json -Depth 8) | Set-Content -Encoding utf8 "$OutDir\manifest.json"

Write-Host "Generated:"
Write-Host "  Server config: $serverPath"
Write-Host "  Client configs: $OutDir\clients"
Write-Host "  Manifest: $OutDir\manifest.json"
Write-Warning "runtime contains private keys. Do not commit or share it."
