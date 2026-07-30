#!/usr/bin/env pwsh
# UMI Build Script - Strict Gatekeeper & Clean Publish
param(
    [switch]$Release,
    [switch]$Verbose,
    [ValidateSet("slim", "portable", "")]
    [string]$Publish = "",
    [switch]$Installer,
    [switch]$Gui
)

$ErrorActionPreference = "Stop"
# For publish we always use Release; otherwise respect -Release flag
$config = if ($Publish -ne "" -or $Release -or $Gui) { "Release" } else { "Debug" }
$publishDir = "publish"

Write-Host "Beende laufende umi.exe / umi-gui.exe Prozesse..." -ForegroundColor Yellow
$umiProcesses = Get-Process -Name "umi" -ErrorAction SilentlyContinue
if ($umiProcesses) {
    $umiProcesses | Stop-Process -Force
    Start-Sleep -Seconds 1
}
$umiGuiProcesses = Get-Process -Name "umi-gui" -ErrorAction SilentlyContinue
if ($umiGuiProcesses) {
    $umiGuiProcesses | Stop-Process -Force
    Start-Sleep -Seconds 1
}

Write-Host "`n🛡️ Starte Gatekeeper Checks..." -ForegroundColor Cyan
Write-Host "=> Baue Projekt (0-Toleranz für Warnungen)..."
dotnet build src/UMI.CLI/UMI.CLI.csproj -c $config /warnaserror
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ FEHLER: Build fehlgeschlagen oder Warnungen gefunden! Fixe den Code." -ForegroundColor Red
    exit 1
}

Write-Host "=> Führe Unit Tests aus..."
dotnet test src/UMI.CLI/UMI.CLI.csproj -c $config --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ FEHLER: Mindestens ein Unit Test ist fehlgeschlagen!" -ForegroundColor Red
    exit 1
}

# ── CLI Publish (always runs, unless -Gui only without -Publish) ──────────────
$shouldPublishCli = ($Publish -ne "") -or (-not $Gui)

