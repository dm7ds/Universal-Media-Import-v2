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
using UMI.Core.Constants;

namespace UMI.Core.Configuration;

/// <summary>
/// Konfiguration für die Ordnerstruktur im Workbench.
/// </summary>
public class LayoutConfig
{
    /// <summary>
    /// Ob Kamera-Unterordner angelegt werden (z.B. OA5/, R10/).
    /// Default: true
    /// </summary>
    [JsonPropertyName("camera_folders")]
    public bool CameraFolders { get; set; } = true;

    /// <summary>
    /// Ob Medientyp-Unterordner angelegt werden (Video/, Photo/).
    /// - "true": Immer Video/ und Photo/
    /// - "false": Nie, alles flach
    /// - "auto": Nur wenn BEIDE Medientypen (Video + Photo) im selben Import vorhanden
    /// Default: "auto"
    /// </summary>
    [JsonPropertyName("media_folders")]
    public string MediaFolders { get; set; } = "auto";

    /// <summary>
    /// Reihenfolge der Ordner-Segmente.
    /// - "camera_first": workbench/date/camera/mediatype/file (Default, bisheriges Verhalten)
    /// - "type_first": workbench/date/mediatype/camera/file
    /// Nur relevant wenn camera_folders=true UND media_folders aktiv.
    /// Default: "camera_first"
    /// </summary>
    [JsonPropertyName("sort_order")]
    public string SortOrder { get; set; } = Constants.SortOrder.CameraFirst;

    /// <summary>
    /// Explizite Reihenfolge der CameraType-Gruppen in der UI.
    /// Leere Liste = alphabetische Reihenfolge.
    /// Typen die nicht in der Liste stehen kommen ans Ende (alphabetisch).
    /// </summary>
    [JsonPropertyName("camera_type_order")]
    public List<string> CameraTypeOrder { get; set; } = new();
}
