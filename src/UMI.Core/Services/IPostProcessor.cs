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
/// Fortschritt während der PostProcessor-Ausführung.
/// </summary>
public record PostProcessingProgress(
    string ProcessorName,
    int Current,
    int Total,
    string CurrentFile);

/// <summary>
/// Ergebnis eines einzelnen PostProcessors.
/// </summary>
public record PostProcessingResult(
    string ProcessorName,
    int ProcessedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Neu erzeugte Dateien: Original-DestPath → Neuer Dateipfad.
    /// Befüllt wenn CreatesNewFiles=true (z.B. Gyroflow → stabilisierte Kopie).
    /// Leeres Dictionary wenn der Prozessor keine neuen Dateien erzeugt.
    /// </summary>
    public IReadOnlyDictionary<string, string> OutputFiles { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Gibt true zurück wenn keine Fehler aufgetreten sind.</summary>
    public bool IsSuccess => FailedCount == 0 && Errors.Count == 0;

    /// <summary>Erstellt ein leeres Erfolgs-Ergebnis.</summary>
    public static PostProcessingResult Empty(string processorName)
        => new(processorName, 0, 0, 0, Array.Empty<string>());

    /// <summary>
    /// Batch-ID wenn die Verarbeitung an die GPU Queue delegiert wurde.
    /// </summary>
    public string? BatchId { get; init; }

    /// <summary>
    /// True wenn die Verarbeitung asynchron in der GPU Queue läuft.
    /// </summary>
    public bool IsDeferredToQueue { get; init; }
}

/// <summary>
/// Interface für modulare Post-Processors (Gyroflow, Racerender, etc.).
/// Post-Processors laufen nach dem Copy auf den Zieldateien in der Workbench.
///
/// Smart-GPS-Koordination via NeedsGps und CreatesNewFiles:
///   - NeedsGps=true → GPS muss vor diesem Prozessor injiziert sein
///   - CreatesNewFiles=true → GPS-Status nach diesem Prozessor zurücksetzen
///                            (neue Datei hat kein GPS → muss neu injiziert werden)
/// </summary>
public interface IPostProcessor
{
    /// <summary>Anzeigename des PostProcessors (für Logging).</summary>
    string Name { get; }

    /// <summary>
    /// Ausführungsreihenfolge (aufsteigend). Niedrige Zahlen laufen zuerst.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gibt true zurück wenn dieser PostProcessor GPS-Daten im Video benötigt.
    /// Der Orchestrator injiziert GPS BEVOR dieser Prozessor ausgeführt wird.
    /// </summary>
    bool NeedsGps { get; }

    /// <summary>
    /// Gibt true zurück wenn dieser PostProcessor neue Dateien erzeugt
    /// (z.B. Gyroflow → stabilisierte Kopie).
    /// Der Orchestrator setzt den GPS-Inject-Status zurück (neues Video hat kein GPS).
    /// </summary>
    bool CreatesNewFiles { get; }

    /// <summary>
    /// Prüft ob dieser PostProcessor für die gegebene Kamera aktiv ist.
    /// </summary>
    bool IsEnabledForCamera(CameraConfig config, ImportContext context);

    /// <summary>
    /// Führt die PostProcessing-Logik auf den importierten Zieldateien aus.
    /// </summary>
    Task<PostProcessingResult> ProcessAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        IProgress<PostProcessingProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Orchestriert alle registrierten PostProcessors mit Smart-GPS-Inject-Logik.
/// </summary>
public interface IPostProcessingOrchestrator
{
    /// <summary>
    /// Gibt die aktiven PostProcessors für den gegebenen Kontext zurück (sortiert nach Order).
    /// </summary>
    IReadOnlyList<IPostProcessor> GetActiveProcessors(CameraConfig config, ImportContext context);

    /// <summary>
    /// Führt alle aktiven PostProcessors aus und koordiniert GPS-Injection smart.
    /// </summary>
    /// <param name="pauseEvent">
    /// Optionaler Pause/Resume-Mechanismus. Wird am Anfang jeder Prozessor-Iteration geprüft.
    /// <c>null</c> = kein Pause (CLI-Default).
    /// </param>
    Task<IReadOnlyList<PostProcessingResult>> RunAsync(
        IReadOnlyList<ImportedFileInfo> files,
        ImportContext context,
        IProgressReporter? progressReporter = null,
        CancellationToken ct = default,
        ManualResetEventSlim? pauseEvent = null);
}
