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

namespace UMI.GUI.ViewModels;

/// <summary>
/// A single row in the Workbench Statistics table.
/// Represents one scanned video with its metadata overview.
/// </summary>
public class StatisticsRowViewModel
{
    /// <summary>Date extracted from the workbench path, e.g. "2026-03-05" or a dash.</summary>
    public required string Date { get; init; }

    /// <summary>Camera ID extracted from the workbench path, e.g. "OA5" or a dash.</summary>
    public required string Source { get; init; }

    /// <summary>The video filename, e.g. "DJI_20260305165134_0001_D.MP4".</summary>
    public required string FileName { get; init; }

    /// <summary>Human-readable pipeline status, e.g. "Ready", "Awaiting Gyroflow", etc.</summary>
    public required string Status { get; init; }

    /// <summary>EIS detection result: "ON", "OFF", or "N/A".</summary>
    public required string Eis { get; init; }

    /// <summary>GPS presence: "Yes", "Built", or "No".</summary>
    public required string Gps { get; init; }

    /// <summary>Metadata backup status: "OK", "Missing", or "N/A".</summary>
    public required string Backup { get; init; }

    /// <summary>File integrity status: "OK", "Size=0", or "\u2014" (em dash).</summary>
    public required string Integrity { get; init; }

    /// <summary>True when history-based status and folder-based status disagree,
    /// or when backup is missing, or when integrity has issues.</summary>
    public bool IsWarning { get; init; }
}
