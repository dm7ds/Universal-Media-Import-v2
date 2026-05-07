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

using UMI.Core.Configuration;
using UMI.Core.Constants;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Service für Layout-Logik: MediaFolders-Auflösung und Pfad-Berechnung.
/// Bestimmt Ordner-Struktur basierend auf Layout-Konfiguration.
/// </summary>
public class LayoutResolver
{
    private readonly LayoutConfig _layoutConfig;

    public LayoutResolver(LayoutConfig layoutConfig)
    {
        _layoutConfig = layoutConfig;
    }

    /// <summary>
    /// Bestimmt ob Video/Photo/TimeLapse Unterordner verwendet werden sollen.
    /// Simpel: Nur wenn BEIDE Medientypen vorhanden sind.
    /// Gyroflow/ ist ein Workflow-Ordner, kein Medientyp-Ordner.
    /// </summary>
    public bool ResolveMediaFolders(
        string mediaFoldersSetting,
        bool hasVideo,
        bool hasPhoto)
    {
        return mediaFoldersSetting.ToLower() switch
        {
            "true" => true,
            "false" => false,
            "auto" => hasVideo && hasPhoto,
            _ => true
        };
    }

    /// <summary>
    /// Berechnet den Zielpfad für eine Datei basierend auf Layout-Konfiguration.
    /// Berücksichtigt sort_order für die Segment-Reihenfolge.
    /// </summary>
    public string CalculateDestPath(
        string workbenchPath,
        string dateStr,
        string cameraId,
        string filename,
        string mediaType,
        string subDir,
        bool useMediaFolders)
    {
        var segments = new List<string> { workbenchPath, dateStr };

        var isWorkflowFolder = subDir == FolderNameConstants.Gyroflow || subDir == FolderNameConstants.Stabilized;

        var sortOrder = _layoutConfig.SortOrder?.ToLower() ?? SortOrder.CameraFirst;

        if (sortOrder == SortOrder.TypeFirst)
        {

            if (useMediaFolders && !isWorkflowFolder)
            {
                segments.Add(subDir);
            }
            else if (isWorkflowFolder)
            {
                segments.Add(subDir);
            }

            if (_layoutConfig.CameraFolders)
            {
                segments.Add(cameraId);
            }
        }
        else
        {

            if (_layoutConfig.CameraFolders)
            {
                segments.Add(cameraId);
            }

            if (useMediaFolders && !isWorkflowFolder)
            {
                segments.Add(subDir);
            }
            else if (isWorkflowFolder)
            {
                segments.Add(subDir);
            }
        }

        segments.Add(filename);
        return Path.Combine(segments.ToArray());
    }

    /// <summary>
    /// Prüft ob ein Nachimport die Ordnerstruktur ändern würde.
    /// Gibt Konflikte zurück wenn Dateien flach liegen aber jetzt media_folders nötig wären.
    /// </summary>
    public List<LayoutConflict> DetectConflicts(
        string workbenchPath,
        string dateStr,
        string cameraId,
        bool useMediaFolders)
    {
        var conflicts = new List<LayoutConflict>();

        if (!useMediaFolders)
            return conflicts;

        var sortOrder = _layoutConfig.SortOrder?.ToLower() ?? SortOrder.CameraFirst;
        string cameraRootPath;

        if (_layoutConfig.CameraFolders)
        {

            cameraRootPath = Path.Combine(workbenchPath, dateStr, cameraId);
        }
        else
        {

            cameraRootPath = Path.Combine(workbenchPath, dateStr);
        }

        if (!Directory.Exists(cameraRootPath))
            return conflicts;

        var flatFiles = Directory.GetFiles(cameraRootPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();

                return ext == ".mp4" || ext == ".mov" || ext == ".jpg" || ext == ".jpeg" ||
                       ext == ".cr3" || ext == ".arw" || ext == ".dng" || ext == ".raw";
            })
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();

        if (flatFiles.Any())
        {
            conflicts.Add(new LayoutConflict
            {
                DateFolder = dateStr,
                CameraId = cameraId,
                ExistingFiles = flatFiles,
                CurrentLayout = "flat",
                RequiredLayout = "media_folders"
            });
        }

        return conflicts;
    }
}
