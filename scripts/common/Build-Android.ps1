param(
  [string]$AndroidHome = "$env:LOCALAPPDATA\Android\Sdk",
  [string]$GradleVersion = "8.10.2"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "."
$androidProject = Join-Path $root "apps\android"
$tools = Join-Path $root ".tools"
$gradleDir = Join-Path $tools "gradle-$GradleVersion"
$gradleBat = Join-Path $gradleDir "bin\gradle.bat"

if (!(Test-Path $AndroidHome)) {
  throw "Android SDK not found. Pass -AndroidHome or install Android SDK."
}

if (!(Test-Path $gradleBat)) {
  New-Item -ItemType Directory -Force $tools | Out-Null
  $zip = Join-Path $tools "gradle-$GradleVersion-bin.zip"
  if (!(Test-Path $zip)) {
    $url = "https://services.gradle.org/distributions/gradle-$GradleVersion-bin.zip"
    Write-Host "Downloading $url"
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $zip
  }
  Expand-Archive -Force $zip $tools
}

$env:ANDROID_HOME = $AndroidHome
$env:ANDROID_SDK_ROOT = $AndroidHome
Push-Location $androidProject
try {
  & $gradleBat --no-daemon assembleDebug
  if ($LASTEXITCODE -ne 0) { throw "Gradle assembleDebug failed with exit code $LASTEXITCODE." }
} finally {
  Pop-Location
}

New-Item -ItemType Directory -Force ".\dist\android" | Out-Null
Copy-Item ".\apps\android\app\build\outputs\apk\debug\app-debug.apk" ".\dist\android\bicarnet-client-debug.apk" -Force
$legacyApk = ".\dist\android\dualnet-client-debug.apk"
if (Test-Path $legacyApk) {
  Remove-Item $legacyApk -Force
}
Write-Host "Android APK built: .\dist\android\bicarnet-client-debug.apk"
