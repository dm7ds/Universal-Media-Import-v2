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

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Pre-Processor für SRT → GPX Konvertierung.
/// Scannt die SD-Karte nach SRT-Sidecars und konvertiert sie zu GPX-Dateien im GPS-Ordner.
/// Order: 15 (nach MetadataBackup, vor GPS-Injection).
/// </summary>
public class SrtConversionPreProcessor : IPreProcessor
{
    private static readonly Regex DjiDatePattern =
        new(@"DJI_(\d{4})(\d{2})(\d{2})", RegexOptions.Compiled);

    private readonly SrtConverter _srtConverter;
    private readonly ILogger<SrtConversionPreProcessor>? _logger;

    public string Name => "SRT → GPX Conversion";
    public int Order => 15;

    public SrtConversionPreProcessor(
        SrtConverter srtConverter,
        ILogger<SrtConversionPreProcessor>? logger = null)
    {
        _srtConverter = srtConverter;
        _logger = logger;
    }

    public bool IsEnabledForCamera(CameraConfig config, ImportContext context)
    {
        if (!config.CustomSettings.TryGetValue("import_srt_sidecars", out var value))
            return false;

        return value switch
        {
            bool b => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            _ => value?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        };
    }

    public async Task<PreProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct = default)
    {

        var srtFiles = await Task.Run(() =>
        {
            if (!Directory.Exists(context.SourcePath))
                return Array.Empty<FileInfo>();

            return new DirectoryInfo(context.SourcePath)
                .GetFiles("*.srt", SearchOption.AllDirectories);
        }, ct);

        if (srtFiles.Length == 0)
        {
            _logger?.LogDebug("Keine SRT-Dateien auf SD-Karte gefunden: {Path}", context.SourcePath);
            return PreProcessingResult.Empty(Name);
        }

        _logger?.LogInformation("SRT → GPX Konvertierung für {Count} Dateien", srtFiles.Length);

        var processed = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var srtFile in srtFiles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var fileDate = ExtractDate(srtFile);
                var gpsDir = Path.Combine(
                    context.WorkbenchPath,
                    fileDate.ToString(DateFormatConstants.FolderFormat),
                    context.CameraId,
                    FolderNameConstants.Gps);

                var gpxPath = Path.Combine(
                    gpsDir,
                    Path.GetFileNameWithoutExtension(srtFile.Name) + ".gpx");

                if (context.DryRun)
                {
                    _logger?.LogInformation("[DRY-RUN] SRT → GPX: {File} → {Output}",
                        srtFile.Name, Path.GetFileName(gpxPath));
                    processed++;
                    continue;
                }

                var result = await _srtConverter.ConvertSrtToGpxAsync(srtFile.FullName, gpxPath);

                if (result is not null)
                {
                    _logger?.LogInformation("SRT → GPX: {File} → {Output}",
                        srtFile.Name, Path.GetFileName(gpxPath));
                    processed++;
                }
                else
                {
                    _logger?.LogWarning("SRT-Konvertierung ergab kein Ergebnis: {File}", srtFile.Name);
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler bei SRT-Konvertierung: {File}", srtFile.Name);
                failed++;
                errors.Add($"{srtFile.Name}: {ex.Message}");
            }
        }

        _logger?.LogInformation("SRT-Konvertierung abgeschlossen: {Count} konvertiert", processed);
        return new PreProcessingResult(Name, processed, skipped, failed, errors);
    }

    private DateTime ExtractDate(FileInfo file)
    {
        var match = DjiDatePattern.Match(Path.GetFileNameWithoutExtension(file.Name));

        if (match.Success)
        {
            try
            {
                return new DateTime(
                    int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
            }
            catch (Exception ex) { _logger?.LogDebug(ex, "Failed to parse date from SRT filename: {File}", file.Name); }
        }

        return new[] { file.LastWriteTime, file.CreationTime }.Min();
    }
}
