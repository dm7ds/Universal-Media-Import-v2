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

using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using UMI.Data.Models;

namespace UMI.Data;

/// <summary>
/// SQLite GPU Task Queue Datenbank.
/// Speichert GPU-Tasks (Gyroflow, etc.) mit Status, Priorität und Fortschritt.
/// </summary>
public class GpuTaskDatabase : IDisposable
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly ILogger<GpuTaskDatabase>? _logger;
    private SqliteConnection? _connection;

    public GpuTaskDatabase(string databasePath, ILogger<GpuTaskDatabase>? logger = null)
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

        _logger?.LogDebug("SQLite GPU Task Queue Datenbank initialisiert: {Path}", GetDatabasePath());
    }

    /// <summary>
    /// Gibt den Pfad der Datenbank zurück.
    /// </summary>
    public string GetDatabasePath() => _databasePath;

    private async Task CreateSchemaAsync()
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS gpu_tasks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_type TEXT NOT NULL,
                status INTEGER NOT NULL DEFAULT 0,
                priority INTEGER NOT NULL DEFAULT 0,
                input_path TEXT NOT NULL,
                output_path TEXT NOT NULL,
                payload_json TEXT,
                file_size INTEGER NOT NULL DEFAULT 0,
                progress_percent REAL NOT NULL DEFAULT 0,
                error_message TEXT,
                retry_count INTEGER NOT NULL DEFAULT 0,
                max_retries INTEGER NOT NULL DEFAULT 3,
                batch_id TEXT,
                created_at TEXT NOT NULL,
                started_at TEXT,
                completed_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_gpu_tasks_status_priority ON gpu_tasks(status, priority DESC, created_at ASC);
            CREATE INDEX IF NOT EXISTS idx_gpu_tasks_batch ON gpu_tasks(batch_id);
        ";

        await GetConnection().ExecuteAsync(sql);
    }

    /// <summary>
    /// Fügt einen neuen GPU-Task ein und gibt die generierte ID zurück.
    /// </summary>
    public async Task<long> InsertTaskAsync(GpuTask task)
    {
        var sql = @"
            INSERT INTO gpu_tasks (
                task_type, status, priority, input_path, output_path,
                payload_json, file_size, progress_percent, error_message,
                retry_count, max_retries, batch_id, created_at, started_at, completed_at
            ) VALUES (
                @TaskType, @Status, @Priority, @InputPath, @OutputPath,
                @PayloadJson, @FileSize, @ProgressPercent, @ErrorMessage,
                @RetryCount, @MaxRetries, @BatchId, @CreatedAt, @StartedAt, @CompletedAt
            );
            SELECT last_insert_rowid();
        ";

        return await GetConnection().ExecuteScalarAsync<long>(sql, task);
    }

    /// <summary>
    /// Fügt mehrere GPU-Tasks in einer Transaktion ein (Performance-Optimierung).
    /// </summary>
    public async Task InsertBatchAsync(IEnumerable<GpuTask> tasks)
    {
        var sql = @"
            INSERT INTO gpu_tasks (
                task_type, status, priority, input_path, output_path,
                payload_json, file_size, progress_percent, error_message,
                retry_count, max_retries, batch_id, created_at, started_at, completed_at
            ) VALUES (
                @TaskType, @Status, @Priority, @InputPath, @OutputPath,
                @PayloadJson, @FileSize, @ProgressPercent, @ErrorMessage,
                @RetryCount, @MaxRetries, @BatchId, @CreatedAt, @StartedAt, @CompletedAt
            );
        ";

        using var transaction = GetConnection().BeginTransaction();
        try
        {
            await GetConnection().ExecuteAsync(sql, tasks, transaction);
            await transaction.CommitAsync();
            _logger?.LogDebug("Batch-Insert: {Count} GPU-Tasks eingefügt", tasks.Count());
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Holt einen GPU-Task anhand seiner ID.
    /// </summary>
    public async Task<GpuTask?> GetByIdAsync(long id)
    {
        var sql = "SELECT * FROM gpu_tasks WHERE id = @Id;";
        return await GetConnection().QuerySingleOrDefaultAsync<GpuTask>(sql, new { Id = id });
    }

    /// <summary>
    /// Holt den nächsten ausstehenden Task.
    /// Batch-Gruppierung: alle Tasks eines Batches werden zusammen verarbeitet
    /// bevor der nächste Batch startet (Kamera für Kamera, nicht gemischt).
    /// Innerhalb gleicher Priority: Batch mit dem ältesten Task zuerst.
    /// </summary>
    public async Task<GpuTask?> GetNextPendingAsync()
    {
        var sql = @"
            SELECT t.* FROM gpu_tasks t
            JOIN (
                SELECT batch_id, MIN(created_at) AS batch_start
                FROM gpu_tasks WHERE status = 0
                GROUP BY batch_id
            ) b ON t.batch_id = b.batch_id
            WHERE t.status = 0
            ORDER BY t.priority DESC, b.batch_start ASC, t.created_at ASC
            LIMIT 1;
        ";

        return await GetConnection().QuerySingleOrDefaultAsync<GpuTask>(sql);
    }

    /// <summary>
    /// Gibt alle Tasks eines Batches zurück.
    /// </summary>
    public async Task<List<GpuTask>> GetByBatchAsync(string batchId)
    {
        var sql = "SELECT * FROM gpu_tasks WHERE batch_id = @BatchId;";
        var results = await GetConnection().QueryAsync<GpuTask>(sql, new { BatchId = batchId });
        return results.ToList();
    }

    /// <summary>
    /// Gibt die IDs aller ausstehenden Tasks zurück (nach Priorität und Erstellungszeit sortiert).
    /// </summary>
    public async Task<List<long>> GetPendingIdsAsync()
    {
        var sql = @"
            SELECT id FROM gpu_tasks
            WHERE status = 0
            ORDER BY priority DESC, created_at ASC;
        ";

        var results = await GetConnection().QueryAsync<long>(sql);
        return results.ToList();
    }

    /// <summary>
    /// Markiert einen Task als "wird gerade verarbeitet".
    /// </summary>
    public async Task MarkInProgressAsync(long id)
    {
        var sql = "UPDATE gpu_tasks SET status = 1, started_at = @StartedAt WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = id, StartedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>
    /// Markiert einen Task als erfolgreich abgeschlossen.
    /// </summary>
    public async Task MarkCompletedAsync(long id)
    {
        var sql = "UPDATE gpu_tasks SET status = 2, completed_at = @CompletedAt, progress_percent = 100 WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = id, CompletedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>
    /// Markiert einen Task als fehlgeschlagen.
    /// </summary>
    public async Task MarkFailedAsync(long id, string error)
    {
        var sql = "UPDATE gpu_tasks SET status = 3, error_message = @Error, completed_at = @CompletedAt WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = id, Error = error, CompletedAt = DateTime.UtcNow.ToString("o") });

        _logger?.LogWarning("GPU Task {Id} fehlgeschlagen: {Error}", id, error);
    }

    /// <summary>
    /// Markiert einen Task als abgebrochen.
    /// </summary>
    public async Task MarkCancelledAsync(long id)
    {
        var sql = "UPDATE gpu_tasks SET status = 4, completed_at = @CompletedAt WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = id, CompletedAt = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>
    /// Setzt alle Pending-Tasks auf Cancelled (Queue leeren).
    /// </summary>
    public async Task<int> CancelAllPendingAsync()
    {
        return await GetConnection().ExecuteAsync(
            "UPDATE gpu_tasks SET status = @Cancelled WHERE status = @Pending",
            new { Cancelled = (int)GpuTaskStatus.Cancelled, Pending = (int)GpuTaskStatus.Pending });
    }

    /// <summary>
    /// Setzt alle InProgress-Tasks auf Pending zurück (für Neustart nach Absturz).
    /// </summary>
    public async Task ResetStaleInProgressAsync()
    {
        var sql = "UPDATE gpu_tasks SET status = 0 WHERE status = 1;";
        var affected = await GetConnection().ExecuteAsync(sql);

        if (affected > 0)
            _logger?.LogWarning("ResetStaleInProgress: {Count} veraltete InProgress-Tasks zurückgesetzt", affected);
    }

    /// <summary>
    /// Aktualisiert den Fortschritt eines Tasks.
    /// </summary>
    public async Task UpdateProgressAsync(long id, double percent)
    {
        var sql = "UPDATE gpu_tasks SET progress_percent = @Percent WHERE id = @Id;";
        await GetConnection().ExecuteAsync(sql, new { Id = id, Percent = percent });
    }

    /// <summary>
    /// Stellt einen fehlgeschlagenen Task in die Queue zurück (erhöht retry_count).
    /// </summary>
    public async Task RequeueFailedAsync(long id)
    {
        var sql = @"
            UPDATE gpu_tasks
            SET status = 0,
                retry_count = retry_count + 1,
                error_message = NULL,
                started_at = NULL,
                completed_at = NULL
            WHERE id = @Id;
        ";

        await GetConnection().ExecuteAsync(sql, new { Id = id });
    }

    /// <summary>
    /// Berechnet GPU Queue Statistiken via SQL-Aggregation.
    /// </summary>
    public async Task<GpuQueueStats> GetStatsAsync()
    {
        var sql = @"
            SELECT
                COALESCE(SUM(CASE WHEN status = 0 THEN 1 ELSE 0 END), 0) AS Pending,
                COALESCE(SUM(CASE WHEN status = 1 THEN 1 ELSE 0 END), 0) AS InProgress,
                COALESCE(SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END), 0) AS Completed,
                COALESCE(SUM(CASE WHEN status = 3 THEN 1 ELSE 0 END), 0) AS Failed,
                COALESCE(SUM(CASE WHEN status = 4 THEN 1 ELSE 0 END), 0) AS Cancelled
            FROM gpu_tasks;
        ";

        return await GetConnection().QuerySingleAsync<GpuQueueStats>(sql);
    }

    /// <summary>
    /// Gibt die IDs aller fehlgeschlagenen Tasks zurück (nach Erstellungszeit sortiert).
    /// </summary>
    public async Task<List<long>> GetFailedTaskIdsAsync()
    {
        var sql = "SELECT id FROM gpu_tasks WHERE status = 3 ORDER BY created_at ASC;";
        var results = await GetConnection().QueryAsync<long>(sql);
        return results.ToList();
    }

    /// <summary>
    /// Löscht abgeschlossene Tasks die älter als das angegebene Datum sind.
    /// </summary>
    public async Task PurgeCompletedAsync(DateTime olderThan)
    {
        var sql = "DELETE FROM gpu_tasks WHERE status = 2 AND completed_at < @OlderThan;";
        var affected = await GetConnection().ExecuteAsync(sql, new { OlderThan = olderThan.ToString("o") });

        _logger?.LogDebug("PurgeCompleted: {Count} abgeschlossene Tasks gelöscht (älter als {Date})", affected, olderThan);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
