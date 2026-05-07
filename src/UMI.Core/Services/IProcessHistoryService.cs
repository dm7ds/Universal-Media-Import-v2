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

using UMI.Core.Models;

namespace UMI.Core.Services;

/// <summary>
/// Service for reading and writing per-file process history entries.
/// Each pipeline step appends an entry to {filename}.history.json in .metadata/.
/// </summary>
public interface IProcessHistoryService
{
    /// <summary>
    /// Reads the process history for a video file. Returns null if no history exists.
    /// </summary>
    Task<ProcessHistory?> ReadAsync(string videoPath, CancellationToken ct = default);

    /// <summary>
    /// Appends a process history entry for a video file.
    /// Creates the history file and directory if they don't exist.
    /// </summary>
    Task WriteEntryAsync(string videoPath, string step,
        Dictionary<string, string>? details = null, CancellationToken ct = default);
}
