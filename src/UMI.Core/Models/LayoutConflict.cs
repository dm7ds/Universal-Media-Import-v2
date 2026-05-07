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
/// Beschreibt einen Layout-Konflikt: Dateien liegen flach, aber jetzt würden media_folders nötig.
/// </summary>
public class LayoutConflict
{
    /// <summary>
    /// Datum-Ordner (z.B. "2026-02-12")
    /// </summary>
    public required string DateFolder { get; set; }

    /// <summary>
    /// Kamera-ID (z.B. "OA5")
    /// </summary>
    public required string CameraId { get; set; }

    /// <summary>
    /// Liste der betroffenen Dateien die flach liegen
    /// </summary>
    public required List<string> ExistingFiles { get; set; }

    /// <summary>
    /// Aktuelles Layout (z.B. "flat" = keine Media-Ordner)
    /// </summary>
    public string CurrentLayout { get; set; } = "flat";

    /// <summary>
    /// Benötigtes Layout für neuen Import (z.B. "media_folders" = Video/ + Photo/)
    /// </summary>
    public string RequiredLayout { get; set; } = "media_folders";
}

/// <summary>
/// Auflösungsstrategie für Layout-Konflikte
/// </summary>
public enum LayoutConflictResolution
{
    /// <summary>
    /// Nur neue Dateien bekommen Unterordner, bestehende bleiben wo sie sind
    /// (z.B. Videos bleiben flach, Fotos → Photo/)
    /// </summary>
    AddSubfolderForNewType,

    /// <summary>
    /// Alles flach lassen, keine Unterordner
    /// (neue Fotos landen neben bestehenden Videos)
    /// </summary>
    KeepFlat,

    /// <summary>
    /// Alles verschieben: Videos → Video/, Fotos → Photo/
    /// ⚠️ WARNUNG: Kann Schnittprogramm-Referenzen brechen!
    /// </summary>
    ReorganizeAll
}
