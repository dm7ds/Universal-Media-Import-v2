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

namespace UMI.Core.Services;

/// <summary>
/// Service für Post-Processing von bereits importierten Videos (GPS, Gyroflow, etc.).
/// </summary>
public interface IPostProcessingService
{
    /// <summary>
    /// Findet Videos im Workbench basierend auf Filter und Modus.
    /// </summary>
    /// <param name="options">Post-Processing Optionen</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>Liste von gefundenen Video-Dateien</returns>
    Task<List<FileInfo>> FindVideosAsync(PostProcessingOptions options, CancellationToken ct = default);

    /// <summary>
    /// Führt Batch-Stabilisierung mit Gyroflow durch.
    /// </summary>
    /// <param name="videos">Videos zur Stabilisierung</param>
    /// <param name="options">Post-Processing Optionen</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>Stabilisierungs-Ergebnis</returns>
    Task<BatchStabilizationResult> StabilizeBatchAsync(List<FileInfo> videos, PostProcessingOptions options, CancellationToken ct = default);

    /// <summary>
    /// Führt GPS-Injection für mehrere Videos durch.
    /// </summary>
    /// <param name="videos">Videos für GPS-Injection</param>
    /// <param name="options">Post-Processing Optionen</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>GPS-Injection Statistiken (injected, failed)</returns>
    Task<(int injected, int failed)> InjectGpsBatchAsync(List<FileInfo> videos, PostProcessingOptions options, CancellationToken ct = default);

    /// <summary>
    /// Findet Videos in Video/postprocess/exported/ Ordnern für den Finalize-Workflow.
    /// Gibt Paare zurück: (exportedFile, matchingOriginalInPostprocess).
    /// </summary>
    /// <param name="workbench">Arbeitsordner (Workbench-Root)</param>
    /// <param name="date">Optionales Datum-Filter (yyyy-MM-dd)</param>
    /// <param name="source">Kamera-Filter (z.B. "OA5" oder "ALL")</param>
    /// <returns>Liste von Paaren (Exported, Original in postprocess/)</returns>
    List<(FileInfo Exported, FileInfo? Original)> FindExportedVideosForFinalize(string workbench, string? date, string source);

    /// <summary>
    /// Finalize-Workflow: GPS in exported Videos injizieren, nach Video/ verschieben, Cleanup.
    /// </summary>
    /// <param name="pairs">Paare aus FindExportedVideosForFinalize()</param>
    /// <param name="options">Post-Processing Optionen</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>Finalize-Statistiken (finalized, failed)</returns>
    Task<(int finalized, int failed)> FinalizeExportedVideosAsync(
        List<(FileInfo Exported, FileInfo? Original)> pairs,
        PostProcessingOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Post-Stabilize Workflow: Metadata Restore → Move → Cleanup für stabilisierte Videos.
    /// </summary>
    /// <param name="jobs">Liste von Stabilisierungs-Jobs (Input/Output-Pfade)</param>
    /// <param name="options">Post-Processing Optionen</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>Mapping von Original-Pfad zu finalem Pfad nach Move</returns>
    Task<Dictionary<string, string>> PostStabilizeWorkflowAsync(
        List<VideoStabilizationJob> jobs,
        PostProcessingOptions options,
        CancellationToken ct = default);
}

/// <summary>
/// Optionen für Post-Processing.
/// </summary>
public class PostProcessingOptions
{
    public required string Workbench { get; init; }
    public required string Source { get; init; }
    public string? Date { get; init; }
    public required string Mode { get; init; }
    public bool Force { get; init; }
    public bool DryRun { get; init; }
    public string? GpxSource { get; init; }
    public IProgress<StabilizationProgress>? StabilizationProgress { get; init; }
    public IProgress<GyroflowRenderProgress>? RenderProgress { get; init; }
}
