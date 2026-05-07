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

using UMI.Core.Models;

namespace UMI.Core.Services;

/// <summary>
/// Abstraktes Interface für Progress-Reporting.
/// CLI und GUI implementieren unterschiedlich.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Scan gestartet (Kamera hinzufügen).
    /// </summary>
    void OnScanStart(string cameraId, string cameraType);

    /// <summary>
    /// Scan-Ergebnis (Totals bekannt).
    /// </summary>
    void OnScanComplete(string cameraId, int fileCount, long totalBytes);

    /// <summary>
    /// Copy-Fortschritt (pro Chunk während Copy).
    /// NEU: Enthält ActiveJobs für Multi-Line Progress.
    /// </summary>
    /// <param name="cameraId">Kamera-ID</param>
    /// <param name="progress">Progress-Objekt mit Gesamt-Stats + aktive Jobs</param>
    void OnCopyProgress(string cameraId, CopyProgress progress);

    /// <summary>
    /// Copy einer Kamera fertig.
    /// </summary>
    void OnCopyComplete(string cameraId);

    /// <summary>
    /// Phase-Start (GPS, Gyroflow etc.).
    /// </summary>
    void OnPhaseStart(string phase, int totalItems);

    /// <summary>
    /// Phase-Fortschritt.
    /// </summary>
    void OnPhaseProgress(string phase, string item);

    /// <summary>
    /// Phase abgeschlossen.
    /// </summary>
    void OnPhaseComplete(string phase);

    /// <summary>
    /// Fehler aufgetreten.
    /// </summary>
    void OnError(string cameraId, string message);

    /// <summary>
    /// Alles fertig.
    /// </summary>
    void OnComplete(ImportProgressState finalState);

    /// <summary>
    /// Gyroflow Render-Fortschritt pro Frame (optional, default noop).
    /// </summary>
    void OnRenderProgress(GyroflowRenderProgress progress) { }

    /// <summary>
    /// Batch-level progress within a phase (e.g. Gyroflow video 2/5).
    /// Provides accurate current/total counts from the actual processor.
    /// </summary>
    void OnBatchProgress(string phase, int current, int total, string currentFile) { }
}
