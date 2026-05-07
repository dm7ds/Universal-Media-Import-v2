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

namespace UMI.Core.Models;

/// <summary>
/// Fingerprint einer SD-Karte (erkannt via version.txt oder EXIF).
/// </summary>
public class SdFingerprint
{
    public string? SerialNumber { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? FirmwareVersion { get; set; }

    /// <summary>
    /// Detektions-Methode: "version.txt", "exif-photo", "user"
    /// </summary>
    public string DetectionMethod { get; set; } = "";
}
