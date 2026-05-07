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

namespace UMI.Data.Models;

/// <summary>
/// Statistiken über den aktuellen Import-Vorgang.
/// Wird via SQL-Aggregation berechnet (kein separates DB-Table).
/// </summary>
public class ImportStats
{
    /// <summary>
    /// Gesamtanzahl der Dateien
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Gesamtgröße aller Dateien in Bytes
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// Anzahl der Fotos
    /// </summary>
    public int PhotosCount { get; set; }

    /// <summary>
    /// Anzahl der Videos
    /// </summary>
    public int VideosCount { get; set; }

    /// <summary>
    /// Anzahl der erkannten Burst-Sequenzen
    /// </summary>
    public int SequencesCount { get; set; }

    /// <summary>
    /// Anzahl der erfolgreich kopierten Dateien
    /// </summary>
    public int CopyCompleted { get; set; }

    /// <summary>
    /// Anzahl der noch ausstehenden Dateien
    /// </summary>
    public int CopyPending { get; set; }

    /// <summary>
    /// Anzahl der fehlgeschlagenen Kopiervorgänge
    /// </summary>
    public int CopyFailed { get; set; }

    /// <summary>
    /// Anzahl der gerade laufenden Kopiervorgänge
    /// </summary>
    public int CopyInProgress { get; set; }

    /// <summary>
    /// Alias für PhotosCount (für Kompatibilität)
    /// </summary>
    public int Photos => PhotosCount;

    /// <summary>
    /// Alias für VideosCount (für Kompatibilität)
    /// </summary>
    public int Videos => VideosCount;

    /// <summary>
    /// Alias für TotalSize (für Kompatibilität)
    /// </summary>
    public long TotalBytes => TotalSize;
}
