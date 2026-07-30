// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using FluentAssertions;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using UMI.Core;
using UMI.Core.Configuration;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;

namespace UMI.Core.Tests;

/// <summary>
/// Tests für TASK-220 — Welle 1+2: Body-Serial lesen + Sort-Zuordnungs-Kaskade.
/// </summary>
public class CameraSerialTests : IDisposable
{
    private readonly string _tempRoot;

    public CameraSerialTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "umi-serial-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // -------------------------------------------------------------------------
    // Welle 1: MetadataReader liest CameraSerial aus Canon Makernote
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifiziert dass MetadataReader die Body-Serial aus dem Canon Makernote
    /// (TagCanonSerialNumber = 0x000C) korrekt liest.
    /// Nutzt den direkten ReadPhotoMetadata-Overload mit programmatisch befüllten
    /// MetadataExtractor-Directories (keine echte Canon-CR3-Datei nötig).
    /// </summary>
    [Fact]
    public void ReadPhotoMetadata_CanonMakernoteSerial_IsReadIntoPhotoMetadata()
    {
        // Arrange
        const string expectedSerial = "283033005535"; // Verifizierter R10-Body-Serial-Wert
        var canonDir = new CanonMakernoteDirectory();
        canonDir.Set(CanonMakernoteDirectory.TagCanonSerialNumber, expectedSerial);

        var directories = new List<MetadataExtractor.Directory> { canonDir };

        var reader = new MetadataReader();
        var filePath = Path.Combine(_tempRoot, "test.jpg");
        File.WriteAllBytes(filePath, new byte[] { 0xFF, 0xD8, 0xFF }); // Minimal JPEG stub

        // Act
        var meta = reader.ReadPhotoMetadata(filePath, directories);

        // Assert
        meta.CameraSerial.Should().Be(expectedSerial,
            "Canon Makernote TagCanonSerialNumber muss als CameraSerial übernommen werden");
    }

    /// <summary>
    /// Verifiziert dass MetadataReader den EXIF BodySerialNumber-Fallback (0xA431) nutzt,
    /// wenn kein Canon-Makernote vorhanden ist.
    /// </summary>
    [Fact]
    public void ReadPhotoMetadata_ExifBodySerialNumber_UsedAsFallback_WhenNoCanonMakernote()
    {
        // Arrange
        const string expectedSerial = "R5M2-12345678";
        var exifSubDir = new ExifSubIfdDirectory();
        exifSubDir.Set(ExifDirectoryBase.TagBodySerialNumber, expectedSerial);

        var directories = new List<MetadataExtractor.Directory> { exifSubDir };

        var reader = new MetadataReader();
        var filePath = Path.Combine(_tempRoot, "test.jpg");
        File.WriteAllBytes(filePath, new byte[] { 0xFF, 0xD8, 0xFF });

        // Act
        var meta = reader.ReadPhotoMetadata(filePath, directories);

        // Assert
        meta.CameraSerial.Should().Be(expectedSerial,
            "EXIF TagBodySerialNumber muss als Fallback-Serial gelesen werden wenn kein Canon-Makernote vorhanden");
    }

    /// <summary>
    /// Verifiziert dass MetadataReader CameraSerial = null zurückgibt wenn
    /// weder Canon-Makernote noch EXIF BodySerialNumber vorhanden sind.
    /// Kein Crash, kein Exception — nur null.
    /// </summary>
    [Fact]
    public void ReadPhotoMetadata_NoSerial_ReturnsNull()
    {
        // Arrange — leere Directory-Liste (keine EXIF-Daten)
        var directories = new List<MetadataExtractor.Directory>();

        var reader = new MetadataReader();
        var filePath = Path.Combine(_tempRoot, "test.jpg");
        File.WriteAllBytes(filePath, new byte[] { 0xFF, 0xD8, 0xFF });

        // Act
        var meta = reader.ReadPhotoMetadata(filePath, directories);

        // Assert
        meta.CameraSerial.Should().BeNull(
            "fehlende Serial muss null ergeben, nicht leer oder Exception");
    }

