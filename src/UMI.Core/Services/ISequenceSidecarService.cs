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
/// Service for reading and writing per-folder sequence sidecar files (.umi-sequences.json).
/// Each file stores the burst-sequence evaluation results for the folder.
/// </summary>
public interface ISequenceSidecarService
{
    /// <summary>
    /// Reads the sequence sidecar for a folder. Returns null if no sidecar file exists.
    /// </summary>
    Task<SequenceSidecar?> ReadAsync(string folderPath, CancellationToken ct = default);

    /// <summary>
    /// Writes the complete sequence sidecar for a folder, replacing any existing file.
    /// </summary>
    Task SaveAsync(string folderPath, SequenceSidecar sidecar, CancellationToken ct = default);

    /// <summary>
    /// Deletes the sequence sidecar for a folder if it exists.
    /// </summary>
    Task DeleteAsync(string folderPath, CancellationToken ct = default);
}
