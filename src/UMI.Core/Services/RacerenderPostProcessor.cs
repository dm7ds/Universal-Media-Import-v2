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
/// PostProcessor-Stub für Racerender-Integration (zukünftige Implementierung).
/// Racerender benötigt GPS-Daten im Video als Input → NeedsGps=true.
/// Erzeugt neue gerenderte Dateien → CreatesNewFiles=true.
/// Order: 2 (nach Gyroflow).
/// </summary>
public class RacerenderPostProcessor : IPostProcessor
{
    private readonly ILogger<RacerenderPostProcessor>? _logger;

    public string Name => "Racerender";
    public int Order => 2;

    /// <summary>Racerender benötigt GPS-Daten im Video als Datenquelle.</summary>
    public bool NeedsGps => true;

    /// <summary>Racerender erzeugt neue gerenderte Video-Dateien.</summary>
    public bool CreatesNewFiles => true;

    public RacerenderPostProcessor(ILogger<RacerenderPostProcessor>? logger = null)
    {
        _logger = logger;
    }

    public bool IsEnabledForCamera(CameraConfig config, ImportContext context)
        => config.PostProcessing.Enabled && config.PostProcessing.Racerender.Enabled;

    public Task<PostProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        IProgress<PostProcessingProgress>? progress = null,
        CancellationToken ct = default)
    {

        _logger?.LogInformation(
            "[{Camera}] Racerender: Stub – Integration noch nicht implementiert ({Count} Videos)",
            context.CameraId, files.Count(f => f.IsVideo));

        return Task.FromResult(new PostProcessingResult(
            Name,
            ProcessedCount: 0,
            SkippedCount: files.Count,
            FailedCount: 0,
            Errors: Array.Empty<string>()));
    }
}
