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

namespace UMI.Core.Configuration;

public class WorkflowConfig
{
    [JsonPropertyName("make_clean")]
    public bool MakeClean { get; set; }

    [JsonPropertyName("create_backup")]
    public bool CreateBackup { get; set; } = true;

    [JsonPropertyName("dry_run")]
    public bool DryRun { get; set; }

    [JsonPropertyName("ignore_folders")]
    public string[] IgnoreFolders { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Globale Run-Optionen (können über Profile gesetzt werden).
/// CLI-Flags überschreiben IMMER diese Werte.
/// </summary>
public class RunOptions
{
    [JsonPropertyName("gps")]
    public bool Gps { get; set; } = false;

    [JsonPropertyName("stabilize")]
    public bool Stabilize { get; set; } = false;

    [JsonPropertyName("dry_run")]
    public bool DryRun { get; set; } = false;

    [JsonPropertyName("force")]
    public bool Force { get; set; } = false;

    [JsonPropertyName("no_eis_sort")]
    public bool NoEisSort { get; set; } = false;
}
