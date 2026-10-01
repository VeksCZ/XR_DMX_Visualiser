# Vydání nové verze na GitHub Releases (pro aktualizátor v aplikaci).
# Postup:
#   1) zvyš Version v Assets/Visualizer/Editor/BuildVisualizer.cs
#   2) v Unity: Tools > Visualizer > Build Windows EXE
#   3) spusť:  .\release.ps1   (volitelně -Notes "co je nového")
param([string]$Notes = "")

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Get-Content (Join-Path $root 'Assets\Visualizer\Editor\BuildVisualizer.cs') -Raw
if ($src -notmatch 'Version\s*=\s*"([^"]+)"') { throw 'Verzi v BuildVisualizer.cs nejde najít.' }
$ver = $Matches[1]
$tag = "v$ver"

$build = Join-Path $root 'Build'
$exe = Join-Path $build 'Windows\DMXVisualiser.exe'
if (-not (Test-Path $exe)) { throw "Chybí build ($exe). Nejdřív Tools > Visualizer > Build Windows EXE." }

$stage = Join-Path $build 'release\DMXVisualiser'
Remove-Item (Join-Path $build 'release') -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $stage -Force | Out-Null
Get-ChildItem (Join-Path $build 'Windows') | Where-Object { $_.Name -notlike '*DoNotShip*' -and $_.Name -ne 'settings.json' } | Copy-Item -Destination $stage -Recurse -Force

$zip = Join-Path $build "DMXVisualiser-$tag-win64.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path $stage -DestinationPath $zip
Write-Host ("Zip: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))

if ([string]::IsNullOrWhiteSpace($Notes)) { $Notes = "DMX Visualiser $ver" }
gh release create $tag $zip --title "DMX Visualiser $ver" --notes $Notes
Write-Host "Vydáno: $tag"
