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
using UMI.Core.Constants;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Pre-Processor für GPX-Aufbau nach dem Copy.
/// Baut optimierte GPX-Dateien aus importierten Videos (DestPath).
/// GPS-Injection ins Video selbst erfolgt NICHT hier – das passiert beim Restore.
/// Order: 20 (nach SRT-Konvertierung).
/// </summary>
public class GpsInjectionPreProcessor : IPreProcessor
{
    private readonly GpsService _gpsService;
    private readonly IProcessHistoryService _processHistory;
    private readonly ILogger<GpsInjectionPreProcessor>? _logger;

    public string Name => "GPX Building";
    public int Order => 20;

    public GpsInjectionPreProcessor(
        GpsService gpsService,
        IProcessHistoryService processHistory,
        ILogger<GpsInjectionPreProcessor>? logger = null)
    {
        _gpsService = gpsService;
        _processHistory = processHistory;
        _logger = logger;
    }

    public bool IsEnabledForCamera(CameraConfig config, ImportContext context)
        => config.Features.GpsInjection;

    public async Task<PreProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct = default)
    {
        var videos = files
            .Where(f => f.IsVideo)
            .Where(f => !Path.GetFileNameWithoutExtension(f.FileName)
                .Contains(FolderNameConstants.Stabilized, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (videos.Count == 0)
        {
            _logger?.LogInformation("Keine Videos für GPX-Building gefunden");
            return PreProcessingResult.Empty(Name);
        }

        _logger?.LogInformation("GPX-Building für {Count} Videos", videos.Count);

        var gpxPath = context.Config.Paths.CustomGpxPath
                      ?? context.GlobalSettings.Paths.GpxSource;

        if (!Directory.Exists(gpxPath))
        {
            _logger?.LogWarning("GPX-Verzeichnis existiert nicht: {Path}", gpxPath);
            return PreProcessingResult.Empty(Name);
        }

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
                    _logger?.LogInformation("[DRY-RUN] GPX-Building: {File}", file.FileName);
                    processed++;
                    continue;
                }

                if (!File.Exists(file.DestPath))
                {
                    _logger?.LogWarning("Datei nicht gefunden (noch nicht kopiert?): {File}", file.DestPath);
                    skipped++;
                    continue;
                }

                var gpxFile = await _gpsService.OptimizeGpsForVideoAsync(file.DestPath, gpxPath);

                if (gpxFile is not null)
                {
                    await _processHistory.WriteEntryAsync(file.DestPath, ProcessSteps.GpxBuilt,
                        new Dictionary<string, string> { ["gpx"] = gpxFile }, ct);

                    _logger?.LogInformation("GPX gebaut: {File}", Path.GetFileName(gpxFile));
                    processed++;
                }
                else
                {
                    _logger?.LogWarning("Keine GPS-Daten gefunden: {File}", file.FileName);
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim GPX-Building: {File}", file.FileName);
                failed++;
                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        return new PreProcessingResult(Name, processed, skipped, failed, errors);
    }
}
