# DMX Visualiser – instalace aplikace do Meta Questu a přenos nastavení (bez spouštění Windows aplikace).
# DMX Visualiser – installs the app on a Meta Quest and copies settings (without running the Windows app).
# Použití / usage: dvojklik na Quest-install.bat  (nebo: powershell -ExecutionPolicy Bypass -File Quest-install.ps1)
param([switch]$SettingsOnly, [switch]$Yes)   # -Yes = bez dotazů / no prompts

$ErrorActionPreference = 'Continue'   # adb píše průběh na stderr; chyby se kontrolují ručně
$here = $PSScriptRoot
$pkg = 'cz.veks.dmxvisualiser'
$repo = 'VeksCZ/XR_DMX_Visualiser'
$remoteSettings = "/sdcard/Android/data/$pkg/files/settings.json"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Say($cs, $en) { Write-Host "$cs`n  $en" }
function Fail($cs, $en) { Write-Host ""; Write-Host "CHYBA: $cs" -ForegroundColor Red; Write-Host "ERROR: $en" -ForegroundColor Red; if (-not $Yes) { Read-Host "Enter" }; exit 1 }
function Ask($cs, $en) { if ($Yes) { return $true }; $a = Read-Host "$cs / $en [A/n]"; return ($a -eq '' -or $a -match '^[aAyY]') }

# --- ADB ---
$adb = $null
$cands = @((Join-Path $here 'platform-tools\adb.exe'))
if ($env:LOCALAPPDATA) { $cands += (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe') }
$cands += ($env:PATH -split ';' | ? { $_ } | % { Join-Path $_.Trim('"') 'adb.exe' })
foreach ($c in $cands) { if (Test-Path $c) { $adb = $c; break } }
if (-not $adb) {
    Say "ADB (Android Debug Bridge) nebylo nalezeno." "ADB (Android Debug Bridge) was not found."
    if (-not (Ask "Stáhnout oficiální platform-tools od Googlu (~7 MB)?" "Download the official platform-tools from Google (~7 MB)?")) { exit 1 }
    $zip = Join-Path $env:TEMP 'platform-tools.zip'
    Invoke-WebRequest 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' -OutFile $zip -UseBasicParsing -ErrorAction Stop
    Expand-Archive $zip -DestinationPath $here -Force
    $adb = Join-Path $here 'platform-tools\adb.exe'
}
Say "ADB: $adb" "ADB: $adb"

# --- Quest ---
& $adb start-server | Out-Null
$dev = $null; $state = $null
foreach ($line in (& $adb devices -l)) {
    if ($line -match '^(\S+)\s+(device|unauthorized|offline)\b') {
        if (-not $dev -or $line -match 'Quest') { $dev = $Matches[1]; $state = $Matches[2] }
    }
}
if (-not $dev) { Fail "Quest není připojený (USB kabel, vývojářský režim)." "No Quest connected (USB cable, developer mode)." }
if ($state -eq 'unauthorized') { Fail "V brýlích potvrď povolení USB ladění a spusť skript znovu." "Allow USB debugging in the headset and run the script again." }
if ($state -ne 'device') { Fail "Quest je ve stavu $state." "Quest state is $state." }
Say "Quest: $dev" "Quest: $dev"
function Adb { & $adb -s $dev @args 2>&1 | % { "$_" } }

$ver = (Adb shell dumpsys package $pkg | Select-String 'versionName=' | Select-Object -First 1)
if ($ver) { $ver = ($ver.Line -split '=')[1].Trim() }
Say ("Aplikace v Questu: " + $(if ($ver) { $ver } else { 'není nainstalovaná' })) ("App on Quest: " + $(if ($ver) { $ver } else { 'not installed' }))

# --- Instalace APK ---
if (-not $SettingsOnly) {
    $apk = Get-ChildItem $here -Filter *.apk -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($apk) { $apk = $apk.FullName }
    else {
        Say "Stahuji aplikaci pro Quest z GitHubu…" "Downloading the Quest app from GitHub…"
        $rel = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers @{ 'User-Agent' = 'DMXVisualiser' } -ErrorAction Stop
        $asset = $rel.assets | ? { $_.name -like '*.apk' } | Select-Object -First 1
        if (-not $asset) { Fail "Poslední vydání nemá APK." "The latest release has no APK." }
        $apk = Join-Path $env:TEMP $asset.name
        Invoke-WebRequest $asset.browser_download_url -OutFile $apk -UseBasicParsing -ErrorAction Stop
    }
    if (Ask "Nainstalovat $([IO.Path]::GetFileName($apk)) do Questu?" "Install $([IO.Path]::GetFileName($apk)) on the Quest?") {
        Say "Instaluji…" "Installing…"
        $out = Adb install -r -g $apk 2>&1 | Out-String
        if ($out -notmatch 'Success') { Fail "Instalace selhala: $out" "Install failed: $out" }
        Say "Nainstalováno." "Installed."
    }
}

# --- Nastavení ---
$pcSettings = Join-Path $here 'settings.json'
if ((Test-Path $pcSettings) -and (Ask "Přenést nastavení světel z PC do Questu?" "Copy the light settings from the PC to the Quest?")) {
    $pc = Get-Content $pcSettings -Raw -Encoding UTF8 | ConvertFrom-Json
    $tmp = Join-Path $env:TEMP 'dmxvis-quest-settings.json'
    Remove-Item $tmp -ErrorAction SilentlyContinue
    Adb pull $remoteSettings $tmp 2>&1 | Out-Null
    if (Test-Path $tmp) { $q = Get-Content $tmp -Raw -Encoding UTF8 | ConvertFrom-Json }
    else {
        # složku vytvoří aplikace při prvním spuštění
        Adb shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 | Out-Null
        Start-Sleep 4
        $q = $pc.PSObject.Copy()
        $q.cameraPreset = 0
    }
    # z PC: patch světel, kalibrace, hazer, jazyk; VR volby v Questu zůstanou
    foreach ($k in 'fixtures', 'hazeBuildRate', 'hazeDecay', 'roomLight', 'language', 'version') {
        if ($q.PSObject.Properties[$k]) { $q.$k = $pc.$k } else { $q | Add-Member -NotePropertyName $k -NotePropertyValue $pc.$k }
    }
    [IO.File]::WriteAllText($tmp, ($q | ConvertTo-Json -Depth 10))
    Adb push $tmp /data/local/tmp/dmxvis-settings.json | Out-Null
    Adb shell am force-stop $pkg | Out-Null
    Adb shell "cat /data/local/tmp/dmxvis-settings.json > $remoteSettings || exit 1; chmod 666 $remoteSettings 2>/dev/null; rm /data/local/tmp/dmxvis-settings.json; exit 0"
    if ($LASTEXITCODE -ne 0) { Fail "Zápis nastavení do Questu selhal." "Writing settings to the Quest failed." }
    Say "Nastavení přeneseno." "Settings copied."
}

Adb shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 | Out-Null
Write-Host ""
Say "Hotovo – aplikace v Questu se spouští." "Done – the app is starting on the Quest."
if (-not $Yes) { Read-Host "Enter" }
