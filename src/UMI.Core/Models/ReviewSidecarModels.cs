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
/// Marks a photo as a favorite, trash, or untagged.
/// </summary>
public enum ReviewTag { None, Favorite, Trash }

/// <summary>
/// Review data for a single photo file: tag and star rating.
/// </summary>
public record PhotoReview
{
    public required string FileName { get; init; }
    public ReviewTag Tag { get; init; } = ReviewTag.None;

    /// <summary>0 = unrated, 1–5 = stars.</summary>
    public int Rating { get; init; }
}

/// <summary>
/// Aggregated review data for all photos in a folder.
/// Stored as .umi-review.json directly in the photo folder.
/// </summary>
public record ReviewSidecar
{
    public required string FolderPath { get; init; }
    public required List<PhotoReview> Reviews { get; init; }
}
