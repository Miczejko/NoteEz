<#
.SYNOPSIS
  Kompiluje firmware ESP32 (NoteEz-ESP32) i wgrywa go na plytke przez arduino-cli.

.DESCRIPTION
  Domyslnie bierze szkic z glownego worktree projektu (katalog nadrzedny wzgledem scripts/).
  Katalog budowania jest staly (poza repo), wiec kolejne kompilacje sa przyrostowe (~kilkanascie sekund);
  pierwsza kompilacja z nowym katalogiem budowania trwa kilka minut (kompilacja rdzenia ESP32).

.PARAMETER Port
  Port COM plytki (np. COM5). Gdy pominiety, skrypt probuje wykryc jedyny podlaczony port szeregowy.

.PARAMETER Worktree
  Katalog glowny repo/worktree, z ktorego bierzemy szkic (np. C:\Users\user\Desktop\Projekty\NoteEz-wt-drawing).
  Domyslnie: glowny worktree (katalog nadrzedny wzgledem scripts/).

.PARAMETER BuildPath
  Katalog budowania (cache). Domyslnie $env:LOCALAPPDATA\NoteEz-build\esp32.

.PARAMETER NoUpload
  Tylko kompilacja, bez wgrywania.

.EXAMPLE
  .\scripts\flash.ps1 -Port COM5
  .\scripts\flash.ps1                                   # autodetekcja portu
  .\scripts\flash.ps1 -Port COM5 -Worktree C:\Users\user\Desktop\Projekty\NoteEz-wt-drawing
  .\scripts\flash.ps1 -NoUpload                         # tylko kompilacja

  Przed wgrywaniem zamknij monitor portu szeregowego (otwarcie go resetuje plytke i blokuje port).
#>
param(
  [string]$Port,
  [string]$Worktree,
  [string]$BuildPath,
  [switch]$NoUpload
)

$ErrorActionPreference = "Stop"

$cli = "C:\Program Files\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe"
$fqbn = "esp32:esp32:esp32c6:CDCOnBoot=cdc,CPUFreq=160,FlashFreq=80,FlashMode=qio,FlashSize=4M,PartitionScheme=huge_app,DebugLevel=none"

if (-not (Test-Path $cli)) {
  throw "Nie znaleziono arduino-cli: $cli"
}

if (-not $Worktree) {
  $Worktree = Split-Path -Parent $PSScriptRoot
}
$sketch = Join-Path $Worktree "NoteEz-ESP32"
if (-not (Test-Path (Join-Path $sketch "NoteEz-ESP32.ino"))) {
  throw "Nie znaleziono szkicu: $sketch\NoteEz-ESP32.ino"
}

if (-not $BuildPath) {
  $BuildPath = Join-Path $env:LOCALAPPDATA "NoteEz-build\esp32"
}
New-Item -ItemType Directory -Force -Path $BuildPath | Out-Null

if (-not $NoUpload -and -not $Port) {
  # autodetekcja: bierzemy port, tylko gdy jest dokladnie jeden port szeregowy
  $json = & $cli board list --format json | Out-String
  $parsed = $json | ConvertFrom-Json
  $ports = @()
  if ($parsed.PSObject.Properties.Name -contains "detected_ports") {
    $ports = @($parsed.detected_ports | Where-Object { $_.port.protocol -eq "serial" } | ForEach-Object { $_.port.address })
  } else {
    $ports = @($parsed | Where-Object { $_.port.protocol -eq "serial" } | ForEach-Object { $_.port.address })
  }
  if ($ports.Count -eq 1) {
    $Port = $ports[0]
    Write-Host "Wykryty port: $Port"
  } else {
    throw "Podaj port: -Port COMx (wykryto portow szeregowych: $($ports.Count): $($ports -join ', '))"
  }
}

Write-Host "Szkic:           $sketch"
Write-Host "Katalog budowy:  $BuildPath"

$args = @("compile", "--fqbn", $fqbn, "--build-path", $BuildPath)
if (-not $NoUpload) {
  $args += @("--upload", "-p", $Port)
}
$args += $sketch

& $cli @args
if ($LASTEXITCODE -ne 0) {
  throw "arduino-cli zakonczyl sie bledem (kod $LASTEXITCODE)"
}
Write-Host "Gotowe."
