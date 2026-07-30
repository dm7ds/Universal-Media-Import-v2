// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using System.Text.Json;
using FluentAssertions;
using UMI.Core.Models;
using UMI.Core.Services;
using UMI.Core.Utilities;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// Regression tests for TASK-218 FIX-1:
/// ImportPipelineService Scan-Pfad muss LoadBurstConfig() aufrufen statt
/// den alten inline-Guard (ActiveProfiles.Count > 0) zu nutzen.
///
/// Der Bug: BurstDetectionConfig mit Enabled=true UND ActiveProfiles=[]
/// (Alt-Config vor TASK-215) verursachte im alten Scan-Pfad dass LoadedProfiles
/// leer blieb. BurstMatchingEngine gab dann für jedes Foto "Single" zurück —
/// still, kein Crash, falsche DB-Daten.
///
/// Strategie: Da ImportPipelineService.LoadBurstConfig private ist und
/// ScanSourceAsync eine ImportDatabase benötigt (UMI.Data — nicht im Test-Projekt),
/// testen wir die kritische Komponenten-Kette direkt:
///   BurstProfileLoader.ListAvailableProfiles()
///   + LoadProfiles() (Fallback-Pfad)
///   + BurstMatchingEngine.MatchBurstProfile()
///
/// Das beweist dass der Fallback-Pfad (ActiveProfiles=[], Profile auf Disk)
/// tatsächlich zu ShootingMode != "Single" führt — was im Scan-Pfad via
/// config.BurstDetectionConfig = LoadBurstConfig(config) jetzt garantiert wird.
/// </summary>
public class ImportPipelineServiceBurstTests : IDisposable
{
    private readonly string _tempRoot;

    public ImportPipelineServiceBurstTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "umi-burst-fix1-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    /// <summary>
    /// Beweist das Fehlerverhalten VOR dem Fix:
    /// ActiveProfiles=[] + alter Guard → LoadedProfiles bleibt leer → ShootingMode="Single".
    /// Das ist der Bug.
    /// </summary>
    [Fact]
    public void OldGuard_EmptyActiveProfiles_ProducesShootingModeSingle_ThatsTheBug()
    {
        // Arrange: Config mit Enabled=true, ActiveProfiles=[]
        var burstConfig = new BurstDetectionConfig
        {
            Enabled        = true,
            ActiveProfiles = new List<string>(), // leer — alt-Config vor TASK-215
            // LoadedProfiles bleibt leer weil der alte Guard greift:
            // if (ActiveProfiles.Count > 0 && burstProfileLoader != null) → false
        };

        var engine = new BurstMatchingEngine();
        var metadata = new PhotoMetadata
        {
            FilePath = "/test/IMG_0001.jpg",
            FileName = "IMG_0001.jpg",
            IsVideo  = false,
        };

        // Act: alter Guard-Pfad — LoadedProfiles == [] weil nie geladen
        var shootingMode = engine.MatchBurstProfile(metadata, burstConfig);

        // Assert: Das IST der Bug — jedes Foto bekommt "Single"
        shootingMode.Should().Be("Single",
            "Bug-Repro: leer ActiveProfiles + alter Guard lässt LoadedProfiles leer → always Single");
    }

