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

using System;
using System.Collections.Generic;
using System.Linq;

namespace UMI.Core;

/// <summary>
/// Filter für den Import-Vorgang.
/// Ermöglicht Auswahl nach Kameras, Typen, Herstellern, etc.
/// </summary>
public class ImportFilter
{
    /// <summary>
    /// Spezifische Kamera-IDs zum Importieren.
    /// Wenn leer: Alle aktivierten Kameras.
    /// </summary>
    public List<string> CameraIds { get; set; } = new();

    /// <summary>
    /// Filter nach Kamera-Typen (Action, Drone, Mirrorless, etc.).
    /// Wenn leer: Alle Typen.
    /// </summary>
    public List<string> CameraTypes { get; set; } = new();

    /// <summary>
    /// Filter nach Herstellern (DJI, Canon, Sony, etc.).
    /// Wenn leer: Alle Hersteller.
    /// </summary>
    public List<string> Manufacturers { get; set; } = new();

    /// <summary>
    /// Nur aktivierte Kameras importieren.
    /// </summary>
    public bool OnlyEnabled { get; set; } = true;

    /// <summary>
    /// Prüft ob eine Kamera diesem Filter entspricht.
    /// </summary>
    public bool Matches(ICameraHandler handler, CameraConfig config)
    {

        if (OnlyEnabled && !config.Enabled)
            return false;

        if (CameraIds.Any() && !CameraIds.Contains(handler.CameraId))
            return false;

        if (CameraTypes.Any() && !CameraTypes.Contains(handler.CameraType))
            return false;

        if (Manufacturers.Any() && !Manufacturers.Contains(handler.Manufacturer, StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Erstellt einen Filter für alle Kameras.
    /// </summary>
    public static ImportFilter All() => new();

    /// <summary>
    /// Erstellt einen Filter für eine spezifische Kamera.
    /// </summary>
    public static ImportFilter ForCamera(string cameraId) => new()
    {
        CameraIds = new List<string> { cameraId }
    };

    /// <summary>
    /// Erstellt einen Filter für einen Kamera-Typ.
    /// </summary>
    public static ImportFilter ForType(string type) => new()
    {
        CameraTypes = new List<string> { type }
    };

    /// <summary>
    /// Erstellt einen Filter für mehrere Kamera-Typen.
    /// </summary>
    public static ImportFilter ForTypes(params string[] types) => new()
    {
        CameraTypes = types.ToList()
    };

    /// <summary>
    /// Erstellt einen Filter für einen Hersteller.
    /// </summary>
    public static ImportFilter ForManufacturer(string manufacturer) => new()
    {
        Manufacturers = new List<string> { manufacturer }
    };

    /// <summary>
    /// Gibt eine benutzerfreundliche Beschreibung des Filters zurück.
    /// </summary>
    public string GetDescription()
    {
        var parts = new List<string>();

        if (CameraIds.Any())
            parts.Add($"Kameras: {string.Join(", ", CameraIds)}");

        if (CameraTypes.Any())
            parts.Add($"Typen: {string.Join(", ", CameraTypes)}");

        if (Manufacturers.Any())
            parts.Add($"Hersteller: {string.Join(", ", Manufacturers)}");

        if (OnlyEnabled)
            parts.Add("Nur aktivierte");

        return parts.Any()
            ? string.Join(" | ", parts)
            : "Alle Kameras";
    }
}

/// <summary>
/// Extension Methods für ImportFilter.
/// </summary>
public static class ImportFilterExtensions
{
    /// <summary>
    /// Filtert Kamera-Handler basierend auf ImportFilter.
    /// </summary>
    public static IEnumerable<(ICameraHandler Handler, CameraConfig Config)> ApplyFilter(
        this IEnumerable<(ICameraHandler Handler, CameraConfig Config)> cameras,
        ImportFilter filter)
    {
        return cameras.Where(c => filter.Matches(c.Handler, c.Config));
    }
}
