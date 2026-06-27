param(
  [Parameter(Mandatory = $true)]
  [string]$DeviceName,
  [string]$Account = "",
  [string]$Address = "",
  [string]$AllowedIps = "0.0.0.0/0",
  [string]$SiteConfig = ".\config\site.local.json",
  [string]$RuntimeDir = ".\runtime",
  [string]$WindowsDist = ".\dist\windows"
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

function Get-AddressIp([string]$AddressValue) {
  return ($AddressValue -split "/")[0]
}

function Get-Slug([string]$Value) {
  $slug = $Value.ToLowerInvariant() -replace "[^a-z0-9]+", "-"
  $slug = $slug.Trim("-")
  if ($slug.Length -eq 0) { $slug = "phone" }
  return $slug
}

function Get-NextClientAddress($Clients, [string]$ServerTunnelAddress) {
  $serverIp = Get-AddressIp $ServerTunnelAddress
  $parts = $serverIp.Split(".")
  if ($parts.Length -ne 4) { throw "Only IPv4 tunnel addresses are supported by this helper: $ServerTunnelAddress" }
  $prefix = "$($parts[0]).$($parts[1]).$($parts[2])."
  $used = @{}
  foreach ($client in $Clients) {
    $ip = Get-AddressIp $client.address
    if ($ip.StartsWith($prefix)) {
      $last = 0
      if ([int]::TryParse($ip.Split(".")[-1], [ref]$last)) {
        $used[$last] = $true
      }
    }
  }
  $serverLast = 0
  if ([int]::TryParse($parts[3], [ref]$serverLast)) {
    $used[$serverLast] = $true
  }
  for ($i = 20; $i -lt 255; $i++) {
    if (!$used.ContainsKey($i)) { return "$prefix$i/32" }
  }
  throw "No free client address found in $prefix.0/24"
}

if (!(Test-Path $SiteConfig)) {
  throw "Site config not found: $SiteConfig"
}

$manifestPath = Join-Path $RuntimeDir "manifest.json"
if (!(Test-Path $manifestPath)) {
  throw "Runtime manifest not found: $manifestPath. Generate the base tunnel once before adding phone clients."
}

$serverConfigPath = Join-Path $RuntimeDir "server\dualnet-server.conf"
if (!(Test-Path $serverConfigPath)) {
  throw "Runtime server config not found: $serverConfigPath"
}

$wg = Resolve-Tool "wg.exe"
$wireguard = Resolve-Tool "wireguard.exe"
$site = Get-Content $SiteConfig -Raw | ConvertFrom-Json
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

$slug = Get-Slug $DeviceName
$clientName = "bicarnet-android-$slug"
if ([string]::IsNullOrWhiteSpace($Account)) {
  $Account = $DeviceName.Trim()
}
if ([string]::IsNullOrWhiteSpace($Account)) {
  $Account = $clientName
}

$clients = @($site.clients)
if ($clients | Where-Object { $_.name -eq $clientName }) {
  throw "Client already exists in site config: $clientName"
}

if ([string]::IsNullOrWhiteSpace($Address)) {
  $Address = Get-NextClientAddress $clients $site.server.tunnelAddress
}
$clientIp = Get-AddressIp $Address
if ($clients | Where-Object { (Get-AddressIp $_.address) -eq $clientIp }) {
  throw "Client address already exists: $Address"
}

$statusApiPort = if ($site.server.statusApiPort) { [int]$site.server.statusApiPort } else { 8787 }
$serverTunnelIp = Get-AddressIp $site.server.tunnelAddress
$publicEndpoint = "$($site.server.publicEndpoint):$($site.server.listenPort)"
$lanEndpoint = if ($site.server.lanEndpoint) { "$($site.server.lanEndpoint):$($site.server.listenPort)" } else { $publicEndpoint }
$serverPublic = $manifest.server.publicKey
if ([string]::IsNullOrWhiteSpace($serverPublic)) {
  throw "Server public key is missing from $manifestPath"
}

New-Item -ItemType Directory -Force (Join-Path $RuntimeDir "clients") | Out-Null

$clientPrivate = New-WgPrivateKey $wg
$clientPublic = Get-WgPublicKey $wg $clientPrivate
$psk = New-PresharedKey $wg

$clientConfig = @"
[Interface]
PrivateKey = $clientPrivate
Address = $Address
DNS = $($site.server.dns)

[Peer]
PublicKey = $serverPublic
PresharedKey = $psk
Endpoint = $($site.server.publicEndpoint):$($site.server.listenPort)
AllowedIPs = $AllowedIps
PersistentKeepalive = 25
"@

$clientConfigPath = Join-Path $RuntimeDir "clients\$clientName.conf"
$clientConfig | Set-Content -Encoding ascii $clientConfigPath

$clientManifest = [ordered]@{
  profileName = $clientName
  tunnelName = $clientName
  account = $Account
  endpoint = $publicEndpoint
  lanEndpoint = $lanEndpoint
  statusApiUrl = "http://$serverTunnelIp`:$statusApiPort/status"
  configPath = (Resolve-Path $clientConfigPath).Path
  wireGuardExe = $wireguard
  generatedAt = (Get-Date).ToString("o")
}
($clientManifest | ConvertTo-Json -Depth 8) | Set-Content -Encoding utf8 (Join-Path $RuntimeDir "clients\$clientName.json")

$serverPeer = @"

[Peer]
# $clientName / $Account
PublicKey = $clientPublic
PresharedKey = $psk
AllowedIPs = $clientIp/32
"@
Add-Content -Encoding ascii -Path $serverConfigPath -Value $serverPeer

$newSiteClient = [ordered]@{
  name = $clientName
  account = $Account
  address = $Address
  allowedIps = $AllowedIps
  persistentKeepalive = 25
}
$site.clients = @($clients + ([pscustomobject]$newSiteClient))
($site | ConvertTo-Json -Depth 8) | Set-Content -Encoding utf8 $SiteConfig

$manifestClients = @($manifest.clients)
$manifest.clients = @($manifestClients + ([pscustomobject]$clientManifest))
($manifest | ConvertTo-Json -Depth 8) | Set-Content -Encoding utf8 $manifestPath

if ($WindowsDist) {
  New-Item -ItemType Directory -Force (Join-Path $WindowsDist "server") | Out-Null
  Copy-Item $serverConfigPath (Join-Path $WindowsDist "server\dualnet-server.conf") -Force
}

Write-Host "Added Android client:"
Write-Host "  Name: $clientName"
Write-Host "  Account: $Account"
Write-Host "  Address: $Address"
Write-Host "  Config: $clientConfigPath"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  .\scripts\common\Package-GeneratedClients.ps1 -AndroidClientName $clientName"
Write-Host "  .\scripts\common\Build-Android.ps1"
Write-Host "  Restart the bicarnet server so WireGuard loads the updated server peer list."
