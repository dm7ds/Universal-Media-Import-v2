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

using Microsoft.Extensions.Logging;

namespace UMI.Core.Services;

/// <summary>
/// Null-Object-Implementation von IGyroflowService.
/// Wird verwendet wenn Gyroflow nicht verfügbar ist.
/// </summary>
public class NullGyroflowService : IGyroflowService
{
    private readonly ILogger<NullGyroflowService>? _logger;

    public NullGyroflowService(ILogger<NullGyroflowService>? logger = null)
    {
        _logger = logger;
    }

    public Task<bool> StabilizeVideoAsync(
        string inputPath,
        string outputPath,
        string? presetPath = null,
        string gpuDevice = "nvidia",
        IProgress<GyroflowRenderProgress>? renderProgress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogWarning("Gyroflow nicht verfügbar - Stabilisierung übersprungen: {InputPath}", inputPath);
        return Task.FromResult(false);
    }

    public Task<BatchStabilizationResult> StabilizeBatchAsync(
        List<VideoStabilizationJob> jobs,
        IProgress<StabilizationProgress>? progress = null,
        IProgress<GyroflowRenderProgress>? renderProgress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogWarning("Gyroflow nicht verfügbar - Batch-Stabilisierung übersprungen ({Count} Videos)", jobs.Count);

        return Task.FromResult(new BatchStabilizationResult
        {
            TotalVideos = jobs.Count,
            SuccessfulVideos = 0,
            FailedVideos = jobs.Count
        });
    }
}
