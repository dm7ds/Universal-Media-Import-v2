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

namespace UMI.Core.Services;

/// <summary>
/// Pre-Processor für automatisches Metadata-Backup nach dem Copy.
/// Liest Metadaten aus importierten Videos (DestPath) und sichert sie via ExifTool.
/// Order: 10 (läuft zuerst, da keine Abhängigkeiten).
/// </summary>
public class MetadataBackupPreProcessor : IPreProcessor
{
    private readonly MetadataService _metadataService;
    private readonly ILogger<MetadataBackupPreProcessor>? _logger;

    public string Name => "Metadata Backup";
    public int Order => 10;

    public MetadataBackupPreProcessor(
        MetadataService metadataService,
        ILogger<MetadataBackupPreProcessor>? logger = null)
    {
        _metadataService = metadataService;
        _logger = logger;
    }

    public bool IsEnabledForCamera(CameraConfig config, ImportContext context)
        => config.Features.MetadataBackup;

    public async Task<PreProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct = default)
    {
        var videos = files.Where(f => f.IsVideo).ToList();

        if (videos.Count == 0)
            return PreProcessingResult.Empty(Name);

        _logger?.LogInformation("Metadata-Backup für {Count} Videos", videos.Count);

        var processed = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var file in videos)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (context.DryRun)
                {
                    _logger?.LogInformation("[DRY-RUN] Metadata-Backup: {File}", file.FileName);
                    skipped++;
                    continue;
                }

                if (!File.Exists(file.DestPath))
                {
                    _logger?.LogWarning("Datei nicht gefunden (noch nicht kopiert?): {File}", file.DestPath);
                    skipped++;
                    continue;
                }

                var success = await _metadataService.CreateBackupAsync(file.DestPath, context.CameraId);

                if (success)
                {
                    _logger?.LogDebug("Metadata gesichert: {File}", file.FileName);
                    processed++;
                }
                else
                {
                    _logger?.LogWarning("Metadata-Backup fehlgeschlagen: {File}", file.FileName);
                    failed++;
                    errors.Add($"Backup fehlgeschlagen: {file.FileName}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler bei Metadata-Backup: {File}", file.FileName);
                failed++;
                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        return new PreProcessingResult(Name, processed, skipped, failed, errors);
    }
}
