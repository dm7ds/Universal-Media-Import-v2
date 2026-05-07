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
using UMI.Core.Models;

namespace UMI.Core.Services;

/// <summary>
/// Orchestriert einen vollständigen Import für eine einzelne Kamera-Quelle:
/// Scan → Copy → Pre-Processing → Post-Processing.
/// Kann von CLI-Commands und der WPF-GUI gleichermassen genutzt werden.
/// </summary>
public interface IImportOrchestrationService
{
    /// <summary>
    /// Führt einen vollständigen Import für eine einzelne Kamera-Quelle aus
    /// (Scan → Copy → Pre-Processing → Post-Processing).
    /// </summary>
    /// <param name="context">Import-Kontext (Kamera-ID, Pfade, Flags).</param>
    /// <param name="progressReporter">Progress-Callbacks für Scan, Copy, Phase-Start/-Ende.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <param name="pauseEvent">
    /// Optionaler Pause/Resume-Mechanismus. <c>ManualResetEventSlim(true)</c> = running,
    /// <c>.Reset()</c> = Pause, <c>.Set()</c> = Resume. <c>null</c> = kein Pause (CLI-Default).
    /// </param>
    /// <returns>Ergebnis mit Statistiken und optionaler Fehlermeldung.</returns>
    Task<ImportOrchestrationResult> RunImportAsync(
        ImportContext context,
        IProgressReporter? progressReporter = null,
        CancellationToken ct = default,
        ManualResetEventSlim? pauseEvent = null);
}