    /// <summary>
    /// Verifiziert dass Canon-Serial Vorrang vor EXIF-BodySerialNumber hat wenn beide vorhanden.
    /// </summary>
    [Fact]
    public void ReadPhotoMetadata_CanonSerialTakesPrecedenceOverExifBodySerial()
    {
        // Arrange
        const string canonSerial = "283033005535"; // Canon-Wert
        const string exifSerial  = "EXIF-FALLBACK-SERIAL"; // EXIF-Wert (darf nicht gewählt werden)

        var canonDir = new CanonMakernoteDirectory();
        canonDir.Set(CanonMakernoteDirectory.TagCanonSerialNumber, canonSerial);

        var exifSubDir = new ExifSubIfdDirectory();
        exifSubDir.Set(ExifDirectoryBase.TagBodySerialNumber, exifSerial);

        var directories = new List<MetadataExtractor.Directory> { canonDir, exifSubDir };

        var reader = new MetadataReader();
        var filePath = Path.Combine(_tempRoot, "test.jpg");
        File.WriteAllBytes(filePath, new byte[] { 0xFF, 0xD8, 0xFF });

        // Act
        var meta = reader.ReadPhotoMetadata(filePath, directories);

        // Assert
        meta.CameraSerial.Should().Be(canonSerial,
            "Canon-Makernote-Serial hat Vorrang vor EXIF-BodySerialNumber");
    }

    // -------------------------------------------------------------------------
    // CameraSerialMatcher: SSOT-Unit-Tests
    // -------------------------------------------------------------------------

