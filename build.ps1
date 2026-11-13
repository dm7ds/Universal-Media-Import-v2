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
# Der Installer verpackt CLI, GUI und Hilfe (installer/umi-setup.iss) -> -Installer baut immer
# alles und immer Release; sonst gaebe es CLI und GUI aus verschiedenen Staenden (I-026).
$publishGui = $Gui -or $Installer
# For publish we always use Release; otherwise respect -Release flag
$config = if ($Publish -ne "" -or $Release -or $publishGui) { "Release" } else { "Debug" }
$publishDir = "publish"
$testProject = "tests/UMI.Core.Tests/UMI.Core.Tests.csproj"

# Alle relativen Pfade unten (publish, src/, Directory.Build.props, README.md, tools/, installer\,
# git ls-files config/) und alle Kindprozesse (dotnet, git, ISCC) beziehen sich auf die Repo-Wurzel,
# egal aus welchem Verzeichnis das Skript gestartet wurde.
Set-Location $PSScriptRoot

#region dirty-check
# Der EXE-Stempel (<Version>+<kurzer Commit>) nennt den Commit, nicht lokale Aenderungen. Nur Pfade,
# aus denen Setup und Binaries entstehen (nicht untracked Fremdkram wie Skizzen im Hauptordner).
function Get-PackagedChanges {
    param([Parameter(Mandatory)][string]$Root)
    # Untracked unter config/ ist Nutzerkram (z.B. eigene Gyroflow-Presets), der nie ins Setup kommt (git ls-files).
    @(& git -C $Root status --porcelain -- src docs/help installer config Directory.Build.props README.md LICENSE THIRD_PARTY_LICENSES.txt |
        Where-Object { $_ -notmatch '^\?\? config/' })
}
function Write-DirtyTreeWarning {
    param([string[]]$Changes)
    if (-not $Changes -or $Changes.Count -eq 0) { return }
    Write-Host "⚠️ ACHTUNG: Arbeitsbaum ist NICHT sauber ($($Changes.Count) Aenderung(en) an verpackten Pfaden)." -ForegroundColor Yellow
    Write-Host "   Der EXE-Stempel nennt nur den Commit — dieses Setup enthaelt lokale Aenderungen, die der Stempel nicht zeigt:" -ForegroundColor Yellow
    foreach ($c in ($Changes | Select-Object -First 10)) { Write-Host "     $c" -ForegroundColor Yellow }
    if ($Changes.Count -gt 10) { Write-Host "     ... und $($Changes.Count - 10) weitere" -ForegroundColor Yellow }
}
#endregion dirty-check

# Nur Prozesse beenden, deren EXE aus diesem publish/ stammt (sperren das Ueberschreiben) —
# nie per Dateiname: installierte (Program Files) und Dev-Builds (src/**/bin) laufen weiter.
# Aufgeloest gegen die Skriptwurzel, nicht gegen das .NET-Startverzeichnis (Set-Location aendert
# [Environment]::CurrentDirectory nicht, GetFullPath("publish") traf damit u.U. ein fremdes Verzeichnis).
Write-Host "Beende laufende UMI-Prozesse aus $publishDir/ ..." -ForegroundColor Yellow
$publishFull = (Join-Path $PSScriptRoot $publishDir).TrimEnd('\') + '\'
$lockingProcesses = @(Get-Process -Name "umi", "umi-gui" -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($publishFull, [System.StringComparison]::OrdinalIgnoreCase) })
foreach ($p in $lockingProcesses) {
    Write-Host "   beende PID $($p.Id): $($p.Path)" -ForegroundColor Yellow
    Stop-Process -Id $p.Id -Force
}
if ($lockingProcesses.Count -gt 0) { Start-Sleep -Seconds 1 }

Write-Host "`n🛡️ Starte Gatekeeper Checks..." -ForegroundColor Cyan
Write-Host "=> Baue Projekt (0-Toleranz für Warnungen)..."
dotnet build src/UMI.CLI/UMI.CLI.csproj -c $config /warnaserror
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ FEHLER: Build fehlgeschlagen oder Warnungen gefunden! Fixe den Code." -ForegroundColor Red
    exit 1
}

# Die Tests liegen in $testProject — UMI.CLI hat keine (ein Aufruf dort lief mit Exit 0, ohne einen
# Test auszufuehren). Darum zaehlt auch "0 Tests gelaufen" als Fehler.
Write-Host "=> Führe Unit Tests aus ($testProject)..."
# Summenzeile in fester Sprache (die dotnet-CLI lokalisiert sie sonst: "gesamt" statt "Total"); danach zurueck.
$previousUiLanguage = $env:DOTNET_CLI_UI_LANGUAGE
$env:DOTNET_CLI_UI_LANGUAGE = "en"
try {
    dotnet test $testProject -c $config 2>&1 | Tee-Object -Variable testLines | Out-Host
    $testExit = $LASTEXITCODE
} finally {
    $env:DOTNET_CLI_UI_LANGUAGE = $previousUiLanguage
}
if ($testExit -ne 0) {
    Write-Host "❌ FEHLER: Mindestens ein Unit Test ist fehlgeschlagen (oder der Testlauf selbst)!" -ForegroundColor Red
    exit 1
}
# Summenzeile (englisch erzwungen): "Passed!  - Failed: 0, Passed: 207, Skipped: 0, Total: 207"
$testTotal = 0
foreach ($line in $testLines) {
    if ("$line" -match '(?i)\btotal(?: tests)?:\s*(\d+)') { $testTotal = [int]$Matches[1] }
}
if ($testTotal -le 0) {
    Write-Host "❌ FEHLER: Der Testlauf meldet keinen einzigen Test — das Gate prueft nichts ($testProject)." -ForegroundColor Red
    exit 1
}
Write-Host "=> $testTotal Tests bestanden." -ForegroundColor Green

