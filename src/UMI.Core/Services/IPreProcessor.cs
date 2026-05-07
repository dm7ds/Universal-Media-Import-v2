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

namespace UMI.Core.Services;

/// <summary>
/// Repräsentiert eine entdeckte Datei mit berechneten Quell- und Zielpfaden.
/// Wird vom PreProcessingOrchestrator an alle Prozessoren übergeben.
/// </summary>
public record ImportedFileInfo(
    string SourcePath,
    string DestPath,
    string FileName,
    long FileSize,
    bool IsPhoto,
    bool IsVideo);

/// <summary>
/// Ergebnis eines einzelnen Pre-Processors.
/// Wird im PreProcessingOrchestrator gesammelt und zurückgegeben.
/// </summary>
public record PreProcessingResult(
    string ProcessorName,
    int ProcessedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<string> Errors)
{
    /// <summary>Gibt true zurück wenn keine Fehler aufgetreten sind.</summary>
    public bool IsSuccess => FailedCount == 0 && Errors.Count == 0;

    /// <summary>Erstellt ein leeres Erfolgs-Ergebnis.</summary>
    public static PreProcessingResult Empty(string processorName)
        => new(processorName, 0, 0, 0, Array.Empty<string>());
}

/// <summary>
/// Interface für modulare Pre-Processor.
/// Pre-Processor laufen in ScanSourceAsync VOR dem Copy (Pipeline V2.1).
/// Reihenfolge wird durch Order bestimmt (aufsteigend).
/// </summary>
public interface IPreProcessor
{
    /// <summary>Anzeigename des Prozessors (für Logging).</summary>
    string Name { get; }

    /// <summary>
    /// Ausführungsreihenfolge (aufsteigend). Niedrige Zahlen laufen zuerst.
    /// Empfohlene Werte: 10, 20, 30 ... (Lücken für spätere Erweiterungen).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Prüft ob dieser Prozessor für die gegebene Kamera aktiv ist.
    /// Wird vor ProcessAsync aufgerufen – gibt false zurück → Prozessor wird übersprungen.
    /// </summary>
    bool IsEnabledForCamera(CameraConfig config, ImportContext context);

    /// <summary>
    /// Führt die Pre-Processing-Logik aus.
    /// Läuft auf Source-Dateien BEVOR sie in die Workbench kopiert werden.
    /// </summary>
    Task<PreProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Orchestriert alle registrierten Pre-Processor.
/// Sortiert nach Order, prüft IsEnabledForCamera und sammelt Ergebnisse.
/// </summary>
public interface IPreProcessingOrchestrator
{
    /// <summary>
    /// Führt alle aktivierten Pre-Processor in der definierten Reihenfolge aus.
    /// </summary>
    /// <param name="pauseEvent">
    /// Optionaler Pause/Resume-Mechanismus. Wird am Anfang jeder Prozessor-Iteration geprüft.
    /// <c>null</c> = kein Pause (CLI-Default).
    /// </param>
    Task<IReadOnlyList<PreProcessingResult>> RunAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        CancellationToken ct = default,
        ManualResetEventSlim? pauseEvent = null);
}