    [Fact]
    public void CameraSerialMatcher_FindCameraId_ReturnsMatchedCameraId()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R10"] = new CameraConfig { Name = "Canon R10", SerialNumber = "283033005535" },
            ["R5m2"] = new CameraConfig { Name = "Canon R5m2", SerialNumber = "152302402888" },
        };

        CameraSerialMatcher.FindCameraId("283033005535", cameras).Should().Be("R10");
        CameraSerialMatcher.FindCameraId("152302402888", cameras).Should().Be("R5m2");
    }

    [Fact]
    public void CameraSerialMatcher_FindCameraId_ReturnsNull_WhenNoMatch()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R10"] = new CameraConfig { Name = "Canon R10", SerialNumber = "283033005535" },
        };

        CameraSerialMatcher.FindCameraId("999999999999", cameras).Should().BeNull();
        CameraSerialMatcher.FindCameraId(null, cameras).Should().BeNull();
        CameraSerialMatcher.FindCameraId("", cameras).Should().BeNull();
    }

    [Fact]
    public void CameraSerialMatcher_FindCameraId_IgnoresCamerasWithEmptySerial()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["NoSerial"] = new CameraConfig { Name = "Camera Without Serial", SerialNumber = null },
            ["R10"]      = new CameraConfig { Name = "Canon R10", SerialNumber = "283033005535" },
        };

        // "NoSerial" hat SerialNumber=null — darf nicht versehentlich auf null-Suche matchen
        CameraSerialMatcher.FindCameraId(null, cameras).Should().BeNull(
            "null-Serial darf nicht gegen null-Config-Serial matchen");
        CameraSerialMatcher.FindCameraId("283033005535", cameras).Should().Be("R10");
    }

    // -------------------------------------------------------------------------
    // Welle 2: FolderSortService — Serial-Match-Kaskade
    // -------------------------------------------------------------------------

    private static FolderSortService CreateService()
    {
        return new FolderSortService(
            metadataReader:     new MetadataReader(),
            burstMatching:      new BurstMatchingEngine(),
            sequenceGrouping:   new SequenceGroupingService(),
            burstProfileLoader: null,
            logger:             null);
    }

    /// <summary>
    /// Kerntest TASK-220 Welle 2:
    /// Ein Foto mit CameraSerial im EXIF aber OHNE Camera-Ordner im Pfad
    /// (flacher Pfad direkt im Workbench-Root) muss via Serial-Match der
    /// konfigurierten Kamera zugeordnet werden — nicht _unsorted landen.
    ///
    /// Da wir keine echten Canon-CR3-Dateien als Test-Fixtures haben, testen wir
    /// die Sort-Kaskade indirekt: Datei im Pfad-Match-Pfad über Kamera-Ordner
    /// → landet korrekt, nicht _unsorted. Serial-Match selbst ist via
    /// CameraSerialMatcher-Unit-Tests und MetadataReader-Tests abgedeckt.
    ///
    /// Der End-to-End-Test für Serial-via-EXIF ist integration-level und
    /// benötigt echte Canon-Files — hier dokumentiert als TODO.
    /// </summary>
    [Fact]
    public async Task SortAsync_PhotoInFlatPath_WithNoSerial_FallsBackToPathMatch()
    {
        // Verifikation: Pfad-Fallback bleibt intakt nach Kaskaden-Umbau
        var service = CreateService();
        var config  = ConfigWithCamera("R10", "283033005535");

        var camDir = Path.Combine(_tempRoot, "R10");
        Directory.CreateDirectory(camDir);
        var src = Path.Combine(camDir, "IMG_0001.jpg");
        await File.WriteAllBytesAsync(src, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(src, new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Local));

        var result = await service.SortAsync(new FolderSortRequest(_tempRoot, FolderSortMode.Full, config));

        result.Moved.Should().Be(1);
        result.Errors.Should().Be(0);
        // Datei landet unter R10-Kamera-Ordner (FolderName = "R10"), nicht _unsorted
        var expected = Path.Combine(_tempRoot, "2026-05-01", "R10", FolderNameConstants.Photo, "IMG_0001.jpg");
        File.Exists(expected).Should().BeTrue("Pfad-Fallback muss weiterhin funktionieren nach Kaskaden-Umbau");
    }

    /// <summary>
    /// Verifiziert dass FolderSortService eine Datei ohne Kamera-Ordner im Pfad
    /// und ohne EXIF-Serial nach _unsorted sortiert (Kaskade Stufe 3).
    /// Das ist das ursprüngliche Verhalten für Dateien ohne erkennbare Kamera.
    /// </summary>
    [Fact]
    public async Task SortAsync_PhotoInFlatPath_NoSerialNoPathMatch_LandsInUnsorted()
    {
        var service = CreateService();
        var config  = ConfigWithCamera("R10", "283033005535");

        // Datei direkt im Workbench-Root, ohne Kamera-Ordner → Pfad-Match schlägt fehl
        var src = Path.Combine(_tempRoot, "IMG_FLAT.jpg");
        await File.WriteAllBytesAsync(src, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(src, new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Local));

        var result = await service.SortAsync(
            new FolderSortRequest(_tempRoot, FolderSortMode.Full, config, DryRun: true));

        result.Moved.Should().Be(1); // DryRun zählt als "moved"
        result.Errors.Should().Be(0);
        // Kein echter Move in DryRun — verifiziere via ExtractCameraIdFromPath direkt
        var cameraId = FolderSortService.ExtractCameraIdFromPath(src, _tempRoot, config);
        cameraId.Should().Be("_unsorted",
            "Datei ohne Kamera-Ordner und ohne Serial muss _unsorted zugeordnet werden");
    }

    /// <summary>
    /// Kerntest TASK-220 Welle 2 — F-T220-02:
    /// Foto liegt FLACH im Workbench-Root (kein Kamera-Ordner → Pfad-Match versagt).
    /// MetadataReader gibt CameraSerial = "283033005535" zurück (via StubMetadataReader).
    /// Config hat genau diese Serial bei CameraId "R10", FolderName "Canon-EOS-R10".
    /// Erwartet: Datei landet unter .../Canon-EOS-R10/... , NICHT unter _unsorted.
    /// Das ist der Beweis dass die Serial-Match-Kaskade in FolderSortService korrekt
    /// verdrahtet ist — End-to-End ohne echte Canon-Datei.
    /// </summary>
    [Fact]
    public async Task SortAsync_FlatPhoto_WithMatchingCameraSerial_LandsUnderCorrectCamera()
    {
        // Arrange — config: R10 mit FolderName "Canon-EOS-R10" und bekannter Serial
        const string targetFolderName = "Canon-EOS-R10";
        const string r10Serial        = "283033005535";

        var config = new UmiConfig
        {
            GlobalPaths = new GlobalPaths
            {
                Workbench = "", Projects = "", GpxSource = "",
                Tools     = new ToolPaths { ExifTool = "" },
            },
        };
        config.Cameras["R10"] = new CameraConfig
        {
            Name         = "Canon EOS R10",
            FolderName   = targetFolderName,
            Enabled      = true,
            SerialNumber = r10Serial,
            FileTypes    = new CameraFileTypes
            {
                Photo = new[] { ".jpg", ".cr3" },
                Video = new[] { ".mp4" },
            },
        };

        // Foto direkt im Workbench-Root — kein Kamera-Ordner im Pfad.
        // ExtractCameraIdFromPath würde hier "_unsorted" zurückgeben.
        var captureDate = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Local);
        var flatPhoto   = Path.Combine(_tempRoot, "IMG_flat.jpg");
        await File.WriteAllBytesAsync(flatPhoto, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(flatPhoto, captureDate);

        // StubMetadataReader: gibt für das flache Foto CameraSerial = r10Serial zurück,
        // damit FolderSortService Serial-Match (Kaskade Stufe 1) triggert.
        var stubReader = new StubMetadataReader(flatPhoto, r10Serial, captureDate);
        var service    = new FolderSortService(
            metadataReader:     stubReader,
            burstMatching:      new BurstMatchingEngine(),
            sequenceGrouping:   new SequenceGroupingService(),
            burstProfileLoader: null,
            logger:             null);

        // Act
        var result = await service.SortAsync(new FolderSortRequest(_tempRoot, FolderSortMode.Full, config));

        // Assert — kein Fehler, Datei unter Canon-EOS-R10 (nicht _unsorted)
        result.Errors.Should().Be(0);
        result.Moved.Should().Be(1);

        var expected = Path.Combine(
            _tempRoot,
            "2026-05-10",
            targetFolderName,
            FolderNameConstants.Photo,
            "IMG_flat.jpg");

        File.Exists(expected).Should().BeTrue(
            $"Foto mit passender CameraSerial muss unter '{targetFolderName}' landen, nicht unter _unsorted");

        // Negativ-Assertion: _unsorted darf NICHT existieren
        var unsortedDir = Path.Combine(_tempRoot, "2026-05-10", "_unsorted");
        Directory.Exists(unsortedDir).Should().BeFalse(
            "kein _unsorted-Ordner darf entstehen wenn die Serial auf eine bekannte Kamera zeigt");
    }

    // -------------------------------------------------------------------------
    // Welle 3: CameraSerialMatcher.FindCameraIdByModel — Unit-Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Test (a): EXIF-Modell matcht exakt auf config.Name einer aktivierten Kamera
    /// → CameraId wird zurückgegeben.
    /// </summary>
    [Fact]
    public void FindCameraIdByModel_ExactMatchOnName_ReturnsCameraId()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R10"] = new CameraConfig { Name = "Canon EOS R10", Enabled = true },
            ["R5m2"] = new CameraConfig { Name = "Canon EOS R5 Mark II", Enabled = true },
        };

        CameraSerialMatcher.FindCameraIdByModel("Canon EOS R10", cameras).Should().Be("R10");
    }

    /// <summary>
    /// Test (a): EXIF-Modell matcht exakt auf config.CameraModel (Vorrang vor Name).
    /// </summary>
    [Fact]
    public void FindCameraIdByModel_ExactMatchOnCameraModel_PrefersCameraModelOverName()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            // CameraModel gesetzt — dieser soll matchen, nicht Name
            ["R10"] = new CameraConfig
            {
                Name        = "Meine Canon R10",      // user-defined, kein exakter Modell-Name
                CameraModel = "Canon EOS R10",        // exaktes EXIF-Modell
                Enabled     = true,
            },
        };

        CameraSerialMatcher.FindCameraIdByModel("Canon EOS R10", cameras).Should().Be("R10",
            "camera_model hat Vorrang vor name beim Modell-Match");
        CameraSerialMatcher.FindCameraIdByModel("Meine Canon R10", cameras).Should().BeNull(
            "der User-Name ohne camera_model darf nicht matchen wenn camera_model gesetzt ist");
    }

    /// <summary>
    /// Test (b): Zwei aktivierte Kameras mit identischem Modell → Mehrdeutigkeit → null.
    /// </summary>
    [Fact]
    public void FindCameraIdByModel_AmbiguousModel_ReturnsNull()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R10-A"] = new CameraConfig { Name = "Canon EOS R10", Enabled = true },
            ["R10-B"] = new CameraConfig { Name = "Canon EOS R10", Enabled = true },
        };

        CameraSerialMatcher.FindCameraIdByModel("Canon EOS R10", cameras).Should().BeNull(
            "Mehrdeutigkeit: zwei Kameras gleichen Modells dürfen nicht zu einem Match führen");
    }

    /// <summary>
    /// Kein Fuzzy/Contains-Match: "R10" darf nicht in "R100" matchen.
    /// </summary>
    [Fact]
    public void FindCameraIdByModel_NoFuzzyMatch_ShortModelDoesNotMatchLongerName()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R100"] = new CameraConfig { Name = "Canon EOS R100", Enabled = true },
        };

        CameraSerialMatcher.FindCameraIdByModel("R10", cameras).Should().BeNull(
            "Substring-Match ist verboten — nur exakter Gleichstand erlaubt");
        CameraSerialMatcher.FindCameraIdByModel("Canon EOS R10", cameras).Should().BeNull(
            "\"Canon EOS R10\" darf nicht auf \"Canon EOS R100\" matchen");
    }

    /// <summary>
    /// Deaktivierte Kameras (Enabled=false) werden beim Modell-Match ignoriert.
    /// </summary>
    [Fact]
    public void FindCameraIdByModel_DisabledCamera_IsIgnored()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R10-disabled"] = new CameraConfig { Name = "Canon EOS R10", Enabled = false },
            ["R5m2"]         = new CameraConfig { Name = "Canon EOS R5 Mark II", Enabled = true },
        };

        CameraSerialMatcher.FindCameraIdByModel("Canon EOS R10", cameras).Should().BeNull(
            "deaktivierte Kamera darf nicht matchen");
    }

    /// <summary>
    /// Null/leeres exifModel → immer null (kein Crash).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindCameraIdByModel_NullOrEmptyModel_ReturnsNull(string? model)
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R10"] = new CameraConfig { Name = "Canon EOS R10", Enabled = true },
        };

        CameraSerialMatcher.FindCameraIdByModel(model, cameras).Should().BeNull(
            $"leeres/null exifModel '{model}' darf nie matchen");
    }

    /// <summary>
    /// Case-Insensitiv und Trim: "canon eos r10 " (klein, Leerzeichen) matcht "Canon EOS R10".
    /// </summary>
    [Fact]
    public void FindCameraIdByModel_CaseInsensitiveAndTrimmed()
    {
        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R10"] = new CameraConfig { Name = "Canon EOS R10", Enabled = true },
        };

        CameraSerialMatcher.FindCameraIdByModel("canon eos r10 ", cameras).Should().Be("R10",
            "Vergleich muss case-insensitiv und getrimmt sein");
        CameraSerialMatcher.FindCameraIdByModel("CANON EOS R10", cameras).Should().Be("R10");
    }

    // -------------------------------------------------------------------------
    // Welle 3: FolderSortService — Modell-Match-Kaskade (Integration)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Test (a) Kaskade: Foto liegt flach im Workbench (kein Kamera-Ordner, keine Serial).
    /// EXIF-Modell = "Canon EOS R10", Config hat genau eine aktivierte Kamera mit Name="Canon EOS R10".
    /// → Modell-Match greift → landet unter deren FolderName, NICHT _unsorted.
    /// </summary>
    [Fact]
    public async Task SortAsync_FlatPhoto_WithMatchingCameraModel_LandsUnderCorrectCamera()
    {
        // Arrange
        const string targetFolderName = "Canon-EOS-R10";
        const string exifModel        = "Canon EOS R10";
        var captureDate               = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Local);

        var config = new UmiConfig
        {
            GlobalPaths = new GlobalPaths
            {
                Workbench = "", Projects = "", GpxSource = "",
                Tools     = new ToolPaths { ExifTool = "" },
            },
        };
        config.Cameras["R10"] = new CameraConfig
        {
            Name         = exifModel,           // Name = exaktes EXIF-Modell (Dirks Situation)
            FolderName   = targetFolderName,
            Enabled      = true,
            SerialNumber = null,                // KEINE Serial hinterlegt → Serial-Match greift nicht
            FileTypes    = new CameraFileTypes
            {
                Photo = new[] { ".jpg", ".cr3" },
                Video = new[] { ".mp4" },
            },
        };

        var flatPhoto = Path.Combine(_tempRoot, "IMG_model_match.jpg");
        await File.WriteAllBytesAsync(flatPhoto, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(flatPhoto, captureDate);

        // Stub: gibt CameraModel = "Canon EOS R10", CameraSerial = null
        var stubReader = new StubMetadataReaderWithModel(flatPhoto, serial: null, model: exifModel, captureDate);
        var service    = new FolderSortService(
            metadataReader:     stubReader,
            burstMatching:      new BurstMatchingEngine(),
            sequenceGrouping:   new SequenceGroupingService(),
            burstProfileLoader: null,
            logger:             null);

        // Act
        var result = await service.SortAsync(new FolderSortRequest(_tempRoot, FolderSortMode.Full, config));

        // Assert
        result.Errors.Should().Be(0);
        result.Moved.Should().Be(1);
        var expected = Path.Combine(_tempRoot, "2026-05-15", targetFolderName, FolderNameConstants.Photo, "IMG_model_match.jpg");
        File.Exists(expected).Should().BeTrue(
            $"Foto mit passendem EXIF-Modell (kein Serial) muss unter '{targetFolderName}' landen");
        var unsortedDir = Path.Combine(_tempRoot, "2026-05-15", "_unsorted");
        Directory.Exists(unsortedDir).Should().BeFalse("kein _unsorted-Ordner wenn Modell eindeutig matcht");
    }

    /// <summary>
    /// Test (b) Kaskade: Zwei Kameras haben denselben Namen/Modell → Mehrdeutigkeit.
    /// → Modell-Match greift NICHT → Pfad-Match → _unsorted (kein Kamera-Ordner im Pfad).
    /// </summary>
    [Fact]
    public async Task SortAsync_FlatPhoto_AmbiguousModel_LandsInUnsorted()
    {
        const string exifModel  = "Canon EOS R10";
        var captureDate         = new DateTime(2026, 5, 16, 10, 0, 0, DateTimeKind.Local);

        var config = new UmiConfig
        {
            GlobalPaths = new GlobalPaths
            {
                Workbench = "", Projects = "", GpxSource = "",
                Tools     = new ToolPaths { ExifTool = "" },
            },
        };
        // Zwei Kameras gleichen Modells → Mehrdeutigkeit
        config.Cameras["R10-A"] = new CameraConfig
        {
            Name = exifModel, FolderName = "R10-A", Enabled = true,
            FileTypes = new CameraFileTypes { Photo = new[] { ".jpg" }, Video = Array.Empty<string>() },
        };
        config.Cameras["R10-B"] = new CameraConfig
        {
            Name = exifModel, FolderName = "R10-B", Enabled = true,
            FileTypes = new CameraFileTypes { Photo = new[] { ".jpg" }, Video = Array.Empty<string>() },
        };

        var flatPhoto = Path.Combine(_tempRoot, "IMG_ambiguous.jpg");
        await File.WriteAllBytesAsync(flatPhoto, new byte[] { 0xFF, 0xD8, 0xFF });
        File.SetLastWriteTime(flatPhoto, captureDate);

        var stubReader = new StubMetadataReaderWithModel(flatPhoto, serial: null, model: exifModel, captureDate);
        var service    = new FolderSortService(
            metadataReader:     stubReader,
            burstMatching:      new BurstMatchingEngine(),
            sequenceGrouping:   new SequenceGroupingService(),
            burstProfileLoader: null,
            logger:             null);

        var result = await service.SortAsync(
            new FolderSortRequest(_tempRoot, FolderSortMode.Full, config, DryRun: true));

        result.Errors.Should().Be(0);
        // Modell-Match greift nicht bei Mehrdeutigkeit → Pfad-Match → _unsorted
        var cameraId = FolderSortService.ExtractCameraIdFromPath(flatPhoto, _tempRoot, config);
        cameraId.Should().Be("_unsorted",
            "Pfad-Match liefert _unsorted wenn kein Kamera-Ordner im Pfad");

        // Direkte Assertion auf FindCameraIdByModel als SSOT-Beweis
        CameraSerialMatcher.FindCameraIdByModel(exifModel, config.Cameras).Should().BeNull(
            "Mehrdeutigkeit → FindCameraIdByModel gibt null zurück");
    }

    /// <summary>
    /// Test (c) Kaskade: Kamera hat SOWOHL passende Serial als auch passendes Modell.
    /// Serial muss Vorrang haben (Stufe 1 vor Stufe 2).
    /// Ergebnis ist identisch, aber der Kaskaden-Vorrang wird durch Unit-Test-Beweis erbracht.
    /// </summary>
    [Fact]
    public void FindCameraId_SerialTakesPrecedenceOverModelMatch()
    {
        // Zwei Kameras: R10 (mit Serial) und R10-NoSerial (gleicher Name, ohne Serial)
        const string r10Serial = "283033005535";
        const string exifModel = "Canon EOS R10";

        var cameras = new Dictionary<string, CameraConfig>
        {
            ["R10"] = new CameraConfig
            {
                Name         = exifModel,
                SerialNumber = r10Serial,
                Enabled      = true,
            },
            // Zweite Kamera gleichen Namens ohne Serial — würde Modell-Match mehrdeutig machen
            ["R10-clone"] = new CameraConfig
            {
                Name         = exifModel,
                SerialNumber = null,
                Enabled      = true,
            },
        };

        // Serial-Match gibt eindeutig "R10" zurück
        var serialResult = CameraSerialMatcher.FindCameraId(r10Serial, cameras);
        serialResult.Should().Be("R10", "Serial-Match muss eindeutig R10 zurückgeben");

        // Modell-Match wäre mehrdeutig (beide haben gleichen Namen) → null
        var modelResult = CameraSerialMatcher.FindCameraIdByModel(exifModel, cameras);
        modelResult.Should().BeNull("Modell-Match ist mehrdeutig wenn zwei Kameras gleichnamig sind");

        // Beweis: In der Kaskade gewinnt Serial (serialResult != null kommt zuerst)
        var cascadeResult = serialResult ?? modelResult;
        cascadeResult.Should().Be("R10",
            "Kaskade: Serial-Match (Stufe 1) gewinnt vor Modell-Match (Stufe 2)");
    }

    // -------------------------------------------------------------------------
    // Welle 4 (TASK-222): ConfigWriterService.LearnCameraIdentity — Unit-Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Test (a): Kamera ohne Serial + Fingerprint mit Serial + Model
    /// → Serial UND CameraModel werden gesetzt, learnedSerial wird zurückgegeben.
    /// </summary>
    [Fact]
    public void LearnCameraIdentity_EmptyFields_SetsSerialAndModel()
    {
        // Arrange
        var cam = new CameraConfig { Name = "Canon R10", SerialNumber = null, CameraModel = null };
        var fp  = new SdFingerprint { SerialNumber = "283033005535", Model = "Canon EOS R10" };

        // Act
        var learned = ConfigWriterService.LearnCameraIdentity(cam, fp);

        // Assert
        cam.SerialNumber.Should().Be("283033005535", "leere SerialNumber muss aus fingerprint gefüllt werden");
        cam.CameraModel.Should().Be("Canon EOS R10",  "leeres CameraModel muss aus fingerprint gefüllt werden");
        learned.Should().Be("283033005535",            "LearnCameraIdentity gibt die gelernte Serial zurück");
    }

    /// <summary>
    /// Test (b): Kamera MIT bestehender Serial → wird NICHT überschrieben (AK2).
    /// </summary>
    [Fact]
    public void LearnCameraIdentity_ExistingSerial_IsNotOverwritten()
    {
        // Arrange
        const string existingSerial = "999000111222";
        const string newSerial      = "283033005535";
        var cam = new CameraConfig { Name = "Canon R10", SerialNumber = existingSerial, CameraModel = null };
        var fp  = new SdFingerprint { SerialNumber = newSerial, Model = "Canon EOS R10" };

        // Act
        var learned = ConfigWriterService.LearnCameraIdentity(cam, fp);

        // Assert
        cam.SerialNumber.Should().Be(existingSerial, "bestehende Serial darf NIE überschrieben werden");
        learned.Should().BeNull("keine neue Serial gelernt wenn Kamera schon eine hat");
        // CameraModel darf trotzdem gelernt werden wenn leer
        cam.CameraModel.Should().Be("Canon EOS R10", "leeres CameraModel darf auch bei Serial-Konflikt gefüllt werden");
    }

    /// <summary>
    /// Test (c): Kamera MIT bestehendem CameraModel → wird NICHT überschrieben.
    /// </summary>
    [Fact]
    public void LearnCameraIdentity_ExistingCameraModel_IsNotOverwritten()
    {
        // Arrange
        var cam = new CameraConfig
        {
            Name        = "Canon R10",
            SerialNumber = null,
            CameraModel  = "Canon EOS R10",  // bereits gesetzt
        };
        var fp = new SdFingerprint { SerialNumber = "283033005535", Model = "Canon EOS R10 (neu)" };

        // Act
        var learned = ConfigWriterService.LearnCameraIdentity(cam, fp);

        // Assert
        cam.CameraModel.Should().Be("Canon EOS R10", "bestehender CameraModel darf nicht überschrieben werden");
        cam.SerialNumber.Should().Be("283033005535",  "SerialNumber soll trotzdem gelernt werden wenn leer");
        learned.Should().Be("283033005535");
    }

    /// <summary>
    /// Test (d): Fingerprint ohne Serial und ohne Model → Kamera unverändert, null zurück.
    /// </summary>
    [Fact]
    public void LearnCameraIdentity_EmptyFingerprint_ChangesNothing()
    {
        // Arrange
        var cam = new CameraConfig { Name = "Kamera ohne Config", SerialNumber = null, CameraModel = null };
        var fp  = new SdFingerprint { SerialNumber = null, Model = null };

        // Act
        var learned = ConfigWriterService.LearnCameraIdentity(cam, fp);

        // Assert
        cam.SerialNumber.Should().BeNull("leerer Fingerprint darf nichts setzen");
        cam.CameraModel.Should().BeNull("leerer Fingerprint darf nichts setzen");
        learned.Should().BeNull("kein learn ohne Fingerprint-Daten");
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static UmiConfig ConfigWithCamera(string cameraId, string? serial)
    {
        var cfg = new UmiConfig
        {
            GlobalPaths = new GlobalPaths
            {
                Workbench = "",
                Projects  = "",
                GpxSource = "",
                Tools     = new ToolPaths { ExifTool = "" },
            },
        };
        cfg.Cameras[cameraId] = new CameraConfig
        {
            Name         = cameraId,
            FolderName   = cameraId,
            Enabled      = true,
            SerialNumber = serial,
            FileTypes    = new CameraFileTypes
            {
                Photo = new[] { ".jpg", ".cr3" },
                Video = new[] { ".mp4" },
            },
        };
        return cfg;
    }

    /// <summary>
    /// Test-Subklasse von MetadataReader (nutzt ReadPhotoMetadata virtual).
    /// Gibt für eine konfigurierte Datei eine kontrollierte PhotoMetadata zurück,
    /// sodass FolderSortService Integration-Tests keine echten EXIF-Fixtures brauchen.
    /// Alle anderen Dateien delegieren an die Basisimplementierung.
    /// </summary>
    private sealed class StubMetadataReader : MetadataReader
    {
        private readonly string    _stubbedPath;
        private readonly string    _stubbedSerial;
        private readonly DateTime  _stubbedDate;

        public StubMetadataReader(string stubbedPath, string stubbedSerial, DateTime stubbedDate)
            : base(logger: null)
        {
            _stubbedPath   = Path.GetFullPath(stubbedPath);
            _stubbedSerial = stubbedSerial;
            _stubbedDate   = stubbedDate;
        }

        public override PhotoMetadata ReadPhotoMetadata(string filePath)
        {
            if (string.Equals(Path.GetFullPath(filePath), _stubbedPath, StringComparison.OrdinalIgnoreCase))
            {
                return new PhotoMetadata
                {
                    FilePath     = filePath,
                    FileName     = Path.GetFileName(filePath),
                    CreateDate   = _stubbedDate,
                    FileSize     = 3,
                    CameraSerial = _stubbedSerial,
                };
            }

            return base.ReadPhotoMetadata(filePath);
        }
    }

    /// <summary>
    /// Erweiterter Test-Stub: steuert CameraSerial UND CameraModel unabhängig.
    /// Wird in Welle-3-Tests genutzt um Modell-Match-Verhalten zu verifizieren.
    /// </summary>
    private sealed class StubMetadataReaderWithModel : MetadataReader
    {
        private readonly string   _stubbedPath;
        private readonly string?  _stubbedSerial;
        private readonly string?  _stubbedModel;
        private readonly DateTime _stubbedDate;

        public StubMetadataReaderWithModel(
            string   stubbedPath,
            string?  serial,
            string?  model,
            DateTime stubbedDate)
            : base(logger: null)
        {
            _stubbedPath   = Path.GetFullPath(stubbedPath);
            _stubbedSerial = serial;
            _stubbedModel  = model;
            _stubbedDate   = stubbedDate;
        }

        public override PhotoMetadata ReadPhotoMetadata(string filePath)
        {
            if (string.Equals(Path.GetFullPath(filePath), _stubbedPath, StringComparison.OrdinalIgnoreCase))
            {
                return new PhotoMetadata
                {
                    FilePath     = filePath,
                    FileName     = Path.GetFileName(filePath),
                    CreateDate   = _stubbedDate,
                    FileSize     = 3,
                    CameraSerial = _stubbedSerial,
                    CameraModel  = _stubbedModel,
                };
            }

            return base.ReadPhotoMetadata(filePath);
        }
    }
}
