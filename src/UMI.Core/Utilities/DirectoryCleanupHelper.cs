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

namespace UMI.Core.Utilities;

/// <summary>
/// Utility für das Aufräumen leerer Verzeichnisse nach einem Import.
/// Wird von CLI (ProcessCommand) und MTP-Pfad (MtpImportService) gemeinsam genutzt — SSOT.
/// </summary>
public static class DirectoryCleanupHelper
{
    /// <summary>
    /// Löscht leere Unterverzeichnisse in <paramref name="sourceDirs"/> (nie <paramref name="rootPath"/> selbst).
    /// Versucht rekursiv Bottom-up zu löschen. Fehler werden still ignoriert (non-fatal).
    /// </summary>
    /// <param name="rootPath">Wurzelpfad der Workbench — wird NIEMALS gelöscht.</param>
    /// <param name="sourceDirs">Ordner die nach dem Verschieben geprüft werden sollen.</param>
    public static void CleanEmptyDirectories(string rootPath, IEnumerable<string> sourceDirs)
    {

        var dirsToCheck = sourceDirs
            .Where(d => !string.Equals(d, rootPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.Length)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var dir in dirsToCheck)
        {
            try
            {

                if (Directory.Exists(dir)
                    && !string.Equals(dir, rootPath, StringComparison.OrdinalIgnoreCase)
                    && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
            catch
            {

            }
        }
    }
}
