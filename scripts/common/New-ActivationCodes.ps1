param(
  [int]$Count = 20,
  [string]$OutputDir = ".\runtime\activation",
  [ValidateSet("client", "admin")]
  [string]$Role = "client",
  [switch]$Append,
  [switch]$Force
)

$ErrorActionPreference = "Stop"

function New-Code {
  $alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
  $chars = New-Object char[] 12
  $bytes = New-Object byte[] 12
  $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
  try {
    $rng.GetBytes($bytes)
  } finally {
    $rng.Dispose()
  }
  for ($i = 0; $i -lt 12; $i++) {
    $chars[$i] = $alphabet[$bytes[$i] % $alphabet.Length]
  }
  $raw = -join $chars
  return "BICAR-{0}-{1}-{2}" -f $raw.Substring(0, 4), $raw.Substring(4, 4), $raw.Substring(8, 4)
}

function Normalize-Code([string]$Code) {
  return (($Code.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) } | ForEach-Object { [char]::ToUpperInvariant($_) }) -join "")
}

function Get-Sha256([string]$Value) {
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
    return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join "")
  } finally {
    $sha.Dispose()
  }
}

$storePath = Join-Path $OutputDir "activation-codes.json"
$plainPath = Join-Path $OutputDir "activation-codes-plain.txt"
if (!$Append -and !$Force -and ((Test-Path $storePath) -or (Test-Path $plainPath))) {
  throw "Activation code files already exist. Use -Force to replace: $OutputDir"
}

New-Item -ItemType Directory -Force $OutputDir | Out-Null

$existingStore = $null
$existingPlain = @()
if ($Append -and (Test-Path $storePath)) {
  $existingStore = Get-Content $storePath -Raw | ConvertFrom-Json
}
if ($Append -and (Test-Path $plainPath)) {
  $existingPlain = Get-Content $plainPath
}

$codes = New-Object System.Collections.Generic.HashSet[string]
while ($codes.Count -lt $Count) {
  [void]$codes.Add((New-Code))
}

$records = foreach ($code in $codes) {
  $normalized = Normalize-Code $code
  [ordered]@{
    codeHash = Get-Sha256 $normalized
    codeSuffix = $normalized.Substring([Math]::Max(0, $normalized.Length - 4))
    role = $Role
    redeemed = $false
    deviceIdHash = ""
    deviceName = ""
    platform = ""
    profileName = ""
    account = ""
    tunnelAddress = ""
    activatedAt = ""
    tokenHash = ""
    blocked = $false
    blockedAt = ""
    blockReason = ""
  }
}

$allRecords = @($records)
if ($existingStore -and $existingStore.codes) {
  $allRecords = @($existingStore.codes) + @($records)
}

$store = [ordered]@{
  version = 1
  generatedAt = (Get-Date).ToString("o")
  codes = @($allRecords)
}

($store | ConvertTo-Json -Depth 6) | Set-Content -Encoding utf8 $storePath
$plainBlock = @(
  "# bicarnet one-time activation codes"
  "# Generated: $((Get-Date).ToString("o"))"
  "# Each code can be redeemed once. Do not commit or share this whole file."
  "# Role: $Role"
  ""
  $codes
)
if ($Append -and $existingPlain.Count -gt 0) {
  @($existingPlain + "" + $plainBlock) | Set-Content -Encoding utf8 $plainPath
} else {
  $plainBlock | Set-Content -Encoding utf8 $plainPath
}

Write-Host "Activation codes generated:"
Write-Host "  Plain codes: $plainPath"
Write-Host "  Server store: $storePath"
