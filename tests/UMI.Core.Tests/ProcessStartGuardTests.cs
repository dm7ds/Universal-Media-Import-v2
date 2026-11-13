// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Wächter (I-021): Prozesse werden nur in bekannten Dateien gestartet, und nur die URL-Öffner starten
/// per ShellExecute. Ein neuer Aufruf irgendwo in <c>src/</c> — insbesondere ein ungeprüfter Start des
/// heruntergeladenen Installers — muss hier auffallen und bewusst in die Allowlist eingetragen werden.
/// Beidseitig: Ein Allowlist-Eintrag ohne Treffer in seiner Datei ist ebenfalls rot, damit die Liste
/// nicht verrottet. Die Liste wurde per Grep über alle <c>src/**/*.cs</c> erhoben, nicht geraten.
///
/// Grenzen (im Security-Audit gemessen, 2026-11-13): Der Wächter arbeitet zeilenweise auf
/// Dateiebene. Er erkennt NICHT: einen zusätzlichen Start in einer bereits gelisteten Datei, einen
/// Alias (<c>using P = System.Diagnostics.Process; P.Start(...)</c>), <c>new Process()</c> mit
/// späterem <c>p.Start()</c> über eine Variable, und einen Aufruf, der über einen Zeilenumbruch
/// zwischen <c>Process</c> und <c>.Start(</c> verteilt ist. Er fängt den versehentlichen Fall, nicht
/// den absichtlich versteckten — für Letzteres bleibt das Review zuständig.
/// </summary>
public class ProcessStartGuardTests
{
    /// <summary>Dateien, die einen Prozess starten (<c>Process.Start(</c> oder <c>new Process</c>), mit Grund.</summary>
    private static readonly IReadOnlyDictionary<string, string> ProcessStartAllowlist = new Dictionary<string, string>
    {
        ["src/UMI.Core/Services/InstallerLauncher.cs"] =
            "Einzige Stelle, die den heruntergeladenen Installer startet — erst nach erneuter SHA256-Prüfung (I-021)",
        ["src/UMI.Core/Services/GyroflowService.cs"] = "Gyroflow-CLI als Kindprozess, UseShellExecute=false",
        ["src/UMI.Core/Services/PostProcessingService.cs"] = "ExifTool-Aufruf (Metadaten-Kopie) nach Stabilisierung, UseShellExecute=false",
        ["src/UMI.Core/Utilities/ExifToolWrapper.cs"] = "ExifTool-Wrapper, UseShellExecute=false",
        ["src/UMI.Core/Utilities/FFprobeWrapper.cs"] = "FFprobe-Wrapper, UseShellExecute=false",
        ["src/UMI.Core/Wizards/Steps/ExifToolStep.cs"] = "ExifTool-Aufruf im Setup-Wizard, UseShellExecute=false",
        ["src/UMI.Core/Wizards/Steps/ToolDetectionHelper.cs"] = "Tool-Erkennung über `where`, UseShellExecute=false",
        ["src/UMI.GUI/ViewModels/ToolsViewModel.cs"] = "Öffnet den Explorer auf das erzeugte Support-Bundle (explorer.exe /select); der Werkzeug-Download startet nichts (I-022)",
        ["src/UMI.GUI/ViewModels/Wizard/Steps/ToolsStepViewModel.cs"] = "OpenUrlCommand: öffnet eine im XAML gebundene Download-Seite im Browser",
        ["src/UMI.GUI/Views/AboutDialog.xaml.cs"] = "Hyperlink-Navigation der About-Seite im Browser",
        ["src/UMI.GUI/Views/HelpWindow.xaml.cs"] = "Öffnet nur http(s)-Links der Hilfe im Browser",
    };

    /// <summary>
    /// Dateien mit <c>UseShellExecute = true</c>. Außer dem Installer-Start nur URL-Öffner —
    /// ShellExecute auf eine Datei/EXE gehört ausschließlich in <c>InstallerLauncher</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ShellExecuteAllowlist = new Dictionary<string, string>
    {
        ["src/UMI.Core/Services/InstallerLauncher.cs"] = "Installer-Start per Shell (UAC) nach SHA256-Prüfung",
        ["src/UMI.GUI/ViewModels/Wizard/Steps/ToolsStepViewModel.cs"] = "URL im Browser öffnen",
        ["src/UMI.GUI/Views/AboutDialog.xaml.cs"] = "URL im Browser öffnen",
        ["src/UMI.GUI/Views/HelpWindow.xaml.cs"] = "http(s)-URL im Browser öffnen",
    };

    private static readonly Regex ProcessStartPattern =
        new(@"\bProcess\.Start\s*\(|\bnew\s+(System\.Diagnostics\.)?Process\b", RegexOptions.Compiled);

    private static readonly Regex ShellExecutePattern =
        new(@"UseShellExecute\s*=\s*true\b", RegexOptions.Compiled);

    [Fact]
    public void ProcessStart_OnlyInAllowlistedFiles_AndEveryAllowlistEntryStillHits()
    {
        var hits = SourceGuard.FilesMatching(ProcessStartPattern);

        AssertAllowlistMatches(hits, ProcessStartAllowlist, "Process.Start / new Process");
    }

    [Fact]
    public void ShellExecute_OnlyInAllowlistedFiles_AndEveryAllowlistEntryStillHits()
    {
        var hits = SourceGuard.FilesMatching(ShellExecutePattern);

        AssertAllowlistMatches(hits, ShellExecuteAllowlist, "UseShellExecute = true");
    }

    /// <summary>Testet die Prüflogik selbst: ein Treffer außerhalb und ein Eintrag ohne Treffer müssen auffallen.</summary>
    [Fact]
    public void AssertAllowlistMatches_FlagsBothDirections()
    {
        var allow = new Dictionary<string, string> { ["a.cs"] = "x", ["unused.cs"] = "y" };

        var unknown = () => AssertAllowlistMatches(new SortedSet<string> { "a.cs", "rogue.cs" }, allow, "p");
        var stale = () => AssertAllowlistMatches(new SortedSet<string> { "a.cs" }, allow, "p");
        var ok = () => AssertAllowlistMatches(new SortedSet<string> { "a.cs", "unused.cs" }, allow, "p");

        unknown.Should().Throw<Exception>().WithMessage("*rogue.cs*");
        stale.Should().Throw<Exception>().WithMessage("*unused.cs*");
        ok.Should().NotThrow();
    }

    private static void AssertAllowlistMatches(
        IReadOnlySet<string> hits, IReadOnlyDictionary<string, string> allowlist, string what)
        => SourceGuard.AssertAllowlistMatches(hits, allowlist, what,
            "Ungeprüfte Starts des Installers sind verboten — der Weg ist IUpdateService.LaunchVerifiedInstallerAsync.");
}
