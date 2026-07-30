// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using FluentAssertions;
using Moq;
using UMI.Core.Services;
using UMI.Core.Utilities;
using UMI.Core.Wizards.Steps;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Regression tests für I-005:
/// Ein Workbench-Pfad INNERHALB von <c>.umi</c> legt sämtliche ordnerbasierten
/// Foto-Funktionen still — jeder Scan filtert seine eigenen Dateien per
/// <see cref="FolderNameConstants.IsInternalPath"/> wieder weg.
///
/// Real aufgetreten: Workbench stand auf
/// <c>E:\umi\.umi\2026-07-07\Canon-EOS-R5m2</c>, der Sequenz-Reviewer meldete
/// "Keine Bilder gefunden" obwohl 108 Bilder im Ordner lagen.
///
/// Der Knackpunkt ist der Root-Fall: <see cref="FolderNameConstants.IsInternalPath"/>
/// sucht nach dem Segment <c>/.umi/</c> — ein VERZEICHNIS-Pfad endet aber auf
/// dem Segment (<c>E:\umi\.umi</c>) und hat keinen Trailing-Separator. Genau
/// dieser Pfad wurde vom User real gesetzt und wäre durch einen naiven Guard
/// geschlüpft. Dafür existiert <see cref="FolderNameConstants.IsInternalDirectory"/>.
/// </summary>
public class InternalPathGuardTests
{
    [Theory]
    // Der real gesetzte Pfad des Users — tief in .umi:
    [InlineData(@"E:\umi\.umi\2026-07-07\Canon-EOS-R5m2")]
    // Der Root-Fall OHNE Trailing-Separator — schlüpft durch IsInternalPath:
    [InlineData(@"E:\umi\.umi")]
    [InlineData(@"E:\umi\.umi\")]
    // Forward-Slashes (ExifTool-Konvention, CLAUDE.md Falle #1):
    [InlineData("E:/umi/.umi")]
    [InlineData("E:/umi/.umi/2026-07-07")]
    // Das zweite interne Verzeichnis:
    [InlineData(@"E:\umi\.metadata")]
    [InlineData(@"E:\umi\.metadata\sub")]
    public void IsInternalDirectory_DetectsInternalFolders(string path)
    {
        FolderNameConstants.IsInternalDirectory(path).Should().BeTrue(
            "ein Workbench unter {0} würde jede eigene Datei wegfiltern", path);
    }

    [Theory]
    // Der korrekte Workbench des Users:
    [InlineData(@"E:\umi")]
    [InlineData(@"E:\umi\2026-07-07\Canon-EOS-R5m2")]
    [InlineData(@"E:\2026\2026-07-07")]
    [InlineData("E:/umi/2026-07-07")]
    // Ähnlich aussehende, aber legitime Namen dürfen NICHT anschlagen:
    [InlineData(@"E:\umi-backup")]
    [InlineData(@"E:\umi\.umi-review")]
    [InlineData(@"E:\meine.umifotos")]
    public void IsInternalDirectory_AllowsNormalMediaFolders(string path)
    {
        FolderNameConstants.IsInternalDirectory(path).Should().BeFalse(
            "{0} ist ein regulärer Medien-Ordner", path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsInternalDirectory_HandlesEmptyInput(string? path)
    {
        // Leerer Pfad ist nicht "intern" — die Leer-Prüfung ist Sache des Aufrufers.
        FolderNameConstants.IsInternalDirectory(path).Should().BeFalse();
    }

    /// <summary>
    /// Belegt WARUM IsInternalDirectory existiert: der Root-Fall ist genau die
    /// Lücke, die IsInternalPath (Datei-Semantik) offen lässt.
    /// </summary>
    [Fact]
    public void IsInternalPath_MissesDirectoryRootCase_HenceTheDirectoryVariant()
    {
        const string workbenchRoot = @"E:\umi\.umi";

        FolderNameConstants.IsInternalPath(workbenchRoot).Should().BeFalse(
            "IsInternalPath erwartet Datei-Pfade mit umschlossenem /.umi/-Segment");

        FolderNameConstants.IsInternalDirectory(workbenchRoot).Should().BeTrue(
            "die Verzeichnis-Variante muss genau diesen Fall fangen");
    }

    /// <summary>
    /// Die eigentliche Schadwirkung: liegt der Scan-Root selbst in .umi, wird
    /// jede darin gefundene Datei als "intern" verworfen — der Ordner wirkt leer.
    /// </summary>
    [Fact]
    public void FilesBelowInternalWorkbench_AreAllFilteredOut_ThatsTheBug()
    {
        var photosInBrokenWorkbench = new[]
        {
            @"E:\umi\.umi\2026-07-07\Canon-EOS-R5m2\_JOV2553.CR3",
            @"E:\umi\.umi\2026-07-07\Canon-EOS-R5m2\_JOV2554.CR3",
        };

        photosInBrokenWorkbench.Should().OnlyContain(
            f => FolderNameConstants.IsInternalPath(f),
            "genau deshalb meldete der Reviewer 0 Fotos");

        // Gegenprobe: derselbe Baum ohne das .umi-Segment bleibt sichtbar.
        FolderNameConstants.IsInternalPath(
            @"E:\umi\2026-07-07\Canon-EOS-R5m2\_JOV2553.CR3").Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Guard am Einstiegspunkt (Audit F-06). Der urspruengliche Fix sicherte
    // nur den Settings-Reiter — die Setup-Wizards liessen denselben kaputten
    // Pfad weiterhin durch (Audit F-01). Dieser Test nagelt den CLI-Wizard-
    // Guard fest, damit die Tuer nicht unbemerkt wieder aufgeht.
    // ------------------------------------------------------------------

    private static WorkbenchPathStep CreateStep() =>
        new(Mock.Of<IConfigWriterService>());

    [Theory]
    [InlineData(@"E:\umi\.umi")]
    [InlineData(@"E:\umi\.umi\2026-07-07\Canon-EOS-R5m2")]
    public async Task CliWizard_RejectsWorkbenchInsideInternalDirectory(string path)
    {
        var values = new Dictionary<string, object?> { ["workbench"] = path };

        var result = await CreateStep().ValidateAsync(values);

        result.IsValid.Should().BeFalse("ein Workbench in .umi macht alle Foto-Scans blind");
        result.ErrorMessage.Should().Contain(".umi");
    }

    [Fact]
    public async Task CliWizard_AcceptsNormalWorkbenchFolder()
    {
        // Existierender, regulaerer Ordner — der Guard darf hier nicht anschlagen.
        var values = new Dictionary<string, object?> { ["workbench"] = Path.GetTempPath() };

        var result = await CreateStep().ValidateAsync(values);

        result.IsValid.Should().BeTrue();
    }
}
