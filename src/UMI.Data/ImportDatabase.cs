// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This file is part of UMI - Universal Media Import.
//
//     UMI - Universal Media Import is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     UMI - Universal Media Import is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with UMI - Universal Media Import.  If not, see <http://www.gnu.org/licenses/>.

using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using UMI.Data.Models;

namespace UMI.Data;

/// <summary>
/// SQLite Import-Datenbank für Pipeline V2.1 (Scan-First Architektur).
/// Speichert gescannte Dateien, Sequenzen und Copy-Status.
/// </summary>
public class ImportDatabase : IDisposable, IAsyncDisposable
{
    /// <summary>Ordnername für Einzelaufnahmen ohne Burst-Sequenz. SSOT liegt in UMI.Core.Utilities.FolderNameConstants.SingleShots.</summary>
    private const string SingleShotsFolder = "Single_Shots";

    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly ILogger<ImportDatabase>? _logger;
    private SqliteConnection? _connection;

    public ImportDatabase(string databasePath, ILogger<ImportDatabase>? logger = null)
    {
        _databasePath = databasePath;
        _connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Cache=Shared";
        _logger = logger;

        DapperConfig.Configure();
    }

    /// <summary>
    /// Gibt die Connection zurück oder wirft Exception wenn nicht initialisiert.
    /// </summary>
    private SqliteConnection GetConnection()
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized. Call InitializeAsync() first.");
        return _connection;
    }

    /// <summary>
    /// Initialisiert die Datenbank: Schema erstellen, WAL-Modus aktivieren, Indices erstellen.
    /// MUSS vor allen anderen Operationen aufgerufen werden.
    /// </summary>
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        await _connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
        await _connection.ExecuteAsync("PRAGMA synchronous=NORMAL;");

        await CreateSchemaAsync();

        _logger?.LogDebug("SQLite Import-Datenbank initialisiert: {Path}", GetDatabasePath());
    }

    /// <summary>
    /// Gibt den Pfad der Datenbank zurück.
    /// </summary>
    public string GetDatabasePath() => _databasePath;

    private async Task CreateSchemaAsync()
    {
        var sql = @"
            -- Imports Table (alle gescannten Dateien)
            CREATE TABLE IF NOT EXISTS imports (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_path TEXT NOT NULL UNIQUE,
                dest_path TEXT,
                filename TEXT NOT NULL,
                camera_id TEXT NOT NULL,
                capture_date TEXT NOT NULL,
                capture_time TEXT NOT NULL,
                media_type TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                is_video INTEGER NOT NULL,
                camera_model TEXT,
                shooting_mode TEXT,
                exposure_time REAL,
                continuous_drive INTEGER,
                exposure_mode INTEGER,
                duration_ms INTEGER,
                sequence_id INTEGER,
                copy_status INTEGER NOT NULL DEFAULT 0,
                error_message TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (sequence_id) REFERENCES sequences(id)
            );

            -- Sequences Table (erkannte Burst-Sequenzen)
            CREATE TABLE IF NOT EXISTS sequences (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                camera_id TEXT NOT NULL,
                capture_date TEXT NOT NULL,
                mode TEXT NOT NULL,
                folder_name TEXT NOT NULL,
                photo_count INTEGER NOT NULL,
                first_photo_time TEXT NOT NULL,
                threshold_used REAL NOT NULL,
                created_at TEXT NOT NULL
            );

            -- Indices für Performance
            CREATE INDEX IF NOT EXISTS idx_imports_camera_date
                ON imports(camera_id, capture_date);
            CREATE INDEX IF NOT EXISTS idx_imports_copy_status
                ON imports(copy_status);
            CREATE INDEX IF NOT EXISTS idx_imports_sequence_id
                ON imports(sequence_id);
        ";

        await GetConnection().ExecuteAsync(sql);
    }

    /// <summary>
    /// Fügt eine gescannte Datei ein.
    /// </summary>
    public async Task<int> InsertImportAsync(ImportedFile import)
    {
        var sql = @"
            INSERT OR IGNORE INTO imports (
                source_path, dest_path, filename, camera_id, capture_date, capture_time,
                media_type, file_size, is_video, camera_model, shooting_mode,
                exposure_time, continuous_drive, exposure_mode, duration_ms,
                sequence_id, copy_status, error_message, created_at, updated_at
            ) VALUES (
                @SourcePath, @DestPath, @Filename, @CameraId, @CaptureDate, @CaptureTime,
                @MediaType, @FileSize, @IsVideo, @CameraModel, @ShootingMode,
                @ExposureTime, @ContinuousDrive, @ExposureMode, @DurationMs,
                @SequenceId, @CopyStatus, @ErrorMessage, @CreatedAt, @UpdatedAt
            );
            SELECT last_insert_rowid();
        ";

        return await GetConnection().ExecuteScalarAsync<int>(sql, import);
    }

    /// <summary>
    /// Fügt mehrere gescannte Dateien in einer Transaktion ein (Performance-Optimierung).
    /// </summary>
    public async Task InsertImportBatchAsync(IEnumerable<ImportedFile> imports)
    {
        var sql = @"
            INSERT OR IGNORE INTO imports (
                source_path, dest_path, filename, camera_id, capture_date, capture_time,
                media_type, file_size, is_video, camera_model, shooting_mode,
                exposure_time, continuous_drive, exposure_mode, duration_ms,
                sequence_id, copy_status, error_message, created_at, updated_at
            ) VALUES (
                @SourcePath, @DestPath, @Filename, @CameraId, @CaptureDate, @CaptureTime,
                @MediaType, @FileSize, @IsVideo, @CameraModel, @ShootingMode,
                @ExposureTime, @ContinuousDrive, @ExposureMode, @DurationMs,
                @SequenceId, @CopyStatus, @ErrorMessage, @CreatedAt, @UpdatedAt
            );
        ";

        using var transaction = GetConnection().BeginTransaction();
        try
        {
            await GetConnection().ExecuteAsync(sql, imports, transaction);
            await transaction.CommitAsync();
            _logger?.LogDebug("Batch-Insert: {Count} Dateien eingefügt", imports.Count());
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Holt alle Fotos einer Kamera an einem bestimmten Tag (für Sequenzerkennung).
    /// Sortiert nach CaptureDate.
    /// </summary>
    public async Task<List<ImportedFile>> GetPhotosForSequenceDetectionAsync(string cameraId, DateTime date)
    {
        var sql = @"
            SELECT * FROM imports
            WHERE camera_id = @CameraId
              AND is_video = 0
              AND date(capture_date) = date(@Date)
            ORDER BY capture_date;
        ";

        var results = await GetConnection().QueryAsync<ImportedFile>(sql, new { CameraId = cameraId, Date = date });
        return results.ToList();
    }

    /// <summary>
    /// Fügt eine erkannte Sequenz ein und gibt die ID zurück.
    /// </summary>
    public async Task<int> InsertSequenceAsync(DetectedSequence sequence)
    {
        var sql = @"
            INSERT INTO sequences (camera_id, capture_date, mode, folder_name, photo_count, first_photo_time, threshold_used, created_at)
            VALUES (@CameraId, @CaptureDate, @Mode, @FolderName, @PhotoCount, @FirstPhotoTime, @ThresholdUsed, @CreatedAt);
            SELECT last_insert_rowid();
        ";

        return await GetConnection().ExecuteScalarAsync<int>(sql, sequence);
    }

    /// <summary>
    /// Weist mehreren Imports eine Sequenz-ID zu.
    /// </summary>
    public async Task AssignSequenceAsync(IEnumerable<long> importIds, long sequenceId)
    {
        var sql = "UPDATE imports SET sequence_id = @SequenceId, updated_at = @UpdatedAt WHERE id = @Id;";

        var updates = importIds.Select(id => new { Id = id, SequenceId = sequenceId, UpdatedAt = DateTime.UtcNow.ToString("o") });

        using var transaction = GetConnection().BeginTransaction();
        try
        {
            await GetConnection().ExecuteAsync(sql, updates, transaction);
            await transaction.CommitAsync();
            _logger?.LogDebug("Sequenz {SequenceId}: {Count} Fotos zugewiesen", sequenceId, importIds.Count());
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Weist Files einer Sequenz zu UND aktualisiert dest_path mit Sequenz-Ordner.
    /// Layout-aware: Funktioniert mit und ohne Photo/ im Pfad.
    /// Beispiel: Photo\IMG_1208.CR3 → Photo\Sport_154111\IMG_1208.CR3
    /// Beispiel: R10\IMG_1208.CR3 → R10\Sport_154111\IMG_1208.CR3
    /// </summary>
    public async Task AssignSequenceToFiles(long sequenceId, string folderName, IEnumerable<long> fileIds)
    {
        var idList = fileIds.ToList();
        if (idList.Count == 0) return;

        // FIX-4 (TASK-218): SQLite IN-clause limit is 32 766 parameters. Chunk to avoid
        // SqliteException on large timelapse sequences. One transaction wraps all batches.
        const int ChunkSize = 900;

        var allUpdates = new List<object>();
        foreach (var chunk in idList.Chunk(ChunkSize))
        {
            var files = await GetConnection().QueryAsync<(long Id, string DestPath, string Filename)>(
                "SELECT id, dest_path, filename FROM imports WHERE id IN @Ids",
                new { Ids = chunk });

            foreach (var file in files)
            {
                var dir = Path.GetDirectoryName(file.DestPath) ?? "";
                var newPath = Path.Combine(dir, folderName, file.Filename);
                allUpdates.Add(new { SeqId = sequenceId, Path = newPath, UpdatedAt = DateTime.UtcNow.ToString("o"), Id = file.Id });
            }
        }

        var sql = "UPDATE imports SET sequence_id = @SeqId, dest_path = @Path, updated_at = @UpdatedAt WHERE id = @Id";

        using var transaction = GetConnection().BeginTransaction();
        try
        {
            await GetConnection().ExecuteAsync(sql, allUpdates, transaction);
            await transaction.CommitAsync();
            _logger?.LogDebug("Sequenz {SequenceId} ({Folder}): {Count} Fotos zugewiesen + dest_path aktualisiert",
                sequenceId, folderName, idList.Count);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Gibt Liste aller Datum-Werte für eine Kamera zurück (für Sequenz-Detection pro Tag).
    /// </summary>
    public async Task<List<string>> GetDistinctDates(string cameraId)
    {
        var sql = @"
            SELECT DISTINCT date(capture_date) as capture_date
            FROM imports
            WHERE camera_id = @CameraId AND is_video = 0
            ORDER BY capture_date;
        ";

        var results = await GetConnection().QueryAsync<string>(sql, new { CameraId = cameraId });
        return results.ToList();
    }

    /// <summary>
    /// Holt alle Fotos einer Kamera an einem Tag, nach CaptureTime sortiert.
    /// </summary>
    public async Task<List<ImportedFile>> GetPhotosByDateAndCamera(string cameraId, string date)
    {
        var sql = @"
            SELECT * FROM imports
            WHERE camera_id = @CameraId
              AND is_video = 0
              AND date(capture_date) = @Date
            ORDER BY capture_time;
        ";

        var results = await GetConnection().QueryAsync<ImportedFile>(sql, new { CameraId = cameraId, Date = date });
        return results.ToList();
    }

    /// <summary>
    /// Weist alle Single-Shots eines Tages dem Single_Shots Ordner zu (nur wenn SEQ existiert).
    /// Layout-aware: Funktioniert mit und ohne Photo/ im Pfad.
    /// Beispiel: Photo\IMG_1208.CR3 → Photo\Single_Shots\IMG_1208.CR3
    /// Beispiel: R10\IMG_1208.CR3 → R10\Single_Shots\IMG_1208.CR3
    /// </summary>
    public async Task AssignSingleShots(string cameraId, string date)
    {

        var files = await GetConnection().QueryAsync<(long Id, string DestPath, string Filename)>(
            @"SELECT id, dest_path, filename FROM imports
              WHERE camera_id = @CameraId
                AND date(capture_date) = @Date
                AND is_video = 0
                AND sequence_id IS NULL",
            new { CameraId = cameraId, Date = date });

        var filesList = files.ToList();
        if (filesList.Count == 0) return;

        var updates = filesList.Select(file =>
        {
            var dir = Path.GetDirectoryName(file.DestPath) ?? "";
            var newPath = Path.Combine(dir, SingleShotsFolder, file.Filename);
            return new { Path = newPath, UpdatedAt = DateTime.UtcNow.ToString("o"), Id = file.Id };
        });

        var sql = "UPDATE imports SET dest_path = @Path, updated_at = @UpdatedAt WHERE id = @Id";

        using var transaction = GetConnection().BeginTransaction();
        try
        {
            await GetConnection().ExecuteAsync(sql, updates, transaction);
            await transaction.CommitAsync();
            _logger?.LogDebug("Single_Shots [{Date}]: {Count} Fotos zugewiesen", date, filesList.Count);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Aktualisiert den dest_path eines Imports (nach Sequenzerkennung).
    /// </summary>
    public async Task UpdateDestPathAsync(long importId, string destPath)
    {
        var sql = "UPDATE imports SET dest_path = @DestPath, updated_at = @UpdatedAt WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = importId, DestPath = destPath, UpdatedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>
    /// Aktualisiert dest_path für mehrere Imports (Batch-Optimierung).
    /// </summary>
    public async Task UpdateDestPathBatchAsync(IEnumerable<(long importId, string destPath)> updates)
    {
        var sql = "UPDATE imports SET dest_path = @DestPath, updated_at = @UpdatedAt WHERE id = @ImportId;";

        var data = updates.Select(u => new { ImportId = u.importId, DestPath = u.destPath, UpdatedAt = DateTime.UtcNow.ToString("o") });

        using var transaction = GetConnection().BeginTransaction();
        try
        {
            await GetConnection().ExecuteAsync(sql, data, transaction);
            await transaction.CommitAsync();
            _logger?.LogDebug("Batch-Update dest_path: {Count} Dateien", updates.Count());
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Holt alle ausstehenden Kopieraufträge (CopyStatus = Pending).
    /// </summary>
    public async Task<List<CopyJob>> GetPendingCopiesAsync()
    {
        var sql = @"
            SELECT id AS ImportId, source_path AS SourcePath, dest_path AS DestPath,
                   file_size AS FileSize, is_video AS IsVideo
            FROM imports
            WHERE copy_status = 0 AND dest_path IS NOT NULL
            ORDER BY is_video, capture_date;
        ";

        var results = await GetConnection().QueryAsync<CopyJob>(sql);
        return results.ToList();
    }

    /// <summary>
    /// Markiert einen Import als "wird gerade kopiert".
    /// </summary>
    public async Task MarkCopyInProgressAsync(long importId)
    {
        var sql = "UPDATE imports SET copy_status = 1, updated_at = @UpdatedAt WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = importId, UpdatedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>
    /// Markiert einen Import als erfolgreich kopiert.
    /// </summary>
    public async Task MarkCopyCompletedAsync(long importId)
    {
        var sql = "UPDATE imports SET copy_status = 2, updated_at = @UpdatedAt WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = importId, UpdatedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>
    /// Markiert einen Import als fehlgeschlagen.
    /// </summary>
    public async Task MarkCopyFailedAsync(long importId, string errorMessage)
    {
        var sql = "UPDATE imports SET copy_status = 3, error_message = @Error, updated_at = @UpdatedAt WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = importId, Error = errorMessage, UpdatedAt = DateTime.UtcNow.ToString("o") });

        _logger?.LogWarning("Copy failed für Import {Id}: {Error}", importId, errorMessage);
    }

    /// <summary>
    /// Berechnet Import-Statistiken via SQL-Aggregation.
    /// </summary>
    public async Task<ImportStats> GetImportStatsAsync()
    {
        var sql = @"
            SELECT
                COUNT(*) AS TotalFiles,
                COALESCE(SUM(file_size), 0) AS TotalSize,
                COALESCE(SUM(CASE WHEN is_video = 0 THEN 1 ELSE 0 END), 0) AS PhotosCount,
                COALESCE(SUM(CASE WHEN is_video = 1 THEN 1 ELSE 0 END), 0) AS VideosCount,
                COALESCE((SELECT COUNT(*) FROM sequences), 0) AS SequencesCount,
                COALESCE(SUM(CASE WHEN copy_status = 2 THEN 1 ELSE 0 END), 0) AS CopyCompleted,
                COALESCE(SUM(CASE WHEN copy_status = 0 THEN 1 ELSE 0 END), 0) AS CopyPending,
                COALESCE(SUM(CASE WHEN copy_status = 3 THEN 1 ELSE 0 END), 0) AS CopyFailed,
                COALESCE(SUM(CASE WHEN copy_status = 1 THEN 1 ELSE 0 END), 0) AS CopyInProgress
            FROM imports;
        ";

        return await GetConnection().QuerySingleAsync<ImportStats>(sql);
    }

    /// <summary>
    /// Berechnet Import-Statistiken NUR für eine bestimmte Kamera.
    /// </summary>
    public async Task<ImportStats> GetStatsByCameraId(string cameraId)
    {
        var sql = @"
            SELECT
                COUNT(*) AS TotalFiles,
                COALESCE(SUM(file_size), 0) AS TotalSize,
                COALESCE(SUM(CASE WHEN is_video = 0 THEN 1 ELSE 0 END), 0) AS PhotosCount,
                COALESCE(SUM(CASE WHEN is_video = 1 THEN 1 ELSE 0 END), 0) AS VideosCount,
                COALESCE((SELECT COUNT(*) FROM sequences WHERE camera_id = @CameraId), 0) AS SequencesCount,
                COALESCE(SUM(CASE WHEN copy_status = 2 THEN 1 ELSE 0 END), 0) AS CopyCompleted,
                COALESCE(SUM(CASE WHEN copy_status = 0 THEN 1 ELSE 0 END), 0) AS CopyPending,
                COALESCE(SUM(CASE WHEN copy_status = 3 THEN 1 ELSE 0 END), 0) AS CopyFailed,
                COALESCE(SUM(CASE WHEN copy_status = 1 THEN 1 ELSE 0 END), 0) AS CopyInProgress
            FROM imports
            WHERE camera_id = @CameraId;
        ";

        return await GetConnection().QuerySingleAsync<ImportStats>(sql, new { CameraId = cameraId });
    }

    /// <summary>
    /// Gibt alle erfolgreich kopierten Dateien als dest_path zurück.
    /// </summary>
    public async Task<List<string>> GetCopiedDestPaths()
    {
        var sql = "SELECT dest_path FROM imports WHERE copy_status = 2 AND dest_path IS NOT NULL;";
        var results = await GetConnection().QueryAsync<string>(sql);
        return results.ToList();
    }

    /// <summary>
    /// Gibt kopierte Dateien NUR für eine bestimmte Kamera zurück.
    /// </summary>
    public async Task<List<string>> GetCopiedDestPaths(string cameraId)
    {
        var sql = "SELECT dest_path FROM imports WHERE copy_status = 2 AND dest_path IS NOT NULL AND camera_id = @CameraId;";
        var results = await GetConnection().QueryAsync<string>(sql, new { CameraId = cameraId });
        return results.ToList();
    }

    /// <summary>
    /// Gibt kopierte Dateien mit Größe zurück (für Post-Import Verification).
    /// </summary>
    public async Task<List<(string DestPath, long FileSize)>> GetCopiedFilesForVerify()
    {
        var sql = "SELECT dest_path, file_size FROM imports WHERE copy_status = 2 AND dest_path IS NOT NULL;";
        var results = await GetConnection().QueryAsync<(string DestPath, long FileSize)>(sql);
        return results.ToList();
    }

    /// <summary>
    /// Gibt kopierte Dateien mit Größe für eine bestimmte Kamera zurück (für Post-Import Verification).
    /// </summary>
    public async Task<List<(string DestPath, long FileSize)>> GetCopiedFilesForVerify(string cameraId)
    {
        var sql = "SELECT dest_path, file_size FROM imports WHERE copy_status = 2 AND dest_path IS NOT NULL AND camera_id = @CameraId;";
        var results = await GetConnection().QueryAsync<(string DestPath, long FileSize)>(sql, new { CameraId = cameraId });
        return results.ToList();
    }

    /// <summary>
    /// Gibt alle Videos einer Kamera zurück (für EIS-Detection).
    /// </summary>
    public async Task<List<ImportedFile>> GetVideosByCamera(string cameraId)
    {
        var sql = "SELECT * FROM imports WHERE camera_id = @CameraId AND is_video = 1;";
        var results = await GetConnection().QueryAsync<ImportedFile>(sql, new { CameraId = cameraId });
        return results.ToList();
    }

    /// <summary>
    /// Gibt Zusammenfassung für Dry-Run Simulation zurück (gruppiert nach Tag + MediaType).
    /// </summary>
    public async Task<List<SimulationSummary>> GetSimulationSummaryAsync(string cameraId)
    {
        var sql = @"
            SELECT capture_date AS CaptureDate,
                   media_type AS MediaType,
                   COUNT(*) AS Count,
                   SUM(file_size) AS TotalSize
            FROM imports
            WHERE camera_id = @CameraId
            GROUP BY capture_date, media_type
            ORDER BY capture_date, media_type;
        ";

        var results = await GetConnection().QueryAsync<SimulationSummary>(sql, new { CameraId = cameraId });
        return results.ToList();
    }

    /// <summary>
    /// Gibt alle Sequenzen für einen bestimmten Tag zurück.
    /// </summary>
    public async Task<List<SequenceInfo>> GetSequencesForDayAsync(string cameraId, string captureDate)
    {
        var sql = @"
            SELECT s.folder_name AS FolderName,
                   s.photo_count AS PhotoCount,
                   s.mode AS Mode
            FROM sequences s
            WHERE s.camera_id = @CameraId
              AND s.capture_date = @CaptureDate
            ORDER BY s.folder_name;
        ";

        var results = await GetConnection().QueryAsync<SequenceInfo>(sql, new { CameraId = cameraId, CaptureDate = captureDate });
        return results.ToList();
    }

    /// <summary>
    /// Gibt Dateiname und Fehlermeldung aller fehlgeschlagenen Importe zurück.
    /// </summary>
    public async Task<List<(string Filename, string ErrorMessage)>> GetFailedFilesAsync()
    {
        var sql = "SELECT filename, error_message FROM imports WHERE copy_status = 3 AND error_message IS NOT NULL;";
        var results = await GetConnection().QueryAsync<(string, string)>(sql);
        return results.ToList();
    }

    /// <summary>
    /// Löscht alle Daten (für neue Import-Session).
    /// </summary>
    public async Task ClearDatabaseAsync()
    {
        await GetConnection().ExecuteAsync("DELETE FROM imports;");
        await GetConnection().ExecuteAsync("DELETE FROM sequences;");
        await GetConnection().ExecuteAsync("VACUUM;");

        _logger?.LogDebug("Datenbank geleert (bereit für neuen Import)");
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    /// <summary>
    /// Gibt die Datenbankverbindung asynchron frei.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}
