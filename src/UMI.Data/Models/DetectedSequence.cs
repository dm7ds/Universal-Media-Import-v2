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
/// Repräsentiert eine erkannte Burst-Sequenz (Sport/Astro).
/// Wird in SQLite `sequences` Tabelle gespeichert.
/// </summary>
public class DetectedSequence
{
    /// <summary>
    /// Primärschlüssel (SQLite: id INTEGER PRIMARY KEY AUTOINCREMENT)
    /// SQLite: INTEGER (Int64)
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Kamera-ID (z.B. "R10")
    /// </summary>
    public string CameraId { get; set; } = "";

    /// <summary>
    /// Aufnahmedatum (YYYY-MM-DD Teil des ersten Fotos)
    /// SQLite: TEXT (ISO 8601 YYYY-MM-DD)
    /// </summary>
    public string CaptureDate { get; set; } = "";

    /// <summary>
    /// Shooting-Modus: "Sport" oder "Astro"
    /// </summary>
    public string Mode { get; set; } = "";

    /// <summary>
    /// Ordnername (z.B. "Sport_143022", "Astro_220145")
    /// Format: {Mode}_{HHmmss} vom ersten Foto der Sequenz
    /// </summary>
    public string FolderName { get; set; } = "";

    /// <summary>
    /// Anzahl der Fotos in dieser Sequenz (Alias für FileCount für Kompatibilität)
    /// SQLite: INTEGER (Int64)
    /// </summary>
    public long PhotoCount { get; set; } = 0;

    /// <summary>
    /// ISO 8601 Zeitstempel des ersten Fotos (für Sortierung)
    /// </summary>
    public string FirstPhotoTime { get; set; } = "";

    /// <summary>
    /// Verwendeter Threshold in Sekunden
    /// </summary>
    public double ThresholdUsed { get; set; }

    /// <summary>
    /// Zeitstempel der Erstellung (Sequenzerkennung)
    /// SQLite: TEXT (ISO 8601)
    /// </summary>
    public string CreatedAt { get; set; } = "";
}
