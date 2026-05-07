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
/// Live-Information über eine aktuell eingesteckte SD-Karte
/// </summary>
public class SdCardInfo
{
    /// <summary>
    /// Volume Serial Number (z.B. "A4F2-8B31")
    /// Ändert sich bei Formatierung!
    /// </summary>
    public string VolumeSerial { get; set; } = "";

    /// <summary>
    /// Disk Serial Number (Win32_DiskDrive.SerialNumber)
    /// Bleibt bei Formatierung erhalten, kann aber null sein (viele USB-Reader liefern nichts)
    /// </summary>
    public string? DiskSerial { get; set; }

    /// <summary>
    /// Disk-Größe in Bytes (für Re-Mapping Heuristik nach Formatierung)
    /// </summary>
    public long DiskSizeBytes { get; set; }

    /// <summary>
    /// Volume Label (z.B. "EOS_DIGITAL", "DJI_ACTION")
    /// </summary>
    public string? VolumeLabel { get; set; }

    /// <summary>
    /// Drive Letter mit Doppelpunkt (z.B. "F:")
    /// </summary>
    public string DriveLetter { get; set; } = "";

    /// <summary>
    /// Drive-Typ (Removable, Fixed, Network, etc.)
    /// Zur Unterscheidung echte SD-Karten vs. Backup-Ordner auf festen Disks
    /// </summary>
    public DriveType DriveType { get; set; } = DriveType.Unknown;

    /// <summary>
    /// Disk Model aus Win32_DiskDrive (z.B. "SanDisk Ultra USB 3.0")
    /// </summary>
    public string? DiskModel { get; set; }

    /// <summary>
    /// Disk Manufacturer aus Win32_DiskDrive (z.B. "SanDisk" oder "(Standard disk drives)")
    /// </summary>
    public string? DiskManufacturer { get; set; }
}

/// <summary>
/// Persistente Registrierung einer SD-Karte zu einer Kamera
/// Wird in config.json unter "sd_cards" gespeichert
/// </summary>
public class SdCardRegistration
{
    /// <summary>
    /// Kamera-ID (z.B. "OA5", "R10", "M2P")
    /// </summary>
    [JsonPropertyName("camera_id")]
    public string CameraId { get; set; } = "";

    /// <summary>
    /// Disk Serial Number für Re-Mapping nach Formatierung
    /// </summary>
    [JsonPropertyName("disk_serial")]
    public string? DiskSerial { get; set; }

    /// <summary>
    /// Disk-Größe in Bytes (für Re-Mapping Heuristik)
    /// </summary>
    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    /// <summary>
    /// Volume Label (informativ)
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>
    /// Kamera-Modell aus EXIF (beim ersten Mal gesetzt)
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Erstmals gesehen (ISO 8601)
    /// </summary>
    [JsonPropertyName("first_seen")]
    public string FirstSeen { get; set; } = "";

    /// <summary>
    /// Zuletzt gesehen (ISO 8601, wird bei jedem Lookup aktualisiert)
    /// </summary>
    [JsonPropertyName("last_seen")]
    public string LastSeen { get; set; } = "";

    /// <summary>
    /// True wenn die Karte keiner festen Kamera zugeordnet ist (CameraId leer).
    /// Floating-Karten lösen bei Watch/QuickImport einen Auswahldialog aus.
    /// </summary>
    [JsonIgnore]
    public bool IsFloating => string.IsNullOrEmpty(CameraId);

    /// <summary>
    /// Sortierungsreihenfolge für Drag & Drop (0 = Default, aufsteigend).
    /// </summary>
    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Nutzungs-Historie: Wie oft wurde diese Karte mit welcher Kamera importiert?
    /// Key = CameraId, Value = Anzahl erfolgreicher Imports.
    /// </summary>
    [JsonPropertyName("usage_history")]
    public Dictionary<string, int> UsageHistory { get; set; } = new();

    /// <summary>
    /// Wann wurde diese Karte zuletzt mit welcher Kamera genutzt?
    /// Key = CameraId, Value = Datum des letzten Imports.
    /// </summary>
    [JsonPropertyName("last_used_with")]
    public Dictionary<string, DateTime> LastUsedWith { get; set; } = new();

    /// <summary>
    /// Records a usage event: increments usage count, updates timestamps.
    /// </summary>
    public void RecordUsage(string cameraId)
    {
        UsageHistory.TryGetValue(cameraId, out var count);
        UsageHistory[cameraId] = count + 1;
        LastUsedWith[cameraId] = DateTime.UtcNow;
        LastSeen = DateTime.UtcNow.ToString("o");
    }

    /// <summary>
    /// Returns empty string (floating) or the camera ID based on the floating flag.
    /// </summary>
    public static string EffectiveCameraId(string cameraId, bool isFloating)
        => isFloating ? "" : cameraId;
}
