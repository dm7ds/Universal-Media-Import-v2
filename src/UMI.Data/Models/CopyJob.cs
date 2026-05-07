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
/// DTO für einen ausstehenden Kopierauftrag.
/// Wird aus ImportedFile-Daten gebaut (kein separates DB-Table).
/// </summary>
public class CopyJob
{
    /// <summary>
    /// Import-ID (Referenz zu ImportedFile.Id)
    /// SQLite: INTEGER (Int64)
    /// </summary>
    public long ImportId { get; set; } = 0;

    /// <summary>
    /// Quellpfad der Datei
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// Zielpfad (bereits berechnet mit SEQ-Ordner)
    /// </summary>
    public string DestPath { get; set; } = "";

    /// <summary>
    /// Dateigröße in Bytes (für Fortschrittsanzeige)
    /// </summary>
    public long FileSize { get; set; } = 0;

    /// <summary>
    /// True wenn Video, False wenn Foto
    /// SQLite: INTEGER (Int64) - 0=Foto, 1=Video
    /// </summary>
    public long IsVideo { get; set; } = 0;
}