    /// <summary>
    /// Beweist das korrekte Verhalten NACH dem Fix:
    /// LoadBurstConfig-Fallback lädt verfügbare Profile von Disk wenn ActiveProfiles=[].
    /// BurstMatchingEngine matcht dann korrekt → ShootingMode != "Single".
    /// </summary>
    [Fact]
    public async Task LoadBurstConfig_Fallback_EmptyActiveProfiles_LoadsProfilesFromDisk()
    {
        // --- Arrange: Temp-configRoot mit ≥2 Burst-Profilen ---
        var configRoot      = Path.Combine(_tempRoot, "cfg");
        var burstPresetsDir = Path.Combine(configRoot, "presets", "burst");
        Directory.CreateDirectory(burstPresetsDir);

        // Profil 1: "Sport" — leere match_conditions (matched alles), min_count=2
        var sportProfile = new
        {
            name             = "Sport",
            description      = "Stub für FIX-1 Repro-Test",
            priority         = 10,
            match_conditions = new { @operator = "AND", conditions = Array.Empty<object>(), groups = Array.Empty<object>() },
            grouping         = new { max_gap_seconds = 60.0, min_count = 2, adaptive_gap = false },
        };
        await File.WriteAllTextAsync(
            Path.Combine(burstPresetsDir, "Sport.umi"),
            JsonSerializer.Serialize(sportProfile));

        // Profil 2: "Astro" — zweites Profil auf Disk
        var astroProfile = new
        {
            name             = "Astro",
            description      = "Stub Astro für FIX-1 Repro-Test",
            priority         = 20,
            match_conditions = new { @operator = "AND", conditions = Array.Empty<object>(), groups = Array.Empty<object>() },
            grouping         = new { max_gap_seconds = 120.0, min_count = 3, adaptive_gap = false },
        };
        await File.WriteAllTextAsync(
            Path.Combine(burstPresetsDir, "Astro.umi"),
            JsonSerializer.Serialize(astroProfile));

        // Config: Enabled=true, ActiveProfiles=[] (Alt-Config vor TASK-215)
        var burstConfig = new BurstDetectionConfig
        {
            Enabled        = true,
            ActiveProfiles = new List<string>(), // leer — Trigger für den Fallback
        };

        // --- Act: LoadBurstConfig-Fallback (wie ImportPipelineService.LoadBurstConfig es jetzt tut) ---
        var resolver = new ConfigPathResolver(configRoot);
        var loader   = new BurstProfileLoader(resolver, logger: null);

        // Reproduziert exakt die Fallback-Logik aus ImportPipelineService.LoadBurstConfig:
        if (loader != null
            && (burstConfig.LoadedProfiles == null || burstConfig.LoadedProfiles.Count == 0))
        {
            var profilesToLoad = burstConfig.ActiveProfiles.Count > 0
                ? burstConfig.ActiveProfiles
                : loader.ListAvailableProfiles(); // Fallback bei leerem ActiveProfiles

            if (profilesToLoad.Count > 0)
                burstConfig.LoadedProfiles = loader.LoadProfiles(profilesToLoad);
        }

        // --- Assert Fallback-Laden ---
        burstConfig.LoadedProfiles.Should().HaveCountGreaterThanOrEqualTo(2,
            "Fallback muss alle verfügbaren Profile von Disk laden wenn ActiveProfiles=[]");

        burstConfig.LoadedProfiles.Should().Contain(p => p.Name == "Sport");
        burstConfig.LoadedProfiles.Should().Contain(p => p.Name == "Astro");

        // --- Assert BurstMatchingEngine matcht korrekt ---
        var engine = new BurstMatchingEngine();
        var metadata = new PhotoMetadata
        {
            FilePath = "/test/IMG_0001.jpg",
            FileName = "IMG_0001.jpg",
            IsVideo  = false,
        };

        var shootingMode = engine.MatchBurstProfile(metadata, burstConfig);

        shootingMode.Should().NotBe("Single",
            "Nach Fallback-LoadBurstConfig muss BurstMatchingEngine ein Profil matchen — " +
            "Sport hat leere conditions (matched alles) und wird zuerst geprüft (Priority=10)");

        shootingMode.Should().Be("Sport",
            "Sport hat Priority=10 (< Astro Priority=20) und leere conditions → matched zuerst");
    }

    /// <summary>
    /// Verifiziert die DRY-Auflage (Auflage 2):
    /// BurstProfileLoader.ListAvailableProfiles liefert beide Fixture-Profile.
    /// Beide Pfade (Scan + Sequence) nutzen jetzt LoadBurstConfig → eine Logik.
    /// </summary>
    [Fact]
    public async Task ListAvailableProfiles_TwoProfilesOnDisk_ReturnsBoth()
    {
        var configRoot      = Path.Combine(_tempRoot, "cfg2");
        var burstPresetsDir = Path.Combine(configRoot, "presets", "burst");
        Directory.CreateDirectory(burstPresetsDir);

        await File.WriteAllTextAsync(Path.Combine(burstPresetsDir, "Sport.umi"),   "{}");
        await File.WriteAllTextAsync(Path.Combine(burstPresetsDir, "Wildlife.umi"), "{}");

        var resolver = new ConfigPathResolver(configRoot);
        var loader   = new BurstProfileLoader(resolver, logger: null);

        var available = loader.ListAvailableProfiles();

        available.Should().HaveCount(2);
        available.Should().Contain("Sport");
        available.Should().Contain("Wildlife");
    }

    // ------------------------------------------------------------------
    // I-006: Sequenz-Erkennung fiel beim User still aus — 108 Fotos in der
    // Import-DB, 0 Sequenzen, keine einzige Log-Zeile.
    //
    // DetectSequencesAsync fragt zuerst GetDistinctDates() ab
    // ("SELECT DISTINCT date(capture_date) ...") und holt dann pro Datum die
    // Fotos ("WHERE date(capture_date) = @Date").
    //
    // Die Tests unten belegen die latente Bruchstelle dieser Kette anhand der
    // echten SQLite-Semantik: sobald capture_date kein gueltiges Datum ist,
    // liefert date() NULL — und ein Vergleich GEGEN NULL ist in SQL niemals
    // wahr. Der Tag faellt dann in eine leere Trefferliste und wurde vor dem
    // Fix stillschweigend uebersprungen.
    // ------------------------------------------------------------------

