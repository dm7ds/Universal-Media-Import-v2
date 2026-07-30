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

namespace UMI.Core.Utilities;

/// <summary>
/// SSOT für die Serial-basierte Kamera-Zuordnung.
/// Wird von <see cref="UMI.Core.Services.SdFingerprintService"/> (SD-Karten-Fingerprint)
/// und <see cref="UMI.Core.Services.FolderSortService"/> (Foto-EXIF-Serial) genutzt.
/// Kein Magic Literal, kein Duplikat der Match-Logik.
/// </summary>
public static class CameraSerialMatcher
{
    /// <summary>
    /// Sucht die CameraId für eine gegebene Body-Seriennummer.
    /// Vergleicht <paramref name="serial"/> gegen <c>config.Cameras[].SerialNumber</c>
    /// (nur Kameras mit nicht-leerer SerialNumber werden berücksichtigt).
    /// </summary>
    /// <param name="serial">Die aus EXIF oder Fingerprint gelesene Body-Serial (null → immer null).</param>
    /// <param name="cameras">Die Kamera-Konfiguration aus UmiConfig.</param>
    /// <returns>CameraId bei Treffer, sonst null.</returns>
    public static string? FindCameraId(string? serial, IReadOnlyDictionary<string, CameraConfig> cameras)
    {
        if (string.IsNullOrEmpty(serial))
            return null;

        return cameras
            .Where(c => !string.IsNullOrEmpty(c.Value.SerialNumber)
                     && c.Value.SerialNumber == serial)
            .Select(c => c.Key)
            .FirstOrDefault();
    }

    /// <summary>
    /// Sucht die CameraId für ein gegebenes EXIF-Kameramodell.
    /// Match-Strategie (exakt, case-insensitive, getrimmt):
    ///   1. gegen <c>config.CameraModel</c> (falls gesetzt)
    ///   2. Fallback auf <c>config.Name</c> (nur bei exaktem Gleichstand)
    /// KEINE Fuzzy/Contains-Logik — "R10" darf NICHT in "R100" matchen.
    /// Nur aktivierte Kameras (<c>config.Enabled == true</c>) werden berücksichtigt.
    /// Eindeutigkeits-Regel: NUR wenn GENAU EINE aktivierte Kamera matcht → CameraId.
    /// Bei 0 oder mehr als 1 Treffer → null (kein Raten).
    /// </summary>
    /// <param name="exifModel">Das aus EXIF gelesene Kameramodell (null/leer → immer null).</param>
    /// <param name="cameras">Die Kamera-Konfiguration aus UmiConfig.</param>
    /// <returns>CameraId bei eindeutigem Treffer, sonst null.</returns>
    public static string? FindCameraIdByModel(string? exifModel, IReadOnlyDictionary<string, CameraConfig> cameras)
    {
        if (string.IsNullOrWhiteSpace(exifModel))
            return null;

        var normalizedInput = exifModel.Trim();

        var matches = cameras
            .Where(c => c.Value.Enabled)
            .Where(c =>
            {
                // Primär: camera_model (falls gesetzt)
                var configModel = c.Value.CameraModel;
                if (!string.IsNullOrWhiteSpace(configModel))
                    return string.Equals(normalizedInput, configModel.Trim(), StringComparison.OrdinalIgnoreCase);

                // Fallback: Name (nur bei exaktem Gleichstand)
                return string.Equals(normalizedInput, c.Value.Name.Trim(), StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        // Eindeutigkeits-Regel: bei Mehrdeutigkeit kein Raten
        return matches.Count == 1 ? matches[0].Key : null;
    }
}
