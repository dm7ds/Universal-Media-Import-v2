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
/// Pre-Processor Stub für Burst-Detection-Sequenzierung.
/// Die tatsächliche Burst-Logik läuft in ImportPipelineService.DetectSequencesAsync
/// innerhalb von ScanSourceAsync (DB-Pfad-Abhängigkeit).
/// Dieser Stub dokumentiert die Integration im neuen Framework und protokolliert das Ergebnis.
/// Order: 30.
/// </summary>
public class BurstDetectionPreProcessor : IPreProcessor
{
    private readonly ILogger<BurstDetectionPreProcessor>? _logger;

    public string Name => "Burst Detection";
    public int Order => 30;

    public BurstDetectionPreProcessor(ILogger<BurstDetectionPreProcessor>? logger = null)
    {
        _logger = logger;
    }

    public bool IsEnabledForCamera(CameraConfig config, ImportContext context)
        => config.Features.BurstDetection;

    public Task<PreProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct = default)
    {

        _logger?.LogDebug(
            "[{Camera}] Burst-Detection: läuft intern in ScanSourceAsync ({Count} Fotos berücksichtigt)",
            context.CameraId,
            files.Count(f => f.IsPhoto));

        return Task.FromResult(new PreProcessingResult(Name, 0, files.Count, 0, Array.Empty<string>()));
    }
}
