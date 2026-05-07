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
/// Optionen für Archivierung.
/// </summary>
public class ArchiveOptions
{
    public required string ProjectName { get; set; }
    public bool IncludeDelivery { get; set; }
    public bool DryRun { get; set; }
}

/// <summary>
/// Validierungs-Ergebnis für Archivierung.
/// </summary>
public class ArchiveValidation
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DirectoryInfo> DateFolders { get; set; } = new();
    public string ProjectPath { get; set; } = "";
}

/// <summary>
/// Ergebnis einer Archivierung.
/// </summary>
public class ArchiveResult
{
    public int FoldersCopied { get; set; }
    public int FilesCopied { get; set; }
    public long BytesCopied { get; set; }
    public bool DryRun { get; set; }
    public List<string> CreatedFolders { get; set; } = new();
}

/// <summary>
/// Progress-Info für Archivierung.
/// </summary>
public class ArchiveProgress
{
    public string CurrentFolder { get; set; } = "";
    public int Current { get; set; }
    public int Total { get; set; }
}
