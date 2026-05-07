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
/// Repräsentiert eine importierte Datei mit allen Metadaten.
/// Wird in SQLite `imports` Tabelle gespeichert.
/// </summary>
public class ImportedFile
{
    /// <summary>
    /// Primärschlüssel (SQLite: id INTEGER PRIMARY KEY AUTOINCREMENT)
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Quellpfad der Datei auf SD-Karte (UNIQUE)
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// Zielpfad nach Import (inkl. SEQ-Ordner), NULL bis Sequenzerkennung abgeschlossen
    /// </summary>
    public string? DestPath { get; set; }

    /// <summary>
    /// Dateiname (ohne Pfad, z.B. "IMG_1208.CR3")
    /// </summary>
    public string Filename { get; set; } = "";

    /// <summary>
    /// Kamera-ID (z.B. "GoPro11", "MyDSLR", "DroneX")
    /// </summary>
    public string CameraId { get; set; } = "";

    /// <summary>
    /// Aufnahmedatum (aus EXIF CreateDate oder DJI-Dateiname)
    /// SQLite: TEXT (ISO 8601 YYYY-MM-DD)
    /// </summary>
    public string CaptureDate { get; set; } = "";

    /// <summary>
    /// ISO 8601 Capture-Zeit für präzise Sortierung (z.B. "2024-09-07T14:30:22")
    /// </summary>
    public string CaptureTime { get; set; } = "";

    /// <summary>
    /// Dateigröße in Bytes
    /// </summary>
    public long FileSize { get; set; } = 0;

    /// <summary>
    /// True wenn Video, False wenn Foto
    /// SQLite: INTEGER (Int64) - 0=Foto, 1=Video
    /// </summary>
    public long IsVideo { get; set; } = 0;

    /// <summary>
    /// Helper-Property für bool-Zugriff (nicht in DB)
    /// </summary>
    public bool IsVideoFlag => IsVideo != 0;

    /// <summary>
    /// Medien-Typ: "photo" oder "video"
    /// </summary>
    public string MediaType { get; set; } = "";

    /// <summary>
    /// Kamera-Modell aus EXIF (z.B. "Canon EOS R6")
    /// </summary>
    public string? CameraModel { get; set; }

    /// <summary>
    /// Shooting-Mode: "Sport", "Astro" oder "Single"
    /// </summary>
    public string? ShootingMode { get; set; }

    /// <summary>
    /// Belichtungszeit in Sekunden (z.B. 8.0, 0.0005), NULL wenn nicht verfügbar
    /// </summary>
    public double? ExposureTime { get; set; }

    /// <summary>
    /// Canon Continuous Drive Mode (0=Single, 5=HighSpeed), NULL wenn nicht verfügbar
    /// SQLite: INTEGER (Int64)
    /// </summary>
    public long? ContinuousDrive { get; set; }

    /// <summary>
    /// Canon Exposure Mode (4=Manual, 6=Bulb), NULL wenn nicht verfügbar
    /// SQLite: INTEGER (Int64)
    /// </summary>
    public long? ExposureMode { get; set; }

    /// <summary>
    /// Video-Dauer in Millisekunden, NULL bei Fotos
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Foreign Key zu DetectedSequence (NULL = kein Burst, einzelnes Foto)
    /// SQLite: INTEGER (Int64)
    /// </summary>
    public long? SequenceId { get; set; }

    /// <summary>
    /// Kopierstatus: 0=Pending, 1=InProgress, 2=Completed, 3=Failed
    /// SQLite: INTEGER (Int64)
    /// </summary>
    public long CopyStatus { get; set; } = 0;

    /// <summary>
    /// Fehlermeldung wenn CopyStatus=Failed, sonst NULL
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Zeitstempel der Erstellung (Scan-Phase)
    /// SQLite: TEXT (ISO 8601)
    /// </summary>
    public string CreatedAt { get; set; } = "";

    /// <summary>
    /// Zeitstempel der letzten Aktualisierung
    /// SQLite: TEXT (ISO 8601)
    /// </summary>
    public string UpdatedAt { get; set; } = "";
}

/// <summary>
/// Copy-Status Konstanten (statt enum wegen SQLite Int64 Mapping)
/// </summary>
public static class CopyStatus
{
    public const long Pending = 0;
    public const long InProgress = 1;
    public const long Completed = 2;
    public const long Failed = 3;
}
