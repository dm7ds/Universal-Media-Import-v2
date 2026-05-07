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

using System.Text.RegularExpressions;

namespace UMI.Core.Utilities;

/// <summary>
/// Helper für Metadata-Pfad-Berechnung unter {Workbench}/.umi/.
/// Unterstützt Layout-unabhängige Pfad-Berechnung und Legacy-Fallback.
/// </summary>
public static class PathHelper
{
    /// <summary>Regex that matches a yyyy-MM-dd date folder name (SSOT for all date-folder checks).</summary>
    public static readonly Regex DateFolderPattern = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    /// <summary>
    /// Berechnet den Pfad einer Metadaten-Datei unter {workbenchRoot}/.umi/{subDir}/{relativePath}/{basename}{extension}.
    /// Der relative Pfad wird aus <paramref name="mediaPath"/> relativ zu <paramref name="workbenchRoot"/> berechnet.
    /// </summary>
    /// <param name="workbenchRoot">Absoluter Pfad zum Workbench-Root (SSOT: config.GlobalPaths.Workbench).</param>
    /// <param name="mediaPath">Absoluter Pfad zur Media-Datei (z.B. E:\Workbench\2026-02-12\OA5\Video\file.mp4).</param>
    /// <param name="subDir">Ziel-Unterverzeichnis unter .umi/ (z.B. UmiSubDir.Metadata).</param>
    /// <param name="extension">Datei-Extension inkl. Punkt (z.B. ".meta.json" oder "_optimized.gpx").</param>
    /// <returns>Vollständiger Pfad (z.B. E:\Workbench\.umi\metadata\2026-02-12\OA5\Video\file.meta.json).</returns>
    public static string GetUmiPath(string workbenchRoot, string mediaPath, FolderNameConstants.UmiSubDir subDir, string extension)
    {
        var fullMedia = Path.GetFullPath(mediaPath);
        var fullWorkbench = Path.GetFullPath(workbenchRoot);

        var relative = Path.GetRelativePath(fullWorkbench, fullMedia);
        var relativeDir = Path.GetDirectoryName(relative);

        var subDirName = FolderNameConstants.GetSubDirName(subDir);

        var targetDir = (relativeDir != null && relativeDir != ".")
            ? Path.Combine(fullWorkbench, FolderNameConstants.UmiDir, subDirName, relativeDir)
            : Path.Combine(fullWorkbench, FolderNameConstants.UmiDir, subDirName);

        var baseName = Path.GetFileNameWithoutExtension(fullMedia);
        return Path.Combine(targetDir, baseName + extension);
    }

    /// <summary>
    /// Berechnet den Pfad einer Sidecar-Datei die für einen Ordner gilt (nicht pro Datei).
    /// Pfad: {workbenchRoot}/.umi/{subDir}/{relativeFolderPath}/{sidecarFileName}.
    /// </summary>
    /// <param name="workbenchRoot">Absoluter Pfad zum Workbench-Root (SSOT: config.GlobalPaths.Workbench).</param>
    /// <param name="folderPath">Absoluter Pfad zum Medien-Ordner.</param>
    /// <param name="subDir">Ziel-Unterverzeichnis unter .umi/ (z.B. UmiSubDir.Review).</param>
    /// <param name="sidecarFileName">Dateiname der Sidecar-Datei (z.B. ".umi-review.json").</param>
    /// <returns>Vollständiger Sidecar-Pfad (z.B. E:\Workbench\.umi\review\2026-02-12\OA5\Photo\.umi-review.json).</returns>
    public static string GetSidecarPath(string workbenchRoot, string folderPath, FolderNameConstants.UmiSubDir subDir, string sidecarFileName)
    {
        var fullFolder = Path.GetFullPath(folderPath);
        var fullWorkbench = Path.GetFullPath(workbenchRoot);

        var relativeFolder = Path.GetRelativePath(fullWorkbench, fullFolder);
        var subDirName = FolderNameConstants.GetSubDirName(subDir);

        var targetDir = (relativeFolder != ".")
            ? Path.Combine(fullWorkbench, FolderNameConstants.UmiDir, subDirName, relativeFolder)
            : Path.Combine(fullWorkbench, FolderNameConstants.UmiDir, subDirName);

        return Path.Combine(targetDir, sidecarFileName);
    }

    /// <summary>
    /// Berechnet Metadata-Pfad für eine Media-Datei im zentralen .metadata/ Ordner.
    /// Strategie: Finde Tag-Root (yyyy-MM-dd Pattern) → füge .metadata/ ein → spiegle Struktur.
    /// </summary>
    /// <param name="mediaPath">Absoluter Pfad zur Media-Datei (z.B. E:\Workbench\2026-02-12\OA5\Video\file.mp4)</param>
    /// <param name="extension">Metadata-Extension (z.B. .meta.json oder _optimized.gpx)</param>
    /// <returns>Metadata-Pfad (z.B. E:\Workbench\2026-02-12\.metadata\OA5\Video\file.meta.json) oder null</returns>
    [Obsolete("Use GetUmiPath() instead. Will be removed in a future version.")]
    public static string? GetMetadataPath(string? mediaPath, string extension)
    {
        if (string.IsNullOrWhiteSpace(mediaPath))
            return null;

        var fullPath = Path.GetFullPath(mediaPath);
        var directory = Path.GetDirectoryName(fullPath);

        if (directory == null)
            return null;

        var tagRoot = FindTagRoot(directory);

        if (tagRoot == null)
            return null;

        var relativePath = Path.GetRelativePath(tagRoot, fullPath);

        var relativeDir = Path.GetDirectoryName(relativePath);

        var metadataDir = relativeDir != null && relativeDir != "."
            ? Path.Combine(tagRoot, FolderNameConstants.MetadataDir, relativeDir)
            : Path.Combine(tagRoot, FolderNameConstants.MetadataDir);

        var fileName = Path.GetFileNameWithoutExtension(fullPath);
        return Path.Combine(metadataDir, fileName + extension);
    }

    /// <summary>
    /// Findet Tag-Root (Ordner mit yyyy-MM-dd Pattern) ausgehend von einem Pfad aufwärts.
    /// </summary>
    /// <param name="directory">Start-Directory</param>
    /// <returns>Tag-Root Pfad oder null wenn nicht gefunden</returns>
    public static string? FindTagRoot(string directory)
    {
        var current = new DirectoryInfo(directory);

        while (current != null)
        {

            if (DateFolderPattern.IsMatch(current.Name))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