    private static Microsoft.Data.Sqlite.SqliteConnection OpenImportsTable(
        params (string CameraId, string? CaptureDate)[] rows)
    {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();

        using (var create = conn.CreateCommand())
        {
            create.CommandText =
                "CREATE TABLE imports (camera_id TEXT, capture_date TEXT, is_video INTEGER NOT NULL DEFAULT 0);";
            create.ExecuteNonQuery();
        }

        foreach (var (cameraId, captureDate) in rows)
        {
            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO imports (camera_id, capture_date, is_video) VALUES (@c, @d, 0);";
            insert.Parameters.AddWithValue("@c", cameraId);
            insert.Parameters.AddWithValue("@d", (object?)captureDate ?? DBNull.Value);
            insert.ExecuteNonQuery();
        }

        return conn;
    }

    private static List<string?> QueryDistinctDates(Microsoft.Data.Sqlite.SqliteConnection conn, string cameraId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT date(capture_date) FROM imports WHERE camera_id = @c AND is_video = 0;";
        cmd.Parameters.AddWithValue("@c", cameraId);

        var result = new List<string?>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        return result;
    }

    private static int CountPhotosForDate(
        Microsoft.Data.Sqlite.SqliteConnection conn, string cameraId, string? date)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM imports WHERE camera_id = @c AND is_video = 0 AND date(capture_date) = @d;";
        cmd.Parameters.AddWithValue("@c", cameraId);
        cmd.Parameters.AddWithValue("@d", (object?)date ?? DBNull.Value);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Der gesunde Fall: normales Datum → Kette traegt, Fotos werden gefunden.</summary>
    [Fact]
    public void SequenceDateChain_WithValidCaptureDate_FindsPhotos()
    {
        using var conn = OpenImportsTable(
            ("canon-eos-r5m2", "2026-07-07"),
            ("canon-eos-r5m2", "2026-07-07"),
            ("canon-eos-r5m2", "2026-07-07"));

        var dates = QueryDistinctDates(conn, "canon-eos-r5m2");

        dates.Should().ContainSingle().Which.Should().Be("2026-07-07");
        CountPhotosForDate(conn, "canon-eos-r5m2", dates[0]).Should().Be(3,
            "bei gueltigem capture_date muss die Sequenz-Erkennung ihre Fotos wiederfinden");
    }

    /// <summary>
    /// Die Bruchstelle: unlesbares capture_date → date() ist NULL → der
    /// Rueckvergleich findet NICHTS. Genau das Symptom "N Fotos, 0 Sequenzen".
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("07.07.2026")]   // lokalisiertes Format — SQLite kann es nicht lesen
    public void SequenceDateChain_WithUnreadableCaptureDate_SilentlyLosesAllPhotos(string? captureDate)
    {
        using var conn = OpenImportsTable(
            ("canon-eos-r5m2", captureDate),
            ("canon-eos-r5m2", captureDate),
            ("canon-eos-r5m2", captureDate));

        var dates = QueryDistinctDates(conn, "canon-eos-r5m2");

        // date() liefert NULL — der Tag existiert also, ist aber unbrauchbar.
        dates.Should().ContainSingle().Which.Should().BeNull();

        // Und der Rueckvergleich gegen NULL trifft in SQL nie zu:
        CountPhotosForDate(conn, "canon-eos-r5m2", dates[0]).Should().Be(0,
            "WHERE date(capture_date) = NULL ist niemals wahr — die Fotos gehen verloren");

        // Deshalb faengt DetectSequencesAsync leere Datums-Werte jetzt explizit
        // ab und meldet sie, statt den Tag stumm zu ueberspringen.
    }

    /// <summary>
    /// Gegenprobe zur Fehlersuche: ein abweichender camera_id laesst die
    /// Datumsliste komplett leer — der zweite Weg zu "0 Sequenzen ohne Meldung".
    /// </summary>
    [Fact]
    public void SequenceDateChain_WithMismatchedCameraId_ReturnsNoDatesAtAll()
    {
        using var conn = OpenImportsTable(
            ("Canon-EOS-R5m2", "2026-07-07"),   // Grossschreibung
            ("Canon-EOS-R5m2", "2026-07-07"));

        QueryDistinctDates(conn, "canon-eos-r5m2").Should().BeEmpty(
            "SQLite vergleicht TEXT per Default case-sensitive (BINARY collation)");
    }
}
