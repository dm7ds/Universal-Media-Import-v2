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

using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UMI.Core.Configuration;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Erstellt ein Support-Paket (ZIP) mit Logs, config.json und einer bundle-info.txt.
/// </summary>
public interface ISupportBundleService
{
    /// <summary>
    /// Erzeugt das Support-Bundle im angegebenen Zielverzeichnis.
    /// </summary>
    /// <param name="targetDirectory">Verzeichnis, in dem die ZIP abgelegt wird.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <returns>Vollständiger Pfad zur erzeugten ZIP-Datei.</returns>
    Task<string> CreateBundleAsync(string targetDirectory, CancellationToken ct = default);
}

/// <summary>
/// Implementierung von <see cref="ISupportBundleService"/>.
/// Robustheit: einzelne fehlende Files stoppen das Bundle nicht — sie werden
/// als Hinweis in bundle-info.txt vermerkt.
/// </summary>
public sealed class SupportBundleService : ISupportBundleService
{
    private const int MaxLogFiles = 10;

    private readonly ConfigPathResolver _paths;
    private readonly UmiConfig? _config;
    private readonly ILogger<SupportBundleService>? _logger;

    public SupportBundleService(
        ConfigPathResolver paths,
        UmiConfig? config = null,
        ILogger<SupportBundleService>? logger = null)
    {
        _paths = paths;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> CreateBundleAsync(string targetDirectory, CancellationToken ct = default)
        => Task.Run(() => CreateBundle(targetDirectory, ct), ct);

    private string CreateBundle(string targetDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(targetDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(targetDirectory, $"umi-support-{timestamp}.zip");

        var missing = new List<string>();

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        // --- Logs (max. MaxLogFiles neueste Dateien) ---
        var logDir = ConfigPathResolver.LogDirectory;
        if (Directory.Exists(logDir))
        {
            var logFiles = Directory.EnumerateFiles(logDir, "*.log", SearchOption.TopDirectoryOnly)
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .Take(MaxLogFiles)
                .ToList();

            if (logFiles.Count == 0)
                missing.Add($"Keine *.log-Dateien gefunden in: {logDir}");

            foreach (var logFile in logFiles)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    archive.CreateEntryFromFile(logFile.FullName, $"logs/{logFile.Name}",
                        CompressionLevel.Optimal);
                }
                catch (Exception ex)
                {
                    missing.Add($"Log-Datei konnte nicht hinzugefügt werden: {logFile.Name} — {ex.Message}");
                    _logger?.LogWarning(ex, "Support bundle: could not add log file {File}", logFile.FullName);
                }
            }
        }
        else
        {
            missing.Add($"Log-Verzeichnis nicht gefunden: {logDir}");
        }

        // --- config.json (KEIN .bak) ---
        var configFile = _paths.ConfigFile;
        if (File.Exists(configFile))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                archive.CreateEntryFromFile(configFile, "config.json", CompressionLevel.Optimal);
            }
            catch (Exception ex)
            {
                missing.Add($"config.json konnte nicht hinzugefügt werden: {ex.Message}");
                _logger?.LogWarning(ex, "Support bundle: could not add config.json");
            }
        }
        else
        {
            missing.Add($"config.json nicht gefunden: {configFile}");
        }

        // --- bundle-info.txt ---
        ct.ThrowIfCancellationRequested();
        var info = BuildBundleInfo(timestamp, missing);
        var infoEntry = archive.CreateEntry("bundle-info.txt", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(infoEntry.Open()))
            writer.Write(info);

        _logger?.LogInformation("Support bundle created: {Path}", zipPath);
        return zipPath;
    }

    private string BuildBundleInfo(string timestamp, IReadOnlyList<string> missing)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== UMI Support Bundle ===");
        sb.AppendLine($"Erstellt:        {timestamp}");
        sb.AppendLine($"Version:         {BuildInfo.Banner}");
        sb.AppendLine($"OS:              {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Framework:       {RuntimeInformation.FrameworkDescription}");

        var logLevel = _config?.Logging?.Level ?? "(unbekannt)";
        sb.AppendLine($"Log-Level:       {logLevel}");

        var workbench = _config?.GlobalPaths?.Workbench ?? "(nicht konfiguriert)";
        sb.AppendLine($"Workbench:       {workbench}");

        var cameraCount = _config?.Cameras?.Count ?? 0;
        sb.AppendLine($"Kameras:         {cameraCount}");

        sb.AppendLine($"Log-Verzeichnis: {ConfigPathResolver.LogDirectory}");
        sb.AppendLine($"Config-Datei:    {_paths.ConfigFile}");

        if (missing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== Fehlende / übersprungene Dateien ===");
            foreach (var m in missing)
                sb.AppendLine($"  - {m}");
        }

        return sb.ToString();
    }
}
