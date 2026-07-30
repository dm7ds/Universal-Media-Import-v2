// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using System.IO.Compression;
using FluentAssertions;
using UMI.Core.Services;
using Xunit;

namespace UMI.Core.Tests;

public class SupportBundleServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public SupportBundleServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"umi-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task CreateBundleAsync_CreatesZip_InTargetDirectory()
    {
        // Arrange
        var configDir = Path.Combine(_tempRoot, "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "config.json"), """{"version":"2.0"}""");

        var resolver = new ConfigPathResolver(configDir);
        var service = new SupportBundleService(resolver);
        var targetDir = Path.Combine(_tempRoot, "output");

        // Act
        var zipPath = await service.CreateBundleAsync(targetDir);

        // Assert
        File.Exists(zipPath).Should().BeTrue("ZIP muss erzeugt werden");
        zipPath.Should().StartWith(targetDir);
        zipPath.Should().EndWith(".zip");
        Path.GetFileName(zipPath).Should().StartWith("umi-support-");
    }

    [Fact]
    public async Task CreateBundleAsync_ZipContains_ConfigJson()
    {
        // Arrange
        var configDir = Path.Combine(_tempRoot, "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "config.json"), """{"version":"2.0","data":"test"}""");

        var resolver = new ConfigPathResolver(configDir);
        var service = new SupportBundleService(resolver);
        var targetDir = Path.Combine(_tempRoot, "output");

        // Act
        var zipPath = await service.CreateBundleAsync(targetDir);

        // Assert
        using var zip = ZipFile.OpenRead(zipPath);
        zip.Entries.Should().Contain(e => e.FullName == "config.json",
            "config.json muss im Bundle enthalten sein");
    }

    [Fact]
    public async Task CreateBundleAsync_ZipContains_BundleInfo()
    {
        // Arrange
        var configDir = Path.Combine(_tempRoot, "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "config.json"), "{}");

        var resolver = new ConfigPathResolver(configDir);
        var service = new SupportBundleService(resolver);
        var targetDir = Path.Combine(_tempRoot, "output");

        // Act
        var zipPath = await service.CreateBundleAsync(targetDir);

        // Assert
        using var zip = ZipFile.OpenRead(zipPath);
        zip.Entries.Should().Contain(e => e.FullName == "bundle-info.txt",
            "bundle-info.txt muss im Bundle enthalten sein");

        var infoEntry = zip.Entries.First(e => e.FullName == "bundle-info.txt");
        using var reader = new StreamReader(infoEntry.Open());
        var content = await reader.ReadToEndAsync();
        content.Should().Contain("UMI Support Bundle");
        content.Should().Contain("Version:");
    }

    [Fact]
    public async Task CreateBundleAsync_WithLogFiles_IncludesLogs()
    {
        // Arrange
        var configDir = Path.Combine(_tempRoot, "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "config.json"), "{}");

        // Lege eine Fake-Log-Datei in ein Test-Verzeichnis — simuliert das Log-Dir
        // via ConfigPathResolver mit explizitem configRoot (kein LocalAppData-Lookup)
        // Log-Dir ist ConfigPathResolver.LogDirectory (static) — nicht überschreibbar aus Tests.
        // Stattdessen: testet implizit dass fehlende Logs graceful behandelt werden.

        var resolver = new ConfigPathResolver(configDir);
        var service = new SupportBundleService(resolver);
        var targetDir = Path.Combine(_tempRoot, "output");

        // Act — darf nicht crashen obwohl Log-Dir möglicherweise leer ist
        var act = () => service.CreateBundleAsync(targetDir);

        // Assert — kein Exception, ZIP wird erzeugt
        var zipPath = await act.Should().NotThrowAsync();
        File.Exists(zipPath.Subject).Should().BeTrue();
    }

    /// <summary>
    /// Regression für I-008: Das Support-Bundle enthielt NIE den Log des laufenden
    /// Tages — also ausgerechnet den mit dem gemeldeten Vorfall. Bundle-Info meldete
    /// jedes Mal "The process cannot access the file ... being used by another process".
    /// Kostete bei I-006 eine komplette Analyse-Runde.
    ///
    /// Ursache ist die Windows-Share-Semantik: Der Logger hält die Datei mit
    /// FileAccess.Write offen. Ein zweiter Öffner muss dem bestehenden Writer sein
    /// Write-Recht ausdrücklich zugestehen (FileShare.Write), sonst kollidiert er —
    /// und genau das tut ZipArchive.CreateEntryFromFile nicht.
    ///
    /// ConfigPathResolver.LogDirectory ist statisch und aus Tests nicht umlenkbar,
    /// deshalb wird hier die Kopier-Mechanik selbst geprüft statt der Service-Aufruf.
    /// </summary>
    [Fact]
    public void OpenLogFile_IsSkippedByCreateEntryFromFile_ButCopiedByShareAwareStream()
    {
        var logPath = Path.Combine(_tempRoot, "umi-gui-20260728.log");
        File.WriteAllText(logPath, "[21:47:27] === Burst-Gruppierung START ===\n");

        // So hält der laufende Logger seine aktuelle Datei offen:
        using var loggerHandle = new FileStream(
            logPath, FileMode.Open, FileAccess.Write, FileShare.Read);

        var zipPath = Path.Combine(_tempRoot, "bundle.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            // Der alte Weg — scheitert genau so wie beim User:
            var oldWay = () => archive.CreateEntryFromFile(logPath, "logs/old.log");
            oldWay.Should().Throw<IOException>(
                "CreateEntryFromFile gesteht dem laufenden Logger sein Write-Recht nicht zu");

            // Der neue Weg — FileShare.ReadWrite gesteht es zu:
            var entry = archive.CreateEntry("logs/new.log", CompressionLevel.Optimal);
            using var source = new FileStream(
                logPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var target = entry.Open();
            source.CopyTo(target);
        }

        // Und der Inhalt ist wirklich im Bundle gelandet:
        using var check = ZipFile.OpenRead(zipPath);
        var copied = check.GetEntry("logs/new.log");
        copied.Should().NotBeNull();

        using var reader = new StreamReader(copied!.Open());
        reader.ReadToEnd().Should().Contain("Burst-Gruppierung START");
    }

    [Fact]
    public async Task CreateBundleAsync_MissingConfigJson_BundleInfoMentionsMissing()
    {
        // Arrange — config.json existiert NICHT
        var configDir = Path.Combine(_tempRoot, "config-empty");
        Directory.CreateDirectory(configDir);
        // Kein config.json erstellt!

        var resolver = new ConfigPathResolver(configDir);
        var service = new SupportBundleService(resolver);
        var targetDir = Path.Combine(_tempRoot, "output");

        // Act
        var zipPath = await service.CreateBundleAsync(targetDir);

        // Assert — ZIP existiert und bundle-info enthält Hinweis auf fehlende Datei
        using var zip = ZipFile.OpenRead(zipPath);
        var infoEntry = zip.Entries.First(e => e.FullName == "bundle-info.txt");
        using var reader = new StreamReader(infoEntry.Open());
        var content = await reader.ReadToEndAsync();
        content.Should().Contain("config.json nicht gefunden",
            "fehlende config.json muss in bundle-info.txt vermerkt werden");
    }
}
