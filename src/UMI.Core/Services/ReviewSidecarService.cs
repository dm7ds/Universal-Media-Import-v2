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

using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMI.Core.Models;
using UMI.Core.Utilities;

namespace UMI.Core.Services;

/// <summary>
/// Implements <see cref="IReviewSidecarService"/> by reading/writing a
/// <c>.umi-review.json</c> file under {Workbench}/.umi/review/.
/// Uses <see cref="JsonDefaults"/> for all serialisation (SSOT).
/// </summary>
public class ReviewSidecarService : IReviewSidecarService
{
    private readonly GlobalPaths _globalPaths;
    private readonly ILogger<ReviewSidecarService>? _logger;

    public ReviewSidecarService(
        GlobalPaths globalPaths,
        ILogger<ReviewSidecarService>? logger = null)
    {
        _globalPaths = globalPaths;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ReviewSidecar?> ReadAsync(string folderPath, CancellationToken ct = default)
    {
        var sidecarPath = PathHelper.GetSidecarPath(_globalPaths.Workbench, folderPath, FolderNameConstants.UmiSubDir.Review, FolderNameConstants.ReviewSidecarFile);
        if (!File.Exists(sidecarPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(sidecarPath, ct);
            return JsonSerializer.Deserialize<ReviewSidecar>(json, JsonDefaults.ReadOptions);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read review sidecar: {Path}", sidecarPath);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(ReviewSidecar sidecar, CancellationToken ct = default)
    {
        var sidecarPath = PathHelper.GetSidecarPath(_globalPaths.Workbench, sidecar.FolderPath, FolderNameConstants.UmiSubDir.Review, FolderNameConstants.ReviewSidecarFile);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
            var json = JsonSerializer.Serialize(sidecar, JsonDefaults.WriteOptions);
            await File.WriteAllTextAsync(sidecarPath, json, ct);
            _logger?.LogDebug("Review sidecar written: {Path}", sidecarPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to write review sidecar: {Path}", sidecarPath);
        }
    }

    /// <inheritdoc/>
    public async Task UpdatePhotoAsync(string folderPath, string fileName,
        ReviewTag? tag = null, int? rating = null, CancellationToken ct = default)
    {
        var existing = await ReadAsync(folderPath, ct);

        var reviews = existing?.Reviews is not null
            ? new List<PhotoReview>(existing.Reviews)
            : new List<PhotoReview>();

        var index = reviews.FindIndex(r =>
            string.Equals(r.FileName, fileName, StringComparison.OrdinalIgnoreCase));

        var current = index >= 0 ? reviews[index] : new PhotoReview { FileName = fileName };

        var updatedTag    = tag    ?? current.Tag;
        var updatedRating = rating ?? current.Rating;

        if (updatedTag == ReviewTag.None && updatedRating == 0)
        {
            // Remove the entry to keep the sidecar clean.
            if (index >= 0)
                reviews.RemoveAt(index);
        }
        else
        {
            var updated = current with { Tag = updatedTag, Rating = updatedRating };
            if (index >= 0)
                reviews[index] = updated;
            else
                reviews.Add(updated);
        }

        var sidecar = new ReviewSidecar { FolderPath = folderPath, Reviews = reviews };
        await SaveAsync(sidecar, ct);

        _logger?.LogDebug("Review updated: {File} Tag={Tag} Rating={Rating}",
            fileName, updatedTag, updatedRating);
    }
}
