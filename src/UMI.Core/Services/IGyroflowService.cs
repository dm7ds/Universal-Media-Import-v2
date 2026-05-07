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

namespace UMI.Core.Services;

/// <summary>
/// Service für Gyroflow-Stabilisierung.
/// </summary>
public interface IGyroflowService
{
    /// <summary>
    /// Stabilisiert ein einzelnes Video.
    /// </summary>
    Task<bool> StabilizeVideoAsync(
        string inputPath,
        string outputPath,
        string? presetPath = null,
        string gpuDevice = "nvidia",
        IProgress<GyroflowRenderProgress>? renderProgress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stabilisiert mehrere Videos parallel (Batch).
    /// </summary>
    Task<BatchStabilizationResult> StabilizeBatchAsync(
        List<VideoStabilizationJob> jobs,
        IProgress<StabilizationProgress>? progress = null,
        IProgress<GyroflowRenderProgress>? renderProgress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-Video Rendering-Fortschritt von Gyroflow --stdout-progress.
/// </summary>
public class GyroflowRenderProgress
{
    public int CurrentFrame { get; set; }
    public int TotalFrames { get; set; }
    public double Percent { get; set; }
    public string Eta { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
