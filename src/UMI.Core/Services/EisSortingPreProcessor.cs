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
/// Pre-Processor Stub für EIS-basierte Sortierung.
/// Die tatsächliche EIS-Logik läuft in EisSortingService.ApplyEisSortingAsync
/// innerhalb von ImportPipelineService.ScanSourceAsync (DB-Pfad-Abhängigkeit).
/// Dieser Stub dokumentiert die Integration im neuen Framework und protokolliert das Ergebnis.
/// Order: 25.
/// </summary>
public class EisSortingPreProcessor : IPreProcessor
{
    private readonly ILogger<EisSortingPreProcessor>? _logger;

    public string Name => "EIS Sorting";
    public int Order => 25;

    public EisSortingPreProcessor(ILogger<EisSortingPreProcessor>? logger = null)
    {
        _logger = logger;
    }

    public bool IsEnabledForCamera(CameraConfig config, ImportContext context)
        => config.Features.EisDetection && !context.NoEisSort;

    public Task<PreProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct = default)
    {

        _logger?.LogDebug(
            "[{Camera}] EIS-Sorting: läuft intern in ScanSourceAsync ({Count} Videos berücksichtigt)",
            context.CameraId,
            files.Count(f => f.IsVideo));

        return Task.FromResult(new PreProcessingResult(Name, 0, files.Count, 0, Array.Empty<string>()));
    }
}
