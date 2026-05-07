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

using System.Threading;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Führt alle registrierten IPreProcessor in der definierten Reihenfolge (Order) aus.
/// Prozessoren werden nach IsEnabledForCamera gefiltert und sequentiell ausgeführt.
/// </summary>
public class PreProcessingOrchestrator : IPreProcessingOrchestrator
{
    private readonly IReadOnlyList<IPreProcessor> _processors;
    private readonly ILogger<PreProcessingOrchestrator>? _logger;

    public PreProcessingOrchestrator(
        IEnumerable<IPreProcessor> processors,
        ILogger<PreProcessingOrchestrator>? logger = null)
    {

        _processors = processors.OrderBy(p => p.Order).ToList();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PreProcessingResult>> RunAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct = default,
        ManualResetEventSlim? pauseEvent = null)
    {
        if (files.Count == 0 || _processors.Count == 0)
            return Array.Empty<PreProcessingResult>();

        var results = new List<PreProcessingResult>(_processors.Count);

        _logger?.LogDebug(
            "[{Camera}] PreProcessing gestartet: {FileCount} Dateien, {ProcessorCount} Prozessoren",
            context.CameraId, files.Count, _processors.Count);

        foreach (var processor in _processors)
        {
            ct.ThrowIfCancellationRequested();
            pauseEvent?.Wait(ct);

            if (!processor.IsEnabledForCamera(context.Config, context))
            {
                _logger?.LogDebug(
                    "[{Camera}] PreProcessor übersprungen (deaktiviert): {Name}",
                    context.CameraId, processor.Name);
                continue;
            }

            _logger?.LogDebug(
                "[{Camera}] PreProcessor startet: {Name} (Order={Order})",
                context.CameraId, processor.Name, processor.Order);

            var result = await processor.ProcessAsync(files, context, ct);
            results.Add(result);

            if (result.IsSuccess)
            {
                _logger?.LogDebug(
                    "[{Camera}] PreProcessor abgeschlossen: {Name} – Verarbeitet={Processed}, Übersprungen={Skipped}",
                    context.CameraId, processor.Name, result.ProcessedCount, result.SkippedCount);
            }
            else
            {
                _logger?.LogWarning(
                    "[{Camera}] PreProcessor mit Fehlern: {Name} – Fehlgeschlagen={Failed}, Fehler: {Errors}",
                    context.CameraId, processor.Name, result.FailedCount,
                    string.Join("; ", result.Errors));
            }
        }

        _logger?.LogDebug(
            "[{Camera}] PreProcessing abgeschlossen: {Count} Prozessoren ausgeführt",
            context.CameraId, results.Count);

        return results;
    }
}
