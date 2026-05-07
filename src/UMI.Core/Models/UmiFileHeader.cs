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

using System.Text.Json.Serialization;

namespace UMI.Core.Models;

/// <summary>
/// Header-Block für alle UMI Config/Preset-Dateien.
/// Ermöglicht Typerkennung, Versionierung und Schema-Migration.
/// </summary>
public class UmiFileHeader
{
    /// <summary>
    /// Dateityp. Siehe UmiFileTypes für erlaubte Werte.
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// App-Version die die Datei erstellt/zuletzt geschrieben hat.
    /// Format: "2.1.0"
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.1.0";

    /// <summary>
    /// Schema-Version der Datei-Struktur.
    /// Startet bei 1, wird bei Breaking Changes erhöht.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Erstellungszeitpunkt (ISO 8601 UTC).
    /// </summary>
    [JsonPropertyName("created")]
    public string Created { get; set; } = DateTime.UtcNow.ToString("o");

    /// <summary>
    /// Letzter Änderungszeitpunkt (ISO 8601 UTC).
    /// </summary>
    [JsonPropertyName("modified")]
    public string Modified { get; set; } = DateTime.UtcNow.ToString("o");

    /// <summary>
    /// Optionale Beschreibung.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Konstanten für UMI-Dateitypen.
/// </summary>
public static class UmiFileTypes
{
    /// <summary>Haupt-Konfiguration (config.json)</summary>
    public const string Config = "config";

    /// <summary>Burst-Profil (Sport.umi, Astro.umi, etc.)</summary>
    public const string BurstProfile = "burst_profile";

    /// <summary>Kamera-Typ (Action.umi, Drone.umi, etc.)</summary>
    public const string CameraType = "camera_type";

    /// <summary>Import-Profil (Action-GPS.umi, etc.)</summary>
    public const string ImportProfile = "import_profile";

    /// <summary>Gyroflow-Preset (OA5_Default.gyroflow)</summary>
    public const string GyroflowPreset = "gyroflow_preset";
}
