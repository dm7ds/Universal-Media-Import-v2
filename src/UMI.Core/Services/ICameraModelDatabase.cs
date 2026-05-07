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
/// Datenbank für bekannte Kameramodelle — identifiziert Make/Model-Kombinationen
/// und ordnet ihnen einen UMI-Kamera-Typ zu.
/// </summary>
public interface ICameraModelDatabase
{
    /// <summary>
    /// Identifiziert eine Kamera anhand von Make und Model (aus EXIF/Metadaten).
    /// </summary>
    /// <param name="make">Kamerahersteller (z.B. "DJI", "GoPro", "SONY"). Case-insensitiv.</param>
    /// <param name="model">Kameramodell (z.B. "FC3582", "HERO12 Black", "ILCE-7M4"). Case-insensitiv.</param>
    /// <returns>
    /// <see cref="CameraModelMatch"/> mit CameraType und optionalem DisplayName,
    /// oder null wenn die Kamera nicht erkannt wurde.
    /// </returns>
    CameraModelMatch? Identify(string make, string model);

    /// <summary>
    /// Identifiziert eine Kamera anhand des Volume-Labels der SD-Karte / des USB-Laufwerks.
    /// Viele Hersteller setzen sprechende Labels (z.B. "DJI_ACTION", "HERO12", "EOS_DIGITAL").
    /// </summary>
    /// <param name="volumeLabel">Volume-Label des Laufwerks. Case-insensitiv.</param>
    /// <returns>
    /// <see cref="CameraModelMatch"/> wenn das Label einem bekannten Muster entspricht,
    /// sonst null.
    /// </returns>
    CameraModelMatch? IdentifyByVolumeLabel(string volumeLabel);
}
