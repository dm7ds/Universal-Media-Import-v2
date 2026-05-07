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

using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;

namespace UMI.Core.Services;

/// <summary>
/// Leichtgewichtiges File-Tracking für Fixed-Path-Quellen.
/// Pro Kamera eine Textdatei (relative_path|file_size) in config/.import_history/.
/// O(1) Lookups via HashSet, Append-Only Schreibzugriff.
/// </summary>
public interface IImportHistoryService
{
    /// <summary>
    /// Lädt die History für eine Kamera als HashSet für O(1) Lookups.
    /// Gibt ein leeres Set zurück wenn keine History existiert.
    /// </summary>
    HashSet<string> LoadHistory(string cameraId);

    /// <summary>
    /// Prüft ob eine Datei bereits importiert wurde.
    /// Key = "relative_path|file_size_bytes" (case-insensitive).
    /// </summary>
    bool IsImported(HashSet<string> history, string relativePath, long fileSize);

    /// <summary>
    /// Fügt importierte Dateien zur History hinzu (Append, kein Rewrite der Datei).
    /// </summary>
    Task AppendToHistoryAsync(string cameraId, IEnumerable<(string RelativePath, long FileSize)> files,
        CancellationToken ct = default);

    /// <summary>
    /// Löscht die History für eine Kamera (für --reset-history).
    /// </summary>
    void ClearHistory(string cameraId);

    /// <summary>
    /// Prüft ob die Workbench Dateien für diese Kamera enthält.
    /// Sucht nach {workbenchPath}/*/{folderName}/ (z.B. 2024-03-15/GoPro11/).
    /// Wenn kein solcher Ordner existiert und die History nicht leer ist, wird die History
    /// automatisch gecleared (Workbench wurde gelöscht → History ist invalid).
    /// Returns: true wenn die History gecleared wurde, false sonst.
    /// </summary>
    bool ClearHistoryIfWorkbenchEmpty(string cameraId, string workbenchPath, string folderName);

    /// <summary>
    /// Gleicht die History gegen den tatsächlichen Workbench-Zustand ab.
    /// Entfernt History-Einträge für Dateien die im Workbench nicht mehr existieren.
    /// Scannt alle Dateien unter {workbenchPath}/*/{folderName}/**/* (case-insensitive Dateiname-Matching).
    /// Returns: Anzahl entfernter Einträge.
    /// </summary>
    int ReconcileHistory(string cameraId, string workbenchPath, string folderName);
}

/// <summary>
/// Implementierung: Textdatei pro Kamera unter config/.import_history/{cameraId}.txt.
/// Format: "relative_path|file_size_bytes" pro Zeile.
/// </summary>
public class ImportHistoryService : IImportHistoryService
{
    private readonly ConfigPathResolver _pathResolver;
    private readonly ILogger<ImportHistoryService>? _logger;

    public ImportHistoryService(
        ConfigPathResolver pathResolver,
        ILogger<ImportHistoryService>? logger = null)
    {
        _pathResolver = pathResolver;
        _logger = logger;
    }

    private string GetHistoryPath(string cameraId)
        => Path.Combine(_pathResolver.Root, ".import_history", $"{cameraId}.txt");

    public HashSet<string> LoadHistory(string cameraId)
    {
        var path = GetHistoryPath(cameraId);
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return File.ReadAllLines(path)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Laden der Import-History für {Camera}", cameraId);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool IsImported(HashSet<string> history, string relativePath, long fileSize)
    {
        var key = $"{relativePath}|{fileSize}";
        return history.Contains(key);
    }

    public async Task AppendToHistoryAsync(
        string cameraId,
        IEnumerable<(string RelativePath, long FileSize)> files,
        CancellationToken ct = default)
    {
        var path = GetHistoryPath(cameraId);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var lines = files.Select(f => $"{f.RelativePath}|{f.FileSize}");
            await File.AppendAllLinesAsync(path, lines, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Schreiben der Import-History für {Camera}", cameraId);
        }
    }

    public void ClearHistory(string cameraId)
    {
        var path = GetHistoryPath(cameraId);
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
            _logger?.LogInformation("Import-History für {Camera} gelöscht", cameraId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Löschen der Import-History für {Camera}", cameraId);
        }
    }

    public bool ClearHistoryIfWorkbenchEmpty(string cameraId, string workbenchPath, string folderName)
    {
        var history = LoadHistory(cameraId);
        if (history.Count == 0)
            return false;

        if (!Directory.Exists(workbenchPath))
        {
            ClearHistory(cameraId);
            _logger?.LogInformation(
                "Workbench-Ordner nicht gefunden — History für {Camera} zurückgesetzt", cameraId);
            return true;
        }

        try
        {
            var hasCameraFiles = Directory.EnumerateDirectories(workbenchPath)
                .Any(dateDir => Directory.Exists(Path.Combine(dateDir, folderName)));

            if (!hasCameraFiles)
            {
                ClearHistory(cameraId);
                _logger?.LogInformation(
                    "Workbench-Ordner '{Folder}' nicht gefunden — History für {Camera} zurückgesetzt",
                    folderName, cameraId);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Workbench-Check fehlgeschlagen für {Camera}", cameraId);
        }

        return false;
    }

    public int ReconcileHistory(string cameraId, string workbenchPath, string folderName)
    {
        var history = LoadHistory(cameraId);
        if (history.Count == 0)
            return 0;

        var existingFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(workbenchPath))
            {
                foreach (var dateDir in Directory.EnumerateDirectories(workbenchPath))
                {
                    var cameraDir = Path.Combine(dateDir, folderName);
                    if (!Directory.Exists(cameraDir))
                        continue;

                    foreach (var file in Directory.EnumerateFiles(cameraDir, "*", SearchOption.AllDirectories))
                        existingFileNames.Add(Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[{Camera}] Workbench-Scan für History-Reconcile fehlgeschlagen", cameraId);
            return 0;
        }

        var kept = new List<string>(history.Count);
        var removedCount = 0;

        foreach (var entry in history)
        {
            var pipeIndex = entry.IndexOf('|');
            var relativePath = pipeIndex >= 0 ? entry[..pipeIndex] : entry;
            var fileName = Path.GetFileName(relativePath);

            if (!string.IsNullOrEmpty(fileName) && existingFileNames.Contains(fileName))
            {
                kept.Add(entry);
            }
            else
            {
                removedCount++;
                _logger?.LogDebug("[{Camera}] History-Eintrag entfernt (nicht in Workbench): {Entry}",
                    cameraId, entry);
            }
        }

        if (removedCount == 0)
            return 0;

        var path = GetHistoryPath(cameraId);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, kept);
            _logger?.LogInformation(
                "[{Camera}] History reconciled: {Removed} veraltete Einträge entfernt, {Kept} behalten",
                cameraId, removedCount, kept.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[{Camera}] Fehler beim Zurückschreiben der bereinigten History", cameraId);
        }

        return removedCount;
    }
}