# ── CLI Publish (always runs, unless -Gui only without -Publish) ──────────────
$shouldPublishCli = ($Publish -ne "") -or (-not $Gui) -or $Installer

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

# ── GUI Publish (-Gui Switch; -Installer implies it) ──────────────────────────
if ($publishGui) {
    Write-Host "`nPublishe UMI GUI ($config, slim, single-file)..." -ForegroundColor Cyan

    # Ensure publish dir exists (may not exist if -Gui only without -Publish)
    if (-not (Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }

    # Selektiv GUI-Artefakte löschen (kein globales Remove-Item -Recurse!)
    Write-Host "Bereinige GUI-Artefakte in $publishDir/..." -ForegroundColor Cyan
    foreach ($f in @("umi-gui.exe", "umi-gui.pdb")) {
        $target = Join-Path $publishDir $f
        if (Test-Path $target) { Remove-Item -Force $target }
    }
    # Hilfe (docs/help) kommt als Content des GUI-Publish; alte Kapitel duerfen nicht liegen bleiben.
    $helpTarget = Join-Path $publishDir "docs/help"
    if (Test-Path $helpTarget) { Remove-Item -Recurse -Force $helpTarget }

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
if ($shouldPublishCli -or $publishGui) {
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

    # I-026: Gate unmittelbar vor ISCC. ISCC verpackt, was in publish/ liegt — auch Altbestand
    # (2.2.1-2.2.5 lokal: aktuelle CLI, aber eine GUI vom Juli). Jede verpackte EXE muss den
    # Stand dieses Commits tragen (ProductVersion = <Version>+<kurzer Commit>, gestempelt von
    # Directory.Build.props), und die Hilfe muss da sein; sonst kein Installer.
    $shortSha = (& git rev-parse --short HEAD) | Select-Object -First 1
    if ($LASTEXITCODE -ne 0 -or -not $shortSha) {
        Write-Host "❌ FEHLER: 'git rev-parse --short HEAD' lieferte nichts — Stand der Binaries nicht prüfbar." -ForegroundColor Red
        exit 1
    }
    $expectedProductVersion = "$cliVersion+$shortSha"
    $gateFindings = @()
    foreach ($packagedExe in @("umi.exe", "umi-gui.exe")) {
        $packagedPath = Join-Path $publishDir $packagedExe
        if (-not (Test-Path $packagedPath)) {
            $gateFindings += "$packagedPath fehlt"
            continue
        }
        $foundProductVersion = (Get-Item $packagedPath).VersionInfo.ProductVersion
        if ($foundProductVersion -ne $expectedProductVersion) {
            $gateFindings += "$packagedPath trägt '$foundProductVersion', erwartet '$expectedProductVersion'"
        }
    }
    $helpIndex = Join-Path $publishDir "docs/help/chapters.json"
    if (-not (Test-Path $helpIndex)) { $gateFindings += "$helpIndex fehlt (Hilfe nicht gepublisht)" }
    # ExifTool liegt als gitignorierte Binaerdatei in tools/exiftool (frischer Worktree: fehlt) —
    # ohne sie bricht ISCC erst mit "No files found" ab.
    if (-not (Test-Path (Join-Path $publishDir "tools/exiftool/exiftool.exe"))) {
        $gateFindings += "$publishDir/tools/exiftool/exiftool.exe fehlt (Quelle tools/exiftool/ nicht vorhanden)"
    }
    if ($gateFindings.Count -gt 0) {
        Write-Host "❌ FEHLER: publish/ enthält keinen aktuellen Stand — Installer wird NICHT gebaut:" -ForegroundColor Red
        foreach ($finding in $gateFindings) { Write-Host "   - $finding" -ForegroundColor Red }
        exit 1
    }
    Write-Host "=> Gate: umi.exe, umi-gui.exe = $expectedProductVersion, Hilfe vorhanden." -ForegroundColor Green
    $packagedChanges = Get-PackagedChanges -Root $PSScriptRoot
    Write-DirtyTreeWarning -Changes $packagedChanges

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
    # Am Ende nochmal, damit die Warnung nicht im ISCC-Log untergeht.
    Write-DirtyTreeWarning -Changes $packagedChanges
    # iscc ExitCode 2 (compile with warnings) gilt als Erfolg — explizit zurücksetzen
    # damit build.ps1 mit Exit 0 endet und nicht iscc's Warning-Code durchsickert
    $global:LASTEXITCODE = 0
}
