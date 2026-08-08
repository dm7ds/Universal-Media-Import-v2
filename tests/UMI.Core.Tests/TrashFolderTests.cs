// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using FluentAssertions;
using UMI.Core.Utilities;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Tests zu I-010 (Aussortieren im Sequenz-Reviewer).
///
/// Der Reviewer selbst lebt in UMI.GUI (WPF, aus diesem Projekt nicht
/// referenzierbar). Geprüft wird deshalb das, was in UMI.Core liegt und
/// worauf der Fix aufsetzt: der Müll-Ordner muss vom Scan ausgeschlossen sein,
/// und die Pfad-Abbildung muss die Ordnerstruktur erhalten.
/// </summary>
public class TrashFolderTests
{
    // ---------------------------------------------------------------
    // Scan-Ausschluss: ohne ihn sammelt der nächste Review die gerade
    // aussortierten Fotos wieder ein und sie stehen erneut im Reviewer.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(@"E:\umi\2026-08-01\Canon-EOS-R5m2\_Trash\Photo\Sport_1\IMG_001.CR3")]
    [InlineData(@"E:\umi\_Trash\IMG_002.CR3")]
    [InlineData("E:/umi/2026-08-01/_Trash/Photo/IMG_003.CR3")]   // Forward-Slashes (ExifTool)
    public void TrashedPhotos_AreExcludedFromScans(string path)
    {
        FolderNameConstants.IsInternalPath(path).Should().BeTrue(
            "aussortierte Fotos dürfen beim nächsten Scan nicht wieder auftauchen");
    }

    [Theory]
    [InlineData(@"E:\umi\2026-08-01\Canon-EOS-R5m2\Photo\Sport_1\IMG_001.CR3")]
    [InlineData(@"E:\umi\_Trashcan\IMG_002.CR3")]      // anderer Ordner, nur ähnlich
    [InlineData(@"E:\umi\Photo\My_Trash_Shots\x.CR3")] // Segment nur als Namensteil
    public void RegularPhotos_StayVisible(string path)
    {
        FolderNameConstants.IsInternalPath(path).Should().BeFalse(
            "{0} ist ein reguläres Foto und darf nicht weggefiltert werden", path);
    }

    // ---------------------------------------------------------------
    // Struktur-Erhalt beim Verschieben (Dirk-Vorgabe: "Dabei soll die
    // Struktur aber beibehalten werden"). Gleiche Pfad-Rechnung wie
    // SequenceReviewerViewModel.ApplyDeleteMode.
    // ---------------------------------------------------------------

    private static string MapIntoTrash(string folderPath, string fullPath)
    {
        var trashRoot = Path.Combine(folderPath, FolderNameConstants.TrashDir);
        var relative  = Path.GetRelativePath(folderPath, fullPath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            relative = Path.GetFileName(fullPath);

        return Path.Combine(trashRoot, relative);
    }

    [Fact]
    public void MoveToTrash_PreservesFolderStructure()
    {
        const string folder = @"E:\umi\2026-08-01\Canon-EOS-R5m2";
        var target = MapIntoTrash(folder, folder + @"\Photo\Sport_123\IMG_001.CR3");

        target.Should().Be(folder + @"\_Trash\Photo\Sport_123\IMG_001.CR3",
            "die Unterordner müssen unter _Trash gespiegelt werden, damit "
            + "nachvollziehbar bleibt woher eine Datei stammt");
    }

    [Fact]
    public void MoveToTrash_FileDirectlyInFolder_LandsAtTrashRoot()
    {
        const string folder = @"E:\umi\2026-08-01";
        MapIntoTrash(folder, folder + @"\IMG_009.CR3")
            .Should().Be(folder + @"\_Trash\IMG_009.CR3");
    }

    /// <summary>
    /// Sicherheitsnetz: Läge eine Datei außerhalb des gereviewten Ordners,
    /// ergäbe der relative Pfad ".." und würde aus dem Müll-Ordner
    /// herausschreiben — im schlimmsten Fall über fremde Dateien.
    /// </summary>
    [Fact]
    public void MoveToTrash_FileOutsideReviewedFolder_StaysInsideTrash()
    {
        const string folder = @"E:\umi\2026-08-01\Canon-EOS-R5m2";
        var target = MapIntoTrash(folder, @"E:\woanders\IMG_042.CR3");

        target.Should().Be(folder + @"\_Trash\IMG_042.CR3");
        target.Should().NotContain("..", "der Zielpfad darf nie aus dem Müll-Ordner herausführen");
    }
}
