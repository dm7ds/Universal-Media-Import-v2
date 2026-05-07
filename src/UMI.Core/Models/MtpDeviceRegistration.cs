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
/// Persistente Registrierung eines MTP-Geräts (z.B. Smartphone, Action-Cam via USB) zu einer Kamera.
/// Wird in config.json unter "mtp_devices" gespeichert.
/// MTP-Seriennummern sind grundsätzlich stabil, aber manche Hersteller (z.B. DJI) liefern
/// identische Fake-Serials für verschiedene Kameramodelle. Der Registry-Key enthält daher
/// immer Serial+Manufacturer+Model (via MtpDeviceDetectionService.GetDeviceKey).
/// </summary>
public class MtpDeviceRegistration
{
    /// <summary>Kamera-ID (z.B. "OA5", "R10")</summary>
    [JsonPropertyName("camera_id")]
    public required string CameraId { get; set; }

    /// <summary>Gerätename (informativ, z.B. "DJI Action 5 Pro")</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>Erstmals gesehen (ISO 8601)</summary>
    [JsonPropertyName("first_seen")]
    public string? FirstSeen { get; set; }

    /// <summary>Zuletzt gesehen (ISO 8601, wird bei jedem Lookup aktualisiert)</summary>
    [JsonPropertyName("last_seen")]
    public string? LastSeen { get; set; }

    /// <summary>Sortierungsreihenfolge für Drag & Drop (0 = Default, aufsteigend).</summary>
    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }
}