if ($shouldPublishCli) {
    # Determine publish profile (default = slim when no -Publish given)
    $publishProfile = if ($Publish -eq "") { "slim" } else { $Publish }

    # Ensure publish dir exists
    if (-not (Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }

    # Selektiv CLI-Artefakte löschen (kein globales Remove-Item -Recurse!)
    Write-Host "Bereinige CLI-Artefakte in $publishDir/..." -ForegroundColor Cyan
    foreach ($f in @("umi.exe", "umi.pdb", "umi", "README.md", "LICENSE", "THIRD_PARTY_LICENSES.txt")) {
        $target = Join-Path $publishDir $f
        if (Test-Path $target) { Remove-Item -Force $target }
    }

    Write-Host "Publishe UMI CLI ($config, $publishProfile)..." -ForegroundColor Cyan

    if ($publishProfile -eq "portable") {
        # Single-file, self-contained — braucht eigene Compilation (kein --no-build)
        $buildArgs = @(
            "publish", "src/UMI.CLI/UMI.CLI.csproj",
            "-c", $config,
            "-o", $publishDir,
            "-p:PublishSingleFile=true",
            "-p:SelfContained=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:DebugType=none"
        )
    } else {
        # Slim — framework-dependent, Single-File (braucht eigene Compilation, kein --no-build)
        $buildArgs = @(
            "publish", "src/UMI.CLI/UMI.CLI.csproj",
            "-c", $config,
            "-o", $publishDir,
            "--no-self-contained",
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:DebugType=none"
        )
    }

    if ($Verbose) { $buildArgs += "-v", "detailed" }

    dotnet @buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ CLI Publish fehlgeschlagen!" -ForegroundColor Red
        exit 1
    }

    # Extra-Dateien ins publish/ Verzeichnis kopieren
    Write-Host "Kopiere Dokumentation nach $publishDir/..." -ForegroundColor Cyan
    Copy-Item "README.md" "$publishDir/"
    Copy-Item "LICENSE" "$publishDir/"
    Copy-Item "THIRD_PARTY_LICENSES.txt" "$publishDir/"

    # Größe berechnen und Ergebnis anzeigen
    $sizeMB = [math]::Round((Get-ChildItem -Recurse $publishDir | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    $version = & "./$publishDir/umi.exe" --version 2>$null
    Write-Host "`n✅ CLI Publish erfolgreich: $version ($publishProfile, $sizeMB MB)" -ForegroundColor Green
}

# ── GUI Publish (-Gui Switch) ─────────────────────────────────────────────────
if ($Gui) {
    Write-Host "`nPublishe UMI GUI ($config, slim, single-file)..." -ForegroundColor Cyan

    # Ensure publish dir exists (may not exist if -Gui only without -Publish)
    if (-not (Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }

    # Selektiv GUI-Artefakte löschen (kein globales Remove-Item -Recurse!)
    Write-Host "Bereinige GUI-Artefakte in $publishDir/..." -ForegroundColor Cyan
    foreach ($f in @("umi-gui.exe", "umi-gui.pdb")) {
        $target = Join-Path $publishDir $f
        if (Test-Path $target) { Remove-Item -Force $target }
    }

    $guiBuildArgs = @(
        "publish", "src/UMI.GUI/UMI.GUI.csproj",
        "-c", $config,
        "-o", $publishDir,
        "--no-self-contained",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:DebugType=none"
    )

    if ($Verbose) { $guiBuildArgs += "-v", "detailed" }

    dotnet @guiBuildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ GUI Publish fehlgeschlagen!" -ForegroundColor Red
        exit 1
    }

    $guiExe = "$publishDir/umi-gui.exe"
    if (Test-Path $guiExe) {
        $guiSizeMB = [math]::Round((Get-Item $guiExe).Length / 1MB, 1)
        Write-Host "✅ GUI Publish erfolgreich: umi-gui.exe ($guiSizeMB MB) → $publishDir/" -ForegroundColor Green
    } else {
        Write-Host "❌ umi-gui.exe nicht in $publishDir/ gefunden!" -ForegroundColor Red
        exit 1
    }
}

# ── Bundle ExifTool (required for media operations) ────────────────────────
if ($shouldPublishCli -or $Gui) {
    $exifToolSource = "tools/exiftool"
    $exifToolTarget = "$publishDir/tools/exiftool"

    if (Test-Path $exifToolSource) {
        if (-not (Test-Path $exifToolTarget)) {
            Write-Host "Kopiere ExifTool nach $publishDir/tools/exiftool/..." -ForegroundColor Cyan
            New-Item -ItemType Directory -Path "$publishDir/tools" -Force | Out-Null
            Copy-Item -Recurse -Force $exifToolSource $exifToolTarget
        } else {
            Write-Host "ExifTool bereits in $publishDir/tools/exiftool/ vorhanden." -ForegroundColor Gray
        }
    } else {
        Write-Host "⚠️ tools/exiftool nicht gefunden — ExifTool wird nicht gebundelt." -ForegroundColor Yellow
    }
}

# Installer erstellen (optional)
if ($Installer) {
    # Guard: Installer requires CLI to have been published (publishProfile must be set)
    if (-not $shouldPublishCli -or -not $publishProfile) {
        Write-Host "⚠️ Installer benötigt einen CLI-Publish (-Publish slim). Bitte '-Publish slim -Installer' angeben." -ForegroundColor Yellow
        exit 1
    }

    if ($publishProfile -eq "portable") {
        Write-Host "⚠️ Installer ist nur für slim-Builds gedacht, nicht portable." -ForegroundColor Yellow
        exit 1
    }

    Write-Host "`n📦 Erstelle Installer..." -ForegroundColor Cyan

    # Templates-only Config-Staging fuer Installer:
    # publish/config/ enthaelt nach lokalen UMI-Laeufen User-Daten (config.json,
    # eigene Gyroflow-Presets etc.). Wir paketieren NUR git-tracked Files aus
    # config/, damit kein User-Material in den Installer wandert.
    $cleanCfgDir = Join-Path $publishDir "config-clean"
    if (Test-Path $cleanCfgDir) { Remove-Item $cleanCfgDir -Recurse -Force }
    New-Item -ItemType Directory -Path $cleanCfgDir -Force | Out-Null

    Write-Host "Staging config (only git-tracked files) -> $cleanCfgDir/" -ForegroundColor Cyan
    $tracked = & git ls-files config/
    if ($LASTEXITCODE -ne 0 -or -not $tracked) {
        Write-Host "❌ FEHLER: 'git ls-files config/' lieferte nichts. Repo nicht initialisiert?" -ForegroundColor Red
        exit 1
    }
    foreach ($rel in $tracked) {
        $rel = $rel.Trim()
        if (-not $rel) { continue }
        $destRel = $rel -replace '^config/', ''
        $destPath = Join-Path $cleanCfgDir $destRel
        $destDir = Split-Path $destPath -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item -Force $rel $destPath
    }
    $stagedCount = (Get-ChildItem -Recurse -File $cleanCfgDir | Measure-Object).Count
    Write-Host "Staged $stagedCount config files." -ForegroundColor Green

    # iscc.exe finden
    $iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($iscc) {
        $isccPath = $iscc.Source
    } elseif (Test-Path "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") {
        $isccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    } elseif (Test-Path "${env:ProgramFiles}\Inno Setup 6\ISCC.exe") {
        $isccPath = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    } else {
        Write-Host "❌ FEHLER: Inno Setup 6 (iscc.exe) nicht gefunden!" -ForegroundColor Red
        Write-Host "   Installiere Inno Setup 6 von https://jrsoftware.org/isdown.php" -ForegroundColor Yellow
        exit 1
    }

    # Version aus Directory.Build.props lesen (SSOT — beide csproj erben).
    [xml]$buildProps = Get-Content "Directory.Build.props"
    $cliVersion = ($buildProps.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
    if (-not $cliVersion) {
        Write-Host "❌ FEHLER: <Version> Tag nicht in Directory.Build.props gefunden!" -ForegroundColor Red
        exit 1
    }

    Write-Host "=> Version: $cliVersion (aus Directory.Build.props)"
    Write-Host "=> Verwende: $isccPath"

    # Retry-Loop: Bis zu 5 Versuche bei Error 32 (Datei gesperrt, z.B. durch Windows Defender)
    $maxAttempts = 5
    $attempt = 0
    $isccExitCode = -1
    do {
        $attempt++
        & $isccPath "installer\umi-setup.iss" "/DMyAppVersion=$cliVersion"
        $isccExitCode = $LASTEXITCODE

        if ($isccExitCode -eq 0) {
            break
        }

        # ISCC Exit-Code 2 = Compile succeeded with warnings (gilt als OK)
        if ($isccExitCode -eq 2) {
            break
        }

        # Nur bei Exit-Code 1 (iscc meldet Error 32 = Datei gesperrt) → Retry
        # Alle anderen Codes sind echte Compile-Fehler → sofort abbrechen
        if ($isccExitCode -ne 1 -or $attempt -ge $maxAttempts) {
            break
        }
        Write-Host "   Installer-Versuch $attempt fehlgeschlagen (Exit $isccExitCode, moeglicherweise Defender-Lock). Warte 3s..." -ForegroundColor Yellow
        Start-Sleep -Seconds 3
    } while ($attempt -lt $maxAttempts)

    if ($isccExitCode -ne 0 -and $isccExitCode -ne 2) {
        Write-Host "❌ Installer-Erstellung fehlgeschlagen nach $attempt Versuchen (Exit $isccExitCode)!" -ForegroundColor Red
        Write-Host "   Tipp: Defender-Exclusion für installer\Output\ kann helfen." -ForegroundColor Yellow
        exit 1
    }

    if ($attempt -gt 1) {
        Write-Host "   Installer erstellt nach $attempt Versuchen." -ForegroundColor Yellow
    }

    # I-003: gezielt die eben gebaute Version referenzieren ($cliVersion aus
    # Directory.Build.props, SSOT) — nicht das Ordner-Listing, das alphabetisch
    # sortiert und so eine ältere UMI_Setup_2.1.1.exe zuerst greifen würde.
    $setupExe = Get-ChildItem "installer\Output\UMI_Setup_$cliVersion.exe" -ErrorAction SilentlyContinue
    if ($setupExe) {
        $setupSizeMB = [math]::Round($setupExe.Length / 1MB, 1)
        Write-Host "Installer erstellt: $($setupExe.FullName) [$setupSizeMB MB]" -ForegroundColor Green
    }
    # iscc ExitCode 2 (compile with warnings) gilt als Erfolg — explizit zurücksetzen
    # damit build.ps1 mit Exit 0 endet und nicht iscc's Warning-Code durchsickert
    $global:LASTEXITCODE = 0
}
