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

using System.Text.Json;
using System.Text.Json.Nodes;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service zum Lesen von UMI-Datei-Headern.
/// Ermöglicht Typerkennung und Schema-Validierung.
/// </summary>
public static class UmiFileReader
{
    /// <summary>
    /// Liest den Header einer .umi oder config.json Datei.
    /// </summary>
    /// <param name="filePath">Pfad zur Datei</param>
    /// <returns>Header-Objekt oder null wenn kein Header vorhanden</returns>
    public static UmiFileHeader? ReadHeader(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            var node = JsonNode.Parse(json);
            var headerNode = node?["_umi_header"];

            if (headerNode == null)
                return null;

            return JsonSerializer.Deserialize<UmiFileHeader>(
                headerNode.ToJsonString(),
                JsonDefaults.ReadOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Erkennt den Dateityp anhand des Headers.
    /// </summary>
    /// <param name="filePath">Pfad zur Datei</param>
    /// <returns>Dateityp (z.B. "config", "burst_profile") oder null wenn kein Header</returns>
    public static string? DetectFileType(string filePath)
    {
        return ReadHeader(filePath)?.Type;
    }

    /// <summary>
    /// Prüft ob eine Datei einen UMI-Header hat.
    /// </summary>
    /// <param name="filePath">Pfad zur Datei</param>
    /// <returns>True wenn Header vorhanden, sonst false</returns>
    public static bool HasHeader(string filePath)
    {
        return ReadHeader(filePath) != null;
    }
}
