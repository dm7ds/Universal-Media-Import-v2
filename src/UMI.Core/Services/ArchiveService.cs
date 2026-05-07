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
using UMI.Core.Models;

namespace UMI.Core.Services;

/// <summary>
/// Service für Archivierung von Workbench zu Projekt-Struktur.
/// </summary>
public class ArchiveService
{
    private readonly UmiConfig _config;
    private readonly ILogger<ArchiveService>? _logger;

    public ArchiveService(
        UmiConfig config,
        ILogger<ArchiveService>? logger = null)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Findet Datum-Ordner im Workbench (yyyy-MM-dd Pattern).
    /// </summary>
    public Task<List<DirectoryInfo>> DiscoverDateFoldersAsync(string workbenchPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(workbenchPath))
            return Task.FromResult(new List<DirectoryInfo>());

        var dateFolders = Directory.GetDirectories(workbenchPath)
            .Select(d => new DirectoryInfo(d))
            .Where(d => d.Name.Length == 10 && d.Name.Contains('-'))
            .OrderBy(d => d.Name)
            .ToList();

        _logger?.LogDebug("Datum-Ordner gefunden: {Count}", dateFolders.Count);
        return Task.FromResult(dateFolders);
    }

    /// <summary>
    /// Validiert Archivierungs-Ziel (Projekt existiert nicht, Datum-Ordner vorhanden).
    /// </summary>
    public async Task<ArchiveValidation> ValidateArchiveTargetAsync(
        string projectName,
        CancellationToken ct = default)
    {
        var projectPath = Path.Combine(_config.GlobalPaths.Projects, projectName);
        var validation = new ArchiveValidation
        {
            ProjectPath = projectPath
        };

        if (Directory.Exists(projectPath))
        {
            validation.IsValid = false;
            validation.ErrorMessage = $"Projekt existiert bereits: {projectPath}";
            return validation;
        }

        validation.DateFolders = await DiscoverDateFoldersAsync(_config.GlobalPaths.Workbench, ct);

        if (validation.DateFolders.Count == 0)
        {
            validation.IsValid = false;
            validation.ErrorMessage = "Keine Datum-Ordner im Workbench gefunden";
            return validation;
        }

        validation.IsValid = true;
        return validation;
    }

    /// <summary>
    /// Erstellt Archiv (Projektstruktur + kopiert RAW-Dateien).
    /// </summary>
    public async Task<ArchiveResult> CreateArchiveAsync(
        ArchiveOptions options,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken ct = default)
    {
        var projectPath = Path.Combine(_config.GlobalPaths.Projects, options.ProjectName);
        var structure = _config.Archiving.ProjectStructure;

        var result = new ArchiveResult
        {
            DryRun = options.DryRun
        };

        ct.ThrowIfCancellationRequested();

        var folders = new List<string>
        {
            structure.Raw,
            structure.Edit,
            structure.Project
        };

        if (options.IncludeDelivery)
        {
            folders.Add(structure.Delivery);
        }

        foreach (var folder in folders)
        {
            var path = Path.Combine(projectPath, folder);

            if (!options.DryRun)
            {
                Directory.CreateDirectory(path);
            }

            result.CreatedFolders.Add(folder);
            _logger?.LogDebug("Erstellt: {Folder}/", folder);
        }

        if (_config.Archiving.CreateReadme && !options.DryRun)
        {
            var readmePath = Path.Combine(projectPath, "README.md");
            var readmeContent = $"""
                # {options.ProjectName}

                Erstellt: {DateTime.Now:yyyy-MM-dd HH:mm}

                ## Struktur

                - **{structure.Raw}/**: Rohdaten vom Workbench
                - **{structure.Edit}/**: DaVinci Resolve Projekte
                - **{structure.Project}/**: Projekt-Dateien (Notizen, Scripts, etc.)
                {(options.IncludeDelivery ? $"- **{structure.Delivery}/**: Finale Exporte" : "")}
                """;

            await File.WriteAllTextAsync(readmePath, readmeContent, ct);
            _logger?.LogDebug("README.md erstellt");
        }

        var dateFolders = await DiscoverDateFoldersAsync(_config.GlobalPaths.Workbench, ct);
        var total = dateFolders.Count;

        for (int i = 0; i < dateFolders.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var dateFolder = dateFolders[i];
            var destPath = Path.Combine(projectPath, structure.Raw, dateFolder.Name);

            progress?.Report(new ArchiveProgress
            {
                CurrentFolder = dateFolder.Name,
                Current = i + 1,
                Total = total
            });

            if (!options.DryRun)
            {
                var copyResult = await CopyDirectoryAsync(dateFolder.FullName, destPath, ct);
                result.FilesCopied += copyResult.FilesCopied;
                result.BytesCopied += copyResult.BytesCopied;
            }

            result.FoldersCopied++;
            _logger?.LogDebug("Kopiert: {Folder}", dateFolder.Name);
        }

        return result;
    }

    /// <summary>
    /// Kopiert Verzeichnis rekursiv (async mit CancellationToken).
    /// </summary>
    private async Task<(int FilesCopied, long BytesCopied)> CopyDirectoryAsync(
        string source,
        string destination,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(destination);

        int filesCopied = 0;
        long bytesCopied = 0;

        foreach (var file in Directory.GetFiles(source))
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destination, fileName);

            using var sourceStream = File.OpenRead(file);
            using var destStream = File.Create(destFile);
            await sourceStream.CopyToAsync(destStream, ct);

            filesCopied++;
            bytesCopied += sourceStream.Length;
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            ct.ThrowIfCancellationRequested();

            var dirName = Path.GetFileName(dir);
            var subResult = await CopyDirectoryAsync(dir, Path.Combine(destination, dirName), ct);

            filesCopied += subResult.FilesCopied;
            bytesCopied += subResult.BytesCopied;
        }

        return (filesCopied, bytesCopied);
    }
}
