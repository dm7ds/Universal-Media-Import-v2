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
/// Service for reading and writing per-folder review sidecar files (.umi-review.json).
/// Each file stores tag and rating information for every reviewed photo in the folder.
/// </summary>
public interface IReviewSidecarService
{
    /// <summary>
    /// Reads the review sidecar for a folder. Returns null if no sidecar file exists.
    /// </summary>
    Task<ReviewSidecar?> ReadAsync(string folderPath, CancellationToken ct = default);

    /// <summary>
    /// Writes the complete review sidecar for a folder, replacing any existing file.
    /// </summary>
    Task SaveAsync(ReviewSidecar sidecar, CancellationToken ct = default);

    /// <summary>
    /// Reads the existing sidecar, updates (or creates) the entry for <paramref name="fileName"/>,
    /// and writes the result back. Removes the entry if tag is None and rating is 0.
    /// </summary>
    Task UpdatePhotoAsync(string folderPath, string fileName,
        ReviewTag? tag = null, int? rating = null, CancellationToken ct = default);
}
